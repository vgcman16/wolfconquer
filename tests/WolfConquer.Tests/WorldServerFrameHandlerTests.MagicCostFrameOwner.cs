using System.Net;
using System.Reflection;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task FrameUnboundMagicCostBenignEditKeepsActualRecoveryClear(bool root) =>
        RunFrameUnboundMagicCostAsync(root, "positive");

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task FrameUnboundMagicCostPendingManaDriftFencesActualFrameOwner(bool root) =>
        RunFrameUnboundMagicCostAsync(root, "drift");

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task FrameUnboundMagicCostSendFaultFencesActualFrameOwner(bool root) =>
        RunFrameUnboundMagicCostAsync(root, "transport");

    private async Task RunFrameUnboundMagicCostAsync(bool root, string change)
    {
        const uint uid = 1293600;
        const ushort id = 1000;
        var users = Path.Combine(_usersRoot, "Users");
        var items = Path.Combine(_usersRoot, "PlayersItems");
        var spells = Path.Combine(_usersRoot, "PlayersSpells");
        var profs = Path.Combine(_usersRoot, "PlayersProfs");
        var gear = Path.Combine(_usersRoot, "PlayersCombatGear");
        var quests = Path.Combine(_usersRoot, "Quests");
        foreach (var path in new[] { users, items, spells, profs, gear, quests }) Directory.CreateDirectory(path);
        WriteSnapshot(users, uid, uid, mapId: 1000, classCode: 142, level: 120,
            money: 100, conquerPoints: 50, minHitPoints: 1000, secondClass: 0,
            extraCharacterLines: "Stamina=100\r\nOpaqueFrameInput=retained");
        var characterPath = Path.Combine(users, uid + ".ini");
        var text = File.ReadAllText(characterPath);
        Assert.Equal(2, text.Split("MinMana=5000", StringSplitOptions.None).Length);
        File.WriteAllText(characterPath, text.Replace("MinMana=5000", "MinMana=1000", StringComparison.Ordinal));
        new PlayerItemSnapshotLoader(items).SaveByCharacterUid(uid, PlayerItemSnapshot.Empty);
        new PlayerSpellSnapshotLoader(spells).SaveByCharacterUid(uid,
            new([new PlayerSpellSnapshotEntry(id, 0, 0, 0, 0, 0)]));
        new PlayerProficiencySnapshotLoader(profs).SaveByCharacterUid(uid, PlayerProficiencySnapshot.Empty);
        new CombatGearSnapshotLoader(gear).SaveByCharacterUid(uid, CombatGearSnapshot.Empty);
        var loader = new CharacterSnapshotLoader(users, items, spells, profs, gear, quests);
        new PlayerQuestSnapshotLoader(quests).SaveByCharacterUid(uid, loader.LoadByAccountId(uid)!.PlayerQuests);
        new LegacyMailboxStore(_usersRoot).Save(uid);
        var world = LegacyMagicAttackRuntimeTests.CreateMagicCostFrameUnboundCatalog();
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        var ticket = handoff.Issue(uid, "magic-cost-frame", ConquerRuntimeDefaults.WorldServer);
        using var log = new StringWriter();
        var handler = new WorldServerFrameHandler(loader, handoff, world, LegacyRuntimeUpdateLoopOptions.Disabled,
            log, protocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        var sent = new List<byte[]>();
        Action<byte[]>? onSend = null;
        var connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (memory, token) =>
        {
            token.ThrowIfCancellationRequested();
            var bytes = memory.ToArray();
            onSend?.Invoke(bytes);
            sent.Add(bytes);
            return ValueTask.CompletedTask;
        });
        Exception? primary = null;
        try
        {
            // Actual ticket redemption/startup, not a manually inserted session.
            await handler.HandleFrameAsync(connection, ReadFrame(new InternalWorldEnterRequestPacket(ticket.Token).ToFrameBytes()));
            var store = Assert.IsType<WorldSessionStore>(typeof(WorldServerFrameHandler)
                .GetField("_sessions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(handler));
            Assert.Same(handler, typeof(WorldSessionStore)
                .GetField("framePersistenceOwner", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(store));
            Assert.True(store.TryGet(connection, out var owner), log.ToString());
            var before = Assert.IsType<WorldSession>(owner);
            // This intentionally selected existing managed/legacy connection
            // branch has a real Frame owner/recovery but no native issuance.
            Assert.Null(connection.NativeGameClient); Assert.Null(before.NativeGameClient);
            Assert.Equal(1000L, before.Character.MinMana);
            Assert.Equal("100", before.Character.Attributes["Stamina"]);
            var context = handler.CreateMonsterRuntimeContext(new(connection, before));
            var recovery = Assert.IsType<LegacyTradeRecoveryState>(context.TradeRecovery);
            Assert.True(recovery.IsReadyForUsers(users)); Assert.False(recovery.IsBlocked);
            Assert.True(LegacyNativeClientPublication.TryCaptureIdentity(store, connection, uid, out var identity));
            Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(before.PlayerSpells, world, id, out _, out var spell));
            var unchangedFiles = Directory.GetFiles(_usersRoot, "*", SearchOption.AllDirectories)
                .Where(path => !string.Equals(path, characterPath, StringComparison.OrdinalIgnoreCase))
                .ToDictionary(path => path, File.ReadAllBytes);
            var start = sent.Count;
            var fault = new IOException("actual-frame-unbound-cost-send-fault");
            WorldSession? committed = null;
            WorldSession? changed = null;
            onSend = bytes =>
            {
                if (committed is not null || !LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) ||
                    !update.Updates.Any(row => row.Type == LegacyMsgUpdateDataType.Stamina && row.Value == 90)) return;
                Assert.False(WorldSessionStore.IsCurrentThreadInsideAnyConnectionGate);
                Assert.True(store.TryGet(connection, out committed));
                Assert.Equal(950L, committed!.Character.MinMana); Assert.Equal(950L, loader.LoadByAccountId(uid)!.MinMana);
                Assert.Equal("90", committed.Character.Attributes["Stamina"]); Assert.True(committed.MagicCooldowns!.ContainsKey(id));
                if (change == "transport") throw fault;
                store.Set(connection, change == "drift"
                    ? committed with { Character = committed.Character with { MinMana = 88 } }
                    : committed with { ActiveNpcId = 17 });
                Assert.True(store.TryGet(connection, out changed));
                Assert.True(LegacyNativeClientPublication.TryCurrent(store, connection, uid, identity, out var sameIssuance));
                Assert.Same(changed, sameIssuance); Assert.Null(changed!.NativeGameClient);
            };

            WorldSession? continuation = null;
            if (change == "transport")
            {
                var failure = await Assert.ThrowsAsync<WorldSessionPublicationException>(async () =>
                    { await InvokeFrameUnboundMagicCostAdmission(context, before, spell, root); });
                Assert.Same(fault, failure.InnerException); Assert.Equal(WorldSessionPublicationStage.MagicCostOutput, failure.Stage);
                Assert.Same(committed, failure.PublishedSession); Assert.Null(failure.FencingFailure);
            }
            else continuation = await InvokeFrameUnboundMagicCostAdmission(context, before, spell, root);
            onSend = null;
            var accepted = Assert.IsType<WorldSession>(committed);
            var frames = sent.Skip(start).ToArray();
            Assert.Equal("90", accepted.Character.Attributes["Stamina"]);
            Assert.True(accepted.MagicCooldowns!.ContainsKey(id));
            if (change == "positive")
            {
                Assert.Same(changed, continuation); Assert.True(store.TryGet(connection, out var current)); Assert.Same(continuation, current);
                Assert.Equal((uint?)17, continuation!.ActiveNpcId); Assert.False(recovery.IsBlocked);
                AssertFrameUnboundMagicCostWire(frames, spell, uid, root);
            }
            else
            {
                Assert.Null(continuation); Assert.True(recovery.IsBlocked); Assert.False(store.TryGet(connection, out _));
                Assert.False(LegacyNativeClientPublication.TryCurrent(store, connection, uid, identity, out _));
                if (change == "transport") Assert.Empty(frames);
                else
                {
                    Assert.Equal(88L, changed!.Character.MinMana);
                    Assert.Equal(accepted.MagicCooldowns[id], changed.MagicCooldowns![id]);
                    Assert.Equal(LegacyMsgUpdatePacket.CreateLegacyRuntimeStaminaTickUpdate(uid, 90).ToFrameBytes(), Assert.Single(frames));
                }
            }
            var saved = loader.LoadByAccountId(uid)!;
            Assert.Equal(change == "drift" ? 88L : 950L, saved.MinMana); Assert.Equal(before.Character.MinHitPoints, saved.MinHitPoints);
            Assert.Equal("retained", saved.Attributes["OpaqueFrameInput"]);
            foreach (var pair in unchangedFiles) Assert.Equal(pair.Value, File.ReadAllBytes(pair.Key));
            Assert.False(LegacyTradeJournal.HasPending(users));
            Assert.DoesNotContain("runtime-exception", log.ToString(), StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("PARITY-BUG", log.ToString(), StringComparison.Ordinal);
        }
        catch (Exception error) { primary = error; }
        finally { onSend = null; }
        try { await connection.DisconnectAsync(); await connection.CompleteTransportCloseAsync(); }
        catch (Exception cleanup)
        {
            if (primary is not null) throw new AggregateException(primary, cleanup);
            throw;
        }
        if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();
        Assert.Equal(change == "drift" ? 88L : 950L, loader.LoadByAccountId(uid)!.MinMana);
    }

    private static void AssertFrameUnboundMagicCostWire(byte[][] frames, LegacyMagicRuntimeSpell spell, uint uid, bool root)
    {
        Assert.Equal(root ? 2 : 3, frames.Length);
        Assert.Equal(LegacyMsgUpdatePacket.CreateLegacyRuntimeStaminaTickUpdate(uid, 90).ToFrameBytes(), frames[0]);
        Assert.Equal(new LegacyMsgUpdatePacket(uid, [new(LegacyMsgUpdateDataType.Mana, 950)]).ToFrameBytes(), frames[1]);
        if (!root) Assert.Equal(LegacyMagicColdTimePacket.FromStartedCooldown(spell).ToFrameBytes(), frames[2]);
    }

    private static async Task<WorldSession?> InvokeFrameUnboundMagicCostAdmission(
        WorldPacketHandlerContext context, WorldSession owner, LegacyMagicRuntimeSpell spell, bool root)
    {
        var method = typeof(WorldPacketHandlers).GetMethod(
            root ? "TryPrepareLegacyRootFirstFamilyCanUseAsync" : "TryPrepareLegacyPlayerMagicCastAsync",
            BindingFlags.Static | BindingFlags.NonPublic, null,
            [typeof(WorldPacketHandlerContext), typeof(WorldSession), typeof(LegacyMagicRuntimeSpell),
             typeof(CancellationToken), typeof(object)], null)!;
        object? result;
        try { result = method.Invoke(null, [context, owner, spell, CancellationToken.None, null]); }
        catch (TargetInvocationException error) when (error.InnerException is { } inner)
        { ExceptionDispatchInfo.Capture(inner).Throw(); throw; }
        return await Assert.IsType<ValueTask<WorldSession?>>(result);
    }
}
