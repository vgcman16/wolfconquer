using System.Runtime.CompilerServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedBasicPhysicalAutoAttackTests
{
    [Fact]
    public Task ReducedTeleportDestinationCommitsFullLocationAndCancelsBeforeTheFirstExistingTransition() => Run(async f =>
    {
        await f.HitAsync(2000, 1700, repeat: false); f.Clear();
        var before = CaptureTeleportBefore(f); var expected = TeleportLocationImage(f, before);
        var destination = new LegacyTeleportDestination(1002, 410, 354);
        f.BeforeSend = (_, _) => AssertTeleportCommitted(f, before, expected, destination, 1700);
        WorldSession? result;
        try { result = await ExecuteOwnedDestinationAsync(f, destination); }
        finally { f.BeforeSend = null; }
        Assert.Same(f.Current(), result); AssertTeleportCommitted(f, before, expected, destination, 1700);
        AssertTeleportCoreWire(f, 1002, 410, 354, entireOwnerOutput: true);
        // The durable phase preserves the saved DynamicID spelling/value.
        string path = Path.GetRelativePath(f.Owner.Data.Root, f.Owner.CharacterPath(Actor));
        var saved = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(expected[path], out _));
        Assert.Equal("1", saved.Attributes["DynamicID"]); Assert.Equal("0", f.Current().Character.Attributes["DynamicID"]);
        await AssertTeleportRepeatStaysCancelledAsync(f, before, expected, destination, 1700);
    });

    [Fact]
    public Task ActualDestinationCharacterReadLeaseRefusesWithoutCancellingAndSameClockRetryCommits() => Run(async f =>
    {
        await f.HitAsync(2000, 1700, repeat: false); f.Clear();
        var before = CaptureTeleportBefore(f); var destination = new LegacyTeleportDestination(1002, 410, 354);
        var now = f.Clock.GetUtcNow();
        using (var lease = new FileStream(f.Owner.CharacterPath(Actor), FileMode.Open, FileAccess.Read, FileShare.Read))
        {
            Assert.True(lease.CanRead); Assert.Null(await ExecuteOwnedDestinationAsync(f, destination));
            f.AssertFiles(before.Files); Assert.Same(before.ActorSession, f.Current()); Assert.Same(before.TargetSession, f.Current(Target));
            Assert.Empty(f.Order); f.AssertCoherent(1700); Assert.True(before.ActorBinding.Client.OnAutoAttack);
            Assert.Same(before.Descriptor, before.ActorBinding.Client.AutoAttack);
            Assert.Equal(before.DescriptorValues, ReadTeleportDescriptor(before.ActorBinding.Client.AutoAttack!));
            Assert.Equal(before.StampBits, Unsafe.BitCast<DateTime, long>(before.ActorBinding.Player.AttackStamp));
            Assert.Equal(before.Px, before.ActorBinding.Player.Px); Assert.Equal(before.Py, before.ActorBinding.Player.Py);
            Assert.True(f.Owner.Handler.TryGetBasicPhysicalRetirementSnapshot(before.ActorBinding, out var unchanged));
            Assert.Same(before.Native, unchanged); AssertTeleportGraphsUnchanged(f, before);
        }
        Assert.Equal(now, f.Clock.GetUtcNow());
        var expected = TeleportLocationImage(f, before);
        f.BeforeSend = (_, _) => AssertTeleportCommitted(f, before, expected, destination, 1700);
        try
        {
            var retried = await ExecuteOwnedDestinationAsync(f, destination);
            Assert.Same(f.Current(), retried);
        }
        finally { f.BeforeSend = null; }
        AssertTeleportCommitted(f, before, expected, destination, 1700);
        AssertTeleportCoreWire(f, 1002, 410, 354, entireOwnerOutput: true);
        await AssertTeleportRepeatStaysCancelledAsync(f, before, expected, destination, 1700);
    });

    [Fact]
    public Task SameCellReducedTeleportPublishesBothCursorsAndCancelsANearbyAcceptedRepeatWithoutChangingSavedBytes() => Run(async f =>
    {
        var files = f.Files();
        await f.Owner.SendAsync(Actor, new MsgMovementPacket { Uid = Actor, Direction = 7, Running = MsgMovementMode.Walk }.ToFrameBytes());
        string path = Path.GetRelativePath(f.Owner.Data.Root, f.Owner.CharacterPath(Actor));
        var text = CharacterSnapshotFileEncoding.Decode(files[path], out var encoding);
        text = ReplaceTeleportLine(ReplaceTeleportLine(text, "X", 830, 831), "Y", 584, 585);
        files[path] = encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray(); f.AssertFiles(files);
        Assert.Equal((ushort)830, f.Binding().Player.Px); Assert.Equal((ushort)584, f.Binding().Player.Py);
        f.Clock.Advance(OrdinaryDelay); f.Clear();
        await f.HitAsync(2000, 1700, repeat: false); f.Clear();
        var before = CaptureTeleportBefore(f); var destination = new LegacyTeleportDestination(1000, 831, 585, 1);
        Assert.Equal((ushort)830, before.Px); Assert.Equal((ushort)584, before.Py);
        f.BeforeSend = (_, _) => AssertTeleportCommitted(f, before, before.Files, destination, 1700);
        WorldSession? result;
        try { result = await ExecuteOwnedDestinationAsync(f, destination); }
        finally { f.BeforeSend = null; }
        Assert.Same(f.Current(), result); AssertTeleportCommitted(f, before, before.Files, destination, 1700);
        Assert.Equal((ushort)831, f.Binding().Player.Px); Assert.Equal((ushort)585, f.Binding().Player.Py);
        AssertTeleportCoreWire(f, 1000, 831, 585, entireOwnerOutput: false);
        // The real target remains one tile away in the same live scope. A
        // range/map miss cannot conceal a stale repeat generation here.
        Assert.Equal(1000L, f.Current(Target).Character.MapId); Assert.Equal("1", f.Current(Target).Character.Attributes["DynamicID"]);
        Assert.Equal((831L, 584L), (f.Current(Target).Character.X, f.Current(Target).Character.Y));
        await AssertTeleportRepeatStaysCancelledAsync(f, before, before.Files, destination, 1700);
    });
}
