using System.Collections;
using System.Reflection;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class OwnedDragonhowlNonPlayerTests
{
    [Fact]
    public Task ActualNonPlayerProcReservationIsAbsentFromBasicAuthorityAndBasicCleanupCannotConsumeIt() => Run(false, async f =>
    {
        var observed = false;
        f.Owner.BeforeSend = (recipient, bytes) =>
        {
            if (recipient != Actor || observed || !LegacyMsgUpdatePacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out var update) ||
                update.Uid != f.TargetUid || !update.Updates.Any(row => row.Type == LegacyMsgUpdateDataType.Hitpoints)) return;
            observed = true;
            var frame = f.Owner.Handler;
            var slots = Field(frame, "basicPhysicalSlots")!;
            Assert.True(TryReadTable(slots, f.Owner.Connection(Actor), out var slot));
            var reservation = Assert.IsType<WorldServerFrameHandler.BasicPhysicalAttempt>(Field(slot!, "Attempt"));
            var basicAttempts = Field(frame, "basicPhysicalAttempts")!;
            Assert.False(TryReadTable(basicAttempts, reservation, out var basicState));
            Assert.Null(basicState);
            // Read only the state issued by this actual proc. No test creates
            // an attempt, request, context, receiving decision or owner token.
            var attempts = Assert.IsAssignableFrom<IEnumerable>(Field(frame, "dragonhowlAttempts"));
            var actual = Assert.Single(attempts.Cast<object>().Select(entry =>
                entry.GetType().GetProperty("Value")!.GetValue(entry)!).Where(state =>
                ReferenceEquals(Field(state, "Reservation"), reservation)));
            Assert.Null(Field(actual, "PlayerAttempt"));
            var source = Field(actual, "Source")!;
            var context = Assert.IsType<WorldPacketHandlerContext>(Field(source, "Context"));
            Assert.False(frame.IsBasicPhysicalAttemptPacketCurrent(context, reservation, f.Request()));
            var stamp = f.Binding.Player.AttackStamp;
            var files = f.Owner.Data.Files();
            frame.EndBasicPhysicalAttempt(f.Owner.Store, reservation);
            Assert.Same(reservation, Field(slot!, "Attempt"));
            Assert.Equal(stamp, f.Binding.Player.AttackStamp);
            f.Owner.Data.AssertFiles(files);
        };
        await f.Attack();
        f.Owner.BeforeSend = null;
        Assert.True(observed, f.Owner.Log.ToString());
        Assert.Equal(InitialHp - Damage, f.Hp());
        Assert.True(f.Binding.Client.OnAutoAttack);
        AssertOneOutput(f, 0, sob: false);
        Assert.False(f.Owner.Recovery.IsBlocked);
    });

    private static object? Field(object owner, string name) => owner.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(owner);

    private static bool TryReadTable(object table, object key, out object? value)
    {
        var arguments = new object?[] { key, null };
        var result = Assert.IsType<bool>(table.GetType().GetMethod("TryGetValue")!.Invoke(table, arguments));
        value = arguments[1]; return result;
    }
}
