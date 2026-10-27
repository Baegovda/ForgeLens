// SM2 secondary physics -> live Unreal setup (PhysicsAsset + post-process RigidBody AnimBP).
// Input: {model}.physics.json from the LibSaber exporter (cloths[].helperBones / maxDistanceMeters, jiggles[]).
// HEURISTIC (labelled): SM2 simulates cloth with an NvCloth-style particle solver (prop_cloth_sim); Unreal gets an
// approximation: every helper bone is a small sphere body, pinned particles (maxDistance 0) and the helper parent
// bone are kinematic, and every other helper is ball-jointed (swing/twist limited) to its nearest helper that is
// closer to the anchor. Cloth swing limits are DERIVED from maxDistanceMeters (atan(r/L)); radius, clamps and damping are not from the game.
#include "SM2PhysicsSetupLibrary.h"

#include "AnimGraphNode_LinkedInputPose.h"
#include "AnimGraphNode_RigidBody.h"
#include "AnimGraphNode_Root.h"
#include "AnimGraphNode_ComponentToLocalSpace.h"
#include "AnimGraphNode_LocalToComponentSpace.h"
#include "Animation/AnimBlueprint.h"
#include "Animation/AnimBlueprintGeneratedClass.h"
#include "AssetRegistry/AssetRegistryModule.h"
#include "AssetToolsModule.h"
#include "Dom/JsonObject.h"
#include "EdGraph/EdGraph.h"
#include "EdGraphSchema_K2.h"
#include "Engine/SkeletalMesh.h"
#include "Factories/AnimBlueprintFactory.h"
#include "Kismet2/KismetEditorUtilities.h"
#include "Misc/FileHelper.h"
#include "PhysicsEngine/BodySetup.h"
#include "PhysicsEngine/PhysicsAsset.h"
#include "PhysicsEngine/PhysicsConstraintTemplate.h"
#include "PhysicsEngine/SkeletalBodySetup.h"
#include "Serialization/JsonReader.h"
#include "Serialization/JsonSerializer.h"
#include "UObject/Package.h"
#include "ClothingAsset.h"
#include "ClothingAssetFactoryInterface.h"
#include "ClothingSystemEditorInterfaceModule.h"
#include "ClothConfigBase.h"
#include "PointWeightMap.h"
#include "Rendering/SkeletalMeshModel.h"
#include "Rendering/SkeletalMeshLODModel.h"
#include "ComponentReregisterContext.h"
#include "Components/SkeletalMeshComponent.h"
#include "ClothingSimulation.h"
#include "SkinnedAssetCompiler.h"

namespace
{
FString FreeName(const FString& Folder, const FString& Base)
{
  FString Name = Base;
  for (int32 N = 1; FPackageName::DoesPackageExist(Folder / Name) || FindObject<UObject>(nullptr, *((Folder / Name) + TEXT(".") + Name)); ++N)
    Name = FString::Printf(TEXT("%s_%d"), *Base, N);
  return Name;
}

TArray<FTransform> RefComponentSpace(const FReferenceSkeleton& Ref)
{
  const TArray<FTransform>& Local = Ref.GetRefBonePose();
  TArray<FTransform> Out;
  Out.SetNum(Local.Num());
  for (int32 I = 0; I < Local.Num(); ++I)
  {
    const int32 P = Ref.GetParentIndex(I);
    Out[I] = P == INDEX_NONE ? Local[I] : Local[I] * Out[P];
  }
  return Out;
}

USkeletalBodySetup* AddBody(UPhysicsAsset* PA, FName Bone, float Radius, bool bKinematic)
{
  const int32 Existing = PA->FindBodyIndex(Bone);
  if (Existing != INDEX_NONE)
    return PA->SkeletalBodySetups[Existing];
  USkeletalBodySetup* Body = NewObject<USkeletalBodySetup>(PA, NAME_None, RF_Transactional);
  Body->BoneName = Bone;
  Body->PhysicsType = bKinematic ? PhysType_Kinematic : PhysType_Simulated;
  Body->CollisionReponse = EBodyCollisionResponse::BodyCollision_Disabled; // helpers overlap; collide with nothing
  FKSphereElem Sphere(Radius);
  Body->AggGeom.SphereElems.Add(Sphere);
  Body->DefaultInstance.LinearDamping = 1.0f;
  Body->DefaultInstance.AngularDamping = 2.0f;
  PA->SkeletalBodySetups.Add(Body);
  PA->UpdateBodySetupIndexMap();
  return Body;
}

void AddConstraint(UPhysicsAsset* PA, FName Child, FName Anchor, const FTransform& ChildCS, const FTransform& AnchorCS, float SwingDeg)
{
  UPhysicsConstraintTemplate* C = NewObject<UPhysicsConstraintTemplate>(PA, NAME_None, RF_Transactional);
  FConstraintInstance& CI = C->DefaultInstance;
  CI.JointName = Child;
  CI.ConstraintBone1 = Child;
  CI.ConstraintBone2 = Anchor;
  // Joint sits at the anchor, so the child swings around it (pendulum).
  CI.SetRefFrame(EConstraintFrame::Frame1, AnchorCS.GetRelativeTransform(ChildCS));
  CI.SetRefFrame(EConstraintFrame::Frame2, FTransform::Identity);
  CI.SetLinearXMotion(LCM_Locked);
  CI.SetLinearYMotion(LCM_Locked);
  CI.SetLinearZMotion(LCM_Locked);
  CI.SetAngularSwing1Limit(ACM_Limited, SwingDeg);
  CI.SetAngularSwing2Limit(ACM_Limited, SwingDeg);
  CI.SetAngularTwistLimit(ACM_Limited, SwingDeg * 0.5f);
  CI.SetDisableCollision(true);
  C->SetDefaultProfile(CI);
  PA->ConstraintSetup.Add(C);
}

UEdGraphPin* PosePin(UEdGraphNode* Node, EEdGraphPinDirection Dir)
{
  for (UEdGraphPin* Pin : Node->Pins)
    if (Pin->Direction == Dir && Pin->PinType.PinCategory == UEdGraphSchema_K2::PC_Struct)
      if (const UScriptStruct* S = Cast<UScriptStruct>(Pin->PinType.PinSubCategoryObject.Get()))
        if (S->GetName().Contains(TEXT("PoseLink")))
          return Pin;
  return nullptr;
}

template <typename T>
T* NewGraphNode(UEdGraph* Graph, int32 X)
{
  FGraphNodeCreator<T> Creator(*Graph);
  T* Node = Creator.CreateNode();
  Node->NodePosX = X;
  Node->NodePosY = 0;
  Creator.Finalize();
  return Node;
}
}

bool USM2PhysicsSetupLibrary::SetupFromJson(USkeletalMesh* Mesh, const FString& JsonPath, const FString& DestFolder, FString& Report)
{
  return SetupFromJsonEx(Mesh, JsonPath, DestFolder, TArray<FString>(), Report);
}

