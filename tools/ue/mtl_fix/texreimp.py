# mtl1-22 texture reimport after redecode-bc5: texreimp.py scan <unix cutoff> | run
# scan: Texture2D under /Game/SM2_Work (not Original/, not Work, not *_bakN) whose import source file was rewritten after
#       the cutoff -> %USERPROFILE%\texreimp_list.json. run: reimports the remaining list items for up to 200 s; items outside
#       /Game/ForgeLens/SM2/Tests/ (user area) are first duplicated to the next free <name>_bakN. Texture settings (sRGB,
#       compression, LOD group, ...) are restored after the import. Prints TXR REMAIN n.
import sys, os, json, time, unreal
EAL = unreal.EditorAssetLibrary; AR = unreal.AssetRegistryHelpers.get_asset_registry()
L = os.path.join(os.environ["USERPROFILE"], "texreimp_list.json"); D = L.replace("_list", "_done")
CONTENT = r"K:\Unreal Projects\UE_VFX_57\Content"
def src_of(ad, pkg):
    t = str(ad.get_tag_value("AssetImportData") or "")
    try: rel = json.loads(t)[0]["RelativeFilename"]
    except Exception: return None
    if os.path.isabs(rel): return rel
    for base in (os.path.join(CONTENT, os.path.dirname(pkg[len("/Game/"):])), unreal.Paths.convert_relative_path_to_full(unreal.Paths.engine_dir() + "Binaries/Win64")):
        f = os.path.normpath(os.path.join(base, rel))
        if os.path.exists(f): return f
    return None
if sys.argv[1] == "scan":
    cut = float(sys.argv[2]); flt = unreal.ARFilter(package_paths=["/Game/SM2_Work"], recursive_paths=True, class_paths=[unreal.TopLevelAssetPath("/Script/Engine", "Texture2D")])
    out = []; n = 0
    for ad in AR.get_assets(flt):
        pkg = str(ad.package_name); n += 1
        if pkg.startswith("/Game/SM2_Work/Original/") or "_bak" in pkg: continue
        s = src_of(ad, pkg)
        if s and os.path.exists(s) and os.path.getmtime(s) >= cut: out.append([pkg, s])
    json.dump(out, open(L, "w")); print("TXR scanned", n, "matched", len(out), "user", sum(1 for p, _ in out if not p.startswith("/Game/ForgeLens/SM2/Tests/")))
    raise SystemExit
items = json.load(open(L)); done = set(json.load(open(D))) if os.path.exists(D) else set()
KEEP = ["srgb", "compression_settings", "lod_group", "flip_green_channel", "mip_gen_settings", "address_x", "address_y", "virtual_texture_streaming", "never_stream"]
t0 = time.time(); ok = fail = bak = 0
for pkg, s in items:
    if pkg in done: continue
    if time.time() - t0 > 200: break
    a = unreal.load_asset(pkg)
    if not a: done.add(pkg); fail += 1; continue
    st = {}
    for k in KEEP:
        try: st[k] = a.get_editor_property(k)
        except Exception: pass
    if not pkg.startswith("/Game/ForgeLens/SM2/Tests/"):
        i = 1
        while EAL.does_asset_exist("%s_bak%d" % (pkg, i)): i += 1
        if EAL.duplicate_asset(pkg, "%s_bak%d" % (pkg, i)): bak += 1; EAL.save_asset("%s_bak%d" % (pkg, i), only_if_is_dirty=False)
    t = unreal.AssetImportTask(); t.filename = s; t.destination_path = pkg.rsplit("/", 1)[0]; t.destination_name = pkg.rsplit("/", 1)[1]
    t.replace_existing = True; t.automated = True; t.save = False
    unreal.AssetToolsHelpers.get_asset_tools().import_asset_tasks([t])
    a = unreal.load_asset(pkg)
    if a:
        for k, v in st.items():
            try: a.set_editor_property(k, v)
            except Exception: pass
        EAL.save_asset(pkg, only_if_is_dirty=False); ok += 1
    else: fail += 1
    done.add(pkg)
json.dump(sorted(done), open(D, "w"))
print("TXR ok", ok, "fail", fail, "bak", bak, "REMAIN", sum(1 for p, _ in items if p not in done))
