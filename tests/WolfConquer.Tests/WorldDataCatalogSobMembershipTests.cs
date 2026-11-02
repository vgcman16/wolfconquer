using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed class WorldDataCatalogSobMembershipTests
{
    private static SobNpc Row(int id, int map, string label) =>
        new(id, label, 0, 1, map, 10, 20, 100, 100, 0, null, null, 1, label, new[] { label });

    [Fact]
    public void ConstructorOwnsMembershipAndBothIndexesWithoutChangingBirthIdentityOrOrder()
    {
        var first = Row(101, 1000, "first");
        var second = Row(101, 1001, "second");
        var third = Row(102, 1000, "third");
        var donor = new List<SobNpc> { first, second, third };
        var catalog = LegacyItemWorldDataFixture.Create([], [], sobNpcs: donor);
        donor[0] = first with { Name = "replacement" }; donor.RemoveAt(1); donor.Add(Row(103, 1000, "added"));

        Assert.Equal(3, catalog.SobNpcs.Count);
        Assert.Same(first, catalog.SobNpcs[0]); Assert.Same(second, catalog.SobNpcs[1]); Assert.Same(third, catalog.SobNpcs[2]);
        Assert.Same(first, catalog.SobNpcsById[101][0]); Assert.Same(second, catalog.SobNpcsById[101][1]);
        Assert.Same(first, catalog.SobNpcsByMapId[1000][0]); Assert.Same(third, catalog.SobNpcsByMapId[1000][1]);
        Assert.False(catalog.SobNpcsById.ContainsKey(103)); Assert.Single(catalog.SobNpcsByMapId[1001]);
        Assert.Same(first.Fields, catalog.SobNpcs[0].Fields); // Raw metadata is deliberately not deep-owned here.
    }

    [Fact]
    public void ArrayDonorAndPublishedIndexInterfacesCannotReplaceMembership()
    {
        var first = Row(201, 1000, "first"); var second = Row(201, 1000, "second");
        SobNpc[] donor = [first, second];
        var catalog = LegacyItemWorldDataFixture.Create([], [], sobNpcs: donor);
        donor[0] = first with { Name = "foreign" };
        Assert.Same(first, catalog.SobNpcs[0]); Assert.Same(first, catalog.SobNpcsById[201][0]);
        Assert.Same(second, catalog.SobNpcsByMapId[1000][1]);
        Assert.False(catalog.SobNpcs is IList<SobNpc>); Assert.False(catalog.SobNpcsById[201] is IList<SobNpc>);
        Assert.False(catalog.SobNpcsByMapId[1000] is IList<SobNpc>);
        var idIndex = Assert.IsAssignableFrom<IDictionary<int, IReadOnlyList<SobNpc>>>(catalog.SobNpcsById);
        var mapIndex = Assert.IsAssignableFrom<IDictionary<int, IReadOnlyList<SobNpc>>>(catalog.SobNpcsByMapId);
        Assert.Throws<NotSupportedException>(() => idIndex[201] = new[] { donor[0] });
        Assert.Throws<NotSupportedException>(() => mapIndex.Clear());
        Assert.Same(first, catalog.SobNpcsById[201][0]); Assert.Same(first, catalog.SobNpcsByMapId[1000][0]);
    }
}
