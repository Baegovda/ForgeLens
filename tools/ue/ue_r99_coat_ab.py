# ue_r99_coat_ab.py — commissar coat before(old ThickCm2+restFit) / after(new Thick0+restFit off)
import sys, os, shutil, unreal

SUBJ = "commissar"
NAME = "cc_commissar"
BASE = os.path.join(os.path.expanduser("~"), "Desktop", "SM2_Work", "Exports", "chars_cloth2", NAME, "tpl", NAME + ".tpl")
PJ = os.path.join(BASE, NAME + ".physics.json")
BAK = PJ + ".bak_r99"
OLD = os.path.join(BASE, NAME + ".physics.r99old.json")
NEW = os.path.join(BASE, NAME + ".physics.r99new.json")
SHOT = os.path.join(os.path.expanduser("~"), "Shots", "r99_coat").replace("\\", "/")
os.makedirs(SHOT, exist_ok=True)

def les():
    return unreal.get_editor_subsystem(unreal.LevelEditorSubsystem)

def ues():
    return unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem)

def world():
    return ues().get_game_world() or ues().get_editor_world()

def swap(src):
    if not os.path.isfile(BAK):
        shutil.copy2(PJ, BAK)
    shutil.copy2(src, PJ)
    print("R99 swap", src, "->", PJ)

def restore():
    if os.path.isfile(BAK):
        shutil.copy2(BAK, PJ)
        print("R99 restored", PJ)

R90 = os.path.join(os.path.dirname(__file__), "ue_r90_cloth.py")
ns = {"__name__": "ue_r90_cloth_r99", "__file__": R90}
with open(R90, encoding="utf-8") as f:
    exec(compile(f.read(), R90, "exec"), ns)
prep = ns["prep"]; scene = ns["scene"]; actor = ns["actor"]; cams = ns["cams"]
measure = ns["measure"]; r90cap = ns["cap"]

def capture_rt(tag):
    """Capture via R90cam SceneCapture RT (works in editor or PIE if actors replicated)."""
    w = world()
    caps = list(unreal.GameplayStatics.get_all_actors_of_class(w, unreal.SceneCapture2D))
    print("R99 caps", len(caps), [a.get_actor_label() for a in caps])
    if not caps:
        # try editor actor subsystem labels
        E = unreal.get_editor_subsystem(unreal.EditorActorSubsystem)
        caps = [a for a in E.get_all_level_actors() if isinstance(a, unreal.SceneCapture2D)]
        print("R99 caps editor", len(caps), [a.get_actor_label() for a in caps])
    if not caps:
        d = SHOT
        unreal.SystemLibrary.execute_console_command(None, 'HighResShot 1280x720 filename="%s/%s.png"' % (d, tag))
        print("R99 HighResShot", d, tag)
        return
    # Prefer front-ish camera
    prefer = [c for c in caps if "front" in c.get_actor_label().lower() or "3q" in c.get_actor_label().lower() or "on" in c.get_actor_label().lower()]
    use = prefer[0] if prefer else caps[0]
    cc = use.capture_component2d
    rt = cc.get_editor_property("texture_target")
    for _ in range(4):
        cc.capture_scene()
    unreal.RenderingLibrary.export_render_target(w, rt, SHOT, tag + ".png")
    print("R99 wrote", os.path.join(SHOT, tag + ".png"), "from", use.get_actor_label())

a = sys.argv[1] if len(sys.argv) > 1 else ""
if a == "restore":
    restore()
elif a == "prep_old":
    swap(OLD); prep(SUBJ, "r99old")
elif a == "prep_new":
    swap(NEW); prep(SUBJ, "r99new")
elif a == "scene_old":
    scene(SUBJ, "r99old", "run"); actor(SUBJ, "r99old", "run"); cams()
elif a == "scene_new":
    scene(SUBJ, "r99new", "run"); actor(SUBJ, "r99new", "run"); cams()
elif a == "sim":
    les().editor_play_simulate(); print("R99 sim")
elif a == "stop":
    les().editor_request_end_play(); print("R99 stop")
elif a == "cap_old":
    capture_rt("before_old")
elif a == "cap_new":
    capture_rt("after_new")
elif a == "cap_old_hrs":
    r90cap(SHOT, "before_old")
elif a == "cap_new_hrs":
    r90cap(SHOT, "after_new")
elif a == "measure_old":
    measure(NAME + "_r99old")
elif a == "measure_new":
    measure(NAME + "_r99new")
else:
    print("usage: prep_*|scene_*|sim|stop|cap_*|measure_*|restore")

