namespace WolfConquer.Core.World;

public enum LegacyMagicSupportKind
{
    Unsupported,
    Healing,
    Revive,
    ManaRestore,
    DetachFlightStatus,
    DispatchXp,
    DragonRoarStamina,
    RemoveExistingSummons,
    ShadowClone,
    Thundercloud,
}

public enum LegacyMagicSupportModelingStatus
{
    Modeled,
    Unsupported,
}

public enum LegacyMagicSupportTargetKind
{
    Unknown,
    Caster,
    Player,
    Monster,
    TeamMember,
}

public sealed record LegacyMagicSupportSpell(
    ushort Id,
    LegacyMagicSort Sort,
    string Name,
    byte Level,
    ushort UseMana,
    uint RawDamage,
    uint Power,
    int Rate,
    uint Duration,
    uint Experience,
    byte MaxTargets,
    byte Range,
    int Status,
    ushort NeedLevel,
    uint WeaponType,
    byte UseStamina,
    ushort CoolDown,
    int ColdTime);

public readonly record struct LegacyMagicSupportClassification(
    LegacyMagicSupportKind Kind,
    LegacyMagicSupportModelingStatus Status,
    bool Supported,
    string Reason,
    string SourceEvidence,
    string NextRuntimeEffect);

public sealed record LegacyMagicSupportActor(
    uint Uid,
    LegacyMagicSupportTargetKind Kind,
    ushort X,
    ushort Y,
    uint Hitpoints,
    uint MaxHitpoints,
    ushort Stamina = 0,
    ushort XpCount = 0,
    bool HasHeavenBlessing = false,
    bool HasSoulShackle = false,
    bool HasFly = false,
    bool HasHeavensWrath = false,
    bool HasInfinity = false,
    bool KeepsXpCountOnRevive = false,
    int BattlePower = 0,
    bool IsTeamMember = false,
    bool IsGuildMember = false);

public sealed record LegacyMagicSupportTargetOutcome(
    uint Uid,
    LegacyMagicSupportTargetKind Kind,
    uint AnimationUid,
    uint HitpointDelta,
    uint NewHitpoints,
    ushort StaminaDelta,
    ushort NewStamina,
    ushort XpCountDelta,
    ushort NewXpCount,
    uint Damage,
    bool Hit,
    bool Revived,
    bool RemovedFly,
    string Reason);

public sealed record LegacyMagicSupportSummonOutcome(
    LegacyMagicSupportKind Kind,
    string Name,
    int Count,
    IReadOnlyList<int> CloneFlags,
    uint PetFlag,
    uint MonsterFamilyId,
    uint Mesh,
    uint FloorItemId,
    uint AttackSpellId,
    ushort X,
    ushort Y,
    int LifetimeSeconds,
    string Reason);

public sealed record LegacyMagicSupportOutcome(
    LegacyMagicSupportKind Kind,
    LegacyMagicSupportModelingStatus Status,
    ushort SpellId,
    byte SpellLevel,
    string Reason,
    string SourceEvidence,
    uint SpellExperienceDamage,
    bool ConsumesXpList,
    int RemovedCloneCount,
    IReadOnlyList<LegacyMagicSupportTargetOutcome> Targets,
    IReadOnlyList<LegacyMagicSupportSummonOutcome> Summons,
    IReadOnlyList<string> PendingRuntimeEffects)
{
    public bool Supported => Status == LegacyMagicSupportModelingStatus.Modeled;

    public bool HasObservableEffect =>
        Targets.Count > 0 ||
        Summons.Count > 0 ||
        RemovedCloneCount > 0;
}

public static partial class LegacyMagicSupportRuntime
{
    private const string RecruitEvidence =
        "references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer.AttackHandler\\Recruit.cs heals self, monsters, visible players for 1005/1190, and nearby team members with Base.CalculateHealtDmg.";

    private const string AddManaEvidence =
        "references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer.AttackHandler\\AddMana.cs handles spell 1195 by restoring missing self mana, sending MsgSpellAnimation, and updating spell experience.";

    private const string DetachStatusEvidence =
        "references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer.AttackHandler\\DetachStatus.cs cases 1050, 1100, 16610, and 6004 revive players or detach Fly from HeavensWrath targets.";

    private const string DispatchXpEvidence =
        "references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer.AttackHandler\\DispatchXp.cs case 12280 restores teammate stamina; other DispatchXp casts consume XPList and add XPCount.";

