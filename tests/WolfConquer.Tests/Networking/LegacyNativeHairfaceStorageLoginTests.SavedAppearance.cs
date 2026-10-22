using System.Text;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHairfaceStorageLoginTests
{
    [Theory]
    [InlineData("ordinary")]
    [InlineData("truncated")]
    [InlineData("missing")]
    [InlineData("malformed")]
    public async Task SavedClassExperiencePromotionAndAppearanceOverwriteTheRetainedPlayer(
        string scenario)
    {
        (string?[] saved, uint experience, uint promote, byte[] appearance) = scenario switch
        {
            "ordinary" => (new string?[] { "4294967295", "12345", "00255ignored", "17", "31", "63", "127", "254" },
                uint.MaxValue, 12345U, new byte[] { 255, 17, 31, 63, 127, 254 }),
            "truncated" => (new string?[] { "00000000000035ignored", "2", "3", "4", "5", "6", "7", "8" },
                35U, 2U, new byte[] { 3, 4, 5, 6, 7, 8 }),
            "missing" => (new string?[8], 0U, 0U, new byte[6]),
            "malformed" => (new string?[] { "-1", "4294967296", "256", "-1", "bad", "99999", "-128", "4294967296" },
                0U, 0U, new byte[6]),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
        };
        string[] keys = ["ClassExperience", "SavePromote", "SkinUnlock", "DoomosaurSkin",
            "PrayingElfSkin", "PolarDragonSkin", "ChargeChi", "ChargeGuildSkill"];
        using var input = new LoginInput();
        var original = WritePostHairfaceFlagValues(input, "False", "True", "False");
        var inserted = new StringBuilder("HairfaceStorage=0/\r\n");
        for (var index = 0; index < keys.Length; index++)
            if (saved[index] is not null)
                inserted.Append(keys[index]).Append('=').Append(saved[index]).Append("\r\n");
        var text = Encoding.UTF8.GetString(original).Replace(
            "HairfaceStorage=0/\r\n", inserted.ToString(), StringComparison.Ordinal);
        var fixtureBytes = Encoding.UTF8.GetBytes(text);
        var expectedMarker = Encoding.UTF8.GetBytes(text.Replace(
            "Online=7\r\n", "Online=1\r\n", StringComparison.Ordinal));
        File.WriteAllBytes(input.FilePath, fixtureBytes); // Only this LoginInput's owned temporary file.

        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectBornAsync(fixture);
            var client = birth.Connection.NativeGameClient!;
            var player = client.Player;
            var flags = player.BitVector;
            var random = player.MyRandom;
            Assert.False(client.FullLoading);
            Assert.Null(client.Socket);
            player.ClassExperience = 777;
            player.SavePromote = 888;
            player.SkinUnlock = 1;
            player.DoomosaurSkin = 2;
            player.PrayingElfSkin = 3;
            player.PolarDragonSkin = 4;
            player.ChargeChi = 5;
            player.ChargeGuildSkill = 6;
            player.SpawinEffect = true;
            player.SpawinEffectSkills = false;
            var (_, foreignBirth) = await ConnectBornAsync(fixture);
            var foreignOwner = foreignBirth.Connection.NativeGameClient!;
            foreignOwner.Player.ClassExperience = 991;
            foreignOwner.Player.SavePromote = 992;
            foreignOwner.Player.SkinUnlock = 99;
            var beasts = new LegacyNativeBeastsRuntime(foreignOwner)
            {
                Activated = false, Points = 17, FixedLevel = 3, flag = 0,
            };
            client.Beasts = beasts;
            var storage = new LegacyNativeHairfaceStorageRuntime(foreignOwner);
            var storageState = ReadEmptyState(storage);
            client.HairfaceStorage = storage;
            Assert.Equal(fixtureBytes, File.ReadAllBytes(input.FilePath));

            var accepted = Assert.IsType<WorldSession>((await SendLoginAsync(fixture, wire)).After);

            Assert.Same(client, accepted.NativeGameClient);
            Assert.Same(player, client.Player);
            Assert.Same(player, accepted.NativePlayer);
            Assert.Same(client, player.Owner);
            Assert.Same(flags, player.BitVector);
            Assert.Same(random, player.MyRandom);
            Assert.Equal(experience, player.ClassExperience);
            Assert.Equal(promote, player.SavePromote);
            Assert.Equal(appearance[0], player.SkinUnlock);
            Assert.Equal(appearance[1], player.DoomosaurSkin);
            Assert.Equal(appearance[2], player.PrayingElfSkin);
            Assert.Equal(appearance[3], player.PolarDragonSkin);
            Assert.Equal(appearance[4], player.ChargeChi);
            Assert.Equal(appearance[5], player.ChargeGuildSkill);
            Assert.False(client.FullLoading);
            Assert.Null(client.Socket);
            Assert.False(client.Fake);
            Assert.Equal(Uid, player.UID);
            Assert.Equal(1U, player.Online);
            Assert.True(player.SpawinEffect);
            Assert.False(player.SpawinEffectSkills);
            Assert.Same(beasts, client.Beasts);
            Assert.Same(foreignOwner, beasts.Owner);
            Assert.False(beasts.Activated);
            Assert.Equal(17U, beasts.Points);
            Assert.Equal((byte)3, beasts.FixedLevel);
            Assert.Equal((byte)0, beasts.Level);
            Assert.Equal(0U, beasts.flag);
            AssertPreservedHairface(client, storage, foreignOwner, storageState);
            Assert.Equal(991U, foreignOwner.Player.ClassExperience);
            Assert.Equal(992U, foreignOwner.Player.SavePromote);
            Assert.Equal((byte)99, foreignOwner.Player.SkinUnlock);
            Assert.Null(foreignOwner.Beasts);
            Assert.Null(foreignOwner.HairfaceStorage);
            for (var index = 0; index < keys.Length; index++)
                AssertSavedFlagText(accepted, keys[index], saved[index]);
            Assert.Equal(1L, accepted.Character.Online);
            Assert.Equal(expectedMarker, File.ReadAllBytes(input.FilePath));
            Assert.Contains("legacy MsgLoginGame Success", log.Text);
            if (scenario == "ordinary")
            {
                Assert.False(byte.TryParse(saved[2], out _));
                // Native ReadByte uses six WCHARs: the five-digit prefix is 00255.
                Assert.Equal((byte)255, player.SkinUnlock);
                Assert.Equal("00255ignored", accepted.Character.Attributes["SkinUnlock"]);
            }
            if (scenario == "truncated")
            {
                Assert.False(uint.TryParse(saved[0], out _));
                Assert.Equal(35U, player.ClassExperience);
                Assert.Equal("00000000000035ignored", accepted.Character.Attributes["ClassExperience"]);
            }
        });
    }
}
