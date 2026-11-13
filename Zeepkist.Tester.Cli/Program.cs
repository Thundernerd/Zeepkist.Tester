using System;
using System.IO;
using System.Linq;

namespace TNRD.Zeepkist.Tester.Cli;

internal static class Program
{
    private static int Main(string[] args)
    {
        return Run(args, Console.Out, Environment.CurrentDirectory);
    }

    internal static int Run(string[] args, TextWriter output, string folder)
    {
        if (args.Length == 0)
        {
            WriteUsage(output);
            return 0;
        }

        var arguments = args.Skip(1).ToList();
        var exitCode = args[0] switch
        {
            "check" => CheckCommand.Run(arguments, output, folder),
            "schema" => SchemaCommand.Run(arguments, output, folder),
            "init" => InitCommand.Run(arguments, output, folder),
            "docs" => DocsCommand.Run(arguments, output, folder),
            "convert" => ConvertCommand.Run(arguments, output, folder),
            _ => null,
        };
        if (exitCode != null)
            return exitCode.Value;

        WriteUsage(output);
        return 1;
    }

    private static void WriteUsage(TextWriter output)
    {
        output.WriteLine("Usage: ztest <command>");
        output.WriteLine(CheckCommand.Usage);
        output.WriteLine(SchemaCommand.Usage);
        output.WriteLine(InitCommand.Usage);
        output.WriteLine(DocsCommand.Usage);
        output.WriteLine(ConvertCommand.Usage);
    }
}
