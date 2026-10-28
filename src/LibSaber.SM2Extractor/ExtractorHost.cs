namespace LibSaber.SM2Extractor;

public static class ExtractorHost
{

  public static int Run(string[] args, TextWriter stdout, TextWriter stderr)
  {
    if (args.Length == 2 && args[0] == "redecode-bc5")
    {
      // Repair exports written before the BC5 byte-order fix (f25ea8b): rewrite BC5 mask PNG/TGA previews from the .dds beside them.
      var (done, skipped) = LibSaber.SM2.Assets.Export.Bc5PreviewFix.Run(args[1], s => stdout.WriteLine("Fixed " + s));
      stdout.WriteLine($"Summary: bc5Fixed={done} skipped={skipped}");
      return 0;
    }

    if (!ExtractorOptions.TryParse(args, out var options, out var error))
    {
      stderr.WriteLine(error);
      stderr.WriteLine();
      stderr.WriteLine(ExtractorOptions.Usage);
      return 1;
    }

    if (options.Verb == ExtractorVerb.Help)
    {
      stdout.WriteLine(ExtractorOptions.Usage);
      return 0;
    }

    try
    {
      return new ExtractorEngine(stdout, stderr).Execute(options);
    }
    catch (Exception ex)
    {
      stderr.WriteLine(ex.Message);
      return 1;
    }
  }

}
