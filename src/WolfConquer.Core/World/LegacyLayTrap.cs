namespace WolfConquer.Core.World;

public enum LegacyLayTrapProjectionStatus
{
    Projected,
    UnsupportedSpell,
    NoDeterministicLine,
    MissingRandomCoordinates,
}

public enum LegacyLayTrapSpellFamily
{
    TwilightDance,
    DaggerStorm,
    SourceCase12550,
    SourceCase12570,
    SpaceLeap,
    AuroraLotus,
    FlameLotus,
    WaterShockwave,
}

public enum LegacyLayTrapTargetKind
{
    Player,
    Monster,
    SobNpc,
}

public enum LegacyRandomFourLayTrapFacing
{
    North,
    South,
    East,
    West,
    NorthWest,
    SouthWest,
    NorthEast,
    SouthEast,
}

public readonly record struct LegacyLayTrapCoordinate(ushort X, ushort Y);

public readonly record struct LegacyRandomFourLayTrapRoll(
    LegacyRandomFourLayTrapFacing Facing,
    sbyte Distance);

public sealed record LegacyLayTrapResolvedTarget(
    uint Uid,
    LegacyLayTrapTargetKind Kind,
    ushort X,
    ushort Y);

public sealed record LegacyLayTrapCastInput(
    ushort SpellId,
    byte SpellLevel,
    byte SpellSoulLevel,
    uint CasterUid,
    uint GuildId,
    ushort CasterX,
    ushort CasterY,
    ushort TargetX,
    ushort TargetY,
    uint FirstFloorUid,
    IReadOnlyList<LegacyLayTrapCoordinate>? RandomFourCoordinates = null,
    LegacyLayTrapResolvedTarget? ResolvedTarget = null);

public sealed record LegacyLayTrapSpellDefinition(
    LegacyLayTrapSpellFamily Family,
    ushort SpellId,
    LegacyMagicSort Sort,
    string Name,
    uint FloorItemId,
    byte FloorColor,
    LegacyFloorItemDropType DropType,
    string FloorName,
    byte CoordinateMaxDistance,
    int FirstTrapLineIndex,
    int TrapLineIndexStride,
    int MaxTraps,
    int TriggerDelayStepMilliseconds,
    int ElseTriggerDelayMilliseconds,
    int TriggerRadius,
    int VisibilityRadius,
    uint RemovalActionType,
    int PlayerDamagePercent);

public sealed record LegacyLayTrapClientFloorSpellState(
    uint OwnerUid,
    ushort X,
    ushort Y,
    byte SoulLevel,
    ushort AttackerX,
    ushort AttackerY);

public sealed record LegacyLayTrapCastAnimation(
    uint CasterUid,
    uint TargetUid,
    ushort X,
    ushort Y,
    ushort SpellId,
    byte SpellLevel,
    byte SoulLevel,
    bool SendsSelfInteraction,
    uint InteractionDamage,
    uint InteractionAttackType);

public sealed record LegacyLayTrapFloorEffect(
    int Index,
    uint FloorUid,
    uint ItemId,
    ushort X,
    ushort Y,
    byte Color,
    LegacyFloorItemDropType DropType,
    uint ItemOwnerUid,
    uint GuildId,
    ushort OwnerX,
    ushort OwnerY,
    string Name,
    int InvokeDelayMilliseconds,
    int ElseInvokeDelayMilliseconds,
    int TriggerRadius,
    int RemainingTrapCountAtTrigger,
    int PlayerAttackBonus,
    int PlayerDamagePercent,
    bool RemovesOnTrigger,
    uint AnimationTargetDamage = 0,
    uint AnimationTargetHit = 0,
    ushort AnimationTargetMoveX = 0,
    ushort AnimationTargetMoveY = 0);

public sealed record LegacyLayTrapProjection(
    LegacyLayTrapProjectionStatus Status,
    string Reason,
    LegacyLayTrapSpellDefinition? Definition,
    LegacyLayTrapClientFloorSpellState? ClientFloorSpellState,
    LegacyLayTrapCastAnimation? CastAnimation,
    IReadOnlyList<LegacyLayTrapFloorEffect> FloorEffects,
    IReadOnlyList<string> SourceEvidence,
    IReadOnlyList<string> PendingSourceBehaviors)
{
    public bool Projected => Status == LegacyLayTrapProjectionStatus.Projected;
}

public sealed record LegacyLayTrapRuntimeMetadata(
    int TriggerRadius,
    int PlayerDamagePercent,
    uint RemovalActionType,
    LegacyFloorItemDropType RemovalDropType,
    int RemovalVisibilityRadius)
{
    public decimal PlayerDamageMultiplier => PlayerDamagePercent / 100m;

    public bool IsTargetInsideTriggerRadius(
        ushort trapX,
        ushort trapY,
        ushort targetX,
        ushort targetY)
    {
        return GetLegacyDistance(trapX, trapY, targetX, targetY) <= TriggerRadius;
    }

    public bool IsViewerInsideRemovalVisibility(
        ushort trapX,
        ushort trapY,
        ushort viewerX,
        ushort viewerY)
    {
        return GetLegacyDistance(trapX, trapY, viewerX, viewerY) <= RemovalVisibilityRadius;
    }

    public uint ApplyPlayerDamageMultiplier(uint damage)
    {
        return checked((uint)((ulong)damage * (uint)PlayerDamagePercent / 100u));
    }

    private static long GetLegacyDistance(long x, long y, long x2, long y2)
    {
        return Math.Max(Math.Abs(x - x2), Math.Abs(y - y2));
    }
}

public sealed record LegacyLayTrapRemovalActionProjection(
    uint OwnerUid,
    ushort OwnerX,
    ushort OwnerY,
    ushort TrapX,
    ushort TrapY,
    uint ActionType);

