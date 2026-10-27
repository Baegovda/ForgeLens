# mtl1-16 cloth solver tuning (in memory, not saved): cfgtune.py <mesh path> "Prop=Value;Prop=Value"
# e.g. "SubdivisionCount=2;IterationCount=3" (ChaosClothSharedSimConfig) or "CollisionThickness=3" (ChaosClothConfig).
import sys, unreal
m = unreal.load_asset(sys.argv[1])
r = unreal.SM2PhysicsSetupLibrary.set_cloth_config_props(m, sys.argv[2] if len(sys.argv) > 2 else "")
n, rep = r if isinstance(r, tuple) else (r, "")
print("CFGT", n, rep)
