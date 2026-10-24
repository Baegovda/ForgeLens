namespace LibSaber.SM2Extractor.Tests;

internal sealed class TempDirectory : IDisposable
{

  public TempDirectory()
  {
    Path = System.IO.Path.Combine(
      System.IO.Path.GetTempPath(),
      "libsaber-sm2-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(Path);
  }

  public string Path { get; }

  public void Dispose()
  {
    if (Directory.Exists(Path))
      Directory.Delete(Path, recursive: true);
  }

}
