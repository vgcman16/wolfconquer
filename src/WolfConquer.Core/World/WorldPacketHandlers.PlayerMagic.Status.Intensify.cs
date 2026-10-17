using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    internal const string RuntimeLegacyInUseIntensifyAttribute = "InUseIntensify";
    internal const string RuntimeLegacyIntensifyStampAttribute = "IntensifyStamp";
    internal const string RuntimeLegacyIntensifyStampUtcAttribute = "IntensifyStampUtc";
    internal const string RuntimeLegacyIntensifyDamageAttribute = "IntensifyDamage";
    internal const string RuntimeLegacyFocusClientSpellIdAttribute = "FocusClientSpellId";
    internal const string RuntimeLegacyFocusClientSpellLevelAttribute = "FocusClientSpellLevel";
    internal const string RuntimeLegacyFocusClientSpellUseSpellSoulAttribute = "FocusClientSpellUseSpellSoul";
    internal const string RuntimeLegacyFocusedExpiresAtUtcAttribute = "LegacyFocusedExpiresAtUtc";

    private const ushort LegacyIntensifyAttachStatusSpellId = 9_000;
    private const ushort LegacySpiritFocusAttachStatusSpellId = 11_670;

    private static async ValueTask<bool> HandleLegacyPlayerIntensifyAttachStatusMagicAsync(
        WorldPacketHandlerContext context,
        MsgAttackQuery query,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        WorldSession session,
        CancellationToken cancellationToken)
    {
        if (spell.Sort != LegacyMagicSort.AttachStatus ||
            spell.Id is not (LegacyIntensifyAttachStatusSpellId or LegacySpiritFocusAttachStatusSpellId))
        {
            return false;
        }

        var resourceSession = await TryPrepareLegacyPlayerMagicCastAsync(
                context,
                session,
                spell,
                cancellationToken)
            .ConfigureAwait(false);
        if (resourceSession is null)
        {
            return true;
        }

        var now = DateTimeOffset.UtcNow;
        var casterUid = CheckedUInt32(resourceSession.Character.Uid, nameof(resourceSession.Character.Uid));
        var armedSession = ApplyLegacyIntensifyAttachStatusFocus(
            resourceSession,
            spell,
            playerSpell,
            now);
        context.Sessions?.Set(context.Connection, armedSession);
        var armedContext = context with { Session = armedSession };

        var interaction = new MsgAttackPacket(query with
        {
            Uid = casterUid,
            KilledMonster = spell.Id,
            SpellLevel = (ushort)Math.Min(playerSpell.Level, ushort.MaxValue),
            SpellId = 0,
        });
        await SendFrameToPlayerAndVisiblePeersAsync(
                armedContext,
                context.Connection,
                armedSession,
                interaction.ToFrameBytes(),
                cancellationToken)
            .ConfigureAwait(false);

        var progressSession = await TryApplyLegacyPlayerMagicSpellExperienceAsync(
                armedContext,
                armedSession,
                playerSpell,
                spell,
                spell.Duration,
                cancellationToken)
            .ConfigureAwait(false);

        var spellName = spell.Id == LegacyIntensifyAttachStatusSpellId ? "Intensify" : "SpiritFocus";
        await context.Log.WriteLineAsync(
                $"WorldServer handled legacy {spellName} AttachStatus magic " +
                $"{FormatSession(armedContext with { Session = progressSession })} spellId={spell.Id} " +
                $"spellLevel={playerSpell.Level} damagePercent={spell.Damage} duration={spell.Duration} " +
                "interactionSpellId=0 armedFocus=True source=AttachStatus.cs case 9000/11670.")
            .ConfigureAwait(false);
        return true;
    }

    private static WorldSession ApplyLegacyIntensifyAttachStatusFocus(
        WorldSession session,
        LegacyMagicRuntimeSpell spell,
        PlayerSpellSnapshotEntry playerSpell,
        DateTimeOffset nowUtc)
    {
        var updatedCharacter = WithCharacterAttribute(
            session.Character,
            RuntimeLegacyInUseIntensifyAttribute,
            "true");
        updatedCharacter = WithCharacterAttribute(
            updatedCharacter,
            RuntimeLegacyIntensifyStampAttribute,
            nowUtc.ToLocalTime().DateTime.ToString("O", CultureInfo.InvariantCulture));
        updatedCharacter = WithCharacterAttribute(
            updatedCharacter,
            RuntimeLegacyIntensifyStampUtcAttribute,
            nowUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        updatedCharacter = WithCharacterAttribute(updatedCharacter, RuntimeLegacyIntensifyDamageAttribute, spell.Damage);
        updatedCharacter = WithCharacterAttribute(updatedCharacter, RuntimeLegacyFocusClientSpellIdAttribute, spell.Id);
        updatedCharacter = WithCharacterAttribute(
            updatedCharacter,
            RuntimeLegacyFocusClientSpellLevelAttribute,
            playerSpell.Level);
        updatedCharacter = WithCharacterAttribute(
            updatedCharacter,
            RuntimeLegacyFocusClientSpellUseSpellSoulAttribute,
            playerSpell.UseSpellSoul);

        return session with { Character = updatedCharacter };
    }
}
