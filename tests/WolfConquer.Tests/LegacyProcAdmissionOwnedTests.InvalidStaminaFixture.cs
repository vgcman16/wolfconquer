using System.Buffers;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private static async Task RunRejectedProcStaminaAdmission(ushort proc, string raw, Func<RejectedProcStaminaFixture, Task> body)
    {
        var f = new RejectedProcStaminaFixture(); var errors = new List<Exception>();
        try { await f.InitializeAsync(proc, raw); await body(f); } catch (Exception error) { errors.Add(error); }
        try { await f.CloseAsync(); } catch (Exception error) { errors.Add(error); }
        try { f.DisposeMetadata(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count == 0) try { f.Data.Dispose(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException(errors);
    }

    private sealed class RejectedProcStaminaFixture
    {
        internal readonly LegacyOwnedAdmissionRuntimeFixture Data = new(bootstrap: false);
        private readonly Dictionary<uint, ConquerTcpConnection> connections = [];
        private readonly Dictionary<uint, List<byte[]>> sent = [];
        private readonly List<CancellationTokenSource> lifetimes = [];
        private NativeLoginBeastMetadataScope? beasts;
        private readonly StringWriter log = new();
        private LegacyOwnedAdmissionRuntime? runtime;
        private WorldServerFrameHandler handler = null!;
        internal Dictionary<string, byte[]> BeforeLogin = [];
        private WorldSessionStore Store => Assert.IsType<WorldSessionStore>(typeof(WorldServerFrameHandler)
            .GetField("_sessions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(handler));

        internal async Task InitializeAsync(ushort proc, string raw)
        {
            foreach (uint uid in new[] { PlayerUid, TargetPlayerUid }) SaveInput(uid, proc, raw);
            var row = CreateMagicRow(proc, sort: proc == 20220 ? (byte)105 : (byte)5,
                useMana: 50, useStamina: 10, damage: 100, range: 3, coldTime: 1000, coolDown: 0);
            var world = CreateMagicCatalogCore(CreateMonsterFamily(), [], [], new MagicType[] { row });
            var clock = new ManualRuntimeTimeProvider(new DateTimeOffset(2026, 10, 29, 12, 0, 0, TimeSpan.Zero));
            beasts = new(1000); Data.Bootstrap();
            Assert.True(LegacyOwnedAdmissionRuntime.TryOpen(Data.Loader, Data.Options(), out runtime, out var reason), reason);
            Assert.NotNull(runtime); BeforeLogin = Data.Files();
            handler = new(Data.Loader, null, world, new LegacyRuntimeUpdateLoopOptions(false, TimeSpan.FromSeconds(1), clock),
                log, protocolProfile: ConquerProtocolProfile.VisibleDx8D13, ownedAdmission: runtime);
            foreach (uint uid in new[] { PlayerUid, TargetPlayerUid })
            {
                var frames = new List<byte[]>(); sent.Add(uid, frames);
                var lifetime = new CancellationTokenSource(); lifetimes.Add(lifetime);
                var connection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958), (memory, token) =>
                { token.ThrowIfCancellationRequested(); frames.Add(memory.ToArray()); return ValueTask.CompletedTask; },
                    _ => { lifetime.Cancel(); return ValueTask.CompletedTask; }, new LegacyNativeGameClientRuntime());
                connections.Add(uid, connection);
                await SendAsync(uid, new LegacyMsgLoginGamePacket(uid).ToFrameBytes());
            }
        }
        private void SaveInput(uint uid, ushort proc, string raw)
        {
            // Same declared actor/target, proc costs, class and raw variants as
            // the old fixture. Invalid stamina exists BEFORE bootstrap/login.
            var source = CreateSession(uid: uid, classCode: 142, reborn: 2, mapId: 1000,
                x: uid == PlayerUid ? 830 : 831, y: 584, minHitPoints: 1000, minMana: 100, stamina: 100,
                spellIds: [proc], attributes: new Dictionary<string, string>
                { ["PkMode"] = "0", ["BattlePower"] = "120", ["LegacyCombatGearMagneticBattlePowerRoll"] = "0" });
            var values = new Dictionary<string, string>(source.Character.Attributes, StringComparer.OrdinalIgnoreCase);
            foreach (var value in CharacterSnapshotScalarPersistence.BuildPersistedValues(source.Character)) values[value.Key] = value.Value;
            values["Name"] = source.Character.Name; values["SecurityPass"] = "0,0,0"; values["Stamina"] = raw;
            File.WriteAllText(Path.Combine(Data.Users, uid.ToString(CultureInfo.InvariantCulture) + ".ini"),
                "[Character]\r\n" + string.Join("\r\n", values.Select(value => value.Key + "=" + value.Value)) + "\r\n", new UTF8Encoding(true));
            new PlayerItemSnapshotLoader(Data.Items).SaveByCharacterUid(uid, PlayerItemSnapshot.Empty);
            new PlayerSpellSnapshotLoader(Data.Spells).SaveByCharacterUid(uid, new PlayerSpellSnapshot(new[] { CreatePlayerSpell(proc) }));
            new PlayerProficiencySnapshotLoader(Data.Proficiencies).SaveByCharacterUid(uid, source.PlayerProficiencies);
            new PlayerQuestSnapshotLoader(Data.Quests).SaveByCharacterUid(uid, source.Character.PlayerQuests);
            new CombatGearSnapshotLoader(Data.CombatGear).SaveByCharacterUid(uid, source.CombatGear ?? CombatGearSnapshot.Empty);
            new LegacyMailboxStore(Data.Root).Save(uid);
        }
        private async Task SendAsync(uint uid, byte[] bytes)
        {
            Assert.True(WolfConquer.Core.Packets.PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
            await handler.HandleFrameAsync(connections[uid], frame);
        }
        internal Task AttemptProcFrameAsync(ushort proc)
        {
            uint owner = proc == 10950 ? PlayerUid : TargetPlayerUid, target = proc == 10950 ? TargetPlayerUid : PlayerUid;
            // Real wire request after failed admission; no fake WorldSession
            // is supplied to force an unreachable admitted proc continuation.
            return SendAsync(owner, new MsgAttackPacket(new MsgAttackQuery(Uid: owner, OpponentUid: target,
                X: 830, Y: 584, AttackType: MsgAttackType.Magic, Damage: 0, KilledMonster: 0,
                ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0, SpellId: proc)).ToFrameBytes());
        }
        internal void AssertNoAdmission()
        {
            Assert.Empty(Store.Snapshot());
            foreach (var pair in connections)
            {
                Assert.False(Store.TryGet(pair.Value, out _)); Assert.Empty(sent[pair.Key]);
                var native = pair.Value.NativeGameClient!; Assert.Null(native.OrdinarySpellBinding);
                Assert.Null(native.MySpells); Assert.Null(native.MyProfs); Assert.Null(native.Inventory); Assert.Null(native.Equipment);
                Assert.Equal(0U, native.Player!.UID); Assert.Equal(0, native.Player.HitPoints);
                Assert.Equal(0U, native.Player.Mana); Assert.Equal((ushort)0, native.Player.Stamina);
                Assert.Empty(native.Player.BitVector.ArrayFlags); Assert.False(native.FullLoading);
            }
        }
        internal async Task CloseAsync()
        {
            var errors = new List<Exception>();
            foreach (var connection in connections.Values)
            {
                try { await connection.DisconnectAsync(); } catch (Exception error) { errors.Add(error); }
                try { await connection.CompleteTransportCloseAsync(); } catch (Exception error) { errors.Add(error); }
            }
            if (runtime is not null) try { await runtime.DisposeAsync(); } catch (Exception error) { errors.Add(error); }
            foreach (var lifetime in lifetimes) lifetime.Dispose();
            if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
            if (errors.Count > 1) throw new AggregateException(errors);
        }
        internal void DisposeMetadata() { beasts?.Dispose(); beasts = null; log.Dispose(); }
    }
}
