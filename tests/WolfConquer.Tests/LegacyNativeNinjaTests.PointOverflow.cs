namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaTests
{
    [Theory]
    [InlineData(uint.MaxValue, 1U)]
    [InlineData(uint.MaxValue - 49U, 50U)]
    public Task LegacyPointOverflowRefusesBeforeMutatingOrOpeningTransport(uint current, uint points) =>
        NativePacketTestThread.Run(() =>
        {
            using var tables = new Tables();
            var (client, ninja) = MailboxNinja();
            ninja.Exp = current;
            var failure = Record.Exception(() => ninja.AddPoints(points));
            Assert.Equal(current, ninja.Exp);
            Assert.IsType<OverflowException>(failure);
            Assert.Null(client.Socket);
        });
}
