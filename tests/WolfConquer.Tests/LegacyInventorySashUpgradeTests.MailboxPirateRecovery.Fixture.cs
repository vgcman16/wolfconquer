using System.Security.Cryptography;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    private static CombatGearSnapshot PirateRecoveryGear(string scenario = "")
    {
        static CombatGearSnapshotItem Item(uint id, uint level, uint seed, uint jade) => new(id, level, seed + 1, seed == 10 ? 1U : 0U,
            seed + 2, seed + 3, [Enumerable.Range(0, 7).Select(value => seed + (uint)value).ToArray(),
                Enumerable.Range(10, 7).Select(value => seed + (uint)value).ToArray()], [jade, 0, 0, 0, 0, 0]);
        CombatGearSnapshotItem[] items = [Item(701, 2, 10, 9001), Item(scenario == "DuplicateItem" ? 701U : 702U, 1, 30, 9002)];
        return PackageJoinGear() with
        {
            Count = items.Length, Items = items,
            AirPowers = new(2, 3, 4, 19, 5, 6, 7),
            BlackJadeBag = new(2, scenario == "DuplicateRune" ? new uint[] { 9001, 9003 } : new uint[] { 9001, 9002 }),
        };
    }

    private static CombatGearSnapshot PirateRecoveryAfter(CombatGearSnapshot before) => before with
    { PirateExp = checked(before.PirateExp + 50), AirPowers = before.AirPowers with { Points = checked(before.AirPowers.Points + 50) } };

    private static async Task<PackageJoinBefore> SeedPirateRecoveryAsync(Fixture fixture, PackageJoin join, string scenario)
    {
        new CombatGearSnapshotLoader(fixture.PackageGearRoot).SaveByCharacterUid(Fixture.Uid, PirateRecoveryGear(scenario));
        if (scenario == "Noncanonical")
            File.AppendAllText(fixture.PackageGearPath, "Unclaimed=1" + Environment.NewLine, new UTF8Encoding(false));
        if (scenario == "MissingAnima")
        {
            var text = File.ReadAllText(fixture.PackageGearPath);
            Assert.Contains("Animas[1]6=46", text, StringComparison.Ordinal);
            File.WriteAllText(fixture.PackageGearPath, text.Replace("Animas[1]6=46" + Environment.NewLine, "", StringComparison.Ordinal), new UTF8Encoding(false));
        }
        var character = File.ReadAllText(fixture.CharacterPath);
        Assert.DoesNotContain("SecurityPass=", character, StringComparison.Ordinal);
        File.WriteAllText(fixture.CharacterPath, character.Replace("[Unrelated]",
            "SecurityPass=111,0,0" + Environment.NewLine + "[Unrelated]", StringComparison.Ordinal), new UTF8Encoding(false));
        WriteSashField(fixture, "Online", "0");
        await join.RestartAsync();
        return CapturePackageJoin(fixture, join);
    }

    private static LegacyItemUidProducerIntentSnapshot PreparePirateRecoveryIntent(Fixture fixture, PackageJoin join,
        PackageJoinBefore before, bool claimAll, bool allocated, string scenario)
    {
        var recipe = CreatePackageJoinStackRecipe(fixture, join, before, claimAll, 7051, PirateRecoveryAfter(PirateRecoveryGear(scenario)));
        var payload = recipe.CopyPayload();
        var request = new LegacyItemUidReservationRequest(recipe.OperationId, LegacyMailboxPackageProducer.Writer,
            recipe.AccountId, recipe.CharacterUid, Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant(), recipe.Count);
        Assert.True(join.Registry.TryPrepareProducerIntent(join.Domain, request, payload, out var intent, out var reason), reason);
        if (allocated) Assert.True(join.Registry.TryReserveProducerIntent(join.Domain, request, payload, out intent, out reason), reason);
        Assert.Equal(allocated, intent!.Allocation is not null);
        return intent;
    }
}
