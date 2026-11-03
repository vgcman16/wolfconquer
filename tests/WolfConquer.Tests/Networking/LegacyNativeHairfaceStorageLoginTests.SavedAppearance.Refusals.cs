using System.Text;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHairfaceStorageLoginTests
{
    [Theory]
    [InlineData("ClassExperience", "00000000000035ignored")]
    [InlineData("ClassExperience", "-1")]
    [InlineData("SavePromote", "4294967296")]
    [InlineData("SkinUnlock", "00255ignored")]
    [InlineData("SkinUnlock", "256")]
    [InlineData("DoomosaurSkin", "-1")]
    [InlineData("PrayingElfSkin", "bad")]
    [InlineData("PolarDragonSkin", "99999")]
    [InlineData("ChargeChi", "-128")]
    [InlineData("ChargeGuildSkill", "4294967296")]
    public async Task MalformedSavedAppearanceRefusesRealLoginBeforePrefixPublication(string key, string value)
    {
        using var input = new LoginInput();
        var original = WritePostHairfaceFlagValues(input, "False", "True", "False");
        var text = Encoding.UTF8.GetString(original).Replace("HairfaceStorage=0/\r\n",
            "HairfaceStorage=0/\r\n" + key + "=" + value + "\r\n", StringComparison.Ordinal);
        var bytes = Encoding.UTF8.GetBytes(text);
        File.WriteAllBytes(input.FilePath, bytes);
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectBornAsync(fixture);
            var client = birth.Connection.NativeGameClient!;
            var player = client.Player;
            player.ClassExperience = 777; player.SavePromote = 888;
            player.SkinUnlock = 1; player.DoomosaurSkin = 2; player.PrayingElfSkin = 3;
            player.PolarDragonSkin = 4; player.ChargeChi = 5; player.ChargeGuildSkill = 6;
            var before = ObservePrefixPlayer(player);
            var beasts = new LegacyNativeBeastsRuntime(client) { Points = 17, FixedLevel = 3, flag = 19 };
            var storage = new LegacyNativeHairfaceStorageRuntime(client);
            var storageState = ReadEmptyState(storage);
            client.Beasts = beasts; client.HairfaceStorage = storage;

            var result = await SendLoginAsync(fixture, wire);

            Assert.Null(result.Before); Assert.Null(result.After);
            Assert.Equal(before, ObservePrefixPlayer(player));
            Assert.Same(beasts, client.Beasts); Assert.Equal(17U, beasts.Points);
            Assert.Equal((byte)3, beasts.FixedLevel); Assert.Equal(19U, beasts.flag);
            AssertPreservedHairface(client, storage, client, storageState);
            Assert.Equal(bytes, File.ReadAllBytes(input.FilePath));
            Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection));
            Assert.DoesNotContain("legacy MsgLoginGame Success", log.Text);
        });
    }
}
