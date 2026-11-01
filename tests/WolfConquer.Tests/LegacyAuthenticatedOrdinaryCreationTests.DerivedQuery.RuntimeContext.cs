using System.Reflection;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyAuthenticatedOrdinaryCreationTests
{
    [Fact]
    public Task ActualAdmittedFrameContextRetainsOwnersThroughEquipmentGrant() => RunPhysicalDeathAsync(async f =>
    {
        var source = f.TargetSession;
        var context = f.Owner.Handler.CreateMonsterRuntimeContext(new(f.Target, source));
        var trades = typeof(WorldServerFrameHandler).GetField("_tradeStates", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(f.Owner.Handler);
        Assert.Same(trades, context.Trades);
        Assert.Same(f.Owner.Runtime.Recovery, context.TradeRecovery);
        Assert.Same(f.Owner.Store, context.Sessions);
        Assert.Same(source, context.Session);
        Assert.True(context.TradeRecovery!.IsReadyForCharacterItemsSpells(f.Owner.Data.Users, f.Owner.Data.Items, f.Owner.Data.Spells));
        Assert.False(source.PlayerSpells.ContainsSpell(20000));

        var before = await EquipForDerivedQueryAsync(f);

        f.Owner.Data.AssertFiles(before.After);
        AssertDerivedQueryHistory(f, before);
        Assert.True(f.Owner.Store.IsBoundSpellOwnerCurrent(before.Binding));
        Assert.Equal("20000:5", f.TargetSession.Character.Attributes["ManagedDerivedSpellOrigins"]);
        Assert.Equal((ushort)5, before.Binding.Storage.ClientSpells[20000].Level);
        Assert.Single(f.Owner.Frames(DeathTargetAccount), bytes => bytes.AsSpan().SequenceEqual(DeathGrantedGolden()));
        Assert.False(LegacyTradeJournal.HasPending(f.Owner.Data.Users));
    });

    [Fact]
    public Task BlockedRealFrameRejectsEquipmentBeforeAnyQueryGrant() => RunPhysicalDeathAsync(async f =>
    {
        var current = f.TargetSession;
        var binding = current.NativeGameClient!.OrdinarySpellBinding!;
        var context = f.Owner.Handler.CreateMonsterRuntimeContext(new(f.Target, current));
        Assert.Same(f.Owner.Runtime.Recovery, context.TradeRecovery);
        var files = f.Owner.Data.Files();
        var history = HistoryFiles(f.Owner);
        var ledger = File.ReadAllBytes(f.Owner.Data.State);
        int output = f.Owner.Frames(DeathTargetAccount).Count;
        context.TradeRecovery!.Block("owned-derived-query-refusal");

        await f.Owner.SendOnAsync(f.Target,
            new MsgItemUsagePacket(DeathArmorUid, 3, MsgItemUsageAction.Equip, 1234, 0, 0, 0).ToFrameBytes());

        Assert.Same(current, f.TargetSession);
        Assert.True(f.Owner.Store.IsBoundSpellOwnerCurrent(binding));
        Assert.False(current.PlayerSpells.ContainsSpell(20000));
        Assert.False(binding.Storage.ClientSpells.ContainsKey(20000));
        Assert.Equal(output, f.Owner.Frames(DeathTargetAccount).Count);
        f.Owner.Data.AssertFiles(files);
        AssertHistoricalFiles(history, HistoryFiles(f.Owner));
        Assert.Equal(ledger, File.ReadAllBytes(f.Owner.Data.State));
        Assert.False(LegacyTradeJournal.HasPending(f.Owner.Data.Users));
    });
}
