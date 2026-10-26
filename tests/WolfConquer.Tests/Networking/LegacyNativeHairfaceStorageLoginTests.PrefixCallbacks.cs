using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHairfaceStorageLoginTests
{
    [Theory]
    [InlineData("online")]
    [InlineData("uid")]
    [InlineData("beast-points")]
    [InlineData("beast-slot")]
    [InlineData("hairface-slot")]
    public async Task GenuineIniConverterCannotBlessChangedNativePrefixOwners(string mutation)
    {
        using var input = new LoginInput();
        var bytes = WritePrefixFields(input, "DeityLandLuckyPoints=1234567\r\n");
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (_, birth) = await ConnectBornAsync(fixture);
            var client = birth.Connection.NativeGameClient!;
            var player = client.Player;
            var beasts = new LegacyNativeBeastsRuntime(client) { Points = 17, flag = 19, FixedLevel = 3 };
            var storage = new LegacyNativeHairfaceStorageRuntime(client);
            client.Beasts = beasts;
            client.HairfaceStorage = storage;
            var snapshot = ReadPrefixSnapshot(input);
            var originalParser = LegacyNativeItemCounterShellIni.ToUInt32;
            var invoked = false;
            PrefixPlayerState? actorPlayer = null;
            LegacyNativeBeastsRuntime? actorBeasts = null;
            LegacyNativeHairfaceStorageRuntime? actorHairface = null;
            try
            {
                // This is the real native INI converter seam, not an accepting
                // persistence callback or a production fault-injection hook.
                LegacyNativeItemCounterShellIni.ToUInt32 = text =>
                {
                    var value = originalParser(text);
                    if (text == "1234567" && !invoked)
                    {
                        invoked = true;
                        switch (mutation)
                        {
                            case "online": player.Online = 42; break;
                            case "uid": player.UID = 42; break;
                            case "beast-points": beasts.Points = 42; break;
                            case "beast-slot": client.Beasts = new(client); break;
                            case "hairface-slot": client.HairfaceStorage = new(client); break;
                        }
                        actorPlayer = ObservePrefixPlayer(player);
                        actorBeasts = client.Beasts;
                        actorHairface = client.HairfaceStorage;
                    }
                    return value; // Return the exact legitimate parsed value.
                };

                Assert.False(LegacyNativeExistingCharacterPrefix.TryPrepare(birth.Connection, snapshot, input.Users,
                    out var prepared, out _));
                Assert.Null(prepared);
            }
            finally { LegacyNativeItemCounterShellIni.ToUInt32 = originalParser; }

            Assert.True(invoked);
            Assert.Equal(actorPlayer, ObservePrefixPlayer(player));
            Assert.Same(actorBeasts, client.Beasts);
            Assert.Same(actorHairface, client.HairfaceStorage);
            Assert.Equal(mutation == "beast-points" ? 42U : 17U, beasts.Points);
            Assert.Equal(19U, beasts.flag);
            Assert.Equal((byte)3, beasts.FixedLevel);
            Assert.Empty(storage.Objects);
            Assert.Equal(bytes, File.ReadAllBytes(input.FilePath));
            Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection));
            Assert.DoesNotContain("legacy MsgLoginGame Success", log.Text);
        });
    }

    [Fact]
    public async Task NativeIniReadsHoldTheActualCapturedFileReadLease()
    {
        using var input = new LoginInput();
        var bytes = WritePrefixFields(input, "DeityLandLuckyPoints=1234567\r\n");
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (_, birth) = await ConnectBornAsync(fixture);
            var snapshot = ReadPrefixSnapshot(input);
            var originalParser = LegacyNativeItemCounterShellIni.ToUInt32;
            var invoked = false;
            IOException? writeRefusal = null;
            try
            {
                LegacyNativeItemCounterShellIni.ToUInt32 = text =>
                {
                    var value = originalParser(text);
                    if (text == "1234567" && !invoked)
                    {
                        invoked = true;
                        var alternate = bytes.ToArray();
                        alternate[^3] ^= 1;
                        try { File.WriteAllBytes(input.FilePath, alternate); }
                        catch (IOException error) { writeRefusal = error; }
                    }
                    return value;
                };
                Assert.True(LegacyNativeExistingCharacterPrefix.TryPrepare(birth.Connection, snapshot, input.Users,
                    out var prepared, out var reason), reason);
                Assert.NotNull(prepared);
                Assert.Equal(bytes, File.ReadAllBytes(input.FilePath));
                Assert.Equal(0U, birth.Connection.NativeGameClient!.Player.UID);
                Assert.Null(birth.Connection.NativeGameClient.Beasts);
                Assert.Null(birth.Connection.NativeGameClient.HairfaceStorage);
            }
            finally { LegacyNativeItemCounterShellIni.ToUInt32 = originalParser; }
            Assert.True(invoked);
            Assert.NotNull(writeRefusal);
            Assert.Equal(bytes, File.ReadAllBytes(input.FilePath));
            Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection));
        });
    }
}
