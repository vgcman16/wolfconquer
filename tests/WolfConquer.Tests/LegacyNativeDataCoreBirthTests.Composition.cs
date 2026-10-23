using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeDataCoreBirthTests
{
    [Fact]
    public Task WholeLoadClientConstructsEveryGenuineOwnerAndBothAchievementOwnersWithoutPackets()=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();var oldSpells=scope.Client.MySpells;
        Assert.Null(scope.Client.Rune);Assert.Null(scope.Client.MyMail);Assert.Null(scope.Client.Achievement);
        LegacyNativeDataCoreAttributes.LoadClient(scope.Player);AssertRealOwners(scope.Client);
        Assert.NotSame(oldSpells,scope.Client.MySpells);Assert.NotNull(scope.Client.Achievement);Assert.NotNull(scope.Player.Achievement);
        Assert.Equal(14,scope.Client.Achievement!.Value.Length);Assert.Equal(14,AchievementBits(scope.Player.Achievement!).Length);
        Assert.NotSame(scope.Client.Achievement.Value,AchievementBits(scope.Player.Achievement!));Assert.All(AchievementBits(scope.Player.Achievement!),word=>Assert.Equal(0u,word));
        Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task RepeatWholeLoadReplacesAllSevenOwnersButPreservesExistingDatabaseAchievementAndOldAliases()=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();scope.Client.Achievement=new(){Key=71};scope.Client.Achievement.Value[0]=0xAABBCCDD;
        var collection=scope.Client.Achievement;LegacyNativeDataCoreAttributes.LoadClient(scope.Player);var first=Members(scope.Client);var firstAchievement=scope.Player.Achievement;
        var item=new LegacyNativeGameItemRuntime{Uid=91};scope.Client.Inventory!.AddDBItem(item);
        var proficiency=new LegacyNativeMsgProficiency{ID=123};scope.Client.MyMail!.Mails[123]=proficiency;
        collection.Value[0]=17;LegacyNativeDataCoreAttributes.LoadClient(scope.Player);var second=Members(scope.Client);
        for(int i=0;i<7;i++)Assert.NotSame(first[i],second[i]);AssertRealOwners(scope.Client);
        Assert.Same(collection,scope.Client.Achievement);Assert.Equal(71,collection.Key);Assert.NotSame(firstAchievement,scope.Player.Achievement);
        Assert.Equal(0xAABBCCDDu,AchievementBits(firstAchievement!)[0]);Assert.Equal(17u,AchievementBits(scope.Player.Achievement!)[0]);
        Assert.Same(item,Assert.Single(((LegacyNativeInventoryRuntime)first[1]).ClientItems).Value);
        Assert.Same(proficiency,Assert.Single(((LegacyNativeRoleMailRuntime)first[6]).Mails).Value);Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task SuppliedPlayerReceivesAchievementWhileItsRealOwnerReceivesMembersEvenIfCurrentPlayerDiffers()=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();var supplied=scope.Player;var replacement=new LegacyNativePlayerRuntime(scope.Client);
        scope.Client.Player=replacement;LegacyNativeDataCoreAttributes.LoadClient(supplied);
        AssertRealOwners(scope.Client);Assert.Same(replacement,scope.Client.Player);Assert.Same(scope.Client,supplied.Owner);
        Assert.NotNull(supplied.Achievement);Assert.Null(replacement.Achievement);Assert.Empty(scope.Queue);
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task NullOrTooLongAchievementArrayFaultsAfterAllSevenRealMemberAssignmentsAndKeepsPriorPlayerAchievement(bool tooLong)=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();LegacyNativeDataCoreAttributes.LoadClient(scope.Player);var previous=Members(scope.Client);var playerAchievement=scope.Player.Achievement;
        var collection=scope.Client.Achievement!;collection.Value=tooLong?new uint[15]:null!;
        if(tooLong)Assert.Throws<IndexOutOfRangeException>(()=>LegacyNativeDataCoreAttributes.LoadClient(scope.Player));
        else Assert.Throws<NullReferenceException>(()=>LegacyNativeDataCoreAttributes.LoadClient(scope.Player));
        var current=Members(scope.Client);for(int i=0;i<7;i++)Assert.NotSame(previous[i],current[i]);AssertRealOwners(scope.Client);
        Assert.Same(collection,scope.Client.Achievement);Assert.Same(playerAchievement,scope.Player.Achievement);Assert.Empty(scope.Queue);
    });

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(13)] [InlineData(14)]
    public Task WholeLoadCopiesExactlyCurrentAchievementArrayLengthWithoutRequiringFourteenWords(int count)=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();uint[] input=Enumerable.Range(1,count).Select(i=>(uint)i).ToArray();scope.Client.Achievement=new(){Value=input};
        LegacyNativeDataCoreAttributes.LoadClient(scope.Player);var words=AchievementBits(scope.Player.Achievement!);
        Assert.Equal(14,words.Length);Assert.Equal(input,words.Take(count).ToArray());Assert.All(words.Skip(count),word=>Assert.Equal(0u,word));
        Assert.NotSame(input,words);AssertRealOwners(scope.Client);Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task NullPlayerFaultDoesNotReplaceAnyExistingMemberOrCreateAchievement()=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();var spell=scope.Client.MySpells;
        Assert.Throws<NullReferenceException>(()=>LegacyNativeDataCoreAttributes.LoadClient(null!));
        Assert.Same(spell,scope.Client.MySpells);Assert.Null(scope.Client.Rune);Assert.Null(scope.Client.MyMail);Assert.Null(scope.Client.Achievement);Assert.Empty(scope.Queue);
    });
}