public static class LegacyLayTrap
{
    public const ushort TwilightDanceSpellId = 12_070;
    public const ushort DaggerStormSpellId = 11_600;
    public const ushort SourceCase12550SpellId = 12_550;
    public const ushort SourceCase12570SpellId = 12_570;
    public const ushort SpaceLeapSpellId = 15_620;
    public const ushort AuroraLotusSpellId = 12_370;
    public const ushort FlameLotusSpellId = 12_380;
    public const ushort WaterShockwaveSpellId = 16_310;
    public const ushort WaterShockwavePassiveSpellId = 16_320;
    public const ushort DustDetachmentSpellId = 16_460;
    public const ushort DustDetachmentPassiveSpellId = 16_470;
    public const ushort SickleWindSpellId = 16_500;
    public const ushort SickleWindPassiveSpellId = 16_510;
    public const ushort FlameOfDestructionSpellId = 16_540;
    public const ushort FlameOfDestructionPassiveSpellId = 16_550;
    public const byte LayTrapSortValue = 21;
    public const byte TwilightDanceCoordinateMaxDistance = 160;
    public const uint TwilightDanceFloorItemId = 40;
    public const byte TwilightDanceFloorColor = 0;
    public const string TwilightDanceFloorName = "trap";
    public const int TwilightDanceFirstTrapLineIndex = 3;
    public const int TwilightDanceTrapLineIndexStride = 7;
    public const int TwilightDanceMaxTraps = 3;
    public const int TwilightDanceTriggerDelayStepMilliseconds = 300;
    public const int TwilightDanceElseTriggerDelayMilliseconds = 0;
    public const int TwilightDanceTriggerRadius = 3;
    public const int TwilightDanceVisibilityRadius = 18;
    public const uint TwilightDanceRemovalActionType = 434;
    public const int TwilightDancePlayerDamagePercent = 70;
    public const LegacyFloorItemDropType TwilightDanceRemovalDropType = LegacyFloorItemDropType.RemoveEffect;
    public const uint DaggerStormNormalFloorItemId = 50;
    public const uint DaggerStormSoulOneFloorItemId = 41;
    public const uint DaggerStormSoulTwoFloorItemId = 42;
    public const byte DaggerStormFloorColor = 11;
    public const byte DaggerStormFlowerType = 11;
    public const int DaggerStormFloorLifetimeMilliseconds = 4_000;
    public const string DaggerStormFloorName = "trap";
    public const uint SpaceLeapFloorItemId = 930;
    public const byte SpaceLeapFloorColor = 17;
    public const byte SpaceLeapFlowerType = 3;
    public const ushort SpaceLeapFloorLife = 5;
    public const int SpaceLeapFloorLifetimeMilliseconds = 17_000;
    public const string SpaceLeapFloorName = "SpaceLeap";
    public const ushort SpaceLeapTeamActionType = 451;
    public const int SpaceLeapTeamRemainTime = 11;
    public const int SpaceLeapTeamDistance = 5_555;
    public const uint AuroraLotusFloorItemId = 930;
    public const uint FlameLotusFloorItemId = 940;
    public const ushort LotusFloorLife = 25;
    public const byte LotusFloorColor = 13;
    public const byte LotusFlowerType = 2;
    public const int LotusFloorLifetimeMilliseconds = 7_000;
    public const string AuroraLotusFloorName = "AuroraLotus";
    public const string FlameLotusFloorName = "FlameLotus";
    public const int FullPowerWaterStatusFlag = 0xAC;
    public const int FullPowerFireStatusFlag = 0xAD;
    public const uint AuroraLotusAppendIconFlag = FullPowerWaterStatusFlag;
    public const uint FlameLotusAppendIconFlag = FullPowerFireStatusFlag;
    public const uint LotusAppendIconTime = 3;
    public const uint LotusAppendIconDamageAfterReset = 0;
    public const uint LotusAppendIconSpellLevel = 0;
    public const uint WaterShockwaveFloorItemId = 2_560;
    public const byte WaterShockwaveFloorColor = 23;
    public const string WaterShockwaveFloorName = "STR_TRAP_ID_2537@@";
    public const int WaterShockwaveInvokeMilliseconds = 3_000;
    public const int WaterShockwaveRepeatMilliseconds = 1_000;
    public const int WaterShockwaveLineRange = 15;
    public const int WaterShockwaveLineWidth = 2;
    public const int WaterShockwaveRemovalVisibilityRadius = 18;
    public const uint DustDetachmentFloorItemId = 2_570;
    public const byte DustDetachmentFloorColor = 14;
    public const int DustDetachmentInvokeMilliseconds = 5_000;
    public const int DustDetachmentRepeatMilliseconds = 1_000;
    public const int DustDetachmentCastSobRadius = 6;
    public const int DustDetachmentRemovalVisibilityRadius = 18;
    public const uint SickleWindFloorItemId = 2_540;
    public const byte SickleWindFloorColor = 14;
    public const int SickleWindInvokeMilliseconds = 3_000;
    public const int SickleWindRepeatMilliseconds = 1_000;
    public const int SickleWindCastSobRadius = 6;
    public const int SickleWindRepeatRadius = 5;
    public const int SickleWindRemovalVisibilityRadius = 18;
    public const uint FlameOfDestructionFloorItemId = 2_530;
    public const byte FlameOfDestructionFloorColor = 14;
    public const int FlameOfDestructionInvokeMilliseconds = 4_000;
    public const int FlameOfDestructionRepeatMilliseconds = 1_000;
    public const int FlameOfDestructionRemovalVisibilityRadius = 18;
    public const uint SourceCase12550FloorItemId = 1_001_390;
    public const uint SourceCase12570FloorItemId = 1_001_380;
    public const byte SourceCases12550And12570FloorColor = 14;
    public const int SourceCase12550FloorDurationMilliseconds = 4_000;
    public const int SourceCase12570FloorDurationMilliseconds = 1_500;
    public const int SourceCases12550And12570ElseDelayMilliseconds = 0;
    public const int SourceCase12550MaxTraps = 4;
    public const int SourceCase12570MaxTraps = 1;
    public const int SourceCase12550TriggerRadius = 3;
    public const int SourceCase12570TriggerRadius = 2;
    public const int SourceCases12550And12570RemovalVisibilityRadius = 18;
    public const int SourceCases12550And12570PlayerAnimationDamageNumerator = 2;
    public const int SourceCases12550And12570PlayerAnimationDamageDenominator = 3;

