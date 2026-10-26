using System.Numerics;
using System.Text.Json.Nodes;

namespace LibSaber.SM2.Assets.Effects;

/// <summary>Per-texture particle data: flipbook grid, motion-vector scale and the 7-point particle outline.</summary>
public sealed record TextureFrames(int FramesX, int FramesY, int FrameMax, float MotionVectorScale, Vector2[] Outline)
{
  public static readonly TextureFrames Single = new(1, 1, 1, 0f, []);
}

/// <summary>
///   <c>part_aliases/part_aliases.ps</c> (default.pak; descriptor <c>res_desc_part_aliases</c>): a dictionary keyed by
///   texture name whose entries are <c>textureDataAdd</c> objects. sm2_retail.exe reads it in 0x1418fe5c0 (called
///   from 0x141a90761): <c>nFrameX</c> @0x143647ea0 and <c>nFrameY</c> @0x143647ea8 default to 1, <c>nFrameMax</c>
///   @0x143647eb0 of 0 means nFrameX*nFrameY and is clamped to 255, plus <c>motionVectorScale</c> @0x143647ec0 and
///   <c>Mesh.Vertex0..6</c> @0x143647ed4 (a convex outline in [-1,1] sprite space used to trim overdraw). The grid
///   is not stored in the texture (.td/.pct carry <c>tileLayout: 0</c>, <c>tiles: []</c>).
/// </summary>
public sealed class PartAliases
{
  private readonly Dictionary<string, TextureFrames> _entries = new(StringComparer.OrdinalIgnoreCase);
  public static readonly PartAliases Empty = new();
  public int Count => _entries.Count;

  public static PartAliases Parse(string text)
  {
    var aliases = new PartAliases();
    foreach (var (name, node) in SslText.Parse(text))
    {
      if (node is not JsonObject o) continue;
      var x = Math.Max(1, (int)o.Float("nFrameX", 1));
      var y = Math.Max(1, (int)o.Float("nFrameY", 1));
      var max = (int)o.Float("nFrameMax", 0);
      if (max <= 0) max = x * y;
      max = Math.Min(Math.Min(max, 255), x * y);
      var outline = new List<Vector2>();
      if (o["Mesh"] is JsonObject mesh)
        for (var i = 0; i < 7; i++)
          if (mesh[$"Vertex{i}"] is JsonObject v)
            outline.Add(new Vector2(v.Float("X", 0), v.Float("Y", 0)));
      aliases._entries[name.Split('#')[0]] = new TextureFrames(x, y, max, o.Float("motionVectorScale", 0), outline.ToArray());
    }

    return aliases;
  }

  public static PartAliases Load(string path) => File.Exists(path) ? Parse(File.ReadAllText(path)) : Empty;

  public TextureFrames this[string? texture] => texture is not null && _entries.TryGetValue(texture, out var e) ? e : TextureFrames.Single;
  public bool Contains(string texture) => _entries.ContainsKey(texture);
}
