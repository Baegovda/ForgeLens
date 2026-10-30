import bpy, sys
args = sys.argv[sys.argv.index('--')+1:]
for f in args:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=f)
    arm = [o for o in bpy.data.objects if o.type == 'ARMATURE'][0]
    act = bpy.data.actions[0]; f0, f1 = map(int, act.frame_range)
    sc = bpy.context.scene
    sc.frame_set(f0); 
    rest = {b.name: b.length for b in arm.data.bones}
    worst = []
    for fr in range(f0, f1 + 1, 2):
        sc.frame_set(fr)
        for pb in arm.pose.bones:
            s = max(pb.matrix.to_scale()); 
            if pb.parent:
                d = (pb.head - pb.parent.head).length
                d0 = (arm.data.bones[pb.name].head_local - arm.data.bones[pb.parent.name].head_local).length
                if d0 > 1e-4: worst.append((d / d0, s, pb.name, fr))
    worst.sort(reverse=True)
    print(f.split('\\')[-1], 'top stretch', [(round(a,2), round(b,2), n, fr) for a, b, n, fr in worst[:6]])