    public static LegacyLayTrapSpellDefinition TwilightDance { get; } = new(
        LegacyLayTrapSpellFamily.TwilightDance,
        TwilightDanceSpellId,
        LegacyMagicSort.LayTrap,
        "TwilightDance",
        TwilightDanceFloorItemId,
        TwilightDanceFloorColor,
        LegacyFloorItemDropType.Effect,
        TwilightDanceFloorName,
        TwilightDanceCoordinateMaxDistance,
        TwilightDanceFirstTrapLineIndex,
        TwilightDanceTrapLineIndexStride,
        TwilightDanceMaxTraps,
        TwilightDanceTriggerDelayStepMilliseconds,
        TwilightDanceElseTriggerDelayMilliseconds,
        TwilightDanceTriggerRadius,
        TwilightDanceVisibilityRadius,
        TwilightDanceRemovalActionType,
        TwilightDancePlayerDamagePercent);

    public static LegacyLayTrapSpellDefinition SourceCase12550 { get; } = new(
        LegacyLayTrapSpellFamily.SourceCase12550,
        SourceCase12550SpellId,
        LegacyMagicSort.LayTrap,
        "LayTrap.cs case 12550",
        SourceCase12550FloorItemId,
        SourceCases12550And12570FloorColor,
        LegacyFloorItemDropType.Effect,
        "",
        CoordinateMaxDistance: 0,
        FirstTrapLineIndex: 0,
        TrapLineIndexStride: 0,
        SourceCase12550MaxTraps,
        SourceCase12550FloorDurationMilliseconds,
        SourceCases12550And12570ElseDelayMilliseconds,
        SourceCase12550TriggerRadius,
        SourceCases12550And12570RemovalVisibilityRadius,
        RemovalActionType: 0,
        PlayerDamagePercent: 0);

    public static LegacyLayTrapSpellDefinition SourceCase12570 { get; } = new(
        LegacyLayTrapSpellFamily.SourceCase12570,
        SourceCase12570SpellId,
        LegacyMagicSort.LayTrap,
        "LayTrap.cs case 12570",
        SourceCase12570FloorItemId,
        SourceCases12550And12570FloorColor,
        LegacyFloorItemDropType.Effect,
        "",
        CoordinateMaxDistance: 0,
        FirstTrapLineIndex: 0,
        TrapLineIndexStride: 0,
        SourceCase12570MaxTraps,
        SourceCase12570FloorDurationMilliseconds,
        SourceCases12550And12570ElseDelayMilliseconds,
        SourceCase12570TriggerRadius,
        SourceCases12550And12570RemovalVisibilityRadius,
        RemovalActionType: 0,
        PlayerDamagePercent: 0);

    public static LegacyLayTrapRuntimeMetadata TwilightDanceRuntime { get; } = new(
        TwilightDanceTriggerRadius,
        TwilightDancePlayerDamagePercent,
        TwilightDanceRemovalActionType,
        TwilightDanceRemovalDropType,
        TwilightDanceVisibilityRadius);

    public static IReadOnlyList<string> TwilightDanceSourceEvidence { get; } =
    [
        "data/DBServer\\magictype.txt rows 12070-12076 use type 12070, sort 21, and name TwilightDance.",
        "references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer.AttackHandler\\LayTrap.cs case 12070 creates LayTrapThree(user.Player.X, Attack.X, user.Player.Y, Attack.Y, 160), then FloorSpell(40u, lCoord.X, lCoord.Y, color 0, Spell, num4, 0, user), OwnerX/Y from the caster, Name \"trap\", ItemOwnerUID from the caster, and increments num4 by 300.",
        "references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer.AttackHandler.Algoritms\\LayTrapThree.cs builds a rounded DDA line and selects indices 3, 10, and 17 by starting at 3, stepping by 7, and stopping at three traps.",
        "references/DecompiledSource\\GameServer\\MrRayzo.Role\\FloorSpell.cs sets DropType to MsgDropID.Effect and Stamp to DateTime.Now.AddMilliseconds(MillisecondsStamp).",
        "references/DecompiledSource\\GameServer\\MrRayzo\\ThreadPool.cs case 12070 invokes ready floor spells, hits monsters/players/SOB NPCs within distance <= 3, applies 70% player damage, sends ActionQuery Type 434 with owner and trap coordinates, removes the effect, and broadcasts removal within distance <= 18.",
    ];

    public static IReadOnlyList<string> SourceCases12550And12570Evidence { get; } =
    [
        "references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer.AttackHandler\\LayTrap.cs case 12550 sends self InteractionCreate, creates MsgSpellAnimation(caster UID, caster UID, Attack.X/Y, spell id/level/soul), creates RandomFourLayTraps(user.Player.X, user.Player.Y), creates FloorSpell(1001390u, coord.X/Y, color 14, Spell, 4000, 0, user), queues an animation target for the floor UID with Damage=1, Hit=1, MoveX/Y=user.Player.X/Y, sends each floor item, calls UpdateSpell.CheckUpdate(..., 10000), then sends animation.",
        "references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer.AttackHandler.Algoritms\\RandomFourLayTraps.cs loops four times from the caster tile, chooses one of paired facings with Pool.GetRandom.Next() % 10 < 3, chooses distance Pool.GetRandom.Next(3, 10), applies UpdatePosition, and stores the resulting coordinates.",
        "references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer.AttackHandler\\LayTrap.cs case 12570 creates MsgSpellAnimation(caster UID, caster UID, Attack.X/Y, spell id/level/soul), resolves Attack.OpponentUID as visible Player, Monster, or SobNpc, creates FloorSpell(1001380u, target.X/Y, color 14, Spell, 1500, 0, user) only when a target resolves, queues the floor UID animation target with Damage=1, Hit=1, MoveX/Y=user.Player.X/Y, sends the floor item, calls UpdateSpell.CheckUpdate(..., 10000), then sends animation even when no target resolves.",
        "references/DecompiledSource\\GameServer\\MrRayzo.Role\\FloorSpell.cs constructor sets floor UID, item ID, X/Y, color, DropType Effect, Stamp=DateTime.Now.AddMilliseconds(MillisecondsStamp), and ElseStamp=DateTime.Now.",
        "references/DecompiledSource\\GameServer\\MrRayzo\\ThreadPool.cs cases 12550/12570 invoke due floor spells, use radius <= 3 for 12550 and <= 2 for 12570, set MsgSpellAnimation.bomb=1, apply monster/player/SOB physical damage, display player damage as Damage * 2 / 3 after the player ReceiveAttack call, then sends MsgDropID.RemoveEffect to viewers within distance <= 18.",
    ];

