using System;
using System.IO;

namespace TNRD.Zeepkist.Tester.Cli;

internal static class Program
{
    private static int Main()
    {
        return Run(Console.Out);
    }

    internal static int Run(TextWriter output)
    {
        output.WriteLine("Usage: ztest <command>");
        output.WriteLine("There are no commands yet.");
        return 0;
    }
}
