using System.Buffers.Binary;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyEquipmentDurabilityTests
{
    [Fact]
    public void ActualLowerWriterRoutesCharacterByAccountAndItemsByCharacterUidWithoutClaimingAdmission()
    {
        // A separate actual v2 file-participant control, not an admitted session:
        // The actual loader and v7 admission currently require equal IDs.
        using var data = new LegacyOwnedAdmissionRuntimeFixture(bootstrap: false);
        const long account = 2;
        var characterPath = Path.Combine(data.Users, account + ".ini");
        Assert.Equal(data.Users, Path.GetDirectoryName(Path.GetFullPath(characterPath)));
        File.Move(data.UserPath, characterPath);
        Assert.False(Directory.Exists(data.Registry));
        var itemsPath = Path.Combine(data.Items, LegacyOwnedAdmissionRuntimeFixture.Uid + ".bin");
        var row = new PlayerItemSnapshotItem { Uid = 1_000_091, ItemId = EquipmentFixture.Necklace,
            Position = 0, StackSize = 1, Durability = 321, MaximumDurability = 321 };
        var rawBefore = PlayerItemSnapshotLoader.ToLegacyBytes(new([row], [], [], [], []));
        rawBefore[4 + 14] = 0xB6;
        File.WriteAllBytes(itemsPath, rawBefore);
        // Parse the actual file with its actual account-key source name. This is
        // explicitly not loader/admission acceptance for this distinct-ID shape.
        var character = CharacterSnapshotParser.ParseText(
            CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(characterPath), out _), Path.GetFileName(characterPath));
        Assert.Equal(account + ".ini", character.SourceFile);
        Assert.Equal(LegacyOwnedAdmissionRuntimeFixture.Uid, character.Uid);
        var currentItems = data.Loader.LoadPlayerItemsByCharacterUid(character.Uid);
        var expected = new WorldSession(account, "FileRoutingOnly", character, currentItems,
            PlayerSpellSnapshot.Empty, PlayerProficiencySnapshot.Empty, UsersRoot: data.Users);
        Assert.Null(expected.NativeGameClient);
        var gear = Assert.Single(currentItems.InventoryItems);
        var proposed = expected with { PlayerItems = new PlayerItemSnapshot([], [], [], [gear.WithPosition(2)], []) };
        Assert.True(LegacyItemSaveGraph.TryCapture(expected.PlayerItems, out var before));
        Assert.True(LegacyItemSaveGraph.TryCapture(proposed.PlayerItems, out var after));
        var characterBefore = File.ReadAllBytes(characterPath);
        var anticipated = (byte[])rawBefore.Clone();
        BinaryPrimitives.WriteUInt16LittleEndian(anticipated.AsSpan(4 + 12, 2), 2);

        Assert.True(LegacyEquipmentMovePersistence.TryPersist(expected, expected, proposed, data.Items,
            before!, after!, out var reason), reason);

        Assert.Equal(characterBefore, File.ReadAllBytes(characterPath));
        Assert.Equal(anticipated, File.ReadAllBytes(itemsPath));
        Assert.False(File.Exists(data.UserPath));
        Assert.False(File.Exists(Path.Combine(data.Items, account + ".bin")));
        Assert.False(Directory.Exists(data.Registry));
        Assert.False(LegacyTradeJournal.HasPending(data.Users));
        // No Store/native owner, PIN, permission or live Frame acceptance is asserted by this lower writer.
    }
}