    private const string CallPetEvidence =
        "references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer.AttackHandler\\CallPet.cs removes existing clones before spell verification, then cases 12090 and 12840 create ShadowClone and Thundercloud projections.";

    public static LegacyMagicSupportClassification Classify(LegacyMagicSupportSpell spell)
    {
        ArgumentNullException.ThrowIfNull(spell);
        return Classify(spell.Sort, spell.Id);
    }

    public static LegacyMagicSupportClassification Classify(LegacyMagicRuntimeSpell spell)
    {
        return Classify(spell.Sort, spell.Id);
    }

    public static LegacyMagicSupportClassification Classify(LegacyMagicSort sort, ushort spellId)
    {
        if (IsSingleTargetDetachSpell(spellId))
        {
            return new LegacyMagicSupportClassification(
                LegacyMagicSupportKind.DetachFlightStatus,
                LegacyMagicSupportModelingStatus.Modeled,
                Supported: true,
                "DetachStatus case 6004 removes Fly from eligible HeavensWrath targets after a deterministic rate check input and applies the source 10% hitpoint damage value.",
                DetachStatusEvidence,
                "Runtime still needs player status mutation, ReceiveAttack fanout, spell animation, and spell experience packet wiring.");
        }

        if (IsReviveSupportSpell(sort, spellId))
        {
            return new LegacyMagicSupportClassification(
                LegacyMagicSupportKind.Revive,
                LegacyMagicSupportModelingStatus.Modeled,
                Supported: true,
                "DetachStatus revive cases are represented as pure target revive outcomes with source-backed eligibility for Revive, Pray, and Mercy Prayer.",
                DetachStatusEvidence,
                "Runtime still needs Player.Revive side effects, target visibility, animation fanout, and optional Taoist epic bonus handling.");
        }

        if (IsRecruitSupportSort(sort))
        {
            return new LegacyMagicSupportClassification(
                LegacyMagicSupportKind.Healing,
                LegacyMagicSupportModelingStatus.Modeled,
                Supported: true,
                "Recruit healing is represented as pure hitpoint deltas, including the 1005/1190 single-target branch and normal team fanout branch.",
                RecruitEvidence,
                "Runtime still needs target selection, hitpoint mutation, animation fanout, and spell experience packet wiring.");
        }

        if (IsAddManaSupportSpell(sort, spellId))
        {
            return new LegacyMagicSupportClassification(
                LegacyMagicSupportKind.ManaRestore,
                LegacyMagicSupportModelingStatus.Modeled,
                Supported: true,
                "AddMana.cs case 1195 restores missing mana for the caster when self-targeted, then sends a spell animation and updates spell experience.",
                AddManaEvidence,
                "Runtime restores caster mana, sends a Mana update, emits MsgSpellAnimation, and applies spell experience.");
        }

        if (IsDispatchXpSort(sort, spellId))
        {
            var kind = spellId == 12_280
                ? LegacyMagicSupportKind.DragonRoarStamina
                : LegacyMagicSupportKind.DispatchXp;
            return new LegacyMagicSupportClassification(
                kind,
                LegacyMagicSupportModelingStatus.Modeled,
                Supported: true,
                "DispatchXp support is represented as pure XPCount or stamina deltas with source range checks and XPList consumption rules.",
                DispatchXpEvidence,
                "Runtime still needs XPList flag mutation, MsgUpdate stamina/XP circle packets, animation fanout, and spell experience packet wiring.");
        }

        if (IsSummonPetSort(sort))
        {
            var kind = spellId switch
            {
                12_840 => LegacyMagicSupportKind.Thundercloud,
                _ => LegacyMagicSupportKind.ShadowClone,
            };

            return new LegacyMagicSupportClassification(
                kind,
                LegacyMagicSupportModelingStatus.Modeled,
                Supported: true,
                "CallPet summon support is represented as pure clone removal or summon projection descriptors for ShadowClone and Thundercloud.",
                CallPetEvidence,
                "Runtime still needs clone state, monster/floor projection creation, pet info packets, action 134/135 fanout, and map random coordinate selection.");
        }

        return new LegacyMagicSupportClassification(
            LegacyMagicSupportKind.Unsupported,
            LegacyMagicSupportModelingStatus.Unsupported,
            Supported: false,
            "This spell is not in the reviewed healing, revive/detach, XP, or CallPet support helper lane.",
            "No reviewed support-helper source evidence for this sort or spell ID.",
            "Map the spell against magictype.txt and the decompiled AttackHandler switch before modeling it here.");
    }

