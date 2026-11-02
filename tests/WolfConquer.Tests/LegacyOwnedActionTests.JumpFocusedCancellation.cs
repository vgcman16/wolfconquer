using System.Buffers.Binary;
using System.Globalization;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedActionTests
{
    [Theory]
    [InlineData(true, false)] [InlineData(true, true)]
    [InlineData(false, false)] [InlineData(false, true)]
    public Task Real9000ArmMovementCancelRetainsOriginalFacingAndCoordinates(bool jump, bool upperIdSpelling) =>
        RunJumpFocus(async f =>
        {
            var saved = await EarnJumpFocusAsync(f);
            var fields = PublishJumpFocusAliases(f, upperIdSpelling);
            const uint actor = OwnedEquipmentBehaviorFixture.Actor, peerUid = OwnedEquipmentBehaviorFixture.Peer;
            var before = f.Current(); var peer = f.Current(peerUid); var native = before.NativeGameClient!;
            var binding = native.OrdinarySpellBinding!; var stamp = native.Player!.AttackStamp;
            var oldFields = fields.ToArray(); var nativeBits = native.Player.BitVector.bits.ToArray();
            var expected = JumpDefenderMovementFiles(f, saved, jump);
            var order = new List<uint>(); f.ActorFrames.Clear(); f.Frames(peerUid).Clear();
            void AssertCommitted()
            {
                var after = f.Current();
                Assert.NotSame(before, after); Assert.Same(peer, f.Current(peerUid));
                Assert.Same(binding, native.OrdinarySpellBinding); Assert.False(binding.IsRetired);
                Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding)); Assert.True(binding.Items.Image.IsCurrentPlain());
                Assert.Equal("false", after.Character.Attributes["inuseintensify"]);
                Assert.Single(after.Character.Attributes.Keys, key => key.Equals("InUseIntensify", StringComparison.OrdinalIgnoreCase));
                Assert.Equal("UP", after.Character.Attributes["Opaque"]); Assert.Equal("low", after.Character.Attributes["opaque"]);
                Assert.Same(StringComparer.Ordinal, LegacyNativeAttributeMapProof.CaptureRaw(after.Character.Attributes).CapturedComparer);
                Assert.Equal(jump ? 832L : 831L, after.Character.X); Assert.Equal(jump ? 585L : 584L, after.Character.Y);
                Assert.Equal(jump ? (ushort)832 : (ushort)831, native.Player.X);
                Assert.Equal(jump ? (ushort)585 : (ushort)584, native.Player.Y);
                Assert.Equal((ushort)830, native.Player.Px); Assert.Equal((ushort)584, native.Player.Py);
                Assert.Equal(jump ? 100U : 0U, (uint)native.Player.Action); Assert.Equal(stamp, native.Player.AttackStamp);
                Assert.Equal(before.Character.MinHitPoints, after.Character.MinHitPoints);
                Assert.Equal(before.Character.MinMana, after.Character.MinMana);
                Assert.Equal(after.Character.MinHitPoints, (long)native.Player.HitPoints);
                Assert.Equal(after.Character.MinMana, (long)native.Player.Mana);
                Assert.False(native.OnAutoAttack); Assert.Null(native.AutoAttack);
                Assert.Same(before.PlayerSpells, after.PlayerSpells); Assert.Same(before.PlayerItems, after.PlayerItems);
                Assert.Equal(45, native.MySpells!.ClientSpells[9000].Experience);
                Assert.Equal(nativeBits, native.Player.BitVector.bits); Assert.Empty(native.Player.BitVector.ArrayFlags);
                Assert.Equal(oldFields, fields.ToArray()); // The original earned arm is not edited in place.
                Assert.False(f.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
                f.Data.AssertFiles(expected);
            }
            f.BeforeSend = (recipient, _) => { AssertCommitted(); order.Add(recipient); };
            uint earliest = unchecked((uint)Environment.TickCount);
            try { await SendJumpFocusMovement(f, jump); }
            finally { f.BeforeSend = null; }
            uint latest = unchecked((uint)Environment.TickCount);
            AssertCommitted();
            Assert.Equal(2, f.ActorFrames.Count); Assert.Single(f.Frames(peerUid));
            Assert.True(MsgActionPacket.TryParse(Read(f.ActorFrames[0]), out var cancel));
            Assert.True(unchecked(cancel.Timestamp - earliest) <= unchecked(latest - earliest),
                "Cancellation timestamp must come from the actual send-time TickCount interval, including wrap.");
            Assert.Equal(JumpFocusCancelGolden(cancel.Timestamp), f.ActorFrames[0]);
            Assert.Equal(JumpDefenderMovementGolden(jump), f.ActorFrames[1]);
            Assert.Equal(JumpDefenderMovementGolden(jump), f.Frames(peerUid)[0]);
            Assert.Equal(new[] { actor, actor, peerUid }, order);
        });

    [Theory]
    [InlineData(true, "focus-id")] [InlineData(false, "focus-id")]
    [InlineData(true, "angle")] [InlineData(false, "angle")]
    [InlineData(true, "armed")] [InlineData(false, "armed")]
    public Task Real9000ArmUsedAliasAmbiguityRefusesBeforeCancelOrMovement(bool jump, string ambiguity) =>
        RunJumpFocus(async f =>
        {
            var files = await EarnJumpFocusAsync(f);
            var fields = PublishJumpFocusAliases(f, upperIdSpelling: false);
            const uint actor = OwnedEquipmentBehaviorFixture.Actor, peerUid = OwnedEquipmentBehaviorFixture.Peer;
            var before = f.Current(); var peer = f.Current(peerUid); var native = before.NativeGameClient!;
            var binding = native.OrdinarySpellBinding!; var nativeBefore = CaptureJumpFocusNative(before);
            var slotBefore = CaptureJumpFocusSlot(f);
            var (key, value) = ambiguity switch
            {
                "focus-id" => ("FocusClientSpellID", "11670"),
                "angle" => ("ANGLE", "4"),
                "armed" => ("InUseIntensify", "false"),
                _ => throw new InvalidOperationException("Unknown declared negative input."),
            };
            // Reversible negative DATA only, after valid actual publication.
            // The arm and its45 XP were earned by the real9000 Frame request.
            fields.Add(key, value); var invalid = fields.ToArray();
            f.ActorFrames.Clear(); f.Frames(peerUid).Clear();
            try
            {
                await SendJumpFocusMovement(f, jump);
                Assert.Same(before, f.Current()); Assert.Same(peer, f.Current(peerUid));
                Assert.Same(binding, native.OrdinarySpellBinding); Assert.False(binding.IsRetired);
                Assert.Equal(nativeBefore, CaptureJumpFocusNative(f.Current()));
                var slotAfter = CaptureJumpFocusSlot(f);
                Assert.Same(slotBefore.Slot, slotAfter.Slot); Assert.Equal(slotBefore.Values, slotAfter.Values);
                Assert.Equal(invalid, fields.ToArray()); Assert.Equal("true", fields["inuseintensify"]);
                Assert.Equal("9000", fields["focusclientspellid"]); Assert.Equal("3", fields["aNgLe"]);
                Assert.Equal(45, native.MySpells!.ClientSpells[9000].Experience);
                Assert.Empty(native.Player!.BitVector.ArrayFlags);
                Assert.All(native.Player.BitVector.bits, word => Assert.Equal(0U, word));
                Assert.Empty(f.ActorFrames); Assert.Empty(f.Frames(peerUid)); f.Data.AssertFiles(files);
                Assert.False(f.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
            }
            finally { fields.Remove(key); }
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
        });

    private static Task RunJumpFocus(Func<OwnedEquipmentBehaviorFixture, Task> body)
    {
        var actor = OwnedEquipmentBehaviorFixture.Character(name: "JumpFocus", minHitPoints: 100) with
            { MinMana = 50, Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Stamina"] = "100" } };
        var input = new OwnedEquipmentBehaviorInput(actor, PlayerItemSnapshot.Empty,
            new(new[] { new PlayerSpellSnapshotEntry(9000, 1, 0, 0, 0, 0) }));
        var peer = new OwnedEquipmentBehaviorInput(OwnedEquipmentBehaviorFixture.Character(
            OwnedEquipmentBehaviorFixture.Peer, "FocusViewer", x: 830, y: 585, minHitPoints: 100), PlayerItemSnapshot.Empty);
        var catalog = NativeCallerWorldDataFixture.Create(Array.Empty<ItemType>(), Array.Empty<XuanbaoRandomAttribute>(),
            magic: new[] { JumpFocusRow(0), JumpFocusRow(1), JumpFocusRow(2) });
        // Disabled real scheduling: this tests the earned armed branch, not a
        // synthetic Focused flag or a private worker invocation/promotion.
        return OwnedEquipmentBehaviorFixture.RunAsync(input, catalog, body, peer);
    }

    private static MagicType JumpFocusRow(byte level)
    {
        var fields = Enumerable.Repeat("0", 53).ToArray(); int id = 900_000 + level;
        fields[0] = id.ToString(CultureInfo.InvariantCulture); fields[1] = "9000"; fields[2] = "6";
        fields[3] = "Intensify"; fields[8] = level.ToString(CultureInfo.InvariantCulture);
        fields[10] = "77"; fields[13] = "45"; fields[15] = "9"; fields[18] = "1000";
        return new(id, 9000, fields[3], fields);
    }

    private static async Task<Dictionary<string, byte[]>> EarnJumpFocusAsync(OwnedEquipmentBehaviorFixture f)
    {
        const uint actor = OwnedEquipmentBehaviorFixture.Actor;
        var expected = f.Data.Files().ToDictionary(row => row.Key, row => row.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
        string path = Path.GetRelativePath(f.Data.Root, Path.Combine(f.Data.Spells, actor + ".bin"));
        var bytes = expected[path]; int count = BinaryPrimitives.ReadInt32LittleEndian(bytes); int found = 0;
        Assert.Equal(4 + count * 12, bytes.Length);
        for (int index = 0; index < count; index++)
        {
            int at = 4 + index * 12;
            if (BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(at)) != 9000) continue;
            found++; Assert.Equal((ushort)1, BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(at + 2)));
            Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(bytes.AsSpan(at + 4)));
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(at + 4), 45);
        }
        Assert.Equal(1, found);
        await f.SendAsync(actor, new MsgAttackPacket(new(actor, actor, 830, 584, MsgAttackType.Magic,
            0, 0, 0, MsgAttackEffect.None, 0, SpellId: 9000)).ToFrameBytes());
        Assert.True(f.Store.TryGet(f.Connection(actor), out var armed), f.Log.ToString()); Assert.NotNull(armed);
        Assert.True(armed.Character.Attributes.TryGetValue("InUseIntensify", out var use) && use == "true", f.Log.ToString());
        Assert.Equal("9000", armed.Character.Attributes["FocusClientSpellId"]);
        Assert.Equal("1", armed.Character.Attributes["FocusClientSpellLevel"]);
        Assert.Equal("77", armed.Character.Attributes["IntensifyDamage"]);
        Assert.True(DateTimeOffset.TryParse(armed.Character.Attributes["IntensifyStampUtc"], CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind, out _));
        Assert.Contains("handled legacy Intensify AttachStatus magic", f.Log.ToString());
        Assert.Equal(45, Assert.Single(armed.PlayerSpells.Spells.Where(row => row.Id == 9000)).Experience);
        Assert.Equal(45, armed.NativeGameClient!.MySpells!.ClientSpells[9000].Experience);
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(armed.NativeGameClient.OrdinarySpellBinding!));
        Assert.Empty(armed.NativeGameClient.Player!.BitVector.ArrayFlags);
        f.Data.AssertFiles(expected); return expected;
    }

    private static Dictionary<string, string> PublishJumpFocusAliases(OwnedEquipmentBehaviorFixture f, bool upperIdSpelling)
    {
        var before = f.Current(); var fields = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var row in before.Character.Attributes)
            if (!row.Key.Equals("Angle", StringComparison.OrdinalIgnoreCase) &&
                !row.Key.Equals("FocusClientSpellId", StringComparison.OrdinalIgnoreCase) &&
                !row.Key.Equals("InUseIntensify", StringComparison.OrdinalIgnoreCase)) fields.Add(row.Key, row.Value);
        fields.Add("Opaque", "UP"); fields.Add("opaque", "low"); fields.Add("aNgLe", "3");
        fields.Add(upperIdSpelling ? "FocusClientSpellID" : "focusclientspellid", before.Character.Attributes["FocusClientSpellId"]);
        fields.Add("inuseintensify", before.Character.Attributes["InUseIntensify"]);
        f.Store.Set(f.Connection(OwnedEquipmentBehaviorFixture.Actor), before with { Character = before.Character with { Attributes = fields } });
        Assert.Same(fields, f.Current().Character.Attributes);
        Assert.Equal("true", fields["inuseintensify"]);
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(f.Current().NativeGameClient!.OrdinarySpellBinding!));
        return fields;
    }

    private static Task SendJumpFocusMovement(OwnedEquipmentBehaviorFixture f, bool jump) => jump
        ? f.SendAsync(OwnedEquipmentBehaviorFixture.Actor, new MsgActionPacket { ObjId = 999, Type = MsgActionType.Jump,
            TargetPositionX = 832, TargetPositionY = 585, Facing = 6, Timestamp = 123456 }.ToFrameBytes())
        : f.SendAsync(OwnedEquipmentBehaviorFixture.Actor, new MsgMovementPacket { Uid = 999, Direction = 6,
            Running = MsgMovementMode.Walk, MapId = 999, Timestamp = 123456 }.ToFrameBytes());

    private static byte[] JumpFocusCancelGolden(uint observedTimestamp)
    {
        var body = new List<byte>(); Field(body, 1, OwnedEquipmentBehaviorFixture.Actor); Field(body, 2, 9000);
        Field(body, 9, observedTimestamp); Field(body, 12, 103); Field(body, 13, 3);
        Field(body, 14, 830); Field(body, 15, 584); return Frame(2046, body);
    }

    private static object?[] CaptureJumpFocusNative(WorldSession session)
    {
        var native = session.NativeGameClient!; var player = native.Player!;
        return [player, player.X, player.Y, player.Px, player.Py, player.Angle, player.Action, player.HitPoints, player.Mana,
            player.Stamina, player.AttackStamp, player.SpellAttackStamp, native.OnAutoAttack, native.AutoAttack,
            native.MySpells, native.MyProfs, native.Inventory, native.Equipment, native.Rune];
    }

    private static JumpAttributeSlot CaptureJumpFocusSlot(OwnedEquipmentBehaviorFixture f)
    {
        var table = typeof(WorldServerFrameHandler).GetField("basicPhysicalSlots", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Handler)!;
        object?[] arguments = [f.Connection(OwnedEquipmentBehaviorFixture.Actor), null];
        Assert.True(Assert.IsType<bool>(table.GetType().GetMethod("TryGetValue")!.Invoke(table, arguments)));
        Assert.NotNull(arguments[1]); var slot = arguments[1]!;
        return new(slot, slot.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .OrderBy(field => field.Name, StringComparer.Ordinal).Select(field => field.GetValue(slot)).ToArray());
    }
}
