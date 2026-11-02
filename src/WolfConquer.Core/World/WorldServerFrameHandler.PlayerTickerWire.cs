using System.Globalization;
using System.Runtime.CompilerServices;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public sealed partial class WorldServerFrameHandler
{
    internal sealed class PlayerTickXpPermit { }
    private sealed class PlayerTickXpData(WorldServerFrameHandler frame, ConquerTcpConnection connection,
        long uid, object identity, ulong xp, byte[] bytes)
    {
        internal readonly WorldServerFrameHandler Frame = frame;
        internal readonly ConquerTcpConnection Connection = connection;
        internal readonly long Uid = uid;
        internal readonly object Identity = identity;
        internal readonly ulong Xp = xp;
        internal readonly byte[] Bytes = bytes, Image = bytes.ToArray();
        internal bool Committed, Claimed;
    }
    private static readonly ConditionalWeakTable<PlayerTickXpPermit, PlayerTickXpData> playerTickXpPermits = new();

    private PlayerTickXpPermit? PreparePlayerTickXpWire(ConquerTcpConnection connection, long uid,
        object identity, WorldSession current, IReadOnlyList<LegacyRuntimeUpdate> updates)
    {
        if (current.NativeGameClient?.OrdinarySpellBinding is null || updates.Count != 1 ||
            updates[0] is not { RuntimeXpCount: { } xp, RuntimeXpListActive: null,
                RuntimeStamina: null, MessageBoxPrompt: null } update) return null;
        var permit = new PlayerTickXpPermit();
        playerTickXpPermits.Add(permit, new(this, connection, uid, identity, xp, update.FrameBytes.ToArray()));
        return permit;
    }
    private static void DiscardPlayerTickXpWire(PlayerTickXpPermit? permit)
    { if (permit is not null) playerTickXpPermits.Remove(permit); }
    private static void CommitPlayerTickXpWire(PlayerTickXpPermit permit)
    { if (!playerTickXpPermits.TryGetValue(permit, out var data) || data.Committed) throw new InvalidOperationException("playerTickXpWireRetired"); data.Committed = true; }

    internal static bool TryReadPlayerTickXpWire(PlayerTickXpPermit permit, WorldServerFrameHandler frame,
        WorldSessionStore store, ConquerTcpConnection connection, object identity, WorldSession current,
        bool claim, out byte[] bytes)
    {
        bytes = null!;
        if (!playerTickXpPermits.TryGetValue(permit, out var data) || !data.Committed ||
            (claim && data.Claimed) || !ReferenceEquals(data.Frame, frame) || !ReferenceEquals(frame._sessions, store) ||
            !ReferenceEquals(data.Connection, connection) || !ReferenceEquals(data.Identity, identity) ||
            current.Character.Uid != data.Uid || frame._tradeRecovery.IsBlocked || frame._tradeStates.HasConnectionEnded(connection) ||
            frame._ownedAdmissionRuntime is { IsStopping: true } ||
            !frame._creationLifetimes.TryGetValue(connection, out var lifetime) || !lifetime.IsCurrent(connection) ||
            !data.Bytes.AsSpan().SequenceEqual(data.Image)) return false;
        try
        {
            _ = LegacyWindWalkerStatusProjection.Read(current.Character);
            var map = LegacyNativeAttributeMapProof.CaptureRaw(current.Character.Attributes);
            var edit = new RuntimeTickAttributeEdit(current.Character, ["XPCount"]);
            if (!edit.Owned.TryGetValue("XPCount", out var raw) ||
                raw != data.Xp.ToString(CultureInfo.InvariantCulture) ||
                LegacyPlayerDeathState.HasStatusFlag(current.Character.Attributes, LegacyRuntimeUpdateTicker.XpListFlag) ||
                !map.IsCurrentPlain()) return false;
        }
        catch (Exception error) when (error is InvalidDataException or InvalidOperationException or ArgumentException or FormatException or OverflowException)
        { return false; }
        if (claim) data.Claimed = true;
        bytes = data.Bytes; return true;
    }
}
