using WolfConquer.Core.World;
using Leaves = WolfConquer.Core.Data.LegacyNativeServerDatabasePersistence;
using TitleType = WolfConquer.Core.World.LegacyNativeMsgTitleStorage.TitleType;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeTitleProfileIntegrationTests
{
    [Fact]
    public Task AddedTitleAndWingRoundTripThroughProfileLoadActivationAndBothRemovals() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        scope.Row(2001, 2001, 2, 7);
        scope.Row(4001, 4001, 3, 11);
        var source = scope.Source.Owner;
        var loaded = scope.Reloaded.Owner;
        // Explicit selected native field inputs. The missing Equip/Process owner
        // is not fabricated; Add itself does not select either physical ID.
        source.Player.SpecialTitleID = 20010002;
        source.Player.SpecialWingID = 40010003;
        source.Player.SpecialTitleScore = 999;
        source.Player.AddSpecialTitle((TitleType)2001, scope.Source.Packet);
        source.Player.AddSpecialTitle((TitleType)4001, scope.Source.Packet);
        Frames(scope.Source.Queue, Frame(0, 0, 2001, 2), Frame(6, 100, 2001, 2),
            Frame(0, 7, 4001, 3), Frame(6, 100, 4001, 3));
        const string profile = "2,2001,1,4001,1,";
        Assert.Equal(profile, Leaves.GetSpecialTitles(source));
        Assert.Equal(999u, source.Player.SpecialTitleScore);
        Assert.Empty(loaded.Player.SpecialTitles);
        Assert.NotSame(source.Player.SpecialTitles, loaded.Player.SpecialTitles);
        var physicalList = loaded.Player.SpecialTitles;

        Leaves.LoadSpecialTitles(loaded, profile);

        Assert.Same(physicalList, loaded.Player.SpecialTitles);
        Assert.Equal(new uint[] { 2001, 4001 }, physicalList.Select(value => (uint)value));
        Assert.Equal(20010002u, loaded.Player.SpecialTitleID);
        Assert.Equal(40010003u, loaded.Player.SpecialWingID);
        Assert.Equal(7u, loaded.Player.SpecialTitleScore); // Reconstructed from metadata, not source's999.
        Assert.Equal(18u, loaded.Player.GetTitlesScore());
        Assert.Equal(profile, Leaves.GetSpecialTitles(loaded));
        Assert.Empty(scope.Reloaded.Queue);
        loaded.Player.ActiveSpecialTitles(scope.Reloaded.Packet);
        Frames(scope.Reloaded.Queue, Frame(0, 18, 2001, 2, titleValue: 100), Frame(0, 18, 4001, 3, titleValue: 100));
        var retainedActivation = scope.Reloaded.Queue.ToArray();
        scope.Reloaded.Queue.Clear();

        loaded.Player.RemoveSpecialTitle((TitleType)2001, scope.Reloaded.Packet);

        Frames(scope.Reloaded.Queue, Frame(5, 0, 2001, 2, title: false), Frame(5, 11, 2001, 2),
            Frame(0, 11, 2001, 2), Frame(3, 11, 2001, 2));
        Assert.Same(physicalList, loaded.Player.SpecialTitles);
        Assert.Equal((TitleType)4001, Assert.Single(physicalList));
        Assert.Equal(0u, loaded.Player.SpecialTitleID); Assert.Equal(0u, loaded.Player.SpecialTitleScore);
        Assert.Equal(40010003u, loaded.Player.SpecialWingID);
        Assert.Equal("1,4001,1,", Leaves.GetSpecialTitles(loaded));
        Assert.Equal(Frame(0, 18, 2001, 2, titleValue: 100), retainedActivation[0]);
        Assert.Equal(Frame(0, 18, 4001, 3, titleValue: 100), retainedActivation[1]);
        scope.Reloaded.Queue.Clear();

        loaded.Player.RemoveSpecialTitle((TitleType)4001, scope.Reloaded.Packet);

        Frames(scope.Reloaded.Queue, Frame(5, 0, 4001, 3, title: false), Frame(5, 0, 4001, 3),
            Frame(0, 0, 4001, 3), Frame(3, 0, 4001, 3));
        Assert.Empty(physicalList); Assert.Equal(0u, loaded.Player.SpecialWingID);
        Assert.Equal("0,", Leaves.GetSpecialTitles(loaded));
        Assert.Equal(profile, Leaves.GetSpecialTitles(source));
        Assert.Equal(999u, source.Player.SpecialTitleScore);
        Assert.Equal(Frame(3, 0, 4001, 3), NativeSpellTestFixture.Bytes(scope.Reloaded.Packet));
        Assert.Equal(4, scope.Reloaded.Packet.Position);
    });

    [Fact]
    public Task AliasedMetadataRoundTripThenLateRemoveFaultPreservesExactQueuedPrefixAndSavablePartialState() => NativePacketTestThread.Run(() =>
    {
        using var scope = new Scope();
        var titleRow = scope.Row(2001, 2003, 9, 7);
        var wingRow = scope.Row(4001, 6001, 5, 11);
        // These are actual additional dictionary keys pointing to the same real
        // metadata rows, so Active's physical-ID quotient lookups remain genuine.
        scope.Rows.Add(2003, titleRow);
        scope.Rows.Add(6001, wingRow);
        var source = scope.Source.Owner;
        var loaded = scope.Reloaded.Owner;
        source.Player.SpecialTitleID = 20010009;
        source.Player.SpecialWingID = 40010005;
        source.Player.AddSpecialTitle((TitleType)2001, scope.Source.Packet);
        source.Player.AddSpecialTitle((TitleType)4001, scope.Source.Packet);
        Frames(scope.Source.Queue, Frame(0, 0, 2003, 9), Frame(6, 100, 2003, 9),
            Frame(0, 7, 6001, 5), Frame(6, 100, 6001, 5));
        string profile = Leaves.GetSpecialTitles(source);
        Assert.Equal("2,2001,1,4001,1,", profile);

        Leaves.LoadSpecialTitles(loaded, profile);
        loaded.Player.ActiveSpecialTitles(scope.Reloaded.Packet);

        Assert.Equal(new uint[] { 2001, 4001 }, loaded.Player.SpecialTitles.Select(value => (uint)value));
        Assert.Equal(20030009u, loaded.Player.SpecialTitleID);
        Assert.Equal(60010005u, loaded.Player.SpecialWingID);
        Assert.Equal(7u, loaded.Player.SpecialTitleScore);
        Assert.Equal("2,2001,0,4001,0,", Leaves.GetSpecialTitles(loaded)); // Raw list keys no longer match physical quotients.
        Assert.Same(titleRow, scope.Rows[2003]); Assert.Same(wingRow, scope.Rows[6001]);
        Frames(scope.Reloaded.Queue, Frame(0, 18, 2003, 9, titleValue: 100), Frame(0, 18, 6001, 5, titleValue: 100));
        var physicalList = loaded.Player.SpecialTitles;
        scope.Rows[4001] = null!; // Real metadata fault after the successful load/activation chain.

        Assert.Throws<NullReferenceException>(() => loaded.Player.RemoveSpecialTitle((TitleType)2001, scope.Reloaded.Packet));

        Frames(scope.Reloaded.Queue, Frame(0, 18, 2003, 9, titleValue: 100), Frame(0, 18, 6001, 5, titleValue: 100),
            Frame(5, 0, 2003, 9, title: false));
        Assert.Same(physicalList, loaded.Player.SpecialTitles);
        Assert.Equal((TitleType)4001, Assert.Single(physicalList));
        Assert.Equal(20030009u, loaded.Player.SpecialTitleID);
        Assert.Equal(60010005u, loaded.Player.SpecialWingID);
        Assert.Equal(7u, loaded.Player.SpecialTitleScore);
        Assert.Equal("1,4001,0,", Leaves.GetSpecialTitles(loaded)); // Get does not inspect the broken metadata row.
        Assert.Equal(profile, Leaves.GetSpecialTitles(source));
        Assert.Equal(Frame(5, 0, 2003, 9, title: false), NativeSpellTestFixture.Bytes(scope.Reloaded.Packet));
        Assert.Equal(4, scope.Reloaded.Packet.Position);
    });
}
