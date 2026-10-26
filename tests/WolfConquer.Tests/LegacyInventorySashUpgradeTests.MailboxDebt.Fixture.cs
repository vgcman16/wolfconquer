using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    private static readonly PacketHandlerRegistry<WorldPacketHandlerContext> DebtRegistry =
        PacketHandlerRegistry<WorldPacketHandlerContext>.FromTypes([typeof(WorldPacketHandlers)]);

    private static LegacyItemUidProducerIntentSnapshot PrepareDebtIntent(Fixture fixture, PackageJoin join,
        PackageJoinBefore initial, bool allocated = true)
    {
        var recipe = CreatePackageJoinRecipe(fixture, join, initial);
        var payload = recipe.CopyPayload();
        var request = new LegacyItemUidReservationRequest(recipe.OperationId, LegacyMailboxPackageProducer.Writer,
            recipe.AccountId, recipe.CharacterUid, Convert.ToHexString(SHA256.HashData(payload)).ToLowerInvariant(), recipe.Count);
        Assert.True(join.Registry.TryPrepareProducerIntent(join.Domain, request, payload, out var intent, out var reason), reason);
        if (allocated) Assert.True(join.Registry.TryReserveProducerIntent(join.Domain, request, payload, out intent, out reason), reason);
        Assert.Equal(allocated, intent!.Allocation is not null);
        return intent;
    }

    private static T ActualDebtFrameOwner<T>(PackageJoin join, string field) where T : class
    {
        Assert.NotNull(join.Handler);
        var member = typeof(WorldServerFrameHandler).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(member);
        return Assert.IsType<T>(member.GetValue(join.Handler)); // Real read-only owner observation; no injection.
    }

    private static WorldPacketHandlerContext ActualDebtContext(Fixture fixture, PackageJoin join) =>
        new(join.Connection!, fixture.Log, join.Current,
            WorldData: ActualDebtFrameOwner<WorldDataCatalog>(join, "_worldData"), Sessions: join.SessionStore,
            PlayerItemsRoot: fixture.Items, Mailboxes: ActualDebtFrameOwner<LegacyMailboxStore>(join, "_mailboxes"),
            ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13)
        {
            Trades = ActualDebtFrameOwner<LegacyTradeStateStore>(join, "_tradeStates"),
            TradeOperations = ActualDebtFrameOwner<WorldOperationBarrier>(join, "_worldOperations"),
            TradeRecovery = ActualDebtFrameOwner<LegacyTradeRecoveryState>(join, "_tradeRecovery"),
            MailboxPackages = ActualDebtFrameOwner<LegacyMailboxPackageProducer>(join, "_mailboxPackages"),
        };

    private static async Task DebtRegisteredAsync(Fixture fixture, WorldPacketHandlerContext context,
        byte[] bytes, bool direct)
    {
        var frame = ReadFrame(bytes);
        Assert.True(DebtRegistry.TryGetHandler(frame.Header.Opcode, out var handler));
        var actual = (direct ? handler(context, frame, CancellationToken.None) :
            DebtRegistry.HandleAsync(context, frame, CancellationToken.None)).AsTask();
        fixture.Calls.Add(actual);
        await fixture.ObserveAsync(actual);
    }

    private static void AssertDebtRetained(Fixture fixture, PackageJoin join, PackageJoinBefore before,
        LegacyItemUidProducerIntentSnapshot original)
    {
        AssertPackageJoinUnchanged(fixture, join, before);
        Assert.True(join.Registry.TryReadProducerIntent(join.Domain, original.Request.OperationId, out var current, out var reason), reason);
        Assert.Equal(original.Request, current!.Request);
        Assert.Equal(original.PreparedGeneration, current.PreparedGeneration);
        Assert.Equal(original.Allocation, current.Allocation);
        Assert.Equal(original.CopyPayload(), current.CopyPayload());
        Assert.Equal(before.Json, JsonSerializer.Serialize(join.Current));
    }
}
