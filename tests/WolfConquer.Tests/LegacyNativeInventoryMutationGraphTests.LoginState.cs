using System.Reflection;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeInventoryMutationGraphTests
{
    [Fact]
    public void ServerFlagRetainsEveryLiteralNameUshortWidthAndFlagsMetadata()
    {
        Assert.Equal(typeof(ushort), Enum.GetUnderlyingType(typeof(LegacyNativeServerFlag)));
        Assert.True(typeof(LegacyNativeServerFlag).IsDefined(typeof(FlagsAttribute), inherit: false));
        Assert.Equal(new[]
        {
            "None", "AcceptLogin", "CreateCharacter", "CreateCharacterSucces", "LoginFull",
            "SetLocation", "OnLoggion", "QueuesSave", "RemoveSpouse", "Disconnect", "UpdateSpouse"
        }, Enum.GetNames<LegacyNativeServerFlag>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    [InlineData(16)]
    [InlineData(32)]
    [InlineData(64)]
    [InlineData(128)]
    [InlineData(256)]
    [InlineData(512)]
    [InlineData(65535)]
    public Task ActualClientFlagIsDirectReplaceableStorageWithoutQueueOrDeleteEffects(int bits) =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            Assert.Equal(LegacyNativeServerFlag.None, scope.Client.ClientFlag);
            Assert.False(scope.Player.Delete);
            scope.Client.ClientFlag = (LegacyNativeServerFlag)bits;
            Assert.Equal((ushort)bits, (ushort)scope.Client.ClientFlag);
            Assert.False(scope.Player.Delete);
            Assert.False(scope.Client.FullLoading);
            Assert.Empty(scope.Queue);
        });

    [Fact]
    public Task LoginRecordAndOnLoginFieldRetainNativePhysicalDefaultsAndAliases() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            var original = scope.Client.OnLogin;
            Assert.Equal((ushort)0, original.Length);
            Assert.Equal((ushort)0, original.PacketID);
            Assert.Equal(0u, original.AccountHash);
            Assert.Equal(0u, original.Key);
            Assert.Equal(0u, original.HDSerial);
            Assert.Null(original.MachineName);
            Assert.Null(original.MacAddress);
            Assert.Null(original.TokenLogin);
            Assert.Null(original.Language);
            string value = new string('x', 3);
            scope.Client.OnLogin = new LegacyNativeMsgLoginClient
            {
                Length = ushort.MaxValue, PacketID = 2065, AccountHash = uint.MaxValue, Key = 700,
                HDSerial = 900, MachineName = value, MacAddress = value, TokenLogin = value, Language = value
            };
            Assert.Equal(700u, scope.Client.OnLogin.Key);
            Assert.Equal(uint.MaxValue, scope.Client.OnLogin.AccountHash);
            Assert.Same(value, scope.Client.OnLogin.MachineName);
            Assert.Same(value, scope.Client.OnLogin.MacAddress);
            Assert.Same(value, scope.Client.OnLogin.TokenLogin);
            Assert.Same(value, scope.Client.OnLogin.Language);
            Assert.Equal(0u, original.Key); // Struct replacement did not mutate held old value.
            Assert.Equal(LegacyNativeServerFlag.None, scope.Client.ClientFlag);
            Assert.Empty(scope.Queue);
        });

    [Fact]
    public void LoginRecordPreservesAllNineFieldDeclarationsInNativeOrder()
    {
        Assert.Equal(new[]
        {
            "Length", "PacketID", "AccountHash", "Key", "HDSerial",
            "MachineName", "MacAddress", "TokenLogin", "Language"
        }, typeof(LegacyNativeMsgLoginClient).GetFields(BindingFlags.Instance | BindingFlags.Public)
            .OrderBy(field => field.MetadataToken).Select(field => field.Name));
    }

    [Fact]
    public Task ExplicitDeleteFlagDoesNotDispatchOrDisconnectAnythingByItself() =>
        NativePacketTestThread.Run(() =>
        {
            using var scope = new Scope();
            scope.Player.Delete = true;
            Assert.True(scope.Player.Delete);
            Assert.True(scope.Client.Socket!.Alive);
            Assert.Equal(LegacyNativeServerFlag.None, scope.Client.ClientFlag);
            Assert.Empty(scope.Queue);
        });
}
