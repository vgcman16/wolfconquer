using System.Collections;
using System.Reflection;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Fact]
    public void SobPrimaryObservationDoesNotMaterializeAnAbsentRuntimeRow()
    {
        var source = CreateEventSobNpc() with { Life = 100, MaxLife = 100 };
        var states = new SobNpcRuntimeStateStore();
        var rows = (IDictionary)typeof(SobNpcRuntimeStateStore).GetField("_states", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(states)!;
        Assert.Empty(rows);
        Assert.True(states.TryCaptureMagicPrimaryTarget(source, out var captured));
        Assert.NotNull(captured);
        Assert.Equal(100, captured.Visible.Life);
        Assert.True(captured.TryEnter(out var lease));
        using (lease) { Assert.True(lease!.IsCurrentPlain()); Assert.Empty(rows); }
        Assert.Empty(rows);
    }

    [Fact]
    public void SobPrimaryObservationUsesTheRealRuntimeDeadAfterimage()
    {
        var source = CreateEventSobNpc() with { Life = 100, MaxLife = 100 };
        var states = new SobNpcRuntimeStateStore();
        var death = states.ApplyDamage(source, 100, DateTimeOffset.UtcNow, LegacySobNpcDeathBehavior.PersistentDeath);
        Assert.True(death.Killed);
        Assert.Equal(100, source.Life);
        Assert.True(states.TryCaptureMagicPrimaryTarget(source, out var captured));
        Assert.Equal(0, captured!.Visible.Life);
        Assert.True(captured.TryEnter(out var lease));
        using (lease) Assert.True(lease!.IsCurrentPlain());
    }

    [Fact]
    public void SobPrimaryObservationRefusesARealDamageMutationBeforeItsLease()
    {
        var source = CreateEventSobNpc() with { Life = 100, MaxLife = 100 };
        var states = new SobNpcRuntimeStateStore();
        _ = states.GetVisible(source);
        Assert.True(states.TryCaptureMagicPrimaryTarget(source, out var captured));
        Assert.Equal(1U, states.ApplyDamage(source, 1, DateTimeOffset.UtcNow,
            LegacySobNpcDeathBehavior.PersistentDeath).DamageApplied);
        Assert.False(captured!.TryEnter(out var lease));
        Assert.Null(lease);
        Assert.Equal(99, states.GetVisible(source).Life);
    }

    [Fact]
    public void SobPrimaryObservationDoesNotAcceptAnEqualVisibleGuildOwnerAba()
    {
        var source = CreateEventSobNpc() with { Life = 100, MaxLife = 100 };
        var states = new SobNpcRuntimeStateStore();
        var before = states.GetVisible(source);
        Assert.True(states.TryCaptureMagicPrimaryTarget(source, out var captured));
        states.SetOwnerGuildId(source, 7);
        states.SetOwnerGuildId(source, 0);
        Assert.Equal(before, states.GetVisible(source));
        Assert.Equal(0U, states.GetOwnerGuildId(source));
        Assert.False(captured!.TryEnter(out var lease));
        Assert.Null(lease);
    }

    [Fact]
    public void SobPrimaryObservationRefusesCaptureDuringAnAlreadyHeldTargetMutation()
    {
        var source = CreateEventSobNpc() with { Life = 100, MaxLife = 100 };
        var states = new SobNpcRuntimeStateStore();
        var gate = typeof(SobNpcRuntimeStateStore).GetField("_gate", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(states)!;
        lock (gate)
        {
            Assert.False(states.TryCaptureMagicPrimaryTarget(source, out var captured));
            Assert.Null(captured);
        }
        Assert.True(states.TryCaptureMagicPrimaryTarget(source, out _));
    }
}
