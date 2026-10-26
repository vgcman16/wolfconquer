using System.Globalization;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHairfaceStorageLoginTests
{
    [Theory]
    [InlineData("malformed-hairface")]
    [InlineData("foreign-hairface")]
    [InlineData("foreign-beast")]
    [InlineData("online-overflow")]
    [InlineData("missing-beast-metadata")]
    [InlineData("denied-character-write")]
    public async Task AdmissionRejectionDoesNotPersistExpiredPaidNobilityOrPublishPrefixState(string rejection)
    {
        using var input = new LoginInput();
        var original = WriteAdmissionExpiryInput(input, rejection == "malformed-hairface");
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectBornAsync(fixture);
            var client = birth.Connection.NativeGameClient!;
            LegacyNativeGameClientRuntime? foreign = null;
            if (rejection is "foreign-hairface" or "foreign-beast")
            {
                var (_, foreignBirth) = await ConnectBornAsync(fixture);
                foreign = foreignBirth.Connection.NativeGameClient!;
                if (rejection == "foreign-hairface") client.HairfaceStorage = new(foreign);
                else client.Beasts = new(foreign) { Points = 17, flag = 19, FixedLevel = 3 };
            }
            if (rejection == "online-overflow") client.Player.Online = uint.MaxValue;
            if (rejection == "missing-beast-metadata") LegacyNativeBeastsAtrribute.Attributes = [];
            var beforePlayer = ObservePrefixPlayer(client.Player);
            var beforeHairface = client.HairfaceStorage;
            var beforeBeast = client.Beasts;
            var foreignPlayer = foreign is null ? null : ObservePrefixPlayer(foreign.Player);
            using var denied = rejection == "denied-character-write"
                ? new FileStream(input.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read)
                : null;

            var dispatch = await SendLoginAsync(fixture, wire);

            Assert.Null(dispatch.Before);
            Assert.Null(dispatch.After);
            Assert.Equal(beforePlayer, ObservePrefixPlayer(client.Player));
            Assert.Same(beforeHairface, client.HairfaceStorage);
            Assert.Same(beforeBeast, client.Beasts);
            if (beforeBeast is not null)
            {
                Assert.Equal(17U, beforeBeast.Points);
                Assert.Equal(19U, beforeBeast.flag);
                Assert.Equal((byte)3, beforeBeast.FixedLevel);
            }
            if (foreign is not null)
            {
                Assert.Equal(foreignPlayer, ObservePrefixPlayer(foreign.Player));
                Assert.Null(foreign.HairfaceStorage);
                Assert.Null(foreign.Beasts);
            }
            Assert.Equal(original, File.ReadAllBytes(input.FilePath));
            Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection));
            Assert.DoesNotContain("legacy MsgLoginGame Success", log.Text);
        });
    }

    [Fact]
    public async Task AcceptedAdmissionPersistsPaidExpiryAndOnlineAndRetainsUnrelatedData()
    {
        using var input = new LoginInput();
        WriteAdmissionExpiryInput(input, malformedHairface: false);
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectBornAsync(fixture);

            var accepted = Assert.IsType<WorldSession>((await SendLoginAsync(fixture, wire)).After);

            Assert.Equal(1L, accepted.Character.Online);
            Assert.Equal("250000000", accepted.Character.Attributes["DonationNobility"]);
            Assert.Equal("250000000", accepted.Character.Attributes["LastNobilityDonation"]);
            Assert.Equal("5", accepted.Character.Attributes["NobilityRank"]);
            Assert.Equal(bool.FalseString, accepted.Character.PaidNobility["IsActive"]);
            Assert.Equal("0", accepted.Character.PaidNobility["Identifier"]);
            var committed = File.ReadAllBytes(input.FilePath);
            var text = new UTF8Encoding(false, true).GetString(committed);
            var saved = CharacterSnapshotParser.ParseText(text);
            Assert.Equal(1L, saved.Online);
            Assert.Equal("250000000", saved.Attributes["DonationNobility"]);
            Assert.Equal("5", saved.Attributes["NobilityRank"]);
            Assert.Equal(bool.FalseString, saved.PaidNobility["IsActive"]);
            Assert.Equal("0", saved.PaidNobility["PaidRank"]);
            Assert.Equal("0", saved.PaidNobility["PeriodTime"]);
            Assert.Contains("[Unrelated]\r\nKeep=unchanged\r\n", text, StringComparison.Ordinal);
            Assert.Equal(1U, birth.Connection.NativeGameClient!.Player.Online);
            Assert.Same(accepted, handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection));
            Assert.Contains("legacy MsgLoginGame Success", log.Text);

            Assert.Same(accepted, (await SendLoginAsync(fixture, wire)).After);
            Assert.Equal(committed, File.ReadAllBytes(input.FilePath));
            Assert.Equal(1U, birth.Connection.NativeGameClient.Player.Online);
        });
    }

    private static byte[] WriteAdmissionExpiryInput(LoginInput input, bool malformedHairface)
    {
        var text = Encoding.UTF8.GetString(input.InitialBytes).Replace("RelicSpirit=True\r\n",
            "RelicSpirit=True\r\nDonationNobility=999000000\r\nLastNobilityDonation=250000000\r\n" +
            "NobilityRank=12\r\nNobilityPosition=0\r\nHairfaceStorage=" +
            (malformedHairface ? "1/4201/0/-1/" : "0/") + "\r\n", StringComparison.Ordinal);
        var expired = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc).ToBinary()
            .ToString(CultureInfo.InvariantCulture);
        text += "[PaidNobility]\r\nRank=King\r\nIdentifier=" + Uid.ToString(CultureInfo.InvariantCulture) +
            "\r\nLastNobilityDonation=250000000\r\nPaidRank=12\r\nPeriodTime=" + expired +
            "\r\nIsActive=True\r\nPosition=0\r\n";
        var bytes = Encoding.UTF8.GetBytes(text);
        File.WriteAllBytes(input.FilePath, bytes);
        return bytes;
    }
}
