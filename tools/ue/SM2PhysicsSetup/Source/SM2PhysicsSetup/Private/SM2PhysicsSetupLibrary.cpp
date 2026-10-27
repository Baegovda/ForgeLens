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
      X.bPinned = Particles == 1 && MaxDistV && MaxDistV->IsValidIndex(I) && (*MaxDistV)[I]->AsNumber() <= 0.0;
      X.MaxDistCm = MaxDistV && MaxDistV->IsValidIndex(I) ? (float)(*MaxDistV)[I]->AsNumber() * 100.0f : -1.0f;
      Helpers.Add(X);
    }
    if (Helpers.Num() == 0)
      continue;
    // Order by distance from the anchor; with no pinned particle the closest helper becomes the pin.
    Helpers.Sort([](const FHelper& A, const FHelper& B) { return A.Dist < B.Dist; });
    if (!Helpers.ContainsByPredicate([](const FHelper& H) { return H.bPinned; }))
      Helpers[0].bPinned = true;
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
    AddBody(PA, Helpers[0].Parent, Radius, true);
    for (int32 I = 0; I < Helpers.Num(); ++I)
    {
      const FHelper& H = Helpers[I];
      AddBody(PA, H.Bone, Radius, H.bPinned);
      H.bPinned ? ++Kin : ++Sim;
      FName Anchor = H.Parent;
      FTransform AnchorT = Pos(H.Parent);
      float Best = BIG_NUMBER;
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
      const float Seg = FMath::Max(Best < BIG_NUMBER ? Best : (H.T.GetLocation() - AnchorT.GetLocation()).Size(), 0.1f);
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
  RB->Node.SimulationSpace = ESimulationSpace::WorldSpace; // actor motion drives the sway
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
