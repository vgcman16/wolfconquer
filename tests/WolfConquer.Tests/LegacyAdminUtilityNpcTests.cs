using System.Buffers;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyAdminUtilityNpcTests
{
    private const uint Stuff12NpcId = 22_224;
    private const uint StuffNpcId = 77_839;
    private const uint InnerPowerBookNpcId = 18_786;

    [Theory]
    [InlineData(StuffNpcId, "NpcHandler.Stuff")]
    [InlineData(Stuff12NpcId, "NpcHandler.Stuff12")]
    public async Task AdminStuffNpcsAreHiddenFromNormalPlayers(uint npcId, string sourceMethod)
    {
        var result = await InvokeAsync(npcId, optionId: 0);

        Assert.True(result.Handled);
        Assert.Empty(result.Sent);
        Assert.Contains("disabled LegacyNpc admin utility", result.Log);
        Assert.Contains(sourceMethod, result.Log);
        Assert.DoesNotContain("PARITY-GAP LegacyNpc AdminUtility", result.Log);
    }

    public static IEnumerable<object?[]> ForgedPrivileges()
    {
        yield return ["AdminUtilityHero[GM]", null, null];
        yield return ["AdminUtilityHero[PM]", null, null];
        yield return ["AdminUtilityHero[gM]", null, null];
        foreach (var role in new[] { "PM", "GM", "Admin", "admin" })
            yield return ["AdminUtilityHero", "Role", role];
        foreach (var key in new[] { "ProjectManager", "GM", "IsGM", "GameMaster", "IsGameMaster", "Admin", "IsAdmin", "Privileged" })
            foreach (var value in new[] { "1", "yes", "true", "TrUe", " true " })
                yield return ["AdminUtilityHero", key, value];
    }

    [Theory]
    [MemberData(nameof(ForgedPrivileges))]
    public async Task ForgedNamesRolesAndBooleanAttributesCannotReachAnyLegacyAdminBranch(string name, string? key, string? value)
    {
        foreach (var npcId in new[] { StuffNpcId, Stuff12NpcId })
            foreach (var option in new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 60, 89, 120, 149, 162, 200, 201, 254, 255 })
            {
                var result = await InvokeAsync(npcId, option, name: name, key: key, value: value);
                Assert.True(result.Handled);
                Assert.Empty(result.Sent);
                Assert.Contains("reason=ownedGmTransactionRequired", result.Log);
                Assert.DoesNotContain(name, result.Log, StringComparison.Ordinal);
                Assert.Equal(100, result.UpdatedSession.Character.Level);
                Assert.Empty(result.UpdatedSession.PlayerItems.InventoryItems);
            }
    }

    [Fact]
    public async Task UnknownNpcRemainsUnhandled()
    {
        var result = await InvokeAsync(99_999, 0, name: "Unknown[GM]");
        Assert.False(result.Handled);
        Assert.Empty(result.Sent);
        Assert.Empty(result.Log);
    }

    [Fact]
    public async Task DisabledAdminRoutesPreserveGenuinelyAdmittedNativeOwnersAndAllSavedFiles()
    {
        await WorldServerFrameHandlerTests.WithNativeItemSpellCallerAsync(false, async fixture =>
        {
            await fixture.Login();
            var before = fixture.Current;
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(fixture.Native.OrdinarySpellBinding);
            var player = binding.Player;
            var image = binding.Items.Image;
            var spellOwner = Assert.IsType<LegacyNativeSpellStorage>(fixture.Native.MySpells);
            var proficiencyOwner = Assert.IsType<LegacyNativeProficiencyStorage>(fixture.Native.MyProfs);
            var spells = spellOwner.ClientSpells;
            var proficiencies = proficiencyOwner.ClientProf;
            var nativeBefore = (player.Level, player.HitPoints, player.Mana, player.HeroPoints,
                player.Kills, player.DragonWarHits, player.DragonWarScore);
            var files = fixture.Files();
            var itemBytes = PlayerItemSnapshotLoader.ToLegacyBytes(before.PlayerItems);
            var spellBytes = PlayerSpellSnapshotLoader.ToLegacyBytes(before.PlayerSpells);
            var proficiencyBytes = PlayerProficiencySnapshotLoader.ToLegacyBytes(before.PlayerProficiencies);
            var dragonWar = new LegacyDragonWarRuntimeStateStore();
            fixture.Sent.Clear();
            foreach (var npcId in new[] { StuffNpcId, Stuff12NpcId })
                foreach (var option in new byte[] { 0, 1, 7, 60, 89, 120, 149, 162, 200, 201, 254, 255 })
                    Assert.True(await WorldPacketHandlers.TryHandleLegacyAdminUtilityNpcAsync(
                        new WorldPacketHandlerContext(fixture.Connection, fixture.Log, Session: fixture.Current,
                            Sessions: fixture.Store, WorldData: fixture.Catalog, DragonWarStates: dragonWar,
                            ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13), fixture.Current,
                        npcId, option, CancellationToken.None));
            Assert.Same(before, fixture.Current);
            Assert.Same(binding, fixture.Native.OrdinarySpellBinding);
            Assert.Same(player, fixture.Native.Player);
            Assert.Same(image, binding.Items.Image);
            Assert.Same(spellOwner, fixture.Native.MySpells);
            Assert.Same(proficiencyOwner, fixture.Native.MyProfs);
            Assert.Same(spells, spellOwner.ClientSpells);
            Assert.Same(proficiencies, proficiencyOwner.ClientProf);
            Assert.True(binding.Items.PhysicalOwnerIsCurrentPlain());
            Assert.Equal(nativeBefore, (player.Level, player.HitPoints, player.Mana, player.HeroPoints,
                player.Kills, player.DragonWarHits, player.DragonWarScore));
            Assert.Equal(itemBytes, PlayerItemSnapshotLoader.ToLegacyBytes(fixture.Current.PlayerItems));
            Assert.Equal(spellBytes, PlayerSpellSnapshotLoader.ToLegacyBytes(fixture.Current.PlayerSpells));
            Assert.Equal(proficiencyBytes, PlayerProficiencySnapshotLoader.ToLegacyBytes(fixture.Current.PlayerProficiencies));
            Assert.Equal(LegacyDragonWarProcess.Dead, dragonWar.Process);
            Assert.Empty(fixture.Sent);
            fixture.CheckFiles(files);
        }, characterLines: ["ProjectManager=1", "GM=yes", "IsGM=true", "GameMaster=1",
            "IsGameMaster=yes", "Admin=true", "IsAdmin=1", "Privileged=true", "Role=Admin"]);
    }

    [Fact]
    public async Task InnerPowerBookNpcRejectsNonSecondReborn()
    {
        var result = await InvokeAsync(InnerPowerBookNpcId, optionId: 0, reborn: 1);

        Assert.True(result.Handled);
        Assert.Contains(ParseNpcReplies(result.Sent), reply => reply.Text.Contains("second rebirth", StringComparison.Ordinal));
        Assert.Contains("source=NpcHandler.InnerPowerNpc", result.Log);
        Assert.DoesNotContain("PARITY-GAP LegacyNpc AdminUtility", result.Log);
        AssertAsciiReplies(result.Sent);
    }

    [Fact]
    public async Task InnerPowerBookNpcShowsTutorialAndReportsMissingBookDataWithoutMutation()
    {
        var root = await InvokeAsync(InnerPowerBookNpcId, optionId: 0, reborn: 2);

        Assert.True(root.Handled);
        Assert.Contains(ParseNpcReplies(root.Sent), reply => reply.Text.Contains("essence of Inner Power", StringComparison.Ordinal));
        Assert.Contains(ParseNpcReplies(root.Sent), reply => reply.Text == "How to acquire Inner Power?" && reply.OptionId == 1);

        var info = await InvokeAsync(InnerPowerBookNpcId, optionId: 1, reborn: 2);
        Assert.True(info.Handled);
        Assert.Contains(ParseNpcReplies(info.Sent), reply => reply.Text.Contains("Potency Points", StringComparison.Ordinal));
        Assert.DoesNotContain("PARITY-GAP LegacyNpc AdminUtility", info.Log);

        var grant = await InvokeAsync(InnerPowerBookNpcId, optionId: 2, reborn: 2);
        Assert.True(grant.Handled);
        Assert.Contains(ParseNpcReplies(grant.Sent), reply => reply.Text == "The first Inner Power book is not available in this runtime.");
        Assert.Contains("PARITY-GAP LegacyNpc AdminUtility", grant.Log);
        Assert.Contains("NpcHandler.InnerPowerNpc", grant.Log);
        Assert.Empty(grant.UpdatedSession.PlayerItems.InventoryItems);
        AssertAsciiReplies(root.Sent.Concat(info.Sent).Concat(grant.Sent));
    }

    private static async Task<AdminUtilityInvokeResult> InvokeAsync(
        uint npcId,
        byte optionId,
        int reborn = 0,
        string name = "AdminUtilityHero",
        string? key = null,
        string? value = null)
    {
        using var log = new StringWriter();
        var sent = new List<byte[]>();
        var disconnected = 0;
        var connection = CreateSendingConnection(sent, () => disconnected++);
        var session = CreateSession(reborn, name, key, value);
        var attributes = session.Character.Attributes.ToArray();
        var sessions = new WorldSessionStore();
        sessions.Set(connection, session);

        var handled = await WorldPacketHandlers.TryHandleLegacyAdminUtilityNpcAsync(
            new WorldPacketHandlerContext(connection, log, Session: session, Sessions: sessions),
            session,
            npcId,
            optionId,
            CancellationToken.None);

        Assert.True(sessions.TryGet(connection, out var updatedSession));
        Assert.NotNull(updatedSession);
        Assert.Same(session, updatedSession);
        Assert.Equal(attributes, session.Character.Attributes.ToArray());
        Assert.Equal(0, disconnected);
        return new AdminUtilityInvokeResult(handled, sent, log.ToString(), updatedSession);
    }

    private static WorldSession CreateSession(int reborn, string name, string? key, string? value)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (key is not null)
        {
            attributes[key] = value!;
        }

        return new WorldSession(
            AccountId: 100,
            Username: "admin-utility-test",
            Character: new CharacterSnapshot(
                Uid: 1_293_600,
                Name: name,
                Body: 1003,
                Face: 0,
                Hair: 0,
                Class: 7051,
                FirstClass: 7051,
                SecondClass: 0,
                Reborn: reborn,
                Level: 100,
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
                Attributes: attributes,
                PaidNobility: new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)),
            PlayerItems: PlayerItemSnapshot.Empty,
            PlayerSpells: PlayerSpellSnapshot.Empty,
            PlayerProficiencies: PlayerProficiencySnapshot.Empty);
    }

    private static ConquerTcpConnection CreateSendingConnection(List<byte[]> sent, Action disconnect)
    {
        return new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (frame, _) =>
        {
            sent.Add(frame.ToArray());
            return ValueTask.CompletedTask;
        }, _ => { disconnect(); return ValueTask.CompletedTask; });
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

    private static PacketFrame ReadFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static void AssertAsciiReplies(IEnumerable<byte[]> sent)
    {
        foreach (var reply in ParseNpcReplies(sent))
        {
            if (!string.IsNullOrEmpty(reply.Text))
            {
                Assert.True(reply.Text.All(static character => character <= 127), reply.Text);
            }
        }
    }

    private sealed record AdminUtilityInvokeResult(
        bool Handled,
        IReadOnlyList<byte[]> Sent,
        string Log,
        WorldSession UpdatedSession);
}
