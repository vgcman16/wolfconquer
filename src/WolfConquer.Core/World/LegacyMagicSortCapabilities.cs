namespace WolfConquer.Core.World;

public enum LegacyMagicSortCapability
{
    VisibleMonsterDamage,
    AttachStatusBuff,
    SupportHealing,
    ReviveSupport,
    ManaRestoreSupport,
    StaminaRestoreSupport,
    XpSkill,
    SummonPet,
    FloorTrapRuntime,
    SpecialCombatDamage,
    PirateSpecial,
    StateAndBuffRuntime,
    PoisonStatusRuntime,
    SourceSilentNoOp,
    UnknownUnsupported,
}
public enum LegacyMagicModelingStatus
{
    FullyModeled,
    PartiallyModeled,
    SourceSilentNoOp,
    MissingRuntimeNotModeled,
}

public readonly record struct LegacyMagicSortCapabilityInfo(
    LegacyMagicSort Sort,
    ushort SpellId,
    LegacyMagicSortCapability Capability,
    LegacyMagicModelingStatus Status,
    bool Supported,
    string SortName,
    string Reason,
    string SourceEvidence,
    string SuggestedNextOwner,
    string NextSeam)
{
    public bool SupportsAttachStatusSelfBuff => LegacyMagicSortCapabilities.IsSupportedAttachStatusBuffSpell(Sort, SpellId);

    public bool SupportsAttachStatusPlayerTargetBuff => LegacyMagicSortCapabilities.IsSupportedAttachStatusPlayerTargetBuffSpell(Sort, SpellId);

    public bool SupportsAttachStatusVisibleMonsterTargetBuff => LegacyMagicSortCapabilities.IsSupportedAttachStatusVisibleMonsterTargetBuffSpell(Sort, SpellId);

    public string Diagnostic => LegacyMagicSortCapabilities.FormatDiagnostic(this);
}

public static partial class LegacyMagicSortCapabilities
{
    private const string ImplementedAttachStatusSelfOrVisiblePlayerBuffSeam = "implemented-attach-status-self-or-visible-player-buff";
    private const string ImplementedClassicAttachStatusBuffSeam = "implemented-classic-attach-status-self-visible-player-visible-monster-buff";
    private const string ImplementedAttachStatusTargetDebuffSeam = "implemented-attach-status-target-debuff";
    private const string ImplementedAurasRuntimeSeam = "implemented-auras-runtime";
    private const string PlayerTargetAndSobMagicRuntimeSeam = "player-target-and-sob-magic-runtime";
    private const string PerimeterSourceParityDebtSeam = "perimeter-source-parity-debt";
    private const string AttachStatusRemainingRuntimeSeam = "attach-status-remaining-runtime";
    private const string HealingSupportRuntimeSeam = "healing-support-runtime";
    private const string MagicReviveSupportRuntimeSeam = "magic-revive-support-runtime";
    private const string ManaRestoreRuntimeSeam = "mana-restore-runtime";
    private const string StaminaRestoreRuntimeSeam = "stamina-restore-runtime";
    private const string XpSkillRuntimeSeam = "xp-skill-runtime";
    private const string ScarOfEarthRuntimeSeam = "scarofearth-runtime";
    private const string HellVortex16370RuntimeSeam = "hellvortex-16370-runtime";
    private const string ShurikenVortexRuntimeSeam = "shurikenvortex-automatic-runtime";
    private const string ManiacDanceRuntimeSeam = "maniacdance-automatic-runtime";
    private const string SummonPetRuntimeSeam = "summon-pet-runtime";
    private const string LayTrapFloorEffectRuntimeSeam = "lay-trap-floor-effect-runtime";
    private const string PhysicalSpellsRuntimeSeam = "physical-spells-combat-runtime";
    private const string FastBladeScentSwordManualLineRuntimeSeam = "fastblade-scentsword-manual-line-runtime";
    private const string ViperFangLineRuntimeSeam = "viperfang-line-runtime";
    private const string DragonTailLineRuntimeSeam = "dragontail-line-runtime";
    private const string DragonSlashLineRuntimeSeam = "dragonslash-line-runtime";
    private const string ThunderStrikerLineRuntimeSeam = "thunderstriker-line-runtime";
    private const string SpeedGunLineRuntimeSeam = "speedgun-line-runtime";
    private const string HookMoonLineRuntimeSeam = "hookmoon-line-runtime";
    private const string RageOfWarLineRuntimeSeam = "rageofwar-line-runtime";
    private const string FatalCrossRuntimeSeam = "fatalcross-combat-runtime";
    private const string DragonWhirlRuntimeSeam = "dragonwhirl-combat-runtime";
    private const string ChargingVortexRuntimeSeam = "charging-vortex-combat-runtime";
    private const string ShockRuntimeSeam = "shock-combat-runtime";
    private const string GunfireStrikeRuntimeSeam = "gunfire-strike-combat-runtime";
    private const string Strike12600RuntimeSeam = "strike-12600-combat-runtime";
    private const string Strike13050RuntimeSeam = "strike-13050-combat-runtime";
    private const string StraightFist12760RuntimeSeam = "straightfist-12760-combat-runtime";
    private const string Rectangle13060RuntimeSeam = "rectangle-13060-runtime";
    private const string Rectangle15690RuntimeSeam = "rectangle-15690-runtime";
    private const string EagleEyeDirectAttackRuntimeSeam = "eagleeye-direct-attack-runtime";
    private const string MountControlRuntimeSeam = "mount-control-runtime";
    private const string ToxicPoisonRuntimeSeam = "toxic-poison-runtime";
    private const string AxeShadowCelestialDanceStatusRuntimeSeam = "axeshadow-celestialdance-status-runtime";
    private const string KineticSparkAttackSpellSideEffectParitySeam = "kineticspark-attackspell-caller-side-effect-parity";
    private const string ArcherPreKineticProcRuntimeSeam = "archer-pre-kineticspark-proc-runtime";
    private const string WildPhoenixPhysicalRangeHookRuntimeSeam = "wildphoenix-physical-range-hook-runtime";
    private const string SectorBack18240RuntimeSeam = "sectorback-18240-runtime";
    private const string CleanSweepSectorBackRuntimeSeam = "cleansweep-sectorback-runtime";
    private const string CruelShadeRuntimeSeam = "cruelshade-combat-runtime";
    private const string GapingWoundsRuntimeSeam = "gapingwounds-combat-runtime";
    private const string DamageOnMonsterSpecialSortRuntimeSeam = "damage-on-monster-special-sort-runtime";
    private const string PirateSpecialRuntimeSeam = "pirate-special-runtime";
    private const string StateAndBuffRuntimeSeam = "state-and-buff-runtime";
    private const string SourceSilentNoOpSeam = "source-silent-noop-confirmation";
    private const string SourceTriageSeam = "source-triage";

    private const string LocalVisibleMonsterEvidence =
        "WolfConquer LegacyMagicRuntime.IsSupportedVisibleMonsterAttack plus WorldPacketHandlers.HandleLegacyPlayerMonsterMagicAttackAsync currently select visible monsters only for these sorts.";

    private const string PerimeterVisibleMonsterEvidence =
        "The matching service-pack DecompiledSource/GameServer/MrRayzo.Database/MagicType.cs line 126 maps MagicSort.Perimeter to 78. " +
        "Its MrRayzo.Game.MsgServer.AttackHandler/Perimeter.cs cases 13040, 14570/14580, and 15670/15680 use physical monster/player/SOB damage with sector arrangements 140/7, 60/3, and 500/5 respectively; its default branch uses Fan range 5 and width 160, monster damage times 2, player damage times 2/3, SOB damage times 4, and always sends the spell animation. " +
        "WolfConquer implements that default Fan branch for the exact D13 selector spell IDs 12210, 12220, 12580, 12590, 12740, and 12750; it implements the exact bounded mixed monster/player/SOB runtime for 13040, 15670, and 15680, plus the matching player/SOB slice for 14570/14580 and their separate visible-monster path. The special mixed owner preserves Base.CalculateSoul's exact no-op behavior, exact decompiled 140/7 compatibility and 500/5 compatibility, the source 13040 map-1505 early return, Trojan target scaling 10/7, non-Trojan 10/11 scaling, monster/SOB-only progress, and no-target silence. The pending weapon-proc/D13 route receives only attack-stamp bypass; it does not stand in for source OnAutoAttack suppression. Broader shared CanAttack edge exactness remains bounded debt.";

    private const string AurasRuntimeEvidence =
        "Matching service-pack DecompiledSource/GameServer/MrRayzo.Game.MsgServer.AttackHandler/Auras.cs, " +
        "MrRayzo.Role/Player.cs Player.AddAura, and MrRayzo/ThreadPool.cs define sort 51 behavior for exact " +
        "spells 10395, 10410, 10420-10424, and 11200 with flags 0x62, 0x64, 0x66, 0x68, 0x6A, " +
        "0x6C, 0x6E, and 0x80. WolfConquer uses its existing D13 opcode 2175 FlagIcon, opcode 2176 " +
        "status/update, and opcode 2239 spell-animation builders.";

    private const string ScarOfEarthRuntimeEvidence =
        "Matching service-pack DecompiledSource/GameServer/MrRayzo.Game.MsgServer.AttackHandler/SectorPasive.cs " +
        "calls CanUseSpell.Verified before its exact spell-12670 gate, constructs a hard-coded range-5 line with " +
        "width 1 and IncreaseRange true, then processes monster, player, and SOB passes in that order. Monsters use " +
        "Physical.OnMonster, players use Physical.OnPlayer followed by soul calculation and /2 scaling, and SOB NPCs " +
        "use Range.OnNpcs; monster/SOB returns feed one IncreaseExperience/UpdateSpell pass before the animation is sent. " +
        "Living monsters receive non-refreshing ScarofEarth flag 180 for 12 seconds, while living players receive it " +
        "on Core.Rate(100 - 5 * (target BattlePower - caster BattlePower)); both target types update their stored spell " +
        "and attacker metadata after each qualifying cast. DecompiledSource/GameServer/MrRayzo/ThreadPool.cs and " +
        "DecompiledSource/GameServer/MrRayzo.Game.MsgMonster/PoolProcesses.cs expire flag 180 before checking its " +
        "two-second invoke cadence and emit victim-authored, self-targeted Damage2 ticks through the original attacker. " +
        ".setup_work/seed-json/magic.json typeId 12670 rows identify ScarofEarth as sort 82 and supply its level-specific " +
        "Damage2 values.";

    private const string HellVortex16370RuntimeEvidence =
        "Matching service-pack DecompiledSource/GameServer/MrRayzo.Game.MsgServer.AttackHandler/HellVortex.cs " +
        "calls CanUseSpell.Verified before its exact spell-16370 fence and constructs MsgSpellAnimation at the query " +
        "Attack.X/Y. A selected monster OpponentUID anchors only the monster loop, whose candidates must be within " +
        "distance <= 6 of that selected monster; a selected player OpponentUID independently anchors only the player " +
        "loop, whose candidates must be within distance <= 6 of that selected player. The handler has no SOB loop. " +
        "Monsters use Physical.OnMonster, soul calculation, and Monster.Execute; players use Physical.OnPlayer, soul " +
        "calculation, then * 12 / 28 before ReceiveAttack.Player.Execute. Those receive/HP/death paths run before each " +
        "target is queued and before the final animation. The handler performs no pushback or movement, never sets the " +
        "animation bomb flag above its default 0, and calls neither IncreaseExperience.Up nor UpdateSpell.CheckUpdate; " +
        "MsgSpellAnimation.Send is last. Matching service-pack DecompiledSource/GameServer/" +
        "MrRayzo.Game.MsgServer.AttackHandler.CheckAttack/CanAttackPlayer.cs gives an equipped HellVortex defender a " +
        "10 percent roll, requires learned spell 16370, creates the counter query with X=0, Y=0, and OpponentUID set " +
        "to the attacker, re-enters ProcescMagic with ignoreStamp true, and rejects the incoming hit. This matching " +
        "service-pack source, not Source7770, is the behavior authority for this exact slice.";

    private const string ShurikenVortexRuntimeEvidence =
        "Matching service-pack DecompiledSource/GameServer/MrRayzo.Game.MsgServer.AttackHandler/" +
        "CombatGearAttack.cs case 6010 calls CanUseSpell.Verified first. Its XPList branch calls " +
        "OpenXpSkill(MsgUpdate.Flags.ShurikenVortex, 20, 1), removes XPList, and sends the learned-level " +
        "self animation. Player.OpenXpSkill is a no-op during AutoHunt; otherwise it sends XPCircle, stages " +
        "old/new XP-skill status vectors, and refreshes an existing ShurikenVortex timer without resetting its " +
        "invoke timer. DecompiledSource/GameServer/MrRayzo/ThreadPool.cs flag key 46 checks an inclusive one-second " +
        "invoke cadence after its expiry-removal block and synthesizes a current-tile magic query for spell 6010, " +
        "allowing a final due tick as the flag expires. CombatGearAttack.cs then borrows Pool.Magic[6009][0], " +
        "selects monster, player, and SOB targets in that order at strict distance < Spell.Range, uses Physical " +
        "damage with player damage divided by 6, accumulates only Monster.Execute/Npc.Execute returns into one " +
        "IncreaseExperience.Up without spell progress, and sends MsgSpellAnimation last even with no targets. " +
        "Physical.cs returns early for ShurikenVortex/ManiacDance and WildPhoenix; otherwise DefensiveStance halves " +
        "damage and marks Block, and attacker Infinity multiplies spell damage by InfinitySDamage. Matching service-pack " +
        "DecompiledSource/GameServer/MrRayzo.Game.MsgServer.AttackHandler.Calculate/Physical.cs lines 394, 577-658, " +
        "770-773, and 913-949 and Base.cs lines 156-173 define the complete modeled nonzero Breakthrough branch. " +
        "WolfConquer keeps worldData missing as a guarded unchanged path and provides a deterministic runtime test " +
        "override for the refinery roll. Refinery succeeds iff adjusted Breakthrough > adjusted Counteraction and a " +
        "0-99 roll < their difference; prior critical/lucky/dash bypasses the outer gate. Within a successful outer " +
        "gate, any qualifying Penetration boost occurs first; an equipped target Barrier then applies before its " +
        "source-nested attacker TacitStrike in sequential-floor order. Actual Penetration also requires lower attacker " +
        "BattlePower and neither Sacrifice nor DeathSigh; it adds 30 percent, or 45 percent for spells 16480/16490, " +
        "sets Penetration, applies /0.67 compensation, or /0.66 for close spell 12080, and suppresses the later " +
        "50-percent bonus. Any failed refinery, BattlePower/status-exclusion failure, or prior critical/lucky/dash path " +
        "keeps flag2 false and receives that later 50-percent bonus. The compensation precedes DefensiveStance and " +
        "Infinity, then the flag2-controlled 50-percent branch precedes IronShield. Physical.cs lines 951-964 define " +
        "the bounded post-IronShield tail: Slayer flag 0xDA scales damage by SlayerPercent / 100 and is consumed; " +
        "NoMercy clamps damage below 22000 to 22000; Assassin plus OnAutoHunt adds 38000. Rune.cs lines 328-364 " +
        "require Reborn >= 2, Level >= 15, and an equipped job-valid rune for each rune effect. Physical.cs lines " +
        "965-977 then check target DragonSwing flag 0xA0. An eligible equipped SwingingTail rune adds 30 to the target " +
        "DragonSwingChance roll only for DBSpell IDs 11030, 11060, 11590, 11600, 11650, and 11660. A successful " +
        "Core.Rate sends the target-authored poisonmiss effect, sets damage to zero, and increments target Dodge by 30. " +
        "The DragonSwing check runs after Slayer/NoMercy/Assassin and before BackDmg. Physical.cs lines 979-986 " +
        "then check target MudWall flag 0x112 and use Ninja.TryGetValueEquip for equipped " +
        "Ninja.ItemType.MudSigilImpeccable 301; when present, damage is reduced directly as " +
        "damage -= damage * (DBItem.Power / 100) / 100. Ninja.TryGetValueEquip enforces Ninja class and selects the " +
        "active primary/alternate equipment set. This direct reduction runs after BackDmg; CombatGearAttack.cs " +
        "applies Shuriken's player /6 afterward. No learned 16440 chance, Damage2, or LegacyMudWallRoll reducer " +
        "exists in the matching DecompiledSource. Target IronShield " +
        "emits AzureShield BlueDamage " +
        "type 55 for the " +
        "absorbed amount. Incoming damage less than or equal to the shield leaves one outgoing damage and consumes " +
        "that amount from the shield; strictly greater incoming damage removes IronShield and sends only the overflow " +
        "into BackDmg. The matching service-pack source is gameplay authority; the exact local D13 client remains wire " +
        "authority for opcode 2176 status and opcode 2239 animation; Source7770 contributes nothing.";

    private const string ManiacDanceRuntimeEvidence =
        "Matching service-pack DecompiledSource/GameServer/MrRayzo.Game.MsgServer.AttackHandler/ManiacDance.cs " +
        "calls CanUseSpell.Verified first, requires XPList for exact spell 12700, removes XPList, opens ManiacDance " +
        "flag 0xB5 for 15 seconds, sends a self-target MsgSpellAnimation, and seeds ManiacDanceStamp from " +
        "Equipment.AttackSpeed(false). Player.RemoveFlag publishes XPList then the reduced status vector, while " +
        "OpenXpSkill publishes XPCircle and staged old/new XP-skill status vectors and is a no-op during AutoHunt. " +
        "DecompiledSource/GameServer/MrRayzo/ThreadPool.cs FloorCallback then uses a " +
        "strict now > ManiacDanceStamp check, schedules every later attack at now+1000ms, and sends a caster-centered " +
        "radius-5 physical monster/player/SOB attack, accumulates Monster.Execute/Npc.Execute returns into one " +
        "IncreaseExperience.Up, and sends MsgSpellAnimation last even when the target list is empty. " +
        "Its player branch calls Physical.OnPlayer, where ShurikenVortex/ManiacDance and WildPhoenix return early; " +
        "otherwise DefensiveStance halves damage and marks Block, and attacker Infinity multiplies spell damage by " +
        "InfinitySDamage. Matching service-pack DecompiledSource/GameServer/" +
        "MrRayzo.Game.MsgServer.AttackHandler.Calculate/Physical.cs lines 394, 577-658, 770-773, and 913-949 and " +
        "Base.cs lines 156-173 define the complete modeled nonzero Breakthrough branch. WolfConquer keeps worldData " +
        "missing as a guarded unchanged path and provides a deterministic runtime test override for the refinery roll. " +
        "Refinery succeeds iff adjusted Breakthrough > adjusted Counteraction and a 0-99 roll < their difference; " +
        "prior critical/lucky/dash bypasses the outer gate. Within a successful outer gate, any qualifying Penetration " +
        "boost occurs first; an equipped target Barrier then applies before its source-nested attacker TacitStrike in " +
        "sequential-floor order. Actual Penetration also requires lower attacker BattlePower and neither Sacrifice nor " +
        "DeathSigh; it adds 30 percent, or 45 percent for spells 16480/16490, sets Penetration, applies /0.67 " +
        "compensation, or /0.66 for close spell 12080, and suppresses the later 50-percent bonus. Any failed refinery, " +
        "BattlePower/status-exclusion failure, or prior critical/lucky/dash path keeps flag2 false and receives that " +
        "later 50-percent bonus. The compensation precedes DefensiveStance and Infinity, then the flag2-controlled " +
        "50-percent branch precedes IronShield. Physical.cs lines 951-964 define the bounded post-IronShield tail: " +
        "Slayer flag 0xDA scales damage by SlayerPercent / 100 and is consumed; NoMercy clamps damage below 22000 " +
        "to 22000; Assassin plus OnAutoHunt adds 38000. Rune.cs lines 328-364 require Reborn >= 2, Level >= 15, " +
        "and an equipped job-valid rune for each rune effect. Physical.cs lines 965-977 then check target DragonSwing " +
        "flag 0xA0. An eligible equipped SwingingTail rune adds 30 to the target DragonSwingChance roll only for " +
        "DBSpell IDs 11030, 11060, 11590, 11600, 11650, and 11660. A successful Core.Rate sends the target-authored " +
        "poisonmiss effect, sets damage to zero, and increments target Dodge by 30. The DragonSwing check runs after " +
        "Slayer/NoMercy/Assassin and before BackDmg. Physical.cs lines 979-986 then check target MudWall flag 0x112 " +
        "and use Ninja.TryGetValueEquip for equipped Ninja.ItemType.MudSigilImpeccable 301; when present, damage is " +
        "reduced directly as damage -= damage * (DBItem.Power / 100) / 100. Ninja.TryGetValueEquip enforces Ninja " +
        "class and selects the active primary/alternate equipment set. This direct reduction runs after BackDmg, " +
        "and ManiacDance has no Shuriken caller /6 step. No learned 16440 chance, Damage2, or LegacyMudWallRoll " +
        "reducer exists in the matching DecompiledSource. Target " +
        "IronShield emits AzureShield BlueDamage type 55 while reducing the shield. Incoming damage less than or " +
        "equal to the shield " +
        "leaves one outgoing damage; strictly greater incoming damage removes IronShield and sends the overflow into " +
        "BackDmg, which remains eligible. " +
        "DecompiledSource/GameServer/MrRayzo.Role.Instance/Equip.cs AttackSpeed starts at 800ms " +
        "and applies the 100ms base reduction, right/left/ring Frequency, Agility/2, Cyclone, and DeadlySight with a " +
        "300ms floor. The matching service-pack source is gameplay authority; the exact local D13 client remains wire " +
        "authority for opcode 2176 status and opcode 2239 animation; Source7770 contributes nothing.";

    private const string StraightFistEvidence =
        "references/DecompiledSource\\GameServer\\MrRayzo.Database\\MagicType.cs maps MagicSort.StraightFist to 76. " +
        "references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer.AttackHandler\\StraightFist.cs verifies resources and increments MisShoot on counter-hit maps before the spell-ID gate; 12230 then returns without damage or animation, while 12760 uses a range-5 line whose width is 1 with spell soul and 0 otherwise, excludes guard monsters, applies physical monster/player/SOB damage, advances monster/SOB progress, and always sends the spell animation.";

    private const string D13RectangleSelectorWireEvidence =
        "Exact D13 client ._cache_Conquer.exe SHA-256 D13B45BD2681DB7D3D8548EA575D89E0D05613BA00E8F9763DBF3F89030402A5 provides wire/order authority: FUN_00AC834C returns family 16 base 13040 and family 9 base 15670, and FUN_00ACD62B adds selector index times 10, making index 2 exactly 13060 and 15690. " +
        "The D13 Frida trace labels the client plaintext send hook outbound and the client receive gates inbound. The exact runtime order is client-outbound/server-inbound physical CMsgInteract opcode 2135 AttackType 2 Damage, followed by server-outbound/client-inbound CMsgMagicEffect opcode 2239; opcode 2239 is neither client-outbound nor a client-selected attack contract. " +
        "The 7770 server is not protocol opcode, layout, direction, or order authority.";

    private const string RectangleSelectorGameplayEvidence =
        "data/DBServer\\magictype.txt exact rows 13060 and 15690 map both spell IDs to sort 87 Rectangle. " +
        "references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer.AttackHandler\\CombatGearAttack.cs case 13060 anchors to the selected player or monster, uses InLine(..., 2), excludes Guard monsters, processes monster/player/SOB targets, sends per-target InteractionCreate frames before progress and the final MsgSpellAnimation, and CombatGearAttack.cs case 15690 anchors the same way, uses Sector.Arrange(100, 5), processes monster/player/SOB targets, sends per-target InteractionCreate frames before progress and the final MsgSpellAnimation. " +
        "These matching decompiled source artifacts are gameplay evidence only. WolfConquer local gameplay owners are WorldPacketHandlers.CombatGear.DirectLineAttacks.cs for 13060 and WorldPacketHandlers.CombatGear.SectorAttacks.cs for 15690.";

    private const string LegacyAttackHandlerEvidence =
        "data/DBServer\\magictype.txt and .artifacts\\legacy-decompile-claim\\MrRayzo.Game.MsgServer\\MsgAttackPacket.cs ProcescMagic dispatch to AttackHandler classes.";

    private const string EagleEyeDirectAttackEvidence =
        "data/DBServer\\magictype.txt spell 11030 uses sort 48 DirectAttack; " +
        "references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer.AttackHandler\\DirectAttack.cs routes EagleEye's primary player target through Physical.OnPlayer and then applies caller scaling of 14/22 after soul calculation, while its secondary in-line player targets use Physical.OnPlayer without that 14/22 scaling; " +
        "references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer.AttackHandler\\Attack.cs case 11030 applies direct physical monster/player/SOB damage, arms EagleEyeCountDown and EagleEyeStamp for 20 seconds on non-BlackSpot monster/player hits, consumes learned spell 11130 on a successful rate roll against a BlackSpot target, sends the self-only 11130 animation and ZeroEagleEye cold-time reset, switches the 11030 animation to a self target carrying damage 11135 after consumption, and always uses the real SOB target without countdown mutation; " +
        "references/DecompiledSource\\GameServer\\MrRayzo\\ThreadPool.cs clears EagleEyeCountDown after EagleEyeStamp plus 20 seconds. WolfConquer local evidence is WorldPacketHandlers.PlayerMagic.EagleEye.cs plus WorldServerFrameHandler.LegacyEagleEye.cs.";

    private const string MountControlEvidence =
        "data/DBServer\\magictype.txt rows 10301-10303 define Riding 7001 sort 32, Spook 7002 sort 30, and WarCry 7003 sort 31; " +
        "references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer.AttackHandler\\Riding.cs applies CanUseSpell first, blocks Fly/transform/SteedRace and listed maps/current tournament after resources, toggles Ride for 2592000 seconds with RemoveOnDead, restores MaxVigor when mounting, and sends a self animation; " +
        "Spook.cs compares the clicked mounted player steed plus, plus progress, then perfection level/progress before dismounting and zeroing stamina; WarCry.cs scans every visible mounted player and compares only steed plus/plus progress even though it constructs an unused Sector. WolfConquer local evidence is WorldPacketHandlers.PlayerMagic.MountSkills.cs.";

    private const string ModeledSharedCanAttackPlayerGates =
        "modeled shared CanAttackPlayer gates: target CombatGear.CheckDamage reject, ImmortalForce/Overwhelm reject with direct ForbiddenPirate update, direct 10 percent equipped-and-learned defender HellVortex 16370 counter reject/countercast with source counter query X/Y 0/0 and a local active-caster/cascade-limit recursion safeguard, Solidness target-rune rejects for source spell ids 15940/3090/16550/16540/16520/16530/1165, attribute-backed tournament non-alive rejects for LastManStand/TopFight/KillerOfElite/ExtremePk/DragonWar/TeamDeathMatch, attribute-backed CTF map 2057 CaptureTheFlag.Attackable target reject, safe zones 10137/10250, map 700 DynamicID zero, dead/ghosted players, invisible target, caster/target watching, NeptuneCurse, Protect, Peace PK, Capture PK target color (FlashingName/BlackName), Team/Guild same-team/same-guild rejects, CS PK same-ServerID rejects, Union PK same-UnionUID/UnionID rejects, Jiang PK target JiangHuActive gate and source-priority Jiang flags, map 1082 same-garment reject, attribute-backed Team friend/guild-ally/clan same-or-ally rejects, attribute-backed Guild ally rejects, TeamQualifier map 700 same-team reject, attribute-backed map 1002 OnMyOwnServer/OnOwnServer reject, accepted player-target FlashingName side effect (30s RemoveOnDead), ChillingSnow/xChillingSnow cold-time side effect, earlier blocked maps 1004/1858/4000/4003/4006/4008/4009/4020 plus map 1002 unless Jiang PK, and the final Constants.BlockAttackMap map/dynamic-ID edges";

    private const string ModeledSharedCanAttackSobGates =
        "modeled shared CanAttackNpc gates: map 3844 always-allow, dead-SOB rejection outside map 3844, bounded NEWGUILDWAR GuildPillar/HonorPillar/ExorcimPillar/TenacityPillar authorization, bounded UnionWar UID 22348 pole authorization (rejects no union, zero pole HP, no guild, same winner union, Dead/Idle process, and treats missing/default process as Dead; otherwise allows damage), bounded map 11083 PoleDomination/PoleDominationBI/PoleDominationDC/PoleDominationPC guild-pole authorization for UIDs 830/831/832/833 (rejects no guild, zero pole HP, same winner guild, Dead/Idle process, and treats default/missing process as Dead; otherwise allows damage), bounded EliteGuildWar map 6521 guild-pole authorization for UIDs 6539/6540/6541 (rejects no guild, zero pole HP, same guild name as pole owner name, and process not Alive; treats MsgEliteGuildWar default Proces as Dead; otherwise allows damage), bounded MsgTrepilWar map 3838 UID 65046 guild-pole authorization (rejects no guild, zero pole HP, same guild name as pole name, and process not Alive; treats MsgTrepilWar constructor default Proces as Dead; otherwise allows damage), bounded MsgTopPole map 1737 UID 65056 player-name pole authorization using Furnitures[Pole] (rejects no guild, zero pole HP, player.Name == pole.Name, and process not Alive; treats MsgTopPole static default Proces as Dead; otherwise allows damage), bounded MsgEmperorWar map 10702 UID 6505 player-name pole authorization using Furnitures[Pole] (rejects no guild, zero pole HP, player.Name == pole.Name, and process not Alive; treats MsgEmperorWar static default Proces as Dead; otherwise allows damage), bounded ChampionsOfWarr map 6546 UID 6547 guild-pole authorization using Furnitures[Pole] (rejects no guild, zero pole HP, same guild name as pole name, and process not Alive; treats default/missing process as Dead; otherwise allows damage), bounded Guild6PoleWar map 10070 GuildPole[1-6] authorization for UIDs 100731/100732/100733/100734/100735/100736 (rejects no guild, zero pole HP, same guild name as pole name, and process not Alive; treats default/missing process as Dead; otherwise allows damage), bounded MsgWarOfPlayers map 6729 UID 6606 player-name pole authorization using Furnitures[Pole] (rejects zero pole HP, client.Player.Name == pole.Name, and process not Alive; treats default/missing process as Dead; otherwise allows damage), bounded ClassPoleWar map 10005 pole-family authorization for MsgWarriorGod UID 8001 IsWarrior, MsgArcherMaster UID 8002 IsArcher, MsgNinjaMaster UID 9803 IsNinja, MsgTrojanMaster UID 8005 IsTrojan, MsgTaoistMaster UID 8006 IsWater, MsgFireMaster UID 8007 IsFire, MsgPirateMaster UID 8008 IsPirate, MsgKungfuKing UID 8009 IsLee, MsgWindWalker UID 9011 IsWindWalker, MsgThnderStrike UID 90111 IsThunderStriker, MsgClassPolaWar UID 8012 no class gate, and MsgMonkMaster UID 8004 IsMonk (wrong class family rejects before pole HP/name/process except MsgClassPolaWar; rejects zero pole HP, client.Player.Name == pole.Name, and process not Alive; treats ClassPoleWar tournament default/missing Proces as Dead; otherwise allows damage), bounded MsgWarFighters map 11073 UID 9406 player-name pole authorization using Furnitures[Pole] (rejects zero pole HP, client.Player.Name == pole.Name, and process not Alive; treats MsgWarFighters default/missing Proces as Dead; otherwise allows damage), bounded MsgSchedules.SuperGuildWar map 3868 UID 820 guild-pole authorization using Furnitures[Pole] (rejects no guild, zero pole HP, same Winner.GuildID, and Dead/Idle process; treats MsgSuperGuildWar default/missing Proces as Dead with default Winner GuildID 0; otherwise allows damage), bounded GuildWarScore map 6784 UID 8447 guild-pole authorization (rejects no guild, zero pole HP, and process not Alive; treats default/missing process as Dead; otherwise allows damage), bounded ClanWarScore map 3839 UID 8448 clan-pole authorization (rejects no clan, zero pole HP, and process not Alive; treats default/missing process as Dead; otherwise allows damage), bounded ClanWar UID 890 pole authorization, bounded ClanDomination TC/PH/AP/DC/BR pole authorization for UIDs 1002/1011/1020/1000/1015 (rejects no clan, zero pole HP, same winner clan, and Dead/Idle process; otherwise allows damage), bounded NobilityPole map 11140 UID authorization for UIDs 22340/22341/22342/22343 with required ranks King/Prince/Duke/Earl (rejects zero pole HP, player.Name == pole.Name, Dead process, and wrong nobility rank; treats MsgNobilityPole/MsgNobilityPole1/MsgNobilityPole2/MsgNobilityPole3 default Proces as Dead; otherwise allows damage), static statue PK-mode gate, and bounded attribute-backed CaptureTheFlag base gate across player-magic SOB paths (rejects process not Alive, no guild, runtime zero HitPoints, and same capturer guild)";

    private const string RemainingCanAttackPlayerGaps =
        "CanAttackPlayer relation-gate review: no remaining reviewed shared player-magic CanAttackPlayer relation gate gaps are currently identified; exact caller-specific CanAttackPlayer side effects stay tracked separately where needed";

    private const string RemainingCanAttackNpcGaps =
        "remaining CanAttackNpc source checks: exact CanAttackNpc side-effect behavior and caller-specific coverage beyond the modeled shared player-magic SOB paths";

    private const string ClassicAttachStatusBuffEvidence =
        "Matching decompiled AttackHandler\\AttachStatus.cs cases 1075/1085/1090/1095 supply behavior and ordering: self, then visible Monster, then visible Player targeting; Spell.Duration; RemoveOnDead true; MagicShield active-state suppression; UpdateSpell.CheckUpdate before MsgSpellAnimation send; and an empty animation when no target is enqueued. " +
        "WolfConquer local owners WorldPacketHandlers.PlayerMagic.Status.ClassicBuffs.cs, WorldServerFrameHandler.LegacyClassicAttachStatus.cs, WorldServerFrameHandler.LegacyMonsterTimedStatusFlags.cs, LegacyPlayerDeathState.cs, MonsterRuntimeStateStore, WorldPacketHandlers.Visibility.Monsters.cs, and LegacyMonsterSpawnPacket.cs implement the matching branch, expiry, death-cleanup, and active-monster spawn-replay behavior. " +
        "Exact D13 client\\Env_DX8\\._cache_Conquer.exe SHA-256 D13B45BD2681DB7D3D8548EA575D89E0D05613BA00E8F9763DBF3F89030402A5 owns wire: server-outbound opcode 2176 CMsgUserAttrib/MsgUpdate type 0x19 StatusFlag vectors and server-outbound opcode 2239 CMsgMagicEffect animations. Matching decompile supplies behavior and ordering, not opcode or layout authority. The Conquer Source 7770 tree is excluded and supplies neither behavior nor wire evidence for this slice.";

