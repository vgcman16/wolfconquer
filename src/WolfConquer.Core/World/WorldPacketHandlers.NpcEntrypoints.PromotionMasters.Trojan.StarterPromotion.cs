using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const string LegacyPromotionTrojanStarterSourceRange = "NpcHandler.cs:48633-48671";
    private const byte LegacyPromotionTrojanStarterPromptOption = 1;
    private const byte LegacyPromotionTrojanStarterAcceptOption = 2;
    private const long LegacyPromotionTrojanStarterLevelGate = 15;
    private const long LegacyPromotionTrojanInternClass = 1_000;
    private const ushort LegacyPromotionTrojanFastBladeSpellId = 1_045;
    private const ushort LegacyPromotionTrojanScentSwordSpellId = 1_046;

    private static async ValueTask<bool> TryHandleLegacyTrojanStarterPromotionAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (optionId == LegacyPromotionTrojanStarterPromptOption)
        {
            if (IsLegacyTrojanStarterPromotionEligible(session))
            {
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacyPromotionTrojanSource,
                        ["The Trojans are all-out offensive melee combat fighters. Do you want to be a qualified Trojan, now?."],
                        [new LegacyNpcDialogOption("Yeah,~please!", LegacyPromotionTrojanStarterAcceptOption)],
                        LegacyPromotionTrojanAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                await context.Log.WriteLineAsync(
                        "WorldServer handled LegacyNpc PromotionTrojan starter prompt " +
                        $"{FormatSession(context)} source={LegacyPromotionTrojanSource} " +
                        $"sourceRange={LegacyPromotionTrojanStarterSourceRange} option={optionId}.")
                    .ConfigureAwait(false);
            }
            else
            {
                await SendLegacyNpcDialogAsync(
                        context,
                        LegacyPromotionTrojanSource,
                        ["Only the powerful deserve to be trained. You are still a mere fledgeling."],
                        [new LegacyNpcDialogOption("Oh,~I~see..", LegacyNpcClosedOption)],
                        LegacyPromotionTrojanAvatarId,
                        cancellationToken)
                    .ConfigureAwait(false);
                await context.Log.WriteLineAsync(
                        "WorldServer skipped LegacyNpc PromotionTrojan starter prompt " +
                        $"{FormatSession(context)} source={LegacyPromotionTrojanSource} " +
                        $"sourceRange={LegacyPromotionTrojanStarterSourceRange} option={optionId} " +
                        $"level={session.Character.Level} class={session.Character.Class}; source promotion gate rejected.")
                    .ConfigureAwait(false);
            }

            return true;
        }

        if (optionId != LegacyPromotionTrojanStarterAcceptOption)
        {
            return false;
        }

        if (!IsLegacyTrojanStarterPromotionEligible(session))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped LegacyNpc PromotionTrojan starter promotion " +
                    $"{FormatSession(context)} source={LegacyPromotionTrojanSource} " +
                    $"sourceRange={LegacyPromotionTrojanStarterSourceRange} option={optionId} " +
                    $"level={session.Character.Level} class={session.Character.Class}; source promotion gate rejected.")
                .ConfigureAwait(false);
            return true;
        }

        var learnedSpells = new List<PlayerSpellSnapshotEntry>(capacity: 2);
        var updatedSpells = session.PlayerSpells;
        if (!updatedSpells.ContainsSpell(LegacyPromotionTrojanFastBladeSpellId))
        {
            var learnedFastBlade = CreateLegacyPromotionSpellEntry(LegacyPromotionTrojanFastBladeSpellId);
            learnedSpells.Add(learnedFastBlade);
            updatedSpells = LegacySkillBookUse.AddOrUpdateSpell(
                updatedSpells,
                learnedFastBlade);
        }

        if (!updatedSpells.ContainsSpell(LegacyPromotionTrojanScentSwordSpellId))
        {
            var learnedScentSword = CreateLegacyPromotionSpellEntry(LegacyPromotionTrojanScentSwordSpellId);
            learnedSpells.Add(learnedScentSword);
            updatedSpells = LegacySkillBookUse.AddOrUpdateSpell(
                updatedSpells,
                learnedScentSword);
        }

        var updatedSession = await ApplyLegacyPromotionSpellsAsync(
                context,
                session,
                updatedSpells,
                optionId,
                LegacyPromotionTrojanStarterSourceRange,
                $"{LegacyPromotionTrojanFastBladeSpellId},{LegacyPromotionTrojanScentSwordSpellId}")
            .ConfigureAwait(false);
        foreach (var learnedSpell in learnedSpells)
        {
            await context.Connection.SendAsync(
                    LegacySpellPacket.FromSnapshot(learnedSpell).ToFrameBytes(),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var updatedCharacter = updatedSession.Character with
        {
            Class = checked(updatedSession.Character.Class + 1),
        };
        updatedSession = await ApplyLegacyPromotionCharacterAsync(
                context,
                updatedSession,
                updatedCharacter,
                optionId,
                LegacyPromotionTrojanStarterSourceRange)
            .ConfigureAwait(false);

        await context.Log.WriteLineAsync(
                "WorldServer handled LegacyNpc PromotionTrojan starter promotion " +
                $"{FormatSession(context with { Session = updatedSession })} source={LegacyPromotionTrojanSource} " +
                $"sourceRange={LegacyPromotionTrojanStarterSourceRange} option={optionId} " +
                $"spellIds={LegacyPromotionTrojanFastBladeSpellId},{LegacyPromotionTrojanScentSwordSpellId}.")
            .ConfigureAwait(false);
        return true;
    }

    private static bool IsLegacyTrojanStarterPromotionEligible(WorldSession session)
    {
        return session.Character.Level >= LegacyPromotionTrojanStarterLevelGate &&
            session.Character.Class == LegacyPromotionTrojanInternClass;
    }
}
