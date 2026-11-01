using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacySecondaryPasswordRuntimeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ForeignAmbiguousUidCannotAdmitAConflictingOrUnprovableAccount(bool unreadable)
    {
        const uint foreign = 1_297_201, target = 1_297_202;
        Seed(foreign, "0,0,0");
        Seed(target, "0,0,0", 20);
        File.AppendAllText(UserPath(foreign), unreadable ? "UID=invalid\n" : $"UID={target}\n");
        var foreignBytes = File.ReadAllBytes(UserPath(foreign));
        var targetBytes = File.ReadAllBytes(UserPath(target));
        var targetItems = File.ReadAllBytes(ItemPath(target));
        var owner = NewOwner();
        var player = CreateConnection(target);

        await owner.Handler.HandleFrameAsync(player.Connection,
            ReadFrame(new LegacyMsgLoginGamePacket(target).ToFrameBytes()), player.Lifetime.Token);

        Assert.DoesNotContain(player.Sent, IsAnswerOk);
        Assert.False(owner.Sessions.TryGet(player.Connection, out _));
        Assert.Equal(0U, player.NativeClient.Player.UID);
        Assert.False(player.NativeClient.Player.IsCheckedPass);
        Assert.Equal(foreignBytes, File.ReadAllBytes(UserPath(foreign)));
        Assert.Equal(targetBytes, File.ReadAllBytes(UserPath(target)));
        Assert.Equal(targetItems, File.ReadAllBytes(ItemPath(target)));
    }
}
