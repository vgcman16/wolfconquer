using System.Globalization;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

internal static partial class Program
{
    private static int SeedOwnedAdmissionFixture(string[] args)
    {
        var profile = ConquerProtocolProfile.RootD11_7899;
        if (args.Length is not (5 or 7) || !TryFixtureId(args[2], out var account) ||
            !TryFixtureId(args[3], out var character) || !TryFixtureId(args[4], out var item) ||
            (args.Length == 7 && (args[5] != "--profile" || !TryFixtureProfile(args[6], out profile))))
        {
            Console.Error.WriteLine("Usage: WolfConquer.DataTool seed-owned-admission-fixture <NEW-absolute-wolf-owned-admission-GUID-dir> <account-key> <character-uid> <item-uid> [--profile RootD11_7899|VisibleDx8D13]");
            return 1;
        }
        if (!LegacyOwnedAdmissionFixture.TryCreate(args[1], account, character, item, profile, out var fixture, out var reason) || fixture is null)
        {
            Console.Error.WriteLine("Synthetic owned-admission fixture refused: " + reason);
            Console.Error.WriteLine("Any newly owned incomplete root or sibling staging directory is retained; do not reuse it. No authentication account or startup authority was created by a failed result.");
            return 1;
        }
        Console.WriteLine("New synthetic owned-admission file fixture created; offline preparation only.");
        if (args.Length == 7) Console.WriteLine("fixtureProfile=" + profile);
        Console.WriteLine("fixtureRoot=" + fixture.RootPath);
        Console.WriteLine("ownedAdmissionConfig=" + fixture.ConfigurationPath);
        Console.WriteLine("expectedDomain=" + fixture.DomainId.ToString("N"));
        Console.WriteLine("accountKey=" + fixture.AccountKey.ToString(CultureInfo.InvariantCulture));
        Console.WriteLine("characterUid=" + fixture.CharacterUid.ToString(CultureInfo.InvariantCulture));
        Console.WriteLine("itemUid=" + fixture.ItemUid.ToString(CultureInfo.InvariantCulture) + " itemId=1001000 stack=1");
        Console.WriteLine("Boundary: synthetic snapshots and their genuine new UID domain only; no account password, authentication fallback, admin authority, server startup or live-client proof.");
        return 0;
    }

    private static bool TryFixtureProfile(string value, out ConquerProtocolProfile profile)
    {
        profile = ConquerProtocolProfile.RootD11_7899;
        if (value == nameof(ConquerProtocolProfile.RootD11_7899)) return true;
        if (value != nameof(ConquerProtocolProfile.VisibleDx8D13)) return false;
        profile = ConquerProtocolProfile.VisibleDx8D13;
        return true;
    }

    private static bool TryFixtureId(string value, out uint id) =>
        uint.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out id) && id != 0;
}
