using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private sealed record LegacyProcPreparation(WorldSession Candidate, byte[][] CostFrames);
    private readonly record struct LegacyCombatProcAdmission(WorldSession Owner, WorldSession? Peer, bool Admitted,
        LegacyPlayerMagicReceiveAbortReason AbortReason, WorldSessionStore.ProcReceipt? Receipt = null,
        LegacyNestedProcAuthorization? Authorization = null);
    private sealed class LegacyNestedProcAuthorization(WorldSession owner, WorldSession target)
    {
        internal CharacterSnapshot Owner { get; private set; } = owner.Character;
        internal CharacterSnapshot Target { get; } = target.Character;
        internal PlayerItemSnapshot Items { get; private set; } = owner.PlayerItems;
        internal PlayerSpellSnapshot Spells { get; private set; } = owner.PlayerSpells;
        internal bool AlternateEquipment { get; } = owner.AlternateEquipmentActive;
        internal WorldSessionStore.ProcReceipt? Receipt { get; set; }
        // The actual Store returns its prepared canonical record. These plain
        // stores do not allocate an authorization object after commitment.
        internal void BindCommitted(WorldSession published)
        { Owner = published.Character; Items = published.PlayerItems; Spells = published.PlayerSpells; }
    }

    // Pure preparation: no Store writer, packet send, resource setter or grant.
    private static bool TryPrepareLegacyCombatGearProc(WorldPacketHandlerContext context, WorldSession expected,
        LegacyMagicRuntimeSpell spell, bool sendMana, out LegacyProcPreparation plan)
    {
        plan = null!;
        try
        {
            if (expected.Character.MinHitPoints <= 0 || spell.ColdTime < 0) return false;
            var candidate = expected; var character = expected.Character; var frames = new List<byte[]>();
            var uid = CheckedUInt32(character.Uid, nameof(character.Uid));
            _ = CheckedUInt32(character.MinMana, nameof(character.MinMana));
            if (character.Attributes.TryGetValue("Stamina", out var rawStamina) &&
                !ulong.TryParse(rawStamina, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)) return false;
            if (character.MapId != 1039)
            {
                var staminaCost = (uint)spell.UseStamina + (HasLegacyStatusFlag(character, LegacyScurvyBombFlag) ? 6U : 0U);
                var stamina = ReadLegacyRuntimeStamina(character);
                var manaCost = spell.UseMana == 0 ? 0U : LegacyMagicResourceCostCalculator.Calculate(spell,
                    expected.PlayerItems, context.WorldData, character.Attributes,
                    expected.AlternateEquipmentActive, character.Class).UseMana;
                if (stamina < staminaCost || character.MinMana < manaCost) return false;
                if (staminaCost > 0)
                {
                    var remaining = stamina - staminaCost;
                    character = WithCharacterAttribute(character, "Stamina", remaining);
                    frames.Add(LegacyMsgUpdatePacket.CreateLegacyRuntimeStaminaTickUpdate(uid, remaining).ToFrameBytes());
                }
                if (manaCost > 0)
                {
                    var remaining = CheckedUInt32(character.MinMana - manaCost, nameof(character.MinMana));
                    character = character with { MinMana = remaining };
                    if (sendMana) frames.Add(new LegacyMsgUpdatePacket(uid,
                        [new(LegacyMsgUpdateDataType.Mana, remaining)]).ToFrameBytes());
                }
            }
            if (LegacyMagicRuntime.UsesLegacyCooldown(spell) &&
                !HasLegacyStatusFlag(expected.Character, LegacyCannonBarrageFlag) &&
                !HasLegacyStatusFlag(expected.Character, LegacyBlackbeardsRageFlag))
            {
                var now = (context.TimeProvider ?? TimeProvider.System).GetUtcNow();
                if (expected.MagicCooldowns is { } previous && previous.TryGetValue(spell.Id, out var readyAt) && readyAt > now)
                    return false;
                var staged = expected.MagicCooldowns is null ? new Dictionary<ushort, DateTimeOffset>() :
                    new Dictionary<ushort, DateTimeOffset>(expected.MagicCooldowns);
                staged[spell.Id] = now.AddMilliseconds(spell.ColdTime);
                candidate = candidate with { MagicCooldowns = staged };
            }
            if (!ReferenceEquals(character, expected.Character)) candidate = candidate with { Character = character };
            plan = new(candidate, frames.ToArray()); return true;
        }
        catch (Exception error) when (error is ArgumentException or FormatException or OverflowException or InvalidDataException)
        { return false; }
    }

    private static bool TryCaptureLegacyProcParticipant(WorldPacketHandlerContext context, WorldSession expected,
        out WorldSessionStore.ProcParticipant participant)
    {
        participant = default;
        if (context.Sessions is not { } world ||
            !LegacyNativeClientPublication.TryCaptureIdentity(world, context.Connection, expected.Character.Uid, out var identity) ||
            !LegacyNativeClientPublication.TryCurrent(world, context.Connection, expected.Character.Uid, identity, out var current) ||
            !ReferenceEquals(current, expected)) return false;
        participant = new(context.Connection, expected, identity); return true;
    }

    private static bool HasOwnedLegacyProcAttachment(WorldSession session, ConquerTcpConnection connection) =>
        session.NativeGameClient?.OrdinarySpellBinding is not null || session.NativePlayer?.HasOwnedVitalsAttachment == true ||
        connection.NativeGameClient?.OrdinarySpellBinding is not null || connection.NativeGameClient?.Player?.HasOwnedVitalsAttachment == true;
}
