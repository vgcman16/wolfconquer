using System.Collections.Concurrent;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedAdmissionFixtureTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task D13ApiOrCliFixtureCommitsTheGenuineCycloneBeforeStartupAndDoesNotRegrantOnReplay(bool useCli)
    {
        var options = await CreateD13ProfileAsync(useCli);
        AssertProfileInitial(options, 1000);
        var before = Files(Destination);
        var loader = options.CreateSnapshotLoader();
        Assert.True(LegacyOwnedAdmissionRuntime.TryOpen(loader, options, out var opened, out var reason), reason);
        var runtime = Assert.IsType<LegacyOwnedAdmissionRuntime>(opened);
        using var log = new StringWriter();
        var sent = new ConcurrentQueue<byte[]>();
        ConquerTcpConnection? connection = null;
        var failures = new List<Exception>();
        var commitObserved = 0;
        try
        {
            var handler = new WorldServerFrameHandler(loader, null, LegacyItemWorldDataFixture.Create([], []),
                LegacyRuntimeUpdateLoopOptions.Disabled, log,
                protocolProfile: ConquerProtocolProfile.VisibleDx8D13, ownedAdmission: runtime);
            connection = new(null, new IPEndPoint(IPAddress.Loopback, 9958), (bytes, _) =>
            {
                var frame = bytes.ToArray();
                if (frame.AsSpan().SequenceEqual(LegacyMsgMessagePacket.AnswerOk().ToFrameBytes()) || IsProfileOpcode(frame, 2101))
                {
                    Assert.Contains("ordinary admission committed key=" + AccountUid, log.ToString(), StringComparison.Ordinal);
                    AssertProfileCommitted(options, before);
                    commitObserved++;
                }
                sent.Enqueue(frame);
                return ValueTask.CompletedTask;
            });
            var request = CollectionStartupFrame(new LegacyMsgLoginGamePacket(AccountUid).ToFrameBytes());
            await handler.HandleFrameAsync(connection, request);

            Assert.Equal(2, commitObserved); // actual AnswerOk and one newly granted2101
            var first = sent.ToArray();
            AssertD13ProfileHeroClasses(first);
            Assert.Equal(ProfileCycloneFrame, Assert.Single(first, value => IsProfileOpcode(value, 2101)));
            var spellIndex = Array.FindIndex(first, value => IsProfileOpcode(value, 2101));
            var sashIndex = Array.FindLastIndex(first, value => LegacyMsgUpdatePacket.TryParse(CollectionStartupFrame(value), out var update) &&
                update.Updates.Any(row => row.Type == LegacyMsgUpdateDataType.InventorySash));
            Assert.True(sashIndex >= 0 && spellIndex == sashIndex + 1);
            AssertProfileCommitted(options, before);
            var committed = Files(Destination);
            var firstCount = sent.Count;
            await handler.HandleFrameAsync(connection, request);
            Assert.Equal(firstCount, sent.Count);
            AssertFiles(Destination, committed);

            var confirmation = ProfileConfirmSpells();
            await handler.HandleFrameAsync(connection, CollectionStartupFrame(confirmation));
            var confirmationFrames = sent.ToArray()[firstCount..];
            Assert.Equal(2, confirmationFrames.Length);
            Assert.Equal(ProfileCycloneFrame, confirmationFrames[0]);
            Assert.Equal(confirmation, confirmationFrames[1]);
            AssertFiles(Destination, committed);
        }
        catch (Exception error) { failures.Add(error); }
        try { if (connection is not null) await connection.CompleteTransportCloseAsync(); } catch (Exception error) { failures.Add(error); }
        try { await runtime.DisposeAsync(); } catch (Exception error) { failures.Add(error); }
        RethrowOwnedAppearanceFailures(failures);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task D13RequiredCycloneJointSaveRefusesARealCharacterOrSpellReplacementLockBeforeAnySuccess(bool lockSpells)
    {
        if (!OperatingSystem.IsWindows()) return;
        var options = await CreateD13ProfileAsync(useCli: false);
        var loader = options.CreateSnapshotLoader();
        Assert.True(LegacyOwnedAdmissionRuntime.TryOpen(loader, options, out var opened, out var reason), reason);
        var runtime = Assert.IsType<LegacyOwnedAdmissionRuntime>(opened);
        var before = Files(Destination);
        var locked = Path.Combine(lockSpells ? options.SpellsRoot : options.UsersRoot,
            AccountUid + (lockSpells ? ".bin" : ".ini"));
        var sent = new ConcurrentQueue<byte[]>();
        ConquerTcpConnection? connection = null;
        var failures = new List<Exception>();
        try
        {
            var handler = new WorldServerFrameHandler(loader, null, LegacyItemWorldDataFixture.Create([], []),
                LegacyRuntimeUpdateLoopOptions.Disabled, TextWriter.Null,
                protocolProfile: ConquerProtocolProfile.VisibleDx8D13, ownedAdmission: runtime);
            connection = new(null, new IPEndPoint(IPAddress.Loopback, 9958),
                (bytes, _) => { sent.Enqueue(bytes.ToArray()); return ValueTask.CompletedTask; });
            using (var held = new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.Read))
                await handler.HandleFrameAsync(connection, CollectionStartupFrame(new LegacyMsgLoginGamePacket(AccountUid).ToFrameBytes()));
            Assert.DoesNotContain(sent, value => value.AsSpan().SequenceEqual(LegacyMsgMessagePacket.AnswerOk().ToFrameBytes()));
            Assert.DoesNotContain(sent, value => IsProfileOpcode(value, 2101) || IsProfileOpcode(value, 2221) || IsProfileOpcode(value, 22210));
            AssertFiles(Destination, before);
            Assert.Empty(loader.LoadPlayerSpellsByCharacterUid(AccountUid).Spells);
            Assert.False(LegacyTradeJournal.HasPending(options.UsersRoot));
        }
        catch (Exception error) { failures.Add(error); }
        try { if (connection is not null) await connection.CompleteTransportCloseAsync(); } catch (Exception error) { failures.Add(error); }
        try { await runtime.DisposeAsync(); } catch (Exception error) { failures.Add(error); }
        RethrowOwnedAppearanceFailures(failures);
    }
}
