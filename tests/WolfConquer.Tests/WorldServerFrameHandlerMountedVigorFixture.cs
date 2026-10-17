using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    private CharacterSnapshotLoader CreateMountedVigorSnapshotLoader()
    {
        var itemsRoot = Path.Combine(_usersRoot, "MountedVigorItems");
        Directory.CreateDirectory(itemsRoot);
        File.WriteAllBytes(Path.Combine(itemsRoot, "1293600.bin"),
            PlayerItemSnapshotLoaderTests.BuildPlayerItems(
                [PlayerItemSnapshotLoaderTests.DbItem(uid: 20_018, itemId: 203_003, position: 18,
                    durability: 6_698, maximumDurability: 6_698)], []));
        return new CharacterSnapshotLoader(_usersRoot, itemsRoot);
    }

    private static void AssertMountedVigorStartup(
        IReadOnlyList<byte[]> sent, WorldSession session, uint expectedVigor)
    {
        var showBytes = Assert.Single(sent, bytes =>
            LegacyShowEquipmentPacket.TryParse(ReadFrame(bytes), out var packet) &&
                packet.Action == LegacyShowEquipmentPacket.ShowAction);
        Assert.True(LegacyShowEquipmentPacket.TryParse(ReadFrame(showBytes), out var show));
        Assert.Equal(20_018U, show.EquipmentSlots.RidingCrop);
        Assert.Equal(203_003U, LegacyPhysicalStatusOverlay.ForOwner(session).RidingCropItemId);
        var infoBytes = Assert.Single(sent, bytes => LegacyServerInfoPacket.TryParse(ReadFrame(bytes), out _));
        Assert.True(LegacyServerInfoPacket.TryParse(ReadFrame(infoBytes), out var info));
        Assert.Equal(expectedVigor, info.Vigor);
        Assert.Equal(expectedVigor.ToString(System.Globalization.CultureInfo.InvariantCulture),
            session.Character.Attributes["Vigor"]);
    }
}
