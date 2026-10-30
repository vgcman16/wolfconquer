using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeRuneStorageSafetyTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void GenuineComparerCannotReenterAddDuringAddOrLoadPreparation(bool load)
    {
        using var scope=new Scope(); var first=Item(7,101); Assert.True(scope.Rune.Add(first));
        var comparer=InstallComparer(scope); var dictionary=scope.Dictionary; var cache=scope.Cache;
        bool? nested=null; comparer.Trigger=7; comparer.OnHash=()=>nested=scope.Rune.Add(Item(99,105));
        var second=Item(8,103);
        if(load)
        {
            lock(scope.Client.SpellAdmissionSyncRoot)
            lock(scope.Client.Player.SpellAdmissionSyncRoot)
            lock(scope.Rune.StorageSyncRoot)
            {
                var prepared=scope.Rune.CaptureLoadPublication(scope.Client,scope.Client.Player);
                prepared.Prepare(new[]{second}); Assert.False(nested!.Value);
                Assert.Same(dictionary,scope.Dictionary); Assert.Same(cache,scope.Cache);
                Assert.True(prepared.IsCurrent()); prepared.PublishUnchecked();
            }
        }
        else Assert.True(scope.Rune.Add(second));
        Assert.False(nested!.Value); Assert.Equal(new[]{first,second},scope.Rune.Objects); Assert.False(scope.Rune.ContainsUID(99));
    }
}
