namespace WolfConquer.Core.Runtime;

public sealed record WorldPublicHeartbeat(
    int SchemaVersion, string Source, Guid InstanceId, DateTimeOffset CapturedAtUtc,
    DateTimeOffset? StartedAtUtc, long UptimeSeconds, int? OnlinePlayers,
    string Profile, string? ClientBuild, string Environment, string State);

internal enum PublicListenerState { Created, Listening, Stopping, Stopped, Faulted }
internal readonly record struct PublicListenerSnapshot(
    PublicListenerState State, DateTimeOffset? StartedAtUtc, long UptimeSeconds);
internal enum PublicRecoveryState { Starting, Ready, Required, Unavailable }
internal readonly record struct PublicWorldSnapshot(string Profile, int? OnlinePlayers, PublicRecoveryState Recovery);
