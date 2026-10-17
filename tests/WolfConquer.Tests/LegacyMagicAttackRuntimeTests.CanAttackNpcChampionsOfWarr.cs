using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const int ChampionsOfWarrPoleLife = 250_000_000;

    [Fact]
    public async Task HandlerMagicAttackAgainstChampionsOfWarrPoleWithActiveEnemyGuildSendsSpellAnimationAndDamagesSobNpc()
    {
        var result = await RunChampionsOfWarrPoleMagicAttackAsync(CreateChampionsOfWarrPoleAttributes());

        AssertChampionsOfWarrPoleManaSpent(result);
        var spell = Assert.Single(ParseSpellAnimations(result.Sent));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(ThunderSpellId, spell.SpellId);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(checked((uint)LegacySobNpcEventRules.ChampionsOfWarrPoleUid), target.Uid);
        Assert.Equal(ExpectedPlayerThunderDamage, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(
            ChampionsOfWarrPoleLife - (int)ExpectedPlayerThunderDamage,
            result.SobNpcStates.GetVisible(result.SobNpc).Life);

        Assert.Contains("WorldServer handled legacy player-SOB magic", result.Output);
        Assert.DoesNotContain("source CanAttackNpc.Verified rejected", result.Output);
    }

    [Theory]
    [InlineData("NoGuild", null, "CasterGuild", ChampionsOfWarrPoleLife, "Alive")]
    [InlineData("ZeroPoleHitPoints", 501, "CasterGuild", 0, "Alive")]
    [InlineData("SamePoleGuildName", 501, "Champions", ChampionsOfWarrPoleLife, "Alive")]
    [InlineData("DeadProcess", 501, "CasterGuild", ChampionsOfWarrPoleLife, "Dead")]
    [InlineData("MissingProcessDefaultsDead", 501, "CasterGuild", ChampionsOfWarrPoleLife, null)]
    public async Task HandlerMagicAttackAgainstChampionsOfWarrPoleSourceGuardBlocksAttack(
        string scenario,
        int? guildId,
        string guildName,
        int poleHitPoints,
        string? process)
    {
        var result = await RunChampionsOfWarrPoleMagicAttackAsync(
            CreateChampionsOfWarrPoleAttributes(
                guildId,
                guildName,
                poleHitPoints,
                process));

        AssertChampionsOfWarrPoleManaSpent(result);
        Assert.Empty(ParseSpellAnimations(result.Sent));
        Assert.Equal(ChampionsOfWarrPoleLife, result.SobNpcStates.GetVisible(result.SobNpc).Life);
        Assert.Contains(ExpectedChampionsOfWarrPoleFailureDiagnostic(scenario, result.SobNpc), result.Output);
        Assert.DoesNotContain("WorldServer handled legacy player-SOB magic", result.Output);
    }

    private static async Task<ChampionsOfWarrPoleAttackResult> RunChampionsOfWarrPoleMagicAttackAsync(
        IReadOnlyDictionary<string, string> attributes)
    {
        var monsterFamily = CreateMonsterFamily();
        var sobNpc = CreateChampionsOfWarrPoleSobNpc();
        var worldData = CreateMagicCatalogWithSobNpcs(
            monsterFamily,
            [sobNpc],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 120, coldTime: 0));
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            mapId: LegacySobNpcEventRules.ChampionsOfWarrMapId,
            x: sobNpc.X - 1,
            y: sobNpc.Y,
            attributes: attributes);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            SobNpcStates: sobNpcStates);

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: checked((uint)sobNpc.Id),
                X: checked((ushort)sobNpc.X),
                Y: checked((ushort)sobNpc.Y),
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: ThunderSpellId)).ToFrameBytes()));

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        return new ChampionsOfWarrPoleAttackResult(
            sobNpc,
            sobNpcStates,
            updatedSession,
            sent,
            log.ToString());
    }

    private static void AssertChampionsOfWarrPoleManaSpent(ChampionsOfWarrPoleAttackResult result)
    {
        Assert.Equal(493, result.UpdatedSession.Character.MinMana);
    }

    private static SobNpc CreateChampionsOfWarrPoleSobNpc()
    {
        return new SobNpc(
            Id: LegacySobNpcEventRules.ChampionsOfWarrPoleUid,
            Name: "Champions",
            Kind: LegacySobNpcEventRules.PoleKind,
            Lookface: 1_137,
            MapId: LegacySobNpcEventRules.ChampionsOfWarrMapId,
            X: 55,
            Y: 54,
            Life: ChampionsOfWarrPoleLife,
            MaxLife: ChampionsOfWarrPoleLife,
            Unknown9: 17,
            Unknown10: 1,
            LocalizedName: null,
            SourceLine: 93,
            RawLine: "6547,Champions,10,1137,6546,55,54,250000000,250000000,17,1",
            Fields:
            [
                "6547",
                "Champions",
                "10",
                "1137",
                "6546",
                "55",
                "54",
                "250000000",
                "250000000",
                "17",
                "1",
            ]);
    }

    private static IReadOnlyDictionary<string, string> CreateChampionsOfWarrPoleAttributes(
        int? guildId = 501,
        string guildName = "CasterGuild",
        int poleHitPoints = ChampionsOfWarrPoleLife,
        string? process = "Alive")
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (guildId is { } id)
        {
            var guildIdText = id.ToString(CultureInfo.InvariantCulture);
            attributes["GuildID"] = guildIdText;
            attributes["GuildId"] = guildIdText;
            attributes["GuildName"] = guildName;
            attributes["LegacyGuildName"] = guildName;
        }

        SetChampionsOfWarrPoleHitPointsAttributePairs(attributes, poleHitPoints);
        if (process is not null)
        {
            attributes["LegacyMsgChampionsOfWarrProcess"] = process;
            attributes["MsgChampionsOfWarrProcess"] = process;
            attributes["RuntimeMsgChampionsOfWarrProcess"] = process;
            attributes["LegacyChampionsOfWarrProcess"] = process;
            attributes["ChampionsOfWarrProcess"] = process;
            attributes["RuntimeChampionsOfWarrProcess"] = process;
        }

        return attributes;
    }

    private static void SetChampionsOfWarrPoleHitPointsAttributePairs(
        Dictionary<string, string> attributes,
        int poleHitPoints)
    {
        var value = poleHitPoints.ToString(CultureInfo.InvariantCulture);
        attributes["LegacyMsgChampionsOfWarrPoleHitPoints"] = value;
        attributes["LegacyMsgChampionsOfWarrPoleHitpoints"] = value;
        attributes["MsgChampionsOfWarrPoleHitPoints"] = value;
        attributes["MsgChampionsOfWarrPoleHitpoints"] = value;
        attributes["RuntimeMsgChampionsOfWarrPoleHitPoints"] = value;
        attributes["RuntimeMsgChampionsOfWarrPoleHitpoints"] = value;
        attributes["LegacyChampionsOfWarrPoleHitPoints"] = value;
        attributes["LegacyChampionsOfWarrPoleHitpoints"] = value;
        attributes["ChampionsOfWarrPoleHitPoints"] = value;
        attributes["ChampionsOfWarrPoleHitpoints"] = value;
        attributes["RuntimeChampionsOfWarrPoleHitPoints"] = value;
        attributes["RuntimeChampionsOfWarrPoleHitpoints"] = value;
    }

    private static string ExpectedChampionsOfWarrPoleFailureDiagnostic(
        string scenario,
        SobNpc sobNpc)
    {
        var prefix =
            $"map={sobNpc.MapId} kind={sobNpc.Kind}; source CanAttackNpc.Verified rejected " +
            $"ChampionsOfWarr pole uid={sobNpc.Id} because ";
        const string SourcePole = "MsgSchedules.ChampionsOfWarr.Furnitures[Pole]";
        return scenario switch
        {
            "NoGuild" => prefix + "caster has no guild",
            "ZeroPoleHitPoints" => prefix + SourcePole + ".HitPoints is zero",
            "SamePoleGuildName" => prefix + "client.Player.MyGuild.GuildName matches " + SourcePole + ".Name",
            "DeadProcess" => prefix + "MsgSchedules.ChampionsOfWarr.Proces is Dead",
            "MissingProcessDefaultsDead" => prefix + "MsgSchedules.ChampionsOfWarr.Proces is Dead",
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, null),
        };
    }

    private readonly record struct ChampionsOfWarrPoleAttackResult(
        SobNpc SobNpc,
        SobNpcRuntimeStateStore SobNpcStates,
        WorldSession UpdatedSession,
        IReadOnlyList<byte[]> Sent,
        string Output);
}
