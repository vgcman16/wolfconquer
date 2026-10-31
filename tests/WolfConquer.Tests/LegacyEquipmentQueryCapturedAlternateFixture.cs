using System.Reflection;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private sealed partial class DirectQueryStagesFixture
    {
        public void AssertCapturedQueryCannotCompleteCurrentEquipmentCaches()
        {
            Assert.True(LegacyNativeClientPublication.TryCaptureIdentity(_sessions, _connections[PlayerUid], PlayerUid, out var identity));
            var entries = typeof(LegacyNativeMagicQueryClient).GetField("Entries",
                BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
            object?[] arguments = [identity, null];
            Assert.True(Assert.IsType<bool>(entries.GetType().GetMethod("TryGetValue")!.Invoke(entries, arguments)));
            Assert.NotNull(arguments[1]);
            var entry = arguments[1]!;
            var epoch = Assert.IsType<LegacyNativeMagicQueryClient.QueryEpoch>(entry.GetType().GetField("Epoch")!.GetValue(entry));
            Assert.False(LegacyNativeMagicQueryClient.TryCompleteWindWalker(_sessions, _connections[PlayerUid],
                PlayerUid, identity, epoch, out var completed));
            Assert.Null(completed);
            // No admitted Monk binding exists in this component fixture.
            // Actual owned Monk drift tests retain that permission coverage.
            Assert.False(LegacyNativeMagicQueryClient.TryCompleteStaminaCache(_sessions, _connections[PlayerUid],
                PlayerUid, identity, epoch));
        }

        public void AssertForeignCapturedSourceRefused(string foreignField)
        {
            var owner = Owner;
            var outputCount = Order.Count;
            var source = foreignField switch
            {
                "uid" => owner with { Character = owner.Character with { Uid = TargetPlayerUid } },
                "account" => owner with { AccountId = checked(owner.AccountId + 1) },
                "native-client" => owner with { NativeGameClient = new LegacyNativeGameClientRuntime() },
                _ => throw new ArgumentOutOfRangeException(nameof(foreignField)),
            };
            Assert.True(LegacyNativeClientPublication.TryCaptureIdentity(_sessions, _connections[PlayerUid], PlayerUid, out var identity));
            Assert.True(LegacyNativeMagicQueryClient.TryRead(_sessions, _connections[PlayerUid], PlayerUid, out _, identity));
            Assert.False(LegacyNativeMagicQueryClient.TryBeginQuery(_sessions, _connections[PlayerUid],
                PlayerUid, identity, out var epoch, source));
            Assert.Null(epoch);
            Assert.False(LegacyNativeMagicQueryClient.TryRead(_sessions, _connections[PlayerUid], PlayerUid, out _, identity));
            Assert.Same(owner, Owner);
            Assert.Equal(outputCount, Order.Count);
        }
    }
}
