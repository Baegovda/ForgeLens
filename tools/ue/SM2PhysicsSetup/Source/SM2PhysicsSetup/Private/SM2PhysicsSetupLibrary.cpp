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
      (void)Particles;
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
    for (int32 I = 0; I < Helpers.Num(); ++I)
    {
      const FHelper& H = Helpers[I];
      AddBody(PA, H.Bone, Radius, H.bPinned);
      H.bPinned ? ++Kin : ++Sim;
      FName Anchor = H.Parent;
      FTransform AnchorT = Pos(H.Parent);
      // mtl1-8: the own (kinematic) parent bone competes with earlier helpers, so chains stay short.
      float Best = H.Dist;
      for (int32 J = 0; J < I; ++J)
      {
        const float D = (Helpers[J].T.GetLocation() - H.T.GetLocation()).Size();
        if (D < Best)
        {
          Best = D;
          Anchor = Helpers[J].Bone;
          AnchorT = Helpers[J].T;
        }
      }
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

bool USM2PhysicsSetupLibrary::CreateChaosCloth(USkeletalMesh* Mesh, const FString& JsonPath, TArray<FString>& Converted, FString& Report)
{
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
      int32 Hit = 0;
      for (const FSoftSkinVertex& V : Sec.SoftVertices)
      {
        float D;
        Nearest(V.Position, D);
        Hit += D < 2.0f;
      }
      SimHit = Sec.SoftVertices.Num() ? float(Hit) / Sec.SoftVertices.Num() : 0.f;
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
    TArray<int32> RenderSections;
    for (int32 S = 0; S < Lod.Sections.Num(); ++S)
    {
      const FSkelMeshSection& Sec = Lod.Sections[S];
      if (S == SimSection || Sec.bDisabled || Sec.HasClothingData() || !Mesh->GetMaterials().IsValidIndex(Sec.MaterialIndex))
        continue;
      const FString Slot = Mesh->GetMaterials()[Sec.MaterialIndex].MaterialSlotName.ToString();
      for (const FString& R : RenderNames)
        if (!R.IsEmpty() && (Slot.Equals(R, ESearchCase::IgnoreCase) || Slot.Contains(R, ESearchCase::IgnoreCase) || R.Contains(Slot, ESearchCase::IgnoreCase)))
        {
          RenderSections.AddUnique(S);
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
      SetProp(Config, TEXT("CollisionThickness"), FString::SanitizeFloat(FMath::Max(0.5, Num(CC, TEXT("collisionThicknessCm"), 1))), Log);
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

    // 6. bind to the render sections (mesh-to-mesh skinning, as the editor's Apply Clothing).
    int32 Bound = 0;
    {
      FScopedSkeletalMeshPostEditChange Scope(Mesh);
      int32 AssetIndex = INDEX_NONE;
      Mesh->GetMeshClothingAssets().Find(Asset, AssetIndex);
      for (int32 S : RenderSections)
      {
        FSkelMeshSection& Sec = Lod.Sections[S];
        FSkelMeshSourceSectionUserData& UD = Lod.UserSectionsData.FindOrAdd(Sec.OriginalDataSectionIndex);
        if (Asset->BindToSkeletalMesh(Mesh, 0, S, 0))
        {
          UD.CorrespondClothAssetIndex = (int16)AssetIndex;
          UD.ClothingData.AssetGuid = Asset->GetAssetGuid();
          UD.ClothingData.AssetLodIndex = 0;
          ++Bound;
        }
      }
    }
    if (Bound > 0)
      Converted.Add(SimName);
    Log.Add(FString::Printf(TEXT("%s: simSection=%d particles=%d pinned=%d worstMatchCm=%.2f boundSections=%d/%d"), *SimName, SimSection, PV.Num(), Pinned,
      WorstMatch, Bound, RenderSections.Num()));
  }
  Mesh->MarkPackageDirty();
  Report = FString::Join(Log, TEXT(" | "));
  return Converted.Num() > 0;
}