    public static IReadOnlyList<string> DaggerStormSourceEvidence { get; } =
    [
        "data/DBServer\\magictype.txt rows 11600-11604 use type 11600, sort 21, and name DaggerStorm.",
        "references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer.AttackHandler\\LayTrap.cs case 11600 creates MsgSpellAnimation(caster UID, caster UID, Attack.X/Y, spell id/level/soul), chooses floor item 50/41/42 for UseSpellSoul 0/1/2, creates a MsgItem effect at Attack.X/Y, sets Name \"trap\", color 11, FlowerType 11, ItemOwnerUID from the caster, GuildID from the caster, stores DBSkill, sends the floor effect, queues caster SpellObj damage 0, runs UpdateSpell.CheckUpdate(..., 10000), and sends animation.",
        "references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgFloorItem\\MsgItem.cs stores the specialSecound constructor argument and treats specialSecound=4 as the effect lifetime; SendAll(RemoveEffect) rewrites floor item 41/42 back to 50 on removal.",
        "references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgFloorItem\\MsgItemPacket.cs defines NormalDaggerStorm=50, SoulOneDaggerStorm=41, and SoulTwoDaggerStorm=42.",
    ];

    public static IReadOnlyList<string> SpaceLeapSourceEvidence { get; } =
    [
        "data/DBServer\\magictype.txt rows 2282-2308 define SpaceLeap spell 15620 as sort 21.",
        "references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer.AttackHandler\\LayTrap.cs lines 344-395, case 15620, runs after CanUseSpell.Verified, breaks silently for non-Water, qualifier, or blocked maps, creates FloorSpell(930u, Attack.X, Attack.Y, color 17, Spell, 17000, 0, user), sets FlowerType=3, Life=MaxLife=5, ItemOwnerUID/GuildID from caster, Name \"SpaceLeap\", queues caster animation target Damage=0 Hit=1 MoveY=caster Y, sends team ActionQuery Type 451 RemainTime=11, applies UpdateSpell.CheckUpdate(..., 10000), and sends animation.",
    ];

    public static IReadOnlyList<string> LotusSourceEvidence { get; } =
    [
        "data/DBServer\\magictype.txt rows 1411-1424 define AuroraLotus spell 12370 and FlameLotus spell 12380 as sort 21 LayTrap.",
        "references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer.AttackHandler\\LayTrap.cs lines 423-466, cases 12370 and 12380, run after CanUseSpell.Verified, require TaoistPower >= 9, and AuroraLotus also rejects Constants.NoRev maps before creating the lotus floor effect.",
        "LayTrap.cs cases 12370/12380 mutate the attack to a self InteractionCreate with Damage=0 and AtkType=0, create MsgSpellAnimation(caster, caster, Attack.X/Y, spell id/level/soul), then create one floor effect at Attack.X/Y using item 930 named AuroraLotus or item 940 named FlameLotus.",
        "references/DecompiledSource\\GameServer\\MrRayzo\\Server.cs lines 667-719 and references/DecompiledSource\\GameServer\\MrRayzo.Role\\GameMap.cs lines 1112-1164 AddFloor create a floor effect with MaxLife=Life=25, DropType Effect, color 13, owner/guild, FlowerType=2, Timer=TqTimer(DateTime.Now.AddSeconds(7)), DBSpell, SpellLevel, and the passed name.",
        "references/DecompiledSource\\GameServer\\MrRayzo.Role\\Core.cs lines 42-52 defines TqTimer packing as 10000000000000*(year-1900)+100000000000*(month-1)+100000000*(dayOfYear-1)+day*1000000+hour*10000+minute*100+second.",
        "references/DecompiledSource\\GameServer\\MrRayzo.Role\\Player.cs lines 3228-3245 sends MsgUpdate AppendIcon 0x4E with flag 172 for Water or 173 for Fire, Time=3, Dmg=TaoistPower*30, Level=0; after lotus success TaoistPower is reset to 0, so Dmg=0, and the corresponding FullPowerWater 0xAC or FullPowerFire 0xAD status flag is removed.",
    ];

    public static IReadOnlyList<string> WaterShockwaveSourceEvidence { get; } =
    [
        "data/DBServer\\magictype.txt rows 2509-2528 define WaterShockwave 16310 and WaterShockwave(Passive) 16320 as sort 21 LayTrap.",
        "references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer.AttackHandler\\LayTrap.cs lines 27-49, cases 16310/16320, create MsgSpellAnimation(caster UID, 0, Attack.X/Y, spell id/level/soul), initialize ClientFloorSpells when missing, create FloorSpell(2560u, Attack.X, Attack.Y, color 23, Spell, 3000, 1000, user), then rewrite FloorPacket.OwnerX/Y to Attack.X/Y and FloorPacket.m_X/m_Y to the caster X/Y, set GuildID, ItemOwnerUID, color 23, Name STR_TRAP_ID_2537@@, send the floor item, and send the animation.",
        "references/DecompiledSource\\GameServer\\MrRayzo\\ThreadPool.cs lines 1116-1264 invoke WaterShockwave floor spells: before the 3000ms invoke it repeats a bomb=1 radius damage pulse every 1000ms, then at invoke time sends bomb=2 line damage/pushback along caster-to-target with line range 15 and width 2, removes the floor spell from Player.FloorSpells, and clears the spell bucket when empty.",
        "references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer.AttackHandler.Calculate\\Physical.cs lines 301-304 scales WaterShockwave player damage by DBSpell.Damage3 % 1000 / 180.",
    ];