    public static LegacyMagicSupportOutcome CalculateHealing(
        LegacyMagicSupportSpell spell,
        LegacyMagicSupportActor caster,
        uint opponentUid,
        LegacyMagicSupportActor? primaryTarget,
        IReadOnlyList<LegacyMagicSupportActor>? teamMembers = null)
    {
        ArgumentNullException.ThrowIfNull(spell);

        if (!IsRecruitSupportSort(spell.Sort))
        {
            return UnsupportedOutcome(spell, "Only Recruit sort 2 spells are valid healing support inputs.");
        }

        var targets = new List<LegacyMagicSupportTargetOutcome>();
        if (IsSingleTargetHealingSpell(spell.Id))
        {
            if (opponentUid == caster.Uid)
            {
                targets.Add(CreateHitpointOutcome(
                    caster with { Kind = LegacyMagicSupportTargetKind.Caster },
                    caster.Uid,
                    CalculateHealingDelta(spell.Power, caster.MaxHitpoints, caster.Hitpoints),
                    "Recruit.cs 1005/1190 self branch heals the caster and animates the caster UID."));
            }
            else if (primaryTarget is { Kind: LegacyMagicSupportTargetKind.Monster } monster)
            {
                targets.Add(CreateHitpointOutcome(
                    monster,
                    monster.Uid,
                    CalculateHealingDelta(spell.Power, monster.MaxHitpoints, monster.Hitpoints),
                    "Recruit.cs 1005/1190 monster branch heals the visible monster and animates the monster UID."));
            }
            else if (primaryTarget is { Kind: LegacyMagicSupportTargetKind.Player or LegacyMagicSupportTargetKind.TeamMember } player)
            {
                targets.Add(CreateHitpointOutcome(
                    player,
                    caster.Uid,
                    CalculateHealingDelta(spell.Power, player.MaxHitpoints, player.Hitpoints),
                    "Recruit.cs 1005/1190 visible-player branch heals the target player but enqueues the caster UID in the spell animation."));
            }

            return CreateOutcome(
                spell,
                LegacyMagicSupportKind.Healing,
                "Modeled Recruit single-target healing branch for Cure/SpiritHealing-style spell IDs 1005 and 1190.",
                RecruitEvidence,
                spellExperienceDamage: spell.Power,
                targets,
                pendingRuntimeEffects:
                [
                    "Apply target hitpoint mutation.",
                    "Send MsgSpellAnimation and spell experience update from UpdateSpell.CheckUpdate.",
                ]);
        }

        if (opponentUid == caster.Uid)
        {
            targets.Add(CreateHitpointOutcome(
                caster with { Kind = LegacyMagicSupportTargetKind.Caster },
                caster.Uid,
                CalculateHealingDelta(spell.Power, caster.MaxHitpoints, caster.Hitpoints),
                "Recruit.cs normal branch heals the caster when OpponentUID is the caster."));
        }
        else if (primaryTarget is { Kind: LegacyMagicSupportTargetKind.Monster } monster)
        {
            var clampHitpoints = AddSaturating(monster.Hitpoints, spell.Power);
            targets.Add(CreateHitpointOutcome(
                monster,
                monster.Uid,
                CalculateHealingDelta(spell.Power, monster.MaxHitpoints, clampHitpoints),
                "Recruit.cs normal monster branch passes current hitpoints plus spell damage into Base.CalculateHealtDmg, suppressing exact/full overheal."));
        }

        foreach (var member in teamMembers ?? Array.Empty<LegacyMagicSupportActor>())
        {
            if (GetChebyshevDistance(caster.X, caster.Y, member.X, member.Y) >= spell.Range)
            {
                continue;
            }

            targets.Add(CreateHitpointOutcome(
                member with { Kind = LegacyMagicSupportTargetKind.TeamMember },
                member.Uid,
                CalculateHealingDelta(spell.Power, member.MaxHitpoints, member.Hitpoints),
                "Recruit.cs normal team branch heals each teammate with distance strictly less than Spell.Range."));
        }

        return CreateOutcome(
            spell,
            LegacyMagicSupportKind.Healing,
            "Modeled Recruit normal healing branch for self, monsters, and nearby team members.",
            RecruitEvidence,
            spellExperienceDamage: spell.Power,
            targets,
            pendingRuntimeEffects:
            [
                "Apply target hitpoint mutation.",
                "Send MsgSpellAnimation and spell experience update from UpdateSpell.CheckUpdate.",
            ]);
    }

