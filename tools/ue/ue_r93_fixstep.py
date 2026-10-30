# ue_r93_fixstep.py (R93): deterministic cloth sampling. Starts Simulate, stops the clip's own playback and drives the anim
# position per engine frame (frame k -> clip time k*STEP), measures/captures at exact frame numbers (not wall clock).
# usage: start,<outdir>,<step>,<f1;f2;f3>   |  status,<outdir>
import sys, os, json, math, unreal
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ue_r90_cloth as R
def _ffr(on, fps):
    # R93: plugin SetEngineFixedFrameRate (GEngine->bUseFixedFrameRate): every engine tick then gets dt = 1/fps exactly
    if hasattr(R.L, 'set_engine_fixed_frame_rate'): R.L.set_engine_fixed_frame_rate(on, fps)
    else: print('R93 WARN no SetEngineFixedFrameRate (old plugin)')
def _start(out, step, frames):
    os.makedirs(out, exist_ok=True); done = os.path.join(out, "fix_done.json")
    if os.path.exists(done): os.remove(done)
    st = {"k": -1, "frames": frames, "step": step, "res": [], "h": None, "dts": []}
    def tick(dt):
        try:
            w = unreal.get_editor_subsystem(unreal.UnrealEditorSubsystem).get_game_world()
            if not w: return
            acts = R.tagged(unreal.SkeletalMeshActor, "R90_")
            if not acts: return
            c = list(acts.values())[0].skeletal_mesh_component
            st["k"] += 1; k = st["k"]
            if k == 0: c.stop()
            st["dts"].append(unreal.GameplayStatics.get_world_delta_seconds(w))
            if k in frames:
                pos = c.get_position(); o = {"frame": k, "clip_t": round(pos, 4)}
                R.cap(out, "f%d" % k)
                for f, args in (("measure_cloth_penetration", (1.0,)), ("measure_cloth_skin_penetration", (1.0, 15.0))):
                    r = getattr(R.L, f)(c, *args); o[f] = [r[0], str(r[1])[:300]] if isinstance(r, tuple) else [r, ""]
                st["res"].append(o)
            if k >= max(frames):
                unreal.unregister_slate_post_tick_callback(st["h"]); R.les().editor_request_end_play(); _ffr(False, 30)
                d = st["dts"]; json.dump({"res": st["res"], "dt_min": min(d), "dt_max": max(d), "n": len(d), "dts": [round(x, 4) for x in d]}, open(done, "w")); return
            an = c.get_editor_property("animation_data").get_editor_property("anim_to_play"); L = an.get_editor_property("sequence_length") if hasattr(an, "get_editor_property") else 0
            t = (k + 1) * step
            if L: t = math.fmod(t, L)
            c.set_position(t, False)
        except Exception as ex:
            json.dump({"err": str(ex)}, open(done, "w"))
            try: unreal.unregister_slate_post_tick_callback(st["h"]); R.les().editor_request_end_play(); _ffr(False, 30)
            except Exception: pass
    _ffr(True, 1.0 / step)
    R.les().editor_play_simulate()
    st["h"] = unreal.register_slate_post_tick_callback(tick); print("R93 started")
if __name__ == "__main__":
    a = sys.argv[1].split(",")
    if a[0] == "start": _start(a[1], float(a[2]), [int(x) for x in a[3].split(";")])



