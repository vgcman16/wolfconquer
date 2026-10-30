using System.Globalization;
using System.Reflection;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyOwnedAllRoundRuneTests
{
    private const uint RuneUid = 1_110_001, RightUid = RuneUid + 1, ShieldUid = RuneUid + 2;
    private const uint RightType = 410_003, ShieldType = 900_003;

    private static Task Run(Func<OwnedEquipmentBehaviorFixture, Task> body, int level = 1,
        ushort position = 102, long job = 2000, long reborn = 2, long characterLevel = 140,
        long hitpoints = 1, string shield = "normal", bool alternateRune = false, bool peer = false, bool duplicateBlue = false,
        ushort stack = 1)
    {
        var rune = new PlayerItemSnapshotItem { Uid = RuneUid, ItemId = checked((uint)(4_020_200 + level)),
            Position = position, Durability = 10, MaximumDurability = 10, StackSize = stack, Color = 5,
            Attribute1 = 112001, Attribute5 = 998002, RuneExperience = 1234 };
        var right = new PlayerItemSnapshotItem { Uid = RightUid, ItemId = RightType,
            Position = shield == "alternate" ? (ushort)54 : (ushort)4,
            Durability = 100, MaximumDurability = 100, StackSize = 1, Color = 5 };
        var left = new PlayerItemSnapshotItem { Uid = ShieldUid, ItemId = ShieldType,
            Position = shield == "alternate" ? (ushort)55 : (ushort)5,
            Durability = shield == "broken" ? (ushort)0 : (ushort)100,
            MaximumDurability = 100, StackSize = 1, Color = 5 };
        var gear = shield == "missing" ? new[] { right } : new[] { right, left };
        var runes = alternateRune ? new[] { rune, new PlayerItemSnapshotItem { Uid = RuneUid + 3, ItemId = 4_020_227,
            Position = 122, Durability = 10, MaximumDurability = 10, StackSize = 1, Color = 5 } } : new[] { rune };
        if (duplicateBlue) runes = runes.Append(new PlayerItemSnapshotItem { Uid = RuneUid + 4, ItemId = 4_020_517,
            Position = 102, Durability = 10, MaximumDurability = 10, StackSize = 1, Color = 5 }).ToArray();
        var items = new PlayerItemSnapshot([], [], [], gear, runes);
        var metadata = new[] { RuneMetadata(rune.ItemId), RuneMetadata(4_020_227),
            OwnedEquipmentBehaviorFixture.Item(RightType), OwnedEquipmentBehaviorFixture.Item(ShieldType) }
            .DistinctBy(row => row.Id).ToArray();
        if (duplicateBlue) metadata = metadata.Append(OwnedEquipmentBehaviorFixture.Item(4_020_517, "OtherOwnedBlue",
            new Dictionary<int, string> { [ItemType.RequiredClassFieldIndex] = "6000", [ItemType.RequiredLevelFieldIndex] = "0",
                [ItemType.RuneSpellIdFieldIndex] = "1416016", [ItemType.StackSizeFieldIndex] = "0" })).ToArray();
        var actor = new OwnedEquipmentBehaviorInput(OwnedEquipmentBehaviorFixture.Character(classCode: job,
            level: characterLevel, reborn: reborn, strength: 100, minHitPoints: hitpoints), items,
            new PlayerSpellSnapshot([new(6001, 4, 1111, 2, 3, 2)]));
        var peers = peer ? new[] { new OwnedEquipmentBehaviorInput(OwnedEquipmentBehaviorFixture.Character(
            uid: OwnedEquipmentBehaviorFixture.Peer, name: "NeutralPeer", classCode: 2000, strength: 100, minHitPoints: 1),
            PlayerItemSnapshot.Empty) } : Array.Empty<OwnedEquipmentBehaviorInput>();
        return OwnedEquipmentBehaviorFixture.RunAsync(actor, OwnedEquipmentBehaviorFixture.Catalog(metadata), body, peers);
    }

    private static ItemType RuneMetadata(uint id) => OwnedEquipmentBehaviorFixture.Item(id, "IdentityRatherThanName",
        new Dictionary<int, string> { [ItemType.RequiredClassFieldIndex] = "2000", [ItemType.RequiredLevelFieldIndex] = "0",
            [ItemType.RuneSpellIdFieldIndex] = id.ToString(CultureInfo.InvariantCulture), [ItemType.StackSizeFieldIndex] = "0" });

    private static Dictionary<string, byte[]> Saved(OwnedEquipmentBehaviorFixture f) => new(StringComparer.OrdinalIgnoreCase)
    {
        [f.CharacterPath()] = File.ReadAllBytes(f.CharacterPath()), [f.ItemPath()] = File.ReadAllBytes(f.ItemPath()),
        [Path.Combine(f.Data.Spells, OwnedEquipmentBehaviorFixture.Actor.ToString(CultureInfo.InvariantCulture) + ".bin")] =
            File.ReadAllBytes(Path.Combine(f.Data.Spells, OwnedEquipmentBehaviorFixture.Actor.ToString(CultureInfo.InvariantCulture) + ".bin")),
        [f.Data.State] = File.ReadAllBytes(f.Data.State),
    };

    private static void AssertSaved(OwnedEquipmentBehaviorFixture f, Dictionary<string, byte[]> saved)
    {
        foreach (var pair in saved) Assert.Equal(pair.Value, File.ReadAllBytes(pair.Key));
        AssertOwner(f);
    }

    private static void AssertOwner(OwnedEquipmentBehaviorFixture f)
    {
        Assert.False(File.Exists(Path.Combine(f.Data.Users, ".managed-trade-journal", "pending.json")));
        var current = f.Current(); var binding = Assert.IsType<LegacyOrdinarySpellBinding>(current.NativeGameClient!.OrdinarySpellBinding);
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
        Assert.Equal(ConquerProtocolProfile.VisibleDx8D13, binding.ItemProfile);
        Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(current.PlayerItems), PlayerItemSnapshotLoader.ToLegacyBytes(binding.ItemSnapshot));
        Assert.False(current.PlayerSpells.ContainsSpell(40202));
        Assert.DoesNotContain((ushort)40202, current.NativeGameClient.MySpells!.ClientSpells.Keys);
        Assert.False(current.Character.Attributes.ContainsKey("ManagedRuneSpellOrigins"));
    }

    private static uint[] CombatFields(OwnedEquipmentBehaviorFixture f)
    {
        var method = typeof(WorldPacketHandlers).GetMethod("CalculateLegacyRuntimeStatusFields", BindingFlags.Static | BindingFlags.NonPublic)!;
        try { return Assert.IsType<uint[]>(method.Invoke(null, [f.Current(), f.Current().NativeGameClient!.OrdinarySpellBinding!.ItemCatalog])); }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
    }

    private static async Task<LegacyPlayerStatusPacket> Query(OwnedEquipmentBehaviorFixture f)
    {
        f.ActorFrames.Clear();
        await f.SendAsync(OwnedEquipmentBehaviorFixture.Actor, new LegacyPlayerStatusQueryPacket(OwnedEquipmentBehaviorFixture.Actor).ToFrameBytes());
        var bytes = Assert.Single(f.ActorFrames);
        Assert.True(LegacyPlayerStatusPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var packet));
        Assert.Equal((ushort)164, System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(bytes));
        Assert.Equal((ushort)WorldPacketOpcodes.LegacyPlayerStatus, System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(2)));
        return packet;
    }

    private static uint DirectDamage(OwnedEquipmentBehaviorFixture f, uint attackerUid, uint targetUid)
    {
        var method = typeof(WorldPacketHandlers).GetMethod("CalculateLegacyDirectPlayerPlayerPhysicalDamageResult", BindingFlags.Static | BindingFlags.NonPublic)!;
        try
        {
            var result = method.Invoke(null, [f.Current(attackerUid), f.Current(targetUid),
                f.Current().NativeGameClient!.OrdinarySpellBinding!.ItemCatalog])!;
            return Assert.IsType<uint>(result.GetType().GetProperty("Damage")!.GetValue(result));
        }
        catch (TargetInvocationException error) when (error.InnerException is not null)
        { ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
    }

    private static void AssertDelta(OwnedEquipmentBehaviorFixture f, uint attack, uint defence, uint magicDefence)
    {
        var session = f.Current(); var catalog = session.NativeGameClient!.OrdinarySpellBinding!.ItemCatalog;
        var actual = CombatFields(f);
        var before = LegacyPlayerStatusCalculator.CalculateFields(session.Character, session.PlayerItems, catalog,
            actual[LegacyPlayerStatusPacket.MaxHPIndex], actual[LegacyPlayerStatusPacket.PrestigeLevelIndex], session.CombatGear,
            session.AlternateEquipmentActive, session.PlayerSpells);
        // The actual runtime retains its independent native query cache. It
        // overrides saved MaxMana even when All-round is absent; do not mistake
        // that existing owner value for a change made by this passive.
        var overlay = LegacyPhysicalStatusOverlay.ForOwner(session);
        Assert.True(overlay.MaxMana.HasValue);
        before[LegacyPlayerStatusPacket.MaxManaIndex] = overlay.MaxMana.Value;
        before[LegacyPlayerStatusPacket.MinAttackIndex] += attack;
        before[LegacyPlayerStatusPacket.MaxAttackIndex] += attack;
        before[LegacyPlayerStatusPacket.DefenceIndex] += defence;
        before[LegacyPlayerStatusPacket.MagicDefenceIndex] += magicDefence;
        Assert.Equal(before, actual);
    }
}