    public static LegacyMagicSupportOutcome CalculateReviveSupport(
        LegacyMagicSupportSpell spell,
        LegacyMagicSupportActor caster,
        LegacyMagicSupportActor? target,
        bool casterIsWatching = false,
        bool mercyPrayerWeaponEligible = true,
        bool casterHasTeam = false,
        bool targetHasTeam = false)
    {
        ArgumentNullException.ThrowIfNull(spell);

        if (!IsReviveSupportSpell(spell.Sort, spell.Id))
        {
            return UnsupportedOutcome(spell, "Only DetachStatus revive spell IDs 1050, 1100, and 16610 are valid revive support inputs.");
        }

        if (casterIsWatching)
        {
            return CreateOutcome(
                spell,
                LegacyMagicSupportKind.Revive,
                "DetachStatus.cs blocks revive spells on watcher-only maps before animation or spell experience update.",
                DetachStatusEvidence,
                spellExperienceDamage: 0,
                targets: [],
                pendingRuntimeEffects:
                [
                    "Runtime should send the source system message when watcher-map rules block the cast.",
                ]);
        }

        if (spell.Id == 16_610)
        {
            if (!mercyPrayerWeaponEligible)
            {
                return CreateOutcome(
                    spell,
                    LegacyMagicSupportKind.Revive,
                    "Mercy Prayer requires no right weapon or a Taoist epic right weapon; the source breaks before animation when this gate fails.",
                    DetachStatusEvidence,
                    spellExperienceDamage: 0,
                    targets: [],
                    pendingRuntimeEffects:
                    [
                        "Runtime should evaluate the caster right-weapon gate before applying Mercy Prayer.",
                    ]);
            }

            if (target?.HasSoulShackle == true)
            {
                return CreateOutcome(
                    spell,
                    LegacyMagicSupportKind.Revive,
                    "Mercy Prayer breaks before animation or spell experience when the visible target has SoulShackle.",
                    DetachStatusEvidence,
                    spellExperienceDamage: 0,
                    targets: [],
                    pendingRuntimeEffects:
                    [
                        "Runtime should read the target SoulShackle status flag before applying Mercy Prayer.",
                    ]);
            }

            if (target is not null &&
                (casterHasTeam || targetHasTeam) &&
                (target.IsTeamMember || target.IsGuildMember))
            {
                return CreateReviveOutcome(spell, target, "Mercy Prayer revives a visible team member or guild member when the target is not SoulShackled.");
            }

            return CreateOutcome(
                spell,
                LegacyMagicSupportKind.Revive,
                "Mercy Prayer sends the spell animation/update path only after the weapon gate, but no target is revived without team or guild eligibility.",
                DetachStatusEvidence,
                spellExperienceDamage: spell.Duration,
                targets: [],
                pendingRuntimeEffects:
                [
                    "Runtime should preserve the source eligibility check against team and guild relation state.",
                ]);
        }

        var targets = new List<LegacyMagicSupportTargetOutcome>();
        if (target is { Kind: LegacyMagicSupportTargetKind.Player or LegacyMagicSupportTargetKind.TeamMember })
        {
            targets.Add(CreateReviveTargetOutcome(target, "DetachStatus.cs Revive/Pray visible-player branch enqueues the target and calls Player.Revive."));
        }

        return CreateOutcome(
            spell,
            LegacyMagicSupportKind.Revive,
            spell.Id == 1_050
                ? "Revive removes XPList from the caster, revives the visible player target, and updates spell experience with Spell.Duration."
                : "Pray revives the visible player target and updates spell experience with Spell.Duration.",
            DetachStatusEvidence,
            spellExperienceDamage: spell.Duration,
            targets,
            consumesXpList: spell.Id == 1_050,
            pendingRuntimeEffects:
            [
                "Call Player.Revive, including hitpoint, stamina, XPCount, map-status, spawn, and optional QuickMount side effects.",
                "Send MsgSpellAnimation and spell experience update from UpdateSpell.CheckUpdate.",
            ]);
    }

