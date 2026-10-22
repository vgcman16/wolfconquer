using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort LegacyInfiniteMistSpellId = 16_380;
    private const uint LegacySkyVeilLevelNineItemId = 4_036_409;
    private const int LegacyInfiniteMistFlag = 0x11A;

    [Fact]
    public async Task HandlerMagicAttackAgainstVisiblePlayerWithInfiniteMistCounterCastsBeforeNormalDamage()
    {
        var result = await RunPlayerReceiveInfiniteMistMagicAttackAsync(
            targetAttributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["LegacyInfiniteMistRoll"] = "0",
            });

        Assert.Equal(5_000 - ExpectedPlayerThunderDamage, result.TargetSession.Character.MinHitPoints);
        Assert.True(HasLegacyStatusFlag(result.CasterSession.Character.Attributes, LegacyInfiniteMistFlag));
        AssertInfiniteMistCounterBeforeNormalThunderDamage(result.TargetSent);
        AssertInfiniteMistCounterBeforeNormalThunderDamage(result.CasterSent);
    }

    [Fact]
    public async Task HandlerMagicAttackAgainstVisiblePlayerWithAttackerSkyVeilSuppressesInfiniteMistAndKeepsDamage()
    {
        var result = await RunPlayerReceiveInfiniteMistMagicAttackAsync(
            casterItems: CreatePlayerItems(new PlayerItemSnapshotItem
            {
                Uid = LegacySkyVeilLevelNineItemId,
                ItemId = LegacySkyVeilLevelNineItemId,
                Position = 101,
            }),
            casterAttributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["LegacySkyVeilInfiniteMistRoll"] = "0",
            },
            targetAttributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["LegacyInfiniteMistRoll"] = "0",
            });

        Assert.Equal(5_000 - ExpectedPlayerThunderDamage, result.TargetSession.Character.MinHitPoints);
        Assert.False(HasLegacyStatusFlag(result.CasterSession.Character.Attributes, LegacyInfiniteMistFlag));
        AssertNoInfiniteMistCounter(result.CasterSent.Concat(result.TargetSent));
        AssertInfiniteMistNormalThunderDamage(result.CasterSent);
        AssertInfiniteMistNormalThunderDamage(result.TargetSent);
    }

    [Fact]
    public async Task HandlerInfiniteMist16380DirectCastAppliesVisibleTargetFlagAndSelfAnimation()
    {
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateInfiniteMistMagicRow());
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            classCode: 5_051,
            spellEntries:
            [
                CreatePlayerSpell(LegacyInfiniteMistSpellId),
            ]);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            x: 832,
            y: 584);
        sessions.Set(casterConnection, casterSession);
        sessions.Set(targetConnection, targetSession);
        var context = new WorldPacketHandlerContext(
            casterConnection,
            log,
            casterSession,
            WorldData: worldData,
            Sessions: sessions);
        var beforeCast = DateTimeOffset.UtcNow;

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
                SpellId: LegacyInfiniteMistSpellId)).ToFrameBytes()));

        var afterCast = DateTimeOffset.UtcNow;
        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.False(HasLegacyStatusFlag(updatedCasterSession.Character.Attributes, LegacyInfiniteMistFlag));
        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        Assert.True(HasLegacyStatusFlag(updatedTargetSession.Character.Attributes, LegacyInfiniteMistFlag));
        AssertLegacyXpSkillExpiresAround(
            updatedTargetSession.Character.Attributes,
            "LegacyInfiniteMistExpiresAtUtc",
            beforeCast,
            afterCast,
            expectedDurationSeconds: 10);

        var sentSnapshot = casterSent.Concat(targetSent).ToArray();
        Assert.Contains(
            ParseMsgUpdates(sentSnapshot),
            update => update.Uid == TargetPlayerUid &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    IsLegacyStatusFlagSet(entry.Values, LegacyInfiniteMistFlag)));
        Assert.DoesNotContain(
            ParseMsgUpdates(sentSnapshot).SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill &&
                entry.Flag == LegacyInfiniteMistFlag);

        foreach (var spell in new[] { Assert.Single(ParseSpellAnimations(casterSent)), Assert.Single(ParseSpellAnimations(targetSent)) })
        {
            Assert.Equal(PlayerUid, spell.AttackerUid);
            Assert.Equal(PlayerUid, spell.AttackedUid);
            Assert.Equal(0, spell.X);
            Assert.Equal(0, spell.Y);
            Assert.Equal(LegacyInfiniteMistSpellId, spell.SpellId);
            var target = Assert.Single(spell.Targets);
            Assert.Equal(PlayerUid, target.Uid);
            Assert.Equal(0U, target.Damage);
            Assert.True(target.Hit);
            Assert.Equal(MsgAttackEffect.None, target.Effect);
        }

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy InfiniteMist AttachStatus magic", output);
        Assert.Contains("source=AttachStatus.cs case 16380", output);
    }

    private static async Task<PlayerReceiveInfiniteMistHitResult> RunPlayerReceiveInfiniteMistMagicAttackAsync(
        PlayerItemSnapshot? casterItems = null,
        IReadOnlyDictionary<string, string>? casterAttributes = null,
        IReadOnlyDictionary<string, string>? targetAttributes = null)
    {
        var monsterFamily = CreateMonsterFamily();
        var worldData = CreateMagicCatalogWithItems(
            monsterFamily,
            [],
            [CreateRetainedNativeMagic421Helmet(), CreateItemType((int)LegacySkyVeilLevelNineItemId, "Sky Veil(+9)")],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 541, coldTime: 0),
            CreateInfiniteMistMagicRow());
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateSession(
            playerItems: casterItems,
            attributes: casterAttributes);
        var targetSession = CreateSession(
            uid: TargetPlayerUid,
            name: "Target",
            classCode: 5_051,
            x: 832,
            y: 584,
            spellEntries:
            [
                CreatePlayerSpell(LegacyInfiniteMistSpellId),
            ],
            attributes: targetAttributes);
        casterSession = CreateRetainedNativeMagic421Input(casterSession);
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
        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);

        return new PlayerReceiveInfiniteMistHitResult(
            updatedCasterSession,
            updatedTargetSession,
            casterSent,
            targetSent,
            log.ToString());
    }

    private static MagicType CreateInfiniteMistMagicRow()
    {
        var row = CreateMagicRow(
            LegacyInfiniteMistSpellId,
            sort: 6,
            useMana: 0,
            damage: 1,
            range: 15,
            coldTime: 0,
            duration: 10);
        var fields = row.Fields.ToArray();
        fields[12] = "10";
        fields[16] = "282";
        return row with { Fields = fields };
    }

    private static void AssertInfiniteMistCounterBeforeNormalThunderDamage(IReadOnlyList<byte[]> frames)
    {
        var infiniteMistIndex = SingleFrameIndex(frames, frame =>
            LegacySpellAnimationPacket.TryParse(frame, out var spell) &&
            IsInfiniteMistCounter(spell));
        var thunderIndex = SingleFrameIndex(frames, frame =>
            LegacySpellAnimationPacket.TryParse(frame, out var spell) &&
            IsNormalThunderDamage(spell));

        Assert.True(infiniteMistIndex < thunderIndex);
    }

    private static void AssertInfiniteMistNormalThunderDamage(IReadOnlyList<byte[]> frames)
    {
        Assert.Contains(ParseSpellAnimations(frames), IsNormalThunderDamage);
    }

    private static void AssertNoInfiniteMistCounter(IEnumerable<byte[]> frames)
    {
        Assert.DoesNotContain(ParseSpellAnimations(frames), IsInfiniteMistCounter);
    }

    private static bool IsInfiniteMistCounter(LegacySpellAnimationPacket spell)
    {
        if (spell.AttackerUid != TargetPlayerUid ||
            spell.AttackedUid != TargetPlayerUid ||
            spell.SpellId != LegacyInfiniteMistSpellId ||
            spell.SpellLevel != 0 ||
            spell.X != 0 ||
            spell.Y != 0)
        {
            return false;
        }

        if (spell.Targets.Count != 1)
        {
            return false;
        }

        var target = spell.Targets[0];
        return target.Uid == TargetPlayerUid &&
            target.Damage == 0 &&
            target.Hit &&
            target.Effect == MsgAttackEffect.None;
    }

    private static bool IsNormalThunderDamage(LegacySpellAnimationPacket spell)
    {
        if (spell.AttackerUid != PlayerUid ||
            spell.SpellId != ThunderSpellId)
        {
            return false;
        }

        if (spell.Targets.Count != 1)
        {
            return false;
        }

        var target = spell.Targets[0];
        return target.Uid == TargetPlayerUid &&
            target.Damage == ExpectedPlayerThunderDamage &&
            target.Hit &&
            target.Effect == MsgAttackEffect.None;
    }

    private sealed record PlayerReceiveInfiniteMistHitResult(
        WorldSession CasterSession,
        WorldSession TargetSession,
        IReadOnlyList<byte[]> CasterSent,
        IReadOnlyList<byte[]> TargetSent,
        string Output);
}
