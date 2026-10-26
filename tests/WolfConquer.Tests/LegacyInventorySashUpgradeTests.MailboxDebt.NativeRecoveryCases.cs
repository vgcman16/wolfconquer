using System.Text;
using System.Text.Json;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ActualD13FreshNativePendingRecoveryVerifiesSavedPinThenReplaysOnlyTheOriginalPackage(bool allocated) =>
        RunPackageJoinAsync(async (fixture, join) =>
        {
            // Synthetic saved PIN is added before pending work exists. Reload via
            // real Frame startup so the pinned recipe has the genuine saved owner.
            var text = Encoding.UTF8.GetString(File.ReadAllBytes(fixture.CharacterPath));
            Assert.DoesNotContain("SecurityPass=", text, StringComparison.Ordinal);
            text = text.Replace("[Unrelated]", "SecurityPass=111,0,0" + Environment.NewLine + "[Unrelated]", StringComparison.Ordinal);
            File.WriteAllBytes(fixture.CharacterPath, new UTF8Encoding(false).GetBytes(text));
            WriteSashField(fixture, "Online", "0");
            await join.RestartAsync();
            var initial = CapturePackageJoin(fixture, join);
            Assert.Equal("111,0,0", initial.Owner.Character.Attributes["SecurityPass"]);
            var intent = PrepareDebtIntent(fixture, join, initial, allocated);
            var pinnedFiles = fixture.PackageFiles();
            var firstLogin = fixture.Sent.Count;

            var native = await join.RestartNativeDebtRecoveryAsync();

            AssertPackageJoinFiles(fixture, pinnedFiles);
            Assert.Same(native, join.Current.NativeGameClient);
            Assert.Same(native, native.Player.Owner);
            Assert.Equal(Fixture.Uid, native.Player.UID);
            Assert.Equal(10U, native.Player.Class);
            Assert.Equal(PackageClassExperience, native.Player.ClassExperience);
            Assert.Equal(1003, native.Player.Body);
            Assert.Equal(initial.Owner.Character.Name, native.Player.Name);
            Assert.False(native.FullLoading);
            Assert.Equal(0U, native.Player.Online);
            Assert.Null(native.Beasts);
            Assert.Null(native.HairfaceStorage);
            Assert.Null(native.MyNinja);
            Assert.Null(native.CombatGear);
            AssertDebtPinnedResourcesEqual(initial.Owner, join.Current);
            Assert.False(native.Player.IsCheckedPass);
            var startup = fixture.Sent.Skip(firstLogin).ToArray();
            Assert.Equal(LegacyMsgMessagePacket.AnswerOk().ToFrameBytes(), startup[0]);
            var startupIds = startup.Select(bytes => ReadFrame(bytes).Header.Opcode).ToArray();
            Assert.Contains(WorldPacketOpcodes.LegacyServerVersion, startupIds);
            Assert.Contains(WorldPacketOpcodes.LegacyServerTimer, startupIds);
            Assert.Contains(WorldPacketOpcodes.LegacyServerConfig, startupIds);
            var loaderAt = Array.IndexOf(startupIds, WorldPacketOpcodes.LegacyLoaderHeroInfo);
            var protoAt = Array.IndexOf(startupIds, WorldPacketOpcodes.LegacyProtoHeroInfo);
            var pinAt = Array.IndexOf(startupIds, LegacySecondaryPasswordPacket.Opcode);
            Assert.True(loaderAt > 0 && protoAt > loaderAt && pinAt > protoAt);
            Assert.DoesNotContain(WorldPacketOpcodes.InternalSnapshotResponse, startupIds);
            Assert.Contains(startup, bytes => bytes.AsSpan().SequenceEqual(new LegacySecondaryPasswordPacket(5, 1, 0).ToFrameBytes()));

            var readonlyOwner = join.Current;
            var readonlyJson = JsonSerializer.Serialize(readonlyOwner);
            var first = fixture.Sent.Count;
            await join.FrameAsync(new MsgActionPacket { ObjId = Fixture.Uid, Type = MsgActionType.SetLocation }.ToFrameBytes());
            await DebtRegisteredAsync(fixture, ActualDebtContext(fixture, join), PackageJoinRequest(true), direct: true);
            Assert.Equal(first, fixture.Sent.Count);
            Assert.Same(readonlyOwner, join.Current);
            Assert.Equal(readonlyJson, JsonSerializer.Serialize(join.Current));
            AssertPackageJoinFiles(fixture, pinnedFiles);
            Assert.False(native.Player.IsCheckedPass);

            await AssertNativeDebtPinAsync(fixture, join, supplied: 999, action: 6, value: 0);
            Assert.False(native.Player.IsCheckedPass);
            AssertPackageJoinFiles(fixture, pinnedFiles);
            first = fixture.Sent.Count;
            await join.FrameAsync(PackageJoinRequest(true));
            Assert.Equal(first, fixture.Sent.Count);
            AssertPackageJoinFiles(fixture, pinnedFiles);

            await AssertNativeDebtPinAsync(fixture, join, supplied: 111, action: 5, value: 257);
            Assert.True(native.Player.IsCheckedPass);
            Assert.True(LegacySecondaryPasswordRuntime.IsAdmitted(join.Connection!, join.Current));
            AssertPackageJoinFiles(fixture, pinnedFiles);
            first = fixture.Sent.Count;
            await DebtRegisteredAsync(fixture, ActualDebtContext(fixture, join), PackageJoinRequest(true), direct: false);
            var committed = AssertPackageJoinAllocation(fixture, join);
            Assert.Equal(intent.Request.OperationId, committed.Request.OperationId);
            Assert.Equal(intent.CopyPayload(), committed.CopyPayload());
            AssertDebtPackageCommitted(fixture, join, initial, committed);
            Assert.Equal(1031U, native.Player.ClassExperience);
            Assert.True(native.Player.IsCheckedPass);
            Assert.False(native.FullLoading);
            Assert.Equal("111,0,0", join.Current.Character.Attributes["SecurityPass"]);
            Assert.Equal(PackageJoinRequest(true), fixture.Sent[^1]);
            Assert.True(fixture.Sent.Count > first);
            var completedFiles = fixture.PackageFiles();
            first = fixture.Sent.Count;
            await join.FrameAsync(PackageJoinRequest(true));
            AssertPackageJoinFiles(fixture, completedFiles);
            AssertPackageJoinReplay(fixture, true, first);
            Assert.Equal(1031U, native.Player.ClassExperience);
            AssertPackageJoinAllocation(fixture, join);
        });

    private static async Task AssertNativeDebtPinAsync(Fixture fixture, PackageJoin join, uint supplied, uint action, uint value)
    {
        var first = fixture.Sent.Count;
        await join.FrameAsync(new LegacySecondaryPasswordPacket(1, 0, supplied).ToFrameBytes());
        var replies = fixture.Sent.Skip(first).ToArray();
        Assert.Equal(action == 5 ? 2 : 1, replies.Length);
        Assert.Equal(new LegacySecondaryPasswordPacket(action, value, 0).ToFrameBytes(), replies[0]);
        if (action == 5)
            Assert.Equal(LegacyMsgMessagePacket.SystemTalk("Secondary verified!").ToFrameBytes(), replies[1]);
    }
}
