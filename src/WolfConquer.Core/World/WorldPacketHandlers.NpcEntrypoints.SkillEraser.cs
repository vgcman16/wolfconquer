using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

public static partial class WorldPacketHandlers
{
    private const uint LegacySkillEraserNpcId = 895;
    private const MsgActionType LegacySkillEraserRemoveSpellAction = (MsgActionType)109;
    private const string LegacySkillEraserSource = "NpcHandler.SkillEraser";
    private const string LegacySkillEraserRemovalText = "All done, the skill is now removed.";
    private const string LegacySkillEraserMenuText =
        "After you forget the skills you don't need, you can concentrate on studying new skills. Which skill would you like to forget?";

    private static readonly LegacyNpcDialogOption[] LegacySkillEraserRootOptions =
    [
        new("Forget skills in this life.", 1),
        new("I'll think about it.", LegacyNpcClosedOption),
    ];

    private static readonly LegacyNpcDialogOption[] LegacySkillEraserCategoryOptions =
    [
        new("Summon skills.", 2),
        new("General weapon skills.", 3),
        new("Transform skills.", 4),
        new("Class-based skills.", 5),
        new("I want to ask about something else.", LegacyNpcClosedOption),
    ];

    private static readonly IReadOnlyDictionary<byte, LegacySkillEraserMenu> LegacySkillEraserMenus =
        new Dictionary<byte, LegacySkillEraserMenu>
        {
            [0] = new(
                "You have to give up something in order to get something. I can help you forget skills you don't need anymore.",
                LegacySkillEraserRootOptions),
            [1] = new(
                "If you don't need some skills anymore, I can help you forget them. What kind of skill would you like to forget?",
                LegacySkillEraserCategoryOptions),
            [2] = new(LegacySkillEraserMenuText,
            [
                new("Bloody Bat.", 49),
                new("Fire Evil.", 44),
                new("Summon Bat Boss.", 45),
                new("Summon Guard.", 46),
                new("Skeleton.", 47),
                new("Summon Bat.", 43),
                new("I want to ask something else.", 1),
            ]),
            [3] = new(LegacySkillEraserMenuText,
            [
                new("FastBlade.", 23),
                new("Phoenix.", 24),
                new("Wide Strike.", 25),
                new("Next.", 26),
                new("Scent Sword.", 27),
                new("Speed Gun.", 28),
                new("Penetration.", 29),
                new("I want to ask something else.", 1),
            ]),
            [4] = new(LegacySkillEraserMenuText,
            [
                new("Night Devil.", 18),
                new("Water Elf.", 19),
                new("Golem.", 20),
                new("Divine Hare.", 22),
                new("I want to ask something else.", 1),
            ]),
            [5] = new(LegacySkillEraserMenuText,
            [
                new("Spirit Healing.", 6),
                new("Serenity.", 7),
                new("Flying Moon.", 8),
                new("Next.", 9),
                new("Cruel Shade.", 10),
                new("Compassion.", 11),
                new("Thunder.", 12),
                new("I want to ask something else.", 1),
            ]),
            [9] = new(LegacySkillEraserMenuText,
            [
                new("Invisibility.", 13),
                new("Fire.", 14),
                new("Kraken's Revenge.", 15),
                new("Pervade.", 16),
                new("Fire Circle.", 17),
                new("Previous.", 5),
                new("I want to ask something else.", 1),
            ]),
            [26] = new(LegacySkillEraserMenuText,
            [
                new("Boreas.", 30),
                new("Snow.", 31),
                new("Boom.", 32),
                new("Next.", 33),
                new("Stranded Monster.", 34),
                new("Halt.", 35),
                new("Previous.", 3),
                new("I want to ask something else.", 1),
            ]),
            [33] = new(LegacySkillEraserMenuText,
            [
                new("Seizer.", 36),
                new("Rage.", 37),
                new("Roamer.", 38),
                new("Next.", 39),
                new("EarthQuake.", 40),
                new("Celestial.", 41),
                new("Previous.", 26),
                new("I want to ask something else.", 1),
            ]),
            [39] = new(LegacySkillEraserMenuText,
            [
                new("Dragon Tail.", 42),
                new("Viper Fang.", 69),
                new("Previous.", 33),
                new("I want to ask something else.", 1),
            ]),
        };

