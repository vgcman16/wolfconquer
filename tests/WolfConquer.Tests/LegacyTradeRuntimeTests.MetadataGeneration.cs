using WolfConquer.Core.Data;
using Fixture = WolfConquer.Tests.OwnedEquipmentBehaviorFixture;

namespace WolfConquer.Tests;

public sealed partial class LegacyTradeRuntimeTests
{
    [Fact]
    public Task AValueEqualReplacementCatalogCannotAuthorizeAnOldManagedComponentOffer() =>
        RunItemClosureAsync(async () =>
        {
            // This existing component is genuinely unbound; owned/native refusal
            // is covered through the actual Runtime/Frame fixture separately.
            var left = AddItemClosurePlayer(1_297_081, 1000, 50, [Item(10)]);
            var right = AddItemClosurePlayer(1_297_082, 2000, 60, [Item(20)]);
            await OpenItemClosureAsync(left, right);
            closureCatalog = LegacyItemWorldDataFixture.Create([Fixture.Item(1_088_001)], [], socialAssociations: closureRelations);
            var originalMetadata = closureCatalog.TradeItemMetadata;
            Assert.True(sessions.IsUnboundTradeItemOwnerCurrent(left.Connection, Current(left)));
            await SendItemClosureAsync(left, new(6) { ItemUID = 10 });
            Assert.True(trades.TryGet(left.Connection, out var party));
            Assert.Same(originalMetadata, Assert.Single(party.Items).Value.MetadataOwner);
            await SendItemClosureAsync(left, new(7) { Money = 300 });
            await SendItemClosureAsync(left, new(9));
            closureCatalog = LegacyItemWorldDataFixture.Create([Fixture.Item(1_088_001)], [], socialAssociations: closureRelations);
            Assert.NotSame(originalMetadata, closureCatalog.TradeItemMetadata);
            var leftStart = left.Sent.Count; var rightStart = right.Sent.Count;

            await SendItemClosureAsync(right, new(9));

            AssertItemClosureSettled(left, right, 1000, 2000);
            AssertInventory(left, 10); AssertInventory(right, 20);
            AssertItemClosureNoFinish(left, leftStart); AssertItemClosureNoFinish(right, rightStart);
            AssertItemClosureNoIncoming(right, rightStart, 10);
        });
}
