using System.Buffers.Binary;
using System.Globalization;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData(false, "100", null, 90UL)] [InlineData(true, "100", null, 90UL)]
    [InlineData(false, "+120", "0", 110UL)] [InlineData(true, "+120", "0", 110UL)]
    [InlineData(false, null, null, 90UL)] [InlineData(true, null, null, 90UL)]
    [InlineData(false, null, "1", 140UL)] [InlineData(true, null, "1", 140UL)]
    [InlineData(false, "18446744073709551615", null, 18446744073709551605UL)]
    [InlineData(true, "18446744073709551615", null, 18446744073709551605UL)]
    [InlineData(false, "65535", null, 65525UL)] [InlineData(true, "65535", null, 65525UL)]
    public async Task OwnedSavedStaminaUsesTheActualAdmissionDomainAndExactAcceptedRemainderAndWireOrder(
        bool root, string? raw, string? blessing, ulong remaining)
    {
        if (raw is not null && !ushort.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
        {
            // Preserve the original exact UInt64.Max input and old expected
            // remainder as historical DATA; it cannot be admitted UInt16 funds.
            Assert.Equal("18446744073709551615", raw); Assert.Equal(18446744073709551605UL, remaining);
            await RunRejectedStaminaAdmission(raw, blessing, false, 1000, root); return;
        }
        await RunStaminaAdmissionOwner(raw, blessing, false, 1000, async (f, world, clock) =>
        {
            if (raw is null)
            {
                var owner = f.Current(PlayerUid); var absentPeer = f.Current(TargetPlayerUid); var files = ProcFiles(f);
                int ownFrames = f.Frames(PlayerUid).Count, peerFrames = f.Frames(TargetPlayerUid).Count;
                foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
                {
                    Assert.False(f.Data.Loader.LoadByAccountId(uid)!.Attributes.ContainsKey("Stamina"));
                    Assert.Equal("0", f.Current(uid).Character.Attributes["Stamina"]);
                    Assert.Equal((ushort)0, f.Current(uid).NativePlayer!.Stamina);
                }
                Assert.Null(await InvokeStaminaAdmission(f, world, clock, root));
                StaminaRefusal(f, owner, absentPeer, files, ownFrames, peerFrames);
                foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
                    Assert.Equal((ushort)0, f.Current(uid).NativePlayer!.Stamina);
                return;
            }
            StaminaInput(f, raw);
            var before = f.Current(PlayerUid); var peer = f.Current(TargetPlayerUid); int first = f.Frames(PlayerUid).Count;
            var filesBefore = ProcFiles(f);
            int replies = 0;
            f.BeforeSend = (uid, _) =>
            {
                Assert.Equal(PlayerUid, uid); replies++;
                var current = f.Current(PlayerUid);
                Assert.Equal(remaining.ToString(CultureInfo.InvariantCulture), current.Character.Attributes["Stamina"]);
                Assert.Equal(checked((ushort)remaining), current.NativePlayer!.Stamina);
                Assert.Equal(950L, current.Character.MinMana); Assert.Equal(950U, current.NativePlayer.Mana);
                Assert.Equal(950L, f.Data.Loader.LoadByAccountId(PlayerUid)!.MinMana);
                Assert.True(f.Store.IsBoundSpellOwnerCurrent(current.NativeGameClient!.OrdinarySpellBinding!));
                Assert.Same(peer, f.Current(TargetPlayerUid));
                AssertStaminaCostOtherFiles(f, filesBefore, raw);
            };
            WorldSession? prepared;
            try { prepared = await InvokeStaminaAdmission(f, world, clock, root); }
            finally { f.BeforeSend = null; }
            Assert.NotNull(prepared); Assert.Same(prepared, f.Current(PlayerUid)); Assert.Same(peer, f.Current(TargetPlayerUid));
            Assert.Equal(remaining.ToString(CultureInfo.InvariantCulture), prepared.Character.Attributes["Stamina"]);
            Assert.Equal(checked((ushort)remaining), prepared.NativePlayer!.Stamina);
            Assert.Equal(950L, prepared.Character.MinMana); Assert.Equal(950U, prepared.NativePlayer!.Mana);
            Assert.Equal(950L, f.Data.Loader.LoadByAccountId(PlayerUid)!.MinMana);
            var frames = f.Frames(PlayerUid).Skip(first).ToArray(); Assert.Equal(root ? 2 : 3, frames.Length);
            Assert.Equal(frames.Length, replies);
            Assert.Equal(StaminaAdmissionUpdateGolden(8, remaining), frames[0]);
            Assert.Equal(StaminaAdmissionUpdateGolden(2, 950), frames[1]);
            var id = root ? (ushort)1000 : (ushort)1045;
            Assert.True(prepared.MagicCooldowns!.ContainsKey(id));
            if (!root)
            {
                Assert.True(LegacyMagicRuntime.TryResolveLearnedSpell(prepared.PlayerSpells, world, id, out _, out var spell));
                Assert.Equal(LegacyMagicColdTimePacket.FromStartedCooldown(spell).ToFrameBytes(), frames[2]);
            }
            Assert.Equal(before.Character.Money, prepared.Character.Money); Assert.Equal(before.Character.ConquerPoints, prepared.Character.ConquerPoints);
            Assert.Same(before.PlayerItems, prepared.PlayerItems); Assert.False(f.Recovery.IsBlocked);
            AssertStaminaCostOtherFiles(f, filesBefore, raw); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        });
    }

    [Theory]
    [InlineData(false, 1039L, (byte)10)] [InlineData(true, 1039L, (byte)10)]
    [InlineData(false, 1000L, (byte)0)] [InlineData(true, 1000L, (byte)0)]
    public Task OwnedMalformedSavedStaminaRefusesAdmissionEvenWhenTheRequestedSpellWouldNotUseIt(bool root, long map, byte cost) =>
        RunRejectedStaminaAdmission("not-a-number", "1", true, map, root, cost);

    private static byte[] StaminaAdmissionUpdateGolden(byte type, ulong value)
    {
        // Independent complete2176 protobuf tags/varints, including the real
        // UInt16.Max→65525 boundary. Never call the production update writer.
        var update = new List<byte> { 8, type, 16 }; StaminaAdmissionVarint(update, value);
        var body = new List<byte> { 8 }; StaminaAdmissionVarint(body, PlayerUid);
        body.Add(18); StaminaAdmissionVarint(body, checked((ulong)update.Count)); body.AddRange(update);
        var bytes = new byte[body.Count + 4]; BinaryPrimitives.WriteUInt16LittleEndian(bytes, checked((ushort)bytes.Length));
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 2176); body.CopyTo(bytes, 4); return bytes;
    }
    private static void StaminaAdmissionVarint(List<byte> output, ulong value)
    { while (value >= 128) { output.Add((byte)((value & 127) | 128)); value >>= 7; } output.Add((byte)value); }

    private static void AssertStaminaCostOtherFiles(OwnedEquipmentBehaviorFixture f, Dictionary<string, byte[]> before, string raw)
    {
        var actual = ProcFiles(f); Assert.Equal(before.Keys.Order(), actual.Keys.Order());
        string actor = Path.GetFullPath(f.CharacterPath(PlayerUid));
        foreach (var row in before)
            if (!string.Equals(Path.GetFullPath(row.Key), actor, StringComparison.OrdinalIgnoreCase)) Assert.Equal(row.Value, actual[row.Key]);
        // Only the existing real Character resource writer may change this
        // participant. Its saved Mana950 is checked before every reply; saved
        // stamina text remains the exact declared compatibility input.
        var saved = f.Data.Loader.LoadByAccountId(PlayerUid)!;
        Assert.Equal(950L, saved.MinMana); Assert.Equal(raw, saved.Attributes["Stamina"]);
    }
}
