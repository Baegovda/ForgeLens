namespace LibSaber.SM2Extractor;

public static class ExtractorHost
{

  public static int Run(string[] args, TextWriter stdout, TextWriter stderr)
  {
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
