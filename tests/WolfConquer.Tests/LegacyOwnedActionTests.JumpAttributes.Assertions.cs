using System.Reflection;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedActionTests
{
    private const string ZeroJumpVector = "0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/0/";

    private static bool IsJumpVectorName(string name) => name.Equals("StatusFlags", StringComparison.OrdinalIgnoreCase) ||
        name.Equals("BitVector", StringComparison.OrdinalIgnoreCase);

    private static Dictionary<string, string> PublishJumpAttributeMap(ActionFixture f, int vectorShape)
    {
        var current = f.Current;
        var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in current.Character.Attributes)
            if (!row.Key.Equals("Action", StringComparison.OrdinalIgnoreCase) &&
                !row.Key.Equals("Angle", StringComparison.OrdinalIgnoreCase) && !IsJumpVectorName(row.Key))
                fields.Add(row.Key, row.Value);
        fields.Add("Opaque", "upper-kept"); fields.Add("opaque", "lower-kept");
        fields.Add("aCtIoN", "250"); fields.Add("aNgLe", "3");
        if (vectorShape is 1 or 3) fields.Add("sTaTuSfLaGs", ZeroJumpVector);
        if (vectorShape is 2 or 3) fields.Add("bItVeCtOr", vectorShape == 3 ? "0/" : ZeroJumpVector);
        f.Store.Set(f.Connection, current with { Character = current.Character with { Attributes = fields } });
        Assert.Same(fields, f.Current.Character.Attributes);
        AssertJumpOpaqueFields(f.Current);
        Assert.Equal(250U, (uint)f.Native.Player!.Action);
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(f.Native.OrdinarySpellBinding!));
        Assert.Empty(f.Sent); Assert.Empty(f.PeerSent); Assert.Empty(f.Order);
        return fields;
    }

    private static void AssertJumpOpaqueFields(WorldSession session)
    {
        Assert.Equal("upper-kept", session.Character.Attributes["Opaque"]);
        Assert.Equal("lower-kept", session.Character.Attributes["opaque"]);
        // Inspect actual storage representation only; this DATA witness does
        // not issue an owner or construct expected movement output.
        var raw = LegacyNativeAttributeMapProof.CaptureRaw(session.Character.Attributes);
        Assert.Same(StringComparer.Ordinal, raw.CapturedComparer); Assert.True(raw.IsCurrentPlain());
    }

    private static void AssertJumpAttributeOwner(ActionFixture f, LegacyOrdinarySpellBinding binding,
        WorldSession before, WorldSession peer, bool away, DateTime stamp)
    {
        var after = f.Current; var player = f.Native.Player!;
        Assert.NotSame(before, after); Assert.Same(peer, f.Get(Peer));
        Assert.Same(binding, f.Native.OrdinarySpellBinding); Assert.False(binding.IsRetired);
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding)); Assert.True(binding.Items.Image.IsCurrentPlain());
        Assert.Same(before.PlayerItems, after.PlayerItems); Assert.Same(before.PlayerSpells, after.PlayerSpells);
        Assert.Same(before.PlayerProficiencies, after.PlayerProficiencies); Assert.Same(before.CombatGear, after.CombatGear);
        AssertJumpOpaqueFields(after);
        Assert.Equal("100", Assert.Single(after.Character.Attributes.Where(row => row.Key.Equals("Action", StringComparison.OrdinalIgnoreCase))).Value);
        Assert.Equal("6", Assert.Single(after.Character.Attributes.Where(row => row.Key.Equals("Angle", StringComparison.OrdinalIgnoreCase))).Value);
        foreach (var row in before.Character.Attributes)
            if (!row.Key.Equals("Action", StringComparison.OrdinalIgnoreCase) && !row.Key.Equals("Angle", StringComparison.OrdinalIgnoreCase) &&
                !row.Key.Equals("X", StringComparison.OrdinalIgnoreCase) && !row.Key.Equals("Y", StringComparison.OrdinalIgnoreCase) &&
                !(away && row.Key.Equals("Away", StringComparison.OrdinalIgnoreCase)))
                Assert.Equal(row.Value, after.Character.Attributes[row.Key]);
        if (away) Assert.Equal("0", after.Character.Attributes["Away"]);
        Assert.Equal((432L, 381L), (after.Character.X, after.Character.Y));
        Assert.Equal((ushort)432, player.X); Assert.Equal((ushort)381, player.Y);
        Assert.Equal((ushort)430, player.Px); Assert.Equal((ushort)380, player.Py);
        Assert.Equal(100U, (uint)player.Action); Assert.Equal(stamp, player.AttackStamp);
        Assert.Equal(before.Character.MinHitPoints, (long)player.HitPoints);
        Assert.Equal(before.Character.MinMana, (long)player.Mana); Assert.Equal((ushort)0, player.Stamina);
        Assert.False(f.Native.OnAutoAttack); Assert.Null(f.Native.AutoAttack); Assert.Null(after.MagicCooldowns);
        Assert.Empty(player.BitVector.ArrayFlags); Assert.All(player.BitVector.bits, word => Assert.Equal(0U, word));
        Assert.False(f.Runtime.Recovery.IsBlocked);
        Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        AssertCoherent(f, uid: Peer);
    }

    private static object?[] JumpAttributeNativeValues(ActionFixture f)
    {
        var player = f.Native.Player!;
        return [player, player.X, player.Y, player.Px, player.Py, player.Map, player.Action,
            player.HitPoints, player.Mana, player.Stamina, player.AttackStamp, player.SpellAttackStamp,
            f.Native.OnAutoAttack, f.Native.AutoAttack, f.Native.Vigor, f.Native.MySpells, f.Native.MyProfs,
            f.Native.Inventory, f.Native.Equipment, f.Native.Rune];
    }

    private sealed record JumpAttributeSlot(object Slot, object?[] Values);
    private static JumpAttributeSlot CaptureJumpAttributeSlot(ActionFixture f)
    {
        // Read the genuine admission-created slot only. Nothing is registered,
        // replaced or fed back to a production owner through this observation.
        var table = typeof(WorldServerFrameHandler).GetField("basicPhysicalSlots", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Handler)!;
        object?[] arguments = [f.Connection, null];
        Assert.True(Assert.IsType<bool>(table.GetType().GetMethod("TryGetValue")!.Invoke(table, arguments)));
        Assert.NotNull(arguments[1]); var slot = arguments[1]!;
        var fields = slot.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .OrderBy(field => field.Name, StringComparer.Ordinal).Select(field => field.GetValue(slot)).ToArray();
        return new(slot, fields);
    }

    private static void AssertJumpAttributeRefusal(ActionFixture f, WorldSession before, WorldSession peer,
        LegacyOrdinarySpellBinding binding, Dictionary<string, byte[]> files, object?[] native, JumpAttributeSlot slot)
    {
        Assert.Same(before, f.Current); Assert.Same(peer, f.Get(Peer));
        Assert.Same(binding, f.Native.OrdinarySpellBinding); Assert.False(binding.IsRetired);
        Assert.Equal(native, JumpAttributeNativeValues(f));
        var afterSlot = CaptureJumpAttributeSlot(f);
        Assert.Same(slot.Slot, afterSlot.Slot); Assert.Equal(slot.Values, afterSlot.Values);
        Assert.Empty(f.Native.Player!.BitVector.ArrayFlags);
        Assert.All(f.Native.Player.BitVector.bits, word => Assert.Equal(0U, word));
        Assert.Empty(f.Sent); Assert.Empty(f.PeerSent); Assert.Empty(f.Order);
        f.Data.AssertFiles(files); Assert.False(f.Runtime.Recovery.IsBlocked);
        Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
    }
}
