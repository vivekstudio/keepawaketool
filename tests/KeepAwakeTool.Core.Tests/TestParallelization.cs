using Xunit;

// The suite is small (sub-second) and one test exercises a real OS FileSystemWatcher
// (ConfigurationStore change events). Under xUnit's default parallel collections the
// OS notification can arrive slower than the test's wait window on a loaded machine
// (e.g. CI), causing an intermittent timeout. Parallelism buys nothing here, so we
// disable it for deterministic runs everywhere (including `dotnet test` in CI).
[assembly: CollectionBehavior(DisableTestParallelization = true)]
