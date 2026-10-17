using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    [Fact]
    public async Task InitialEquipmentShowOwnsRidingCropBeforeRuneStartupWithoutPublic251()
    {
        var root = Path.Combine(Path.GetTempPath(), "wolf-native-startup-crop-" + Guid.NewGuid());
        try
        {
            const uint uid = 1_293_700;
            var users = Path.Combine(root, "Users");
            var items = Path.Combine(root, "PlayersItems");
            Directory.CreateDirectory(users);
            Directory.CreateDirectory(items);
            WriteSnapshot(users, uid, uid: uid, classCode: 2_005);
            new PlayerItemSnapshotLoader(items).SaveByCharacterUid(uid, PlayerItemSnapshot.Empty with
            {
                EquipmentItems =
                [
                    new PlayerItemSnapshotItem { Uid = 2_001, ItemId = 111_095, Position = 1,
                        Durability = 100, MaximumDurability = 100 },
                    new PlayerItemSnapshotItem { Uid = 2_002, ItemId = 203_003, Position = 18,
                        Durability = 100, MaximumDurability = 100 },
                ],
                RuneItems = [new PlayerItemSnapshotItem { Uid = 3_001, ItemId = 4_010_501, Position = 101 }],
            });
            ItemType Input(int id, string hp)
            {
                var fields = Enumerable.Repeat("0", 60).ToArray();
                fields[0] = id.ToString(System.Globalization.CultureInfo.InvariantCulture);
                fields[1] = "InitialCrop" + id; fields[18] = hp;
                return new ItemType(id, fields[1], fields);
            }
            var world = CreateStaticVisibilityCatalog(items:
                [Input(111_095, "50000"), Input(203_003, "0"), Input(4_010_501, "0")]);
            var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
            var ticket = handoff.Issue(uid, "native-startup-crop", ConquerRuntimeDefaults.WorldServer);
            var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(users, items), handoff,
                worldData: world, runtimeUpdateLoop: LegacyRuntimeUpdateLoopOptions.Disabled);
            var sent = new List<byte[]>();
            var connection = CreateSendingConnection(sent);
            await handler.HandleFrameAsync(connection,
                ReadFrame(new InternalWorldEnterRequestPacket(ticket.Token).ToFrameBytes()));
            var current = GetSession(handler, connection);
            Assert.Equal((long)uid, current.Character.Uid);
            Assert.Equal(203_003U, current.PhysicalStatusOverlay!.RidingCropItemId);
            Assert.Equal((long)uid, current.PhysicalStatusOverlay.OwnerUid);
            var cropIndex = FindLegacyGameItemIndex(sent, 2_002);
            var runeIndex = FindLegacyGameItemIndex(sent, 3_001);
            var status = Assert.Single(sent, bytes => LegacyPlayerStatusPacket.TryParse(ReadFrame(bytes),
                out var packet) && packet.Uid == uid);
            Assert.True(cropIndex >= 0 && sent.IndexOf(status) > cropIndex && runeIndex > sent.IndexOf(status));
            Assert.False(current.Character.Attributes.ContainsKey("RidingCropItemId"));
            // Real internal handoff startup only, with NO251. This is not a fresh external-client login/relogin claim.
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