bool USM2PhysicsSetupLibrary::SetupFromJsonEx(USkeletalMesh* Mesh, const FString& JsonPath, const FString& DestFolder, const TArray<FString>& SkipCloths, FString& Report)
{
  if (!Mesh)
  {
    Report = TEXT("no mesh");
    return false;
  }
  FString Text;
  TSharedPtr<FJsonObject> Root;
  if (!FFileHelper::LoadFileToString(Text, *JsonPath) || !FJsonSerializer::Deserialize(TJsonReaderFactory<>::Create(Text), Root) || !Root.IsValid())
  {
    Report = TEXT("cannot read ") + JsonPath;
    return false;
  }
  const FString Folder = DestFolder.IsEmpty() ? FPackageName::GetLongPackagePath(Mesh->GetPathName()) : DestFolder;
  const FReferenceSkeleton& Ref = Mesh->GetRefSkeleton();
  const TArray<FTransform> CS = RefComponentSpace(Ref);
  auto Pos = [&](FName B) { const int32 I = Ref.FindBoneIndex(B); return I == INDEX_NONE ? FTransform::Identity : CS[I]; };

  // Physics asset.
  const FString PAName = FreeName(Folder, TEXT("PA_") + Mesh->GetName() + TEXT("_SM2"));
  UPackage* PAPkg = CreatePackage(*(Folder / PAName));
  UPhysicsAsset* PA = NewObject<UPhysicsAsset>(PAPkg, *PAName, RF_Public | RF_Standalone | RF_Transactional);
  PA->SetPreviewMesh(Mesh);
  int32 Sim = 0, Kin = 0, Missing = 0;

  for (const TSharedPtr<FJsonValue>& ClothV : Root->GetArrayField(TEXT("cloths")))
  {
    const TSharedPtr<FJsonObject> Cloth = ClothV->AsObject();
    FString SimName;
    Cloth->TryGetStringField(TEXT("SimMeshName"), SimName);
    if (SkipCloths.ContainsByPredicate([&](const FString& N) { return N.Equals(SimName, ESearchCase::IgnoreCase); }))
      continue; // mtl1-9: this cloth runs as Chaos Cloth
    const TArray<TSharedPtr<FJsonValue>>* MaxDistV = nullptr;
    Cloth->TryGetArrayField(TEXT("maxDistanceMeters"), MaxDistV);
    struct FHelper { FName Bone; FName Parent; FTransform T; float Dist; bool bPinned; float MaxDistCm; };
    TArray<FHelper> Helpers;
    bool bAnyMulti = false; // mtl1-12: cloth has multi-particle helpers (robe-style chains)
    const TArray<TSharedPtr<FJsonValue>>& HB = Cloth->GetArrayField(TEXT("helperBones"));
    for (int32 I = 0; I < HB.Num(); ++I)
    {
      const TSharedPtr<FJsonObject> H = HB[I]->AsObject();
      FHelper X;
      X.Bone = FName(H->GetStringField(TEXT("bone")));
      X.Parent = FName(H->GetStringField(TEXT("parent")));
      if (Ref.FindBoneIndex(X.Bone) == INDEX_NONE || Ref.FindBoneIndex(X.Parent) == INDEX_NONE)
      {
        ++Missing;
        continue;
      }
      X.T = Pos(X.Bone);
      X.Dist = (X.T.GetLocation() - Pos(X.Parent).GetLocation()).Size();
      // One particle per helper: helper N = sim vertex N, pinned when its max distance is 0 (HEURISTIC index match).
      const int32 Particles = H->HasField(TEXT("particles")) ? (int32)H->GetNumberField(TEXT("particles")) : 1;
      // mtl1-8: the index match also applies to multi-particle helpers (particles=3 on robes/leg cloths); before, those had
      // no pins at all and 1024 bodies hung from one pin (tech_priest robe sagged ~1.4 m). HEURISTIC, same as above.
      bAnyMulti |= Particles > 1;
      X.bPinned = MaxDistV && MaxDistV->IsValidIndex(I) && (*MaxDistV)[I]->AsNumber() <= 0.0;
      X.MaxDistCm = MaxDistV && MaxDistV->IsValidIndex(I) ? (float)(*MaxDistV)[I]->AsNumber() * 100.0f : -1.0f;
      Helpers.Add(X);
    }
    if (Helpers.Num() == 0)
      continue;
    // Order by distance from the anchor; with no pinned particle the closest helper becomes the pin.
    Helpers.Sort([](const FHelper& A, const FHelper& B) { return A.Dist < B.Dist; });
    // mtl1-8: every skeleton parent of the cloth (LEG1L/LEG1R/LEG2L/LEG2R...) pins its closest helper, so each region
    // follows its own bone instead of hanging off another region's chain (HEURISTIC).
    {
      TSet<FName> Seen;
      for (FHelper& H : Helpers) // sorted by distance to own parent
        if (!Seen.Contains(H.Parent))
        {
          Seen.Add(H.Parent);
          H.bPinned = true;
        }
    }
    // Body radius: half the median nearest-neighbour spacing (unreal units), clamped.
    float Spacing = 2.0f;
    if (Helpers.Num() > 1)
    {
      TArray<float> NN;
      for (const FHelper& A : Helpers)
      {
        float Best = BIG_NUMBER;
        for (const FHelper& B : Helpers)
          if (&A != &B)
            Best = FMath::Min(Best, (A.T.GetLocation() - B.T.GetLocation()).Size());
        NN.Add(Best);
      }
      NN.Sort();
      Spacing = NN[NN.Num() / 2];
    }
    const float Radius = FMath::Clamp(Spacing * 0.5f, 0.5f, 10.0f);
    for (const FHelper& H : Helpers)
      AddBody(PA, H.Parent, Radius, true); // kinematic body on every skeleton parent (was only Helpers[0].Parent)
    // mtl1-11: chain depth cap. Measured: tech_priest skirt helpers formed chains of 3.3 cm links; with gravity the compounded
    // 18-30 deg swing per link let helper 0 drop 64 cm (rest z 49 -> -15); with gravity off it stayed at 45; a SLERP drive of
    // 1e5 changed nothing (RigidBody node). A helper deeper than MaxChainDepth hangs from its own kinematic skeleton parent
    // instead, with the swing derived from NvCloth maxDistance (atan(r/L)), the same motion-constraint bound as the source sim.
    // mtl1-12: only for cloths with multi-particle helpers; chaplain (all single-particle) got worse with the cap (6 -> 7.9 cm).
    const int32 MaxChainDepth = bAnyMulti ? 1 : MAX_int32; // HEURISTIC (measured: 3 -> tech_priest h0 sag 29 cm, 1 -> ~10 cm)
    TArray<int32> Depth;
    Depth.Init(0, Helpers.Num());
    for (int32 I = 0; I < Helpers.Num(); ++I)
    {
      const FHelper& H = Helpers[I];
      AddBody(PA, H.Bone, Radius, H.bPinned);
      H.bPinned ? ++Kin : ++Sim;
      FName Anchor = H.Parent;
      FTransform AnchorT = Pos(H.Parent);
      // mtl1-8: the own (kinematic) parent bone competes with earlier helpers, so chains stay short.
      float Best = H.Dist;
      int32 AnchorJ = INDEX_NONE;
      for (int32 J = 0; J < I; ++J)
      {
        if (Depth[J] >= MaxChainDepth)
          continue;
        const float D = (Helpers[J].T.GetLocation() - H.T.GetLocation()).Size();
        if (D < Best)
        {
          Best = D;
          Anchor = Helpers[J].Bone;
          AnchorT = Helpers[J].T;
          AnchorJ = J;
        }
      }
      Depth[I] = H.bPinned ? 0 : (AnchorJ == INDEX_NONE ? 1 : Depth[AnchorJ] + 1);
      // Swing limit DERIVED from NvCloth maxDistance (motion constraint radius r around the skinned position): a particle
      // pivoting at distance L from its anchor can deviate atan(r / L). Clamped 5..60 deg (clamp HEURISTIC); 25 deg when no data.
      const float Seg = FMath::Max(Best, 0.1f);
      const float Swing = H.MaxDistCm >= 0.0f ? FMath::Clamp(FMath::RadiansToDegrees(FMath::Atan(H.MaxDistCm / Seg)), 5.0f, 60.0f) : 25.0f;
      AddConstraint(PA, H.Bone, Anchor, H.T, AnchorT, Swing);
    }
  }

  // Jiggle bones (prop_phys_jiggle): simulated sphere ball-jointed to the skeleton parent (HEURISTIC limits).
  const TArray<TSharedPtr<FJsonValue>>* Jiggles = nullptr;
  if (Root->TryGetArrayField(TEXT("jiggles"), Jiggles))
    for (const TSharedPtr<FJsonValue>& JV : *Jiggles)
    {
      FString BoneName;
      if (!JV->AsObject()->TryGetStringField(TEXT("bone"), BoneName))
        continue;
      const int32 BI = Ref.FindBoneIndex(FName(BoneName));
      const int32 ParentIdx = BI == INDEX_NONE ? INDEX_NONE : Ref.GetParentIndex(BI);
      if (ParentIdx == INDEX_NONE)
      {
        ++Missing;
        continue;
      }
      const FName Parent = Ref.GetBoneName(ParentIdx);
      AddBody(PA, Parent, 1.0f, true);
      AddBody(PA, FName(BoneName), 2.0f, false);
      AddConstraint(PA, FName(BoneName), Parent, CS[BI], CS[ParentIdx], 15.0f);
      ++Sim;
    }

  PA->UpdateBodySetupIndexMap();
  PA->UpdateBoundsBodiesArray();
  PA->RefreshPhysicsAssetChange();
  FAssetRegistryModule::AssetCreated(PA);
  PA->MarkPackageDirty();

  // Post-process AnimBP: InputPose -> LocalToComponent -> RigidBody -> ComponentToLocal -> Output.
  const FString ABPName = FreeName(Folder, TEXT("ABP_") + Mesh->GetName() + TEXT("_SM2Phys"));
  UAnimBlueprintFactory* Factory = NewObject<UAnimBlueprintFactory>();
  Factory->TargetSkeleton = Mesh->GetSkeleton();
  Factory->ParentClass = UAnimInstance::StaticClass();
  Factory->PreviewSkeletalMesh = Mesh;
  IAssetTools& Tools = FModuleManager::LoadModuleChecked<FAssetToolsModule>("AssetTools").Get();
  UAnimBlueprint* ABP = Cast<UAnimBlueprint>(Tools.CreateAsset(ABPName, Folder, UAnimBlueprint::StaticClass(), Factory));
  if (!ABP)
  {
    Report = TEXT("AnimBP creation failed");
    return false;
  }
  UEdGraph* Graph = nullptr;
  for (UEdGraph* G : ABP->FunctionGraphs)
    if (G && G->GetFName() == UEdGraphSchema_K2::GN_AnimGraph)
      Graph = G;
  UAnimGraphNode_Root* Out = nullptr;
  if (Graph)
    for (UEdGraphNode* N : Graph->Nodes)
      if (UAnimGraphNode_Root* R = Cast<UAnimGraphNode_Root>(N))
        Out = R;
  if (!Graph || !Out)
  {
    Report = TEXT("AnimGraph/Output node not found");
    return false;
  }
  auto* In = NewGraphNode<UAnimGraphNode_LinkedInputPose>(Graph, -900);
  auto* L2C = NewGraphNode<UAnimGraphNode_LocalToComponentSpace>(Graph, -650);
  auto* RB = NewGraphNode<UAnimGraphNode_RigidBody>(Graph, -400);
  auto* C2L = NewGraphNode<UAnimGraphNode_ComponentToLocalSpace>(Graph, -150);
  RB->Node.OverridePhysicsAsset = PA;
  // mtl1-8: simulate in the CENTRE (pelvis) bone space; that bone's world motion is fed back clamped through SimSpaceSettings.
  // Root-motion clips move the bones, not the actor. In WorldSpace a 13 m/s dash left the cloth metres behind (sorcerer_exalted).
  // The limits are HEURISTIC: a run (~4.5 m/s) passes unclamped, and a dash/teleport is capped.
  const FName BaseBone = Ref.FindBoneIndex(TEXT("CENTRE")) != INDEX_NONE ? FName(TEXT("CENTRE")) : (Ref.GetNum() > 1 ? Ref.GetBoneName(1) : NAME_None);
  if (BaseBone != NAME_None)
  {
    RB->Node.SimulationSpace = ESimulationSpace::BaseBoneSpace;
    RB->Node.BaseBoneRef.BoneName = BaseBone;
  }
  else
    RB->Node.SimulationSpace = ESimulationSpace::ComponentSpace;
  // mtl1-9 (measured live, PhysTestMap): WorldAlpha/DampingAlpha 0 = the base bone's world motion is not fed back. With 1/0.5
  // the sorcerer_exalted dash (13 m/s) blew up (mean 234-611 cm, max 106 m); with 0/0 mean 41-62 cm, and calgar/chaplain/
  // commissar/titus stay within +-1.5 cm of the 1/0.5 numbers. HEURISTIC.
  RB->Node.SimSpaceSettings.WorldAlpha = 0.0f;
  RB->Node.SimSpaceSettings.DampingAlpha = 0.0f;
  RB->Node.SimSpaceSettings.MaxLinearVelocity = 600.0f;     // cm/s
  RB->Node.SimSpaceSettings.MaxLinearAcceleration = 2000.0f; // cm/s^2
  RB->Node.SimSpaceSettings.MaxAngularVelocity = 10.0f;      // rad/s
  RB->Node.SimSpaceSettings.MaxAngularAcceleration = 100.0f; // rad/s^2
  RB->Node.bEnableWorldGeometry = false;
  const bool bLinked = PosePin(In, EGPD_Output) && PosePin(L2C, EGPD_Input) && PosePin(RB, EGPD_Input) && PosePin(C2L, EGPD_Input) && PosePin(Out, EGPD_Input);
  if (bLinked)
  {
    PosePin(In, EGPD_Output)->MakeLinkTo(PosePin(L2C, EGPD_Input));
    PosePin(L2C, EGPD_Output)->MakeLinkTo(PosePin(RB, EGPD_Input));
    PosePin(RB, EGPD_Output)->MakeLinkTo(PosePin(C2L, EGPD_Input));
    PosePin(C2L, EGPD_Output)->MakeLinkTo(PosePin(Out, EGPD_Input));
  }
  FKismetEditorUtilities::CompileBlueprint(ABP);
  ABP->MarkPackageDirty();

  Mesh->Modify();
  Mesh->SetPhysicsAsset(PA);
  Mesh->SetPostProcessAnimBlueprint(TSubclassOf<UAnimInstance>(Cast<UClass>(ABP->GeneratedClass)));
  Mesh->MarkPackageDirty();

  Report = FString::Printf(TEXT("PA=%s ABP=%s simulated=%d kinematic=%d constraints=%d missingBones=%d linked=%d compiled=%d"),
    *PA->GetPathName(), *ABP->GetPathName(), Sim, Kin, PA->ConstraintSetup.Num(), Missing, bLinked ? 1 : 0,
    ABP->Status != BS_Error ? 1 : 0);
  return bLinked && ABP->Status != BS_Error;
}

