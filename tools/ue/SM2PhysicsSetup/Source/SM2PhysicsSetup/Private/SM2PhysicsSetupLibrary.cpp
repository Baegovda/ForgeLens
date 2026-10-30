// SM2 secondary physics -> live Unreal setup (PhysicsAsset + post-process RigidBody AnimBP).
// Input: {model}.physics.json from the LibSaber exporter (cloths[].helperBones / maxDistanceMeters, jiggles[]).
// HEURISTIC (labelled): SM2 simulates cloth with an NvCloth-style particle solver (prop_cloth_sim); Unreal gets an
// approximation: every helper bone is a small sphere body, pinned particles (maxDistance 0) and the helper parent
// bone are kinematic, and every other helper is ball-jointed (swing/twist limited) to its nearest helper that is
// closer to the anchor. Cloth swing limits are DERIVED from maxDistanceMeters (atan(r/L)); radius, clamps and damping are not from the game.
#include "SM2PhysicsSetupLibrary.h"
#include "HAL/IConsoleManager.h"

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

constexpr EPhysicsType SM2SimType = PhysType_Default;
inline bool IsSM2Sim(const USkeletalBodySetup* B) { return B && B->PhysicsType != PhysType_Kinematic; }

// Every body: simple collision as complex; PhysType_Simulated -> Default (see AddBody). Returns the number changed.
int32 NormalizeBodies(UPhysicsAsset* PA)
{
  int32 N = 0;
  if (!PA) return 0;
  for (USkeletalBodySetup* B : PA->SkeletalBodySetups)
  {
    if (!B) continue;
    bool bChanged = false;
    if (B->CollisionTraceFlag != CTF_UseSimpleAsComplex) { B->CollisionTraceFlag = CTF_UseSimpleAsComplex; bChanged = true; }
    if (B->PhysicsType == PhysType_Simulated) { B->PhysicsType = SM2SimType; bChanged = true; }
    N += bChanged ? 1 : 0;
  }
  return N;
}

