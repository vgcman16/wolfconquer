using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyOperatorTestKitsTests
{
    [Theory]
    [InlineData("basic-trojan", 9)]
    [InlineData("socket-materials", 6)]
    public void RecipesUseConfirmedRowsWithoutGrantingOrPreOpeningAnything(string kit, int count)
    {
        var operation = Guid.NewGuid();
        var domain = Guid.NewGuid();
        var request = LegacyOperatorTestKits.Create(operation, domain, 2, 1293600, kit);
        uint[] expected = kit == "basic-trojan"
            ? [118003, 120003, 130003, 150003, 160013, 410003, 420003, 1088000, 1088001]
            : [700011, 700031, 1200005, 1200006, 1088000, 1088001];
        Assert.Equal(operation, request.OperationId);
        Assert.Equal(domain, request.ExpectedDomain);
        Assert.Equal(2u, request.AccountId);
        Assert.Equal(1293600u, request.CharacterUid);
        Assert.Equal(count, request.Items.Count);
        Assert.Equal(expected, request.Items.Select(item => item.ItemId));
        Assert.All(request.Items, item =>
        {
            Assert.Equal((ushort)1, item.StackSize);
            Assert.Equal((byte)0, item.Plus);
            Assert.False(item.Bound);
            Assert.False(item.FirstSocket);
            Assert.False(item.SecondSocket);
        });
        Assert.All(request.Title + request.SenderName + request.Content, value => Assert.InRange(value, ' ', '~'));
    }

    [Fact]
    public void ChangingOneRequestCannotRewriteFutureRecipesOrTheirRetainedOperationIds()
    {
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var domain = Guid.NewGuid();
        var first = LegacyOperatorTestKits.Create(firstId, domain, 2, 1293600, "basic-trojan");
        var firstRows = Assert.IsType<LegacyOperatorGearMailItem[]>(first.Items);
        firstRows[0] = new(1);
        var second = LegacyOperatorTestKits.Create(secondId, domain, 2, 1293600, "basic-trojan");
        Assert.Equal(118003u, second.Items[0].ItemId);
        Assert.Equal(firstId, first.OperationId);
        Assert.Equal(secondId, second.OperationId);
        Assert.NotSame(first.Items, second.Items);
    }

    [Theory]
    [InlineData(0u, 1u, "basic-trojan")]
    [InlineData(1u, 0u, "basic-trojan")]
    [InlineData(1u, 1u, "unknown")]
    [InlineData(1u, 1u, "BASIC-TROJAN")]
    public void InvalidIdentitiesOrUnknownRecipeCannotProduceARequest(uint account, uint character, string kit) =>
        Assert.ThrowsAny<ArgumentException>(() => LegacyOperatorTestKits.Create(Guid.NewGuid(), Guid.NewGuid(), account, character, kit));

    [Fact]
    public void EmptyOperationOrDomainCannotProduceARequest()
    {
        Assert.Throws<ArgumentException>(() => LegacyOperatorTestKits.Create(Guid.Empty, Guid.NewGuid(), 1, 1, "basic-trojan"));
        Assert.Throws<ArgumentException>(() => LegacyOperatorTestKits.Create(Guid.NewGuid(), Guid.Empty, 1, 1, "basic-trojan"));
    }
}
