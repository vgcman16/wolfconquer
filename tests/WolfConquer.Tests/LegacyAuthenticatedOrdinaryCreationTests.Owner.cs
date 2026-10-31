using System.Globalization;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyAuthenticatedOrdinaryCreationTests
{
    [Theory]
    [InlineData(254, true)] [InlineData(255, false)]
    public void DataOnlyNameReadBudgetChargesBothOwnCharacterAndItsPossibleStage(int unrelatedMebibytes, bool fits)
    {
        // DATA ONLY arithmetic boundary: each dictionary entry represents one
        // complete read. Shared array identity must not deduplicate that charge.
        // This creates no lease, saved Character, permit, UID work or journal.
        var mebibyte = new byte[1024 * 1024];
        var images = Enumerable.Range(0, unrelatedMebibytes).ToDictionary(index => index.ToString(CultureInfo.InvariantCulture),
            _ => mebibyte, StringComparer.Ordinal);
        var scan = new LegacyCharacterCreationNameClaim.NameScan(images, JournalPresent: true);
        if (fits) LegacyCharacterCreationNameClaim.RequireProjectedNameBudget(scan, mebibyte);
        else Assert.Throws<InvalidDataException>(() => LegacyCharacterCreationNameClaim.RequireProjectedNameBudget(scan, mebibyte));
    }

    [Fact]
    public Task FreshUidAlreadyDecodedFromAnotherAccountLeafRefusesBeforeIntent() => Run(async f =>
    {
        Assert.True(f.Runtime.HasAuthenticatedCreationPermit(f.Handler, f.Connection));
        await AssertCreationOwnerPreflightRefused(f, "NewOwner");
    }, beforeBootstrap: f =>
    {
        // Declared negative saved input, before bootstrap. This is not a
        // fabricated producer/history or a positive new Character grant.
        var text = File.ReadAllText(f.Data.UserPath).Replace("UID=" + CreationFixture.RuneActor + "\r\n",
            "UID=" + CreationFixture.Account + "\r\n", StringComparison.Ordinal)
            .Replace("Name=OwnedStartup\r\n", "Name=DifferentName\r\n", StringComparison.Ordinal);
        File.WriteAllText(Path.Combine(f.Data.Users, (CreationFixture.Account + 7).ToString(CultureInfo.InvariantCulture) + ".ini"),
            text, new UTF8Encoding(true));
    });

    [Fact]
    public Task ExistingCompleteNameCollidesByOrdinalIgnoreCaseRatherThanHash() => Run(async f =>
    {
        Assert.True(f.Runtime.HasAuthenticatedCreationPermit(f.Handler, f.Connection));
        await AssertCreationOwnerPreflightRefused(f, "OWNEDSTARTUP");
    });

    [Theory]
    [InlineData((int)LegacyTradeJournalFileKind.Character)] [InlineData((int)LegacyTradeJournalFileKind.Items)]
    [InlineData((int)LegacyTradeJournalFileKind.Spells)] [InlineData((int)LegacyTradeJournalFileKind.Proficiencies)]
    [InlineData((int)LegacyTradeJournalFileKind.Quests)] [InlineData((int)LegacyTradeJournalFileKind.CombatGear)]
    [InlineData((int)LegacyTradeJournalFileKind.Mailbox)]
    public Task EachNewAccountParticipantAppearingAfterRealPermitRefusesWithoutRemovingEvidence(int kindValue) => Run(async f =>
    {
        var kind = (LegacyTradeJournalFileKind)kindValue;
        Assert.True(f.Runtime.HasAuthenticatedCreationPermit(f.Handler, f.Connection));
        var path = kind == LegacyTradeJournalFileKind.Mailbox ? Path.Combine(f.Data.Mailbox, CreationFixture.Account + ".bin") : f.DataPath(kind);
        // Explicit external-corruption negative after the genuine permit. No
        // handwritten work/receipt is accepted and this leaf must be preserved.
        byte[] bytes = kind switch
        {
            LegacyTradeJournalFileKind.Character => new UTF8Encoding(true).GetBytes(File.ReadAllText(f.Data.UserPath)
                .Replace("UID=" + CreationFixture.RuneActor + "\r\n", "UID=" + CreationFixture.Account + "\r\n", StringComparison.Ordinal)
                .Replace("Name=OwnedStartup\r\n", "Name=OccupiedLeaf\r\n", StringComparison.Ordinal)),
            LegacyTradeJournalFileKind.CombatGear => File.ReadAllBytes(Path.Combine(f.Data.CombatGear, CreationFixture.RuneActor + ".ini")),
            _ => new byte[4],
        };
        File.WriteAllBytes(path, bytes);
        await AssertCreationOwnerPreflightRefused(f, "NewOwner");
        Assert.Equal(bytes, File.ReadAllBytes(path));
    });

    [Fact]
    public Task ActualSnapshotErrorIsNotTheTypedMissingSnapshotThatCanIssuePermit() => Run(async f =>
    {
        // A malformed externally appearing Character is a negative file input,
        // not a missing snapshot and not permission to repair/create it.
        File.WriteAllText(f.CharacterPath, "[Character]\r\nUID=" + CreationFixture.Account + "\r\n", new UTF8Encoding(false));
        var ticket = await f.AuthenticateAsync(); await f.EnterAsync(ticket);
        Assert.True(PacketReader.TryRead(Assert.Single(f.Sent), out var response));
        Assert.Equal(WorldSnapshotWireStatus.SnapshotError, WorldSnapshotResponsePacket.ReadStatus(response));
        Assert.False(f.Runtime.HasAuthenticatedCreationPermit(f.Handler, f.Connection));
        f.Sent.Clear(); await AssertNoCreation(f);
    }, enter: false);

    [Fact]
    public Task ARealPermitCannotMoveToAnotherConnection() => Run(async f =>
    {
        var other = f.NewConnection(); var files = f.Data.Files();
        Assert.True(f.Runtime.HasAuthenticatedCreationPermit(f.Handler, f.Connection));
        Assert.False(f.Runtime.HasAuthenticatedCreationPermit(f.Handler, other));
        await f.SendOnAsync(other, new LegacyNewRolePacket("NewOwner", 1008, f.Selection).ToFrameBytes());
        f.Data.AssertFiles(files); Assert.Empty(f.Sent); Assert.False(f.TryCurrent(out _));
        Assert.True(f.Runtime.HasAuthenticatedCreationPermit(f.Handler, f.Connection));
    });

    [Fact]
    public Task ReplacingTheActualPreCharacterPlayerReferenceInvalidatesPermit() => Run(async f =>
    {
        var old = f.Native.Player; f.Native.Player = new LegacyNativePlayerRuntime(f.Native);
        Assert.NotSame(old, f.Native.Player); Assert.Equal(0u, f.Native.Player.UID);
        Assert.False(f.Runtime.HasAuthenticatedCreationPermit(f.Handler, f.Connection));
        await AssertNoCreation(f);
    });

    [Fact]
    public Task ARealExistingCharacterAdmissionOnTheConnectionInvalidatesTheBirthPermit() => Run(async f =>
    {
        await f.SendAsync(new LegacyMsgLoginGamePacket(CreationFixture.RuneActor).ToFrameBytes());
        var accepted = f.Current;
        Assert.Equal((long)CreationFixture.RuneActor, accepted.Character.Uid);
        Assert.False(f.Runtime.HasAuthenticatedCreationPermit(f.Handler, f.Connection));
        f.Sent.Clear(); var files = f.Data.Files();
        await f.CreateAsync(); f.Data.AssertFiles(files); Assert.Empty(f.Sent);
        Assert.Same(accepted, f.Current); Assert.False(File.Exists(f.CharacterPath));
    });

    [Fact]
    public Task ActualEofBeforeAnyRegistrationCannotBecomeALiveCreationConnection() => Run(async f =>
    {
        await f.Connection.CompleteTransportCloseAsync();
        var ticket = await f.AuthenticateAsync(); await f.EnterAsync(ticket);
        AssertMissingCreationReply(f);
        Assert.False(f.Runtime.HasAuthenticatedCreationPermit(f.Handler, f.Connection));
        f.Sent.Clear(); await AssertNoCreation(f);
    }, enter: false);

    [Fact]
    public Task ActualEofAfterTheRealPermitPreventsCreation() => Run(async f =>
    {
        Assert.True(f.Runtime.HasAuthenticatedCreationPermit(f.Handler, f.Connection));
        await f.Connection.CompleteTransportCloseAsync();
        Assert.False(f.Runtime.HasAuthenticatedCreationPermit(f.Handler, f.Connection));
        await AssertNoCreation(f);
    });

    [Fact]
    public Task ActualEofDuringMissingSnapshotReplyCannotRaceIntoPermitInstallation() => Run(async f =>
    {
        Task? closed = null;
        f.BeforeSend = (account, bytes) =>
        {
            var frame = CreationFixture.ReadFrame(bytes);
            if (account == CreationFixture.Account && frame.Header.Opcode == WorldPacketOpcodes.InternalSnapshotResponse)
            {
                Assert.True(PacketReader.TryRead(bytes, out var response));
                Assert.Equal(WorldSnapshotWireStatus.MissingSnapshot, WorldSnapshotResponsePacket.ReadStatus(response));
                closed ??= f.Connection.CompleteTransportCloseAsync();
            }
        };
        var ticket = await f.AuthenticateAsync(); await f.EnterAsync(ticket);
        f.BeforeSend = null; Assert.NotNull(closed); await closed!;
        Assert.False(f.Runtime.HasAuthenticatedCreationPermit(f.Handler, f.Connection));
        f.Sent.Clear(); await AssertNoCreation(f);
    }, enter: false);

    [Fact]
    public Task AnotherActualFramesInstalledCloseCallbackCannotBeBorrowedByTheCreator() => Run(async f =>
    {
        var foreign = new WorldServerFrameHandler(snapshotLoader: null,
            protocolProfile: ConquerProtocolProfile.VisibleDx8D13, log: TextWriter.Null);
        await foreign.HandleFrameAsync(f.Connection, CreationFixture.ReadFrame(new InternalWorldSnapshotRequestPacket(CreationFixture.Account).ToFrameBytes()));
        f.Sent.Clear(); var ticket = await f.AuthenticateAsync(); await f.EnterAsync(ticket);
        AssertMissingCreationReply(f);
        Assert.False(f.Runtime.HasAuthenticatedCreationPermit(f.Handler, f.Connection));
        f.Sent.Clear(); await AssertNoCreation(f);
    }, enter: false);

    [Fact]
    public Task StoppingTheActualRuntimeDoesNotLeaveAUsablePermit() => Run(async f =>
    {
        await f.Runtime.DisposeAsync();
        Assert.False(f.Runtime.HasAuthenticatedCreationPermit(f.Handler, f.Connection));
        await AssertNoCreation(f);
    });

    private static async Task AssertCreationOwnerPreflightRefused(CreationFixture f, string name)
    {
        var files = f.Data.Files();
        Assert.False(f.Runtime.TryEnterAuthenticatedCreation(f.Handler, f.Connection, name, out var lease, out var reason));
        Assert.Null(lease); Assert.Contains("authenticatedCreationPreflightRefused", reason, StringComparison.Ordinal);
        await f.CreateAsync(name); f.Data.AssertFiles(files); Assert.Empty(f.Sent);
        Assert.False(f.TryCurrent(out _)); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        Assert.True(f.Runtime.HasAuthenticatedCreationPermit(f.Handler, f.Connection)); // Preflight never consumes it.
    }

    private static async Task AssertNoCreation(CreationFixture f)
    {
        var files = f.Data.Files(); var native = f.Native; var player = native.Player;
        await f.CreateAsync(); f.Data.AssertFiles(files); Assert.Empty(f.Sent);
        Assert.False(f.TryCurrent(out _)); Assert.Same(native, f.Native); Assert.Same(player, native.Player);
        Assert.Equal(0u, native.Player.UID); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
    }

    private static void AssertMissingCreationReply(CreationFixture f)
    {
        Assert.True(PacketReader.TryRead(Assert.Single(f.Sent), out var response));
        Assert.Equal(WorldSnapshotWireStatus.MissingSnapshot, WorldSnapshotResponsePacket.ReadStatus(response));
    }
}
