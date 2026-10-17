using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort IronShieldAttachStatusSpellId = 14_160;
    private const int IronShieldStatusFlag = 0xD9;

    [Fact]
    public async Task HandlerIronShield14160SelfCastAppliesStatusAzureShieldUpdateAndAnimation()
    {
        const uint duration = 35;
        const int damageOnMonster = 1_800;
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateMagicRow(
                IronShieldAttachStatusSpellId,
                sort: 6,
                level: 0,
                experience: 50,
                damageOnMonster: damageOnMonster,
                duration: duration,
                coldTime: 0),
            CreateMagicRow(
                IronShieldAttachStatusSpellId,
                sort: 6,
                level: 1,
                experience: 200,
                damageOnMonster: damageOnMonster,
                duration: duration,
                coldTime: 0));
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(spellIds: [IronShieldAttachStatusSpellId]);
        sessions.Set(connection, session);
        var context = new WorldPacketHandlerContext(
            connection,
            log,
            session,
            WorldData: worldData,
            Sessions: sessions);
        var beforeCast = DateTimeOffset.UtcNow;

        await PacketHandlers.HandleAsync(
            context,
            ReadFrame(new MsgAttackPacket(new MsgAttackQuery(
                Uid: PlayerUid,
                OpponentUid: PlayerUid,
                X: 847,
                Y: 592,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: IronShieldAttachStatusSpellId)).ToFrameBytes()));

        var afterCast = DateTimeOffset.UtcNow;
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.True(HasLegacyStatusFlag(updatedSession.Character.Attributes, IronShieldStatusFlag));
        Assert.Equal(damageOnMonster.ToString(), updatedSession.Character.Attributes["IronShieldDamage"]);
        Assert.Equal("true", updatedSession.Character.Attributes["LegacyIronShieldRemoveOnDead"]);
        AssertLegacyXpSkillExpiresAround(
            updatedSession.Character.Attributes,
            "LegacyIronShieldExpiresAtUtc",
            beforeCast,
            afterCast,
            checked((int)duration));
        var updatedSpell = Assert.Single(updatedSession.PlayerSpells.Spells);
        Assert.Equal(IronShieldAttachStatusSpellId, updatedSpell.Id);
        Assert.Equal(0, updatedSpell.Level);

        var sentSnapshot = sent.ToArray();
        var interactionIndex = SingleFrameIndex(
            sentSnapshot,
            frame => MsgAttackPacket.TryParse(frame, out var packet) &&
                packet.Query.AttackType == MsgAttackType.Magic &&
                packet.Query.SpellId == IronShieldAttachStatusSpellId);
        var statusIndex = SingleFrameIndex(
            sentSnapshot,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    IsLegacyStatusFlagSet(entry.Values, IronShieldStatusFlag)));
        var azureShieldIndex = SingleFrameIndex(
            sentSnapshot,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.AzureShield));
        var animationIndex = SingleFrameIndex(
            sentSnapshot,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var packet) &&
                packet.SpellId == IronShieldAttachStatusSpellId);
        Assert.True(interactionIndex < statusIndex);
        Assert.True(statusIndex < azureShieldIndex);
        Assert.True(azureShieldIndex < animationIndex);
        Assert.Empty(ParseShowEquipmentRefreshes(sentSnapshot));
        Assert.DoesNotContain(
            sentSnapshot,
            frame => LegacySpellPacket.TryParse(ReadFrame(frame), out _));

        var azureShieldEntry = Assert.Single(
            ParseMsgUpdates([sentSnapshot[azureShieldIndex]]).SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.AzureShield);
        Assert.Equal((uint)IronShieldStatusFlag, azureShieldEntry.Flag);
        Assert.Equal(duration, azureShieldEntry.Time);
        Assert.Equal((uint)damageOnMonster, azureShieldEntry.Damage);
        Assert.Equal(0U, azureShieldEntry.SpellLevel);

        var spell = Assert.Single(ParseSpellAnimations(sentSnapshot));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(PlayerUid, spell.AttackedUid);
        Assert.Equal(847, spell.X);
        Assert.Equal(592, spell.Y);
        Assert.Equal(IronShieldAttachStatusSpellId, spell.SpellId);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(PlayerUid, target.Uid);
        Assert.Equal(0U, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy IronShield AttachStatus magic", output);
        Assert.Contains("statusFlag=217", output);
        Assert.Contains("ironShieldDamage=1800", output);
        Assert.Contains("source=AttachStatus.cs case 14160", output);
        Assert.DoesNotContain("appliedExperience=500", output);
    }

    [Fact]
    public async Task RuntimeLoopExpiresIronShield14160FlagAndDamageAttribute()
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
            spellIds: [IronShieldAttachStatusSpellId],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(IronShieldStatusFlag),
                ["IronShieldDamage"] = "1800",
                ["LegacyIronShieldExpiresAtUtc"] = now.AddSeconds(-1).ToString("O"),
                ["LegacyIronShieldRemoveOnDead"] = "true",
            });
        SetHandlerSession(handler, connection, session);

        try
        {
            StartHandlerRuntimeUpdateLoop(handler, connection, session, cancel.Token);

            await WaitUntilAsync(
                () =>
                {
                    var current = AssertHandlerSession(handler, connection);
                    return !HasLegacyStatusFlag(current.Character.Attributes, IronShieldStatusFlag) &&
                        !current.Character.Attributes.ContainsKey("IronShieldDamage") &&
                        !current.Character.Attributes.ContainsKey("LegacyIronShieldExpiresAtUtc") &&
                        !current.Character.Attributes.ContainsKey("LegacyIronShieldRemoveOnDead");
                },
                TimeSpan.FromSeconds(1));

            var expiredSession = AssertHandlerSession(handler, connection);
            Assert.False(HasLegacyStatusFlag(expiredSession.Character.Attributes, IronShieldStatusFlag));
            Assert.False(expiredSession.Character.Attributes.ContainsKey("IronShieldDamage"));
            Assert.False(expiredSession.Character.Attributes.ContainsKey("LegacyIronShieldExpiresAtUtc"));
            Assert.False(expiredSession.Character.Attributes.ContainsKey("LegacyIronShieldRemoveOnDead"));
            Assert.Contains(ParseMsgUpdates(sent), update => update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                entry.Values is not null &&
                !IsLegacyStatusFlagSet(entry.Values, IronShieldStatusFlag)));
            Assert.Contains("WorldServer expired legacy IronShield flags", log.ToString());
        }
        finally
        {
            await cancel.CancelAsync();
        }
    }
}
