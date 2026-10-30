# ue_seq_fire.py (R59): F46-style fire Level Sequence builder.
# Duplicates the F46 template LS_TitusWalkFire into <dest>/LS_<name> (template untouched), then
#  - swaps the generated pitchUp35 aim-pose section for the aim-hold clip named in docs/handoff/MUZZLE_AIM.tsv (column aimHoldClips),
#  - re-attaches every MuzzleFlash binding to socket SM2_shootTip (flag tip -> SM2_tip) on the rifle (sockets via ew_socket.py).
# Usage: uerun.ps1 ue_seq_fire.py <muzzle_aim.tsv>,<cls>[,shootTip|tip[,dest[,frame]]]   (dest default /Game/SM2_Work/Sequences/BoltRifleFire)
# Prints R59 lines; [frame] opens the sequence and parks the playhead there (for a screenshot).
import sys
try:
    import unreal
except ImportError:  # unit tests (aim_clip only)
    unreal = None
TEMPLATE = "/Game/SM2_Work/Sequences/TitusWalkFire/LS_TitusWalkFire"
CLIPDIRS = ["/Game/SM2_Work/Sequences/TitusWalkFire/Anims"]
PREFIX = "cc_titus_assault__"

def aim_clip(tsv, cls):
    rows = [l.rstrip("\n").split("\t") for l in open(tsv, encoding="utf-8")]
    h = rows[0]; i = h.index("aimHoldClips")
    for r in rows[1:]:
        if r[0] == cls and len(r) > i and r[i]:
            return r[i].split(";")[0].replace(".sani", "")
    raise SystemExit("R59 no aim clip for " + cls)

def find_anim(name):
    for d in CLIPDIRS:
        p = f"{d}/{PREFIX}{name}"
        if unreal.EditorAssetLibrary.does_asset_exist(p): return unreal.load_asset(p)
    raise SystemExit("R59 clip not imported: " + name + " (export with SM2Extractor --anim and import under " + CLIPDIRS[0] + ")")

def build(tsv, cls, which="shootTip", dest="/Game/SM2_Work/Sequences/BoltRifleFire", frame=None):
    sock = "SM2_" + which
    clip = aim_clip(tsv, cls); anim = find_anim(clip)
    name = "LS_" + cls.replace("_client.cls", "")
    path = f"{dest}/{name}"
    if unreal.EditorAssetLibrary.does_asset_exist(path): unreal.EditorAssetLibrary.delete_asset(path)  # our own output, rebuilt
    ls = unreal.EditorAssetLibrary.duplicate_asset(TEMPLATE, path)
    swapped = 0; att = 0
    for b in ls.get_bindings():
        n = str(b.get_display_name())
        for t in b.get_tracks():
            for s in t.get_sections():
                if isinstance(s, unreal.MovieSceneSkeletalAnimationSection):
                    p = s.get_editor_property("params"); a = p.get_editor_property("animation")
                    if a and "pitchUp" in a.get_name():
                        p.set_editor_property("animation", anim); s.set_editor_property("params", p); swapped += 1
                if isinstance(s, unreal.MovieScene3DAttachSection) and n.startswith("MuzzleFlash"):
                    s.set_editor_property("attach_socket_name", sock); att += 1
    unreal.EditorAssetLibrary.save_loaded_asset(ls)
    print(f"R59 seq={path} cls={cls} aim={clip} swapped={swapped} muzzle_attach={att} socket={sock}")
    if frame is not None:
        unreal.LevelSequenceEditorBlueprintLibrary.open_level_sequence(ls)
        unreal.LevelSequenceEditorBlueprintLibrary.set_current_time(int(frame))
        print("R59 opened at frame", frame)
    return path, swapped, att

if __name__ == "__main__":
    a = sys.argv[1:]
    if len(a) == 1: a = a[0].split(",")  # uerun.ps1 passes one comma-joined arg
    build(a[0], a[1], a[2] if len(a) > 2 else "shootTip", a[3] if len(a) > 3 else "/Game/SM2_Work/Sequences/BoltRifleFire",
          a[4] if len(a) > 4 else None)
