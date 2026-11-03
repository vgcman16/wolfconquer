using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeExistingCharacterPrefixTests
{
    [Theory]
    [InlineData("SpawinEffect", "yes")]
    [InlineData("SpawinEffectSkills", "1")]
    [InlineData("DeityLandLuckyPoints", "4294967296")]
    [InlineData("HeroPoints", "bad")]
    [InlineData("ClassExperience", "-1")]
    [InlineData("SavePromote", "4294967296")]
    [InlineData("SkinUnlock", "256")]
    [InlineData("DoomosaurSkin", "-1")]
    [InlineData("PrayingElfSkin", "256")]
    [InlineData("PolarDragonSkin", "256")]
    [InlineData("ChargeChi", "256")]
    [InlineData("ChargeGuildSkill", "256")]
    [InlineData("Body", "65536")]
    [InlineData("Face", "-1")]
    [InlineData("TailedBeastActivated", "bad")]
    [InlineData("TailedBeastTotalPoints", "4294967296")]
    [InlineData("TailedBeastFlag", "-1")]
    public async Task PresentInvalidRealIniScalarsRefuseWithoutDefaultsOrPublication(string key, string value) =>
        await AssertInvalidPresentPrefixAsync(key, value);

    [Theory]
    [InlineData("control")]
    [InlineData("truncated")]
    [InlineData("nul-tail")]
    [InlineData("beast-derived-overflow")]
    public async Task RealIniHiddenTailOrDerivedOverflowCannotLeavePrefixEffects(string failure)
    {
        var (key, value, reason) = failure switch
        {
            "control" => ("Name", "Bob\tAdmin", "normalPrefixFieldControlValue"),
            "truncated" => ("Name", new string('n', 255), "normalPrefixFieldTruncated"),
            "nul-tail" => ("RelicSpirit", "True\0not-a-bool", "normalPrefixFieldControlValue"),
            _ => ("TailedBeastTotalPoints", "4294967295", "normalPrefixBeastStateUnavailable")
        };
        await AssertInvalidPresentPrefixAsync(key, value, reason);
    }

    [Fact]
    public async Task RealIniValidQuotedOptionalAndWhitespaceNumbersKeepGenuineSuccessfulAdmission()
    {
        using var input = new PrefixInput("True");
        var text = Encoding.UTF8.GetString(input.InitialBytes).Replace("RelicSpirit=True\r\n",
            "RelicSpirit=\"True\"\r\nSpawinEffect=\"False\"\r\nDeityLandLuckyPoints=\"17\"\r\n" +
            "SkinUnlock=\"3\"\r\nTailedBeastActivated=\"False\"\r\nTailedBeastFlag=\"19\"\r\n", StringComparison.Ordinal)
            .Replace("Body=1008", "Body=  +1008  ", StringComparison.Ordinal)
            .Replace("Face=154", "Face=  +154  ", StringComparison.Ordinal);
        var before = Encoding.UTF8.GetBytes(text); File.WriteAllBytes(input.FilePath, before);
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectBornAsync(fixture);
            var admitted = Assert.IsType<WorldSession>((await SendLoginAsync(fixture, wire)).After);
            var client = birth.Connection.NativeGameClient!;
            Assert.Same(client, admitted.NativeGameClient); Assert.Same(client.Player, admitted.NativePlayer);
            Assert.Equal(Uid, client.Player.UID); Assert.Equal(1U, client.Player.Online);
            Assert.True(client.Player.RelicSpirit); Assert.False(client.Player.SpawinEffect);
            Assert.Equal(17U, client.Player.DeityLandLuckyPoints); Assert.Equal((byte)3, client.Player.SkinUnlock);
            Assert.Equal((ushort)1008, client.Player.Body); Assert.Equal((ushort)154, client.Player.Face);
            var beasts = Assert.IsType<LegacyNativeBeastsRuntime>(client.Beasts);
            Assert.False(beasts.Activated); Assert.Equal(19U, beasts.flag);
            Assert.Equal("\"True\"", admitted.Character.Attributes["RelicSpirit"]);
            Assert.Equal(Encoding.UTF8.GetBytes(text.Replace("Online=7\r\n", "Online=1\r\n", StringComparison.Ordinal)),
                File.ReadAllBytes(input.FilePath));
            Assert.Same(admitted, handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection));
            Assert.Contains("legacy MsgLoginGame Success", log.Text);
        });
    }

    private static async Task AssertInvalidPresentPrefixAsync(string key, string value,
        string expectedReason = "normalPrefixOptionalValueInvalid")
    {
        using var input = new PrefixInput("True");
        var text = Encoding.UTF8.GetString(input.InitialBytes);
        string original = key switch { "Body" => "Body=1008", "Face" => "Face=154", "Name" => "Name=PrefixOwner", _ => "RelicSpirit=True" };
        string replacement = key is "Body" or "Face" or "Name" or "RelicSpirit"
            ? key + "=" + value : original + "\r\n" + key + "=" + value;
        var before = Encoding.UTF8.GetBytes(text.Replace(original + "\r\n", replacement + "\r\n", StringComparison.Ordinal));
        File.WriteAllBytes(input.FilePath, before);
        var loaded = new CharacterSnapshotLoader(input.Users).TryLoadByAccountId(Uid);
        Assert.Equal(CharacterSnapshotLoadStatus.Success, loaded.Status); // Reaches the real prefix, not an earlier parse refusal.
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectBornAsync(fixture);
            var client = birth.Connection.NativeGameClient!;
            var player = client.Player;
            var values = LegacyNativePlayerRuntime.ExistingPrefixLoad.Read(player);
            var flags = player.BitVector; var random = player.MyRandom;
            var beasts = new LegacyNativeBeastsRuntime(client) { Points = 17, flag = 19, FixedLevel = 3 };
            var storage = new LegacyNativeHairfaceStorageRuntime(client);
            client.Beasts = beasts; client.HairfaceStorage = storage;
            var dispatch = await SendLoginAsync(fixture, wire);
            Assert.Null(dispatch.Before); Assert.Null(dispatch.After);
            Assert.Equal(values, LegacyNativePlayerRuntime.ExistingPrefixLoad.Read(player));
            Assert.Same(flags, player.BitVector); Assert.Same(random, player.MyRandom);
            Assert.Same(beasts, client.Beasts); Assert.Same(storage, client.HairfaceStorage);
            Assert.Equal(17U, beasts.Points); Assert.Equal(19U, beasts.flag); Assert.Equal((byte)3, beasts.FixedLevel);
            Assert.Empty(storage.Objects); Assert.False(client.FullLoading); Assert.Null(client.OrdinarySpellBinding);
            Assert.Equal(before, File.ReadAllBytes(input.FilePath));
            Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection));
            Assert.Contains(expectedReason, log.Text);
            Assert.DoesNotContain("legacy MsgLoginGame Success", log.Text);
        });
    }
}
