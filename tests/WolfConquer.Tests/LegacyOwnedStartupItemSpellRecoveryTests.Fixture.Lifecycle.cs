using System.Buffers;
using System.Net;
using System.Runtime.ExceptionServices;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

internal sealed partial class LegacyOwnedStartupItemSpellRecoveryFixture
{
    internal Task StageAsync(int boundary) => ProducePendingAsync(LegacyJournalInterruption.StopAfter(boundary), boundary);
    internal Task StopAfterStageFlushedAsync(int ordinal) =>
        ProducePendingAsync(LegacyJournalInterruption.StopAfterStageFlushed(ordinal), null);
    private byte[] Request() => new MsgItemUsagePacket(ItemUid, mode == "book" ? 0UL : 102UL,
        mode is "rune-equip" or "rune-displace" ? MsgItemUsageAction.EquipRune : mode == "book" ? MsgItemUsageAction.Equip : MsgItemUsageAction.UnequipRune,
        1234, 0, 0, 0).ToFrameBytes();

    private async Task ProducePendingAsync(LegacyJournalInterruption interruption, int? boundary)
    {
        var original = Current; var native = original.NativeGameClient!;
        var spellMap = Assert.IsType<LegacyNativeSpellStorage>(native.MySpells).ClientSpells;
        var itemBytes = PlayerItemSnapshotLoader.ToLegacyBytes(original.PlayerItems);
        int first = sent.Count;
        using (interruption)
        {
            Handler.ItemSpellJournalInterruption = interruption;
            try { await SendAsync(Request()); }
            finally { Handler.ItemSpellJournalInterruption = null; }
            Assert.True(interruption.PendingObserved, Log.ToString());
            if (boundary is { } count) Assert.Equal(Enumerable.Range(0, count + 1), interruption.Visited);
        }
        Assert.True(Runtime.Recovery.IsBlocked); Assert.Same(original, Current);
        Assert.Equal(first, sent.Count); Assert.Same(spellMap, Assert.IsType<LegacyNativeSpellStorage>(native.MySpells).ClientSpells);
        Assert.Equal(itemBytes, PlayerItemSnapshotLoader.ToLegacyBytes(Current.PlayerItems));
        AssertNativeMatchesCurrent(expectedBlocked: true);
        var record = JsonNode.Parse(File.ReadAllBytes(PendingPath))!.AsObject(); Assert.Equal(mode == "book" ? 9 : 14, record["Version"]!.GetValue<int>());
        After = new(StringComparer.OrdinalIgnoreCase); var order = new List<string>();
        foreach (var change in record["Changes"]!.AsArray())
        {
            Assert.Equal((long)Uid, change!["FileId"]!.GetValue<long>());
            var path = (LegacyTradeJournalFileKind)change["Kind"]!.GetValue<int>() switch
            {
                LegacyTradeJournalFileKind.Character => CharacterPath,
                LegacyTradeJournalFileKind.Items => ItemsPath,
                LegacyTradeJournalFileKind.Spells => SpellsPath,
                _ => throw new InvalidDataException("Unexpected genuine item/spell target."),
            };
            var recordedBefore = change["Before"] is null ? null : Convert.FromBase64String(change["Before"]!.GetValue<string>());
            Assert.Equal(Before[path], recordedBefore);
            Assert.True(After.TryAdd(path, Convert.FromBase64String(change["After"]!.GetValue<string>()))); order.Add(path);
        }
        Assert.Equal(new[] { CharacterPath, ItemsPath, SpellsPath }, order); OrderedTargets = order;
        var domain = record["CharacterItemsSpellsDomain"]!;
        Assert.Equal(Data.Users, domain["Users"]!.GetValue<string>()); Assert.Equal(Data.Items, domain["Items"]!.GetValue<string>());
        Assert.Equal(Data.Spells, domain["Spells"]!.GetValue<string>());
        AssertExpectedCapturedImages(); Assert.Equal(RegistryState, File.ReadAllBytes(Data.State));
        if (boundary is { } replaced)
            for (var index = 0; index < order.Count; index++)
            {
                var expected = index < replaced ? After[order[index]] : Before[order[index]];
                if (expected is null) Assert.False(File.Exists(order[index]));
                else Assert.Equal(expected, File.ReadAllBytes(order[index]));
            }
    }

