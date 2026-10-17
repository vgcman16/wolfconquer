using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyPromotionMastersTaoistPureSkillTests : IDisposable
{
    private const uint TaoistPromotionNpcId = 10_000;
    private const long CharacterUid = 1_293_740;
    private const ushort TaoistAvatarId = 6;
    private const string PromotionGateText =
        "Sorry. I can`t teach you this skill until you reach level 40 and get promoted.";

    private static readonly PureSkillCase AzureShield = new(
        OptionId: 51,
        SpellId: 10_312,
        SpellName: "AzureShield",
        SourceRange: "NpcHandler.cs:41657-41696",
        CurrentClass: 13_002,
        FirstClass: 13_002,
        SecondClass: 13_005,
        WrongCurrentClass: 14_002,
        WrongComboClass: 14_002,
        RebornGateText: "Only pure Fire Taoists, who have trained as Water Taoists for all 3 of their lives, can learn the Pure Skill, AzureShield.",
        ComboGateText: "Only pure water Taoists, who have trained as Water Taoists for all 3 of their lives, can learn the Pure Skill, AzureShield.",
        ComboGateReason: "source pure Water Taoist combo gate rejected.");

    private static readonly PureSkillCase HeavenBlade = new(
        OptionId: 52,
        SpellId: 10_310,
        SpellName: "HeavenBlade",
        SourceRange: "NpcHandler.cs:41729-41768",
        CurrentClass: 14_002,
        FirstClass: 14_002,
        SecondClass: 14_005,
        WrongCurrentClass: 13_002,
        WrongComboClass: 13_002,
        RebornGateText: "Only pure Fire Taoists, who have trained as Fire Taoists for all 3 of their lives, can learn the Pure Skill, Heaven Blade.",
        ComboGateText: "Only pure Fire Taoists, who have trained as Fire Taoists for all 3 of their lives, can learn the Pure Skill, Heaven Blade.",
        ComboGateReason: "source pure Fire Taoist combo gate rejected.");

    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "WolfConquer-LegacyPromotionMastersTaoistPureSkillTests-" + Guid.NewGuid().ToString("N"));

    public static TheoryData<PureSkillCase> TaoistPureSkills => new()
    {
        AzureShield,
        HeavenBlade,
    };

    [Theory]
    [MemberData(nameof(TaoistPureSkills))]
    public async Task TaoistPureSkillOptionLearnsSpellAndPersists(PureSkillCase skill)
    {
        var result = await InvokeTaoistPromotionAsync(skill);

        Assert.True(result.Handled);
        Assert.True(result.Sessions.TryGet(result.Connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal([(uint)skill.SpellId], updated.PlayerSpells.SpellIds.ToArray());
        AssertPersistedSpells(result.SpellsRoot, [(uint)skill.SpellId]);

        var spellPacket = Assert.Single(ParseSpellPackets(result.Sent));
        Assert.Equal(skill.SpellId, spellPacket.Id);
        AssertDialog(
            ParseNpcReplies(result.Sent),
            "Congratulation! You just have learned the skill " + skill.SpellName,
            "Ok , thank you.");

        Assert.Contains("WorldServer handled LegacyNpc PromotionTaoist pure skill", result.Log);
        Assert.Contains("source=NpcHandler.PromotionTaoist", result.Log);
        Assert.Contains("sourceRange=" + skill.SourceRange, result.Log);
        Assert.Contains("option=" + skill.OptionId, result.Log);
        Assert.Contains("spellId=" + skill.SpellId, result.Log);
        Assert.DoesNotContain("PARITY-GAP", result.Log);
    }

    [Theory]
    [MemberData(nameof(TaoistPureSkills))]
    public async Task TaoistPureSkillOptionRejectsBeforeSecondRebirth(PureSkillCase skill)
    {
        var result = await InvokeTaoistPromotionAsync(skill, reborn: 1);

        Assert.True(result.Handled);
        Assert.Empty(ParseSpellPackets(result.Sent));
        AssertPersistedSpells(result.SpellsRoot, []);
        AssertDialog(ParseNpcReplies(result.Sent), skill.RebornGateText, "Ok~I~see.");
        Assert.Contains("sourceRange=" + skill.SourceRange, result.Log);
        Assert.Contains("source reborn gate rejected", result.Log);
    }

    [Theory]
    [MemberData(nameof(TaoistPureSkills))]
    public async Task TaoistPureSkillOptionRejectsNonPureClassCombo(PureSkillCase skill)
    {
        var result = await InvokeTaoistPromotionAsync(skill, firstClass: skill.WrongComboClass);

        Assert.True(result.Handled);
        Assert.Empty(ParseSpellPackets(result.Sent));
        AssertPersistedSpells(result.SpellsRoot, []);
        AssertDialog(ParseNpcReplies(result.Sent), skill.ComboGateText, "Ok~I~see.");
        Assert.Contains("sourceRange=" + skill.SourceRange, result.Log);
        Assert.Contains(skill.ComboGateReason, result.Log);
    }

    [Theory]
    [MemberData(nameof(TaoistPureSkills))]
    public async Task TaoistPureSkillOptionRejectsBelowLevelGate(PureSkillCase skill)
    {
        var result = await InvokeTaoistPromotionAsync(skill, level: 39);

        Assert.True(result.Handled);
        Assert.Empty(ParseSpellPackets(result.Sent));
        AssertPersistedSpells(result.SpellsRoot, []);
        AssertDialog(ParseNpcReplies(result.Sent), PromotionGateText, "Okay.~I~see..");
        Assert.Contains("sourceRange=" + skill.SourceRange, result.Log);
        Assert.Contains("source level gate rejected", result.Log);
    }

    [Theory]
    [MemberData(nameof(TaoistPureSkills))]
    public async Task TaoistPureSkillOptionAlreadyLearnedDoesNotDuplicate(PureSkillCase skill)
    {
        var learned = new PlayerSpellSnapshot([Entry(skill.SpellId)]);
        var result = await InvokeTaoistPromotionAsync(
            skill,
            playerSpells: learned,
            caseName: "AlreadyLearned-" + skill.SpellName);

        Assert.True(result.Handled);
        Assert.Empty(ParseSpellPackets(result.Sent));
        Assert.True(result.Sessions.TryGet(result.Connection, out var updated));
        Assert.NotNull(updated);
        Assert.Equal([(uint)skill.SpellId], updated.PlayerSpells.SpellIds.ToArray());
        AssertPersistedSpells(result.SpellsRoot, [(uint)skill.SpellId]);
        AssertDialog(ParseNpcReplies(result.Sent), "You`ve learned " + skill.SpellName + "!", "Okay.");
        Assert.Contains("sourceRange=" + skill.SourceRange, result.Log);
        Assert.Contains("spellId=" + skill.SpellId + "; already learned", result.Log);
    }

    [Theory]
    [MemberData(nameof(TaoistPureSkills))]
    public async Task TaoistPureSkillOptionWrongCurrentBranchSilentlyHandled(PureSkillCase skill)
    {
        var result = await InvokeTaoistPromotionAsync(skill, classCode: skill.WrongCurrentClass);

        Assert.True(result.Handled);
        Assert.Empty(result.Sent);
        AssertPersistedSpells(result.SpellsRoot, []);
        Assert.True(result.Sessions.TryGet(result.Connection, out var updated));
        Assert.NotNull(updated);
        Assert.Empty(updated.PlayerSpells.SpellIds);
        Assert.Contains("sourceRange=" + skill.SourceRange, result.Log);
        Assert.Contains("source current class gate broke without dialog", result.Log);
        Assert.DoesNotContain("PARITY-GAP", result.Log);
        Assert.DoesNotContain("mutation unavailable", result.Log);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private async Task<TaoistPureSkillInvokeResult> InvokeTaoistPromotionAsync(
        PureSkillCase skill,
        long? classCode = null,
        long? firstClass = null,
        long? secondClass = null,
        long reborn = 2,
        long level = 40,
        PlayerSpellSnapshot? playerSpells = null,
        string? caseName = null)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var connection = CreateSendingConnection(sent);
        var spellsRoot = Path.Combine(
            _root,
            caseName ?? skill.SpellName + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(spellsRoot);

        var initialSpells = playerSpells ?? PlayerSpellSnapshot.Empty;
        new PlayerSpellSnapshotLoader(spellsRoot).SaveByCharacterUid(CharacterUid, initialSpells);

        var session = CreatePureTaoistSession(
            classCode ?? skill.CurrentClass,
            firstClass ?? skill.FirstClass,
            secondClass ?? skill.SecondClass,
            reborn,
            level,
            initialSpells);
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyPromotionMastersNpcAsync(
            new WorldPacketHandlerContext(
                connection,
                log,
                Session: session,
                Sessions: sessions,
                PlayerSpellsRoot: spellsRoot),
            session,
            TaoistPromotionNpcId,
            skill.OptionId,
            CancellationToken.None);

        return new TaoistPureSkillInvokeResult(
            handled,
            sent,
            connection,
            sessions,
            spellsRoot,
            log.ToString());
    }

    private static WorldSession CreatePureTaoistSession(
        long classCode,
        long firstClass,
        long secondClass,
        long reborn,
        long level,
        PlayerSpellSnapshot playerSpells)
    {
        return new WorldSession(
            AccountId: 100,
            Username: "taoist-pure-skill-test",
            Character: new CharacterSnapshot(
                Uid: CharacterUid,
                Name: "PureTaoist",
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: classCode,
                FirstClass: firstClass,
                SecondClass: secondClass,
                Reborn: reborn,
                Level: level,
                Experience: 0,
                MapId: 1_002,
                X: 430,
                Y: 378,
                PreviousMapId: 1_002,
                PreviousMapX: 430,
                PreviousMapY: 378,
                Strength: 0,
                Agility: 0,
                Vitality: 0,
                Spirit: 0,
                AttributePoints: 0,
                MinHitPoints: 1,
                MinMana: 0,
                Money: 0,
                ConquerPoints: 0,
                BoundConquerPoints: 0,
                BankConquerPoints: 0,
                PkPoints: 0,
                Online: 0,
                SourceFile: "fixture",
                Attributes: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: playerSpells,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }

    private static PlayerSpellSnapshotEntry Entry(ushort id)
    {
        return new PlayerSpellSnapshotEntry(
            id,
            Level: 0,
            Experience: 0,
            PreviousLevel: 0,
            SoulLevel: 0,
            UseSpellSoul: 0);
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        });
    }

    private static IReadOnlyList<LegacyNpcReplyPacket> ParseNpcReplies(IEnumerable<byte[]> sent)
    {
        var packets = new List<LegacyNpcReplyPacket>();
        foreach (var bytes in sent)
        {
            if (LegacyNpcReplyPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static IReadOnlyList<LegacySpellPacket> ParseSpellPackets(IEnumerable<byte[]> sent)
    {
        var packets = new List<LegacySpellPacket>();
        foreach (var bytes in sent)
        {
            if (LegacySpellPacket.TryParse(ReadFrame(bytes), out var packet))
            {
                packets.Add(packet);
            }
        }

        return packets;
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static void AssertDialog(
        IReadOnlyList<LegacyNpcReplyPacket> replies,
        string expectedText,
        string expectedOption)
    {
        Assert.Contains(replies, reply =>
            reply.InteractType == LegacyNpcReplyInteractType.Dialog &&
            reply.Text == expectedText);
        Assert.Contains(replies, reply =>
            reply.InteractType == LegacyNpcReplyInteractType.Option &&
            reply.OptionId == LegacyNpcReplyPacket.ClosedOptionId &&
            reply.Text == expectedOption);
        Assert.Contains(replies, reply =>
            reply.InteractType == LegacyNpcReplyInteractType.Avatar &&
            reply.InputMaxLength == TaoistAvatarId);
    }

    private static void AssertPersistedSpells(string spellsRoot, IReadOnlyList<uint> expectedSpellIds)
    {
        Assert.Equal(expectedSpellIds, new PlayerSpellSnapshotLoader(spellsRoot)
            .LoadByCharacterUid(CharacterUid)
            .SpellIds
            .ToArray());
    }

    public sealed record PureSkillCase(
        byte OptionId,
        ushort SpellId,
        string SpellName,
        string SourceRange,
        long CurrentClass,
        long FirstClass,
        long SecondClass,
        long WrongCurrentClass,
        long WrongComboClass,
        string RebornGateText,
        string ComboGateText,
        string ComboGateReason);

    private sealed record TaoistPureSkillInvokeResult(
        bool Handled,
        IReadOnlyList<byte[]> Sent,
        ConquerTcpConnection Connection,
        WorldSessionStore Sessions,
        string SpellsRoot,
        string Log);
}