    public static IReadOnlyList<string> FlameOfDestructionSourceEvidence { get; } =
    [
        "data/DBServer\\magictype.txt rows 2739-2758 define FlameofDestruction 16540 and FlameofDestruction(Passive) 16550 as sort 21 LayTrap.",
        "references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer.AttackHandler\\LayTrap.cs lines 50-71 create MsgSpellAnimation(caster UID, 0, Attack.X/Y, spell id/level/soul), initialize ClientFloorSpells, create FloorSpell(2530u, Attack.X, Attack.Y, color 14, Spell, 4000, 1000, user), set OwnerX/Y to caster X/Y, GuildID, ItemOwnerUID=0, color 14, and no special name.",
        "references/DecompiledSource\\GameServer\\MrRayzo\\ThreadPool.cs lines 1331-1429 repeat before due with bomb=1, attacker=floor UID, X/Y=floor packet X/Y, hits monsters/players/SOBs within DBSkill.MaxTargets, then due final sends RemoveEffect to viewers within distance <= 18 with no final damage.",
        "references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer.AttackHandler.Calculate\\Physical.cs lines 979-990 applies BackDmg, MudWall, and then the 16540/16550 lethal-hit guard before ThreadPool.cs lines 1385-1403 applies its DamageOnHuman clamp, ReceiveAttack, soul display transform, and unconditional player animation target enqueue.",
        "ThreadPool.cs cases 16540/16550 also apply modeled FlameSigilRapid gear-contributor behavior as an equipment bonus during the repeat pulse branch.",
    ];

    public static IReadOnlyList<string> DustDetachmentSourceEvidence { get; } =
    [
        "data/DBServer\\magictype.txt rows 2659-2678 define DustDetachment 16460 and DustDetachment(Passive) 16470 as sort 21 LayTrap.",
        "references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer.AttackHandler\\LayTrap.cs lines 73-106 create MsgSpellAnimation(caster UID, 0, Attack.X/Y, spell id/level/soul), initialize ClientFloorSpells, create FloorSpell(2570u, Attack.X, Attack.Y, color 14, Spell, 5000, 1000, user), set OwnerX/Y to caster X/Y, GuildID, ItemOwnerUID=0, color 14, and no special name.",
        "LayTrap.cs cases 16460/16470 immediately hit visible SOB NPCs within distance <= 6 from caster X/Y through CanAttackNpc.Verified and Physical.OnNpcs, display Base.CalculateSoul(damage, UseSpellSoul) / 2, mutate SOB HP, queue the target in the caster animation, and send role/user updates.",
        "references/DecompiledSource\\GameServer\\MrRayzo\\ThreadPool.cs lines 1431-1505 repeat before due with bomb=1, attacker=floor UID, X/Y=floor packet X/Y, hits monsters and players within DBSkill.MaxTargets, displays monster Base.CalculateSoul(damage, LevelHu) / 4 and player source damage unchanged, then due final sends RemoveEffect to viewers within distance <= 18 from the caster-origin ClientFloorSpells X/Y.",
        "ThreadPool.cs cases 16460/16470 also apply modeled DustSigilStunn gear-contributor behavior as a player-target stun side effect during the repeat pulse branch.",
    ];

    public static IReadOnlyList<string> SickleWindSourceEvidence { get; } =
    [
        "data/DBServer\\magictype.txt rows 2699-2718 define SickleWind 16500 and SickleWind(Passive) 16510 as sort 21 LayTrap.",
        "references/DecompiledSource\\GameServer\\MrRayzo.Game.MsgServer.AttackHandler\\LayTrap.cs lines 108-140 create MsgSpellAnimation(caster UID, 0, Attack.X/Y, spell id/level/soul), initialize ClientFloorSpells, create FloorSpell(2540u, Attack.X, Attack.Y, color 14, Spell, 3000, 1000, user), set OwnerX/Y to caster X/Y, GuildID, ItemOwnerUID=0, color 14, and no special name.",
        "LayTrap.cs cases 16500/16510 immediately hit visible SOB NPCs within distance <= 6 from caster X/Y through CanAttackNpc.Verified and Physical.OnNpcs, display Base.CalculateSoul(damage, UseSpellSoul) / 2, mutate SOB HP, queue the target in the caster animation, and send the cast animation only to the user.",
        "references/DecompiledSource\\GameServer\\MrRayzo\\ThreadPool.cs lines 1506-1572 repeat before due with bomb=1, attacker=floor UID, X/Y=floor packet X/Y, hits monsters and players within hard-coded distance <= 5, displays monster Base.CalculateSoul(damage, LevelHu) * 55 / 100 and player Base.CalculateSoul(damage, LevelHu), then due final sends RemoveEffect to viewers within distance <= 18 from the caster-origin ClientFloorSpells X/Y.",
        "ThreadPool.cs cases 16500/16510 have no SOB repeat branch.",
    ];

    public static bool TryGetDefinition(ushort spellId, out LegacyLayTrapSpellDefinition definition)
    {
        if (spellId == TwilightDanceSpellId)
        {
            definition = TwilightDance;
            return true;
        }

        if (spellId == SourceCase12550SpellId)
        {
            definition = SourceCase12550;
            return true;
        }

        if (spellId == SourceCase12570SpellId)
        {
            definition = SourceCase12570;
            return true;
        }

        definition = default!;
        return false;
    }

