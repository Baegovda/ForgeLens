namespace LibSaber.SM2.Assets.Physics;

/// <summary>
///   Optional Chaos cloth rest-fit knobs (physics.json sidecar + SM2PhysicsSetup).
///   <para>
///   <b>HEURISTIC / non-game</b> (R76/R91/R93/R97k): retail exe has <b>no</b> restFit / ShallowFrac / ThickCm
///   constants (REA_DEEP §2). Game <c>collisionThickness</c> default is <b>0.0</b> (FUN_14108ef40 config ctor).
///   Rest-fit is a Chaos/UE collision fitting pass only. <see cref="Enabled"/> defaults <b>false</b> so
///   thickness and capsule radii come from authored data; enable only if PIE shows start-inside explosions.
///   </para>
/// </summary>
public static class RestFitPolicy
{
  public const string Evidence =
    "HEURISTIC optional (R76/R91/R93/R97k A/B). Exe has no restFit/ShallowFrac; collisionThickness default 0.0 PROVEN FUN_14108ef40. Off by default.";

  /// <summary>When false (default), plugin skips rest-fit radius shrink and uses authored radii + collisionThickness.</summary>
  public const bool Enabled = false;

  public const float Floor = 0.9f;
  public const float DeepFrac = 0.5f;
  public const float ShallowFracDefault = 1.0f;

  public static readonly IReadOnlyDictionary<string, float> ShallowOverrides =
    new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase)
    {
      ["coat_SIM_01"] = 0.04f,
      ["straps_SIM_01"] = 0.04f,
    };

  public static float ShallowFracFor(string? clothName)
  {
    if (!string.IsNullOrEmpty(clothName) && ShallowOverrides.TryGetValue(clothName, out var v))
      return v;
    return ShallowFracDefault;
  }

  public static string ShallowOverrideCVar()
    => string.Join(";", ShallowOverrides.Select(kv => $"{kv.Key}={kv.Value:0.##}"));
}
