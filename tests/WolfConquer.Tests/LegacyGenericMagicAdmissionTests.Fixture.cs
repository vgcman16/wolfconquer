using System.Globalization;
using System.Net;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicClassGatePreparationTests
{
    private sealed class GenericAdmissionFixture
    {
        public sealed record Image(WorldSession Owner, string Values, object? Identity);
        private readonly StringWriter log = new(CultureInfo.InvariantCulture);
        private readonly List<Task> calls = [];
        private readonly HashSet<Task> observed = [];
        private readonly WorldPacketHandlerContext context;

        public GenericAdmissionFixture(WorldSession input, WorldDataCatalog? catalog = null,
            bool registered = true, Action<GenericAdmissionFixture, byte[]>? onSend = null)
        {
            Original = input;
            Store = registered ? new WorldSessionStore() : null;
            Connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
                sendAsync: (memory, _) =>
                {
                    var bytes = memory.ToArray();
                    Frames.Add(bytes);
                    onSend?.Invoke(this, bytes); // Observation/fault/retirement only, never acceptance.
                    return ValueTask.CompletedTask;
                });
            Store?.Set(Connection, input);
            context = new WorldPacketHandlerContext(Connection, log, input,
                WorldData: catalog ?? CreateCatalog(), Sessions: Store,
                ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
        }

        public WorldSession Original { get; }
        public WorldSessionStore? Store { get; }
        public ConquerTcpConnection Connection { get; }
        public List<byte[]> Frames { get; } = [];
        public string Log => log.ToString();
        public WorldSession Current => Store is null ? Original :
            Assert.IsType<WorldSession>(Store.TryGet(Connection, out var current) ? current : null);

        public Image Capture()
        {
            object? identity = null;
            if (Store is { } world)
            {
                Assert.True(LegacyNativeClientPublication.TryCaptureIdentity(
                    world, Connection, Current.Character.Uid, out var captured));
                identity = captured;
            }
            return new(Current, JsonSerializer.Serialize(Current), identity);
        }

        public void AssertUnchanged(Image before)
        {
            Assert.Same(before.Owner, Current);
            Assert.Equal(before.Values, JsonSerializer.Serialize(Current));
            Assert.Same(before.Owner.Character.Attributes, Current.Character.Attributes);
            Assert.Same(before.Owner.PlayerItems, Current.PlayerItems);
            Assert.Same(before.Owner.PlayerSpells, Current.PlayerSpells);
            Assert.Same(before.Owner.MagicCooldowns, Current.MagicCooldowns);
            if (Store is { } world)
            {
                Assert.NotNull(before.Identity);
                Assert.True(LegacyNativeClientPublication.TryCurrent(world, Connection,
                    before.Owner.Character.Uid, before.Identity, out var current));
                Assert.Same(before.Owner, current);
            }
            Assert.Empty(Frames);
        }

        public async Task<WorldSession?> InvokeAsync(LegacyMagicRuntimeSpell spell, bool sixArguments,
            bool ignoreAttackStamp = false, bool supplyIdentity = false, bool recruit = false)
        {
            object? identity = supplyIdentity ? Capture().Identity : null;
            Type[] types = recruit
                ? [typeof(WorldPacketHandlerContext), typeof(WorldSession), typeof(LegacyMagicRuntimeSpell), typeof(CancellationToken)]
                : sixArguments
                ? [typeof(WorldPacketHandlerContext), typeof(WorldSession), typeof(LegacyMagicRuntimeSpell),
                    typeof(bool), typeof(CancellationToken), typeof(object)]
                : [typeof(WorldPacketHandlerContext), typeof(WorldSession), typeof(LegacyMagicRuntimeSpell),
                    typeof(CancellationToken), typeof(object)];
            var method = Assert.IsAssignableFrom<MethodInfo>(typeof(WorldPacketHandlers).GetMethod(
                recruit ? "TryPrepareLegacyRecruitHealingMagicCastAsync" : "TryPrepareLegacyPlayerMagicCastAsync",
                BindingFlags.Static | BindingFlags.NonPublic,
                binder: null, types: types, modifiers: null));
            object? invocation;
            try
            {
                invocation = method.Invoke(null, recruit
                    ? [context with { Session = Current }, Current, spell, CancellationToken.None]
                    : sixArguments
                    ? [context with { Session = Current }, Current, spell, ignoreAttackStamp, CancellationToken.None, identity]
                    : [context with { Session = Current }, Current, spell, CancellationToken.None, identity]);
            }
            catch (TargetInvocationException error) when (error.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(error.InnerException).Throw();
                throw;
            }
            var actual = Assert.IsType<ValueTask<WorldSession?>>(invocation).AsTask();
            calls.Add(actual);
            try { return await actual; }
            finally { observed.Add(actual); } // Direct await observes the actual terminal result, no timeout-as-drain.
        }

        private async Task CleanupAsync()
        {
            var errors = new List<Exception>();
            foreach (var actual in calls.Where(task => !observed.Contains(task)))
                try { await actual; } catch (Exception error) { errors.Add(error); }
            try { await Connection.CompleteTransportCloseAsync(); }
            catch (Exception error) { errors.Add(error); }
            try { log.Dispose(); } catch (Exception error) { errors.Add(error); }
            if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
            if (errors.Count != 0) throw new AggregateException("Generic admission fixture cleanup failed.", errors);
        }

        public static async Task RunAsync(GenericAdmissionFixture fixture, Func<GenericAdmissionFixture, Task> body)
        {
            Exception? primary = null;
            try { await body(fixture); } catch (Exception error) { primary = error; }
            try { await fixture.CleanupAsync(); }
            catch (Exception cleanup)
            {
                if (primary is not null)
                    throw new AggregateException("Generic admission assertion/operation and cleanup both failed.", primary, cleanup);
                throw;
            }
            if (primary is not null) ExceptionDispatchInfo.Capture(primary).Throw();
        }
    }

    private static WorldSession GenericAdmissionInput(long mana = 1_000, ushort stamina = 100) =>
        CreateSession(classCode: 1_005, minMana: mana, stamina: stamina, playerSpellIds: [1_045]);

    private static LegacyMagicRuntimeSpell GenericAdmissionSpell(ushort mana = 40, int cold = 60_000) =>
        CreateSpell(1_045, useMana: mana, useStamina: 10, coldTime: cold);

    private static void AssertGenericAdmissionWire(GenericAdmissionFixture fixture, LegacyMagicRuntimeSpell spell,
        ulong stamina = 90, ulong? mana = 960)
    {
        var uid = checked((uint)fixture.Original.Character.Uid);
        var expected = new List<byte[]>
            { LegacyMsgUpdatePacket.CreateLegacyRuntimeStaminaTickUpdate(uid, stamina).ToFrameBytes() };
        if (mana is { } value)
            expected.Add(new LegacyMsgUpdatePacket(uid,
                [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mana, value)]).ToFrameBytes());
        if (spell.ColdTime > 0) expected.Add(LegacyMagicColdTimePacket.FromStartedCooldown(spell).ToFrameBytes());
        Assert.Equal(expected.Count, fixture.Frames.Count);
        for (var index = 0; index < expected.Count; index++) Assert.Equal(expected[index], fixture.Frames[index]);
    }
}
