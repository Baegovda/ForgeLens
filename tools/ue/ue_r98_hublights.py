# ue_r98_hublights.py (R98b): place the game's local lights into L_hub_battle_barge.
# Shared math lives in ue_level_lights_core.py / LevelLightUe.cs (ARGB PROVEN; K=10 HEURISTIC FIT).
# usage: <K> <fill: keep|off|N> [spotScale]
import unreal, sys, csv, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ue_level_lights_core as core

K = float(sys.argv[1]) if len(sys.argv) > 1 else core.INTENSITY_FIT_K
FILL = sys.argv[2] if len(sys.argv) > 2 else "keep"
SS = float(sys.argv[3]) if len(sys.argv) > 3 else 1.0
TSV = r"C:/Users/Revaptor_FX/Desktop/SM2_Work/Raw/level_lights/hub_battle_barge_lights.tsv"
LV = "/Game/SM2_Work/Tests/r54_level/L_hub_battle_barge"
les = unreal.get_editor_subsystem(unreal.LevelEditorSubsystem)
w = unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem).get_editor_world()
if w.get_path_name().split(".")[0] != LV:
    les.load_level(LV)
E = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
old = [a for a in E.get_all_level_actors() if "R98B" in [str(t) for t in a.tags]]
for a in old:
    E.destroy_actor(a)
n = 0
skipped = 0
with open(TSV, newline="", encoding="utf-8") as f:
    for r in csv.DictReader(f, delimiter="\t"):
        lt = r.get("lightType", "")
        if lt not in ("point", "spot"):
            continue
        try:
            params = core.light_row_to_ue(r, k=K, spot_scale=SS)
        except Exception:
            skipped += 1
            continue
        if not params:
            skipped += 1
            continue
        loc = params["location"]
        pl = E.spawn_actor_from_class(unreal.PointLight, unreal.Vector(loc[0], loc[1], loc[2]))
        pl.tags = ["R98B"]
        lc = pl.point_light_component
        lc.set_mobility(unreal.ComponentMobility.MOVABLE)
        lc.set_intensity(params["intensity"])
        lc.set_attenuation_radius(params["attenuation_radius"])
        cr, cg, cb = params["color_linear"]
        lc.set_light_color(unreal.LinearColor(cr, cg, cb, 1), True)
        lc.set_editor_property("cast_shadows", False)
        n += 1
for a in E.get_all_level_actors():
    if isinstance(a, unreal.DirectionalLight) and a.get_actor_label() != "DirectionalLight":
        if FILL == "off":
            a.light_component.set_intensity(0.0)
        elif FILL != "keep":
            a.light_component.set_intensity(float(FILL))
        print("R98B fill", a.get_actor_label(), a.light_component.intensity)
les.save_current_level()
print("R98B placed", n, "skipped", skipped, "K", K, "fill", FILL, "evidence", core.INTENSITY_EVIDENCE)
