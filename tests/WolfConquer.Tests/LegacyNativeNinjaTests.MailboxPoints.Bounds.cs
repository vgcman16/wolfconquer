using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaTests
{
    [Fact]
    public void MailboxPointsOversizedSavedLineRefusesBeforeProjectionOrNativeMutation()
    {
        using var tables = new Tables();
        var (client, ninja) = MailboxNinja();
        var before = MailboxNinjaState(ninja);
        var oversized = MailboxNinjaLine + new string('0', 256 * 1024);
        Assert.False(ninja.TryPrepareMailboxPoints(client, oversized, 50, out var projection));
        Assert.Null(projection);
        Assert.Equal(before, MailboxNinjaState(ninja));
        Assert.Null(client.Socket);
    }

    [Fact]
    public void MailboxPointsTooManyNativeRowsRefusesBeforeProjectionOrNativeMutation()
    {
        using var tables = new Tables();
        var (client, ninja) = MailboxNinja();
        ninja.Items.Clear();
        for (uint index = 1; index <= 4097; index++)
            Assert.True(ninja.Items.TryAdd(index, new() { ItemID = index, Level = 1 }));
        var before = MailboxNinjaState(ninja);
        Assert.False(ninja.TryPrepareMailboxPoints(client, MailboxNinjaLine, 50, out var projection));
        Assert.Null(projection);
        Assert.Equal(before, MailboxNinjaState(ninja));
        Assert.Null(client.Socket);
    }
}
