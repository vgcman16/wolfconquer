using System.Text;
using WolfConquer.Core.World;
using Cache = WolfConquer.Core.World.LegacyNativeBeastsAtrribute;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHairfaceStorageLoginTests
{
    [Theory]
    [InlineData(true, "00000000000035ignored", (byte)3)]
    [InlineData(false, "35", (byte)2)]
    public async Task BeastPointsAndHighBitFlagLoadIntoTheRetainedConstructorOwnedBeast(
        bool activated, string savedPoints, byte expectedLevel)
    {
        await AssertBeastPointsLoginAsync(activated, savedPoints, "2147483665",
            expectedPoints: 5, expectedLevel: expectedLevel, expectedFlag: 0x80000011U);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("not-uint", "4294967296")]
    public async Task MissingOrMalformedBeastUInt32ValuesApplyZeroDefaultsNotPriorFlag(
        string? savedPoints, string? savedFlag)
    {
        await AssertBeastPointsLoginAsync(true, savedPoints, savedFlag,
            expectedPoints: 0, expectedLevel: 1, expectedFlag: 0);
    }

    private static async Task AssertBeastPointsLoginAsync(
        bool activated, string? savedPoints, string? savedFlag,
        uint expectedPoints, byte expectedLevel, uint expectedFlag)
    {
        using var input = new LoginInput();
        var fixtureBytes = WriteBeastPointsValues(input, activated, savedPoints, savedFlag);
        var expectedMarker = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(fixtureBytes)
            .Replace("Online=7\r\n", "Online=1\r\n", StringComparison.Ordinal));
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            // WithHandlerAsync explicitly owns a NativeLoginBeastMetadataScope(1000).
            // Change only that test-owned cache, retaining its identity for finally restoration.
            var attributes = Cache.Attributes;
            Assert.Single(attributes);
            Assert.Equal(1000U, attributes[1].RequiredPoints);
            attributes.Clear();
            attributes.Add(1, new Cache.Attribute { Level = 1, RequiredPoints = 10 });
            attributes.Add(2, new Cache.Attribute { Level = 2, RequiredPoints = 20 });
            attributes.Add(3, new Cache.Attribute { Level = 3, RequiredPoints = 40 });
            var (wire, birth) = await ConnectBornAsync(fixture);
            var client = birth.Connection.NativeGameClient!;
            var player = client.Player;
            var playerFlags = player.BitVector;
            var random = player.MyRandom;
            var (_, foreignBirth) = await ConnectBornAsync(fixture);
            var foreignOwner = foreignBirth.Connection.NativeGameClient!;
            var beasts = new LegacyNativeBeastsRuntime(client)
            {
                Activated = !activated, Points = 7, FixedLevel = 7, flag = 0x40000008U,
            };
            client.Beasts = beasts;
            var storage = new LegacyNativeHairfaceStorageRuntime(client);
            var storageState = ReadEmptyState(storage);
            client.HairfaceStorage = storage;

            var accepted = Assert.IsType<WorldSession>((await SendLoginAsync(fixture, wire)).After);

            Assert.Same(client, accepted.NativeGameClient);
            Assert.Same(player, accepted.NativePlayer);
            Assert.Same(player, client.Player);
            Assert.Same(playerFlags, player.BitVector);
            Assert.Same(random, player.MyRandom);
            Assert.Equal(Uid, player.UID);
            Assert.Equal(1U, player.Online);
            Assert.False(player.SpawinEffect);
            Assert.True(player.SpawinEffectSkills);
            Assert.Same(beasts, client.Beasts);
            Assert.Same(client, beasts.Owner);
            Assert.Equal(activated, beasts.Activated);
            Assert.Equal(expectedPoints, beasts.Points);
            Assert.Equal(expectedLevel, beasts.Level);
            Assert.Equal((byte)1, beasts.FixedLevel);
            Assert.Equal(expectedFlag, beasts.flag);
            AssertPreservedHairface(client, storage, client, storageState);
            Assert.Null(foreignOwner.Beasts);
            Assert.Null(foreignOwner.HairfaceStorage);
            Assert.Equal(0U, foreignOwner.Player.UID);
            Assert.Equal(0U, foreignOwner.Player.Online);
            Assert.Same(attributes, Cache.Attributes);
            Assert.Equal(new byte[] { 1, 2, 3 }, attributes.Keys);
            Assert.Equal(new uint[] { 10, 20, 40 }, attributes.Values.Select(row => row.RequiredPoints));
            AssertSavedFlagText(accepted, "TailedBeastTotalPoints", savedPoints);
            AssertSavedFlagText(accepted, "TailedBeastFlag", savedFlag);
            Assert.Equal(expectedMarker, File.ReadAllBytes(input.FilePath));
            Assert.Contains("legacy MsgLoginGame Success", log.Text);
            if (savedPoints == "00000000000035ignored")
            {
                Assert.Equal(14, "00000000000035".Length);
                Assert.False(uint.TryParse(savedPoints, out _));
                // Native ReadUInt32's fifteen-WCHAR buffer reads fourteen digits => 35.
                Assert.Equal(5U, beasts.Points);
            }

            // A real repeated accepted-login frame must stop before another saved-state publication.
            var repeated = await SendLoginAsync(fixture, wire);
            Assert.Same(accepted, repeated.Before);
            Assert.Same(accepted, repeated.After);
            Assert.Same(beasts, client.Beasts);
            Assert.Equal(expectedPoints, beasts.Points);
            Assert.Equal(expectedLevel, beasts.Level);
            Assert.Equal((byte)1, beasts.FixedLevel);
            Assert.Equal(expectedFlag, beasts.flag);
            Assert.Equal(1U, player.Online);
            Assert.Equal(expectedMarker, File.ReadAllBytes(input.FilePath));
            Assert.Contains("ignored repeated legacy MsgLoginGame", log.Text);
        });
    }

    private static byte[] WriteBeastPointsValues(
        LoginInput input, bool activated, string? points, string? flag)
    {
        var original = WritePostHairfaceFlagValues(input, activated ? "True" : "False", "False", "True");
        var added = new StringBuilder("HairfaceStorage=0/\r\n");
        if (points is not null) added.Append("TailedBeastTotalPoints=").Append(points).Append("\r\n");
        if (flag is not null) added.Append("TailedBeastFlag=").Append(flag).Append("\r\n");
        var text = Encoding.UTF8.GetString(original).Replace("HairfaceStorage=0/\r\n", added.ToString(), StringComparison.Ordinal);
        var bytes = Encoding.UTF8.GetBytes(text);
        File.WriteAllBytes(input.FilePath, bytes);
        return bytes;
    }
}
