using Xunit;

// Keep test execution deterministic and aligned with KeepAwakeTool.Core.Tests
// (see the matching file there). The full suite is sub-second; disabling
// parallel collections removes CI flakiness with no meaningful cost.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