    private const string AttachStatusEvidence =
        "magictype.txt sort 6 rows and decompiled AttackHandler\\AttachStatus.cs cases 6002, 6011, 8003, 9000, 9876, 1075, 1085, 1090, 1095, 11670, 11970, 12200, 12270, 13030, 14160, 14320, 14380, 14710, 15440, 15470, 15750, 15920, 16380, 16440/16450, and 30000; WolfConquer TryGetLegacyAttachStatusFlag maps the standard buff spell IDs. AttachStatus.cs case 6002 targets a visible player, lets equipped Sturdiness avoid the effect, otherwise applies PoisonStar flag 0x77 for Spell.Duration with RemoveOnDead true on an 80 percent roll, sends the source no-drugs system message to the target, uses damage 1 for the success animation target, uses spell ID damage with Hit=0 for the miss animation target, sends an empty animation when Sturdiness avoids or no target is visible, and runs UpdateSpell.CheckUpdate(..., 250) except for Sturdiness avoidance. AttachStatus.cs case 6011 silently does nothing unless XPList is active, then removes XPList, opens FatalStrike flag 0x2F for 60 seconds, and sends a caster animation with attacked UID 0 and a zero-damage self target. AttachStatus.cs case 8003 blocks Fly while transformed, riding, or under PathOfShadow with the source system message, refreshes an active Fly flag or applies Fly flag 0x1B for Spell.Duration with RemoveOnDead true, sends a caster animation with attacked UID 0 and a self target carrying Spell.Duration, and has no interaction echo or spell-progress tick. AttachStatus.cs cases 9000/11670 rewrite the interaction with SpellID=0, KilledMonster=ClientSpell.ID, and SpellLevel=ClientSpell.Level, arm InUseIntensify/IntensifyStamp/IntensifyDamage/FocusClientSpell, run UpdateSpell.CheckUpdate(..., Spell.Duration), and ThreadPool.cs promotes the charge after 5 seconds into Focused flag 0x15 for 60 seconds with a self spell animation. AttachStatus.cs case 9876 applies CastPray flag 0x1E for 2592000 seconds with RemoveOnDead true, sends a caster animation with attacked UID 0 and a zero-damage self target, and has no interaction echo or spell-progress tick. AttachStatus.cs case 11970 consumes XPList, restores stamina to the 100/150 HeavenBlessing maximum, opens SuperCyclone for 40 seconds, and sends the self animation. AttachStatus.cs case 12200 echoes the interaction, skips status/progress/animation for active DragonWar participants, toggles DragonSwing off when active, otherwise applies DragonSwing flag 0xA0 to self for 2592000 seconds with RemoveOnDead false, sends MsgUpdate.DataType.DragonSwing 0x4B using Spell.Duration, damage 33, and ClientSpell.Level, stores DragonSwingChance from Spell.Damage/100, runs duration-backed spell progress, and sends an empty animation when not self-targeted. AttachStatus.cs case 12270 echoes the interaction, toggles AutoHunt flag 0x94 off when active, otherwise applies AutoHunt to self for 2592000 seconds with RemoveOnDead false and stamp 8, sends a self-shaped animation at Attack.X/Y with the caster as attacked UID and a zero-damage self target only on self-cast, and runs duration-backed spell progress after the animation. AttachStatus.cs case 11160 clears Ride first, toggles DefensiveStance flag 0x7E, adds it for Spell.Duration with RemoveOnDead false when absent, sends MsgUpdate.DataType.DefensiveStance 0x38 with time, damage, and learned level only when adding, and sends a targetless source animation at Attack.X/Y with a zero-damage self target. AttachStatus.cs case 11620 toggles PathOfShadow flag 0x91, adds it for 2592000 seconds with RemoveOnDead false when absent, clears KineticSpark when removing an active PathOfShadow flag, and sends a targetless source animation at Attack.X/Y with a zero-damage self target. AttachStatus.cs case 12680 applies Backfire flag 0xB3 to self for 10 seconds with RemoveOnDead false and sends a targetless source animation at Attack.X/Y with a zero-damage self target. AttachStatus.cs case 13030 echoes the interaction, toggles RevengeTail off when active, otherwise applies RevengeTail to self for 10 seconds with stamp 20, stores RevengeTailChange 5 or 100000 with RevengeGale, and sends a source-shaped self or targetless animation at Attack.X/Y. AttachStatus.cs case 14160 echoes the interaction, stores IronShieldDamage from DamageOnMonster, applies IronShield to self for Spell.Duration with RemoveOnDead, sends MsgUpdate.DataType.AzureShield with DamageOnMonster and ClientSpell.Level, then sends the self animation at Attack.X/Y without an equipment refresh or spell-progress update. AttachStatus.cs case 14320 echoes the interaction, toggles Sacrifice off when active and refreshes equipment, otherwise applies Sacrifice to self for 2592000 seconds with RemoveOnDead false, sends a self animation at Attack.X/Y, refreshes equipment, and runs UpdateSpell.CheckUpdate(..., 500). AttachStatus.cs case 14380 echoes the interaction, applies Fly and Infinity to self for Spell.Duration with RemoveOnDead, stores InfinitySDamage from DamageOnHuman and InfinityDamage from DamageOnMonster, sends the self animation at Attack.X/Y, then sends MsgUpdate.DataType.Infinity. AttachStatus.cs case 14500 echoes the interaction, stores FineRainPower and defFineRainPower from Spell.Damage, sends FineRain1 and FineRain2 countdown updates, applies both FineRain flags to self and in-range living teammates with RemoveOnDead, breaks the team loop on a dead teammate, refreshes equipment, fills HP to max, sends team member info through the HitPoints setter side effect, and sends one animation containing self plus applied teammate targets without a spell-progress update. AttachStatus.cs case 14710 echoes the interaction, applies ImmortalForce flag 0xDC to self for Spell.Duration with RemoveOnDead true, and sends a source-shaped self animation at Attack.X/Y without equipment refresh or spell-progress update. AttachStatus.cs case 15380 gates the caster as WindWalker with MainFlag.OnMeleeAttack, echoes the interaction, applies CrackStar flag 0xE6 to self for Spell.Duration with RemoveOnDead, and sends a source-shaped self animation at 0/0. AttachStatus.cs case 15440 echoes the interaction, applies RiseofTaoism to self for Spell.Duration with RemoveOnDead, stores RiseofTaoismExtraMDamage from Spell.Damage, sends MsgUpdate.DataType.FineRain with Damage 50000, sends the self animation at Attack.X/Y, and refreshes equipment without a spell-progress update. AttachStatus.cs case 15470 echoes the interaction, applies IronGuard to self for Spell.Duration with RemoveOnDead, stores FuryStrikeHP from DamageOnHuman and FuryStrikeAttack from (byte)Spell.Damage, sends MsgUpdate.DataType.SkillCountdown, sends the self animation at Attack.X/Y, and refreshes equipment without a spell-progress update. AttachStatus.cs case 15750 toggles LightningShield flag 0xF0, adds it for 2592000 seconds with RemoveOnDead false when absent, removes LightningShieldActivated 0xED and sends MsgUpdate.DataType.AzureShield 0x31 reset when removing an active LightningShield flag, and sends a self-shaped animation at Attack.X/Y with a zero-damage self target. AttachStatus.cs case 15920 echoes the interaction, applies HawksAmbition to self when absent, stores Spell.Damage as HawksAmbitionPower, sends ArchiveSkill, self animation, QueryEquipment, and UpdateSpell.CheckUpdate(..., 500). AttachStatus.cs case 16380 gates the caster as Ninja, applies InfiniteMist to the visible player target, and sends the odd self-shaped animation from caster to caster. AttachStatus.cs cases 16440/16450 apply MudWall to self with ArchiveSkill countdown, self animation, and QueryEquipment. AttachStatus.cs cases 12950, 12960, and 13020 handle HealingSnow, ChillingSnow, and FreezingPelter as self-target toggles that clear the sibling snow flags before applying the selected snow flag. AttachStatus.cs case 13020 calls QueryEquipment when adding FreezingPelter and QueryEquipment(..., CallItems:false) when removing it; WolfConquer sends the local QueryEquipment-equivalent equipment refresh for the add path and suppresses the appearance packet on removal. AttachStatus.cs case 30000 sets AzureShieldLevel from ClientSpell.Level, AzureShieldDefence to 50000, applies AzureShield to self or visible players for 15 seconds per learned level with RemoveOnDead, runs duration-backed spell progress, and sends the animation even when no target is applied. " +
        "Matching decompiled AttachStatus.cs case 16390 applies BonePulse flag 0x11F for exactly 10 seconds with RemoveOnDead true and stores DamageOnHuman as BonePulse power. StatusFlagsBigVector32.Flag.Expire uses Now >= Timer + Seconds, GetClear removes RemoveOnDead flags, and ThreadPool.cs lines 2956-3038 remove every expired flag; MudWall flag 0x112/274 additionally calls QueryEquipment while HawksAmbition flag 0x100/256 and BonePulse flag 0x11F/287 use the generic removal path. WolfConquer owner WorldServerFrameHandler.LegacyCombatGearAttachStatus.cs expires HawksAmbition, BonePulse, and MudWall together, cleans timer aliases, sends one existing exact-D13 opcode 2176 status-vector fanout, and runs the existing QueryEquipment-equivalent equipment/spawn refresh only when MudWall is removed; LegacyPlayerDeathState clears their transient timer metadata on death. Exact D13 owns all packet layouts and field ordering; 7770 is excluded from this lifecycle slice. " +
        "AttachStatus.cs case 10405 prepares resources first; on map 1038 during live NEWGUILDWAR it applies the same/allied Exorcim-owner gate, and the visible player target must be dead. Existing SoulShackle flag 0x6F or SacredBlessing flag 0xFF still gives original-duration-backed progress and an empty animation. A target-minus-caster BattlePower gap greater than or equal to 20 sends the exact message 'Your BattlePower is lower than your opponent get a better BattlePower to be able to use this skill on him.' and then gives no progress or animation. Equipped Sturdiness avoidance uses rate 5 + level * 5, and FreeSoul avoidance follows the same empty-animation/no-progress result while additionally sending opcode 2319 effect 0x0E. Evocation subtracts byte(20 + level * 5) from the uint duration with the source's unchecked wraparound semantics. Success sends opcode 2176 MsgUpdate type 0x36 with flag 0x6F, effective Time, Damage 0, and learned level; applies timed RemoveOnDead SoulShackle; uses the original duration for progress; and sends opcode 2239 animation with target damage 0.";

    private const string SpecialXpSkillActivationEvidence =
        "AttachStatus.cs case 6011 requires XPList, then RemoveFlag(MsgUpdate.Flags.XPList), OpenXpSkill(MsgUpdate.Flags.FatalStrike, 60), and a self-target MsgSpellAnimation; BladeFlurry.cs RemoveFlag(MsgUpdate.Flags.XPList) then OpenXpSkill(MsgUpdate.Flags.BladeFlurry, 50); ChainBolt.cs RemoveFlag(MsgUpdate.Flags.XPList) then OpenXpSkill(MsgUpdate.Flags.ChaintBolt, 50); DragonCyclone.cs ContainFlag(MsgUpdate.Flags.XPList), RemoveFlag(MsgUpdate.Flags.XPList), OpenXpSkill(MsgUpdate.Flags.DragonCyclone, (int)DBSpell.Duration), and a self-target MsgSpellAnimation; Omnipotence.cs ContainFlag(MsgUpdate.Flags.XPList), Stamina = MaxStamina, RemoveFlag(MsgUpdate.Flags.XPList), and OpenXpSkill(MsgUpdate.Flags.Omnipotence, 40, 0).";

    private const string BreathFocusEvidence =
        "data/DBServer\\magictype.txt rows 11960-11969 define BreathFocus as sort 70 with damage values used as stamina restore amounts; decompiled MsgAttackPacket.ProcescMagic can route Trojan fallback casts to AttackHandler\\BreathFocus.cs; BreathFocus.cs calls CanUseSpell.Verified, creates MsgSpellAnimation from caster UID at caster X/Y for spell 11960 with ClientSpell.Level and ClientSpell.UseSpellSoul, restores stamina by DBSpell.Damage only when current stamina is below 100 and clamps to 100 before SendUpdate Stamina, runs UpdateSpell.CheckUpdate(..., 1000), then sends the animation.";

    private const string CombatGearSelfStatusEvidence =
        "references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer.AttackHandler\\CombatGearAttack.cs cases 20300 and 20350 are intercepted by MsgAttackPacket.ProcescMagic through CombatGear.CheckSpells before generic MagicSort handling; both cases apply a self status flag, send ArchiveSkill with Spell.Duration and Spell.DamageOnHuman, send a self-target MsgSpellAnimation, then call QueryEquipment. CombatGearAttack.cs case 20370 applies Oblivion|HawksAmbition status flag 0x170, stores WildPhoenix packed damage as DamageOnHuman*1000+Damage2 with GDamage charges, sends ArchiveSkill with Time=Spell.Duration, Damage=packedDamage, SpellLevel=GDamage, sends a self-target zero-damage None animation, then calls QueryEquipment. references/DecompiledSource\\GameServer\\MrRayzo\\MyCombatGear.cs WildPhoenix() decrements the charge, refreshes the ArchiveSkill, and can reduce incoming player-target damage to 1; AttackEffect(...) applies the packed WildPhoenix damage bonus for Taoist5 spell ids 1165, 1001, and 1002 against players, monsters, and SOB NPCs. references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer.AttackHandler\\Line.cs case 11005 calls Physical.OnPlayer, then Base.CalculateSoul, then divides player damage by 2 before ReceiveAttack.Player.Execute. references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer.AttackHandler\\MoveLine.cs player branch calls Physical.OnPlayer, then Base.CalculateSoul, then scales damage by *12/16 before ReceiveAttack.Player.Execute. references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer.AttackHandler\\CannonBarrage.cs player branch calls Physical.OnPlayer, then Base.CalculateSoul, then divides player damage by 11 before ReceiveAttack.Player.Execute. references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer.AttackHandler\\ScurvyBomb.cs player branch applies fatigue/status, calls Physical.OnPlayer, then Base.CalculateSoul, then doubles player damage before ReceiveAttack.Player.Execute. references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer.AttackHandler\\PirateXpSkill.cs lines 46-54 call Physical.OnPlayer, then Base.CalculateSoul, then doubles player damage before ReceiveAttack.Player.Execute. references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer.AttackHandler\\BombPirte.cs lines 118-120 call Physical.OnPlayer before dividing player damage by 2. references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer.AttackHandler\\HellVortex.cs player branch calls Physical.OnPlayer, then Base.CalculateSoul, then scales damage by *12/28 before ReceiveAttack.Player.Execute; Physical.cs applies target.Owner.CombatGear.WildPhoenix(). WolfConquer models the initial self-status packet path plus bounded generic magic player-target defender charge/archive refresh and qualifying attacker damage bonus for player, monster, and SOB magic paths; remaining strict debt is the wider Physical/Range calculator WildPhoenix call-site coverage.";

    private const string NebulousHuntSectorBackPhysicalEvidence =
        "references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer.AttackHandler\\SectorBack.cs case 10990/18240 shares the line-width-2 branch; the player branch calls Physical.OnPlayer, then Base.CalculateSoul, then divides player damage by 7 before ReceiveAttack.Player.Execute, sets Hit=0, and queues the player target for animation. data/DBServer\\magictype.txt rows 18240-18246 define type 18240 as sort 69/SectorBack CrescentShadow level rows. references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer.AttackHandler\\Physical.cs applies target.Owner.CombatGear.WildPhoenix().";

    private const string CleanSweepSectorBackEvidence =
        "references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer.AttackHandler\\SectorBack.cs case 15870 handles CleanSweep sort-69/SectorBack after CanUseSpell.Verified and the 500ms StampSecorSpells gate; it builds a MsgSpellAnimation at Attack.X/Y plus an InLineAlgorithm from caster to Attack.X/Y with Spell.Range and line width 2, hits visible monsters, players, and SOB NPCs through CanAttackMonster/CanAttackPlayer/CanAttackNpc, scales monster Physical.OnMonster plus Base.CalculateSoul damage by /2 before Monster.Execute, scales player Physical.OnPlayer plus Base.CalculateSoul damage by *5/12 before ReceiveAttack.Player.Execute then sets Hit=0 for animation, applies SOB Physical.OnNpcs plus Base.CalculateSoul full runtime damage through Npc.Execute then changes animation damage to *50/100, runs IncreaseExperience.Up and UpdateSpell.CheckUpdate, sends the animation, stores user.Player.CleanSweepPower = Spell.Damage3, sends MsgUpdate.Flags.CleanSweep ArchiveSkill with Time=Spell.Duration, Damage=Spell.Damage3, and SpellLevel=Spell.Damage3, then AddSpellFlag(CleanSweep, Spell.Duration, RemoveOnDead). references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer.AttackHandler\\Physical.cs applies target.Owner.CombatGear.WildPhoenix(). data/DBServer\\magictype.txt rows 15870-15879 and 15993 define CleanSweep as sort 69/SectorBack with flag 254, duration 35, range 5, and Damage3 power rows. references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer\\MsgUpdate.cs defines CleanSweep = 0xFE.";

    private const string ModeledWildPhoenixPhysicalRangeCallSites =
        "generic magic, PhysicalSpells, KineticSpark, SpeedGun, DragonWhirl, HookMoon, ViperFang, MoveLine, CannonBarrage, ScurvyBomb, PirateXpSkill 11060, BombPirte 11070, DragonTail, DragonSlash, ThunderStrikerLine, Shock, FatalCross, RageOfWar, AttackStatus default, AttackStatus 7000, AttackStatus 15880, BlackSpot 11120, HellVortex 16370, SectorBack 10990/18240, and CleanSweep/SectorBack 15870";

    private const string WildPhoenixCombatGearReason =
        "WolfConquer models the initial WildPhoenix CombatGearAttack self-status packet path with resources, cooldowns, Oblivion|HawksAmbition status vector updates, ArchiveSkill packed DamageOnHuman*1000+Damage2 and GDamage charge level, self spell animation, and QueryEquipment-equivalent equipment refresh. " +
        "It also models bounded generic magic player-target defender charge/archive refresh with damage override 1, the PhysicalSpells player-target defender charge/archive hook before the source /2 scaling step with zero-damage target preservation plus the source ShurikenVortex/ManiacDance target-status early return that preempts WildPhoenix, the KineticSpark player Physical/Range defender charge/archive hook, the SpeedGun 1260 player Physical.OnPlayer WildPhoenix defender hook, the DragonWhirl 10315 player Physical.OnPlayer WildPhoenix defender hook, the HookMoon 15930 player Physical.OnPlayer WildPhoenix defender hook, the ViperFang 11005 player Physical.OnPlayer WildPhoenix defender hook, the MoveLine 11110 player Physical.OnPlayer WildPhoenix defender hook, the CannonBarrage 11050 player Physical.OnPlayer WildPhoenix defender hook before /11 scaling, the ScurvyBomb 11040 player Physical.OnPlayer WildPhoenix defender hook, the PirateXpSkill/BlackbeardsRage 11060 player Physical.OnPlayer WildPhoenix defender hook before *2 scaling, the BombPirte 11070 player Physical.OnPlayer WildPhoenix defender hook before /2 scaling, the DragonTail 11000 player Physical.OnPlayer WildPhoenix defender hook, the DragonSlash 12350 player Physical.OnPlayer WildPhoenix defender hook, the ThunderStrikerLine 15810 player Physical.OnPlayer WildPhoenix defender hook, the Shock 15720 player Physical.OnPlayer WildPhoenix defender hook, the FatalCross 11980 player Physical.OnPlayer WildPhoenix defender hook, the RageOfWar 12930 initial/floor player Physical.OnPlayer WildPhoenix defender hook, the AttackStatus default player Physical.OnPlayer WildPhoenix defender hook, the AttackStatus 7000 player Range.OnPlayer WildPhoenix defender hook, the AttackStatus 15880 player Physical.OnPlayer WildPhoenix defender hook, the BlackSpot 11120 direct player Physical.OnPlayer WildPhoenix defender hook, the HellVortex 16370 player counter Physical.OnPlayer WildPhoenix defender hook, the SectorBack 10990/18240 player Physical.OnPlayer WildPhoenix defender hook before /7 scaling, the CleanSweep/SectorBack 15870 player Physical.OnPlayer WildPhoenix defender hook before *5/12 scaling, and the qualifying Taoist5 AttackEffect damage bonus for spell ids 1165, 1001, and 1002 against player, monster, and SOB magic paths. " +
        "Remaining WildPhoenix parity is the wider Physical/Range calculator call-site coverage outside " + ModeledWildPhoenixPhysicalRangeCallSites + ".";

    private const string WildPhoenixCombatGearNextOwner =
        "CombatGear runtime owner: implement remaining WildPhoenix Physical/Range calculator call-site coverage outside " + ModeledWildPhoenixPhysicalRangeCallSites + " without broadening the already-modeled claims.";

    private const string PhysicalSpellsEvidence =
        "data/DBServer\\magictype.txt rows 10490-10494 TripleAttack, 11140-11144 Windstorm, 13290 Windstorm, and 13610 TripleAttack use sort 55; decompiled MsgAttackPacket.ProcescMagic case MagicSort.PhysicalSpells dispatches PhysicalSpells.Execute; PhysicalSpells.cs calls CanUseSpell.Verified, applies three physical hits to one Monster, Player, or SobNpc target, and chains spell 11140 into LayTrap.Execute for learned 14720 BloomofDeath. Each player hit calls ReceiveAttack.Player.Execute after Physical.OnPlayer, Base.CalculateSoul, and the caller /2; the receive handler opens with attacker CombatGear.Update, defender CombatGear.UpdateTarget, attacker speffect group 2, defender speffect groups 3/4/5, and Ninja SageMode using gouyu Chance / 100. Decompiled Physical.cs returns monster damage 2 immediately when MonsterRole.IsFloor is true, before one-damage monster rules, gear/status math, guard division, and non-lethal clamping; GameMap.AddFloor marks those floor monsters/effects with IsFloor, 25 max HP, and 25 current HP. Decompiled Physical.cs returns raw player damage 1 when the target has ShurikenVortex or ManiacDance before the later WildPhoenix branch, then PhysicalSpells.cs applies Base.CalculateSoul and divides player damage by 2. Decompiled Line.cs contains a case 10490 branch, but magictype.txt routes the live 10490 row through PhysicalSpells(55).";

    private const string AttackStatusDefaultEvidence =
        "data/DBServer\\magictype.txt rows 5030-5039 Phoenix use sort 16 and exact row 15880 defines SongofPhoenix as sort 16 with range 5; references/DecompiledSource\\GameServer\\MrRayzo.Database\\MagicType.cs defines AttackStatus = 16. Decompiled AttackStatus.cs default branch handles non-15560 direct monster/SOB/player physical damage through Physical.OnMonster, Physical.OnNpcs, and Physical.OnPlayer, divides player damage /4 after Base.CalculateSoul, calls ReceiveAttack.Player.Execute for an accepted player, then runs IncreaseExperience.Up, UpdateSpell.CheckUpdate, and MsgSpellAnimation. ReceiveAttack.Player.Execute opens with attacker CombatGear.Update, defender CombatGear.UpdateTarget, attacker speffect group 2, defender speffect groups 3/4/5, and Ninja SageMode using gouyu Chance / 100. AttackStatus.cs also has source-backed special cases 7000, 10800, 15560, 15880, and 20000; case 7000 selects caster-centered monster, player, and SOB targets at strict distance < Spell.Range, calls Range.OnPlayer for accepted players, applies identity Base.CalculateSoul, executes ReceiveAttack.Player, then runs IncreaseExperience.Up, UpdateSpell.CheckUpdate, and one shared animation. Matching Range.cs returns ShurikenVortex/ManiacDance damage from Base.CalculateExtraAttack(0, PhysicalDamageIncrease, PhysicalDamageDecrease) before WildPhoenix's fixed one, applies DefensiveStance's 40 percent Block return before ranged BackDmg, and reaches BackDmg only on the normal path. Matching BackDmg.cs evaluates learned-defender Heaven's Wonder 16350 first, then RevengeTail 13030; ranged=true suppresses only the later 14470 branch, while Backfire and CounterKill/Reflect remain later branches. Matching decompiled case 15880 is a caster-centered strict-distance < Spell.Range attack over visible monsters, players, and SOB NPCs in that order: monsters use Range.OnMonster with full runtime damage and 60% animation damage, players use Physical.OnPlayer plus Base.CalculateSoul then * 7 / 13 runtime and animation damage before ReceiveAttack.Player.Execute, and SOB NPCs use Range.OnNpcs plus Base.CalculateSoul then * 50 / 100 runtime and animation damage. It accumulates monster/SOB progress, runs IncreaseExperience.Up and UpdateSpell.CheckUpdate, sends MsgSpellAnimation, and returns. The later SongofPhoenix flag/power block is unreachable for case 15880 because that special branch already returned. Matching decompiled Physical.cs explicitly selects DamageOnHuman % 1000 for spell 15880. Exact D13 client executable SHA-256 D13B45BD2681DB7D3D8548EA575D89E0D05613BA00E8F9763DBF3F89030402A owns packet opcodes and layouts; the matching service-pack decompile supplies gameplay behavior and ordering, and Source7770 is excluded.";

    private const string RangeOnPlayerStatusReturnEvidence =
        "Matching Range.OnPlayer evaluates Base.CalculateExtraAttack(0, PhysicalDamageIncrease, PhysicalDamageDecrease) " +
        "from zero using attacker PhysicalDamageIncrease and defender PhysicalDamageDecrease; WildPhoenix's fixed one-damage return remains separate.";

    private const string FatalCrossEvidence =
        "references/DecompiledSource\\GameServer\\MrRayzo.Database\\MagicType.cs defines FatalCross = 71; decompiled MsgAttackPacket.ProcescMagic dispatches sort 71 to FatalCross.Execute; FatalCross.cs calls CanUseSpell.Verified first, accepts spell 11980 only when the cast point is within distance <= 13, restores SuperCyclone stamina by 20 while below the 100/150 HeavenBlessing threshold, then hits visible monsters, players, and SOB NPCs in an InLineAlgorithm width-2/range-13 line. Monsters use Physical.OnMonster damage multiplied by 3; players call Physical.OnPlayer, Base.CalculateSoul, then scale damage by 22/28; SOB NPCs apply Physical.OnNpcs damage to runtime state and triple the animation damage afterward.";

    private const string DragonWhirlEvidence =
        "data/DBServer\\magictype.txt spell 10315 uses sort 40 DragonWhirl; decompiled MsgAttackPacket.ProcescMagic dispatches sort 40 to DragonWhirl.Execute. DragonWhirl.cs calls CanUseSpell.Verified first, accepts spell 10315 only, runs CheckGuildWar on the cast tile after resources, returns after resources when the cast point distance is >= Spell.Range, sends a zero-damage MsgAttack marker with AtkType=53 and KilledMonster=10315, hits visible monsters, players, and SOB NPCs in an InLineAlgorithm width-4 line whose range is the cast distance, applies Physical.OnMonster/OnPlayer/OnNpcs damage with shared CanAttack gates, sends MsgSpellAnimation, applies experience/spell progress, moves/repositions the caster to the source line final coords, then calls Player.View.Role(clear:false, InteractionCreate), which refreshes visible player spawns to the caster and the caster spawn to visible peers.";

    private const string ChargingVortexEvidence =
        "references/DecompiledSource\\GameServer\\MrRayzo.Database\\MagicType.cs line 115 defines ChargingVortex = 64. " +
        "references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer\\MsgAttackPacket.cs lines 1014-1015 dispatch MagicSort.ChargingVortex to ChargingVortex.Execute. " +
        "data/DBServer\\magictype.txt row 1155 defines spell 11190 ChargingVortex as sort 64, range 20, and stamina cost 20. " +
        "references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer.AttackHandler\\ChargingVortex.cs calls CanUseSpell.Verified first, accepts spell 11190 only, runs CheckGuildWar on the cast tile after resources, returns after resources when the cast point distance is >= Spell.Range, rewrites the interaction to AtkType=53 and KilledMonster=11190, hits visible monsters, players, and SOB NPCs in an InLineAlgorithm width-4 line whose range is the cast distance, applies Physical.OnMonster/OnPlayer/OnNpcs with shared CanAttack gates, sends experience/spell progress and MsgSpellAnimation, then moves the caster to the line endpoint and refreshes the source view-role state.";

    private const string ShockEvidence =
        "data/DBServer\\magictype.txt spell 15720 uses sort 34 CrackingShock; decompiled Shock.cs calls CanUseSpell.Verified first, accepts spell 15720 only for ThunderStriker classes, scans visible monsters, players, and SOB NPCs within distance <= 6 of Attack.X/Y, applies Physical.OnMonster damage through Base.CalculateSoul then *2, applies Physical.OnPlayer damage through Base.CalculateSoul then *75/100, applies Physical.OnNpcs damage through Base.CalculateSoul, mutates runtime target HP through ReceiveAttack handlers, queues MsgSpellAnimation targets, then runs IncreaseExperience.Up and UpdateSpell.CheckUpdate using the accumulated monster/SOB damage.";

    private const string GunfireStrikeEvidence =
        "references/DecompiledSource\\GameServer\\MrRayzo.Database\\MagicType.cs line 128 defines Strike = 81. " +
        "references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer\\MsgAttackPacket.cs lines 990-992 dispatch MagicSort.Strike to Strike.Execute. " +
        "references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer.AttackHandler\\Strike.cs case 14590 runs after CanUseSpell.Verified, resolves Attack.OpponentUID as one visible monster, player, or SOB NPC, applies CanAttackMonster/CanAttackPlayer/CanAttackNpc gates, applies Physical.OnMonster/OnPlayer/OnNpcs plus Base.CalculateSoul, scales player damage by *2/3, mutates runtime state through ReceiveAttack handlers, accumulates monster/SOB damage for IncreaseExperience.Up and UpdateSpell.CheckUpdate, and sends MsgSpellAnimation even when no target is selected; Strike.cs case 13050 is not claimed by this slice. " +
        "data/DBServer\\magictype.txt row 1991 defines spell 14590 Gunfire as sort 81/Strike. " +
        "WolfConquer WorldPacketHandlers.PlayerMagic.Strike.cs handles direct-target spell IDs 12600 and 14590 with sort 81.";

    private const string Strike12600Evidence =
        "references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer.AttackHandler\\Strike.cs case 12600 runs after CanUseSpell.Verified, resolves one visible monster/player/SOB target, applies shared CanAttack gates and physical plus Base.CalculateSoul damage, then scales monster damage *2, player damage *2/3, and SOB damage *4, accumulates only monster/SOB return damage for IncreaseExperience.Up and UpdateSpell.CheckUpdate, and always sends MsgSpellAnimation with attacker and attacked UID both equal to the caster; the matching magictype.txt rows use sort 81 Strike. WolfConquer local evidence is WorldPacketHandlers.PlayerMagic.Strike.cs and LegacyStrike12600RuntimeTests.cs.";

    private const string Strike13050Evidence =
        "data/DBServer\\magictype.txt row 1659 defines spell 13050 as sort 81 Strike with range 5; " +
        "references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer.AttackHandler\\Strike.cs case 13050 runs after CanUseSpell.Verified, reanchors Attack.X/Y to a clicked visible player or monster, scans visible monsters, players, and SOB NPCs within distance <= 5 of the caster, applies shared CanAttack gates and physical plus Base.CalculateSoul damage without extra target-type scaling, sends one AtkType 2 interaction to the caster for each accepted target when OnAutoAttack is false, accumulates only monster/SOB return damage for IncreaseExperience.Up and UpdateSpell.CheckUpdate, sends no animation when no target is accepted, and otherwise sends a caster animation with attacked UID 0. WolfConquer local evidence is WorldPacketHandlers.PlayerMagic.Strike13050.cs and LegacyStrike13050RuntimeTests.cs.";

    private const string ToxicEvidence =
        "data/DBServer\\magictype.txt spell 6001 resolves ToxicFog sort 27 rows; decompiled Toxic.cs handles ClientSpell.ID 6001 after CanUseSpell.Verified, poisons non-boss visible monsters and visible players within distance < 5 from Attack.X/Y, sets PoisonLevel = level + soul, applies AddSpellFlag(Poisoned, Spell.Duration, RemoveOnDead) with type 1 for monsters and type 2 for players, skips players with ToxinEraser/Sturdiness avoidance, calls UpdateSpell.CheckUpdate with damage=1000 when any target was poisoned, and sends MsgSpellAnimation without damage animation targets.";

    private const string AxeShadowCelestialDanceStatusEvidence =
        "references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer.AttackHandler\\Wildwind.cs case 15940 sends MsgSpellAnimation, targets visible monsters/players/SOB NPCs through a width-2 InLineAlgorithm, applies Physical.OnMonster/OnPlayer/OnNpcs plus Base.CalculateSoul, scales player damage by 7/23, halves monster/SOB animation damage, sets player BleedDamage=3000, applies MsgUpdate.Flags.AxeShadow for Spell.Duration with RemoveOnDead, sends ArchiveSkill with Time=Spell.Duration and zero damage/level, and stores AxeShadowPower = Spell.Damage3. " +
        "references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer.AttackHandler\\Bomb.cs case 7030 sends MsgSpellAnimation, targets visible monsters/players/SOB NPCs from the caster-centered Spell.Range, applies Physical.OnMonster/OnPlayer/OnNpcs plus Base.CalculateSoul, scales monster/SOB damage by /3 and player damage by 12/27, sends MsgUpdate.Flags.CelestialDance with DataType.CelestialDance (0x77) and Time=Spell.Duration, applies the CelestialDance flag for Spell.Duration with RemoveOnDead, and stores CelestialDancePower = Spell.DamageOnHuman % 1000. " +
        "references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer\\MsgUpdate.cs identifies CelestialDance = 0xFA, AxeShadow = 0xFB, and DataType.CelestialDance = 0x77.";

