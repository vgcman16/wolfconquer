using System.Buffers.Binary;
using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedStaminaTests
{
    private static ushort CurrentStamina(StaminaFixture f) => ushort.Parse(f.Current.Character.Attributes["Stamina"], NumberStyles.None, CultureInfo.InvariantCulture);
    private static void AssertCoherent(StaminaFixture f, ushort? expected = null)
    {
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Native.OrdinarySpellBinding);
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding)); Assert.Same(f.Catalog, binding.ItemCatalog);
        Assert.True(binding.Items.Image.IsCurrentPlain()); Assert.Equal(CurrentStamina(f), f.Native.Player!.Stamina);
        if (expected is { } value) Assert.Equal(value, CurrentStamina(f));
        Assert.Equal(f.Current.Character.MinHitPoints, (long)f.Native.Player.HitPoints);
        Assert.Equal(f.Current.Character.MinMana, (long)f.Native.Player.Mana);
        Assert.False(f.Native.FullLoading); // Actual managed readiness, never a test-set native loading flag.
        // Equipment persistence preserves physical order. Existing complete
        // file oracles retain those bytes; this checks the loader-defined
        // logical order against the current and native complete item images.
        var logicalSaved = PlayerItemSnapshotLoader.ToLegacyBytes(
            PlayerItemSnapshotLoader.Parse(File.ReadAllBytes(f.ItemsPath)));
        Assert.Equal(logicalSaved, PlayerItemSnapshotLoader.ToLegacyBytes(f.Current.PlayerItems));
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(f.Current.PlayerItems), PlayerItemSnapshotLoader.ToLegacyBytes(binding.Items.Image.FinalSnapshot));
        Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
    }
    private static void ClearOutput(StaminaFixture f) { f.Sent.Clear(); f.PeerSent.Clear(); f.Order.Clear(); }
    private static void AssertFan(StaminaFixture f, bool present)
    {
        Assert.Equal(present, f.Native.Player!.BitVector.ArrayFlags.TryGetValue(194, out var row));
        Assert.Equal(present, (f.Native.Player.BitVector.bits[6] & 4U) != 0);
        Assert.Equal(present, LegacyPlayerDeathState.HasStatusFlag(f.Current.Character.Attributes, 194));
        if (present) { Assert.Equal(2592000, row!.Secounds); Assert.False(row.RemoveOnDead); }
    }
    private static async Task MintFan(StaminaFixture f)
    {
        AssertFan(f, false); await f.EquipFanAsync(); AssertFan(f, true);
        if (f.Options.Sitting && f.Current.Character.Attributes.GetValueOrDefault("Action") != "250") await f.SitAsync();
        if (f.Options.Sitting) Assert.Equal("250", f.Current.Character.Attributes["Action"]);
        AssertCoherent(f); ClearOutput(f);
    }

    private static byte[] StaminaGolden(ushort value)
    {
        // Independent protobuf tags/varints: UID1, one update2, type1=8, value2.
        var update = new List<byte> { 8, 8, 16 }; WriteVarint(update, value);
        var body = new List<byte> { 8 }; WriteVarint(body, Uid); body.Add(18); WriteVarint(body, checked((ulong)update.Count)); body.AddRange(update);
        var frame = new byte[body.Count + 4]; BinaryPrimitives.WriteUInt16LittleEndian(frame, checked((ushort)frame.Length));
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(2), 2176); body.CopyTo(frame, 4); return frame;
    }
    private static void WriteVarint(List<byte> output, ulong value)
    { while (value >= 128) { output.Add((byte)((value & 127) | 128)); value >>= 7; } output.Add((byte)value); }
    private static byte[] FanEffectGolden() => Convert.FromHexString("1800F407D49D24000A010C0054534D5F53584A5F48506866");

    private static async Task TickAndAssert(StaminaFixture f, ushort value, bool effect = false)
    {
        var files = f.Data.Files(); var spells = f.Native.MySpells!.ClientSpells;
        int first = f.Sent.Count, peerFirst = f.PeerSent.Count, orderFirst = f.Order.Count;
        f.BeforeSend = (_, _) => { AssertCoherent(f, value); f.Data.AssertFiles(files); };
        try { await f.TickAsync(); } finally { f.BeforeSend = null; }
        AssertCoherent(f, value); f.Data.AssertFiles(files); Assert.Same(spells, f.Native.MySpells.ClientSpells);
        var actor = f.Sent.Skip(first).ToArray();
        Assert.Equal(effect ? 2 : 1, actor.Length); int index = 0;
        if (effect) Assert.Equal(FanEffectGolden(), actor[index++]); Assert.Equal(StaminaGolden(value), actor[index]);
        var peers = f.PeerSent.Skip(peerFirst).ToArray();
        if (effect && f.Options.Peer) Assert.Equal(FanEffectGolden(), Assert.Single(peers)); else Assert.Empty(peers);
        var order = f.Order.Skip(orderFirst).ToArray();
        if (effect)
        {
            var effects = order.Select((entry, cursor) => (entry, cursor)).Where(item => item.entry.Bytes.SequenceEqual(FanEffectGolden())).ToArray();
            Assert.Equal(f.Options.Peer ? new[] { Uid, Peer } : new[] { Uid }, effects.Select(item => item.entry.Recipient).Order());
            Assert.True(effects.All(item => item.cursor < order.Length - 1));
        }
        Assert.Equal(Uid, order[^1].Recipient); Assert.Equal(StaminaGolden(value), order[^1].Bytes);
        Assert.DoesNotContain(f.Current.PlayerSpells.Spells, row => row.Id == 194);
    }
    private static async Task TickHeld(StaminaFixture f)
    {
        var before = f.Current; ushort stamina = CurrentStamina(f); var files = f.Data.Files();
        int actor = f.Sent.Count, peer = f.PeerSent.Count; await f.TickAsync();
        Assert.Equal(actor, f.Sent.Count); Assert.Equal(peer, f.PeerSent.Count); AssertCoherent(f, stamina); f.Data.AssertFiles(files);
        Assert.Same(before.PlayerItems, f.Current.PlayerItems); Assert.Same(before.PlayerSpells, f.Current.PlayerSpells);
    }
}
