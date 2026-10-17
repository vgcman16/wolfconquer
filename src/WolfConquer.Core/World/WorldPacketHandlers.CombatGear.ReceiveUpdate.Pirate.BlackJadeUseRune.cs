using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const ushort LegacyCombatGearPirateFusingUseRuneSpellId = 21_590;
    private const ushort LegacyCombatGearPirateBarrierUseRuneSpellId = 21_730;
    private const ushort LegacyCombatGearPirateShellUseRuneSpellId = 21_740;
    private const ushort LegacyCombatGearPirateStormUseRuneSpellId = 21_750;
    private const ushort LegacyCombatGearPirateThrashUseRuneSpellId = 21_760;
    private const ushort LegacyCombatGearPirateThunderUseRuneSpellId = 21_770;
    private const ushort LegacyCombatGearPirateTorrentUseRuneSpellId = 21_780;
    private const ushort LegacyCombatGearPirateTideUseRuneSpellId = 21_790;
    private const ushort LegacyCombatGearPirateSplashUseRuneSpellId = 21_800;
    private const ushort LegacyCombatGearPirateSailingUseRuneSpellId = 21_810;
    private const ushort LegacyCombatGearPirateVastUseRuneSpellId = 21_820;
    private const int LegacyCombatGearPirateBlackJadeUseRuneStatusField = 16;

    private static async ValueTask<(WorldSession Session, bool Applied)>
        TrySendLegacyCombatGearPirateBlackJadeUseRuneAsync(
            WorldPacketHandlerContext context,
            WorldSession session,
            WorldDataCatalog worldData,
            uint defenderUid,
            ushort targetX,
            ushort targetY,
            CancellationToken cancellationToken)
    {
        var candidates = EnumerateLegacyCombatGearPirateBlackJadeUseRuneCandidates(session, worldData).ToList();
        if (candidates.Count == 0)
        {
            return (session, false);
        }

        for (var attempt = 0; attempt < candidates.Count; attempt++)
        {
            var spellId = candidates[ReadLegacyCombatGearAttackSpellIndex(session.Character, candidates.Count)];
            if (!IsLegacyCombatGearPirateBlackJadeUseRuneSpell(spellId))
            {
                continue;
            }

            if (!TryResolveLegacyPlayerMagicRuntimeSpell(session, worldData, spellId, out var playerSpell, out var spell))
            {
                await LogLegacyCombatGearPirateBlackJadeUseRuneGapAsync(
                        context,
                        spellId,
                        spellLevel: 0,
                        $"black-jade UseRune selected allowed spellId={spellId}, but no learned/catalog spell resolved.")
                    .ConfigureAwait(false);
                continue;
            }

            if (!CheckLegacyCombatGearPirateBlackJadeUseRuneRateRoll(session.Character, worldData, playerSpell, spell))
            {
                continue;
            }

            if (!TryReadLegacyMagicUInt32Field(
                    worldData,
                    spell.Id,
                    playerSpell.Level,
                    LegacyCombatGearPirateBlackJadeUseRuneStatusField,
                    out var statusFlag) ||
                statusFlag > int.MaxValue)
            {
                await LogLegacyCombatGearPirateBlackJadeUseRuneGapAsync(
                        context,
                        spell.Id,
                        playerSpell.Level,
                        $"black-jade UseRune selected spellId={spell.Id}, but no bounded status flag resolved.")
                    .ConfigureAwait(false);
                continue;
            }

            var updatedSession = await SendLegacyCombatGearPirateBlackJadeUseRuneStatusAsync(
                    context,
                    session,
                    playerSpell,
                    spell,
                    defenderUid,
                    targetX,
                    targetY,
                    checked((int)statusFlag),
                    cancellationToken)
                .ConfigureAwait(false);
            return (updatedSession, true);
        }

        return (session, false);
    }

    private static IEnumerable<ushort> EnumerateLegacyCombatGearPirateBlackJadeUseRuneCandidates(
        WorldSession session,
        WorldDataCatalog worldData)
    {
        if (session.Character.Class / 1000 != 7)
        {
            yield break;
        }

        var combatGear = session.CombatGear ?? CombatGearSnapshot.Empty;
        foreach (var item in combatGear.Items)
        {
            if (!IsLegacyCombatGearForClass(session.Character.Class, item.ItemID))
            {
                continue;
            }

            foreach (var blackJadeId in item.BlackJades)
            {
                if (blackJadeId == 0 ||
                    blackJadeId > int.MaxValue ||
                    !worldData.DaoqiDictTypesById.TryGetValue((int)blackJadeId, out var blackJade))
                {
                    continue;
                }

                if (TryReadLegacyCombatGearBlackJadeCandidateSpellId(blackJade, 3, out var firstSpellId))
                {
                    yield return firstSpellId;
                }

                if (TryReadLegacyCombatGearBlackJadeCandidateSpellId(blackJade, 5, out var secondSpellId))
                {
                    yield return secondSpellId;
                }
            }
        }
    }

    private static bool TryReadLegacyCombatGearBlackJadeCandidateSpellId(
        DaoqiDictType blackJade,
        int attrValueFieldIndex,
        out ushort spellId)
    {
        spellId = 0;
        if (!TryReadDaoqiField(blackJade, attrValueFieldIndex, out var attrValue) ||
            attrValue <= 0)
        {
            return false;
        }

        var candidate = attrValue / 1000;
        if (candidate <= 0 ||
            candidate > ushort.MaxValue)
        {
            return false;
        }

        spellId = checked((ushort)candidate);
        return true;
    }

    private static bool IsLegacyCombatGearPirateBlackJadeUseRuneSpell(ushort spellId)
    {
        return spellId is
            LegacyCombatGearPirateFusingUseRuneSpellId or
            LegacyCombatGearPirateBarrierUseRuneSpellId or
            LegacyCombatGearPirateShellUseRuneSpellId or
            LegacyCombatGearPirateStormUseRuneSpellId or
            LegacyCombatGearPirateThrashUseRuneSpellId or
            LegacyCombatGearPirateThunderUseRuneSpellId or
            LegacyCombatGearPirateTorrentUseRuneSpellId or
            LegacyCombatGearPirateTideUseRuneSpellId or
            LegacyCombatGearPirateSplashUseRuneSpellId or
            LegacyCombatGearPirateSailingUseRuneSpellId or
            LegacyCombatGearPirateVastUseRuneSpellId;
    }

    private static bool CheckLegacyCombatGearPirateBlackJadeUseRuneRateRoll(
        CharacterSnapshot character,
        WorldDataCatalog worldData,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell)
    {
        var denominator = spell.Id == LegacyCombatGearPirateFusingUseRuneSpellId ? 10_000U : 100U;
        var rate = spell.Id == LegacyCombatGearPirateFusingUseRuneSpellId
            ? ReadLegacyMagicUInt32FieldOrDefault(worldData, spell.Id, playerSpell.Level, LegacyMagicDamageField)
            : ReadLegacyMagicUInt32FieldOrDefault(worldData, spell.Id, playerSpell.Level, LegacyMagicRateField);
        return CheckLegacyCombatGearPirateBlackJadeUseRuneRateRoll(character, spell.Id, rate, denominator);
    }

    private static bool CheckLegacyCombatGearPirateBlackJadeUseRuneRateRoll(
        CharacterSnapshot character,
        ushort spellId,
        uint rate,
        uint denominator)
    {
        if (denominator == 0)
        {
            return false;
        }

        if (rate >= denominator)
        {
            return true;
        }

        var roll = TryReadUInt32Attribute(
            character.Attributes,
            out var forcedRoll,
            RuntimeLegacyCombatGearAttackRateRollAttribute + "." +
                spellId.ToString(CultureInfo.InvariantCulture),
            RuntimeLegacyCombatGearAttackRateRollAttribute + spellId.ToString(CultureInfo.InvariantCulture),
            RuntimeLegacyCombatGearAttackRateRollAttribute)
            ? forcedRoll % denominator
            : checked((uint)Random.Shared.Next(0, checked((int)denominator)));
        return roll < rate;
    }

    private static async ValueTask<WorldSession> SendLegacyCombatGearPirateBlackJadeUseRuneStatusAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        PlayerSpellSnapshotEntry playerSpell,
        LegacyMagicRuntimeSpell spell,
        uint defenderUid,
        ushort targetX,
        ushort targetY,
        int statusFlag,
        CancellationToken cancellationToken)
    {
        await SendLegacyCombatGearPirateReceiveInteractionAsync(
                context,
                session,
                defenderUid,
                playerSpell,
                spell,
                targetX,
                targetY,
                cancellationToken)
            .ConfigureAwait(false);

        var togglesOff = spell.Duration == 0 &&
            statusFlag != 0 &&
            HasLegacyStatusFlag(session.Character, statusFlag);
        var isZeroDurationStatusToggle = spell.Duration == 0 && statusFlag != 0;
        var updatedCharacter = WithCharacterAttribute(
            togglesOff
                ? WithoutLegacyStatusFlag(session.Character, statusFlag)
                : WithLegacySourceAddSpellFlag(session.Character, statusFlag, removeOnDead: true),
            spell.Name,
            spell.Damage);
        var updatedSession = session with
        {
            Character = updatedCharacter,
        };
        context.Sessions?.Set(context.Connection, updatedSession);
        var updatedContext = context with { Session = updatedSession };

        await SendPlayerStatusFlagVectorToVisibleAsync(updatedContext, updatedSession, cancellationToken)
            .ConfigureAwait(false);
        if (!togglesOff)
        {
            await SendLegacyCombatGearArchiveSkillUpdateAsync(
                    updatedContext,
                    updatedSession,
                    statusFlag,
                    spell.Duration,
                    spell.Damage,
                    spellLevel: 0,
                    visible: true,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        await SendLegacyCombatGearPirateReceiveAnimationAsync(
                updatedContext,
                updatedSession,
                playerSpell,
                spell,
                CheckedUInt32(updatedSession.Character.Uid, nameof(updatedSession.Character.Uid)),
                originX: 0,
                originY: 0,
                targetHit: !isZeroDurationStatusToggle,
                cancellationToken)
            .ConfigureAwait(false);

        await SendLegacyCombatGearPirateEquipmentRefreshAsync(
                updatedContext,
                updatedSession,
                cancellationToken)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer applied legacy CombatGear Pirate black-jade UseRune " +
                $"{FormatSession(updatedContext)} spellId={spell.Id} statusFlag={statusFlag} " +
                $"gDamage={spell.Damage} toggledOff={togglesOff} equipmentRefreshPackets=1; " +
                "source=ReceiveAttack.Player/Npc.Execute -> client.CombatGear.Update(attacked) -> " +
                "MyCombatGear.Update Pirate black-jade random UseRune -> CombatGearAttack QueryEquipment.")
            .ConfigureAwait(false);
        return updatedSession;
    }

    private static async ValueTask LogLegacyCombatGearPirateBlackJadeUseRuneGapAsync(
        WorldPacketHandlerContext context,
        ushort spellId,
        ushort spellLevel,
        string reason)
    {
        await LogParityGapAsync(
                context,
                LegacyParityGapDiagnostics.Create(
                    LegacyParityGapCategory.Magic,
                    "combatgear-pirate-black-jade-random-userune-spell",
                    reason,
                    severity: LegacyParityGapSeverity.Info,
                    facts:
                    [
                        LegacyParityGapDiagnostics.Fact("opcode", WorldPacketOpcodes.MsgAttack),
                        LegacyParityGapDiagnostics.Fact("source", "MyCombatGear.Update Pirate black-jade UseRune"),
                        LegacyParityGapDiagnostics.Fact("spellId", spellId),
                        LegacyParityGapDiagnostics.Fact("spellLevel", spellLevel),
                    ],
                    dedupeFacts:
                    [
                        LegacyParityGapDiagnostics.Fact("source", "MyCombatGear.Update Pirate black-jade UseRune"),
                        LegacyParityGapDiagnostics.Fact("spellId", spellId),
                        LegacyParityGapDiagnostics.Fact("spellLevel", spellLevel),
                    ]))
            .ConfigureAwait(false);
    }
}
