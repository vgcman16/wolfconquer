using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData("42", "9001,85,70,99,0,1", true, "")]
    [InlineData("0", "9001,85,70,99,0,1", false, "caster has no guild")]
    [InlineData("42", "9001,85,70,42,0,1", false, "capturerGuildId=42 matches caster GuildID")]
    [InlineData("42", "9001,85,70,99,0,0", false, "CaptureTheFlag.Proces is not Alive")]
    public async Task HandlerMagicAttackAgainstCaptureTheFlagBaseHonorsSourceCanAttackNpcGate(
        string guildId,
        string captureTheFlagBases,
        bool expectedDamage,
        string expectedDiagnostic)
    {
        var monsterFamily = CreateMonsterFamily();
        var sobNpc = CreateCaptureTheFlagBaseSobNpc();
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
            mapId: sobNpc.MapId,
            x: sobNpc.X - 1,
            y: sobNpc.Y,
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["GuildID"] = guildId,
                ["CaptureTheFlagBases"] = captureTheFlagBases,
            });
        sessions.Set(connection, session);
        var rejectionBefore = CaptureCtfRejectionState(session);
        Assert.True(sobNpcStates.TryCaptureMagicPrimaryTarget(sobNpc, out var initialObservation));
        Assert.NotNull(initialObservation);
        Assert.Same(sobNpc, initialObservation.Source);
        var initialView = initialObservation.Visible;
        Assert.NotSame(sobNpc, initialView);
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
        var spells = ParseSpellAnimations(sent);
        var output = log.ToString();
        if (expectedDamage)
        {
            Assert.Equal(493, updatedSession.Character.MinMana);
            // The view used during permission reads must never become the
            // runtime producer. Actual damage still belongs to this source.
            Assert.True(sobNpcStates.TryCaptureMagicPrimaryTarget(sobNpc, out var canonical), output);
            Assert.NotNull(canonical);
            Assert.Same(sobNpc, canonical.Source);
            Assert.False(sobNpcStates.TryCaptureMagicPrimaryTarget(initialView, out _));
            var spell = Assert.Single(spells);
            var target = Assert.Single(spell.Targets);
            Assert.Equal(checked((uint)sobNpc.Id), target.Uid);
            Assert.Equal(ExpectedPlayerThunderDamage, target.Damage);
            Assert.Equal(sobNpc.Life - (int)ExpectedPlayerThunderDamage, sobNpcStates.GetVisible(sobNpc).Life);
            Assert.Contains("WorldServer handled legacy player-SOB magic", output);
            Assert.DoesNotContain("rejected CaptureTheFlag base", output);
            return;
        }

        AssertCtfRejectedState(rejectionBefore, updatedSession, sent);
        Assert.Empty(spells);
        Assert.Equal(sobNpc.Life, sobNpcStates.GetVisible(sobNpc).Life);
        Assert.Contains("source CanAttackNpc.Verified rejected CaptureTheFlag base uid=9001", output);
        Assert.Contains(expectedDiagnostic, output);
        Assert.DoesNotContain("WorldServer handled legacy player-SOB magic", output);
    }

    [Fact]
    public async Task HandlerMagicAttackAgainstRuntimeDeadCaptureTheFlagBaseIsRejectedBeforeDamage()
    {
        var monsterFamily = CreateMonsterFamily();
        var sobNpc = CreateCaptureTheFlagBaseSobNpc();
        var worldData = CreateMagicCatalogWithSobNpcs(
            monsterFamily,
            [sobNpc],
            CreateMagicRow(ThunderSpellId, sort: 1, useMana: 7, damage: 120, coldTime: 0));
        var sessions = new WorldSessionStore();
        var sobNpcStates = new SobNpcRuntimeStateStore();
        var killed = sobNpcStates.ApplyDamage(
            sobNpc,
            requestedDamage: checked((uint)sobNpc.Life),
            now: new DateTimeOffset(2026, 6, 24, 0, 0, 0, TimeSpan.Zero));
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(
            mapId: sobNpc.MapId,
            x: sobNpc.X - 1,
            y: sobNpc.Y,
            attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["GuildID"] = "42",
                ["CaptureTheFlagBases"] = "9001,85,70,99,0,1",
            });
        sessions.Set(connection, session);
        var rejectionBefore = CaptureCtfRejectionState(session);
        Assert.True(sobNpcStates.TryCaptureMagicPrimaryTarget(sobNpc, out var initialObservation));
        Assert.NotNull(initialObservation);
        Assert.Same(sobNpc, initialObservation.Source);
        var initialView = initialObservation.Visible;
        Assert.NotSame(sobNpc, initialView);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions,
            SobNpcStates: sobNpcStates);

        Assert.Equal(50_000, sobNpc.Life);
        Assert.Equal(checked((uint)sobNpc.Life), killed.DamageApplied);
        Assert.True(killed.Killed);
        Assert.Equal(0U, killed.RemainingHitpoints);
        var runtimeLifeBeforeAttack = sobNpcStates.GetVisible(sobNpc).Life;
        Assert.Equal(0, runtimeLifeBeforeAttack);

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
        AssertCtfRejectedState(rejectionBefore, updatedSession, sent);
        Assert.True(sobNpcStates.TryCaptureMagicPrimaryTarget(sobNpc, out var canonical));
        Assert.NotNull(canonical);
        Assert.Same(sobNpc, canonical.Source);
        Assert.False(sobNpcStates.TryCaptureMagicPrimaryTarget(initialView, out _));
        Assert.Empty(ParseSpellAnimations(sent));
        Assert.Equal(50_000, sobNpc.Life);
        Assert.Equal(runtimeLifeBeforeAttack, sobNpcStates.GetVisible(sobNpc).Life);
        var output = log.ToString();
        Assert.Contains("source CanAttackNpc.Verified rejected dead SOB NPC", output);
        Assert.DoesNotContain("WorldServer handled legacy player-SOB magic", output);
    }

    // Detached component controls: no native/file runtime is fabricated here.
    private sealed record CtfRejectionState(WorldSession Owner, CharacterSnapshot Character,
        PlayerSpellSnapshotEntry[] Spells, KeyValuePair<ushort, DateTimeOffset>[]? Cooldowns, ushort RandomSpell);

    private static CtfRejectionState CaptureCtfRejectionState(WorldSession owner) => new(owner,
        owner.Character with
        {
            Attributes = new Dictionary<string, string>(owner.Character.Attributes, StringComparer.OrdinalIgnoreCase),
            PaidNobility = new Dictionary<string, string>(owner.Character.PaidNobility, StringComparer.OrdinalIgnoreCase),
        }, owner.PlayerSpells.Spells.ToArray(), owner.MagicCooldowns?.OrderBy(row => row.Key).ToArray(), owner.LegacyRandomSpell);

    private static void AssertCtfRejectedState(CtfRejectionState before, WorldSession after, IReadOnlyList<byte[]> sent)
    {
        Assert.Same(before.Owner, after);
        Assert.Equal(before.Character, after.Character with
        {
            Attributes = before.Character.Attributes,
            PaidNobility = before.Character.PaidNobility,
        });
        Assert.Equal(before.Character.Attributes.OrderBy(row => row.Key), after.Character.Attributes.OrderBy(row => row.Key));
        Assert.Equal(before.Character.PaidNobility.OrderBy(row => row.Key), after.Character.PaidNobility.OrderBy(row => row.Key));
        Assert.Equal(before.Spells, after.PlayerSpells.Spells.ToArray());
        Assert.Equal(before.Cooldowns, after.MagicCooldowns?.OrderBy(row => row.Key).ToArray());
        Assert.Equal(before.RandomSpell, after.LegacyRandomSpell);
        Assert.Equal(500, after.Character.MinMana);
        Assert.Empty(sent);
    }

    private static SobNpc CreateCaptureTheFlagBaseSobNpc()
    {
        return new SobNpc(
            Id: 9_001,
            Name: "CaptureTheFlagBase",
            Kind: LegacySobNpcEventRules.PoleKind,
            Lookface: 1_137,
            MapId: 2_057,
            X: 85,
            Y: 70,
            Life: 50_000,
            MaxLife: 50_000,
            Unknown9: 17,
            Unknown10: 0,
            LocalizedName: null,
            SourceLine: 7,
            RawLine: "9001,CaptureTheFlagBase,10,1137,2057,85,70,50000,50000,17,0",
            Fields: ["9001", "CaptureTheFlagBase", "10", "1137", "2057", "85", "70", "50000", "50000", "17", "0"]);
    }
}