    public static LegacyMagicSupportOutcome CalculateDetachStatus(
        LegacyMagicSupportSpell spell,
        LegacyMagicSupportActor caster,
        LegacyMagicSupportActor? target,
        int rateRoll)
    {
        ArgumentNullException.ThrowIfNull(spell);

        if (!IsSingleTargetDetachSpell(spell.Id))
        {
            return UnsupportedOutcome(spell, "Only DetachStatus spell ID 6004 is valid for detach-flight support inputs.");
        }

        var targets = new List<LegacyMagicSupportTargetOutcome>();
        if (target is { Kind: LegacyMagicSupportTargetKind.Player or LegacyMagicSupportTargetKind.TeamMember } player &&
            player.HasFly &&
            player.HasHeavensWrath &&
            !player.HasInfinity)
        {
            var effectiveRate = spell.Rate + caster.BattlePower - player.BattlePower;
            var normalizedRoll = Math.Clamp(rateRoll, 0, 99);
            if (effectiveRate > normalizedRoll)
            {
                var damage = (uint)(player.Hitpoints * 0.1d);
                var newHitpoints = player.Hitpoints <= damage ? 0 : player.Hitpoints - damage;
                targets.Add(new LegacyMagicSupportTargetOutcome(
                    player.Uid,
                    player.Kind,
                    player.Uid,
                    HitpointDelta: 0,
                    NewHitpoints: newHitpoints,
                    StaminaDelta: 0,
                    NewStamina: player.Stamina,
                    XpCountDelta: 0,
                    NewXpCount: player.XpCount,
                    Damage: damage,
                    Hit: true,
                    Revived: false,
                    RemovedFly: true,
                    $"DetachStatus.cs 6004 success removes Fly and applies floor(current hitpoints * 0.1); effectiveRate={effectiveRate} roll={normalizedRoll}."));
            }
            else
            {
                targets.Add(new LegacyMagicSupportTargetOutcome(
                    player.Uid,
                    player.Kind,
                    player.Uid,
                    HitpointDelta: 0,
                    NewHitpoints: player.Hitpoints,
                    StaminaDelta: 0,
                    NewStamina: player.Stamina,
                    XpCountDelta: 0,
                    NewXpCount: player.XpCount,
                    Damage: spell.Id,
                    Hit: false,
                    Revived: false,
                    RemovedFly: false,
                    $"DetachStatus.cs 6004 miss enqueues a target with Hit=0 and the spell id as animation damage; effectiveRate={effectiveRate} roll={normalizedRoll}."));
            }
        }

        return CreateOutcome(
            spell,
            LegacyMagicSupportKind.DetachFlightStatus,
            "Modeled DetachStatus 6004 fly-removal support branch.",
            DetachStatusEvidence,
            spellExperienceDamage: 250,
            targets,
            pendingRuntimeEffects:
            [
                "Remove Fly status flag when RemovedFly is true.",
                "Route damage through player receive-attack side effects and send MsgSpellAnimation.",
            ]);
    }

