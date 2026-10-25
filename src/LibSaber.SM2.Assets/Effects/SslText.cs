using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace LibSaber.SM2.Assets.Effects;

/// <summary>
///   Tolerant reader for the SSL text that retail .sfx (and .td, .cls) files are written in:
///   <c>key = value</c>, <c>{ ... }</c> objects, <c>[ a, b ]</c> arrays, quoted strings,
///   True/False and numbers. Produces a JsonNode tree in file order.
/// </summary>
public static class SslText
{
  private static readonly Regex Token = new("\"(?:[^\"\\\\]|\\\\.)*\"|[\\[\\]{}=,]|[^\\s\\[\\]{}=,\"]+", RegexOptions.Compiled);

  public static bool LooksText(ReadOnlySpan<byte> bytes)
  {
    var n = Math.Min(bytes.Length, 512);
    if (n == 0) return false;
    for (var i = 0; i < n; i++)
    {
      var b = bytes[i];
      if (b < 9 || (b > 13 && b < 32) || b == 127) return false;
    }
    return true;
  }

  public static JsonObject Parse(string text)
  {
    var tokens = Token.Matches(text).Select(m => m.Value).ToList();
    var i = 0;
    return ReadObject(tokens, ref i, null);
  }

  public static JsonObject Parse(byte[] bytes) => Parse(Encoding.UTF8.GetString(bytes));

  private static JsonObject ReadObject(List<string> t, ref int i, string? end)
  {
    var o = new JsonObject();
    while (i < t.Count && t[i] != end)
    {
      var key = t[i++];
      if (key is "," or "=") continue;
      if (i < t.Count && t[i] == "=") i++;
      if (i >= t.Count) break;
      var value = ReadValue(t, ref i);
      o[o.ContainsKey(key) ? key + "#" + o.Count : key] = value;
    }
    i++;
    return o;
  }

  private static JsonNode? ReadValue(List<string> t, ref int i)
  {
    var s = t[i++];
    if (s == "{") return ReadObject(t, ref i, "}");
    if (s == "[")
    {
      var a = new JsonArray();
      while (i < t.Count && t[i] != "]")
      {
        if (t[i] == ",") { i++; continue; }
        a.Add(ReadValue(t, ref i));
      }
      i++;
      return a;
    }
    if (s.Length >= 2 && s[0] == '"') return JsonValue.Create(s[1..^1]);
    if (s == "True") return JsonValue.Create(true);
    if (s == "False") return JsonValue.Create(false);
    if (s == "None") return null;
    if (long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l)) return JsonValue.Create(l);
    if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return JsonValue.Create(d);
    return JsonValue.Create(s);
  }
}
