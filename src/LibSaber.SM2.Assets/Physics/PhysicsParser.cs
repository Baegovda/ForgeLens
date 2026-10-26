using System.Numerics;
using System.Text.Json.Nodes;
using LibSaber.SM2.Assets.Effects;

namespace LibSaber.SM2.Assets.Physics;

/// <summary>
///   Reads <c>prop_cloth_sim</c> and <c>prop_phys_jiggle</c> from character class text (.cls). Key names are the
///   engine's field strings (cloth: 0x1435f10c0..0x1435f1510; jiggle: settings layout at 0x14147e5b5).
/// </summary>
public static class PhysicsParser
{
  public static PhysicsSetup Parse(string clsText, string source = "")
  {
    var root = SslText.Parse(clsText);
    var setup = new PhysicsSetup { SourceClass = source };
    if (Child(root, "properties") is JsonObject properties)
      root = properties;
    if (Child(root, "prop_cloth_sim") is JsonObject cloth)
      ReadCloth(cloth, setup);
    if (Child(root, "prop_phys_jiggle") is JsonObject jiggle)
      ReadJiggle(jiggle, setup);
    return setup;
  }

  /// <summary>True when the text declares either physics property (cheap pre-filter, no parse).</summary>
  public static bool Declares(string clsText)
    => clsText.Contains("prop_cloth_sim", StringComparison.Ordinal) || clsText.Contains("prop_phys_jiggle", StringComparison.Ordinal);

  private static void ReadCloth(JsonObject prop, PhysicsSetup setup)
  {
    var sharedCapsules = new Dictionary<string, ClothCapsule>(StringComparer.OrdinalIgnoreCase);
    var sharedSpheres = new Dictionary<string, ClothSphere>(StringComparer.OrdinalIgnoreCase);
    var sharedBoxes = new Dictionary<string, ClothBox>(StringComparer.OrdinalIgnoreCase);
    foreach (var item in Items(Child(prop, "sharedCDTCapsules")))
      if (ReadCapsule(item) is { } capsule) sharedCapsules[capsule.Name] = capsule;
    foreach (var item in Items(Child(prop, "sharedCDTSpheres")))
      if (ReadSphere(item) is { } sphere) sharedSpheres[sphere.Name] = sphere;
    foreach (var item in Items(Child(prop, "sharedCDTBoxes")))
      if (ReadBox(item) is { } box) sharedBoxes[box.Name] = box;

    foreach (var item in Items(Child(prop, "clothes")))
    {
      var entry = new ClothEntry { SimMeshName = Str(item, "simMeshName") };
      foreach (var render in Items(Child(item, "renderMeshes")))
      {
        var name = Str(render, "name");
        if (name.Length > 0)
          entry.RenderMeshes.Add(name);
      }

      if (Child(item, "simRemapConfig") is JsonObject remap)
      {
        entry.MaxInfluenceDist = Num(remap, "maxInfluenceDist", entry.MaxInfluenceDist);
        entry.DistanceRemapPower = Num(remap, "distanceRemapPower", entry.DistanceRemapPower);
        entry.UseTopologicalDistance = Bool(remap, "useTopologicalDistance", false);
      }

      if (Child(item, "config") is JsonObject config)
        entry.Config = ReadConfig(config, sharedCapsules, sharedSpheres, sharedBoxes, setup.Notes, entry.SimMeshName);
      if (entry.SimMeshName.Length > 0)
        setup.Cloths.Add(entry);
    }
  }

