import unreal, sys, builtins
mode=sys.argv[1]; out='C:/Users/Revaptor_FX/Desktop/SM2_Work/Shots/r95_ingame/'
eas=unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
def clean():
    for a in eas.get_all_level_actors():
        if 'R95Cap' in [str(t) for t in a.tags]: eas.destroy_actor(a)
if mode=='capture':
    key=float(sys.argv[2]); rim=float(sys.argv[3]); amb=float(sys.argv[4]); ev=float(sys.argv[5]); tag=sys.argv[6]; yaw=float(sys.argv[7]) if len(sys.argv)>7 else 0
    clean()
    mesh=unreal.load_asset('/Game/SM2_Work/Characters/cc_titus_assault/cc_titus_assault')
    loc=unreal.Vector(0,20000,0)
    sk=eas.spawn_actor_from_class(unreal.SkeletalMeshActor,loc,unreal.Rotator(0,0,yaw)); sk.tags=['R95Cap']
    sk.skeletal_mesh_component.set_skeletal_mesh_asset(mesh)
    b=mesh.get_bounds(); h=b.box_extent.z*2; c=loc+b.origin
    def pl(off,col,inten):
        l=eas.spawn_actor_from_class(unreal.PointLight,c+off,unreal.Rotator(0,0,0)); l.tags=['R95Cap']
        lc=l.point_light_component; lc.set_intensity(inten); lc.set_light_color(col); lc.set_attenuation_radius(h*4); return l
    pl(unreal.Vector(-h*0.8,h*0.9,h*0.4),unreal.LinearColor(1.0,0.75,0.45,1),key)   # warm candle key, front-left
    pl(unreal.Vector(h*0.9,-h*0.6,h*0.2),unreal.LinearColor(0.3,1.0,0.5,1),rim)     # green rim (holo table), back-right
    pl(unreal.Vector(0,h*1.2,-h*0.3),unreal.LinearColor(0.35,0.55,0.6,1),amb)       # teal fill
    cap=eas.spawn_actor_from_class(unreal.SceneCapture2D,c+unreal.Vector(0,h*1.6,h*0.05),unreal.Rotator(0,0,-90)); cap.tags=['R95Cap']
    cc=cap.capture_component2d; cc.capture_source=unreal.SceneCaptureSource.SCS_FINAL_COLOR_LDR; cc.fov_angle=40
    pp=cc.post_process_settings; pp.override_auto_exposure_method=True; pp.auto_exposure_method=unreal.AutoExposureMethod.AEM_MANUAL; pp.override_auto_exposure_bias=True; pp.auto_exposure_bias=ev; cc.post_process_settings=pp
    rt=unreal.RenderingLibrary.create_render_target2d(unreal.EditorLevelLibrary.get_editor_world(),900,1000,unreal.TextureRenderTargetFormat.RTF_RGBA8)
    cc.texture_target=rt; cc.capture_every_frame=False
    unreal.AutomationLibrary.finish_loading_before_screenshot()
    cc.capture_scene(); builtins._r95c=(tag,rt); print('ok',h)
else:
    tag,rt=builtins._r95c
    unreal.RenderingLibrary.export_render_target(unreal.EditorLevelLibrary.get_editor_world(),rt,out,'ue_chapel_%s.png'%tag); clean(); print('exported',tag)