namespace
{
UAnimGraphNode_RigidBody* FindRigidBodyNode(UAnimBlueprint* ABP)
{
  if (!ABP)
    return nullptr;
  for (UEdGraph* G : ABP->FunctionGraphs)
    if (G)
      for (UEdGraphNode* N : G->Nodes)
        if (UAnimGraphNode_RigidBody* RB = Cast<UAnimGraphNode_RigidBody>(N))
          return RB;
  return nullptr;
}

// Reflection setter so the plugin needs no link dependency on ChaosCloth (property names from UE 5.7 ChaosClothConfig.h).

// mtl1-13: cloth collision from the sidecar (ue.chaosCloth.collision). Offsets are bone-local cm; exports without
// collision.space="ueMesh" are X-mirrored against the imported mesh (measured mtl1-11 for points, derived for bone-local).
// mtl1-14d GENERIC RULE (replaces the above for bone-local offsets): imported bone frames already absorb the X reflection
// (same finding as the Manny retarget), so bone-local offsets must be the RAW SM2 values. Raw = files without space or
// space="boneLocal"; files written with space="ueMesh" (mtl1-12..14 extractor) carry X-negated offsets and are un-negated.
// Then every capsule/sphere is shrunk so no FREE rest-pose particle of that cloth starts inside it (start-inside particles
// are what blew the sorcerer cloak into a disc / spike ball).
USkeletalBodySetup* CollBody(UPhysicsAsset* PA, FName Bone)
{
  const int32 Existing = PA->FindBodyIndex(Bone);
  if (Existing != INDEX_NONE)
    return PA->SkeletalBodySetups[Existing];
  USkeletalBodySetup* Body = NewObject<USkeletalBodySetup>(PA, NAME_None, RF_Transactional);
  Body->BoneName = Bone;
  Body->PhysicsType = PhysType_Kinematic;
  PA->SkeletalBodySetups.Add(Body);
  PA->UpdateBodySetupIndexMap();
  return Body;
}

FVector JVec(const TSharedPtr<FJsonObject>& O, const TCHAR* F, bool bMirror)
{
  const TArray<TSharedPtr<FJsonValue>>* A = nullptr;
  if (!O.IsValid() || !O->TryGetArrayField(F, A) || A->Num() < 3)
    return FVector::ZeroVector;
  const FVector V((*A)[0]->AsNumber(), (*A)[1]->AsNumber(), (*A)[2]->AsNumber());
  return bMirror ? FVector(-V.X, V.Y, V.Z) : V;
}

int32 AddCollision(UPhysicsAsset* PA, const TSharedPtr<FJsonObject>& Coll, const FReferenceSkeleton& RS, const TArray<FTransform>& CS, TArray<FString>& Log, const FString& Who,
  const TArray<FVector3f>* RestPV = nullptr, const TArray<float>* RestMaxDist = nullptr, float ThickCm = 2.0f)
{
  int32 Fitted = 0;
  float MinScale = 1.0f;
  // Largest radius scale s (<=1) so that every free rest particle P is outside the swept shape: dist(P) >= s*r(t) + ThickCm.
  auto FitScale = [&](const FVector& A, const FVector& B, float RA, float RB) {
    float S = 1.0f;
    if (!RestPV) return S;
    const FVector AB = B - A; const double L2 = AB.SizeSquared();
    for (int32 I = 0; I < RestPV->Num(); ++I)
    {
      if (RestMaxDist && RestMaxDist->IsValidIndex(I) && (*RestMaxDist)[I] <= 0.1f) continue; // pinned: follows skin
      const FVector P((*RestPV)[I]);
      const double T = L2 > 1e-6 ? FMath::Clamp(FVector::DotProduct(P - A, AB) / L2, 0.0, 1.0) : 0.0;
      const float R = FMath::Lerp(RA, RB, (float)T);
      const float D = (float)FVector::Dist(P, A + AB * T);
      if (R > KINDA_SMALL_NUMBER && D < R + ThickCm)
        S = FMath::Min(S, FMath::Max(0.0f, D - ThickCm) / R);
    }
    return FMath::Max(S, 0.25f); // HEURISTIC floor: keep a core so limbs still collide
  };
  if (!Coll.IsValid())
    return 0;
  FString Space;
  Coll->TryGetStringField(TEXT("space"), Space);
  const bool bMirror = Space == TEXT("ueMesh"); // un-negate extractor-mirrored offsets -> raw bone-local
  int32 N = 0, Missing = 0;
  auto BoneCS = [&](const FString& B, FTransform& Out) {
    const int32 I = RS.FindBoneIndex(FName(*B));
    if (I == INDEX_NONE) { ++Missing; return false; }
    Out = CS[I];
    return true;
  };
  const TArray<TSharedPtr<FJsonValue>>* Arr = nullptr;
  if (Coll->TryGetArrayField(TEXT("capsules"), Arr))
    for (const TSharedPtr<FJsonValue>& V : *Arr)
    {
      const TSharedPtr<FJsonObject> K = V->AsObject();
      const FString B1 = K->GetStringField(TEXT("BoneFirst")), B2 = K->GetStringField(TEXT("BoneSecond"));
      FTransform T1, T2;
      if (!BoneCS(B1, T1)) continue;
      const FVector P1 = JVec(K, TEXT("offsetFirstCm"), bMirror);
      FVector P2 = P1;
      if (!B2.IsEmpty() && BoneCS(B2, T2))
        P2 = T1.InverseTransformPosition(T2.TransformPosition(JVec(K, TEXT("offsetSecondCm"), bMirror)));
      float R1 = (float)K->GetNumberField(TEXT("radiusFirstCm")), R2 = (float)K->GetNumberField(TEXT("radiusSecondCm"));
      {
        const float S = FitScale(T1.TransformPosition(P1), T1.TransformPosition(P2), R1, R2);
        if (S < 0.999f) { ++Fitted; MinScale = FMath::Min(MinScale, S); R1 *= S; R2 *= S; }
      }
      const FVector Axis = P2 - P1;
      const float Len = Axis.Size();
      USkeletalBodySetup* Body = CollBody(PA, FName(*B1));
      const FQuat Rot = Len > KINDA_SMALL_NUMBER ? FQuat::FindBetweenNormals(FVector::UpVector, Axis / Len) : FQuat::Identity;
      if (Len <= KINDA_SMALL_NUMBER)
      {
        FKSphereElem E(FMath::Max(R1, R2));
        E.Center = P1;
        Body->AggGeom.SphereElems.Add(E);
      }
      else if (FMath::IsNearlyEqual(R1, R2, 0.05f))
      {
        FKSphylElem E(R1, Len);
        E.Center = (P1 + P2) * 0.5;
        E.Rotation = Rot.Rotator();
        Body->AggGeom.SphylElems.Add(E);
      }
      else
      {
        FKTaperedCapsuleElem E(R1, R2, Len); // Radius0 at -Z (first), Radius1 at +Z (second)
        E.Center = (P1 + P2) * 0.5;
        E.Rotation = Rot.Rotator();
        Body->AggGeom.TaperedCapsuleElems.Add(E);
      }
      ++N;
    }
  if (Coll->TryGetArrayField(TEXT("spheres"), Arr))
    for (const TSharedPtr<FJsonValue>& V : *Arr)
    {
      const TSharedPtr<FJsonObject> K = V->AsObject();
      FTransform T;
      if (!BoneCS(K->GetStringField(TEXT("Bone")), T)) continue;
      float Rs = (float)K->GetNumberField(TEXT("radiusCm"));
      const FVector C = JVec(K, TEXT("offsetCm"), bMirror);
      { const float S = FitScale(T.TransformPosition(C), T.TransformPosition(C), Rs, Rs); if (S < 0.999f) { ++Fitted; MinScale = FMath::Min(MinScale, S); Rs *= S; } }
      FKSphereElem E(Rs);
      E.Center = C;
      CollBody(PA, FName(*K->GetStringField(TEXT("Bone"))))->AggGeom.SphereElems.Add(E);
      ++N;
    }
  if (Coll->TryGetArrayField(TEXT("boxes"), Arr))
    for (const TSharedPtr<FJsonValue>& V : *Arr)
    {
      const TSharedPtr<FJsonObject> K = V->AsObject();
      FTransform T;
      if (!BoneCS(K->GetStringField(TEXT("Bone")), T)) continue;
      const FVector Sz = JVec(K, TEXT("sizeCm"), false);
      FKBoxElem E(Sz.X, Sz.Y, Sz.Z);
      E.Center = JVec(K, TEXT("offsetCm"), bMirror);
      CollBody(PA, FName(*K->GetStringField(TEXT("Bone"))))->AggGeom.BoxElems.Add(E);
      ++N;
    }
  Log.Add(FString::Printf(TEXT("%s: collision shapes=%d missingBones=%d unnegated=%d restFitted=%d minRadiusScale=%.2f"), *Who, N, Missing, bMirror ? 1 : 0, Fitted, MinScale));
  return N;
}

bool SetProp(UObject* Obj, const TCHAR* Name, const FString& Value, TArray<FString>& Log)
{
  FProperty* P = Obj ? Obj->GetClass()->FindPropertyByName(FName(Name)) : nullptr;
  if (!P)
  {
    Log.Add(FString::Printf(TEXT("%s:missing"), Name));
    return false;
  }
  const TCHAR* Ok = P->ImportText_Direct(*Value, P->ContainerPtrToValuePtr<void>(Obj), Obj, PPF_None);
  if (!Ok)
    Log.Add(FString::Printf(TEXT("%s:bad(%s)"), Name, *Value));
  return Ok != nullptr;
}

double Num(const TSharedPtr<FJsonObject>& O, const TCHAR* K, double Def)
{
  double V = Def;
  return O.IsValid() && O->TryGetNumberField(K, V) ? V : Def;
}
}

