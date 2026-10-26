using System.Globalization;
using System.Text;
using WolfConquer.Core.Accounts;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeExistingCharacterPrefixTests
{
    private const uint Uid = 1_293_600;

    [Fact]
    public async Task AcceptedLoginLoadsTrueRelicSpiritAndMarksLocalOnline()
    {
        await AssertAcceptedAsync("True", initialOnline: 0, expectedOnline: 1, expectedRelic: true);
    }

    [Fact]
    public async Task AcceptedLoginLoadsFalseRelicSpiritAndMarksLocalOnline()
    {
        await AssertAcceptedAsync("False", initialOnline: 0, expectedOnline: 1, expectedRelic: false);
    }

    [Fact]
    public async Task AcceptedLoginMissingRelicSpiritDefaultsFalseAndContinues()
    {
        await AssertAcceptedAsync(null, initialOnline: 0, expectedOnline: 1, expectedRelic: false);
    }

    [Fact]
    public async Task AcceptedLoginMalformedRelicSpiritDefaultsFalseAndContinues()
    {
        await AssertAcceptedAsync("bad", initialOnline: 0, expectedOnline: 1, expectedRelic: false);
    }

    [Fact]
    public async Task AcceptedLoginNumericRelicSpiritIsNotTrueAndContinues()
    {
        await AssertAcceptedAsync("1", initialOnline: 0, expectedOnline: 1, expectedRelic: false);
    }

    [Fact]
    public async Task AcceptedLoginIncrementsCurrentNineNotSavedSevenOrMarkerOne()
    {
        await AssertAcceptedAsync("True", initialOnline: 9, expectedOnline: 10, expectedRelic: true);
    }

    [Fact]
    public async Task CurrentOnlineOverflowRejectsWithoutWrappingOrPublishingThePrefix()
    {
        using var input = new PrefixInput("True");
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectBornAsync(fixture);
            var client = birth.Connection.NativeGameClient!;
            var player = client.Player;
            player.Online = uint.MaxValue;
            var flags = player.BitVector;
            var random = player.MyRandom;

            var rejected = await SendLoginAsync(fixture, wire);

            Assert.Null(rejected.Before);
            Assert.Null(rejected.After);
            Assert.Equal(0U, player.UID);
            Assert.Equal(uint.MaxValue, player.Online);
            Assert.False(player.RelicSpirit);
            Assert.Same(flags, player.BitVector);
            Assert.Same(random, player.MyRandom);
            Assert.Null(client.Beasts);
            Assert.Null(client.HairfaceStorage);
            Assert.Equal(input.InitialBytes, File.ReadAllBytes(input.FilePath));
            Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection));
            Assert.DoesNotContain("legacy MsgLoginGame Success", log.Text);
        });
    }

    [Fact]
    public async Task LocalWriteFailureRetainsCompleteNativePrefixAndPublishesNoSession()
    {
        using var input = new PrefixInput("True");
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectBornAsync(fixture);
            var client = Assert.IsType<LegacyNativeGameClientRuntime>(birth.Connection.NativeGameClient);
            var player = client.Player;
            player.Online = 9; // Controlled CURRENT field, not a new seeding API or ordinary-birth claim.
            using (var held = new FileStream(input.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Assert.Throws<IOException>(() =>
                {
                    using var denied = new FileStream(input.FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                });
                var login = await SendLoginAsync(fixture, wire);
                Assert.Null(login.Before);
                Assert.Null(login.After);
                Assert.Equal(0U, player.UID);
                Assert.Equal(9U, player.Online);
                Assert.False(player.RelicSpirit);
                Assert.Same(player, client.Player);
                Assert.Null(client.Beasts);
                Assert.Contains("skipped legacy accepted-login local prefix/admission key=1293600", log.Text);
                Assert.Contains("reason=tradeJournalWriteFailed:IOException", log.Text);
                Assert.DoesNotContain("legacy MsgLoginGame Success", log.Text);
            }
            Assert.Equal(input.InitialBytes, File.ReadAllBytes(input.FilePath));
            Assert.Same(player, birth.Connection.NativeGameClient!.Player);
            Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection));
            Assert.False(LegacyTradeJournal.HasPending(input.Users));
            // The unchanged loopback drains output to Stream.Null. No ANSWER_OK is SOURCE-proven
            // by the return-before-send branch, not claimed as observed wire absence here.
        });
    }

    [Fact]
    public async Task RepeatedAcceptedLoginDoesNotIncrementAgainOrReplaceTheSession()
    {
        using var input = new PrefixInput("True");
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectBornAsync(fixture);
            var first = await SendLoginAsync(fixture, wire);
            var accepted = Assert.IsType<WorldSession>(first.After);
            var player = Assert.IsType<LegacyNativePlayerRuntime>(accepted.NativePlayer);
            var bytes = File.ReadAllBytes(input.FilePath);
            var repeated = await SendLoginAsync(fixture, wire);
            Assert.Same(accepted, repeated.Before);
            Assert.Same(accepted, repeated.After);
            Assert.Same(birth.Connection.NativeGameClient!.Player, player);
            Assert.Equal(1U, player.Online);
            Assert.Equal(Uid, player.UID);
            Assert.True(player.RelicSpirit);
            Assert.Equal(bytes, File.ReadAllBytes(input.FilePath));
            Assert.Contains("ignored repeated legacy MsgLoginGame", log.Text);
        });
    }

    [Fact]
    public async Task DuplicateSecondLoginSkipsPrefixAndThirdRetryUsesItsOwnPlayer()
    {
        using var input = new PrefixInput("True");
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (firstWire, firstBirth) = await ConnectBornAsync(fixture);
            var first = await SendLoginAsync(fixture, firstWire);
            var accepted = Assert.IsType<WorldSession>(first.After);
            Assert.Equal(1U, accepted.NativePlayer!.Online);
            var (secondWire, secondBirth) = await ConnectBornAsync(fixture);
            var rejected = await SendLoginAsync(fixture, secondWire);
            Assert.Null(rejected.After);
            var rejectedPlayer = secondBirth.Connection.NativeGameClient!.Player;
            Assert.Equal(0U, rejectedPlayer.UID);
            Assert.Equal(0U, rejectedPlayer.Online);
            Assert.False(rejectedPlayer.RelicSpirit);
            Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(firstBirth.Connection));
            Assert.Contains("rejected duplicate legacy MsgLoginGame", log.Text);
            Assert.Equal(input.ExpectedMarkerBytes, File.ReadAllBytes(input.FilePath));
            var (thirdWire, thirdBirth) = await ConnectBornAsync(fixture);
            var third = await SendLoginAsync(fixture, thirdWire);
            var retried = Assert.IsType<WorldSession>(third.After);
            Assert.Same(thirdBirth.Connection.NativeGameClient!.Player, retried.NativePlayer);
            Assert.NotSame(accepted.NativePlayer, retried.NativePlayer);
            Assert.NotSame(rejectedPlayer, retried.NativePlayer);
            Assert.Equal(Uid, retried.NativePlayer!.UID);
            Assert.Equal(1U, retried.NativePlayer.Online);
            Assert.True(retried.NativePlayer.RelicSpirit);
        });
    }

    [Fact]
    public async Task MissingExistingSnapshotDoesNotRunThePrefix()
    {
        using var input = new PrefixInput(null, create: false);
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectBornAsync(fixture);
            var result = await SendLoginAsync(fixture, wire);
            Assert.Null(result.After);
            AssertUnchangedUnacceptedPlayer(birth);
            Assert.False(File.Exists(input.FilePath));
            Assert.Contains("pending NEW_ROLE", log.Text);
        });
    }

    [Fact]
    public async Task UidMismatchDoesNotRunThePrefix()
    {
        using var input = new PrefixInput("True", storedUid: Uid + 1);
        await AssertRejectedSnapshotAsync(input, "uidMismatch");
    }

    [Fact]
    public async Task MalformedRequiredSnapshotValueDoesNotRunThePrefix()
    {
        using var input = new PrefixInput("True", malformedLevel: true);
        await AssertRejectedSnapshotAsync(input, "snapshotParseFailed");
    }

    [Fact]
    public async Task ModernSavedAdmissionUpdatesPresenceWithoutInventingANativeOwner()
    {
        using var input = new PrefixInput("True");
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var wire = await fixture.ConnectNongameAsync();
            var login = await SendLoginAsync(fixture, wire);
            var accepted = Assert.IsType<WorldSession>(login.After);
            Assert.Null(login.Connection.NativeGameClient);
            Assert.Null(accepted.NativePlayer);
            Assert.Equal(1L, accepted.Character.Online);
            Assert.Equal("1", accepted.Character.Attributes["Online"]);
            Assert.Equal(input.ExpectedMarkerBytes, File.ReadAllBytes(input.FilePath));
            Assert.Contains("ordinary admission committed key=1293600.", log.Text);
            Assert.Contains("legacy MsgLoginGame Success", log.Text);
        }, mode: ConquerTcpWireMode.Modern);
    }

    [Fact]
    public async Task InternalHandoffDoesNotRunTheExistingCharacterPrefix()
    {
        using var input = new PrefixInput("True");
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectBornAsync(fixture);
            var ticket = handoff.Issue(Uid, "prefix-handoff", ConquerRuntimeDefaults.WorldServer);
            await wire.SendAsync(new InternalWorldEnterRequestPacket(ticket.Token).ToFrameBytes(), fixture.Token);
            var dispatch = await fixture.NextAsync();
            Assert.Equal(WorldPacketOpcodes.InternalWorldEnterRequest, dispatch.Opcode);
            var accepted = Assert.IsType<WorldSession>(dispatch.After);
            Assert.Same(birth.Connection.NativeGameClient, accepted.NativeGameClient);
            Assert.Equal(0U, accepted.NativePlayer!.UID);
            Assert.Equal(0U, accepted.NativePlayer.Online);
            Assert.False(accepted.NativePlayer.RelicSpirit);
            Assert.Equal(7L, accepted.Character.Online);
            Assert.Equal(input.InitialBytes, File.ReadAllBytes(input.FilePath));
        }, handoff: handoff);
    }

    [Fact]
    public async Task NewRoleDoesNotRunTheExistingCharacterPrefix()
    {
        using var input = new PrefixInput(null, create: false);
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectBornAsync(fixture);
            Assert.Null((await SendLoginAsync(fixture, wire)).After);
            await wire.SendAsync(new LegacyNewRolePacket("PrefixRole", 1008, 12).ToFrameBytes(), fixture.Token);
            var created = await fixture.NextAsync();
            Assert.Equal(LegacyNewRolePacket.Opcode, created.Opcode);
            var accepted = Assert.IsType<WorldSession>(created.After);
            Assert.Same(birth.Connection.NativeGameClient!.Player, accepted.NativePlayer);
            Assert.Equal(0U, accepted.NativePlayer!.UID);
            Assert.Equal(0U, accepted.NativePlayer.Online);
            Assert.False(accepted.NativePlayer.RelicSpirit);
            Assert.Equal(0L, accepted.Character.Online);
            Assert.Contains("legacy NewRole Success", log.Text);
            Assert.DoesNotContain("accepted-login local prefix", log.Text);
        });
    }

    [Fact]
    public async Task LaterDirectionCopyRetainsPhysicalPrefixAndDoesNotRestoreSavedSeven()
    {
        using var input = new PrefixInput("True");
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectBornAsync(fixture);
            var accepted = Assert.IsType<WorldSession>((await SendLoginAsync(fixture, wire)).After);
            await wire.SendAsync(new MsgActionPacket
            {
                ObjId = Uid, Type = MsgActionType.ChangeDirection, Facing = 3,
            }.ToFrameBytes(), fixture.Token);
            var direction = await fixture.NextAsync();
            var copied = Assert.IsType<WorldSession>(direction.After);
            Assert.NotSame(accepted, copied);
            Assert.Same(birth.Connection.NativeGameClient!.Player, copied.NativePlayer);
            Assert.Equal(1U, copied.NativePlayer!.Online);
            Assert.True(copied.NativePlayer.RelicSpirit);
            Assert.Equal(1L, copied.Character.Online);
            Assert.Equal("1", copied.Character.Attributes["Online"]);
            Assert.Equal("3", copied.Character.Attributes["Angle"]);
            var reloaded = new CharacterSnapshotLoader(input.Users).TryLoadByAccountId(Uid);
            Assert.Equal(CharacterSnapshotLoadStatus.Success, reloaded.Status);
            Assert.Equal(1L, reloaded.Snapshot!.Online);
            Assert.Equal("1", reloaded.Snapshot.Attributes["Online"]);
            Assert.Contains("[Unrelated]\r\nKeep=unchanged", File.ReadAllText(input.FilePath));
        });
    }

    [Fact]
    public async Task ForeignExistingBeastOwnerRejectsWithoutChangingEitherPlayerOrTheSave()
    {
        using var input = new PrefixInput("True");
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectBornAsync(fixture);
            var client = birth.Connection.NativeGameClient!;
            var player = client.Player;
            var flags = player.BitVector;
            var random = player.MyRandom;
            var (_, foreignBirth) = await ConnectBornAsync(fixture);
            var foreignOwner = foreignBirth.Connection.NativeGameClient!;
            Assert.NotSame(client, foreignOwner);
            var existing = new LegacyNativeBeastsRuntime(foreignOwner)
            {
                Activated = true, Points = 73, flag = 0x80000011U, FixedLevel = 7,
            };
            client.Beasts = existing;

            var rejected = await SendLoginAsync(fixture, wire);

            Assert.Null(rejected.Before);
            Assert.Null(rejected.After);
            Assert.Same(player, client.Player);
            Assert.Same(flags, player.BitVector);
            Assert.Same(random, player.MyRandom);
            Assert.Equal(0U, player.UID);
            Assert.Equal(0U, player.Online);
            Assert.False(player.RelicSpirit);
            Assert.Null(client.HairfaceStorage);
            AssertPreservedBeast(client, existing, foreignOwner, points: 73, flag: 0x80000011U,
                fixedLevel: 7, activated: true);
            Assert.Null(foreignOwner.Beasts);
            Assert.Equal(0U, foreignOwner.Player.UID);
            Assert.Equal(0U, foreignOwner.Player.Online);
            Assert.False(foreignOwner.Player.RelicSpirit);
            Assert.Equal(input.InitialBytes, File.ReadAllBytes(input.FilePath));
            Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection));
            Assert.DoesNotContain("legacy MsgLoginGame Success", log.Text);
        });
    }

    [Fact]
    public async Task FailedLocalWriteRetainsNativePrefixAndExistingBeast()
    {
        using var input = new PrefixInput("True");
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectBornAsync(fixture);
            var client = birth.Connection.NativeGameClient!;
            var player = client.Player;
            var flags = player.BitVector;
            var random = player.MyRandom;
            player.Online = 9;
            var existing = new LegacyNativeBeastsRuntime(client)
            {
                Activated = true, Points = 97, flag = 0x40000008U, FixedLevel = 9,
            };
            client.Beasts = existing;

            using (var held = new FileStream(input.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Assert.Throws<IOException>(() =>
                {
                    using var denied = new FileStream(input.FilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                });
                var rejected = await SendLoginAsync(fixture, wire);
                Assert.Null(rejected.Before);
                Assert.Null(rejected.After);
                Assert.Equal(0U, player.UID);
                Assert.Equal(9U, player.Online);
                Assert.False(player.RelicSpirit);
                Assert.Same(player, client.Player);
                Assert.Same(flags, player.BitVector);
                Assert.Same(random, player.MyRandom);
                AssertPreservedBeast(client, existing, client, points: 97, flag: 0x40000008U, fixedLevel: 9);
                Assert.Contains("reason=tradeJournalWriteFailed:IOException", log.Text);
                Assert.DoesNotContain("legacy MsgLoginGame Success", log.Text);
            }
            Assert.Equal(input.InitialBytes, File.ReadAllBytes(input.FilePath));
            Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection));
            Assert.False(LegacyTradeJournal.HasPending(input.Users));
        });
    }

    [Fact]
    public async Task RepeatedAcceptedLoginRetainsTheSameBornBeast()
    {
        using var input = new PrefixInput("True");
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectBornAsync(fixture);
            var client = birth.Connection.NativeGameClient!;
            var player = client.Player;
            var flags = player.BitVector;
            var random = player.MyRandom;
            Assert.Null(client.Beasts);
            var accepted = Assert.IsType<WorldSession>((await SendLoginAsync(fixture, wire)).After);
            var beasts = AssertNewBeast(client);
            var bytes = File.ReadAllBytes(input.FilePath);

            var repeated = await SendLoginAsync(fixture, wire);

            Assert.Same(accepted, repeated.Before);
            Assert.Same(accepted, repeated.After);
            Assert.Same(beasts, AssertNewBeast(client));
            Assert.Same(player, accepted.NativePlayer);
            Assert.Same(flags, player.BitVector);
            Assert.Same(random, player.MyRandom);
            Assert.Equal(Uid, player.UID);
            Assert.Equal(1U, player.Online);
            Assert.True(player.RelicSpirit);
            Assert.Equal(input.ExpectedMarkerBytes, bytes);
            Assert.Equal(bytes, File.ReadAllBytes(input.FilePath));
            Assert.Contains("ignored repeated legacy MsgLoginGame", log.Text);
        });
    }

    [Fact]
    public async Task DuplicateRejectionLeavesBeastAbsentAndThirdRetryOwnsANewBeast()
    {
        using var input = new PrefixInput("True");
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (firstWire, firstBirth) = await ConnectBornAsync(fixture);
            var firstClient = firstBirth.Connection.NativeGameClient!;
            var first = Assert.IsType<WorldSession>((await SendLoginAsync(fixture, firstWire)).After);
            var firstBeast = AssertNewBeast(firstClient);
            var (secondWire, secondBirth) = await ConnectBornAsync(fixture);
            var secondClient = secondBirth.Connection.NativeGameClient!;
            Assert.Null(secondClient.Beasts);

            var rejected = await SendLoginAsync(fixture, secondWire);

            Assert.Null(rejected.After);
            Assert.Null(secondClient.Beasts);
            Assert.Equal(0U, secondClient.Player.UID);
            Assert.Equal(0U, secondClient.Player.Online);
            Assert.False(secondClient.Player.RelicSpirit);
            Assert.Same(firstBeast, firstClient.Beasts);
            Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(firstBirth.Connection));
            Assert.Equal(input.ExpectedMarkerBytes, File.ReadAllBytes(input.FilePath));
            Assert.Contains("rejected duplicate legacy MsgLoginGame", log.Text);

            var (thirdWire, thirdBirth) = await ConnectBornAsync(fixture);
            var thirdClient = thirdBirth.Connection.NativeGameClient!;
            Assert.Null(thirdClient.Beasts);
            var third = Assert.IsType<WorldSession>((await SendLoginAsync(fixture, thirdWire)).After);
            var thirdBeast = AssertNewBeast(thirdClient);
            Assert.NotSame(firstClient, thirdClient);
            Assert.NotSame(firstBeast, thirdBeast);
            Assert.Same(thirdClient, third.NativeGameClient);
            Assert.Same(thirdClient.Player, third.NativePlayer);
            Assert.NotSame(first.NativePlayer, third.NativePlayer);
            Assert.Equal(Uid, thirdClient.Player.UID);
            Assert.Equal(1U, thirdClient.Player.Online);
            Assert.True(thirdClient.Player.RelicSpirit);
            Assert.Same(firstBeast, firstClient.Beasts);
            Assert.Equal(input.ExpectedMarkerBytes, File.ReadAllBytes(input.FilePath));
        });
    }

    [Fact]
    public async Task DirectionCopyRetainsTheAcceptedBeastAndOwner()
    {
        using var input = new PrefixInput("True");
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectBornAsync(fixture);
            var client = birth.Connection.NativeGameClient!;
            var player = client.Player;
            var flags = player.BitVector;
            var random = player.MyRandom;
            var accepted = Assert.IsType<WorldSession>((await SendLoginAsync(fixture, wire)).After);
            var beasts = AssertNewBeast(client);

            await wire.SendAsync(new MsgActionPacket
            {
                ObjId = Uid, Type = MsgActionType.ChangeDirection, Facing = 3,
            }.ToFrameBytes(), fixture.Token);
            var copied = Assert.IsType<WorldSession>((await fixture.NextAsync()).After);

            Assert.NotSame(accepted, copied);
            Assert.Same(client, copied.NativeGameClient);
            Assert.Same(player, copied.NativePlayer);
            Assert.Same(beasts, AssertNewBeast(client));
            Assert.Same(flags, player.BitVector);
            Assert.Same(random, player.MyRandom);
            Assert.Equal(1U, player.Online);
            Assert.Equal(Uid, player.UID);
            Assert.True(player.RelicSpirit);
            Assert.Equal("3", copied.Character.Attributes["Angle"]);
            // Direction is session-only here; the scalar persistence contract excludes Angle.
            var expected = input.ExpectedMarkerBytes;
            Assert.Equal(expected, File.ReadAllBytes(input.FilePath));
            var fresh = new CharacterSnapshotLoader(input.Users).TryLoadByAccountId(Uid);
            Assert.Equal(CharacterSnapshotLoadStatus.Success, fresh.Status);
            Assert.Equal(1L, fresh.Snapshot!.Online);
            Assert.Equal("1", fresh.Snapshot.Attributes["Angle"]);
        });
    }

    [Fact]
    public async Task InternalHandoffLeavesTheSocketBornBeastFieldUnborn()
    {
        using var input = new PrefixInput("True");
        var handoff = new AccountHandoffService(new InMemoryAccountHandoffTicketStore());
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectBornAsync(fixture);
            var client = birth.Connection.NativeGameClient!;
            Assert.Null(client.Beasts);
            var ticket = handoff.Issue(Uid, "beast-handoff", ConquerRuntimeDefaults.WorldServer);

            await wire.SendAsync(new InternalWorldEnterRequestPacket(ticket.Token).ToFrameBytes(), fixture.Token);
            var dispatch = await fixture.NextAsync();

            Assert.Equal(WorldPacketOpcodes.InternalWorldEnterRequest, dispatch.Opcode);
            var accepted = Assert.IsType<WorldSession>(dispatch.After);
            Assert.Same(client, accepted.NativeGameClient);
            Assert.Same(client.Player, accepted.NativePlayer);
            Assert.Null(client.Beasts);
            Assert.Equal(0U, client.Player.UID);
            Assert.Equal(0U, client.Player.Online);
            Assert.False(client.Player.RelicSpirit);
            Assert.Equal(7L, accepted.Character.Online);
            Assert.Equal(input.InitialBytes, File.ReadAllBytes(input.FilePath));
        }, handoff: handoff);
    }

    [Fact]
    public async Task NewRoleLeavesTheSocketBornBeastFieldUnborn()
    {
        using var input = new PrefixInput(null, create: false);
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectBornAsync(fixture);
            var client = birth.Connection.NativeGameClient!;
            Assert.Null((await SendLoginAsync(fixture, wire)).After);
            Assert.Null(client.Beasts);

            await wire.SendAsync(new LegacyNewRolePacket("BeastRole", 1008, 12).ToFrameBytes(), fixture.Token);
            var created = await fixture.NextAsync();

            Assert.Equal(LegacyNewRolePacket.Opcode, created.Opcode);
            var accepted = Assert.IsType<WorldSession>(created.After);
            Assert.Same(client, accepted.NativeGameClient);
            Assert.Same(client.Player, accepted.NativePlayer);
            Assert.Null(client.Beasts);
            Assert.Equal(0U, client.Player.UID);
            Assert.Equal(0U, client.Player.Online);
            Assert.False(client.Player.RelicSpirit);
            Assert.Equal(0L, accepted.Character.Online);
            var fresh = new CharacterSnapshotLoader(input.Users).TryLoadByAccountId(Uid);
            Assert.Equal(CharacterSnapshotLoadStatus.Success, fresh.Status);
            Assert.Equal(0L, fresh.Snapshot!.Online);
            Assert.Contains("legacy NewRole Success", log.Text);
            Assert.DoesNotContain("accepted-login local prefix", log.Text);
        });
    }

    private static LegacyNativeBeastsRuntime AssertNewBeast(LegacyNativeGameClientRuntime client)
    {
        var beasts = Assert.IsType<LegacyNativeBeastsRuntime>(client.Beasts);
        Assert.Same(client, beasts.Owner);
        Assert.Equal((byte)1, beasts.FixedLevel);
        Assert.False(beasts.Activated);
        Assert.Equal(0U, beasts.Points);
        Assert.Equal(0U, beasts.flag);
        Assert.Equal((byte)0, beasts.Level);
        return beasts;
    }

    private static void AssertPreservedBeast(
        LegacyNativeGameClientRuntime client, LegacyNativeBeastsRuntime expected,
        LegacyNativeGameClientRuntime expectedOwner, uint points, uint flag, byte fixedLevel, bool activated = true)
    {
        Assert.Same(expected, client.Beasts);
        Assert.Same(expectedOwner, expected.Owner);
        Assert.Equal(activated, expected.Activated);
        Assert.Equal(points, expected.Points);
        Assert.Equal(flag, expected.flag);
        Assert.Equal(fixedLevel, expected.FixedLevel);
        Assert.Equal((byte)0, expected.Level);
    }

    private static async Task AssertAcceptedAsync(
        string? relic, uint initialOnline, uint expectedOnline, bool expectedRelic)
    {
        using var input = new PrefixInput(relic);
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectBornAsync(fixture);
            var client = birth.Connection.NativeGameClient!;
            var player = client.Player;
            var flags = player.BitVector;
            var random = player.MyRandom;
            Assert.Equal(0U, player.UID);
            Assert.Equal(0U, player.Online);
            player.Online = initialOnline; // Explicit state-control case on the REAL socket-born Player.
            var login = await SendLoginAsync(fixture, wire);
            var accepted = Assert.IsType<WorldSession>(login.After);
            Assert.Same(client, accepted.NativeGameClient);
            Assert.Same(player, accepted.NativePlayer);
            Assert.Same(flags, player.BitVector);
            Assert.Same(random, player.MyRandom);
            Assert.Equal(Uid, player.UID);
            Assert.Equal(expectedOnline, player.Online);
            Assert.Equal(expectedRelic, player.RelicSpirit);
            AssertNewBeast(client);
            Assert.Equal(1L, accepted.Character.Online);
            Assert.Equal("1", accepted.Character.Attributes["Online"]);
            Assert.Equal(input.ExpectedMarkerBytes, File.ReadAllBytes(input.FilePath));
            var fresh = new CharacterSnapshotLoader(input.Users).TryLoadByAccountId(Uid);
            Assert.Equal(CharacterSnapshotLoadStatus.Success, fresh.Status);
            Assert.Equal(1L, fresh.Snapshot!.Online);
            Assert.Equal("1", fresh.Snapshot.Attributes["Online"]);
            Assert.Contains("legacy MsgLoginGame Success", log.Text);
            Assert.DoesNotContain("skipped legacy accepted-login local prefix", log.Text);
        });
    }

    private static async Task AssertRejectedSnapshotAsync(PrefixInput input, string reason)
    {
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectBornAsync(fixture);
            var rejected = await SendLoginAsync(fixture, wire);
            Assert.Null(rejected.After);
            AssertUnchangedUnacceptedPlayer(birth);
            Assert.Equal(input.InitialBytes, File.ReadAllBytes(input.FilePath));
            Assert.Contains(reason, log.Text);
            Assert.DoesNotContain("accepted-login local prefix", log.Text);
        });
    }

    private static void AssertUnchangedUnacceptedPlayer(NativePlayerDispatch birth)
    {
        var player = birth.Connection.NativeGameClient!.Player;
        Assert.Equal(0U, player.UID);
        Assert.Equal(0U, player.Online);
        Assert.False(player.RelicSpirit);
        Assert.Null(birth.Connection.NativeGameClient.Beasts);
    }

    private static async Task<(NativePlayerWireClient Wire, NativePlayerDispatch Birth)> ConnectBornAsync(
        NativePlayerLoopback fixture)
    {
        var wire = await fixture.ConnectGameAsync(false);
        var birth = await fixture.NextAsync();
        Assert.Equal(NativePlayerLoopback.ProbeOpcode, birth.Opcode);
        Assert.Null(birth.Before);
        Assert.Null(birth.After);
        var client = Assert.IsType<LegacyNativeGameClientRuntime>(birth.Connection.NativeGameClient);
        Assert.Same(client, client.Player.Owner);
        return (wire, birth);
    }

    private static async Task<NativePlayerDispatch> SendLoginAsync(
        NativePlayerLoopback fixture, NativePlayerWireClient wire)
    {
        await wire.SendAsync(new LegacyMsgLoginGamePacket(Uid).ToFrameBytes(), fixture.Token);
        var dispatch = await fixture.NextAsync();
        Assert.Equal(LegacyMsgLoginGamePacket.Opcode, dispatch.Opcode);
        return dispatch;
    }

    private static async Task WithHandlerAsync(
        PrefixInput input,
        Func<NativePlayerLoopback, WorldServerFrameHandler, NativePlayerDiagnosticLog, Task> body,
        ConquerTcpWireMode mode = ConquerTcpWireMode.LegacyGameServer,
        AccountHandoffService? handoff = null)
    {
        using var beastMetadata = new NativeLoginBeastMetadataScope(1000);
        using var log = new NativePlayerDiagnosticLog();
        var handler = new WorldServerFrameHandler(new CharacterSnapshotLoader(input.Users), handoff, log: log);
        await NativePlayerLoopback.RunAsync(mode,
            fixture => body(fixture, handler, log),
            handler.HandleFrameAsync, handler.ReadCurrentSessionForNativeOwnerObservation);
        log.AssertNoWorldRuntimeDiagnostics();
    }

    private sealed class PrefixInput : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "wolf-login-local-prefix-" + Guid.NewGuid().ToString("N"));
        internal string Users => Path.Combine(root, "Users");
        internal string FilePath => Path.Combine(Users, Uid.ToString(CultureInfo.InvariantCulture) + ".ini");
        internal byte[] InitialBytes { get; }
        internal byte[] ExpectedMarkerBytes { get; }

        internal PrefixInput(string? relic, bool create = true, long storedUid = Uid, bool malformedLevel = false)
        {
            Directory.CreateDirectory(Users);
            var lines = new List<string>
            {
                "[Character]", "UID=" + storedUid.ToString(CultureInfo.InvariantCulture), "Name=PrefixOwner",
                "Body=1008", "Face=154", "Class=7051", "Map=1015", "X=830", "Y=584",
                "PMap=1000", "PMapX=500", "PMapY=650", malformedLevel ? "Level=bad" : "Level=140",
                "Experience=0", "Reborn=2", "FirstClass=0", "SecoundeClass=7050", "Haire=27",
                "Agility=36", "Strength=0", "Vitaliti=447", "Spirit=0", "Atributes=12",
                "MinHitPoints=10836", "MinMana=5000", "Money=1000", "ConquerPoints=0",
                "BankCps=0", "BoundConquerPoints=0", "PkPoints=0", "Online=7", "Angle=1",
                "Day=" + DateTime.Now.DayOfYear.ToString(CultureInfo.InvariantCulture),
            };
            if (relic is not null) lines.Add("RelicSpirit=" + relic);
            lines.AddRange(["", "[Unrelated]", "Keep=unchanged"]);
            var text = string.Join("\r\n", lines) + "\r\n";
            InitialBytes = Encoding.UTF8.GetBytes(text);
            ExpectedMarkerBytes = Encoding.UTF8.GetBytes(text.Replace("Online=7\r\n", "Online=1\r\n", StringComparison.Ordinal));
            if (create) File.WriteAllBytes(FilePath, InitialBytes);
        }

        public void Dispose()
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true); // Only this owned GUID fixture.
        }
    }
}
