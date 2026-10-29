#pragma once

#include "CoreMinimal.h"
#include "Kismet/BlueprintFunctionLibrary.h"
#include "SM2PhysicsSetupLibrary.generated.h"

class USkeletalMesh;
class UAnimBlueprint;

class UPhysicsAsset;
class USkeletalMeshComponent;
UCLASS()
class SM2PHYSICSSETUP_API USM2PhysicsSetupLibrary : public UBlueprintFunctionLibrary
{
  GENERATED_BODY()

public:
  /**
   * Reads an SM2 {model}.physics.json and builds, next to the mesh (or in DestFolder):
   *  PA_{mesh}_SM2: one small sphere body per cloth helper bone ({SIM}_physNN) + kinematic anchor bodies,
   *                 constraints chaining each helper to its nearest helper closer to the anchor (HEURISTIC, see .cpp);
   *  ABP_{mesh}_SM2Phys: post-process AnimBP  InputPose -> LocalToComponent -> RigidBody -> ComponentToLocal -> Output.
   * Assigns both to the mesh (does not save; the caller saves). Never replaces existing assets: a free _N name is used.
   */
  UFUNCTION(BlueprintCallable, Category = "SM2")
  static bool SetupFromJson(USkeletalMesh* Mesh, const FString& JsonPath, const FString& DestFolder, FString& Report);

  /** mtl1-9: SetupFromJson, but cloths whose simMeshName is in SkipCloths get no helper bodies (they run as Chaos Cloth instead). */
  UFUNCTION(BlueprintCallable, Category = "SM2")
  static bool SetupFromJsonEx(USkeletalMesh* Mesh, const FString& JsonPath, const FString& DestFolder, const TArray<FString>& SkipCloths, FString& Report);

  /**
   * mtl1-9: edit the RigidBody node of an ABP made by SetupFromJson and recompile. SimSpace 0 = Component, 1 = World, 2 = BaseBone.
   * Lets the SimSpace limits be tuned from Python without rebuilding the plugin.
   */
  UFUNCTION(BlueprintCallable, Category = "SM2")
  static bool TuneRigidBody(UAnimBlueprint* ABP, int32 SimSpace, FName BaseBone, float WorldAlpha, float DampingAlpha, float MaxLinearVelocity,
    float MaxLinearAcceleration, float MaxAngularVelocity, float MaxAngularAcceleration, FString& Report);

  /** Angular SLERP drive toward the rest pose on every constraint (fights the sag of long cloth chains in bone mode), plus swing-limit scale. Saves nothing; caller saves. */
  UFUNCTION(BlueprintCallable, Category = "SM2|Physics")
  static bool TuneConstraintDrive(UPhysicsAsset* PA, float Stiffness, float Damping, float SwingScale, FString& Report);

  /** Diagnostic: body type and every constraint touching Bone (limits, drive, anchor distance in the rest pose of Mesh). */
  UFUNCTION(BlueprintCallable, Category = "SM2|Physics")
  static FString DescribeBody(UPhysicsAsset* PA, USkeletalMesh* Mesh, FName Bone);

  /** Diagnostic: gravity on/off for every simulated body. Caller saves. */
  UFUNCTION(BlueprintCallable, Category = "SM2|Physics")
  static int32 SetSimGravity(UPhysicsAsset* PA, bool bEnable);

  /**
   * phys52 migration for assets made before phys52: every body of PA (and of each RigidBody node's override PA in ABP)
   * gets CollisionTraceFlag=UseSimpleAsComplex and PhysType_Simulated -> Default; every RigidBody node in ABP gets
   * bUseDefaultAsSimulated, then ABP is compiled. Stops the component's world-scene double simulation and the PIE
   * "ComplexAsSimple collision" warnings. Edits in place: the caller backs the assets up first and saves.
   */
  UFUNCTION(BlueprintCallable, Category = "SM2|Physics")
  static int32 FixWorldPhysics(UPhysicsAsset* PA, UAnimBlueprint* ABP, FString& Report);

  /**
   * mtl1-9 Chaos Cloth option: for every cloth in {model}.physics.json, finds the LOD0 section that is the game _SIM mesh
   * (positions match simPositionsUeCm), builds a Clothing Asset from it (section disabled, as the editor's
   * "Create Clothing Data from Section"), paints MaxDistance from maxDistanceMeters (0 = pinned), sets the ChaosClothConfig
   * from ue.chaosCloth and binds it to the render sections named in renderMeshes (material slot names).
   * Modifies Mesh (call it on a duplicate). Converted lists the simMeshNames that became cloth; the rest stay bone-based.
   */
  UFUNCTION(BlueprintCallable, Category = "SM2")
  static bool CreateChaosCloth(USkeletalMesh* Mesh, const FString& JsonPath, TArray<FString>& Converted, FString& Report);

  /** mtl1-13 verification: cloth particles (current sim, game thread) inside the clothing assets' collision bodies by more than ThresholdCm. Returns the count. */
  /** mtl1-13b: set (or clear with nullptr) the collision PhysicsAsset on every Clothing Asset of Mesh; returns the previous one. */
  UFUNCTION(BlueprintCallable, Category = "SM2")
  static UPhysicsAsset* SetClothCollision(USkeletalMesh* Mesh, UPhysicsAsset* Collision);

  /** mtl1-16: set reflected properties on every cloth config (ChaosClothConfig, ChaosClothSharedSimConfig, ...) of every
   *  clothing asset of Mesh. KeyValues = "Prop=Value;Prop=Value" (UE text format). Only configs that have the property are
   *  touched. Returns the number of properties set; Report lists old->new. Does not save the mesh. */
  UFUNCTION(BlueprintCallable, Category = "SM2")
  static int32 SetClothConfigProps(USkeletalMesh* Mesh, const FString& KeyValues, FString& Report);

  UFUNCTION(BlueprintCallable, Category = "SM2")
  static int32 MeasureClothPenetration(USkeletalMeshComponent* Comp, float ThresholdCm, FString& Report, UPhysicsAsset* OverrideCollision = nullptr);

  // mtl1-32: free cloth particles behind the CPU-skinned body surface (non-cloth, enabled sections of the same mesh) by more
  // than ThresholdCm along the nearest body vertex normal, ignoring particles farther than MaxDistCm from any body vertex.
  UFUNCTION(BlueprintCallable, Category = "SM2")
  static int32 MeasureClothSkinPenetration(USkeletalMeshComponent* Comp, float ThresholdCm, float MaxDistCm, FString& Report);

  // mtl1-33 (HEURISTIC): free particles whose path from the animated (skinned) position to the simulated position passes more
  // than ThresholdCm behind the body surface while both ends are outside = cloth on the wrong side of a limb.
  UFUNCTION(BlueprintCallable, Category = "SM2")
  static int32 MeasureClothWrongSide(USkeletalMeshComponent* Comp, float ThresholdCm, float MaxDistCm, FString& Report);
};