    private const string KineticSparkDirectToggleAndPassiveProcEvidence =
        "data/DBServer\\magictype.txt rows 1204-1208 define spell 11590 KineticSpark as sort 67; " +
        "references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer.AttackHandler\\KineticSpark.cs Execute case 11590 toggles MsgUpdate.Flags.KineticSpark (0x93), adds it for 2592000 seconds with RemoveOnDead when absent, removes it when present, and sends a self-target MsgSpellAnimation. " +
        "references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer.AttackHandler\\KineticSpark.cs AttackSpell case 11590 monster branch resolves Attack.OpponentUID as the primary visible Monster, anchors animation X/Y to that monster, applies CanAttackMonster.Verified, Range.OnMonster, Base.CalculateSoul, Monster.Execute, then chains to visible monsters within Spell.Range / 2 using the StarRaid-adjusted target budget and scaled chain damage; ReceiveAttack.Monster.Execute applies the Ninja SageMode branch using gouyu_immortals Rate / 100 and SageMode seconds before IncreaseExperience.Up, UpdateSpell.CheckUpdate, and animation send only when targets exist; WolfConquer models the bounded monster receive-side Ninja SageMode branch using gouyu_immortal Rate. " +
        "KineticSpark.cs lines 182-217 resolve Attack.OpponentUID as the primary visible Player, anchor animation X/Y to that player, apply CanAttackPlayer.Verified, Physical.OnPlayer plus Base.CalculateSoul for the primary hit, then chain visible players within strict Spell.Range / 2 using Range.OnPlayer, Base.CalculateSoul, StarRaid-adjusted target budget, and scaled chain damage; Range.OnPlayer checks ShurikenVortex/ManiacDance before WildPhoenix, returns from DefensiveStance after a 40 percent Block reduction, and invokes ranged BackDmg for normal hits; ReceiveAttack.Player.Execute runs for every accepted hit before the one shared animation, which sends only when targets exist. ReceiveAttack.Player.Execute lines 17-18 plus MyCombatGear.Update lines 995-998 produce two source-shaped defender UpdateTarget attempts for player hits, MyCombatGear.Update lines around 1000-1037 increment StoneCracker ArrowBlades and StarFlow, apply Water|Top4Weekly, SageSpeedMode|Top4Weekly, and Lightning|Top4Weekly status counters, fire ProcescMagic 10950 on the third StarFlow step, reset StarFlow, then route the ArchShadow 18980 branch through ProcescMagic(18980); MsgUpdate.cs flags identify Water as 0x10D, Lightning as 0x110, SageSpeedMode as 0x111, and Top4Weekly as 0x40; CombatGearAttack.cs case 10950 runs the StarFlow AoE at Attack.X/Y over visible monsters, players, and SOBs within distance <= 2.5; CombatGearAttack case 18980 applies Dead|Top4Weekly|HawksAmbition plus Archshadow GDamage to the attacked player; MyCombatGear.Update lines 1039-1046 route Dragonhowl 17620 through ProcescMagic(17620, user.Player.UID, user.Player.UID, Target.X, Target.Y) and Bloodlust 17600/17610 through ProcescMagic(17600, user.Player.UID, user.Player.UID, Target.X, Target.Y) then ProcescMagic(17610, user.Player.UID, user.Player.UID, Target.X, Target.Y); CombatGearAttack.cs cases 17600 and 17610 add Immersion/Insouciance, set ImmersionPower/InsouciancePower, send ArchiveSkill updates, and send self-target animations, while case 17620 adds DragonPierceCrit plus bounded DragonPierceTortoise/DragonPierceBreak/DragonPierceStamina flags/powers/ArchiveSkill updates and a self-target animation; MsgUpdate.cs flags identify Immersion as 0x12F, Insouciance as 0x130, DragonPierceCrit as 0x133, DragonPierceTortoise as 0x134, DragonPierceBreak as 0x135, and DragonPierceStamina as 0x136; MyCombatGear.cs lines after Bloodlust route Taoist3 through ProcescMagic(20380, user.Player.UID, Target.UID, Target.X, Target.Y) and Taoist1 through ProcescMagic(20330, user.Player.UID, Target.UID, Target.X, Target.Y), then Pirate1/Pirate2/Pirate3 through ProcescMagic(21710), ProcescMagic(21720), and ProcescMagic(21930); data/DBServer\\magictype.txt rows 21710/21720/21930 define Armed status 413, Sense status 412, and Dark target buff removal; CombatGearAttack.cs cases 21710 and 21720 apply Pirate receive-side status branches with self animations, while case 21930 sends the interaction/animation for all calls, strips visible-player target buffs/enqueues a target only on player-view hits, and for SOB player-view misses sends a targetless animation; CombatGearAttack.cs case 20380 handles the defender CrackMantra branch with TopPirate|HawksAmbition or Freeze|Top4Weekly|HawksAmbition, 5-second ArchiveSkill, and a self animation targeting the defender; CombatGearAttack.cs case 20330 handles the attacker DeadwoodCurse branch with DefensiveStance|HawksAmbition, duration ArchiveSkill, and a self animation; MsgUpdate.cs flags identify TopPirate as 0x7A, Freeze as 0x3B, Top4Weekly as 0x40, DefensiveStance as 0x7E, HawksAmbition as 0x100, AttackUp as 0xE9, Absolution as 0xD8, Duel as 0xE1, BloodTide as 0xE5, and FineRain update type 0x66; speffect_attribute.Get(attacked.Owner, 3/4/5) applies defender Solid/Bloodthirst/Elan self-effects, ReceiveAttack.Player.cs calls speffect_attribute.Get(client, 2u, attacked.UID), speffect_attribute.cs case 2 routes attacker Venom through spell 20000 via MsgAttackPacket.ProcescMagic(..., ignoreStamp: true) after VenomLevel and GetVenom(level + 1) rate pass, ReceiveAttack.Player.Execute lines 94-96 set mutual ThundercloudSight for player hits, ReceiveAttack.Player.Execute lines 97-117 apply HookMoonCaster multiply, CleanSweep additive percent, AxeShadow additive percent, target HawksAmbition reduction/removal, and attacker AttackUp doubling, ReceiveAttack.Player.Execute lines 118-224 activate/drain LightningShield/AzureShield using target spell 15750, source 20% roll, ThunderRampage/FlashShield shield-left math, status flag 0xED, and source drain-before-HP mutation, and ReceiveAttack.Player.Execute lines 226-234 clear target Duel/XPCount/XP duration plus attacker/target Absolution flags, and ReceiveAttack.Player.Execute lines 235-261 activate Trojan BloodTide learned spell 15500 on the source 10% roll and 70-second cooldown, store target max HP as BloodTideHP capped to 250000 for high-HP attackers, add status flag 0xE5 for spell duration, send FineRain 229 with duration and BloodTideHP, send a self BloodTide animation, and query alternate equipment; ReceiveAttack.Player.Execute lines 263-408 apply attacker Caprice from equipped rune item level with a 15-second cooldown, level-based chance table, random branch 1 stamina transfer gated by target Consolidation counter-roll, and branch 2 XPList removal, ReceiveAttack.Player.Execute lines 410-427 apply defender ReverseMagic from equipped rune item level with source chance itemLevel*0.5 (level 9 = 5%), then reset attacker XPCount and remove the active XP skill flag, ReceiveAttack.Player.Execute lines 430-465 apply attacker Slow Pace from equipped rune item level with a level-based chance table and reduce target Dodge by 50%, ReceiveAttack.Player.Execute lines 470-554 apply XPBooster, XPKiller, TidalWave, and Quench player receive rune reactions using MsgUpdate XPDuration/TidalWave/Quench packets, and ReceiveAttack.Player.Execute lines 557-576 apply NeptuneCurse receive damage overrides, set client.Player.AttackHit = true, and run the target EagleEyeCountDown learned spell 11130 plus Magic row rate roll before sending a self-only animation, ReceiveAttack.Player.Execute lines 577-597 apply SkillTournament kill-system/death-check handling, decrement target SkillTournamentLifes with a center message and return while lives remain, or increment attacker TournamentKills, send accession1, set target HP 0, and call Dead when lives reach zero, and ReceiveAttack.Player.Execute lines 598-611 apply FreezeWar garment checks that add or remove Freeze flag 0x3B before returning, and ReceiveAttack.Player.Execute lines 613-624 apply FootBall pass-the-ball handling on map 3844 with lianhuaran04 flag 0xAB transferring to the attacker for 2592000 seconds before returning; WolfConquer models the bounded KineticSpark player receive InfiniteMist ArchiveSkill emit-order, bounded player receive-side Ninja SageMode branch using gouyu_immortal Rate, the bounded ThundercloudSight side effect, the bounded player-hit defender CombatGear UpdateTarget self/status passive subset with those two attempts, defender UpdateTarget MagneticLight 20220 and AblazeBlade 20360 player branches, the bounded attacker-side MyCombatGear.Update StoneCracker ArrowBlades/StarFlow 10950 counter/status/mixed-target HP branch including recursive 10950 player-hit CombatGear.Update and defender UpdateTarget ordering, the bounded attacker-side MyCombatGear.Update StoneCracker ArchShadow 18980 branch, the bounded receive-side CombatGear Dragonhowl/Bloodlust status/power/self-animation branches, the bounded receive-side CombatGear Taoist 20330/20380 status/power/self-animation branches, the bounded receive-side CombatGear Pirate 21710/21720/21930 status/buff-strip self-animation or target-animation branches, the bounded receive-side CombatGear Pirate black-jade random UseRune status branches for 21590/21730/21740/21750/21760/21770/21780/21790/21800/21810/21820, bounded defender speffect_attribute.Get types 3-5, the bounded attacker Venom speffect type 2 bridge through modeled AttackStatus spell 20000, and bounded HookMoonCaster/CleanSweep/AxeShadow/HawksAmbition/AttackUp/LightningShield/Duel/Absolution/BloodTide/Caprice/ReverseMagic/SlowPace/XPBooster/XPKiller/TidalWave/Quench/NeptuneCurse/AttackHit/EagleEye/SkillTournament/FreezeWar/Football/DragonWar/MirrorOfSin/LightOfStamina/CalmWind/DivineGuard/KillingFlash/DrainingTouch/BloodSpawn player receive modifiers on the shared player-player magic HP-mutation path while leaving remaining Ninja/Sage branches, DragonWar playtest/economy polish, deeper rune/perfection counters, PK/tournament bookkeeping, event/SOB branches, and exact CanAttack side effects as shared runtime debt. " +
        "KineticSpark.cs lines 218-253 resolve Attack.OpponentUID as the primary visible SobNpc, apply CanAttackNpc.Verified, Range.OnNpcs plus Base.CalculateSoul for the primary hit, then chain visible SobNpc targets within Spell.Range / 2 using Magic.OnNpcs, Base.CalculateSoul, StarRaid-adjusted target budget, scaled chain damage, Npc.Execute, accumulated SOB experience/progress damage, and animation send only when targets exist; ReceiveAttack.Npc.Execute lines 22-34 run CombatGear.Update and Ninja/Sage checks, and current ReceiveAttack.Npc.cs lines 213-224 apply the Ninja SageMode branch using gouyu_immortals Rate / 100, then lines 33-34 set client.Player.ThundercloudSight = attacked.UID and client.Player.AttackHit = true for SOB hits, then applies HookMoonCaster, CleanSweep, AxeShadow, and CelestialDance damage modifiers before HP mutation; MsgUpdate.cs flags identify CelestialDance as 0xFA and AxeShadow as 0xFB, Wildwind.cs case 15940 sets AxeShadowPower from Spell.Damage3, and Bomb.cs case 7030 sets CelestialDancePower from Spell.DamageOnHuman % 1000; WolfConquer models the bounded SOB ThundercloudSight and AttackHit side effects, bounded SOB receive-side CombatGear StoneCracker ArrowBlades/StarFlow 10950 counter/status branch, bounded SOB receive-side CombatGear Dragonhowl 17620, Bloodlust 17600/17610, Taoist DeadwoodCurse 20330, Taoist3 CrackMantra 20380 SOB player-view-miss no-op, player-target-only defender UpdateTarget skip for SOB hits, Pirate Armed/Sense 21710/21720 attacker self/status branches, Pirate Dark 21930 SOB no-op interaction/targetless-animation, Pirate black-jade random UseRune status branches for 21590/21730/21740/21750/21760/21770/21780/21790/21800/21810/21820, and the bounded SOB receive-side Ninja SageMode branch using gouyu_immortal Rate, plus bounded HookMoonCaster, CleanSweep, AxeShadow, and CelestialDance SOB receive damage modifiers for KineticSpark SOB hits and the shared generic player-SOB magic HP-mutation path, the bounded HookMoonCaster/CleanSweep/AxeShadow/HawksAmbition/AttackUp/LightningShield/Duel/Absolution/BloodTide/Caprice/ReverseMagic/SlowPace/XPBooster/XPKiller/TidalWave/Quench/NeptuneCurse/AttackHit/EagleEye/SkillTournament/FreezeWar/Football/DragonWar/MirrorOfSin/LightOfStamina/CalmWind/DivineGuard/KillingFlash/DrainingTouch/BloodSpawn player receive modifiers for shared player-player magic, the 15940/7030 self-status setter/expiry path, and the bounded 15940 Wildwind / 7030 Bomb target-damage loops while leaving remaining Ninja/Sage branches outside the bounded player/SOB/monster SageMode paths, speffect_attribute.Get, DragonWar playtest/economy polish, event/SOB branches, and PK/tournament bookkeeping as shared runtime debt. " +
        "references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer\\MsgAttackPacket.cs lines 356-400, 436-480, and 518-562 show the AttackID.Archer player, monster, and SOB caller branches run the source direct-target Base.GetDistance <= 18 caller gate plus direct-target CanAttackMonster/CanAttackPlayer/CanAttackNpc gates, then MyCombatGear.CheckCombatGear, before the passive 18750/10990 hooks and the later Kinetic gate sequence of MsgUpdate.Flags.KineticSpark, Base.Rate(30), Pool.Magic[11590], and current Attack.SpellLevel invokes KineticSpark.AttackSpell.";

    private const string PlayerReceiveMirrorOfSinEvidence =
        "ReceiveAttack.Player.Execute lines 625-640 apply attacker PerfectionStatus.MirrorOfSin through Base.Rate, send MsgRefineEffect opcode 2319 with RefineEffects.MirrorOfSin 0x0A, Id=attacked.UID, dwParam=attacked.UID through View.SendView(..., me: true), optionally add XPList flag 0x04 for 20 seconds when OnXPSkill is Normal, XPList is absent, and Base.Rate(10) passes, send MsgUpdate.DataType.XPList 0x65 value 1, and continue normal damage without returning. ReceiveAttack.Player.Execute lines 641-656 apply attacker PerfectionStatus.LightOfStamina when target prestige is at least attacker prestige and Base.Rate passes, send MsgRefineEffect opcode 2319 with RefineEffects.LightOfStamina 7, Id=attacked.UID, dwParam=attacked.UID through View.SendView(..., me: true), restore attacker stamina to 100 when below 100, send MsgUpdate.DataType.Stamina 8, and continue normal damage without returning. ReceiveAttack.Player.Execute lines 657-668 apply attacker PerfectionStatus.CalmWind through Base.Rate, send MsgRefineEffect opcode 2319 with RefineEffects.CalmWind 4, Id=client.Player.UID, dwParam=client.Player.UID through View.SendView(..., me: true), and continue normal damage without returning. ReceiveAttack.Player.Execute lines 668-684 apply attacker PerfectionStatus.DivineGuard when target prestige is at least attacker prestige and Base.Rate passes, send MsgRefineEffect opcode 2319 with RefineEffects.DivineGuard 0x0B, Id=client.Player.UID, dwParam=client.Player.UID through View.SendView(..., me: true), optionally add MsgUpdate.Flags.DivineGuard 0xB1 for 5 seconds when Core.Rate(6) passes, send MsgUpdate.DataType.AzureShield 0x31 with Time 15, and continue normal damage without returning. ReceiveAttack.Player.Execute lines 684-699 apply attacker PerfectionStatus.KillingFlash through Base.Rate, send MsgRefineEffect opcode 2319 with RefineEffects.KillingFlash 9, Id=client.Player.UID, dwParam=client.Player.UID through View.SendView(..., me: true), optionally add XPList flag 0x04 for 20 seconds when OnXPSkill is Normal, XPList is absent, and Core.Rate(10) passes, send MsgUpdate.DataType.XPList 0x65 value 1, and continue normal damage without returning. ReceiveAttack.Player.Execute lines 700-727 apply attacker PerfectionStatus.DrainingTouch through Base.Rate, send MsgRefineEffect opcode 2319 with RefineEffects.DrainingTouch 5, Id=client.Player.UID, dwParam=client.Player.UID through View.SendView(..., me: true), restore attacker HitPoints to Owner.Status.MaxHitpoints and Mana to Owner.Status.MaxMana when below max, send SendUpdateHP and MsgUpdate.DataType.Mana 2 when either resource changed, and continue normal damage without returning. ReceiveAttack.Player.Execute lines 727-753 apply defender PerfectionStatus.BloodSpawn through Base.Rate, send MsgRefineEffect opcode 2319 with RefineEffects.BloodSpawn 6, Id=attacked.UID, dwParam=attacked.UID through View.SendView(..., me: true), restore defender HitPoints to Owner.Status.MaxHitpoints and Mana to Owner.Status.MaxMana when below max, send SendUpdateHP and MsgUpdate.DataType.Mana 2 when either resource changed, and continue normal damage without returning.";

    private const string PlayerReceiveDragonWarEvidence =
        "ReceiveAttack.Player.Execute lines 760-786 apply active DragonWar tournament receive handling before ordinary HP mutation: if the attacked player has MsgUpdate.Flags.DragonSwing 0xA0 and DragonWarHits is zero, the source calls KillSystem.CheckDead, kills the target, gives the attacker +20 DragonWarScore, adds DragonSwing for 2592000 seconds, sets attacker DragonWarHits to 5, and removes target DragonSwing; if the attacked player has DragonSwing with hits remaining, the attacker gains +5 DragonWarScore and target DragonWarHits decrements; if only the attacker has DragonSwing, the source calls KillSystem.Update, gives +15 DragonWarScore, and kills the target; otherwise active DragonWar still returns before ordinary damage. MsgUpdate.cs identifies Flags.DragonSwing as 0xA0 and DataType.DragonSwing as 0x4B. KillerSystem.cs Update/Send defines the receive-side kill-streak UID/dead/flag state, +10000 CP reward, sound/fc1..fc4, and same map/dynamic talk broadcast; WolfConquer separately models the global DragonWar join, revive, score-panel, tuned reward, transition, and lifecycle paths, leaving playtest/economy polish as the remaining DragonWar work.";

    private const string PlayerReceiveInfiniteMistEvidence =
        "ReceiveAttack.Player.Execute lines 34-90 apply defender learned InfiniteMist 16380 before normal receive handling: dead defenders return immediately, the defender's Magic.Rate roll gates the counter, attacker Sky Veil suppresses the counter with source hard-coded +1..+9 rates 20/30/40/50/60/70/80/90/100, and an unsuppressed counter calls ProcescMagic as the defender with ignoreStamp=true. AttachStatus.cs case 16380 requires the defender/caster to be Ninja, resolves the attacker as the visible player target, adds MsgUpdate.Flags.InfiniteMist 0x11A to the attacker for Spell.Duration with RemoveOnDead, and sends the odd source animation shape with defender UID as attacker, attacked, and zero-damage target.";

    private const string PlayerReceiveMudWallEvidence =
        "Matching service-pack DecompiledSource/GameServer/MrRayzo.Game.MsgServer.AttackHandler.Calculate/Physical.cs lines 979-986 check target MudWall flag 0x112 after BackDmg and call Ninja.TryGetValueEquip for equipped Ninja.ItemType.MudSigilImpeccable 301. When equipped, the matching source directly applies damage -= damage * (DBItem.Power / 100) / 100. Matching Ninja.TryGetValueEquip enforces Ninja class and selects the active primary/alternate equipment set. No learned 16440 chance, Damage2, or LegacyMudWallRoll reducer exists in the matching DecompiledSource. WolfConquer retains the direct player-magic MudSigilImpeccable reduction, first resolving equipped MsgNinja type 301 and gouyu_type Power from GouyuType.Values[5], with explicit legacy level/power attributes as compatibility fallbacks; those fallback attributes and the exact primary/chained KineticSpark caller order remain unaudited rather than matching-source-complete. The matching service-pack source is gameplay authority, the exact D13 client is wire authority, and Source7770 contributes nothing.";

    private const string ArcherPreKineticProcEvidence =
        "references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer\\MsgAttackPacket.cs AttackID.Archer lines around 356-388, 436-468, and 518-550 show the source direct-target Base.GetDistance <= 18 caller gate and direct-target CanAttack gate wrapping MyCombatGear.CheckCombatGear before the learned-spell plus Base.Rate(30) hooks for spell 18750 CrescentShadow and spell 10990 NebulousHunt, which stay before the later KineticSpark checks. data/DBServer\\magictype.txt line 1055 defines spell 10990 NebulousHunt as sort 69/SectorBack. data/DBServer\\magictype.txt lines 3166-3172 define spell 18750 CrescentShadow as sort 5/Bomb levels.";

    private const string CruelShadeEvidence =
        "data/DBServer\\magictype.txt rows 3050-3053 define spell 3050 CruelShade with sort 26 DecLife; references/DecompiledSource\\GameServer\\MrRayzo.Database\\MagicType.cs defines DecLife = 26; decompiled MsgAttackPacket.ProcescMagic dispatches MagicSort.DecLife to DecLife.Execute and excludes spell 3050 from the later Trojan BreathFocus 11960 fallback. DecLife.cs calls CanUseSpell.Verified first, handles ClientSpell.ID 3050, targets Attack.OpponentUID as one visible monster, player, or SOB NPC within distance < Spell.Range, applies CanAttackMonster/CanAttackPlayer/CanAttackNpc gates, gives boss monsters damage 1, applies HP-based DecLife damage to non-boss monsters, applies HP-based player DecLife damage and then overwrites it with ToxinEraser percentage scaling, applies SOB damage as Spell.Damage % 100 percent of SOB HP, mutates runtime state through ReceiveAttack handlers, then runs IncreaseExperience.Up, UpdateSpell.CheckUpdate, and MsgSpellAnimation. DecLife.cs also has a separate GapingWounds 11230 branch, so CruelShade evidence must not imply whole-sort DecLife parity.";

    private const string GapingWoundsEvidence =
        "data/DBServer\\magictype.txt rows 1171-1175 define GapingWounds level rows with type id 11230 and sort 26 DecLife; references/DecompiledSource\\GameServer\\MrRayzo.Database\\MagicType.cs defines DecLife = 26; decompiled MsgAttackPacket.ProcescMagic dispatches MagicSort.DecLife to DecLife.Execute. DecLife.cs lines 25-83 handle ClientSpell.ID 11230, targets Attack.OpponentUID as one visible monster, player, or SOB NPC within distance < Spell.Range, applies CanAttackMonster/CanAttackPlayer/CanAttackNpc gates, gives boss monsters damage 1, applies non-boss monster damage as HitPoints * (100 - Spell.Damage) / 100, requires !PerfectionEffects.ToxinEraser(player2) before the player branch, and PerfectionEffects.cs lines 43-59 makes that gate Target.Owner.PerfectionStatus.ToxinEraser > 0.0 && Base.Rate(Target.Owner.PerfectionStatus.ToxinEraser). The player branch applies player HP damage as HitPoints * (100 - Spell.Damage) / 100 and then overwrites it with HitPoints * (100 - Math.Min(99.0, player2.Owner.PerfectionStatus.ToxinEraser)) / 100; source PerfectionStatus initializes ToxinEraser to 0.0 before perfection-level upgrades. GapingWounds applies SOB damage as HitPoints * (100 - Spell.Damage) / 100, mutates runtime state through ReceiveAttack handlers, then runs IncreaseExperience.Up, UpdateSpell.CheckUpdate, and MsgSpellAnimation. This is the GapingWounds 11230 branch and stays separate from CruelShade 3050.";

    private const string AddManaEvidence =
        "references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer.AttackHandler\\AddMana.cs handles spell 1195 by restoring missing self mana, sending MsgSpellAnimation, and updating spell experience.";

    private const string BombPirteEvidence =
        "decompiled Flags.SpellID.GaleBomb = 11070; MsgAttackPacket.ProcescMagic case MagicSort.BombPirte dispatches BombPirte.Execute; BombPirte.cs calls CanUseSpell.Verified, uses a shared level target cap of 3/4/5 across radius-6 monster/player/SOB loops, applies monster physical damage with pushback, applies player physical damage /2 after BombPirte.cs lines 118-120 call Physical.OnPlayer before the /2 scaling step, with pushback and CheckGuildWar floor validation, applies player protect/AttackStamp after pushback, applies SOB damage with maps 1038/3868 returning before SOB damage, includes Acalanatha/NatureShield rune checks, sends MsgSpellAnimation, increases XP, and updates spell experience. references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer.AttackHandler\\Physical.cs applies target.Owner.CombatGear.WildPhoenix(). references/DecompiledSource\\GameServer\\MrRayzo.Role\\GameMap.cs Pushback keeps the original MoveX/MoveY when the first candidate tile is blocked.";

    private const string ScurvyBombEvidence =
        "data/DBServer\\magictype.txt spell 11040 uses sort 56; decompiled MsgAttackPacket.ProcescMagic case MagicSort.ScurvyBomb dispatches ScurvyBomb.Execute; ScurvyBomb.cs applies visible-player fatigue/status before Physical.OnPlayer, then Base.CalculateSoul, then doubles player damage before ReceiveAttack.Player.Execute; Physical.cs applies target.Owner.CombatGear.WildPhoenix(). WolfConquer HandleLegacyPlayerPirateSpecialMagicAsync accepts LegacyMagicSort.ScurvyBomb for the shared visible-monster damage, visible-player fatigue/status plus player-target WildPhoenix defender charge/archive hook, and visible-SOB damage branches.";

    private const string CannonBarrageEvidence =
        "data/DBServer\\magictype.txt spell 11050 uses sort 57; decompiled MsgAttackPacket.ProcescMagic case MagicSort.CannonBarrage dispatches CannonBarrage.Execute; CannonBarrage.cs calls CanUseSpell.Verified, consumes XPList to open or refresh CannonBarrage with OpenXpSkill(..., 50) and returns without sending damage animation, returns without animation when CannonBarrage status is absent, applies visible radius-6 physical monster damage while the flag is present, calls Physical.OnPlayer for visible radius-6 player targets, then Base.CalculateSoul, then divides visible radius-6 player physical damage by 11 before ReceiveAttack.Player.Execute and animation enqueue, and applies visible SOB damage. references/DecompiledSource\\GameServer\\MrRayzo.Role\\Player.cs OpenXpSkill resets XPCount to 0, removes a different active OnXPSkill flag, and applies the passed timer through AddFlag/UpdateFlag.";

    private const string PirateXpSkillEvidence =
        "data/DBServer\\magictype.txt spell 11060 uses sort 63; decompiled MsgAttackPacket.ProcescMagic case MagicSort.PirateXpSkill dispatches PirateXpSkill.Execute; PirateXpSkill.cs calls CanUseSpell.Verified, XPList opens/refreshes BlackbeardsRage with OpenXpSkill(..., 50) and returns without animation, and the non-XPList branch hits radius-6 visible monsters, players with damage *2, and SOB NPCs. references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer.AttackHandler\\PirateXpSkill.cs lines 46-54 call Physical.OnPlayer, then Base.CalculateSoul, then doubles player damage before ReceiveAttack.Player.Execute. references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer.AttackHandler\\Physical.cs applies target.Owner.CombatGear.WildPhoenix(). references/DecompiledSource\\GameServer\\MrRayzo.Role\\Player.cs OpenXpSkill resets XPCount to 0, removes a different active OnXPSkill flag, and applies the passed timer through AddFlag/UpdateFlag.";

    private const string BlackSpotEvidence =
        "references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer.AttackHandler\\BlackSpot.cs calls CanUseSpell.Verified, targets Attack.OpponentUID as Monster, Player, or SobNpc, gates caster-to-target distance <= 2, applies CanAttackMonster.Verified/CanAttackPlayer.Verified/CanAttackNpc.Verified before target effects, applies Physical.OnMonster/OnPlayer/OnNpcs, and for alive monster/player targets sets BlackSpot with Stamp_BlackSpot = DateTime.Now.AddSeconds(Spell.Duration) before sending stream.BlackspotCreate(Active: true, UID); references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer\\MsgBlackspot.cs defines the active state payload, and the source-backed BlackspotCreate packet uses opcode 2188.";

    private const string AddBlackSpotEvidence =
        "references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer.AttackHandler\\AddBlackSpot.cs calls CanUseSpell.Verified, requires learned BlackSpot spell 11120, uses learned BlackSpot 11120 Spell.Duration for BlackSpot expiry, checks visible monsters first and then visible players within the hard-coded 3/4/5 AddBlackSpot level budget and Spell.Range from Attack.X/Y, applies CanAttackMonster.Verified/CanAttackPlayer.Verified before marking, sends zero-damage MsgSpellAnimation targets, and sends stream.BlackspotCreate(Active: true, UID); WolfConquer uses LegacyBlackspotPacket opcode 2188 for the active packet. The legacy AddBlackSpot handler has no SOB loop.";

    private const string MoveLineEvidence =
        "references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer.AttackHandler\\MoveLine.cs calls CanUseSpell.Verified, builds a 12-cell MoveCoords.CheckBladeTeampsCoords path from the caster to Attack.X/Y, uses ValidLocation to truncate the line and the final valid line cell for animation X/Y, applies the source CheckGuildWar final endpoint gate for map 1038 gate-crossing, hits monsters, players, and SOB NPCs within <= 1.5 of the path, calls Physical.OnPlayer for player targets, then Base.CalculateSoul, then scales player physical spell damage * 12 / 16 before ReceiveAttack.Player.Execute and animation enqueue, applies SOB damage, can emit active BlackspotCreate opcode 2188 packets for monster targets when learned BlackSpot 11120 procs, for normal/non-TideTrap casts moves the view then assigns user.Player.X/Y to the final line landing cell, and for equipped TideTrap plus learned spell 14250 sets MsgSpellAnimation.bomb/SpellEffect=1 without moving the caster, then creates a floor spell effect item 1080 named trap at the landing when the animation has targets, storing the MoveLine Spell on the FloorSpell while the 14250 ClientFloorSpells entry is created from Pool.Magic[14250][0]. references/DecompiledSource\\GameServer\\MrRayzo.Role\\FloorSpell.cs stamps FloorSpell with DateTime.Now.AddMilliseconds(MillisecondsStamp); references/DecompiledSource\\GameServer\\MrRayzo\\ThreadPool.cs case 14250 checks ClientFloorSpells.CheckInvocke, builds a 12-cell owner-to-trap line, hits monsters, players, and SOB NPCs within <= 2.0 of that line, uses the floor spell DBSkill for target verification and monster/player/SOB damage while keeping the 14250 client-floor skill for the trap spell animation, applies player physical damage * 60 / 100, sends the trap spell animation, removes the floor effect, and drops the ClientFloorSpells entry when empty.";

    private const string FastBladeScentSwordManualLineEvidence =
        "data/DBServer\\magictype.txt rows 28-29 and 60-67 define spell 1045 FastBlade and 1046 ScentSword as sort 96 ManualLine level rows; references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer.AttackHandler\\Line.cs case 1045/1046 builds MsgSpellAnimation at Attack.X/Y, uses InLineAlgorithm from caster to Attack.X/Y, scans visible monsters, players, and SOB NPCs with InLine(..., 2), applies CanAttackMonster/CanAttackPlayer/CanAttackNpc gates, doubles monster animation damage, applies the Physical.OnPlayer 1045/1046 110% branch followed by player damage * 15 / 21, applies SOB damage, then runs IncreaseExperience.Up, UpdateSpell.CheckUpdate, Send, and JustMe. WolfConquer local handler evidence: WorldPacketHandlers.PlayerMagic.FastBladeScentSword.cs handles only spell ids 1045/1046 with sort 96, LegacyMagicRuntime.GetLegacyLineWidth maps 1045/1046 to width 2, and focused runtime proof covers mixed monster/player/SOB targets, guard exclusion, source damage scaling, progress, and Send plus JustMe fanout.";

    private const string FastBladeScentSwordLineSourceSilentEvidence =
        "data/DBServer\\magictype.txt rows around 1712/1713/2951 define spell ids 13450 FastBlade, 13460 ScentSword, and 18003 ScentSword as sort 14 Line with stamina 20, mana 0, and ColdTime 0; references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer\\MsgAttackPacket.cs dispatches MagicSort.Line and sort 96 to AttackHandler\\Line.cs. Line.cs calls CanUseSpell.Verified before switching ClientSpell.ID, handles cases 1045/1046, and has no cases for 13450, 13460, or 18003, so these ids fall through after resource preparation without MsgSpellAnimation or monster/player/SOB damage.";

    private const string RoamerWildwindSourceSilentEvidence =
        "data/DBServer\\magictype.txt rows 7040-7049 define spell type 7040 Roamer as sort 90 Wildwind level rows; references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer\\MsgAttackPacket.cs dispatches MagicSort.Wildwind to AttackHandler\\Wildwind.cs. Wildwind.cs calls CanUseSpell.Verified before switching ClientSpell.ID, handles cases 16520, 16530, 16430, 16420, 16480, 16490, 17640, 17650, 17660, 15940, 14680, 14530, and 15790, and has no case for spell ID 7040, so Roamer falls through without MsgSpellAnimation or monster/player/SOB damage.";

    private const string ViperFangLineEvidence =
        "data/DBServer\\magictype.txt rows 1061-1065 define spell 11005 ViperFang as sort 14 Line with damage raw 30095/30100/30105/30110/30130, range 5-9, stamina 20, mana 0, CoolDown 600, and ColdTime 0; references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer.AttackHandler\\Line.cs case 11005 builds MsgSpellAnimation at Attack.X/Y, uses InLineAlgorithm from caster to Attack.X/Y, scans visible monsters, players, and SOB NPCs with InLine(..., 1), applies CanAttackMonster/CanAttackPlayer/CanAttackNpc gates, increments HitShoot on player hits in counter-hit maps, applies physical monster/SOB damage unchanged, calls Physical.OnPlayer, then Base.CalculateSoul, then divides player damage by 2 before ReceiveAttack.Player.Execute, runs IncreaseExperience.Up and UpdateSpell.CheckUpdate with only accumulated monster/SOB damage, and sends one normal animation without the FastBlade/ScentSword JustMe self echo. references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer.AttackHandler\\Physical.cs applies target.Owner.CombatGear.WildPhoenix().";

    private const string DragonTailLineEvidence =
        "data/DBServer\\magictype.txt rows 1056-1060 define spell 11000 DragonTail as sort 14 Line with damage raw 30095/30100/30105/30110/30115, range 5-9, stamina 20, mana 0, CoolDown 600, and ColdTime 0; references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer.AttackHandler\\Line.cs case 11000 builds MsgSpellAnimation at Attack.X/Y, uses InLineAlgorithm from caster to Attack.X/Y, scans visible monsters, players, and SOB NPCs with InLine(..., 0), applies CanAttackMonster/CanAttackPlayer/CanAttackNpc gates, increments HitShoot on monster hits in counter-hit maps, applies physical monster/player/SOB damage unchanged, runs IncreaseExperience.Up and UpdateSpell.CheckUpdate with only accumulated monster/SOB damage, and sends one normal animation. WolfConquer local handler evidence: WorldPacketHandlers.PlayerMagic.DragonTail.cs handles only spell id 11000 with sort 14.";

