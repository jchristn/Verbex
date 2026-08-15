using global::Xunit;

// The shared suites toggle an ambient storage mode on TestContext immediately before each
// case executes, so cases must run sequentially rather than in parallel.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
