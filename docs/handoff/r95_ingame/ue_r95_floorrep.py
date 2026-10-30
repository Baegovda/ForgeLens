import unreal, sys, builtins
mode=sys.argv[1]; out='C:/Users/Revaptor_FX/Desktop/SM2_Work/Shots/r95_ingame/'
E=unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
def clean():
    for a in E.get_all_level_actors():
        if 'R95Cap' in [str(t) for t in a.tags]: E.destroy_actor(a)
def pp(s, ev):
    s.set_editor_property("override_auto_exposure_method", True); s.set_editor_property("auto_exposure_method", unreal.AutoExposureMethod.AEM_MANUAL)
    s.set_editor_property("override_auto_exposure_bias", True); s.set_editor_property("auto_exposure_bias", ev)
    s.set_editor_property("override_auto_exposure_apply_physical_camera_exposure", True); s.set_editor_property("auto_exposure_apply_physical_camera_exposure", False)
if mode=='capture':
    clean(); evs=[float(x) for x in sys.argv[2].split('/')]; O=unreal.Vector(0,40000,0)
    fl=E.spawn_actor_from_object(unreal.load_asset("/Engine/BasicShapes/Plane"),O); fl.tags=['R95Cap']; fl.set_actor_scale3d(unreal.Vector(40,40,1)); fl.static_mesh_component.set_material(0,unreal.load_asset("/Game/SM2_Work/Tests/r72_mats/M_r72_grey"))
    mesh=unreal.load_asset('/Game/SM2_Work/Characters/cc_titus_assault/cc_titus_assault'); ext=mesh.get_bounds().box_extent; w=max(ext.x,ext.y)
    a=E.spawn_actor_from_class(unreal.SkeletalMeshActor,O,unreal.Rotator(0,0,0)); a.tags=['R95Cap']; a.skeletal_mesh_component.set_skinned_asset_and_update(mesh)
    pl=E.spawn_actor_from_class(unreal.PointLight,O+unreal.Vector(0,max(600,w*2),ext.z*1.5)); pl.tags=['R95Cap']; pl.point_light_component.set_intensity(3000); pl.point_light_component.set_attenuation_radius(max(3000,w*6))
    import math; fov=35.0; dist=max(ext.z*2*0.6,w*0.75)/math.tan(math.radians(fov/2))+100
    cp=O+unreal.Vector(dist*0.35,dist,ext.z*1.3); tg=O+unreal.Vector(0,0,ext.z*0.8); res=[]
    for ev in evs:
        cap=E.spawn_actor_from_class(unreal.SceneCapture2D,cp,unreal.MathLibrary.find_look_at_rotation(cp,tg)); cap.tags=['R95Cap']
        cc=cap.capture_component2d; cc.capture_source=unreal.SceneCaptureSource.SCS_FINAL_COLOR_LDR; cc.fov_angle=fov
        f=unreal.EngineShowFlagsSetting(); f.set_editor_property("show_flag_name","EyeAdaptation"); f.set_editor_property("enabled",True); cc.set_editor_property("show_flag_settings",[f])
        ps=cc.get_editor_property("post_process_settings"); pp(ps,ev); cc.set_editor_property("post_process_settings",ps); cc.set_editor_property("post_process_blend_weight",1.0)
        rt=unreal.RenderingLibrary.create_render_target2d(unreal.EditorLevelLibrary.get_editor_world(),900,1000,unreal.TextureRenderTargetFormat.RTF_RGBA8_SRGB); cc.texture_target=rt; cc.capture_every_frame=False
        unreal.AutomationLibrary.finish_loading_before_screenshot(); cc.capture_scene(); cc.capture_scene(); res.append((ev,rt))
    builtins._r95f=res; print('ok')
else:
    for ev,rt in builtins._r95f: unreal.RenderingLibrary.export_render_target(unreal.EditorLevelLibrary.get_editor_world(),rt,out,'floorrep_ev%s.png'%ev)
    clean(); print('exported')
