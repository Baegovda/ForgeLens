# ue_r97_orb.py (R97h): Vortex orb from game data. The in-game orb is fx_vortex_beast_back_idle.sfx (npc_vortex_beast_client.cls:
# nameParent "DISK", isAutoStart True). Its main emitters (whirl_lines/whirl_core/core) are MESH particles of
# tpl/fx_vortex_beast_lwis (5 parts: circle_smoke, main_core, main_lines, circle_wave_in, main_cap; texture vfx_vortex_beast),
# constant 1-3/s, life 1 s = a persistent mesh. This imports that mesh + an additive unlit material (HEURISTIC colour/intensity,
# the sfx colour is per-emitter overrides not wired) and attaches it to bone DISK of R92_vortex in Map_Floor.
# The sprite emitters (spark_01 sparks 6-11/s, runes 66/s, warp 22/s) are NOT reproduced (no Niagara authoring API in Python).
# uerun.ps1 ue_r97_orb.py <lwis_export_dir> [intensity=20] [r,g,b=1,0.35,1]
import sys, unreal
src = sys.argv[1]; inten = float(sys.argv[2]) if len(sys.argv) > 2 else 20.0
col = [float(x) for x in (sys.argv[3] if len(sys.argv) > 3 else "1,0.35,1").split(",")]
D = "/Game/ForgeLens/SM2/VFX/vortex_orb"; EAL = unreal.EditorAssetLibrary; AT = unreal.AssetToolsHelpers.get_asset_tools(); MEL = unreal.MaterialEditingLibrary
import glob, os
fbx = glob.glob(os.path.join(src, "**", "fx_vortex_beast_lwis.fbx"), recursive=True)[0]; png = glob.glob(os.path.join(src, "**", "vfx_vortex_beast.png"), recursive=True)[0]
if not EAL.does_asset_exist(D + "/fx_vortex_beast_lwis"):
    t = unreal.AssetImportTask(); t.filename = fbx; t.destination_path = D; t.automated = True; t.save = True; t.replace_existing = True
    o = unreal.FbxImportUI(); o.import_mesh = True; o.import_as_skeletal = False; o.import_materials = False; o.import_textures = False
    o.mesh_type_to_import = unreal.FBXImportType.FBXIT_STATIC_MESH; o.static_mesh_import_data.combine_meshes = True; t.options = o
    t2 = unreal.AssetImportTask(); t2.filename = png; t2.destination_path = D; t2.automated = True; t2.save = True; t2.replace_existing = True
    AT.import_asset_tasks([t, t2])
mesh = unreal.load_asset(D + "/fx_vortex_beast_lwis"); tex = unreal.load_asset(D + "/vfx_vortex_beast")
mp = D + "/M_vortex_orb"
m = unreal.load_asset(mp) if EAL.does_asset_exist(mp) else AT.create_asset("M_vortex_orb", D, unreal.Material, unreal.MaterialFactoryNew())
MEL.delete_all_material_expressions(m)
m.set_editor_property("blend_mode", unreal.BlendMode.BLEND_ADDITIVE); m.set_editor_property("shading_model", unreal.MaterialShadingModel.MSM_UNLIT); m.set_editor_property("two_sided", True)
ts = MEL.create_material_expression(m, unreal.MaterialExpressionTextureSample, -700, 0); ts.set_editor_property("texture", tex)
pan = MEL.create_material_expression(m, unreal.MaterialExpressionRotator, -900, 0); pan.set_editor_property("speed", 0.15)
MEL.connect_material_expressions(pan, "", ts, "UVs")
c = MEL.create_material_expression(m, unreal.MaterialExpressionVectorParameter, -700, 250); c.set_editor_property("parameter_name", "OrbColor"); c.set_editor_property("default_value", unreal.LinearColor(col[0], col[1], col[2], 1))
i = MEL.create_material_expression(m, unreal.MaterialExpressionScalarParameter, -700, 400); i.set_editor_property("parameter_name", "OrbIntensity"); i.set_editor_property("default_value", inten)
a1 = MEL.create_material_expression(m, unreal.MaterialExpressionMultiply, -400, 0); MEL.connect_material_expressions(ts, "RGB", a1, "A"); MEL.connect_material_expressions(ts, "A", a1, "B")
a2 = MEL.create_material_expression(m, unreal.MaterialExpressionMultiply, -250, 100); MEL.connect_material_expressions(a1, "", a2, "A"); MEL.connect_material_expressions(c, "", a2, "B")
a3 = MEL.create_material_expression(m, unreal.MaterialExpressionMultiply, -100, 200); MEL.connect_material_expressions(a2, "", a3, "A"); MEL.connect_material_expressions(i, "", a3, "B")
MEL.connect_material_property(a3, "", unreal.MaterialProperty.MP_EMISSIVE_COLOR)
MEL.recompile_material(m); EAL.save_loaded_asset(m)
for k in range(len(mesh.static_materials)): mesh.set_material(k, m)
EAL.save_loaded_asset(mesh)
les = unreal.get_editor_subsystem(unreal.LevelEditorSubsystem); E = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
if "Map_Floor" not in unreal.EditorLevelLibrary.get_editor_world().get_path_name(): les.load_level("/Game/ForgeLens/SM2/Tests/r92_floor/Map_Floor")
vx = [a for a in E.get_all_level_actors() if a.get_actor_label() == "R92_vortex"][0]
for a in E.get_all_level_actors():
    if a.get_actor_label() == "R97_vortex_orb": E.destroy_actor(a)
sk = vx.skeletal_mesh_component; bones = [str(sk.get_bone_name(k)) for k in range(sk.get_num_bones())]
bone = "DISK" if "DISK" in bones else next((b for b in bones if b.lower() == "disk"), None)
orb = E.spawn_actor_from_object(mesh, sk.get_socket_location(bone) if bone else vx.get_actor_location()); orb.set_actor_label("R97_vortex_orb")
orb.static_mesh_component.set_editor_property("cast_shadow", False)
if bone: orb.attach_to_actor(vx, bone, unreal.AttachmentRule.SNAP_TO_TARGET, unreal.AttachmentRule.SNAP_TO_TARGET, unreal.AttachmentRule.KEEP_WORLD, False)
les.save_current_level()
print("R97ORB mesh", mesh.get_path_name(), "slots", len(mesh.static_materials), "bone", bone, "bounds", mesh.get_bounds().box_extent, "at", orb.get_actor_location(), "scale", orb.get_actor_scale3d())

