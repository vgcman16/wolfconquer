using Quest = WolfConquer.Core.Data.LegacyNativeQuestInfo;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeQuestMetadataTests
{
    [Theory]
    [InlineData(null, 0, 0, 0, 0, null)]
    [InlineData("", 0, 0, 0, 0, null)]
    [InlineData("17", 17, 0, 0, 0, null)]
    [InlineData("17,65536,19,20,N,M", 17, 0, 0, 0, null)]
    [InlineData("17,18,19,bad,N,M", 17, 18, 19, 0, null)]
    [InlineData("17,18,19,20,N", 17, 18, 19, 20, "N")]
    public void NpcConstructorCatchesFaultAfterEachAlreadyAssignedPhysicalPrefix(string? line, int id, int map, int x, int y, string? name)
    {
        using var scope = new Scope(); var npc = new Quest.DBQuest.NpcInfo(line!);
        Assert.Equal((uint)id, npc.ID); Assert.Equal((ushort)map, npc.Map); Assert.Equal((ushort)x, npc.X); Assert.Equal((ushort)y, npc.Y);
        Assert.Equal(name, npc.Name); Assert.Null(npc.MapName);
    }

    [Theory]
    [InlineData(1005u, true)]
    [InlineData(1006u, true)]
    [InlineData(1045u, true)]
    [InlineData(3006u, true)]
    [InlineData(2006u, false)]
    [InlineData(10100u, false)]
    [InlineData(uint.MaxValue, false)]
    public void ProfessionFilterUsesNativeModuloAndThousandClassNormalization(uint playerClass, bool expected)
    {
        using var scope = new Scope(); var row = Row(17, 1005, 3005);
        Assert.Equal(expected, row.CanAcceptQuest(playerClass));
        Assert.Equal(new uint[] { 1005, 3005 }, row.Profession);
    }

    [Fact]
    public void ZeroOrOneProfessionPermitsAnyClassButNullProfessionFaults()
    {
        using var scope = new Scope(); var row = Row(17);
        Assert.True(row.CanAcceptQuest(uint.MaxValue)); row.Profession = new uint[] { 999 };
        Assert.True(row.CanAcceptQuest(1)); row.Profession = null!;
        Assert.Throws<NullReferenceException>(() => row.CanAcceptQuest(1006));
    }

    [Fact]
    public void ExplicitQuestIdBypassesBothProfessionAndPredicateAndUsesNpcListNotGlobalCatalog()
    {
        using var scope = new Scope(); var first = Row(17); first.Profession = null!;
        var duplicate = Row(17, 1005, 3005); Npc(7, first, duplicate);
        Quest.AllQuests.Add(17, duplicate);
        int called = 0;
        Assert.Same(first, Quest.GetFinishQuest(7, 1, 17, _ => { called++; throw new InvalidOperationException(); }));
        Assert.Equal(0, called); Assert.Null(Quest.GetFinishQuest(7, 1, 99, _ => { called++; return true; }));
        Assert.Equal(0, called);
    }

    [Fact]
    public void ZeroQuestIdFiltersBeforePredicateInPhysicalListOrderAndUsesLiveObjects()
    {
        using var scope = new Scope(); var denied = Row(1, 9, 8); var two = Row(2, 1005, 3005); var three = Row(3, 1005, 3005);
        Npc(7, denied, two, three); var seen = new List<ushort>();
        Assert.Same(three, Quest.GetFinishQuest(7, 1045, P: row => { seen.Add(row.MissionId); return row.MissionId == 3; }));
        Assert.Equal(new ushort[] { 2, 3 }, seen);
        two.Profession[0] = 9; three.Profession[0] = 9;
        Assert.Null(Quest.GetFinishQuest(7, 1045));
        denied.Profession = Array.Empty<uint>(); Assert.Same(denied, Quest.GetFinishQuest(7, 1045));
    }

    [Fact]
    public void PredicateExceptionEscapesAfterEligibilityAndNoLaterRowIsVisited()
    {
        using var scope = new Scope(); Npc(7, Row(1, 8, 9), Row(2, 1005, 3005), Row(3, 1005, 3005));
        var seen = new List<ushort>(); var failure = new InvalidOperationException("Owned predicate failure");
        Assert.Same(failure, Assert.Throws<InvalidOperationException>(() => Quest.GetFinishQuest(7, 1005,
            P: row => { seen.Add(row.MissionId); throw failure; })));
        Assert.Equal(new ushort[] { 2 }, seen);
    }

    [Fact]
    public void MissingNpcAndCorruptPhysicalListFaultBeforeAnyPredicateOrDefaultFallback()
    {
        using var scope = new Scope(); int called = 0;
        Assert.Throws<KeyNotFoundException>(() => Quest.GetFinishQuest(7, 1005, P: _ => { called++; return true; }));
        Quest.NpcQuests.Add(7, null!);
        Assert.Throws<NullReferenceException>(() => Quest.GetFinishQuest(7, 1005));
        Quest.NpcQuests[7] = new Quest.ListNpcQuests { Quests = null! };
        Assert.Throws<NullReferenceException>(() => Quest.GetFinishQuest(7, 1005));
        Quest.NpcQuests[7].Quests = new() { null! };
        Assert.Throws<NullReferenceException>(() => Quest.GetFinishQuest(7, 1005, 17));
        Assert.Equal(0, called);
    }

    [Fact]
    public void KingdomLookupUsesActualMutableListAndAcceptsPhysicalDuplicateEntries()
    {
        using var scope = new Scope(); Quest.KingDomMissions.Add(35024); Quest.KingDomMissions.Add(35024);
        Assert.True(Quest.IsKingDomMission(35024)); Quest.KingDomMissions.Remove(35024);
        Assert.True(Quest.IsKingDomMission(35024)); Quest.KingDomMissions.Clear(); Assert.False(Quest.IsKingDomMission(35024));
        Quest.KingDomMissions.Add(uint.MaxValue); Assert.True(Quest.IsKingDomMission(uint.MaxValue));
        Quest.KingDomMissions = null!; Assert.Throws<NullReferenceException>(() => Quest.IsKingDomMission(1));
    }
}
