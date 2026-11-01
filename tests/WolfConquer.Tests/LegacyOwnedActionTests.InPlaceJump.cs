using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedActionTests
{
    [Fact]
    public Task RealInPlaceJumpAfterADiagonalWalkPublishesBothSourceCursorsWithoutChangingAnySavedByte() => Run(async f =>
    {
        // Actual direction7 advances BOTH coordinates. The later in-place
        // jump must update both old cursors even though neither position moves.
        var walked = ExpectedCoordinates(f, f.Data.Files(), (Uid, 430, 380, 431, 381));
        await f.SendAsync(new MsgMovementPacket { Direction = 7, Uid = 999,
            Running = MsgMovementMode.Walk, Timestamp = 123456, MapId = 999 }.ToFrameBytes());
        AssertFilesAndOwner(f, walked, 0);
        Assert.Equal(431L, f.Current.Character.X); Assert.Equal(381L, f.Current.Character.Y);
        Assert.Equal((ushort)430, f.Native.Player!.Px); Assert.Equal((ushort)380, f.Native.Player.Py);
        Assert.Equal(DiagonalWalkGolden(), Assert.Single(f.Sent));
        Assert.Equal(f.Sent[0], Assert.Single(f.PeerSent));
        ClearOutput(f);

        var before = f.Data.Files(); var peer = f.Get(Peer); var stamp = f.Native.Player.AttackStamp;
        f.BeforeSend = (_, _) =>
        {
            AssertFilesAndOwner(f, before, 100);
            Assert.Equal(431L, f.Current.Character.X); Assert.Equal(381L, f.Current.Character.Y);
            Assert.Equal((ushort)431, f.Native.Player.Px); Assert.Equal((ushort)381, f.Native.Player.Py);
            Assert.Equal((ushort)431, f.Native.Player.X); Assert.Equal((ushort)381, f.Native.Player.Y);
            Assert.Equal(stamp, f.Native.Player.AttackStamp);
            Assert.Same(peer, f.Get(Peer));
        };
        try { await f.JumpAsync(431, 381); } finally { f.BeforeSend = null; }
        AssertFilesAndOwner(f, before, 100);
        Assert.Equal((ushort)431, f.Native.Player.Px); Assert.Equal((ushort)381, f.Native.Player.Py);
        Assert.Equal(stamp, f.Native.Player.AttackStamp); Assert.Same(peer, f.Get(Peer));
        Assert.Equal(InPlaceJumpGolden(), Assert.Single(f.Sent));
        Assert.Equal(f.Sent[0], Assert.Single(f.PeerSent));
        Assert.Equal(new[] { Uid, Peer }, f.Order.Select(row => row.Recipient));
        Assert.Equal((ushort)0, f.Native.Player.Stamina); Assert.Null(f.Current.MagicCooldowns);
        // No accepted attack/worker is manufactured merely to claim that its
        // cancellation ran. Genuine armed cancellation belongs to the AA tests.
    });

    private static byte[] DiagonalWalkGolden()
    {
        var body = new List<byte>(); Field(body, 1, 7); Field(body, 2, Uid); Field(body, 3, 0);
        Field(body, 4, 123456); Field(body, 5, 1002); return Frame(2205, body);
    }

    private static byte[] InPlaceJumpGolden()
    {
        var body = new List<byte>(); Field(body, 1, Uid); Field(body, 7, 431); Field(body, 8, 381);
        Field(body, 9, 123456); Field(body, 12, 137); Field(body, 13, 6); Field(body, 14, 431); Field(body, 15, 381);
        Field(body, 17, 1002); return Frame(2046, body);
    }
}