    private const string DragonSlashLineEvidence =
        "data/DBServer\\magictype.txt rows 1405-1409 define spell 12350 DragonSlash as sort 14 Line with damage raw 30110/30115/30120/30125/30135, range 11, stamina 15, mana 0, CoolDown 600, and ColdTime 0; references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer.AttackHandler\\Line.cs case 12350 builds MsgSpellAnimation at Attack.X/Y, uses InLineAlgorithm from caster to Attack.X/Y with constructor range 10 and InLine(..., 0), scans visible monsters, players, and SOB NPCs with the target distance gate still using Spell.Range, applies CanAttackMonster/CanAttackPlayer/CanAttackNpc gates, increments HitShoot on monster hits in counter-hit maps, applies physical monster/SOB damage unchanged, applies physical player damage * 10 / 18, runs IncreaseExperience.Up and UpdateSpell.CheckUpdate with only accumulated Monster.Execute/Npc.Execute return values, where Monster.Execute caps outgoing damage at monster max health and applies AdjustExp, and sends one normal animation. WolfConquer local handler evidence: WorldPacketHandlers.PlayerMagic.DragonSlash.cs handles only spell id 12350 with sort 14.";

    private const string ThunderStrikerLineEvidence =
        "data/DBServer\\magictype.txt rows 2384-2393 define spell 15810 ThunderBlast as sort 14 Line with raw damage 30300, range 11, stamina 15, mana 0, CoolDown 600, ColdTime 0, and secondary damage fields 30110-30135; references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer.AttackHandler\\Line.cs case 15810 breaks after CanUseSpell.Verified when AtributesStatus.IsThunderStriker is false, otherwise builds MsgSpellAnimation at Attack.X/Y, uses InLineAlgorithm from caster to Attack.X/Y with Spell.Range, excludes guard monsters, scans visible monsters, players, and SOB NPCs with InLine(..., 1), applies CanAttackMonster/CanAttackPlayer/CanAttackNpc gates, applies physical monster runtime damage then doubles only monster animation damage, applies physical player damage * 53 / 100, applies SOB damage * 2, runs IncreaseExperience.Up and UpdateSpell.CheckUpdate with only accumulated monster/SOB damage, and sends one normal animation. WolfConquer local handler evidence: WorldPacketHandlers.PlayerMagic.ThunderStrikerLine.cs handles only spell id 15810 with sort 14.";

    private const string SpeedGunLineEvidence =
        "data/DBServer\\magictype.txt rows 127-136 define spell 1260 SpeedGun as sort 14 Line with range 9, stamina 1, mana 0, CoolDown 1, and ColdTime 0; references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer.AttackHandler\\Line.cs case 1260 builds MsgSpellAnimation at Attack.X/Y, uses InLineAlgorithm from caster to Attack.X/Y with Spell.Range and InLine(..., 1), scans visible monsters without guard-family exclusion, scans players with a hard-coded distance < 3, increments HitShoot on player hits in counter-hit maps, scans SOB NPCs with Spell.Range, applies physical monster/player/SOB damage unchanged, runs IncreaseExperience.Up and UpdateSpell.CheckUpdate with only accumulated monster/SOB damage, and sends animation only when at least one target was hit. WolfConquer local handler evidence: WorldPacketHandlers.PlayerMagic.SpeedGun.cs handles only spell id 1260 with sort 14 and includes the requested bounded counter-hit map HitShoot state.";

    private const string HookMoonLineEvidence =
        "data/DBServer\\magictype.txt rows 2465-2474 define spell 15930 HookMoon as sort 14 Line with range 18, max target field 11, stamina 0, mana 0, CoolDown 1, ColdTime 0, duration fields, and Damage3 values 30140-30180; references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer.AttackHandler\\Line.cs case 15930 uses a constructor range 11 and InLine(..., 2), excludes guard monsters, applies physical monster damage * 2 / 3 while doubling monster animation damage, applies physical player damage * 7 / 16, applies SOB damage * 50 / 100, sends one normal animation, then applies HookMoon target/caster status flags and ArchiveSkill updates. WolfConquer local handler evidence: WorldPacketHandlers.PlayerMagic.HookMoon.cs handles only spell id 15930 with sort 14 and models the HookMoon target/caster status attributes plus ArchiveSkill updates.";

    private const string RageOfWarLineEvidence =
        "data/DBServer\\magictype.txt rows 1577-1583 define spell 12930 RageofWar as sort 14 Line with range 20, stamina 15, mana 0, CoolDown 600, and ColdTime 0; references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer.AttackHandler\\Line.cs case 12930 gates monsters/players/SOB NPCs by hard-coded distance < 11, excludes guard monsters, uses InLine(..., 1) for monsters/SOB NPCs and InLine(..., 0) for players, increments HitShoot on initial player hits in counter-hit maps, applies physical monster/SOB damage unchanged, applies physical player damage * 10 / 13, runs IncreaseExperience.Up and UpdateSpell.CheckUpdate with only accumulated monster/SOB damage, sends one normal animation, then creates a 1500 floor spell effect for 2000ms at the nearest hit target and appends a floor animation target. references/DecompiledSource\\GameServer\\MrRayzo\\ThreadPool.cs case 12930 invokes due floor effects, sets bomb=1, hits monsters/players/SOBs within <= 3, scales player damage by *60/100, applies progress, and sends RemoveEffect within distance <= 18. WolfConquer local handler evidence: WorldPacketHandlers.PlayerMagic.RageOfWar.cs handles only spell id 12930 with sort 14, models the bounded initial-cast counter-hit map HitShoot state, 1500 floor-effect item packet/runtime metadata, floor animation target, delayed floor-effect damage, and removal packet.";

    private const string LayTrapEvidence =
        "data/DBServer\\magictype.txt contains sort 21 floor/trap rows including 14720-14728 BloomofDeath, 12070 TwilightDance, 12550, 12570, 12990, 15770 SkyFall, and 14250 TideTrap; decompiled MsgAttackPacket.ProcescMagic case MagicSort.LayTrap dispatches LayTrap.Execute; LayTrap.cs calls CanUseSpell.Verified, creates FloorSpell/MsgFloorItem effects for multiple spells, case 12990 creates FloorSpell(1530u, target.X, target.Y, color 14, duration 1000) with DontShow=1 and ItemOwnerUID=caster for visible player/monster/SOB targets, case 14720 BloomofDeath hits visible monsters, visible players, and SOB NPCs in caster radius 6, dividing visible-player damage by 5 before UpdateSpell.CheckUpdate and MsgSpellAnimation.Send, and case 15770 SkyFall performs direct ThunderStriker radius damage.";

    private const string ThunderStrikerLayTrapEvidence =
        "data/DBServer\\magictype.txt row 2362 defines spell 15770 SkyFall as sort 21 LayTrap with raw damage 30200, range 15, mana 0, CoolDown 800, and ColdTime 12; references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer.AttackHandler\\LayTrap.cs case 15770 runs after CanUseSpell.Verified, breaks without animation when AtributesStatus.IsThunderStriker is false or user.Player.ThunderStrikerEnergy < 100, subtracts ThunderStrikerEnergy by 100, builds MsgSpellAnimation at Attack.X/Y with SpellEffect/bomb flag 1, hits visible monsters, players, and SOB NPCs within distance <= 4 of Attack.X/Y through CanAttackMonster/CanAttackPlayer/CanAttackNpc, applies physical monster damage * 5, physical player damage * 18 / 28, and physical SOB damage * 3, then runs UpdateSpell.CheckUpdate with 10000 damage and sends the animation. WolfConquer local handler evidence: WorldPacketHandlers.PlayerMagic.Traps.ThunderStriker.cs handles only spell id 15770 with sort 21.";

    private const ushort BloomOfDeathLayTrapSpellId = 14_720;
    private const ushort LegacyLayTrap12990SpellId = 12_990;
    private const ushort ThunderStrikerLayTrapSpellId = 15_770;

    private static string TwilightDanceLayTrapEvidence =>
        string.Join(" ", LegacyLayTrap.TwilightDanceSourceEvidence);

    private static string SourceCases12550And12570LayTrapEvidence =>
        string.Join(" ", LegacyLayTrap.SourceCases12550And12570Evidence);

    private static string DaggerStormLayTrapEvidence =>
        string.Join(" ", LegacyLayTrap.DaggerStormSourceEvidence);

    private static string SpaceLeapLayTrapEvidence =>
        string.Join(" ", LegacyLayTrap.SpaceLeapSourceEvidence);

    private static string LotusLayTrapEvidence =>
        string.Join(" ", LegacyLayTrap.LotusSourceEvidence);

    private static string WaterShockwaveLayTrapEvidence =>
        string.Join(" ", LegacyLayTrap.WaterShockwaveSourceEvidence);

    private static string DustDetachmentLayTrapEvidence =>
        string.Join(" ", LegacyLayTrap.DustDetachmentSourceEvidence);

    private static string SickleWindLayTrapEvidence =>
        string.Join(" ", LegacyLayTrap.SickleWindSourceEvidence);

    private static string FlameOfDestructionLayTrapEvidence =>
        string.Join(" ", LegacyLayTrap.FlameOfDestructionSourceEvidence);

    private static readonly int[] KnownSortValues =
    [
        0, 1, 2, 4, 5, 6, 7, 11, 14, 16, 19, 20, 21, 22, 23, 24, 25, 26, 27, 28,
        29, 30, 31, 32, 33, 34, 35, 36, 37, 38, 40, 41, 42, 43, 44, 45, 46, 47,
        48, 50, 51, 52, 53, 54, 55, 56, 57, 58, 59, 60, 61, 62, 63, 64, 65, 66,
        67, 68, 69, 70, 71, 72, 73, 75, 76, 78, 79, 81, 82, 83, 84, 85, 86, 87,
        88, 89, 90, 91, 92, 94, 95, 96, 98, 99, 100, 101, 102, 103, 104, 105, 106,
        107,
    ];

    public static IReadOnlyList<LegacyMagicSortCapabilityInfo> KnownSorts { get; } =
        KnownSortValues.Select(sort => Classify((LegacyMagicSort)sort)).ToArray();

    public static LegacyMagicSortCapabilityInfo Classify(LegacyMagicRuntimeSpell spell)
    {
        return Classify(spell.Sort, spell.Id);
    }

