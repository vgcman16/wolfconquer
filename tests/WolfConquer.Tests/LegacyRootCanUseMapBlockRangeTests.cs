using System.Globalization;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData((byte)1, 832L, (byte)9, 3_825UL, 1_000L, false, true)]
    [InlineData((byte)48, 832L, (byte)9, 3_825UL, 1_000L, false, true)]
    [InlineData((byte)1, 850L, (byte)20, 0UL, 1_000L, false, true)]
    [InlineData((byte)48, 850L, (byte)20, 0UL, 1_000L, false, true)]
    [InlineData((byte)1, 840L, (byte)9, 0UL, 1_000L, false, false)]
    [InlineData((byte)1, 865L, (byte)40, 0UL, 1_000L, false, false)]
    [InlineData((byte)1, 832L, (byte)9, 0UL, 1_001L, false, false)]
    [InlineData((byte)1, 830L, (byte)9, 0UL, 1_000L, true, true)]
    public async Task RootCanUseRawMapBlockLookupControlsOnlyPreCostRangeAdmission(
        byte sort, long targetX, byte range, ulong casterDynamicId, long targetMap,
        bool selfTarget, bool expectedAdmission)
    {
        var id = sort == 48 ? (ushort)1_011 : (ushort)1_000;
        var worldData = CreateMagicCatalog(CreateMonsterFamily(), [],
            CreateMagicRow(id, sort: sort, useMana: 7, useStamina: 5,
                damage: 120, range: range, coolDown: 0, coldTime: 0));
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        var peerSent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var peerConnection = CreateSendingConnection(peerSent);
        using var log = new StringWriter(CultureInfo.InvariantCulture);
        var clock = new ManualRuntimeTimeProvider(RootMagicEntryOrigin);
        var owner = CreateSession(mapId: 1_000, strength: 0, spirit: 0,
            minMana: 1_000, stamina: 100, spellIds: [id],
            attributes: new Dictionary<string, string>
            {
                ["PkMode"] = "0",
                ["DynamicID"] = casterDynamicId.ToString(CultureInfo.InvariantCulture),
            });
        var peer = CreateSession(uid: TargetPlayerUid, name: "RawMapRangePeer",
            mapId: targetMap, x: targetX, y: 584, strength: 0, spirit: 0,
            minHitPoints: 100_000, minMana: 1_000, stamina: 100, spellIds: [],
            attributes: new Dictionary<string, string> { ["PkMode"] = "0", ["DynamicID"] = "0" });
        sessions.Set(connection, owner);
        sessions.Set(peerConnection, peer);
        var context = new WorldPacketHandlerContext(connection, log, owner,
            WorldData: worldData, Sessions: sessions, TimeProvider: clock,
            ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        Assert.False(owner.Character.Attributes.ContainsKey("LegacyAttackStampUtc"));
        var targetUid = selfTarget ? PlayerUid : TargetPlayerUid;
        var queryX = selfTarget ? owner.Character.X : peer.Character.X;
        var queryY = selfTarget ? owner.Character.Y : peer.Character.Y;

        await PacketHandlers.HandleAsync(context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid, OpponentUid: targetUid,
                X: checked((ushort)queryX), Y: checked((ushort)queryY),
                AttackType: MsgAttackType.Magic, Damage: 0, KilledMonster: 0,
                ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0,
                SpellId: id)).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var current));
        Assert.NotNull(current);
        if (expectedAdmission)
            Assert.Equal(RootMagicEntryOrigin.ToString("O", CultureInfo.InvariantCulture),
                current.Character.Attributes["LegacyAttackStampUtc"]);
        else
        {
            Assert.Same(owner, current);
            Assert.False(current.Character.Attributes.ContainsKey("LegacyAttackStampUtc"));
            Assert.Empty(sent);
            Assert.Empty(peerSent);
        }
        Assert.Equal(expectedAdmission ? 993L : 1_000L, current.Character.MinMana);
        Assert.Equal(expectedAdmission ? "95" : "100", current.Character.Attributes["Stamina"]);
        var costs = ParseMsgUpdates(sent).Where(packet => packet.Uid == PlayerUid)
            .SelectMany(packet => packet.Updates)
            .Where(entry => entry.Type is LegacyMsgUpdateDataType.Stamina or LegacyMsgUpdateDataType.Mana)
            .Select(entry => (entry.Type, entry.Value)).ToArray();
        (LegacyMsgUpdateDataType Type, ulong Value)[] expectedCosts = expectedAdmission
            ? [(LegacyMsgUpdateDataType.Stamina, 95UL), (LegacyMsgUpdateDataType.Mana, 993UL)] : [];
        Assert.Equal(expectedCosts, costs);
        // Later damage/CanAttack/visibility is NOT the pre-cost map-object lookup being proved here.
        // Positions, map/instance IDs and learned/catalog inputs are declared; no stamp/cost/cache outcome is seeded.
    }
}
