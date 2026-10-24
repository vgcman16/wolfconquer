using System.Globalization;
using System.Net;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData(15_440, false)]
    [InlineData(15_440, true)]
    [InlineData(1_000, false)]
    [InlineData(1_000, true)]
    [InlineData(1_001, false)]
    [InlineData(1_001, true)]
    public async Task NativeMagicPreparationStaminaCallbackPreservesOriginalClientOrStopsBeforeReplacementCosts(
        ushort spellId, bool rebind)
    {
        using var fixture = new NativeMagicPreparationOriginalClientFixture(spellId, "stamina",
            rebind ? "rebind" : "copy");
        await fixture.CastAsync();
        fixture.AssertCallback();
        if (rebind) fixture.AssertReplacementUntouched();
        else fixture.AssertOriginalContinued(deadAtCallback: false);
    }

    [Theory]
    [InlineData(15_440)]
    [InlineData(1_000)]
    [InlineData(1_001)]
    public async Task NativeMagicPreparationManaCallbackRebindStopsBeforeReplacementCooldownAndDispatch(
        ushort spellId)
    {
        using var fixture = new NativeMagicPreparationOriginalClientFixture(spellId, "mana", "rebind");
        await fixture.CastAsync();
        fixture.AssertCallback();
        fixture.AssertReplacementUntouched();
    }

    [Theory]
    [InlineData(15_440)]
    [InlineData(1_000)]
    [InlineData(1_001)]
    public async Task NativeMagicPreparationSameClientDeadStaminaCallbackStillFinishesEnteredPreparation(
        ushort spellId)
    {
        using var fixture = new NativeMagicPreparationOriginalClientFixture(spellId, "stamina", "dead");
        await fixture.CastAsync();
        fixture.AssertCallback();
        fixture.AssertOriginalContinued(deadAtCallback: true);
    }

    [Fact]
    public async Task NativeRisePreparationInteractionCallbackRebindDoesNotResumeIntoReplacementFlagsOrQuery()
    {
        using var fixture = new NativeMagicPreparationOriginalClientFixture(15_440, "interaction", "rebind");
        await fixture.CastAsync();
        fixture.AssertCallback();
        fixture.AssertReplacementUntouched();
    }

    private sealed class NativeMagicPreparationOriginalClientFixture : IDisposable
    {
        private const ushort UnrelatedCooldown = 60_000;
        private static readonly DateTimeOffset SentinelDeadline =
            new(2030, 1, 2, 3, 4, 5, TimeSpan.Zero);
        private readonly ushort spellId;
        private readonly string boundary;
        private readonly string mutation;
        private readonly WorldSessionStore sessions = new();
        private readonly StringWriter log = new(CultureInfo.InvariantCulture);
        private readonly ConquerTcpConnection ownerConnection;
        private readonly ConquerTcpConnection peerConnection;
        private readonly WorldPacketHandlerContext context;
        private bool armed;
        private int callbacks;
        private int ownerFramesAfterCallback;
        private int peerFramesAfterCallback;
        private WorldSession? replacement;

        public NativeMagicPreparationOriginalClientFixture(ushort spellId, string boundary, string mutation)
        {
            this.spellId = spellId;
            this.boundary = boundary;
            this.mutation = mutation;
            var fields = Enumerable.Repeat("0", 64).ToArray();
            fields[0] = "111098"; fields[1] = "NativePreparationRawHelmet";
            fields[18] = "5000"; fields[19] = "500";
            var spell = CreateMagicRow(spellId, sort: spellId == 15_440 ? (byte)6 : (byte)1,
                useMana: 37, useStamina: 11, damage: spellId == 15_440 ? 27U : 99U,
                duration: spellId == 15_440 ? 60U : 0U, range: 18, coolDown: 500, coldTime: 4_000);
            var world = CreateMagicCatalogCore(CreateMonsterFamily(), [], [], [spell],
                items: [new ItemType(111_098, fields[1], fields)]);
            var items = PlayerItemSnapshot.Empty with { EquipmentItems =
                [new PlayerItemSnapshotItem { Uid = 99_371, ItemId = 111_098,
                    Position = 1, Durability = 100, MaximumDurability = 100 }] };
            // Declared legal raw profile/item inputs; no native authentication/acquisition claim.
            var owner = CreateSession(mapId: 1_015, classCode: 2_005, reborn: 2,
                strength: 0, spirit: 0, minHitPoints: 5_000, minMana: 500, stamina: 100,
                spellIds: [spellId], playerItems: items,
                attributes: new Dictionary<string, string> { ["PkMode"] = "0" }) with
                { MagicCooldowns = new Dictionary<ushort, DateTimeOffset> { [UnrelatedCooldown] = SentinelDeadline } };
            var peer = CreateSession(uid: TargetPlayerUid, name: "NativePreparationPeer",
                mapId: 1_015, x: 832, y: 584, classCode: 2_005, strength: 0, spirit: 0,
                minHitPoints: 5_000, minMana: 0, stamina: 100, spellIds: [],
                attributes: new Dictionary<string, string> { ["PkMode"] = "0" });
            ownerConnection = new ConquerTcpConnection(null, new IPEndPoint(IPAddress.Loopback, 9958),
                sendAsync: (memory, sendCancellationToken) =>
                {
                    var bytes = memory.ToArray();
                    OwnerFrames.Add(bytes);
                    if (!armed || !AtBoundary(bytes) || Interlocked.CompareExchange(ref callbacks, 1, 0) != 0)
                        return ValueTask.CompletedTask;
                    var current = Owner;
                    Assert.Equal(89UL, ulong.Parse(current.Character.Attributes["Stamina"], CultureInfo.InvariantCulture));
                    // Every resource-send callback observes the complete admitted
                    // resource/deadline image, even though wire order remains stamina then mana.
                    Assert.Equal(463L, current.Character.MinMana);
                    var enteredCooldowns = Assert.IsAssignableFrom<IReadOnlyDictionary<ushort, DateTimeOffset>>(current.MagicCooldowns);
                    Assert.Equal(context.TimeProvider!.GetUtcNow().AddMilliseconds(4_000), enteredCooldowns[spellId]);
                    Assert.Equal(SentinelDeadline, enteredCooldowns[UnrelatedCooldown]);
                    Assert.Equal(2, enteredCooldowns.Count);
                    if (boundary == "interaction")
                    {
                        Assert.False(HasLegacyStatusFlag(current.Character.Attributes, 227));
                        Assert.DoesNotContain(OwnerFrames, frame => LegacyPlayerStatusPacket.TryParse(ReadFrame(frame), out _));
                    }
                    var attributes = new Dictionary<string, string>(current.Character.Attributes)
                        { ["PreparationCallbackMarker"] = mutation };
                    if (mutation == "rebind")
                    {
                        attributes["Stamina"] = "77";
                        replacement = current with
                        {
                            Character = current.Character with { MinMana = 777, MinHitPoints = 4_321, Attributes = attributes },
                            MagicCooldowns = new Dictionary<ushort, DateTimeOffset> { [UnrelatedCooldown] = SentinelDeadline },
                        };
                        Assert.True(sessions.TryRemove(ownerConnection!, out var removed));
                        Assert.Same(current, removed);
                        sessions.Set(ownerConnection!, replacement); // Declared lifecycle sentinel input, same connection/UID; not native-auth proof.
                    }
                    else
                        sessions.Set(ownerConnection!, current with { Character = current.Character with
                            { Attributes = attributes, MinHitPoints = mutation == "dead" ? 0 : current.Character.MinHitPoints } });
                    ownerFramesAfterCallback = OwnerFrames.Count;
                    peerFramesAfterCallback = PeerFrames.Count;
                    return ValueTask.CompletedTask;
                });
            peerConnection = CreateSendingConnection(PeerFrames);
            sessions.Set(ownerConnection, owner);
            sessions.Set(peerConnection, peer);
            var clock = new ManualRuntimeTimeProvider(new DateTimeOffset(2026, 10, 17, 12, 0, 0, TimeSpan.Zero));
            context = new WorldPacketHandlerContext(ownerConnection, log, owner, WorldData: world,
                Sessions: sessions, TimeProvider: clock, ProtocolProfile: ConquerProtocolProfile.VisibleDx8D13);
            Assert.Equal("37", world.MagicByTypeId[spellId][0].Fields[9]);
            Assert.Equal("11", world.MagicByTypeId[spellId][0].Fields[29]);
            Assert.Equal("4000", world.MagicByTypeId[spellId][0].Fields[47]);
            Assert.Empty(owner.PlayerItems.RuneItems);
        }

        private List<byte[]> OwnerFrames { get; } = [];
        private List<byte[]> PeerFrames { get; } = [];
        private WorldSession Owner => Assert.IsType<WorldSession>(
            sessions.TryGet(ownerConnection, out var current) ? current : null);
        private WorldSession Peer => Assert.IsType<WorldSession>(
            sessions.TryGet(peerConnection, out var current) ? current : null);

        public async Task CastAsync()
        {
            // Earn cached MA1 by real public query BEFORE arming a resource callback.
            await PacketHandlers.HandleAsync(context with { Session = Owner },
                ReadFrame(new MsgActionPacket { ObjId = PlayerUid, Type = (MsgActionType)251 }.ToFrameBytes()));
            var statusBytes = Assert.Single(OwnerFrames,
                bytes => LegacyPlayerStatusPacket.TryParse(ReadFrame(bytes), out _));
            Assert.True(LegacyPlayerStatusPacket.TryParse(ReadFrame(statusBytes), out var status));
            Assert.Equal(PlayerUid, status.Uid);
            Assert.Equal(1U, status.MagicAttack);
            Assert.Equal(5_000U, status.MaxHP);
            Assert.Equal(500U, status.MaxMana);
            Assert.Equal(5_000L, Owner.Character.MinHitPoints);
            Assert.Equal(500L, Owner.Character.MinMana);
            OwnerFrames.Clear(); PeerFrames.Clear(); // Only completed, positively proved raw-input query setup.
            armed = true;
            await PacketHandlers.HandleAsync(context with { Session = Owner },
                ReadFrame(new MsgAttackPacket(new MsgAttackQuery(Uid: PlayerUid,
                    OpponentUid: spellId == 15_440 ? PlayerUid : TargetPlayerUid,
                    X: 832, Y: 584, AttackType: MsgAttackType.Magic, Damage: 0, KilledMonster: 0,
                    ResponseDamage: 0, Effect: MsgAttackEffect.None, Unknown: 0, SpellId: spellId)).ToFrameBytes()));
            armed = false;
        }

        private bool AtBoundary(byte[] bytes) => boundary == "interaction"
            ? MsgAttackPacket.TryParse(ReadFrame(bytes), out var interaction) &&
                interaction.Query.Uid == PlayerUid && interaction.Query.OpponentUid == PlayerUid &&
                interaction.Query.AttackType == MsgAttackType.Magic && interaction.Query.SpellId == spellId
            : LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var update) && update.Uid == PlayerUid &&
                update.Updates.Any(entry => entry.Type == (boundary == "stamina"
                    ? LegacyMsgUpdateDataType.Stamina : LegacyMsgUpdateDataType.Mana));

        public void AssertCallback() => Assert.Equal(1, Volatile.Read(ref callbacks));

        public void AssertReplacementUntouched()
        {
            Assert.NotNull(replacement);
            Assert.Same(replacement, Owner);
            Assert.Equal(PlayerUid, Owner.Character.Uid);
            Assert.Equal(777L, Owner.Character.MinMana);
            Assert.Equal(4_321L, Owner.Character.MinHitPoints);
            Assert.Equal("77", Owner.Character.Attributes["Stamina"]);
            Assert.Equal("rebind", Owner.Character.Attributes["PreparationCallbackMarker"]);
            var cooldowns = Assert.IsAssignableFrom<IReadOnlyDictionary<ushort, DateTimeOffset>>(Owner.MagicCooldowns);
            Assert.Equal(SentinelDeadline, cooldowns[UnrelatedCooldown]);
            Assert.False(cooldowns.ContainsKey(spellId));
            Assert.Single(cooldowns);
            Assert.Equal(5_000L, Peer.Character.MinHitPoints);
            Assert.Equal(ownerFramesAfterCallback, OwnerFrames.Count);
            Assert.Equal(peerFramesAfterCallback, PeerFrames.Count);
            Assert.False(HasLegacyStatusFlag(Owner.Character.Attributes, 227));
            Assert.DoesNotContain(ParseSpellAnimations(OwnerFrames), packet => packet.SpellId == spellId);
            Assert.DoesNotContain(OwnerFrames, bytes => LegacyMagicColdTimePacket.TryParse(ReadFrame(bytes), out _));
        }

        public void AssertOriginalContinued(bool deadAtCallback)
        {
            Assert.Equal(deadAtCallback ? "dead" : "copy", Owner.Character.Attributes["PreparationCallbackMarker"]);
            Assert.Equal("89", Owner.Character.Attributes["Stamina"]);
            Assert.Equal(463L, Owner.Character.MinMana);
            var cooldowns = Assert.IsAssignableFrom<IReadOnlyDictionary<ushort, DateTimeOffset>>(Owner.MagicCooldowns);
            Assert.Equal(SentinelDeadline, cooldowns[UnrelatedCooldown]);
            Assert.True(cooldowns.ContainsKey(spellId));
            Assert.Equal(2, cooldowns.Count);
            var animation = Assert.Single(ParseSpellAnimations(OwnerFrames), packet => packet.SpellId == spellId);
            Assert.Equal(PlayerUid, animation.AttackerUid);
            if (spellId == 15_440)
            {
                Assert.True(HasLegacyStatusFlag(Owner.Character.Attributes, 227));
                Assert.Equal(deadAtCallback ? 50_000L : 55_000L, Owner.Character.MinHitPoints);
                Assert.DoesNotContain(OwnerFrames, bytes => LegacyMagicColdTimePacket.TryParse(ReadFrame(bytes), out _));
            }
            else
            {
                var target = Assert.Single(animation.Targets);
                Assert.Equal(TargetPlayerUid, target.Uid);
                Assert.Equal(50U, target.Damage); // QueriedMA1 + raw99, then native caller /2.
                Assert.True(target.Hit);
                Assert.Equal(MsgAttackEffect.None, target.Effect);
                Assert.Equal(4_950L, Peer.Character.MinHitPoints);
                Assert.Equal(deadAtCallback ? 0L : 5_000L, Owner.Character.MinHitPoints);
                Assert.DoesNotContain(OwnerFrames, bytes => LegacyMagicColdTimePacket.TryParse(ReadFrame(bytes), out _));
            }
        }

        public void Dispose()
        {
            GC.KeepAlive(ownerConnection); GC.KeepAlive(peerConnection);
            log.Dispose(); // No listeners, global loop, account/save files or external processes.
        }
    }
}
