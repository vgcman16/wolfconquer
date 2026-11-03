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
    public async Task HostileGlobalIniConverterIsNeverInvokedAndCannotMutateRealPrefixOwners(string mutation)
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
            client.Beasts = beasts; client.HairfaceStorage = storage;
            var before = ObservePrefixPlayer(player);
            var snapshot = ReadPrefixSnapshot(input);
            var originalParser = LegacyNativeItemCounterShellIni.ToUInt32;
            var invoked = false;
            try
            {
                // Install the real historical global seam, with genuinely hostile effects.
                // The fixed prefix must perform its own invariant conversion and never call it.
                LegacyNativeItemCounterShellIni.ToUInt32 = text =>
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
                    return originalParser(text);
                };
                Assert.True(LegacyNativeExistingCharacterPrefix.TryPrepare(birth.Connection, snapshot, input.Users,
                    out var prepared, out var reason), reason);
                Assert.NotNull(prepared);
                Assert.False(invoked);
                Assert.Equal(before, ObservePrefixPlayer(player));
                Assert.Same(beasts, client.Beasts); Assert.Same(storage, client.HairfaceStorage);
                Assert.Equal(17U, beasts.Points); Assert.Equal(19U, beasts.flag); Assert.Equal((byte)3, beasts.FixedLevel);
                Assert.Empty(storage.Objects);

                // A genuine exact file-prefix commit still succeeds; it cannot borrow the converter.
                Assert.True(prepared!.TryCommit(out var updated, out reason), reason);
                Assert.False(invoked);
                Assert.Equal(Uid, player.UID); Assert.Equal(1U, player.Online);
                Assert.Equal(1234567U, player.DeityLandLuckyPoints);
                Assert.Same(beasts, client.Beasts); Assert.Same(storage, client.HairfaceStorage);
                Assert.Equal(1L, updated.Online);
                Assert.Equal(System.Text.Encoding.UTF8.GetBytes(System.Text.Encoding.UTF8.GetString(bytes)
                    .Replace("Online=7\r\n", "Online=1\r\n", StringComparison.Ordinal)), File.ReadAllBytes(input.FilePath));
            }
            finally { LegacyNativeItemCounterShellIni.ToUInt32 = originalParser; }
            Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection));
            Assert.DoesNotContain("legacy MsgLoginGame Success", log.Text);
        });
    }

    [Fact]
    public async Task HostileGlobalIniConverterCannotReplaceCapturedFileDuringReadOnlyPreparation()
    {
        using var input = new LoginInput();
        var bytes = WritePrefixFields(input, "DeityLandLuckyPoints=1234567\r\n");
        await WithHandlerAsync(input, async (fixture, handler, _) =>
        {
            var (_, birth) = await ConnectBornAsync(fixture);
            var client = birth.Connection.NativeGameClient!;
            var before = ObservePrefixPlayer(client.Player);
            var snapshot = ReadPrefixSnapshot(input);
            var originalParser = LegacyNativeItemCounterShellIni.ToUInt32;
            var invoked = false;
            try
            {
                LegacyNativeItemCounterShellIni.ToUInt32 = text =>
                {
                    invoked = true;
                    var alternate = bytes.ToArray(); alternate[^3] ^= 1;
                    File.WriteAllBytes(input.FilePath, alternate);
                    return originalParser(text);
                };
                Assert.True(LegacyNativeExistingCharacterPrefix.TryPrepare(birth.Connection, snapshot, input.Users,
                    out var prepared, out var reason), reason);
                Assert.NotNull(prepared); Assert.False(invoked);
                Assert.Equal(bytes, File.ReadAllBytes(input.FilePath));
                Assert.Equal(before, ObservePrefixPlayer(client.Player));
                Assert.Null(client.Beasts); Assert.Null(client.HairfaceStorage);
            }
            finally { LegacyNativeItemCounterShellIni.ToUInt32 = originalParser; }
            Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection));
            // Non-invocation is the witness here, not an assertion that a callback tested the file lease.
        });
    }
}
