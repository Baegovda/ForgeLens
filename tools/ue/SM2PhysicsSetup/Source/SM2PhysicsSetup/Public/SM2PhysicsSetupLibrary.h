#pragma once

#include "CoreMinimal.h"
#include "Kismet/BlueprintFunctionLibrary.h"
#include "SM2PhysicsSetupLibrary.generated.h"

class USkeletalMesh;

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
};