USkeletalBodySetup* AddBody(UPhysicsAsset* PA, FName Bone, float Radius, bool bKinematic)
{
  const int32 Existing = PA->FindBodyIndex(Bone);
  if (Existing != INDEX_NONE)
    return PA->SkeletalBodySetups[Existing];
  USkeletalBodySetup* Body = NewObject<USkeletalBodySetup>(PA, NAME_None, RF_Transactional);
  Body->BoneName = Bone;
  // phys52: simulated bodies are PhysType_Default + the RigidBody node's bUseDefaultAsSimulated. A PhysType_Simulated body is
  // ALSO simulated by the skeletal mesh component in the world scene whenever the component's collision has physics
  // (GetSimulatingAndBlendWeight in BodyInstance.cpp: blend weight 1), so it was simulated twice and the world ragdoll was
  // blended over the AnimNode result; with the project's DefaultShapeComplexity=CTF_UseComplexAsSimple every such body also
  // logged "Trying to simulate physics on ... but it has ComplexAsSimple collision" in PIE. Default follows the component's
  // SimulatePhysics (off), so only the AnimNode simulates them.
  Body->PhysicsType = bKinematic ? PhysType_Kinematic : SM2SimType;
  Body->CollisionTraceFlag = CTF_UseSimpleAsComplex; // primitives only; never inherit the project's ComplexAsSimple default
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

// phys52 helpers
double JNum(const TSharedPtr<FJsonObject>& O, const TCHAR* F, double Default)
{
  double V = Default;
  return O.IsValid() && O->TryGetNumberField(F, V) ? V : Default;
}

bool JBool(const TSharedPtr<FJsonObject>& O, const TCHAR* F, bool Default)
{
  bool V = Default;
  return O.IsValid() && O->TryGetBoolField(F, V) ? V : Default;
}

FVector JDir(const TSharedPtr<FJsonObject>& O, const TCHAR* F, const FVector& Default)
{
  const TArray<TSharedPtr<FJsonValue>>* A = nullptr;
  if (!O.IsValid() || !O->TryGetArrayField(F, A) || A->Num() < 3)
    return Default;
  const FVector V((*A)[0]->AsNumber(), (*A)[1]->AsNumber(), (*A)[2]->AsNumber());
  return V.IsNearlyZero() ? Default : V;
}

// phys52: SLERP angular drive in acceleration mode (stiffness [1/s^2], damping [1/s]), twist locked, symmetric swing cone.
void SetSpringJoint(FConstraintInstance& CI, float SwingDeg, bool bLocked, float Stiffness, float Damping)
{
  CI.SetLinearXMotion(LCM_Locked);
  CI.SetLinearYMotion(LCM_Locked);
  CI.SetLinearZMotion(LCM_Locked);
  const EAngularConstraintMotion Swing = bLocked ? ACM_Locked : ACM_Limited;
  CI.SetAngularSwing1Limit(Swing, SwingDeg);
  CI.SetAngularSwing2Limit(Swing, SwingDeg);
  CI.SetAngularTwistLimit(ACM_Locked, 0.0f);
  CI.SetDisableCollision(true);
  if (Stiffness > 0.0f || Damping > 0.0f)
  {
    CI.SetAngularDriveMode(EAngularDriveMode::SLERP);
    CI.SetOrientationDriveSLERP(Stiffness > 0.0f);
    CI.SetAngularVelocityDriveSLERP(Damping > 0.0f);
    CI.SetAngularDriveParams(Stiffness, Damping, 0.0f);
    CI.SetAngularDriveAccelerationMode(true);
  }
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

  // Jiggle bones (prop_phys_jiggle). phys52: the sidecar key is "Name" (C# member name); older plugin builds read "bone" and
  // silently skipped every jiggle (Titus: 0 of 34 bodies). "Name" first, "bone" as fallback; skipped ones are counted + logged.
  // Two passes: (1) every jiggle bone gets a SIMULATED body (a body that already exists as a kinematic cloth anchor, e.g.
  // Belt_jiggle / Shin_L_jiggle, is switched to simulated: the jiggle data wins), (2) kinematic parent only when the parent has
  // no body, then the joint. Joint at the jiggle bone origin (the engine re-aims the bone's offset axis at the spring point).
  // Mapping in ue.rigidBody (DERIVED, see PhysicsSidecar.cs); old sidecars without it get 15 deg / no drive (HEURISTIC).
  int32 JigSim = 0, JigMissing = 0, JigConverted = 0;
  TArray<FString> JigSkipped;
  {
    struct FJig { FName Bone; int32 BI; int32 ParIdx; TSharedPtr<FJsonObject> RB; double SourceMass; };
    TArray<FJig> Jigs;
    const TArray<TSharedPtr<FJsonValue>>* Jiggles = nullptr;
    if (Root->TryGetArrayField(TEXT("jiggles"), Jiggles))
      for (const TSharedPtr<FJsonValue>& JV : *Jiggles)
      {
        const TSharedPtr<FJsonObject> J = JV->AsObject();
        FString BoneName;
        if (!J.IsValid() || (!J->TryGetStringField(TEXT("Name"), BoneName) && !J->TryGetStringField(TEXT("bone"), BoneName)) || BoneName.IsEmpty())
        {
          ++Missing; ++JigMissing;
          JigSkipped.Add(TEXT("<no Name/bone>"));
          continue;
        }
        const int32 BI = Ref.FindBoneIndex(FName(BoneName));
        const int32 ParentIdx = BI == INDEX_NONE ? INDEX_NONE : Ref.GetParentIndex(BI);
        if (ParentIdx == INDEX_NONE)
        {
          ++Missing; ++JigMissing;
          JigSkipped.Add(BoneName + (BI == INDEX_NONE ? TEXT(" (bone not in skeleton)") : TEXT(" (root bone)")));
          continue;
        }
        const TSharedPtr<FJsonObject>* Ue = nullptr;
        const TSharedPtr<FJsonObject>* RBp = nullptr;
        TSharedPtr<FJsonObject> RBo;
        if (J->TryGetObjectField(TEXT("ue"), Ue) && (*Ue)->TryGetObjectField(TEXT("rigidBody"), RBp))
          RBo = *RBp;
        const TSharedPtr<FJsonObject>* Src = nullptr;
        const double SourceMass = J->TryGetObjectField(TEXT("source"), Src) ? JNum(*Src, TEXT("Mass"), 1.0) : 1.0;
        Jigs.Add({ FName(BoneName), BI, ParentIdx, RBo, SourceMass });
      }
    TSet<FName> JigSet;
    for (const FJig& J : Jigs)
      JigSet.Add(J.Bone);
    // Chain depth (parent is a jiggle). The engine evaluates parent before child one-way; a RigidBody chain is two-way, so
    // deeper bodies get lighter (x0.25 per level) to keep children from yanking parents (HEURISTIC).
    auto DepthOf = [&](int32 BI)
    {
      int32 D = 0;
      for (int32 P = Ref.GetParentIndex(BI); P != INDEX_NONE && JigSet.Contains(Ref.GetBoneName(P)) && D < 16; P = Ref.GetParentIndex(P))
        ++D;
      return D;
    };
    // Pass 1: simulated bodies.
    for (const FJig& J : Jigs)
    {
      const bool bHad = PA->FindBodyIndex(J.Bone) != INDEX_NONE;
      USkeletalBodySetup* Body = AddBody(PA, J.Bone, 1.0f, false);
      if (bHad && !IsSM2Sim(Body))
      {
        Body->PhysicsType = SM2SimType;
        ++JigConverted;
        UE_LOG(LogTemp, Display, TEXT("SM2PhysicsSetup: jiggle %s had a kinematic (cloth anchor) body; now simulated"), *J.Bone.ToString());
      }
      const FVector OffsetDir = JDir(J.RB, TEXT("offsetDirUe"), FVector::ZAxisVector).GetSafeNormal();
      const float ComCm = (float)JNum(J.RB, TEXT("comDistanceCm"), 2.0);
      Body->AggGeom.SphereElems.Reset();
      FKSphereElem Sphere(1.0f);
      Sphere.Center = OffsetDir * ComCm; // point mass at the spring point (gravity lever, DERIVED)
      Body->AggGeom.SphereElems.Add(Sphere);
      Body->CollisionReponse = EBodyCollisionResponse::BodyCollision_Disabled;
      Body->DefaultInstance.SetMassOverride(FMath::Pow(0.25f, (float)DepthOf(J.BI)), true);
      Body->DefaultInstance.LinearDamping = 0.0f;  // damping is the drive's (game exp(-damping dt) decay)
      Body->DefaultInstance.AngularDamping = J.RB.IsValid() ? 0.0f : 2.0f;
      Body->DefaultInstance.SetEnableGravity(J.RB.IsValid() ? JBool(J.RB, TEXT("gravityEnabled"), false) : true);
    }
    // Pass 2: anchors + joints.
    for (const FJig& J : Jigs)
    {
      const FName Parent = Ref.GetBoneName(J.ParIdx);
      if (PA->FindBodyIndex(Parent) == INDEX_NONE)
      {
        AddBody(PA, Parent, 1.0f, true);
        ++Kin;
      }
      bool bDup = false;
      for (const UPhysicsConstraintTemplate* C : PA->ConstraintSetup)
        bDup |= C && C->DefaultInstance.ConstraintBone1 == J.Bone;
      if (bDup)
      {
        JigSkipped.Add(J.Bone.ToString() + TEXT(" (already constrained, joint kept)"));
        ++JigSim;
        continue;
      }
      if (!J.RB.IsValid())
      {
        AddConstraint(PA, J.Bone, Parent, CS[J.BI], CS[J.ParIdx], 15.0f); // old sidecar: previous behaviour
        ++JigSim;
        continue;
      }
      const FVector OffsetDir = JDir(J.RB, TEXT("offsetDirUe"), FVector::ZAxisVector).GetSafeNormal();
      const FVector RestDir = JDir(J.RB, TEXT("restDirUe"), OffsetDir).GetSafeNormal();
      UPhysicsConstraintTemplate* C = NewObject<UPhysicsConstraintTemplate>(PA, NAME_None, RF_Transactional);
      FConstraintInstance& CI = C->DefaultInstance;
      CI.JointName = J.Bone;
      CI.ConstraintBone1 = J.Bone;
      CI.ConstraintBone2 = Parent;
      const FTransform F1(FRotationMatrix::MakeFromX(OffsetDir).ToQuat(), FVector::ZeroVector);
      const FTransform Desired = FTransform(FQuat::FindBetweenNormals(OffsetDir, RestDir)) * CS[J.BI];
      CI.SetRefFrame(EConstraintFrame::Frame1, F1);
      CI.SetRefFrame(EConstraintFrame::Frame2, (F1 * Desired).GetRelativeTransform(CS[J.ParIdx]));
      SetSpringJoint(CI, (float)JNum(J.RB, TEXT("swingDeg"), 15.0), JBool(J.RB, TEXT("locked"), false),
        (float)JNum(J.RB, TEXT("driveStiffness"), 0.0), (float)JNum(J.RB, TEXT("driveDamping"), 0.0));
      C->SetDefaultProfile(CI);
      PA->ConstraintSetup.Add(C);
      ++JigSim;
    }
    Sim += JigSim;
    for (const FString& S : JigSkipped)
      UE_LOG(LogTemp, Warning, TEXT("SM2PhysicsSetup: jiggle skipped/note: %s"), *S);
  }

  NormalizeBodies(PA);
  PA->UpdateBodySetupIndexMap();
  PA->UpdateBoundsBodiesArray();
  PA->RefreshPhysicsAssetChange();
  FAssetRegistryModule::AssetCreated(PA);
  PA->MarkPackageDirty();

  // phys52: prop_motion_damper_controller -> PA_{mesh}_SM2Damper for a SECOND RigidBody node (HEURISTIC model, see the
  // sidecar's springDamperNote): each joint is an angular spring-damper on its bone (pivot = bone origin, capsule body to
  // segmentTo, mass = joint mass), SLERP acceleration drive (omega0^2, 2*zeta*omega0), cone = jointAngle, twist locked; its
  // skeleton parent is kinematic unless it is a joint too. constrains = kinematic capsule (up -> down locator, radius) that
  // only the joint bodies collide with; shrunk so no rest-pose joint body starts inside it. A joint that already has a
  // simulated body in the main PA is skipped (no double simulation).
  UPhysicsAsset* DPA = nullptr;
  int32 DamperBodies = 0, DamperSkipped = 0, DamperCaps = 0;
  float DamperGravity = 0.0f;
  TArray<FString> DamperLog;
  {
    const TArray<TSharedPtr<FJsonValue>>* Dampers = nullptr;
    if (Root->TryGetArrayField(TEXT("springDampers"), Dampers) && Dampers->Num() > 0)
    {
      const FString DName = FreeName(Folder, TEXT("PA_") + Mesh->GetName() + TEXT("_SM2Damper"));
      DPA = NewObject<UPhysicsAsset>(CreatePackage(*(Folder / DName)), *DName, RF_Public | RF_Standalone | RF_Transactional);
      DPA->SetPreviewMesh(Mesh);
      const float BodyR = 3.0f; // cm, HEURISTIC (collision radius of a joint segment)
      TArray<FName> JointBodies;
      struct FSeg { FVector A, B; };
      TArray<FSeg> RestSegs;
      for (const TSharedPtr<FJsonValue>& DV : *Dampers)
      {
        const TSharedPtr<FJsonObject> D = DV->AsObject();
        if (!D.IsValid())
          continue;
        for (const TSharedPtr<FJsonValue>& JV : D->GetArrayField(TEXT("joints")))
        {
          const TSharedPtr<FJsonObject> J = JV->AsObject();
          FString N, SegTo;
          J->TryGetStringField(TEXT("Name"), N);
          J->TryGetStringField(TEXT("segmentTo"), SegTo);
          const int32 BI = Ref.FindBoneIndex(FName(N));
          const int32 ParIdx = BI == INDEX_NONE ? INDEX_NONE : Ref.GetParentIndex(BI);
          const int32 SI = SegTo.IsEmpty() ? INDEX_NONE : Ref.FindBoneIndex(FName(SegTo));
          if (ParIdx == INDEX_NONE || SI == INDEX_NONE)
          {
            ++DamperSkipped;
            DamperLog.Add(N + (BI == INDEX_NONE ? TEXT(": bone not in skeleton") : ParIdx == INDEX_NONE ? TEXT(": root bone") : TEXT(": leaf joint (no child bone, its rotation moves nothing)")));
            continue;
          }
          const int32 MainIdx = PA->FindBodyIndex(FName(N));
          if (MainIdx != INDEX_NONE && IsSM2Sim(PA->SkeletalBodySetups[MainIdx]))
          {
            ++DamperSkipped;
            DamperLog.Add(N + TEXT(": already simulated in the main PA (jiggle/cloth)"));
            continue;
          }
          const TSharedPtr<FJsonObject>* UeP = nullptr;
          TSharedPtr<FJsonObject> Ue;
          if (J->TryGetObjectField(TEXT("ue"), UeP))
            Ue = *UeP;
          const FVector SegLocal = CS[SI].GetLocation() - CS[BI].GetLocation();
          const FVector SegDirLocal = CS[BI].InverseTransformVectorNoScale(SegLocal).GetSafeNormal();
          const float SegLen = FMath::Max(SegLocal.Size(), 1.0f);
          // Body (reuse = kinematic parent made earlier for another joint -> make it simulated).
          USkeletalBodySetup* Body = AddBody(DPA, FName(N), 1.0f, false);
          Body->PhysicsType = SM2SimType;
          Body->AggGeom.SphereElems.Reset();
          FKSphylElem Cap(BodyR, FMath::Max(SegLen - 2.0f * BodyR, 0.1f));
          Cap.Center = SegDirLocal * SegLen * 0.5f;
          Cap.Rotation = FRotationMatrix::MakeFromZ(SegDirLocal).Rotator();
          Body->AggGeom.SphylElems.Add(Cap);
          Body->CollisionReponse = EBodyCollisionResponse::BodyCollision_Enabled;
          Body->DefaultInstance.SetMassOverride((float)JNum(Ue, TEXT("massKg"), 1.0), true);
          Body->DefaultInstance.LinearDamping = 0.0f;
          Body->DefaultInstance.AngularDamping = 0.0f;
          const float G = (float)JNum(Ue, TEXT("gravityScale"), 0.0);
          Body->DefaultInstance.SetEnableGravity(G > 0.0f);
          DamperGravity = FMath::Max(DamperGravity, G);
          JointBodies.AddUnique(FName(N));
          RestSegs.Add({ CS[BI].GetLocation(), CS[SI].GetLocation() });
          const FName Parent = Ref.GetBoneName(ParIdx);
          if (DPA->FindBodyIndex(Parent) == INDEX_NONE)
            AddBody(DPA, Parent, 1.0f, true); // kinematic hanger; becomes simulated if it is a joint reached later
          UPhysicsConstraintTemplate* C = NewObject<UPhysicsConstraintTemplate>(DPA, NAME_None, RF_Transactional);
          FConstraintInstance& CI = C->DefaultInstance;
          CI.JointName = FName(N);
          CI.ConstraintBone1 = FName(N);
          CI.ConstraintBone2 = Parent;
          const FTransform F1(FRotationMatrix::MakeFromX(SegDirLocal).ToQuat(), FVector::ZeroVector);
          CI.SetRefFrame(EConstraintFrame::Frame1, F1);
          CI.SetRefFrame(EConstraintFrame::Frame2, (F1 * CS[BI]).GetRelativeTransform(CS[ParIdx]));
          SetSpringJoint(CI, (float)JNum(Ue, TEXT("coneDeg"), 30.0), false, (float)JNum(Ue, TEXT("driveStiffness"), 0.0), (float)JNum(Ue, TEXT("driveDamping"), 0.0));
          C->SetDefaultProfile(CI);
          DPA->ConstraintSetup.Add(C);
          ++DamperBodies;
          DamperLog.Add(FString::Printf(TEXT("%s: body to %s (%.1f cm), parent %s, cone %.0f, drive %.1f/%.1f, mass %.2f, gravity %.2f"), *N, *SegTo, SegLen, *Parent.ToString(),
            JNum(Ue, TEXT("coneDeg"), 30.0), JNum(Ue, TEXT("driveStiffness"), 0.0), JNum(Ue, TEXT("driveDamping"), 0.0), JNum(Ue, TEXT("massKg"), 1.0), G));
        }
        // Capsule limits.
        const TArray<TSharedPtr<FJsonValue>>* Cons = nullptr;
        if (D->TryGetArrayField(TEXT("constrains"), Cons))
          for (const TSharedPtr<FJsonValue>& KV : *Cons)
          {
            const TSharedPtr<FJsonObject> K = KV->AsObject();
            FString UpB, DnB;
            K->TryGetStringField(TEXT("upBone"), UpB);
            K->TryGetStringField(TEXT("downBone"), DnB);
            const int32 UI = Ref.FindBoneIndex(FName(UpB)), DI = Ref.FindBoneIndex(FName(DnB));
            if (UI == INDEX_NONE || DI == INDEX_NONE)
            {
              DamperLog.Add(FString::Printf(TEXT("constraint %s-%s skipped: locator bone not in skeleton%s"), *UpB, *DnB, JBool(K, TEXT("HasExternalLocators"), false) ? TEXT(" (hasExternalLocators)") : TEXT("")));
              continue;
            }
            const FVector UpW = CS[UI].TransformPosition(JDir(K, TEXT("upOffsetCm"), FVector::ZeroVector));
            const FVector DnW = CS[DI].TransformPosition(JDir(K, TEXT("downOffsetCm"), FVector::ZeroVector));
            float R = (float)JNum(K, TEXT("radiusCm"), 0.0);
            // Shrink off the rest pose: distance between the capsule axis and each joint segment (sampled).
            float Clear = BIG_NUMBER;
            for (const FSeg& Sg : RestSegs)
              for (int32 T = 0; T <= 8; ++T)
              {
                const FVector P = FMath::Lerp(Sg.A, Sg.B, T / 8.0f);
                Clear = FMath::Min(Clear, (float)FMath::PointDistToSegment(P, UpW, DnW));
              }
            const float RMax = Clear - BodyR - 0.5f;
            if (R > RMax)
            {
              DamperLog.Add(FString::Printf(TEXT("constraint %s-%s radius %.1f -> %.1f cm (rest pose clearance %.1f, HEURISTIC shrink)"), *UpB, *DnB, R, FMath::Max(RMax, 0.5f), Clear));
              R = FMath::Max(RMax, 0.5f);
            }
            const FName CapBone = Ref.GetBoneName(UI);
            const bool bNew = DPA->FindBodyIndex(CapBone) == INDEX_NONE;
            USkeletalBodySetup* CB = AddBody(DPA, CapBone, 1.0f, true);
            if (bNew)
              CB->AggGeom.SphereElems.Reset();
            const FVector A = CS[UI].InverseTransformPosition(UpW), B = CS[UI].InverseTransformPosition(DnW);
            const FVector Axis = (B - A).GetSafeNormal();
            FKSphylElem KC(R, FMath::Max((B - A).Size(), 0.1f));
            KC.Center = (A + B) * 0.5f;
            KC.Rotation = FRotationMatrix::MakeFromZ(Axis.IsNearlyZero() ? FVector::ZAxisVector : Axis).Rotator();
            CB->AggGeom.SphylElems.Add(KC);
            CB->CollisionReponse = EBodyCollisionResponse::BodyCollision_Enabled;
            ++DamperCaps;
            DamperLog.Add(FString::Printf(TEXT("constraint capsule on %s: %s -> %s, r %.1f cm, len %.1f cm"), *CapBone.ToString(), *UpB, *DnB, R, (B - A).Size()));
          }
      }
      DPA->UpdateBodySetupIndexMap();
      // Collision: only joint bodies vs capsule bodies (joint-joint and anything-kinematic-hanger pairs off).
      for (int32 I = 0; I < DPA->SkeletalBodySetups.Num(); ++I)
        for (int32 K = I + 1; K < DPA->SkeletalBodySetups.Num(); ++K)
        {
          const bool bJI = JointBodies.Contains(DPA->SkeletalBodySetups[I]->BoneName), bJK = JointBodies.Contains(DPA->SkeletalBodySetups[K]->BoneName);
          const bool bCI = DPA->SkeletalBodySetups[I]->AggGeom.SphylElems.Num() > 0 && !bJI, bCK = DPA->SkeletalBodySetups[K]->AggGeom.SphylElems.Num() > 0 && !bJK;
          if (!((bJI && bCK) || (bJK && bCI)))
            DPA->DisableCollision(I, K);
        }
      for (USkeletalBodySetup* B : DPA->SkeletalBodySetups)
        if (!JointBodies.Contains(B->BoneName) && B->AggGeom.SphylElems.Num() == 0)
          B->CollisionReponse = EBodyCollisionResponse::BodyCollision_Disabled;
      NormalizeBodies(DPA);
      DPA->UpdateBoundsBodiesArray();
      DPA->RefreshPhysicsAssetChange();
      FAssetRegistryModule::AssetCreated(DPA);
      DPA->MarkPackageDirty();
      for (const FString& S : DamperLog)
        UE_LOG(LogTemp, Display, TEXT("SM2PhysicsSetup damper: %s"), *S);
      if (DamperBodies == 0)
        DamperLog.Add(TEXT("no damper body created; second RigidBody node not added"));
    }
  }

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
  RB->Node.bUseDefaultAsSimulated = true; // phys52: bodies are PhysType_Default (see AddBody)
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
  // phys52: second RigidBody node for the motion dampers. Base-bone space on the skeleton root: root motion / clip loop
  // jumps are removed, the pelvis/belt motion relative to the root drives the swing (HEURISTIC). Gravity 980 * max joint
  // gravity (one value per node; joints with gravity 0 have it off) HEURISTIC.
  UAnimGraphNode_RigidBody* RB2 = nullptr;
  if (DPA && DamperBodies > 0)
  {
    C2L->NodePosX = 100;
    RB2 = NewGraphNode<UAnimGraphNode_RigidBody>(Graph, -150);
    RB2->NodePosY = 200;
    RB2->NodeComment = TEXT("SM2 motion damper (phys52)");
    RB2->Node.OverridePhysicsAsset = DPA;
    RB2->Node.bUseDefaultAsSimulated = true;
    RB2->Node.SimulationSpace = ESimulationSpace::BaseBoneSpace;
    RB2->Node.BaseBoneRef.BoneName = Ref.GetBoneName(0);
    RB2->Node.SimSpaceSettings.WorldAlpha = 0.0f;
    RB2->Node.SimSpaceSettings.DampingAlpha = 0.0f;
    RB2->Node.bEnableWorldGeometry = false;
    RB2->Node.bOverrideWorldGravity = true;
    RB2->Node.OverrideWorldGravity = FVector(0.0, 0.0, -980.0 * DamperGravity);
  }
  const bool bLinked = PosePin(In, EGPD_Output) && PosePin(L2C, EGPD_Input) && PosePin(RB, EGPD_Input) && PosePin(C2L, EGPD_Input) && PosePin(Out, EGPD_Input)
    && (!RB2 || PosePin(RB2, EGPD_Input));
  if (bLinked)
  {
    PosePin(In, EGPD_Output)->MakeLinkTo(PosePin(L2C, EGPD_Input));
    PosePin(L2C, EGPD_Output)->MakeLinkTo(PosePin(RB, EGPD_Input));
    if (RB2)
    {
      PosePin(RB, EGPD_Output)->MakeLinkTo(PosePin(RB2, EGPD_Input));
      PosePin(RB2, EGPD_Output)->MakeLinkTo(PosePin(C2L, EGPD_Input));
    }
    else
      PosePin(RB, EGPD_Output)->MakeLinkTo(PosePin(C2L, EGPD_Input));
    PosePin(C2L, EGPD_Output)->MakeLinkTo(PosePin(Out, EGPD_Input));
  }
  FKismetEditorUtilities::CompileBlueprint(ABP);
  ABP->MarkPackageDirty();

  Mesh->Modify();
  Mesh->SetPhysicsAsset(PA);
  Mesh->SetPostProcessAnimBlueprint(TSubclassOf<UAnimInstance>(Cast<UClass>(ABP->GeneratedClass)));
  Mesh->MarkPackageDirty();

  Report = FString::Printf(TEXT("PA=%s ABP=%s simulated=%d kinematic=%d constraints=%d missingBones=%d linked=%d compiled=%d jiggles=%d jiggleMissing=%d jiggleFromKinematic=%d damperPA=%s damperBodies=%d damperSkipped=%d damperCapsules=%d damperGravity=%.2f"),
    *PA->GetPathName(), *ABP->GetPathName(), Sim, Kin, PA->ConstraintSetup.Num(), Missing, bLinked ? 1 : 0,
    ABP->Status != BS_Error ? 1 : 0, JigSim, JigMissing, JigConverted, DPA ? *DPA->GetPathName() : TEXT("none"), DamperBodies, DamperSkipped, DamperCaps, DamperGravity);
  if (JigSkipped.Num() > 0)
    Report += TEXT(" jiggleSkipped=[") + FString::Join(JigSkipped, TEXT("; ")) + TEXT("]");
  if (DamperLog.Num() > 0)
    Report += TEXT(" damperLog=[") + FString::Join(DamperLog, TEXT("; ")) + TEXT("]");
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
          if (!RB->NodeComment.StartsWith(TEXT("SM2 motion damper"))) // phys52: TuneRigidBody edits the main node only
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
  Body->CollisionTraceFlag = CTF_UseSimpleAsComplex;
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
  // R76 shared fix: the old fit took the MIN over every free rest particle, so one deeply embedded particle shrank the whole
  // shape towards the 0.25 floor (R69 PROVEN 0.65-0.72 on the sorcerer) and every later frame collided against an undersized
  // body (viewer R73: radius x0.75 4.66 %, x0.5 5.6 % vs x1 2.44 % free particles inside the body). Now particles deeper than
  // half the radius count as authored-inside (Chaos pushes them out on the first step, as the viewer does) and do not drive
  // the fit; shallow ones may shrink the shape to at most RestFitFloor. The raw (old) scale is kept only for remap rejection.
  // R91: A/B knobs without a rebuild. sm2.RestFitFloor (default 0.9) and sm2.RestFitDeepFrac (default 0.5; 0 = legacy:
  // every particle drives the fit, as before R76). Legacy pre-R76 behaviour = RestFitFloor 0.25 + RestFitDeepFrac 0.
  static TAutoConsoleVariable<float> CVarFitFloor(TEXT("sm2.RestFitFloor"), 0.9f, TEXT("SM2 cloth rest-fit radius floor (R76 default 0.9; legacy 0.25)"));
  static TAutoConsoleVariable<float> CVarDeepFrac(TEXT("sm2.RestFitDeepFrac"), 0.5f, TEXT("SM2 cloth rest-fit: particles deeper than this fraction of the radius are ignored (0 = legacy, none ignored)"));
  const float RestFitFloor = FMath::Clamp(CVarFitFloor.GetValueOnGameThread(), 0.0f, 1.0f), DeepFrac = FMath::Clamp(CVarDeepFrac.GetValueOnGameThread(), 0.0f, 1.0f);
  // R93 A/B: the floor is only applied when fewer than this fraction of the free rest particles sit shallowly inside the shape
  // (many shallow hits = the authored shape is really too big for this mesh, e.g. a single coat capsule). 1 = always floor (R91).
  static TAutoConsoleVariable<float> CVarShallowFrac(TEXT("sm2.RestFitShallowFrac"), 1.0f, TEXT("SM2 cloth rest-fit: skip the radius floor when the shallow-inside fraction of free particles is >= this (1 = always floor)"));
  float ShallowFrac = FMath::Clamp(CVarShallowFrac.GetValueOnGameThread(), 0.0f, 1.0f);
  // R97k: per-cloth override "name=frac;name=frac" (exact cloth name). Commissar coat_SIM_01 + straps_SIM_01 (names unique to the commissar) need the floor skipped
  // (R97j A/B: global 0.04 fixed the coat but hurt titus/tech_priest/chaplain/straps).
  static TAutoConsoleVariable<FString> CVarShallowOverride(TEXT("sm2.RestFitShallowOverride"), TEXT("coat_SIM_01=0.04;straps_SIM_01=0.04"), TEXT("SM2 cloth rest-fit: per-cloth ShallowFrac overrides, 'cloth=frac;cloth=frac' ('' = none)"));
  { TArray<FString> Ents; CVarShallowOverride.GetValueOnGameThread().ParseIntoArray(Ents, TEXT(";"), true);
    for (const FString& E : Ents) { FString K, V; if (E.Split(TEXT("="), &K, &V) && K.TrimStartAndEnd().Equals(Who, ESearchCase::IgnoreCase)) { ShallowFrac = FMath::Clamp(FCString::Atof(*V), 0.0f, 1.0f); Log.Add(FString::Printf(TEXT("%s: shallowFrac override %.3f"), *Who, ShallowFrac)); } } }
  int32 DeepIgnored = 0, FloorSkipped = 0;
  float LastRawScale = 1.0f;
  // R97g: per-shape rest-fit log (raw S before the floor, shallow/deep/free particle counts) for the commissar coat A/B.
  float LastPreFloor = 1.0f; int32 LastShallow = 0, LastDeep = 0, LastFree = 0;
  auto FitScale = [&](const FVector& A, const FVector& B, float RA, float RB) {
    float S = 1.0f, Raw = 1.0f; int32 Shallow = 0, Free = 0;
    LastRawScale = 1.0f; LastPreFloor = 1.0f; LastShallow = LastDeep = LastFree = 0;
    if (!RestPV) return S;
    const FVector AB = B - A; const double L2 = AB.SizeSquared();
    for (int32 I = 0; I < RestPV->Num(); ++I)
    {
      if (RestMaxDist && RestMaxDist->IsValidIndex(I) && (*RestMaxDist)[I] <= 0.1f) continue; // pinned: follows skin
      ++Free; const FVector P((*RestPV)[I]);
      const double T = L2 > 1e-6 ? FMath::Clamp(FVector::DotProduct(P - A, AB) / L2, 0.0, 1.0) : 0.0;
      const float R = FMath::Lerp(RA, RB, (float)T);
      const float D = (float)FVector::Dist(P, A + AB * T);
      if (R > KINDA_SMALL_NUMBER && D < R + ThickCm)
      {
        const float Si = FMath::Max(0.0f, D - ThickCm) / R;
        Raw = FMath::Min(Raw, Si);
        if (D < R * DeepFrac) { ++DeepIgnored; ++LastDeep; continue; }
        ++Shallow; // R97g: was never counted, so sm2.RestFitShallowFrac could not trigger (R93 floorSkipped 0 everywhere)
        S = FMath::Min(S, Si);
      }
    }
    LastRawScale = FMath::Max(Raw, 0.25f); LastPreFloor = S; LastShallow = Shallow; LastFree = Free;
    if (ShallowFrac < 1.0f && Free > 0 && (float)Shallow >= ShallowFrac * (float)Free) { ++FloorSkipped; return FMath::Max(S, 0.25f); }
    return FMath::Max(S, RestFitFloor); // HEURISTIC floor (R76): keep >= 90 % of the authored radius
  };
  if (!Coll.IsValid())
    return 0;
  FString Space;
  Coll->TryGetStringField(TEXT("space"), Space);
  const bool bMirror = Space == TEXT("ueMesh"); // un-negate extractor-mirrored offsets -> raw bone-local
  int32 N = 0, Missing = 0, Remapped = 0, BoxRestInside = 0, FromLocator = 0;
  // mtl1-33: generic missing-bone handling. Shared CDT shapes are defined per prop and can name bones the model does not
  // have (calgar: box on Backpack_CDT_L). Rule (HEURISTIC, sidecar collision.missingBoneRemap=false -> skip only):
  // split names into lower-case tokens at '_' / case changes are NOT split; side tokens l/r/left/right and "cdt" are qualifiers,
  // the rest are core tokens. A candidate bone must contain every core token and no opposite side; score +1 for a matching
  // "cdt" qualifier, +1 for a matching side. The unique best candidate is used, ties or no candidate -> the shape is skipped.
  // Every remap and skip is logged by shape name.
  bool bRemap = true;
  Coll->TryGetBoolField(TEXT("missingBoneRemap"), bRemap);
  TArray<FString> MissLog, RemapLog;
  auto Tok = [](const FString& S) { TArray<FString> T; S.ToLower().ParseIntoArray(T, TEXT("_"), true); return T; };
  auto IsSide = [](const FString& T) { return T == TEXT("l") || T == TEXT("r") || T == TEXT("left") || T == TEXT("right"); };
  auto SideOf = [&](const TArray<FString>& T) { for (const FString& X : T) if (IsSide(X)) return X.Left(1); return FString(); };
  auto Resolve = [&](const FString& B, const FString& Shape) -> int32 {
    const int32 I = RS.FindBoneIndex(FName(*B)); // FName compare is case-insensitive
    if (I != INDEX_NONE) return I;
    ++Missing;
    int32 Best = INDEX_NONE, BestScore = -1, Ties = 0;
    if (bRemap)
    {
      const TArray<FString> MT = Tok(B);
      const FString MS = SideOf(MT);
      const bool MC = MT.Contains(TEXT("cdt"));
      TArray<FString> Core;
      for (const FString& X : MT) if (!IsSide(X) && X != TEXT("cdt")) Core.Add(X);
      for (int32 K = 0; K < RS.GetRawBoneNum() && Core.Num() > 0; ++K)
      {
        const TArray<FString> CT = Tok(RS.GetBoneName(K).ToString());
        bool Ok = true;
        for (const FString& X : Core) Ok &= CT.Contains(X);
        const FString CSd = SideOf(CT);
        if (!Ok || (!MS.IsEmpty() && !CSd.IsEmpty() && CSd != MS)) continue;
        const int32 Sc = (CT.Contains(TEXT("cdt")) == MC ? 1 : 0) + (CSd == MS ? 1 : 0);
        if (Sc > BestScore) { BestScore = Sc; Best = K; Ties = 0; }
        else if (Sc == BestScore) ++Ties;
      }
    }
    if (Best != INDEX_NONE && Ties == 0)
    {
      ++Remapped;
      RemapLog.AddUnique(FString::Printf(TEXT("%s:%s->%s"), *Shape, *B, *RS.GetBoneName(Best).ToString()));
      return Best;
    }
    MissLog.AddUnique(FString::Printf(TEXT("%s:%s%s"), *Shape, *B, Ties ? TEXT("(ambiguous)") : TEXT("")));
    return INDEX_NONE;
  };
  // mtl1-33 (UE evidence, calgar): Backpack_CDT_L -> powerplant_backpack_cdt put the 52x60x100 cm box around 114 free rest
  // particles of the cloak (own-collider penetration 0 -> 204, worst 12.7 cm). A remapped shape that still holds free rest
  // particles (box: any inside; capsule/sphere: rest fit hit the 0.25 floor) is therefore dropped and logged.
  int32 Rejected = 0;
  auto RejectRemap = [&](const FString& B, const FName& Name, const FString& Shape, bool bBad) {
    if (!bBad || Name == FName(*B)) return false;
    ++Rejected;
    MissLog.AddUnique(FString::Printf(TEXT("%s:%s(remap->%s rejected: rest particles inside)"), *Shape, *B, *Name.ToString()));
    return true;
  };
  auto BoneCS = [&](const FString& B, const FString& Shape, FTransform& Out, FName& Name) {
    const int32 I = Resolve(B, Shape);
    if (I == INDEX_NONE) return false;
    Out = CS[I];
    Name = RS.GetBoneName(I);
    return true;
  };
  const TArray<TSharedPtr<FJsonValue>>* Arr = nullptr;
  // mtl1-37: shapes whose bone was a tpl_markup locator arrive already on the locator's parent bone (exporter, ResolvedFrom).
  static const TCHAR* ShapeArrays[] = { TEXT("capsules"), TEXT("spheres"), TEXT("boxes") };
  for (const TCHAR* F : ShapeArrays)
    if (Coll->TryGetArrayField(F, Arr))
      for (const TSharedPtr<FJsonValue>& V : *Arr)
      {
        FString RF;
        if (V->AsObject().IsValid() && V->AsObject()->TryGetStringField(TEXT("ResolvedFrom"), RF) && !RF.IsEmpty()) ++FromLocator;
      }
  if (Coll->TryGetArrayField(TEXT("capsules"), Arr))
    for (const TSharedPtr<FJsonValue>& V : *Arr)
    {
      const TSharedPtr<FJsonObject> K = V->AsObject();
      const FString B1 = K->GetStringField(TEXT("BoneFirst")), B2 = K->GetStringField(TEXT("BoneSecond"));
      FTransform T1, T2;
      FName N1, N2;
      FString SN = K->HasField(TEXT("Name")) ? K->GetStringField(TEXT("Name")) : B1;
      if (!BoneCS(B1, SN, T1, N1)) continue;
      const FVector P1 = JVec(K, TEXT("offsetFirstCm"), bMirror);
      FVector P2 = P1;
      if (!B2.IsEmpty())
      {
        if (!BoneCS(B2, SN, T2, N2)) continue; // mtl1-33: skip instead of collapsing to a sphere at the first bone
        P2 = T1.InverseTransformPosition(T2.TransformPosition(JVec(K, TEXT("offsetSecondCm"), bMirror)));
      }
      float R1 = (float)K->GetNumberField(TEXT("radiusFirstCm")), R2 = (float)K->GetNumberField(TEXT("radiusSecondCm"));
      {
        const float S = FitScale(T1.TransformPosition(P1), T1.TransformPosition(P2), R1, R2);
        Log.Add(FString::Printf(TEXT("%s: restShape capsule %s%s%s r=%.1f/%.1f preFloor=%.3f raw=%.3f final=%.3f shallow=%d deep=%d freeParticles=%d"), *Who, *B1, B2.IsEmpty() ? TEXT("") : TEXT("->"), *B2, R1, R2, LastPreFloor, LastRawScale, S, LastShallow, LastDeep, LastFree));
        if (RejectRemap(B1, N1, SN, LastRawScale <= 0.2501f) || (!B2.IsEmpty() && RejectRemap(B2, N2, SN, LastRawScale <= 0.2501f))) continue;
        if (S < 0.999f) { ++Fitted; MinScale = FMath::Min(MinScale, S); R1 *= S; R2 *= S; }
      }
      const FVector Axis = P2 - P1;
      const float Len = Axis.Size();
      USkeletalBodySetup* Body = CollBody(PA, N1);
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
      FName BN;
      if (!BoneCS(K->GetStringField(TEXT("Bone")), K->HasField(TEXT("Name")) ? K->GetStringField(TEXT("Name")) : K->GetStringField(TEXT("Bone")), T, BN)) continue;
      float Rs = (float)K->GetNumberField(TEXT("radiusCm"));
      const FVector C = JVec(K, TEXT("offsetCm"), bMirror);
      { const float S = FitScale(T.TransformPosition(C), T.TransformPosition(C), Rs, Rs);
        Log.Add(FString::Printf(TEXT("%s: restShape sphere %s r=%.1f preFloor=%.3f raw=%.3f final=%.3f shallow=%d deep=%d freeParticles=%d"), *Who, *K->GetStringField(TEXT("Bone")), Rs, LastPreFloor, LastRawScale, S, LastShallow, LastDeep, LastFree));
        if (RejectRemap(K->GetStringField(TEXT("Bone")), BN, K->HasField(TEXT("Name")) ? K->GetStringField(TEXT("Name")) : BN.ToString(), LastRawScale <= 0.2501f)) continue;
        if (S < 0.999f) { ++Fitted; MinScale = FMath::Min(MinScale, S); Rs *= S; } }
      FKSphereElem E(Rs);
      E.Center = C;
      CollBody(PA, BN)->AggGeom.SphereElems.Add(E);
      ++N;
    }
  if (Coll->TryGetArrayField(TEXT("boxes"), Arr))
    for (const TSharedPtr<FJsonValue>& V : *Arr)
    {
      const TSharedPtr<FJsonObject> K = V->AsObject();
      FTransform T;
      FName BN;
      if (!BoneCS(K->GetStringField(TEXT("Bone")), K->HasField(TEXT("Name")) ? K->GetStringField(TEXT("Name")) : K->GetStringField(TEXT("Bone")), T, BN)) continue;
      const FVector Sz = JVec(K, TEXT("sizeCm"), false);
      FKBoxElem E(Sz.X, Sz.Y, Sz.Z);
      E.Center = JVec(K, TEXT("offsetCm"), bMirror);
      // mtl1-37: a box resolved from a rotated tpl_markup locator carries its edge directions (sizeCm order) in the bone frame.
      const TArray<TSharedPtr<FJsonValue>>* Ax = nullptr;
      if (K->TryGetArrayField(TEXT("axesUe"), Ax) && Ax->Num() == 3)
      {
        FVector A[3];
        for (int32 J = 0; J < 3; ++J)
        {
          const TArray<TSharedPtr<FJsonValue>>& C = (*Ax)[J]->AsArray();
          A[J] = C.Num() >= 3 ? FVector(C[0]->AsNumber(), C[1]->AsNumber(), C[2]->AsNumber()).GetSafeNormal() : FVector::ZeroVector;
        }
        if (FVector::DotProduct(FVector::CrossProduct(A[0], A[1]), A[2]) < 0) A[0] = -A[0]; // the axis convert mirrors X; a box is symmetric
        E.Rotation = FMatrix(A[0], A[1], A[2], FVector::ZeroVector).Rotator();
      }
      int32 In = 0;
      if (RestPV) // mtl1-33: boxes are not rest-fitted; count free rest particles that start inside (logged)
        for (int32 I = 0; I < RestPV->Num(); ++I)
        {
          if (RestMaxDist && RestMaxDist->IsValidIndex(I) && (*RestMaxDist)[I] <= 0.1f) continue;
          const FVector Q = E.Rotation.Quaternion().UnrotateVector(T.InverseTransformPosition(FVector((*RestPV)[I])) - E.Center).GetAbs() - FVector(Sz.X, Sz.Y, Sz.Z) * 0.5;
          In += (Q.X < 0 && Q.Y < 0 && Q.Z < 0) ? 1 : 0;
        }
      if (RejectRemap(K->GetStringField(TEXT("Bone")), BN, K->HasField(TEXT("Name")) ? K->GetStringField(TEXT("Name")) : BN.ToString(), In > 0)) continue;
      BoxRestInside += In;
      CollBody(PA, BN)->AggGeom.BoxElems.Add(E);
      ++N;
    }
  Log.Add(FString::Printf(TEXT("fitFloor=%.2f deepFrac=%.2f shallowFrac=%.3f floorSkipped=%d"), RestFitFloor, DeepFrac, ShallowFrac, FloorSkipped));
  Log.Add(FString::Printf(TEXT("%s: restParticles total=%d"), *Who, RestPV ? RestPV->Num() : 0)); // R97g particle count
  Log.Add(FString::Printf(TEXT("%s: collision shapes=%d missingBones=%d remapped=%d remapRejected=%d unnegated=%d restFitted=%d minRadiusScale=%.2f boxRestInside=%d tplLocator=%d deepIgnored=%d"), *Who, N, Missing, Remapped, Rejected, bMirror ? 1 : 0, Fitted, MinScale, BoxRestInside, FromLocator, DeepIgnored));
  if (RemapLog.Num()) Log.Add(FString::Printf(TEXT("%s: collision remap (HEURISTIC) %s"), *Who, *FString::Join(RemapLog, TEXT(" "))));
  if (MissLog.Num()) Log.Add(FString::Printf(TEXT("%s: collision skipped (bone not in mesh) %s"), *Who, *FString::Join(MissLog, TEXT(" "))));
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
  // mtl1-31 GENERIC: one collision PhysicsAsset PER CLOTH (the game's cdt shapes belong to one cloth config). A single PA shared
  // by all cloths made e.g. the tech_priest skirt collide with the tabards' 30 cm leg capsules (fitted only to the tabard rest
  // particles). Sidecar ue.chaosCloth.collisionPerCloth=false puts that cloth back on the shared PA (pre-mtl1-31 behaviour).
  UPhysicsAsset* SharedCollPA = nullptr;
  auto NewCollPA = [&](const FString& Suffix) {
    const FString Folder = FPackageName::GetLongPackagePath(Mesh->GetOutermost()->GetName()) / TEXT("ChaosCloth");
    const FString Name = FreeName(Folder, TEXT("PA_") + Mesh->GetName() + TEXT("_ClothCollision") + Suffix);
    UPackage* Pkg = CreatePackage(*(Folder / Name));
    UPhysicsAsset* PA = NewObject<UPhysicsAsset>(Pkg, *Name, RF_Public | RF_Standalone | RF_Transactional);
    PA->PreviewSkeletalMesh = Mesh;
    FAssetRegistryModule::AssetCreated(PA);
    PA->MarkPackageDirty();
    return PA;
  };

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
    TArray<FString> RenderNames;
    const TArray<TSharedPtr<FJsonValue>>* RV = nullptr;
    if (Cloth->TryGetArrayField(TEXT("RenderMeshes"), RV))
      for (const TSharedPtr<FJsonValue>& V : *RV)
        RenderNames.Add(V->AsString());
    auto IsRenderSlot = [&](const FSkelMeshSection& Sec)
    {
      if (!Mesh->GetMaterials().IsValidIndex(Sec.MaterialIndex))
        return false;
      const FString Slot = Mesh->GetMaterials()[Sec.MaterialIndex].MaterialSlotName.ToString();
      for (const FString& R : RenderNames)
        if (!R.IsEmpty() && (Slot.Equals(R, ESearchCase::IgnoreCase) || Slot.Contains(R, ESearchCase::IgnoreCase) || R.Contains(Slot, ESearchCase::IgnoreCase)))
          return true;
      return false;
    };
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
      // mtl1-28b: a render slot (RenderMeshes) is never the hidden _SIM shell, even when its vertices sit on the particles
      // (mtl15 cc_sorcerer skirt_REND: claimed as sim by vertex hits -> 0 render sections -> bone-based). Step 2b handles it.
      if (IsRenderSlot(Sec))
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
    // 2b. mtl1-28 GENERIC fallback: exports made without --include-cloth-sim have no _SIM section (mtl15 cc_sorcerer: 0 cloths,
    // physics.json identical to mirr15). Use the primary render section (most vertices) as its own sim mesh when its vertices
    // lie on the game's sim particles (>= 50% within 3 cm, HEURISTIC); MaxDistance still comes from the nearest sim particle.
    // Sidecar ue.chaosCloth.renderAsSim = false disables it.
    bool bRenderAsSim = false;
    if (SimSection == INDEX_NONE && RenderSections.Num() > 0)
    {
      bool bAllow = true;
      const TSharedPtr<FJsonObject>* UeR = nullptr;
      const TSharedPtr<FJsonObject>* CCR = nullptr;
      if (Cloth->TryGetObjectField(TEXT("ue"), UeR) && (*UeR)->TryGetObjectField(TEXT("chaosCloth"), CCR))
        (*CCR)->TryGetBoolField(TEXT("renderAsSim"), bAllow);
      int32 Best = INDEX_NONE;
      for (int32 S : RenderSections)
        if (Best == INDEX_NONE || Lod.Sections[S].SoftVertices.Num() > Lod.Sections[Best].SoftVertices.Num())
          Best = S;
      int32 Hit = 0;
      const FSkelMeshSection& BS = Lod.Sections[Best];
      for (const FSoftSkinVertex& V : BS.SoftVertices)
      {
        float D;
        Nearest(V.Position, D);
        Hit += D < 3.0f;
      }
      const float Ratio = BS.SoftVertices.Num() ? float(Hit) / BS.SoftVertices.Num() : 0.f;
      Log.Add(FString::Printf(TEXT("%s: no _SIM section; render-as-sim candidate section %d verts=%d hit3cm=%.2f allow=%d"), *SimName, Best,
        BS.SoftVertices.Num(), Ratio, bAllow ? 1 : 0));
      if (bAllow && Ratio >= 0.5f)
      {
        SimSection = Best;
        bRenderAsSim = true;
        const FName PrimarySlot = Mesh->GetMaterials()[BS.MaterialIndex].MaterialSlotName;
        RenderSlots.Remove(PrimarySlot);
        RenderSlots.Insert(PrimarySlot, 0); // primary first; mtl1-30: the other render slots get their own render-as-sim asset in step 6
        RenderSections = {Best};
      }
    }
    if (SimSection == INDEX_NONE || RenderSections.Num() == 0)
    {
      Log.Add(FString::Printf(TEXT("%s: sim section %d (slot hit %.2f), %d render sections -> stays bone-based"), *SimName, SimSection, SimHit, RenderSections.Num()));
      continue;
    }

    UPhysicsAsset* CollPA = nullptr;
    {
      bool bPerCloth = true;
      const TSharedPtr<FJsonObject>* UeP = nullptr;
      const TSharedPtr<FJsonObject>* CCP = nullptr;
      if (Cloth->TryGetObjectField(TEXT("ue"), UeP) && (*UeP)->TryGetObjectField(TEXT("chaosCloth"), CCP))
        (*CCP)->TryGetBoolField(TEXT("collisionPerCloth"), bPerCloth);
      CollPA = bPerCloth ? NewCollPA(TEXT("_") + SimName) : (SharedCollPA ? SharedCollPA : (SharedCollPA = NewCollPA(FString())));
    }
    // mtl1-30: steps 3-5b as a lambda so one cloth can make several clothing assets (UE binds one section per asset LOD).
    auto MakeAsset = [&](int32 Src, bool bRemoveSrc, const FString& Suffix, bool bAddCollision, int32& Pinned, float& WorstMatch) -> UClothingAssetCommon*
    {
      // 3. clothing asset from the sim section (the editor's Create Clothing Data from Section, section removed = disabled).
      UClothingAssetCommon* Asset = nullptr;
      {
        FScopedSkeletalMeshPostEditChange Outer(Mesh);
        if (bRemoveSrc)
        {
          FScopedSkeletalMeshPostEditChange Inner(Mesh);
          Mesh->RemoveMeshSection(0, Src);
        }
        FSkeletalMeshClothBuildParams Params;
        Params.AssetName = TEXT("SM2Cloth_") + SimName + Suffix;
        Params.LodIndex = 0;
        Params.SourceSection = Src;
        Params.bRemoveFromMesh = false; // already disabled above
        Asset = Cast<UClothingAssetCommon>(Factory->CreateFromSkeletalMesh(Mesh, Params));
        if (Asset)
          Mesh->AddClothingAsset(Asset);
      }
      SM2WaitMeshBuild(Mesh);
      if (!Asset || Asset->LodData.Num() == 0)
      {
        Log.Add(SimName + Suffix + TEXT(": factory failed"));
        return (UClothingAssetCommon*)nullptr;
      }

      // 4. MaxDistance from the game (maxDistanceMeters x100 per sim particle, nearest particle; 0 = pinned / kinematic).
      FClothLODDataCommon& CLod = Asset->LodData[0];
      const TArray<FVector3f>& PV = CLod.PhysicalMeshData.Vertices;
      FPointWeightMap& Mask = CLod.PointWeightMaps.AddDefaulted_GetRef();
      Mask.Initialize(PV.Num());
      Mask.Name = TEXT("SM2_MaxDistance");
      Mask.CurrentTarget = (uint8)EWeightMapTargetCommon::MaxDistance;
      Mask.bEnabled = true;
      Pinned = 0;
      WorstMatch = 0.0f;
      for (int32 I = 0; I < PV.Num(); ++I)
      {
        float D;
        const int32 N = Nearest(PV[I], D);
        WorstMatch = FMath::Max(WorstMatch, D);
        const float Cm = MaxV->IsValidIndex(N) ? (float)(*MaxV)[N]->AsNumber() * 100.0f : 0.0f;
        Mask.Values[I] = Cm;
        Pinned += Cm <= 0.0f;
      }
      const TArray<float> MaxCm = Mask.Values; // Mask ref is invalidated by the AddDefaulted below
      // 4b. mtl1-22: backstop (HEURISTIC, generic; not from the game). A free particle may sink at most BackstopCm behind its
      // skinned position along the animated normal (non-legacy Chaos backstop: sphere surface at Distance behind, radius R).
      // R = clamp(2 x MaxDistance, 5, 50) cm approximates a plane locally; pinned particles get 0 (off).
      // Sidecar ue.chaosCloth.backstopCm overrides the 1 cm default; < 0 disables.
      {
        const TSharedPtr<FJsonObject>* UeB = nullptr;
        const TSharedPtr<FJsonObject>* CCB = nullptr;
        double BackCm = 1.0;
        if (Cloth->TryGetObjectField(TEXT("ue"), UeB) && (*UeB)->TryGetObjectField(TEXT("chaosCloth"), CCB))
          (*CCB)->TryGetNumberField(TEXT("backstopCm"), BackCm);
        if (BackCm >= 0.0)
        {
          const TArray<float> MDv = MaxCm;
          CLod.PointWeightMaps.AddDefaulted(2);
          FPointWeightMap& BD = CLod.PointWeightMaps[CLod.PointWeightMaps.Num() - 2];
          BD.Initialize(PV.Num());
          BD.Name = TEXT("SM2_BackstopDistance");
          BD.CurrentTarget = (uint8)EWeightMapTargetCommon::BackstopDistance;
          BD.bEnabled = true;
          FPointWeightMap& BR = CLod.PointWeightMaps.Last();
          BR.Initialize(PV.Num());
          BR.Name = TEXT("SM2_BackstopRadius");
          BR.CurrentTarget = (uint8)EWeightMapTargetCommon::BackstopRadius;
          BR.bEnabled = true;
          for (int32 I = 0; I < PV.Num(); ++I)
          {
            const bool bFree = MDv[I] > 0.0f;
            BD.Values[I] = bFree ? (float)BackCm : 0.0f;
            BR.Values[I] = bFree ? FMath::Clamp(2.0f * MDv[I], 5.0f, 50.0f) : 0.0f;
          }
          Log.Add(FString::Printf(TEXT("%s: backstop %.2fcm on %d free particles"), *SimName, BackCm, PV.Num() - Pinned));
        }
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
        if ((bAddCollision && AddCollision(CollPA, Coll, Mesh->GetRefSkeleton(), RefCS, Log, SimName, &PV, &MaxCm, FMath::Max(2.0f, (float)Num(CC, TEXT("collisionThicknessCm"), 1))) > 0) || CollPA->SkeletalBodySetups.Num() > 0)
        {
          Asset->PhysicsAsset = CollPA;
          Asset->RefreshBoneMapping(Mesh);
        }
      }
      return Asset;
    };
    int32 Pinned = 0;
    float WorstMatch = 0.0f;
    // slot of the sim section, taken before any rebuild (section indices move); mtl1-30 extra assets re-find it by slot.
    const FName SimSlot = Mesh->GetMaterials().IsValidIndex(Lod.Sections[SimSection].MaterialIndex) ? Mesh->GetMaterials()[Lod.Sections[SimSection].MaterialIndex].MaterialSlotName : NAME_None;
    UClothingAssetCommon* Asset = MakeAsset(SimSection, !bRenderAsSim, FString(), true, Pinned, WorstMatch);
    if (!Asset)
      continue;
    const int32 NumParticles = Asset->LodData[0].PhysicalMeshData.Vertices.Num();
    // 6. bind to the render sections (mesh-to-mesh skinning, as the editor's Apply Clothing).
    // mtl1-15: one rebuild scope per section, indices re-resolved by slot each time (they move after every rebuild), and a
    // failed bind is cleaned off the section. UClothingAssetCommon::BindToSkeletalMesh maps ONE section per asset LOD, so a
    // second bind of the same asset fails (mtl1-15b: half-bound sections exploded the sorcerer mesh).
    // mtl1-30 GENERIC: every further render slot gets its OWN clothing asset instead of staying bone-skinned:
    //   - _SIM source: another asset built from the same sim section (identical sim, identical config), bound to that slot;
    //   - render-as-sim: the slot's own section, if >= 50% of its vertices lie within 3 cm of the game's sim particles.
    // Sidecar ue.chaosCloth.bindAllRenderSlots = false restores the old "primary slot only" behaviour.
    int32 Bound = 0;
    {
      TMap<FName, int32> NV;
      for (const FSkelMeshSection& Sx : Lod.Sections)
        if (Mesh->GetMaterials().IsValidIndex(Sx.MaterialIndex))
          NV.FindOrAdd(Mesh->GetMaterials()[Sx.MaterialIndex].MaterialSlotName) += Sx.SoftVertices.Num();
      if (!bRenderAsSim) // primary = the matching render section with the most vertices (render-as-sim: already first)
        RenderSlots.StableSort([&](const FName& A, const FName& B) { return NV.FindRef(A) > NV.FindRef(B); });
    }
    bool bAllSlots = true;
    {
      const TSharedPtr<FJsonObject>* UeA = nullptr;
      const TSharedPtr<FJsonObject>* CCA = nullptr;
      if (Cloth->TryGetObjectField(TEXT("ue"), UeA) && (*UeA)->TryGetObjectField(TEXT("chaosCloth"), CCA))
        (*CCA)->TryGetBoolField(TEXT("bindAllRenderSlots"), bAllSlots);
    }
    auto FindSlotSection = [&](const FName& SlotN, bool bAllowDisabled) {
      const FSkeletalMeshLODModel& LL = Model->LODModels[0];
      for (int32 S = 0; S < LL.Sections.Num(); ++S)
      {
        const FSkelMeshSection& Sec = LL.Sections[S];
        if ((!bAllowDisabled && Sec.bDisabled) || Sec.HasClothingData() || !Mesh->GetMaterials().IsValidIndex(Sec.MaterialIndex) ||
          Mesh->GetMaterials()[Sec.MaterialIndex].MaterialSlotName != SlotN)
          continue;
        return S;
      }
      return (int32)INDEX_NONE;
    };
    TArray<TPair<UClothingAssetCommon*, FName>> Jobs;
    Jobs.Add({Asset, RenderSlots[0]});
    for (int32 K = 1; K < RenderSlots.Num(); ++K)
    {
      const FName X = RenderSlots[K];
      if (!bAllSlots)
      {
        Log.Add(FString::Printf(TEXT("%s: extra render slot %s stays bone-skinned (bindAllRenderSlots=false)"), *SimName, *X.ToString()));
        continue;
      }
      int32 Src = INDEX_NONE;
      if (bRenderAsSim)
      {
        const int32 SX = FindSlotSection(X, false);
        if (SX != INDEX_NONE)
        {
          int32 Hit = 0;
          const FSkelMeshSection& XS = Model->LODModels[0].Sections[SX];
          TArray<float> Ds;
          for (const FSoftSkinVertex& V : XS.SoftVertices) { float D; Nearest(V.Position, D); Hit += D < 3.0f; Ds.Add(D); }
          Ds.Sort();
          const float Med = Ds.Num() ? Ds[Ds.Num() / 2] : 0.f;
          const float R = XS.SoftVertices.Num() ? float(Hit) / XS.SoftVertices.Num() : 0.f;
          // the game drives a render mesh from the sim mesh within simRemapConfig.maxInfluenceDist (m) of it
          const float InflCm = (float)Num(Cloth, TEXT("MaxInfluenceDist"), 0) * 100.0f;
          Log.Add(FString::Printf(TEXT("%s: extra slot %s render-as-sim section %d verts=%d hit3cm=%.2f medianCm=%.2f maxInfluenceCm=%.1f"), *SimName, *X.ToString(), SX,
            XS.SoftVertices.Num(), R, Med, InflCm));
          if (R >= 0.5f)
            Src = SX; // its own sim mesh
          else if (Med <= InflCm && RenderSlots.Num() > 0)
          {
            Src = FindSlotSection(RenderSlots[0], false); // driven by (a copy of) the primary sim mesh, as the game's sim remap
            Log.Add(FString::Printf(TEXT("%s: extra slot %s driven by the primary sim mesh (section %d)"), *SimName, *X.ToString(), Src));
          }
        }
      }
      else
        Src = SimSlot.IsNone() ? INDEX_NONE : FindSlotSection(SimSlot, true);
      if (Src == INDEX_NONE)
      {
        Log.Add(FString::Printf(TEXT("%s: extra render slot %s stays bone-skinned (no sim source)"), *SimName, *X.ToString()));
        continue;
      }
      int32 P2 = 0;
      float W2 = 0.f;
      if (UClothingAssetCommon* A2 = MakeAsset(Src, false, TEXT("_") + X.ToString(), false, P2, W2))
      {
        Jobs.Add({A2, X});
        Log.Add(FString::Printf(TEXT("%s: extra slot %s -> own asset %s particles=%d pinned=%d worstMatchCm=%.2f"), *SimName, *X.ToString(), *A2->GetName(),
          A2->LodData[0].PhysicalMeshData.Vertices.Num(), P2, W2));
      }
    }
    int32 Fail = 0;
    FString SlotLog;
    for (const TPair<UClothingAssetCommon*, FName>& J : Jobs)
    {
      UClothingAssetCommon* JA = J.Key;
      const FName SlotN = J.Value;
      SlotLog += SlotN.ToString() + TEXT(" ");
      int32 AssetIndex = INDEX_NONE;
      Mesh->GetMeshClothingAssets().Find(JA, AssetIndex);
      SM2WaitMeshBuild(Mesh);
      bool bOk = false;
      {
        FScopedSkeletalMeshPostEditChange Scope(Mesh);
        FSkeletalMeshLODModel& L6 = Model->LODModels[0];
        for (int32 S = 0; S < L6.Sections.Num(); ++S)
        {
          FSkelMeshSection& Sec = L6.Sections[S];
          if (Sec.bDisabled || Sec.HasClothingData() || !Mesh->GetMaterials().IsValidIndex(Sec.MaterialIndex) ||
            Mesh->GetMaterials()[Sec.MaterialIndex].MaterialSlotName != SlotN)
            continue;
          if (JA->BindToSkeletalMesh(Mesh, 0, S, 0))
          {
            FSkelMeshSourceSectionUserData& UD = L6.UserSectionsData.FindOrAdd(Sec.OriginalDataSectionIndex);
            UD.CorrespondClothAssetIndex = (int16)AssetIndex;
            UD.ClothingData.AssetGuid = JA->GetAssetGuid();
            UD.ClothingData.AssetLodIndex = 0;
            bOk = true;
          }
          else
          {
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
      Bound += bOk;
      Fail += !bOk;
      FBindRec R{JA, {SlotN}, SimName + (JA == Asset ? FString() : TEXT("/") + SlotN.ToString())};
      BindRecs.Add(R);
    }
    SM2WaitMeshBuild(Mesh);
    Log.Add(FString::Printf(TEXT("%s: slots=[%s] bound=%d failed=%d assets=%d"), *SimName, *SlotLog, Bound, Fail, Jobs.Num()));
    if (Bound > 0)
      Converted.Add(SimName);
    Log.Add(FString::Printf(TEXT("%s: simSection=%d%s particles=%d pinned=%d worstMatchCm=%.2f boundSections=%d/%d"), *SimName, SimSection,
      bRenderAsSim ? TEXT(" (render-as-sim)") : TEXT(""), NumParticles, Pinned, WorstMatch, Bound, RenderSlots.Num()));
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

int32 USM2PhysicsSetupLibrary::FixWorldPhysics(UPhysicsAsset* PA, UAnimBlueprint* ABP, FString& Report)
{
  TArray<UPhysicsAsset*> PAs;
  if (PA) PAs.AddUnique(PA);
  TArray<UAnimGraphNode_RigidBody*> Nodes;
  if (ABP)
    for (UEdGraph* G : ABP->FunctionGraphs)
      if (G)
        for (UEdGraphNode* N : G->Nodes)
          if (UAnimGraphNode_RigidBody* RB = Cast<UAnimGraphNode_RigidBody>(N))
          {
            Nodes.Add(RB);
            if (RB->Node.OverridePhysicsAsset) PAs.AddUnique(RB->Node.OverridePhysicsAsset.Get());
          }
  int32 Bodies = 0;
  for (UPhysicsAsset* P : PAs)
  {
    P->Modify();
    for (USkeletalBodySetup* B : P->SkeletalBodySetups) if (B) B->Modify();
    const int32 N = NormalizeBodies(P);
    Bodies += N;
    if (N) { P->RefreshPhysicsAssetChange(); P->MarkPackageDirty(); }
  }
  for (UAnimGraphNode_RigidBody* RB : Nodes) { RB->Modify(); RB->Node.bUseDefaultAsSimulated = true; }
  if (ABP && Nodes.Num()) { FKismetEditorUtilities::CompileBlueprint(ABP); ABP->MarkPackageDirty(); }
  Report = FString::Printf(TEXT("physicsAssets=%d bodiesChanged=%d rigidBodyNodes=%d compiled=%d"), PAs.Num(), Bodies, Nodes.Num(),
    ABP ? (ABP->Status != BS_Error ? 1 : 0) : -1);
  return Bodies;
}

int32 USM2PhysicsSetupLibrary::MeasureClothPenetration(USkeletalMeshComponent* Comp, float ThresholdCm, FString& Report, UPhysicsAsset* OverrideCollision)
{
  if (!Comp || !Comp->GetSkeletalMeshAsset())
  {
    Report = TEXT("no component");
    return -1;
  }
  TMap<int32, UPhysicsAsset*> AssetPA; // mtl1-31: each cloth is tested against ITS OWN collision PA (or the override)
  // mtl1-13b: pinned particles (MaxDistance ~0) follow the skin and cannot be pushed out; count them separately.
  TMap<int32, const TArray<float>*> MaxD;
  const TArray<UClothingAssetBase*>& CAs = Comp->GetSkeletalMeshAsset()->GetMeshClothingAssets();
  for (int32 Ai = 0; Ai < CAs.Num(); ++Ai)
    if (UClothingAssetCommon* C = Cast<UClothingAssetCommon>(CAs[Ai]))
    {
      if (C->PhysicsAsset || OverrideCollision)
        AssetPA.Add(Ai, OverrideCollision ? OverrideCollision : C->PhysicsAsset.Get());
      if (C->LodData.Num() > 0)
        if (const FPointWeightMap* M = C->LodData[0].PhysicalMeshData.FindWeightMap(EWeightMapTargetCommon::MaxDistance))
          MaxD.Add(Ai, &M->Values);
    }
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
    TArray<UPhysicsAsset*, TInlineAllocator<1>> PAs;
    if (UPhysicsAsset* const* PAk = AssetPA.Find(KV.Key))
      PAs.Add(*PAk);
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
  Report = FString::Printf(TEXT("particles=%d inside>%.1fcm=%d (free) +%d pinned worstFreeCm=%.2f nan=%d collisionPAs=%d"), Total, ThresholdCm, Inside, InsidePinned, Worst, NaN, [&] { TSet<UPhysicsAsset*> U; for (const auto& KV : AssetPA) U.Add(KV.Value); return U.Num(); }());
  return Inside;
}

// mtl1-33: shared body-skin helpers (HEURISTIC). Body = imported-model LOD0 sections that are enabled, carry no clothing data and
// whose material is not a cloth/disabled section's; linear-blend CPU skinning with the component's current bone transforms;
// a uniform hash grid (cell = MaxDistCm) makes the nearest-vertex query exact within MaxDistCm.
namespace SM2Skin
{
  FMatrix Blend(const TArray<FTransform>& CS, const TArray<FMatrix44f>& Inv, const int32* Bones, const float* W, int32 Num)
  {
    FMatrix M(ForceInitToZero);
    float Wsum = 0.f;
    for (int32 K = 0; K < Num; ++K)
    {
      const int32 B = Bones[K];
      if (W[K] <= 0.f || !CS.IsValidIndex(B) || !Inv.IsValidIndex(B)) continue;
      M += (FMatrix(Inv[B]) * CS[B].ToMatrixWithScale()) * W[K];
      Wsum += W[K];
    }
    if (Wsum <= 0.f) return FMatrix::Identity;
    M *= 1.0f / Wsum;
    return M;
  }
  struct FBody
  {
    TArray<FVector> P, N;
    TArray<int32> Sec;
    TMap<FIntVector, TArray<int32>> Grid;
    float Cell = 10.f;
    FIntVector Key(const FVector& Q) const { return FIntVector(FMath::FloorToInt(Q.X / Cell), FMath::FloorToInt(Q.Y / Cell), FMath::FloorToInt(Q.Z / Cell)); }
    int32 Nearest(const FVector& Q, double MaxD2) const
    {
      const FIntVector K0 = Key(Q);
      int32 Best = INDEX_NONE;
      double BestD2 = MaxD2;
      for (int32 X = -1; X <= 1; ++X) for (int32 Y = -1; Y <= 1; ++Y) for (int32 Z = -1; Z <= 1; ++Z)
        if (const TArray<int32>* C = Grid.Find(K0 + FIntVector(X, Y, Z)))
          for (int32 I : *C)
          {
            const double D2 = FVector::DistSquared(Q, P[I]);
            if (D2 < BestD2) { BestD2 = D2; Best = I; }
          }
      return Best;
    }
    // depth behind the surface (positive = inside) of the nearest vertex, or -1e9 when none within MaxDist
    float Depth(const FVector& Q, double MaxD2, int32* Hit = nullptr) const
    {
      const int32 I = Nearest(Q, MaxD2);
      if (Hit) *Hit = I;
      return I == INDEX_NONE ? -1e9f : -(float)FVector::DotProduct(Q - P[I], N[I]);
    }
  };
  void Build(USkeletalMeshComponent* Comp, const FSkeletalMeshLODModel& L, float Cell, FBody& Out)
  {
    USkeletalMesh* Mesh = Comp->GetSkeletalMeshAsset();
    const TArray<FTransform>& CS = Comp->GetComponentSpaceTransforms();
    const TArray<FMatrix44f>& Inv = Mesh->GetRefBasesInvMatrix();
    Out.Cell = FMath::Max(Cell, 1.0f);
    TSet<int32> ClothMats;
    for (const FSkelMeshSection& S : L.Sections)
      if (S.HasClothingData() || S.bDisabled) ClothMats.Add(S.MaterialIndex);
    for (int32 Si = 0; Si < L.Sections.Num(); ++Si)
    {
      const FSkelMeshSection& S = L.Sections[Si];
      if (S.bDisabled || S.HasClothingData() || ClothMats.Contains(S.MaterialIndex)) continue;
      for (const FSoftSkinVertex& V : S.SoftVertices)
      {
        int32 Bs[MAX_TOTAL_INFLUENCES]; float Ws[MAX_TOTAL_INFLUENCES];
        int32 Nb = 0;
        for (int32 K = 0; K < MAX_TOTAL_INFLUENCES; ++K)
          if (V.InfluenceWeights[K] > 0 && S.BoneMap.IsValidIndex(V.InfluenceBones[K])) { Bs[Nb] = S.BoneMap[V.InfluenceBones[K]]; Ws[Nb++] = V.InfluenceWeights[K] / 65535.f; }
        if (Nb == 0) continue;
        const FMatrix M = Blend(CS, Inv, Bs, Ws, Nb);
        const FVector P = M.TransformPosition(FVector(V.Position));
        Out.Grid.FindOrAdd(Out.Key(P)).Add(Out.P.Num());
        Out.P.Add(P);
        Out.N.Add(M.TransformVector(FVector(V.TangentZ.X, V.TangentZ.Y, V.TangentZ.Z)).GetSafeNormal());
        Out.Sec.Add(Si);
      }
    }
  }
  // component-space sim positions of one clothing data entry (same space pick as MeasureClothPenetration)
  void SimPositions(USkeletalMeshComponent* Comp, const FClothSimulData& D, TArray<FVector>& Out)
  {
    const FTransform CompTM = Comp->GetComponentTransform();
    FVector CA(0), CB(0);
    for (const FVector3f& P : D.Positions) { CA += D.Transform.TransformPosition(FVector(P)); CB += CompTM.TransformPosition(FVector(P)); }
    CA /= FMath::Max(1, D.Positions.Num()); CB /= FMath::Max(1, D.Positions.Num());
    const bool bUseA = FVector::Dist(CA, Comp->Bounds.Origin) <= FVector::Dist(CB, Comp->Bounds.Origin);
    Out.SetNum(D.Positions.Num());
    for (int32 I = 0; I < D.Positions.Num(); ++I)
      Out[I] = D.Positions[I].ContainsNaN() ? FVector(std::numeric_limits<double>::quiet_NaN()) : CompTM.InverseTransformPosition(bUseA ? D.Transform.TransformPosition(FVector(D.Positions[I])) : CompTM.TransformPosition(FVector(D.Positions[I])));
  }
  FString TopSlots(USkeletalMesh* Mesh, const FSkeletalMeshLODModel& L, TMap<int32, int32>& PerSec)
  {
    PerSec.ValueSort([](int32 A, int32 B) { return A > B; });
    FString Top;
    int32 N = 0;
    for (const auto& KV : PerSec)
    {
      if (N++ == 3) break;
      const int32 Mi = L.Sections[KV.Key].MaterialIndex;
      Top += FString::Printf(TEXT(" %s:%d"), Mesh->GetMaterials().IsValidIndex(Mi) ? *Mesh->GetMaterials()[Mi].MaterialSlotName.ToString() : TEXT("?"), KV.Value);
    }
    return Top;
  }
  FString PerCloth(const TArray<UClothingAssetBase*>& CAs, const TMap<int32, int32>& M)
  {
    FString PC;
    for (const auto& KV : M)
      PC += FString::Printf(TEXT(" %s:%d"), CAs.IsValidIndex(KV.Key) && CAs[KV.Key] ? *CAs[KV.Key]->GetName() : TEXT("?"), KV.Value);
    return PC;
  }
}

int32 USM2PhysicsSetupLibrary::MeasureClothSkinPenetration(USkeletalMeshComponent* Comp, float ThresholdCm, float MaxDistCm, FString& Report)
{
  // HEURISTIC metric (mtl1-32): a free particle counts as inside when dot(P - V, N) < -ThresholdCm for its nearest body vertex V
  // (normal N) within MaxDistCm. mtl1-33: same numbers, now via the SM2Skin grid.
  USkeletalMesh* Mesh = Comp ? Comp->GetSkeletalMeshAsset() : nullptr;
  if (!Mesh || !Mesh->GetImportedModel() || Mesh->GetImportedModel()->LODModels.Num() == 0)
  {
    Report = TEXT("no mesh");
    return -1;
  }
  const FSkeletalMeshLODModel& L = Mesh->GetImportedModel()->LODModels[0];
  SM2Skin::FBody Body;
  SM2Skin::Build(Comp, L, MaxDistCm, Body);
  TMap<int32, const TArray<float>*> MaxD;
  const TArray<UClothingAssetBase*>& CAs = Mesh->GetMeshClothingAssets();
  for (int32 Ai = 0; Ai < CAs.Num(); ++Ai)
    if (UClothingAssetCommon* C = Cast<UClothingAssetCommon>(CAs[Ai]))
      if (C->LodData.Num() > 0)
        if (const FPointWeightMap* Wm = C->LodData[0].PhysicalMeshData.FindWeightMap(EWeightMapTargetCommon::MaxDistance))
          MaxD.Add(Ai, &Wm->Values);
  const TMap<int32, FClothSimulData>& Data = Comp->GetCurrentClothingData_GameThread();
  int32 Total = 0, Free = 0, Near = 0, Inside = 0, InsidePinned = 0;
  float Worst = 0.f;
  TMap<int32, int32> PerSec, PerCloth;
  const double MaxD2 = (double)MaxDistCm * MaxDistCm;
  TArray<FVector> SP;
  for (const auto& KV : Data)
  {
    if (KV.Value.Positions.Num() == 0) continue;
    SM2Skin::SimPositions(Comp, KV.Value, SP);
    const TArray<float>* const* MV = MaxD.Find(KV.Key);
    for (int32 Pi = 0; Pi < SP.Num(); ++Pi)
    {
      ++Total;
      if (SP[Pi].ContainsNaN()) continue;
      const bool bPinned = MV && (*MV)->IsValidIndex(Pi) && (**MV)[Pi] < 0.1f;
      Free += !bPinned;
      int32 Hit = INDEX_NONE;
      const float Depth = Body.Depth(SP[Pi], MaxD2, &Hit);
      if (Hit == INDEX_NONE) continue;
      ++Near;
      if (Depth > ThresholdCm)
      {
        if (bPinned) { ++InsidePinned; continue; }
        ++Inside;
        PerSec.FindOrAdd(Body.Sec[Hit])++;
        PerCloth.FindOrAdd(KV.Key)++;
        Worst = FMath::Max(Worst, Depth);
      }
    }
  }
  Report = FString::Printf(TEXT("particles=%d free=%d nearBody=%d skinInside>%.1fcm=%d (free) +%d pinned worstFreeCm=%.2f bodyVerts=%d top:%s cloth:%s"),
    Total, Free, Near, ThresholdCm, Inside, InsidePinned, Worst, Body.P.Num(), *SM2Skin::TopSlots(Mesh, L, PerSec), *SM2Skin::PerCloth(CAs, PerCloth));
  return Inside;
}

int32 USM2PhysicsSetupLibrary::MeasureClothWrongSide(USkeletalMeshComponent* Comp, float ThresholdCm, float MaxDistCm, FString& Report)
{
  // HEURISTIC metric (mtl1-33): wrong side of a limb. For every free particle, A = its animated (skinned, unsimulated) position
  // from the clothing asset's physical mesh and bone weights, P = its simulated position. When neither end is inside the body
  // but a point on the segment A->P (1 cm steps) is more than ThresholdCm behind the body surface, the particle went AROUND or
  // THROUGH a body part to get where it is (e.g. a skirt pulled behind a leg), which the per-point skin metric cannot see.
  USkeletalMesh* Mesh = Comp ? Comp->GetSkeletalMeshAsset() : nullptr;
  if (!Mesh || !Mesh->GetImportedModel() || Mesh->GetImportedModel()->LODModels.Num() == 0)
  {
    Report = TEXT("no mesh");
    return -1;
  }
  const FSkeletalMeshLODModel& L = Mesh->GetImportedModel()->LODModels[0];
  SM2Skin::FBody Body;
  SM2Skin::Build(Comp, L, MaxDistCm, Body);
  const TArray<FTransform>& CS = Comp->GetComponentSpaceTransforms();
  const TArray<FMatrix44f>& Inv = Mesh->GetRefBasesInvMatrix();
  const TArray<UClothingAssetBase*>& CAs = Mesh->GetMeshClothingAssets();
  const TMap<int32, FClothSimulData>& Data = Comp->GetCurrentClothingData_GameThread();
  const double MaxD2 = (double)MaxDistCm * MaxDistCm;
  int32 Total = 0, Free = 0, Moved = 0, Wrong = 0, EndInside = 0, Mismatch = 0;
  float MeanMove = 0.f, WorstMove = 0.f;
  TMap<int32, int32> PerSec, PerCloth;
  TArray<FVector> SP;
  for (const auto& KV : Data)
  {
    UClothingAssetCommon* C = CAs.IsValidIndex(KV.Key) ? Cast<UClothingAssetCommon>(CAs[KV.Key]) : nullptr;
    if (!C || C->LodData.Num() == 0 || KV.Value.Positions.Num() == 0) continue;
    const FClothPhysicalMeshData& PM = C->LodData[0].PhysicalMeshData;
    if (PM.Vertices.Num() != KV.Value.Positions.Num() || PM.BoneData.Num() != PM.Vertices.Num()) { ++Mismatch; continue; }
    const FPointWeightMap* MV = PM.FindWeightMap(EWeightMapTargetCommon::MaxDistance);
    SM2Skin::SimPositions(Comp, KV.Value, SP);
    for (int32 Pi = 0; Pi < SP.Num(); ++Pi)
    {
      ++Total;
      if (SP[Pi].ContainsNaN() || (MV && MV->Values.IsValidIndex(Pi) && MV->Values[Pi] < 0.1f)) continue;
      ++Free;
      const FClothVertBoneData& BD = PM.BoneData[Pi];
      int32 Bs[MAX_TOTAL_INFLUENCES]; float Ws[MAX_TOTAL_INFLUENCES];
      int32 Nb = 0;
      for (int32 K = 0; K < FMath::Min(BD.NumInfluences, (int32)MAX_TOTAL_INFLUENCES); ++K)
        if (C->UsedBoneIndices.IsValidIndex(BD.BoneIndices[K])) { Bs[Nb] = C->UsedBoneIndices[BD.BoneIndices[K]]; Ws[Nb++] = BD.BoneWeights[K]; }
      if (Nb == 0) continue;
      const FVector A = SM2Skin::Blend(CS, Inv, Bs, Ws, Nb).TransformPosition(FVector(PM.Vertices[Pi]));
      const FVector& P = SP[Pi];
      const float Len = (float)FVector::Dist(A, P);
      MeanMove += Len;
      WorstMove = FMath::Max(WorstMove, Len);
      if (Len < 2.f * ThresholdCm) continue;
      ++Moved;
      if (Body.Depth(A, MaxD2) > ThresholdCm || Body.Depth(P, MaxD2) > ThresholdCm) { ++EndInside; continue; }
      const int32 Steps = FMath::Clamp(FMath::CeilToInt(Len), 2, 300);
      for (int32 St = 1; St < Steps; ++St)
      {
        int32 Hit = INDEX_NONE;
        if (Body.Depth(FMath::Lerp(A, P, (double)St / Steps), MaxD2, &Hit) > ThresholdCm)
        {
          ++Wrong;
          PerSec.FindOrAdd(Body.Sec[Hit])++;
          PerCloth.FindOrAdd(KV.Key)++;
          break;
        }
      }
    }
  }
  Report = FString::Printf(TEXT("particles=%d free=%d moved>=%.1fcm=%d wrongSide=%d endInside=%d meanMoveCm=%.2f worstMoveCm=%.1f mismatch=%d top:%s cloth:%s"),
    Total, Free, 2.f * ThresholdCm, Moved, Wrong, EndInside, Free ? MeanMove / Free : 0.f, WorstMove, Mismatch, *SM2Skin::TopSlots(Mesh, L, PerSec), *SM2Skin::PerCloth(CAs, PerCloth));
  return Wrong;
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

#include "Engine/Engine.h"
bool USM2PhysicsSetupLibrary::SetEngineFixedFrameRate(bool bEnable, float Fps)
{
  if (!GEngine) return false;
  const bool bPrev = GEngine->bUseFixedFrameRate;
  if (Fps > 0.f) GEngine->FixedFrameRate = Fps;
  GEngine->bUseFixedFrameRate = bEnable;
  return bPrev;
}

