using System.Buffers;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class OwnedReviverPreparedSpawnTests
{
    private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
    private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task RealBoundPreparationSerializesFutureActionAndRejectsChangedFutureAttributes(bool mutate) =>
        OwnedEquipmentBehaviorFixture.RunAsync(
            new(OwnedEquipmentBehaviorFixture.Character(minHitPoints: 1), PlayerItemSnapshot.Empty),
            OwnedEquipmentBehaviorFixture.Catalog(), f =>
            {
                var store = f.Store;
                var connection = f.Connection(OwnedEquipmentBehaviorFixture.Actor);
                var before = f.Current();
                var binding = before.NativeGameClient!.OrdinarySpellBinding!;
                var physicalAction = binding.Player.Action;
                var files = f.Data.Files();
                f.ActorFrames.Clear();
                var fields = new Dictionary<string, string>(before.Character.Attributes, StringComparer.OrdinalIgnoreCase)
                { ["Action"] = "65786", ["ReviverProofTest"] = "before" };
                var proposed = before with { Character = before.Character with { Attributes = fields } };
                var gate = TableValue(store, "_connectionGates", connection);
                var holder = TableValue(store, "_sessions", connection);
                lock (f.Handler.RequireActionLifecycleGate(store))
                lock (gate)
                using (EnterConnectionGate())
                using (var publication = Prepare(store, connection, holder, before, proposed))
                {
                    if (mutate) fields["ReviverProofTest"] = "after";
                    if (mutate)
                        Assert.IsType<InvalidOperationException>(Assert.Throws<TargetInvocationException>(
                            () => InvokeSpawn(publication, 5000)).InnerException);
                    else
                    {
                        var bytes = InvokeSpawn(publication, 5000);
                        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
                        Assert.True(LegacyPlayerSpawnPacket.TryParse(frame, out var packet));
                        Assert.Equal(250UL, packet.GetNumericField(31));
                    }
                    Assert.Same(before, f.Current());
                    Assert.Equal(physicalAction, binding.Player.Action);
                    Assert.True(store.IsBoundSpellOwnerCurrent(binding));
                    f.Data.AssertFiles(files); Assert.Empty(f.ActorFrames);
                }
                return Task.CompletedTask;
            });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task RealHolderSuccessorOrRetiredNativeBindingRejectsEarlierPreparedSpawnWithoutWritingOrSending(bool retire) =>
        OwnedEquipmentBehaviorFixture.RunAsync(
            new(OwnedEquipmentBehaviorFixture.Character(minHitPoints: 1), PlayerItemSnapshot.Empty),
            OwnedEquipmentBehaviorFixture.Catalog(), f =>
            {
                var store = f.Store;
                var connection = f.Connection(OwnedEquipmentBehaviorFixture.Actor);
                var before = f.Current();
                var binding = before.NativeGameClient!.OrdinarySpellBinding!;
                var fields = new Dictionary<string, string>(before.Character.Attributes, StringComparer.OrdinalIgnoreCase)
                { ["Action"] = "65786" };
                var proposed = before with { Character = before.Character with { Attributes = fields } };
                var gate = TableValue(store, "_connectionGates", connection);
                var holder = TableValue(store, "_sessions", connection);
                IDisposable publication;
                LegacyNativePlayerRuntime.OwnedActionPublication action;
                WorldSession final;
                lock (f.Handler.RequireActionLifecycleGate(store))
                lock (gate)
                using (EnterConnectionGate())
                {
                    publication = Prepare(store, connection, holder, before, proposed);
                    Assert.NotEmpty(InvokeSpawn(publication, 5000));
                    action = (LegacyNativePlayerRuntime.OwnedActionPublication)publication.GetType()
                        .GetField("action", PrivateInstance)!.GetValue(publication)!;
                    final = (WorldSession)publication.GetType().GetProperty("FinalSession", PrivateInstance)!.GetValue(publication)!;
                    // Release the genuine preparation's actor locks before a
                    // separate real Store writer may publish its successor.
                    publication.Dispose();
                }
                lock (f.Handler.RequireActionLifecycleGate(store))
                lock (gate)
                using (EnterConnectionGate())
                lock (binding.Client.SpellAdmissionSyncRoot)
                lock (binding.Player.SpellAdmissionSyncRoot)
                {
                    Assert.True(action.TryReadPreparedSpawnAction(binding, before, final, out var value));
                    Assert.Equal((ushort)250, value);
                    Assert.False(action.TryReadPreparedSpawnAction(binding, before with { }, final, out _));
                    Assert.False(action.TryReadPreparedSpawnAction(binding, before, final with { }, out _));
                }
                WorldSession? successor = null;
                if (retire)
                {
                    Assert.True(store.TryRemove(connection, out var removed));
                    Assert.Same(before, removed);
                }
                else
                    store.Set(connection, before with { Character = before.Character with
                    { Attributes = new Dictionary<string, string>(before.Character.Attributes, StringComparer.OrdinalIgnoreCase)
                        { ["ReviverSuccessorTest"] = "actual-holder" } } }, out successor);
                var files = f.Data.Files(); var physicalAction = binding.Player.Action;
                f.ActorFrames.Clear();
                lock (f.Handler.RequireActionLifecycleGate(store))
                lock (gate)
                using (EnterConnectionGate())
                lock (binding.Client.SpellAdmissionSyncRoot)
                lock (binding.Player.SpellAdmissionSyncRoot)
                {
                    Assert.False(action.TryReadPreparedSpawnAction(binding, before, final, out _));
                    Assert.IsType<InvalidOperationException>(Assert.Throws<TargetInvocationException>(
                        () => InvokeSpawn(publication, 5000)).InnerException);
                    if (retire) Assert.False(store.TryGet(connection, out _));
                    else Assert.Same(successor, f.Current());
                    Assert.Equal(physicalAction, binding.Player.Action);
                    Assert.Equal(!retire, store.IsBoundSpellOwnerCurrent(binding));
                    f.Data.AssertFiles(files); Assert.Empty(f.ActorFrames);
                }
                return Task.CompletedTask;
            });

    // Reflection selects the production Store constructor, real holder and
    // gate; it does not construct an action proof or grant a publication.
    private static IDisposable Prepare(WorldSessionStore store, object connection, object holder,
        WorldSession before, WorldSession proposed)
    {
        var type = typeof(WorldSessionStore).GetNestedType("PreparedSessionSpellPublication", BindingFlags.NonPublic)!;
        var ctor = type.GetConstructors(PrivateInstance).Single();
        var parameters = ctor.GetParameters();
        object?[] values = parameters.Select(p => p.HasDefaultValue ? p.DefaultValue : null).ToArray();
        values[0] = store; values[1] = connection; values[2] = holder; values[3] = before; values[4] = proposed;
        return (IDisposable)ctor.Invoke(values);
    }

    private static byte[] InvokeSpawn(IDisposable publication, uint maximum) =>
        (byte[])publication.GetType().GetMethod("PrepareReviverSpawn", PrivateInstance)!.Invoke(publication, [maximum])!;

    private static object TableValue(WorldSessionStore store, string name, object key)
    {
        var table = typeof(WorldSessionStore).GetField(name, PrivateInstance)!.GetValue(store)!;
        object?[] args = [key, null];
        Assert.True((bool)table.GetType().GetMethod("TryGetValue")!.Invoke(table, args)!);
        return Assert.IsAssignableFrom<object>(args[1]);
    }

    private static IDisposable EnterConnectionGate() => (IDisposable)typeof(WorldSessionStore)
        .GetMethod("EnterNativeConnectionGate", PrivateStatic)!.Invoke(null, null)!;
}
