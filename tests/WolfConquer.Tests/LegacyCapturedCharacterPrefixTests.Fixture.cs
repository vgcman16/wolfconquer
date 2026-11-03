using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Data.SqlWorld;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyCapturedCharacterPrefixTests
{
    internal const long CapturedAccount = 501;
    internal const uint CapturedCharacter = 9001;

    internal static string CompleteText() => Encoding.UTF8.GetString(SqlWorldStateTestData.Character(CapturedCharacter));

    internal static string AddFields(string text, string fields) => text.Replace("[Unrelated]",
        fields + "[Unrelated]", StringComparison.Ordinal);

    internal static byte[] CharacterBytes(string fields = "") => Encoding.UTF8.GetBytes(AddFields(CompleteText(), fields));

    internal static SqlWorldStateImage Image(byte[] bytes)
    {
        Assert.True(SqlWorldStateImage.TryCreate(CapturedAccount, CapturedCharacter, bytes,
            PlayerItemSnapshotLoader.ToLegacyBytes(PlayerItemSnapshot.Empty), out var image, out var failure), failure.ToString());
        Assert.Equal(SqlWorldStateFailure.None, failure);
        return Assert.IsType<SqlWorldStateImage>(image);
    }

    internal static LegacyCapturedCharacterPrefix Capture(byte[] bytes)
    {
        Assert.True(LegacyCapturedCharacterPrefix.TryCreate(Image(bytes), out var captured, out var reason), reason);
        return Assert.IsType<LegacyCapturedCharacterPrefix>(captured);
    }

    private static void Refuses(byte[] bytes, string expectedReason)
    {
        var before = bytes.ToArray();
        var image = Image(bytes); // A genuine complete core image; no fabricated parser owner.
        Assert.False(LegacyCapturedCharacterPrefix.TryCreate(image, out var captured, out var reason));
        Assert.Null(captured);
        Assert.Equal(expectedReason, reason);
        Assert.Equal(before, bytes);
        Assert.Equal(before, image.CopyCharacterBytes());
    }
}
