using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class OwnedRuneSpellIdentityTests
{
    private const uint RuneId = 4_020_517, RuneUid = 900_001;
    private const ushort UnrelatedId = 6001;

    [Theory]
    [InlineData(1u, (ushort)1)]
    [InlineData(99u, (ushort)1)]
    [InlineData(6_553_600u, (ushort)1)]
    [InlineData(6_553_700u, (ushort)1)]
    [InlineData(uint.MaxValue, (ushort)23592)]
    public Task InvalidRuneSpellMetadataCannotMoveTheRuneOrRemoveAnUnrelatedWrappedSpell(uint encoded, ushort endangered) =>
        WithRune(encoded, endangered, async f =>
        {
            var before = f.Current();
            var native = before.NativeGameClient!;
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(native.OrdinarySpellBinding);
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
            Assert.Contains(before.PlayerSpells.Spells, row => row.Id == endangered);
            Assert.True(native.MySpells!.ClientSpells.ContainsKey(endangered));
            var files = f.Data.Files(); int first = f.ActorFrames.Count;
            var player = native.Player!;
            var vitals = (player.HitPoints, player.Money, player.ConquerPoints, player.WHMoney);

            await f.DispatchAsync(Request());

            Assert.Same(before, f.Current());
            Assert.Equal((ushort)102, Assert.Single(f.Current().PlayerItems.RuneItems).Position);
            Assert.Equal(vitals, (player.HitPoints, player.Money, player.ConquerPoints, player.WHMoney));
            Assert.Same(binding, native.OrdinarySpellBinding); Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
            Assert.True(native.MySpells.ClientSpells.ContainsKey(endangered));
            Assert.Equal(before.PlayerSpells.Spells.Count, native.MySpells.ClientSpells.Count);
            Assert.Equal(first, f.ActorFrames.Count); f.Data.AssertFiles(files);
            Assert.False(LegacyTradeJournal.HasPending(f.Data.Users)); Assert.False(f.Recovery.IsBlocked);
            Assert.Contains("held MsgItemUsage unequip rune", f.Log.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("Frame exception", f.Log.ToString(), StringComparison.OrdinalIgnoreCase);
        });

    [Theory]
    [InlineData(0u, (ushort)14160, false)]
    [InlineData(1_416_016u, (ushort)14160, true)]
    [InlineData(6_553_599u, ushort.MaxValue, true)]
    public Task ValidRuneMetadataAndMaximumSpellIdentityKeepTheOriginalCommitAndReplySequence(
        uint encoded, ushort mapped, bool removes) => WithRune(encoded, mapped, async f =>
        {
            var before = f.Current(); var native = before.NativeGameClient!;
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(native.OrdinarySpellBinding);
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
            var saved = f.Data.Files();
            var characterPath = Path.GetRelativePath(f.Data.Root, f.CharacterPath(OwnedEquipmentBehaviorFixture.Actor));
            var itemsPath = Path.Combine(f.Data.Items, OwnedEquipmentBehaviorFixture.Actor + ".bin");
            var spellsPath = Path.Combine(f.Data.Spells, OwnedEquipmentBehaviorFixture.Actor + ".bin");
            var expectedSpells = removes ? before.PlayerSpells.Spells.Where(row => row.Id != mapped).ToArray() :
                before.PlayerSpells.Spells.ToArray();
            var expectedItems = before.PlayerItems with
            { RuneItems = before.PlayerItems.RuneItems.Select(row => row.Uid == RuneUid ? row.WithPosition(212) : row).ToArray() };
            var expectedFiles = new Dictionary<string, byte[]>(saved, StringComparer.OrdinalIgnoreCase)
            {
                [Path.GetRelativePath(f.Data.Root, itemsPath)] = PlayerItemSnapshotLoader.ToLegacyBytes(expectedItems),
                [Path.GetRelativePath(f.Data.Root, spellsPath)] = PlayerSpellSnapshotLoader.ToLegacyBytes(new PlayerSpellSnapshot(expectedSpells)),
            };
            if (removes)
            {
                var text = CharacterSnapshotFileEncoding.Decode(saved[characterPath], out var encoding);
                var marker = $"ManagedRuneSpellOrigins={mapped}:{encoded % 100}\r\n";
                Assert.Contains(marker, text, StringComparison.Ordinal);
                expectedFiles[characterPath] = encoding.GetPreamble().Concat(encoding.GetBytes(
                    text.Replace(marker, "ManagedRuneSpellOrigins=\r\n", StringComparison.Ordinal))).ToArray();
            }
            int observations = 0, first = f.ActorFrames.Count;
            void AssertCommitted()
            {
                Assert.NotSame(before, f.Current()); Assert.Same(binding, native.OrdinarySpellBinding);
                Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
                Assert.Equal((ushort)212, Assert.Single(f.Current().PlayerItems.RuneItems).Position);
                Assert.Equal(expectedSpells, f.Current().PlayerSpells.Spells);
                Assert.Equal(expectedSpells.Length, native.MySpells!.ClientSpells.Count);
                foreach (var row in expectedSpells)
                {
                    Assert.True(native.MySpells.ClientSpells.TryGetValue(row.Id, out var physical));
                    Assert.Equal(row.Level, physical!.Level); Assert.Equal(row.Experience, physical.Experience);
                    Assert.Equal(row.SoulLevel, physical.SoulLevel); Assert.Equal(row.PreviousLevel, physical.PreviousLevel);
                }
                Assert.Equal(!removes, native.MySpells.ClientSpells.ContainsKey(mapped));
                f.Data.AssertFiles(expectedFiles);
                Assert.Equal(expectedFiles[characterPath], File.ReadAllBytes(f.CharacterPath(OwnedEquipmentBehaviorFixture.Actor)));
                Assert.False(LegacyTradeJournal.HasPending(f.Data.Users)); Assert.False(f.Recovery.IsBlocked);
            }
            f.BeforeSend = (recipient, _) =>
            { Assert.Equal(OwnedEquipmentBehaviorFixture.Actor, recipient); AssertCommitted(); observations++; };
            try { await f.DispatchAsync(Request()); }
            finally { f.BeforeSend = null; }

            AssertCommitted(); Assert.Equal(removes ? 6 : 5, observations);
            var output = f.ActorFrames.Skip(first).ToArray(); Assert.Equal(observations, output.Length);
            int echo = 0;
            if (removes)
            {
                var removal = new MsgActionPacket
                { ObjId = OwnedEquipmentBehaviorFixture.Actor, DwParam = mapped, Type = (MsgActionType)109 };
                Assert.Equal(removal.ToFrameBytes(), output[echo++]);
            }
            Assert.Equal(Request().ToFrameBytes(), output[echo]);
            Assert.Contains("committed MsgItemUsage unequip rune", f.Log.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("Frame exception", f.Log.ToString(), StringComparison.OrdinalIgnoreCase);
        }, createClaim: removes);

    private static MsgItemUsagePacket Request() => new(RuneUid, 0, MsgItemUsageAction.UnequipRune, 1234, 22, 33, 44);

    private static Task WithRune(uint encoded, ushort learned, Func<OwnedEquipmentBehaviorFixture, Task> body, bool createClaim = false)
    {
        var item = new PlayerItemSnapshotItem
        { Uid = RuneUid, ItemId = RuneId, Position = createClaim ? (ushort)212 : (ushort)102, Durability = 1, MaximumDurability = 1, StackSize = 1 };
        var items = new PlayerItemSnapshot([], [], [], [], [item]);
        var spells = new PlayerSpellSnapshot(createClaim ? [new(UnrelatedId, 4, 1111, 2, 3, 2)] :
            [new(learned, 7, 1234, 2, 3, 1), new(UnrelatedId, 4, 1111, 2, 3, 2)]);
        var metadata = OwnedEquipmentBehaviorFixture.Item(RuneId, "OwnedRuneIdentity",
            new Dictionary<int, string> { [ItemType.RuneSpellIdFieldIndex] = encoded.ToString(System.Globalization.CultureInfo.InvariantCulture) });
        var effects = MagicEffectParser.Parse($"[{(long)learned * 100}]\r\nIsRuneSkill=1\r\n", "owned-rune-identity-MagicEffect.ini");
        var catalog = NativeCallerWorldDataFixture.Create(new ItemType[] { metadata }, [], magicEffects: effects);
        return OwnedEquipmentBehaviorFixture.RunAsync(new(OwnedEquipmentBehaviorFixture.Character(), items, spells), catalog, async fixture =>
        {
            if (createClaim)
            {
                await fixture.DispatchAsync(new MsgItemUsagePacket(RuneUid, 102, MsgItemUsageAction.EquipRune, 4321, 55, 66, 77));
                Assert.Equal($"{learned}:{encoded % 100}", fixture.Current().Character.Attributes["ManagedRuneSpellOrigins"]);
                Assert.Contains(fixture.Current().PlayerSpells.Spells, row => row.Id == learned);
            }
            await body(fixture);
        });
    }
}
