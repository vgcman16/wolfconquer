using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort LegacyBloodTideSpellId = 15_500;
    private const int LegacyBloodTideFlag = 0xE5;
    private const uint LegacyBloodTideFineRainUpdateType = 0x66;
    private const uint LegacyBloodTideFineRainFlag = 229;
    private const uint LegacyBloodTideDurationSeconds = 45;
    private const uint ExpectedBloodTideHitpoints = 5_000;

    [Fact]
    public async Task HandlerMagicAttackAgainstVisiblePlayerAppliesBloodTideForTrojanSourceWhenRollAndCooldownAllow()
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalogWithItems(
            monsterFamily,
            [],
            [CreateRetainedNativeMagic421Helmet(), CreateRetainedNativeMagic421Helmet(itemId: 111_003, name: "Retained alternate helmet")],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 541, coldTime: 0),
            CreateMagicRow(
                LegacyBloodTideSpellId,
                sort: 0,
                useMana: 0,
                damage: 0,
                coldTime: 0,
                duration: LegacyBloodTideDurationSeconds));
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterItems = CreateBloodTideCasterItems();
        var casterSession = CreateSession(
            classCode: 1_000,
            spellEntries:
            [
                CreatePlayerSpell(ThunderSpellId),
                CreatePlayerSpell(LegacyBloodTideSpellId),
            ],
            playerItems: casterItems,
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["LegacyBloodTideRoll"] = "0",
                ["LegacyBloodTideStampUtc"] = DateTimeOffset.UtcNow
                    .AddMinutes(-2)
                    .ToString("O", CultureInfo.InvariantCulture),
            }) with
        {
            AlternateEquipmentActive = true,
        };
        var targetSession = CreateSession(uid: TargetPlayerUid, name: "Target", x: 832, y: 584);
        targetSession = CreateRetainedNativeMagic421Input(targetSession);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);

        context = await PrepareRetainedNativeMagic421QueriesAsync(context, targetConnection, casterSent, targetSent);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: checked((ushort)targetSession.Character.X),
                Y: checked((ushort)targetSession.Character.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ThunderSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.True(HasLegacyStatusFlag(updatedCasterSession.Character.Attributes, LegacyBloodTideFlag));
        Assert.Equal(
            ExpectedBloodTideHitpoints.ToString(CultureInfo.InvariantCulture),
            updatedCasterSession.Character.Attributes["BloodTideHP"]);
        Assert.True(updatedCasterSession.Character.Attributes.ContainsKey("LegacyBloodTideStampUtc"));

        var casterUpdateEntries = new List<LegacyMsgUpdateEntry>();
        foreach (var update in ParseMsgUpdates(casterSent))
        {
            if (update.Uid == PlayerUid)
            {
                casterUpdateEntries.AddRange(update.Updates);
            }
        }

        var fineRainEntry = Assert.Single(
            casterUpdateEntries,
            entry => (uint)entry.Type == LegacyBloodTideFineRainUpdateType);
        Assert.Equal(LegacyBloodTideFineRainFlag, fineRainEntry.Flag);
        Assert.Equal(LegacyBloodTideDurationSeconds, fineRainEntry.Time);
        Assert.Equal(ExpectedBloodTideHitpoints, fineRainEntry.Damage);
        Assert.Equal(ExpectedBloodTideHitpoints, fineRainEntry.SpellLevel);

        var bloodTideAnimation = Assert.Single(
            ParseSpellAnimations(casterSent),
            spell => spell.SpellId == LegacyBloodTideSpellId);
        Assert.Equal(PlayerUid, bloodTideAnimation.AttackerUid);
        Assert.Equal(PlayerUid, bloodTideAnimation.AttackedUid);
        Assert.Equal(0, bloodTideAnimation.X);
        Assert.Equal(0, bloodTideAnimation.Y);
        Assert.Equal(0, bloodTideAnimation.SpellLevel);
        Assert.Empty(bloodTideAnimation.Targets);

        var equipmentRefresh = Assert.Single(ParseShowEquipmentRefreshes(casterSent));
        Assert.Equal(
            LegacyShowEquipmentPacket.CreateEquipmentRefresh(casterItems, alternateEquipmentActive: true),
            equipmentRefresh);
        Assert.Equal(LegacyShowEquipmentPacket.ShowAction, equipmentRefresh.Action);
        Assert.Equal(1U, equipmentRefresh.Alternante);
        Assert.Equal(51_500U, equipmentRefresh.EquipmentSlots.Head);

        foreach (var spell in new[]
        {
            Assert.Single(ParseSpellAnimations(casterSent), spell => spell.SpellId == ThunderSpellId),
            Assert.Single(ParseSpellAnimations(targetSent), spell => spell.SpellId == ThunderSpellId),
        })
        {
            Assert.Equal(PlayerUid, spell.AttackerUid);
            Assert.Equal(ThunderSpellId, spell.SpellId);
            var target = Assert.Single(spell.Targets);
            Assert.Equal(TargetPlayerUid, target.Uid);
            Assert.Equal(ExpectedPlayerThunderDamage, target.Damage);
            Assert.True(target.Hit);
        }

        Assert.Contains("WorldServer handled legacy player-player magic", log.ToString());
    }

    private static PlayerItemSnapshot CreateBloodTideCasterItems()
    {
        return new PlayerItemSnapshot(
            InventoryItems: [],
            RelicFuseItems: [],
            MythsoulBagItems: [],
            EquipmentItems:
            [
                new PlayerItemSnapshotItem
                {
                    Uid = 51_500,
                    ItemId = 111_003,
                    Durability = 100,
                    MaximumDurability = 100,
                    Position = 51,
                },
            ],
            RuneItems: []);
    }
}