bool USM2PhysicsSetupLibrary::TuneRigidBody(UAnimBlueprint* ABP, int32 SimSpace, FName BaseBone, float WorldAlpha, float DampingAlpha, float MaxLinearVelocity,
  float MaxLinearAcceleration, float MaxAngularVelocity, float MaxAngularAcceleration, FString& Report)
{
  UAnimGraphNode_RigidBody* RB = FindRigidBodyNode(ABP);
  if (!RB)
  {
    Report = TEXT("no RigidBody node");
    return false;
  }
  RB->Modify();
  RB->Node.SimulationSpace = SimSpace == 1 ? ESimulationSpace::WorldSpace : SimSpace == 2 ? ESimulationSpace::BaseBoneSpace : ESimulationSpace::ComponentSpace;
  if (SimSpace == 2)
    RB->Node.BaseBoneRef.BoneName = BaseBone;
  RB->Node.SimSpaceSettings.WorldAlpha = WorldAlpha;
  RB->Node.SimSpaceSettings.DampingAlpha = DampingAlpha;
  RB->Node.SimSpaceSettings.MaxLinearVelocity = MaxLinearVelocity;
  RB->Node.SimSpaceSettings.MaxLinearAcceleration = MaxLinearAcceleration;
  RB->Node.SimSpaceSettings.MaxAngularVelocity = MaxAngularVelocity;
  RB->Node.SimSpaceSettings.MaxAngularAcceleration = MaxAngularAcceleration;
  FKismetEditorUtilities::CompileBlueprint(ABP);
  ABP->MarkPackageDirty();
  Report = FString::Printf(TEXT("space=%d base=%s world=%.2f damp=%.2f vel=%.0f acc=%.0f angvel=%.1f angacc=%.1f compiled=%d"), SimSpace, *BaseBone.ToString(),
    WorldAlpha, DampingAlpha, MaxLinearVelocity, MaxLinearAcceleration, MaxAngularVelocity, MaxAngularAcceleration, ABP->Status != BS_Error ? 1 : 0);
  return ABP->Status != BS_Error;
}

// mtl1-15c: skeletal mesh builds run async in UE5; editing the LODModel (cloth bind) while a build from an earlier
// PostEditChange (PA/ABP assignment, previous cloth) is still in flight produced spiky garbage render data on the
// sorcerer (16-bit bone index, 2+ clothing assets). Always drain the mesh's async build before and after each edit.
static void SM2WaitMeshBuild(USkeletalMesh* Mesh)
{
  if (Mesh)
  {
    USkinnedAsset* A = Mesh;
    FSkinnedAssetCompilingManager::Get().FinishCompilation(MakeArrayView(&A, 1));
  }
}

