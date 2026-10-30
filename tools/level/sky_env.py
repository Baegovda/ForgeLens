# R80 (t172u/R75): game sky + env lighting for the hub_battle_barge UE copy (/Game/ForgeLens/SM2/Tests/r54_level). Run in the editor via uerun.ps1.
#   state                      -> print sky/fog/sun/skylight/atmosphere actors and settings
#   import <hdr>               -> import the decoded long-lat HDR (bc6hdr, BC6H UF16 -> RGBE) as TextureCube /Game/ForgeLens/SM2/Tests/r54_level/Sky/T_buttle_barge_sky_1
#   apply [skyInt] [fogScale]  -> SkyLight = specified cubemap (angle -44.33, game intensity 8 * skyInt), SkyAtmosphere hidden,
#                                 fog = game hbbm_fog_v2 colour (40,50,83)/255, sun colour (255,236,196), R78 PPV exposure; tags R78Env
#   revert                     -> undo apply (R64 DaylightAmbientCubemap skylight, atmosphere visible, R66 fog)
# Values from game presets (PROVEN read): sky intensity 8, turn -44.33, sun [255,236,196] x150, fog colour [40,50,83] x15, ExposureScale 0.55, WhitePoint 10.
# Mapping game units -> UE units is HEURISTIC (scales are arguments).
import sys, unreal
eas = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
mode = sys.argv[1] if len(sys.argv) > 1 else "state"
CUBE = "/Game/ForgeLens/SM2/Tests/r54_level/Sky/T_buttle_barge_sky_1"
def acts(cls): return [a for a in eas.get_all_level_actors() if isinstance(a, cls)]
if mode == "state":
    print("R78 level", unreal.EditorLevelLibrary.get_editor_world().get_name())
    for a in acts(unreal.SkyLight):
        k = a.light_component; c = k.get_editor_property("cubemap")
        print("R78 skylight", a.get_name(), k.get_editor_property("source_type"), c.get_path_name() if c else None, k.get_editor_property("intensity"), k.get_editor_property("source_cubemap_angle"))
    for a in acts(unreal.DirectionalLight): print("R78 sun", a.get_name(), a.get_actor_rotation(), a.light_component.get_editor_property("intensity"), a.light_component.get_editor_property("light_color"))
    for a in acts(unreal.ExponentialHeightFog): f = a.component; print("R78 fog", a.get_name(), f.get_editor_property("fog_density"), f.get_editor_property("fog_inscattering_luminance"))
    for a in acts(unreal.SkyAtmosphere): print("R78 atmosphere", a.get_name(), a.is_hidden_ed(), a.get_editor_property("root_component").is_visible())
    for a in acts(unreal.PostProcessVolume): print("R78 ppv", a.get_name(), a.get_editor_property("unbound"), [str(t) for t in a.tags])
    print("R78 cube exists", unreal.EditorAssetLibrary.does_asset_exist(CUBE))
elif mode == "import":
    t = unreal.AssetImportTask(); t.filename = sys.argv[2]; t.destination_path = CUBE.rsplit("/", 1)[0]; t.destination_name = CUBE.rsplit("/", 1)[1]
    t.automated = True; t.replace_existing = True; t.save = True
    unreal.AssetToolsHelpers.get_asset_tools().import_asset_tasks([t])
    a = unreal.load_asset(CUBE); print("R78 imported", type(a).__name__ if a else None, t.imported_object_paths)
elif mode == "apply":
    si = float(sys.argv[2]) if len(sys.argv) > 2 else 1.0; fs = float(sys.argv[3]) if len(sys.argv) > 3 else 1.0
    cube = unreal.load_asset(CUBE)
    for a in acts(unreal.SkyLight):
        k = a.light_component; k.set_editor_property("source_type", unreal.SkyLightSourceType.SLS_SPECIFIED_CUBEMAP); k.set_editor_property("cubemap", cube)
        k.set_editor_property("source_cubemap_angle", -44.33); k.set_editor_property("intensity", 8.0 * si); k.set_light_color(unreal.LinearColor(1, 1, 1, 1)); k.recapture_sky()
        print("R78 skylight", a.get_name(), "cube", cube.get_name() if cube else None, "int", 8.0 * si)
    for a in acts(unreal.SkyAtmosphere): a.set_is_temporarily_hidden_in_editor(True); a.set_actor_hidden_in_game(True); a.root_component.set_visibility(False, True); print("R78 atmosphere hidden", a.get_name())
    for a in acts(unreal.ExponentialHeightFog):
        f = a.component; f.set_editor_property("fog_inscattering_luminance", unreal.LinearColor(40 / 255 * 0.25 * fs, 50 / 255 * 0.25 * fs, 83 / 255 * 0.25 * fs, 1))
        f.set_editor_property("fog_density", 0.00005 * fs); print("R78 fog", a.get_name())
    for a in acts(unreal.DirectionalLight): a.light_component.set_light_color(unreal.LinearColor(1.0, 236 / 255, 196 / 255, 1)); print("R78 sun colour", a.get_name())
    print("R78 applied si", si, "fs", fs)
elif mode == "revert":
    cube = unreal.load_asset("/Engine/MapTemplates/Sky/DaylightAmbientCubemap")
    for a in acts(unreal.SkyLight):
        k = a.light_component; k.set_editor_property("cubemap", cube); k.set_editor_property("source_cubemap_angle", 0.0); k.set_editor_property("intensity", 1.0); k.recapture_sky()
    for a in acts(unreal.SkyAtmosphere): a.set_is_temporarily_hidden_in_editor(False); a.set_actor_hidden_in_game(False); a.root_component.set_visibility(True, True)
    for a in acts(unreal.ExponentialHeightFog): f = a.component; f.set_editor_property("fog_density", 0.0003); f.set_editor_property("fog_inscattering_luminance", unreal.LinearColor(0.05, 0.07, 0.12, 1))
    for a in acts(unreal.DirectionalLight): a.light_component.set_light_color(unreal.LinearColor(0.9, 0.95, 1.0, 1))
    print("R78 reverted")
