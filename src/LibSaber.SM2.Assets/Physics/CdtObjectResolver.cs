using System.Numerics;
using LibSaber.SM2.Assets.Meshes;

namespace LibSaber.SM2.Assets.Physics;

/// <summary>
///   mtl1-37: CDT shape "bones" are Mod Tools <c>link_tpl_obj</c> requests (meta_desc, FINDINGS §3): any object of the
///   template's object list, which includes <c>.tpl_markup</c> locators, not only skeleton bones. A shape whose bone is
///   not a template object but a locator is moved onto the locator's parent object with the locator transform folded
///   into its offsets (and box rotation). Evidence: calgar <c>Backpack_CDT_L</c> = locator on <c>powerplant_backpack</c>
///   at (0.203, 0, -0.593), rotation 0 (cc_calgar.tpl_markup). Runs before the UE plugin's name-heuristic remap, which
///   then only sees names that are in neither list.
/// </summary>
public static class CdtObjectResolver
{
  /// <summary>Rewrites shapes in place; returns how many bone references were resolved. Idempotent.</summary>
  public static int Apply(PhysicsSetup setup, IReadOnlyList<Bone> bones, IReadOnlyList<TplLocator> locators)
  {
    if (locators.Count == 0)
      return 0;
    var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    foreach (var b in bones)
    {
      known.Add(b.Name);
      if (b.SourceName.Length > 0) known.Add(b.SourceName);
    }

    var byName = new Dictionary<string, TplLocator>(StringComparer.OrdinalIgnoreCase);
    foreach (var l in locators)
      if (!known.Contains(l.Name) && known.Contains(l.Parent))
        byName.TryAdd(l.Name, l);

    var resolved = 0;
    var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
    var notes = new SortedSet<string>(StringComparer.Ordinal);
    bool Map(string bone, Vector3 offset, out string parent, out Vector3 moved, out TplLocator? loc)
    {
      parent = bone; moved = offset; loc = null;
      if (known.Contains(bone) || !byName.TryGetValue(bone, out var l))
        return false;
      loc = l;
      parent = l.Parent;
      moved = Vector3.Transform(offset, l.Local);
      notes.Add($"{l.Name} -> locator on {l.Parent} at ({l.Position.X:0.###}, {l.Position.Y:0.###}, {l.Position.Z:0.###}) rot ({l.RotationDegrees.X:0.#}, {l.RotationDegrees.Y:0.#}, {l.RotationDegrees.Z:0.#})");
      return true;
    }

    foreach (var cloth in setup.Cloths)
    {
      var c = cloth.Config;
      foreach (var k in c.Capsules.Where(seen.Add))
      {
        var from = new List<string>();
        if (Map(k.BoneFirst, k.BoneFirstOffset, out var p1, out var o1, out _)) { from.Add("BoneFirst=" + k.BoneFirst); k.BoneFirst = p1; k.BoneFirstOffset = o1; resolved++; }
        if (k.BoneSecond.Length > 0 && Map(k.BoneSecond, k.BoneSecondOffset, out var p2, out var o2, out _)) { from.Add("BoneSecond=" + k.BoneSecond); k.BoneSecond = p2; k.BoneSecondOffset = o2; resolved++; }
        if (from.Count > 0) k.ResolvedFrom = string.Join(" ", from);
      }
      foreach (var k in c.Spheres.Where(seen.Add))
        if (Map(k.Bone, k.BoneOffset, out var p, out var o, out _)) { k.ResolvedFrom = "Bone=" + k.Bone; k.Bone = p; k.BoneOffset = o; resolved++; }
      foreach (var k in c.Boxes.Where(seen.Add))
        if (Map(k.Bone, k.BoneOffset, out var p, out var o, out var l))
        {
          k.ResolvedFrom = "Bone=" + k.Bone; k.Bone = p; k.BoneOffset = o; resolved++;
          k.Rotation = Quaternion.Normalize(Quaternion.Concatenate(k.Rotation, Quaternion.CreateFromRotationMatrix(l!.Rotation)));
        }
    }

    // phys52: spring-damper constrains name locators too (upLocatorName / downLocatorName); same link_tpl_obj rule.
    foreach (var k in setup.SpringDampers.SelectMany(d => d.Constrains).Where(seen.Add))
    {
      var from = new List<string>();
      if (Map(k.UpLocatorName, Vector3.Zero, out var p1, out var o1, out _)) { from.Add("Up=" + k.UpLocatorName); k.UpBone = p1; k.UpOffset = o1; resolved++; }
      if (Map(k.DownLocatorName, Vector3.Zero, out var p2, out var o2, out _)) { from.Add("Down=" + k.DownLocatorName); k.DownBone = p2; k.DownOffset = o2; resolved++; }
      if (from.Count > 0) k.ResolvedFrom = string.Join(" ", from);
    }

    foreach (var n in notes)
      setup.Notes.Add("mtl1-37 CDT bone resolved via tpl_markup (link_tpl_obj): " + n);
    return resolved;
  }
}
