using Xunit;

// The tests replace the global console output and the global Spectre console,
// so they must not run in parallel.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
