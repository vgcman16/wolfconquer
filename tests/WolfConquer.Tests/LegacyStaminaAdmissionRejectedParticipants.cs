using System.Globalization;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    // Saved fixtures only. Actual admission decides whether a native owner
    // exists; this helper never publishes an invalid Character into the Store.
    internal static async Task RunRejectedStaminaParticipants(OwnedEquipmentBehaviorInput actor,
        OwnedEquipmentBehaviorInput peer, WorldDataCatalog world, ushort spell, bool allowAnimation = false,
        bool actorAttemptsUnavailablePeer = false)
    {
        var f = new RejectedSavedStaminaFixture(); var errors = new List<Exception>();
        try
        {
            await f.InitializeParticipantsAsync(actor, peer, world);
            f.AssertParticipantsRefused();
            foreach (var input in new[] { actor, peer })
            {
                if (HasAdmissibleSavedStamina(input)) continue;
                uint uid = checked((uint)input.Character.Uid);
                uint other = uid == PlayerUid ? TargetPlayerUid : PlayerUid;
                if (allowAnimation)
                {
                    await f.SendAsync(uid, new MsgActionPacket { ObjId = uid, Type = MsgActionType.AllowAnimation }.ToFrameBytes());
                    f.AssertParticipantsRefused();
                }
                await f.SendAsync(uid, new MsgAttackPacket(new MsgAttackQuery(uid, other, 832, 584,
                    MsgAttackType.Magic, 0, 0, 0, MsgAttackEffect.None, 0, SpellId: spell)).ToFrameBytes());
                f.AssertParticipantsRefused();
            }
            if (actorAttemptsUnavailablePeer && HasAdmissibleSavedStamina(actor) && !HasAdmissibleSavedStamina(peer))
            {
                // Preserve the original receive direction as well: the real
                // admitted caster cannot spend against an unavailable peer.
                await f.SendAsync(PlayerUid, new MsgAttackPacket(new MsgAttackQuery(PlayerUid, TargetPlayerUid,
                    checked((ushort)peer.Character.X), checked((ushort)peer.Character.Y), MsgAttackType.Magic,
                    0, 0, 0, MsgAttackEffect.None, 0, SpellId: spell)).ToFrameBytes());
                f.AssertParticipantsRefused();
            }
        }
        catch (Exception error) { errors.Add(error); }
        try { await f.CloseAsync(); } catch (Exception error) { errors.Add(error); }
        try { f.DisposeMetadata(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count == 0) try { f.Data.Dispose(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException(errors);
    }

    private static bool HasAdmissibleSavedStamina(OwnedEquipmentBehaviorInput input)
    {
        var rows = input.Character.Attributes.Where(row =>
            string.Equals(row.Key, "Stamina", StringComparison.OrdinalIgnoreCase)).ToArray();
        return rows.Length == 0 || rows.Length == 1 && ushort.TryParse(rows[0].Value,
            NumberStyles.Integer, CultureInfo.InvariantCulture, out _);
    }

    private sealed partial class RejectedSavedStaminaFixture
    {
        private OwnedEquipmentBehaviorInput[] participantInputs = [];
        private readonly Dictionary<uint, WorldSession> retainedParticipants = [];

        internal async Task InitializeParticipantsAsync(OwnedEquipmentBehaviorInput actor,
            OwnedEquipmentBehaviorInput peer, WorldDataCatalog world)
        {
            Assert.Equal((long)PlayerUid, actor.Character.Uid);
            Assert.Equal((long)TargetPlayerUid, peer.Character.Uid);
            participantInputs = [actor, peer];
            Assert.Contains(participantInputs, input => !HasAdmissibleSavedStamina(input));
            Save(actor); Save(peer);
            beasts = new(1000); Data.Bootstrap();
            Assert.True(LegacyOwnedAdmissionRuntime.TryOpen(Data.Loader, Data.Options(), out runtime, out var reason), reason);
            Assert.NotNull(runtime);
            var clock = new ManualRuntimeTimeProvider(new DateTimeOffset(2026, 10, 29, 12, 0, 0, TimeSpan.Zero));
            handler = new(Data.Loader, null, world, new LegacyRuntimeUpdateLoopOptions(false, TimeSpan.FromSeconds(1), clock),
                log, protocolProfile: ConquerProtocolProfile.VisibleDx8D13, ownedAdmission: runtime);
            NewConnection(PlayerUid); NewConnection(TargetPlayerUid);

            // Valid counterparts enter through the genuine Frame first. Their
            // legitimate initial-query files are part of the later baseline.
            foreach (var input in participantInputs.Where(HasAdmissibleSavedStamina))
            {
                uint uid = checked((uint)input.Character.Uid);
                await SendAsync(uid, new LegacyMsgLoginGamePacket(uid).ToFrameBytes());
                Assert.True(Store.TryGet(connections[uid], out var current));
                retainedParticipants.Add(uid, Assert.IsType<WorldSession>(current));
            }
            foreach (var frames in sent.Values) frames.Clear();
            baseline = Data.Files();
            foreach (var input in participantInputs.Where(input => !HasAdmissibleSavedStamina(input)))
            {
                uint uid = checked((uint)input.Character.Uid);
                await SendAsync(uid, new LegacyMsgLoginGamePacket(uid).ToFrameBytes());
                AssertParticipantsRefused();
            }
        }

        internal void AssertParticipantsRefused()
        {
            foreach (var input in participantInputs)
            {
                uint uid = checked((uint)input.Character.Uid);
                Assert.Empty(sent[uid]);
                if (!HasAdmissibleSavedStamina(input))
                {
                    AssertUnadmitted(uid);
                    var saved = Assert.IsType<CharacterSnapshot>(Data.Loader.LoadByAccountId(uid));
                    var raw = Assert.Single(input.Character.Attributes, row =>
                        string.Equals(row.Key, "Stamina", StringComparison.OrdinalIgnoreCase));
                    Assert.Equal(raw.Value, saved.Attributes[raw.Key]);
                    Assert.Equal(input.Character.MinMana, saved.MinMana);
                    continue;
                }
                Assert.True(Store.TryGet(connections[uid], out var current));
                Assert.Same(retainedParticipants[uid], current);
                var binding = Assert.IsType<LegacyOrdinarySpellBinding>(current!.NativeGameClient!.OrdinarySpellBinding);
                Assert.False(binding.IsRetired); Assert.True(Store.IsBoundSpellOwnerCurrent(binding));
                Assert.True(binding.Items.Image.IsCurrentPlain());
                Assert.Equal(uid, binding.Player.UID);
                Assert.Equal(current.Character.MinHitPoints, (long)binding.Player.HitPoints);
                Assert.Equal(current.Character.MinMana, (long)binding.Player.Mana);
                Assert.Equal(ushort.Parse(current.Character.Attributes["Stamina"], CultureInfo.InvariantCulture), binding.Player.Stamina);
                Assert.Null(current.MagicCooldowns); Assert.Equal((ushort)0, binding.Player.RandomSpell);
                Assert.Equal((ushort)0, current.NativeGameClient.OnSoulSpell);
                Assert.Equal(PlayerItemSnapshotLoader.ToLegacyBytes(current.PlayerItems),
                    PlayerItemSnapshotLoader.ToLegacyBytes(binding.Items.Image.FinalSnapshot));
            }
            Data.AssertFiles(baseline);
            Assert.False(runtime!.Recovery.IsBlocked); Assert.False(LegacyTradeJournal.HasPending(Data.Users));
        }
    }
}