    private async Task LoginAsync()
    {
        var cancellation = new CancellationTokenSource(); lifetime = cancellation; resources.Add(cancellation);
        var native = new LegacyNativeGameClientRuntime();
        connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
            (bytes, token) => { token.ThrowIfCancellationRequested(); sent.Add(bytes.ToArray()); return ValueTask.CompletedTask; },
            _ => { cancellation.Cancel(); return ValueTask.CompletedTask; }, native);
        await SendAsync(new LegacyMsgLoginGamePacket(Uid).ToFrameBytes());
        Assert.Equal(Uid, Current.Character.Uid); Assert.Same(native, Current.NativeGameClient);
        Assert.NotNull(native.OrdinarySpellBinding); Assert.True(LegacySecondaryPasswordRuntime.IsAdmitted(connection, Current));
    }
    private async Task SendAsync(byte[] bytes) => await Handler.HandleFrameAsync(connection!, ReadFrame(bytes), lifetime!.Token);
    private static PacketFrame ReadFrame(byte[] bytes)
    { Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _)); return frame; }

    internal async Task AssertFreshFrameAsync(LegacyOwnedAdmissionRuntime runtime)
    {
        resources.Add(new NativeLoginBeastMetadataScope(1000));
        Handler = new WorldServerFrameHandler(Data.Loader, null, BuildCatalog(), LegacyRuntimeUpdateLoopOptions.Disabled,
            TextWriter.Null, protocolProfile: ConquerProtocolProfile.VisibleDx8D13, ownedAdmission: runtime);
        await LoginAsync(); AssertNativeMatchesCurrent();
        Assert.Equal(After[ItemsPath], PlayerItemSnapshotLoader.ToLegacyBytes(Current.PlayerItems));
        Assert.Equal(PlayerSpellSnapshotLoader.Parse(After[SpellsPath]).Spells, Current.PlayerSpells.Spells);
        var saved = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(After[CharacterPath], out _));
        Assert.Equal(saved.Money, Current.Character.Money); Assert.Equal(saved.ConquerPoints, Current.Character.ConquerPoints);
        Assert.Equal(saved.MinHitPoints, Current.Character.MinHitPoints);
    }

    internal async Task AssertReplayedBookRequestDoesNotConsumeAsync()
    {
        var files = Data.Files(); var current = Current; int first = sent.Count;
        await SendAsync(Request());
        Assert.Same(current, Current); Data.AssertFiles(files); AssertNativeMatchesCurrent();
        if (Stack == 1) Assert.Equal(first, sent.Count);
        else
        {
            byte[][] expected = [LegacyNpcReplyPacket.ForDialog("You already have this spell.").ToFrameBytes(),
                LegacyNpcReplyPacket.ForClosedOption("I~see.").ToFrameBytes(), LegacyNpcReplyPacket.ForFinish().ToFrameBytes()];
            Assert.Equal(expected.Length, sent.Count - first);
            for (var index = 0; index < expected.Length; index++) Assert.Equal(expected[index], sent[first + index]);
        }
    }

    internal async Task CloseAsync()
    {
        var errors = new List<Exception>();
        if (connection is not null)
        {
            try { await connection.DisconnectAsync(); } catch (Exception error) { errors.Add(error); }
            try { await connection.CompleteTransportCloseAsync(); } catch (Exception error) { errors.Add(error); }
        }
        connection = null;
        for (var index = runtimes.Count - 1; index >= 0; index--)
            try { await runtimes[index].DisposeAsync(); } catch (Exception error) { errors.Add(error); }
        runtimes.Clear();
        for (var index = resources.Count - 1; index >= 0; index--)
            try { resources[index].Dispose(); } catch (Exception error) { errors.Add(error); }
        resources.Clear(); lifetime = null;
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException(errors);
    }
}
