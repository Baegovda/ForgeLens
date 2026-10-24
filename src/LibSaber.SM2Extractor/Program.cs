namespace LibSaber.SM2Extractor;

public static class Program
{

  public static int Main(string[] args)
    => ExtractorHost.Run(args, Console.Out, Console.Error);

}
