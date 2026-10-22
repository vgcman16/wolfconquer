using System.Text;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHairfaceStorageLoginTests
{
    [Theory]
    [InlineData("True ignored", "False", "True", true, false, true)]
    [InlineData("False", "True", "False", false, true, false)]
    public async Task PostHairfaceFlagsReadIntoRetainedPlayerAndForeignOwnedBeast(
        string activated, string effect, string skills,
        bool expectedActivated, bool expectedEffect, bool expectedSkills)
    {
        await AssertPostHairfaceFlagsAsync(activated, effect, skills,
            expectedActivated, expectedEffect, expectedSkills);
    }

    [Theory]
    [InlineData(null, null, null)]
    [InlineData("1", "bad", "TrueX")]
    public async Task MissingOrMalformedPostHairfaceFlagsResetControlledTrueStateToFalse(
        string? activated, string? effect, string? skills)
    {
        await AssertPostHairfaceFlagsAsync(activated, effect, skills, false, false, false);
    }

    private static async Task AssertPostHairfaceFlagsAsync(
        string? activated, string? effect, string? skills,
        bool expectedActivated, bool expectedEffect, bool expectedSkills)
    {
        using var input = new LoginInput();
        var original = WritePostHairfaceFlagValues(input, activated, effect, skills);
        var expectedMarker = Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(original)
            .Replace("Online=7\r\n", "Online=1\r\n", StringComparison.Ordinal));
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectBornAsync(fixture);
            var client = birth.Connection.NativeGameClient!;
            var player = client.Player;
            var flags = player.BitVector;
            var random = player.MyRandom;
            var (_, foreignBirth) = await ConnectBornAsync(fixture);
            var foreignOwner = foreignBirth.Connection.NativeGameClient!;
            foreignOwner.Player.SpawinEffect = true;
            foreignOwner.Player.SpawinEffectSkills = false;
            var beasts = new LegacyNativeBeastsRuntime(foreignOwner)
            {
                Activated = !expectedActivated, Points = 73, flag = 0x80000011U, FixedLevel = 7,
            };
            client.Beasts = beasts;
            player.SpawinEffect = !expectedEffect;
            player.SpawinEffectSkills = !expectedSkills;
            var storage = new LegacyNativeHairfaceStorageRuntime(foreignOwner);
            var storageState = ReadEmptyState(storage);
            client.HairfaceStorage = storage;

            var accepted = Assert.IsType<WorldSession>((await SendLoginAsync(fixture, wire)).After);

            Assert.Same(client, accepted.NativeGameClient);
            Assert.Same(player, client.Player);
            Assert.Same(player, accepted.NativePlayer);
            Assert.Same(client, player.Owner);
            Assert.Same(flags, player.BitVector);
            Assert.Same(random, player.MyRandom);
            Assert.Equal(Uid, player.UID);
            Assert.Equal(1U, player.Online);
            Assert.True(player.RelicSpirit);
            Assert.Equal(expectedEffect, player.SpawinEffect);
            Assert.Equal(expectedSkills, player.SpawinEffectSkills);
            Assert.Same(beasts, client.Beasts);
            Assert.Same(foreignOwner, beasts.Owner);
            Assert.Equal(expectedActivated, beasts.Activated);
            // Missing TotalPoints reads zero through the real setter; missing flag reads zero.
            Assert.Equal(73U, beasts.Points);
            Assert.Equal(0U, beasts.flag);
            Assert.Equal((byte)7, beasts.FixedLevel);
            Assert.Equal(expectedActivated ? (byte)1 : (byte)0, beasts.Level);
            AssertPreservedHairface(client, storage, foreignOwner, storageState);
            Assert.Null(foreignOwner.Beasts);
            Assert.Null(foreignOwner.HairfaceStorage);
            Assert.Equal(0U, foreignOwner.Player.UID);
            Assert.Equal(0U, foreignOwner.Player.Online);
            Assert.True(foreignOwner.Player.SpawinEffect);
            Assert.False(foreignOwner.Player.SpawinEffectSkills);
            Assert.Equal(1L, accepted.Character.Online);
            AssertSavedFlagText(accepted, "TailedBeastActivated", activated);
            AssertSavedFlagText(accepted, "SpawinEffect", effect);
            AssertSavedFlagText(accepted, "SpawinEffectSkills", skills);
            Assert.Equal(expectedMarker, File.ReadAllBytes(input.FilePath));
            Assert.Contains("legacy MsgLoginGame Success", log.Text);

            if (activated == "True ignored")
            {
                // Native ReadBool uses its real six-WCHAR buffer: "True " is parsed,
                // while the untouched full snapshot value is not a bool at all.
                Assert.False(bool.TryParse(activated, out _));
                Assert.True(beasts.Activated);
            }

        });
    }

    private static void AssertSavedFlagText(WorldSession session, string key, string? expected)
    {
        if (expected is null) Assert.False(session.Character.Attributes.ContainsKey(key));
        else Assert.Equal(expected, session.Character.Attributes[key]);
    }

    private static byte[] WritePostHairfaceFlagValues(
        LoginInput input, string? activated, string? effect, string? skills)
    {
        Assert.Equal(input.InitialBytes, File.ReadAllBytes(input.FilePath));
        var added = new StringBuilder("RelicSpirit=True\r\nHairfaceStorage=0/\r\n");
        if (activated is not null) added.Append("TailedBeastActivated=").Append(activated).Append("\r\n");
        if (effect is not null) added.Append("SpawinEffect=").Append(effect).Append("\r\n");
        if (skills is not null) added.Append("SpawinEffectSkills=").Append(skills).Append("\r\n");
        var text = Encoding.UTF8.GetString(input.InitialBytes);
        Assert.Contains("RelicSpirit=True\r\n", text, StringComparison.Ordinal);
        var bytes = Encoding.UTF8.GetBytes(text.Replace("RelicSpirit=True\r\n", added.ToString(), StringComparison.Ordinal));
        File.WriteAllBytes(input.FilePath, bytes);
        return bytes;
    }
}
