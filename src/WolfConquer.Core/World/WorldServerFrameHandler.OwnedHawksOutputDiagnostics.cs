using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    private bool IsActualCommittedHawksOutput(ConquerTcpConnection connection, LegacyCommittedHawksOutputException error) =>
        error.IsFor(this, _sessions, connection);

    private async ValueTask ReportCommittedHawksOutputAsync(PacketFrame frame, LegacyCommittedHawksOutputException error)
    {
        await _log.WriteLineAsync(
            "WorldServer committed Hawks Ambition outcome; output interrupted " +
            $"{FormatFrameDiagnostic(frame)} exception={error}").ConfigureAwait(false);
    }
}