    private static readonly IReadOnlyDictionary<byte, LegacySkillEraserSpell> LegacySkillEraserSpells =
        new Dictionary<byte, LegacySkillEraserSpell>
        {
            [6] = new(1_190, "Spirit Healing"),
            [7] = new(10_400, "Serenity"),
            [8] = new(1_320, "Flying Moon"),
            [10] = new(3_050, "Cruel Shade"),
            [11] = new(10_430, "Compassion"),
            [12] = new(1_000, "Thunder"),
            [13] = new(1_075, "Invisibility"),
            [14] = new(1_001, "Fire"),
            [15] = new(11_100, "Kraken's Revenge"),
            [16] = new(3_306, "Pervade"),
            [17] = new(1_120, "Fire Circle"),
            [18] = new(1_360, "Night Devil"),
            [19] = new(1_280, "Water Elf"),
            [20] = new(1_270, "Golem"),
            [22] = new(10_522, "Divine Hare"),
            [23] = new(1_045, "FastBlade"),
            [24] = new(1_420, "Phoenix"),
            [25] = new(1_250, "Wide Strike"),
            [27] = new(1_046, "Scent Sword"),
            [28] = new(1_260, "Speed Gun"),
            [29] = new(1_290, "Penetration"),
            [30] = new(5_050, "Boreas"),
            [31] = new(5_010, "Snow"),
            [32] = new(5_040, "Boom"),
            [34] = new(5_020, "Stranded Monster"),
            [35] = new(1_300, "Halt"),
            [36] = new(7_109, "Seizer"),
            [37] = new(7_020, "Rage"),
            [38] = new(7_040, "Roamer"),
            [40] = new(7_010, "EarthQuake"),
            [41] = new(7_030, "Celestial"),
            [42] = new(11_000, "Dragon Tail"),
            [43] = new(4_010, "Summon Bat"),
            [44] = new(4_060, "Fire Evil"),
            [45] = new(4_000, "Guard"),
            [46] = new(4_000, "Guard"),
            [47] = new(4_070, "Skeleton"),
            [49] = new(8_262, "Bloody Bat"),
            [69] = new(11_005, "Viper Fang"),
        };

    public static async ValueTask<bool> TryHandleLegacySkillEraserNpcAsync(
        WorldPacketHandlerContext context,
        WorldSession session,
        uint npcId,
        byte optionId,
        CancellationToken cancellationToken)
    {
        if (npcId != LegacySkillEraserNpcId)
        {
            return false;
        }

        if (LegacySkillEraserMenus.TryGetValue(optionId, out var menu))
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacySkillEraserSource,
                    [menu.Text],
                    menu.Options,
                    avatarId: 100,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        if (!LegacySkillEraserSpells.TryGetValue(optionId, out var spell))
        {
            return false;
        }

        var remainingSpells = session.PlayerSpells.Spells
            .Where(candidate => candidate.Id != spell.SpellId)
            .ToArray();
        if (remainingSpells.Length == session.PlayerSpells.Spells.Count)
        {
            await SendLegacyNpcDialogAsync(
                    context,
                    LegacySkillEraserSource,
                    [$"Sorry, you haven't learned the skill {spell.Name}. How can you forget it?"],
                    [new LegacyNpcDialogOption("Okay.", 1)],
                    avatarId: 6,
                    cancellationToken)
                .ConfigureAwait(false);
            return true;
        }

        var updatedSpells = new PlayerSpellSnapshot(remainingSpells);
        var removalFrame = new MsgActionPacket
        {
            ObjId = CheckedUInt32(session.Character.Uid, nameof(session.Character.Uid)),
            DwParam = spell.SpellId,
            Type = LegacySkillEraserRemoveSpellAction,
        }.ToFrameBytes();
        if (!TryCommitLegacyFreeSpells(context, session, updatedSpells, cancellationToken,
                out var updatedSession, out var reason))
        {
            await context.Log.WriteLineAsync("WorldServer refused skill erasure before commitment; spell unchanged. reason=" + reason)
                .ConfigureAwait(false);
            return true;
        }
        await context.Connection.SendAsync(removalFrame, cancellationToken).ConfigureAwait(false);

        await SendLegacyNpcDialogAsync(
                context with { Session = updatedSession },
                LegacySkillEraserSource,
                [LegacySkillEraserRemovalText],
                [new LegacyNpcDialogOption("Okay.", LegacyNpcClosedOption)],
                avatarId: 6,
                cancellationToken)
            .ConfigureAwait(false);
        await context.Log.WriteLineAsync(
                "WorldServer removed LegacySkillEraser spell " +
                $"{FormatSession(context with { Session = updatedSession })} npcId={npcId} option={optionId} " +
                $"source={LegacySkillEraserSource} spellId={spell.SpellId} " +
                $"persisted=true.")
            .ConfigureAwait(false);
        return true;
    }

    private readonly record struct LegacySkillEraserMenu(
        string Text,
        IReadOnlyList<LegacyNpcDialogOption> Options);

    private readonly record struct LegacySkillEraserSpell(ushort SpellId, string Name);
}
