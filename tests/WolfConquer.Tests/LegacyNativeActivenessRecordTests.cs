using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeActivenessRecordTests
{
    [Fact]
    public void ExactConstructorAndRecordKeepNativeByteReadOfUInt32ActivityPoints()
    {
        var owner = new LegacyNativeGameClientRuntime();
        Assert.Null(owner.Activeness);
        var activity = new LegacyNativeActivenessRuntime(owner);
        Assert.Same(owner, activity.user);
        Assert.Equal("0/0/0/", activity.ToString());
        activity.ClaimRewards = 7; activity.ActivityPoints = 1000;
        activity.Tasks.Add(900, new() { ID = 2, Progres = 3, Completed = 4 });
        activity.Tasks.Add(800, new() { ID = 1, Progres = 5, Completed = 6 });
        Assert.Equal("7/1000/2/2/3/4/1/5/6/", activity.ToString());
        var loaded = new LegacyNativeActivenessRuntime(null!);
        loaded.Load(activity.ToString());
        Assert.Equal(0u, loaded.ActivityPoints); // Source Read((byte)0), not uint or unchecked byte cast.
        Assert.Equal((byte)7, loaded.ClaimRewards);
        Assert.Equal("7/0/2/2/3/4/1/5/6/", loaded.ToString());
        var prior = loaded.Tasks[2];
        loaded.Load("1/255/2/2/9/9/3/8/7/");
        Assert.Equal(255u, loaded.ActivityPoints);
        Assert.Same(prior, loaded.Tasks[2]);
        Assert.Equal((byte)3, prior.Progres);
        Assert.Equal((byte)8, loaded.Tasks[3].Progres);
    }

    [Fact]
    public Task WholeIncreasePerformsNativeTwoStepCompletionAndActualPacketSendsOnce() => NativePacketTestThread.Run(() =>
    {
        using var tables = new NativeSaveRecordTables();
        using var fixture = new NativeSpellTestFixture();
        var activity = new LegacyNativeActivenessRuntime(fixture.Owner);
        LegacyNativePoolSaveRecords.ActivityTasks[7] = new() { ID = 99, NeedProgress = 2, ActiveValue = 5 };
        activity.IncreaseTask(123); // Missing catalog is a genuine no-publication path.
        Assert.Empty(activity.Tasks); Assert.Empty(fixture.Queue);
        activity.IncreaseTask(7);
        var task = Assert.Single(activity.Tasks).Value;
        Assert.Equal(7u, task.ID); // Lookup key, not mutable metadata ID.
        Assert.Equal((byte)1, task.Progres); Assert.Equal((byte)0, task.Completed);
        Assert.Equal(0u, activity.ActivityPoints);
        Assert.Equal(Convert.FromHexString("0C000C090301070000000001"), fixture.Queue.Dequeue());
        activity.IncreaseTask(7);
        Assert.Equal((byte)2, task.Progres); Assert.Equal((byte)1, task.Completed);
        Assert.Equal(5u, activity.ActivityPoints);
        Assert.Equal(Convert.FromHexString("19007E08010200000001000000050000000200000002000000"), fixture.Queue.Dequeue());
        Assert.Equal(Convert.FromHexString("0C000C090301070000000102"), fixture.Queue.Dequeue());
        activity.IncreaseTask(7);
        Assert.Empty(fixture.Queue);
        Assert.Equal(5u, activity.ActivityPoints);
    });

    [Fact]
    public Task ExistingTasksBypassOpenGateZeroProgressCompletesImmediatelyAndTotalsWrap() => NativePacketTestThread.Run(() =>
    {
        using var tables = new NativeSaveRecordTables();
        using var fixture = new NativeSpellTestFixture();
        var activity = new LegacyNativeActivenessRuntime(fixture.Owner) { ActivityPoints = uint.MaxValue };
        LegacyNativePoolSaveRecords.ActivityTasks[1] = new() { ID = 1, NeedProgress = 0, ActiveValue = 2, OpenLev = 2130 };
        activity.IncreaseTask(1);
        Assert.Empty(activity.Tasks); // Cold real player does not pass2130 gate.
        activity.Tasks[1] = new() { ID = 1 };
        activity.IncreaseTask(1);
        Assert.Equal((byte)0, activity.Tasks[1].Progres);
        Assert.Equal((byte)1, activity.Tasks[1].Completed);
        Assert.Equal(1u, activity.ActivityPoints);
        Assert.Equal(2, fixture.Queue.Count);
    });

    [Fact]
    public Task CheckTasksReadsActualCachedMetadataValuesAndDoesNotReplaceAnExistingTask() => NativePacketTestThread.Run(() =>
    {
        using var tables = new NativeSaveRecordTables();
        using var fixture = new NativeSpellTestFixture();
        var activity = new LegacyNativeActivenessRuntime(fixture.Owner);
        var kept = new LegacyNativeActivenessRuntime.Task { ID = 3, Progres = 99, Completed = 9 };
        activity.Tasks[3] = kept;
        LegacyNativePoolSaveRecords.ActivityTasks.Add(90, new() { ID = 1 });
        LegacyNativePoolSaveRecords.ActivityTasks.Add(91, new() { ID = 2, OpenLev = 2130 });
        LegacyNativePoolSaveRecords.ActivityTasks.Add(92, new() { ID = 3 });
        activity.CheckTasks(fixture.Packet);
        Assert.Equal(new uint[] { 3, 1 }, activity.Tasks.Keys);
        Assert.Same(kept, activity.Tasks[3]);
        Assert.Equal((byte)99, kept.Progres);
        Assert.Equal(Convert.FromHexString("12000C090302030000000963010000000000"), fixture.Queue.Dequeue());
        Assert.Empty(fixture.Queue);
    });

    [Fact]
    public Task DirectSingleListClaimAndPointMethodsUseRealPrimitiveCodecs() => NativePacketTestThread.Run(() =>
    {
        using var fixture = new NativeSpellTestFixture();
        var activity = new LegacyNativeActivenessRuntime(fixture.Owner) { ClaimRewards = 255, ActivityPoints = uint.MaxValue };
        activity.UpdateClaimRewards(fixture.Packet);
        activity.UpdateActivityPoints(fixture.Packet);
        activity.UpdateSingleTask(new() { ID = 0x12345678, Completed = 9, Progres = 8 }, fixture.Packet);
        Assert.Equal(Convert.FromHexString("05003908FF"), fixture.Queue.Dequeue());
        Assert.Equal(Convert.FromHexString("19007E08010200000001000000FFFFFFFF0200000002000000"), fixture.Queue.Dequeue());
        Assert.Equal(Convert.FromHexString("0C000C090301785634120908"), fixture.Queue.Dequeue());
    });

    [Fact]
    public void WholeActivityCatalogLoaderRetainsNativeByteCountAndReplaceOnAddSemantics()
    {
        using var files = new NativeSaveRecordFiles();
        var path = Path.Combine(files.Root, "ActivityTask.ini");
        File.WriteAllText(path, "[ActivityTask]\r\nNum=2\r\n[1]\r\nProgress=7\r\nActiveValue=9\r\nOpenLev=2130\r\n[2]\r\nProgress=999\r\nActiveValue=255\r\nOpenLev=140\r\n");
        var table = new LegacyNativeActivityTaskTable();
        table.Load();
        Assert.Equal(new uint[] { 1, 2 }, table.Keys);
        Assert.Equal((byte)7, table[1].NeedProgress);
        Assert.Equal((byte)9, table[1].ActiveValue);
        Assert.Equal(2130u, table[1].OpenLev);
        Assert.Equal((byte)0, table[2].NeedProgress);
        var first = table[1];
        table.Load();
        Assert.NotSame(first, table[1]);
        File.WriteAllText(path, "[ActivityTask]\r\nNum=999\r\n");
        table.Load();
        Assert.Equal(2, table.Count); // ReadByte fails to0; no cache clearing.
    }
}
