using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyOwnedAttributeArithmeticTests
{
    private const uint Actor = OwnedEquipmentBehaviorFixture.Actor;
    private static OwnedEquipmentBehaviorInput Input() => new(
        OwnedEquipmentBehaviorFixture.Character(classCode: 14005, agility: 0, strength: 0, minHitPoints: 30)
            with { Vitality = 0, Spirit = 10, AttributePoints = 12 }, PlayerItemSnapshot.Empty);

    [Theory]
    [InlineData("BloodTideHP", "4294967295")]
    public Task InvalidIntermediateArithmeticRefusesBeforeAllocation(string key, string value) =>
        OwnedEquipmentBehaviorFixture.RunAsync(Input(), OwnedEquipmentBehaviorFixture.Catalog(), async f =>
        {
            var admitted = f.Current();
            var fields = new Dictionary<string, string>(admitted.Character.Attributes, StringComparer.OrdinalIgnoreCase) { [key] = value };
            // A real generic publication supplies data; it does not supply query authority.
            f.Store.Set(f.Connection(Actor), admitted with { Character = admitted.Character with { Attributes = fields } });
            await AssertRefusedAsync(f);
        });

    [Fact]
    public Task SavedMaximumVigorClampsForRuntimeWithoutRewritingSavedValue()
    {
        var input = Input();
        var attributes = new Dictionary<string, string>(input.Character.Attributes, StringComparer.OrdinalIgnoreCase)
            { ["Vigor"] = "4294967295" };
        input = input with { Character = input.Character with { Attributes = attributes } };
        return OwnedEquipmentBehaviorFixture.RunAsync(input, OwnedEquipmentBehaviorFixture.Catalog(), async f =>
        {
            var before = f.Current(); var binding = before.NativeGameClient!.OrdinarySpellBinding!;
            Assert.Equal("0", before.Character.Attributes["Vigor"]); // No equipped crop or steed.
            var expected = f.Data.Files().ToDictionary(row => row.Key, row => row.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
            string key = Path.GetRelativePath(f.Data.Root, f.CharacterPath());
            string text = CharacterSnapshotFileEncoding.Decode(expected[key], out var encoding);
            Assert.Contains("\r\nVigor=4294967295\r\n", text);
            Assert.Contains("\r\nSpirit=10\r\n", text); Assert.Contains("\r\nAtributes=12\r\n", text);
            text = text.Replace("\r\nSpirit=10\r\n", "\r\nSpirit=20\r\n", StringComparison.Ordinal)
                .Replace("\r\nAtributes=12\r\n", "\r\nAtributes=2\r\n", StringComparison.Ordinal);
            expected[key] = encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();
            int start = f.ActorFrames.Count;
            await f.SendAsync(Actor, new LegacyAttributeSetPacket(0, 0, 0, 10).ToFrameBytes());
            var after = f.Current();
            Assert.Equal("0", after.Character.Attributes["Vigor"]);
            Assert.Equal(20L, after.Character.Spirit); Assert.Equal(2L, after.Character.AttributePoints);
            Assert.Equal((ushort)20, binding.Player.Spirit); Assert.Equal((ushort)2, binding.Player.Atributes);
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding)); Assert.False(binding.Recovery.IsBlocked);
            f.Data.AssertFiles(expected);
            Assert.Equal(new ushort[] { 2203, 2176, 2176, 2301, 2176, 2176, 2176, 2176, 2128 },
                f.ActorFrames.Skip(start).Select(bytes => OwnedEquipmentBehaviorFixture.ReadFrame(bytes).Header.Opcode));

            // The first allocation ran a complete subsequent equipment query.
            // Its own completion must issue the next genuine Vigor receipt.
            text = text.Replace("\r\nSpirit=20\r\n", "\r\nSpirit=22\r\n", StringComparison.Ordinal)
                .Replace("\r\nAtributes=2\r\n", "\r\nAtributes=0\r\n", StringComparison.Ordinal);
            expected[key] = encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();
            int second = f.ActorFrames.Count;
            await f.SendAsync(Actor, new LegacyAttributeSetPacket(0, 0, 0, 2).ToFrameBytes());
            var final = f.Current();
            Assert.Equal("0", final.Character.Attributes["Vigor"]);
            Assert.Equal(22L, final.Character.Spirit); Assert.Equal(0L, final.Character.AttributePoints);
            Assert.Equal((ushort)22, binding.Player.Spirit); Assert.Equal((ushort)0, binding.Player.Atributes);
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding)); Assert.False(binding.Recovery.IsBlocked);
            f.Data.AssertFiles(expected);
            Assert.Equal(new ushort[] { 2203, 2176, 2176, 2301, 2176, 2176, 2176, 2176, 2128 },
                f.ActorFrames.Skip(second).Select(bytes => OwnedEquipmentBehaviorFixture.ReadFrame(bytes).Header.Opcode));
        });
    }

    [Theory]
    [InlineData("4294967294")]
    [InlineData("0")]
    public Task ExternalSavedVigorDriftRefusesAllocationEvenAfterQueryClamp(string changedSeed)
    {
        var input = Input();
        var attributes = new Dictionary<string, string>(input.Character.Attributes, StringComparer.OrdinalIgnoreCase)
            { ["Vigor"] = "4294967295" };
        input = input with { Character = input.Character with { Attributes = attributes } };
        return OwnedEquipmentBehaviorFixture.RunAsync(input, OwnedEquipmentBehaviorFixture.Catalog(), async f =>
        {
            Assert.Equal("0", f.Current().Character.Attributes["Vigor"]);
            string path = f.CharacterPath();
            var bytes = File.ReadAllBytes(path);
            string text = CharacterSnapshotFileEncoding.Decode(bytes, out var encoding);
            Assert.Contains("\r\nVigor=4294967295\r\n", text);
            text = text.Replace("\r\nVigor=4294967295\r\n", "\r\nVigor=" + changedSeed + "\r\n", StringComparison.Ordinal);
            File.WriteAllBytes(path, encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray());
            await AssertRefusedAsync(f);
        });
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    public Task ReplacedRuntimeVigorMapCannotBorrowQueryReceipt(string value)
    {
        var input = Input();
        var attributes = new Dictionary<string, string>(input.Character.Attributes, StringComparer.OrdinalIgnoreCase)
            { ["Vigor"] = "4294967295" };
        input = input with { Character = input.Character with { Attributes = attributes } };
        return OwnedEquipmentBehaviorFixture.RunAsync(input, OwnedEquipmentBehaviorFixture.Catalog(), async f =>
        {
            var before = f.Current();
            Assert.Equal("0", before.Character.Attributes["Vigor"]);
            var replacement = new Dictionary<string, string>(before.Character.Attributes, StringComparer.OrdinalIgnoreCase)
                { ["Vigor"] = value };
            f.Store.Set(f.Connection(Actor), before with { Character = before.Character with { Attributes = replacement } });
            await AssertRefusedAsync(f);
        });
    }

    [Fact]
    public Task UnrelatedSameVigorMapDuringQueryCannotAcquireCompletionReceipt()
    {
        var input = Input();
        var attributes = new Dictionary<string, string>(input.Character.Attributes, StringComparer.OrdinalIgnoreCase)
            { ["Vigor"] = "4294967295" };
        input = input with { Character = input.Character with { Attributes = attributes } };
        return OwnedEquipmentBehaviorFixture.RunAsync(input, OwnedEquipmentBehaviorFixture.Catalog(), async f =>
        {
            bool replaced = false;
            f.BeforeSend = (recipient, bytes) =>
            {
                if (recipient != Actor || replaced || !LegacyUserTotalRefineLevPacket.TryParse(
                        OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out _)) return;
                replaced = true;
                var current = f.Current();
                Assert.Equal("0", current.Character.Attributes["Vigor"]);
                var copy = new Dictionary<string, string>(current.Character.Attributes, StringComparer.OrdinalIgnoreCase);
                f.Store.Set(f.Connection(Actor), current with { Character = current.Character with { Attributes = copy } });
            };
            try { await f.SendAsync(Actor, LegacyRuneStoragePacket.Startup().ToFrameBytes()); }
            finally { f.BeforeSend = null; }
            Assert.True(replaced);
            await AssertRefusedAsync(f);
        });
    }

    [Fact]
    public Task LostNativeOwnerCannotUseQueryVigorReceipt()
    {
        var input = Input();
        var attributes = new Dictionary<string, string>(input.Character.Attributes, StringComparer.OrdinalIgnoreCase)
            { ["Vigor"] = "4294967295" };
        input = input with { Character = input.Character with { Attributes = attributes } };
        return OwnedEquipmentBehaviorFixture.RunAsync(input, OwnedEquipmentBehaviorFixture.Catalog(), async f =>
        {
            var before = f.Current(); var binding = before.NativeGameClient!.OrdinarySpellBinding!;
            Assert.Equal("0", before.Character.Attributes["Vigor"]);
            var files = f.Data.Files(); int start = f.ActorFrames.Count;
            var storage = binding.Client.MyProfs;
            try
            {
                binding.Client.MyProfs = null;
                await f.SendAsync(Actor, new LegacyAttributeSetPacket(0, 0, 0, 10).ToFrameBytes());
                Assert.Same(before, f.Current());
                Assert.Equal(start, f.ActorFrames.Count);
                f.Data.AssertFiles(files);
            }
            finally { binding.Client.MyProfs = storage; }
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
        });
    }

    [Fact]
    public Task CumulativeMagicOverflowAfterNewSpiritRefusesBeforeAllocation()
    {
        var row = RuneParser.ParseStorageAttributeRow("owned-rune-arithmetic.txt", 1,
            "1@@0@@3@@2147482640@@3@@2147482640@@0@@0@@");
        var witchery = OwnedEquipmentBehaviorFixture.Item(4_031_301, "Witchery (+1)");
        var catalog = NativeCallerWorldDataFixture.Create(new[] { witchery }, [], runeStorageAttributes: new[] { row });
        var input = Input() with { Items = PlayerItemSnapshot.Empty with
        { RuneItems = new PlayerItemSnapshotItem[] { new() { Uid = 99103, ItemId = 4_031_301, Position = 103,
            Durability = 100, MaximumDurability = 100, StackSize = 1 } } } };
        return OwnedEquipmentBehaviorFixture.RunAsync(input, catalog, async f =>
        {
            var before = f.Current(); var binding = before.NativeGameClient!.OrdinarySpellBinding!;
            Assert.Equal(uint.MaxValue - 4, LegacyNativeMagicQueryClient.Read(f.Store, f.Connection(Actor), Actor, binding.NativeIdentity).MagicAttack);
            // The ordinary status projection omits the actual query's Witchery
            // increment, so evaluating that projection alone cannot catch this.
            var raw = LegacyPlayerStatusCalculator.CalculateFields(before.Character with { Spirit = 20 }, before.PlayerItems,
                catalog, 60, 0, alternateEquipmentActive: before.AlternateEquipmentActive);
            Assert.Equal(uint.MaxValue - 1994, raw[LegacyPlayerStatusPacket.MagicAttackIndex]);
            await AssertRefusedAsync(f);
        });
    }

    [Fact]
    public Task RealHpSetterDuringAllocationPreservesOpaqueCaseVariants() =>
        OwnedEquipmentBehaviorFixture.RunAsync(Input(), OwnedEquipmentBehaviorFixture.Catalog(), async f =>
        {
            var admitted = f.Current();
            var fields = new Dictionary<string, string>(admitted.Character.Attributes, StringComparer.Ordinal)
            { ["Other"] = "one", ["other"] = "two", ["BloodTideHP"] = "5" };
            f.Store.Set(f.Connection(Actor), admitted with { Character = admitted.Character with { Attributes = fields } });
            var before = f.Current(); var binding = before.NativeGameClient!.OrdinarySpellBinding!;
            var expected = f.Data.Files().ToDictionary(pair => pair.Key, pair => pair.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
            string path = Path.GetRelativePath(f.Data.Root, f.CharacterPath());
            string text = CharacterSnapshotFileEncoding.Decode(expected[path], out var encoding);
            Assert.Contains("\r\nMinHitPoints=30\r\n", text);
            text = text.Replace("\r\nSpirit=10\r\n", "\r\nSpirit=20\r\n", StringComparison.Ordinal)
                .Replace("\r\nAtributes=12\r\n", "\r\nAtributes=2\r\n", StringComparison.Ordinal)
                .Replace("\r\nMinHitPoints=30\r\n", "\r\nMinHitPoints=35\r\n", StringComparison.Ordinal);
            expected[path] = encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();
            int first = f.ActorFrames.Count;
            await f.SendAsync(Actor, new LegacyAttributeSetPacket(0, 0, 0, 10).ToFrameBytes());
            var after = f.Current();
            Assert.Equal(20L, after.Character.Spirit); Assert.Equal(2L, after.Character.AttributePoints);
            Assert.Equal(35L, after.Character.MinHitPoints); Assert.Equal(35, binding.Player.HitPoints);
            Assert.Equal("one", after.Character.Attributes["Other"]); Assert.Equal("two", after.Character.Attributes["other"]);
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding)); Assert.False(binding.Recovery.IsBlocked);
            f.Data.AssertFiles(expected);
            Assert.Equal(new ushort[] { 2203, 2176, 2176, 2301, 2176, 2176, 2176, 2176, 2128 },
                f.ActorFrames.Skip(first).Select(bytes => OwnedEquipmentBehaviorFixture.ReadFrame(bytes).Header.Opcode));
        });

    private static async Task AssertRefusedAsync(OwnedEquipmentBehaviorFixture f)
    {
        var before = f.Current(); var binding = before.NativeGameClient!.OrdinarySpellBinding!;
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
        var files = f.Data.Files(); int first = f.ActorFrames.Count;
        var magic = LegacyNativeMagicQueryClient.Read(f.Store, f.Connection(Actor), Actor, binding.NativeIdentity);
        var status = binding.Client.Status; var hp = binding.Player.HitPoints;
        await f.SendAsync(Actor, new LegacyAttributeSetPacket(0, 0, 0, 10).ToFrameBytes());
        Assert.Same(before, f.Current()); Assert.Equal(first, f.ActorFrames.Count); f.Data.AssertFiles(files);
        Assert.Equal((ushort)10, binding.Player.Spirit); Assert.Equal((ushort)12, binding.Player.Atributes);
        Assert.Equal(hp, binding.Player.HitPoints); Assert.Same(status, binding.Client.Status);
        Assert.Equal(magic, LegacyNativeMagicQueryClient.Read(f.Store, f.Connection(Actor), Actor, binding.NativeIdentity));
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding)); Assert.False(binding.Recovery.IsBlocked);
    }
}