bool USM2PhysicsSetupLibrary::CreateChaosCloth(USkeletalMesh* Mesh, const FString& JsonPath, TArray<FString>& Converted, FString& Report)
{
  SM2WaitMeshBuild(Mesh);
  Converted.Reset();
  FString Text;
  TSharedPtr<FJsonObject> Root;
  if (!Mesh || !FFileHelper::LoadFileToString(Text, *JsonPath) || !FJsonSerializer::Deserialize(TJsonReaderFactory<>::Create(Text), Root) || !Root.IsValid())
  {
    Report = TEXT("no mesh or cannot read ") + JsonPath;
    return false;
  }
  FSkeletalMeshModel* Model = Mesh->GetImportedModel();
  if (!Model || !Model->LODModels.IsValidIndex(0))
  {
    Report = TEXT("no LOD0");
    return false;
  }
  UClothingAssetFactoryBase* Factory = FModuleManager::LoadModuleChecked<FClothingSystemEditorInterfaceModule>("ClothingSystemEditorInterface").GetClothingAssetFactory();
  UClass* ChaosConfigClass = FindObject<UClass>(nullptr, TEXT("/Script/ChaosCloth.ChaosClothConfig"));
  TArray<FString> Log;
  Mesh->Modify();
  const TArray<FTransform> RefCS = RefComponentSpace(Mesh->GetRefSkeleton());
  UPhysicsAsset* CollPA = nullptr;
  {
    const FString Folder = FPackageName::GetLongPackagePath(Mesh->GetOutermost()->GetName()) / TEXT("ChaosCloth");
    const FString Name = FreeName(Folder, TEXT("PA_") + Mesh->GetName() + TEXT("_ClothCollision"));
    UPackage* Pkg = CreatePackage(*(Folder / Name));
    CollPA = NewObject<UPhysicsAsset>(Pkg, *Name, RF_Public | RF_Standalone | RF_Transactional);
    CollPA->PreviewSkeletalMesh = Mesh;
    FAssetRegistryModule::AssetCreated(CollPA);
    CollPA->MarkPackageDirty();
  }

  // mtl1-14e GENERIC: binds made for earlier cloths can be lost when later cloths edit the mesh (the first clothing asset ended up
  // with no bound section -> not simulated, no ChaosClothingInteractor). Remember each asset's render slots and re-verify after the loop.
  struct FBindRec { UClothingAssetCommon* Asset; TArray<FName> Slots; FString Name; };
  TArray<FBindRec> BindRecs;
  for (const TSharedPtr<FJsonValue>& ClothV : Root->GetArrayField(TEXT("cloths")))
  {
    const TSharedPtr<FJsonObject> Cloth = ClothV->AsObject();
    FString SimName;
    Cloth->TryGetStringField(TEXT("SimMeshName"), SimName);
    const TArray<TSharedPtr<FJsonValue>>* PosV = nullptr;
    const TArray<TSharedPtr<FJsonValue>>* MaxV = nullptr;
    if (!Cloth->TryGetArrayField(TEXT("simPositionsUeCm"), PosV) || !Cloth->TryGetArrayField(TEXT("maxDistanceMeters"), MaxV) || PosV->Num() < 9)
    {
      Log.Add(SimName + TEXT(": no simPositionsUeCm, stays bone-based"));
      continue;
    }
    TArray<FVector3f> SimPos;
    for (int32 I = 0; I + 2 < PosV->Num(); I += 3)
      SimPos.Add(FVector3f((*PosV)[I]->AsNumber(), (*PosV)[I + 1]->AsNumber(), (*PosV)[I + 2]->AsNumber()));
    // mtl1-11: exports before simPositionsSpace="ueMesh" are X-mirrored against the imported mesh. Detect on the slot-named
    // section by centroid (mirror only if it fits clearly better) so old and new exports both work.
    bool bMirrored = false;
    {
      FString Space;
      Cloth->TryGetStringField(TEXT("simPositionsSpace"), Space);
      const FSkeletalMeshLODModel& L0 = Model->LODModels[0];
      for (const FSkelMeshSection& Sec : L0.Sections)
      {
        if (Space == TEXT("ueMesh") || Sec.SoftVertices.Num() == 0 || !Mesh->GetMaterials().IsValidIndex(Sec.MaterialIndex) ||
          !Mesh->GetMaterials()[Sec.MaterialIndex].MaterialSlotName.ToString().Equals(SimName, ESearchCase::IgnoreCase))
          continue;
        FVector3f SC(0), PC(0);
        for (const FSoftSkinVertex& V : Sec.SoftVertices) SC += V.Position;
        for (const FVector3f& P : SimPos) PC += P;
        SC /= Sec.SoftVertices.Num(); PC /= SimPos.Num();
        const FVector3f PM(-PC.X, PC.Y, PC.Z);
        if (FVector3f::Dist(SC, PM) + 0.5f < FVector3f::Dist(SC, PC))
        {
          for (FVector3f& P : SimPos) P.X = -P.X;
          bMirrored = true;
        }
        break;
      }
    }
    if (bMirrored)
      Log.Add(SimName + TEXT(": simPositions X-mirrored to match the imported mesh (old export)"));
    auto Nearest = [&](const FVector3f& P, float& OutD) {
      int32 Best = INDEX_NONE;
      float BD = TNumericLimits<float>::Max();
      for (int32 I = 0; I < SimPos.Num(); ++I)
      {
        const float D = FVector3f::DistSquared(P, SimPos[I]);
        if (D < BD)
        {
          BD = D;
          Best = I;
        }
      }
      OutD = FMath::Sqrt(BD);
      return Best;
    };

    // 1. the _SIM section: every vertex sits on a sim particle (0.5 cm) and the vertex count is close.
    FSkeletalMeshLODModel& Lod = Model->LODModels[0];
    int32 SimSection = INDEX_NONE;
    // 1a. by material slot name (the exporter names the sim mesh's slot after the cloth, e.g. cloak_SIM).
    float SimHit = -1.f;
    for (int32 S = 0; S < Lod.Sections.Num() && SimSection == INDEX_NONE; ++S)
    {
      const FSkelMeshSection& Sec = Lod.Sections[S];
      if (Sec.bDisabled || Sec.HasClothingData() || !Mesh->GetMaterials().IsValidIndex(Sec.MaterialIndex))
        continue;
      if (!Mesh->GetMaterials()[Sec.MaterialIndex].MaterialSlotName.ToString().Equals(SimName, ESearchCase::IgnoreCase))
        continue;
      int32 Hit = 0, HitM = 0;
      for (const FSoftSkinVertex& V : Sec.SoftVertices)
      {
        float D;
        Nearest(V.Position, D);
        Hit += D < 2.0f;
        Nearest(FVector3f(-V.Position.X, V.Position.Y, V.Position.Z), D);
        HitM += D < 2.0f;
      }
      if (HitM > Hit && !bMirrored) // old export, X-mirrored: decide by vertex hits, not centroid
      {
        for (FVector3f& P : SimPos) P.X = -P.X;
        bMirrored = true;
        Hit = HitM;
        Log.Add(SimName + TEXT(": simPositions X-mirrored by vertex hits"));
      }
      SimHit = Sec.SoftVertices.Num() ? float(Hit) / Sec.SoftVertices.Num() : 0.f;
      {
        FBox3f SB(ForceInit), PB(ForceInit);
        for (const FSoftSkinVertex& V : Sec.SoftVertices) SB += V.Position;
        for (const FVector3f& P : SimPos) PB += P;
        const FVector3f DC = SB.GetCenter() - PB.GetCenter();
        Log.Add(FString::Printf(TEXT("%s DIAG secVerts=%d simParticles=%d secCenter=(%.1f %.1f %.1f) simCenter=(%.1f %.1f %.1f) secExt=(%.1f %.1f %.1f) simExt=(%.1f %.1f %.1f) hit=%.2f"),
          *SimName, Sec.SoftVertices.Num(), SimPos.Num(), SB.GetCenter().X, SB.GetCenter().Y, SB.GetCenter().Z, PB.GetCenter().X, PB.GetCenter().Y, PB.GetCenter().Z,
          SB.GetExtent().X, SB.GetExtent().Y, SB.GetExtent().Z, PB.GetExtent().X, PB.GetExtent().Y, PB.GetExtent().Z, Sec.SoftVertices.Num() ? float(Hit) / Sec.SoftVertices.Num() : 0.f));
        (void)DC;
      }
      if (SimHit >= 0.8f) // HEURISTIC
        SimSection = S;
    }
    for (int32 S = 0; S < Lod.Sections.Num() && SimSection == INDEX_NONE; ++S)
    {
      const FSkelMeshSection& Sec = Lod.Sections[S];
      if (Sec.bDisabled || Sec.HasClothingData() || Sec.SoftVertices.Num() < SimPos.Num() / 2 || Sec.SoftVertices.Num() > SimPos.Num() * 3)
        continue;
      int32 Hit = 0;
      for (const FSoftSkinVertex& V : Sec.SoftVertices)
      {
        float D;
        Nearest(V.Position, D);
        Hit += D < 0.5f;
      }
      if (Hit >= Sec.SoftVertices.Num() * 0.95f)
        SimSection = S;
    }
    // 2. render sections: material slot name listed in RenderMeshes (case-insensitive, either containing the other).
    TArray<FString> RenderNames;
    const TArray<TSharedPtr<FJsonValue>>* RV = nullptr;
    if (Cloth->TryGetArrayField(TEXT("RenderMeshes"), RV))
      for (const TSharedPtr<FJsonValue>& V : *RV)
        RenderNames.Add(V->AsString());
    // mtl1-15: exact (case-insensitive) slot match first; Contains only when no exact hit (beads_REND vs beads_REND1 etc).
    TArray<int32> RenderSections;
    TArray<FName> RenderSlots;
    for (int32 Pass = 0; Pass < 2 && RenderSections.Num() == 0; ++Pass)
      for (int32 S = 0; S < Lod.Sections.Num(); ++S)
      {
        const FSkelMeshSection& Sec = Lod.Sections[S];
        if (S == SimSection || Sec.bDisabled || Sec.HasClothingData() || !Mesh->GetMaterials().IsValidIndex(Sec.MaterialIndex))
          continue;
        const FName SlotN = Mesh->GetMaterials()[Sec.MaterialIndex].MaterialSlotName;
        const FString Slot = SlotN.ToString();
        for (const FString& R : RenderNames)
          if (!R.IsEmpty() && (Slot.Equals(R, ESearchCase::IgnoreCase) ||
            (Pass == 1 && (Slot.Contains(R, ESearchCase::IgnoreCase) || R.Contains(Slot, ESearchCase::IgnoreCase)))))
          {
            RenderSections.AddUnique(S);
            RenderSlots.AddUnique(SlotN);
            break;
          }
      }
    if (SimSection == INDEX_NONE || RenderSections.Num() == 0)
    {
      Log.Add(FString::Printf(TEXT("%s: sim section %d (slot hit %.2f), %d render sections -> stays bone-based"), *SimName, SimSection, SimHit, RenderSections.Num()));
      continue;
    }

    // 3. clothing asset from the sim section (the editor's Create Clothing Data from Section, section removed = disabled).
    UClothingAssetCommon* Asset = nullptr;
    {
      FScopedSkeletalMeshPostEditChange Outer(Mesh);
      {
        FScopedSkeletalMeshPostEditChange Inner(Mesh);
        Mesh->RemoveMeshSection(0, SimSection);
      }
      FSkeletalMeshClothBuildParams Params;
      Params.AssetName = TEXT("SM2Cloth_") + SimName;
      Params.LodIndex = 0;
      Params.SourceSection = SimSection;
      Params.bRemoveFromMesh = false; // already disabled above
      Asset = Cast<UClothingAssetCommon>(Factory->CreateFromSkeletalMesh(Mesh, Params));
      if (Asset)
        Mesh->AddClothingAsset(Asset);
    }
    SM2WaitMeshBuild(Mesh);
    if (!Asset || Asset->LodData.Num() == 0)
    {
      Log.Add(SimName + TEXT(": factory failed"));
      continue;
    }

    // 4. MaxDistance from the game (maxDistanceMeters x100 per sim particle, nearest particle; 0 = pinned / kinematic).
    FClothLODDataCommon& CLod = Asset->LodData[0];
    const TArray<FVector3f>& PV = CLod.PhysicalMeshData.Vertices;
    FPointWeightMap& Mask = CLod.PointWeightMaps.AddDefaulted_GetRef();
    Mask.Initialize(PV.Num());
    Mask.Name = TEXT("SM2_MaxDistance");
    Mask.CurrentTarget = (uint8)EWeightMapTargetCommon::MaxDistance;
    Mask.bEnabled = true;
    int32 Pinned = 0;
    float WorstMatch = 0.0f;
    for (int32 I = 0; I < PV.Num(); ++I)
    {
      float D;
      const int32 N = Nearest(PV[I], D);
      WorstMatch = FMath::Max(WorstMatch, D);
      const float Cm = MaxV->IsValidIndex(N) ? (float)(*MaxV)[N]->AsNumber() * 100.0f : 0.0f;
      Mask.Values[I] = Cm;
      Pinned += Cm <= 0.0f;
    }
    Asset->ApplyParameterMasks(true);

    // 5. ChaosClothConfig from ue.chaosCloth (copied; see PhysicsSidecar note). Velocity clamps are HEURISTIC (root-motion dashes).
    UObject* Config = nullptr;
    for (const auto& KV : Asset->ClothConfigs)
      if (KV.Value && ChaosConfigClass && KV.Value->IsA(ChaosConfigClass))
        Config = KV.Value;
    if (!Config && ChaosConfigClass)
    {
      Config = NewObject<UClothConfigBase>(Asset, ChaosConfigClass, NAME_None, RF_Transactional);
      Asset->ClothConfigs.Add(ChaosConfigClass->GetFName(), Cast<UClothConfigBase>(Config));
    }
    const TSharedPtr<FJsonObject>* UeO = nullptr;
    TSharedPtr<FJsonObject> CC;
    if (Cloth->TryGetObjectField(TEXT("ue"), UeO))
    {
      const TSharedPtr<FJsonObject>* C = nullptr;
      if ((*UeO)->TryGetObjectField(TEXT("chaosCloth"), C))
        CC = *C;
    }
    if (Config)
    {
      auto W = [](double V) { return FString::Printf(TEXT("(Low=%f,High=%f)"), V, V); };
      SetProp(Config, TEXT("EdgeStiffnessWeighted"), W(Num(CC, TEXT("edgeStiffness"), 1)), Log);
      SetProp(Config, TEXT("BendingStiffnessWeighted"), W(Num(CC, TEXT("bendingStiffness"), 1)), Log);
      SetProp(Config, TEXT("AreaStiffnessWeighted"), W(Num(CC, TEXT("areaStiffness"), 1)), Log);
      SetProp(Config, TEXT("TetherStiffness"), W(FMath::Max(0.5, Num(CC, TEXT("tetherStiffness"), 1))), Log);
      SetProp(Config, TEXT("TetherScale"), W(FMath::Clamp(Num(CC, TEXT("tetherScale"), 1), 1.0, 1.2)), Log);
      SetProp(Config, TEXT("DampingCoefficient"), FString::SanitizeFloat(FMath::Clamp(Num(CC, TEXT("dampingCoefficient"), 0.01), 0.0, 1.0)), Log);
      // mtl1-13b: thicker collision + CCD against fast limbs (HEURISTIC; measured penetration 2-6% at 1 cm).
      SetProp(Config, TEXT("CollisionThickness"), FString::SanitizeFloat(FMath::Max(2.0, Num(CC, TEXT("collisionThicknessCm"), 1))), Log);
      SetProp(Config, TEXT("bUseCCD"), TEXT("True"), Log);
      SetProp(Config, TEXT("FrictionCoefficient"), FString::SanitizeFloat(Num(CC, TEXT("frictionCoefficient"), 0.8)), Log);
      const double LV = FMath::Clamp(Num(CC, TEXT("linearVelocityScale"), 0.75), 0.0, 1.0);
      SetProp(Config, TEXT("LinearVelocityScale"), FString::Printf(TEXT("(X=%f,Y=%f,Z=%f)"), LV, LV, LV), Log);
      SetProp(Config, TEXT("AngularVelocityScale"), FString::SanitizeFloat(FMath::Clamp(Num(CC, TEXT("angularVelocityScale"), 0.75), 0.0, 1.0)), Log);
      SetProp(Config, TEXT("bEnableLinearVelocityClamping"), TEXT("True"), Log);
      SetProp(Config, TEXT("MaxLinearVelocity"), TEXT("(X=600,Y=600,Z=600)"), Log);
      SetProp(Config, TEXT("bEnableAngularVelocityClamping"), TEXT("True"), Log);
      SetProp(Config, TEXT("MaxAngularVelocity"), TEXT("10"), Log);
    }
    else
      Log.Add(TEXT("no ChaosClothConfig class"));
    // mtl1-16: 2 solver substeps by default (sidecar ue.chaosCloth.solverSubsteps overrides). HEURISTIC from live SIE runs
    // (pen16): sorcerer_exalted free particles >1 cm inside 243-359 -> 174-272, worst 6.6-11.2 -> 4.8-11.1 cm; calgar stays 0;
    // tech_priest 132-140 -> 59-192. 4 substeps or IterationCount 4 gave no further clear gain. Shared config = whichever
    // ClothConfigs entry has SubdivisionCount (ChaosClothSharedSimConfig).
    for (const auto& KV : Asset->ClothConfigs)
      if (UClothConfigBase* Cfg = KV.Value.Get())
        if (Cfg->GetClass()->FindPropertyByName(TEXT("SubdivisionCount")))
          SetProp(Cfg, TEXT("SubdivisionCount"), FString::FromInt(FMath::Clamp((int32)Num(CC, TEXT("solverSubsteps"), 2), 1, 8)), Log);

    // 5b. mtl1-13: sidecar collision -> shared collision PhysicsAsset on this clothing asset.
    {
      const TSharedPtr<FJsonObject>* CO = nullptr;
      TSharedPtr<FJsonObject> Coll;
      if (CC.IsValid() && CC->TryGetObjectField(TEXT("collision"), CO))
        Coll = *CO;
      if (AddCollision(CollPA, Coll, Mesh->GetRefSkeleton(), RefCS, Log, SimName, &PV, &Mask.Values, FMath::Max(2.0f, (float)Num(CC, TEXT("collisionThicknessCm"), 1))) > 0 || CollPA->SkeletalBodySetups.Num() > 0)
      {
        Asset->PhysicsAsset = CollPA;
        Asset->RefreshBoneMapping(Mesh);
      }
    }
    // 6. bind to the render sections (mesh-to-mesh skinning, as the editor's Apply Clothing).
    // mtl1-15: one rebuild scope per section, indices re-resolved by slot each time (they move after every rebuild), and a
    // failed bind is cleaned off the section: sorcerer skirt/beads (2 render sections) left a half-bound second section that
    // turned the whole mesh into spikes on the next PostEditChange (16-bit bone index build).
    int32 Bound = 0;
    // mtl1-15b: UClothingAssetCommon::BindToSkeletalMesh maps one section per asset LOD (LodMap "already mapped" -> false);
    // the failed 2nd attempt on skirt_adds_REND / beads_REND1 still left the sorcerer mesh exploding after the next rebuild.
    // So bind the first matching render slot only; the other render sections of the cloth stay bone-skinned (logged).
    TArray<FName> Extra;
    {
      // primary = the matching render section with the most vertices (the cloth body, not trims/metal bits).
      TMap<FName, int32> NV;
      for (const FSkelMeshSection& Sx : Lod.Sections)
        if (Mesh->GetMaterials().IsValidIndex(Sx.MaterialIndex))
          NV.FindOrAdd(Mesh->GetMaterials()[Sx.MaterialIndex].MaterialSlotName) += Sx.SoftVertices.Num();
      RenderSlots.StableSort([&](const FName& A, const FName& B) { return NV.FindRef(A) > NV.FindRef(B); });
    }
    while (RenderSlots.Num() > 1)
      Extra.Add(RenderSlots.Pop());
    if (Extra.Num() > 0)
    {
      FString E;
      for (const FName& N : Extra) E += N.ToString() + TEXT(" ");
      Log.Add(FString::Printf(TEXT("%s: extra render slots stay bone-skinned [%s]"), *SimName, *E));
    }
    {
      FString SlotLog;
      for (const FName& N : RenderSlots) SlotLog += N.ToString() + TEXT(" ");
      int32 AssetIndex = INDEX_NONE;
      Mesh->GetMeshClothingAssets().Find(Asset, AssetIndex);
      int32 Fail = 0;
      for (const FName& SlotN : RenderSlots)
      {
        SM2WaitMeshBuild(Mesh);
        FScopedSkeletalMeshPostEditChange Scope(Mesh);
        FSkeletalMeshLODModel& L6 = Model->LODModels[0];
        for (int32 S = 0; S < L6.Sections.Num(); ++S)
        {
          FSkelMeshSection& Sec = L6.Sections[S];
          if (Sec.bDisabled || Sec.HasClothingData() || !Mesh->GetMaterials().IsValidIndex(Sec.MaterialIndex) ||
            Mesh->GetMaterials()[Sec.MaterialIndex].MaterialSlotName != SlotN)
            continue;
          if (Asset->BindToSkeletalMesh(Mesh, 0, S, 0))
          {
            FSkelMeshSourceSectionUserData& UD = L6.UserSectionsData.FindOrAdd(Sec.OriginalDataSectionIndex);
            UD.CorrespondClothAssetIndex = (int16)AssetIndex;
            UD.ClothingData.AssetGuid = Asset->GetAssetGuid();
            UD.ClothingData.AssetLodIndex = 0;
            ++Bound;
          }
          else
          {
            ++Fail;
            Sec.ClothMappingDataLODs.Empty();
            Sec.ClothingData = FClothingSectionData();
            Sec.CorrespondClothAssetIndex = INDEX_NONE;
            if (FSkelMeshSourceSectionUserData* UD = L6.UserSectionsData.Find(Sec.OriginalDataSectionIndex))
            {
              UD->CorrespondClothAssetIndex = INDEX_NONE;
              UD->ClothingData = FClothingSectionData();
            }
          }
          break; // one section per slot
        }
      }
      SM2WaitMeshBuild(Mesh);
      Log.Add(FString::Printf(TEXT("%s: slots=[%s] bound=%d failed=%d"), *SimName, *SlotLog, Bound, Fail));
    }
    {
      FBindRec R{Asset, {}, SimName};
      R.Slots = RenderSlots;
      BindRecs.Add(R);
    }
    if (Bound > 0)
      Converted.Add(SimName);
    Log.Add(FString::Printf(TEXT("%s: simSection=%d particles=%d pinned=%d worstMatchCm=%.2f boundSections=%d/%d"), *SimName, SimSection, PV.Num(), Pinned,
      WorstMatch, Bound, RenderSections.Num()));
  }
  if (BindRecs.Num() > 0)
  {
    FScopedSkeletalMeshPostEditChange Scope(Mesh);
    FSkeletalMeshLODModel& L = Model->LODModels[0];
    for (FBindRec& R : BindRecs)
    {
      int32 Idx = INDEX_NONE;
      Mesh->GetMeshClothingAssets().Find(R.Asset, Idx);
      int32 Have = 0, Re = 0;
      for (const FSkelMeshSection& Sx : L.Sections)
        Have += Sx.HasClothingData() && Sx.ClothingData.AssetGuid == R.Asset->GetAssetGuid();
      if (Have > 0) // mtl1-15: already bound -> never rebind (rebind attached wrong sections on sorcerer)
      {
        Log.Add(FString::Printf(TEXT("%s: bindCheck assetIdx=%d had=%d rebound=skip"), *R.Name, Idx, Have));
        continue;
      }
      for (int32 S = 0; S < L.Sections.Num(); ++S)
      {
        FSkelMeshSection& Sec = L.Sections[S];
        if (Sec.bDisabled || Sec.HasClothingData() || !Mesh->GetMaterials().IsValidIndex(Sec.MaterialIndex) ||
          !R.Slots.Contains(Mesh->GetMaterials()[Sec.MaterialIndex].MaterialSlotName))
          continue;
        Log.Add(FString::Printf(TEXT("%s: bindTry sec=%d orig=%d chunk=%d verts=%d"), *R.Name, S, Sec.OriginalDataSectionIndex, Sec.ChunkedParentSectionIndex, Sec.SoftVertices.Num()));
        if (R.Asset->BindToSkeletalMesh(Mesh, 0, S, 0))
        {
          FSkelMeshSourceSectionUserData& UD = L.UserSectionsData.FindOrAdd(Sec.OriginalDataSectionIndex);
          UD.CorrespondClothAssetIndex = (int16)Idx;
          UD.ClothingData.AssetGuid = R.Asset->GetAssetGuid();
          UD.ClothingData.AssetLodIndex = 0;
          ++Re;
        }
      }
      Log.Add(FString::Printf(TEXT("%s: bindCheck assetIdx=%d had=%d rebound=%d slots=%d"), *R.Name, Idx, Have, Re, R.Slots.Num()));
    }
  }
  SM2WaitMeshBuild(Mesh);
  // post-rebuild verification (after the scoped PostEditChange): which sections carry which clothing asset.
  {
    FSkeletalMeshLODModel& L = Model->LODModels[0];
    {
      FString All;
      for (int32 S = 0; S < L.Sections.Num(); ++S)
      {
        const FSkelMeshSection& Sc = L.Sections[S];
        const FSkelMeshSourceSectionUserData* U = L.UserSectionsData.Find(Sc.OriginalDataSectionIndex);
        All += FString::Printf(TEXT("%d:o%d,c%d,v%d,%s%s%s "), S, Sc.OriginalDataSectionIndex, Sc.ChunkedParentSectionIndex, Sc.SoftVertices.Num(),
          Sc.bDisabled ? TEXT("D") : TEXT(""), Sc.HasClothingData() ? TEXT("C") : TEXT(""), (U && U->ClothingData.AssetGuid.IsValid()) ? TEXT("u") : TEXT(""));
      }
      Log.Add(TEXT("sections ") + All);
    }
    for (FBindRec& R : BindRecs)
    {
      FString Secs;
      for (int32 S = 0; S < L.Sections.Num(); ++S)
        if (L.Sections[S].HasClothingData() && L.Sections[S].ClothingData.AssetGuid == R.Asset->GetAssetGuid())
          Secs += FString::Printf(TEXT("%d(orig%d,chunk%d) "), S, L.Sections[S].OriginalDataSectionIndex, L.Sections[S].ChunkedParentSectionIndex);
      Log.Add(FString::Printf(TEXT("%s: postBind sections=[%s]"), *R.Name, *Secs));
    }
  }
  Mesh->MarkPackageDirty();
  SM2WaitMeshBuild(Mesh);
  Report = FString::Join(Log, TEXT(" | "));
  return Converted.Num() > 0;
}