    public static LegacyMagicSupportOutcome CalculateDispatchXp(
        LegacyMagicSupportSpell spell,
        LegacyMagicSupportActor caster,
        uint opponentUid,
        LegacyMagicSupportActor? primaryTarget,
        IReadOnlyList<LegacyMagicSupportActor>? teamMembers = null,
        bool casterHadXpList = true,
        bool casterHasTeam = false)
    {
        ArgumentNullException.ThrowIfNull(spell);

        if (!IsDispatchXpSort(spell.Sort, spell.Id))
        {
            return UnsupportedOutcome(spell, "Only DispatchXp sort 11 support spells are valid XP support inputs.");
        }

        var members = teamMembers ?? Array.Empty<LegacyMagicSupportActor>();
        if (spell.Id == 12_280)
        {
            var targets = new List<LegacyMagicSupportTargetOutcome>();
            foreach (var member in members)
            {
                if (member.Uid == caster.Uid ||
                    GetChebyshevDistance(caster.X, caster.Y, member.X, member.Y) > spell.MaxTargets)
                {
                    continue;
                }

                var cap = member.HasHeavenBlessing ? (ushort)150 : (ushort)100;
                var newStamina = AddCapped(member.Stamina, checked((ushort)Math.Min(ushort.MaxValue, spell.Power)), cap);
                targets.Add(new LegacyMagicSupportTargetOutcome(
                    member.Uid,
                    LegacyMagicSupportTargetKind.TeamMember,
                    member.Uid,
                    HitpointDelta: 0,
                    NewHitpoints: member.Hitpoints,
                    StaminaDelta: SubtractFloorZero(newStamina, member.Stamina),
                    NewStamina: newStamina,
                    XpCountDelta: 0,
                    NewXpCount: member.XpCount,
                    Damage: 0,
                    Hit: true,
                    Revived: false,
                    RemovedFly: false,
                    "DispatchXp.cs DragonRoar restores teammate stamina within distance <= Spell.MaxTargets, capped at 150 with HeavenBlessing or 100 otherwise."));
            }

            return CreateOutcome(
                spell,
                LegacyMagicSupportKind.DragonRoarStamina,
                "Modeled DispatchXp DragonRoar teammate stamina support branch.",
                DispatchXpEvidence,
                spellExperienceDamage: casterHasTeam || members.Count > 0 ? 200U : 0U,
                targets,
                pendingRuntimeEffects:
                [
                    "Apply teammate stamina mutation and send MsgUpdate.DataType.Stamina.",
                    "Send MsgSpellAnimation and conditional spell experience update from UpdateSpell.CheckUpdate.",
                ]);
        }

        if (!casterHadXpList)
        {
            return CreateOutcome(
                spell,
                LegacyMagicSupportKind.DispatchXp,
                "DispatchXp.cs returns before animation when RemoveFlag(XPList) fails.",
                DispatchXpEvidence,
                spellExperienceDamage: 0,
                targets: [],
                pendingRuntimeEffects:
                [
                    "Runtime should consume the caster XPList flag before applying generic DispatchXp.",
                ]);
        }

        var xpTargets = new List<LegacyMagicSupportTargetOutcome>();
        if (opponentUid == caster.Uid)
        {
            xpTargets.Add(CreateXpOutcome(caster with { Kind = LegacyMagicSupportTargetKind.Caster }, "DispatchXp.cs generic self branch adds 20 XPCount to the caster."));
        }
        else if (primaryTarget is { Kind: LegacyMagicSupportTargetKind.Player or LegacyMagicSupportTargetKind.TeamMember } player)
        {
            xpTargets.Add(CreateXpOutcome(player, "DispatchXp.cs generic visible-player branch adds 20 XPCount to the target."));
        }

        if (casterHasTeam || members.Count > 0)
        {
            foreach (var member in members)
            {
                if (member.Uid == caster.Uid ||
                    GetChebyshevDistance(caster.X, caster.Y, member.X, member.Y) >= 18)
                {
                    continue;
                }

                xpTargets.Add(CreateXpOutcome(
                    member with { Kind = LegacyMagicSupportTargetKind.TeamMember },
                    "DispatchXp.cs generic team branch adds 20 XPCount to each teammate with distance strictly less than 18."));
            }
        }

        return CreateOutcome(
            spell,
            LegacyMagicSupportKind.DispatchXp,
            "Modeled generic DispatchXp XPList-consuming XPCount support branch.",
            DispatchXpEvidence,
            spellExperienceDamage: xpTargets.Count > 0 || casterHasTeam ? 200U : 0U,
            xpTargets,
            consumesXpList: true,
            pendingRuntimeEffects:
            [
                "Remove caster XPList flag and send the source XPList update.",
                "Apply XPCount mutations, send MsgSpellAnimation, and conditionally update spell experience.",
            ]);
    }

