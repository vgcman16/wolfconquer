using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaMutationTests
{
    [Fact]
    public Task CompleteOrdinaryLoadRetainsIndependentSaveAndNativeUserWireGoldens() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            const string saved = "1/2/3/4/5/6/7/8/9/10/11/12/777/14/15/16/17/18/19/1/100/1/17/1000/2000/";
            scope.Meta(700, 100, 1, 20);
            scope.Ninja.Load(saved);
            Assert.Empty(scope.Queue);
            Assert.Equal(112U, scope.Ninja.Score); // Stored777 is not trusted as derived score.
            Assert.Equal(saved.Replace("/777/", "/112/", StringComparison.Ordinal), scope.Ninja.ToString());
            var scalars = ProducerScalarState(scope.Ninja);
            var items = scope.Ninja.Items;
            var item = Assert.Single(items).Value;
            scope.Client.Player.UID = 444;

            LegacyNativeMsgNinja.MsgNinjaUser.Create(scope.Client, scope.Ninja, 123);

            var expected = Convert.FromHexString(
                "34005B090800107B180120022803300438054006480750085809600A680B700C787080010E88010F900110980111A00112A80113");
            Assert.Single(scope.Queue);
            scope.Sent(expected);
            Assert.Equal(scalars, ProducerScalarState(scope.Ninja));
            Assert.Same(items, scope.Ninja.Items);
            Assert.Same(item, Assert.Single(items).Value);
            Assert.Equal(444U, scope.Client.Player.UID);
            Assert.Equal(saved.Replace("/777/", "/112/", StringComparison.Ordinal), scope.Ninja.ToString());
            Assert.Empty(scope.Log);
        });
}