bool USM2PhysicsSetupLibrary::TuneConstraintDrive(UPhysicsAsset* PA, float Stiffness, float Damping, float SwingScale, FString& Report)
{
  if (!PA)
  {
    Report = TEXT("no physics asset");
    return false;
  }
  PA->Modify();
  int32 N = 0;
  for (UPhysicsConstraintTemplate* C : PA->ConstraintSetup)
  {
    if (!C)
      continue;
    C->Modify();
    FConstraintInstance& CI = C->DefaultInstance;
    CI.SetAngularDriveMode(EAngularDriveMode::SLERP);
    CI.SetOrientationDriveSLERP(Stiffness > 0.f);
    CI.SetAngularVelocityDriveSLERP(Damping > 0.f);
    CI.SetAngularDriveParams(Stiffness, Damping, 0.f);
    CI.SetAngularOrientationTarget(FQuat::Identity);
    if (SwingScale > 0.f && SwingScale != 1.f)
    {
      CI.SetAngularSwing1Limit(CI.GetAngularSwing1Motion(), FMath::Clamp(CI.GetAngularSwing1Limit() * SwingScale, 1.f, 90.f));
      CI.SetAngularSwing2Limit(CI.GetAngularSwing2Motion(), FMath::Clamp(CI.GetAngularSwing2Limit() * SwingScale, 1.f, 90.f));
      CI.SetAngularTwistLimit(CI.GetAngularTwistMotion(), FMath::Clamp(CI.GetAngularTwistLimit() * SwingScale, 1.f, 90.f));
    }
    C->SetDefaultProfile(CI);
    ++N;
  }
  PA->MarkPackageDirty();
  Report = FString::Printf(TEXT("drive stiffness=%.0f damping=%.0f swingScale=%.2f constraints=%d"), Stiffness, Damping, SwingScale, N);
  return N > 0;
}

