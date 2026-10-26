using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyOrdinaryAdmissionCharacterTextTests
{
    [Theory]
    [InlineData("\r\n", false)]
    [InlineData("\r\n", true)]
    [InlineData("\n", false)]
    [InlineData("\n", true)]
    [InlineData("\r", false)]
    [InlineData("\r", true)]
    public void MissingFinalFieldKeepsRecordSeparatorsAndOriginalTerminalNewline(string newline, bool terminated)
    {
        var before = "[Unrelated]" + newline + "Keep = exact  " + newline +
            "[Character]" + newline + "UID=1" + (terminated ? newline : "");
        var expected = "[Unrelated]" + newline + "Keep = exact  " + newline +
            "[Character]" + newline + "UID=1" + newline + "Online=1" + (terminated ? newline : "");

        var actual = LegacyOrdinaryAdmissionCharacterText.Project(before,
            new Dictionary<string, string> { ["Online"] = "1" }, null);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AppendedPaidSectionKeepsTheOriginalTerminalNewline(bool terminated)
    {
        var before = "[Character]\r\nUID=1\r\nOnline=1\r\n[Unrelated]\r\nKeep=exact" + (terminated ? "\r\n" : "");
        var expected = "[Character]\r\nUID=1\r\nOnline=1\r\n[Unrelated]\r\nKeep=exact\r\n" +
            "[PaidNobility]\r\nIsActive=False" + (terminated ? "\r\n" : "");

        // Formatter-only approved-value input; this does not give an absent
        // paid rank or expiry any gameplay authority.
        var actual = LegacyOrdinaryAdmissionCharacterText.Project(before,
            new Dictionary<string, string> { ["Online"] = "1" },
            new Dictionary<string, string> { ["IsActive"] = "False" });

        Assert.Equal(expected, actual);
    }
}
