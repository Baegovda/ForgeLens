import unreal, sys, builtins
mode=sys.argv[1]; out='C:/Users/Revaptor_FX/Desktop/SM2_Work/Shots/r95_ingame/'
eas=unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
if mode=='capture':
    for a in eas.get_all_level_actors():
        if a.tags and 'R95Cap' in [str(t) for t in a.tags]: eas.destroy_actor(a)
    mesh=unreal.load_asset('/Game/SM2_Work/Characters/cc_titus_assault/cc_titus_assault')
    loc=unreal.Vector(0,5000,0)
    sk=eas.spawn_actor_from_class(unreal.SkeletalMeshActor,loc,unreal.Rotator(0,0,0)); sk.tags=['R95Cap']
    sk.skeletal_mesh_component.set_skeletal_mesh_asset(mesh)
    b=mesh.get_bounds(); h=b.box_extent.z*2; c=loc+b.origin
    rts=[]
    for name,src in (('unlit',unreal.SceneCaptureSource.SCS_BASE_COLOR),('lit',unreal.SceneCaptureSource.SCS_FINAL_COLOR_LDR)):
        cap=eas.spawn_actor_from_class(unreal.SceneCapture2D,c+unreal.Vector(0,h*1.6,0),unreal.Rotator(0,0,-90)); cap.tags=['R95Cap']
        cc=cap.capture_component2d; cc.capture_source=src; cc.fov_angle=40
        rt=unreal.RenderingLibrary.create_render_target2d(eas.get_editor_world() if hasattr(eas,'get_editor_world') else unreal.EditorLevelLibrary.get_editor_world(),900,1000,unreal.TextureRenderTargetFormat.RTF_RGBA8)
        cc.texture_target=rt; cc.capture_every_frame=False
        unreal.AutomationLibrary.finish_loading_before_screenshot() if hasattr(unreal.AutomationLibrary,'finish_loading_before_screenshot') else None
        cc.capture_scene(); rts.append((name,rt))
    builtins._r95=rts; print('captured',h)
else:
    w=unreal.EditorLevelLibrary.get_editor_world()
    for name,rt in builtins._r95:
        unreal.RenderingLibrary.export_render_target(w,rt,out,'ue_titus_%s.png'%name)
    for a in eas.get_all_level_actors():
        if a.tags and 'R95Cap' in [str(t) for t in a.tags]: eas.destroy_actor(a)
    print('exported')
