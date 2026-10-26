using System.Security.Cryptography;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    private static async Task<PackageJoinBefore> ReloadNinjaRecoverySavedStateAsync(Fixture fixture, PackageJoin join,
        string line = PackageJoinNinjaLine)
    {
        var text = Encoding.UTF8.GetString(File.ReadAllBytes(fixture.CharacterPath));
        Assert.DoesNotContain("MsgNinja=", text, StringComparison.Ordinal);
        Assert.DoesNotContain("SecurityPass=", text, StringComparison.Ordinal);
        text = text.Replace("[Unrelated]", "MsgNinja=" + line + Environment.NewLine +
            "SecurityPass=111,0,0" + Environment.NewLine + "[Unrelated]", StringComparison.Ordinal);
        File.WriteAllBytes(fixture.CharacterPath, new UTF8Encoding(false).GetBytes(text));
        WriteSashField(fixture, "Online", "0");
        await join.RestartAsync();
        Assert.Equal(5051, join.Current.Character.Class);
        return CapturePackageJoin(fixture, join);
    }

    private static LegacyItemUidProducerIntentSnapshot PrepareNinjaRecoveryIntent(Fixture fixture, PackageJoin join,
        PackageJoinBefore before, bool claimAll, bool allocated)
    {
        var recipe = CreatePackageJoinStackRecipe(fixture, join, before, claimAll, job: 5051);
        var payload = recipe.CopyPayload();
        var request = new LegacyItemUidReservationRequest(recipe.OperationId, LegacyMailboxPackageProducer.Writer,
            recipe.AccountId, recipe.CharacterUid, Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant(), recipe.Count);
        Assert.True(join.Registry.TryPrepareProducerIntent(join.Domain, request, payload, out var intent, out var failure), failure);
        if (allocated) Assert.True(join.Registry.TryReserveProducerIntent(join.Domain, request, payload, out intent, out failure), failure);
        Assert.Equal(allocated, intent!.Allocation is not null);
        return intent;
    }

    private static void AssertNinjaRecoveryIntentRetained(PackageJoin join, LegacyItemUidProducerIntentSnapshot intent)
    {
        Assert.True(join.Registry.TryReadProducerIntent(join.Domain, intent.Request.OperationId, out var retained, out var reason), reason);
        Assert.Equal(intent.Request, retained!.Request);
        Assert.Equal(intent.PreparedGeneration, retained.PreparedGeneration);
        Assert.Equal(intent.Allocation, retained.Allocation);
        Assert.Equal(intent.CopyPayload(), retained.CopyPayload());
    }
}
