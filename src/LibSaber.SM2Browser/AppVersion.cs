// R71 (t148u): window-title version, read from the assembly (csproj <Version>; the git hash rides in InformationalVersion "+<hash>" = file ProductVersion, not shown).
using System.Reflection;

namespace LibSaber.SM2Browser;

internal static class AppVersion
{
  /// <summary>Plain version for the title, e.g. "0.71.0" (csproj &lt;Version&gt;, no hash).</summary>
  public static string Version
  {
    get
    {
      var info = typeof(AppVersion).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "?";
      var plus = info.IndexOf('+');
      return plus < 0 ? info : info[..plus];
    }
  }
}
