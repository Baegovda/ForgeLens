namespace LibSaber.SM2.Assets.Physics;

/// <summary>
///   Chaos cloth rest-fit knobs shared by the physics.json sidecar and SM2PhysicsSetup.
///   <para>
///   <b>HEURISTIC</b> (R76/R91/R93/R97k): not derived from game data. Chosen by A/B against viewer/UE cloth captures
///   so free rest particles do not crush collision radii (sorcerer R69 PROVEN raw scales 0.65–0.72; floor 0.9 keeps
///   ≥90 % of authored radius). Per-cloth shallow overrides for commissar <c>coat_SIM_01</c> / <c>straps_SIM_01</c>
///   (R97j/k): global ShallowFrac 0.04 fixed the coat but hurt titus/tech_priest/chaplain.
///   </para>
///   <para>
///   <b>F45</b> physics motion vs in-game: unverified (owner observation only; never launch the game).
///   Closing evidence for the floor itself would be retail NvCloth / Chaos rest pose vs authored capsule radii.
///   </para>
/// </summary>
public static class RestFitPolicy
{
  public const string Evidence =
    "HEURISTIC R76/R91/R93/R97k (A/B vs cloth captures; not from game data). F45 motion unverified.";

  public const float Floor = 0.9f;
  public const float DeepFrac = 0.5f;
  /// <summary>Default: always apply the radius floor (R91).</summary>
  public const float ShallowFracDefault = 1.0f;

  /// <summary>Commissar coat / straps (names unique to that character). Skip the floor when many shallow hits.</summary>
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

  /// <summary>CVar-compatible string <c>name=frac;name=frac</c> (plugin default / docs).</summary>
  public static string ShallowOverrideCVar()
    => string.Join(";", ShallowOverrides.Select(kv => $"{kv.Key}={kv.Value:0.##}"));
}
