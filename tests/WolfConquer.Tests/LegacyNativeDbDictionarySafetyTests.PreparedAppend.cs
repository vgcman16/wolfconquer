using WolfConquer.Core.Data;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeDbDictionarySafetyTests
{
    [Fact]
    public void PreparedAppendPublishesIntoTheSameRealOwnerWithNativeDeferredCacheAndOneRevision()
    {
        var owner=new LegacyNativeDbSafeDictionary<string,string>(4,StringComparer.OrdinalIgnoreCase);
        owner.Add("Kept","old"); owner.GetValues(); long revision=owner.Revision;
        lock(owner.SyncRoot)
        {
            var prepared=owner.CapturePreparedAppend();
            prepared.Prepare(new[]{new KeyValuePair<string,string>("Next","new")});
            Assert.Single(owner); Assert.False(owner.Update); Assert.Equal(revision,owner.Revision);
            Assert.True(prepared.IsCurrent()); prepared.PublishUnchecked();
        }
        Assert.Same(StringComparer.OrdinalIgnoreCase,owner.Comparer);
        Assert.Equal(revision+1,owner.Revision); Assert.True(owner.Update); Assert.Equal(new[]{"old"},owner.MyArray);
        Assert.Equal(new[]{"old","new"},owner.GetValues()); Assert.Equal("new",owner["NEXT"]);
    }

    [Fact]
    public void PreparedAppendComparerCannotReenterTheActualOwnerWriter()
    {
        var comparer=new ReenteringComparer(); var owner=new LegacyNativeDbSafeDictionary<string,string>(4,comparer);
        owner.Add("kept","old"); owner.GetValues(); long revision=owner.Revision;
        lock(owner.SyncRoot)
        {
            var prepared=owner.CapturePreparedAppend(); comparer.Attempt=()=>owner.Clear();
            Assert.Throws<InvalidOperationException>(()=>prepared.Prepare(new[]{new KeyValuePair<string,string>("next","new")}));
            Assert.True(prepared.IsCurrent());
        }
        Assert.Equal(revision,owner.Revision); Assert.False(owner.Update); Assert.Equal(new[]{"old"},owner.MyArray);
        Assert.Single(owner); Assert.Equal("old",owner["kept"]);
    }

    [Fact]
    public void PreparedAppendDetectsARealInterveningRevisionWithoutUndoingThatWriter()
    {
        var owner=new LegacyNativeDbSafeDictionary<string,string>(); owner.Add("kept","old");
        lock(owner.SyncRoot)
        {
            var prepared=owner.CapturePreparedAppend();
            prepared.Prepare(new[]{new KeyValuePair<string,string>("planned","unpublished")});
            owner.Add("other","actual");
            Assert.False(prepared.IsCurrent()); Assert.Equal("actual",owner["other"]); Assert.False(owner.ContainsKey("planned"));
        }
    }
}