  private static ClothConfig ReadConfig(
    JsonObject o,
    Dictionary<string, ClothCapsule> sharedCapsules,
    Dictionary<string, ClothSphere> sharedSpheres,
    Dictionary<string, ClothBox> sharedBoxes,
    List<string> notes,
    string owner)
  {
    var c = new ClothConfig();
    c.Vertical = ReadPhase(Child(o, "vertical"));
    c.Horizontal = ReadPhase(Child(o, "horizontal"));
    c.Bend = ReadPhase(Child(o, "bend"));
    c.Shear = ReadPhase(Child(o, "shear"));
    if (Child(o, "tether") is JsonObject tether)
    {
      c.Tether.Stiffness = Num(tether, "stiffness", c.Tether.Stiffness);
      c.Tether.Scale = Num(tether, "scale", c.Tether.Scale);
      c.Tether.UseTopologicalDistance = Bool(tether, "useTopologicalDistance", false);
    }

    if (Child(o, "motion") is JsonObject motion)
    {
      c.Motion.Enable = Bool(motion, "enable", true);
      c.Motion.Stiffness = Num(motion, "stiffness", c.Motion.Stiffness);
      c.Motion.DistanceScale = Num(motion, "distanceScale", c.Motion.DistanceScale);
    }

    if (Child(o, "separation") is JsonObject separation)
    {
      c.Separation.Enable = Bool(separation, "enable", false);
      c.Separation.RadiusScale = Num(separation, "radiusScale", 1);
      c.Separation.OffsetScale = Num(separation, "offsetScale", 1);
    }

    if (Child(o, "selfCollision") is JsonObject self)
    {
      c.SelfCollision.Enable = Bool(self, "enable", false);
      c.SelfCollision.Stiffness = Num(self, "stiffness", c.SelfCollision.Stiffness);
      c.SelfCollision.Distance = Num(self, "distance", 0);
    }

    c.StiffnessMultiplier = Num(o, "stiffnessMultiplier", 1);
    c.Gravity = Vec(Child(o, "gravity"), c.Gravity);
    foreach (var item in Items(Child(o, "cdtCapsules")))
    {
      var capsule = ReadCapsule(item);
      if (capsule is not null && capsule.BoneFirst.Length == 0 && sharedCapsules.TryGetValue(capsule.Name, out var shared))
        capsule = shared;
      if (capsule is { BoneFirst.Length: > 0 })
        c.Capsules.Add(capsule);
      else if (capsule is not null)
        notes.Add($"{owner}: capsule '{capsule.Name}' has no bones and no shared definition.");
    }

    foreach (var item in Items(Child(o, "cdtSpheres")))
    {
      var sphere = ReadSphere(item);
      if (sphere is not null && sphere.Bone.Length == 0 && sharedSpheres.TryGetValue(sphere.Name, out var shared))
        sphere = shared;
      if (sphere is { Bone.Length: > 0 })
        c.Spheres.Add(sphere);
    }

    foreach (var item in Items(Child(o, "cdtBoxes")))
    {
      var box = ReadBox(item);
      if (box is not null && box.Bone.Length == 0 && sharedBoxes.TryGetValue(box.Name, out var shared))
        box = shared;
      if (box is { Bone.Length: > 0 })
        c.Boxes.Add(box);
    }

    if (Child(o, "environment") is JsonObject env)
    {
      var e = c.Environment;
      e.AirDrag = Num(env, "airDrag", e.AirDrag);
      e.AirLift = Num(env, "airLift", e.AirLift);
      e.LocalWindVelocity = Vec(Child(env, "localWindVelocity"), e.LocalWindVelocity);
      e.GlobalWindVelocity = Vec(Child(env, "globalWindVelocity"), e.GlobalWindVelocity);
      e.UseWindSystem = Bool(env, "useWindSystem", e.UseWindSystem);
      e.WindSystemInfluenceScale = Num(env, "windSystemInfluenceScale", e.WindSystemInfluenceScale);
      e.FluidDensity = Num(env, "fluidDensity", e.FluidDensity);
    }

    c.SolverFrequency = Num(o, "solverFrequency", c.SolverFrequency);
    c.StiffnessFrequency = Num(o, "stiffnessFrequency", Num(o, "stiffnessFrequencyScale", c.StiffnessFrequency));
    c.AccelerationFilterWidth = (int)Num(o, "acceleationFilterWidth", c.AccelerationFilterWidth);
    c.CollisionThickness = Num(o, "collisionThickness", 0);
    c.Friction = Num(o, "friction", 0);
    c.Damping = Num(o, "damping", c.Damping);
    if (Child(o, "drag") is JsonObject drag)
    {
      c.LinearDrag = Num(drag, "linear", 0);
      c.AngularDrag = Num(drag, "angular", 0);
    }

    if (Child(o, "inertia") is JsonObject inertia)
    {
      c.LinearInertia = Num(inertia, "linear", 1);
      c.AngularInertia = Num(inertia, "angular", 1);
      c.CentrifugalInertia = Num(inertia, "centrifugal", 1);
    }

    c.CollisionMassScale = Num(o, "collisionMassScale", c.CollisionMassScale);
    return c;
  }

  private static ClothPhase ReadPhase(JsonNode? node)
  {
    var p = new ClothPhase();
    if (node is not JsonObject o)
      return p;
    p.Stiffness = Num(o, "stiffness", p.Stiffness);
    p.StiffnessMultiplier = Num(o, "stiffnessMultiplier", p.StiffnessMultiplier);
    p.CompressionLimit = Num(o, "compressionLimit", p.CompressionLimit);
    p.StretchLimit = Num(o, "stretchLimit", p.StretchLimit);
    return p;
  }