    public static uint GetDaggerStormFloorItemId(byte useSpellSoul)
    {
        return useSpellSoul switch
        {
            0 => DaggerStormNormalFloorItemId,
            1 => DaggerStormSoulOneFloorItemId,
            2 => DaggerStormSoulTwoFloorItemId,
            _ => 0,
        };
    }

    public static bool IsTwilightDanceTriggerTargetInRange(
        ushort trapX,
        ushort trapY,
        ushort targetX,
        ushort targetY)
    {
        return TwilightDanceRuntime.IsTargetInsideTriggerRadius(trapX, trapY, targetX, targetY);
    }

    public static bool IsTwilightDanceRemovalVisibleToViewer(
        ushort trapX,
        ushort trapY,
        ushort viewerX,
        ushort viewerY)
    {
        return TwilightDanceRuntime.IsViewerInsideRemovalVisibility(trapX, trapY, viewerX, viewerY);
    }

    public static bool IsSpaceLeapBlockedMap(long mapId)
    {
        return mapId is
            10_397 or
            10_398 or
            10_399 or
            10_401 or
            10_402 or
            10_403 or
            10_404 or
            10_437 or
            10_438 or
            10_439 or
            10_440 or
            10_441 or
            10_442 or
            10_770 or
            10_298 or
            10_762 or
            10_761 or
            10_300 or
            10_760 or
            10_593 or
            10_763 or
            10_299 or
            10_079 or
            3_872 or
            10_759 or
            10_301;
    }

    public static bool IsWaterShockwaveSpellId(ushort spellId)
    {
        return spellId is WaterShockwaveSpellId or WaterShockwavePassiveSpellId;
    }

    public static bool IsDustDetachmentSpellId(ushort spellId)
    {
        return spellId is DustDetachmentSpellId or DustDetachmentPassiveSpellId;
    }

    public static bool IsSickleWindSpellId(ushort spellId)
    {
        return spellId is SickleWindSpellId or SickleWindPassiveSpellId;
    }

    public static bool IsFlameOfDestructionSpellId(ushort spellId)
    {
        return spellId is FlameOfDestructionSpellId or FlameOfDestructionPassiveSpellId;
    }

    public static ulong PackLegacyTqTimer(DateTime dateTime)
    {
        return
            10_000_000_000_000UL * checked((ulong)(dateTime.Year - 1900)) +
            100_000_000_000UL * checked((ulong)(dateTime.Month - 1)) +
            100_000_000UL * checked((ulong)(dateTime.DayOfYear - 1)) +
            1_000_000UL * checked((ulong)dateTime.Day) +
            10_000UL * checked((ulong)dateTime.Hour) +
            100UL * checked((ulong)dateTime.Minute) +
            checked((ulong)dateTime.Second);
    }

    public static uint ApplyTwilightDancePlayerDamageMultiplier(uint damage)
    {
        return TwilightDanceRuntime.ApplyPlayerDamageMultiplier(damage);
    }

    public static LegacyLayTrapRemovalActionProjection BuildTwilightDanceRemovalAction(
        LegacyLayTrapFloorEffect effect)
    {
        ArgumentNullException.ThrowIfNull(effect);

        return BuildTwilightDanceRemovalAction(
            effect.ItemOwnerUid,
            effect.OwnerX,
            effect.OwnerY,
            effect.X,
            effect.Y);
    }

    public static LegacyLayTrapRemovalActionProjection BuildTwilightDanceRemovalAction(
        uint ownerUid,
        ushort ownerX,
        ushort ownerY,
        ushort trapX,
        ushort trapY)
    {
        return new LegacyLayTrapRemovalActionProjection(
            ownerUid,
            ownerX,
            ownerY,
            trapX,
            trapY,
            TwilightDanceRuntime.RemovalActionType);
    }

    public static LegacyLayTrapProjection Project(LegacyLayTrapCastInput input)
    {
        if (!TryGetDefinition(input.SpellId, out var definition))
        {
            return new LegacyLayTrapProjection(
                LegacyLayTrapProjectionStatus.UnsupportedSpell,
                $"LayTrap helper currently models only source cases {TwilightDanceSpellId}, {SourceCase12550SpellId}, and {SourceCase12570SpellId}; spell {input.SpellId} remains a parent wiring/runtime seam.",
                null,
                null,
                null,
                [],
                TwilightDanceSourceEvidence.Concat(SourceCases12550And12570Evidence).ToArray(),
                ["Other LayTrap.cs branches remain outside this bounded helper slice."]);
        }

        if (input.SpellId == SourceCase12550SpellId)
        {
            return ProjectSourceCase12550(input, definition);
        }

        if (input.SpellId == SourceCase12570SpellId)
        {
            return ProjectSourceCase12570(input, definition);
        }

        var coordinates = SelectTwilightDanceCoordinates(
            input.CasterX,
            input.CasterY,
            input.TargetX,
            input.TargetY);
        if (coordinates.Count == 0)
        {
            return new LegacyLayTrapProjection(
                LegacyLayTrapProjectionStatus.NoDeterministicLine,
                "Source LayTrapThree divides by the line step count; identical caster and target coordinates do not provide a finite deterministic trap line.",
                definition,
                null,
                null,
                [],
                TwilightDanceSourceEvidence,
                ["Runtime should decide whether to reject or source-capture same-tile TwilightDance casts before wiring."]);
        }

        var effects = new List<LegacyLayTrapFloorEffect>(coordinates.Count);
        for (var index = 0; index < coordinates.Count; index++)
        {
            var coordinate = coordinates[index];
            var remainingTrapCount = coordinates.Count - index;
            effects.Add(new LegacyLayTrapFloorEffect(
                index,
                unchecked(input.FirstFloorUid + (uint)index),
                definition.FloorItemId,
                coordinate.X,
                coordinate.Y,
                definition.FloorColor,
                definition.DropType,
                input.CasterUid,
                input.GuildId,
                input.CasterX,
                input.CasterY,
                definition.FloorName,
                index * definition.TriggerDelayStepMilliseconds,
                definition.ElseTriggerDelayMilliseconds,
                definition.TriggerRadius,
                remainingTrapCount,
                GetTwilightDancePlayerAttackBonus(remainingTrapCount),
                definition.PlayerDamagePercent,
                RemovesOnTrigger: true));
        }

        return new LegacyLayTrapProjection(
            LegacyLayTrapProjectionStatus.Projected,
            "Source LayTrap.cs case 12070 projects three TwilightDance floor effects from LayTrapThree and leaves runtime triggering/removal to ThreadPool case 12070.",
            definition,
            new LegacyLayTrapClientFloorSpellState(
                input.CasterUid,
                input.TargetX,
                input.TargetY,
                input.SpellSoulLevel,
                AttackerX: 0,
                AttackerY: 0),
            new LegacyLayTrapCastAnimation(
                input.CasterUid,
                input.CasterUid,
                input.TargetX,
                input.TargetY,
                input.SpellId,
                input.SpellLevel,
                input.SpellSoulLevel,
                SendsSelfInteraction: true,
                InteractionDamage: 0,
                InteractionAttackType: 0),
            effects.ToArray(),
            TwilightDanceSourceEvidence,
            [
                "This sidecar does not perform CanUseSpell.Verified resource/cooldown checks, socket fanout, damage calculation, spell experience, or removal packet emission.",
            ]);
    }

