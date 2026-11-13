using TNRD.Zeepkist.Tester.Sdk;

namespace TNRD.Zeepkist.Tester.Cli;

/// <summary>
/// What <see cref="RegistryLoader.Load"/> found.
/// </summary>
/// <param name="Config">The nearest config, or null when there's none.</param>
/// <param name="Registry">The registry, or null when there's a <paramref name="Problem"/>.</param>
/// <param name="Problem">Why there's no registry, or null.</param>
internal sealed record LoadedRegistry(TestConfig Config, StepRegistry Registry, string Problem);