  private static ClothCapsule? ReadCapsule(JsonObject o)
  {
    var name = Str(o, "name");
    return new ClothCapsule
    {
      Name = name,
      BoneFirst = Str(o, "boneFirst"),
      RadiusFirst = Num(o, "radiusFirst", 0),
      BoneFirstOffset = Vec(Child(o, "boneFirstOffset"), Vector3.Zero),
      BoneSecond = Str(o, "boneSecond"),
      RadiusSecond = Num(o, "radiusSecond", Num(o, "radiusFirst", 0)),
      BoneSecondOffset = Vec(Child(o, "boneSecondOffset"), Vector3.Zero),
    };
  }

  private static ClothSphere? ReadSphere(JsonObject o)
    => new() { Name = Str(o, "name"), Bone = Str(o, "bone"), Radius = Num(o, "radius", 0), BoneOffset = Vec(Child(o, "boneOffset"), Vector3.Zero) };

  private static ClothBox? ReadBox(JsonObject o)
    => new() { Name = Str(o, "name"), Bone = Str(o, "bone"), Size = Vec(Child(o, "size"), Vector3.Zero), BoneOffset = Vec(Child(o, "boneOffset"), Vector3.Zero) };

  private static void ReadJiggle(JsonObject prop, PhysicsSetup setup)
  {
    if (Child(prop, "physJiggle") is not JsonObject jiggle || Child(jiggle, "objectsSettings") is not JsonObject groups)
      return;
    foreach (var (group, node) in groups)
    {
      if (group.StartsWith("__", StringComparison.Ordinal))
        continue;
      var list = node is JsonObject wrapped ? Child(wrapped, "__value") : node;
      foreach (var item in Items(list))
      {
        var s = new JiggleSettings
        {
          Group = group.Split('#')[0],
          Name = Str(item, "name"),
          Stiffness = Num(item, "stiffness", 0),
          Mass = Num(item, "mass", 1),
          Damping = Num(item, "damping", 0),
          Gravity = Num(item, "gravity", 0),
          OffsetDirection = Vec(Child(item, "offsetDirection"), Vector3.UnitZ),
          OffsetDistance = Num(item, "offsetDistance", 0),
          ConstraintMin = Vec(Child(item, "constraintMin"), new Vector3(-1)),
          ConstraintMax = Vec(Child(item, "constraintMax"), new Vector3(1)),
          JumpsIntensity = Num(item, "jumpsIntensity", 0),
          JumpsConstraintMin = Num(item, "jumpsConstraintMin", 0),
          JumpsConstraintMax = Num(item, "jumpsConstraintMax", 0),
        };
        if (s.Name.Length > 0)
          setup.Jiggles.Add(s);
      }
    }
  }

  private static JsonNode? Child(JsonObject? o, string key)
    => o is not null && o.TryGetPropertyValue(key, out var value) ? value : null;

  private static IEnumerable<JsonObject> Items(JsonNode? node)
  {
    if (node is JsonObject wrapped && Child(wrapped, "__value") is JsonArray inner)
      node = inner;
    if (node is not JsonArray array)
      yield break;
    foreach (var item in array)
      if (item is JsonObject o)
        yield return o;
  }

  private static string Str(JsonObject o, string key)
    => Child(o, key) is JsonValue v && v.TryGetValue<string>(out var s) ? s : "";

  private static float Num(JsonObject o, string key, float fallback)
  {
    if (Child(o, key) is not JsonValue v)
      return fallback;
    if (v.TryGetValue<double>(out var d)) return (float)d;
    if (v.TryGetValue<long>(out var l)) return l;
    if (v.TryGetValue<bool>(out var b)) return b ? 1 : 0;
    return fallback;
  }

  private static bool Bool(JsonObject o, string key, bool fallback)
  {
    if (Child(o, key) is not JsonValue v)
      return fallback;
    if (v.TryGetValue<bool>(out var b)) return b;
    if (v.TryGetValue<long>(out var l)) return l != 0;
    return fallback;
  }

  private static Vector3 Vec(JsonNode? node, Vector3 fallback)
  {
    switch (node)
    {
      case JsonArray a when a.Count >= 3:
        return new Vector3(F(a[0]), F(a[1]), F(a[2]));
      case JsonObject o when o.ContainsKey("x"):
        return new Vector3(Num(o, "x", 0), Num(o, "y", 0), Num(o, "z", 0));
      default:
        return fallback;
    }

    static float F(JsonNode? n)
      => n is JsonValue v ? (v.TryGetValue<double>(out var d) ? (float)d : v.TryGetValue<long>(out var l) ? l : 0) : 0;
  }
}