    public static IReadOnlyList<LegacyLayTrapCoordinate> SelectTwilightDanceCoordinates(
        ushort casterX,
        ushort casterY,
        ushort targetX,
        ushort targetY,
        byte maxDistance = TwilightDanceCoordinateMaxDistance)
    {
        var deltaX = targetX - casterX;
        var deltaY = targetY - casterY;
        var steps = Math.Max(Math.Abs(deltaX), Math.Abs(deltaY));
        if (steps == 0)
        {
            return [];
        }

        var line = new List<LegacyLayTrapCoordinate>(maxDistance + 1)
        {
            new(casterX, casterY),
        };
        var currentX = (float)casterX;
        var currentY = (float)casterY;
        var stepX = (float)deltaX / steps;
        var stepY = (float)deltaY / steps;

        for (var index = 0; index < maxDistance; index++)
        {
            currentX += stepX;
            currentY += stepY;
            line.Add(new LegacyLayTrapCoordinate(
                ToLegacyUInt16(Math.Round(currentX)),
                ToLegacyUInt16(Math.Round(currentY))));
        }

        var trapCoordinates = new List<LegacyLayTrapCoordinate>(TwilightDanceMaxTraps);
        for (var index = TwilightDanceFirstTrapLineIndex; index < line.Count; index += TwilightDanceTrapLineIndexStride)
        {
            if (trapCoordinates.Count == TwilightDanceMaxTraps)
            {
                break;
            }

            trapCoordinates.Add(line[index]);
        }

        return trapCoordinates;
    }

    public static int GetTwilightDancePlayerAttackBonus(int remainingTrapCount)
    {
        return remainingTrapCount switch
        {
            3 => 15,
            2 => 25,
            1 => 35,
            _ => 0,
        };
    }

    public static IReadOnlyList<LegacyLayTrapCoordinate> SelectRandomFourLayTrapCoordinates(
        ushort casterX,
        ushort casterY,
        IReadOnlyList<LegacyRandomFourLayTrapRoll> rolls)
    {
        ArgumentNullException.ThrowIfNull(rolls);

        var coordinates = new List<LegacyLayTrapCoordinate>(Math.Min(SourceCase12550MaxTraps, rolls.Count));
        for (var index = 0; index < rolls.Count && index < SourceCase12550MaxTraps; index++)
        {
            var x = casterX;
            var y = casterY;
            UpdateRandomFourLayTrapPosition(rolls[index].Facing, rolls[index].Distance, ref x, ref y);
            coordinates.Add(new LegacyLayTrapCoordinate(x, y));
        }

        return coordinates;
    }

    public static void UpdateRandomFourLayTrapPosition(
        LegacyRandomFourLayTrapFacing facing,
        sbyte count,
        ref ushort x,
        ref ushort y)
    {
        var deltaX = 0;
        var deltaY = 0;
        switch (facing)
        {
            case LegacyRandomFourLayTrapFacing.North:
                deltaX -= count;
                deltaY -= count;
                break;
            case LegacyRandomFourLayTrapFacing.South:
                deltaX = count;
                deltaY = count;
                break;
            case LegacyRandomFourLayTrapFacing.East:
                deltaX = count;
                deltaY -= count;
                break;
            case LegacyRandomFourLayTrapFacing.West:
                deltaX -= count;
                deltaY = count;
                break;
            case LegacyRandomFourLayTrapFacing.NorthWest:
                deltaX -= count;
                break;
            case LegacyRandomFourLayTrapFacing.SouthWest:
                deltaY = count;
                break;
            case LegacyRandomFourLayTrapFacing.NorthEast:
                deltaY -= count;
                break;
            case LegacyRandomFourLayTrapFacing.SouthEast:
                deltaX = count;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(facing), facing, "Unknown RandomFourLayTraps facing.");
        }

