# ue_r98_hublights.py (R98b): place the game's local lights (Raw/level_lights/hub_battle_barge_lights.tsv: 760 point + 1064 spot)
# into L_hub_battle_barge as unshadowed movable PointLights (tag R98B). Spots have no direction in the TSV -> placed as points.
# Position: SM2 metres (x,y,z) -> UE cm (x*100, z*100, y*100) (same mapping as ue_vfx_frame/ue_r66_level).
# Colour ColorCtrl = [A,R,G,B] (PROVEN R97y). UE intensity (cd) = K * Intensity; attenuation radius = RadiusCtrl * 100 (fallback 1000).
# usage: <K> <fill: keep|off> [spotScale]
import unreal, sys, csv
K=float(sys.argv[1]); FILL=sys.argv[2] if len(sys.argv)>2 else "keep"; SS=float(sys.argv[3]) if len(sys.argv)>3 else 1.0
TSV=r"C:/Users/Revaptor_FX/Desktop/SM2_Work/Raw/level_lights/hub_battle_barge_lights.tsv"
LV="/Game/SM2_Work/Tests/r54_level/L_hub_battle_barge"
les=unreal.get_editor_subsystem(unreal.LevelEditorSubsystem)
w=unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem).get_editor_world()
if w.get_path_name().split(".")[0]!=LV: les.load_level(LV)
E=unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
old=[a for a in E.get_all_level_actors() if "R98B" in [str(t) for t in a.tags]]
for a in old: E.destroy_actor(a)
n=0; skipped=0
with open(TSV,newline="",encoding="utf-8") as f:
    for r in csv.DictReader(f,delimiter="\t"):
        lt=r.get("lightType","")
        if lt not in ("point","spot"): continue
        try:
            x,y,z=float(r["x"]),float(r["y"]),float(r["z"]); I=float(r["Intensity"] or 0)
            c=[float(v) for v in r["ColorCtrl"].strip("[]").split(",")]
        except Exception: skipped+=1; continue
        if I<=0: skipped+=1; continue
        rad=float(r["RadiusCtrl"] or 0) or 10.0
        pl=E.spawn_actor_from_class(unreal.PointLight,unreal.Vector(x*100,z*100,y*100)); pl.tags=["R98B"]
        lc=pl.point_light_component; lc.set_mobility(unreal.ComponentMobility.MOVABLE)
        lc.set_intensity(K*I*(SS if lt=="spot" else 1.0)); lc.set_attenuation_radius(rad*100)
        lc.set_light_color(unreal.LinearColor(c[1]/255.0,c[2]/255.0,c[3]/255.0,1),True)
        lc.set_editor_property("cast_shadows",False)
        n+=1
for a in E.get_all_level_actors():
    if isinstance(a,unreal.DirectionalLight) and a.get_actor_label()!="DirectionalLight":
        if FILL=="off": a.light_component.set_intensity(0.0)
        elif FILL!="keep": a.light_component.set_intensity(float(FILL))
        print("R98B fill",a.get_actor_label(),a.light_component.intensity)
les.save_current_level(); print("R98B placed",n,"skipped",skipped,"K",K,"fill",FILL)
