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
    public async Task HostileGlobalIniConverterCannotChangePhysicalLevelOrInfluenceGenuinePrefixCommit()
    {
        using var input = new LoginInput();
        var bytes = WritePrefixFields(input, "DeityLandLuckyPoints=1234567\r\n");
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (_, birth) = await ConnectBornAsync(fixture);
            var client = birth.Connection.NativeGameClient!;
            var player = client.Player;
            player.TransformationID = 17;
            var before = LegacyNativePlayerRuntime.ExistingPrefixLoad.Read(player);
            var flags = player.BitVector; var random = player.MyRandom;
            var snapshot = ReadPrefixSnapshot(input);
            var original = LegacyNativeItemCounterShellIni.ToUInt32;
            var fired = false;
            try
            {
                LegacyNativeItemCounterShellIni.ToUInt32 = text =>
                {
                    fired = true;
                    SetClassLevelField(player, "_level", (ushort)9); // Hostile deny-only seam, never a success owner.
                    return original(text);
                };
                Assert.True(LegacyNativeExistingCharacterPrefix.TryPrepare(birth.Connection, snapshot, input.Users,
                    out var prepared, out var reason), reason);
                Assert.NotNull(prepared); Assert.False(fired);
                Assert.True(prepared!.IsCurrentForAdmission());
                Assert.Equal(before, LegacyNativePlayerRuntime.ExistingPrefixLoad.Read(player));
                Assert.Equal((ushort)0, player.Level); Assert.Equal(0U, player.Class);
                Assert.Equal(0U, player.UID); Assert.Equal(0U, player.Online);
                Assert.Equal((ushort)17, player.TransformationID);
                Assert.Null(client.Beasts); Assert.Null(client.HairfaceStorage);
                Assert.Equal(bytes, File.ReadAllBytes(input.FilePath));
                Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection));

                Assert.True(prepared!.TryCommit(out var updated, out reason), reason);
                Assert.False(fired);
                Assert.Equal((ushort)140, player.Level); Assert.Equal(7051U, player.Class);
                Assert.Equal(Uid, player.UID); Assert.Equal(1U, player.Online);
                Assert.Equal((ushort)17, player.TransformationID);
                Assert.Equal(1234567U, player.DeityLandLuckyPoints);
                Assert.Equal(140L, updated.Level); Assert.Equal(7051L, updated.Class);
                Assert.Equal(1L, updated.Online);
                Assert.Same(flags, player.BitVector); Assert.Same(random, player.MyRandom);
                Assert.Same(client, player.Owner); Assert.Same(player, client.Player);
                Assert.IsType<LegacyNativeBeastsRuntime>(client.Beasts);
                Assert.IsType<LegacyNativeHairfaceStorageRuntime>(client.HairfaceStorage);
                Assert.Equal(PresenceImage(bytes), File.ReadAllBytes(input.FilePath));
                Assert.True(prepared.IsPublishedAdmissionOwnerCurrent());
                Assert.False(prepared.IsCurrentForAdmission());
                Assert.False(prepared.TryCommit(out _, out _));
                Assert.True(prepared.IsPublishedAdmissionOwnerCurrent());
                Assert.Equal((ushort)140, player.Level); Assert.False(fired);
                Assert.Equal(PresenceImage(bytes), File.ReadAllBytes(input.FilePath));
                Assert.Null(client.OrdinarySpellBinding); Assert.False(client.FullLoading);
                Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection));
                Assert.DoesNotContain("legacy MsgLoginGame Success", log.Text);
            }
            finally { LegacyNativeItemCounterShellIni.ToUInt32 = original; }
        });
    }
}
