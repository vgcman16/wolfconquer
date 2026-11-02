using System.Buffers.Binary;
using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedActionTests
{
    private const string DefenderExpiry = "LegacyAuraMagicDefenderExpiresAtUtc";
    private const string DefenderDeathPolicy = "LegacyAuraMagicDefenderRemoveOnDead";
    // The actual managed Auras producer stores ten words; the later
    // representation-only input deliberately supplies an equivalent18-word map.
    private const string EarnedDefenderVector = "0/0/0/0/1/0/0/0/0/0/";
    private const string DefenderVector = "0/0/0/0/1/0/0/0/0/0/0/0/0/0/0/0/0/0/";

    [Theory]
    [InlineData(true, 1)] [InlineData(true, 2)] [InlineData(true, 3)]
    [InlineData(false, 1)] [InlineData(false, 2)] [InlineData(false, 3)]
    public Task ActualEarned11200MovementCleanupReadsUniqueVectorAliasesAndRetiresItsMetadata(bool jump, int vectorShape)
    {
        const uint actor = OwnedEquipmentBehaviorFixture.Actor, peerUid = OwnedEquipmentBehaviorFixture.Peer;
        var character = OwnedEquipmentBehaviorFixture.Character(actor, "JumpAura", minHitPoints: 100) with
            { MinMana = 50, Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Stamina"] = "100" } };
        var actorInput = new OwnedEquipmentBehaviorInput(character, PlayerItemSnapshot.Empty,
            new(new[] { new PlayerSpellSnapshotEntry(11200, 1, 0, 0, 0, 0) }));
        var peerInput = new OwnedEquipmentBehaviorInput(
            OwnedEquipmentBehaviorFixture.Character(peerUid, "JumpViewer", x: 830, y: 585, minHitPoints: 100), PlayerItemSnapshot.Empty);
        var catalog = NativeCallerWorldDataFixture.Create(Array.Empty<ItemType>(), Array.Empty<XuanbaoRandomAttribute>(),
            magic: new[] { JumpDefenderRow(0), JumpDefenderRow(1), JumpDefenderRow(2) });
        return OwnedEquipmentBehaviorFixture.RunAsync(actorInput, catalog, async f =>
        {
            var native = f.Connection(actor).NativeGameClient!; var binding = native.OrdinarySpellBinding!;
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
            var nativeBits = native.Player!.BitVector.bits.ToArray();
            var nativeFlags = native.Player.BitVector.ArrayFlags.ToArray();
            var castFiles = JumpDefenderCastFiles(f, f.Data.Files());
            f.ActorFrames.Clear(); f.Frames(peerUid).Clear();
            await f.SendAsync(actor, new MsgAttackPacket(new(actor, actor, 830, 584, MsgAttackType.Magic,
                0, 0, 0, MsgAttackEffect.None, 0, SpellId: 11200)).ToFrameBytes());
            // This is a real admitted cast prerequisite, deliberately not a
            // synthetic flag, direct handler call or borrowed spell owner.
            Assert.True(f.Store.TryGet(f.Connection(actor), out var earned), f.Log.ToString());
            Assert.NotNull(earned);
            Assert.True(earned.Character.Attributes.TryGetValue("StatusFlags", out var raw), f.Log.ToString());
            Assert.Equal(EarnedDefenderVector, raw);
            Assert.True(earned.Character.Attributes.ContainsKey(DefenderExpiry), f.Log.ToString());
            Assert.Equal("true", earned.Character.Attributes[DefenderDeathPolicy]);
            var expiry = earned.Character.Attributes[DefenderExpiry];
            Assert.True(DateTimeOffset.TryParse(expiry, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out _));
            Assert.Contains("handled matching legacy Auras MagicDefender magic", f.Log.ToString());
            Assert.Equal(200, Assert.Single(earned.PlayerSpells.Spells.Where(row => row.Id == 11200)).Experience);
            Assert.Equal(200, native.MySpells!.ClientSpells[11200].Experience);
            Assert.Equal((ushort)1, native.MySpells.ClientSpells[11200].Level);
            f.Data.AssertFiles(castFiles);
            // The existing Auras owner publishes managed flag128 and its
            // duration/death policy. It does not mint a native128 timer lease.
            Assert.Equal(nativeBits, native.Player.BitVector.bits);
            Assert.Equal(nativeFlags, native.Player.BitVector.ArrayFlags.ToArray());

            var fields = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var row in earned.Character.Attributes)
                if (!IsJumpVectorName(row.Key) && !row.Key.Equals(DefenderExpiry, StringComparison.OrdinalIgnoreCase) &&
                    !row.Key.Equals(DefenderDeathPolicy, StringComparison.OrdinalIgnoreCase)) fields.Add(row.Key, row.Value);
            fields.Add("Opaque", "UP"); fields.Add("opaque", "down");
            fields.Add(DefenderExpiry.ToLowerInvariant(), expiry);
            fields.Add(DefenderDeathPolicy.ToLowerInvariant(), "true");
            if (vectorShape is 1 or 3) fields.Add("sTaTuSfLaGs", DefenderVector);
            if (vectorShape is 2 or 3) fields.Add("bItVeCtOr", "0/0/0/0/1/");
            f.Store.Set(f.Connection(actor), earned with { Character = earned.Character with { Attributes = fields } });
            var before = f.Current(); var peer = f.Current(peerUid); var stamp = native.Player.AttackStamp;
            f.Data.AssertFiles(castFiles);
            Assert.Same(binding, native.OrdinarySpellBinding); Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
            Assert.Equal(expiry, before.Character.Attributes[DefenderExpiry.ToLowerInvariant()]);
            Assert.Equal("true", before.Character.Attributes[DefenderDeathPolicy.ToLowerInvariant()]);
            var expectedFiles = JumpDefenderMovementFiles(f, castFiles, jump);
            f.ActorFrames.Clear(); f.Frames(peerUid).Clear();
            var order = new List<uint>();
            void AssertMoved()
            {
                var after = f.Current();
                Assert.NotSame(before, after); Assert.Same(peer, f.Current(peerUid));
                Assert.Same(binding, native.OrdinarySpellBinding); Assert.False(binding.IsRetired);
                Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding)); Assert.True(binding.Items.Image.IsCurrentPlain());
                Assert.Equal(jump ? 832L : 831L, after.Character.X); Assert.Equal(jump ? 585L : 584L, after.Character.Y);
                Assert.Equal(jump ? (ushort)832 : (ushort)831, native.Player.X);
                Assert.Equal(jump ? (ushort)585 : (ushort)584, native.Player.Y);
                Assert.Equal((ushort)830, native.Player.Px); Assert.Equal((ushort)584, native.Player.Py);
                Assert.Equal(jump ? 100U : 0U, (uint)native.Player.Action);
                Assert.Equal(stamp, native.Player.AttackStamp); Assert.False(native.OnAutoAttack); Assert.Null(native.AutoAttack);
                Assert.Equal(before.Character.MinHitPoints, after.Character.MinHitPoints);
                Assert.Equal(before.Character.MinMana, after.Character.MinMana);
                Assert.Equal(after.Character.MinHitPoints, (long)native.Player.HitPoints);
                Assert.Equal(after.Character.MinMana, (long)native.Player.Mana);
                Assert.Equal("UP", after.Character.Attributes["Opaque"]); Assert.Equal("down", after.Character.Attributes["opaque"]);
                Assert.Same(StringComparer.Ordinal, LegacyNativeAttributeMapProof.CaptureRaw(after.Character.Attributes).CapturedComparer);
                var vectors = after.Character.Attributes.Where(row => IsJumpVectorName(row.Key)).ToArray();
                Assert.Equal(vectorShape == 3 ? 2 : 1, vectors.Length);
                foreach (var vector in vectors)
                    Assert.All(vector.Value.Split('/', StringSplitOptions.RemoveEmptyEntries), word => Assert.Equal(0UL, ulong.Parse(word, CultureInfo.InvariantCulture)));
                Assert.DoesNotContain(after.Character.Attributes.Keys, key => key.Equals(DefenderExpiry, StringComparison.OrdinalIgnoreCase) ||
                    key.Equals(DefenderDeathPolicy, StringComparison.OrdinalIgnoreCase));
                Assert.Equal(nativeBits, native.Player.BitVector.bits);
                Assert.Equal(nativeFlags, native.Player.BitVector.ArrayFlags.ToArray());
                Assert.Same(before.PlayerSpells, after.PlayerSpells); Assert.Same(before.PlayerItems, after.PlayerItems);
                Assert.Equal(200, native.MySpells.ClientSpells[11200].Experience);
                Assert.False(f.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
                f.Data.AssertFiles(expectedFiles);
            }
            f.BeforeSend = (recipient, _) => { AssertMoved(); order.Add(recipient); };
            try
            {
                if (jump) await f.SendAsync(actor, new MsgActionPacket { ObjId = 999, Type = MsgActionType.Jump,
                    TargetPositionX = 832, TargetPositionY = 585, Facing = 6, Timestamp = 123456 }.ToFrameBytes());
                else await f.SendAsync(actor, new MsgMovementPacket { Uid = 999, Direction = 6, Running = MsgMovementMode.Walk,
                    Timestamp = 123456, MapId = 999 }.ToFrameBytes());
            }
            finally { f.BeforeSend = null; }
            AssertMoved();
            var expectedFrames = new[] { JumpDefenderCancellationGolden(), JumpDefenderMovementGolden(jump) };
            Assert.Equal(2, f.ActorFrames.Count); Assert.Equal(2, f.Frames(peerUid).Count);
            for (int index = 0; index < 2; index++)
            { Assert.Equal(expectedFrames[index], f.ActorFrames[index]); Assert.Equal(expectedFrames[index], f.Frames(peerUid)[index]); }
            Assert.Equal(new[] { actor, peerUid, actor, peerUid }, order);
        }, peerInput);
    }

    private static MagicType JumpDefenderRow(byte level)
    {
        var fields = Enumerable.Repeat("0", 53).ToArray();
        var id = 1_120_000 + level;
        fields[0] = id.ToString(CultureInfo.InvariantCulture); fields[1] = "11200"; fields[2] = "51";
        fields[3] = "MagicDefender"; fields[8] = level.ToString(CultureInfo.InvariantCulture);
        fields[13] = "90"; fields[15] = "9"; fields[18] = "10000";
        return new(id, 11200, fields[3], fields);
    }

    private static Dictionary<string, byte[]> JumpDefenderCastFiles(OwnedEquipmentBehaviorFixture f, Dictionary<string, byte[]> before)
    {
        var result = before.ToDictionary(row => row.Key, row => row.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
        var key = Path.GetRelativePath(f.Data.Root, Path.Combine(f.Data.Spells, OwnedEquipmentBehaviorFixture.Actor + ".bin"));
        var image = result[key]; int count = BinaryPrimitives.ReadInt32LittleEndian(image);
        Assert.Equal(4 + count * 12, image.Length); int matches = 0;
        for (int index = 0; index < count; index++)
        {
            int at = 4 + index * 12;
            if (BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan(at)) != 11200) continue;
            matches++; Assert.Equal((ushort)1, BinaryPrimitives.ReadUInt16LittleEndian(image.AsSpan(at + 2)));
            Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(image.AsSpan(at + 4)));
            BinaryPrimitives.WriteInt32LittleEndian(image.AsSpan(at + 4), 200);
        }
        Assert.Equal(1, matches); return result;
    }

    private static Dictionary<string, byte[]> JumpDefenderMovementFiles(OwnedEquipmentBehaviorFixture f,
        Dictionary<string, byte[]> before, bool jump)
    {
        var result = before.ToDictionary(row => row.Key, row => row.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
        var key = Path.GetRelativePath(f.Data.Root, f.CharacterPath());
        string text = CharacterSnapshotFileEncoding.Decode(result[key], out var encoding);
        text = ReplaceLine(text, "X", 830, jump ? 832 : 831);
        if (jump) text = ReplaceLine(text, "Y", 584, 585);
        result[key] = encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray(); return result;
    }

    private static byte[] JumpDefenderCancellationGolden()
    {
        var body = new List<byte>(); Field(body, 1, OwnedEquipmentBehaviorFixture.Actor);
        // Repeated update field2: {type49, flag128}; absent value/time/damage.
        body.AddRange(new byte[] { 18, 5, 8, 49, 32, 128, 1 }); return Frame(2176, body);
    }

    private static byte[] JumpDefenderMovementGolden(bool jump)
    {
        var body = new List<byte>();
        if (!jump)
        {
            Field(body, 1, 6); Field(body, 2, OwnedEquipmentBehaviorFixture.Actor); Field(body, 3, 0);
            Field(body, 4, 123456); Field(body, 5, 1015); return Frame(2205, body);
        }
        Field(body, 1, OwnedEquipmentBehaviorFixture.Actor); Field(body, 7, 832); Field(body, 8, 585);
        Field(body, 9, 123456); Field(body, 12, 137); Field(body, 13, 6); Field(body, 14, 832); Field(body, 15, 585);
        Field(body, 17, 1015); return Frame(2046, body);
    }
}