        x = unchecked((ushort)(x + deltaX));
        y = unchecked((ushort)(y + deltaY));
    }

    private static LegacyLayTrapProjection ProjectSourceCase12550(
        LegacyLayTrapCastInput input,
        LegacyLayTrapSpellDefinition definition)
    {
        if (input.RandomFourCoordinates is null || input.RandomFourCoordinates.Count == 0)
        {
            return new LegacyLayTrapProjection(
                LegacyLayTrapProjectionStatus.MissingRandomCoordinates,
                "LayTrap.cs case 12550 depends on RandomFourLayTraps Pool.GetRandom rolls; pure projection requires caller-supplied source-equivalent coordinates.",
                definition,
                null,
                BuildSourceCase12550Or12570Animation(input, sendsSelfInteraction: true),
                [],
                SourceCases12550And12570Evidence,
                [
                    "Provide RandomFourCoordinates from source-captured rolls or SelectRandomFourLayTrapCoordinates; this helper does not invent random coordinates.",
                    "Runtime should still perform UpdateSpell.CheckUpdate(..., 10000) and send the animation after floor item emission.",
                ]);
        }

        var count = Math.Min(SourceCase12550MaxTraps, input.RandomFourCoordinates.Count);
        var effects = new List<LegacyLayTrapFloorEffect>(count);
        for (var index = 0; index < count; index++)
        {
            var coordinate = input.RandomFourCoordinates[index];
            effects.Add(BuildSourceCase12550Or12570FloorEffect(
                definition,
                index,
                unchecked(input.FirstFloorUid + (uint)index),
                coordinate.X,
                coordinate.Y,
                input.CasterX,
                input.CasterY,
                definition.TriggerDelayStepMilliseconds));
        }

        return new LegacyLayTrapProjection(
            LegacyLayTrapProjectionStatus.Projected,
            "Source LayTrap.cs case 12550 projects four RandomFourLayTraps floor effects and queues one animation target per created floor UID.",
            definition,
            new LegacyLayTrapClientFloorSpellState(
                input.CasterUid,
                input.TargetX,
                input.TargetY,
                input.SpellSoulLevel,
                AttackerX: 0,
                AttackerY: 0),
            BuildSourceCase12550Or12570Animation(input, sendsSelfInteraction: true),
            effects.ToArray(),
            SourceCases12550And12570Evidence,
            [
                "This sidecar does not perform CanUseSpell.Verified resource/cooldown checks, socket fanout, random roll generation, or spell experience updates.",
                "LayTrap.cs case 12550 does not assign FloorPacket owner/guild/name fields after FloorSpell construction; those fields are left at local model defaults.",
            ]);
    }

    private static LegacyLayTrapProjection ProjectSourceCase12570(
        LegacyLayTrapCastInput input,
        LegacyLayTrapSpellDefinition definition)
    {
        if (input.ResolvedTarget is null)
        {
            return new LegacyLayTrapProjection(
                LegacyLayTrapProjectionStatus.Projected,
                "Source LayTrap.cs case 12570 still updates spell progress and sends animation when Attack.OpponentUID resolves to no visible Player, Monster, or SobNpc.",
                definition,
                null,
                BuildSourceCase12550Or12570Animation(input, sendsSelfInteraction: false),
                [],
                SourceCases12550And12570Evidence,
                [
                    "No floor item or animation target is projected because no source-visible target resolved.",
                    "Runtime should still perform UpdateSpell.CheckUpdate(..., 10000) and send the animation.",
                ]);
        }

        var effect = BuildSourceCase12550Or12570FloorEffect(
            definition,
            index: 0,
            input.FirstFloorUid,
            input.ResolvedTarget.X,
            input.ResolvedTarget.Y,
            input.CasterX,
            input.CasterY,
            definition.TriggerDelayStepMilliseconds);

        return new LegacyLayTrapProjection(
            LegacyLayTrapProjectionStatus.Projected,
            "Source LayTrap.cs case 12570 projects one target-position floor effect when Attack.OpponentUID resolves to a visible Player, Monster, or SobNpc.",
            definition,
            new LegacyLayTrapClientFloorSpellState(
                input.CasterUid,
                input.TargetX,
                input.TargetY,
                input.SpellSoulLevel,
                AttackerX: 0,
                AttackerY: 0),
            BuildSourceCase12550Or12570Animation(input, sendsSelfInteraction: false),
            [effect],
            SourceCases12550And12570Evidence,
            [
                "This sidecar does not perform visible target resolution, CanUseSpell.Verified resource/cooldown checks, socket fanout, or spell experience updates.",
                "LayTrap.cs case 12570 does not assign FloorPacket owner/guild/name fields after FloorSpell construction; those fields are left at local model defaults.",
            ]);
    }

    private static LegacyLayTrapCastAnimation BuildSourceCase12550Or12570Animation(
        LegacyLayTrapCastInput input,
        bool sendsSelfInteraction)
    {
        return new LegacyLayTrapCastAnimation(
            input.CasterUid,
            input.CasterUid,
            input.TargetX,
            input.TargetY,
            input.SpellId,
            input.SpellLevel,
            input.SpellSoulLevel,
            sendsSelfInteraction,
            InteractionDamage: 0,
            InteractionAttackType: 0);
    }

    private static LegacyLayTrapFloorEffect BuildSourceCase12550Or12570FloorEffect(
        LegacyLayTrapSpellDefinition definition,
        int index,
        uint floorUid,
        ushort x,
        ushort y,
        ushort casterX,
        ushort casterY,
        int durationMilliseconds)
    {
        return new LegacyLayTrapFloorEffect(
            index,
            floorUid,
            definition.FloorItemId,
            x,
            y,
            definition.FloorColor,
            definition.DropType,
            ItemOwnerUid: 0,
            GuildId: 0,
            OwnerX: 0,
            OwnerY: 0,
            definition.FloorName,
            durationMilliseconds,
            definition.ElseTriggerDelayMilliseconds,
            definition.TriggerRadius,
            RemainingTrapCountAtTrigger: 0,
            PlayerAttackBonus: 0,
            definition.PlayerDamagePercent,
            RemovesOnTrigger: false,
            AnimationTargetDamage: 1,
            AnimationTargetHit: 1,
            AnimationTargetMoveX: casterX,
            AnimationTargetMoveY: casterY);
    }

    private static ushort ToLegacyUInt16(double value)
    {
        return unchecked((ushort)(int)value);
    }
}
