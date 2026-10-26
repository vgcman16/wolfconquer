using System.Globalization;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHairfaceStorageLoginTests
{
    [Fact]
    public async Task RealAdmissionRejectsMissingRequiredOnlineWithoutChangingFinalNewlineImage()
    {
        using var input = new LoginInput();
        PrepareAdmissionInput(input);
        var text = FinalCharacterSection(input).Replace("Online=7\r\n", "", StringComparison.Ordinal);
        var before = Encoding.UTF8.GetBytes(text);
        File.WriteAllBytes(input.FilePath, before);

        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectAdmissionPinWire(fixture);
            var native = Assert.IsType<LegacyNativeGameClientRuntime>(birth.Connection.NativeGameClient);
            var playerBefore = ObservePrefixPlayer(native.Player);
            Assert.Null((await SendLoginAsync(fixture, wire)).After);
            Assert.Null((await SendLoginAsync(fixture, wire)).After);
            Assert.Equal(playerBefore, ObservePrefixPlayer(native.Player));
            Assert.Null(native.HairfaceStorage); Assert.Null(native.Beasts);
            Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection));
            Assert.Equal(before, File.ReadAllBytes(input.FilePath));
            Assert.False(LegacyTradeJournal.HasPending(input.Users));
            Assert.DoesNotContain("ordinary admission committed key=", log.Text);
            Assert.Contains("snapshot error", log.Text);
            Assert.Contains("Online", log.Text);
            await fixture.CloseClientTransportAsync();
            AssertNoAdmissionAnswerOk(wire.CopyCapturedServerFramesAtEof());
        });
    }

    [Fact]
    public async Task RealPaidExpiryInsertsMissingFinalSectionFieldsWithoutAddingAFinalNewline()
    {
        using var input = new LoginInput();
        PrepareAdmissionInput(input);
        var character = FinalCharacterSection(input);
        var expired = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc).ToBinary()
            .ToString(CultureInfo.InvariantCulture);
        var before = character + "\r\n[PaidNobility]\r\nIsActive=True\r\nPeriodTime=" + expired +
            "\r\nLastNobilityDonation=20";
        var expected = Encoding.UTF8.GetBytes(character.Replace("Online=7\r\n", "Online=1\r\n", StringComparison.Ordinal) +
            "\r\nDonationNobility=20\r\nLastNobilityDonation=20\r\nNobilityRank=0\r\nNobilityPosition=-1" +
            "\r\n[PaidNobility]\r\nIsActive=False\r\nPeriodTime=0\r\nLastNobilityDonation=0" +
            "\r\nIdentifier=0\r\nPaidRank=0\r\nRank=Serf\r\nPosition=-1");
        File.WriteAllBytes(input.FilePath, Encoding.UTF8.GetBytes(before));

        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectBornAsync(fixture);
            var admitted = Assert.IsType<WorldSession>((await SendLoginAsync(fixture, wire)).After);
            Assert.Equal("20", admitted.Character.Attributes["DonationNobility"]);
            Assert.Equal("False", admitted.Character.PaidNobility["IsActive"]);
            Assert.Equal("0", admitted.Character.PaidNobility["PeriodTime"]);
            Assert.Equal(1U, birth.Connection.NativeGameClient!.Player.Online);
            Assert.Equal(expected, File.ReadAllBytes(input.FilePath));
            Assert.False(LegacyTradeJournal.HasPending(input.Users));
            Assert.Same(admitted, (await SendLoginAsync(fixture, wire)).After);
            Assert.Equal(expected, File.ReadAllBytes(input.FilePath));
        });
    }

    private static string FinalCharacterSection(LoginInput input)
    {
        var text = Encoding.UTF8.GetString(input.InitialBytes);
        var unrelated = text.IndexOf("\r\n[Unrelated]", StringComparison.Ordinal);
        Assert.True(unrelated > 0);
        return text[..unrelated].TrimEnd('\r', '\n');
    }
}