    public static LegacyMagicSupportOutcome CalculateCallPet(
        LegacyMagicSupportSpell spell,
        LegacyMagicSupportActor caster,
        ushort castX,
        ushort castY,
        int existingCloneCount,
        bool doubleThunderRuneEquipped = false)
    {
        ArgumentNullException.ThrowIfNull(spell);

        if (existingCloneCount > 0)
        {
            return CreateOutcome(
                spell,
                LegacyMagicSupportKind.RemoveExistingSummons,
                "CallPet.cs removes all existing clones before CanUseSpell verification and does not create a new spell animation in that branch.",
                CallPetEvidence,
                spellExperienceDamage: 0,
                targets: [],
                removedCloneCount: existingCloneCount,
                pendingRuntimeEffects:
                [
                    "Send action type 135 removal for each clone and clear Player.MyClones.",
                ]);
        }

        if (!IsSummonPetSort(spell.Sort))
        {
            return UnsupportedOutcome(spell, "Only CallPet sort 23 spells are valid summon support inputs.");
        }

        if (spell.Id == 12_090)
        {
            var cloneFlags = spell.Level >= 4 ? new[] { 3, 10_003 } : new[] { 3 };
            return CreateOutcome(
                spell,
                LegacyMagicSupportKind.ShadowClone,
                "Modeled CallPet ShadowClone summon descriptor.",
                CallPetEvidence,
                spellExperienceDamage: 1_000,
                targets: [],
                summons:
                [
                    new LegacyMagicSupportSummonOutcome(
                        LegacyMagicSupportKind.ShadowClone,
                        "ShadowClone",
                        cloneFlags.Length,
                        cloneFlags,
                        PetFlag: 0,
                        MonsterFamilyId: 0,
                        Mesh: 0,
                        FloorItemId: 0,
                        AttackSpellId: 0,
                        castX,
                        castY,
                        LifetimeSeconds: 0,
                        spell.Level >= 4
                            ? "CallPet.cs level >= 4 creates two ShadowClone entries with flags 3 and 10003."
                            : "CallPet.cs level < 4 creates one ShadowClone entry with flag 3."),
                ],
                pendingRuntimeEffects:
                [
                    "Create Clone runtime entries, send action type 134 for each clone, then send MsgSpellAnimation and spell experience update.",
                ]);
        }

        if (spell.Id == 12_840)
        {
            var count = doubleThunderRuneEquipped ? 2 : 1;
            var petFlag = spell.Level >= 5 ? 9U : 3U;
            return CreateOutcome(
                spell,
                LegacyMagicSupportKind.Thundercloud,
                "Modeled CallPet Thundercloud summon descriptor.",
                CallPetEvidence,
                spellExperienceDamage: 1_000,
                targets: [],
                summons:
                [
                    new LegacyMagicSupportSummonOutcome(
                        LegacyMagicSupportKind.Thundercloud,
                        "Thundercloud",
                        count,
                        Array.Empty<int>(),
                        petFlag,
                        MonsterFamilyId: 999_996_641,
                        Mesh: 980,
                        FloorItemId: 3_843,
                        AttackSpellId: 13_190,
                        castX,
                        castY,
                        LifetimeSeconds: 25,
                        doubleThunderRuneEquipped
                            ? "CallPet.cs DoubleThunder creates two Thundercloud floor-monster projections; later coordinates come from Map.GetRandCoord."
                            : "CallPet.cs creates one Thundercloud floor-monster projection at the attack coordinates."),
                ],
                pendingRuntimeEffects:
                [
                    "Create MonsterFamily copy from family 1 and overwrite Thundercloud projection fields.",
                    "Send pet info, floor/monster spawn, action type 134, MsgSpellAnimation, and spell experience update.",
                    "Use map random coordinate selection for subsequent DoubleThunder projections.",
                ]);
        }

        return UnsupportedOutcome(spell, "CallPet.cs only models spell IDs 12090 and 12840 after existing clone removal.");
    }

    private static uint CalculateHealingDelta(uint power, uint maxHitpoints, uint currentHitpoints)
    {
        if (maxHitpoints == currentHitpoints)
        {
            return 0;
        }

        if (maxHitpoints > currentHitpoints)
        {
            var missing = maxHitpoints - currentHitpoints;
            return missing >= power ? power : missing;
        }

        return 0;
    }

    private static int GetChebyshevDistance(ushort x, ushort y, ushort x2, ushort y2)
    {
        return Math.Max(Math.Abs(x - x2), Math.Abs(y - y2));
    }

    private static uint AddSaturating(uint left, uint right)
    {
        var sum = (ulong)left + right;
        return sum > uint.MaxValue ? uint.MaxValue : (uint)sum;
    }

    private static ushort AddSaturating(ushort left, ushort right)
    {
        var sum = left + right;
        return sum > ushort.MaxValue ? ushort.MaxValue : (ushort)sum;
    }

    private static uint AddCapped(uint left, uint right, uint cap)
    {
        var sum = (ulong)left + right;
        return sum > cap ? cap : (uint)sum;
    }

    private static ushort AddCapped(ushort left, ushort right, ushort cap)
    {
        var sum = left + right;
        return sum > cap ? cap : (ushort)sum;
    }

    private static ushort SubtractFloorZero(ushort left, ushort right)
    {
        return left > right ? checked((ushort)(left - right)) : (ushort)0;
    }

}
