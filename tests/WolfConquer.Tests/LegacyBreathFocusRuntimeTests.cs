using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort BreathFocusSpellId = 11_960;

    [Theory]
    [InlineData(93UL, 12U, 100UL, true)]
    [InlineData(100UL, 12U, 100UL, false)]
    public async Task HandlerBreathFocusRestoresStaminaProgressesByOneThousandAndSendsCasterAnimation(
        ulong startingStamina,
        uint staminaDamage,
        ulong expectedStamina,
        bool expectsStaminaUpdate)
    {
        var spellsRoot = Path.Combine(
            Path.GetTempPath(),
            "wolfconquer-breathfocus-spell-xp-" + Guid.NewGuid().ToString("N"),
            "PlayersSpells");
        try
        {
            var monsterFamily = CreateMonsterFamily();
            var worldData = CreateMagicCatalog(
                monsterFamily,
                [],
                CreateBreathFocusRow(damage: staminaDamage, experience: 5_000),
                CreateBreathFocusRow(level: 1, damage: staminaDamage + 1, experience: 10_000));
            var sessions = new WorldSessionStore();
            var sent = new List<byte[]>();
            using var log = new StringWriter();
            var connection = CreateSendingConnection(sent);
            var session = CreateSession(
                x: 830,
                y: 584,
                stamina: startingStamina,
                spellEntries:
                [
                    new PlayerSpellSnapshotEntry(
                        BreathFocusSpellId,
                        Level: 0,
                        Experience: 0,
                        PreviousLevel: 0,
                        SoulLevel: 3,
                        UseSpellSoul: 3),
                ]);
            sessions.Set(connection, session);
            var context = new WorldPacketHandlerContext(
                connection,
                log,
                session,
                WorldData: worldData,
                Sessions: sessions,
                PlayerSpellsRoot: spellsRoot);

            await PacketHandlers.HandleAsync(
                context,
                ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                    Uid: PlayerUid,
                    OpponentUid: 0,
                    X: 900,
                    Y: 901,
                    AttackType: MsgAttackType.Magic,
                    Damage: 0,
                    KilledMonster: 0,
                    ResponseDamage: 0,
                    Effect: MsgAttackEffect.None,
                    Unknown: 0,
                    SpellId: BreathFocusSpellId)).ToFrameBytes()));

            var animationIndex = SingleFrameIndex(
                sent,
                frame => LegacySpellAnimationPacket.TryParse(frame, out _));
            var spellPacketIndex = SingleFrameIndex(
                sent,
                frame => LegacySpellPacket.TryParse(frame, out _));
            var coldTimeIndex = SingleFrameIndex(
                sent,
                frame => LegacyMagicColdTimePacket.TryParse(frame, out _));
            Assert.True(coldTimeIndex < animationIndex);
            Assert.True(spellPacketIndex < animationIndex);

            var animation = Assert.Single(ParseSpellAnimations(sent));
            Assert.Equal(PlayerUid, animation.AttackerUid);
            Assert.Equal(0U, animation.AttackedUid);
            Assert.Equal(830, animation.X);
            Assert.Equal(584, animation.Y);
            Assert.Equal(BreathFocusSpellId, animation.SpellId);
            Assert.Equal(0, animation.SpellLevel);
            Assert.Equal(3, animation.LevelHu);
            Assert.Empty(animation.Targets);

            var spellUpdate = Assert.Single(ParseBreathFocusSpellPackets(sent));
            Assert.Equal(BreathFocusSpellId, spellUpdate.Id);
            Assert.Equal(0, spellUpdate.Level);
            Assert.Equal(1_000, spellUpdate.Experience);

            var updates = ParseMsgUpdates(sent);
            if (expectsStaminaUpdate)
            {
                var staminaUpdateIndex = SingleFrameIndex(
                    sent,
                    frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                        update.Uid == PlayerUid &&
                        update.Updates.SequenceEqual(
                            [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, expectedStamina)]));
                Assert.True(staminaUpdateIndex < spellPacketIndex);
            }
            else
            {
                Assert.DoesNotContain(
                    updates,
                    update => update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.Stamina));
            }

            var coldTime = Assert.Single(ParseMagicColdTimePackets(sent));
            var coldTimeSpell = Assert.Single(coldTime.Spells);
            Assert.Equal(BreathFocusSpellId, coldTimeSpell.SpellId);
            Assert.Equal(1_500, coldTimeSpell.Time);

            Assert.True(sessions.TryGet(connection, out var updatedSession));
            Assert.NotNull(updatedSession);
            Assert.Equal(
                expectedStamina.ToString(CultureInfo.InvariantCulture),
                updatedSession.Character.Attributes["Stamina"]);
            var updatedSpell = Assert.Single(updatedSession.PlayerSpells.Spells);
            Assert.Equal(BreathFocusSpellId, updatedSpell.Id);
            Assert.Equal(0, updatedSpell.Level);
            Assert.Equal(1_000, updatedSpell.Experience);

            var persisted = new PlayerSpellSnapshotLoader(spellsRoot).LoadByCharacterUid(PlayerUid);
            Assert.Equal(1_000, Assert.Single(persisted.Spells).Experience);

            var output = log.ToString();
            Assert.Contains("WorldServer handled legacy BreathFocus magic", output);
            Assert.Contains("progressDamage=1000", output);
        }
        finally
        {
            var tempRoot = Path.GetDirectoryName(spellsRoot);
            if (!string.IsNullOrWhiteSpace(tempRoot) && Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static MagicType CreateBreathFocusRow(
        byte level = 0,
        uint damage = 5,
        uint experience = 0)
    {
        return CreateMagicRow(
            BreathFocusSpellId,
            sort: 70,
            level: level,
            useMana: 0,
            damage: damage,
            experience: experience,
            useStamina: 0,
            coolDown: 1,
            coldTime: 1_500);
    }

    private static List<LegacySpellPacket> ParseBreathFocusSpellPackets(IEnumerable<byte[]> frames)
    {
        var packets = new List<LegacySpellPacket>();
        foreach (var bytes in frames)
        {
            if (LegacySpellPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }
}
