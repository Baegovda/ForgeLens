# ew_socket.py (R58): add skeleton sockets SM2_<bone> on bones via EngineWire animation_physics create_socket
# (UE 5.7 Python cannot: SkeletalMeshSocket.SocketName/BoneName are read-only - tried ctor, new_object, set_editor_property).
# Usage (PC, editor running): python ew_socket.py <skeletal mesh path> <bone> [<bone>...]   then lists sockets.
import sys, json, subprocess, os, tempfile
here = os.path.dirname(os.path.abspath(__file__)); call = os.path.join(here, "enginewire_call.py")
def run(args):
    f = tempfile.NamedTemporaryFile("w", suffix=".json", delete=False); json.dump(args, f); f.close()
    try: return subprocess.run([sys.executable, call, "animation_physics", f.name], capture_output=True, text=True).stdout.strip()
    finally: os.unlink(f.name)
if __name__ == "__main__":
    mesh, bones = sys.argv[1], sys.argv[2:]
    for b in bones:
        print(run({"action": "create_socket", "skeletalMeshPath": mesh, "assetPath": mesh, "boneName": b, "socketName": "SM2_" + b, "save": True}).splitlines()[0])
    print(run({"action": "list_sockets", "skeletalMeshPath": mesh, "assetPath": mesh}))
