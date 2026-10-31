# ue_r98_orbbloom.py (R98a): large soft pink-white bloom card for the vortex orb in Map_Floor.
# Additive unlit material M_orb_bloomcard (radial falloff), plane facing the LS_orb_cam camera, attached to the Vortex (tag R98A).
# usage: <intensity> <size_cm> <core_white 0..1> [<pow> [<r> <g> <b> tint]]
import unreal, sys, math
I=float(sys.argv[1]); SZ=float(sys.argv[2]); W=float(sys.argv[3]); PW=float(sys.argv[4]) if len(sys.argv)>4 else 2.0
D="/Game/ForgeLens/SM2/VFX/vortex_orb"; E=unreal.EditorAssetLibrary; L=unreal.MaterialEditingLibrary; AT=unreal.AssetToolsHelpers.get_asset_tools()
MP=D+"/M_orb_bloomcard"
if not E.does_asset_exist(MP):
    m=AT.create_asset("M_orb_bloomcard",D,unreal.Material,unreal.MaterialFactoryNew())
    m.set_editor_property("blend_mode",unreal.BlendMode.BLEND_ADDITIVE); m.set_editor_property("shading_model",unreal.MaterialShadingModel.MSM_UNLIT)
    tc=L.create_material_expression(m,unreal.MaterialExpressionTextureCoordinate,-900,0)
    c5=L.create_material_expression(m,unreal.MaterialExpressionConstant2Vector,-900,150); c5.set_editor_property("r",0.5); c5.set_editor_property("g",0.5)
    ds=L.create_material_expression(m,unreal.MaterialExpressionDistance,-700,0); L.connect_material_expressions(tc,"",ds,"A"); L.connect_material_expressions(c5,"",ds,"B")
    m2=L.create_material_expression(m,unreal.MaterialExpressionMultiply,-550,0); m2.set_editor_property("const_b",2.0); L.connect_material_expressions(ds,"",m2,"A")
    om=L.create_material_expression(m,unreal.MaterialExpressionOneMinus,-420,0); L.connect_material_expressions(m2,"",om,"")
    sat=L.create_material_expression(m,unreal.MaterialExpressionSaturate,-300,0); L.connect_material_expressions(om,"",sat,"")
    pp=L.create_material_expression(m,unreal.MaterialExpressionScalarParameter,-300,150); pp.set_editor_property("parameter_name","Falloff"); pp.set_editor_property("default_value",2.0)
    pw=L.create_material_expression(m,unreal.MaterialExpressionPower,-180,0); L.connect_material_expressions(sat,"",pw,"Base"); L.connect_material_expressions(pp,"",pw,"Exp")
    # colour: lerp(Tint, White, mask^4 * CoreWhite)
    tint=L.create_material_expression(m,unreal.MaterialExpressionVectorParameter,-300,300); tint.set_editor_property("parameter_name","Tint"); tint.set_editor_property("default_value",unreal.LinearColor(1,0.45,0.9,1))
    wh=L.create_material_expression(m,unreal.MaterialExpressionConstant3Vector,-300,450); wh.set_editor_property("constant",unreal.LinearColor(1,0.9,1,1))
    p4=L.create_material_expression(m,unreal.MaterialExpressionPower,-180,550); p4.set_editor_property("const_exponent",4.0); L.connect_material_expressions(sat,"",p4,"Base")
    cw=L.create_material_expression(m,unreal.MaterialExpressionScalarParameter,-180,650); cw.set_editor_property("parameter_name","CoreWhite"); cw.set_editor_property("default_value",0.6)
    ma=L.create_material_expression(m,unreal.MaterialExpressionMultiply,-60,600); L.connect_material_expressions(p4,"",ma,"A"); L.connect_material_expressions(cw,"",ma,"B")
    lp=L.create_material_expression(m,unreal.MaterialExpressionLinearInterpolate,0,350); L.connect_material_expressions(tint,"",lp,"A"); L.connect_material_expressions(wh,"",lp,"B"); L.connect_material_expressions(ma,"",lp,"Alpha")
    ip=L.create_material_expression(m,unreal.MaterialExpressionScalarParameter,0,0); ip.set_editor_property("parameter_name","Intensity"); ip.set_editor_property("default_value",4.0)
    a1=L.create_material_expression(m,unreal.MaterialExpressionMultiply,150,100); L.connect_material_expressions(pw,"",a1,"A"); L.connect_material_expressions(ip,"",a1,"B")
    a2=L.create_material_expression(m,unreal.MaterialExpressionMultiply,300,200); L.connect_material_expressions(a1,"",a2,"A"); L.connect_material_expressions(lp,"",a2,"B")
    L.connect_material_property(a2,"",unreal.MaterialProperty.MP_EMISSIVE_COLOR)
    L.recompile_material(m); E.save_loaded_asset(m)
MIP=D+"/MI_orb_bloomcard"
mi=unreal.load_asset(MIP) if E.does_asset_exist(MIP) else AT.create_asset("MI_orb_bloomcard",D,unreal.MaterialInstanceConstant,unreal.MaterialInstanceConstantFactoryNew())
mi.set_editor_property("parent",unreal.load_asset(MP))
L.set_material_instance_scalar_parameter_value(mi,"Intensity",I); L.set_material_instance_scalar_parameter_value(mi,"CoreWhite",W); L.set_material_instance_scalar_parameter_value(mi,"Falloff",PW)
if len(sys.argv)>7: L.set_material_instance_vector_parameter_value(mi,"Tint",unreal.LinearColor(float(sys.argv[5]),float(sys.argv[6]),float(sys.argv[7]),1))
E.save_loaded_asset(mi)
LV=unreal.get_editor_subsystem(unreal.LevelEditorSubsystem); LV.load_level("/Game/ForgeLens/SM2/Tests/r92_floor/Map_Floor")
A=unreal.get_editor_subsystem(unreal.EditorActorSubsystem); acts=A.get_all_level_actors()
for a in acts:
    if "R98A" in [str(t) for t in a.tags]: A.destroy_actor(a)
acts=A.get_all_level_actors()
na=[x for x in acts if x.get_actor_label()=="R97_vortex_orb_ns"][0]; vx=na.get_attach_parent_actor(); loc=na.get_actor_location()
cap=[x for x in acts if isinstance(x,unreal.SceneCapture2D) and any(str(t)=="cap_vortex" for t in x.tags)][0]; cl=cap.get_actor_location()
d=cl-loc; n=d.normal(); p=loc+n*20.0
# plane mesh faces +Z; rotate +Z onto n
rot=unreal.MathLibrary.make_rot_from_z(n)
sm=A.spawn_actor_from_class(unreal.StaticMeshActor,p,rot); sm.tags=["R98A"]; sm.set_actor_label("R98A_orb_bloomcard")
c=sm.static_mesh_component; c.set_static_mesh(unreal.load_asset("/Engine/BasicShapes/Plane")); c.set_material(0,mi)
c.set_editor_property("cast_shadow",False); c.set_collision_enabled(unreal.CollisionEnabled.NO_COLLISION); c.set_mobility(unreal.ComponentMobility.MOVABLE)
sm.set_actor_scale3d(unreal.Vector(SZ/100.0,SZ/100.0,1))
sm.attach_to_actor(vx,"DISK",unreal.AttachmentRule.KEEP_WORLD,unreal.AttachmentRule.KEEP_WORLD,unreal.AttachmentRule.KEEP_WORLD,False)
LV.save_current_level(); print("R98A bloom", I, SZ, W, PW, loc, cl)

