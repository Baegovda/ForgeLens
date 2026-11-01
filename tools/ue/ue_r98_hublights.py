# ue_r98_hublights.py: place the game's local lights into L_hub_battle_barge.
# Shared math: ue_level_lights_core.py / LevelLightUe.cs
# PROVEN packer FUN_14224ecf0 + P0.w FUN_14224f010; UE unit bridge INFERRED (no FIT K=10).
# Spots stay SpotLight with cones. usage: [fill: keep|off|N] [spotScale]
import unreal, sys, csv, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ue_level_lights_core as core

FILL = sys.argv[1] if len(sys.argv) > 1 else "keep"
SS = float(sys.argv[2]) if len(sys.argv) > 2 else 1.0
# Back-compat: old CLI was <K> <fill> [spotScale] — if argv1 looks numeric, treat as ignored K and shift.
if len(sys.argv) > 1:
    try:
        float(sys.argv[1])
        FILL = sys.argv[2] if len(sys.argv) > 2 else "keep"
        SS = float(sys.argv[3]) if len(sys.argv) > 3 else 1.0
    except ValueError:
        pass
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
n_spot = 0
skipped = 0
with open(TSV, newline="", encoding="utf-8") as f:
    for r in csv.DictReader(f, delimiter="\t"):
        lt = r.get("lightType", "")
        if lt not in ("point", "spot"):
            continue
        try:
            params = core.light_row_to_ue(r, spot_scale=SS)
        except Exception:
            skipped += 1
            continue
        if not params:
            skipped += 1
            continue
        loc = params["location"]
        cr, cg, cb = params["color_linear"]
        if params["lightType"] == "spot":
            pl = E.spawn_actor_from_class(unreal.SpotLight, unreal.Vector(loc[0], loc[1], loc[2]))
            lc = pl.spot_light_component
            try:
                lc.set_outer_cone_angle(float(params["outer_cone_degrees"]))
                lc.set_inner_cone_angle(float(params["inner_cone_degrees"]))
            except Exception:
                lc.set_editor_property("outer_cone_angle", float(params["outer_cone_degrees"]))
                lc.set_editor_property("inner_cone_angle", float(params["inner_cone_degrees"]))
            n_spot += 1
        else:
            pl = E.spawn_actor_from_class(unreal.PointLight, unreal.Vector(loc[0], loc[1], loc[2]))
            lc = pl.point_light_component
        pl.tags = ["R98B"]
        lc.set_mobility(unreal.ComponentMobility.MOVABLE)
        lc.set_intensity(params["intensity"])
        lc.set_attenuation_radius(params["attenuation_radius"])
        try:
            lc.set_editor_property("use_inverse_squared_falloff", bool(params["use_inverse_squared"]))
        except Exception:
            pass
        if not params["use_inverse_squared"]:
            try:
                lc.set_editor_property("light_falloff_exponent", float(params["falloff_exponent"]))
            except Exception:
                pass
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
print("R98B placed", n, "spots", n_spot, "skipped", skipped, "fill", FILL, "evidence", core.INTENSITY_EVIDENCE)
