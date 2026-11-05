using System.Collections.Generic;

namespace TNRD.Zeepkist.Tester;

/// <summary>
/// A mod a run tests, from run.json.
/// </summary>
internal sealed class ModUnderTest
{
    public string Guid { get; set; }

    /// <summary>
    /// GUIDs the mod had before, as a copy installed from back then loads next to it instead of being skipped.
    /// </summary>
    public List<string> OldGuids { get; set; } = [];
}
