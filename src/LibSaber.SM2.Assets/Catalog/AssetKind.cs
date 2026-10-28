namespace LibSaber.SM2.Assets.Catalog;

public enum AssetKind
{
  Other = 0,
  Texture,
  TextureMip,
  Model,
  ModelData,
  Animation,
  Effect,
  Text,
}

public enum AssetFilter
{
  All = 0,
  Textures,
  Models,
  Animations,
  Effects,
  Text,
}

public static class AssetClassification
{

  public static string CompoundExtension(string fileName)
  {
    var slash = fileName.LastIndexOfAny(new[] { '/', '\\' });
    var name = slash >= 0 ? fileName[(slash + 1)..] : fileName;
    if (name.EndsWith(".resource", StringComparison.OrdinalIgnoreCase))
    {
      var cut = name.Length - ".resource".Length;
      var dot = cut > 0 ? name.LastIndexOf('.', cut - 1) : -1;
      if (dot >= 0)
        return name[dot..].ToLowerInvariant();
    }

    var ext = Path.GetExtension(name);
    return string.IsNullOrEmpty(ext) ? string.Empty : ext.ToLowerInvariant();
  }

  public static string BaseName(string fileName)
  {
    var slash = fileName.LastIndexOfAny(new[] { '/', '\\' });
    var name = slash >= 0 ? fileName[(slash + 1)..] : fileName;
    var ext = CompoundExtension(name);
    return ext.Length == 0 ? name : name[..^ext.Length];
  }

  public static AssetKind Classify(string extension)
  {
    switch (extension)
    {
      case ".pct.resource":
      case ".pct":
        return AssetKind.Texture;
      case ".pct_mip":
        return AssetKind.TextureMip;
      case ".tpl":
      case ".tpl.resource":
        return AssetKind.Model;
      case ".tpl_data":
      case ".lods_base":
      case ".geom_dbg":
      case ".tpl_markup":
        return AssetKind.ModelData;
      case ".sani":
      case ".sani.resource":
      case ".sani_data":
        return AssetKind.Animation;
      case ".sfx":
      case ".ps":
      case ".s3dprs":
        return AssetKind.Effect;
      case ".resource":
      case ".td.resource":
      case ".ssc.resource":
      case ".prop":
      case ".cls":
      case ".td":
        return AssetKind.Text;
      default:
        return AssetKind.Other;
    }
  }

  /// <summary>
  ///   mtl1-42: extensions the browser preview draws (texture, model, animation clip, effect). The default file list shows
  ///   only these; "Show all files" lists the auxiliary payloads (.tpl_data, .tpl_markup, .lods_base, .geom_dbg,
  ///   .tpl.resource, .pct_mip, .sani_data, ...) and text again.
  /// </summary>
  public static bool IsPreviewable(string extension)
    => extension is ".pct.resource" or ".pct" or ".tpl" or ".sani" or ".sfx";

  public static bool Matches(AssetFilter filter, AssetKind kind)
  {
    return filter switch
    {
      AssetFilter.All => true,
      AssetFilter.Textures => kind is AssetKind.Texture or AssetKind.TextureMip,
      AssetFilter.Models => kind is AssetKind.Model or AssetKind.ModelData,
      AssetFilter.Animations => kind == AssetKind.Animation,
      AssetFilter.Effects => kind == AssetKind.Effect,
      AssetFilter.Text => kind == AssetKind.Text,
      _ => true,
    };
  }

  public static string FormatSize(long bytes)
  {
    if (bytes < 1024)
      return bytes + " B";
    if (bytes < 1024 * 1024)
      return (bytes / 1024.0).ToString("0.0") + " KB";
    if (bytes < 1024L * 1024 * 1024)
      return (bytes / (1024.0 * 1024)).ToString("0.0") + " MB";
    return (bytes / (1024.0 * 1024 * 1024)).ToString("0.00") + " GB";
  }

}
