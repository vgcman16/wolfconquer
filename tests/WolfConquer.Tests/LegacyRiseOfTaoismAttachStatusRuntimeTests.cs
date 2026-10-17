using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const ushort RiseOfTaoismAttachStatusSpellId = 15_440;

    [Fact]
    public async Task HandlerRiseOfTaoism15440SelfCastAppliesStatusFineRainAnimationAndEquipment()
    {
        const uint duration = 35;
        const uint extraMagicDamage = 27;
        var worldData = CreateMagicCatalog(
            CreateMonsterFamily(),
            [],
            CreateMagicRow(
                RiseOfTaoismAttachStatusSpellId,
                sort: 6,
                level: 0,
                experience: 50,
                damage: extraMagicDamage,
                duration: duration,
                coldTime: 0),
            CreateMagicRow(
                RiseOfTaoismAttachStatusSpellId,
                sort: 6,
                level: 1,
                experience: 200,
                damage: extraMagicDamage,
                duration: duration,
                coldTime: 0));
        var sessions = new WorldSessionStore();
        var sent = new List<byte[]>();
        using var log = new StringWriter();
        var connection = CreateSendingConnection(sent);
        var session = CreateSession(spellIds: [RiseOfTaoismAttachStatusSpellId]);
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
                X: 845,
                Y: 590,
                AttackType: MsgAttackType.Magic,
                Damage: 0,
                KilledMonster: 0,
                ResponseDamage: 0,
                Effect: MsgAttackEffect.None,
                Unknown: 0,
                SpellId: RiseOfTaoismAttachStatusSpellId)).ToFrameBytes()));

        var afterCast = DateTimeOffset.UtcNow;
        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.True(HasLegacyStatusFlag(updatedSession.Character.Attributes, LegacyRiseOfTaoismFlag));
        Assert.Equal(extraMagicDamage.ToString(), updatedSession.Character.Attributes["RiseofTaoismExtraMDamage"]);
        Assert.Equal("true", updatedSession.Character.Attributes["LegacyRiseOfTaoismRemoveOnDead"]);
        AssertLegacyXpSkillExpiresAround(
            updatedSession.Character.Attributes,
            "LegacyRiseOfTaoismExpiresAtUtc",
            beforeCast,
            afterCast,
            checked((int)duration));
        var updatedSpell = Assert.Single(updatedSession.PlayerSpells.Spells);
        Assert.Equal(RiseOfTaoismAttachStatusSpellId, updatedSpell.Id);
        Assert.Equal(0, updatedSpell.Level);

        var sentSnapshot = sent.ToArray();
        var interactionIndex = SingleFrameIndex(
            sentSnapshot,
            frame => MsgAttackPacket.TryParse(frame, out var packet) &&
                packet.Query.AttackType == MsgAttackType.Magic &&
                packet.Query.SpellId == RiseOfTaoismAttachStatusSpellId);
        var statusIndex = SingleFrameIndex(
            sentSnapshot,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                update.Updates.Any(entry =>
                    entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                    entry.Values is not null &&
                    IsLegacyStatusFlagSet(entry.Values, LegacyRiseOfTaoismFlag)));
        var fineRainIndex = SingleFrameIndex(
            sentSnapshot,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.FineRain));
        var animationIndex = SingleFrameIndex(
            sentSnapshot,
            frame => LegacySpellAnimationPacket.TryParse(frame, out var packet) &&
                packet.SpellId == RiseOfTaoismAttachStatusSpellId);
        var equipmentIndex = SingleFrameIndex(
            sentSnapshot,
            frame => LegacyShowEquipmentPacket.TryParse(frame, out _));
        Assert.True(interactionIndex < statusIndex);
        Assert.True(statusIndex < fineRainIndex);
        Assert.True(fineRainIndex < animationIndex);
        Assert.True(animationIndex < equipmentIndex);
        Assert.DoesNotContain(
            sentSnapshot,
            frame => LegacyMsgUpdatePacket.TryParse(ReadFrame(frame), out var update) &&
                update.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.ArchiveSkill));
        Assert.DoesNotContain(
            sentSnapshot,
            frame => LegacySpellPacket.TryParse(ReadFrame(frame), out _));

        var fineRainEntry = Assert.Single(
            ParseMsgUpdates([sentSnapshot[fineRainIndex]]).SelectMany(update => update.Updates),
            entry => entry.Type == LegacyMsgUpdateDataType.FineRain);
        Assert.Equal((uint)LegacyRiseOfTaoismFlag, fineRainEntry.Flag);
        Assert.Equal(duration, fineRainEntry.Time);
        Assert.Equal(50_000U, fineRainEntry.Damage);
        Assert.Equal(0U, fineRainEntry.SpellLevel);

        var spell = Assert.Single(ParseSpellAnimations(sentSnapshot));
        Assert.Equal(PlayerUid, spell.AttackerUid);
        Assert.Equal(PlayerUid, spell.AttackedUid);
        Assert.Equal(845, spell.X);
        Assert.Equal(590, spell.Y);
        Assert.Equal(RiseOfTaoismAttachStatusSpellId, spell.SpellId);
        var target = Assert.Single(spell.Targets);
        Assert.Equal(PlayerUid, target.Uid);
        Assert.Equal(0U, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);

        var equipmentRefresh = Assert.Single(ParseShowEquipmentRefreshes(sentSnapshot));
        Assert.Equal(LegacyShowEquipmentPacket.ShowAction, equipmentRefresh.Action);

        var output = log.ToString();
        Assert.Contains("WorldServer handled legacy RiseofTaoism AttachStatus magic", output);
        Assert.Contains("statusFlag=227", output);
        Assert.Contains("fineRainDamage=50000", output);
        Assert.Contains("source=AttachStatus.cs case 15440", output);
        Assert.DoesNotContain("appliedExperience=500", output);
    }

    [Fact]
    public async Task RuntimeLoopExpiresRiseOfTaoism15440FlagAndDamageAttribute()
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
            spellIds: [RiseOfTaoismAttachStatusSpellId],
            attributes: new Dictionary<string, string>
            {
                ["StatusFlags"] = BuildStatusFlags(LegacyRiseOfTaoismFlag),
                ["RiseofTaoismExtraMDamage"] = "27",
                ["LegacyRiseOfTaoismExpiresAtUtc"] = now.AddSeconds(-1).ToString("O"),
                ["LegacyRiseOfTaoismRemoveOnDead"] = "true",
            });
        SetHandlerSession(handler, connection, session);

        try
        {
            StartHandlerRuntimeUpdateLoop(handler, connection, session, cancel.Token);

            await WaitUntilAsync(
                () =>
                {
                    var current = AssertHandlerSession(handler, connection);
                    return !HasLegacyStatusFlag(current.Character.Attributes, LegacyRiseOfTaoismFlag) &&
                        !current.Character.Attributes.ContainsKey("RiseofTaoismExtraMDamage") &&
                        !current.Character.Attributes.ContainsKey("LegacyRiseOfTaoismExpiresAtUtc") &&
                        !current.Character.Attributes.ContainsKey("LegacyRiseOfTaoismRemoveOnDead");
                },
                TimeSpan.FromSeconds(1));

            var expiredSession = AssertHandlerSession(handler, connection);
            Assert.False(HasLegacyStatusFlag(expiredSession.Character.Attributes, LegacyRiseOfTaoismFlag));
            Assert.False(expiredSession.Character.Attributes.ContainsKey("RiseofTaoismExtraMDamage"));
            Assert.False(expiredSession.Character.Attributes.ContainsKey("LegacyRiseOfTaoismExpiresAtUtc"));
            Assert.False(expiredSession.Character.Attributes.ContainsKey("LegacyRiseOfTaoismRemoveOnDead"));
            Assert.Contains(ParseMsgUpdates(sent), update => update.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                entry.Values is not null &&
                !IsLegacyStatusFlagSet(entry.Values, LegacyRiseOfTaoismFlag)));
            Assert.Contains("WorldServer expired legacy RiseofTaoism flags", log.ToString());
        }
        finally
        {
            await cancel.CancelAsync();
        }
    }
}
