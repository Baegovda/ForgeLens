# ue_r97_mfparam.py (R97o): copy LS_mf_all to <dest> and set Niagara user-parameter overrides on every MuzzleFlash binding.
# usage: uerun ue_r97_mfparam.py <dest LS>,<Name=val;Name=val>  (floats; '2:' prefix = vector2 both comps)
import sys, unreal
a = sys.argv[1:]; a = a[0].split(",") if len(a) == 1 else a
SRC = "/Game/ForgeLens/SM2/Sequences/BoltRifleFire_r97/LS_mf_all"
E = unreal.EditorAssetLibrary
if E.does_asset_exist(a[0]): E.delete_asset(a[0])
E.duplicate_asset(SRC, a[0]); ls = unreal.load_asset(a[0]); n = 0
kv = [x.split("=") for x in a[1].split(";") if x]
for b in ls.get_bindings():
    o = b.get_object_template()
    if o and isinstance(o, unreal.NiagaraActor):
        c = o.get_editor_property("niagara_component"); n += 1
        for k, v in kv:
            if v.startswith("2:"): c.set_variable_vec2(k, unreal.Vector2D(float(v[2:]), float(v[2:])))
            else: c.set_variable_float(k, float(v))
E.save_loaded_asset(ls)
print("R97P", a[0], kv, "bindings", n)
