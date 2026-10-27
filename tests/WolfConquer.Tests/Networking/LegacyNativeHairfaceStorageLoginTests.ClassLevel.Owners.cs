using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHairfaceStorageLoginTests
{
    [Theory]
    [InlineData("class")]
    [InlineData("level")]
    [InlineData("transformation")]
    public async Task PublishedPrefixProofRefusesChangedPhysicalClassLevelOrTransformation(string changed)
    {
        using var input = new LoginInput();
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (_, birth) = await ConnectBornAsync(fixture);
            var player = birth.Connection.NativeGameClient!.Player;
            Assert.True(LegacyNativeExistingCharacterPrefix.TryPrepare(birth.Connection, ReadPrefixSnapshot(input), input.Users,
                out var prepared, out var reason), reason);
            Assert.True(prepared!.TryCommit(out _, out reason), reason);
            Assert.True(prepared.IsPublishedAdmissionOwnerCurrent());
            var committed = File.ReadAllBytes(input.FilePath);
            switch (changed)
            {
                case "class": SetClassLevelField(player, "_class", 7052U); break;
                case "level": SetClassLevelField(player, "_level", (ushort)139); break;
                case "transformation": player.TransformationID = 23; break;
            }
            Assert.False(prepared.IsPublishedAdmissionOwnerCurrent());
            Assert.Equal(committed, File.ReadAllBytes(input.FilePath));
            Assert.Equal(Uid, player.UID);
            Assert.Equal(1U, player.Online);
            Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection));
        });
    }

    [Fact]
    public async Task GenuineIniConversionCannotBlessAnInterveningPhysicalLevelChange()
    {
        using var input = new LoginInput();
        var bytes = WritePrefixFields(input, "DeityLandLuckyPoints=1234567\r\n");
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (_, birth) = await ConnectBornAsync(fixture);
            var client = birth.Connection.NativeGameClient!;
            var player = client.Player;
            var snapshot = ReadPrefixSnapshot(input);
            var original = LegacyNativeItemCounterShellIni.ToUInt32;
            var fired = false;
            try
            {
                LegacyNativeItemCounterShellIni.ToUInt32 = text =>
                {
                    var result = original(text);
                    if (text == "1234567" && !fired)
                    {
                        fired = true;
                        SetClassLevelField(player, "_level", (ushort)9);
                    }
                    return result;
                };
                Assert.False(LegacyNativeExistingCharacterPrefix.TryPrepare(birth.Connection, snapshot, input.Users,
                    out var prepared, out _));
                Assert.Null(prepared);
            }
            finally { LegacyNativeItemCounterShellIni.ToUInt32 = original; }
            Assert.True(fired);
            Assert.Equal((ushort)9, player.Level);
            Assert.Equal(0U, player.Class);
            Assert.Equal(0U, player.UID);
            Assert.Equal(0U, player.Online);
            Assert.Null(client.Beasts);
            Assert.Null(client.HairfaceStorage);
            Assert.Equal(bytes, File.ReadAllBytes(input.FilePath));
            Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection));
        });
    }
}