FString USM2PhysicsSetupLibrary::DescribeBody(UPhysicsAsset* PA, USkeletalMesh* Mesh, FName Bone)
{
  if (!PA || !Mesh)
    return TEXT("no PA/mesh");
  const FReferenceSkeleton& RS = Mesh->GetRefSkeleton();
  auto CS = [&](FName B) {
    int32 I = RS.FindBoneIndex(B);
    FTransform T = FTransform::Identity;
    while (I != INDEX_NONE) { T = T * RS.GetRefBonePose()[I]; I = RS.GetParentIndex(I); }
    return T;
  };
  FString Out;
  const int32 BI = PA->FindBodyIndex(Bone);
  const int32 SkI = RS.FindBoneIndex(Bone);
  Out += FString::Printf(TEXT("%s body=%d type=%d skelParent=%s restZ=%.1f"), *Bone.ToString(), BI,
    BI != INDEX_NONE ? (int32)PA->SkeletalBodySetups[BI]->PhysicsType : -1,
    SkI != INDEX_NONE && RS.GetParentIndex(SkI) != INDEX_NONE ? *RS.GetBoneName(RS.GetParentIndex(SkI)).ToString() : TEXT("-"), CS(Bone).GetLocation().Z);
  for (UPhysicsConstraintTemplate* C : PA->ConstraintSetup)
  {
    if (!C) continue;
    const FConstraintInstance& CI = C->DefaultInstance;
    if (CI.ConstraintBone1 != Bone && CI.ConstraintBone2 != Bone) continue;
    const FName O = CI.ConstraintBone1 == Bone ? CI.ConstraintBone2 : CI.ConstraintBone1;
    const int32 OB = PA->FindBodyIndex(O);
    Out += FString::Printf(TEXT(" | %s %s(body %d type %d z %.1f) dist=%.1f lin=%d/%d/%d swing=%.0f/%.0f twist=%.0f f1=(%.1f %.1f %.1f) drive=%.0f"),
      CI.ConstraintBone1 == Bone ? TEXT("child-of") : TEXT("parent-of"), *O.ToString(), OB, OB != INDEX_NONE ? (int32)PA->SkeletalBodySetups[OB]->PhysicsType : -1,
      CS(O).GetLocation().Z, (CS(O).GetLocation() - CS(Bone).GetLocation()).Size(),
      (int32)CI.GetLinearXMotion(), (int32)CI.GetLinearYMotion(), (int32)CI.GetLinearZMotion(), CI.GetAngularSwing1Limit(), CI.GetAngularSwing2Limit(), CI.GetAngularTwistLimit(),
      CI.GetRefFrame(EConstraintFrame::Frame1).GetLocation().X, CI.GetRefFrame(EConstraintFrame::Frame1).GetLocation().Y, CI.GetRefFrame(EConstraintFrame::Frame1).GetLocation().Z,
      CI.ProfileInstance.AngularDrive.SlerpDrive.Stiffness);
  }
  return Out;
}

