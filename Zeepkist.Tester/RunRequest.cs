using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace TNRD.Zeepkist.Tester;

/// <summary>
/// The run the runner asked for, from the run.json it deploys. The run id on the command line has to match it, so a
/// run.json left behind by a crashed run can't start tests in normal play.
/// </summary>
internal sealed class RunRequest
{
    public const string Flag = "-ztest";

    public string RunId { get; set; }

    public List<ModUnderTest> Mods { get; set; } = [];

    public static bool TryRead(string folder, string[] args, out RunRequest request)
    {
        request = null;
        var flagAt = Array.IndexOf(args, Flag);
        if (flagAt < 0 || flagAt + 1 >= args.Length)
            return false;

        var path = Path.Combine(folder, "run.json");
        if (!File.Exists(path))
            return false;

        try
        {
            request = JsonConvert.DeserializeObject<RunRequest>(File.ReadAllText(path));
        }
        catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
        {
            return false;
        }

        if (request?.RunId == args[flagAt + 1])
            return true;

        request = null;
        return false;
    }
}
