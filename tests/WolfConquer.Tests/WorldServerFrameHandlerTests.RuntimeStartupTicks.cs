using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    [Fact]
    public async Task HandlerRuntimeLoopSendsLegacyXpAndStaminaAfterStartupWhenEnabled()
    {
        Directory.CreateDirectory(_usersRoot);
        WriteSnapshot(1293600, uid: 1293600);
        using var cancel = new CancellationTokenSource();
        var handler = new WorldServerFrameHandler(
            new CharacterSnapshotLoader(_usersRoot),
            handoffService: null,
            worldData: null,
            new LegacyRuntimeUpdateLoopOptions(
                Enabled: true,
                TimeSpan.FromMilliseconds(5),
                TimeProvider.System));
        var sent = new RuntimeThreadSafeFrameCapture();
        var connection = CreateSendingConnection(sent);
        byte[][] observed = [];
        Exception? primary = null;
        try
        {
            await handler.HandleFrameAsync(
                connection,
                ReadFrame(new LegacyMsgLoginGamePacket(1293600).ToFrameBytes()),
                cancel.Token);
            await WaitUntilAsync(() =>
            {
                // One real capture snapshot: startup/global packets may precede
                // or interleave these updates and are not a timer completion signal.
                observed = sent.ToArray();
                var updates = ParseLegacyMsgUpdates(observed);
                return HasSingleLegacyMsgUpdate(updates, LegacyMsgUpdateDataType.XPCircle, 1) &&
                    HasSingleLegacyMsgUpdate(updates, LegacyMsgUpdateDataType.Stamina, 5);
            }, TimeSpan.FromSeconds(2));
        }
        catch (Exception error) { primary = error; throw; }
        finally
        {
            try { await cancel.CancelAsync(); }
            catch (Exception cleanup) when (primary is not null) { throw new AggregateException(primary, cleanup); }
        }

        int xp = FindSingleUpdateIndex(observed, 1_293_600, LegacyMsgUpdateDataType.XPCircle, 1);
        int stamina = FindSingleUpdateIndex(observed, 1_293_600, LegacyMsgUpdateDataType.Stamina, 5);
        Assert.True(xp < stamina, "The actual first XP update must precede the actual first stamina update.");
        AssertSingleUpdate(observed[xp], 1_293_600, LegacyMsgUpdateDataType.XPCircle, 1);
        AssertSingleUpdate(observed[stamina], 1_293_600, LegacyMsgUpdateDataType.Stamina, 5);
    }
}