int32 USM2PhysicsSetupLibrary::SetSimGravity(UPhysicsAsset* PA, bool bEnable)
{
  if (!PA) return 0;
  PA->Modify();
  int32 N = 0;
  for (USkeletalBodySetup* B : PA->SkeletalBodySetups)
    if (B && B->PhysicsType != PhysType_Kinematic)
    {
      B->Modify();
      B->DefaultInstance.SetEnableGravity(bEnable);
      ++N;
    }
  PA->MarkPackageDirty();
  return N;
}

int32 USM2PhysicsSetupLibrary::MeasureClothPenetration(USkeletalMeshComponent* Comp, float ThresholdCm, FString& Report, UPhysicsAsset* OverrideCollision)
{
  if (!Comp || !Comp->GetSkeletalMeshAsset())
  {
    Report = TEXT("no component");
    return -1;
  }
  TSet<UPhysicsAsset*> PAs;
  // mtl1-13b: pinned particles (MaxDistance ~0) follow the skin and cannot be pushed out; count them separately.
  TMap<int32, const TArray<float>*> MaxD;
  const TArray<UClothingAssetBase*>& CAs = Comp->GetSkeletalMeshAsset()->GetMeshClothingAssets();
  for (int32 Ai = 0; Ai < CAs.Num(); ++Ai)
    if (UClothingAssetCommon* C = Cast<UClothingAssetCommon>(CAs[Ai]))
    {
      if (C->PhysicsAsset && !OverrideCollision)
        PAs.Add(C->PhysicsAsset);
      if (C->LodData.Num() > 0)
        if (const FPointWeightMap* M = C->LodData[0].PhysicalMeshData.FindWeightMap(EWeightMapTargetCommon::MaxDistance))
          MaxD.Add(Ai, &M->Values);
    }
  if (OverrideCollision)
    PAs.Add(OverrideCollision);
  int32 InsidePinned = 0;
  const TMap<int32, FClothSimulData>& Data = Comp->GetCurrentClothingData_GameThread();
  const FTransform CompTM = Comp->GetComponentTransform();
  int32 Total = 0, Inside = 0, NaN = 0;
  float Worst = 0.f;
  for (const auto& KV : Data)
  {
    const FClothSimulData& D = KV.Value;
    if (D.Positions.Num() == 0) continue;
    // positions are in the space of D.Transform (component-relative in Chaos); pick the candidate whose centroid lies inside the component bounds.
    FVector CA(0), CB(0);
    for (const FVector3f& P : D.Positions) { CA += D.Transform.TransformPosition(FVector(P)); CB += CompTM.TransformPosition(FVector(P)); }
    CA /= D.Positions.Num(); CB /= D.Positions.Num();
    const bool bUseA = FVector::Dist(CA, Comp->Bounds.Origin) <= FVector::Dist(CB, Comp->Bounds.Origin);
    const TArray<float>* const* MV = MaxD.Find(KV.Key);
    for (int32 Pi = 0; Pi < D.Positions.Num(); ++Pi)
    {
      const FVector3f& P3 = D.Positions[Pi];
      const bool bPinned = MV && (*MV)->IsValidIndex(Pi) && (**MV)[Pi] < 0.1f;
      ++Total;
      if (P3.ContainsNaN()) { ++NaN; continue; }
      const FVector W = bUseA ? D.Transform.TransformPosition(FVector(P3)) : CompTM.TransformPosition(FVector(P3));
      float Deepest = 0.f;
      for (UPhysicsAsset* PA : PAs)
        for (USkeletalBodySetup* B : PA->SkeletalBodySetups)
        {
          const int32 BI = Comp->GetBoneIndex(B->BoneName);
          if (BI == INDEX_NONE) continue;
          const FTransform BT = Comp->GetBoneTransform(BI);
          for (const FKSphereElem& E : B->AggGeom.SphereElems)
          {
            const float Dd = (W - BT.TransformPosition(E.Center)).Size() - E.Radius;
            Deepest = FMath::Min(Deepest, Dd);
          }
          auto Seg = [&](const FVector& C, const FRotator& R, float Len, float R0, float R1) {
            const FVector Ax = BT.TransformVectorNoScale(R.Quaternion().GetUpVector());
            const FVector Cw = BT.TransformPosition(C);
            const float T = FMath::Clamp((float)FVector::DotProduct(W - Cw, Ax), -Len * 0.5f, Len * 0.5f);
            const float Rr = FMath::Lerp(R0, R1, (T + Len * 0.5f) / FMath::Max(Len, 0.001f));
            Deepest = FMath::Min(Deepest, (float)(W - (Cw + Ax * T)).Size() - Rr);
          };
          for (const FKSphylElem& E : B->AggGeom.SphylElems) Seg(E.Center, E.Rotation, E.Length, E.Radius, E.Radius);
          for (const FKTaperedCapsuleElem& E : B->AggGeom.TaperedCapsuleElems) Seg(E.Center, E.Rotation, E.Length, E.Radius0, E.Radius1);
          for (const FKBoxElem& E : B->AggGeom.BoxElems)
          {
            const FVector L = BT.InverseTransformPosition(W) - E.Center;
            const FVector Lr = E.Rotation.Quaternion().UnrotateVector(L);
            const FVector Q = Lr.GetAbs() - FVector(E.X, E.Y, E.Z) * 0.5;
            if (Q.X < 0 && Q.Y < 0 && Q.Z < 0) Deepest = FMath::Min(Deepest, (float)Q.GetMax());
          }
        }
      if (-Deepest > ThresholdCm) { if (bPinned) ++InsidePinned; else ++Inside; }
      if (!bPinned) Worst = FMath::Max(Worst, -Deepest);
    }
  }
  Report = FString::Printf(TEXT("particles=%d inside>%.1fcm=%d (free) +%d pinned worstFreeCm=%.2f nan=%d collisionPAs=%d"), Total, ThresholdCm, Inside, InsidePinned, Worst, NaN, PAs.Num());
  return Inside;
}

UPhysicsAsset* USM2PhysicsSetupLibrary::SetClothCollision(USkeletalMesh* Mesh, UPhysicsAsset* Collision)
{
  UPhysicsAsset* Prev = nullptr;
  if (!Mesh)
    return Prev;
  FScopedSkeletalMeshPostEditChange Scope(Mesh);
  Mesh->Modify();
  for (UClothingAssetBase* A : Mesh->GetMeshClothingAssets())
    if (UClothingAssetCommon* C = Cast<UClothingAssetCommon>(A))
    {
      if (!Prev) Prev = C->PhysicsAsset;
      C->Modify();
      C->PhysicsAsset = Collision;
      C->RefreshBoneMapping(Mesh);
    }
  Mesh->MarkPackageDirty();
  return Prev;
}

int32 USM2PhysicsSetupLibrary::SetClothConfigProps(USkeletalMesh* Mesh, const FString& KeyValues, FString& Report)
{
  int32 N = 0;
  TArray<FString> Log, Pairs;
  if (!Mesh)
    return 0;
  KeyValues.ParseIntoArray(Pairs, TEXT(";"), true);
  for (UClothingAssetBase* A : Mesh->GetMeshClothingAssets())
    if (UClothingAssetCommon* C = Cast<UClothingAssetCommon>(A))
      for (const auto& KV : C->ClothConfigs)
      {
        UClothConfigBase* Cfg = KV.Value.Get();
        if (!Cfg)
          continue;
        for (const FString& Pair : Pairs)
        {
          FString K, V;
          if (!Pair.Split(TEXT("="), &K, &V))
            continue;
          K.TrimStartAndEndInline();
          FProperty* P = Cfg->GetClass()->FindPropertyByName(FName(*K));
          if (!P)
            continue;
          FString Old;
          P->ExportTextItem_Direct(Old, P->ContainerPtrToValuePtr<void>(Cfg), nullptr, Cfg, PPF_None);
          Cfg->Modify();
          if (SetProp(Cfg, *K, V.TrimStartAndEnd(), Log))
          {
            ++N;
            Log.Add(FString::Printf(TEXT("%s.%s.%s %s->%s"), *C->GetName(), *Cfg->GetClass()->GetName(), *K, *Old, *V));
          }
        }
      }
  Report = FString::Join(Log, TEXT(" | "));
  return N;
}
