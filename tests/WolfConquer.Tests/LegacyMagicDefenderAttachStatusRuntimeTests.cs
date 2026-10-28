using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort MagicDefenderAttachStatusSpellId = 30_000;
    private const int MagicDefenderAzureShieldStatusFlag = 0x5D;
    private const uint MagicDefenderAzureShieldDefence = 50_000;

    [Fact]
    public async Task HandlerMagicDefender30000SelfCastStoresAzureShieldSourceFieldsAndSpecialDuration()
    {
        var worldData = CreateMagicDefenderCatalog();
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateMagicDefenderSession(stamina: 100);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions);
        using var combatSpellOwner = new LegacyCombatSpellTestOwner(context, ref session);
        context = combatSpellOwner.Context;
        var beforeCast = DateTimeOffset.UtcNow;

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: PlayerUid,
                X: 848,
                Y: 593,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: MagicDefenderAttachStatusSpellId)).ToFrameBytes()));

        var afterCast = DateTimeOffset.UtcNow;
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Equal(490, updatedSession.Character.MinMana);
        Assert.Equal("95", updatedSession.Character.Attributes["Stamina"]);
        AssertMagicDefenderApplied(updatedSession, beforeCast, afterCast);

        var updates = ParseMsgUpdates(sent);
        Assert.Contains(updates, update => update.Uid == PlayerUid &&
            update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                entry.Values is not null &&
                IsLegacyStatusFlagSet(entry.Values, MagicDefenderAzureShieldStatusFlag)));
        Assert.DoesNotContain(
            updates.SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.AzureShield);

        var spell = Assert.Single(ParseSpellAnimations(sent));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(0U, spell.AttackedUid);
        Assert.Equal(848, spell.X);
        Assert.Equal(593, spell.Y);
        Assert.Equal(MagicDefenderAttachStatusSpellId, spell.SpellId);
        Assert.Equal(2, spell.SpellLevel);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(PlayerUid, target.Uid);
        Assert.Equal(0U, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);

        var progressFrames = ParseLegacySpellPackets(sent);
        Assert.True(progressFrames.Count == 1, "Expected exactly one real spell progress frame. Actual handler log:\n" + log);
        var spellUpdate = Assert.Single(progressFrames);
        Assert.Equal(MagicDefenderAttachStatusSpellId, spellUpdate.Id);
        Assert.Equal(3, spellUpdate.Level);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy MagicDefender AttachStatus magic", output);
        Assert.Contains("statusFlag=93", output);
        Assert.Contains("azureShieldLevel=2", output);
        Assert.Contains("azureShieldDefence=50000", output);
        Assert.Contains("source=AttachStatus.cs case 30000", output);
    }

    [Fact]
    public async Task HandlerMagicDefender30000VisiblePlayerTargetStoresAzureShieldOnTargetOnly()
    {
        var worldData = CreateMagicDefenderCatalog();
        var sessions = new WorldSessionStore();
        var casterSent = new List<byte[]>();
        var targetSent = new List<byte[]>();
        using var log = new StringWriter();
        var casterConnection = CreateSendingConnection(casterSent);
        var targetConnection = CreateSendingConnection(targetSent);
        var casterSession = CreateMagicDefenderSession(stamina: 100);
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
        using var combatSpellOwner = new LegacyCombatSpellTestOwner(context, ref casterSession);
        context = combatSpellOwner.Context;
        var beforeCast = DateTimeOffset.UtcNow;

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: TargetPlayerUid,
                X: 832,
                Y: 584,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: MagicDefenderAttachStatusSpellId)).ToFrameBytes()));

        var afterCast = DateTimeOffset.UtcNow;
        Assert.True(sessions.TryGet(casterConnection, out var updatedCasterSession));
        Assert.NotNull(updatedCasterSession);
        Assert.Equal(490, updatedCasterSession.Character.MinMana);
        Assert.Equal("95", updatedCasterSession.Character.Attributes["Stamina"]);
        Assert.False(HasLegacyStatusFlag(updatedCasterSession.Character.Attributes, MagicDefenderAzureShieldStatusFlag));
        Assert.False(updatedCasterSession.Character.Attributes.ContainsKey("AzureShieldLevel"));
        Assert.False(updatedCasterSession.Character.Attributes.ContainsKey("AzureShieldDefence"));

        Assert.True(sessions.TryGet(targetConnection, out var updatedTargetSession));
        Assert.NotNull(updatedTargetSession);
        AssertMagicDefenderApplied(updatedTargetSession, beforeCast, afterCast);

        foreach (var spell in new[] { Assert.Single(ParseSpellAnimations(casterSent)), Assert.Single(ParseSpellAnimations(targetSent)) })
        {
            Assert.Equal(PlayerUid, spell.AttackerUid);
            Assert.Equal(0U, spell.AttackedUid);
            Assert.Equal(832, spell.X);
            Assert.Equal(584, spell.Y);
            Assert.Equal(MagicDefenderAttachStatusSpellId, spell.SpellId);
            var target = Assert.Single(spell.Targets);
            Assert.Equal(TargetPlayerUid, target.Uid);
            Assert.Equal(0U, target.Damage);
            Assert.True(target.Hit);
        }

        Assert.Contains(ParseMsgUpdates(casterSent), update => update.Uid == TargetPlayerUid &&
            update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                entry.Values is not null &&
                IsLegacyStatusFlagSet(entry.Values, MagicDefenderAzureShieldStatusFlag)));
        Assert.Contains(ParseMsgUpdates(targetSent), update => update.Uid == TargetPlayerUid &&
            update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                entry.Values is not null &&
                IsLegacyStatusFlagSet(entry.Values, MagicDefenderAzureShieldStatusFlag)));

        var spellUpdate = Assert.Single(ParseLegacySpellPackets(casterSent));
        Assert.Equal(MagicDefenderAttachStatusSpellId, spellUpdate.Id);
        Assert.Equal(3, spellUpdate.Level);
        Assert.Empty(ParseLegacySpellPackets(targetSent));

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy MagicDefender AttachStatus magic", output);
        Assert.Contains("targetUid=1293601", output);
        Assert.Contains("targetApplied=True", output);
    }

    [Fact]
    public async Task RuntimeLoopExpiresMagicDefender30000AzureShieldAndSourceFields()
    {
        using var log = new StringWriter();
        using var cancel = new CancellationTokenSource();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var now = DateTimeOffset.UtcNow;
        var handler = new WorldServerFrameHandler(
            snapshotLoader: null,
            handoffService: null,
            worldData: null,
            runtimeUpdateLoop: new LegacyRuntimeUpdateLoopOptions(
                Enabled: true,
                TimeSpan.FromMilliseconds(10),
                new AnchoredRuntimeTimeProvider(now.AddSeconds(10))),
            log: log);
        var session = CreateSession(
            spellIds: [MagicDefenderAttachStatusSpellId],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(MagicDefenderAzureShieldStatusFlag),
                ["AzureShieldLevel"] = "2",
                ["AzureShieldDefence"] = MagicDefenderAzureShieldDefence.ToString(),
                ["LegacyMagicDefenderExpiresAtUtc"] = now.AddSeconds(-1).ToString("O"),
                ["LegacyMagicDefenderRemoveOnDead"] = "true",
            });
        SetHandlerSession(handler, connection, session);

        try
        {
            StartHandlerRuntimeUpdateLoop(handler, connection, session, cancel.Token);

            await WaitUntilAsync(
                () =>
                {
                    var current = AssertHandlerSession(handler, connection);
                    return !HasLegacyStatusFlag(current.Character.Attributes, MagicDefenderAzureShieldStatusFlag) &&
                        !current.Character.Attributes.ContainsKey("AzureShieldLevel") &&
                        !current.Character.Attributes.ContainsKey("AzureShieldDefence") &&
                        !current.Character.Attributes.ContainsKey("LegacyMagicDefenderExpiresAtUtc") &&
                        !current.Character.Attributes.ContainsKey("LegacyMagicDefenderRemoveOnDead");
                },
                TimeSpan.FromSeconds(1));

            var expiredSession = AssertHandlerSession(handler, connection);
            Assert.False(HasLegacyStatusFlag(expiredSession.Character.Attributes, MagicDefenderAzureShieldStatusFlag));
            Assert.False(expiredSession.Character.Attributes.ContainsKey("AzureShieldLevel"));
            Assert.False(expiredSession.Character.Attributes.ContainsKey("AzureShieldDefence"));
            Assert.Contains(ParseMsgUpdates(sent), update => update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                entry.Values is not null &&
                !IsLegacyStatusFlagSet(entry.Values, MagicDefenderAzureShieldStatusFlag)));
            Assert.Contains("WorldServer expired legacy MagicDefender AzureShield flags", log.ToString());
        }
        finally
        {
            await cancel.CancelAsync();
        }
    }

    private static WorldDataCatalog CreateMagicDefenderCatalog()
    {
        return CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateMagicRow(
                MagicDefenderAttachStatusSpellId,
                sort: 6,
                level: 0,
                experience: 50,
                duration: 90,
                coldTime: 0),
            CreateMagicRow(
                MagicDefenderAttachStatusSpellId,
                sort: 6,
                level: 1,
                experience: 50,
                duration: 90,
                coldTime: 0),
            CreateMagicRow(
                MagicDefenderAttachStatusSpellId,
                sort: 6,
                level: 2,
                useMana: 10,
                experience: 50,
                useStamina: 5,
                duration: 90,
                coldTime: 0),
            CreateMagicRow(
                MagicDefenderAttachStatusSpellId,
                sort: 6,
                level: 3,
                experience: 200,
                duration: 90,
                coldTime: 0));
    }

    private static WorldSession CreateMagicDefenderSession(ulong stamina)
    {
        return CreateSession(
            stamina: stamina,
            spellEntries:
            [
                new PlayerSpellSnapshotEntry(
                    MagicDefenderAttachStatusSpellId,
                    Level: 2,
                    Experience: 0,
                    PreviousLevel: 0,
                    SoulLevel: 0,
                    UseSpellSoul: 0)
            ]);
    }

    private static void AssertMagicDefenderApplied(
        WorldSession session,
        DateTimeOffset beforeCast,
        DateTimeOffset afterCast)
    {
        Assert.True(HasLegacyStatusFlag(session.Character.Attributes, MagicDefenderAzureShieldStatusFlag));
        Assert.Equal("2", session.Character.Attributes["AzureShieldLevel"]);
        Assert.Equal(MagicDefenderAzureShieldDefence.ToString(), session.Character.Attributes["AzureShieldDefence"]);
        Assert.Equal("true", session.Character.Attributes["LegacyMagicDefenderRemoveOnDead"]);
        AssertLegacyXpSkillExpiresAround(
            session.Character.Attributes,
            "LegacyMagicDefenderExpiresAtUtc",
            beforeCast,
            afterCast,
            30);
    }

    private static IReadOnlyList<LegacySpellPacket> ParseLegacySpellPackets(IEnumerable<byte[]> frames)
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
