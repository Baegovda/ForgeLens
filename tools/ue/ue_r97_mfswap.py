# ue_r97_mfswap.py (R97o): copy LS_arifle_bolt_rifle to <dest LS> and point every MuzzleFlash Niagara binding at <system>.
# usage: uerun ue_r97_mfswap.py <system path>,<dest LS path>
import sys, unreal
a = sys.argv[1:]; a = a[0].split(",") if len(a) == 1 else a
SRC = "/Game/ForgeLens/SM2/Sequences/BoltRifleFire_r97/LS_arifle_bolt_rifle"
E = unreal.EditorAssetLibrary
if not E.does_asset_exist(a[1]): E.duplicate_asset(SRC, a[1])
ls = unreal.load_asset(a[1]); ns = unreal.load_asset(a[0]); n = 0
for b in ls.get_bindings():
    o = b.get_object_template()
    if o and isinstance(o, unreal.NiagaraActor):
        o.get_editor_property("niagara_component").set_asset(ns); n += 1
E.save_loaded_asset(ls)
print("R97S", a[1], "->", a[0], "swapped", n)
