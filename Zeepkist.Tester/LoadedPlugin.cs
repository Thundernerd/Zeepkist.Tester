namespace TNRD.Zeepkist.Tester;

/// <summary>
/// What <see cref="BuildCheck"/> looks at of a plugin BepInEx loaded.
/// </summary>
internal sealed class LoadedPlugin
{
    public LoadedPlugin(string guid, string name, string location)
    {
        Guid = guid;
        Name = name;
        Location = location;
    }

    public string Guid { get; }
    public string Name { get; }
    public string Location { get; }
}
