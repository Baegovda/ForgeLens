using UnrealBuildTool;

public class SM2PhysicsSetup : ModuleRules
{
  public SM2PhysicsSetup(ReadOnlyTargetRules Target) : base(Target)
  {
    PCHUsage = PCHUsageMode.UseExplicitOrSharedPCHs;
    PublicDependencyModuleNames.AddRange(new[] { "Core", "CoreUObject", "Engine" });
    PrivateDependencyModuleNames.AddRange(new[] { "UnrealEd", "AnimGraph", "AnimGraphRuntime", "BlueprintGraph", "Kismet", "Json", "AssetTools", "AssetRegistry", "PhysicsCore" });
  }
}
