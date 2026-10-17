using System.Buffers;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyDragonWarRuntimeStateStoreTests
{
    private const int LegacyDragonWarMapId = 700;
    private const int LegacyDragonSwingFlag = 0xA0;
    private const uint LegacyYellowColor = 0x00FFFF00;
    private const uint LegacyCenterChatType = 2_011;
    private const uint FirstRightCornerChatType = 2_108;
    private const uint ContinueRightCornerChatType = 2_109;
    private const string DragonWarInvitationText = "DragonWar[SS/FB]";
    private const string DragonWarInvitationCenterText = " " + DragonWarInvitationText;

    [Fact]
    public void TryJoinOnlyAcceptsIdleProcess()
    {
        var store = new LegacyDragonWarRuntimeStateStore();
        var session = CreateSession();

        Assert.False(store.TryJoin(session, out var closedUpdate));
        Assert.Equal(default, closedUpdate);

        store.SetProcess(LegacyDragonWarProcess.Alive);

        Assert.False(store.TryJoin(session, out var aliveUpdate));
        Assert.Equal(default, aliveUpdate);

        store.SetProcess(LegacyDragonWarProcess.Dead);
        store.Open(new DateTimeOffset(2026, 6, 25, 19, 0, 0, TimeSpan.Zero));

        Assert.True(store.TryJoin(session, out var idleUpdate));
        Assert.Equal(LegacyDragonWarMapId, idleUpdate.Character.MapId);
    }

    [Fact]
    public void OpenFromDeadExposesDragonWarInvitationMetadata()
    {
        var store = new LegacyDragonWarRuntimeStateStore();

        var update = store.Open(new DateTimeOffset(2026, 6, 25, 19, 0, 0, TimeSpan.Zero));

        Assert.NotNull(update);
        var prompt = Assert.IsType<LegacyRuntimeMessageBoxPrompt>(update.MessageBoxPrompt);
        Assert.Equal(LegacyRuntimeMessageBoxPrompt.DragonWarInvitationKey, prompt.PendingKey);
        Assert.Equal(DragonWarInvitationText, prompt.Text);
        Assert.Equal((ushort)1002, prompt.MapId);
        Assert.Equal((ushort)319, prompt.X);
        Assert.Equal((ushort)256, prompt.Y);
        Assert.Equal((ushort)0, prompt.DynamicId);
        Assert.Equal(60, prompt.Seconds);

        var frame = ReadFrame(update.FrameBytes);
        Assert.True(LegacyMsgMessagePacket.TryParse(frame, out var message));
        Assert.Equal((ushort)2179, frame.Header.Opcode);
        Assert.Equal(LegacyYellowColor, message.Color);
        Assert.Equal(LegacyCenterChatType, message.ChatType);
        Assert.Equal(
        [
            "SYSTEM",
            "ALL",
            string.Empty,
            DragonWarInvitationCenterText,
            string.Empty,
            string.Empty,
            string.Empty
        ], message.Content);
    }

    [Fact]
    public void OpenWhileIdleDoesNotEmitSecondDragonWarInvitation()
    {
        var store = new LegacyDragonWarRuntimeStateStore();
        var now = new DateTimeOffset(2026, 6, 25, 19, 0, 0, TimeSpan.Zero);

        Assert.NotNull(store.Open(now));

        var duplicate = store.Open(now.AddSeconds(10));

        Assert.Null(duplicate);
    }

    [Fact]
    public void TryJoinResetsScoreHitsTeleportsAndRemovesDragonSwing()
    {
        var store = new LegacyDragonWarRuntimeStateStore();
        var session = CreateSession(
            attributes: new Dictionary<string, string>
            {
                ["DragonWarScore"] = "95",
                ["DragonWarHits"] = "4",
                ["StatusFlags"] = BuildStatusFlags(LegacyDragonSwingFlag, 0x93),
            });
        store.Open(new DateTimeOffset(2026, 6, 25, 19, 0, 0, TimeSpan.Zero));

        Assert.True(store.TryJoin(session, out var updated));

        Assert.Equal("0", updated.Character.Attributes["DragonWarScore"]);
        Assert.Equal("0", updated.Character.Attributes["DragonWarHits"]);
        Assert.Equal(LegacyDragonWarMapId, updated.Character.MapId);
        Assert.Equal(session.Character.MapId, updated.Character.PreviousMapId);
        Assert.Equal(session.Character.X, updated.Character.PreviousMapX);
        Assert.Equal(session.Character.Y, updated.Character.PreviousMapY);
        Assert.True(updated.Character.Attributes.TryGetValue("DynamicID", out var dynamicId));
        Assert.NotEqual("0", dynamicId);
        Assert.False(HasLegacyStatusFlag(updated.Character.Attributes, LegacyDragonSwingFlag));
        Assert.True(HasLegacyStatusFlag(updated.Character.Attributes, 0x93));
    }

    [Fact]
    public void CreateDueUpdatesBuildsLegacyCheckUpScoreboardForEachPlayer()
    {
        var store = new LegacyDragonWarRuntimeStateStore();
        var now = new DateTimeOffset(2026, 6, 25, 19, 0, 0, TimeSpan.Zero);
        var sessions = new[]
        {
            CreateSession(uid: 1, name: "Alpha", attributes: DragonWarAttributes(score: 40, hits: 2, kills: 4)),
            CreateSession(uid: 2, name: "Bravo", attributes: DragonWarAttributes(score: 70, hits: 5, kills: 7)),
            CreateSession(uid: 3, name: "Charlie", attributes: DragonWarAttributes(score: 30, hits: 1, kills: 3)),
            CreateSession(uid: 4, name: "Delta", attributes: DragonWarAttributes(score: 50, hits: 0, kills: 5)),
            CreateSession(uid: 5, name: "Echo", attributes: DragonWarAttributes(score: 10, hits: 7, kills: 1)),
        };
        store.Open(now);

        var joinedSessions = new List<WorldSession>(sessions.Length);
        foreach (var session in sessions)
        {
            Assert.True(store.TryJoin(session, out var joined));
            var attributes = new Dictionary<string, string>(
                joined.Character.Attributes,
                StringComparer.OrdinalIgnoreCase)
            {
                ["DragonWarScore"] = session.Character.Attributes["DragonWarScore"],
                ["DragonWarHits"] = session.Character.Attributes["DragonWarHits"],
                ["TournamentKills"] = session.Character.Attributes["TournamentKills"],
            };
            joinedSessions.Add(joined with { Character = joined.Character with { Attributes = attributes } });
        }

        store.SetProcess(LegacyDragonWarProcess.Alive);

        var updates = store.CreateDueUpdates(now, joinedSessions).ToList();

        foreach (var session in joinedSessions)
        {
            var playerUpdates = updates.Where(update => update.RecipientUid == session.Character.Uid).ToList();
            Assert.Contains(
                playerUpdates,
                update => update.ChatType == FirstRightCornerChatType &&
                    update.Message == "DragonWar Score: ");
            Assert.Contains(
                playerUpdates,
                update => update.ChatType == ContinueRightCornerChatType &&
                    update.Message == "No 1. Bravo (70)");
            Assert.Contains(
                playerUpdates,
                update => update.ChatType == ContinueRightCornerChatType &&
                    update.Message == "No 2. Delta (50)");
            Assert.Contains(
                playerUpdates,
                update => update.ChatType == ContinueRightCornerChatType &&
                    update.Message == "No 3. Alpha (40)");
            Assert.Contains(
                playerUpdates,
                update => update.ChatType == ContinueRightCornerChatType &&
                    update.Message == "No 4. Charlie (30)");
            Assert.DoesNotContain(
                playerUpdates,
                update => update.ChatType == ContinueRightCornerChatType &&
                    update.Message.Contains("Echo", StringComparison.Ordinal));
            Assert.Contains(
                playerUpdates,
                update => update.ChatType == ContinueRightCornerChatType &&
                    update.Message == $"My tournament Kills: {session.Character.Attributes["TournamentKills"]}");
        }
    }

    private static Dictionary<string, string> DragonWarAttributes(int score, int hits, int kills)
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["DragonWarScore"] = score.ToString(),
            ["DragonWarHits"] = hits.ToString(),
            ["TournamentKills"] = kills.ToString(),
        };
    }

    private static WorldSession CreateSession(
        long uid = 1_293_600,
        string name = "HeroOne",
        IReadOnlyDictionary<string, string>? attributes = null)
    {
        return new WorldSession(
            AccountId: uid,
            Username: name,
            Character: CreateSnapshot(uid, name, attributes ?? new Dictionary<string, string>()),
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }

    private static CharacterSnapshot CreateSnapshot(
        long uid,
        string name,
        IReadOnlyDictionary<string, string> attributes)
    {
        return new CharacterSnapshot(
            Uid: uid,
            Name: name,
            Body: 1003,
            Face: 0,
            Hair: 0,
            Class: 7051,
            FirstClass: 0,
            SecondClass: 0,
            Reborn: 0,
            Level: 140,
            Experience: 0,
            MapId: 1002,
            X: 705,
            Y: 577,
            PreviousMapId: 1002,
            PreviousMapX: 705,
            PreviousMapY: 577,
            Strength: 0,
            Agility: 0,
            Vitality: 0,
            Spirit: 0,
            AttributePoints: 0,
            MinHitPoints: 61_229,
            MinMana: 0,
            Money: 0,
            ConquerPoints: 0,
            BoundConquerPoints: 0,
            BankConquerPoints: 0,
            PkPoints: 0,
            Online: 0,
            SourceFile: "",
            Attributes: new Dictionary<string, string>(attributes, StringComparer.OrdinalIgnoreCase),
            PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));
    }

    private static string BuildStatusFlags(params int[] flags)
    {
        var values = new ulong[4];
        foreach (var flag in flags)
        {
            var offset = flag / 64;
            if ((uint)offset >= values.Length)
            {
                continue;
            }

            values[offset] |= 1UL << (flag % 64);
        }

        return string.Join(',', values);
    }

    private static bool HasLegacyStatusFlag(IReadOnlyDictionary<string, string> attributes, int flag)
    {
        return attributes.TryGetValue("StatusFlags", out var raw) &&
            HasLegacyStatusFlag(raw, flag);
    }

    private static bool HasLegacyStatusFlag(string statusFlags, int flag)
    {
        var parts = statusFlags.Split(',', StringSplitOptions.TrimEntries);
        var offset = flag / 64;
        return offset < parts.Length &&
            ulong.TryParse(parts[offset], out var value) &&
            (value & (1UL << (flag % 64))) != 0;
    }

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }
}
