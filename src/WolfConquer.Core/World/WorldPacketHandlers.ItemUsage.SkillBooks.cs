using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private static async ValueTask HandleLegacySkillBookUseAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        PlayerItemSnapshotItem item,
        LegacySkillBookUseClassification skillBook,
        CancellationToken cancellationToken)
    {
        if (skillBook.Status == LegacySkillBookUseStatus.UnsupportedSkillBook)
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.UnsupportedItemUse(
                        item.ItemId,
                        action: (uint)MsgItemUsageAction.Equip,
                        actionName: MsgItemUsageAction.Equip.ToString(),
                        itemUid: item.Uid,
                        source: "MsgItemUsage.SkillBook",
                        reason: "legacy skill book item id is in the source book range but its spell mapping is not modeled"))
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "PARITY-GAP MsgItemUsage skill book unsupported " +
                    $"{FormatSession(context)} itemUid={item.Uid} itemId={item.ItemId}; " +
                    "source skill-book branch exists but local spell mapping is missing.")
                .ConfigureAwait(false);
            return;
        }

        if (skillBook.Status == LegacySkillBookUseStatus.MissingSpellMetadata ||
            skillBook.SpellEntry is not { } learnedSpell)
        {
            await LogParityGapAsync(
                    context,
                    LegacyParityGapDiagnostics.UnsupportedItemUse(
                        item.ItemId,
                        action: (uint)MsgItemUsageAction.Equip,
                        actionName: MsgItemUsageAction.Equip.ToString(),
                        itemUid: item.Uid,
                        source: "MsgItemUsage.SkillBook",
                        reason: "legacy skill book source mapping exists but magic metadata is not loaded"))
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "PARITY-GAP MsgItemUsage skill book missing magic metadata " +
                    $"{FormatSession(context)} itemUid={item.Uid} itemId={item.ItemId} spellId={skillBook.SpellId}.")
                .ConfigureAwait(false);
            return;
        }

        if (session.PlayerSpells.Spells.Any(spell => spell.Id == learnedSpell.Id))
        {
            await SendLegacySkillBookDialogAsync(context, "You already have this spell.", cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage skill book " +
                    $"{FormatSession(context)} itemUid={item.Uid} itemId={item.ItemId} spellId={learnedSpell.Id}; already learned.")
                .ConfigureAwait(false);
            return;
        }

        if (!LegacyMagicClassGate.CanUsePlayerSpell(
                session.Character,
                learnedSpell.Id,
                context.WorldData?.MagicTypeOperationsByReferencedMagicTypeId,
                out var classGate))
        {
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage skill book " +
                    $"{FormatSession(context)} itemUid={item.Uid} itemId={item.ItemId} spellId={learnedSpell.Id} " +
                    $"class={session.Character.Class} " +
                    $"classFamilies={LegacyMagicClassGate.FormatFamilies(classGate.CharacterFamilies)} " +
                    $"requiredClassFamilies={LegacyMagicClassGate.FormatFamilies(classGate.RequiredFamilies)}; " +
                    "source magictypeop profession gate rejected.")
                .ConfigureAwait(false);
            return;
        }

        if (!CanUseLegacySkillBookForRequirements(session, skillBook.ItemId, learnedSpell.Id, out var requirementRejection))
        {
            await SendLegacySkillBookDialogAsync(context, requirementRejection.DialogText, cancellationToken)
                .ConfigureAwait(false);
            await context.Log.WriteLineAsync(
                    "WorldServer skipped MsgItemUsage skill book " +
                    $"{FormatSession(context)} itemUid={item.Uid} itemId={item.ItemId} spellId={learnedSpell.Id} " +
                    $"{requirementRejection.LogDetails}; source skill-book requirement gate rejected.")
                .ConfigureAwait(false);
            return;
        }

        var updatedSpells = LegacySkillBookUse.AddOrUpdateSpell(session.PlayerSpells, learnedSpell);
        var updatedItems = ConsumeSingleLegacyInventoryItem(session.PlayerItems, item, out var consumeFrame);
        var updated = session with
        {
            PlayerItems = updatedItems,
            PlayerSpells = updatedSpells,
        };
        context.Sessions?.Set(context.Connection, updated);

        if (!string.IsNullOrWhiteSpace(context.PlayerSpellsRoot))
        {
            new PlayerSpellSnapshotLoader(context.PlayerSpellsRoot).SaveByCharacterUid(
                updated.Character.Uid,
                updatedSpells);
        }
        else
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP MsgItemUsage skill book missing PlayerSpellsRoot " +
                    $"{FormatSession(context)} itemUid={item.Uid} itemId={item.ItemId} spellId={learnedSpell.Id}; " +
                    "learned spell is session-only.")
                .ConfigureAwait(false);
        }

        if (!string.IsNullOrWhiteSpace(context.PlayerItemsRoot))
        {
            new PlayerItemSnapshotLoader(context.PlayerItemsRoot).SaveByCharacterUid(
                updated.Character.Uid,
                updatedItems);
        }
        else
        {
            await context.Log.WriteLineAsync(
                    "PARITY-GAP MsgItemUsage skill book missing PlayerItemsRoot " +
                    $"{FormatSession(context)} itemUid={item.Uid} itemId={item.ItemId} spellId={learnedSpell.Id}; " +
                    "consumed item is session-only.")
                .ConfigureAwait(false);
        }

        await context.Connection.SendAsync(consumeFrame, cancellationToken).ConfigureAwait(false);
        await context.Connection.SendAsync(LegacySpellPacket.FromSnapshot(learnedSpell).ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);
        await SendLegacySkillBookDialogAsync(
                context with { Session = updated },
                $"You~have~learned~{learnedSpell.Id}.",
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer handled MsgItemUsage skill book " +
                $"{FormatSession(context with { Session = updated })} itemUid={item.Uid} itemId={item.ItemId} " +
                $"spellId={learnedSpell.Id}.")
            .ConfigureAwait(false);
    }

    private static async ValueTask SendLegacySkillBookDialogAsync(
        WorldPacketHandlerContext context,
        string text,
        CancellationToken cancellationToken)
    {
        await context.Connection.SendAsync(LegacyNpcReplyPacket.ForDialog(text).ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(LegacyNpcReplyPacket.ForClosedOption("I~see.").ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);
        await context.Connection.SendAsync(LegacyNpcReplyPacket.ForFinish().ToFrameBytes(), cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool CanUseLegacySkillBookForRequirements(
        WorldSession session,
        uint itemId,
        ushort spellId,
        out LegacySkillBookRequirementRejection rejection)
    {
        rejection = default;
        if (!TryGetSourceBackedSkillBookRequirement(itemId, spellId, out var requirement))
        {
            return true;
        }

        if (session.Character.Level < requirement.MinimumLevel)
        {
            rejection = new LegacySkillBookRequirementRejection(
                "Sorry,~you~cannot~learn~this~spell~before~your~Level~is~" +
                requirement.MinimumLevel.ToString(CultureInfo.InvariantCulture) +
                ".~Please~train~harder.",
                $"level={session.Character.Level} requiredLevel={requirement.MinimumLevel}");
            return false;
        }

        if (requirement.RequiresFireTaoist && !IsLegacyFireTaoistClass(session.Character.Class))
        {
            rejection = new LegacySkillBookRequirementRejection(
                requirement.WrongClassDialogText,
                $"class={session.Character.Class} requiredClassFamily=14");
            return false;
        }

        if (requirement.ProficiencyId != 0)
        {
            var proficiency = session.PlayerProficiencies.Proficiencies
                .FirstOrDefault(entry => entry.Id == requirement.ProficiencyId);
            if (proficiency is null || proficiency.Level < requirement.MinimumProficiencyLevel)
            {
                rejection = new LegacySkillBookRequirementRejection(
                    requirement.MissingProficiencyDialogText,
                    $"proficiencyId={requirement.ProficiencyId} " +
                    $"proficiencyLevel={(proficiency?.Level ?? 0)} " +
                    $"requiredProficiencyLevel={requirement.MinimumProficiencyLevel}");
                return false;
            }
        }

        return true;
    }

    private static bool TryGetSourceBackedSkillBookRequirement(
        uint itemId,
        ushort spellId,
        out LegacySkillBookRequirement requirement)
    {
        requirement = (itemId, spellId) switch
        {
            (1_060_101, 1_165) => new LegacySkillBookRequirement(
                MinimumLevel: 84,
                RequiresFireTaoist: true,
                WrongClassDialogText: "Sorry,this spell is just for Fire Taoist`s."),
            (721_157, 11_000) => new LegacySkillBookRequirement(
                MinimumLevel: 40),
            (721_158, 11_005) => new LegacySkillBookRequirement(
                MinimumLevel: 40),
            (725_005, 1_045) => new LegacySkillBookRequirement(
                MinimumLevel: 40,
                ProficiencyId: 410,
                MinimumProficiencyLevel: 5,
                MissingProficiencyDialogText:
                    "Sorry,~you~cannot~learn~this~skill~before~you~practice~your~blade~to~level~5.~Please~train~harder."),
            (725_010, 1_046) => new LegacySkillBookRequirement(
                MinimumLevel: 40,
                ProficiencyId: 420,
                MinimumProficiencyLevel: 5,
                MissingProficiencyDialogText:
                    "Sorry,~you~cannot~learn~this~skill~before~you~practice~your~sword~to~level~5.~Please~train~harder."),
            _ => default
        };

        return requirement.MinimumLevel != 0;
    }

    private readonly record struct LegacySkillBookRequirement(
        long MinimumLevel,
        uint ProficiencyId = 0,
        uint MinimumProficiencyLevel = 0,
        string MissingProficiencyDialogText = "",
        bool RequiresFireTaoist = false,
        string WrongClassDialogText = "");

    private readonly record struct LegacySkillBookRequirementRejection(
        string DialogText,
        string LogDetails);
}
