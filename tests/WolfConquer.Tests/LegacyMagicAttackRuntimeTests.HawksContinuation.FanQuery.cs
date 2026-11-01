using System.Buffers.Binary;
using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    internal static Task RunOwnedHawksNestedFanQueryAsync()
    {
        const uint right = 1_038_001, left = 1_038_002;
        var original = CreateSession(classCode: 16051, reborn: 2, spellIds: [HawksAmbitionAttachStatusSpellId]);
        var character = original.Character with { Agility = 36 };
        var items = new PlayerItemSnapshot([], [], [], new PlayerItemSnapshotItem[]
        {
            new() { Uid = right, ItemId = 626003, Position = 4, Durability = 10, MaximumDurability = 10, Color = 5 },
            new() { Uid = left, ItemId = 626003, Position = 5, Durability = 10, MaximumDurability = 10, Color = 5 },
        }, []);
        var viewer = CreateSession(uid: TargetPlayerUid, name: "FanPhaseViewer", x: character.X + 1,
            y: character.Y, spellIds: []);
        var clock = new HawksOwnedClock();
        return OwnedEquipmentBehaviorFixture.RunWithOptionsAsync(new(character, items, original.PlayerSpells),
            HawksContinuationFanCatalog(), new LegacyRuntimeUpdateLoopOptions(false, TimeSpan.FromSeconds(1), clock), async f =>
            {
                var initial = f.Current(PlayerUid); var peer = f.Current(TargetPlayerUid);
                var native = initial.NativeGameClient!; var binding = native.OrdinarySpellBinding!;
                Assert.True(native.Player!.BitVector.ArrayFlags.TryGetValue(194, out var firstRow));
                Assert.NotNull(firstRow); Assert.Equal(2_592_000, firstRow.Secounds); Assert.False(firstRow.RemoveOnDead);
                Assert.True(HasLegacyStatusFlag(initial.Character.Attributes, 194));
                var setupFiles = f.Data.Files();
                // Shorten only the registration genuinely issued by startup,
                // then let the actual Frame expiry owner remove it. This is
                // not a claim that30days elapsed or a raw flag assignment.
                Assert.True(native.Player.BitVector.UpdateFlag(194, 0, true, 0));
                await f.Handler.SendDueLegacyWindWalkerFanExpirationsAsync(CancellationToken.None);
                f.Data.AssertFiles(setupFiles);
                var before = f.Current(PlayerUid);
                Assert.False(native.Player!.BitVector.ArrayFlags.ContainsKey(194));
                Assert.False(HasLegacyStatusFlag(before.Character.Attributes, 194));
                var files = f.Data.Files(); string characterKey = Path.GetRelativePath(f.Data.Root, f.CharacterPath(PlayerUid));
                string spellsKey = Path.GetRelativePath(f.Data.Root, Path.Combine(f.Data.Spells,
                    PlayerUid.ToString(CultureInfo.InvariantCulture) + ".bin"));
                var paid = files.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
                paid[characterKey] = ReplaceOwnedHawksMana(paid[characterKey]);
                var progressed = paid.ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
                Assert.Equal(16, progressed[spellsKey].Length);
                BinaryPrimitives.WriteUInt16LittleEndian(progressed[spellsKey].AsSpan(6), 1);
                f.ActorFrames.Clear(); f.Frames(TargetPlayerUid).Clear();
                int stage = 0; bool nested = false, sawProgress = false; var outerVectors = new List<(uint Recipient, byte[] Bytes)>();
                var vector = HawksContinuationFanVectorGolden();
                f.BeforeSend = (uid, bytes) =>
                {
                    if (LegacySpellPacket.TryParse(ReadFrame(bytes), out _)) sawProgress = true;
                    var expected = sawProgress ? progressed : paid;
                    f.Data.AssertFiles(expected);
                    AssertOwnedHawksCurrent(f, before, clock, sawProgress);
                    Assert.Same(peer, f.Current(TargetPlayerUid));
                    if (stage == 2 && LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) &&
                        update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag && entry.Values is { } flags &&
                            IsLegacyStatusFlagSet(flags, HawksAmbitionStatusFlag)))
                    {
                        Assert.Equal(vector, bytes); outerVectors.Add((uid, bytes));
                        Assert.True(HasLegacyStatusFlag(f.Current(PlayerUid).Character.Attributes, 194));
                        Assert.True(native.Player.BitVector.ArrayFlags.ContainsKey(194));
                    }
                    if (nested || uid != PlayerUid || !MsgAttackPacket.TryParse(ReadFrame(bytes), out var attack) ||
                        attack.Query.SpellId != HawksAmbitionAttachStatusSpellId) return;
                    nested = true;
                    // Ordinary query is Shared→Shared. Equip would need an
                    // Exclusive upgrade while the outer Magic holds Shared,
                    // so no nested Equip or barrier bypass is used here.
                    stage = 1;
                    f.SendAsync(PlayerUid, LegacyRuneStoragePacket.Startup().ToFrameBytes()).GetAwaiter().GetResult();
                    stage = 2;
                    Assert.True(native.Player.BitVector.ArrayFlags.TryGetValue(194, out var row));
                    Assert.NotNull(row); Assert.Equal(2_592_000, row.Secounds); Assert.False(row.RemoveOnDead);
                    Assert.NotSame(firstRow, row);
                };
                var request = new MsgAttackPacket(new MsgAttackQuery(PlayerUid, PlayerUid, 0, 0,
                    MsgAttackType.Magic, 0, 0, 0, MsgAttackEffect.None, 0,
                    SpellId: HawksAmbitionAttachStatusSpellId)).ToFrameBytes();
                try { await f.SendAsync(PlayerUid, request); } finally { f.BeforeSend = null; }
                Assert.True(nested); Assert.True(sawProgress); Assert.Equal(2, stage);
                Assert.Equal(new[] { PlayerUid, TargetPlayerUid }, outerVectors.Select(row => row.Recipient));
                f.Data.AssertFiles(progressed); Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
                AssertOwnedHawksCurrent(f, before, clock, true);
                Assert.Same(peer, f.Current(TargetPlayerUid)); Assert.True(native.Player.BitVector.ArrayFlags.ContainsKey(194));
                Assert.Equal(OwnedHawksInteractionGolden(), request); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
            }, new OwnedEquipmentBehaviorInput(viewer.Character, viewer.PlayerItems, viewer.PlayerSpells));
    }

    private static byte[] HawksContinuationFanVectorGolden()
    {
        var entries = new List<(int Field, ulong Value)> { (1, 25) };
        for (int word = 0; word < 18; word++) entries.Add((3, word == 6 ? 4UL : word == 8 ? 1UL : 0UL));
        return OwnedHawksUpdateGolden(entries.ToArray());
    }

    private static WorldDataCatalog HawksContinuationFanCatalog()
    {
        // Exact DBServer/itemtype.txt line17968; no invented active Rune,
        // flag, constructor, clock stamp or relaxed Equip requirement.
        const string fan = "626003@@PrideFan@@16000@@0@@5@@0@@0@@2@@0@@0@@0@@153@@0@@9@@5@@0@@51@@0@@0@@0@@1599@@1599@@0@@0@@0@@0@@0@@0@@0@@0@@0@@1@@1000@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@0@@Handfan@@None@@5@@0@@0@@0@@1340@@0@@0@@0@@0@@0@@0@@";
        return NativeCallerWorldDataFixture.Create(new[] { ItemTypeParser.ParseLine(fan) }, [], magic: new[]
        {
            CreateMagicRow(HawksAmbitionAttachStatusSpellId, sort: 6, level: 0, experience: 50, damage: 35, duration: 33, coldTime: 0),
            CreateMagicRow(HawksAmbitionAttachStatusSpellId, sort: 6, level: 1, experience: 200, damage: 35, duration: 33, coldTime: 0),
        });
    }
}
