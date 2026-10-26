using System.Globalization;
using System.Text;

namespace LibSaber.SM2.Assets.Customization;

/// <summary>
///   Reader for SM2's text SSL (<c>.sso</c>, text <c>.cls</c>): <c>key = value</c> pairs, <c>{ }</c> objects,
///   <c>[ ]</c> arrays with optional commas, quoted strings, numbers and <c>True</c>/<c>False</c>.
///   Objects become <see cref="Dictionary{TKey,TValue}"/> (insertion order kept), arrays <see cref="List{T}"/>,
///   scalars string / double / bool. A bare key with no <c>=</c> maps to null.
/// </summary>
public static class SslText
{
  public static Dictionary<string, object?> Parse(string text)
  {
    var tokens = Tokenize(text);
    var position = 0;
    return ReadObject(tokens, ref position, null);
  }

  public static Dictionary<string, object?>? Obj(object? value, params string[] path)
  {
    var current = value;
    foreach (var key in path)
    {
      if (current is not Dictionary<string, object?> map || !map.TryGetValue(key, out current))
        return null;
    }

    return current as Dictionary<string, object?>;
  }

  public static IEnumerable<string> Strings(object? value)
  {
    if (value is List<object?> list)
    {
      foreach (var item in list)
      {
        if (item is string text)
          yield return text;
      }
    }
  }

  internal static List<string> Tokenize(string text)
  {
    var tokens = new List<string>();
    var i = 0;
    while (i < text.Length)
    {
      var c = text[i];
      if (char.IsWhiteSpace(c))
      {
        i++;
        continue;
      }

      if (c is '{' or '}' or '[' or ']' or '=' or ',')
      {
        tokens.Add(c.ToString());
        i++;
        continue;
      }

      if (c == '"')
      {
        var builder = new StringBuilder("\"");
        i++;
        while (i < text.Length && text[i] != '"')
        {
          if (text[i] == '\\' && i + 1 < text.Length)
          {
            builder.Append(text[i + 1]);
            i += 2;
            continue;
          }

          builder.Append(text[i++]);
        }

        i++;
        tokens.Add(builder.Append('"').ToString());
        continue;
      }

      var start = i;
      while (i < text.Length && !char.IsWhiteSpace(text[i]) && text[i] is not ('{' or '}' or '[' or ']' or '=' or ',' or '"'))
        i++;
      tokens.Add(text[start..i]);
    }

    return tokens;
  }

  private static Dictionary<string, object?> ReadObject(List<string> tokens, ref int position, string? end)
  {
    var map = new Dictionary<string, object?>(StringComparer.Ordinal);
    while (position < tokens.Count && tokens[position] != end)
    {
      var key = tokens[position++];
      if (key == ",")
        continue;
      if (key is "}" or "]")
        continue; // stray closer at top level
      if (position < tokens.Count && tokens[position] == "=")
      {
        position++;
        map[Unquote(key)] = ReadValue(tokens, ref position);
      }
      else
      {
        map[Unquote(key)] = null;
      }
    }

    if (end is not null && position < tokens.Count && tokens[position] == end)
      position++;
    return map;
  }

  private static object? ReadValue(List<string> tokens, ref int position)
  {
    if (position >= tokens.Count)
      return null;
    var token = tokens[position++];
    switch (token)
    {
      case "{":
        return ReadObject(tokens, ref position, "}");
      case "[":
        var list = new List<object?>();
        while (position < tokens.Count && tokens[position] != "]")
        {
          if (tokens[position] == ",")
          {
            position++;
            continue;
          }

          list.Add(ReadValue(tokens, ref position));
        }

        position++;
        return list;
      case "True":
        return true;
      case "False":
        return false;
    }

    if (token.StartsWith('"'))
      return Unquote(token);
    if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
      return number;
    return token;
  }

  private static string Unquote(string token)
    => token.Length >= 2 && token[0] == '"' && token[^1] == '"' ? token[1..^1] : token;
}
