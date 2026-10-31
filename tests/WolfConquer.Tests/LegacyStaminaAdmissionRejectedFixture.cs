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
    private static Task RunRejectedStaminaAdmission(string raw, string? blessing, bool first, long map, bool root, byte cost = 10) =>
        RunRejectedStaminaInputs(StaminaAdmissionInput(PlayerUid, raw, blessing, first, map),
            StaminaAdmissionInput(TargetPlayerUid, raw, blessing, first, map), StaminaAdmissionWorld(cost), raw, first,
            root ? (ushort)1000 : (ushort)1045);

    private static async Task RunRejectedStaminaInputs(OwnedEquipmentBehaviorInput actor, OwnedEquipmentBehaviorInput peer,
        WorldDataCatalog world, string raw, bool? first, ushort spell, bool allowAnimation = false)
    {
        var f = new RejectedSavedStaminaFixture(); var errors = new List<Exception>();
        try
        {
            await f.InitializeAsync(actor, peer, world, raw, first);
            f.AssertRefused();
            if (allowAnimation)
            {
                await f.SendAsync(PlayerUid, new MsgActionPacket { ObjId = PlayerUid, Type = MsgActionType.AllowAnimation }.ToFrameBytes());
                f.AssertRefused(); // Cannot earn the manual equipment prefix without admission.
            }
            await f.SendAsync(PlayerUid, new MsgAttackPacket(new MsgAttackQuery(PlayerUid, TargetPlayerUid, 832, 584,
                MsgAttackType.Magic, 0, 0, 0, MsgAttackEffect.None, 0, SpellId: spell)).ToFrameBytes());
            f.AssertRefused();
        }
        catch (Exception error) { errors.Add(error); }
        try { await f.CloseAsync(); } catch (Exception error) { errors.Add(error); }
        try { f.DisposeMetadata(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count == 0) try { f.Data.Dispose(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException(errors);
    }

    private sealed class RejectedSavedStaminaFixture
    {
        internal readonly LegacyOwnedAdmissionRuntimeFixture Data = new(bootstrap: false);
        private readonly Dictionary<uint, ConquerTcpConnection> connections = [];
        private readonly Dictionary<uint, List<byte[]>> sent = [];
        private readonly Dictionary<uint, CancellationTokenSource> lifetimes = [];
        private readonly StringWriter log = new();
        private NativeLoginBeastMetadataScope? beasts;
        private LegacyOwnedAdmissionRuntime? runtime;
        private WorldServerFrameHandler handler = null!;
        private Dictionary<string, byte[]> baseline = [];
        private WorldSession? retainedPeer;
        private string raw = "";
        private long savedMana;
        private WorldSessionStore Store => Assert.IsType<WorldSessionStore>(typeof(WorldServerFrameHandler)
            .GetField("_sessions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(handler));

        internal async Task InitializeAsync(OwnedEquipmentBehaviorInput actor, OwnedEquipmentBehaviorInput peer,
            WorldDataCatalog world, string raw, bool? first)
        {
            this.raw = raw; savedMana = actor.Character.MinMana;
            Assert.Equal((long)PlayerUid, actor.Character.Uid); Assert.Equal((long)TargetPlayerUid, peer.Character.Uid);
            Save(actor); Save(peer);
            foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
            {
                if (first is not { } expected) break;
                string text = CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(CharacterPath(uid)), out _);
                int stamina = text.IndexOf("\nStamina=", StringComparison.Ordinal), witness = text.IndexOf("\nStaminaOrderWitness=", StringComparison.Ordinal);
                Assert.True(stamina >= 0 && witness >= 0); Assert.Equal(expected, stamina < witness);
            }
            beasts = new(1000); Data.Bootstrap();
            Assert.True(LegacyOwnedAdmissionRuntime.TryOpen(Data.Loader, Data.Options(), out runtime, out var reason), reason);
            Assert.NotNull(runtime);
            var clock = new ManualRuntimeTimeProvider(new DateTimeOffset(2026, 10, 29, 12, 0, 0, TimeSpan.Zero));
            handler = new(Data.Loader, null, world, new LegacyRuntimeUpdateLoopOptions(false, TimeSpan.FromSeconds(1), clock),
                log, protocolProfile: ConquerProtocolProfile.VisibleDx8D13, ownedAdmission: runtime);
            NewConnection(PlayerUid); NewConnection(TargetPlayerUid);

            // The root-frame family retains its originally VALID peer input.
            // Admit that actual peer first and retain its exact owner; never
            // manufacture a second malformed row just to force an empty Store.
            var beforePeerLogin = Data.Files();
            await SendAsync(TargetPlayerUid, new LegacyMsgLoginGamePacket(TargetPlayerUid).ToFrameBytes());
            if (Store.TryGet(connections[TargetPlayerUid], out var admitted))
            {
                Assert.NotNull(admitted); retainedPeer = admitted;
                Assert.True(ushort.TryParse(peer.Character.Attributes["Stamina"], NumberStyles.Integer, CultureInfo.InvariantCulture, out _));
            }
            else
            {
                Assert.Equal(raw, peer.Character.Attributes["Stamina"]); AssertUnadmitted(TargetPlayerUid);
                Data.AssertFiles(beforePeerLogin); // A failed peer cannot dirty the later baseline.
            }
            sent[TargetPlayerUid].Clear(); baseline = Data.Files(); // BEFORE the malformed actor's actual login.
            await SendAsync(PlayerUid, new LegacyMsgLoginGamePacket(PlayerUid).ToFrameBytes());
        }

        private void Save(OwnedEquipmentBehaviorInput input)
        {
            uint uid = checked((uint)input.Character.Uid);
            var values = new Dictionary<string, string>(input.Character.Attributes, StringComparer.OrdinalIgnoreCase);
            foreach (var value in CharacterSnapshotScalarPersistence.BuildPersistedValues(input.Character)) values[value.Key] = value.Value;
            values["Name"] = input.Character.Name; values["SecurityPass"] = "0,0,0";
            File.WriteAllText(CharacterPath(uid), "[Character]\r\n" + string.Join("\r\n", values.Select(value => value.Key + "=" + value.Value)) + "\r\n", new UTF8Encoding(true));
            new PlayerItemSnapshotLoader(Data.Items).SaveByCharacterUid(uid, input.Items);
            new PlayerSpellSnapshotLoader(Data.Spells).SaveByCharacterUid(uid, input.Spells ?? PlayerSpellSnapshot.Empty);
            new PlayerProficiencySnapshotLoader(Data.Proficiencies).SaveByCharacterUid(uid, input.Proficiencies ?? PlayerProficiencySnapshot.Empty);
            new PlayerQuestSnapshotLoader(Data.Quests).SaveByCharacterUid(uid, input.Character.PlayerQuests);
            new CombatGearSnapshotLoader(Data.CombatGear).SaveByCharacterUid(uid, input.CombatGear ?? CombatGearSnapshot.Empty);
            new LegacyMailboxStore(Data.Root).Save(uid);
        }
        private string CharacterPath(uint uid) => Path.Combine(Data.Users, uid.ToString(CultureInfo.InvariantCulture) + ".ini");
        private void NewConnection(uint uid)
        {
            var output = new List<byte[]>(); sent.Add(uid, output);
            var lifetime = new CancellationTokenSource(); lifetimes.Add(uid, lifetime);
            connections.Add(uid, new(null, new IPEndPoint(IPAddress.Loopback, 9958), (memory, token) =>
            { token.ThrowIfCancellationRequested(); output.Add(memory.ToArray()); return ValueTask.CompletedTask; },
                _ => { lifetime.Cancel(); return ValueTask.CompletedTask; }, new LegacyNativeGameClientRuntime()));
        }
        internal async Task SendAsync(uint uid, byte[] bytes)
        {
            Assert.True(WolfConquer.Core.Packets.PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
            await handler.HandleFrameAsync(connections[uid], frame, lifetimes[uid].Token);
        }
        private void AssertUnadmitted(uint uid)
        {
            Assert.False(Store.TryGet(connections[uid], out _)); Assert.Empty(sent[uid]);
            var native = connections[uid].NativeGameClient!;
            Assert.Null(native.OrdinarySpellBinding); Assert.Null(native.MySpells); Assert.Null(native.MyProfs);
            Assert.Null(native.Inventory); Assert.Null(native.Equipment); Assert.Null(native.Rune);
            Assert.Equal(0U, native.Player!.UID); Assert.Equal(0, native.Player.HitPoints); Assert.Equal(0U, native.Player.Mana);
            Assert.Equal((ushort)0, native.Player.Stamina); Assert.Equal((ushort)0, native.Player.RandomSpell);
            Assert.Equal((ushort)0, native.OnSoulSpell); Assert.Empty(native.Player.BitVector.ArrayFlags); Assert.False(native.FullLoading);
        }
        internal void AssertRefused()
        {
            AssertUnadmitted(PlayerUid); Assert.Empty(sent[TargetPlayerUid]);
            if (retainedPeer is null) AssertUnadmitted(TargetPlayerUid);
            else
            {
                Assert.True(Store.TryGet(connections[TargetPlayerUid], out var current)); Assert.Same(retainedPeer, current);
                var binding = retainedPeer.NativeGameClient!.OrdinarySpellBinding!; Assert.True(Store.IsBoundSpellOwnerCurrent(binding));
                Assert.True(binding.Items.Image.IsCurrentPlain());
                Assert.Equal(retainedPeer.Character.MinHitPoints, (long)binding.Player.HitPoints);
                Assert.Equal(retainedPeer.Character.MinMana, (long)binding.Player.Mana);
                Assert.Equal(ushort.Parse(retainedPeer.Character.Attributes["Stamina"], CultureInfo.InvariantCulture), binding.Player.Stamina);
                Assert.Null(retainedPeer.MagicCooldowns); Assert.Equal((ushort)0, retainedPeer.LegacyRandomSpell);
                Assert.Equal((ushort)0, binding.Player.RandomSpell); Assert.Equal((ushort)0, retainedPeer.NativeGameClient.OnSoulSpell);
                var savedItems = PlayerItemSnapshotLoader.ToLegacyBytes(PlayerItemSnapshotLoader.Parse(
                    File.ReadAllBytes(Path.Combine(Data.Items, TargetPlayerUid + ".bin"))));
                Assert.Equal(savedItems, PlayerItemSnapshotLoader.ToLegacyBytes(retainedPeer.PlayerItems));
                Assert.Equal(savedItems, PlayerItemSnapshotLoader.ToLegacyBytes(binding.Items.Image.FinalSnapshot));
            }
            var saved = Assert.IsType<CharacterSnapshot>(Data.Loader.LoadByAccountId(PlayerUid));
            Assert.Equal(raw, saved.Attributes["Stamina"]); Assert.Equal(savedMana, saved.MinMana);
            Data.AssertFiles(baseline); Assert.False(runtime!.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(Data.Users));
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
            foreach (var lifetime in lifetimes.Values) lifetime.Dispose();
            if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
            if (errors.Count > 1) throw new AggregateException(errors);
        }
        internal void DisposeMetadata() { beasts?.Dispose(); beasts = null; log.Dispose(); }
    }
}