    public static LegacyMagicSortCapabilityInfo Classify(LegacyMagicSort sort, ushort spellId = 0)
    {
        if (TryClassifyClassSkillRuntime(sort, spellId, out var classSkillInfo))
        {
            return classSkillInfo;
        }

        if (TryClassifySecondaryClassSkillRuntime(sort, spellId, out classSkillInfo))
        {
            return classSkillInfo;
        }

        if (TryClassifyTertiaryClassSkillRuntime(sort, spellId, out classSkillInfo))
        {
            return classSkillInfo;
        }

        if (TryClassifyQuaternaryClassSkillRuntime(sort, spellId, out classSkillInfo))
        {
            return classSkillInfo;
        }

        if (TryClassifyCounterKillRuntime(sort, spellId, out classSkillInfo))
        {
            return classSkillInfo;
        }

        if (TryClassifyClassicBombRangeRuntime(sort, spellId, out classSkillInfo))
        {
            return classSkillInfo;
        }

        if (TryClassifyPetAttachStatusRuntime(sort, spellId, out classSkillInfo))
        {
            return classSkillInfo;
        }

        if (TryClassifyTransformRuntime(sort, spellId, out classSkillInfo))
        {
            return classSkillInfo;
        }

        if (IsModeledScarOfEarthRuntimeSlice(sort, spellId))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.SpecialCombatDamage,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    "WolfConquer models the bounded Scar of Earth 12670 / sort 82 runtime as a resource-first " +
                    "exact-ID branch: a hard-coded range-5, width-1 increased line with ordered monster, player, and " +
                    "SOB passes; physical monster/player damage with the source player /2 scaling; Range.OnNpcs-shaped " +
                    "SOB damage; one combined monster/SOB progress update; and animation-last ordering. Living monster " +
                    "targets receive non-refreshing flag 180 for 12 seconds, and living player targets receive it through " +
                    "the source chance 100 - 5 * (target BattlePower - caster BattlePower). Recasts preserve the existing " +
                    "flag expiry/invoke cadence while updating spell/attacker metadata. Player and monster flags publish " +
                    "initial and expiry status vectors and run two-second victim-authored self ticks using Damage2. " +
                    "Remaining parity is exact offline-caster retention for periodic ticks and receive-attack side effects " +
                    "outside the bounded shared runtime; this claim does not cover generic sort-82 spell IDs.",
                sourceEvidence: ScarOfEarthRuntimeEvidence,
                suggestedNextOwner:
                    "Shared combat owner: tighten offline-caster retention and remaining ReceiveAttack side effects without " +
                    "broadening this exact spell-12670 capability to generic sort 82.",
                nextSeam: ScarOfEarthRuntimeSeam);
        }

        if (IsModeledHellVortex16370RuntimeSlice(sort, spellId))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.SpecialCombatDamage,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    "WolfConquer models only the exact HellVortex 16370 / sort 98 runtime slice: sort dispatch and " +
                    "resource preparation occur before the exact-ID fence; a selected monster UID produces only " +
                    "monster fanout within distance <= 6 around that selected monster, while a selected player UID " +
                    "produces only player fanout within distance <= 6 around that selected player. The categories do " +
                    "not mix, and there is no SOB branch. Selected centers use the raw neighboring-map-block lookup, " +
                    "including foreign-instance and retained dead roles; recipients still use instance-filtered " +
                    "visibility and attack gates. Monster and player targets use their Physical damage paths; " +
                    "player damage applies soul first and then * 12 / 28. Each player gate and session is refreshed " +
                    "at its receive turn; status returns precede WildPhoenix, and the bounded normal Physical tail " +
                    "preserves breakthrough, stance, Infinity and the nonpenetrating 50 percent bonus. Heaven's Wonder " +
                    "and RevengeTail run before MudWall and caller scaling. Opening CombatGear, speffect groups " +
                    "2/3/4/5 and Ninja Chance / 100 run before HP mutation, with each player's receive publication " +
                    "completed before the next target's gate. The cast has no pushback or movement, uses " +
                    "bomb flag 0, performs no IncreaseExperience or UpdateSpell call, publishes the modeled receive/HP/" +
                    "death effects before the animation, and sends the query X/Y animation last. The shared player " +
                    "gate preserves the equipped-and-learned defender's 10 percent countercast and source counter query " +
                    "X=0/Y=0. WolfConquer's active-caster/cascade-limit recursion guard is a bounded local safeguard and " +
                    "remaining parity debt. Exact shared Physical/ReceiveAttack side-effect coverage also remains debt; " +
                    "this record does not cover generic sort-98 IDs or characterize source counter recursion beyond the " +
                    "observed counter re-entry.",
                sourceEvidence: HellVortex16370RuntimeEvidence,
                suggestedNextOwner:
                    "Shared combat owner: tighten exact Physical/ReceiveAttack side effects and review the local bounded " +
                    "recursion safeguard against matching-source behavior without broadening this exact spell-16370 " +
                    "capability to generic sort 98.",
                nextSeam: HellVortex16370RuntimeSeam);
        }

        if (IsModeledShurikenVortexRuntimeSlice(sort, spellId))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.XpSkill,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    "WolfConquer models exact ShurikenVortex 6010 resource-first XPList activation with source-ordered " +
                    "XPCircle, staged XP-skill status vectors, XPList removal, and self animation; AutoHunt leaves the " +
                    "skill unopened while still consuming XPList, and same-skill refresh preserves the current invoke " +
                    "schedule while extending the 20-second expiry. The runtime performs an inclusive one-second " +
                    "current-tile automatic cast before expiry processing, including the source final-due-tick shape. " +
                    "Its active branch borrows row 6009 level zero, applies strict-range ordered monster/player/SOB " +
                    "physical damage with the player /6 step, publishes target HP/death effects before one combined " +
                    "monster/SOB return-experience update, performs no spell progress or immediate retaliation, and " +
                    "keeps the D13 animation last even when no target qualifies. Player target-status defense checks " +
                    "ShurikenVortex/ManiacDance before WildPhoenix. For normal eligible player hits, worldData missing " +
                    "remains a guarded unchanged path; otherwise the complete matching-service-pack nonzero " +
                    "Physical.OnPlayer branch is modeled. Refinery succeeds iff adjusted Breakthrough > adjusted " +
                    "Counteraction and roll < difference, with a deterministic runtime test override. Within a " +
                    "successful outer gate, any qualifying Penetration boost occurs first, then an equipped target " +
                    "Barrier and its source-nested attacker TacitStrike apply in sequential-floor order. Actual " +
                    "Penetration additionally requires lower attacker BattlePower and neither Sacrifice nor DeathSigh, " +
                    "adds 30 percent or 45 percent for spells 16480/16490, sets Penetration, applies /0.67 compensation " +
                    "or /0.66 for close spell 12080, and suppresses the later 50-percent bonus. Any failed refinery, " +
                    "BattlePower/status-exclusion failure, or prior critical/lucky/dash path keeps flag2 false and " +
                    "receives that later 50-percent bonus. Source order places compensation before DefensiveStance and " +
                    "Infinity, then the flag2-controlled 50-percent branch before IronShield. The bounded " +
                    "post-IronShield tail scales damage by SlayerPercent / 100 when Slayer flag 0xDA is present and " +
                    "consumes that flag. With Rune eligibility requiring Reborn >= 2, Level >= 15, and an equipped " +
                    "job-valid rune, NoMercy clamps damage below 22000 to 22000 and Assassin plus OnAutoHunt adds " +
                    "38000. Target DragonSwing flag 0xA0 then gates its DragonSwingChance roll. An eligible equipped " +
                    "SwingingTail adds 30 only for spell IDs 11030, 11060, 11590, 11600, 11650, and 11660; success " +
                    "sends the target-authored poisonmiss effect, sets damage to zero, and adds 30 target Dodge. The " +
                    "DragonSwing check follows the Slayer/rune tail and precedes BackDmg. After BackDmg, target " +
                    "MudWall flag 0x112 plus equipped Ninja MudSigilImpeccable type 301 directly subtracts current " +
                    "damage * (Power / 100) / 100; Ninja class and active primary/alternate equipment-set checks apply, " +
                    "then Shuriken's caller /6 follows. " +
                    "IronShield publishes " +
                    "BlueDamage type 55, consumes shield power, " +
                    "keeps one outgoing damage while incoming damage is less than or equal to the shield, and removes " +
                    "the status while forwarding only overflow when incoming damage is strictly greater. This " +
                    "capability remains partial for remaining ReceiveAttack tails and " +
                    "caller-specific CanAttack edges; it does not claim full Physical parity.",
                sourceEvidence: ShurikenVortexRuntimeEvidence,
                suggestedNextOwner:
                    "Shared combat owner: finish remaining ReceiveAttack tails and " +
                    "caller-specific CanAttack edges without changing exact ShurikenVortex activation, cadence, " +
                    "targeting, combined XP, or D13 packet order.",
                nextSeam: ShurikenVortexRuntimeSeam);
        }

        if (IsModeledManiacDanceRuntimeSlice(sort, spellId))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.XpSkill,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    "WolfConquer models exact ManiacDance 12700 resource-first XPList activation, 15-second flag " +
                    "lifetime, source-ordered XPList/status/XPCircle replacement packets, AutoHunt no-op, source " +
                    "equipment-speed initial delay, self " +
                    "animation, strict timed expiry, and the automatic one-second caster-centered radius-5 " +
                    "monster/player/SOB physical attack. The automatic branch preserves character XP without spell " +
                    "progress through one combined return update, sends an empty animation when no target qualifies, " +
                    "keeps the animation last, uses shared CanAttack gates, and checks ShurikenVortex/ManiacDance " +
                    "before WildPhoenix for player targets. For normal eligible player hits, worldData missing remains " +
                    "a guarded unchanged path; otherwise the complete matching-service-pack nonzero Physical.OnPlayer " +
                    "branch is modeled. Refinery succeeds iff adjusted Breakthrough > adjusted Counteraction and roll " +
                    "< difference, with a deterministic runtime test override. Within a successful outer gate, any " +
                    "qualifying Penetration boost occurs first, then an equipped target Barrier and its source-nested " +
                    "attacker TacitStrike apply in sequential-floor order. Actual Penetration additionally requires " +
                    "lower attacker BattlePower and neither Sacrifice nor DeathSigh, adds 30 percent or 45 percent for " +
                    "spells 16480/16490, sets Penetration, applies /0.67 compensation or /0.66 for close spell 12080, " +
                    "and suppresses the later 50-percent bonus. Any failed refinery, BattlePower/status-exclusion " +
                    "failure, or prior critical/lucky/dash path keeps flag2 false and receives that later 50-percent " +
                    "bonus. Source order places compensation before DefensiveStance and Infinity, then the " +
                    "flag2-controlled 50-percent branch before IronShield. The bounded post-IronShield tail scales " +
                    "damage by SlayerPercent / 100 when Slayer flag 0xDA is present and consumes that flag. With Rune " +
                    "eligibility requiring Reborn >= 2, Level >= 15, and an equipped job-valid rune, NoMercy clamps " +
                    "damage below 22000 to 22000 and Assassin plus OnAutoHunt adds 38000. Target DragonSwing flag " +
                    "0xA0 then gates its DragonSwingChance roll. An eligible equipped SwingingTail adds 30 only for " +
                    "spell IDs 11030, 11060, 11590, 11600, 11650, and 11660; success sends the target-authored " +
                    "poisonmiss effect, sets damage to zero, and adds 30 target Dodge. The DragonSwing check follows " +
                    "the Slayer/rune tail and precedes BackDmg. After BackDmg, target MudWall flag 0x112 plus equipped " +
                    "Ninja MudSigilImpeccable type 301 directly subtracts current damage * (Power / 100) / 100; Ninja " +
                    "class and active primary/alternate equipment-set checks apply, and ManiacDance has no Shuriken " +
                    "caller /6 step. IronShield " +
                    "publishes BlueDamage type 55, " +
                    "consumes shield power, keeps " +
                    "one outgoing damage " +
                    "while incoming damage is less than or equal to the shield, and removes the status while forwarding " +
                    "only overflow when incoming damage is strictly greater. This capability remains partial for " +
                    "remaining ReceiveAttack tails and caller-specific CanAttack edges; it does " +
                    "not claim full Physical parity.",
                sourceEvidence: ManiacDanceRuntimeEvidence,
                suggestedNextOwner:
                    "Shared combat owner: finish remaining ReceiveAttack tails and " +
                    "caller-specific CanAttack edges without changing the exact ManiacDance activation, cadence, " +
                    "targeting, or D13 packet contract.",
                nextSeam: ManiacDanceRuntimeSeam);
        }

        if (IsSupportedAurasRuntimeSpell(sort, spellId))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.StateAndBuffRuntime,
                LegacyMagicModelingStatus.FullyModeled,
                reason:
                    "This exact Auras spell is modeled after CanUseSpell ordering. The seven ordinary auras preserve " +
                    "self-or-visible-player primary AddAura toggling/replacement, 2592000-second persistence, owner-only " +
                    "FlagIcon add packets, immediate removal/add status vectors, source stat effects, no-map strict-distance " +
                    "team fanout for 30 seconds, caster-to-caster animation with only team targets, primary-add-only progress, " +
                    "expiry, and death cleanup. Spell 11200 separately removes Ride, toggles MagicDefender flag 0x80, sends " +
                    "AzureShield update, targetless caster-to-caster animation, progress, and its own expiry/death metadata " +
                    "without using AddAura or FlagIcon.",
                sourceEvidence: AurasRuntimeEvidence,
                suggestedNextOwner:
                    "No next gameplay owner for exact matching Auras IDs; keep adjacent sort-51 IDs source-silent after CanUseSpell unless matching evidence expands the set.",
                nextSeam: ImplementedAurasRuntimeSeam);
        }

        if (IsSoulShackleAttachStatusTargetDebuff(sort, spellId))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.StateAndBuffRuntime,
                LegacyMagicModelingStatus.FullyModeled,
                reason:
                    "This AttachStatus spell ID is modeled as the source SoulShackle target debuff: resources first; map-1038 live NEWGUILDWAR Exorcim-owner relation gating; a visible dead-player target; existing SoulShackle/SacredBlessing duration-backed progress plus empty animation; exact BattlePower-gap rejection; Sturdiness and FreeSoul avoidance; Evocation's unchecked effective-duration subtraction; opcode 2176 MsgUpdate type 0x36 with flag 0x6F, effective Time, Damage 0, and learned level; timed RemoveOnDead status; original-duration progress; and opcode 2239 zero-damage target animation.",
                sourceEvidence: AttachStatusEvidence,
                suggestedNextOwner:
                    "No next gameplay owner for the modeled SoulShackle direct AttachStatus debuff; continue adjacent AttachStatus cases.",
                nextSeam: ImplementedAttachStatusTargetDebuffSeam);
        }

        if (IsSupportedAttachStatusBuffSpell(sort, spellId))
        {
            if (IsClassicAttachStatusBuffSpell(sort, spellId))
            {
                return CreateInfo(
                    sort,
                    spellId,
                    LegacyMagicSortCapability.AttachStatusBuff,
                    LegacyMagicModelingStatus.FullyModeled,
                    reason:
                        "This classic AttachStatus buff family is fully modeled for Invisibility 1075 flag 0x16, StarOfAccuracy 1085 flag 0x07, MagicShield 1090 flag 0x08, and Stigma 1095 flag 0x09. Each completed case prepares resources and cooldowns and routes self, visible-player, and visible-monster targets. A successful application sends opcode 2176 MsgUpdate type 0x19 StatusFlag, advances duration-backed spell progress, then sends opcode 2239 zero-damage animation. An already-active MagicShield suppresses status reapplication and its animation target while still advancing progress and sending an empty animation; an unknown target likewise advances progress and sends an empty animation. Timed expiry, RemoveOnDead player and monster death cleanup, and active monster status replay in spawn packets are modeled.",
                    sourceEvidence: ClassicAttachStatusBuffEvidence,
                    suggestedNextOwner:
                        "No next gameplay owner for classic AttachStatus cases 1075/1085/1090/1095; continue adjacent AttachStatus cases without importing 7770 behavior or wire.",
                    nextSeam: ImplementedClassicAttachStatusBuffSeam);
            }

            if (IsAttachStatusXpListSelfBuff(sort, spellId))
            {
                return CreateInfo(
                    sort,
                    spellId,
                    LegacyMagicSortCapability.XpSkill,
                    LegacyMagicModelingStatus.FullyModeled,
                    reason:
                        "This XPList-gated AttachStatus self buff is modeled directly from cases 1015, 1020, and 8002: resources prepare first, a missing XPList silently stops the cast, XPList removal sends its scalar and status-vector updates without resetting XPCount, Accuracy and Shield use AddFlag timer semantics, Fly refreshes an already-active timer, timed removal is modeled, and the self spell animation preserves each case's target-damage field.",
                    sourceEvidence: AttachStatusEvidence,
                    suggestedNextOwner:
                        "No next gameplay owner for the Accuracy/Shield/Fly XPList AttachStatus family; continue adjacent class-specific source cases.",
                    nextSeam: XpSkillRuntimeSeam);
            }

            if (IsAttachStatusOpenXpSkill(sort, spellId))
            {
                return CreateInfo(
                    sort,
                    spellId,
                    LegacyMagicSortCapability.XpSkill,
                    LegacyMagicModelingStatus.FullyModeled,
                    reason:
                        "This AttachStatus XP skill is modeled for source XPList gating/removal, OpenXpSkill flag timing, status vector updates, timed expiry, and self spell animation; SuperCyclone 11970 additionally restores stamina to the 100/150 HeavenBlessing maximum, while FatalStrike 6011 opens FatalStrike for 60 seconds.",
                    sourceEvidence: AttachStatusEvidence,
                    suggestedNextOwner: "No next gameplay owner for modeled AttachStatus XPList activations; continue adjacent AttachStatus cases and other special XP-skill runtimes.",
                    nextSeam: XpSkillRuntimeSeam);
            }

            if (IsMagicDefenderAttachStatusTargetBuff(sort, spellId))
            {
                return CreateInfo(
                    sort,
                    spellId,
                    LegacyMagicSortCapability.AttachStatusBuff,
                    LegacyMagicModelingStatus.FullyModeled,
                    reason:
                        "This AttachStatus spell ID is modeled for the source MagicDefender/AzureShield direct cast: self or visible-player target, AzureShield status flag and expiry, status vector updates, AzureShieldLevel from learned spell level, AzureShieldDefence 50000, source duration 15 seconds per learned level, animation even when no target is applied, and duration-backed spell-progress tick.",
                    sourceEvidence: AttachStatusEvidence,
                    suggestedNextOwner: "No next gameplay owner for MagicDefender direct AttachStatus cast; continue adjacent AttachStatus cases.",
                    nextSeam: ImplementedAttachStatusSelfOrVisiblePlayerBuffSeam);
            }

            if (IsInfiniteMistAttachStatusTargetBuff(sort, spellId))
            {
                return CreateInfo(
                    sort,
                    spellId,
                    LegacyMagicSortCapability.AttachStatusBuff,
                    LegacyMagicModelingStatus.FullyModeled,
                    reason:
                        "This AttachStatus spell ID is modeled for the source InfiniteMist direct cast: Ninja caster gate, visible-player target requirement, target InfiniteMist status flag and expiry, no ArchiveSkill packet, and the decompiled self-shaped zero-damage animation.",
                    sourceEvidence: $"{AttachStatusEvidence} {PlayerReceiveInfiniteMistEvidence}",
                    suggestedNextOwner: "No next gameplay owner for InfiniteMist direct AttachStatus cast; continue adjacent AttachStatus cases.",
                    nextSeam: ImplementedAttachStatusSelfOrVisiblePlayerBuffSeam);
            }

            if (IsHawksAmbitionAttachStatusSelfBuff(sort, spellId))
            {
                return CreateInfo(
                    sort,
                    spellId,
                    LegacyMagicSortCapability.AttachStatusBuff,
                    LegacyMagicModelingStatus.FullyModeled,
                    reason:
                        "This AttachStatus spell ID is modeled for the source HawksAmbition direct cast: interaction echo, self HawksAmbition status flag and online expiry, RemoveOnDead metadata cleanup, stored HawksAmbitionPower from row Damage, ArchiveSkill packet, self animation, equipment refresh, and the 500-point spell-progress tick.",
                    sourceEvidence: AttachStatusEvidence,
                    suggestedNextOwner: "No next gameplay owner for HawksAmbition direct AttachStatus cast; continue adjacent AttachStatus cases.",
                    nextSeam: ImplementedAttachStatusSelfOrVisiblePlayerBuffSeam);
            }

            if (IsRevengeTailAttachStatusSelfToggle(sort, spellId))
            {
                return CreateInfo(
                    sort,
                    spellId,
                    LegacyMagicSortCapability.AttachStatusBuff,
                    LegacyMagicModelingStatus.FullyModeled,
                    reason:
                        "This AttachStatus spell ID is modeled for the source RevengeTail direct cast: interaction echo, active-state removal, self-only 10-second RevengeTail status and RevengeTailChange counter, RevengeGale rune boost, targetless non-self animation, and timed expiry.",
                    sourceEvidence: AttachStatusEvidence,
                    suggestedNextOwner: "No next gameplay owner for RevengeTail direct AttachStatus cast; continue adjacent AttachStatus cases.",
                    nextSeam: ImplementedAttachStatusSelfOrVisiblePlayerBuffSeam);
            }

            if (IsDragonSwingAttachStatusSelfToggle(sort, spellId))
            {
                return CreateInfo(
                    sort,
                    spellId,
                    LegacyMagicSortCapability.AttachStatusBuff,
                    LegacyMagicModelingStatus.FullyModeled,
                    reason:
                        "This AttachStatus spell ID is modeled for the source DragonSwing direct cast: interaction echo, active DragonWar participant guard, active-state removal, self-only 2592000-second DragonSwing flag 0xA0 with RemoveOnDead false, MsgUpdate.DataType.DragonSwing 0x4B carrying Spell.Duration/damage 33/learned level, DragonSwingChance from row Damage/100, duration-backed spell progress, empty non-self animation, timed expiry, and monster physical defense reads the real status flag.",
                    sourceEvidence: AttachStatusEvidence,
                    suggestedNextOwner: "No next gameplay owner for DragonSwing direct AttachStatus cast; continue adjacent AttachStatus cases.",
                    nextSeam: ImplementedAttachStatusSelfOrVisiblePlayerBuffSeam);
            }

            if (IsAutoHuntAttachStatusSelfToggle(sort, spellId))
            {
                return CreateInfo(
                    sort,
                    spellId,
                    LegacyMagicSortCapability.AttachStatusBuff,
                    LegacyMagicModelingStatus.FullyModeled,
                    reason:
                        "This AttachStatus spell ID is modeled for the source AutoHunt direct cast: interaction echo, active-state removal, self-only 2592000-second AutoHunt flag 0x94 with RemoveOnDead false and stamp 8, source-shaped self animation before duration-backed spell progress, empty non-self animation, timed expiry, and no coupling to the separate AutoHunt packet mode.",
                    sourceEvidence: AttachStatusEvidence,
                    suggestedNextOwner: "No next gameplay owner for AutoHunt direct AttachStatus cast; continue adjacent AttachStatus cases or source stamina tick behavior if needed.",
                    nextSeam: ImplementedAttachStatusSelfOrVisiblePlayerBuffSeam);
            }

            if (IsFlyAttachStatusSelfBuff(sort, spellId))
            {
                return CreateInfo(
                    sort,
                    spellId,
                    LegacyMagicSortCapability.AttachStatusBuff,
                    LegacyMagicModelingStatus.FullyModeled,
                    reason:
                        "This AttachStatus spell ID is modeled for the source Fly direct cast: transform/Ride/PathOfShadow gate with the source system message, Fly flag 0x1B refresh or application for Spell.Duration with RemoveOnDead true, status vector update and timed expiry, source-shaped animation at Attack.X/Y with attacked UID 0 and a Spell.Duration self target, no interaction echo, and no spell-progress tick.",
                    sourceEvidence: AttachStatusEvidence,
                    suggestedNextOwner: "No next gameplay owner for Fly direct AttachStatus cast; continue adjacent AttachStatus cases.",
                    nextSeam: ImplementedAttachStatusSelfOrVisiblePlayerBuffSeam);
            }

            if (IsCastPrayAttachStatusSelfBuff(sort, spellId))
            {
                return CreateInfo(
                    sort,
                    spellId,
                    LegacyMagicSortCapability.AttachStatusBuff,
                    LegacyMagicModelingStatus.FullyModeled,
                    reason:
                        "This AttachStatus spell ID is modeled for the source CastPray direct cast: CastPray flag 0x1E for 2592000 seconds with RemoveOnDead true, status vector update and timed expiry, source-shaped animation at Attack.X/Y with attacked UID 0 and a zero-damage self target, no interaction echo, and no spell-progress tick.",
                    sourceEvidence: AttachStatusEvidence,
                    suggestedNextOwner: "No next gameplay owner for CastPray direct AttachStatus cast; continue adjacent AttachStatus cases.",
                    nextSeam: ImplementedAttachStatusSelfOrVisiblePlayerBuffSeam);
            }

            if (IsIntensifyAttachStatusFocusCharge(sort, spellId))
            {
                return CreateInfo(
                    sort,
                    spellId,
                    LegacyMagicSortCapability.AttachStatusBuff,
                    LegacyMagicModelingStatus.FullyModeled,
                    reason:
                        "This AttachStatus spell ID is modeled for the source Intensify/SpiritFocus focus charge: rewritten interaction with SpellID 0, KilledMonster set to the client spell ID, learned SpellLevel echo, InUseIntensify focus state, IntensifyDamage from row Damage, duration-backed spell progress, 5-second runtime promotion into Focused flag 0x15, self spell animation, status vector update, 60-second timed expiry, movement/jump/MsgAttack cancellation before promotion, MsgAttack-start Focused removal with a one-packet doFocus marker, marker cleanup after packet handling, and direct monster/direct SOB/direct player physical one-shot boost through that marker.",
                    sourceEvidence: AttachStatusEvidence,
                    suggestedNextOwner: "Combat runtime owner: prove and wire doFocus damage usage beyond direct physical calculators, especially magic and attack-replacement paths.",
                    nextSeam: ImplementedAttachStatusSelfOrVisiblePlayerBuffSeam);
            }

            if (IsInfinityAttachStatusFlyBuff(sort, spellId))
            {
                return CreateInfo(
                    sort,
                    spellId,
                    LegacyMagicSortCapability.AttachStatusBuff,
                    LegacyMagicModelingStatus.FullyModeled,
                    reason:
                        "This AttachStatus spell ID is modeled for the source Infinity/Fly direct cast: interaction echo, Fly and Infinity self flags, InfinitySDamage and InfinityDamage combat bonus fields, self animation at Attack.X/Y, visible Infinity countdown update, and timed expiry.",
                    sourceEvidence: AttachStatusEvidence,
                    suggestedNextOwner: "No next gameplay owner for Infinity/Fly direct AttachStatus cast; continue adjacent AttachStatus cases.",
                    nextSeam: ImplementedAttachStatusSelfOrVisiblePlayerBuffSeam);
            }

            if (IsSacrificeAttachStatusSelfToggle(sort, spellId))
            {
                return CreateInfo(
                    sort,
                    spellId,
                    LegacyMagicSortCapability.AttachStatusBuff,
                    LegacyMagicModelingStatus.FullyModeled,
                    reason:
                        "This AttachStatus spell ID is modeled for the source Sacrifice direct cast: interaction echo, active-state removal, self-only 2592000-second Sacrifice status with RemoveOnDead false, self animation at Attack.X/Y, equipment refresh on add and remove, 500-point spell-progress tick on add, and timed expiry.",
                    sourceEvidence: AttachStatusEvidence,
                    suggestedNextOwner: "No next gameplay owner for Sacrifice direct AttachStatus cast; continue adjacent AttachStatus cases.",
                    nextSeam: ImplementedAttachStatusSelfOrVisiblePlayerBuffSeam);
            }

            if (IsRiseOfTaoismAttachStatusSelfBuff(sort, spellId))
            {
                return CreateInfo(
                    sort,
                    spellId,
                    LegacyMagicSortCapability.AttachStatusBuff,
                    LegacyMagicModelingStatus.FullyModeled,
                    reason:
                        "This AttachStatus spell ID is modeled for the source RiseofTaoism direct cast: interaction echo, self status flag and expiry, stored RiseofTaoismExtraMDamage from row Damage, visible FineRain countdown update with Damage 50000, self animation, equipment refresh, and no spell-progress tick.",
                    sourceEvidence: AttachStatusEvidence,
                    suggestedNextOwner: "No next gameplay owner for RiseofTaoism direct AttachStatus cast; continue adjacent AttachStatus cases.",
                    nextSeam: ImplementedAttachStatusSelfOrVisiblePlayerBuffSeam);
            }

            if (IsIronGuardAttachStatusSelfBuff(sort, spellId))
            {
                return CreateInfo(
                    sort,
                    spellId,
                    LegacyMagicSortCapability.AttachStatusBuff,
                    LegacyMagicModelingStatus.FullyModeled,
                    reason:
                        "This AttachStatus spell ID is modeled for the source IronGuard direct cast: interaction echo, self status flag and expiry, FuryStrikeHP from DamageOnHuman, FuryStrikeAttack from the source byte-cast Damage value, visible SkillCountdown update, self animation, equipment refresh, and no spell-progress tick.",
                    sourceEvidence: AttachStatusEvidence,
                    suggestedNextOwner: "No next gameplay owner for IronGuard direct AttachStatus cast; continue adjacent AttachStatus cases.",
                    nextSeam: ImplementedAttachStatusSelfOrVisiblePlayerBuffSeam);
            }

            if (IsIronShieldAttachStatusSelfBuff(sort, spellId))
            {
                return CreateInfo(
                    sort,
                    spellId,
                    LegacyMagicSortCapability.AttachStatusBuff,
                    LegacyMagicModelingStatus.FullyModeled,
                    reason:
                        "This AttachStatus spell ID is modeled for the source IronShield direct cast: interaction echo, self status flag and expiry, IronShieldDamage from DamageOnMonster, visible AzureShield update carrying DamageOnMonster and spell level, self animation, no equipment refresh, and no spell-progress tick.",
                    sourceEvidence: AttachStatusEvidence,
                    suggestedNextOwner: "No next gameplay owner for IronShield direct AttachStatus cast; continue adjacent AttachStatus cases.",
                    nextSeam: ImplementedAttachStatusSelfOrVisiblePlayerBuffSeam);
            }

            if (IsFineRainAttachStatusTeamBuff(sort, spellId))
            {
                return CreateInfo(
                    sort,
                    spellId,
                    LegacyMagicSortCapability.AttachStatusBuff,
                    LegacyMagicModelingStatus.FullyModeled,
                    reason:
                        "This AttachStatus spell ID is modeled for the source FineRain direct cast: interaction echo, self and in-range living teammate FineRain1/FineRain2 status flags and expiries, FineRainPower/defFineRainPower from row Damage, visible FineRain countdown updates, equipment refreshes, HP fill to max, team member-info refresh, source dead-teammate break behavior, self-plus-team animation, and no spell-progress tick.",
                    sourceEvidence: AttachStatusEvidence,
                    suggestedNextOwner: "No next gameplay owner for FineRain direct AttachStatus cast; continue adjacent AttachStatus cases.",
                    nextSeam: ImplementedAttachStatusSelfOrVisiblePlayerBuffSeam);
            }

            if (IsCrackStarAttachStatusSelfBuff(sort, spellId))
            {
                return CreateInfo(
                    sort,
                    spellId,
                    LegacyMagicSortCapability.AttachStatusBuff,
                    LegacyMagicModelingStatus.FullyModeled,
                    reason:
                        "This AttachStatus spell ID is modeled for the source CrackStar direct cast: WindWalker and MainFlag.OnMeleeAttack gate, interaction echo, self CrackStar status flag and expiry, status vector update, self animation at 0/0, and no spell-progress tick.",
                    sourceEvidence: AttachStatusEvidence,
                    suggestedNextOwner: "No next gameplay owner for CrackStar direct AttachStatus cast; continue adjacent AttachStatus cases.",
                    nextSeam: ImplementedAttachStatusSelfOrVisiblePlayerBuffSeam);
            }

            if (IsBackfireAttachStatusSelfBuff(sort, spellId))
            {
                return CreateInfo(
                    sort,
                    spellId,
                    LegacyMagicSortCapability.AttachStatusBuff,
                    LegacyMagicModelingStatus.FullyModeled,
                    reason:
                        "This AttachStatus spell ID is modeled for the source Backfire direct cast: self Backfire status flag 0xB3, source hard-coded 10-second duration with RemoveOnDead false, status vector update, targetless source animation at Attack.X/Y with a zero-damage self target, and no interaction echo or spell-progress tick.",
                    sourceEvidence: AttachStatusEvidence,
                    suggestedNextOwner: "No next gameplay owner for Backfire direct AttachStatus cast; continue adjacent AttachStatus cases.",
                    nextSeam: ImplementedAttachStatusSelfOrVisiblePlayerBuffSeam);
            }

            if (IsPathOfShadowAttachStatusSelfToggle(sort, spellId))
            {
                return CreateInfo(
                    sort,
                    spellId,
                    LegacyMagicSortCapability.AttachStatusBuff,
                    LegacyMagicModelingStatus.FullyModeled,
                    reason:
                        "This AttachStatus spell ID is modeled for the source PathOfShadow direct cast: self PathOfShadow status flag 0x91, source 2592000-second duration with RemoveOnDead false when adding, active-state removal that also clears KineticSpark 0x93, status vector update, targetless source animation at Attack.X/Y with a zero-damage self target, and no interaction echo or spell-progress tick.",
                    sourceEvidence: AttachStatusEvidence,
                    suggestedNextOwner: "No next gameplay owner for PathOfShadow direct AttachStatus cast; continue adjacent AttachStatus cases.",
                    nextSeam: ImplementedAttachStatusSelfOrVisiblePlayerBuffSeam);
            }

            if (IsDefensiveStanceAttachStatusSelfToggle(sort, spellId))
            {
                return CreateInfo(
                    sort,
                    spellId,
                    LegacyMagicSortCapability.AttachStatusBuff,
                    LegacyMagicModelingStatus.FullyModeled,
                    reason:
                        "This AttachStatus spell ID is modeled for the source DefensiveStance direct cast: source Ride removal before the cast state, self DefensiveStance status flag 0x7E, duration from Spell.Duration with RemoveOnDead false when adding, active-state removal, visible DefensiveStance countdown update 0x38 carrying time/damage/learned level only when adding, targetless source animation at Attack.X/Y with a zero-damage self target, and no interaction echo or spell-progress tick.",
                    sourceEvidence: AttachStatusEvidence,
                    suggestedNextOwner: "No next gameplay owner for DefensiveStance direct AttachStatus cast; continue adjacent AttachStatus cases.",
                    nextSeam: ImplementedAttachStatusSelfOrVisiblePlayerBuffSeam);
            }

            if (IsLightningShieldAttachStatusSelfToggle(sort, spellId))
            {
                return CreateInfo(
                    sort,
                    spellId,
                    LegacyMagicSortCapability.AttachStatusBuff,
                    LegacyMagicModelingStatus.FullyModeled,
                    reason:
                        "This AttachStatus spell ID is modeled for the source LightningShield direct cast: self LightningShield status flag 0xF0, source 2592000-second duration with RemoveOnDead false when adding, active-state removal that also clears LightningShieldActivated 0xED and sends an AzureShield reset 0x31, status vector update, self-shaped animation at Attack.X/Y with a zero-damage self target, and no interaction echo or spell-progress tick.",
                    sourceEvidence: AttachStatusEvidence,
                    suggestedNextOwner: "No next gameplay owner for LightningShield direct AttachStatus cast; continue adjacent AttachStatus cases.",
                    nextSeam: ImplementedAttachStatusSelfOrVisiblePlayerBuffSeam);
            }

            if (IsImmortalForceAttachStatusSelfBuff(sort, spellId))
            {
                return CreateInfo(
                    sort,
                    spellId,
                    LegacyMagicSortCapability.AttachStatusBuff,
                    LegacyMagicModelingStatus.FullyModeled,
                    reason:
                        "This AttachStatus spell ID is modeled for the source ImmortalForce direct cast: interaction echo, self ImmortalForce status flag 0xDC, duration from Spell.Duration with RemoveOnDead true, status vector update, self-shaped animation at Attack.X/Y with a zero-damage self target, and no equipment refresh or spell-progress tick.",
                    sourceEvidence: AttachStatusEvidence,
                    suggestedNextOwner: "No next gameplay owner for ImmortalForce direct AttachStatus cast; continue adjacent AttachStatus cases.",
                    nextSeam: ImplementedAttachStatusSelfOrVisiblePlayerBuffSeam);
            }

            if (IsCombatGearAttachStatusSelfBuff(sort, spellId))
            {
                if (IsCombatGearWildPhoenixSelfStatusBuff(sort, spellId))
                {
                    return CreateInfo(
                        sort,
                        spellId,
                        LegacyMagicSortCapability.AttachStatusBuff,
                        LegacyMagicModelingStatus.PartiallyModeled,
                        reason: WildPhoenixCombatGearReason,
                        sourceEvidence: CombatGearSelfStatusEvidence + " " + NebulousHuntSectorBackPhysicalEvidence + " " + CleanSweepSectorBackEvidence,
                        suggestedNextOwner: WildPhoenixCombatGearNextOwner,
                        nextSeam: WildPhoenixPhysicalRangeHookRuntimeSeam);
                }

                return CreateInfo(
                    sort,
                    spellId,
                    LegacyMagicSortCapability.AttachStatusBuff,
                    LegacyMagicModelingStatus.FullyModeled,
                    reason:
                        "This CombatGear sort-6 spell ID is modeled through the source CombatGearAttack self-status path with resources, cooldowns, status vector updates, ArchiveSkill damage from DamageOnHuman, self spell animation, and QueryEquipment-equivalent equipment refresh.",
                    sourceEvidence: CombatGearSelfStatusEvidence,
                    suggestedNextOwner:
                        "No next gameplay owner for PhoenixBlessing/HolyProtection self-status runtime; continue with team aura spells 20390/20310.",
                    nextSeam: ImplementedAttachStatusSelfOrVisiblePlayerBuffSeam);
            }

            if (IsSnowAttachStatusSelfToggle(sort, spellId))
            {
                return CreateInfo(
                    sort,
                    spellId,
                    LegacyMagicSortCapability.AttachStatusBuff,
                    LegacyMagicModelingStatus.FullyModeled,
                    reason:
                        "This AttachStatus spell ID is modeled for the HealingSnow/ChillingSnow/FreezingPelter snow self-toggle family with resources, cooldowns, source interaction packet, mutual snow status clearing, status vector updates, spell animation, source-shaped spell experience update, FreezingPelter's add-side QueryEquipment-equivalent equipment refresh, and FreezingPelter removal's CallItems:false appearance suppression.",
                    sourceEvidence: AttachStatusEvidence,
                    suggestedNextOwner: "No next gameplay owner for the snow self-toggle family; broaden AttachStatus only for other spell IDs.",
                    nextSeam: ImplementedAttachStatusSelfOrVisiblePlayerBuffSeam);
            }

            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.AttachStatusBuff,
                LegacyMagicModelingStatus.FullyModeled,
                reason:
                    "This AttachStatus spell ID is modeled for self and visible-player targets with resources, cooldowns, status vector updates, spell animation, and duration-backed spell experience.",
                sourceEvidence: AttachStatusEvidence,
                suggestedNextOwner: "No next gameplay owner for this key spell; broaden AttachStatus only for other spell IDs.",
                nextSeam: ImplementedAttachStatusSelfOrVisiblePlayerBuffSeam);
        }

        if (IsPoisonStarAttachStatusTargetDebuff(sort, spellId))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.PoisonStatusRuntime,
                LegacyMagicModelingStatus.FullyModeled,
                reason:
                    "This AttachStatus spell ID is modeled for the source PoisonStar target debuff: resources first, visible-player target lookup, equipped Sturdiness avoidance, 80 percent success roll, target PoisonStar flag 0x77 with duration and RemoveOnDead true on success, target system message blocking drug use, source-shaped success/miss/empty spell animation, 250-point spell-progress tick except on Sturdiness avoidance, and timed expiry.",
                sourceEvidence: AttachStatusEvidence,
                suggestedNextOwner: "No next gameplay owner for PoisonStar direct AttachStatus debuff; continue adjacent AttachStatus cases or item-use blocking only if a source branch requires it.",
                nextSeam: ImplementedAttachStatusTargetDebuffSeam);
        }

        if (IsModeledAxeShadowCelestialDanceStatusSetter(sort, spellId))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.StateAndBuffRuntime,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    "WolfConquer models the bounded AxeShadow/CelestialDance runtime path: source-shaped resources/cooldown, 15940 Wildwind width-2 monster/player/SOB target damage, player BleedDamage=3000, AxeShadow flag 0xFB with Spell.Duration ArchiveSkill and AxeShadowPower from Damage3, 7030 caster-centered Bomb monster/player/SOB target damage, CelestialDance flag 0xFA with update type 0x77 and CelestialDancePower from DamageOnHuman % 1000, status vector updates, and duration-backed runtime expiry. Remaining parity for these spell IDs is exact shared CanAttack/global side effects beyond the modeled gates.",
                sourceEvidence: AxeShadowCelestialDanceStatusEvidence,
                suggestedNextOwner:
                    $"Magic combat owner: tighten exact shared CanAttack/global side effects for 15940 Wildwind and 7030 CelestialDance; keep the modeled target-damage and self-status paths bounded in their partials.",
                nextSeam: AxeShadowCelestialDanceStatusRuntimeSeam);
        }

        if (IsModeledBombPirteRuntimeSlice(sort, spellId))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.PirateSpecial,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    $"WolfConquer models the GaleBomb/BombPirte pirate-special runtime slice for spell 11070: radius-6 visible-monster physical damage, radius-6 visible-player physical damage divided by 2 with the BombPirte 11070 player Physical.OnPlayer WildPhoenix defender hook before /2 scaling, radius-6 visible-SOB damage, the shared level-based target cap (3/4/5), source-shaped monster/player pushback including blocked-first-tile original-coordinate behavior, player GuildWar endpoint rejection/pullback, map 1038/3868 SOB return, player protect/AttackStamp, Acalanatha/NatureShield player-target rune avoidance, {ModeledSharedCanAttackPlayerGates}, and {ModeledSharedCanAttackSobGates}. Remaining parity stays explicit: other pirate-special handlers, exact global/rune gaps, {RemainingCanAttackPlayerGaps}, and {RemainingCanAttackNpcGaps}.",
                sourceEvidence: $"{LegacyAttackHandlerEvidence} {BombPirteEvidence}",
                suggestedNextOwner:
                    $"Magic combat owner: finish BombPirte exact global/rune gaps, {RemainingCanAttackPlayerGaps}, and {RemainingCanAttackNpcGaps}; keep other pirate-special handlers tracked separately.",
                nextSeam: PirateSpecialRuntimeSeam);
        }

        if (IsModeledScurvyBombRuntimeSlice(sort, spellId))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.PirateSpecial,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    $"WolfConquer models the ScurvyBomb pirate-special runtime slice for spell 11040 with the shared visible-monster radius/level-based target caps, visible-player fatigue/status before Physical.OnPlayer, player-target WildPhoenix defender charge/archive override before ScurvyBomb *2 scaling, visible-SOB damage branch, {ModeledSharedCanAttackPlayerGates}, and {ModeledSharedCanAttackSobGates}. Remaining parity is exact rune gaps plus {RemainingCanAttackPlayerGaps} and {RemainingCanAttackNpcGaps}.",
                sourceEvidence: $"{LegacyAttackHandlerEvidence} {ScurvyBombEvidence}",
                suggestedNextOwner:
                    $"Magic combat owner: finish ScurvyBomb exact rune gaps, {RemainingCanAttackPlayerGaps}, and {RemainingCanAttackNpcGaps}; keep the other pirate-special handlers separate.",
                nextSeam: PirateSpecialRuntimeSeam);
        }

        if (IsModeledCannonBarrageRuntimeSlice(sort, spellId))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.PirateSpecial,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    $"WolfConquer models the CannonBarrage pirate-special runtime slice for spell 11050 with the status-gated visible-monster radius-6 physical damage branch, visible-player radius-6 physical damage divided by 11 with player-target WildPhoenix defender charge/archive damage override before the CannonBarrage /11 scaling step, source-shaped zero-damage WildPhoenix player target preservation, visible-SOB radius-6 physical damage branch, the no-flag no-animation guard after the shared resource/cooldown gate, the bounded XPList/OpenXpSkill/CannonBarrage flag transition slice that consumes XPList, resets XPCount to 0, opens or refreshes the CannonBarrage flag for the source 50-second timer window, removes other active OnXPSkill flags, expires the flag in the online runtime loop, skips damage animation, {ModeledSharedCanAttackPlayerGates}, and {ModeledSharedCanAttackSobGates}. Remaining parity is exact rune gaps plus {RemainingCanAttackPlayerGaps} and {RemainingCanAttackNpcGaps}.",
                sourceEvidence: $"{LegacyAttackHandlerEvidence} {CannonBarrageEvidence}",
                suggestedNextOwner:
                    $"Magic combat owner: finish CannonBarrage exact rune gaps, {RemainingCanAttackPlayerGaps}, and {RemainingCanAttackNpcGaps}.",
                nextSeam: PirateSpecialRuntimeSeam);
        }

        if (IsModeledPirateXpSkillRuntimeSlice(sort, spellId))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.PirateSpecial,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    $"WolfConquer models the PirateXpSkill pirate-special runtime slice for spell 11060 after CanUseSpell.Verified: XPList opens/refreshes BlackbeardsRage with OpenXpSkill(..., 50), resets XPCount to 0, keeps the source 50-second timer window, removes other active OnXPSkill flags, expires the flag in the online runtime loop, and returns without animation; the non-XPList branch hits radius-6 visible monsters, players with damage *2 after the PirateXpSkill/BlackbeardsRage 11060 player Physical.OnPlayer WildPhoenix defender hook before *2 scaling, SOB NPCs, {ModeledSharedCanAttackPlayerGates}, and {ModeledSharedCanAttackSobGates}. Remaining parity is exact rune/source/event gaps plus {RemainingCanAttackPlayerGaps} and {RemainingCanAttackNpcGaps}.",
                sourceEvidence: $"{LegacyAttackHandlerEvidence} {PirateXpSkillEvidence}",
                suggestedNextOwner:
                    $"Magic combat owner: finish PirateXpSkill exact rune/source/event gaps, {RemainingCanAttackPlayerGaps}, and {RemainingCanAttackNpcGaps}.",
                nextSeam: PirateSpecialRuntimeSeam);
        }

        if (IsModeledBlackSpotRuntimeSlice(sort, spellId))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.PirateSpecial,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    $"WolfConquer models the BlackSpot direct-target runtime slice for spell 11120: direct target selection by OpponentUID, source CanAttack target filtering with {ModeledSharedCanAttackPlayerGates} and {ModeledSharedCanAttackSobGates}, caster-to-target distance <= 2, physical monster/player/SOB damage including BlackSpot direct player Physical.OnPlayer damage with player-target WildPhoenix defender charge/archive damage override, alive visible-monster and alive visible-player BlackSpot state with duration from Spell.Duration, active BlackspotCreate opcode 2188 packets, source player expiry/inactive BlackspotCreate(false) runtime ticks, reciprocal active player replay, newly visible monster active replay, and the source-backed monster BlackSpot inactive packet lifecycle for expiry/death.",
                sourceEvidence: $"{LegacyAttackHandlerEvidence} {BlackSpotEvidence}",
                suggestedNextOwner:
                    $"Magic combat owner: finish exact BlackSpot rune edge effects, {RemainingCanAttackPlayerGaps}, and {RemainingCanAttackNpcGaps} before expanding BlackSpot-family parity claims.",
                nextSeam: PirateSpecialRuntimeSeam);
        }

        if (IsModeledAddBlackSpotRuntimeSlice(sort, spellId))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.PirateSpecial,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    $"WolfConquer models the AddBlackSpot monster/player-marking runtime slice for spell 11100/sort 62: it gates on learned BlackSpot 11120, applies source CanAttackMonster/CanAttackPlayer filtering with {ModeledSharedCanAttackPlayerGates}, marks visible monsters first and then visible players within the hard-coded 3/4/5 level budget and Attack.X/Y range, sends zero-damage animation targets, emits active LegacyBlackspotPacket opcode 2188 packets, sources BlackSpot duration from learned BlackSpot 11120, and relies on the shared BlackSpot packet lifecycle: player expiry/inactive plus active replay, monster active replay, and the source-backed monster inactive packets for expiry/death. Remaining parity is exact rune edge effects plus {RemainingCanAttackPlayerGaps}; the legacy AddBlackSpot handler has no SOB loop, so CanAttackNpc event-pole/statue checks are not part of this handler.",
                sourceEvidence: $"{LegacyAttackHandlerEvidence} {AddBlackSpotEvidence}",
                suggestedNextOwner:
                    $"Magic combat owner: finish AddBlackSpot exact rune edge effects and {RemainingCanAttackPlayerGaps} before expanding BlackSpot-family parity claims.",
                nextSeam: PirateSpecialRuntimeSeam);
        }

        if (IsModeledMoveLineRuntimeSlice(sort, spellId))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.PirateSpecial,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    "Player damage additionally models status-before-WildPhoenix returns, the bounded breakthrough branch, " +
                    "DefensiveStance, Infinity and the non-penetration 50% bonus before caller scaling; other Physical/Receive tails remain partial. " +
                    $"WolfConquer models the bounded MoveLine runtime slice for spell 11110/sort 61: it uses the 12-cell MoveCoords.CheckBladeTeampsCoords line from caster to Attack.X/Y, models ValidLocation truncation, chooses the final valid line cell for animation X/Y, applies the source CheckGuildWar final endpoint gate for map 1038 gate-crossing, updates the caster session position to the final line landing cell for normal/non-TideTrap casts, leaves the caster in place for equipped TideTrap plus learned spell 14250, sends the TideTrap bomb flag through spell animation field 9, creates/broadcasts a floor effect item 1080 named trap at the landing when targets were hit, can trigger/apply the bounded TideTrap floor-effect lifecycle for that item 1080 named trap with a source-shaped owner-to-trap line hit, uses the stored source floor item's MoveLine DBSkill for TideTrap detonation target checks/damage while emitting the 14250 trap animation, hits monsters, players, and SOBs within <= 1.5 of the MoveLine path, applies player physical spell damage with player-target WildPhoenix defender charge/archive damage override before the MoveLine * 12 / 16 scaling step, preserves source-shaped zero-damage WildPhoenix player targets for animation, applies SOB damage, emits active BlackSpot opcode 2188 packets for monster targets when learned BlackSpot 11120 procs/is modeled, uses {ModeledSharedCanAttackPlayerGates}, and uses {ModeledSharedCanAttackSobGates}. Shared BlackSpot player expiry/inactive and active replay behavior is modeled. Remaining gaps stay explicit: full GuildWar gate open lifecycle, {RemainingCanAttackPlayerGaps}, and {RemainingCanAttackNpcGaps}; this does not claim full GuildWar jump parity.",
                sourceEvidence: $"{LegacyAttackHandlerEvidence} {MoveLineEvidence}",
                suggestedNextOwner:
                    $"Magic combat owner: finish full GuildWar gate open lifecycle, {RemainingCanAttackPlayerGaps}, and {RemainingCanAttackNpcGaps} before claiming full MoveLine/GuildWar parity.",
                nextSeam: PirateSpecialRuntimeSeam);
        }

        if (IsKnownPirateSpecialSpellId(spellId) || IsPirateSpecialSort(sort))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.PirateSpecial,
                LegacyMagicModelingStatus.MissingRuntimeNotModeled,
                reason:
                    $"Legacy pirate handlers apply custom monster/player/SOB damage, XP-skill flags, BlackSpot state, or stamina fatigue; WolfConquer does not model this pirate sort/spell beyond the explicitly supported ScurvyBomb 11040 visible-monster, visible-player fatigue/status plus player-target WildPhoenix defender hook before *2 scaling, and visible-SOB damage slice, CannonBarrage 11050 status-gated visible-monster, visible-player /11, visible-SOB damage, no-flag guard, and XPList/OpenXpSkill/CannonBarrage flag-transition slice, PirateXpSkill 11060 XPList/BlackbeardsRage, radius-6 monster damage, player *2 damage with player-target WildPhoenix defender hook before *2 scaling, and SOB damage slice, BlackSpot 11120 direct visible-monster/player/SOB damage plus visible-monster/player active-state and shared player expiry/replay slice, AddBlackSpot 11100 learned-BlackSpot-gated visible-monster/player mark/active-packet slice, MoveLine 11110 line-hit damage/animation/reposition/active-packet plus TideTrap floor-effect trigger/apply lifecycle and ValidLocation/map-1038 GuildWar final-endpoint gate slice, and BombPirte 11070 radius-6 monster, player /2, SOB damage, shared target cap, player-target WildPhoenix defender hook before /2 scaling, source-shaped monster/player pushback with blocked-first-tile original-coordinate behavior, player GuildWar endpoint rejection/pullback, map 1038/3868 SOB return, player protect/AttackStamp, and Acalanatha/NatureShield rune avoidance. Where those supported pirate slices have player/SOB targets, WolfConquer shares {ModeledSharedCanAttackPlayerGates} and {ModeledSharedCanAttackSobGates}; explicit gaps remain {RemainingCanAttackPlayerGaps} and {RemainingCanAttackNpcGaps}.",
                sourceEvidence:
                    $"{LegacyAttackHandlerEvidence} Pirate cases dispatch to ScurvyBomb, CannonBarrage, BlackSpot, BombPirte, MoveLine, AddBlackSpot, and PirateXpSkill.",
                suggestedNextOwner:
                    $"Magic combat owner: add remaining pirate-special handlers and finish exact global/rune gaps, {RemainingCanAttackPlayerGaps}, and {RemainingCanAttackNpcGaps} for modeled pirate slices.",
                nextSeam: PirateSpecialRuntimeSeam);
        }

        if (IsFastBladeScentSwordLineSourceSilentSpell(sort, spellId))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.SourceSilentNoOp,
                LegacyMagicModelingStatus.SourceSilentNoOp,
                supported: false,
                reason:
                    "These later FastBlade/ScentSword-looking Line sort rows are source-silent after CanUseSpell.Verified: Line.cs has no switch case for this spell ID, so WolfConquer prepares the stamina-only source resources and then stops without spell animation or monster/player/SOB damage. They must not be classified as the generic visible-monster Line damage fallback.",
                sourceEvidence: $"{LegacyAttackHandlerEvidence} {FastBladeScentSwordLineSourceSilentEvidence}",
                suggestedNextOwner: "No gameplay owner until a live capture proves these Line.cs fall-through spell IDs have observable behavior beyond preparation.",
                nextSeam: SourceSilentNoOpSeam);
        }

        if (IsRoamerWildwindSourceSilentSpell(sort, spellId))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.SourceSilentNoOp,
                LegacyMagicModelingStatus.SourceSilentNoOp,
                supported: false,
                reason:
                    "Roamer spell 7040 is a Wildwind sort row, but the source Wildwind.cs switch has no case for this spell ID after CanUseSpell.Verified, so the cast falls through without spell animation or monster/player/SOB damage. It must not be classified as the generic missing Wildwind special-combat runtime.",
                sourceEvidence: $"{LegacyAttackHandlerEvidence} {RoamerWildwindSourceSilentEvidence}",
                suggestedNextOwner: "No gameplay owner until a live capture proves Roamer 7040 has observable behavior beyond the source fall-through.",
                nextSeam: SourceSilentNoOpSeam);
        }

        if (IsSourceSilentNoOpSort(sort))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.SourceSilentNoOp,
                LegacyMagicModelingStatus.SourceSilentNoOp,
                supported: false,
                reason:
                    "magictype.txt contains rows for this sort, but the decompiled MsgAttackPacket.ProcescMagic switch has no dispatch branch, so the legacy source is silent/no-op for normal player magic casts.",
                sourceEvidence: LegacyAttackHandlerEvidence,
                suggestedNextOwner: "No gameplay owner until a live capture proves this source-silent sort should have observable behavior.",
                nextSeam: SourceSilentNoOpSeam);
        }

        if (IsModeledFastBladeScentSwordManualLineRuntimeSlice(sort, spellId))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.SpecialCombatDamage,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    $"WolfConquer models the bounded FastBlade/ScentSword 1045/1046 ManualLine runtime slice for sort 96: source-shaped resource/cooldown/stamina ordering, visible monster/player/SOB line target loops, width-2 ManualLine target shape from caster to Attack.X/Y with strict range, guard-monster exclusion, source physical monster/player/SOB damage, the source Physical.OnPlayer 110% branch before player damage scaled by * 15 / 21, doubled monster animation damage only, runtime monster/player/SOB HP mutation, source Send plus JustMe self animation fanout, and monster/SOB-only spell/proficiency progress. Remaining parity is exact CanAttack side-effect edges, {RemainingCanAttackPlayerGaps}, {RemainingCanAttackNpcGaps}, and adjacent non-1045/1046 Line.cs cases. This claim is only for spell IDs 1045 and 1046 with sort 96 ManualLine; generic ManualLine and later FastBlade/ScentSword IDs such as 13450, 13460, and 18003 stay separate.",
                sourceEvidence: $"{LegacyAttackHandlerEvidence} {FastBladeScentSwordManualLineEvidence}",
                suggestedNextOwner:
                    $"Magic combat owner: tighten exact CanAttack side effects, {RemainingCanAttackPlayerGaps}, and {RemainingCanAttackNpcGaps}; keep adjacent Line.cs cases plus generic ManualLine and later FastBlade/ScentSword IDs separate.",
                nextSeam: FastBladeScentSwordManualLineRuntimeSeam);
        }

        if (IsModeledViperFangLineRuntimeSlice(sort, spellId))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.SpecialCombatDamage,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    $"WolfConquer models the bounded ViperFang 11005 Line runtime slice: source-shaped resource/cooldown/stamina ordering, visible monster/player/SOB line target loops, hard-coded width-1 Line target shape from caster to Attack.X/Y with strict range, source counter-hit map HitShoot increments for player hits, source physical monster/SOB damage, player Physical.OnPlayer damage with player-target WildPhoenix defender charge/archive damage override before the ViperFang Base.CalculateSoul and player /2 scaling step, runtime monster/player/SOB HP mutation, one normal MsgSpellAnimation fanout, and monster/SOB-only character/spell/proficiency progress. Remaining parity is exact CanAttack side-effect edges, {RemainingCanAttackPlayerGaps}, and {RemainingCanAttackNpcGaps}. This claim is only for spell ID 11005 with sort 14 Line; generic Line and adjacent Line.cs cases stay separate.",
                sourceEvidence: $"{LegacyAttackHandlerEvidence} {ViperFangLineEvidence}",
                suggestedNextOwner:
                    $"Magic combat owner: tighten exact ViperFang CanAttack side effects, including {RemainingCanAttackPlayerGaps} and {RemainingCanAttackNpcGaps}; keep adjacent Line.cs cases separate.",
                nextSeam: ViperFangLineRuntimeSeam);
        }

        if (IsModeledDragonTailLineRuntimeSlice(sort, spellId))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.SpecialCombatDamage,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    $"WolfConquer models the bounded DragonTail 11000 Line runtime slice: source-shaped resource/cooldown/stamina ordering, visible monster/player/SOB line target loops, hard-coded width-0 Line target shape from caster to Attack.X/Y with strict range, source counter-hit map HitShoot increments for monster hits, source physical monster/player/SOB damage, player-target WildPhoenix defender charge/archive damage override before the DragonTail soul step, runtime monster/player/SOB HP mutation, one normal MsgSpellAnimation fanout, and monster/SOB-only character/spell/proficiency progress. Remaining parity is exact CanAttack side-effect edges, {RemainingCanAttackPlayerGaps}, and {RemainingCanAttackNpcGaps}. This claim is only for spell ID 11000 with sort 14 Line; generic Line and adjacent Line.cs cases stay separate.",
                sourceEvidence: $"{LegacyAttackHandlerEvidence} {DragonTailLineEvidence}",
                suggestedNextOwner:
                    $"Magic combat owner: tighten DragonTail exact CanAttack side effects, including {RemainingCanAttackPlayerGaps} and {RemainingCanAttackNpcGaps}; keep adjacent Line.cs cases separate.",
                nextSeam: DragonTailLineRuntimeSeam);
        }

        if (IsModeledDragonSlashLineRuntimeSlice(sort, spellId))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.SpecialCombatDamage,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    $"WolfConquer models the bounded DragonSlash 12350 Line runtime slice: source-shaped resource/cooldown/stamina ordering, visible monster/player/SOB line target loops, source constructor range-10 width-0 Line target shape from caster to Attack.X/Y while target distance still gates by Spell.Range, source counter-hit map HitShoot increments for monster hits, source physical monster/SOB damage, source physical player damage scaled by * 10 / 18, player-target WildPhoenix defender charge/archive damage override before the DragonSlash soul/scaling step, source-shaped zero-damage WildPhoenix player target preservation for animation, runtime monster/player/SOB HP mutation, one normal MsgSpellAnimation fanout, and monster/SOB-only character/spell/proficiency progress using source-shaped Monster.Execute/Npc.Execute return values. Remaining parity is exact CanAttack side-effect edges, {RemainingCanAttackPlayerGaps}, and {RemainingCanAttackNpcGaps}. This claim is only for spell ID 12350 with sort 14 Line; generic Line and adjacent Line.cs cases stay separate.",
                sourceEvidence: $"{LegacyAttackHandlerEvidence} {DragonSlashLineEvidence}",
                suggestedNextOwner:
                    $"Magic combat owner: tighten DragonSlash exact CanAttack side effects, including {RemainingCanAttackPlayerGaps} and {RemainingCanAttackNpcGaps}; keep adjacent Line.cs cases separate.",
                nextSeam: DragonSlashLineRuntimeSeam);
        }

        if (IsModeledThunderStrikerLineRuntimeSlice(sort, spellId))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.SpecialCombatDamage,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    $"WolfConquer models the bounded ThunderStriker/ThunderBlast 15810 Line runtime slice: source-shaped resource/cooldown/stamina ordering, ThunderStriker class gate after resources, visible monster/player/SOB line target loops, source Spell.Range width-1 Line target shape from caster to Attack.X/Y, guard-monster exclusion, source physical monster runtime damage with doubled monster animation damage only, source physical player damage scaled by * 53 / 100, player-target WildPhoenix defender charge/archive damage override before the ThunderStrikerLine soul/scaling step, source-shaped zero-damage WildPhoenix player target preservation for animation, source SOB damage scaled by * 2, runtime monster/player/SOB HP mutation, one normal MsgSpellAnimation fanout, and monster/SOB-only character/spell/proficiency progress. Remaining parity is exact CanAttack side-effect edges, {RemainingCanAttackPlayerGaps}, and {RemainingCanAttackNpcGaps}. This claim is only for spell ID 15810 with sort 14 Line; generic Line and adjacent Line.cs cases stay separate.",
                sourceEvidence: $"{LegacyAttackHandlerEvidence} {ThunderStrikerLineEvidence}",
                suggestedNextOwner:
                    $"Magic combat owner: tighten exact ThunderStriker CanAttack side effects, including {RemainingCanAttackPlayerGaps} and {RemainingCanAttackNpcGaps}; keep adjacent Line.cs cases separate.",
                nextSeam: ThunderStrikerLineRuntimeSeam);
        }

        if (IsModeledSpeedGunLineRuntimeSlice(sort, spellId))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.SpecialCombatDamage,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    $"WolfConquer models the bounded SpeedGun 1260 Line runtime slice: source-shaped resource/cooldown/stamina ordering, visible monster/player/SOB line target loops, width-1 Line target shape from caster to Attack.X/Y, hard-coded player distance < 3, no guard-monster exclusion, bounded counter-hit map HitShoot increments for source player hits, source physical monster/player/SOB damage, player-target WildPhoenix defender charge/archive damage override before the soul step, runtime monster/player/SOB HP mutation, animation only when targets are hit, and monster/SOB-only character/spell/proficiency progress. Remaining parity is exact CanAttack side-effect edges, {RemainingCanAttackPlayerGaps}, and {RemainingCanAttackNpcGaps}. This claim is only for spell ID 1260 with sort 14 Line; generic Line and adjacent Line.cs cases stay separate.",
                sourceEvidence: $"{LegacyAttackHandlerEvidence} {SpeedGunLineEvidence}",
                suggestedNextOwner:
                    $"Magic combat owner: tighten SpeedGun exact CanAttack side effects, including {RemainingCanAttackPlayerGaps} and {RemainingCanAttackNpcGaps}; keep adjacent Line.cs cases separate.",
                nextSeam: SpeedGunLineRuntimeSeam);
        }

        if (IsModeledHookMoonLineRuntimeSlice(sort, spellId))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.SpecialCombatDamage,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    $"WolfConquer models the bounded HookMoon 15930 Line runtime slice: source-shaped resource/cooldown ordering, visible monster/player/SOB line target loops, source constructor range-11 width-2 Line target shape from caster to Attack.X/Y, guard-monster exclusion, source physical monster runtime damage * 2 / 3 with doubled monster animation damage only, source physical player damage scaled by * 7 / 16, player-target WildPhoenix defender charge/archive damage override before the HookMoon soul/scaling step, source-shaped zero-damage WildPhoenix player target preservation for HookMoon target/caster status and animation, source SOB damage scaled by * 50 / 100, runtime monster/player/SOB HP mutation, one normal MsgSpellAnimation fanout, source HookMoon target/caster status attributes with ArchiveSkill updates, and monster/SOB-only character/spell/proficiency progress. Remaining parity is exact HookMoon timed status expiry/duplicate AddFlag edges, exact CanAttack side-effect edges, {RemainingCanAttackPlayerGaps}, and {RemainingCanAttackNpcGaps}. This claim is only for spell ID 15930 with sort 14 Line; generic Line and adjacent Line.cs cases stay separate.",
                sourceEvidence: $"{LegacyAttackHandlerEvidence} {HookMoonLineEvidence}",
                suggestedNextOwner:
                    $"Magic combat owner: tighten HookMoon timed status expiry/duplicate AddFlag behavior and exact CanAttack side effects, including {RemainingCanAttackPlayerGaps} and {RemainingCanAttackNpcGaps}; keep adjacent Line.cs cases separate.",
                nextSeam: HookMoonLineRuntimeSeam);
        }

        if (IsModeledRageOfWarLineRuntimeSlice(sort, spellId))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.SpecialCombatDamage,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    "Initial and delayed animations retain every accepted player even when ordinary scaled damage rounds to zero. " +
                    $"WolfConquer models the bounded RageOfWar 12930 Line runtime slice: source-shaped resource/cooldown/stamina ordering, visible monster/player/SOB line target loops, hard-coded distance < 11 target gate, width-1 monster/SOB target shape, width-0 player target shape, guard-monster exclusion, bounded counter-hit map HitShoot increments for source initial player hits, source physical monster/SOB damage, source physical player damage from the initial player Physical.OnPlayer path scaled by * 10 / 13 with player-target WildPhoenix defender charge/archive damage override before the initial RageOfWar Base.CalculateSoul and player *10/13 scaling step, source-shaped zero-damage WildPhoenix player target preservation, runtime monster/player/SOB HP mutation, one normal MsgSpellAnimation fanout, source 1500 floor-effect item create/broadcast plus animation append metadata, delayed floor-effect radius damage with bomb flag and RemoveEffect, delayed floor player Physical.OnPlayer damage with player-target WildPhoenix defender charge/archive damage override before the delayed RageOfWar player *60/100 scaling step, and monster/SOB-only character/spell/proficiency progress. Remaining RageOfWar parity is exact CanAttack side-effect edges, {RemainingCanAttackPlayerGaps}, and {RemainingCanAttackNpcGaps}. This claim is only for spell ID 12930 with sort 14 Line; generic Line and adjacent Line.cs cases stay separate.",
                sourceEvidence: $"{LegacyAttackHandlerEvidence} {RageOfWarLineEvidence}",
                suggestedNextOwner:
                    $"Magic combat owner: tighten exact RageOfWar CanAttack side effects, including {RemainingCanAttackPlayerGaps} and {RemainingCanAttackNpcGaps}; keep adjacent Line.cs cases separate.",
                nextSeam: RageOfWarLineRuntimeSeam);
        }

        if (IsArcherCrescentShadowPreKineticProcSpell(sort, spellId))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.VisibleMonsterDamage,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    "WolfConquer models the direct sort-5/Bomb visible-monster damage slice for CrescentShadow 18750 and the bounded source-backed Archer AttackID passive proc: source direct-target Base.GetDistance <= 18 caller gate, MyCombatGear.CheckCombatGear consumption before the passive hook, learned 18750 plus Base.Rate(30), distance <= 2, player/monster/SOB magic routing, and ordering before the later KineticSpark gate sequence. Remaining CrescentShadow parity is exact passive caller side effects, exact CanAttack edge side effects, and broader Bomb spell/runtime edges.",
                sourceEvidence: $"{LocalVisibleMonsterEvidence} {ArcherPreKineticProcEvidence}",
                suggestedNextOwner:
                    "Magic combat owner: tighten exact Archer CrescentShadow 18750 passive caller side effects, exact CanAttack edges, and broader Bomb spell/runtime edges after the bounded pre-Kinetic hook.",
                nextSeam: ArcherPreKineticProcRuntimeSeam);
        }

        if (IsSupportedPerimeterVisibleMonsterAttackSpell(sort, spellId))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.VisibleMonsterDamage,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    "WolfConquer models the bounded Perimeter runtime for exactly spell IDs 12210, 12220, 12580, 12590, 12740, 12750, 13040, 14570, 14580, 15670, and 15680. The six D13 default-Fan IDs model monster/player/SOB damage and empty-target animation; 13040 and 15670/15680 now use the exact bounded mixed monster/player/SOB runtime with source-shaped anchoring, Base.CalculateSoul's exact no-op behavior, exact decompiled 140/7 compatibility, exact decompiled 500/5 compatibility, Trojan target scaling 10/7, non-Trojan 10/11 scaling, monster/SOB-only progress before one final animation, no-target silence, and map-1505 behavior; player/SOB damage is also modeled for 14570/14580. Their ordinary no-pending ProcescMagic route uses the source-default 500 ms attack cadence, while the matching pending weapon-proc/D13 route consumes the selector and receives attack-stamp bypass only; a mismatched pending selector remains rejected. Exact source OnAutoAttack interaction suppression, broader shared CanAttack side effects, deeper UpdateSpell exactness, and remaining wire nuances stay fenced.",
                sourceEvidence: $"{LocalVisibleMonsterEvidence} {PerimeterVisibleMonsterEvidence}",
                suggestedNextOwner:
                    "Magic combat owner: tighten exact Perimeter OnAutoAttack provenance, broader CanAttack side effects, and remaining wire nuances without broadening unrelated spell IDs.",
                nextSeam: PerimeterSourceParityDebtSeam);
        }

        if (IsModeledStraightFist12760RuntimeSlice(sort, spellId))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.SpecialCombatDamage,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    $"WolfConquer models the bounded StraightFist 12760 runtime: source-shaped resource/cooldown preparation and counter-map MisShoot, strict distance < 5 line targeting with soul-selected width 0/1, guard-monster exclusion, mixed visible monster/player/SOB physical damage, runtime HP mutation, counter-map HitShoot, monster/SOB character and spell progress, and source-shaped empty-target animation. Remaining parity is exact Base.CalculateSoul and shared CanAttack side-effect edges, {RemainingCanAttackPlayerGaps}, and {RemainingCanAttackNpcGaps}.",
                sourceEvidence: $"{LegacyAttackHandlerEvidence} {StraightFistEvidence}",
                suggestedNextOwner:
                    "Magic combat owner: tighten StraightFist 12760 exact soul and shared CanAttack side-effect edges without broadening adjacent sort-76 spells.",
                nextSeam: StraightFist12760RuntimeSeam);
        }

        if (IsModeledStraightFist12230SourceSilentRuntimeSlice(sort, spellId))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.SpecialCombatDamage,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    "WolfConquer models the source StraightFist 12230 resource/cooldown and counter-map MisShoot ordering followed by the spell-ID switch return without damage or animation. The exact D13 automatic-selector adapter deliberately treats 12230 as a normal physical fallback because this source-silent branch is not an executable automatic proc animation.",
                sourceEvidence: $"{LegacyAttackHandlerEvidence} {StraightFistEvidence}",
                suggestedNextOwner:
                    "Protocol/runtime owner: keep D13 selector 12230 fenced to physical fallback unless exact client wire evidence proves a different executable automatic-proc contract.",
                nextSeam: StraightFist12760RuntimeSeam);
        }

        if (IsModeledRectangle13060RuntimeSlice(sort, spellId))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.SpecialCombatDamage,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    "WolfConquer models the bounded exact sort 87/Rectangle plus spell 13060 runtime through the existing CombatGear direct-line owner: selected player/monster anchoring, width-2 line targeting, guard-monster exclusion, mixed visible monster/player/SOB physical damage, per-target interaction frames before progress and the final spell animation, and monster/SOB progress. This claim is only for Rectangle spell 13060; generic Rectangle and every other Rectangle spell ID stay unsupported.",
                sourceEvidence: $"{D13RectangleSelectorWireEvidence} {RectangleSelectorGameplayEvidence}",
                suggestedNextOwner:
                    "CombatGear runtime owner: tighten remaining exact CanAttack and side-effect edges for Rectangle 13060 without broadening generic Rectangle or adjacent spell IDs.",
                nextSeam: Rectangle13060RuntimeSeam);
        }

        if (IsModeledRectangle15690RuntimeSlice(sort, spellId))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.SpecialCombatDamage,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    "WolfConquer models the bounded exact sort 87/Rectangle plus spell 15690 runtime through the existing CombatGear sector owner: selected player/monster anchoring, a 100-degree distance-5 sector, mixed visible monster/player/SOB physical damage, per-target interaction frames before progress and the final spell animation, and monster/SOB progress. This claim is only for Rectangle spell 15690; generic Rectangle and every other Rectangle spell ID stay unsupported.",
                sourceEvidence: $"{D13RectangleSelectorWireEvidence} {RectangleSelectorGameplayEvidence}",
                suggestedNextOwner:
                    "CombatGear runtime owner: tighten remaining exact CanAttack and side-effect edges for Rectangle 15690 without broadening generic Rectangle or adjacent spell IDs.",
                nextSeam: Rectangle15690RuntimeSeam);
        }

        if (IsModeledEagleEyeDirectAttackRuntimeSlice(sort, spellId))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.SpecialCombatDamage,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    "WolfConquer models the bounded EagleEye 11030 DirectAttack runtime: source-shaped resource/cooldown preparation, direct visible monster/player/SOB physical damage, shared target gates, primary-player plus exact width-zero in-line secondary-player Physical.OnPlayer defense ordering with ShurikenVortex and ManiacDance before WildPhoenix, caller-level 14/22 primary scaling after soul calculation so defended source damage one becomes zero, deliberately unscaled secondary damage so defended source damage one remains one, per-target learned-defender 16350 BackDmg with nested-before-shared-animation ordering and attacker/defender state carry, non-BlackSpot 20-second countdown arming, BlackSpot learned-11130 rate-gated consume with self animation and ZeroEagleEye cold-time reset, consumed self target damage 11135, source-shaped SOB target animation without countdown mutation, runtime HP mutation, monster spell progress, and SOB character/spell progress. Remaining parity includes exact Base.CalculateSoul and CanAttack side-effect edges.",
                sourceEvidence: $"{LegacyAttackHandlerEvidence} {EagleEyeDirectAttackEvidence}",
                suggestedNextOwner:
                    $"Magic combat owner: tighten exact Base.CalculateSoul and CanAttack side effects, including {RemainingCanAttackPlayerGaps} and {RemainingCanAttackNpcGaps}.",
                nextSeam: EagleEyeDirectAttackRuntimeSeam);
        }

        if (IsModeledMountControlRuntimeSlice(sort, spellId))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.StateAndBuffRuntime,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    "WolfConquer models the bounded mount-control runtime for Riding 7001, Spook 7002, and WarCry 7003: source resource/cooldown ordering, Riding state/map/tournament gates after resources, Ride status toggle, 2592000-second timed expiry, death-time RemoveOnDead flag and metadata cleanup, status fanout and self animation, available MaxVigor restoration, Spook clicked-mounted-player steed plus/progress and perfection fallback, WarCry all-visible-mounted-player plus/progress comparison with no sector filter, dismount status fanout, zero target stamina, source interaction packets, and no spell progress. Remaining parity is authoritative MaxVigor when the local snapshot lacks it and exact OnAutoAttack interaction suppression.",
                sourceEvidence: $"{LegacyAttackHandlerEvidence} {MountControlEvidence}",
                suggestedNextOwner:
                    "Mount runtime owner: source authoritative MaxVigor from character status and add exact OnAutoAttack state when those local runtime fields are modeled.",
                nextSeam: MountControlRuntimeSeam);
        }

        if (IsVisibleMonsterDamageSupportedSort(sort))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.VisibleMonsterDamage,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    "WolfConquer models the visible-monster damage slice for this sort, but the legacy handlers also include player and/or SOB target branches that are not modeled.",
                sourceEvidence: $"{LocalVisibleMonsterEvidence} {LegacyAttackHandlerEvidence}",
                suggestedNextOwner: "Magic runtime combat owner: add player-target and SOB-target magic paths for these already-partial sorts.",
                nextSeam: PlayerTargetAndSobMagicRuntimeSeam);
        }

        if (sort == LegacyMagicSort.AttachStatus)
        {
            if (spellId == 0)
            {
                return CreateInfo(
                    sort,
                    spellId,
                    LegacyMagicSortCapability.AttachStatusBuff,
                    LegacyMagicModelingStatus.PartiallyModeled,
                    reason:
                        "AttachStatus sort 6 has many source cases; WolfConquer currently models the target debuff PoisonStar 6002, the five key status-buff spell IDs 1075, 1085, 1090, 1095, and 30000, the Intensify/SpiritFocus focus-charge pair 9000/11670 through runtime promotion into Focused plus decompiled doFocus/AjustAttack physical damage for monster, SOB, raw direct player, and shared player-target physical damage, the SuperCyclone XPList activation spell 11970, RevengeTail 13030 self toggle/counter with RevengeGale boost and timed expiry, Infinity/Fly 14380 self flags with Infinity damage fields and countdown update, HawksAmbition 15920 self status with interaction echo and 500 spell-progress tick, InfiniteMist 16380 direct target status with self-shaped animation, MudWall 16440/16450 self status with ArchiveSkill countdown and equipment refresh, the snow self-toggle cases 12950, 12960, and 13020 including FreezingPelter's add-side QueryEquipment-equivalent equipment refresh and removal CallItems:false appearance suppression, plus CombatGearAttack initial self-status cases 20300, 20350, and 20370. WildPhoenix generic magic defender charge/archive refresh and qualifying attacker damage bonus are modeled, while the wider Physical/Range calculator call-site coverage remains separate debt.",
                    sourceEvidence: $"{AttachStatusEvidence} {CombatGearSelfStatusEvidence}",
                    suggestedNextOwner: "Magic support owner: implement the remaining AttachStatus switch cases by spell ID.",
                    nextSeam: AttachStatusRemainingRuntimeSeam);
            }

            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.AttachStatusBuff,
                LegacyMagicModelingStatus.MissingRuntimeNotModeled,
                reason:
                    "WolfConquer does not map this AttachStatus spell ID to a legacy status flag or spell-specific behavior.",
                sourceEvidence: AttachStatusEvidence,
                suggestedNextOwner: "Magic support owner: add this AttachStatus case from decompiled AttachStatus.cs.",
                nextSeam: AttachStatusRemainingRuntimeSeam);
        }

        if (IsReviveSupportSort(sort, spellId))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.ReviveSupport,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    "WolfConquer models magic-cast Revive/Pray visible-player revive support with target selection, immediate revive side effects, animation fanout, XPList removal, and spell XP; Mercy Prayer still needs the remaining source guild, watcher-map, and Taoist epic bonus details.",
                sourceEvidence:
                    $"{LegacyAttackHandlerEvidence} DetachStatus.cs cases 1050, 1100, and 16610 call Player.Revive and then update spell experience.",
                suggestedNextOwner: "Support/runtime owner: finish Mercy Prayer same-guild eligibility, watcher-map system message, and Taoist epic bonus status fanout.",
                nextSeam: MagicReviveSupportRuntimeSeam);
        }

        if (IsRecruitSupportSort(sort))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.SupportHealing,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    "WolfConquer models Recruit healing for self, visible players for 1005/1190, visible monsters, and nearby team fanout with resources, cooldowns, hitpoint mutation, animation, and spell XP.",
                sourceEvidence:
                    $"{LegacyAttackHandlerEvidence} Recruit.cs adjusts target hitpoints and sends MsgSpellAnimation targets.",
                suggestedNextOwner: "Support/runtime owner: broaden remaining support sorts such as revive, XP-skill, and summon pet.",
                nextSeam: HealingSupportRuntimeSeam);
        }

        if (IsAddManaSupportSort(sort, spellId))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.ManaRestoreSupport,
                LegacyMagicModelingStatus.FullyModeled,
                reason:
                    "WolfConquer models AddMana spell 1195 for self mana restore with resource checks, cooldown, mana update, spell animation, and source-style spell experience from spell power.",
                sourceEvidence: $"{LegacyAttackHandlerEvidence} {AddManaEvidence}",
                suggestedNextOwner: "No next gameplay owner for AddMana 1195; broaden other state/support magic sorts after live skill sweeps.",
                nextSeam: ManaRestoreRuntimeSeam);
        }

        if (IsXpSkillSort(sort, spellId))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.XpSkill,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    "WolfConquer models the main live DispatchXp paths for Roar and DragonRoar: XPList gating/removal, XPCount mutation, teammate stamina restore, empty DragonRoar animation, spell animation, and spell XP. Remaining parity is in broader global map/precheck ordering and source-exact team membership edge cases.",
                sourceEvidence:
                    $"{LegacyAttackHandlerEvidence} DispatchXp.cs handles DragonRoar and XPList-targeted XPCount updates.",
                suggestedNextOwner: "XP-skill owner: finish source-exact global prechecks and team membership/range edge cases after broader magic-target parity.",
                nextSeam: XpSkillRuntimeSeam);
        }

        if (IsModeledSpecialXpSkillActivationSlice(sort, spellId))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.XpSkill,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    "WolfConquer models this special XP skill activation branch: BladeFlurry spell 11610 consumes XPList and opens or refreshes BladeFlurry for 50 seconds; ChainBolt spell 10309 consumes XPList and opens or refreshes ChaintBolt for 50 seconds; DragonCyclone spell 12290 requires XPList, removes XPList, opens DragonCyclone using DBSpell.Duration, and sends the source self animation; Omnipotence spell 12860 requires XPList, fills stamina to the 100/150 HeavenBlessing maximum, removes XPList, and opens Omnipotence for 40 seconds. BladeFlurry, ChainBolt, DragonCyclone, and Omnipotence also model their non-XP runtime branches. BladeFlurry and Omnipotence player targets use Range.OnPlayer: BladeFlurry uses fixed 185 percent damage inside distance three and otherwise the row damage percent; ShurikenVortex or ManiacDance returns Base.CalculateExtraAttack from zero using attacker PhysicalDamageIncrease and defender PhysicalDamageDecrease; WildPhoenix remains a fixed one-damage return before DefensiveStance; DefensiveStance applies its 40 percent Block return before BackDmg; and normal eligible hits call BackDmg before receive/progress output and the one shared animation.",
                sourceEvidence:
                    $"{LegacyAttackHandlerEvidence} {SpecialXpSkillActivationEvidence} " +
                    "BladeFlurry.cs non-XP branch iterates visible Monster/Player/SobNpc targets within Base.GetDistance(caster,target) <= DBSpell.Range and uses Range.OnMonster/OnPlayer/OnNpcs; Range.cs applies fixed 185 percent player damage inside distance three, returns ShurikenVortex/ManiacDance damage through Base.CalculateExtraAttack(0, PhysicalDamageIncrease, PhysicalDamageDecrease) using attacker PhysicalDamageIncrease and defender PhysicalDamageDecrease before WildPhoenix's fixed one-damage return, then applies its remaining defense returns and BackDmg.Calculate(..., ranged: true), and BladeFlurry sends progression before the shared animation. " +
                    "ChainBolt.cs non-XP branch targets the clicked primary Monster/Player/SobNpc, sets MsgSpell.X/Y to the primary target center, chains only same-type visible targets with Base.GetDistance(primary,target) < DBSpell.Range / 2, uses Magic.OnMonster/OnPlayer/OnNpcs, and sends animation only when targets exist. " +
                    "DragonCyclone.cs active non-XP branch requires ContainFlag(MsgUpdate.Flags.DragonCyclone), computes movement coords toward Attack.X/Y with CheckBladeTeampsCoords and distance Math.Min(DBSpell.Range, caster distance to target point), lands on the last coord, gates landing through CheckGuildWar, calls user.Shift, filters monsters by Base.GetDistance(Attack.X,Attack.Y,target) <= DBSpell.Range plus MoveCoords.InRange(target, 2, coord), filters players and SobNpc targets by Base.GetDistance(Attack.X,Attack.Y,target) <= 6 plus MoveCoords.InRange(target, 2, coord), uses Physical.OnMonster/OnPlayer/OnNpcs, applies experience/spell update, and sends animation. " +
                    "Omnipotence.cs non-XP branch iterates visible Monster/Player/SobNpc targets within Base.GetDistance(caster,target) < DBSpell.Range and uses Physical.OnMonster, Range.OnPlayer, and Physical.OnNpcs. Range.cs returns ShurikenVortex/ManiacDance damage through Base.CalculateExtraAttack(0, PhysicalDamageIncrease, PhysicalDamageDecrease) using attacker PhysicalDamageIncrease and defender PhysicalDamageDecrease before WildPhoenix's fixed one-damage return, then applies DefensiveStance's 40 percent Block return before BackDmg.Calculate(..., ranged: true); Omnipotence.cs sends the shared animation after the target loops, experience update, and spell update.",
                suggestedNextOwner:
                    "Magic combat owner: no special XP active runtime branch remains identified here; continue broader class skill ownership, class-skill formulas, and damage-formula parity.",
                nextSeam: XpSkillRuntimeSeam);
        }

        if (IsSummonPetSort(sort))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.SummonPet,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    "WolfConquer now executes the main live CallPet paths for ShadowClone and Thundercloud: clone removal, clone/pet projection state, pet info, role spawn, action 134/135 fanout, spell animation, spell XP, DoubleThunder rune detection, source-radius-5 nearby Thundercloud placement, source-lifetime Thundercloud action-135 expiry cleanup, and source owner-death Thundercloud cleanup. Source `m_ID == 3843` Thundercloud floor monsters do not run the `m_ID == 940` periodic damage branch. Other CallPet sort-23 spell IDs fall through after the legacy CanUseSpell resource/cooldown gate without summon visuals, and sort 72 Summon rows are source-silent because ProcescMagic has no Summon branch.",
                sourceEvidence:
                    $"{LegacyAttackHandlerEvidence} CallPet.cs cases 12090 and 12840 create clones, pet info, monster/floor projections, and revive-monster action frames; other CallPet spell IDs have no switch case after CanUseSpell.Verified. PoolProcesses.cs removes Thundercloud floor m_ID 3843 with action 135 at RemoveFloor while damage pulses are limited to m_ID 940; Player.cs removes owned Thunderclouds with action 135 on death; MsgAttackPacket.ProcescMagic has no MagicSort.Summon case for sort 72 rows.",
                suggestedNextOwner: "No source-backed summon owner remains for sort 72 or other sort-23 fallthrough rows; only reopen pet attack behavior behind a live capture, otherwise continue boss rewards and special death branches.",
                nextSeam: SummonPetRuntimeSeam);
        }

        if (IsModeledLayTrapRuntimeSlice(sort, spellId))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.FloorTrapRuntime,
                    LegacyMagicModelingStatus.PartiallyModeled,
                    reason:
                    "WolfConquer models the direct LayTrap runtime slices for TwilightDance 12070, DaggerStorm 11600, SpaceLeap 15620, and BloomofDeath 14720, plus AuroraLotus 12370 and FlameLotus 12380 visual floor effects, WaterShockwave 16310/16320 source-shaped floor placement plus delayed repeat pulse and due final line damage/pushback, DustDetachment 16460/16470 floor placement plus cast-time SOB damage, repeat monster/player pulse, and remove-only final invoke, SickleWind 16500/16510 floor placement plus cast-time SOB damage, hard-coded radius-5 repeat monster/player pulse, self-only cast animation, and remove-only final invoke, FlameofDestruction 16540/16550 floor placement plus repeat pulse and remove-only final invoke, source-case delayed floor traps 12550/12570, target floor-effect spell 12990, and ThunderStriker/SkyFall 15770 direct radius damage: TwilightDance uses source-shaped LayTrapThree floor-effect projection, three trap FloorSpell/MsgFloorItem effects named trap, 300ms trigger-delay stepping, runtime floor spell trigger/removal, monster/player/SOB target checks within distance <= 3, 70% player damage, ActionQuery Type 434 removal, and removal broadcast within distance <= 18; DaggerStorm 11600 creates one source-shaped visual floor effect named trap at Attack.X/Y, uses item 50/41/42 by spell soul, color/FlowerType 11, caster owner/guild metadata, 4000ms visual expiry with source removal rewriting soul item 41/42 back to 50, caster spell animation damage 0, and 10000 spell experience; SpaceLeap 15620 runs the Water/qualifier/blocked-map source gate after resources, creates one source-shaped visual floor effect named SpaceLeap at Attack.X/Y, item 930, color 17, FlowerType 3, Life=MaxLife=5, caster owner/guild metadata, 17000ms visual expiry, team ActionQuery Type 451 RemainTime=11, caster spell animation Damage=0 Hit=1 MoveY=casterY, and 10000 spell experience; AuroraLotus 12370 requires TaoistPower >= 9 and a non-NoRev map after resources, sends the self interaction, creates item 930 color 13 named AuroraLotus with Life=MaxLife=25, FlowerType=2, source TqTimer now+7s, empty caster spell animation, 10000 spell experience, TaoistPower reset, AppendIcon flag 172 with damage 0, FullPowerWater removal, and 7000ms visual expiry; FlameLotus 12380 requires TaoistPower >= 9 after resources without the NoRev gate, creates item 940 color 13 named FlameLotus with the same Life/FlowerType/TqTimer/animation/experience/Tao reset flow, AppendIcon flag 173, FullPowerFire removal, and 7000ms visual expiry; WaterShockwave 16310/16320 creates item 2560 color 23 named STR_TRAP_ID_2537@@, keeps packet X/Y at the caster, stores Attack.X/Y in OwnerX/Y for the later line, preserves guild/owner metadata, sends an empty caster animation at Attack.X/Y, stores the 3000ms invoke plus 1000ms repeat metadata, emits bomb=1 repeat radius pulses every 1000ms before due from the floor UID at packet X/Y, emits the due final bomb=2 line target damage and pushback from the floor UID at OwnerX/Y, applies source WaterShockwave player damage scaling Damage3 % 1000 / 180, and removes the floor effect; DustDetachment 16460/16470 creates item 2570 color 14 at Attack.X/Y, stores caster OwnerX/Y and guild metadata with ItemOwnerUID=0, sends a caster animation at Attack.X/Y with cast-time SOB targets within radius <= 6 from caster X/Y, stores the 5000ms invoke plus 1000ms repeat metadata with DBSkill.MaxTargets radius, emits bomb=1 repeat radius pulses from the floor UID for monsters/players only, displays monster damage as Base.CalculateSoul(damage, LevelHu) / 4 and player damage unchanged, removes only with RemoveEffect at invoke time using the caster-origin viewer radius, and runs modeled DustSigilStunn gear-contributor player stun behavior during the repeat pulse branch; SickleWind 16500/16510 creates item 2540 color 14 at Attack.X/Y, stores caster OwnerX/Y and guild metadata with ItemOwnerUID=0, sends a self-only caster animation at Attack.X/Y with cast-time SOB targets within radius <= 6 from caster X/Y, stores the 3000ms invoke plus 1000ms repeat metadata with hard-coded radius 5, emits bomb=1 repeat radius pulses from the floor UID for monsters/players only, displays monster damage as Base.CalculateSoul(damage, LevelHu) * 55 / 100 and player damage as Base.CalculateSoul(damage, LevelHu), removes only with RemoveEffect at invoke time using the caster-origin viewer radius, and has no SOB repeat branch; FlameofDestruction 16540/16550 creates item 2530 color 14 at Attack.X/Y, stores caster OwnerX/Y and guild metadata with ItemOwnerUID=0, sends an empty caster animation at Attack.X/Y, stores the 4000ms invoke plus 1000ms repeat metadata, emits bomb=1 repeat radius pulses from the floor UID within DBSkill.MaxTargets for monsters/players/SOBs, applies source player DamageOnHuman clamping and SOB 10% damage scaling, removes only with RemoveEffect at invoke time using the caster-origin viewer radius, and runs modeled FlameSigilRapid gear-contributor equipment bonus behavior during the repeat pulse branch; LayTrap 12550 creates four source-shaped item 1001390 floor effects from RandomFourLayTraps, sends the self interaction, queues floor UID animation targets, applies 10000 spell experience, triggers after 4000ms with radius <= 3, and removes within distance <= 18; LayTrap 12570 creates one source-shaped item 1001380 floor effect at a resolved visible player/monster/SOB target, queues the floor UID animation target, applies 10000 spell experience, triggers after 1500ms with radius <= 2, and removes within distance <= 18; LayTrap 12990 creates source floor item 1530 color 14 at the visible target coordinates, stores a 1000ms floor-effect due time, sends caster-position spell animation, and applies 10000 spell experience damage while local floor packets do not encode source DontShow/Angle fields; BloomofDeath uses the direct radius-6 monster/player/SOB damage branch with player damage divided by 5 and SpellEffect 1; ThunderStriker/SkyFall uses the source ThunderStrikerEnergy >= 100 attribute-backed gate after resources, spends 100 energy, hits visible monster/player/SOB targets within radius <= 4 of Attack.X/Y with source physical damage multipliers monster *5, player *18/28, SOB *3, sends SpellEffect 1, and updates spell experience with 10000 source damage. Remaining direct LayTrap parity stays explicit for other unmodeled sort-21 floor/trap branches; TideTrap 14250 is source-backed through MoveLine's equipped TideTrap branch and ThreadPool case 14250 rather than a direct LayTrap.cs case.",
                    sourceEvidence: $"{LegacyAttackHandlerEvidence} {LayTrapEvidence} {ThunderStrikerLayTrapEvidence} {TwilightDanceLayTrapEvidence} {DaggerStormLayTrapEvidence} {SpaceLeapLayTrapEvidence} {LotusLayTrapEvidence} {WaterShockwaveLayTrapEvidence} {DustDetachmentLayTrapEvidence} {SickleWindLayTrapEvidence} {FlameOfDestructionLayTrapEvidence} {SourceCases12550And12570LayTrapEvidence}",
                    suggestedNextOwner:
                    "Floor/trap magic owner: keep other direct LayTrap.Execute branches as separate parity debt beyond the modeled TwilightDance 12070, DaggerStorm 11600, SpaceLeap 15620, LayTrap 12550/12570, BloomofDeath 14720, AuroraLotus/FlameLotus 12370/12380, WaterShockwave 16310/16320 placement/repeat pulse/final line damage, DustDetachment 16460/16470 placement/cast-time SOB/repeat pulse/remove-only final invoke, SickleWind 16500/16510 placement/cast-time SOB/self-only cast animation/repeat monster-player pulse/remove-only final invoke, FlameofDestruction 16540/16550 placement/repeat pulse/remove-only final invoke, LayTrap 12990, and ThunderStriker/SkyFall 15770 slices; TideTrap 14250 belongs to the MoveLine/ThreadPool path, and DustSigilStunn/FlameSigilRapid are modeled gear-contributor behavior on the DustDetachment/FlameofDestruction repeat branches rather than separate direct LayTrap branches.",
                nextSeam: LayTrapFloorEffectRuntimeSeam);
        }

        if (IsDirectTideTrapLayTrapRow(sort, spellId))
        {
            return CreateInfo(
                sort,
                spellId,
                    LegacyMagicSortCapability.FloorTrapRuntime,
                    LegacyMagicModelingStatus.MissingRuntimeNotModeled,
                    reason:
                    "The magictype.txt DB row for TideTrap 14250 is sort 21 LayTrap, but the decompiled LayTrap.cs has no direct case 14250. The source-backed TideTrap runtime is reached through MoveLine spell 11110 when equipped TideTrap plus learned 14250 creates the floor spell, and ThreadPool case 14250 owns the ClientFloorSpells lifecycle.",
                sourceEvidence: $"{LegacyAttackHandlerEvidence} {MoveLineEvidence} {LayTrapEvidence}",
                suggestedNextOwner: "Magic combat owner: keep TideTrap parity on the MoveLine 11110 plus ThreadPool case 14250 path; do not implement a nonexistent direct LayTrap.Execute case 14250 unless new source evidence appears.",
                nextSeam: LayTrapFloorEffectRuntimeSeam);
        }

        if (IsLayTrapSort(sort))
        {
            return CreateInfo(
                sort,
                spellId,
                    LegacyMagicSortCapability.FloorTrapRuntime,
                    LegacyMagicModelingStatus.MissingRuntimeNotModeled,
                    reason:
                    "WolfConquer models only the bounded direct TwilightDance 12070, DaggerStorm 11600, SpaceLeap 15620, and BloomofDeath 14720 LayTrap runtime slices, AuroraLotus/FlameLotus 12370/12380 visual floor-effect creation, WaterShockwave 16310/16320 floor-effect placement/repeat pulse/final line damage, DustDetachment 16460/16470 floor-effect placement/cast-time SOB/repeat pulse/remove-only final invoke, SickleWind 16500/16510 floor-effect placement/cast-time SOB/self-only cast animation/repeat monster-player pulse/remove-only final invoke, FlameofDestruction 16540/16550 floor-effect placement/repeat pulse/remove-only final invoke, LayTrap 12550/12570 delayed source-case floor traps, LayTrap 12990 target floor-effect creation, and ThunderStriker/SkyFall 15770 direct radius damage. Other direct sort 21 LayTrap rows remain explicit parity debt: the source shows this sort owns broader floor/trap effect creation, while PhysicalSpells Windstorm's bounded chain into BloomofDeath is a separate modeled contact. TideTrap 14250 is diagnosed separately because its DB row is sort 21 but the source-backed runtime is the MoveLine/ThreadPool path, not a direct LayTrap.Execute case.",
                sourceEvidence: $"{LegacyAttackHandlerEvidence} {LayTrapEvidence} {DaggerStormLayTrapEvidence} {SpaceLeapLayTrapEvidence} {LotusLayTrapEvidence} {WaterShockwaveLayTrapEvidence} {DustDetachmentLayTrapEvidence} {SickleWindLayTrapEvidence} {FlameOfDestructionLayTrapEvidence} {SourceCases12550And12570LayTrapEvidence}",
                suggestedNextOwner: "Floor/trap magic owner: implement remaining direct LayTrap.Execute floor-effect item creation/lifetime/invoke routing before broadening support beyond the modeled TwilightDance 12070, DaggerStorm 11600, SpaceLeap 15620, LayTrap 12550/12570, BloomofDeath 14720, AuroraLotus/FlameLotus 12370/12380, WaterShockwave 16310/16320 placement/repeat pulse/final line damage, DustDetachment 16460/16470 placement/cast-time SOB/repeat pulse/remove-only final invoke, SickleWind 16500/16510 placement/cast-time SOB/self-only cast animation/repeat monster-player pulse/remove-only final invoke, FlameofDestruction 16540/16550 placement/repeat pulse/remove-only final invoke, LayTrap 12990, and ThunderStriker/SkyFall 15770 slices; keep TideTrap 14250 on the MoveLine/ThreadPool path, and treat DustSigilStunn/FlameSigilRapid as modeled gear-contributor behavior on the DustDetachment/FlameofDestruction repeat branches.",
                nextSeam: LayTrapFloorEffectRuntimeSeam);
        }

        if (IsPhysicalSpellsSort(sort))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.SpecialCombatDamage,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    "WolfConquer models the main legacy PhysicalSpells.Execute direct-target path used by all live sort-55 rows, including TripleAttack 10490/13610 and Windstorm 11140/13290: after CanUseSpell-style resource/coldtime checks it performs three physical hits against one monster, player, or SOB target, preserves the source Physical.OnMonster IsFloor early return as three damage-2 floor hits, preserves the player-target WildPhoenix defender charge/archive hook before the source /2 scaling step so defended hits animate as zero damage, preserves the source ShurikenVortex/ManiacDance target-status early return before WildPhoenix so those status-defended hits enqueue as zero damage without consuming WildPhoenix charges, routes every player hit through the opening ReceiveAttack.Player envelope for attacker/defender CombatGear updates, speffect groups 2/3/4/5, and Ninja SageMode using gouyu Chance / 100, and only Windstorm 11140 chains into the immediate LayTrap.Execute BloomofDeath 14720 radius hit when learned. Remaining parity is the later ReceiveAttack.Player tail, exact remaining special physical effect flags, and full CanAttack side effects.",
                sourceEvidence: PhysicalSpellsEvidence,
                suggestedNextOwner: "Magic combat owner: tighten PhysicalSpells exact damage/effect side effects and full CanAttack side effects after TripleAttack/Windstorm live tests.",
                nextSeam: PhysicalSpellsRuntimeSeam);
        }

        if (IsModeledAttackStatusRuntimeSlice(sort, spellId))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.SpecialCombatDamage,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    "WolfConquer models the AttackStatus default damage branch used by Phoenix weapon procs plus source-backed special cases 7000, 10800, 15560, 15880, and 20000. The modeled branch performs CanUseSpell-style resource/coldtime checks, strict source range checks, MsgSpellAnimation, monster/SOB/player effects where the source has them, default-branch player physical damage divided by /4 after the source Physical.OnPlayer hook with WildPhoenix defender charge/archive override and zero-damage target preservation, and the default player branch's opening ReceiveAttack.Player envelope for attacker/defender CombatGear updates, speffect groups 2/3/4/5, and Ninja SageMode using gouyu Chance / 100. The source 7000 player Range.OnPlayer caller ordering remains separately modeled: ShurikenVortex/ManiacDance status damage returns before WildPhoenix without consuming its charge, WildPhoenix returns before DefensiveStance, DefensiveStance applies the 40 percent Block return without BackDmg, and normal eligible ranged BackDmg invokes learned-defender Heaven's Wonder 16350 then RevengeTail 13030 while carrying nested caster state through each target's opening ReceiveAttack.Player envelope and immediate receive publication before progression and one shared animation. The 7000 opening envelope performs attacker/defender CombatGear updates, speffect groups 2/3/4/5, and Ninja SageMode using gouyu Chance / 100 for every accepted player hit. It also models source-shaped 10800 ShieldofTruth status/equipment refresh packets and matching-decompile 15880 SongofPhoenix caster-centered strict-range monster/player/SOB loops. The 15880 slice uses monster full runtime damage with 60% animation display, player DamageOnHuman-selected Physical.OnPlayer damage scaled by * 7 / 13 with the WildPhoenix defender hook before scaling, and an opening ReceiveAttack.Player envelope with immediate per-target receive publication before the next player and the shared animation. Its player envelope carries attacker state through CombatGear updates, speffect groups 2/3/4/5, and Ninja SageMode using gouyu Chance / 100; SOB damage scaled by * 50 / 100 remains intact, followed by monster/SOB progress, then animation and an immediate return without applying the unreachable later SongofPhoenix flag/power block. AttackStatus remains partially modeled for exact CanAttack side effects, Backfire, CounterKill/Reflect, and broader Range.OnPlayer formulas. Any other special player-damage branches' opening receive envelopes remain separate debt.",
                sourceEvidence: AttackStatusDefaultEvidence + " " + RangeOnPlayerStatusReturnEvidence,
                suggestedNextOwner: "Magic combat owner: tighten exact AttackStatus CanAttack side effects, Backfire, CounterKill/Reflect, and broader Range.OnPlayer formulas after live tests.",
                nextSeam: DamageOnMonsterSpecialSortRuntimeSeam);
        }

        if (IsModeledFatalCrossMonsterRuntimeSlice(sort, spellId))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.SpecialCombatDamage,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    "WolfConquer models the bounded FatalCross 11980 runtime slice: after source-shaped CanUseSpell resources/cooldown it accepts cast points within distance <= 13, restores SuperCyclone stamina by 20 below the source 100/150 HeavenBlessing threshold, selects visible monsters, players, and SOB NPCs in the source width-2/range-13 line, applies source-shaped monster/SOB physical damage multipliers, applies player Physical.OnPlayer damage with player-target WildPhoenix defender charge/archive damage override before the FatalCross Base.CalculateSoul and player *22/28 scaling step, source-shaped zero-damage WildPhoenix player target preservation, sends MsgSpellAnimation, mutates runtime HP, and applies monster/SOB spell progress. Remaining FatalCross parity is exact CanAttack edge gaps.",
                sourceEvidence: $"{LegacyAttackHandlerEvidence} {FatalCrossEvidence}",
                suggestedNextOwner:
                    $"Magic combat owner: tighten exact FatalCross {RemainingCanAttackPlayerGaps}/{RemainingCanAttackNpcGaps} edges.",
                nextSeam: FatalCrossRuntimeSeam);
        }

        if (IsModeledDragonWhirlRuntimeSlice(sort, spellId))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.SpecialCombatDamage,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    $"WolfConquer models the bounded DragonWhirl 10315 runtime slice: source-shaped CanUseSpell resources/cooldown first, spell 10315 only, CheckGuildWar cast-tile rejection after resources with pullback, strict cast point distance < Spell.Range, source width-4 line with range equal to cast distance, visible monster/player/SOB physical damage using shared target gates, player-target WildPhoenix defender charge/archive damage override before player damage application, zero-damage InMoveSpell marker with KilledMonster=10315, MsgSpellAnimation, runtime HP mutation, monster/SOB spell progress, final source landing InMoveSpell interaction, caster session reposition to the computed landing, and source view-role LegacyPlayerSpawn refresh to the caster and visible peers. Remaining DragonWhirl parity is exact CanAttack edge gaps.",
                sourceEvidence: $"{LegacyAttackHandlerEvidence} {DragonWhirlEvidence}",
                suggestedNextOwner:
                    $"Magic combat owner: tighten exact DragonWhirl {RemainingCanAttackPlayerGaps}/{RemainingCanAttackNpcGaps} edges.",
                nextSeam: DragonWhirlRuntimeSeam);
        }

        if (IsModeledChargingVortexRuntimeSlice(sort, spellId))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.SpecialCombatDamage,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    $"WolfConquer models the bounded learned ChargingVortex 11190 runtime slice: source-shaped CanUseSpell resources/cooldown first, CheckGuildWar cast-tile rejection after resources with pullback, strict cast point distance < Spell.Range, source width-4 line with range equal to cast distance, visible monster/player/SOB physical damage using shared target gates, player-target WildPhoenix defender charge/archive damage override before player damage application, zero-damage InMoveSpell marker with KilledMonster=11190, MsgSpellAnimation, runtime HP mutation, monster/SOB spell progress, final source landing InMoveSpell interaction, caster session reposition, and source view-role LegacyPlayerSpawn refresh. Remaining ChargingVortex parity is exact CanAttack edge gaps, {RemainingCanAttackPlayerGaps}, and {RemainingCanAttackNpcGaps}.",
                sourceEvidence: $"{LegacyAttackHandlerEvidence} {ChargingVortexEvidence}",
                suggestedNextOwner:
                    $"Magic combat owner: tighten exact ChargingVortex {RemainingCanAttackPlayerGaps}/{RemainingCanAttackNpcGaps} edges.",
                nextSeam: ChargingVortexRuntimeSeam);
        }

        if (IsModeledShockRuntimeSlice(sort, spellId))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.SpecialCombatDamage,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    "WolfConquer models the bounded Shock/CrackingShock 15720 runtime slice: after source-shaped " +
                    "CanUseSpell resources/cooldown it returns without animation for non-ThunderStriker classes, " +
                    "preserves decoded Attack.X/Y without an added push, selects visible monsters, players, and SOB " +
                    "NPCs within inclusive radius <= 6, applies source-shaped monster/SOB physical damage through " +
                    "Base.CalculateSoul with source multipliers (monster *2, SOB unchanged), applies player " +
                    "Physical.OnPlayer status returns before WildPhoenix, then the bounded normal breakthrough, " +
                    "DefensiveStance/Block, Infinity and nonpenetrating 50-percent branch. Heaven's Wonder and " +
                    "RevengeTail see the pre-MudWall physical hit, with refreshed counter state retained before " +
                    "one MudWall reduction, identity soul and player *75/100 scaling. Each current player gate " +
                    "finishes its counter before the next receive. It retains ordinary and " +
                    "WildPhoenix zero-damage player targets, applies SOB Infinity spell scaling after the physical " +
                    "minimum-one floor and retains accepted zero-damage SOB animation targets without HP or progress " +
                    "publication, preserves requested SOB overkill animation damage without inflating HP loss or " +
                    "progress, and runs opening CombatGear, speffect groups 2/3/4/5 and Ninja Chance / 100 before " +
                    "each accepted player's HP mutation and immediate receive publication, including unchanged " +
                    "HP for ordinary zero-damage receives. It applies monster/SOB " +
                    "spell and proficiency progress, then sends one MsgSpellAnimation even for an empty valid cast. " +
                    $"Other Physical/Receive tails, exact CanAttack side-effect edges, {RemainingCanAttackPlayerGaps}, " +
                    $"and {RemainingCanAttackNpcGaps} remain partial.",
                sourceEvidence: $"{LegacyAttackHandlerEvidence} {ShockEvidence}",
                suggestedNextOwner:
                    $"Magic combat owner: tighten exact Shock soul-scaling evidence and CanAttack side effects, including {RemainingCanAttackPlayerGaps} and {RemainingCanAttackNpcGaps}.",
                nextSeam: ShockRuntimeSeam);
        }

        if (IsModeledStrike13050RuntimeSlice(sort, spellId))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.SpecialCombatDamage,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    $"WolfConquer models the bounded Strike 13050 caster-centered area runtime: source-shaped resource/cooldown preparation, clicked player/monster animation-coordinate reanchoring, radius <= 5 mixed visible monster/player/SOB selection from the caster, shared target gates, unscaled post-soul physical damage, runtime HP mutation, manual AtkType 2 interactions before animation, monster/SOB-only character and spell progress, attacked UID 0 animation, and source no-target silence. Remaining parity is exact OnAutoAttack interaction suppression, Base.CalculateSoul, and CanAttack side-effect edges, {RemainingCanAttackPlayerGaps}, and {RemainingCanAttackNpcGaps}.",
                sourceEvidence: $"{LegacyAttackHandlerEvidence} {Strike13050Evidence}",
                suggestedNextOwner:
                    "Magic combat owner: model authoritative OnAutoAttack state and tighten Strike 13050 exact soul and CanAttack edges.",
                nextSeam: Strike13050RuntimeSeam);
        }

        if (IsModeledStrike12600RuntimeSlice(sort, spellId))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.SpecialCombatDamage,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    $"WolfConquer models the bounded Strike 12600 direct-target runtime: source-shaped resource/cooldown preparation, direct visible monster/player/SOB selection, shared target gates, post-soul monster *2, player *2/3 with the WildPhoenix defender hook before scaling, SOB *4, runtime HP mutation, monster/SOB character and spell progress, and source-shaped caster/caster MsgSpellAnimation including empty-target casts. Remaining parity is exact Base.CalculateSoul and CanAttack side-effect edges, {RemainingCanAttackPlayerGaps}, and {RemainingCanAttackNpcGaps}.",
                sourceEvidence: $"{LegacyAttackHandlerEvidence} {Strike12600Evidence}",
                suggestedNextOwner:
                    "Magic combat owner: tighten Strike 12600 exact soul and CanAttack edges; keep Strike 13050 as a separate area-strike runtime.",
                nextSeam: Strike12600RuntimeSeam);
        }

        if (IsModeledGunfireStrikeRuntimeSlice(sort, spellId))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.SpecialCombatDamage,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    $"WolfConquer models the bounded Gunfire 14590 Strike runtime slice: source-shaped resource/cooldown preparation, direct visible monster/player/SOB target selection, shared target gates, physical damage, source player *2/3 scaling with the player-target WildPhoenix defender hook before scaling, runtime HP mutation, monster/SOB character and spell progress, and source-shaped MsgSpellAnimation including empty-target casts. Remaining Gunfire parity is exact Base.CalculateSoul behavior, exact CanAttack side-effect edges, {RemainingCanAttackPlayerGaps}, and {RemainingCanAttackNpcGaps}. This claim is only spell ID 14590; other Strike IDs use their own capability slices or remain unsupported.",
                sourceEvidence: $"{LegacyAttackHandlerEvidence} {GunfireStrikeEvidence}",
                suggestedNextOwner:
                    $"Magic combat owner: tighten exact Gunfire Base.CalculateSoul and CanAttack side effects, including {RemainingCanAttackPlayerGaps} and {RemainingCanAttackNpcGaps}; keep each other Strike spell on its dedicated slice.",
                nextSeam: GunfireStrikeRuntimeSeam);
        }

        if (IsUnsupportedStrikeSort(sort))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.SpecialCombatDamage,
                LegacyMagicModelingStatus.MissingRuntimeNotModeled,
                supported: false,
                reason:
                    "Only Strike spells 12600, 13050, and Gunfire spell 14590 have bounded modeled Strike sort 81 runtime slices. Generic and every other Strike query remain unsupported and do not inherit those dedicated capability claims.",
                sourceEvidence: $"{LegacyAttackHandlerEvidence} {Strike12600Evidence} {Strike13050Evidence} {GunfireStrikeEvidence}",
                suggestedNextOwner:
                    "Source scout: identify a separate decompiled Strike.cs case and implement it with dedicated tests before broadening Strike sort support.",
                nextSeam: SourceTriageSeam);
        }

        if (IsModeledToxicFogRuntimeSlice(sort, spellId))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.PoisonStatusRuntime,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    "WolfConquer models the bounded ToxicFog 6001 runtime slice for Toxic sort 27: source-shaped CanUseSpell resource/cooldown ordering, non-boss visible monster and valid visible-player poison selection within distance < 5 from Attack.X/Y, PoisonLevel = ClientSpell.Level + ClientSpell.UseSpellSoul, AddSpellFlag(Poisoned, Spell.Duration, RemoveOnDead) with type 1 for monsters and type 2 for players, ToxinEraser skip plus bounded Sturdiness avoidance, UpdateSpell.CheckUpdate damage=1000 when any target was poisoned, and empty-target MsgSpellAnimation. Remaining ToxicFog parity gaps are exact poison tick damage/expiry, the exact Sturdiness probabilistic edge, and exact player target side-effect edges outside the modeled valid-player gates. This claim is only for spell 6001; generic Toxic sort 27 queries, other Toxic spell IDs, PoisonStar, and other poison rows are not claimed.",
                sourceEvidence: $"{LegacyAttackHandlerEvidence} {ToxicEvidence}",
                suggestedNextOwner:
                    "Magic poison runtime owner: tighten exact poison tick damage/expiry, exact Sturdiness probability parity, and any remaining player target side effects; keep non-6001 Toxic/poison rows fenced until each source-backed slice lands.",
                nextSeam: ToxicPoisonRuntimeSeam);
        }

        if (IsUnsupportedToxicPoisonSort(sort))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.PoisonStatusRuntime,
                LegacyMagicModelingStatus.MissingRuntimeNotModeled,
                supported: false,
                reason:
                    "Only ToxicFog spell 6001 has a bounded modeled Toxic sort 27 runtime slice. This generic or non-6001 Toxic sort query stays unsupported so other poison/Toxic rows are not claimed by the ToxicFog metadata.",
                sourceEvidence: $"{LegacyAttackHandlerEvidence} {ToxicEvidence}",
                suggestedNextOwner:
                    "Source scout or magic poison runtime owner: review any non-6001 Toxic sort rows separately before broadening Toxic poison runtime support.",
                nextSeam: ToxicPoisonRuntimeSeam);
        }

        if (IsModeledBreathFocusRuntimeSlice(sort, spellId))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.StaminaRestoreSupport,
                LegacyMagicModelingStatus.FullyModeled,
                reason:
                    "WolfConquer models the bounded BreathFocus 11960 runtime slice: source-shaped CanUseSpell resource/cooldown ordering, optional caster stamina restore by DBSpell.Damage when stamina is below 100 with clamp to 100 and Stamina update, UpdateSpell.CheckUpdate damage=1000, and a targetless MsgSpellAnimation from caster UID at caster X/Y using ClientSpell.Level and ClientSpell.UseSpellSoul. This claim is only the direct BreathFocus handler slice; automatic Trojan fallback selection stays outside this bounded runtime slice.",
                sourceEvidence: $"{LegacyAttackHandlerEvidence} {BreathFocusEvidence}",
                suggestedNextOwner:
                    "Magic support owner: keep BreathFocus bounded to spell 11960 unless the separate Trojan fallback caller path is explicitly ported and tested.",
                nextSeam: StaminaRestoreRuntimeSeam);
        }

        if (IsModeledKineticSparkDirectToggleRuntimeSlice(sort, spellId))
        {
            return NormalizeKineticSparkNinjaSageInfo(CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.SpecialCombatDamage,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    "WolfConquer models the bounded KineticSpark 11590 runtime slices for sort 67: " +
                    "direct Execute toggle flips the self KineticSpark status flag 0x93, uses the source 2592000-second RemoveOnDead duration when adding the flag, removes it when already present, and sends the self-target spell animation; " +
                    "the monster passive proc is modeled for the Archer caller-path AttackSpell case 11590 monster branch, where the caller runs the source direct-target Base.GetDistance <= 18 caller gate and primary direct-target CanAttackMonster gate before the Kinetic 30% rate gate, with primary visible monster targeting, chain monsters within Spell.Range / 2, StarRaid target budget, Range.OnMonster damage shape, source soul/scaled chain damage, bounded monster receive-side Ninja SageMode branch using gouyu_immortal Rate, monster experience/progress updates, and animation only when at least one target exists; " +
                    "the player passive proc is modeled for the Archer caller-path AttackSpell case 11590 player branch, where the caller runs the source direct-target Base.GetDistance <= 18 caller gate and primary direct-target CanAttackPlayer gate before the Kinetic 30% rate gate, with primary visible-player targeting through Physical.OnPlayer, chained visible players inside strict Spell.Range / 2 through Range.OnPlayer, shared CanAttackPlayer gates, ShurikenVortex/ManiacDance before WildPhoenix, Range DefensiveStance 40 percent Block return without BackDmg, bounded learned-defender Heaven's Wonder 16350 BackDmg on normal eligible hits, source soul/scaled chain damage, player HP/death mutation and receive packets before one shared animation, bounded mutual ThundercloudSight side effects, bounded player-hit defender CombatGear UpdateTarget self/status passives, defender UpdateTarget MagneticLight 20220 and AblazeBlade 20360 player branches, bounded attacker-side MyCombatGear.Update StoneCracker ArrowBlades/StarFlow 10950 counter/status/mixed-target HP branch, bounded attacker-side MyCombatGear.Update StoneCracker ArchShadow 18980 branch, bounded receive-side CombatGear Dragonhowl/Bloodlust status/power/self-animation branches for Dragonhowl 17620 and Bloodlust 17600/17610, bounded receive-side CombatGear Taoist 20330/20380 status/power/self-animation branches for Taoist3/CrackMantra 20380 and Taoist1/DeadwoodCurse 20330, bounded receive-side CombatGear Pirate 21710/21720/21930 status/buff-strip self-animation or target-animation branches, bounded receive-side CombatGear Pirate black-jade random UseRune status branches for 21590/21730/21740/21750/21760/21770/21780/21790/21800/21810/21820, bounded defender speffect_attribute.Get Solid/Bloodthirst/Elan self-effects, bounded attacker Venom speffect type 2 bridge through modeled AttackStatus spell 20000, bounded KineticSpark player receive InfiniteMist ArchiveSkill emit-order, bounded player receive-side Ninja SageMode branch using gouyu_immortal Rate, zero-damage spell progress, and animation only when at least one target exists; " +
                    "production retains a direct player-magic MudWall/MudSigilImpeccable compatibility reduction: target flag 0x112 plus Ninja-gated active-set type 301 power subtracts current damage * (Power / 100) / 100, while its explicit legacy level/power attribute fallbacks and exact placement across KineticSpark primary/chained player callers remain unaudited rather than matching-source-complete; " +
                    "the SOB passive proc is modeled for the Archer caller-path AttackSpell case 11590 SOB branch, where the caller runs the source direct-target Base.GetDistance <= 18 caller gate and primary direct-target CanAttackNpc gate before the Kinetic 30% rate gate, with primary visible-SOB targeting, chained visible SOB NPCs within Spell.Range / 2, shared CanAttackNpc gates, source physical primary damage, source magic chained damage, source soul/scaled chain damage, SOB HP/death mutation, bounded caster ThundercloudSight and AttackHit side effects, bounded SOB receive-side CombatGear StoneCracker ArrowBlades/StarFlow 10950 counter/status branch, bounded SOB receive-side CombatGear Dragonhowl/Bloodlust self-status branches, bounded SOB receive-side CombatGear Taoist DeadwoodCurse status branch, Taoist3 CrackMantra 20380 SOB no-op, defender UpdateTarget player-only skip, bounded SOB receive-side CombatGear Pirate Armed/Sense status branches, Pirate Dark 21930 SOB no-op interaction/targetless-animation, and black-jade random UseRune status branches for 21590/21730/21740/21750/21760/21770/21780/21790/21800/21810/21820, bounded SOB receive-side Ninja SageMode branch using gouyu_immortal Rate, bounded HookMoonCaster/CleanSweep/AxeShadow/CelestialDance receive damage modifiers, SOB experience/progress damage, and animation only when at least one target exists; " +
                    "the Archer caller now also models source MyCombatGear.CheckCombatGear consumption before the 18750/10990 passive hooks, KineticSpark gate, ShadowofChaser branch, and normal fallback for player, monster, and SOB targets, " +
                    "the bounded HookMoonCaster/CleanSweep/AxeShadow/HawksAmbition/AttackUp/LightningShield/Duel/Absolution/BloodTide/Caprice/ReverseMagic/SlowPace/XPBooster/XPKiller/TidalWave/Quench/NeptuneCurse/AttackHit/EagleEye/SkillTournament/FreezeWar/Football/DragonWar/MirrorOfSin/LightOfStamina/CalmWind/DivineGuard/KillingFlash/DrainingTouch/BloodSpawn player receive modifiers on shared player-player magic hits, " +
                    "StarFlow 10950 post-receive player animation damage/update ordering, recursive StarFlow 10950 player-hit CombatGear.Update/UpdateTarget ordering, and the 15940/7030 AxeShadow/CelestialDance self-status setter/expiry plus bounded target-damage path is modeled separately. " +
                    $"Remaining KineticSpark parity is the retained direct MudSigilImpeccable compatibility fallback's exact primary/chained player-caller order, remaining Ninja/Sage branches outside the bounded player/SOB/monster SageMode paths, DragonWar playtest/economy polish, deeper rune/perfection counters, PK/tournament bookkeeping, event/SOB branches, exact CanAttack side effects, {RemainingCanAttackPlayerGaps}, and {RemainingCanAttackNpcGaps}.",
                sourceEvidence: KineticSparkDirectToggleAndPassiveProcEvidence + " " + RangeOnPlayerStatusReturnEvidence + " " + PlayerReceiveMirrorOfSinEvidence + " " + PlayerReceiveDragonWarEvidence + " " + PlayerReceiveInfiniteMistEvidence + " " + PlayerReceiveMudWallEvidence,
                suggestedNextOwner:
                    "Magic combat owner: tighten exact KineticSpark AttackSpell side-effect parity for remaining Ninja/Sage branches outside the bounded player/SOB/monster SageMode paths, DragonWar playtest/economy polish, deeper rune/perfection counters, PK/tournament bookkeeping, event/SOB branches, exact CanAttack side effects, " +
                    "while auditing the retained direct MudSigilImpeccable compatibility reduction and its primary/chained player-caller order against matching Physical.cs lines 979-986, " +
                    $"{RemainingCanAttackPlayerGaps}, and {RemainingCanAttackNpcGaps} before broadening KineticSpark parity beyond the direct toggle plus Archer caller-path monster/player/SOB passive procs, bounded ThundercloudSight and AttackHit side effects, bounded SOB receive-side CombatGear StoneCracker ArrowBlades/StarFlow 10950 counter/status branch, bounded SOB receive-side CombatGear Dragonhowl/Bloodlust, Taoist DeadwoodCurse, Taoist3 CrackMantra SOB no-op, defender UpdateTarget player-only skip, Pirate Armed/Sense, Pirate Dark SOB no-op, Pirate black-jade UseRune, and Ninja SageMode branches, bounded HookMoonCaster/CleanSweep/AxeShadow/CelestialDance SOB receive damage modifiers, bounded HookMoonCaster/CleanSweep/AxeShadow/HawksAmbition/AttackUp/LightningShield/Duel/Absolution/BloodTide/Caprice/ReverseMagic/SlowPace/XPBooster/XPKiller/TidalWave/Quench/NeptuneCurse/AttackHit/EagleEye/SkillTournament/FreezeWar/Football/DragonWar/MirrorOfSin/LightOfStamina/CalmWind/DivineGuard/KillingFlash/DrainingTouch/BloodSpawn player receive modifiers, StarFlow 10950 post-receive player animation damage/update ordering, bounded AxeShadow/CelestialDance self-status setter/expiry and target-damage path, bounded player-hit defender CombatGear UpdateTarget self/status passives, defender UpdateTarget MagneticLight 20220 and AblazeBlade 20360 player branches, bounded attacker-side MyCombatGear.Update StoneCracker ArrowBlades/StarFlow 10950 counter/status/mixed-target HP branch, bounded attacker-side MyCombatGear.Update StoneCracker ArchShadow 18980 branch, bounded receive-side CombatGear Dragonhowl/Bloodlust status/power/self-animation branches, bounded receive-side CombatGear Taoist 20330/20380 status/power/self-animation branches, bounded receive-side CombatGear Pirate 21710/21720/21930 status/buff-strip self-animation or target-animation branches, bounded receive-side CombatGear Pirate black-jade random UseRune status branches, bounded SOB receive-side Ninja SageMode branch using gouyu_immortal Rate, bounded player receive-side Ninja SageMode branch using gouyu_immortal Rate, bounded KineticSpark player receive InfiniteMist ArchiveSkill emit-order, bounded monster receive-side Ninja SageMode branch using gouyu_immortal Rate, bounded defender speffect_attribute.Get Solid/Bloodthirst/Elan self-effects, bounded attacker Venom speffect type 2 bridge through modeled AttackStatus spell 20000, and CombatGear caller consumption.",
                nextSeam: KineticSparkAttackSpellSideEffectParitySeam));
        }

        if (IsArcherNebulousHuntPreKineticProcSpell(sort, spellId))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.SpecialCombatDamage,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    "WolfConquer models the bounded NebulousHunt 10990 sort-69/SectorBack runtime: source-shaped line width 2 over visible monsters, players, and SOB NPCs, source physical damage, monster/SOB animation damage doubled after runtime damage, player Physical.OnPlayer damage with player-target WildPhoenix defender charge/archive override before the NebulousHunt Base.CalculateSoul and /7 scaling step, player damage divided by 7, source-shaped zero-damage player target preservation for animation, and monster/SOB spell progress. The source-backed Archer AttackID passive proc must remain ordered before KineticSpark: the legacy Archer caller checks the source direct-target Base.GetDistance <= 18 caller gate, learned 10990 spell, and Base.Rate(30) before the later KineticSpark gate sequence. Remaining parity is exact passive caller side effects, exact CanAttack side-effect edges, exact Archer/Kinetic/rune/event gaps, and adjacent SectorBack cases outside the separately bounded 18240 and 15870 slices. This claim is only for spell ID 10990 with sort 69/SectorBack; SectorBack 18240 has its own bounded metadata record, and adjacent SectorBack cases stay separate.",
                sourceEvidence: ArcherPreKineticProcEvidence + " " + NebulousHuntSectorBackPhysicalEvidence,
                suggestedNextOwner:
                    "Magic combat owner: verify exact Archer NebulousHunt 10990 learned-spell plus 30% pre-Kinetic passive caller side effects stay before KineticSpark, tighten exact CanAttack side-effect edges and Archer/Kinetic/rune/event gaps, and keep SectorBack 18240 on its own bounded runtime record separate from the bounded 10990 passive-proc slice.",
                nextSeam: ArcherPreKineticProcRuntimeSeam);
        }

        if (IsModeledSectorBack18240RuntimeSlice(sort, spellId))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.SpecialCombatDamage,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    $"WolfConquer models the bounded SectorBack 18240 runtime: source-shaped line width 2 over visible monsters, players, and SOB NPCs, source physical damage, monster/SOB animation damage doubled after runtime damage, player Physical.OnPlayer damage with player-target WildPhoenix defender charge/archive override before the SectorBack 18240 Base.CalculateSoul and /7 scaling step, player damage divided by 7, source-shaped zero-damage player target preservation for animation, and monster/SOB spell progress. Remaining SectorBack 18240 parity is exact CanAttack side-effect edges, {RemainingCanAttackPlayerGaps}, and {RemainingCanAttackNpcGaps}. This claim is only for spell ID 18240 with sort 69/SectorBack; magictype level rows 18241-18246 are source data for type 18240 but are not separate runtime ID claims, CleanSweep rows 15871-15879/15993 stay unimplemented, and adjacent SectorBack cases stay separate.",
                sourceEvidence: $"{LegacyAttackHandlerEvidence} {NebulousHuntSectorBackPhysicalEvidence}",
                suggestedNextOwner:
                    $"Magic combat owner: tighten exact SectorBack 18240 CanAttack side effects, including {RemainingCanAttackPlayerGaps} and {RemainingCanAttackNpcGaps}; keep magictype level rows 18241-18246, CleanSweep rows 15871-15879/15993, and adjacent SectorBack cases separate unless the runtime resolution by type is reviewed.",
                nextSeam: SectorBack18240RuntimeSeam);
        }

        if (IsModeledCleanSweepSectorBackRuntimeSlice(sort, spellId))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.SpecialCombatDamage,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    $"WolfConquer models the bounded CleanSweep 15870 sort-69/SectorBack runtime: line-width-2 mixed monster/player/SOB targets with range from the CleanSweep row, source damage scaling (monster /2, player *5/12, SOB runtime full but animation *50/100), player WildPhoenix defender hook before CleanSweep scaling, source-shaped zero-damage WildPhoenix player animation target with Hit=false, self CleanSweep flag/archive update using flag 254 with Time=Duration, Damage=Damage3, SpellLevel=Damage3, and CleanSweep power/expiry state. Remaining CleanSweep parity is exact CanAttack side-effect edges, {RemainingCanAttackPlayerGaps}, and {RemainingCanAttackNpcGaps}. This claim is bounded to spell ID 15870 with sort 69/SectorBack; magictype level rows 15871-15879 and 15993 are source data for CleanSweep type 15870 but are not separate runtime ID claims, and adjacent SectorBack cases stay separate.",
                sourceEvidence: $"{LegacyAttackHandlerEvidence} {CleanSweepSectorBackEvidence}",
                suggestedNextOwner:
                    $"Magic combat owner: tighten exact CleanSweep 15870 CanAttack side effects, including {RemainingCanAttackPlayerGaps} and {RemainingCanAttackNpcGaps}; keep magictype level rows 15871-15879/15993 and adjacent SectorBack cases separate unless the runtime resolution by type is reviewed.",
                nextSeam: CleanSweepSectorBackRuntimeSeam);
        }

        if (IsCruelShadeDecLifeSpell(sort, spellId))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.SpecialCombatDamage,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    $"WolfConquer models the bounded CruelShade 3050 runtime slice for DecLife sort 26: source-shaped CanUseSpell resource/cooldown ordering, single visible monster/player/SOB target resolution from Attack.OpponentUID, strict distance < Spell.Range, shared monster/player/SOB target gates, monster boss damage = 1, non-boss monster HP-based DecLife damage, player HP-based DecLife damage overwritten by ToxinEraser percentage scaling, SOB Spell.Damage % 100 HP damage, runtime HP mutation through ReceiveAttack handlers, IncreaseExperience.Up, UpdateSpell.CheckUpdate, MsgSpellAnimation, and the source BreathFocus 11960 fallback exclusion for spell 3050. Remaining CruelShade parity is exact CanAttack side-effect edges, {RemainingCanAttackPlayerGaps}, and {RemainingCanAttackNpcGaps}. This is only the CruelShade spell 3050 slice; DecLife also owns GapingWounds 11230 and should stay separate.",
                sourceEvidence: $"{LegacyAttackHandlerEvidence} {CruelShadeEvidence}",
                suggestedNextOwner:
                    $"Magic combat owner: tighten exact CruelShade CanAttack side effects, including {RemainingCanAttackPlayerGaps} and {RemainingCanAttackNpcGaps}; keep GapingWounds 11230 separate before broadening DecLife sort 26.",
                nextSeam: CruelShadeRuntimeSeam);
        }

        if (IsGapingWoundsDecLifeSpell(sort, spellId))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.SpecialCombatDamage,
                LegacyMagicModelingStatus.PartiallyModeled,
                reason:
                    $"WolfConquer models the bounded GapingWounds 11230 runtime slice for DecLife sort 26: source-shaped CanUseSpell resource/cooldown ordering, direct Attack.OpponentUID resolution as one visible monster/player/SOB target, strict distance < Spell.Range, shared monster/player/SOB target gates, monster boss damage = 1, non-boss monster HP * (100 - Spell.Damage) / 100 damage, player requires !ToxinEraser before damage using the source PerfectionEffects.ToxinEraser rate gate and then uses the source ToxinEraser overwrite math with missing local status treated as the source default 0 percent, SOB HP * (100 - Spell.Damage) / 100 damage, runtime HP mutation through ReceiveAttack handlers, direct player-hit mutual ThundercloudSight persistence plus shared pre-animation response/qualifier side-effect sends, IncreaseExperience.Up, UpdateSpell.CheckUpdate, and MsgSpellAnimation. Remaining GapingWounds parity is exact CanAttack side-effect edges, {RemainingCanAttackPlayerGaps}, and {RemainingCanAttackNpcGaps}. This is only the GapingWounds spell 11230 slice and stays separate from CruelShade 3050.",
                sourceEvidence: $"{LegacyAttackHandlerEvidence} {GapingWoundsEvidence}",
                suggestedNextOwner:
                    $"Magic combat owner: tighten exact GapingWounds 11230 CanAttack side effects, including {RemainingCanAttackPlayerGaps} and {RemainingCanAttackNpcGaps}; keep CruelShade 3050 and unknown DecLife spell ids separate before broadening DecLife sort 26.",
                nextSeam: GapingWoundsRuntimeSeam);
        }

        if (IsSpecialCombatDamageSort(sort))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.SpecialCombatDamage,
                LegacyMagicModelingStatus.MissingRuntimeNotModeled,
                reason:
                    "The decompiled handler for this sort applies spell-specific monster/player/SOB damage or effects; WolfConquer currently rejects this sort before target selection.",
                sourceEvidence:
                    $"{LegacyAttackHandlerEvidence} Examples include Shock.cs, PhysicalSpells.cs, Toxic.cs, KineticSpark.cs, and other AttackHandler classes.",
                suggestedNextOwner: "Magic combat owner: add shared special-sort target selection and damage/effect hooks.",
                nextSeam: DamageOnMonsterSpecialSortRuntimeSeam);
        }

        if (IsStateAndBuffRuntimeSort(sort))
        {
            return CreateInfo(
                sort,
                spellId,
                LegacyMagicSortCapability.StateAndBuffRuntime,
                LegacyMagicModelingStatus.MissingRuntimeNotModeled,
                reason:
                    "The legacy source dispatches this sort to state, transport, aura, trap, transform, dance, or buffer logic; WolfConquer has no magic runtime owner for that state family yet.",
                sourceEvidence: LegacyAttackHandlerEvidence,
                suggestedNextOwner: "Magic support owner: implement the specific AttackHandler class for this state/buff sort.",
                nextSeam: StateAndBuffRuntimeSeam);
        }

        return CreateInfo(
            sort,
            spellId,
            LegacyMagicSortCapability.UnknownUnsupported,
            LegacyMagicModelingStatus.MissingRuntimeNotModeled,
            supported: false,
            reason:
                "This sort is not in the current modeled set and is not yet tied to a reviewed legacy handler or magictype.txt inventory group.",
            sourceEvidence: "No reviewed source evidence beyond the caller-provided sort/spell ID.",
            suggestedNextOwner: "Source scout: map this sort or spell ID against magictype.txt and the decompiled MsgAttackPacket.ProcescMagic switch.",
            nextSeam: SourceTriageSeam);
    }

    public static string FormatDiagnostic(LegacyMagicSort sort, ushort spellId = 0)
    {
        return FormatDiagnostic(Classify(sort, spellId));
    }

    public static string FormatDiagnostic(LegacyMagicSortCapabilityInfo info)
    {
        return
            $"magicSort={info.SortName} spellId={info.SpellId} capability={GetCapabilityName(info.Capability)} " +
            $"status={GetStatusName(info.Status)} supported={info.Supported.ToString().ToLowerInvariant()} " +
            $"nextSeam={info.NextSeam} reason={info.Reason}";
    }

    public static string GetSortName(LegacyMagicSort sort)
    {
        var sortValue = (int)sort;
        var name = sortValue switch
        {
            0 => "SourceSilent",
            1 => "Attack",
            2 => "Recruit",
            3 => "Cross",
            4 => "Sector",
            5 => "Bomb",
            6 => "AttachStatus",
            7 => "DetachStatus",
            8 => "Square",
            9 => "JumpAttack",
            10 => "RandomTransport",
            11 => "DispatchXp",
            12 => "Collide",
            13 => "SerialCut",
            14 => "Line",
            15 => "AtkRange",
            16 => "AttackStatus",
            17 => "CallTeamMember",
            18 => "RecordTransportSpell",
            19 => "Transform",
            20 => "AddMana",
            21 => "LayTrap",
            22 => "Dance",
            23 => "CallPet",
            24 => "Vampire",
            25 => "Instead",
            26 => "DecLife",
            27 => "Toxic",
            28 => "ShurikenVortex",
            29 => "CounterKill",
            30 => "Spook",
            31 => "WarCry",
            32 => "Riding",
            33 => "TailSweep",
            34 => "Shock",
            35 => "FlameShower",
            36 => "MistyFlame",
            37 => "ChainBolt",
            38 => "StarArrow",
            40 => "DragonWhirl",
            41 => "RevengePunch",
            42 => "SurgingShake",
            43 => "SoaringLeap",
            44 => "ArrowHail",
            45 => "ArrowHailAlternate",
            46 => "RemoveBuffers",
            47 => "Tranquility",
            48 => "DirectAttack",
            50 => "Compasion",
            51 => "Auras",
            52 => "ShieldBlock",
            53 => "Oblivion",
            54 => "WhirlwindKick",
            55 => "PhysicalSpells",
            56 => "ScurvyBomb",
            57 => "CannonBarrage",
            58 => "BlackSpot",
            59 => "AdrenalineRush",
            60 => "BombPirte",
            61 => "MoveLine",
            62 => "AddBlackSpot",
            63 => "PirateXpSkill",
            64 => "ChargingVortex",
            65 => "MortalDrag",
            66 => "WrathOfDragon",
            67 => "KineticSpark",
            68 => "BladeFlurry",
            69 => "SectorBack",
            70 => "BreathFocus",
            71 => "FatalCross",
            72 => "Summon",
            73 => "FatalSpin",
            75 => "DragonCyclone",
            76 => "StraightFist",
            78 => "Perimeter",
            79 => "AirKick",
            81 => "Strike",
            82 => "SectorPasive",
            83 => "ManiacDance",
            84 => "Pounce",
            85 => "Omnipotence",
            86 => "BurntFrost",
            87 => "Rectangle",
            88 => "RemoveStamin",
            89 => "PetAttachStatus",
            90 => "Wildwind",
            91 => "SwirlingStorm",
            92 => "Pitching",
            94 => "ThunderRampage",
            95 => "HeavensWrath",
            96 => "ManualLine",
            98 => "HellVortex",
            99 => "TripleAttack",
            100 => "SharpThorn",
            101 => "ShadowStrike",
            102 => "LeapChop",
            103 => "ArrowBlades",
            104 => "CrackShot",
            105 => "Berveda",
            106 => "LordThreat",
            107 => "NutSummon",
            _ => "Unknown",
        };

        return $"{name}({sortValue})";
    }

    public static bool IsSupportedPerimeterVisibleMonsterAttackSpell(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.Perimeter &&
            spellId is
                12_210 or 12_220 or
                12_580 or 12_590 or
                12_740 or 12_750 or
                13_040 or
                14_570 or 14_580 or
                15_670 or 15_680;
    }

    public static bool IsSupportedAurasRuntimeSpell(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.Auras &&
            spellId is 10_395 or 10_410 or 10_420 or 10_421 or 10_422 or 10_423 or 10_424 or 11_200;
    }

    public static bool IsSupportedPerimeterSpecialRuntimeSpell(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.Perimeter &&
            spellId is 13_040 or 15_670 or 15_680;
    }

    public static bool IsSupportedStraightFistVisibleMonsterAttackSpell(
        LegacyMagicSort sort,
        ushort spellId)
    {
        return IsModeledStraightFist12760RuntimeSlice(sort, spellId);
    }

    public static bool IsSupportedAttachStatusBuffSpell(LegacyMagicSort sort, ushort spellId)
    {
        return IsAttachStatusXpListSelfBuff(sort, spellId) ||
            IsAttachStatusOpenXpSkill(sort, spellId) ||
            IsSupportedAttachStatusPlayerTargetBuffSpell(sort, spellId) ||
            IsHawksAmbitionAttachStatusSelfBuff(sort, spellId) ||
            IsRevengeTailAttachStatusSelfToggle(sort, spellId) ||
            IsDragonSwingAttachStatusSelfToggle(sort, spellId) ||
            IsAutoHuntAttachStatusSelfToggle(sort, spellId) ||
            IsFlyAttachStatusSelfBuff(sort, spellId) ||
            IsCastPrayAttachStatusSelfBuff(sort, spellId) ||
            IsIntensifyAttachStatusFocusCharge(sort, spellId) ||
            IsInfinityAttachStatusFlyBuff(sort, spellId) ||
            IsSacrificeAttachStatusSelfToggle(sort, spellId) ||
            IsRiseOfTaoismAttachStatusSelfBuff(sort, spellId) ||
            IsIronGuardAttachStatusSelfBuff(sort, spellId) ||
            IsIronShieldAttachStatusSelfBuff(sort, spellId) ||
            IsFineRainAttachStatusTeamBuff(sort, spellId) ||
            IsCrackStarAttachStatusSelfBuff(sort, spellId) ||
            IsBackfireAttachStatusSelfBuff(sort, spellId) ||
            IsPathOfShadowAttachStatusSelfToggle(sort, spellId) ||
            IsDefensiveStanceAttachStatusSelfToggle(sort, spellId) ||
            IsLightningShieldAttachStatusSelfToggle(sort, spellId) ||
            IsImmortalForceAttachStatusSelfBuff(sort, spellId) ||
            IsSnowAttachStatusSelfToggle(sort, spellId) ||
            IsCombatGearAttachStatusSelfBuff(sort, spellId);
    }

    private static bool IsAttachStatusOpenXpSkill(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.AttachStatus &&
            spellId is 6_011 or 11_970;
    }

    private static bool IsAttachStatusXpListSelfBuff(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.AttachStatus &&
            spellId is 1_015 or 1_020 or 8_002;
    }

    public static bool IsSupportedAttachStatusPlayerTargetBuffSpell(LegacyMagicSort sort, ushort spellId)
    {
        return IsClassicAttachStatusBuffSpell(sort, spellId) ||
            (sort == LegacyMagicSort.AttachStatus && spellId is 16_380 or 30_000);
    }

    public static bool IsSupportedAttachStatusVisibleMonsterTargetBuffSpell(
        LegacyMagicSort sort,
        ushort spellId)
    {
        return IsClassicAttachStatusBuffSpell(sort, spellId);
    }

    private static bool IsClassicAttachStatusBuffSpell(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.AttachStatus &&
            spellId is 1_075 or 1_085 or 1_090 or 1_095;
    }

    private static bool IsPoisonStarAttachStatusTargetDebuff(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.AttachStatus &&
            spellId == 6_002;
    }

    private static bool IsSoulShackleAttachStatusTargetDebuff(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.AttachStatus &&
            spellId == 10_405;
    }

    private static bool IsMagicDefenderAttachStatusTargetBuff(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.AttachStatus &&
            spellId == 30_000;
    }

    private static bool IsInfiniteMistAttachStatusTargetBuff(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.AttachStatus &&
            spellId == 16_380;
    }

    private static bool IsHawksAmbitionAttachStatusSelfBuff(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.AttachStatus &&
            spellId == 15_920;
    }

    private static bool IsRevengeTailAttachStatusSelfToggle(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.AttachStatus &&
            spellId == 13_030;
    }

    private static bool IsDragonSwingAttachStatusSelfToggle(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.AttachStatus &&
            spellId == 12_200;
    }

    private static bool IsAutoHuntAttachStatusSelfToggle(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.AttachStatus &&
            spellId == 12_270;
    }

    private static bool IsFlyAttachStatusSelfBuff(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.AttachStatus &&
            spellId == 8_003;
    }

    private static bool IsCastPrayAttachStatusSelfBuff(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.AttachStatus &&
            spellId == 9_876;
    }

    private static bool IsIntensifyAttachStatusFocusCharge(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.AttachStatus &&
            spellId is 9_000 or 11_670;
    }

    private static bool IsInfinityAttachStatusFlyBuff(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.AttachStatus &&
            spellId == 14_380;
    }

    private static bool IsSacrificeAttachStatusSelfToggle(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.AttachStatus &&
            spellId == 14_320;
    }

    private static bool IsRiseOfTaoismAttachStatusSelfBuff(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.AttachStatus &&
            spellId == 15_440;
    }

    private static bool IsIronGuardAttachStatusSelfBuff(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.AttachStatus &&
            spellId == 15_470;
    }

    private static bool IsIronShieldAttachStatusSelfBuff(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.AttachStatus &&
            spellId == 14_160;
    }

    private static bool IsFineRainAttachStatusTeamBuff(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.AttachStatus &&
            spellId == 14_500;
    }

    private static bool IsCrackStarAttachStatusSelfBuff(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.AttachStatus &&
            spellId == 15_380;
    }

    private static bool IsBackfireAttachStatusSelfBuff(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.AttachStatus &&
            spellId == 12_680;
    }

    private static bool IsPathOfShadowAttachStatusSelfToggle(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.AttachStatus &&
            spellId == 11_620;
    }

    private static bool IsDefensiveStanceAttachStatusSelfToggle(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.AttachStatus &&
            spellId == 11_160;
    }

    private static bool IsLightningShieldAttachStatusSelfToggle(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.AttachStatus &&
            spellId == 15_750;
    }

    private static bool IsImmortalForceAttachStatusSelfBuff(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.AttachStatus &&
            spellId == 14_710;
    }

    private static bool IsSnowAttachStatusSelfToggle(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.AttachStatus &&
            spellId is 12_950 or 12_960 or 13_020;
    }

    private static bool IsCombatGearAttachStatusSelfBuff(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.AttachStatus &&
            spellId is 20_300 or 20_350 or 20_370;
    }

    private static bool IsCombatGearWildPhoenixSelfStatusBuff(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.AttachStatus &&
            spellId == 20_370;
    }

    private static LegacyMagicSortCapabilityInfo CreateInfo(
        LegacyMagicSort sort,
        ushort spellId,
        LegacyMagicSortCapability capability,
        LegacyMagicModelingStatus status,
        string reason,
        string sourceEvidence,
        string suggestedNextOwner,
        string nextSeam,
        bool? supported = null)
    {
        return new LegacyMagicSortCapabilityInfo(
            sort,
            spellId,
            capability,
            status,
            Supported: supported ?? status is LegacyMagicModelingStatus.FullyModeled or LegacyMagicModelingStatus.PartiallyModeled,
            SortName: GetSortName(sort),
            Reason: reason,
            SourceEvidence: sourceEvidence,
            SuggestedNextOwner: suggestedNextOwner,
            NextSeam: nextSeam);
    }

    private static bool IsVisibleMonsterDamageSupportedSort(LegacyMagicSort sort)
    {
        return sort is
            LegacyMagicSort.Attack or
            LegacyMagicSort.DirectAttack or
            LegacyMagicSort.Bomb or
            LegacyMagicSort.Sector or
            LegacyMagicSort.Line or
            LegacyMagicSort.ManualLine;
    }

    private static bool IsModeledEagleEyeDirectAttackRuntimeSlice(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.DirectAttack && spellId == 11_030;
    }

    private static bool IsModeledMountControlRuntimeSlice(LegacyMagicSort sort, ushort spellId)
    {
        return (sort, spellId) is
            (LegacyMagicSort.Riding, 7_001) or
            (LegacyMagicSort.Spook, 7_002) or
            (LegacyMagicSort.WarCry, 7_003);
    }

    private static bool IsSourceSilentNoOpSort(LegacyMagicSort sort)
    {
        return (int)sort is
            0 or
            33 or
            35 or
            36 or
            41 or
            42 or
            43 or
            44 or
            45 or
            59 or
            66 or
            72 or
            100 or
            101 or
            102 or
            106 or
            107;
    }

    private static bool IsPirateSpecialSort(LegacyMagicSort sort)
    {
        return (int)sort is 56 or 57 or 58 or 60 or 61 or 62 or 63;
    }

    private static bool IsModeledBombPirteRuntimeSlice(LegacyMagicSort sort, ushort spellId)
    {
        return (int)sort == 60 &&
            spellId is 0 or 11_070;
    }

    private static bool IsModeledScurvyBombRuntimeSlice(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.ScurvyBomb &&
            spellId == 11_040;
    }

    private static bool IsModeledCannonBarrageRuntimeSlice(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.CannonBarrage &&
            spellId == 11_050;
    }

    private static bool IsModeledPirateXpSkillRuntimeSlice(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.PirateXpSkill &&
            spellId == 11_060;
    }

    private static bool IsModeledBlackSpotRuntimeSlice(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.BlackSpot &&
            spellId == 11_120;
    }

    private static bool IsModeledAddBlackSpotRuntimeSlice(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.AddBlackSpot &&
            spellId == 11_100;
    }

    private static bool IsModeledMoveLineRuntimeSlice(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.MoveLine &&
            spellId == 11_110;
    }

    private static bool IsModeledFastBladeScentSwordManualLineRuntimeSlice(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.ManualLine &&
            spellId is 1_045 or 1_046;
    }

    private static bool IsModeledViperFangLineRuntimeSlice(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.Line &&
            spellId == 11_005;
    }

    private static bool IsModeledDragonTailLineRuntimeSlice(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.Line &&
            spellId == 11_000;
    }

    private static bool IsModeledDragonSlashLineRuntimeSlice(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.Line &&
            spellId == 12_350;
    }

    private static bool IsModeledThunderStrikerLineRuntimeSlice(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.Line &&
            spellId == 15_810;
    }

    private static bool IsModeledSpeedGunLineRuntimeSlice(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.Line &&
            spellId == 1_260;
    }

    private static bool IsModeledHookMoonLineRuntimeSlice(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.Line &&
            spellId == 15_930;
    }

    private static bool IsModeledRageOfWarLineRuntimeSlice(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.Line &&
            spellId == 12_930;
    }

    private static bool IsFastBladeScentSwordLineSourceSilentSpell(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.Line &&
            spellId is 13_450 or 13_460 or 18_003;
    }

    private static bool IsRoamerWildwindSourceSilentSpell(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.Wildwind &&
            spellId == 7_040;
    }

    private static bool IsKnownPirateSpecialSpellId(ushort spellId)
    {
        return spellId is
            >= 11_040 and <= 11_044 or
            11_050 or
            >= 11_060 and <= 11_064 or
            >= 11_070 and <= 11_074 or
            >= 11_100 and <= 11_104 or
            >= 11_110 and <= 11_114 or
            >= 11_120 and <= 11_124 or
            13_300 or
            13_310 or
            13_570 or
            13_580 or
            >= 20_320 and <= 20_329 or
            >= 21_020 and <= 21_029 or
            21_270;
    }

    private static bool IsReviveSupportSort(LegacyMagicSort sort, ushort spellId)
    {
        return sort == (LegacyMagicSort)7 ||
            spellId is 1_050 or 1_100 or 6_004 or 16_610;
    }

    private static bool IsRecruitSupportSort(LegacyMagicSort sort)
    {
        return sort == (LegacyMagicSort)2;
    }

    private static bool IsAddManaSupportSort(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.AddMana && (spellId == 0 || spellId == 1_195);
    }

    private static bool IsXpSkillSort(LegacyMagicSort sort, ushort spellId)
    {
        return sort == (LegacyMagicSort)11 ||
            spellId is 1_040 or 12_280;
    }

    private static bool IsModeledSpecialXpSkillActivationSlice(LegacyMagicSort sort, ushort spellId)
    {
        return (sort == LegacyMagicSort.BladeFlurry && spellId == 11_610) ||
            (sort == LegacyMagicSort.ChainBolt && spellId == 10_309) ||
            (sort == LegacyMagicSort.DragonCyclone && spellId == 12_290) ||
            (sort == LegacyMagicSort.Omnipotence && spellId == 12_860);
    }

    private static bool IsSummonPetSort(LegacyMagicSort sort)
    {
        return sort == (LegacyMagicSort)23;
    }

    private static bool IsLayTrapSort(LegacyMagicSort sort)
    {
        return sort == LegacyMagicSort.LayTrap;
    }

    private static bool IsDirectTideTrapLayTrapRow(LegacyMagicSort sort, ushort spellId)
    {
        return IsLayTrapSort(sort) &&
            spellId == 14_250;
    }

    private static bool IsModeledLayTrapRuntimeSlice(LegacyMagicSort sort, ushort spellId)
    {
        return IsLayTrapSort(sort) &&
            (spellId == LegacyLayTrap.TwilightDanceSpellId ||
                spellId == LegacyLayTrap.DaggerStormSpellId ||
                spellId == LegacyLayTrap.SpaceLeapSpellId ||
                spellId == LegacyLayTrap.AuroraLotusSpellId ||
                spellId == LegacyLayTrap.FlameLotusSpellId ||
                spellId == LegacyLayTrap.WaterShockwaveSpellId ||
                spellId == LegacyLayTrap.WaterShockwavePassiveSpellId ||
                spellId == LegacyLayTrap.DustDetachmentSpellId ||
                spellId == LegacyLayTrap.DustDetachmentPassiveSpellId ||
                spellId == LegacyLayTrap.SickleWindSpellId ||
                spellId == LegacyLayTrap.SickleWindPassiveSpellId ||
                spellId == LegacyLayTrap.FlameOfDestructionSpellId ||
                spellId == LegacyLayTrap.FlameOfDestructionPassiveSpellId ||
                spellId == LegacyLayTrap.SourceCase12550SpellId ||
                spellId == LegacyLayTrap.SourceCase12570SpellId ||
                spellId == LegacyLayTrap12990SpellId ||
                spellId == BloomOfDeathLayTrapSpellId ||
                spellId == ThunderStrikerLayTrapSpellId);
    }

    private static bool IsPhysicalSpellsSort(LegacyMagicSort sort)
    {
        return (int)sort is 55;
    }

    private static bool IsModeledAttackStatusRuntimeSlice(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.AttackStatus;
    }

    private static bool IsModeledFatalCrossMonsterRuntimeSlice(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.FatalCross &&
            spellId == 11_980;
    }

    private static bool IsModeledDragonWhirlRuntimeSlice(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.DragonWhirl &&
            spellId is 0 or 10_315;
    }

    private static bool IsModeledChargingVortexRuntimeSlice(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.ChargingVortex &&
            spellId is 0 or 11_190;
    }

    private static bool IsModeledShockRuntimeSlice(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.Shock &&
            spellId is 0 or 15_720;
    }

    private static bool IsModeledGunfireStrikeRuntimeSlice(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.Strike &&
            spellId == 14_590;
    }

    private static bool IsModeledManiacDanceRuntimeSlice(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.ManiacDance && spellId == 12_700;
    }

    private static bool IsModeledShurikenVortexRuntimeSlice(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.ShurikenVortex && spellId == 6_010;
    }

    private static bool IsModeledScarOfEarthRuntimeSlice(LegacyMagicSort sort, ushort spellId)
    {
        return (int)sort == 82 && spellId == 12_670;
    }

    private static bool IsModeledHellVortex16370RuntimeSlice(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.HellVortex && spellId == 16_370;
    }

    private static bool IsModeledStrike13050RuntimeSlice(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.Strike &&
            spellId == 13_050;
    }

    private static bool IsModeledStrike12600RuntimeSlice(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.Strike &&
            spellId == 12_600;
    }

    private static bool IsModeledStraightFist12760RuntimeSlice(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.StraightFist && spellId == 12_760;
    }

    private static bool IsModeledStraightFist12230SourceSilentRuntimeSlice(
        LegacyMagicSort sort,
        ushort spellId)
    {
        return sort == LegacyMagicSort.StraightFist && spellId == 12_230;
    }

    private static bool IsModeledRectangle13060RuntimeSlice(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.Rectangle && spellId == 13_060;
    }

    private static bool IsModeledRectangle15690RuntimeSlice(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.Rectangle && spellId == 15_690;
    }

    private static bool IsUnsupportedStrikeSort(LegacyMagicSort sort)
    {
        return sort == LegacyMagicSort.Strike;
    }

    private static bool IsModeledToxicFogRuntimeSlice(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.Toxic &&
            spellId == 6_001;
    }

    private static bool IsUnsupportedToxicPoisonSort(LegacyMagicSort sort)
    {
        return sort == LegacyMagicSort.Toxic;
    }

    private static bool IsModeledBreathFocusRuntimeSlice(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.BreathFocus &&
            spellId is 0 or 11_960;
    }

    private static bool IsModeledKineticSparkDirectToggleRuntimeSlice(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.KineticSpark &&
            spellId == 11_590;
    }

    // Correct the audited field split without duplicating the long legacy evidence transcript.
    private static LegacyMagicSortCapabilityInfo NormalizeKineticSparkNinjaSageInfo(
        LegacyMagicSortCapabilityInfo info)
    {
        return info with
        {
            Reason = NormalizeKineticSparkNinjaSageEvidence(info.Reason),
            SourceEvidence = NormalizeKineticSparkNinjaSageEvidence(info.SourceEvidence),
            SuggestedNextOwner = NormalizeKineticSparkNinjaSageEvidence(info.SuggestedNextOwner),
        };
    }

    private static string NormalizeKineticSparkNinjaSageEvidence(string evidence)
    {
        return evidence
            .Replace(
                "ReceiveAttack.Player.Execute lines 94-96",
                "ReceiveAttack.Player.Execute lines 23-31 use gouyu_immortal Chance / 100 for Ninja SageMode, and lines 94-96",
                StringComparison.Ordinal)
            .Replace(
                "current ReceiveAttack.Npc.cs lines 213-224 apply the Ninja SageMode branch using gouyu_immortals Rate / 100",
                "ReceiveAttack.Npc.cs lines 23-31 apply the Ninja SageMode branch using gouyu_immortals Chance / 100",
                StringComparison.Ordinal)
            .Replace(
                "bounded player receive-side Ninja SageMode branch using gouyu_immortal Rate",
                "bounded player receive-side Ninja SageMode branch using gouyu_immortal Chance",
                StringComparison.Ordinal)
            .Replace(
                "bounded SOB receive-side Ninja SageMode branch using gouyu_immortal Rate",
                "bounded SOB receive-side Ninja SageMode branch using gouyu_immortal Chance",
                StringComparison.Ordinal);
    }

    private static bool IsModeledAxeShadowCelestialDanceStatusSetter(LegacyMagicSort sort, ushort spellId)
    {
        return (sort == LegacyMagicSort.Wildwind && spellId == 15_940) ||
            (sort == LegacyMagicSort.Bomb && spellId == 7_030);
    }

    private static bool IsArcherCrescentShadowPreKineticProcSpell(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.Bomb &&
            spellId == 18_750;
    }

    private static bool IsArcherNebulousHuntPreKineticProcSpell(LegacyMagicSort sort, ushort spellId)
    {
        return (int)sort == 69 &&
            spellId == 10_990;
    }

    private static bool IsModeledSectorBack18240RuntimeSlice(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.SectorBack &&
            spellId == 18_240;
    }

    private static bool IsModeledCleanSweepSectorBackRuntimeSlice(LegacyMagicSort sort, ushort spellId)
    {
        return sort == LegacyMagicSort.SectorBack &&
            spellId == 15_870;
    }

    private static bool IsCruelShadeDecLifeSpell(LegacyMagicSort sort, ushort spellId)
    {
        return (int)sort == 26 &&
            spellId == 3_050;
    }

    private static bool IsGapingWoundsDecLifeSpell(LegacyMagicSort sort, ushort spellId)
    {
        return (int)sort == 26 &&
            spellId == 11_230;
    }

    private static bool IsSpecialCombatDamageSort(LegacyMagicSort sort)
    {
        return (int)sort is
            16 or
            24 or
            26 or
            27 or
            28 or
            29 or
            30 or
            31 or
            34 or
            37 or
            38 or
            40 or
            54 or
            55 or
            64 or
            65 or
            67 or
            68 or
            69 or
            70 or
            71 or
            73 or
            75 or
            76 or
            78 or
            79 or
            81 or
            82 or
            83 or
            84 or
            85 or
            86 or
            87 or
            90 or
            91 or
            92 or
            94 or
            95 or
            98 or
            99 or
            103 or
            104 or
            105;
    }

    private static bool IsStateAndBuffRuntimeSort(LegacyMagicSort sort)
    {
        return (int)sort is
            10 or
            17 or
            18 or
            19 or
            20 or
            21 or
            22 or
            25 or
            32 or
            46 or
            47 or
            50 or
            51 or
            52 or
            53 or
            88 or
            89;
    }

    private static string GetCapabilityName(LegacyMagicSortCapability capability)
    {
        return capability switch
        {
            LegacyMagicSortCapability.VisibleMonsterDamage => "visibleMonsterDamage",
            LegacyMagicSortCapability.AttachStatusBuff => "attachStatusBuff",
            LegacyMagicSortCapability.SupportHealing => "supportHealing",
            LegacyMagicSortCapability.ReviveSupport => "reviveSupport",
            LegacyMagicSortCapability.ManaRestoreSupport => "manaRestoreSupport",
            LegacyMagicSortCapability.StaminaRestoreSupport => "staminaRestoreSupport",
            LegacyMagicSortCapability.XpSkill => "xpSkill",
            LegacyMagicSortCapability.SummonPet => "summonPet",
            LegacyMagicSortCapability.FloorTrapRuntime => "floorTrapRuntime",
            LegacyMagicSortCapability.SpecialCombatDamage => "specialCombatDamage",
            LegacyMagicSortCapability.PirateSpecial => "pirateSpecial",
            LegacyMagicSortCapability.StateAndBuffRuntime => "stateAndBuffRuntime",
            LegacyMagicSortCapability.PoisonStatusRuntime => "poisonStatusRuntime",
            LegacyMagicSortCapability.SourceSilentNoOp => "sourceSilentNoOp",
            LegacyMagicSortCapability.UnknownUnsupported => "unknownUnsupported",
            _ => "unknownUnsupported",
        };
    }

    private static string GetStatusName(LegacyMagicModelingStatus status)
    {
        return status switch
        {
            LegacyMagicModelingStatus.FullyModeled => "fullyModeled",
            LegacyMagicModelingStatus.PartiallyModeled => "partiallyModeled",
            LegacyMagicModelingStatus.SourceSilentNoOp => "sourceSilentNoOp",
            LegacyMagicModelingStatus.MissingRuntimeNotModeled => "missingRuntimeNotModeled",
            _ => "missingRuntimeNotModeled",
        };
    }
}
