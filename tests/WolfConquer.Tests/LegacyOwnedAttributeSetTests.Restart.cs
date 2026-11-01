using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyAuthenticatedOrdinaryCreationTests
{
    [Fact]
    public Task CommittedAllocationSurvivesEchoTransportLossAndRealRuntimeReopen() => RunPhysicalDeathAsync(async f =>
    {
        var before = f.TargetSession;
        var oldBinding = before.NativeGameClient!.OrdinarySpellBinding!;
        Assert.True(f.Owner.Store.IsBoundSpellOwnerCurrent(oldBinding));
        Assert.Equal(10L, before.Character.Spirit); Assert.Equal(12L, before.Character.AttributePoints);
        var expected = f.Owner.Data.Files().ToDictionary(row => row.Key, row => row.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
        var history = HistoryFiles(f.Owner); var ledger = File.ReadAllBytes(f.Owner.Data.State);
        string key = Path.GetRelativePath(f.Owner.Data.Root, f.CharacterPath);
        string text = CharacterSnapshotFileEncoding.Decode(expected[key], out var encoding);
        Assert.Contains("\r\nSpirit=10\r\n", text); Assert.Contains("\r\nAtributes=12\r\n", text);
        text = text.Replace("\r\nSpirit=10\r\n", "\r\nSpirit=20\r\n", StringComparison.Ordinal)
            .Replace("\r\nAtributes=12\r\n", "\r\nAtributes=2\r\n", StringComparison.Ordinal);
        expected[key] = encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();
        int echo = 0;
        f.Owner.BeforeSend = (account, bytes) =>
        {
            if (account != DeathTargetAccount ||
                !LegacyAttributeSetPacket.TryParse(CreationFixture.ReadFrame(bytes), out _)) return;
            Assert.Equal(0, echo++);
            f.Owner.Data.AssertFiles(expected);
            Assert.Equal(20L, f.TargetSession.Character.Spirit);
            Assert.Equal((ushort)20, oldBinding.Player.Spirit); Assert.Equal((ushort)2, oldBinding.Player.Atributes);
            // Real fixture transport closes at the committed echo; its real
            // cancellation makes the ensuing query unable to complete.
            f.Target.DisconnectAsync().GetAwaiter().GetResult();
        };
        try
        {
            try { await f.Owner.SendOnAsync(f.Target, new LegacyAttributeSetPacket(0, 0, 0, 10).ToFrameBytes()); }
            catch (OperationCanceledException) { }
        }
        finally { f.Owner.BeforeSend = null; }
        Assert.Equal(1, echo);
        Assert.True(f.Owner.Runtime.Recovery.IsBlocked); Assert.True(oldBinding.IsRetired);
        Assert.False(f.Owner.Store.TryGet(f.Target, out _));
        f.Owner.Data.AssertFiles(expected); AssertHistoricalFiles(history, HistoryFiles(f.Owner));
        Assert.Equal(ledger, File.ReadAllBytes(f.Owner.Data.State));
        Assert.False(LegacyTradeJournal.HasPending(f.Owner.Data.Users));

        await f.Owner.CloseAsync();
        f.Owner.Data.AssertFiles(expected); AssertHistoricalFiles(history, HistoryFiles(f.Owner));
        f.Owner.OpenLifetimeFrame();
        Assert.False(f.Owner.Runtime.Recovery.IsBlocked);
        f.Owner.Data.AssertFiles(expected);
        f.Owner.Frames(DeathTargetAccount).Clear();
        var reopened = f.Owner.NewConnection(DeathTargetAccount);
        await f.Owner.SendOnAsync(reopened, new LegacyMsgLoginGamePacket(DeathTargetAccount).ToFrameBytes());
        var current = f.Owner.CurrentFor(reopened);
        var binding = current.NativeGameClient!.OrdinarySpellBinding!;
        Assert.NotSame(oldBinding, binding); Assert.True(f.Owner.Store.IsBoundSpellOwnerCurrent(binding));
        Assert.Equal(20L, current.Character.Spirit); Assert.Equal(2L, current.Character.AttributePoints);
        Assert.Equal((ushort)20, binding.Player.Spirit); Assert.Equal((ushort)2, binding.Player.Atributes);
        Assert.Equal(200L, current.Character.MinHitPoints); Assert.Equal(200, binding.Player.HitPoints);
        Assert.Equal(7051L, current.Character.Class); Assert.Equal(447L, current.Character.Vitality);
        Assert.Equal(0L, current.Character.Strength); Assert.Equal(0L, current.Character.Agility);
        Assert.Empty(current.PlayerItems.RuneItems);
        // Class 7051: 447 * 24 + 20 * 3. The fixture's score-zero
        // rune-storage row contributes another 23 even with an empty rune bag.
        const uint expectedMaximum = 447U * 24U + 20U * 3U + 23U;
        Assert.True(LegacyEquipmentHitpointsStatus.TryGetForOwner(current, out var maximum));
        Assert.Equal(expectedMaximum, maximum); Assert.Equal(100U, LegacyPhysicalStatusOverlay.ForOwner(current).MaxMana);
        var status = Assert.Single(f.Owner.Frames(DeathTargetAccount).Select(CreationFixture.ReadFrame)
            .Where(frame => frame.Header.Opcode == LegacyPlayerStatusPacket.Opcode)
            .Select(frame => { Assert.True(LegacyPlayerStatusPacket.TryParse(frame, out var packet)); return packet; }));
        Assert.Equal(expectedMaximum, status.MaxHP); Assert.Equal(100U, status.MaxMana);
        f.Owner.Data.AssertFiles(expected); AssertHistoricalFiles(history, HistoryFiles(f.Owner));
        Assert.Equal(ledger, File.ReadAllBytes(f.Owner.Data.State));
        Assert.False(LegacyTradeJournal.HasPending(f.Owner.Data.Users));
    });
}

