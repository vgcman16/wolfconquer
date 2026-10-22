namespace WolfConquer.Updates.Hosting;

public sealed record FeedHostOptions(
    string PublishRoot,
    int Port = 48237,
    IReadOnlyCollection<string>? BootstrapFiles = null,
    int MaxConcurrentRequests = 4);
