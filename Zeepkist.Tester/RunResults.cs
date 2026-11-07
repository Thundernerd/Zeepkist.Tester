using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester;

/// <summary>
/// What a run has found so far, as results.json. It's written after every step, so a crash still leaves the steps
/// before it, and <see cref="Done"/> tells the runner the game is about to quit.
/// </summary>
internal sealed class RunResults
{
    private readonly string _folder;

    public RunResults(string runId, string folder, List<LoggedException> exceptions)
    {
        RunId = runId;
        Exceptions = exceptions;
        _folder = folder;
        Directory.CreateDirectory(folder);
        Write();
    }

    public string RunId { get; }
    public bool Done { get; private set; }
    public bool Passed { get; private set; }
    public List<RunStep> Steps { get; } = [];
    public List<string> Screenshots { get; } = [];

    public Dictionary<string, string> BlankScreenshots { get; } = [];

    /// <summary>
    /// The game's resolution, window mode and screen size, so a run at an unexpected size stands out.
    /// </summary>
    public string Display { get; set; } = "";

    /// <summary>
    /// Written with the next step, rather than each time the game logs one.
    /// </summary>
    public List<LoggedException> Exceptions { get; }

    [JsonIgnore]
    public bool AnyStepFailed => Steps.Exists(step => !step.Passed);

    /// <summary>
    /// A failed step or start in a scenario takes its own screenshot.
    /// </summary>
    [JsonIgnore]
    public bool AnyStepFailedOutsideAScenario => Steps.Exists(step => !step.Passed && step.Scenario.Length == 0);

    public string ScreenshotPath(string name)
    {
        return Path.Combine(_folder, FileName(name));
    }

    /// <summary>
    /// Lists a screenshot saved at its <see cref="ScreenshotPath"/>.
    /// </summary>
    public void AddScreenshot(string name)
    {
        Screenshots.Add(FileName(name));
    }

    public void MarkBlank(string name, string color)
    {
        BlankScreenshots[FileName(name)] = color;
    }

    public void Add(RunStep step)
    {
        Steps.Add(step);
        Write();
    }

    public void Finish()
    {
        Done = true;
        Passed = Steps.Count > 0 && !AnyStepFailed;
        Write();
    }

    private static string FileName(string screenshot)
    {
        return screenshot + ".png";
    }

    /// <summary>
    /// Writes to a temporary file first, so the runner never reads half a file.
    /// </summary>
    private void Write()
    {
        var path = Path.Combine(_folder, "results.json");
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonConvert.SerializeObject(this, Formatting.Indented));
        if (File.Exists(path))
            File.Replace(temporary, path, null);
        else
            File.Move(temporary, path);
    }
}
