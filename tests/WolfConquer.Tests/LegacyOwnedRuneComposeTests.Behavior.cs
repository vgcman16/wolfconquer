using System.Buffers.Binary;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedRuneComposeTests
{
    [Fact]
    public Task ActualComposePreservesCurrentAndSavedHealthUnderARealActiveTransform() => Run(async f =>
    {
        var before = f.Current;
        f.Store.Set(f.Connection, before with
        { Character = LegacyTransformState.Apply(before.Character, 207, 3000, DateTimeOffset.UtcNow.AddHours(1)) });
        Assert.True(LegacyTransformState.TryGetActiveMaxHitpoints(f.Current.Character, out var cap)); Assert.Equal(3000U, cap);
        Assert.Equal(3000L, f.Current.Character.MinHitPoints); Assert.Equal(3000, f.Native.Player!.HitPoints);
        var saved = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(f.CharacterPath), out _));
        Assert.Equal(3000L, saved.MinHitPoints); Assert.False(LegacyTransformState.TryGetActiveMaxHitpoints(saved, out _));
        var character = File.ReadAllBytes(f.CharacterPath);
        await ComposeAndAssert(f, ComposeFixture.Request(), ComposeFixture.TargetType + 1, 0, [ComposeFixture.Material]);
        Assert.Equal(character, File.ReadAllBytes(f.CharacterPath)); Assert.Equal(3000L, f.Current.Character.MinHitPoints);
        Assert.Equal(3000, f.Native.Player.HitPoints);
    });

    [Fact]
    public Task ActualEquippedComposeConsumesTheWholeEssenceStackAndRetainsTheExactExistingGrant() => Run(async f =>
    {
        Assert.Equal("14160:16", f.Origins); var oldSpell = f.Native.MySpells!.ClientSpells[14160];
        await ComposeAndAssert(f, ComposeFixture.Request(), ComposeFixture.TargetType + 1, 0, [ComposeFixture.Material]);
        Assert.Equal("14160:16", f.Origins); Assert.Equal((ushort)16, f.Current.PlayerSpells.Spells.Single(row => row.Id == 14160).Level);
        Assert.Equal((ushort)16, f.Native.MySpells.ClientSpells[14160].Level);
        Assert.Same(oldSpell, f.Native.MySpells.ClientSpells[14160]);
        // Compose did not perform progression. The player's actual next Equip
        // retires its exact old grant and creates the new item's real level17.
        var items = File.ReadAllBytes(f.ItemsPath); var spells = File.ReadAllBytes(f.SpellsPath);
        int ordinal = f.Current.PlayerSpells.Spells.Select((row, index) => (row, index)).Single(pair => pair.row.Id == 14160).index;
        var expectedSpells = spells.ToArray(); BinaryPrimitives.WriteUInt16LittleEndian(expectedSpells.AsSpan(4 + ordinal * 12 + 2), 17);
        var text = CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(f.CharacterPath), out var encoding);
        Assert.Contains("ManagedRuneSpellOrigins=14160:16\r\n", text, StringComparison.Ordinal);
        var expectedCharacter = encoding.GetPreamble().Concat(encoding.GetBytes(text.Replace("ManagedRuneSpellOrigins=14160:16\r\n",
            "ManagedRuneSpellOrigins=14160:17\r\n", StringComparison.Ordinal))).ToArray();
        int first = f.Sent.Count;
        f.BeforeSend = _ =>
        {
            Assert.Equal(items, File.ReadAllBytes(f.ItemsPath)); Assert.Equal(expectedSpells, File.ReadAllBytes(f.SpellsPath));
            Assert.Equal(expectedCharacter, File.ReadAllBytes(f.CharacterPath)); f.AssertConverged();
        };
        var equip = new MsgItemUsagePacket(ComposeFixture.Target, 102, MsgItemUsageAction.EquipRune, 112233, 44, 55, 66);
        try { await f.DispatchAsync(equip); } finally { f.BeforeSend = null; }
        Assert.Equal(first + 8, f.Sent.Count);
        Assert.Equal(new MsgItemUsagePacket(ComposeFixture.Target, 102, MsgItemUsageAction.UnequipRune, 0, 0, 0, 0).ToFrameBytes(), f.Sent[first]);
        Assert.Equal(new MsgActionPacket { ObjId = ComposeFixture.Uid, DwParam = 14160, Type = (MsgActionType)109 }.ToFrameBytes(), f.Sent[first + 1]);
        Assert.Equal(Convert.FromHexString("19003508000000005037000011000000000000000000000000"), f.Sent[first + 2]);
        Assert.Equal(equip.ToFrameBytes(), f.Sent[first + 3]);
        Assert.Equal("14160:17", f.Origins); Assert.Equal((ushort)17, f.Current.PlayerSpells.Spells.Single(row => row.Id == 14160).Level);
        Assert.Equal((ushort)17, f.Native.MySpells.ClientSpells[14160].Level); f.AssertConverged();
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task StoredComposeUsesTheSameRealCurveWithoutInventingAnEquipAdmission(bool rawZero) => Run(async f =>
    {
        ushort stack = rawZero ? (ushort)0 : (ushort)1;
        Assert.Equal(stack, Assert.Single(f.Current.PlayerItems.RuneItems, row => row.Uid == ComposeFixture.Target).StackSize);
        Assert.Equal(stack, Assert.Single(f.Native.OrdinarySpellBinding!.Items.Rune.Objects, row => row.Uid == ComposeFixture.Target).StackSize);
        await ComposeAndAssert(f, ComposeFixture.Request(), ComposeFixture.TargetType + 1, 0, [ComposeFixture.Material]);
        Assert.Equal((ushort)212, f.Current.PlayerItems.RuneItems.Single(row => row.Uid == ComposeFixture.Target).Position);
        Assert.Equal(stack, Assert.Single(f.Current.PlayerItems.RuneItems, row => row.Uid == ComposeFixture.Target).StackSize);
        Assert.Null(f.Origins); Assert.Equal(f.OriginalSpells, File.ReadAllBytes(f.SpellsPath));
    }, mode: rawZero ? "stored-zero" : "stored");

    [Fact]
    public Task ActualPassiveLevelAdvanceRetainsDisplayIdentityWithoutCreatingAPhantomSpell() => Run(async f =>
    {
        await ComposeAndAssert(f, ComposeFixture.Request(), ComposeFixture.PassiveType + 1, 0, [ComposeFixture.Material]);
        Assert.Null(f.Origins); Assert.Equal(f.OriginalSpells, File.ReadAllBytes(f.SpellsPath));
        Assert.DoesNotContain(f.Current.PlayerSpells.Spells, row => row.Id == 40301);
        var item = Assert.Single(f.Native.OrdinarySpellBinding!.Items.Rune.Objects, row => row.Uid == ComposeFixture.Target);
        Assert.Equal(4_030_101U, item.SpellID); Assert.Equal((LegacyNativeGameItemFlags.ItemEffect)1, item.Effect);
    }, mode: "passive");

    [Fact]
    public Task ActualZeroStackMaterialContributesOneUnitAndIsRetiredWithoutRewritingOtherRows() => Run(async f =>
    {
        Assert.Equal((ushort)0, Assert.Single(f.Current.PlayerItems.InventoryItems, row => row.Uid == ComposeFixture.Material).StackSize);
        await ComposeAndAssert(f, ComposeFixture.Request(), ComposeFixture.TargetType, 10, [ComposeFixture.Material]);
    }, mode: "stack-zero");

    [Fact]
    public Task ActualEssenceAtItsConfiguredCapConsumesExactly10000UnitsWithoutReachingTheRuneMaximum() => Run(async f =>
    {
        var material = Assert.Single(f.Current.PlayerItems.InventoryItems, row => row.Uid == ComposeFixture.Material);
        Assert.Equal((ushort)10000, material.StackSize); Assert.Equal((ushort)10000, f.Native.Inventory!.ClientItems[ComposeFixture.Material].StackSize);
        Assert.Equal("10000", Assert.Single(f.Catalog.ItemsById[(int)ComposeFixture.EssenceType]).Fields[ItemType.StackSizeFieldIndex]);
        await ComposeAndAssert(f, ComposeFixture.Request(), ComposeFixture.TargetType + 4, 17500, [ComposeFixture.Material]);
        Assert.Equal("14160:16", f.Origins); Assert.Equal((ushort)16, f.Native.MySpells!.ClientSpells[14160].Level);
    }, mode: "essence-cap");

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task DeclaredOneUsesOnlyItsCompletePrefixAndIgnoresSurplusMaterialWords(bool missingSurplus) => Run(async f =>
    {
        var surplus = missingSurplus ? 1_020_999U : ComposeFixture.Surplus;
        await ComposeAndAssert(f, ComposeFixture.Request(1, ComposeFixture.Material, surplus), ComposeFixture.TargetType + 1, 0, [ComposeFixture.Material]);
        Assert.Contains(f.Current.PlayerItems.InventoryItems, row => row.Uid == ComposeFixture.Surplus);
    });

    [Theory]
    [InlineData(0U)] [InlineData(50U)]
    public Task SourceEmptySelectionStillPublishesOnlyTheOwnedTargetUpdate(uint declaredCount) => Run(async f =>
        await ComposeAndAssert(f, ComposeFixture.Request(declaredCount), ComposeFixture.TargetType, 0, []));

    [Fact]
    public Task ConsumingTheLastActualRuneProviderRetiresOnlyItsExactOwnedRowBeforeItsItemRemoval() => Run(async f =>
    {
        Assert.Equal("14160:16,15470:0", f.Origins);
        await ComposeAndAssert(f, ComposeFixture.Request(), ComposeFixture.TargetType, 100, [ComposeFixture.Material], retiredSpell: 15470);
        Assert.Equal("14160:16", f.Origins); Assert.DoesNotContain(f.Current.PlayerSpells.Spells, row => row.Id == 15470);
    }, mode: "provider");

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task AStoredSingleRuneMaterialUsesRawZeroOrOneAsOnePhysicalUnit(bool rawZero) => Run(async f =>
    {
        ushort stack = rawZero ? (ushort)0 : (ushort)1;
        var material = Assert.Single(f.Current.PlayerItems.RuneItems, row => row.Uid == ComposeFixture.Material);
        Assert.Equal(ComposeFixture.OtherType, material.ItemId); Assert.Equal((ushort)212, material.Position); Assert.Equal(stack, material.StackSize);
        Assert.Equal(stack, Assert.Single(f.Native.OrdinarySpellBinding!.Items.Rune.Objects, row => row.Uid == ComposeFixture.Material).StackSize);
        Assert.Equal("14160:16", f.Origins); Assert.DoesNotContain(f.Current.PlayerSpells.Spells, row => row.Id == 15470);
        await ComposeAndAssert(f, ComposeFixture.Request(), ComposeFixture.TargetType, 100, [ComposeFixture.Material]);
        Assert.Equal("14160:16", f.Origins); Assert.Equal((ushort)16, f.Native.MySpells!.ClientSpells[14160].Level);
    }, mode: rawZero ? "rune-material-zero" : "rune-material");

    [Fact]
    public Task ConsumingOneSharedProviderPreservesTheAlreadyOwnedTargetGrantByteForByte() => Run(async f =>
    {
        Assert.Equal("14160:16", f.Origins);
        await ComposeAndAssert(f, ComposeFixture.Request(), ComposeFixture.TargetType + 5, 4000, [ComposeFixture.Material]);
        Assert.Equal("14160:16", f.Origins); Assert.Equal((ushort)16, f.Current.PlayerSpells.Spells.Single(row => row.Id == 14160).Level);
    }, mode: "shared");

    [Fact]
    public Task ConsumingAnUnmarkedPermanentProviderPreservesItsCompleteSavedAndNativeSpell() => Run(async f =>
    {
        var row = f.Current.PlayerSpells.Spells.Single(value => value.Id == 15470); Assert.Equal(1234, row.Experience);
        await ComposeAndAssert(f, ComposeFixture.Request(), ComposeFixture.TargetType, 100, [ComposeFixture.Material]);
        Assert.Equal(row, f.Current.PlayerSpells.Spells.Single(value => value.Id == 15470)); Assert.Equal("14160:16", f.Origins);
    }, mode: "permanent");

    private sealed record ExpectedImages(byte[] Character, byte[] Items, byte[] Spells, uint[] Consumed)
    {
        internal void AssertFiles(ComposeFixture f)
        {
            Assert.Equal(Character, File.ReadAllBytes(f.CharacterPath)); Assert.Equal(Items, File.ReadAllBytes(f.ItemsPath));
            Assert.Equal(Spells, File.ReadAllBytes(f.SpellsPath)); Assert.Equal(f.Ledger, File.ReadAllBytes(f.Data.State));
        }
    }

    private static ExpectedImages Expect(ComposeFixture f, uint itemId, uint experience, uint[] consumed, ushort? retiredSpell = null)
    {
        var before = f.Current.PlayerItems;
        var after = before with
        {
            InventoryItems = before.InventoryItems.Where(row => !consumed.Contains(row.Uid)).ToArray(),
            RuneItems = before.RuneItems.Where(row => !consumed.Contains(row.Uid)).Select(row => row.Uid == ComposeFixture.Target
                ? row.WithItemId(itemId).WithRuneExperience(experience) : row).ToArray(),
        };
        var character = File.ReadAllBytes(f.CharacterPath); var spells = File.ReadAllBytes(f.SpellsPath);
        if (retiredSpell is { } id)
        {
            var old = f.Origins!; var next = string.Join(',', old.Split(',').Where(value => !value.StartsWith(id + ":", StringComparison.Ordinal)));
            var text = CharacterSnapshotFileEncoding.Decode(character, out var encoding);
            Assert.Contains("ManagedRuneSpellOrigins=" + old + "\r\n", text, StringComparison.Ordinal);
            character = encoding.GetPreamble().Concat(encoding.GetBytes(text.Replace("ManagedRuneSpellOrigins=" + old + "\r\n",
                "ManagedRuneSpellOrigins=" + next + "\r\n", StringComparison.Ordinal))).ToArray();
            var count = BinaryPrimitives.ReadInt32LittleEndian(spells); var rows = Enumerable.Range(0, count)
                .Select(index => spells.AsSpan(4 + index * 12, 12).ToArray()).Where(row => BinaryPrimitives.ReadUInt16LittleEndian(row) != id).ToArray();
            Assert.Equal(count - 1, rows.Length); spells = new byte[4 + rows.Length * 12];
            BinaryPrimitives.WriteInt32LittleEndian(spells, rows.Length);
            for (int index = 0; index < rows.Length; index++) rows[index].CopyTo(spells, 4 + index * 12);
        }
        return new(character, PlayerItemSnapshotLoader.ToLegacyBytes(after), spells, consumed);
    }

    private static async Task ComposeAndAssert(ComposeFixture f, byte[] request, uint itemId, uint experience, uint[] consumed, ushort? retiredSpell = null)
    {
        var expected = Expect(f, itemId, experience, consumed, retiredSpell); var before = f.Current;
        var oldIds = LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(File.ReadAllBytes(f.ItemsPath)); int first = f.Sent.Count;
        f.BeforeSend = _ => { expected.AssertFiles(f); f.AssertConverged(); };
        try { await f.SendAsync(request); } finally { f.BeforeSend = null; }
        expected.AssertFiles(f); f.AssertConverged();
        var output = f.Sent.Skip(first).ToArray(); Assert.Equal(consumed.Length + 1 + (retiredSpell.HasValue ? 1 : 0), output.Length);
        int index = 0;
        if (retiredSpell is { } id) Assert.Equal(new MsgActionPacket { ObjId = ComposeFixture.Uid, DwParam = id, Type = (MsgActionType)109 }.ToFrameBytes(), output[index++]);
        foreach (var uid in consumed) Assert.Equal(RemoveGolden(uid), output[index++]);
        var target = Assert.Single(f.Current.PlayerItems.RuneItems, row => row.Uid == ComposeFixture.Target);
        Assert.Equal(itemId, target.ItemId); Assert.Equal(experience, target.RuneExperience);
        var update = LegacyGameItemPacket.FromSnapshotItem(target, LegacyGameItemPacket.CreateRuneSpellOverride(target, f.Catalog.ItemsById), LegacyGameItemPacket.UpdateItemMode).ToFrameBytes();
        Assert.Equal(UpdateGolden(target), update); Assert.Equal(update, output[index]); Assert.True(LegacyGameItemPacket.TryParse(ComposeReadFrame(output[index]), out var decoded));
        Assert.Equal(ComposeFixture.Target, decoded.Uid); Assert.Equal(itemId, decoded.ItemId); Assert.Equal(experience, decoded.RuneExperience);
        Assert.Equal(LegacyGameItemPacket.UpdateItemMode, decoded.Mode);
        Assert.Equal(oldIds.Except(consumed).Order(), LegacyItemUidRawCatalog.ReadAdmissionPlayerItemUids(expected.Items).Order());
        Assert.Equal((uint)0, before.PlayerItems.RuneItems.Single(row => row.Uid == ComposeFixture.Target).RuneExperience);
        Assert.Equal((byte)0xA5, File.ReadAllBytes(f.SpellsPath)[15]);
        var rankings = Assert.IsType<LegacyRuneRankingState>(typeof(WorldServerFrameHandler).GetField("_runeRankings",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(f.Handler));
        var score = LegacyRuneStoragePacket.ComputeStorageScore(f.Current.PlayerItems);
        if (score >= LegacyRuneRankingState.MinimumScore) Assert.Equal(score, Assert.Single(rankings.Snapshot(), row => row.CharacterUid == ComposeFixture.Uid).Score);
        else Assert.DoesNotContain(rankings.Snapshot(), row => row.CharacterUid == ComposeFixture.Uid);
    }

    private static byte[] RemoveGolden(uint uid)
    {
        var bytes = new byte[91]; BinaryPrimitives.WriteUInt16LittleEndian(bytes, 91); BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 2137);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), uid); BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(16), 3); return bytes;
    }
    private static PacketFrame ComposeReadFrame(byte[] bytes) => OwnedEquipmentBehaviorFixture.ReadFrame(bytes);

    // Full independent D13 2116 layout for this explicit zero-extra-field input.
    // All reserved and owner/signature tail bytes remain zero.
    private static byte[] UpdateGolden(PlayerItemSnapshotItem item)
    {
        var bytes = new byte[206]; BinaryPrimitives.WriteUInt16LittleEndian(bytes, 206);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 2116); BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), item.Uid);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), item.ItemId); BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 10);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(14), 10); BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(16), 3);
        bytes[18] = checked((byte)item.Position);
        uint display = item.ItemId / 10000 == 403 ? 4_030_101U : 1_416_000U + item.ItemId % 100 - 1;
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(25), display); if (item.ItemId / 10000 == 403) bytes[29] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(54), item.StackSize);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(166), item.RuneExperience); return bytes;
    }
}
