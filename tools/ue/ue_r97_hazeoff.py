import unreal
P="/Game/ForgeLens/SM2/Sequences/BoltRifleFire_r97/LS_arifle_bolt_rifle"
E=unreal.EditorAssetLibrary; bk="/Game/ForgeLens/SM2/Tests/_bak_r97/LS_arifle_bolt_rifle_preHaze"
if not E.does_asset_exist(bk): E.duplicate_asset(P,bk)
ls=unreal.load_asset(P); n=0
for b in ls.get_bindings():
    o=b.get_object_template()
    if o and isinstance(o, unreal.NiagaraActor):
        o.get_editor_property("niagara_component").set_variable_float("Haze SpriteSize", 0.0); n+=1
E.save_loaded_asset(ls); print("R97H haze off on", n, "backup", bk)
