using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed class LegacyMagicSortCapabilitiesTests
{
    [Theory]
    [InlineData(1, "Attack(1)")]
    [InlineData(4, "Sector(4)")]
    [InlineData(5, "Bomb(5)")]
    [InlineData(14, "Line(14)")]
    [InlineData(48, "DirectAttack(48)")]
    [InlineData(96, "ManualLine(96)")]
    public void ClassifyExistingVisibleMonsterSortsAsPartiallyModeled(int sortValue, string expectedSortName)
    {
        var info = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)sortValue, spellId: 1_000);

        Assert.Equal(LegacyMagicSortCapability.VisibleMonsterDamage, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal(expectedSortName, info.SortName);
        Assert.Equal("player-target-and-sob-magic-runtime", info.NextSeam);
        Assert.Contains("visible-monster damage slice", info.Reason);
        Assert.Contains("WorldPacketHandlers.HandleLegacyPlayerMonsterMagicAttackAsync", info.SourceEvidence);
        Assert.Contains("player-target and SOB-target", info.SuggestedNextOwner);
        Assert.Contains("status=partiallyModeled", info.Diagnostic);
    }

    [Fact]
    public void ClassifyGapingWoundsDecLifeIncludesToxinEraserRateGateAndDefaultStatus()
    {
        var info = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)26, spellId: 11_230);

        Assert.Equal(LegacyMagicSortCapability.SpecialCombatDamage, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("gapingwounds-combat-runtime", info.NextSeam);
        Assert.Contains("PerfectionEffects.ToxinEraser rate gate", info.Reason);
        Assert.Contains("source default 0 percent", info.Reason);
        Assert.Contains("PerfectionEffects.cs lines 43-59", info.SourceEvidence);
        Assert.Contains("Base.Rate(Target.Owner.PerfectionStatus.ToxinEraser)", info.SourceEvidence);
        Assert.Contains("source PerfectionStatus initializes ToxinEraser to 0.0", info.SourceEvidence);
        Assert.DoesNotContain("when owner perfection data is present", info.SourceEvidence);
    }

    [Fact]
    public void ClassifyViperFangLineAsDedicatedRuntimeSlice()
    {
        var info = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.Line, 11_005);

        Assert.Equal(LegacyMagicSortCapability.SpecialCombatDamage, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("viperfang-line-runtime", info.NextSeam);
        Assert.Contains("hard-coded width-1 Line target shape", info.Reason);
        Assert.Contains("source counter-hit map HitShoot increments for player hits", info.Reason);
        Assert.Contains("player Physical.OnPlayer damage with player-target WildPhoenix defender charge/archive damage override before the ViperFang Base.CalculateSoul and player /2 scaling step", info.Reason);
        Assert.DoesNotContain("Remaining parity is exact counter-hit map HitShoot state", info.Reason);
        Assert.DoesNotContain("counter-hit-map HitShoot", info.SuggestedNextOwner);
        Assert.Contains("calls Physical.OnPlayer, then Base.CalculateSoul, then divides player damage by 2 before ReceiveAttack.Player.Execute", info.SourceEvidence);
        Assert.Contains("increments HitShoot on player hits in counter-hit maps", info.SourceEvidence);
        Assert.Contains("Physical.cs applies target.Owner.CombatGear.WildPhoenix()", info.SourceEvidence);
        Assert.Contains("Line.cs case 11005", info.SourceEvidence);
        Assert.Contains("status=partiallyModeled", info.Diagnostic);
    }

    [Fact]
    public void ClassifyDragonTailLineAsDedicatedRuntimeSlice()
    {
        var info = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.Line, 11_000);

        Assert.Equal(LegacyMagicSortCapability.SpecialCombatDamage, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("dragontail-line-runtime", info.NextSeam);
        Assert.Contains("hard-coded width-0 Line target shape", info.Reason);
        Assert.Contains("source counter-hit map HitShoot increments for monster hits", info.Reason);
        Assert.Contains("player-target WildPhoenix defender charge/archive damage override", info.Reason);
        Assert.DoesNotContain("Remaining parity is exact counter-hit map HitShoot state", info.Reason);
        Assert.DoesNotContain("counter-hit-map HitShoot", info.SuggestedNextOwner);
        Assert.Contains("Line.cs case 11000", info.SourceEvidence);
        Assert.Contains("status=partiallyModeled", info.Diagnostic);
    }

    [Fact]
    public void ClassifyDragonSlashLineAsDedicatedRuntimeSlice()
    {
        var info = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.Line, 12_350);

        Assert.Equal(LegacyMagicSortCapability.SpecialCombatDamage, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("dragonslash-line-runtime", info.NextSeam);
        Assert.Contains("source constructor range-10 width-0 Line target shape", info.Reason);
        Assert.Contains("source counter-hit map HitShoot increments for monster hits", info.Reason);
        Assert.Contains("source physical player damage scaled by * 10 / 18", info.Reason);
        Assert.Contains("player-target WildPhoenix defender charge/archive damage override", info.Reason);
        Assert.Contains("zero-damage WildPhoenix player target preservation", info.Reason);
        Assert.DoesNotContain("Remaining parity is exact counter-hit map HitShoot state", info.Reason);
        Assert.DoesNotContain("counter-hit-map HitShoot", info.SuggestedNextOwner);
        Assert.Contains("magictype.txt rows 1405-1409", info.SourceEvidence);
        Assert.Contains("Line.cs case 12350", info.SourceEvidence);
        Assert.Contains("status=partiallyModeled", info.Diagnostic);
    }

    [Fact]
    public void ClassifyThunderStrikerLineAsDedicatedRuntimeSlice()
    {
        var info = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.Line, 15_810);

        Assert.Equal(LegacyMagicSortCapability.SpecialCombatDamage, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("thunderstriker-line-runtime", info.NextSeam);
        Assert.Contains("ThunderStriker class gate after resources", info.Reason);
        Assert.Contains("source physical player damage scaled by * 53 / 100", info.Reason);
        Assert.Contains("player-target WildPhoenix defender charge/archive damage override", info.Reason);
        Assert.Contains("zero-damage WildPhoenix player target preservation", info.Reason);
        Assert.Contains("magictype.txt rows 2384-2393", info.SourceEvidence);
        Assert.Contains("Line.cs case 15810", info.SourceEvidence);
        Assert.Contains("status=partiallyModeled", info.Diagnostic);
    }

    [Fact]
    public void ClassifySpeedGunLineAsDedicatedRuntimeSlice()
    {
        var info = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.Line, 1_260);

        Assert.Equal(LegacyMagicSortCapability.SpecialCombatDamage, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("speedgun-line-runtime", info.NextSeam);
        Assert.Contains("hard-coded player distance < 3", info.Reason);
        Assert.Contains("no guard-monster exclusion", info.Reason);
        Assert.Contains("bounded counter-hit map HitShoot increments for source player hits", info.Reason);
        Assert.Contains("player-target WildPhoenix defender charge/archive damage override", info.Reason);
        Assert.DoesNotContain("Remaining parity is exact counter-hit map HitShoot state", info.Reason);
        Assert.DoesNotContain("counter-hit-map HitShoot", info.SuggestedNextOwner);
        Assert.Contains("magictype.txt rows 127-136", info.SourceEvidence);
        Assert.Contains("Line.cs case 1260", info.SourceEvidence);
        Assert.Contains("increments HitShoot on player hits in counter-hit maps", info.SourceEvidence);
        Assert.Contains("WorldPacketHandlers.PlayerMagic.SpeedGun.cs", info.SourceEvidence);
        Assert.Contains("status=partiallyModeled", info.Diagnostic);
    }

    [Fact]
    public void ClassifyHookMoonLineAsDedicatedRuntimeSlice()
    {
        var info = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.Line, 15_930);

        Assert.Equal(LegacyMagicSortCapability.SpecialCombatDamage, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("hookmoon-line-runtime", info.NextSeam);
        Assert.Contains("source constructor range-11 width-2", info.Reason);
        Assert.Contains("player-target WildPhoenix defender charge/archive damage override", info.Reason);
        Assert.Contains("zero-damage WildPhoenix player target preservation", info.Reason);
        Assert.Contains("source HookMoon target/caster status attributes with ArchiveSkill updates", info.Reason);
        Assert.Contains("timed status expiry/duplicate AddFlag", info.Reason);
        Assert.Contains("magictype.txt rows 2465-2474", info.SourceEvidence);
        Assert.Contains("Line.cs case 15930", info.SourceEvidence);
        Assert.Contains("WorldPacketHandlers.PlayerMagic.HookMoon.cs", info.SourceEvidence);
        Assert.Contains("status=partiallyModeled", info.Diagnostic);
    }

    [Fact]
    public void ClassifyRageOfWarLineAsDedicatedRuntimeSlice()
    {
        var info = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.Line, 12_930);

        Assert.Equal(LegacyMagicSortCapability.SpecialCombatDamage, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("rageofwar-line-runtime", info.NextSeam);
        Assert.Contains("hard-coded distance < 11", info.Reason);
        Assert.Contains("bounded counter-hit map HitShoot increments for source initial player hits", info.Reason);
        Assert.Contains("source physical player damage from the initial player Physical.OnPlayer path scaled by * 10 / 13", info.Reason);
        Assert.Contains("player-target WildPhoenix defender charge/archive damage override before the initial RageOfWar Base.CalculateSoul and player *10/13 scaling step", info.Reason);
        Assert.Contains("source-shaped zero-damage WildPhoenix player target preservation", info.Reason);
        Assert.Contains("source 1500 floor-effect item create/broadcast plus animation append metadata", info.Reason);
        Assert.Contains("delayed floor-effect radius damage with bomb flag and RemoveEffect", info.Reason);
        Assert.Contains("delayed floor player Physical.OnPlayer damage with player-target WildPhoenix defender charge/archive damage override before the delayed RageOfWar player *60/100 scaling step", info.Reason);
        Assert.Contains("Remaining RageOfWar parity is exact CanAttack side-effect edges", info.Reason);
        AssertContainsCanAttackPlayerRelationGateReview(info.Reason);
        AssertContainsRemainingCanAttackNpcGaps(info.Reason);
        Assert.DoesNotContain("Remaining parity is exact counter-hit map HitShoot state", info.Reason);
        Assert.DoesNotContain("delayed RageOfWar floor-effect invocation/damage semantics", info.Reason);
        Assert.DoesNotContain("model delayed RageOfWar floor-effect invocation/damage semantics", info.SuggestedNextOwner);
        Assert.DoesNotContain("counter-hit-map HitShoot", info.SuggestedNextOwner);
        Assert.Contains("magictype.txt rows 1577-1583", info.SourceEvidence);
        Assert.Contains("ThreadPool.cs case 12930", info.SourceEvidence);
        Assert.Contains("Line.cs case 12930", info.SourceEvidence);
        Assert.Contains("increments HitShoot on initial player hits in counter-hit maps", info.SourceEvidence);
        Assert.Contains("WorldPacketHandlers.PlayerMagic.RageOfWar.cs", info.SourceEvidence);
        Assert.Contains("models the bounded initial-cast counter-hit map HitShoot state", info.SourceEvidence);
        Assert.Contains("status=partiallyModeled", info.Diagnostic);
    }

    [Theory]
    [InlineData(1_075, "Invisibility 1075 flag 0x16")]
    [InlineData(1_085, "StarOfAccuracy 1085 flag 0x07")]
    [InlineData(1_090, "MagicShield 1090 flag 0x08")]
    [InlineData(1_095, "Stigma 1095 flag 0x09")]
    public void ClassifyClassicAttachStatusBuffCasesAsCompleteAcrossSelfPlayerAndMonsterTargets(
        ushort spellId,
        string expectedCaseEvidence)
    {
        var info = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)6, spellId);

        Assert.Equal(LegacyMagicSortCapability.AttachStatusBuff, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.FullyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.True(info.SupportsAttachStatusSelfBuff);
        Assert.True(info.SupportsAttachStatusPlayerTargetBuff);
        Assert.True(info.SupportsAttachStatusVisibleMonsterTargetBuff);
        Assert.Equal("AttachStatus(6)", info.SortName);
        Assert.Equal(
            "implemented-classic-attach-status-self-visible-player-visible-monster-buff",
            info.NextSeam);
        Assert.Contains(expectedCaseEvidence, info.Reason);
        Assert.Contains("self, visible-player, and visible-monster targets", info.Reason);
        Assert.Contains("opcode 2176 MsgUpdate type 0x19 StatusFlag", info.Reason);
        Assert.Contains("advances duration-backed spell progress", info.Reason);
        Assert.Contains("then sends opcode 2239 zero-damage animation", info.Reason);
        Assert.Contains("already-active MagicShield", info.Reason);
        Assert.Contains("unknown target", info.Reason);
        Assert.Contains("Timed expiry", info.Reason);
        Assert.Contains("player and monster death cleanup", info.Reason);
        Assert.Contains("active monster status replay in spawn packets", info.Reason);
        Assert.Contains(
            "Matching decompiled AttackHandler\\AttachStatus.cs cases 1075/1085/1090/1095 supply behavior and ordering",
            info.SourceEvidence);
        Assert.Contains("WorldPacketHandlers.PlayerMagic.Status.ClassicBuffs.cs", info.SourceEvidence);
        Assert.Contains("WorldServerFrameHandler.LegacyMonsterTimedStatusFlags.cs", info.SourceEvidence);
        Assert.Contains("LegacyMonsterSpawnPacket.cs", info.SourceEvidence);
        Assert.Contains("Exact D13 client\\Env_DX8\\._cache_Conquer.exe", info.SourceEvidence);
        Assert.Contains("owns wire", info.SourceEvidence);
        Assert.Contains("type 0x19 StatusFlag vectors", info.SourceEvidence);
        Assert.Contains("opcode 2239 CMsgMagicEffect animations", info.SourceEvidence);
        Assert.Contains("Matching decompile supplies behavior and ordering", info.SourceEvidence);
        Assert.Contains("The Conquer Source 7770 tree is excluded", info.SourceEvidence);
        Assert.Contains("status=fullyModeled", info.Diagnostic);
    }

    [Theory]
    [InlineData(9_000, "Intensify")]
    [InlineData(11_670, "SpiritFocus")]
    public void ClassifyIntensifyAttachStatusFocusChargeAsModeled(ushort spellId, string expectedName)
    {
        var info = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)6, spellId);

        Assert.Equal(LegacyMagicSortCapability.AttachStatusBuff, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.FullyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.True(info.SupportsAttachStatusSelfBuff);
        Assert.False(info.SupportsAttachStatusPlayerTargetBuff);
        Assert.Equal("AttachStatus(6)", info.SortName);
        Assert.Equal("implemented-attach-status-self-or-visible-player-buff", info.NextSeam);
        Assert.Contains("focus charge", info.Reason);
        Assert.Contains("SpellID 0", info.Reason);
        Assert.Contains("5-second runtime promotion", info.Reason);
        Assert.Contains("Focused flag 0x15", info.Reason);
        Assert.Contains(expectedName, info.Reason);
        Assert.Contains("AttachStatus.cs cases 9000/11670", info.SourceEvidence);
        Assert.Contains("ThreadPool.cs promotes the charge after 5 seconds", info.SourceEvidence);
        Assert.Contains("status=fullyModeled", info.Diagnostic);
    }

    [Fact]
    public void ClassifyPoisonStarAttachStatusDirectCastAsModeledTargetDebuff()
    {
        var info = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)6, 6_002);

        Assert.Equal(LegacyMagicSortCapability.PoisonStatusRuntime, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.FullyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.False(info.SupportsAttachStatusSelfBuff);
        Assert.False(info.SupportsAttachStatusPlayerTargetBuff);
        Assert.Equal("AttachStatus(6)", info.SortName);
        Assert.Equal("implemented-attach-status-target-debuff", info.NextSeam);
        Assert.Contains("PoisonStar target debuff", info.Reason);
        Assert.Contains("Sturdiness avoidance", info.Reason);
        Assert.Contains("80 percent success roll", info.Reason);
        Assert.Contains("AttachStatus.cs case 6002", info.SourceEvidence);
        Assert.Contains("status=fullyModeled", info.Diagnostic);
    }

    [Fact]
    public void ClassifySoulShackleAttachStatusDirectCastAsFullyModeledStateAndBuffRuntime()
    {
        var info = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)6, 10_405);

        Assert.True(info.Supported);
        Assert.Equal(LegacyMagicModelingStatus.FullyModeled, info.Status);
        Assert.Equal(LegacyMagicSortCapability.StateAndBuffRuntime, info.Capability);
        Assert.False(info.SupportsAttachStatusSelfBuff);
        Assert.False(info.SupportsAttachStatusPlayerTargetBuff);
        Assert.Equal("implemented-attach-status-target-debuff", info.NextSeam);

        var claim = $"{info.Reason} {info.SourceEvidence}";
        Assert.Contains("opcode 2176", claim);
        Assert.Contains("0x36", claim);
        Assert.Contains("flag 0x6F", claim);
        Assert.DoesNotContain("2215", claim);
        Assert.DoesNotContain("GameUpdate", claim);
    }

    [Fact]
    public void ClassifyRevengeTailAttachStatusDirectCastAsModeledSelfToggle()
    {
        var info = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)6, 13_030);

        Assert.Equal(LegacyMagicSortCapability.AttachStatusBuff, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.FullyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.True(info.SupportsAttachStatusSelfBuff);
        Assert.False(info.SupportsAttachStatusPlayerTargetBuff);
        Assert.Equal("AttachStatus(6)", info.SortName);
        Assert.Equal("implemented-attach-status-self-or-visible-player-buff", info.NextSeam);
        Assert.Contains("RevengeTail direct cast", info.Reason);
        Assert.Contains("RevengeGale rune boost", info.Reason);
        Assert.Contains("AttachStatus.cs case 13030", info.SourceEvidence);
        Assert.Contains("status=fullyModeled", info.Diagnostic);
    }

    [Fact]
    public void ClassifyDragonSwingAttachStatusDirectCastAsModeledSelfToggle()
    {
        var info = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)6, 12_200);

        Assert.Equal(LegacyMagicSortCapability.AttachStatusBuff, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.FullyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.True(info.SupportsAttachStatusSelfBuff);
        Assert.False(info.SupportsAttachStatusPlayerTargetBuff);
        Assert.Equal("AttachStatus(6)", info.SortName);
        Assert.Equal("implemented-attach-status-self-or-visible-player-buff", info.NextSeam);
        Assert.Contains("DragonSwing direct cast", info.Reason);
        Assert.Contains("active DragonWar participant guard", info.Reason);
        Assert.Contains("DataType.DragonSwing 0x4B", info.Reason);
        Assert.Contains("DragonSwingChance", info.Reason);
        Assert.Contains("AttachStatus.cs case 12200", info.SourceEvidence);
        Assert.Contains("DragonSwing flag 0xA0", info.SourceEvidence);
        Assert.Contains("MsgUpdate.DataType.DragonSwing 0x4B", info.SourceEvidence);
        Assert.Contains("status=fullyModeled", info.Diagnostic);
    }

    [Fact]
    public void ClassifyAutoHuntAttachStatusDirectCastAsModeledSelfToggle()
    {
        var info = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)6, 12_270);

        Assert.Equal(LegacyMagicSortCapability.AttachStatusBuff, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.FullyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.True(info.SupportsAttachStatusSelfBuff);
        Assert.False(info.SupportsAttachStatusPlayerTargetBuff);
        Assert.Equal("AttachStatus(6)", info.SortName);
        Assert.Equal("implemented-attach-status-self-or-visible-player-buff", info.NextSeam);
        Assert.Contains("AutoHunt direct cast", info.Reason);
        Assert.Contains("flag 0x94", info.Reason);
        Assert.Contains("stamp 8", info.Reason);
        Assert.Contains("AutoHunt packet mode", info.Reason);
        Assert.Contains("AttachStatus.cs case 12270", info.SourceEvidence);
        Assert.Contains("AutoHunt flag 0x94", info.SourceEvidence);
        Assert.Contains("status=fullyModeled", info.Diagnostic);
    }

    [Fact]
    public void ClassifyFlyAttachStatusDirectCastAsModeledSelfBuff()
    {
        var info = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)6, 8_003);

        Assert.Equal(LegacyMagicSortCapability.AttachStatusBuff, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.FullyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.True(info.SupportsAttachStatusSelfBuff);
        Assert.False(info.SupportsAttachStatusPlayerTargetBuff);
        Assert.Equal("AttachStatus(6)", info.SortName);
        Assert.Equal("implemented-attach-status-self-or-visible-player-buff", info.NextSeam);
        Assert.Contains("Fly direct cast", info.Reason);
        Assert.Contains("transform/Ride/PathOfShadow gate", info.Reason);
        Assert.Contains("Fly flag 0x1B", info.Reason);
        Assert.Contains("Spell.Duration self target", info.Reason);
        Assert.Contains("no spell-progress tick", info.Reason);
        Assert.Contains("AttachStatus.cs case 8003", info.SourceEvidence);
        Assert.Contains("attacked UID 0", info.SourceEvidence);
        Assert.Contains("status=fullyModeled", info.Diagnostic);
    }

    [Fact]
    public void ClassifyCastPrayAttachStatusDirectCastAsModeledSelfBuff()
    {
        var info = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)6, 9_876);

        Assert.Equal(LegacyMagicSortCapability.AttachStatusBuff, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.FullyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.True(info.SupportsAttachStatusSelfBuff);
        Assert.False(info.SupportsAttachStatusPlayerTargetBuff);
        Assert.Equal("AttachStatus(6)", info.SortName);
        Assert.Equal("implemented-attach-status-self-or-visible-player-buff", info.NextSeam);
        Assert.Contains("CastPray direct cast", info.Reason);
        Assert.Contains("flag 0x1E", info.Reason);
        Assert.Contains("2592000 seconds with RemoveOnDead true", info.Reason);
        Assert.Contains("zero-damage self target", info.Reason);
        Assert.Contains("no spell-progress tick", info.Reason);
        Assert.Contains("AttachStatus.cs case 9876", info.SourceEvidence);
        Assert.Contains("CastPray flag 0x1E", info.SourceEvidence);
        Assert.Contains("status=fullyModeled", info.Diagnostic);
    }

    [Fact]
    public void ClassifyHawksAmbitionAttachStatusDirectCastAsModeledSelfBuff()
    {
        var info = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)6, 15_920);

        Assert.Equal(LegacyMagicSortCapability.AttachStatusBuff, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.FullyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("AttachStatus(6)", info.SortName);
        Assert.Equal("implemented-attach-status-self-or-visible-player-buff", info.NextSeam);
        Assert.Contains("HawksAmbition direct cast", info.Reason);
        Assert.Contains("online expiry", info.Reason);
        Assert.Contains("RemoveOnDead metadata cleanup", info.Reason);
        Assert.Contains("AttachStatus.cs case 15920", info.SourceEvidence);
        Assert.Contains("AttachStatus.cs case 16390", info.SourceEvidence);
        Assert.Contains("StatusFlagsBigVector32.Flag.Expire", info.SourceEvidence);
        Assert.Contains("ThreadPool.cs lines 2956-3038", info.SourceEvidence);
        Assert.Contains("WorldServerFrameHandler.LegacyCombatGearAttachStatus.cs", info.SourceEvidence);
        Assert.Contains("exact-D13 opcode 2176 status-vector fanout", info.SourceEvidence);
        Assert.Contains("7770 is excluded", info.SourceEvidence);
        Assert.Contains("status=fullyModeled", info.Diagnostic);
    }

    [Fact]
    public void ClassifyInfinityAttachStatusDirectCastAsModeledFlyBuff()
    {
        var info = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)6, 14_380);

        Assert.Equal(LegacyMagicSortCapability.AttachStatusBuff, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.FullyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.True(info.SupportsAttachStatusSelfBuff);
        Assert.False(info.SupportsAttachStatusPlayerTargetBuff);
        Assert.Equal("AttachStatus(6)", info.SortName);
        Assert.Equal("implemented-attach-status-self-or-visible-player-buff", info.NextSeam);
        Assert.Contains("Infinity/Fly direct cast", info.Reason);
        Assert.Contains("InfinitySDamage and InfinityDamage", info.Reason);
        Assert.Contains("AttachStatus.cs case 14380", info.SourceEvidence);
        Assert.Contains("status=fullyModeled", info.Diagnostic);
    }

    [Fact]
    public void ClassifySacrificeAttachStatusDirectCastAsModeledSelfToggle()
    {
        var info = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)6, 14_320);

        Assert.Equal(LegacyMagicSortCapability.AttachStatusBuff, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.FullyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.True(info.SupportsAttachStatusSelfBuff);
        Assert.False(info.SupportsAttachStatusPlayerTargetBuff);
        Assert.Equal("AttachStatus(6)", info.SortName);
        Assert.Equal("implemented-attach-status-self-or-visible-player-buff", info.NextSeam);
        Assert.Contains("Sacrifice direct cast", info.Reason);
        Assert.Contains("RemoveOnDead false", info.Reason);
        Assert.Contains("AttachStatus.cs case 14320", info.SourceEvidence);
        Assert.Contains("status=fullyModeled", info.Diagnostic);
    }

    [Fact]
    public void ClassifyRiseOfTaoismAttachStatusDirectCastAsModeledSelfBuff()
    {
        var info = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)6, 15_440);

        Assert.Equal(LegacyMagicSortCapability.AttachStatusBuff, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.FullyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.True(info.SupportsAttachStatusSelfBuff);
        Assert.False(info.SupportsAttachStatusPlayerTargetBuff);
        Assert.Equal("AttachStatus(6)", info.SortName);
        Assert.Equal("implemented-attach-status-self-or-visible-player-buff", info.NextSeam);
        Assert.Contains("RiseofTaoism direct cast", info.Reason);
        Assert.Contains("FineRain countdown update", info.Reason);
        Assert.Contains("AttachStatus.cs case 15440", info.SourceEvidence);
        Assert.Contains("status=fullyModeled", info.Diagnostic);
    }

    [Fact]
    public void ClassifyIronGuardAttachStatusDirectCastAsModeledSelfBuff()
    {
        var info = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)6, 15_470);

        Assert.Equal(LegacyMagicSortCapability.AttachStatusBuff, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.FullyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.True(info.SupportsAttachStatusSelfBuff);
        Assert.False(info.SupportsAttachStatusPlayerTargetBuff);
        Assert.Equal("AttachStatus(6)", info.SortName);
        Assert.Equal("implemented-attach-status-self-or-visible-player-buff", info.NextSeam);
        Assert.Contains("IronGuard direct cast", info.Reason);
        Assert.Contains("SkillCountdown update", info.Reason);
        Assert.Contains("AttachStatus.cs case 15470", info.SourceEvidence);
        Assert.Contains("status=fullyModeled", info.Diagnostic);
    }

    [Fact]
    public void ClassifyIronShieldAttachStatusDirectCastAsModeledSelfBuff()
    {
        var info = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)6, 14_160);

        Assert.Equal(LegacyMagicSortCapability.AttachStatusBuff, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.FullyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.True(info.SupportsAttachStatusSelfBuff);
        Assert.False(info.SupportsAttachStatusPlayerTargetBuff);
        Assert.Equal("AttachStatus(6)", info.SortName);
        Assert.Equal("implemented-attach-status-self-or-visible-player-buff", info.NextSeam);
        Assert.Contains("IronShield direct cast", info.Reason);
        Assert.Contains("AzureShield update", info.Reason);
        Assert.Contains("AttachStatus.cs case 14160", info.SourceEvidence);
        Assert.Contains("status=fullyModeled", info.Diagnostic);
    }

    [Fact]
    public void ClassifyFineRainAttachStatusDirectCastAsModeledTeamBuff()
    {
        var info = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)6, 14_500);

        Assert.Equal(LegacyMagicSortCapability.AttachStatusBuff, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.FullyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.True(info.SupportsAttachStatusSelfBuff);
        Assert.False(info.SupportsAttachStatusPlayerTargetBuff);
        Assert.Equal("AttachStatus(6)", info.SortName);
        Assert.Equal("implemented-attach-status-self-or-visible-player-buff", info.NextSeam);
        Assert.Contains("FineRain direct cast", info.Reason);
        Assert.Contains("team", info.Reason);
        Assert.Contains("AttachStatus.cs case 14500", info.SourceEvidence);
        Assert.Contains("status=fullyModeled", info.Diagnostic);
    }

    [Fact]
    public void ClassifyMagicDefenderAttachStatusDirectCastAsModeledAzureShieldBuff()
    {
        var info = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)6, 30_000);

        Assert.Equal(LegacyMagicSortCapability.AttachStatusBuff, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.FullyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.True(info.SupportsAttachStatusSelfBuff);
        Assert.True(info.SupportsAttachStatusPlayerTargetBuff);
        Assert.False(info.SupportsAttachStatusVisibleMonsterTargetBuff);
        Assert.Equal("AttachStatus(6)", info.SortName);
        Assert.Equal("implemented-attach-status-self-or-visible-player-buff", info.NextSeam);
        Assert.Contains("MagicDefender/AzureShield direct cast", info.Reason);
        Assert.Contains("AzureShieldDefence 50000", info.Reason);
        Assert.Contains("15 seconds per learned level", info.Reason);
        Assert.Contains("AttachStatus.cs case 30000", info.SourceEvidence);
        Assert.Contains("status=fullyModeled", info.Diagnostic);
    }

    [Fact]
    public void ClassifyCrackStarAttachStatusDirectCastAsModeledWindWalkerSelfBuff()
    {
        var info = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)6, 15_380);

        Assert.Equal(LegacyMagicSortCapability.AttachStatusBuff, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.FullyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.True(info.SupportsAttachStatusSelfBuff);
        Assert.False(info.SupportsAttachStatusPlayerTargetBuff);
        Assert.Equal("AttachStatus(6)", info.SortName);
        Assert.Equal("implemented-attach-status-self-or-visible-player-buff", info.NextSeam);
        Assert.Contains("CrackStar direct cast", info.Reason);
        Assert.Contains("MainFlag.OnMeleeAttack", info.Reason);
        Assert.Contains("AttachStatus.cs case 15380", info.SourceEvidence);
        Assert.Contains("status=fullyModeled", info.Diagnostic);
    }

    [Fact]
    public void ClassifyBackfireAttachStatusDirectCastAsModeledSelfBuff()
    {
        var info = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)6, 12_680);

        Assert.Equal(LegacyMagicSortCapability.AttachStatusBuff, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.FullyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.True(info.SupportsAttachStatusSelfBuff);
        Assert.False(info.SupportsAttachStatusPlayerTargetBuff);
        Assert.Equal("AttachStatus(6)", info.SortName);
        Assert.Equal("implemented-attach-status-self-or-visible-player-buff", info.NextSeam);
        Assert.Contains("Backfire direct cast", info.Reason);
        Assert.Contains("10-second duration with RemoveOnDead false", info.Reason);
        Assert.Contains("no interaction echo", info.Reason);
        Assert.Contains("AttachStatus.cs case 12680", info.SourceEvidence);
        Assert.Contains("Backfire flag 0xB3", info.SourceEvidence);
        Assert.Contains("status=fullyModeled", info.Diagnostic);
    }

    [Fact]
    public void ClassifyPathOfShadowAttachStatusDirectCastAsModeledSelfToggle()
    {
        var info = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)6, 11_620);

        Assert.Equal(LegacyMagicSortCapability.AttachStatusBuff, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.FullyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.True(info.SupportsAttachStatusSelfBuff);
        Assert.False(info.SupportsAttachStatusPlayerTargetBuff);
        Assert.Equal("AttachStatus(6)", info.SortName);
        Assert.Equal("implemented-attach-status-self-or-visible-player-buff", info.NextSeam);
        Assert.Contains("PathOfShadow direct cast", info.Reason);
        Assert.Contains("2592000-second duration with RemoveOnDead false", info.Reason);
        Assert.Contains("clears KineticSpark 0x93", info.Reason);
        Assert.Contains("no interaction echo", info.Reason);
        Assert.Contains("AttachStatus.cs case 11620", info.SourceEvidence);
        Assert.Contains("PathOfShadow flag 0x91", info.SourceEvidence);
        Assert.Contains("status=fullyModeled", info.Diagnostic);
    }

    [Fact]
    public void ClassifyDefensiveStanceAttachStatusDirectCastAsModeledSelfToggle()
    {
        var info = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)6, 11_160);

        Assert.Equal(LegacyMagicSortCapability.AttachStatusBuff, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.FullyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.True(info.SupportsAttachStatusSelfBuff);
        Assert.False(info.SupportsAttachStatusPlayerTargetBuff);
        Assert.Equal("AttachStatus(6)", info.SortName);
        Assert.Equal("implemented-attach-status-self-or-visible-player-buff", info.NextSeam);
        Assert.Contains("DefensiveStance direct cast", info.Reason);
        Assert.Contains("Ride removal", info.Reason);
        Assert.Contains("countdown update 0x38", info.Reason);
        Assert.Contains("no interaction echo", info.Reason);
        Assert.Contains("AttachStatus.cs case 11160", info.SourceEvidence);
        Assert.Contains("DefensiveStance flag 0x7E", info.SourceEvidence);
        Assert.Contains("MsgUpdate.DataType.DefensiveStance 0x38", info.SourceEvidence);
        Assert.Contains("status=fullyModeled", info.Diagnostic);
    }

    [Fact]
    public void ClassifyLightningShieldAttachStatusDirectCastAsModeledSelfToggle()
    {
        var info = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)6, 15_750);

        Assert.Equal(LegacyMagicSortCapability.AttachStatusBuff, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.FullyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.True(info.SupportsAttachStatusSelfBuff);
        Assert.False(info.SupportsAttachStatusPlayerTargetBuff);
        Assert.Equal("AttachStatus(6)", info.SortName);
        Assert.Equal("implemented-attach-status-self-or-visible-player-buff", info.NextSeam);
        Assert.Contains("LightningShield direct cast", info.Reason);
        Assert.Contains("LightningShield status flag 0xF0", info.Reason);
        Assert.Contains("LightningShieldActivated 0xED", info.Reason);
        Assert.Contains("AzureShield reset 0x31", info.Reason);
        Assert.Contains("AttachStatus.cs case 15750", info.SourceEvidence);
        Assert.Contains("LightningShield flag 0xF0", info.SourceEvidence);
        Assert.Contains("MsgUpdate.DataType.AzureShield 0x31", info.SourceEvidence);
        Assert.Contains("status=fullyModeled", info.Diagnostic);
    }

    [Fact]
    public void ClassifyImmortalForceAttachStatusDirectCastAsModeledSelfBuff()
    {
        var info = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)6, 14_710);

        Assert.Equal(LegacyMagicSortCapability.AttachStatusBuff, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.FullyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.True(info.SupportsAttachStatusSelfBuff);
        Assert.False(info.SupportsAttachStatusPlayerTargetBuff);
        Assert.Equal("AttachStatus(6)", info.SortName);
        Assert.Equal("implemented-attach-status-self-or-visible-player-buff", info.NextSeam);
        Assert.Contains("ImmortalForce direct cast", info.Reason);
        Assert.Contains("ImmortalForce status flag 0xDC", info.Reason);
        Assert.Contains("RemoveOnDead true", info.Reason);
        Assert.Contains("interaction echo", info.Reason);
        Assert.Contains("AttachStatus.cs case 14710", info.SourceEvidence);
        Assert.Contains("ImmortalForce flag 0xDC", info.SourceEvidence);
        Assert.Contains("status=fullyModeled", info.Diagnostic);
    }

    [Fact]
    public void ClassifyInfiniteMistAttachStatusDirectCastAsModeledTargetBuff()
    {
        var info = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)6, 16_380);

        Assert.Equal(LegacyMagicSortCapability.AttachStatusBuff, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.FullyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.True(info.SupportsAttachStatusPlayerTargetBuff);
        Assert.Equal("AttachStatus(6)", info.SortName);
        Assert.Equal("implemented-attach-status-self-or-visible-player-buff", info.NextSeam);
        Assert.Contains("InfiniteMist direct cast", info.Reason);
        Assert.Contains("AttachStatus.cs case 16380", info.SourceEvidence);
        Assert.Contains("status=fullyModeled", info.Diagnostic);
    }

    [Fact]
    public void ClassifySuperCycloneAttachStatusXpListActivationAsModeledXpSkill()
    {
        var info = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)6, 11_970);

        Assert.Equal(LegacyMagicSortCapability.XpSkill, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.FullyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.True(info.SupportsAttachStatusSelfBuff);
        Assert.False(info.SupportsAttachStatusPlayerTargetBuff);
        Assert.Equal("AttachStatus(6)", info.SortName);
        Assert.Equal("xp-skill-runtime", info.NextSeam);
        Assert.Contains("XPList gating/removal", info.Reason);
        Assert.Contains("SuperCyclone 11970 additionally restores stamina to the 100/150 HeavenBlessing maximum", info.Reason);
        Assert.Contains("opens SuperCyclone for 40 seconds", info.SourceEvidence);
        Assert.Contains("status=fullyModeled", info.Diagnostic);
    }

    [Fact]
    public void ClassifyFatalStrikeAttachStatusXpListActivationAsModeledXpSkill()
    {
        var info = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)6, 6_011);

        Assert.Equal(LegacyMagicSortCapability.XpSkill, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.FullyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.True(info.SupportsAttachStatusSelfBuff);
        Assert.False(info.SupportsAttachStatusPlayerTargetBuff);
        Assert.Equal("AttachStatus(6)", info.SortName);
        Assert.Equal("xp-skill-runtime", info.NextSeam);
        Assert.Contains("XPList gating/removal", info.Reason);
        Assert.Contains("FatalStrike 6011 opens FatalStrike for 60 seconds", info.Reason);
        Assert.Contains("AttachStatus.cs case 6011", info.SourceEvidence);
        Assert.Contains("opens FatalStrike flag 0x2F for 60 seconds", info.SourceEvidence);
        Assert.Contains("zero-damage self target", info.SourceEvidence);
        Assert.Contains("status=fullyModeled", info.Diagnostic);
    }

    [Fact]
    public void ClassifyBladeFlurryXpListActivationAsPartiallyModeledXpSkill()
    {
        var info = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)68, 11_610);

        Assert.Equal(LegacyMagicSortCapability.XpSkill, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("BladeFlurry(68)", info.SortName);
        Assert.Equal("xp-skill-runtime", info.NextSeam);
        Assert.Contains("BladeFlurry", info.Reason);
        Assert.Contains("spell 11610", info.Reason);
        Assert.Contains("consumes XPList", info.Reason);
        Assert.Contains("opens or refreshes BladeFlurry for 50 seconds", info.Reason);
        Assert.Contains("BladeFlurry.cs", info.SourceEvidence);
        Assert.Contains("RemoveFlag(MsgUpdate.Flags.XPList)", info.SourceEvidence);
        Assert.Contains("OpenXpSkill(MsgUpdate.Flags.BladeFlurry, 50)", info.SourceEvidence);
        Assert.Contains("non-XP branch", info.SourceEvidence);
        Assert.Contains("Range.OnMonster/OnPlayer/OnNpcs", info.SourceEvidence);
        Assert.Contains("fixed 185 percent damage inside distance three", info.Reason);
        Assert.Contains(
            "ShurikenVortex or ManiacDance returns Base.CalculateExtraAttack from zero using attacker PhysicalDamageIncrease and defender PhysicalDamageDecrease",
            info.Reason);
        Assert.Contains("WildPhoenix remains a fixed one-damage return", info.Reason);
        Assert.Contains("DefensiveStance applies its 40 percent Block return before BackDmg", info.Reason);
        Assert.Contains("normal eligible hits call BackDmg before receive/progress output and the one shared animation", info.Reason);
        Assert.Contains("Range.cs applies fixed 185 percent player damage inside distance three", info.SourceEvidence);
        Assert.Contains("Base.CalculateExtraAttack(0, PhysicalDamageIncrease, PhysicalDamageDecrease)", info.SourceEvidence);
        Assert.Contains("using attacker PhysicalDamageIncrease and defender PhysicalDamageDecrease", info.SourceEvidence);
        Assert.Contains("WildPhoenix's fixed one-damage return", info.SourceEvidence);
        Assert.Contains("BackDmg.Calculate(..., ranged: true)", info.SourceEvidence);
        Assert.Contains("BladeFlurry sends progression before the shared animation", info.SourceEvidence);
        Assert.Contains(
            "magicSort=BladeFlurry(68) spellId=11610 capability=xpSkill status=partiallyModeled supported=true nextSeam=xp-skill-runtime",
            info.Diagnostic);
    }

    [Fact]
    public void ClassifyChainBoltXpListActivationAsPartiallyModeledXpSkill()
    {
        var info = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)37, 10_309);

        Assert.Equal(LegacyMagicSortCapability.XpSkill, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("ChainBolt(37)", info.SortName);
        Assert.Equal("xp-skill-runtime", info.NextSeam);
        Assert.Contains("ChainBolt", info.Reason);
        Assert.Contains("spell 10309", info.Reason);
        Assert.Contains("consumes XPList", info.Reason);
        Assert.Contains("opens or refreshes ChaintBolt for 50 seconds", info.Reason);
        Assert.Contains("ChainBolt.cs", info.SourceEvidence);
        Assert.Contains("RemoveFlag(MsgUpdate.Flags.XPList)", info.SourceEvidence);
        Assert.Contains("OpenXpSkill(MsgUpdate.Flags.ChaintBolt, 50)", info.SourceEvidence);
        Assert.Contains("targets the clicked primary Monster/Player/SobNpc", info.SourceEvidence);
        Assert.Contains("sets MsgSpell.X/Y to the primary target center", info.SourceEvidence);
        Assert.Contains("chains only same-type visible targets", info.SourceEvidence);
        Assert.Contains("Base.GetDistance(primary,target) < DBSpell.Range / 2", info.SourceEvidence);
        Assert.Contains("Magic.OnMonster/OnPlayer/OnNpcs", info.SourceEvidence);
        Assert.Contains("sends animation only when targets exist", info.SourceEvidence);
        Assert.Contains("BladeFlurry, ChainBolt, DragonCyclone, and Omnipotence also model their non-XP runtime branches", info.Reason);
        Assert.Contains("no special XP active runtime branch remains identified here", info.SuggestedNextOwner);
        Assert.Contains("class skill ownership", info.SuggestedNextOwner);
        Assert.Contains("damage-formula parity", info.SuggestedNextOwner);
        Assert.DoesNotContain("port ChainBolt non-XP chained damage", info.SuggestedNextOwner);
        Assert.Contains(
            "magicSort=ChainBolt(37) spellId=10309 capability=xpSkill status=partiallyModeled supported=true nextSeam=xp-skill-runtime",
            info.Diagnostic);
    }

    [Fact]
    public void ClassifyDragonCycloneXpListActivationAsPartiallyModeledXpSkill()
    {
        var info = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)75, 12_290);

        Assert.Equal(LegacyMagicSortCapability.XpSkill, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("DragonCyclone(75)", info.SortName);
        Assert.Equal("xp-skill-runtime", info.NextSeam);
        Assert.Contains("DragonCyclone", info.Reason);
        Assert.Contains("spell 12290", info.Reason);
        Assert.Contains("requires XPList", info.Reason);
        Assert.Contains("removes XPList", info.Reason);
        Assert.Contains("opens DragonCyclone using DBSpell.Duration", info.Reason);
        Assert.Contains("DragonCyclone.cs", info.SourceEvidence);
        Assert.Contains("ContainFlag(MsgUpdate.Flags.XPList)", info.SourceEvidence);
        Assert.Contains("RemoveFlag(MsgUpdate.Flags.XPList)", info.SourceEvidence);
        Assert.Contains("OpenXpSkill(MsgUpdate.Flags.DragonCyclone, (int)DBSpell.Duration)", info.SourceEvidence);
        Assert.Contains("active non-XP branch requires ContainFlag(MsgUpdate.Flags.DragonCyclone)", info.SourceEvidence);
        Assert.Contains("computes movement coords toward Attack.X/Y with CheckBladeTeampsCoords", info.SourceEvidence);
        Assert.Contains("Math.Min(DBSpell.Range, caster distance to target point)", info.SourceEvidence);
        Assert.Contains("lands on the last coord", info.SourceEvidence);
        Assert.Contains("gates landing through CheckGuildWar", info.SourceEvidence);
        Assert.Contains("calls user.Shift", info.SourceEvidence);
        Assert.Contains("filters monsters by Base.GetDistance(Attack.X,Attack.Y,target) <= DBSpell.Range plus MoveCoords.InRange(target, 2, coord)", info.SourceEvidence);
        Assert.Contains("filters players and SobNpc targets by Base.GetDistance(Attack.X,Attack.Y,target) <= 6 plus MoveCoords.InRange(target, 2, coord)", info.SourceEvidence);
        Assert.Contains("uses Physical.OnMonster/OnPlayer/OnNpcs", info.SourceEvidence);
        Assert.Contains("applies experience/spell update", info.SourceEvidence);
        Assert.Contains("sends animation", info.SourceEvidence);
        Assert.Contains("no special XP active runtime branch remains identified here", info.SuggestedNextOwner);
        Assert.Contains(
            "magicSort=DragonCyclone(75) spellId=12290 capability=xpSkill status=partiallyModeled supported=true nextSeam=xp-skill-runtime",
            info.Diagnostic);
    }

    [Fact]
    public void ClassifyOmnipotenceXpListActivationAsPartiallyModeledXpSkill()
    {
        var info = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)85, 12_860);

        Assert.Equal(LegacyMagicSortCapability.XpSkill, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("Omnipotence(85)", info.SortName);
        Assert.Equal("xp-skill-runtime", info.NextSeam);
        Assert.Contains("Omnipotence", info.Reason);
        Assert.Contains("spell 12860", info.Reason);
        Assert.Contains("requires XPList", info.Reason);
        Assert.Contains("fills stamina to the 100/150 HeavenBlessing maximum", info.Reason);
        Assert.Contains("removes XPList", info.Reason);
        Assert.Contains("opens Omnipotence for 40 seconds", info.Reason);
        Assert.Contains("Omnipotence.cs", info.SourceEvidence);
        Assert.Contains("ContainFlag(MsgUpdate.Flags.XPList)", info.SourceEvidence);
        Assert.Contains("Stamina = MaxStamina", info.SourceEvidence);
        Assert.Contains("RemoveFlag(MsgUpdate.Flags.XPList)", info.SourceEvidence);
        Assert.Contains("OpenXpSkill(MsgUpdate.Flags.Omnipotence, 40, 0)", info.SourceEvidence);
        Assert.Contains("non-XP branch", info.SourceEvidence);
        Assert.Contains("Physical.OnMonster, Range.OnPlayer, and Physical.OnNpcs", info.SourceEvidence);
        Assert.Contains(
            "ShurikenVortex or ManiacDance returns Base.CalculateExtraAttack from zero using attacker PhysicalDamageIncrease and defender PhysicalDamageDecrease",
            info.Reason);
        Assert.Contains("WildPhoenix remains a fixed one-damage return before DefensiveStance", info.Reason);
        Assert.Contains("DefensiveStance applies its 40 percent Block return before BackDmg", info.Reason);
        Assert.Contains(
            "normal eligible hits call BackDmg before receive/progress output and the one shared animation",
            info.Reason);
        Assert.Contains("Base.CalculateExtraAttack(0, PhysicalDamageIncrease, PhysicalDamageDecrease)", info.SourceEvidence);
        Assert.Contains("using attacker PhysicalDamageIncrease and defender PhysicalDamageDecrease", info.SourceEvidence);
        Assert.Contains("WildPhoenix's fixed one-damage return", info.SourceEvidence);
        Assert.Contains("BackDmg.Calculate(..., ranged: true)", info.SourceEvidence);
        Assert.Contains("shared animation after the target loops, experience update, and spell update", info.SourceEvidence);
        Assert.Contains(
            "magicSort=Omnipotence(85) spellId=12860 capability=xpSkill status=partiallyModeled supported=true nextSeam=xp-skill-runtime",
            info.Diagnostic);
    }

    [Fact]
    public void CounterKillCapabilityRecordsAttackStatusBladeFlurryOmnipotenceAndKineticSparkCallers()
    {
        var info = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)29, 16_350);

        Assert.Contains(
            "BladeFlurry 11610 and Omnipotence 12860's non-XP visible-player Range.OnPlayer targets",
            info.Reason);
        Assert.Contains("AttackStatus 7000's strict-range visible-player loop", info.Reason);
        Assert.Contains("StarArrow 10313's pulled selected-player Range.OnPlayer branch", info.Reason);
        Assert.Contains(
            "ShurikenVortex/ManiacDance return Base.CalculateExtraAttack from zero using attacker PhysicalDamageIncrease and defender PhysicalDamageDecrease before WildPhoenix's fixed one-damage return",
            info.Reason);
        Assert.Contains(
            "AttackStatus 7000, BladeFlurry, Omnipotence, and KineticSpark normal eligible ranged player hits invoke BackDmg",
            info.Reason);
        Assert.Contains("KineticSpark 11590's clicked Physical.OnPlayer plus strict-range chained Range.OnPlayer targets", info.Reason);
        Assert.Contains("Matching AttackHandler/StarArrow.cs routes spell 10313", info.SourceEvidence);
        Assert.Contains("Base.CalculateExtraAttack(0, PhysicalDamageIncrease, PhysicalDamageDecrease)", info.SourceEvidence);
        Assert.Contains("using attacker PhysicalDamageIncrease and defender PhysicalDamageDecrease", info.SourceEvidence);
        Assert.Contains("WildPhoenix's fixed one-damage return", info.SourceEvidence);
        Assert.DoesNotContain("remaining KineticSpark Range.OnPlayer caller remains debt", info.Reason);
        Assert.DoesNotContain("KineticSpark ranged player-calculation", info.SuggestedNextOwner);
        Assert.DoesNotContain("BladeFlurry", info.SuggestedNextOwner);
        Assert.DoesNotContain("Omnipotence", info.SuggestedNextOwner);
        Assert.DoesNotContain("AttackStatus 7000", info.SuggestedNextOwner);
    }

    [Theory]
    [InlineData(12_950, "HealingSnow")]
    [InlineData(12_960, "ChillingSnow")]
    [InlineData(13_020, "FreezingPelter")]
    public void ClassifySupportedSnowAttachStatusSelfToggles(ushort spellId, string expectedName)
    {
        var info = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)6, spellId);

        Assert.Equal(LegacyMagicSortCapability.AttachStatusBuff, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.FullyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.True(info.SupportsAttachStatusSelfBuff);
        Assert.False(info.SupportsAttachStatusPlayerTargetBuff);
        Assert.Equal("AttachStatus(6)", info.SortName);
        Assert.Equal("implemented-attach-status-self-or-visible-player-buff", info.NextSeam);
        Assert.Contains(expectedName, info.SourceEvidence);
        Assert.Contains("HealingSnow/ChillingSnow/FreezingPelter snow self-toggle family", info.Reason);
        Assert.Contains("mutual snow status clearing", info.Reason);
        Assert.Contains("status=fullyModeled", info.Diagnostic);
    }

    [Theory]
    [InlineData(20_300, "PhoenixBlessing")]
    [InlineData(20_350, "HolyProtection")]
    public void ClassifySupportedCombatGearAttachStatusSelfBuffs(ushort spellId, string expectedName)
    {
        var info = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)6, spellId);

        Assert.Equal(LegacyMagicSortCapability.AttachStatusBuff, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.FullyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.True(info.SupportsAttachStatusSelfBuff);
        Assert.False(info.SupportsAttachStatusPlayerTargetBuff);
        Assert.Equal("AttachStatus(6)", info.SortName);
        Assert.Equal("implemented-attach-status-self-or-visible-player-buff", info.NextSeam);
        Assert.Contains("CombatGearAttack self-status path", info.Reason);
        Assert.Contains(expectedName, info.SuggestedNextOwner);
        Assert.Contains("CombatGearAttack.cs cases 20300 and 20350", info.SourceEvidence);
        Assert.Contains("QueryEquipment", info.SourceEvidence);
        Assert.Contains("status=fullyModeled", info.Diagnostic);
    }

    [Fact]
    public void ClassifyWildPhoenixCombatGearAttachStatusSelfBuffAsPartialMagicRuntime()
    {
        var info = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)6, spellId: 20_370);

        Assert.Equal(LegacyMagicSortCapability.AttachStatusBuff, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.True(info.SupportsAttachStatusSelfBuff);
        Assert.False(info.SupportsAttachStatusPlayerTargetBuff);
        Assert.Equal("AttachStatus(6)", info.SortName);
        Assert.Equal("wildphoenix-physical-range-hook-runtime", info.NextSeam);
        Assert.Contains("initial WildPhoenix CombatGearAttack self-status packet path", info.Reason);
        Assert.Contains("Oblivion|HawksAmbition", info.Reason);
        Assert.Contains("DamageOnHuman*1000+Damage2", info.Reason);
        Assert.Contains("GDamage charge level", info.Reason);
        Assert.Contains("generic magic player-target defender charge/archive refresh", info.Reason);
        Assert.Contains("PhysicalSpells player-target defender charge/archive hook", info.Reason);
        Assert.Contains("KineticSpark player Physical/Range defender charge/archive hook", info.Reason);
        Assert.Contains("SpeedGun 1260 player Physical.OnPlayer WildPhoenix defender hook", info.Reason);
        Assert.Contains("DragonWhirl 10315 player Physical.OnPlayer WildPhoenix defender hook", info.Reason);
        Assert.Contains("HookMoon 15930 player Physical.OnPlayer WildPhoenix defender hook", info.Reason);
        Assert.Contains("ViperFang 11005 player Physical.OnPlayer WildPhoenix defender hook", info.Reason);
        Assert.Contains("MoveLine 11110 player Physical.OnPlayer WildPhoenix defender hook", info.Reason);
        Assert.Contains("CannonBarrage 11050 player Physical.OnPlayer WildPhoenix defender hook", info.Reason);
        Assert.Contains("ScurvyBomb 11040 player Physical.OnPlayer WildPhoenix defender hook", info.Reason);
        Assert.Contains("PirateXpSkill/BlackbeardsRage 11060 player Physical.OnPlayer WildPhoenix defender hook before *2 scaling", info.Reason);
        Assert.Contains("BombPirte 11070 player Physical.OnPlayer WildPhoenix defender hook before /2 scaling", info.Reason);
        Assert.Contains("DragonTail 11000 player Physical.OnPlayer WildPhoenix defender hook", info.Reason);
        Assert.Contains("DragonSlash 12350 player Physical.OnPlayer WildPhoenix defender hook", info.Reason);
        Assert.Contains("ThunderStrikerLine 15810 player Physical.OnPlayer WildPhoenix defender hook", info.Reason);
        Assert.Contains("Shock 15720 player Physical.OnPlayer WildPhoenix defender hook", info.Reason);
        Assert.Contains("FatalCross 11980 player Physical.OnPlayer WildPhoenix defender hook", info.Reason);
        Assert.Contains("RageOfWar 12930 initial/floor player Physical.OnPlayer WildPhoenix defender hook", info.Reason);
        Assert.Contains("AttackStatus default player Physical.OnPlayer WildPhoenix defender hook", info.Reason);
        Assert.Contains("AttackStatus 7000 player Range.OnPlayer WildPhoenix defender hook", info.Reason);
        Assert.Contains("AttackStatus 15880 player Physical.OnPlayer WildPhoenix defender hook", info.Reason);
        Assert.Contains("BlackSpot 11120 direct player Physical.OnPlayer WildPhoenix defender hook", info.Reason);
        Assert.Contains("HellVortex 16370 player counter Physical.OnPlayer WildPhoenix defender hook", info.Reason);
        Assert.Contains("SectorBack 10990/18240 player Physical.OnPlayer WildPhoenix defender hook before /7 scaling", info.Reason);
        Assert.Contains("CleanSweep/SectorBack 15870 player Physical.OnPlayer WildPhoenix defender hook before *5/12 scaling", info.Reason);
        Assert.Contains("AttackEffect damage bonus", info.Reason);
        Assert.Contains("outside generic magic, PhysicalSpells, KineticSpark, SpeedGun, DragonWhirl, HookMoon, ViperFang, MoveLine, CannonBarrage, ScurvyBomb, PirateXpSkill 11060, BombPirte 11070, DragonTail, DragonSlash, ThunderStrikerLine, Shock, FatalCross, RageOfWar, AttackStatus default, AttackStatus 7000, AttackStatus 15880, BlackSpot 11120, HellVortex 16370, SectorBack 10990/18240, and CleanSweep/SectorBack 15870", info.Reason);
        Assert.DoesNotContain("outside generic magic, PhysicalSpells, KineticSpark, SpeedGun, DragonWhirl, HookMoon, ViperFang, MoveLine, CannonBarrage, ScurvyBomb, PirateXpSkill 11060, BombPirte 11070, DragonTail, DragonSlash, ThunderStrikerLine, Shock, FatalCross, RageOfWar, AttackStatus default, AttackStatus 7000, AttackStatus 15880, BlackSpot 11120, HellVortex 16370, and SectorBack 10990/18240", info.Reason);
        Assert.DoesNotContain("NebulousHunt/SectorBack 10990 player Physical.OnPlayer WildPhoenix defender hook before /7 scaling", info.Reason);
        Assert.DoesNotContain("outside generic magic, PhysicalSpells, KineticSpark, SpeedGun, DragonWhirl, HookMoon, ViperFang, MoveLine, CannonBarrage, ScurvyBomb, PirateXpSkill 11060, BombPirte 11070, DragonTail, DragonSlash, ThunderStrikerLine, Shock, FatalCross, RageOfWar, AttackStatus default, AttackStatus 7000, AttackStatus 15880, BlackSpot 11120, and HellVortex 16370", info.Reason);
        Assert.Contains("CombatGearAttack.cs case 20370", info.SourceEvidence);
        Assert.Contains("status flag 0x170", info.SourceEvidence);
        Assert.Contains("SpellLevel=GDamage", info.SourceEvidence);
        Assert.Contains("MyCombatGear.cs WildPhoenix()", info.SourceEvidence);
        Assert.Contains("AttackEffect(...)", info.SourceEvidence);
        Assert.Contains("Line.cs case 11005 calls Physical.OnPlayer, then Base.CalculateSoul, then divides player damage by 2 before ReceiveAttack.Player.Execute", info.SourceEvidence);
        Assert.Contains("MoveLine.cs player branch calls Physical.OnPlayer, then Base.CalculateSoul, then scales damage by *12/16 before ReceiveAttack.Player.Execute", info.SourceEvidence);
        Assert.Contains("CannonBarrage.cs player branch calls Physical.OnPlayer, then Base.CalculateSoul, then divides player damage by 11 before ReceiveAttack.Player.Execute", info.SourceEvidence);
        Assert.Contains("ScurvyBomb.cs player branch applies fatigue/status, calls Physical.OnPlayer, then Base.CalculateSoul, then doubles player damage before ReceiveAttack.Player.Execute", info.SourceEvidence);
        Assert.Contains("PirateXpSkill.cs lines 46-54 call Physical.OnPlayer, then Base.CalculateSoul, then doubles player damage before ReceiveAttack.Player.Execute", info.SourceEvidence);
        Assert.Contains("BombPirte.cs lines 118-120 call Physical.OnPlayer before dividing player damage by 2", info.SourceEvidence);
        Assert.Contains("HellVortex.cs player branch calls Physical.OnPlayer, then Base.CalculateSoul, then scales damage by *12/28 before ReceiveAttack.Player.Execute", info.SourceEvidence);
        Assert.Contains("SectorBack.cs case 10990/18240", info.SourceEvidence);
        Assert.Contains("calls Physical.OnPlayer, then Base.CalculateSoul, then divides player damage by 7 before ReceiveAttack.Player.Execute", info.SourceEvidence);
        Assert.Contains("magictype.txt rows 18240-18246 define type 18240 as sort 69/SectorBack", info.SourceEvidence);
        Assert.Contains("SectorBack.cs case 15870", info.SourceEvidence);
        Assert.Contains("scales player Physical.OnPlayer plus Base.CalculateSoul damage by *5/12 before ReceiveAttack.Player.Execute", info.SourceEvidence);
        Assert.Contains("MsgUpdate.cs defines CleanSweep = 0xFE", info.SourceEvidence);
        Assert.Contains("Physical.cs applies target.Owner.CombatGear.WildPhoenix()", info.SourceEvidence);
        Assert.Contains("outside generic magic, PhysicalSpells, KineticSpark, SpeedGun, DragonWhirl, HookMoon, ViperFang, MoveLine, CannonBarrage, ScurvyBomb, PirateXpSkill 11060, BombPirte 11070, DragonTail, DragonSlash, ThunderStrikerLine, Shock, FatalCross, RageOfWar, AttackStatus default, AttackStatus 7000, AttackStatus 15880, BlackSpot 11120, HellVortex 16370, SectorBack 10990/18240, and CleanSweep/SectorBack 15870", info.SuggestedNextOwner);
        Assert.DoesNotContain("outside generic magic, PhysicalSpells, KineticSpark, SpeedGun, DragonWhirl, HookMoon, ViperFang, MoveLine, CannonBarrage, ScurvyBomb, PirateXpSkill 11060, BombPirte 11070, DragonTail, DragonSlash, ThunderStrikerLine, Shock, FatalCross, RageOfWar, AttackStatus default, AttackStatus 7000, AttackStatus 15880, BlackSpot 11120, HellVortex 16370, and SectorBack 10990/18240", info.SuggestedNextOwner);
        Assert.DoesNotContain("outside generic magic, PhysicalSpells, KineticSpark, SpeedGun, DragonWhirl, HookMoon, ViperFang, MoveLine, CannonBarrage, ScurvyBomb, PirateXpSkill 11060, BombPirte 11070, DragonTail, DragonSlash, ThunderStrikerLine, Shock, FatalCross, RageOfWar, AttackStatus default, AttackStatus 7000, AttackStatus 15880, BlackSpot 11120, and HellVortex 16370", info.SuggestedNextOwner);
        Assert.Contains("status=partiallyModeled", info.Diagnostic);
    }

    [Fact]
    public void ClassifyFreezingPelterDocumentsModeledEquipmentQuerySideEffect()
    {
        var info = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)6, 13_020);

        Assert.Contains("QueryEquipment", info.Reason);
        Assert.Contains("add-side", info.Reason);
        Assert.Contains("CallItems:false", info.Reason);
        Assert.Contains("case 13020 calls QueryEquipment when adding FreezingPelter", info.SourceEvidence);
        Assert.Contains("QueryEquipment(..., CallItems:false) when removing it", info.SourceEvidence);
    }

    [Fact]
    public void ClassifyAttachStatusSortInventoryWithoutSpellIdAsPartial()
    {
        var info = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)6);

        Assert.Equal(LegacyMagicSortCapability.AttachStatusBuff, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.False(info.SupportsAttachStatusSelfBuff);
        Assert.Equal("attach-status-remaining-runtime", info.NextSeam);
        Assert.Contains("five key status-buff spell IDs", info.Reason);
        Assert.Contains("snow self-toggle cases 12950, 12960, and 13020", info.Reason);
        Assert.Contains("CombatGearAttack initial self-status cases 20300, 20350, and 20370", info.Reason);
        Assert.Contains("WildPhoenix generic magic defender charge/archive refresh and qualifying attacker damage bonus are modeled", info.Reason);
        Assert.Contains("Physical/Range calculator call-site coverage remains separate debt", info.Reason);
    }

    [Fact]
    public void ClassifyUnsupportedAttachStatusSpellAsMissingWithReason()
    {
        var info = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)6, spellId: 9_999);

        Assert.Equal(LegacyMagicSortCapability.AttachStatusBuff, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.MissingRuntimeNotModeled, info.Status);
        Assert.False(info.Supported);
        Assert.False(info.SupportsAttachStatusSelfBuff);
        Assert.False(info.SupportsAttachStatusPlayerTargetBuff);
        Assert.Equal("AttachStatus(6)", info.SortName);
        Assert.Equal("attach-status-remaining-runtime", info.NextSeam);
        Assert.Contains("does not map this AttachStatus spell ID", info.Reason);
        Assert.Contains("status=missingRuntimeNotModeled", info.Diagnostic);
    }

    [Theory]
    [InlineData(0, 50, "SourceSilent(0)")]
    [InlineData(72, 12_020, "Summon(72)")]
    [InlineData(100, 18_820, "SharpThorn(100)")]
    [InlineData(107, 22_050, "NutSummon(107)")]
    public void ClassifySourceSilentNoOpSortsFromMagicTypeRows(int sortValue, ushort spellId, string expectedSortName)
    {
        var info = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)sortValue, spellId);

        Assert.Equal(LegacyMagicSortCapability.SourceSilentNoOp, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.SourceSilentNoOp, info.Status);
        Assert.False(info.Supported);
        Assert.Equal(expectedSortName, info.SortName);
        Assert.Equal("source-silent-noop-confirmation", info.NextSeam);
        Assert.Contains("magictype.txt contains rows", info.Reason);
        Assert.Contains("MsgAttackPacket.cs ProcescMagic", info.SourceEvidence);
    }

    [Theory]
    [InlineData(56, 11_041, "ScurvyBomb(56)", LegacyMagicSortCapability.PirateSpecial, "pirate-special-runtime")]
    public void ClassifyUnsupportedCombatPirateSummonReviveAndXpExamplesAsMissing(
        int sortValue,
        ushort spellId,
        string expectedSortName,
        LegacyMagicSortCapability expectedCapability,
        string expectedNextSeam)
    {
        var info = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)sortValue, spellId);

        Assert.Equal(expectedCapability, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.MissingRuntimeNotModeled, info.Status);
        Assert.False(info.Supported);
        Assert.Equal(expectedSortName, info.SortName);
        Assert.Equal(expectedNextSeam, info.NextSeam);
        Assert.Contains("WolfConquer", info.Reason);
        Assert.Contains("magictype.txt", info.SourceEvidence);
        Assert.NotEmpty(info.SuggestedNextOwner);
    }

    [Fact]
    public void ClassifyFatalCrossRuntimeSpellAsPartiallyModeledCombatCapability()
    {
        var info = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.FatalCross, spellId: 11_980);

        Assert.Equal(LegacyMagicSortCapability.SpecialCombatDamage, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("FatalCross(71)", info.SortName);
        Assert.Equal("fatalcross-combat-runtime", info.NextSeam);
        Assert.Contains("FatalCross 11980", info.Reason);
        Assert.Contains("distance <= 13", info.Reason);
        Assert.Contains("width-2/range-13 line", info.Reason);
        Assert.Contains("player Physical.OnPlayer damage", info.Reason);
        Assert.Contains("player-target WildPhoenix defender charge/archive damage override before the FatalCross Base.CalculateSoul and player *22/28 scaling step", info.Reason);
        Assert.Contains("source-shaped zero-damage WildPhoenix player target preservation", info.Reason);
        Assert.Contains("monster/SOB spell progress", info.Reason);
        Assert.Contains("FatalCross.cs", info.SourceEvidence);
        Assert.Contains("Physical.OnPlayer, Base.CalculateSoul, then scale damage by 22/28", info.SourceEvidence);
        Assert.Contains("supported=true", info.Diagnostic);
    }

    [Fact]
    public void ClassifyCrackingShockRuntimeSpellAsPartiallyModeledCombatCapability()
    {
        var info = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.Shock, spellId: 15_720);

        Assert.Equal(LegacyMagicSortCapability.SpecialCombatDamage, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("Shock(34)", info.SortName);
        Assert.Equal("shock-combat-runtime", info.NextSeam);
        Assert.Contains("Shock/CrackingShock 15720", info.Reason);
        Assert.Contains("non-ThunderStriker", info.Reason);
        Assert.Contains("preserves decoded Attack.X/Y without an added push", info.Reason);
        Assert.Contains("inclusive radius <= 6", info.Reason);
        Assert.Contains("monster *2", info.Reason);
        Assert.Contains("player *75/100", info.Reason);
        Assert.Contains("SOB unchanged", info.Reason);
        Assert.Contains("Physical.OnPlayer status returns before WildPhoenix", info.Reason);
        Assert.Contains("RevengeTail see the pre-MudWall physical hit", info.Reason);
        Assert.Contains("one MudWall reduction, identity soul and player *75/100 scaling", info.Reason);
        Assert.Contains("unchanged HP for ordinary zero-damage receives", info.Reason);
        Assert.Contains("retains ordinary and WildPhoenix zero-damage player targets", info.Reason);
        Assert.Contains("preserves requested SOB overkill animation damage without inflating HP loss or progress", info.Reason);
        Assert.Contains("monster/SOB spell and proficiency progress", info.Reason);
        Assert.Contains("Shock.cs", info.SourceEvidence);
        Assert.Contains("spell 15720 uses sort 34 CrackingShock", info.SourceEvidence);
        Assert.Contains("IncreaseExperience.Up", info.SourceEvidence);
        Assert.Contains("UpdateSpell.CheckUpdate", info.SourceEvidence);
        Assert.Contains("supported=true", info.Diagnostic);
    }

    [Fact]
    public void ClassifyGunfireStrikeRuntimeSpellAsPartiallyModeledCombatCapability()
    {
        var info = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.Strike, spellId: 14_590);

        Assert.Equal(LegacyMagicSortCapability.SpecialCombatDamage, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("Strike(81)", info.SortName);
        Assert.Equal("gunfire-strike-combat-runtime", info.NextSeam);
        Assert.Contains("Gunfire 14590 Strike runtime slice", info.Reason);
        Assert.Contains("source player *2/3 scaling", info.Reason);
        Assert.Contains("only spell ID 14590", info.Reason);
        Assert.Contains("MagicType.cs line 128 defines Strike = 81", info.SourceEvidence);
        Assert.Contains("MsgAttackPacket.cs lines 990-992 dispatch MagicSort.Strike", info.SourceEvidence);
        Assert.Contains("Strike.cs case 14590", info.SourceEvidence);
        Assert.Contains("magictype.txt row 1991 defines spell 14590 Gunfire as sort 81/Strike", info.SourceEvidence);
        Assert.Contains("supported=true", info.Diagnostic);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(14_591)]
    public void ClassifyOtherStrikeSpellIdsAsUnsupported(ushort spellId)
    {
        var info = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.Strike, spellId);

        Assert.Equal(LegacyMagicSortCapability.SpecialCombatDamage, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.MissingRuntimeNotModeled, info.Status);
        Assert.False(info.Supported);
        Assert.Equal("Strike(81)", info.SortName);
        Assert.Equal("source-triage", info.NextSeam);
        Assert.Contains("Only Strike spells 12600, 13050, and Gunfire spell 14590", info.Reason);
        Assert.Contains("case 13050", info.SourceEvidence);
        Assert.Contains("supported=false", info.Diagnostic);
    }

    [Fact]
    public void ClassifyDragonWhirlRuntimeSpellAsPartiallyModeledCombatCapability()
    {
        var info = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.DragonWhirl, spellId: 10_315);

        Assert.Equal(LegacyMagicSortCapability.SpecialCombatDamage, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("DragonWhirl(40)", info.SortName);
        Assert.Equal("dragonwhirl-combat-runtime", info.NextSeam);
        Assert.Contains("DragonWhirl 10315", info.Reason);
        Assert.Contains("strict cast point distance < Spell.Range", info.Reason);
        Assert.Contains("width-4 line", info.Reason);
        Assert.Contains("player-target WildPhoenix defender charge/archive damage override", info.Reason);
        Assert.Contains("zero-damage InMoveSpell marker", info.Reason);
        Assert.Contains("caster session reposition", info.Reason);
        Assert.Contains("source view-role LegacyPlayerSpawn refresh to the caster and visible peers", info.Reason);
        Assert.DoesNotContain("source view-role LegacyPlayerSpawn refresh to visible peers.", info.Reason);
        Assert.Contains("Remaining DragonWhirl parity is exact CanAttack edge gaps", info.Reason);
        Assert.Contains("spell 10315 uses sort 40 DragonWhirl", info.SourceEvidence);
        Assert.Contains("DragonWhirl.cs", info.SourceEvidence);
        Assert.Contains("AtkType=53", info.SourceEvidence);
        Assert.Contains("KilledMonster=10315", info.SourceEvidence);
        Assert.Contains("supported=true", info.Diagnostic);
    }

    [Fact]
    public void ClassifyChargingVortexRuntimeSpellAsPartiallyModeledCombatCapability()
    {
        var info = LegacyMagicSortCapabilities.Classify(
            LegacyMagicSort.ChargingVortex,
            spellId: 11_190);

        Assert.Equal(LegacyMagicSortCapability.SpecialCombatDamage, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("ChargingVortex(64)", info.SortName);
        Assert.Equal("charging-vortex-combat-runtime", info.NextSeam);
        Assert.Contains("ChargingVortex 11190 runtime slice", info.Reason);
        Assert.Contains("strict cast point distance < Spell.Range", info.Reason);
        Assert.Contains("source width-4 line", info.Reason);
        Assert.Contains("KilledMonster=11190", info.Reason);
        Assert.Contains("caster session reposition", info.Reason);
        Assert.Contains("MagicType.cs line 115 defines ChargingVortex = 64", info.SourceEvidence);
        Assert.Contains("MsgAttackPacket.cs lines 1014-1015 dispatch MagicSort.ChargingVortex", info.SourceEvidence);
        Assert.Contains("magictype.txt row 1155 defines spell 11190 ChargingVortex", info.SourceEvidence);
        Assert.Contains("ChargingVortex.cs", info.SourceEvidence);
        Assert.Contains("supported=true", info.Diagnostic);
    }

    [Fact]
    public void ClassifyPhoenixAttackStatusSpellAsModeledCombatCapability()
    {
        var info = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)16, spellId: 5_030);

        Assert.Equal(LegacyMagicSortCapability.SpecialCombatDamage, info.Capability);
        Assert.True(
            info.Status is LegacyMagicModelingStatus.PartiallyModeled or LegacyMagicModelingStatus.FullyModeled,
            $"Expected AttackStatus/Phoenix to be at least partially modeled, got {info.Status}: {info.Diagnostic}");
        Assert.True(info.Supported);
        Assert.Equal("AttackStatus(16)", info.SortName);
        Assert.Contains("Phoenix", info.Reason);
        Assert.Contains("source-backed special cases 7000, 10800, 15560, 15880, and 20000", info.Reason);
        Assert.Contains("data/DBServer\\magictype.txt rows 5030-5039", info.SourceEvidence);
        Assert.Contains("Phoenix", info.SourceEvidence);
        Assert.Contains("MagicType.cs defines AttackStatus = 16", info.SourceEvidence);
        Assert.Contains("AttackStatus.cs default branch", info.SourceEvidence);
        Assert.Contains("special cases 7000, 10800, 15560, 15880, and 20000", info.SourceEvidence);
        Assert.Contains("Physical.OnMonster", info.SourceEvidence);
        Assert.Contains("Physical.OnNpcs", info.SourceEvidence);
        Assert.Contains("Physical.OnPlayer", info.SourceEvidence);
        Assert.Contains("player damage /4", info.SourceEvidence);
        Assert.Contains("divides player damage /4 after Base.CalculateSoul", info.SourceEvidence);
        Assert.Contains("default-branch player physical damage divided by /4", info.Reason);
        Assert.Contains("WildPhoenix defender charge/archive override", info.Reason);
        Assert.Contains("zero-damage target preservation", info.Reason);
        Assert.Contains("default player branch's opening ReceiveAttack.Player envelope", info.Reason);
        Assert.Contains("speffect groups 2/3/4/5", info.Reason);
        Assert.Contains("Ninja SageMode using gouyu Chance / 100", info.Reason);
        Assert.Contains("other special player-damage branches' opening receive envelopes", info.Reason);
        Assert.Contains("calls ReceiveAttack.Player.Execute for an accepted player", info.SourceEvidence);
        Assert.Contains("defender speffect groups 3/4/5", info.SourceEvidence);
        Assert.Contains("IncreaseExperience.Up", info.SourceEvidence);
        Assert.Contains("UpdateSpell.CheckUpdate", info.SourceEvidence);
        Assert.Contains("MsgSpellAnimation", info.SourceEvidence);
        Assert.Contains("magicSort=AttackStatus(16) spellId=5030", info.Diagnostic);
        Assert.Contains("supported=true", info.Diagnostic);
    }

    [Theory]
    [InlineData(7_000)]
    [InlineData(10_800)]
    [InlineData(15_560)]
    [InlineData(15_880)]
    [InlineData(20_000)]
    public void ClassifyAttackStatusSpecialCasesAsModeledCombatCapabilities(ushort spellId)
    {
        var info = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)16, spellId);

        Assert.Equal(LegacyMagicSortCapability.SpecialCombatDamage, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("AttackStatus(16)", info.SortName);
        Assert.Contains("source-backed special cases", info.Reason);
        if (spellId == 15_880)
        {
            Assert.Contains("matching-decompile 15880 SongofPhoenix caster-centered strict-range monster/player/SOB loops", info.Reason);
            Assert.Contains("monster full runtime damage with 60% animation display", info.Reason);
            Assert.Contains("player DamageOnHuman-selected Physical.OnPlayer damage scaled by * 7 / 13", info.Reason);
            Assert.Contains("opening ReceiveAttack.Player envelope with immediate per-target receive publication", info.Reason);
            Assert.Contains("15880", info.Reason);
            Assert.Contains("player envelope carries attacker state through CombatGear updates", info.Reason);
            Assert.Contains("Ninja SageMode using gouyu Chance / 100", info.Reason);
            Assert.Contains("SOB damage scaled by * 50 / 100", info.Reason);
            Assert.Contains("animation and an immediate return without applying the unreachable later SongofPhoenix flag/power block", info.Reason);
            Assert.Contains("exact row 15880 defines SongofPhoenix as sort 16 with range 5", info.SourceEvidence);
            Assert.Contains("caster-centered strict-distance < Spell.Range", info.SourceEvidence);
            Assert.Contains("monsters use Range.OnMonster with full runtime damage and 60% animation damage", info.SourceEvidence);
            Assert.Contains("players use Physical.OnPlayer plus Base.CalculateSoul then * 7 / 13", info.SourceEvidence);
            Assert.Contains("runtime and animation damage before ReceiveAttack.Player.Execute", info.SourceEvidence);
            Assert.Contains("SOB NPCs use Range.OnNpcs plus Base.CalculateSoul then * 50 / 100", info.SourceEvidence);
            Assert.Contains("sends MsgSpellAnimation, and returns", info.SourceEvidence);
            Assert.Contains("later SongofPhoenix flag/power block is unreachable", info.SourceEvidence);
            Assert.Contains("Matching decompiled Physical.cs explicitly selects DamageOnHuman % 1000", info.SourceEvidence);
            Assert.DoesNotContain("Conquer Source 7770", info.SourceEvidence);
        }
        else if (spellId == 7_000)
        {
            Assert.Contains("source 7000 player Range.OnPlayer caller ordering", info.Reason);
            Assert.Contains("ShurikenVortex/ManiacDance status damage returns before WildPhoenix", info.Reason);
            Assert.Contains("DefensiveStance applies the 40 percent Block return without BackDmg", info.Reason);
            Assert.Contains("normal eligible ranged BackDmg invokes learned-defender Heaven's Wonder 16350 then RevengeTail 13030", info.Reason);
            Assert.Contains("each target's opening ReceiveAttack.Player envelope", info.Reason);
            Assert.Contains("7000 opening envelope performs attacker/defender CombatGear updates", info.Reason);
            Assert.Contains("Ninja SageMode using gouyu Chance / 100 for every accepted player hit", info.Reason);
            Assert.Contains("case 7000 selects caster-centered monster, player, and SOB targets", info.SourceEvidence);
            Assert.Contains("Base.CalculateExtraAttack(0, PhysicalDamageIncrease, PhysicalDamageDecrease)", info.SourceEvidence);
            Assert.Contains("using attacker PhysicalDamageIncrease and defender PhysicalDamageDecrease", info.SourceEvidence);
            Assert.Contains("WildPhoenix's fixed one", info.SourceEvidence);
            Assert.Contains("applies DefensiveStance's 40 percent Block return before ranged BackDmg", info.SourceEvidence);
            Assert.Contains("broader Range.OnPlayer formulas", info.SuggestedNextOwner);
            Assert.Contains("Exact D13 client executable", info.SourceEvidence);
            Assert.Contains("Source7770 is excluded", info.SourceEvidence);
        }
        Assert.Contains($"spellId={spellId}", info.Diagnostic);
        Assert.Contains("supported=true", info.Diagnostic);
    }

    [Fact]
    public void ClassifyAttackStatus7000RecordsRangedRevengeTailBackDamageParity()
    {
        var info = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)16, spellId: 7_000);

        Assert.Equal(LegacyMagicSortCapability.SpecialCombatDamage, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Contains(
            "normal eligible ranged BackDmg invokes learned-defender Heaven's Wonder 16350 then RevengeTail 13030",
            info.Reason);
        Assert.Contains("Backfire, CounterKill/Reflect, and broader Range.OnPlayer formulas", info.Reason);
        Assert.Contains(
            "Matching BackDmg.cs evaluates learned-defender Heaven's Wonder 16350 first, then RevengeTail 13030",
            info.SourceEvidence);
        Assert.Contains("ranged=true suppresses only the later 14470 branch", info.SourceEvidence);
        Assert.Contains("Exact D13 client executable", info.SourceEvidence);
        Assert.Contains("owns packet opcodes and layouts", info.SourceEvidence);
        Assert.Contains("matching service-pack decompile supplies gameplay behavior and ordering", info.SourceEvidence);
        Assert.Contains("Source7770 is excluded", info.SourceEvidence);
        Assert.DoesNotContain("immediate per-target receive packet ordering", info.Reason);
        Assert.DoesNotContain("immediate per-target receive packet ordering", info.SuggestedNextOwner);
    }

    [Fact]
    public void ClassifyAttackStatus10800DoesNotAdvertisePacketRefreshGap()
    {
        var info = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)16, spellId: 10_800);

        Assert.Equal(LegacyMagicSortCapability.SpecialCombatDamage, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Contains("source-shaped 10800 ShieldofTruth status/equipment refresh packets", info.Reason);
        Assert.Contains("AttackStatus remains partially modeled for exact CanAttack side effects", info.Reason);
        Assert.DoesNotContain("few packet refresh details", info.Reason);
        Assert.DoesNotContain("missing status/equipment refresh packets", info.SuggestedNextOwner);
        Assert.DoesNotContain("packet refresh details", info.Diagnostic);
    }

    [Fact]
    public void ClassifyScurvyBombMonsterVisiblePlayerAndSobRuntimeSpellAsPartiallyModeledPirateSlice()
    {
        var info = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.ScurvyBomb, spellId: 11_040);

        Assert.Equal(LegacyMagicSortCapability.PirateSpecial, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("ScurvyBomb(56)", info.SortName);
        Assert.Equal("pirate-special-runtime", info.NextSeam);
        Assert.Contains("ScurvyBomb", info.Reason);
        Assert.Contains("spell 11040", info.Reason);
        Assert.Contains("visible-monster", info.Reason);
        Assert.Contains("visible-player fatigue/status before Physical.OnPlayer", info.Reason);
        Assert.Contains("player-target WildPhoenix defender charge/archive override before ScurvyBomb *2 scaling", info.Reason);
        Assert.Contains("visible-SOB damage", info.Reason);
        Assert.DoesNotContain("SOB target handling", info.Reason);
        AssertContainsModeledSharedPlayerCanAttackGates(info.Reason);
        AssertContainsModeledSharedSobCanAttackGates(info.Reason);
        Assert.Contains("exact rune gaps", info.Reason);
        AssertContainsCanAttackPlayerRelationGateReview(info.Reason);
        AssertContainsRemainingCanAttackNpcGaps(info.Reason);
        Assert.Contains("MagicSort.ScurvyBomb", info.SourceEvidence);
        Assert.Contains("HandleLegacyPlayerPirateSpecialMagicAsync", info.SourceEvidence);
        Assert.Contains("visible-player fatigue/status plus player-target WildPhoenix defender charge/archive hook", info.SourceEvidence);
        Assert.Contains("ScurvyBomb.cs applies visible-player fatigue/status before Physical.OnPlayer", info.SourceEvidence);
        Assert.Contains("Physical.cs applies target.Owner.CombatGear.WildPhoenix()", info.SourceEvidence);
        Assert.Contains("visible-SOB damage", info.SourceEvidence);
        Assert.DoesNotContain("SOB target handling", info.SuggestedNextOwner);
        Assert.Contains("exact rune gaps", info.SuggestedNextOwner);
        AssertContainsCanAttackPlayerRelationGateReview(info.SuggestedNextOwner);
        AssertContainsRemainingCanAttackNpcGaps(info.SuggestedNextOwner);
        Assert.Contains(
            "magicSort=ScurvyBomb(56) spellId=11040 capability=pirateSpecial status=partiallyModeled supported=true nextSeam=pirate-special-runtime",
            info.Diagnostic);
    }

    [Fact]
    public void ClassifyShurikenVortexActivationAndAutomaticDamageAsSourceBackedPartialRuntime()
    {
        var info = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.ShurikenVortex, spellId: 6_010);

        Assert.Equal(LegacyMagicSortCapability.XpSkill, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("ShurikenVortex(28)", info.SortName);
        Assert.Equal("shurikenvortex-automatic-runtime", info.NextSeam);
        Assert.Contains("source-ordered XPCircle", info.Reason);
        Assert.Contains("AutoHunt leaves the skill unopened", info.Reason);
        Assert.Contains("inclusive one-second", info.Reason);
        Assert.Contains("row 6009 level zero", info.Reason);
        Assert.Contains("strict-range ordered monster/player/SOB", info.Reason);
        Assert.Contains("one combined monster/SOB return-experience update", info.Reason);
        Assert.Contains("keeps the D13 animation last", info.Reason);
        Assert.Contains("worldData missing remains a guarded unchanged path", info.Reason);
        Assert.Contains("complete matching-service-pack nonzero Physical.OnPlayer branch is modeled", info.Reason);
        Assert.Contains(
            "Refinery succeeds iff adjusted Breakthrough > adjusted Counteraction and roll < difference",
            info.Reason);
        Assert.Contains("deterministic runtime test override", info.Reason);
        Assert.Contains(
            "target Barrier and its source-nested attacker TacitStrike apply in sequential-floor order",
            info.Reason);
        Assert.Contains("lower attacker BattlePower and neither Sacrifice nor DeathSigh", info.Reason);
        Assert.Contains("adds 30 percent or 45 percent for spells 16480/16490", info.Reason);
        Assert.Contains("sets Penetration", info.Reason);
        Assert.Contains("/0.67 compensation or /0.66 for close spell 12080", info.Reason);
        Assert.Contains("suppresses the later 50-percent bonus", info.Reason);
        Assert.Contains(
            "failed refinery, BattlePower/status-exclusion failure, or prior critical/lucky/dash path keeps flag2 false",
            info.Reason);
        Assert.Contains("receives that later 50-percent bonus", info.Reason);
        Assert.Contains("Source order places compensation before DefensiveStance and Infinity", info.Reason);
        Assert.Contains("flag2-controlled 50-percent branch before IronShield", info.Reason);
        Assert.Contains("IronShield publishes BlueDamage type 55", info.Reason);
        Assert.Contains("incoming damage is less than or equal to the shield", info.Reason);
        Assert.Contains("forwarding only overflow when incoming damage is strictly greater", info.Reason);
        Assert.Contains("After BackDmg, target MudWall flag 0x112", info.Reason);
        Assert.Contains(
            "equipped Ninja MudSigilImpeccable type 301 directly subtracts current damage * (Power / 100) / 100",
            info.Reason);
        Assert.Contains("Ninja class and active primary/alternate equipment-set checks apply", info.Reason);
        Assert.Contains("then Shuriken's caller /6 follows", info.Reason);
        Assert.Contains("remains partial for remaining ReceiveAttack tails", info.Reason);
        Assert.Contains("caller-specific CanAttack edges", info.Reason);
        Assert.DoesNotContain("partial for MudWall", info.Reason);
        Assert.Contains("does not claim full Physical parity", info.Reason);
        Assert.Contains("CombatGearAttack.cs case 6010", info.SourceEvidence);
        Assert.Contains("OpenXpSkill(MsgUpdate.Flags.ShurikenVortex, 20, 1)", info.SourceEvidence);
        Assert.Contains("ThreadPool.cs flag key 46", info.SourceEvidence);
        Assert.Contains("final due tick", info.SourceEvidence);
        Assert.Contains("Pool.Magic[6009][0]", info.SourceEvidence);
        Assert.Contains("one IncreaseExperience.Up", info.SourceEvidence);
        Assert.Contains("DefensiveStance halves damage and marks Block", info.SourceEvidence);
        Assert.Contains("InfinitySDamage", info.SourceEvidence);
        Assert.Contains("Physical.cs lines 394, 577-658, 770-773, and 913-949", info.SourceEvidence);
        Assert.Contains("Base.cs lines 156-173", info.SourceEvidence);
        Assert.Contains("a 0-99 roll < their difference", info.SourceEvidence);
        Assert.Contains(
            "equipped target Barrier then applies before its source-nested attacker TacitStrike in sequential-floor order",
            info.SourceEvidence);
        Assert.Contains("Target IronShield emits AzureShield BlueDamage type 55", info.SourceEvidence);
        Assert.Contains("Physical.cs lines 979-986", info.SourceEvidence);
        Assert.Contains("target MudWall flag 0x112", info.SourceEvidence);
        Assert.Contains("damage -= damage * (DBItem.Power / 100) / 100", info.SourceEvidence);
        Assert.Contains("Ninja.TryGetValueEquip enforces Ninja class", info.SourceEvidence);
        Assert.Contains("active primary/alternate equipment set", info.SourceEvidence);
        Assert.Contains("direct reduction runs after BackDmg", info.SourceEvidence);
        Assert.Contains("applies Shuriken's player /6 afterward", info.SourceEvidence);
        Assert.Contains(
            "No learned 16440 chance, Damage2, or LegacyMudWallRoll reducer exists in the matching DecompiledSource",
            info.SourceEvidence);
        Assert.Contains("exact local D13 client remains wire authority", info.SourceEvidence);
        Assert.Contains("Source7770 contributes nothing", info.SourceEvidence);
        Assert.Contains("opcode 2239", info.SourceEvidence);
        Assert.DoesNotContain("Source7770", info.Reason);
        Assert.Contains("finish remaining ReceiveAttack tails", info.SuggestedNextOwner);
        Assert.Contains("caller-specific CanAttack edges", info.SuggestedNextOwner);
        Assert.DoesNotContain("finish MudWall", info.SuggestedNextOwner);
        Assert.DoesNotContain("DragonSwing", info.SuggestedNextOwner);
        Assert.Contains(
            "magicSort=ShurikenVortex(28) spellId=6010 capability=xpSkill status=partiallyModeled supported=true nextSeam=shurikenvortex-automatic-runtime",
            info.Diagnostic);
    }

    [Fact]
    public void ClassifyManiacDanceActivationAndAutomaticDamageAsSourceBackedPartialRuntime()
    {
        var info = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.ManiacDance, spellId: 12_700);

        Assert.Equal(LegacyMagicSortCapability.XpSkill, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("ManiacDance(83)", info.SortName);
        Assert.Equal("maniacdance-automatic-runtime", info.NextSeam);
        Assert.Contains("resource-first XPList activation", info.Reason);
        Assert.Contains("15-second flag", info.Reason);
        Assert.Contains("equipment-speed initial delay", info.Reason);
        Assert.Contains("one-second caster-centered radius-5", info.Reason);
        Assert.Contains("character XP without spell progress", info.Reason);
        Assert.Contains("one combined return update", info.Reason);
        Assert.Contains("empty animation when no target qualifies", info.Reason);
        Assert.Contains("keeps the animation last", info.Reason);
        Assert.Contains("ShurikenVortex/ManiacDance before WildPhoenix", info.Reason);
        Assert.Contains("worldData missing remains a guarded unchanged path", info.Reason);
        Assert.Contains("complete matching-service-pack nonzero Physical.OnPlayer branch is modeled", info.Reason);
        Assert.Contains(
            "Refinery succeeds iff adjusted Breakthrough > adjusted Counteraction and roll < difference",
            info.Reason);
        Assert.Contains("deterministic runtime test override", info.Reason);
        Assert.Contains(
            "target Barrier and its source-nested attacker TacitStrike apply in sequential-floor order",
            info.Reason);
        Assert.Contains("lower attacker BattlePower and neither Sacrifice nor DeathSigh", info.Reason);
        Assert.Contains("adds 30 percent or 45 percent for spells 16480/16490", info.Reason);
        Assert.Contains("sets Penetration", info.Reason);
        Assert.Contains("/0.67 compensation or /0.66 for close spell 12080", info.Reason);
        Assert.Contains("suppresses the later 50-percent bonus", info.Reason);
        Assert.Contains(
            "failed refinery, BattlePower/status-exclusion failure, or prior critical/lucky/dash path keeps flag2 false",
            info.Reason);
        Assert.Contains("receives that later 50-percent bonus", info.Reason);
        Assert.Contains("Source order places compensation before DefensiveStance and Infinity", info.Reason);
        Assert.Contains("flag2-controlled 50-percent branch before IronShield", info.Reason);
        Assert.Contains("ManiacDance has no Shuriken caller /6 step", info.Reason);
        Assert.Contains("IronShield publishes BlueDamage type 55", info.Reason);
        Assert.Contains("incoming damage is less than or equal to the shield", info.Reason);
        Assert.Contains("forwarding only overflow when incoming damage is strictly greater", info.Reason);
        Assert.Contains("After BackDmg, target MudWall flag 0x112", info.Reason);
        Assert.Contains(
            "equipped Ninja MudSigilImpeccable type 301 directly subtracts current damage * (Power / 100) / 100",
            info.Reason);
        Assert.Contains("Ninja class and active primary/alternate equipment-set checks apply", info.Reason);
        Assert.Contains("ManiacDance has no Shuriken caller /6 step", info.Reason);
        Assert.Contains("remains partial for remaining ReceiveAttack tails", info.Reason);
        Assert.Contains("caller-specific CanAttack edges", info.Reason);
        Assert.DoesNotContain("partial for MudWall", info.Reason);
        Assert.Contains("does not claim full Physical parity", info.Reason);
        Assert.Contains("ManiacDance.cs", info.SourceEvidence);
        Assert.Contains("ThreadPool.cs FloorCallback", info.SourceEvidence);
        Assert.Contains("strict now > ManiacDanceStamp", info.SourceEvidence);
        Assert.Contains("no-op during AutoHunt", info.SourceEvidence);
        Assert.Contains("one IncreaseExperience.Up", info.SourceEvidence);
        Assert.Contains("DefensiveStance halves damage and marks Block", info.SourceEvidence);
        Assert.Contains("InfinitySDamage", info.SourceEvidence);
        Assert.Contains("Physical.cs lines 394, 577-658, 770-773, and 913-949", info.SourceEvidence);
        Assert.Contains("Base.cs lines 156-173", info.SourceEvidence);
        Assert.Contains("a 0-99 roll < their difference", info.SourceEvidence);
        Assert.Contains(
            "equipped target Barrier then applies before its source-nested attacker TacitStrike in sequential-floor order",
            info.SourceEvidence);
        Assert.Contains("Target IronShield emits AzureShield BlueDamage type 55", info.SourceEvidence);
        Assert.Contains("Physical.cs lines 979-986", info.SourceEvidence);
        Assert.Contains("target MudWall flag 0x112", info.SourceEvidence);
        Assert.Contains("damage -= damage * (DBItem.Power / 100) / 100", info.SourceEvidence);
        Assert.Contains("Ninja.TryGetValueEquip enforces Ninja class", info.SourceEvidence);
        Assert.Contains("active primary/alternate equipment set", info.SourceEvidence);
        Assert.Contains("direct reduction runs after BackDmg", info.SourceEvidence);
        Assert.Contains("ManiacDance has no Shuriken caller /6 step", info.SourceEvidence);
        Assert.Contains(
            "No learned 16440 chance, Damage2, or LegacyMudWallRoll reducer exists in the matching DecompiledSource",
            info.SourceEvidence);
        Assert.Contains("exact local D13 client remains wire authority", info.SourceEvidence);
        Assert.Contains("Source7770 contributes nothing", info.SourceEvidence);
        Assert.Contains("MsgSpellAnimation last", info.SourceEvidence);
        Assert.Contains("right/left/ring Frequency", info.SourceEvidence);
        Assert.Contains("opcode 2239", info.SourceEvidence);
        Assert.Contains("finish remaining ReceiveAttack tails", info.SuggestedNextOwner);
        Assert.Contains("caller-specific CanAttack edges", info.SuggestedNextOwner);
        Assert.DoesNotContain("finish MudWall", info.SuggestedNextOwner);
        Assert.DoesNotContain("DragonSwing", info.SuggestedNextOwner);
        Assert.Contains(
            "magicSort=ManiacDance(83) spellId=12700 capability=xpSkill status=partiallyModeled supported=true nextSeam=maniacdance-automatic-runtime",
            info.Diagnostic);
    }

    [Theory]
    [InlineData(28, 6_010, "then Shuriken's caller /6 follows")]
    [InlineData(83, 12_700, "ManiacDance has no Shuriken caller /6 step")]
    public void ClassifyAutomaticPlayerPhysicalPostIronShieldAndDragonSwingTailAsModeledButStillPartial(
        int sortValue,
        ushort spellId,
        string expectedCallerOrder)
    {
        var info = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)sortValue, spellId);

        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Contains("bounded post-IronShield tail", info.Reason);
        Assert.Contains("SlayerPercent / 100 when Slayer flag 0xDA is present", info.Reason);
        Assert.Contains("consumes that flag", info.Reason);
        Assert.Contains(
            "Rune eligibility requiring Reborn >= 2, Level >= 15, and an equipped job-valid rune",
            info.Reason);
        Assert.Contains("NoMercy clamps damage below 22000 to 22000", info.Reason);
        Assert.Contains("Assassin plus OnAutoHunt adds 38000", info.Reason);
        Assert.Contains(expectedCallerOrder, info.Reason);
        Assert.Contains("Target DragonSwing flag 0xA0", info.Reason);
        Assert.Contains("DragonSwingChance roll", info.Reason);
        Assert.Contains("eligible equipped SwingingTail adds 30", info.Reason);
        Assert.Contains("11030, 11060, 11590, 11600, 11650, and 11660", info.Reason);
        Assert.Contains("target-authored poisonmiss effect", info.Reason);
        Assert.Contains("sets damage to zero", info.Reason);
        Assert.Contains("adds 30 target Dodge", info.Reason);
        Assert.Contains("DragonSwing check follows the Slayer/rune tail and precedes BackDmg", info.Reason);
        Assert.Contains("After BackDmg, target MudWall flag 0x112", info.Reason);
        Assert.Contains("damage * (Power / 100) / 100", info.Reason);
        Assert.Contains("Ninja class and active primary/alternate equipment-set checks apply", info.Reason);

        Assert.Contains("Physical.cs lines 951-964", info.SourceEvidence);
        Assert.Contains(
            "Slayer flag 0xDA scales damage by SlayerPercent / 100 and is consumed",
            info.SourceEvidence);
        Assert.Contains("NoMercy clamps damage below 22000 to 22000", info.SourceEvidence);
        Assert.Contains("Assassin plus OnAutoHunt adds 38000", info.SourceEvidence);
        Assert.Contains("Rune.cs lines 328-364", info.SourceEvidence);
        Assert.Contains("Reborn >= 2, Level >= 15", info.SourceEvidence);
        Assert.Contains("Physical.cs lines 965-977", info.SourceEvidence);
        Assert.Contains("target DragonSwing flag 0xA0", info.SourceEvidence);
        Assert.Contains("eligible equipped SwingingTail rune adds 30", info.SourceEvidence);
        Assert.Contains("target DragonSwingChance roll", info.SourceEvidence);
        Assert.Contains("DBSpell IDs 11030, 11060, 11590, 11600, 11650, and 11660", info.SourceEvidence);
        Assert.Contains("successful Core.Rate", info.SourceEvidence);
        Assert.Contains("target-authored poisonmiss effect", info.SourceEvidence);
        Assert.Contains("sets damage to zero", info.SourceEvidence);
        Assert.Contains("increments target Dodge by 30", info.SourceEvidence);
        Assert.Contains("after Slayer/NoMercy/Assassin and before BackDmg", info.SourceEvidence);
        Assert.Contains("Physical.cs lines 979-986", info.SourceEvidence);
        Assert.Contains("damage -= damage * (DBItem.Power / 100) / 100", info.SourceEvidence);
        Assert.Contains("Ninja.TryGetValueEquip enforces Ninja class", info.SourceEvidence);
        Assert.Contains("active primary/alternate equipment set", info.SourceEvidence);
        Assert.Contains("direct reduction runs after BackDmg", info.SourceEvidence);
        Assert.Contains(
            "No learned 16440 chance, Damage2, or LegacyMudWallRoll reducer exists in the matching DecompiledSource",
            info.SourceEvidence);
        Assert.Contains("exact local D13 client remains wire authority", info.SourceEvidence);
        Assert.Contains("Source7770 contributes nothing", info.SourceEvidence);

        var partialGapStart = info.Reason.IndexOf("capability remains partial for", StringComparison.Ordinal);
        Assert.True(partialGapStart >= 0, "Expected remaining partial-gap evidence.");
        var partialGap = info.Reason[partialGapStart..];
        Assert.Contains(
            "remaining ReceiveAttack tails and caller-specific CanAttack edges",
            partialGap);
        Assert.DoesNotContain("MudWall", partialGap);
        Assert.DoesNotContain("DragonSwing", partialGap);
        Assert.DoesNotContain("Slayer", partialGap);
        Assert.DoesNotContain("NoMercy", partialGap);
        Assert.DoesNotContain("Assassin", partialGap);
        Assert.DoesNotContain("Slayer", info.SuggestedNextOwner);
        Assert.DoesNotContain("NoMercy", info.SuggestedNextOwner);
        Assert.DoesNotContain("Assassin", info.SuggestedNextOwner);
        Assert.DoesNotContain("DragonSwing", info.SuggestedNextOwner);
        Assert.DoesNotContain("MudWall", info.SuggestedNextOwner);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(12_701)]
    public void ClassifyGenericAndAdjacentManiacDanceIdsAsUnsupported(ushort spellId)
    {
        var info = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.ManiacDance, spellId);

        Assert.Equal(LegacyMagicModelingStatus.MissingRuntimeNotModeled, info.Status);
        Assert.False(info.Supported);
        Assert.Equal("ManiacDance(83)", info.SortName);
        Assert.DoesNotContain("exact ManiacDance 12700", info.Reason);
    }

    [Fact]
    public void ClassifyCannonBarrageStatusGatedMonsterPlayerSobAndXpFlagRuntimeSpellAsPartiallyModeledPirateSlice()
    {
        var info = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.CannonBarrage, spellId: 11_050);

        Assert.Equal(LegacyMagicSortCapability.PirateSpecial, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("CannonBarrage(57)", info.SortName);
        Assert.Equal("pirate-special-runtime", info.NextSeam);
        Assert.Contains("CannonBarrage", info.Reason);
        Assert.Contains("spell 11050", info.Reason);
        Assert.Contains("status-gated visible-monster radius-6 physical damage", info.Reason);
        Assert.Contains("visible-player radius-6 physical damage divided by 11", info.Reason);
        Assert.Contains("player-target WildPhoenix defender charge/archive damage override before the CannonBarrage /11 scaling step", info.Reason);
        Assert.Contains("source-shaped zero-damage WildPhoenix player target preservation", info.Reason);
        Assert.Contains("visible-SOB radius-6 physical damage", info.Reason);
        Assert.Contains("no-flag no-animation guard after the shared resource/cooldown gate", info.Reason);
        Assert.Contains("bounded XPList/OpenXpSkill/CannonBarrage flag transition slice", info.Reason);
        Assert.Contains("consumes XPList", info.Reason);
        Assert.Contains("opens or refreshes the CannonBarrage flag for the source 50-second timer window", info.Reason);
        Assert.Contains("resets XPCount to 0", info.Reason);
        Assert.Contains("removes other active OnXPSkill flags", info.Reason);
        Assert.Contains("expires the flag in the online runtime loop", info.Reason);
        AssertContainsModeledSharedPlayerCanAttackGates(info.Reason);
        AssertContainsModeledSharedSobCanAttackGates(info.Reason);
        Assert.Contains("exact rune gaps", info.Reason);
        AssertContainsCanAttackPlayerRelationGateReview(info.Reason);
        AssertContainsRemainingCanAttackNpcGaps(info.Reason);
        Assert.Contains("MagicSort.CannonBarrage", info.SourceEvidence);
        Assert.Contains("CannonBarrage.Execute", info.SourceEvidence);
        Assert.Contains("consumes XPList to open or refresh CannonBarrage with OpenXpSkill(..., 50)", info.SourceEvidence);
        Assert.Contains("Player.cs OpenXpSkill resets XPCount to 0", info.SourceEvidence);
        Assert.Contains("removes a different active OnXPSkill flag", info.SourceEvidence);
        Assert.Contains("applies the passed timer through AddFlag/UpdateFlag", info.SourceEvidence);
        Assert.Contains("returns without sending damage animation", info.SourceEvidence);
        Assert.Contains("returns without animation when CannonBarrage status is absent", info.SourceEvidence);
        Assert.Contains("calls Physical.OnPlayer for visible radius-6 player targets", info.SourceEvidence);
        Assert.Contains("then Base.CalculateSoul", info.SourceEvidence);
        Assert.Contains("divides visible radius-6 player physical damage by 11", info.SourceEvidence);
        Assert.Contains("applies visible SOB damage", info.SourceEvidence);
        Assert.DoesNotContain("exact OpenXpSkill 50-duration timer semantics", info.SuggestedNextOwner);
        Assert.DoesNotContain("XPList/OpenXpSkill state transitions", info.SuggestedNextOwner);
        Assert.DoesNotContain("CannonBarrage flag transitions", info.SuggestedNextOwner);
        Assert.DoesNotContain("visible-SOB damage", info.SuggestedNextOwner);
        Assert.DoesNotContain("player /11 damage", info.SuggestedNextOwner);
        AssertContainsCanAttackPlayerRelationGateReview(info.SuggestedNextOwner);
        AssertContainsRemainingCanAttackNpcGaps(info.SuggestedNextOwner);
        Assert.Contains(
            "magicSort=CannonBarrage(57) spellId=11050 capability=pirateSpecial status=partiallyModeled supported=true nextSeam=pirate-special-runtime",
            info.Diagnostic);
    }

    [Fact]
    public void ClassifyPirateXpSkillBlackbeardsMonsterPlayerSobAndXpFlagRuntimeSpellAsPartiallyModeledPirateSlice()
    {
        var info = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.PirateXpSkill, spellId: 11_060);

        Assert.Equal(LegacyMagicSortCapability.PirateSpecial, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("PirateXpSkill(63)", info.SortName);
        Assert.Equal("pirate-special-runtime", info.NextSeam);
        Assert.Contains("PirateXpSkill", info.Reason);
        Assert.Contains("spell 11060", info.Reason);
        Assert.Contains("CanUseSpell.Verified", info.Reason);
        Assert.Contains("XPList opens/refreshes BlackbeardsRage with OpenXpSkill(..., 50)", info.Reason);
        Assert.Contains("resets XPCount to 0", info.Reason);
        Assert.Contains("keeps the source 50-second timer window", info.Reason);
        Assert.Contains("removes other active OnXPSkill flags", info.Reason);
        Assert.Contains("expires the flag in the online runtime loop", info.Reason);
        Assert.Contains("returns without animation", info.Reason);
        Assert.Contains("non-XPList branch hits radius-6 visible monsters", info.Reason);
        Assert.Contains("players with damage *2", info.Reason);
        Assert.Contains("PirateXpSkill/BlackbeardsRage 11060 player Physical.OnPlayer WildPhoenix defender hook before *2 scaling", info.Reason);
        Assert.Contains("SOB NPCs", info.Reason);
        Assert.DoesNotContain("exact OpenXpSkill timer semantics", info.Reason);
        AssertContainsModeledSharedPlayerCanAttackGates(info.Reason);
        AssertContainsModeledSharedSobCanAttackGates(info.Reason);
        Assert.Contains("exact rune/source/event gaps", info.Reason);
        AssertContainsCanAttackPlayerRelationGateReview(info.Reason);
        AssertContainsRemainingCanAttackNpcGaps(info.Reason);
        Assert.Contains("MagicSort.PirateXpSkill", info.SourceEvidence);
        Assert.Contains("PirateXpSkill.Execute", info.SourceEvidence);
        Assert.Contains("CanUseSpell.Verified", info.SourceEvidence);
        Assert.Contains("XPList opens/refreshes BlackbeardsRage with OpenXpSkill(..., 50)", info.SourceEvidence);
        Assert.Contains("Player.cs OpenXpSkill resets XPCount to 0", info.SourceEvidence);
        Assert.Contains("removes a different active OnXPSkill flag", info.SourceEvidence);
        Assert.Contains("applies the passed timer through AddFlag/UpdateFlag", info.SourceEvidence);
        Assert.Contains("returns without animation", info.SourceEvidence);
        Assert.Contains("non-XPList branch hits radius-6 visible monsters", info.SourceEvidence);
        Assert.Contains("players with damage *2", info.SourceEvidence);
        Assert.Contains("PirateXpSkill.cs lines 46-54 call Physical.OnPlayer, then Base.CalculateSoul, then doubles player damage before ReceiveAttack.Player.Execute", info.SourceEvidence);
        Assert.Contains("Physical.cs applies target.Owner.CombatGear.WildPhoenix()", info.SourceEvidence);
        Assert.Contains("SOB NPCs", info.SourceEvidence);
        Assert.DoesNotContain("exact OpenXpSkill 50-duration timer semantics", info.SuggestedNextOwner);
        Assert.Contains("exact rune/source/event gaps", info.SuggestedNextOwner);
        AssertContainsCanAttackPlayerRelationGateReview(info.SuggestedNextOwner);
        AssertContainsRemainingCanAttackNpcGaps(info.SuggestedNextOwner);
        Assert.DoesNotContain("XPList/Blackbeards flag transition", info.SuggestedNextOwner);
        Assert.DoesNotContain("BlackbeardsRage flag transition", info.SuggestedNextOwner);
        Assert.Contains(
            "magicSort=PirateXpSkill(63) spellId=11060 capability=pirateSpecial status=partiallyModeled supported=true nextSeam=pirate-special-runtime",
            info.Diagnostic);
    }

    [Fact]
    public void ClassifyBlackSpotDirectMonsterStateAndActivePacketRuntimeSpellAsPartiallyModeledPirateSlice()
    {
        var info = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.BlackSpot, spellId: 11_120);

        Assert.Equal(LegacyMagicSortCapability.PirateSpecial, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("BlackSpot(58)", info.SortName);
        Assert.Equal("pirate-special-runtime", info.NextSeam);
        Assert.Contains("BlackSpot", info.Reason);
        Assert.Contains("spell 11120", info.Reason);
        Assert.Contains("direct target selection by OpponentUID", info.Reason);
        Assert.Contains("source CanAttack target filtering", info.Reason);
        AssertContainsModeledSharedPlayerCanAttackGates(info.Reason);
        AssertContainsModeledSharedSobCanAttackGates(info.Reason);
        Assert.Contains("caster-to-target distance <= 2", info.Reason);
        Assert.Contains("physical monster/player/SOB damage", info.Reason);
        Assert.Contains("BlackSpot direct player Physical.OnPlayer damage with player-target WildPhoenix defender charge/archive damage override", info.Reason);
        Assert.Contains("alive visible-monster and alive visible-player BlackSpot state", info.Reason);
        Assert.Contains("duration from Spell.Duration", info.Reason);
        Assert.Contains("active BlackspotCreate opcode 2188 packets", info.Reason);
        Assert.Contains("source player expiry/inactive BlackspotCreate(false) runtime ticks", info.Reason);
        Assert.Contains("reciprocal active player replay", info.Reason);
        Assert.Contains("newly visible monster active replay", info.Reason);
        Assert.Contains("source-backed monster BlackSpot inactive packet lifecycle for expiry/death", info.Reason);
        Assert.Contains("BlackSpot.cs", info.SourceEvidence);
        Assert.Contains("Attack.OpponentUID", info.SourceEvidence);
        Assert.Contains("distance <= 2", info.SourceEvidence);
        Assert.Contains("CanAttackMonster.Verified/CanAttackPlayer.Verified/CanAttackNpc.Verified", info.SourceEvidence);
        Assert.Contains("Physical.OnMonster/OnPlayer/OnNpcs", info.SourceEvidence);
        Assert.Contains("Stamp_BlackSpot = DateTime.Now.AddSeconds(Spell.Duration)", info.SourceEvidence);
        Assert.Contains("stream.BlackspotCreate(Active: true, UID)", info.SourceEvidence);
        Assert.Contains("MsgBlackspot.cs", info.SourceEvidence);
        Assert.Contains("opcode 2188", info.SourceEvidence);
        Assert.Contains("rune edge effects", info.SuggestedNextOwner);
        AssertContainsCanAttackPlayerRelationGateReview(info.SuggestedNextOwner);
        AssertContainsRemainingCanAttackNpcGaps(info.SuggestedNextOwner);
        Assert.DoesNotContain("broader CanAttack side-effect matrix", info.SuggestedNextOwner);
        Assert.DoesNotContain("capture-proven monster inactive packet edge", info.SuggestedNextOwner);
        Assert.DoesNotContain("player/SOB direct targets", info.SuggestedNextOwner);
        Assert.DoesNotContain("standalone player blackspot state", info.SuggestedNextOwner);
        Assert.DoesNotContain("AddBlackSpot/MoveLine synergies", info.SuggestedNextOwner);
        Assert.Contains(
            "magicSort=BlackSpot(58) spellId=11120 capability=pirateSpecial status=partiallyModeled supported=true nextSeam=pirate-special-runtime",
            info.Diagnostic);
    }

    [Fact]
    public void ClassifyAddBlackSpotVisibleMonsterPlayerMarkingRuntimeSpellAsPartiallyModeledPirateSlice()
    {
        var info = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.AddBlackSpot, spellId: 11_100);

        Assert.Equal(LegacyMagicSortCapability.PirateSpecial, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("AddBlackSpot(62)", info.SortName);
        Assert.Equal("pirate-special-runtime", info.NextSeam);
        Assert.Contains("AddBlackSpot", info.Reason);
        Assert.Contains("spell 11100/sort 62", info.Reason);
        Assert.Contains("gates on learned BlackSpot 11120", info.Reason);
        Assert.Contains("source CanAttackMonster/CanAttackPlayer filtering", info.Reason);
        AssertContainsModeledSharedPlayerCanAttackGates(info.Reason);
        Assert.Contains("monster/player-marking runtime slice", info.Reason);
        Assert.Contains("visible monsters first and then visible players", info.Reason);
        Assert.Contains("hard-coded 3/4/5 level budget and Attack.X/Y range", info.Reason);
        Assert.Contains("zero-damage animation targets", info.Reason);
        Assert.Contains("active LegacyBlackspotPacket opcode 2188", info.Reason);
        Assert.Contains("sources BlackSpot duration from learned BlackSpot 11120", info.Reason);
        Assert.Contains("shared BlackSpot packet lifecycle", info.Reason);
        Assert.Contains("player expiry/inactive plus active replay", info.Reason);
        Assert.Contains("source-backed monster inactive packets for expiry/death", info.Reason);
        Assert.Contains("Remaining parity is exact rune edge effects", info.Reason);
        AssertContainsCanAttackPlayerRelationGateReview(info.Reason);
        Assert.Contains("CanAttackNpc event-pole/statue checks are not part of this handler", info.Reason);
        Assert.DoesNotContain("Deferred parity remains", info.Reason);
        Assert.Contains("legacy AddBlackSpot handler has no SOB loop", info.Reason);
        Assert.Contains("AddBlackSpot.cs", info.SourceEvidence);
        Assert.Contains("CanUseSpell.Verified", info.SourceEvidence);
        Assert.Contains("requires learned BlackSpot spell 11120", info.SourceEvidence);
        Assert.Contains("learned BlackSpot 11120 Spell.Duration", info.SourceEvidence);
        Assert.Contains("hard-coded 3/4/5 AddBlackSpot level budget", info.SourceEvidence);
        Assert.Contains("Spell.Range from Attack.X/Y", info.SourceEvidence);
        Assert.Contains("CanAttackMonster.Verified/CanAttackPlayer.Verified", info.SourceEvidence);
        Assert.Contains("zero-damage MsgSpellAnimation targets", info.SourceEvidence);
        Assert.Contains("stream.BlackspotCreate(Active: true, UID)", info.SourceEvidence);
        Assert.Contains("LegacyBlackspotPacket opcode 2188", info.SourceEvidence);
        Assert.Contains("legacy AddBlackSpot handler has no SOB loop", info.SourceEvidence);
        Assert.DoesNotContain("visible-player target handling", info.SuggestedNextOwner);
        Assert.DoesNotContain("SOB target handling", info.SuggestedNextOwner);
        Assert.Contains("exact rune edge effects", info.SuggestedNextOwner);
        AssertContainsCanAttackPlayerRelationGateReview(info.SuggestedNextOwner);
        Assert.DoesNotContain("broader CanAttack side-effect matrix", info.SuggestedNextOwner);
        Assert.DoesNotContain("capture-proven inactive edge", info.SuggestedNextOwner);
        Assert.DoesNotContain("MoveLine synergy", info.SuggestedNextOwner);
        Assert.Contains(
            "magicSort=AddBlackSpot(62) spellId=11100 capability=pirateSpecial status=partiallyModeled supported=true nextSeam=pirate-special-runtime",
            info.Diagnostic);
    }

    [Fact]
    public void ClassifyMoveLineLineHitTideTrapLifecycleAndGuildWarGateCheckRuntimeSpellAsPartiallyModeledPirateSlice()
    {
        var info = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.MoveLine, spellId: 11_110);

        Assert.Equal(LegacyMagicSortCapability.PirateSpecial, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("MoveLine(61)", info.SortName);
        Assert.Equal("pirate-special-runtime", info.NextSeam);
        Assert.Contains("MoveLine", info.Reason);
        Assert.Contains("spell 11110/sort 61", info.Reason);
        Assert.Contains("bounded MoveLine runtime slice", info.Reason);
        Assert.Contains("12-cell MoveCoords.CheckBladeTeampsCoords line", info.Reason);
        Assert.Contains("caster to Attack.X/Y", info.Reason);
        Assert.Contains("models ValidLocation truncation", info.Reason);
        Assert.Contains("final valid line cell for animation X/Y", info.Reason);
        Assert.Contains("source CheckGuildWar final endpoint gate", info.Reason);
        Assert.Contains("map 1038 gate-crossing", info.Reason);
        Assert.Contains("updates the caster session position to the final line landing cell", info.Reason);
        Assert.Contains("normal/non-TideTrap casts", info.Reason);
        Assert.Contains("leaves the caster in place for equipped TideTrap plus learned spell 14250", info.Reason);
        Assert.Contains("TideTrap bomb flag through spell animation field 9", info.Reason);
        Assert.Contains("floor effect item 1080 named trap", info.Reason);
        Assert.Contains("can trigger/apply the bounded TideTrap floor-effect lifecycle", info.Reason);
        Assert.Contains("item 1080 named trap", info.Reason);
        Assert.Contains("source-shaped owner-to-trap line hit", info.Reason);
        Assert.Contains("source floor item's MoveLine DBSkill", info.Reason);
        Assert.Contains("14250 trap animation", info.Reason);
        Assert.Contains("monsters, players, and SOBs within <= 1.5 of the MoveLine path", info.Reason);
        Assert.Contains("player physical spell damage with player-target WildPhoenix defender charge/archive damage override before the MoveLine * 12 / 16 scaling step", info.Reason);
        Assert.Contains("source-shaped zero-damage WildPhoenix player targets for animation", info.Reason);
        Assert.Contains("SOB damage", info.Reason);
        Assert.Contains("active BlackSpot opcode 2188 packets", info.Reason);
        Assert.Contains("learned BlackSpot 11120 procs/is modeled", info.Reason);
        AssertContainsModeledSharedPlayerCanAttackGates(info.Reason);
        AssertContainsModeledSharedSobCanAttackGates(info.Reason);
        Assert.DoesNotContain("Deferred parity remains physical caster reposition", info.Reason);
        Assert.DoesNotContain("exact TideTrap trap invocation/damage lifecycle", info.Reason);
        Assert.DoesNotContain("full TideTrap timer semantics beyond the modeled invoke stamp", info.Reason);
        Assert.DoesNotContain("full TideTrap timer semantics", info.Reason);
        Assert.Contains("stored source floor item's MoveLine DBSkill", info.Reason);
        Assert.Contains("Shared BlackSpot player expiry/inactive and active replay behavior is modeled", info.Reason);
        Assert.Contains("full GuildWar gate open lifecycle", info.Reason);
        AssertContainsCanAttackPlayerRelationGateReview(info.Reason);
        AssertContainsRemainingCanAttackNpcGaps(info.Reason);
        Assert.Contains("does not claim full GuildWar jump parity", info.Reason);
        Assert.DoesNotContain("exact CheckGuildWar/source precheck ordering", info.Reason);
        Assert.DoesNotContain("including ValidLocation semantics", info.Reason);
        Assert.Contains("MoveLine.cs", info.SourceEvidence);
        Assert.Contains("CanUseSpell.Verified", info.SourceEvidence);
        Assert.Contains("12-cell MoveCoords.CheckBladeTeampsCoords path", info.SourceEvidence);
        Assert.Contains("ValidLocation to truncate the line", info.SourceEvidence);
        Assert.Contains("final valid line cell for animation X/Y", info.SourceEvidence);
        Assert.Contains("source CheckGuildWar final endpoint gate for map 1038 gate-crossing", info.SourceEvidence);
        Assert.Contains("within <= 1.5 of the path", info.SourceEvidence);
        Assert.Contains("player physical spell damage * 12 / 16", info.SourceEvidence);
        Assert.Contains("SOB damage", info.SourceEvidence);
        Assert.Contains("BlackspotCreate opcode 2188", info.SourceEvidence);
        Assert.Contains("learned BlackSpot 11120 procs", info.SourceEvidence);
        Assert.Contains("moves the view then assigns user.Player.X/Y to the final line landing cell", info.SourceEvidence);
        Assert.Contains("MsgSpellAnimation.bomb/SpellEffect=1", info.SourceEvidence);
        Assert.Contains("floor spell effect item 1080 named trap", info.SourceEvidence);
        Assert.Contains("FloorSpell.cs stamps FloorSpell", info.SourceEvidence);
        Assert.Contains("DateTime.Now.AddMilliseconds(MillisecondsStamp)", info.SourceEvidence);
        Assert.Contains("storing the MoveLine Spell on the FloorSpell", info.SourceEvidence);
        Assert.Contains("14250 ClientFloorSpells entry", info.SourceEvidence);
        Assert.Contains("ThreadPool.cs case 14250", info.SourceEvidence);
        Assert.Contains("ClientFloorSpells.CheckInvocke", info.SourceEvidence);
        Assert.Contains("12-cell owner-to-trap line", info.SourceEvidence);
        Assert.Contains("within <= 2.0 of that line", info.SourceEvidence);
        Assert.Contains("uses the floor spell DBSkill for target verification", info.SourceEvidence);
        Assert.Contains("player physical damage * 60 / 100", info.SourceEvidence);
        Assert.Contains("removes the floor effect", info.SourceEvidence);
        Assert.Contains("drops the ClientFloorSpells entry when empty", info.SourceEvidence);
        Assert.DoesNotContain("physical caster reposition", info.SuggestedNextOwner);
        Assert.DoesNotContain("TideTrap trap invocation/damage lifecycle", info.SuggestedNextOwner);
        Assert.DoesNotContain("full TideTrap timer semantics", info.SuggestedNextOwner);
        Assert.DoesNotContain("bounded floor-effect invoke/apply slice", info.SuggestedNextOwner);
        Assert.DoesNotContain("full BlackSpot inactive/expiry/replay behavior", info.SuggestedNextOwner);
        Assert.Contains("full GuildWar gate open lifecycle", info.SuggestedNextOwner);
        Assert.Contains("full MoveLine/GuildWar parity", info.SuggestedNextOwner);
        AssertContainsCanAttackPlayerRelationGateReview(info.SuggestedNextOwner);
        AssertContainsRemainingCanAttackNpcGaps(info.SuggestedNextOwner);
        Assert.DoesNotContain("exact CheckGuildWar/source precheck ordering", info.SuggestedNextOwner);
        Assert.DoesNotContain("ValidLocation semantics", info.SuggestedNextOwner);
        Assert.Contains(
            "magicSort=MoveLine(61) spellId=11110 capability=pirateSpecial status=partiallyModeled supported=true nextSeam=pirate-special-runtime",
            info.Diagnostic);
    }

    [Fact]
    public void ClassifyRemainingMoveLineSpellKeepsGuildWarGateCheckSliceBounded()
    {
        var info = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.MoveLine, spellId: 11_111);

        Assert.Equal(LegacyMagicSortCapability.PirateSpecial, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.MissingRuntimeNotModeled, info.Status);
        Assert.False(info.Supported);
        Assert.Equal("MoveLine(61)", info.SortName);
        Assert.Equal("pirate-special-runtime", info.NextSeam);
        Assert.Contains("MoveLine 11110 line-hit damage/animation/reposition/active-packet plus TideTrap floor-effect trigger/apply lifecycle and ValidLocation/map-1038 GuildWar final-endpoint gate slice", info.Reason);
        Assert.DoesNotContain("full GuildWar jump parity", info.Reason);
    }

    [Fact]
    public void ClassifyPhysicalSpellsSortWithSourceBackedCombatDiagnostic()
    {
        var info = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)55, spellId: 11_140);

        Assert.Equal(LegacyMagicSortCapability.SpecialCombatDamage, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("PhysicalSpells(55)", info.SortName);
        Assert.Equal("physical-spells-combat-runtime", info.NextSeam);
        Assert.Contains("PhysicalSpells.Execute", info.Reason);
        Assert.Contains("CanUseSpell-style", info.Reason);
        Assert.Contains("three physical hits", info.Reason);
        Assert.Contains("TripleAttack 10490/13610", info.Reason);
        Assert.Contains("11140", info.Reason);
        Assert.Contains("Windstorm 11140/13290", info.Reason);
        Assert.Contains("WildPhoenix defender charge/archive hook before the source /2 scaling step", info.Reason);
        Assert.Contains("defended hits animate as zero damage", info.Reason);
        Assert.Contains("Physical.OnMonster IsFloor early return as three damage-2 floor hits", info.Reason);
        Assert.Contains("ShurikenVortex/ManiacDance target-status early return before WildPhoenix", info.Reason);
        Assert.Contains("without consuming WildPhoenix charges", info.Reason);
        Assert.Contains("opening ReceiveAttack.Player envelope", info.Reason);
        Assert.Contains("speffect groups 2/3/4/5", info.Reason);
        Assert.Contains("Ninja SageMode using gouyu Chance / 100", info.Reason);
        Assert.Contains("only Windstorm 11140 chains", info.Reason);
        Assert.Contains("BloomofDeath 14720", info.Reason);
        Assert.Contains("later ReceiveAttack.Player tail", info.Reason);
        Assert.Contains("magictype.txt rows 10490-10494 TripleAttack, 11140-11144 Windstorm, 13290 Windstorm, and 13610 TripleAttack use sort 55", info.SourceEvidence);
        Assert.Contains("MsgAttackPacket.ProcescMagic case MagicSort.PhysicalSpells", info.SourceEvidence);
        Assert.Contains("PhysicalSpells.cs calls CanUseSpell.Verified", info.SourceEvidence);
        Assert.Contains("Each player hit calls ReceiveAttack.Player.Execute", info.SourceEvidence);
        Assert.Contains("defender speffect groups 3/4/5", info.SourceEvidence);
        Assert.Contains("Ninja SageMode using gouyu Chance / 100", info.SourceEvidence);
        Assert.Contains("Physical.cs returns monster damage 2 immediately when MonsterRole.IsFloor is true", info.SourceEvidence);
        Assert.Contains("GameMap.AddFloor marks those floor monsters/effects with IsFloor, 25 max HP, and 25 current HP", info.SourceEvidence);
        Assert.Contains("Physical.cs returns raw player damage 1 when the target has ShurikenVortex or ManiacDance before the later WildPhoenix branch", info.SourceEvidence);
        Assert.Contains("Line.cs contains a case 10490 branch", info.SourceEvidence);
        Assert.Contains("LayTrap.Execute", info.SourceEvidence);
        Assert.Contains("exact damage/effect side effects", info.SuggestedNextOwner);
        Assert.DoesNotContain("broader special-combat sort rows", info.Reason);
        Assert.DoesNotContain("continue special-combat sort rows", info.SuggestedNextOwner);
        Assert.Contains(
            "magicSort=PhysicalSpells(55) spellId=11140 capability=specialCombatDamage status=partiallyModeled supported=true nextSeam=physical-spells-combat-runtime",
            info.Diagnostic);
        Assert.Contains("PhysicalSpells.Execute", info.Diagnostic);
    }

    [Theory]
    [InlineData(90, 15_940, "Wildwind(90)", "AxeShadow")]
    [InlineData(5, 7_030, "Bomb(5)", "CelestialDance")]
    public void ClassifyAxeShadowCelestialDanceStatusSettersAsModeledRuntimeWithSharedSideEffectDebt(
        int sort,
        ushort spellId,
        string sortName,
        string statusName)
    {
        var info = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)sort, spellId);

        Assert.Equal(LegacyMagicSortCapability.StateAndBuffRuntime, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal(sortName, info.SortName);
        Assert.Equal("axeshadow-celestialdance-status-runtime", info.NextSeam);
        Assert.Contains("target damage", info.Reason);
        Assert.Contains(statusName, info.Reason);
        Assert.Contains("duration-backed runtime expiry", info.Reason);
        Assert.Contains("exact shared CanAttack/global side effects", info.Reason);
        Assert.Contains("Wildwind.cs case 15940", info.SourceEvidence);
        Assert.Contains("Bomb.cs case 7030", info.SourceEvidence);
        Assert.Contains("DataType.CelestialDance = 0x77", info.SourceEvidence);
        Assert.Contains("tighten exact shared CanAttack/global side effects", info.SuggestedNextOwner);
        Assert.DoesNotContain("add the 15940 Wildwind and 7030 CelestialDance target-damage loops", info.SuggestedNextOwner);
        Assert.Contains(
            $"spellId={spellId} capability=stateAndBuffRuntime status=partiallyModeled supported=true nextSeam=axeshadow-celestialdance-status-runtime",
            info.Diagnostic);
    }

    [Fact]
    public void ClassifyBreathFocusAsFullyModeledStaminaRestoreRuntime()
    {
        var info = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.BreathFocus, 11_960);

        Assert.Equal(LegacyMagicSortCapability.StaminaRestoreSupport, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.FullyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("BreathFocus(70)", info.SortName);
        Assert.Equal("stamina-restore-runtime", info.NextSeam);
        Assert.Contains("optional caster stamina restore by DBSpell.Damage", info.Reason);
        Assert.Contains("targetless MsgSpellAnimation from caster UID at caster X/Y", info.Reason);
        Assert.Contains("automatic Trojan fallback selection stays outside", info.Reason);
        Assert.Contains("BreathFocus.cs calls CanUseSpell.Verified", info.SourceEvidence);
        Assert.Contains("UpdateSpell.CheckUpdate(..., 1000)", info.SourceEvidence);
        Assert.Contains(
            "magicSort=BreathFocus(70) spellId=11960 capability=staminaRestoreSupport status=fullyModeled supported=true nextSeam=stamina-restore-runtime",
            info.Diagnostic);
    }

    [Fact]
    public void ClassifyKineticSparkDirectToggleAndPassiveProcsAsPartiallyModeledWithSideEffectDebt()
    {
        var info = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.KineticSpark, spellId: 11_590);
        const string pirateBlackJadeUseRuneFamilySpellIds = "21590/21730/21740/21750/21760/21770/21780/21790/21800/21810/21820";

        Assert.Equal(LegacyMagicSortCapability.SpecialCombatDamage, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("KineticSpark(67)", info.SortName);
        Assert.Equal("kineticspark-attackspell-caller-side-effect-parity", info.NextSeam);
        Assert.Contains("direct Execute toggle", info.Reason);
        Assert.Contains("status flag 0x93", info.Reason);
        Assert.Contains("2592000-second RemoveOnDead duration", info.Reason);
        Assert.Contains("self-target spell animation", info.Reason);
        Assert.Contains("monster passive proc", info.Reason);
        Assert.Contains("AttackSpell case 11590 monster branch", info.Reason);
        Assert.Contains("player passive proc", info.Reason);
        Assert.Contains("AttackSpell case 11590 player branch", info.Reason);
        Assert.Contains("SOB passive proc", info.Reason);
        Assert.Contains("AttackSpell case 11590 SOB branch", info.Reason);
        Assert.Contains("Archer caller-path", info.Reason);
        Assert.Contains("source direct-target Base.GetDistance <= 18 caller gate", info.Reason);
        Assert.Contains("primary direct-target CanAttackMonster gate before the Kinetic 30% rate gate", info.Reason);
        Assert.Contains("primary direct-target CanAttackPlayer gate before the Kinetic 30% rate gate", info.Reason);
        Assert.Contains("primary direct-target CanAttackNpc gate before the Kinetic 30% rate gate", info.Reason);
        Assert.Contains("primary visible monster targeting", info.Reason);
        Assert.Contains("chain monsters within Spell.Range / 2", info.Reason);
        Assert.Contains("StarRaid target budget", info.Reason);
        Assert.Contains("Range.OnMonster damage shape", info.Reason);
        Assert.Contains("monster experience/progress updates", info.Reason);
        Assert.Contains("primary visible-player targeting", info.Reason);
        Assert.Contains("primary visible-player targeting through Physical.OnPlayer", info.Reason);
        Assert.Contains("chained visible players inside strict Spell.Range / 2 through Range.OnPlayer", info.Reason);
        Assert.Contains("shared CanAttackPlayer gates", info.Reason);
        Assert.Contains("ShurikenVortex/ManiacDance before WildPhoenix", info.Reason);
        Assert.Contains("Range DefensiveStance 40 percent Block return without BackDmg", info.Reason);
        Assert.Contains("Heaven's Wonder 16350 BackDmg on normal eligible hits", info.Reason);
        Assert.Contains("receive packets before one shared animation", info.Reason);
        Assert.Contains("player HP/death mutation", info.Reason);
        Assert.Contains("bounded mutual ThundercloudSight side effects", info.Reason);
        Assert.Contains("bounded player-hit defender CombatGear UpdateTarget self/status passives", info.Reason);
        Assert.Contains("bounded defender speffect_attribute.Get Solid/Bloodthirst/Elan self-effects", info.Reason);
        Assert.Contains("bounded attacker Venom speffect type 2 bridge through modeled AttackStatus spell 20000", info.Reason);
        Assert.Contains("zero-damage spell progress", info.Reason);
        Assert.Contains("primary visible-SOB targeting", info.Reason);
        Assert.Contains("chained visible SOB NPCs within Spell.Range / 2", info.Reason);
        Assert.Contains("shared CanAttackNpc gates", info.Reason);
        Assert.Contains("source physical primary damage", info.Reason);
        Assert.Contains("source magic chained damage", info.Reason);
        Assert.Contains("SOB HP/death mutation", info.Reason);
        Assert.Contains("bounded caster ThundercloudSight and AttackHit side effects", info.Reason);
        Assert.Contains(
            "bounded SOB receive-side CombatGear StoneCracker ArrowBlades/StarFlow 10950 counter/status branch",
            info.Reason);
        Assert.Contains("bounded SOB receive-side CombatGear Dragonhowl/Bloodlust self-status branches", info.Reason);
        Assert.Contains("bounded SOB receive-side CombatGear Taoist DeadwoodCurse status branch", info.Reason);
        Assert.Contains("Taoist3 CrackMantra 20380 SOB no-op", info.Reason);
        Assert.Contains("defender UpdateTarget player-only skip", info.Reason);
        Assert.Contains("bounded SOB receive-side CombatGear Pirate Armed/Sense status branches", info.Reason);
        Assert.Contains("Pirate Dark 21930 SOB no-op interaction/targetless-animation", info.Reason);
        Assert.Contains("black-jade random UseRune status branches", info.Reason);
        Assert.Contains("bounded KineticSpark player receive InfiniteMist ArchiveSkill emit-order", info.Reason);
        Assert.Contains("bounded player receive-side Ninja SageMode branch using gouyu_immortal Chance", info.Reason);
        Assert.Contains(
            "production retains a direct player-magic MudWall/MudSigilImpeccable compatibility reduction",
            info.Reason);
        Assert.Contains("target flag 0x112 plus Ninja-gated active-set type 301 power", info.Reason);
        Assert.Contains("subtracts current damage * (Power / 100) / 100", info.Reason);
        Assert.Contains(
            "exact placement across KineticSpark primary/chained player callers remain unaudited",
            info.Reason);
        Assert.Contains(
            "retained direct MudSigilImpeccable compatibility fallback's exact primary/chained player-caller order",
            info.Reason);
        Assert.DoesNotContain("using learned MudWall 16440", info.Reason);
        Assert.DoesNotContain("chance-then-direct", info.Reason);
        Assert.Contains("bounded SOB receive-side Ninja SageMode branch using gouyu_immortal Chance", info.Reason);
        Assert.Contains("bounded monster receive-side Ninja SageMode branch using gouyu_immortal Rate", info.Reason);
        Assert.Contains("SOB experience/progress damage", info.Reason);
        Assert.Contains("MyCombatGear.CheckCombatGear consumption before the 18750/10990 passive hooks", info.Reason);
        Assert.Contains("animation only when at least one target exists", info.Reason);
        Assert.DoesNotContain("Remaining KineticSpark parity is the AttackSpell SOB passive proc branch", info.Reason);
        Assert.DoesNotContain("Remaining KineticSpark parity is CombatGear Update/UpdateTarget", info.Reason);
        Assert.DoesNotContain("Remaining KineticSpark parity is the source double-call shape", info.Reason);
        Assert.Contains("bounded attacker-side MyCombatGear.Update StoneCracker ArrowBlades/StarFlow 10950 counter/status/mixed-target HP branch", info.Reason);
        Assert.Contains("bounded attacker-side MyCombatGear.Update StoneCracker ArchShadow 18980 branch", info.Reason);
        Assert.Contains("bounded receive-side CombatGear Dragonhowl/Bloodlust status/power/self-animation branches", info.Reason);
        Assert.Contains("Dragonhowl 17620", info.Reason);
        Assert.Contains("Bloodlust 17600/17610", info.Reason);
        Assert.Contains("bounded receive-side CombatGear Taoist 20330/20380 status/power/self-animation branches", info.Reason);
        Assert.Contains("Taoist3/CrackMantra 20380", info.Reason);
        Assert.Contains("Taoist1/DeadwoodCurse 20330", info.Reason);
        Assert.Contains("bounded receive-side CombatGear Pirate 21710/21720/21930 status/buff-strip self-animation or target-animation branches", info.Reason);
        Assert.Contains("bounded receive-side CombatGear Pirate black-jade random UseRune status branches", info.Reason);
        Assert.Contains(pirateBlackJadeUseRuneFamilySpellIds, info.Reason);
        Assert.DoesNotContain("remaining attacker-side MyCombatGear.Update branches", info.Reason);
        Assert.DoesNotContain("unsupported individual UseRune spell execution paths", info.Reason);
        Assert.Contains("Taoist 20330/20380", info.Reason);
        Assert.Contains("Pirate 21710/21720/21930", info.Reason);
        Assert.Contains("StarFlow 10950 post-receive player animation damage/update ordering", info.Reason);
        Assert.Contains(
            "recursive StarFlow 10950 player-hit CombatGear.Update/UpdateTarget ordering",
            info.Reason);
        Assert.DoesNotContain("remaining 10950 defender UpdateTarget recursion", info.Reason);
        Assert.DoesNotContain("remaining 10950 recursive attacker CombatGear counter/update loops", info.Reason);
        Assert.DoesNotContain("Remaining KineticSpark parity is attacker-side MyCombatGear.Update, Ninja/Sage", info.Reason);
        Assert.Contains("defender UpdateTarget MagneticLight 20220 and AblazeBlade 20360 player branches", info.Reason);
        Assert.DoesNotContain("attacker Venom speffect_attribute.Get type 2", info.Reason);
        Assert.Contains("remaining Ninja/Sage branches outside the bounded player/SOB/monster SageMode paths", info.Reason);
        Assert.Contains("NeptuneCurse/AttackHit/EagleEye/SkillTournament/FreezeWar/Football/DragonWar/MirrorOfSin/LightOfStamina/CalmWind/DivineGuard/KillingFlash/DrainingTouch/BloodSpawn player receive modifiers", info.Reason);
        Assert.Contains("DragonWar playtest/economy polish", info.Reason);
        Assert.DoesNotContain("NeptuneCurse/EagleEye and later ReceiveAttack.Player branches", info.Reason);
        Assert.DoesNotContain("SkillTournament/FreezeWar and later ReceiveAttack.Player branches", info.Reason);
        Assert.DoesNotContain("remaining SOB CombatGear Update branches", info.Reason);
        Assert.DoesNotContain("player-target-only CrackMantra/UpdateTarget branches", info.Reason);
        Assert.DoesNotContain("remaining SOB CombatGear Update branches (StoneCracker", info.Reason);
        Assert.DoesNotContain("player-target-only CrackMantra/Pirate Dark/UpdateTarget branches", info.Reason);
        Assert.DoesNotContain("remaining SOB CombatGear Update branches (player-target-only CrackMantra/Pirate Dark/UpdateTarget branches and Pirate black-jade UseRune)", info.Reason);
        Assert.Contains("deeper rune/perfection counters", info.Reason);
        Assert.DoesNotContain("deeper rune/perfection/tournament counters", info.Reason);
        Assert.Contains("PK/tournament bookkeeping", info.Reason);
        Assert.Contains("event/SOB branches", info.Reason);
        Assert.Contains("exact CanAttack side effects", info.Reason);
        Assert.DoesNotContain("ThundercloudSight, perfection/rune/tournament counters", info.Reason);
        Assert.DoesNotContain("remaining combat gear caller details around the pre-proc/Kinetic sequence", info.Reason);
        AssertContainsCanAttackPlayerRelationGateReview(info.Reason);
        AssertContainsRemainingCanAttackNpcGaps(info.Reason);
        Assert.Contains("magictype.txt rows 1204-1208", info.SourceEvidence);
        Assert.Contains("spell 11590 KineticSpark as sort 67", info.SourceEvidence);
        Assert.Contains("Execute case 11590 toggles MsgUpdate.Flags.KineticSpark (0x93)", info.SourceEvidence);
        Assert.Contains("2592000 seconds with RemoveOnDead", info.SourceEvidence);
        Assert.Contains("self-target MsgSpellAnimation", info.SourceEvidence);
        Assert.Contains("KineticSpark.cs AttackSpell case 11590 monster branch", info.SourceEvidence);
        Assert.Contains("primary visible Monster", info.SourceEvidence);
        Assert.Contains("visible monsters within Spell.Range / 2", info.SourceEvidence);
        Assert.Contains("StarRaid-adjusted target budget", info.SourceEvidence);
        Assert.Contains("IncreaseExperience.Up", info.SourceEvidence);
        Assert.Contains("UpdateSpell.CheckUpdate", info.SourceEvidence);
        Assert.Contains("animation send only when targets exist", info.SourceEvidence);
        Assert.Contains("KineticSpark.cs lines 182-217", info.SourceEvidence);
        Assert.Contains("primary visible Player", info.SourceEvidence);
        Assert.Contains("CanAttackPlayer.Verified", info.SourceEvidence);
        Assert.Contains("Physical.OnPlayer", info.SourceEvidence);
        Assert.Contains("Range.OnPlayer", info.SourceEvidence);
        Assert.Contains("Base.CalculateExtraAttack(0, PhysicalDamageIncrease, PhysicalDamageDecrease)", info.SourceEvidence);
        Assert.Contains("using attacker PhysicalDamageIncrease and defender PhysicalDamageDecrease", info.SourceEvidence);
        Assert.Contains("WildPhoenix's fixed one-damage return", info.SourceEvidence);
        Assert.Contains("ReceiveAttack.Player.Execute", info.SourceEvidence);
        Assert.Contains("and lines 94-96 set mutual ThundercloudSight for player hits", info.SourceEvidence);
        Assert.Contains("ReceiveAttack.Player.Execute lines 17-18", info.SourceEvidence);
        Assert.Contains("MyCombatGear.Update lines 995-998", info.SourceEvidence);
        Assert.Contains("two source-shaped defender UpdateTarget attempts", info.SourceEvidence);
        Assert.Contains("speffect_attribute.Get(attacked.Owner, 3/4/5)", info.SourceEvidence);
        Assert.Contains("defender Solid/Bloodthirst/Elan self-effects", info.SourceEvidence);
        Assert.Contains("set mutual ThundercloudSight for player hits", info.SourceEvidence);
        Assert.Contains("bounded ThundercloudSight side effect", info.SourceEvidence);
        Assert.Contains("bounded player-hit defender CombatGear UpdateTarget self/status passive subset", info.SourceEvidence);
        Assert.Contains("bounded defender speffect_attribute.Get types 3-5", info.SourceEvidence);
        Assert.Contains("with those two attempts", info.SourceEvidence);
        Assert.Contains("speffect_attribute.Get(client, 2u, attacked.UID)", info.SourceEvidence);
        Assert.Contains("speffect_attribute.cs case 2 routes attacker Venom through spell 20000", info.SourceEvidence);
        Assert.Contains("MsgAttackPacket.ProcescMagic(..., ignoreStamp: true)", info.SourceEvidence);
        Assert.Contains("VenomLevel", info.SourceEvidence);
        Assert.Contains("GetVenom(level + 1) rate pass", info.SourceEvidence);
        Assert.Contains("bounded attacker Venom speffect type 2 bridge through modeled AttackStatus spell 20000", info.SourceEvidence);
        Assert.Contains("MyCombatGear.Update lines around 1000-1037", info.SourceEvidence);
        Assert.Contains("increment StoneCracker ArrowBlades and StarFlow", info.SourceEvidence);
        Assert.Contains("Water|Top4Weekly", info.SourceEvidence);
        Assert.Contains("SageSpeedMode|Top4Weekly", info.SourceEvidence);
        Assert.Contains("Lightning|Top4Weekly", info.SourceEvidence);
        Assert.Contains("fire ProcescMagic 10950 on the third StarFlow step", info.SourceEvidence);
        Assert.Contains("reset StarFlow", info.SourceEvidence);
        Assert.Contains("route the ArchShadow 18980 branch through ProcescMagic(18980)", info.SourceEvidence);
        Assert.Contains("MsgUpdate.cs flags identify Water as 0x10D", info.SourceEvidence);
        Assert.Contains("Lightning as 0x110", info.SourceEvidence);
        Assert.Contains("SageSpeedMode as 0x111", info.SourceEvidence);
        Assert.Contains("Top4Weekly as 0x40", info.SourceEvidence);
        Assert.Contains("CombatGearAttack.cs case 10950 runs the StarFlow AoE", info.SourceEvidence);
        Assert.Contains("visible monsters, players, and SOBs within distance <= 2.5", info.SourceEvidence);
        Assert.Contains("bounded attacker-side MyCombatGear.Update StoneCracker ArchShadow 18980 branch", info.SourceEvidence);
        Assert.Contains("bounded attacker-side MyCombatGear.Update StoneCracker ArrowBlades/StarFlow 10950 counter/status/mixed-target HP branch", info.SourceEvidence);
        Assert.Contains("bounded receive-side CombatGear Dragonhowl/Bloodlust status/power/self-animation branches", info.SourceEvidence);
        Assert.Contains("MyCombatGear.Update lines 1039-1046", info.SourceEvidence);
        Assert.Contains("ProcescMagic(17620, user.Player.UID, user.Player.UID, Target.X, Target.Y)", info.SourceEvidence);
        Assert.Contains("ProcescMagic(17600, user.Player.UID, user.Player.UID, Target.X, Target.Y)", info.SourceEvidence);
        Assert.Contains("ProcescMagic(17610, user.Player.UID, user.Player.UID, Target.X, Target.Y)", info.SourceEvidence);
        Assert.Contains("CombatGearAttack.cs cases 17600 and 17610", info.SourceEvidence);
        Assert.Contains("case 17620", info.SourceEvidence);
        Assert.Contains("Immersion as 0x12F", info.SourceEvidence);
        Assert.Contains("Insouciance as 0x130", info.SourceEvidence);
        Assert.Contains("DragonPierceCrit as 0x133", info.SourceEvidence);
        Assert.Contains("DragonPierceTortoise as 0x134", info.SourceEvidence);
        Assert.Contains("DragonPierceBreak as 0x135", info.SourceEvidence);
        Assert.Contains("DragonPierceStamina as 0x136", info.SourceEvidence);
        Assert.Contains("MyCombatGear.cs lines after Bloodlust", info.SourceEvidence);
        Assert.Contains("Taoist3 through ProcescMagic(20380, user.Player.UID, Target.UID, Target.X, Target.Y)", info.SourceEvidence);
        Assert.Contains("Taoist1 through ProcescMagic(20330, user.Player.UID, Target.UID, Target.X, Target.Y)", info.SourceEvidence);
        Assert.Contains("CombatGearAttack.cs case 20380", info.SourceEvidence);
        Assert.Contains("defender CrackMantra branch", info.SourceEvidence);
        Assert.Contains("TopPirate|HawksAmbition", info.SourceEvidence);
        Assert.Contains("Freeze|Top4Weekly|HawksAmbition", info.SourceEvidence);
        Assert.Contains("5-second ArchiveSkill", info.SourceEvidence);
        Assert.Contains("self animation targeting the defender", info.SourceEvidence);
        Assert.Contains("CombatGearAttack.cs case 20330", info.SourceEvidence);
        Assert.Contains("attacker DeadwoodCurse branch", info.SourceEvidence);
        Assert.Contains("DefensiveStance|HawksAmbition", info.SourceEvidence);
        Assert.Contains("duration ArchiveSkill", info.SourceEvidence);
        Assert.Contains("then Pirate1/Pirate2/Pirate3 through ProcescMagic(21710), ProcescMagic(21720), and ProcescMagic(21930)", info.SourceEvidence);
        Assert.Contains("magictype.txt rows 21710/21720/21930", info.SourceEvidence);
        Assert.Contains("Armed status 413", info.SourceEvidence);
        Assert.Contains("Sense status 412", info.SourceEvidence);
        Assert.Contains("Dark target buff removal", info.SourceEvidence);
        Assert.Contains("CombatGearAttack.cs cases 21710 and 21720", info.SourceEvidence);
        Assert.Contains("case 21930 sends the interaction/animation for all calls", info.SourceEvidence);
        Assert.Contains("for SOB player-view misses sends a targetless animation", info.SourceEvidence);
        Assert.Contains("bounded receive-side CombatGear Pirate 21710/21720/21930 status/buff-strip self-animation or target-animation branches", info.SourceEvidence);
        Assert.Contains("TopPirate as 0x7A", info.SourceEvidence);
        Assert.Contains("Freeze as 0x3B", info.SourceEvidence);
        Assert.Contains("DefensiveStance as 0x7E", info.SourceEvidence);
        Assert.Contains("HawksAmbition as 0x100", info.SourceEvidence);
        Assert.Contains("bounded receive-side CombatGear Taoist 20330/20380 status/power/self-animation branches", info.SourceEvidence);
        Assert.Contains("bounded receive-side CombatGear Pirate black-jade random UseRune status branches", info.SourceEvidence);
        Assert.Contains(pirateBlackJadeUseRuneFamilySpellIds, info.SourceEvidence);
        Assert.DoesNotContain("remaining attacker-side MyCombatGear.Update branches", info.SourceEvidence);
        Assert.DoesNotContain("unsupported individual UseRune spell execution paths", info.SourceEvidence);
        Assert.Contains("Dragonhowl 17620", info.SourceEvidence);
        Assert.Contains("Bloodlust 17600/17610", info.SourceEvidence);
        Assert.Contains("Taoist 20330/20380", info.SourceEvidence);
        Assert.Contains("Pirate 21710/21720/21930", info.SourceEvidence);
        Assert.Contains(
            "including recursive 10950 player-hit CombatGear.Update and defender UpdateTarget ordering",
            info.SourceEvidence);
        Assert.DoesNotContain("remaining 10950 defender UpdateTarget recursion", info.SourceEvidence);
        Assert.DoesNotContain("remaining 10950 recursive ReceiveAttack side effects", info.SourceEvidence);
        Assert.Contains("defender UpdateTarget MagneticLight 20220 and AblazeBlade 20360 player branches", info.SourceEvidence);
        Assert.DoesNotContain("attacker Venom speffect_attribute.Get type 2", info.SourceEvidence);
        Assert.Contains("ReceiveAttack.Player.Execute lines 118-224 activate/drain LightningShield/AzureShield", info.SourceEvidence);
        Assert.Contains("deeper rune/perfection counters", info.SourceEvidence);
        Assert.DoesNotContain("deeper rune/perfection/tournament counters", info.SourceEvidence);
        Assert.Contains("PK/tournament bookkeeping", info.SourceEvidence);
        Assert.Contains("event/SOB branches", info.SourceEvidence);
        Assert.Contains("exact CanAttack side effects", info.SourceEvidence);
        Assert.Contains("KineticSpark.cs lines 218-253", info.SourceEvidence);
        Assert.Contains("primary visible SobNpc", info.SourceEvidence);
        Assert.Contains("CanAttackNpc.Verified", info.SourceEvidence);
        Assert.Contains("Range.OnNpcs", info.SourceEvidence);
        Assert.Contains("Magic.OnNpcs", info.SourceEvidence);
        Assert.Contains("Npc.Execute", info.SourceEvidence);
        Assert.Contains("ReceiveAttack.Npc.Execute lines 22-34", info.SourceEvidence);
        Assert.Contains("lines 33-34 set client.Player.ThundercloudSight = attacked.UID and client.Player.AttackHit = true", info.SourceEvidence);
        Assert.Contains("applies HookMoonCaster, CleanSweep, AxeShadow, and CelestialDance damage modifiers before HP mutation", info.SourceEvidence);
        Assert.Contains("models the bounded SOB ThundercloudSight and AttackHit side effects", info.SourceEvidence);
        Assert.Contains(
            "bounded SOB receive-side CombatGear StoneCracker ArrowBlades/StarFlow 10950 counter/status branch",
            info.SourceEvidence);
        Assert.Contains("bounded SOB receive-side CombatGear Dragonhowl 17620", info.SourceEvidence);
        Assert.Contains("Bloodlust 17600/17610", info.SourceEvidence);
        Assert.Contains("Taoist DeadwoodCurse 20330", info.SourceEvidence);
        Assert.Contains("Taoist3 CrackMantra 20380 SOB player-view-miss no-op", info.SourceEvidence);
        Assert.Contains("player-target-only defender UpdateTarget skip for SOB hits", info.SourceEvidence);
        Assert.Contains("Pirate Armed/Sense 21710/21720", info.SourceEvidence);
        Assert.Contains("Pirate Dark 21930 SOB no-op interaction/targetless-animation", info.SourceEvidence);
        Assert.Contains("Pirate black-jade random UseRune status branches", info.SourceEvidence);
        Assert.Contains(
            "ReceiveAttack.Player.Execute lines 23-31 use gouyu_immortal Chance / 100 for Ninja SageMode",
            info.SourceEvidence);
        Assert.Contains(
            "ReceiveAttack.Npc.cs lines 23-31 apply the Ninja SageMode branch using gouyu_immortals Chance / 100",
            info.SourceEvidence);
        Assert.Contains(
            "ReceiveAttack.Monster.Execute applies the Ninja SageMode branch using gouyu_immortals Rate / 100",
            info.SourceEvidence);
        Assert.DoesNotContain(
            "current ReceiveAttack.Npc.cs lines 213-224 apply the Ninja SageMode branch using gouyu_immortals Rate / 100",
            info.SourceEvidence);
        Assert.Contains("bounded KineticSpark player receive InfiniteMist ArchiveSkill emit-order", info.SourceEvidence);
        Assert.Contains("bounded player receive-side Ninja SageMode branch using gouyu_immortal Chance", info.SourceEvidence);
        Assert.Contains(
            "Physical.cs lines 979-986 check target MudWall flag 0x112 after BackDmg",
            info.SourceEvidence);
        Assert.Contains("damage -= damage * (DBItem.Power / 100) / 100", info.SourceEvidence);
        Assert.Contains("Ninja.TryGetValueEquip enforces Ninja class", info.SourceEvidence);
        Assert.Contains("active primary/alternate equipment set", info.SourceEvidence);
        Assert.Contains("explicit legacy level/power attributes as compatibility fallbacks", info.SourceEvidence);
        Assert.Contains(
            "exact primary/chained KineticSpark caller order remain unaudited rather than matching-source-complete",
            info.SourceEvidence);
        Assert.Contains(
            "No learned 16440 chance, Damage2, or LegacyMudWallRoll reducer exists in the matching DecompiledSource",
            info.SourceEvidence);
        Assert.Contains("matching service-pack source is gameplay authority", info.SourceEvidence);
        Assert.Contains("exact D13 client is wire authority", info.SourceEvidence);
        Assert.Contains("Source7770 contributes nothing", info.SourceEvidence);
        Assert.DoesNotContain("ReceiveAttack.Player.Execute lines 1108-1141", info.SourceEvidence);
        Assert.DoesNotContain("ReceiveAttack.Player.Execute lines 2146-2156", info.SourceEvidence);
        Assert.DoesNotContain("Rate starts at DBSpell.DamageOnHuman / 100", info.SourceEvidence);
        Assert.DoesNotContain("passing Rate roll", info.SourceEvidence);
        Assert.DoesNotContain("source chance-then-direct reduction order", info.SourceEvidence);
        Assert.Contains("bounded monster receive-side Ninja SageMode branch using gouyu_immortal Rate", info.SourceEvidence);
        Assert.Contains("bounded SOB receive-side Ninja SageMode branch using gouyu_immortal Chance", info.SourceEvidence);
        Assert.Contains("bounded HookMoonCaster, CleanSweep, AxeShadow, and CelestialDance SOB receive damage modifiers", info.SourceEvidence);
        Assert.Contains("shared generic player-SOB magic HP-mutation path", info.SourceEvidence);
        Assert.DoesNotContain("remaining SOB CombatGear Update branches", info.SourceEvidence);
        Assert.DoesNotContain("player-target-only CrackMantra/UpdateTarget branches", info.SourceEvidence);
        Assert.DoesNotContain("remaining SOB CombatGear Update branches (StoneCracker", info.SourceEvidence);
        Assert.DoesNotContain("player-target-only CrackMantra/Pirate Dark/UpdateTarget branches", info.SourceEvidence);
        Assert.DoesNotContain("remaining SOB CombatGear Update branches (player-target-only CrackMantra/Pirate Dark/UpdateTarget branches and Pirate black-jade UseRune)", info.SourceEvidence);
        Assert.DoesNotContain("leaving SOB CombatGear Update/UpdateTarget", info.SourceEvidence);
        Assert.Contains("event/SOB branches", info.SourceEvidence);
        Assert.Contains("remaining Ninja/Sage branches outside the bounded player/SOB/monster SageMode paths", info.SourceEvidence);
        Assert.Contains("ReceiveAttack.Player.Execute lines 263-408 apply attacker Caprice", info.SourceEvidence);
        Assert.Contains("ReceiveAttack.Player.Execute lines 430-465 apply attacker Slow Pace", info.SourceEvidence);
        Assert.Contains("ReceiveAttack.Player.Execute lines 470-554 apply XPBooster, XPKiller, TidalWave, and Quench", info.SourceEvidence);
        Assert.Contains("ReceiveAttack.Player.Execute lines 557-576 apply NeptuneCurse receive damage overrides", info.SourceEvidence);
        Assert.Contains("set client.Player.AttackHit = true", info.SourceEvidence);
        Assert.Contains("target EagleEyeCountDown learned spell 11130 plus Magic row rate roll", info.SourceEvidence);
        Assert.Contains("ReceiveAttack.Player.Execute lines 577-597 apply SkillTournament kill-system/death-check handling", info.SourceEvidence);
        Assert.Contains("decrement target SkillTournamentLifes with a center message and return while lives remain", info.SourceEvidence);
        Assert.Contains("increment attacker TournamentKills, send accession1, set target HP 0, and call Dead", info.SourceEvidence);
        Assert.Contains("ReceiveAttack.Player.Execute lines 598-611 apply FreezeWar garment checks", info.SourceEvidence);
        Assert.Contains("Freeze flag 0x3B before returning", info.SourceEvidence);
        Assert.Contains("ReceiveAttack.Player.Execute lines 613-624 apply FootBall pass-the-ball handling", info.SourceEvidence);
        Assert.Contains("ReceiveAttack.Player.Execute lines 625-640 apply attacker PerfectionStatus.MirrorOfSin", info.SourceEvidence);
        Assert.Contains("MsgRefineEffect opcode 2319", info.SourceEvidence);
        Assert.Contains("RefineEffects.MirrorOfSin 0x0A", info.SourceEvidence);
        Assert.Contains("XPList flag 0x04 for 20 seconds", info.SourceEvidence);
        Assert.Contains("ReceiveAttack.Player.Execute lines 641-656 apply attacker PerfectionStatus.LightOfStamina", info.SourceEvidence);
        Assert.Contains("RefineEffects.LightOfStamina 7", info.SourceEvidence);
        Assert.Contains("MsgUpdate.DataType.Stamina 8", info.SourceEvidence);
        Assert.Contains("ReceiveAttack.Player.Execute lines 657-668 apply attacker PerfectionStatus.CalmWind", info.SourceEvidence);
        Assert.Contains("RefineEffects.CalmWind 4", info.SourceEvidence);
        Assert.Contains("ReceiveAttack.Player.Execute lines 668-684 apply attacker PerfectionStatus.DivineGuard", info.SourceEvidence);
        Assert.Contains("RefineEffects.DivineGuard 0x0B", info.SourceEvidence);
        Assert.Contains("MsgUpdate.Flags.DivineGuard 0xB1", info.SourceEvidence);
        Assert.Contains("MsgUpdate.DataType.AzureShield 0x31", info.SourceEvidence);
        Assert.Contains("ReceiveAttack.Player.Execute lines 684-699 apply attacker PerfectionStatus.KillingFlash", info.SourceEvidence);
        Assert.Contains("RefineEffects.KillingFlash 9", info.SourceEvidence);
        Assert.Contains("XPList flag 0x04 for 20 seconds", info.SourceEvidence);
        Assert.Contains("MsgUpdate.DataType.XPList 0x65 value 1", info.SourceEvidence);
        Assert.Contains("ReceiveAttack.Player.Execute lines 700-727 apply attacker PerfectionStatus.DrainingTouch", info.SourceEvidence);
        Assert.Contains("RefineEffects.DrainingTouch 5", info.SourceEvidence);
        Assert.Contains("MsgUpdate.DataType.Mana 2", info.SourceEvidence);
        Assert.Contains("ReceiveAttack.Player.Execute lines 727-753 apply defender PerfectionStatus.BloodSpawn", info.SourceEvidence);
        Assert.Contains("RefineEffects.BloodSpawn 6", info.SourceEvidence);
        Assert.Contains("restore defender HitPoints to Owner.Status.MaxHitpoints and Mana to Owner.Status.MaxMana", info.SourceEvidence);
        Assert.Contains("continue normal damage without returning", info.SourceEvidence);
        Assert.Contains("lianhuaran04 flag 0xAB transferring to the attacker for 2592000 seconds", info.SourceEvidence);
        Assert.Contains("bounded HookMoonCaster/CleanSweep/AxeShadow/HawksAmbition/AttackUp/LightningShield/Duel/Absolution/BloodTide/Caprice/ReverseMagic/SlowPace/XPBooster/XPKiller/TidalWave/Quench/NeptuneCurse/AttackHit/EagleEye/SkillTournament/FreezeWar/Football/DragonWar/MirrorOfSin/LightOfStamina/CalmWind/DivineGuard/KillingFlash/DrainingTouch/BloodSpawn player receive modifiers", info.SourceEvidence);
        Assert.Contains("DragonWar playtest/economy polish", info.SourceEvidence);
        Assert.DoesNotContain("NeptuneCurse/EagleEye and later ReceiveAttack.Player branches", info.SourceEvidence);
        Assert.DoesNotContain("SkillTournament/FreezeWar and later ReceiveAttack.Player branches", info.SourceEvidence);
        Assert.Contains("bounded 15940 Wildwind / 7030 Bomb target-damage loops", info.SourceEvidence);
        Assert.DoesNotContain("15940/7030 target-damage loops", info.SourceEvidence);
        Assert.DoesNotContain("AxeShadow/CelestialDance status setter spell paths", info.SourceEvidence);
        Assert.Contains("PK/tournament bookkeeping as shared runtime debt", info.SourceEvidence);
        Assert.Contains("MsgAttackPacket.cs lines 356-400, 436-480, and 518-562", info.SourceEvidence);
        Assert.Contains("AttackID.Archer player, monster, and SOB caller branches", info.SourceEvidence);
        Assert.Contains("source direct-target Base.GetDistance <= 18 caller gate", info.SourceEvidence);
        Assert.Contains("direct-target CanAttackMonster/CanAttackPlayer/CanAttackNpc gates", info.SourceEvidence);
        Assert.Contains("MyCombatGear.CheckCombatGear", info.SourceEvidence);
        Assert.Contains("Base.Rate(30)", info.SourceEvidence);
        Assert.Contains("Pool.Magic[11590]", info.SourceEvidence);
        Assert.DoesNotContain("model or verify the KineticSpark.AttackSpell player", info.SuggestedNextOwner);
        Assert.DoesNotContain("model or verify the KineticSpark.AttackSpell SOB", info.SuggestedNextOwner);
        Assert.Contains("exact KineticSpark AttackSpell side-effect parity", info.SuggestedNextOwner);
        Assert.DoesNotContain("source double-call shape", info.SuggestedNextOwner);
        Assert.DoesNotContain("remaining attacker-side MyCombatGear.Update branches", info.SuggestedNextOwner);
        Assert.DoesNotContain("remaining SOB CombatGear Update branches", info.SuggestedNextOwner);
        Assert.DoesNotContain("player-target-only CrackMantra/UpdateTarget branches", info.SuggestedNextOwner);
        Assert.DoesNotContain("player-target-only CrackMantra/Pirate Dark/UpdateTarget branches", info.SuggestedNextOwner);
        Assert.DoesNotContain("remaining SOB CombatGear Update branches (player-target-only CrackMantra/Pirate Dark/UpdateTarget branches and Pirate black-jade UseRune)", info.SuggestedNextOwner);
        Assert.DoesNotContain("unsupported individual UseRune spell execution paths", info.SuggestedNextOwner);
        Assert.Contains("Taoist 20330/20380", info.SuggestedNextOwner);
        Assert.Contains("Pirate 21710/21720/21930", info.SuggestedNextOwner);
        Assert.DoesNotContain("remaining 10950 defender UpdateTarget recursion", info.SuggestedNextOwner);
        Assert.DoesNotContain(
            "remaining 10950 recursive attacker CombatGear counter/update loops",
            info.SuggestedNextOwner);
        Assert.Contains("StarFlow 10950 post-receive player animation damage/update ordering", info.SuggestedNextOwner);
        Assert.Contains("defender UpdateTarget MagneticLight 20220 and AblazeBlade 20360 player branches", info.SuggestedNextOwner);
        Assert.DoesNotContain("attacker Venom speffect_attribute.Get type 2", info.SuggestedNextOwner);
        Assert.Contains("remaining Ninja/Sage branches outside the bounded player/SOB/monster SageMode paths", info.SuggestedNextOwner);
        Assert.Contains("DragonWar playtest/economy polish", info.SuggestedNextOwner);
        Assert.DoesNotContain("NeptuneCurse/EagleEye and later ReceiveAttack.Player branches", info.SuggestedNextOwner);
        Assert.DoesNotContain("SkillTournament/FreezeWar and later ReceiveAttack.Player branches", info.SuggestedNextOwner);
        Assert.DoesNotContain("15940/7030 target-damage loops", info.SuggestedNextOwner);
        Assert.DoesNotContain("AxeShadow/CelestialDance status setter spell paths", info.SuggestedNextOwner);
        Assert.Contains("deeper rune/perfection counters", info.SuggestedNextOwner);
        Assert.DoesNotContain("deeper rune/perfection/tournament counters", info.SuggestedNextOwner);
        Assert.Contains("PK/tournament bookkeeping", info.SuggestedNextOwner);
        Assert.Contains("event/SOB branches", info.SuggestedNextOwner);
        Assert.DoesNotContain("remaining combat gear caller details around the pre-proc/Kinetic sequence", info.SuggestedNextOwner);
        Assert.Contains("CombatGear caller consumption", info.SuggestedNextOwner);
        Assert.Contains(
            "bounded SOB receive-side CombatGear StoneCracker ArrowBlades/StarFlow 10950 counter/status branch",
            info.SuggestedNextOwner);
        Assert.Contains("bounded SOB receive-side CombatGear Dragonhowl/Bloodlust, Taoist DeadwoodCurse, Taoist3 CrackMantra SOB no-op, defender UpdateTarget player-only skip, Pirate Armed/Sense, Pirate Dark SOB no-op, Pirate black-jade UseRune, and Ninja SageMode branches", info.SuggestedNextOwner);
        Assert.Contains("bounded KineticSpark player receive InfiniteMist ArchiveSkill emit-order", info.SuggestedNextOwner);
        Assert.Contains("bounded player receive-side Ninja SageMode branch using gouyu_immortal Chance", info.SuggestedNextOwner);
        Assert.Contains(
            "auditing the retained direct MudSigilImpeccable compatibility reduction",
            info.SuggestedNextOwner);
        Assert.Contains(
            "primary/chained player-caller order against matching Physical.cs lines 979-986",
            info.SuggestedNextOwner);
        Assert.DoesNotContain(
            "preserving the modeled bounded player MudWall/MudSigilImpeccable receive damage reductions",
            info.SuggestedNextOwner);
        Assert.Contains("bounded SOB receive-side Ninja SageMode branch using gouyu_immortal Chance", info.SuggestedNextOwner);
        Assert.Contains("bounded monster receive-side Ninja SageMode branch using gouyu_immortal Rate", info.SuggestedNextOwner);
        Assert.Contains("bounded HookMoonCaster/CleanSweep/AxeShadow/CelestialDance SOB receive damage modifiers", info.SuggestedNextOwner);
        Assert.Contains("bounded HookMoonCaster/CleanSweep/AxeShadow/HawksAmbition/AttackUp/LightningShield/Duel/Absolution/BloodTide/Caprice/ReverseMagic/SlowPace/XPBooster/XPKiller/TidalWave/Quench/NeptuneCurse/AttackHit/EagleEye/SkillTournament/FreezeWar/Football/DragonWar/MirrorOfSin/LightOfStamina/CalmWind/DivineGuard/KillingFlash/DrainingTouch/BloodSpawn player receive modifiers", info.SuggestedNextOwner);
        Assert.Contains("bounded AxeShadow/CelestialDance self-status setter/expiry and target-damage path", info.SuggestedNextOwner);
        Assert.Contains("bounded player-hit defender CombatGear UpdateTarget self/status passives", info.SuggestedNextOwner);
        Assert.Contains("bounded defender speffect_attribute.Get Solid/Bloodthirst/Elan self-effects", info.SuggestedNextOwner);
        Assert.Contains("bounded attacker Venom speffect type 2 bridge through modeled AttackStatus spell 20000", info.SuggestedNextOwner);
        Assert.Contains("bounded attacker-side MyCombatGear.Update StoneCracker ArrowBlades/StarFlow 10950 counter/status/mixed-target HP branch", info.SuggestedNextOwner);
        Assert.Contains("bounded attacker-side MyCombatGear.Update StoneCracker ArchShadow 18980 branch", info.SuggestedNextOwner);
        Assert.Contains("bounded receive-side CombatGear Dragonhowl/Bloodlust status/power/self-animation branches", info.SuggestedNextOwner);
        Assert.Contains("bounded receive-side CombatGear Taoist 20330/20380 status/power/self-animation branches", info.SuggestedNextOwner);
        Assert.Contains("bounded receive-side CombatGear Pirate 21710/21720/21930 status/buff-strip self-animation or target-animation branches", info.SuggestedNextOwner);
        Assert.Contains("bounded receive-side CombatGear Pirate black-jade random UseRune status branches", info.SuggestedNextOwner);
        Assert.Contains("bounded ThundercloudSight and AttackHit side effects", info.SuggestedNextOwner);
        Assert.Contains("exact CanAttack side effects", info.SuggestedNextOwner);
        Assert.Contains("Archer caller-path monster/player/SOB passive procs", info.SuggestedNextOwner);
        AssertContainsCanAttackPlayerRelationGateReview(info.SuggestedNextOwner);
        AssertContainsRemainingCanAttackNpcGaps(info.SuggestedNextOwner);
        Assert.Contains(
            "magicSort=KineticSpark(67) spellId=11590 capability=specialCombatDamage status=partiallyModeled supported=true nextSeam=kineticspark-attackspell-caller-side-effect-parity",
            info.Diagnostic);
        Assert.Contains("monster passive proc", info.Diagnostic);
        Assert.Contains("player passive proc", info.Diagnostic);
        Assert.Contains("SOB passive proc", info.Diagnostic);
        Assert.Contains("bounded mutual ThundercloudSight side effects", info.Diagnostic);
        Assert.Contains("bounded player-hit defender CombatGear UpdateTarget self/status passives", info.Diagnostic);
        Assert.Contains("bounded defender speffect_attribute.Get Solid/Bloodthirst/Elan self-effects", info.Diagnostic);
        Assert.Contains("bounded attacker Venom speffect type 2 bridge through modeled AttackStatus spell 20000", info.Diagnostic);
        Assert.Contains("bounded caster ThundercloudSight and AttackHit side effects", info.Diagnostic);
        Assert.Contains("event/SOB branches", info.Diagnostic);
    }

    [Fact]
    public void ClassifyCrescentShadowBombDocumentsArcherPreKineticProcDebt()
    {
        var info = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.Bomb, spellId: 18_750);

        Assert.Equal(LegacyMagicSortCapability.VisibleMonsterDamage, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("Bomb(5)", info.SortName);
        Assert.Equal("archer-pre-kineticspark-proc-runtime", info.NextSeam);
        Assert.Contains("direct sort-5/Bomb visible-monster damage slice", info.Reason);
        Assert.Contains("CrescentShadow 18750", info.Reason);
        Assert.Contains("bounded source-backed Archer AttackID passive proc", info.Reason);
        Assert.Contains("source direct-target Base.GetDistance <= 18 caller gate", info.Reason);
        Assert.Contains("MyCombatGear.CheckCombatGear consumption before the passive hook", info.Reason);
        Assert.Contains("learned 18750 plus Base.Rate(30)", info.Reason);
        Assert.Contains("distance <= 2", info.Reason);
        Assert.Contains("player/monster/SOB magic routing", info.Reason);
        Assert.Contains("ordering before the later KineticSpark gate sequence", info.Reason);
        Assert.Contains("Remaining CrescentShadow parity is exact passive caller side effects", info.Reason);
        Assert.DoesNotContain("exact combat gear and CanAttack edge side effects", info.Reason);
        Assert.Contains("exact CanAttack edge side effects", info.Reason);
        Assert.Contains("broader Bomb spell/runtime edges", info.Reason);
        Assert.Contains("magictype.txt lines 3166-3172", info.SourceEvidence);
        Assert.Contains("spell 18750 CrescentShadow as sort 5/Bomb", info.SourceEvidence);
        Assert.Contains("AttackID.Archer lines around 356-388, 436-468, and 518-550", info.SourceEvidence);
        Assert.Contains("source direct-target Base.GetDistance <= 18 caller gate", info.SourceEvidence);
        Assert.Contains("MyCombatGear.CheckCombatGear", info.SourceEvidence);
        Assert.Contains("learned-spell plus Base.Rate(30) hooks", info.SourceEvidence);
        Assert.Contains("before the later KineticSpark checks", info.SourceEvidence);
        Assert.Contains("exact Archer CrescentShadow 18750 passive caller side effects", info.SuggestedNextOwner);
        Assert.DoesNotContain("exact combat gear/CanAttack edges", info.SuggestedNextOwner);
        Assert.Contains("exact CanAttack edges", info.SuggestedNextOwner);
        Assert.Contains("broader Bomb spell/runtime edges", info.SuggestedNextOwner);
        Assert.Contains(
            "magicSort=Bomb(5) spellId=18750 capability=visibleMonsterDamage status=partiallyModeled supported=true nextSeam=archer-pre-kineticspark-proc-runtime",
            info.Diagnostic);
    }

    [Fact]
    public void ClassifyNebulousHuntSectorBackDocumentsModeledRuntimeAndArcherPreKineticProcDebt()
    {
        var info = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)69, spellId: 10_990);

        Assert.Equal(LegacyMagicSortCapability.SpecialCombatDamage, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("SectorBack(69)", info.SortName);
        Assert.Equal("archer-pre-kineticspark-proc-runtime", info.NextSeam);
        Assert.Contains("bounded NebulousHunt 10990 sort-69/SectorBack runtime", info.Reason);
        Assert.Contains("source-shaped line width 2", info.Reason);
        Assert.Contains("monster/SOB animation damage doubled", info.Reason);
        Assert.Contains("player Physical.OnPlayer damage with player-target WildPhoenix defender charge/archive override before the NebulousHunt Base.CalculateSoul and /7 scaling step", info.Reason);
        Assert.Contains("player damage divided by 7", info.Reason);
        Assert.Contains("source-shaped zero-damage player target preservation", info.Reason);
        Assert.Contains("NebulousHunt 10990", info.Reason);
        Assert.Contains("sort-69/SectorBack", info.Reason);
        Assert.Contains("must remain ordered before KineticSpark", info.Reason);
        Assert.Contains("source direct-target Base.GetDistance <= 18 caller gate", info.Reason);
        Assert.Contains("MyCombatGear.CheckCombatGear", info.SourceEvidence);
        Assert.Contains("learned 10990 spell", info.Reason);
        Assert.Contains("Base.Rate(30)", info.Reason);
        Assert.Contains("before the later KineticSpark gate sequence", info.Reason);
        Assert.Contains("Remaining parity is exact passive caller side effects", info.Reason);
        Assert.Contains("exact CanAttack side-effect edges", info.Reason);
        Assert.Contains("exact Archer/Kinetic/rune/event gaps", info.Reason);
        Assert.Contains("adjacent SectorBack cases outside the separately bounded 18240 and 15870 slices", info.Reason);
        Assert.DoesNotContain("broader SectorBack spell IDs such as 18240", info.Reason);
        Assert.Contains("This claim is only for spell ID 10990", info.Reason);
        Assert.Contains("magictype.txt line 1055", info.SourceEvidence);
        Assert.Contains("spell 10990 NebulousHunt as sort 69/SectorBack", info.SourceEvidence);
        Assert.Contains("SectorBack.cs case 10990/18240", info.SourceEvidence);
        Assert.Contains("calls Physical.OnPlayer, then Base.CalculateSoul, then divides player damage by 7 before ReceiveAttack.Player.Execute", info.SourceEvidence);
        Assert.Contains("Physical.cs applies target.Owner.CombatGear.WildPhoenix()", info.SourceEvidence);
        Assert.Contains("AttackID.Archer lines around 356-388, 436-468, and 518-550", info.SourceEvidence);
        Assert.Contains("source direct-target Base.GetDistance <= 18 caller gate", info.SourceEvidence);
        Assert.Contains("learned-spell plus Base.Rate(30) hooks", info.SourceEvidence);
        Assert.Contains("before the later KineticSpark checks", info.SourceEvidence);
        Assert.Contains("passive caller side effects stay before KineticSpark", info.SuggestedNextOwner);
        Assert.Contains("Archer/Kinetic/rune/event gaps", info.SuggestedNextOwner);
        Assert.Contains("keep SectorBack 18240 on its own bounded runtime record", info.SuggestedNextOwner);
        Assert.DoesNotContain("broader SectorBack spell IDs such as 18240 separate", info.SuggestedNextOwner);
        Assert.Contains(
            "magicSort=SectorBack(69) spellId=10990 capability=specialCombatDamage status=partiallyModeled supported=true nextSeam=archer-pre-kineticspark-proc-runtime",
            info.Diagnostic);
    }

    [Fact]
    public void ClassifySectorBack18240DocumentsModeledRuntimeWithoutFullSectorBackParity()
    {
        var info = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)69, spellId: 18_240);

        Assert.Equal(LegacyMagicSortCapability.SpecialCombatDamage, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("SectorBack(69)", info.SortName);
        Assert.Equal("sectorback-18240-runtime", info.NextSeam);
        Assert.Contains("bounded SectorBack 18240 runtime", info.Reason);
        Assert.Contains("source-shaped line width 2", info.Reason);
        Assert.Contains("monster/SOB animation damage doubled after runtime damage", info.Reason);
        Assert.Contains("player Physical.OnPlayer damage with player-target WildPhoenix defender charge/archive override before the SectorBack 18240 Base.CalculateSoul and /7 scaling step", info.Reason);
        Assert.Contains("player damage divided by 7", info.Reason);
        Assert.Contains("source-shaped zero-damage player target preservation", info.Reason);
        Assert.Contains("Remaining SectorBack 18240 parity is exact CanAttack side-effect edges", info.Reason);
        AssertContainsCanAttackPlayerRelationGateReview(info.Reason);
        AssertContainsRemainingCanAttackNpcGaps(info.Reason);
        Assert.Contains("This claim is only for spell ID 18240", info.Reason);
        Assert.Contains("magictype level rows 18241-18246 are source data", info.Reason);
        Assert.Contains("not separate runtime ID claims", info.Reason);
        Assert.Contains("CleanSweep rows 15871-15879/15993 stay unimplemented", info.Reason);
        Assert.Contains("adjacent SectorBack cases stay separate", info.Reason);
        Assert.Contains("SectorBack.cs case 10990/18240", info.SourceEvidence);
        Assert.Contains("case 10990/18240 shares the line-width-2 branch", info.SourceEvidence);
        Assert.Contains("calls Physical.OnPlayer, then Base.CalculateSoul, then divides player damage by 7 before ReceiveAttack.Player.Execute", info.SourceEvidence);
        Assert.Contains("magictype.txt rows 18240-18246 define type 18240 as sort 69/SectorBack", info.SourceEvidence);
        Assert.Contains("Physical.cs applies target.Owner.CombatGear.WildPhoenix()", info.SourceEvidence);
        Assert.Contains("tighten exact SectorBack 18240 CanAttack side effects", info.SuggestedNextOwner);
        AssertContainsCanAttackPlayerRelationGateReview(info.SuggestedNextOwner);
        AssertContainsRemainingCanAttackNpcGaps(info.SuggestedNextOwner);
        Assert.Contains("keep magictype level rows 18241-18246, CleanSweep rows 15871-15879/15993, and adjacent SectorBack cases separate", info.SuggestedNextOwner);
        Assert.Contains(
            "magicSort=SectorBack(69) spellId=18240 capability=specialCombatDamage status=partiallyModeled supported=true nextSeam=sectorback-18240-runtime",
            info.Diagnostic);

        var adjacentLevelRow = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)69, spellId: 18_241);

        Assert.Equal(LegacyMagicSortCapability.SpecialCombatDamage, adjacentLevelRow.Capability);
        Assert.Equal(LegacyMagicModelingStatus.MissingRuntimeNotModeled, adjacentLevelRow.Status);
        Assert.False(adjacentLevelRow.Supported);
        Assert.Equal("damage-on-monster-special-sort-runtime", adjacentLevelRow.NextSeam);
        Assert.DoesNotContain("bounded SectorBack 18240", adjacentLevelRow.Reason);
    }

    [Fact]
    public void ClassifyCleanSweepSectorBackDocumentsModeledRuntimeAndPowerState()
    {
        var info = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)69, spellId: 15_870);

        Assert.Equal(LegacyMagicSortCapability.SpecialCombatDamage, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("SectorBack(69)", info.SortName);
        Assert.Equal("cleansweep-sectorback-runtime", info.NextSeam);
        Assert.Contains("bounded CleanSweep 15870 sort-69/SectorBack runtime", info.Reason);
        Assert.Contains("line-width-2 mixed monster/player/SOB targets", info.Reason);
        Assert.Contains("source damage scaling (monster /2, player *5/12, SOB runtime full but animation *50/100)", info.Reason);
        Assert.Contains("player WildPhoenix defender hook before CleanSweep scaling", info.Reason);
        Assert.Contains("source-shaped zero-damage WildPhoenix player animation target with Hit=false", info.Reason);
        Assert.Contains("self CleanSweep flag/archive update using flag 254", info.Reason);
        Assert.Contains("Time=Duration, Damage=Damage3, SpellLevel=Damage3", info.Reason);
        Assert.Contains("CleanSweep power/expiry state", info.Reason);
        Assert.Contains("Remaining CleanSweep parity is exact CanAttack side-effect edges", info.Reason);
        AssertContainsCanAttackPlayerRelationGateReview(info.Reason);
        AssertContainsRemainingCanAttackNpcGaps(info.Reason);
        Assert.Contains("This claim is bounded to spell ID 15870", info.Reason);
        Assert.Contains("magictype level rows 15871-15879 and 15993 are source data", info.Reason);
        Assert.Contains("not separate runtime ID claims", info.Reason);
        Assert.Contains("adjacent SectorBack cases stay separate", info.Reason);
        Assert.Contains("SectorBack.cs case 15870", info.SourceEvidence);
        Assert.Contains("line width 2", info.SourceEvidence);
        Assert.Contains("scales monster Physical.OnMonster plus Base.CalculateSoul damage by /2", info.SourceEvidence);
        Assert.Contains("scales player Physical.OnPlayer plus Base.CalculateSoul damage by *5/12", info.SourceEvidence);
        Assert.Contains("sets Hit=0 for animation", info.SourceEvidence);
        Assert.Contains("full runtime damage through Npc.Execute then changes animation damage to *50/100", info.SourceEvidence);
        Assert.Contains("CleanSweepPower = Spell.Damage3", info.SourceEvidence);
        Assert.Contains("Time=Spell.Duration, Damage=Spell.Damage3, and SpellLevel=Spell.Damage3", info.SourceEvidence);
        Assert.Contains("AddSpellFlag(CleanSweep, Spell.Duration, RemoveOnDead)", info.SourceEvidence);
        Assert.Contains("magictype.txt rows 15870-15879 and 15993 define CleanSweep as sort 69/SectorBack", info.SourceEvidence);
        Assert.Contains("flag 254", info.SourceEvidence);
        Assert.Contains("duration 35", info.SourceEvidence);
        Assert.Contains("range 5", info.SourceEvidence);
        Assert.Contains("Damage3 power rows", info.SourceEvidence);
        Assert.Contains("MsgUpdate.cs defines CleanSweep = 0xFE", info.SourceEvidence);
        Assert.Contains("Physical.cs applies target.Owner.CombatGear.WildPhoenix()", info.SourceEvidence);
        Assert.Contains("tighten exact CleanSweep 15870 CanAttack side effects", info.SuggestedNextOwner);
        Assert.Contains("keep magictype level rows 15871-15879/15993 and adjacent SectorBack cases separate", info.SuggestedNextOwner);
        Assert.Contains(
            "magicSort=SectorBack(69) spellId=15870 capability=specialCombatDamage status=partiallyModeled supported=true nextSeam=cleansweep-sectorback-runtime",
            info.Diagnostic);

        foreach (var adjacentCleanSweepSpellId in new ushort[] { 15_871, 15_879, 15_993 })
        {
            var adjacentLevelRow = LegacyMagicSortCapabilities.Classify(
                (LegacyMagicSort)69,
                spellId: adjacentCleanSweepSpellId);

            Assert.Equal(LegacyMagicSortCapability.SpecialCombatDamage, adjacentLevelRow.Capability);
            Assert.Equal(LegacyMagicModelingStatus.MissingRuntimeNotModeled, adjacentLevelRow.Status);
            Assert.False(adjacentLevelRow.Supported);
            Assert.Equal("damage-on-monster-special-sort-runtime", adjacentLevelRow.NextSeam);
            Assert.DoesNotContain("bounded CleanSweep 15870", adjacentLevelRow.Reason);
            Assert.Contains("spell-specific monster/player/SOB damage or effects", adjacentLevelRow.Reason);
        }
    }

    [Fact]
    public void ClassifyTwilightDanceLayTrapRuntimeSpellAsPartiallyModeledFloorTrapSlice()
    {
        var info = LegacyMagicSortCapabilities.Classify(
            LegacyMagicSort.LayTrap,
            spellId: LegacyLayTrap.TwilightDanceSpellId);

        Assert.Equal(LegacyMagicSortCapability.FloorTrapRuntime, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("LayTrap(21)", info.SortName);
        Assert.Equal("lay-trap-floor-effect-runtime", info.NextSeam);
        Assert.Contains("direct LayTrap runtime slices for TwilightDance 12070, DaggerStorm 11600, SpaceLeap 15620, and BloomofDeath 14720", info.Reason);
        Assert.Contains("LayTrapThree floor-effect projection", info.Reason);
        Assert.Contains("three trap FloorSpell/MsgFloorItem effects named trap", info.Reason);
        Assert.Contains("300ms trigger-delay stepping", info.Reason);
        Assert.Contains("runtime floor spell trigger/removal", info.Reason);
        Assert.Contains("monster/player/SOB target checks within distance <= 3", info.Reason);
        Assert.Contains("70% player damage", info.Reason);
        Assert.Contains("ActionQuery Type 434 removal", info.Reason);
        Assert.Contains("removal broadcast within distance <= 18", info.Reason);
        Assert.Contains("BloomofDeath uses the direct radius-6 monster/player/SOB damage branch", info.Reason);
        Assert.Contains("player damage divided by 5", info.Reason);
        Assert.Contains("SpellEffect 1", info.Reason);
        Assert.Contains("TideTrap 14250 is source-backed through MoveLine's equipped TideTrap branch and ThreadPool case 14250", info.Reason);
        Assert.Contains("other unmodeled sort-21 floor/trap branches", info.Reason);
        Assert.Contains("magictype.txt contains sort 21 floor/trap rows", info.SourceEvidence);
        Assert.Contains("12070 TwilightDance", info.SourceEvidence);
        Assert.Contains("rows 12070-12076 use type 12070, sort 21, and name TwilightDance", info.SourceEvidence);
        Assert.Contains("LayTrap.cs case 12070 creates LayTrapThree", info.SourceEvidence);
        Assert.Contains("FloorSpell(40u", info.SourceEvidence);
        Assert.Contains("LayTrapThree.cs builds a rounded DDA line", info.SourceEvidence);
        Assert.Contains("ThreadPool.cs case 12070 invokes ready floor spells", info.SourceEvidence);
        Assert.Contains("applies 70% player damage", info.SourceEvidence);
        Assert.Contains("ActionQuery Type 434", info.SourceEvidence);
        Assert.Contains("broadcasts removal within distance <= 18", info.SourceEvidence);
        Assert.Contains("TideTrap 14250 belongs to the MoveLine/ThreadPool path", info.SuggestedNextOwner);
        Assert.DoesNotContain("especially TideTrap 14250", info.SuggestedNextOwner);
        Assert.DoesNotContain("broader unsupported floor/trap rows", info.SuggestedNextOwner);
        Assert.Contains("modeled TwilightDance 12070, DaggerStorm 11600, SpaceLeap 15620, LayTrap 12550/12570, BloomofDeath 14720", info.SuggestedNextOwner);
        Assert.Contains(
            "magicSort=LayTrap(21) spellId=12070 capability=floorTrapRuntime status=partiallyModeled supported=true nextSeam=lay-trap-floor-effect-runtime",
            info.Diagnostic);
    }

    [Fact]
    public void ClassifyBloomofDeathLayTrapRuntimeSpellAsPartiallyModeledRadiusSlice()
    {
        var info = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.LayTrap, spellId: 14_720);

        Assert.Equal(LegacyMagicSortCapability.FloorTrapRuntime, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("LayTrap(21)", info.SortName);
        Assert.Equal("lay-trap-floor-effect-runtime", info.NextSeam);
        Assert.Contains("direct LayTrap runtime slices for TwilightDance 12070, DaggerStorm 11600, SpaceLeap 15620, and BloomofDeath 14720", info.Reason);
        Assert.Contains("BloomofDeath uses the direct radius-6 monster/player/SOB damage branch", info.Reason);
        Assert.Contains("player damage divided by 5", info.Reason);
        Assert.Contains("SpellEffect 1", info.Reason);
        Assert.Contains("TideTrap 14250 is source-backed through MoveLine's equipped TideTrap branch and ThreadPool case 14250", info.Reason);
        Assert.Contains("magictype.txt contains sort 21 floor/trap rows", info.SourceEvidence);
        Assert.Contains("14720-14728 BloomofDeath", info.SourceEvidence);
        Assert.Contains("12070 TwilightDance", info.SourceEvidence);
        Assert.Contains("14250 TideTrap", info.SourceEvidence);
        Assert.Contains("MagicSort.LayTrap dispatches LayTrap.Execute", info.SourceEvidence);
        Assert.Contains("LayTrap.cs calls CanUseSpell.Verified", info.SourceEvidence);
        Assert.Contains("FloorSpell/MsgFloorItem effects", info.SourceEvidence);
        Assert.Contains("case 14720 BloomofDeath", info.SourceEvidence);
        Assert.Contains("visible monsters, visible players, and SOB NPCs in caster radius 6", info.SourceEvidence);
        Assert.Contains("dividing visible-player damage by 5", info.SourceEvidence);
        Assert.Contains("UpdateSpell.CheckUpdate", info.SourceEvidence);
        Assert.Contains("MsgSpellAnimation.Send", info.SourceEvidence);
        Assert.Contains("TideTrap 14250 belongs to the MoveLine/ThreadPool path", info.SuggestedNextOwner);
        Assert.DoesNotContain("broader unsupported floor/trap rows", info.SuggestedNextOwner);
        Assert.Contains("modeled TwilightDance 12070, DaggerStorm 11600, SpaceLeap 15620, LayTrap 12550/12570, BloomofDeath 14720", info.SuggestedNextOwner);
        Assert.Contains(
            "magicSort=LayTrap(21) spellId=14720 capability=floorTrapRuntime status=partiallyModeled supported=true nextSeam=lay-trap-floor-effect-runtime",
            info.Diagnostic);
    }

    [Fact]
    public void ClassifyLayTrap12990RuntimeSpellAsPartiallyModeledTargetFloorEffectSlice()
    {
        var info = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.LayTrap, spellId: 12_990);

        Assert.Equal(LegacyMagicSortCapability.FloorTrapRuntime, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("LayTrap(21)", info.SortName);
        Assert.Equal("lay-trap-floor-effect-runtime", info.NextSeam);
        Assert.Contains("target floor-effect spell 12990", info.Reason);
        Assert.Contains("source floor item 1530 color 14", info.Reason);
        Assert.Contains("visible target coordinates", info.Reason);
        Assert.Contains("1000ms floor-effect due time", info.Reason);
        Assert.Contains("caster-position spell animation", info.Reason);
        Assert.Contains("10000 spell experience damage", info.Reason);
        Assert.Contains("DontShow/Angle", info.Reason);
        Assert.Contains("case 12990 creates FloorSpell(1530u", info.SourceEvidence);
        Assert.Contains("ItemOwnerUID=caster", info.SourceEvidence);
        Assert.Contains("LayTrap 12990", info.SuggestedNextOwner);
        Assert.Contains(
            "magicSort=LayTrap(21) spellId=12990 capability=floorTrapRuntime status=partiallyModeled supported=true nextSeam=lay-trap-floor-effect-runtime",
            info.Diagnostic);
    }

    [Fact]
    public void ClassifyDaggerStormLayTrapRuntimeSpellAsPartiallyModeledVisualFloorEffectSlice()
    {
        var info = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.LayTrap, spellId: 11_600);

        Assert.Equal(LegacyMagicSortCapability.FloorTrapRuntime, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("LayTrap(21)", info.SortName);
        Assert.Equal("lay-trap-floor-effect-runtime", info.NextSeam);
        Assert.Contains("DaggerStorm 11600 creates one source-shaped visual floor effect named trap", info.Reason);
        Assert.Contains("item 50/41/42 by spell soul", info.Reason);
        Assert.Contains("color/FlowerType 11", info.Reason);
        Assert.Contains("4000ms visual expiry", info.Reason);
        Assert.Contains("caster spell animation damage 0", info.Reason);
        Assert.Contains("10000 spell experience", info.Reason);
        Assert.Contains("rows 11600-11604 use type 11600, sort 21, and name DaggerStorm", info.SourceEvidence);
        Assert.Contains("LayTrap.cs case 11600", info.SourceEvidence);
        Assert.Contains("NormalDaggerStorm=50", info.SourceEvidence);
        Assert.Contains("DaggerStorm 11600", info.SuggestedNextOwner);
        Assert.Contains(
            "magicSort=LayTrap(21) spellId=11600 capability=floorTrapRuntime status=partiallyModeled supported=true nextSeam=lay-trap-floor-effect-runtime",
            info.Diagnostic);
    }

    [Fact]
    public void ClassifySpaceLeapLayTrapRuntimeSpellAsPartiallyModeledVisualFloorEffectSlice()
    {
        var info = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.LayTrap, spellId: 15_620);

        Assert.Equal(LegacyMagicSortCapability.FloorTrapRuntime, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("LayTrap(21)", info.SortName);
        Assert.Equal("lay-trap-floor-effect-runtime", info.NextSeam);
        Assert.Contains("SpaceLeap 15620 runs the Water/qualifier/blocked-map source gate after resources", info.Reason);
        Assert.Contains("item 930", info.Reason);
        Assert.Contains("17000ms visual expiry", info.Reason);
        Assert.Contains("ActionQuery Type 451", info.Reason);
        Assert.Contains("MoveY=casterY", info.Reason);
        Assert.Contains("10000 spell experience", info.Reason);
        Assert.Contains("magictype.txt rows 2282-2308 define SpaceLeap spell 15620 as sort 21", info.SourceEvidence);
        Assert.Contains("LayTrap.cs lines 344-395, case 15620", info.SourceEvidence);
        Assert.Contains("FloorSpell(930u", info.SourceEvidence);
        Assert.Contains("SpaceLeap 15620", info.SuggestedNextOwner);
        Assert.Contains(
            "magicSort=LayTrap(21) spellId=15620 capability=floorTrapRuntime status=partiallyModeled supported=true nextSeam=lay-trap-floor-effect-runtime",
            info.Diagnostic);
    }

    [Theory]
    [InlineData(12_370, "AuroraLotus 12370 requires TaoistPower >= 9 and a non-NoRev map", "item 930 color 13 named AuroraLotus", "AppendIcon flag 172", "FullPowerWater removal")]
    [InlineData(12_380, "FlameLotus 12380 requires TaoistPower >= 9 after resources without the NoRev gate", "item 940 color 13 named FlameLotus", "AppendIcon flag 173", "FullPowerFire removal")]
    public void ClassifyLotusLayTrapRuntimeSpellsAsPartiallyModeledVisualFloorEffectSlices(
        ushort spellId,
        string gateSlice,
        string floorSlice,
        string appendIconSlice,
        string flagSlice)
    {
        var info = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.LayTrap, spellId: spellId);

        Assert.Equal(LegacyMagicSortCapability.FloorTrapRuntime, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("LayTrap(21)", info.SortName);
        Assert.Equal("lay-trap-floor-effect-runtime", info.NextSeam);
        Assert.Contains("AuroraLotus 12370 and FlameLotus 12380 visual floor effects", info.Reason);
        Assert.Contains(gateSlice, info.Reason);
        Assert.Contains(floorSlice, info.Reason);
        Assert.Contains("Life=MaxLife=25", info.Reason);
        Assert.Contains("FlowerType=2", info.Reason);
        Assert.Contains("source TqTimer now+7s", info.Reason);
        Assert.Contains("empty caster spell animation", info.Reason);
        Assert.Contains("10000 spell experience", info.Reason);
        Assert.Contains("TaoistPower reset", info.Reason);
        Assert.Contains(appendIconSlice, info.Reason);
        Assert.Contains(flagSlice, info.Reason);
        Assert.Contains("7000ms visual expiry", info.Reason);
        Assert.Contains("magictype.txt rows 1411-1424", info.SourceEvidence);
        Assert.Contains("LayTrap.cs lines 423-466", info.SourceEvidence);
        Assert.Contains("AddFloor", info.SourceEvidence);
        Assert.Contains("TqTimer(DateTime.Now.AddSeconds(7))", info.SourceEvidence);
        Assert.Contains("AppendIcon 0x4E", info.SourceEvidence);
        Assert.Contains("AuroraLotus/FlameLotus 12370/12380", info.SuggestedNextOwner);
        Assert.Contains("Remaining direct LayTrap parity stays explicit for other unmodeled sort-21 floor/trap branches", info.Reason);
        Assert.Contains(
            $"magicSort=LayTrap(21) spellId={spellId} capability=floorTrapRuntime status=partiallyModeled supported=true nextSeam=lay-trap-floor-effect-runtime",
            info.Diagnostic);
    }

    [Theory]
    [InlineData(16_310)]
    [InlineData(16_320)]
    public void ClassifyWaterShockwaveLayTrapRuntimeSpellsAsPartiallyModeledPlacementRepeatAndFinalLineSlices(
        ushort spellId)
    {
        var info = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.LayTrap, spellId: spellId);

        Assert.Equal(LegacyMagicSortCapability.FloorTrapRuntime, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("LayTrap(21)", info.SortName);
        Assert.Equal("lay-trap-floor-effect-runtime", info.NextSeam);
        Assert.Contains("WaterShockwave 16310/16320 source-shaped floor placement", info.Reason);
        Assert.Contains("item 2560 color 23 named STR_TRAP_ID_2537@@", info.Reason);
        Assert.Contains("packet X/Y at the caster", info.Reason);
        Assert.Contains("OwnerX/Y for the later line", info.Reason);
        Assert.Contains("3000ms invoke plus 1000ms repeat metadata", info.Reason);
        Assert.Contains("emits bomb=1 repeat radius pulses every 1000ms before due from the floor UID at packet X/Y", info.Reason);
        Assert.Contains("emits the due final bomb=2 line target damage and pushback from the floor UID at OwnerX/Y", info.Reason);
        Assert.DoesNotContain("delayed 1000ms repeat pulses fenced", info.Reason);
        Assert.DoesNotContain("watershockwave-delayed-repeat-pulse-runtime", info.Reason);
        Assert.Contains("magictype.txt rows 2509-2528", info.SourceEvidence);
        Assert.Contains("LayTrap.cs lines 27-49", info.SourceEvidence);
        Assert.Contains("ThreadPool.cs lines 1116-1264", info.SourceEvidence);
        Assert.Contains("Physical.cs lines 301-304", info.SourceEvidence);
        Assert.Contains("WaterShockwave 16310/16320 placement/repeat pulse/final line damage", info.SuggestedNextOwner);
        Assert.DoesNotContain("delayed 1000ms repeat pulse", info.SuggestedNextOwner);
        Assert.DoesNotContain("watershockwave-delayed-repeat-pulse-runtime", info.SuggestedNextOwner);
        Assert.Contains(
            $"magicSort=LayTrap(21) spellId={spellId} capability=floorTrapRuntime status=partiallyModeled supported=true nextSeam=lay-trap-floor-effect-runtime",
            info.Diagnostic);
    }

    [Theory]
    [InlineData(16_460)]
    [InlineData(16_470)]
    public void ClassifyDustDetachmentLayTrapRuntimeSpellsAsPartiallyModeledCastSobRepeatAndRemoveOnlySlices(
        ushort spellId)
    {
        var info = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.LayTrap, spellId: spellId);

        Assert.Equal(LegacyMagicSortCapability.FloorTrapRuntime, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("LayTrap(21)", info.SortName);
        Assert.Equal("lay-trap-floor-effect-runtime", info.NextSeam);
        Assert.Contains("DustDetachment 16460/16470 floor placement plus cast-time SOB damage", info.Reason);
        Assert.Contains("item 2570 color 14 at Attack.X/Y", info.Reason);
        Assert.Contains("ItemOwnerUID=0", info.Reason);
        Assert.Contains("cast-time SOB targets within radius <= 6 from caster X/Y", info.Reason);
        Assert.Contains("5000ms invoke plus 1000ms repeat metadata", info.Reason);
        Assert.Contains("DBSkill.MaxTargets radius", info.Reason);
        Assert.Contains("monsters/players only", info.Reason);
        Assert.Contains("Base.CalculateSoul(damage, LevelHu) / 4", info.Reason);
        Assert.Contains("player damage unchanged", info.Reason);
        Assert.Contains("RemoveEffect at invoke time using the caster-origin viewer radius", info.Reason);
        Assert.Contains("modeled DustSigilStunn gear-contributor player stun behavior", info.Reason);
        Assert.Contains("magictype.txt rows 2659-2678", info.SourceEvidence);
        Assert.Contains("LayTrap.cs lines 73-106", info.SourceEvidence);
        Assert.Contains("ThreadPool.cs lines 1431-1505", info.SourceEvidence);
        Assert.Contains("modeled DustSigilStunn gear-contributor behavior", info.SourceEvidence);
        Assert.Contains("DustDetachment 16460/16470 placement/cast-time SOB/repeat pulse/remove-only final invoke", info.SuggestedNextOwner);
        Assert.Contains("DustSigilStunn/FlameSigilRapid are modeled gear-contributor behavior", info.SuggestedNextOwner);
        Assert.Contains(
            $"magicSort=LayTrap(21) spellId={spellId} capability=floorTrapRuntime status=partiallyModeled supported=true nextSeam=lay-trap-floor-effect-runtime",
            info.Diagnostic);
    }

    [Theory]
    [InlineData(16_500)]
    [InlineData(16_510)]
    public void ClassifySickleWindLayTrapRuntimeSpellsAsPartiallyModeledCastSobRepeatAndRemoveOnlySlices(
        ushort spellId)
    {
        var info = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.LayTrap, spellId: spellId);

        Assert.Equal(LegacyMagicSortCapability.FloorTrapRuntime, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("LayTrap(21)", info.SortName);
        Assert.Equal("lay-trap-floor-effect-runtime", info.NextSeam);
        Assert.Contains("SickleWind 16500/16510 floor placement plus cast-time SOB damage", info.Reason);
        Assert.Contains("item 2540 color 14 at Attack.X/Y", info.Reason);
        Assert.Contains("ItemOwnerUID=0", info.Reason);
        Assert.Contains("self-only caster animation", info.Reason);
        Assert.Contains("cast-time SOB targets within radius <= 6 from caster X/Y", info.Reason);
        Assert.Contains("3000ms invoke plus 1000ms repeat metadata", info.Reason);
        Assert.Contains("hard-coded radius 5", info.Reason);
        Assert.Contains("monsters/players only", info.Reason);
        Assert.Contains("Base.CalculateSoul(damage, LevelHu) * 55 / 100", info.Reason);
        Assert.Contains("player damage as Base.CalculateSoul(damage, LevelHu)", info.Reason);
        Assert.Contains("no SOB repeat branch", info.Reason);
        Assert.Contains("RemoveEffect at invoke time using the caster-origin viewer radius", info.Reason);
        Assert.Contains("magictype.txt rows 2699-2718", info.SourceEvidence);
        Assert.Contains("LayTrap.cs lines 108-140", info.SourceEvidence);
        Assert.Contains("ThreadPool.cs lines 1506-1572", info.SourceEvidence);
        Assert.Contains("send the cast animation only to the user", info.SourceEvidence);
        Assert.Contains("SickleWind 16500/16510 placement/cast-time SOB/self-only cast animation/repeat monster-player pulse/remove-only final invoke", info.SuggestedNextOwner);
        Assert.Contains(
            $"magicSort=LayTrap(21) spellId={spellId} capability=floorTrapRuntime status=partiallyModeled supported=true nextSeam=lay-trap-floor-effect-runtime",
            info.Diagnostic);
    }

    [Theory]
    [InlineData(16_540)]
    [InlineData(16_550)]
    public void ClassifyFlameOfDestructionLayTrapRuntimeSpellsAsPartiallyModeledRepeatAndRemoveOnlySlices(
        ushort spellId)
    {
        var info = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.LayTrap, spellId: spellId);

        Assert.Equal(LegacyMagicSortCapability.FloorTrapRuntime, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("LayTrap(21)", info.SortName);
        Assert.Equal("lay-trap-floor-effect-runtime", info.NextSeam);
        Assert.Contains("FlameofDestruction 16540/16550 floor placement plus repeat pulse and remove-only final invoke", info.Reason);
        Assert.Contains("item 2530 color 14 at Attack.X/Y", info.Reason);
        Assert.Contains("ItemOwnerUID=0", info.Reason);
        Assert.Contains("4000ms invoke plus 1000ms repeat metadata", info.Reason);
        Assert.Contains("bomb=1 repeat radius pulses from the floor UID within DBSkill.MaxTargets", info.Reason);
        Assert.Contains("player DamageOnHuman clamping", info.Reason);
        Assert.Contains("SOB 10% damage scaling", info.Reason);
        Assert.Contains("RemoveEffect at invoke time using the caster-origin viewer radius", info.Reason);
        Assert.Contains("modeled FlameSigilRapid gear-contributor equipment bonus behavior", info.Reason);
        Assert.Contains("magictype.txt rows 2739-2758", info.SourceEvidence);
        Assert.Contains("LayTrap.cs lines 50-71", info.SourceEvidence);
        Assert.Contains("ThreadPool.cs lines 1331-1429", info.SourceEvidence);
        Assert.Contains("FlameofDestruction 16540/16550 placement/repeat pulse/remove-only final invoke", info.SuggestedNextOwner);
        Assert.Contains("DustSigilStunn/FlameSigilRapid are modeled gear-contributor behavior", info.SuggestedNextOwner);
        Assert.Contains(
            $"magicSort=LayTrap(21) spellId={spellId} capability=floorTrapRuntime status=partiallyModeled supported=true nextSeam=lay-trap-floor-effect-runtime",
            info.Diagnostic);
    }

    [Theory]
    [InlineData(12_550, "LayTrap 12550 creates four source-shaped item 1001390 floor effects", "RandomFourLayTraps", "4000ms", "radius <= 3")]
    [InlineData(12_570, "LayTrap 12570 creates one source-shaped item 1001380 floor effect", "resolves Attack.OpponentUID as visible Player, Monster, or SobNpc", "1500ms", "radius <= 2")]
    public void ClassifySourceCaseLayTrap12550And12570AsPartiallyModeledDelayedFloorTrapSlices(
        ushort spellId,
        string reasonSlice,
        string sourceSlice,
        string delaySlice,
        string radiusSlice)
    {
        var info = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.LayTrap, spellId: spellId);

        Assert.Equal(LegacyMagicSortCapability.FloorTrapRuntime, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("LayTrap(21)", info.SortName);
        Assert.Equal("lay-trap-floor-effect-runtime", info.NextSeam);
        Assert.Contains("source-case delayed floor traps 12550/12570", info.Reason);
        Assert.Contains(reasonSlice, info.Reason);
        Assert.Contains(delaySlice, info.Reason);
        Assert.Contains(radiusSlice, info.Reason);
        Assert.Contains("removes within distance <= 18", info.Reason);
        Assert.Contains("10000 spell experience", info.Reason);
        Assert.Contains("LayTrap.cs case 12550", info.SourceEvidence);
        Assert.Contains("LayTrap.cs case 12570", info.SourceEvidence);
        Assert.Contains(sourceSlice, info.SourceEvidence);
        Assert.Contains("ThreadPool.cs cases 12550/12570", info.SourceEvidence);
        Assert.Contains("LayTrap 12550/12570", info.SuggestedNextOwner);
        Assert.Contains(
            $"magicSort=LayTrap(21) spellId={spellId} capability=floorTrapRuntime status=partiallyModeled supported=true nextSeam=lay-trap-floor-effect-runtime",
            info.Diagnostic);
    }

    [Fact]
    public void ClassifyThunderStrikerLayTrapRuntimeSpellAsPartiallyModeledRadiusSlice()
    {
        var info = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.LayTrap, spellId: 15_770);

        Assert.Equal(LegacyMagicSortCapability.FloorTrapRuntime, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("LayTrap(21)", info.SortName);
        Assert.Equal("lay-trap-floor-effect-runtime", info.NextSeam);
        Assert.Contains("ThunderStriker/SkyFall 15770 direct radius damage", info.Reason);
        Assert.Contains("ThunderStrikerEnergy >= 100 attribute-backed gate after resources", info.Reason);
        Assert.Contains("spends 100 energy", info.Reason);
        Assert.Contains("radius <= 4 of Attack.X/Y", info.Reason);
        Assert.Contains("monster *5", info.Reason);
        Assert.Contains("player *18/28", info.Reason);
        Assert.Contains("SOB *3", info.Reason);
        Assert.Contains("SpellEffect 1", info.Reason);
        Assert.Contains("10000 source damage", info.Reason);
        Assert.Contains("magictype.txt row 2362 defines spell 15770 SkyFall as sort 21 LayTrap", info.SourceEvidence);
        Assert.Contains("LayTrap.cs case 15770", info.SourceEvidence);
        Assert.Contains("ThunderStrikerEnergy < 100", info.SourceEvidence);
        Assert.Contains("WorldPacketHandlers.PlayerMagic.Traps.ThunderStriker.cs", info.SourceEvidence);
        Assert.Contains("ThunderStriker/SkyFall 15770", info.SuggestedNextOwner);
        Assert.Contains(
            "magicSort=LayTrap(21) spellId=15770 capability=floorTrapRuntime status=partiallyModeled supported=true nextSeam=lay-trap-floor-effect-runtime",
            info.Diagnostic);
    }

    [Fact]
    public void ClassifyTideTrapLayTrapSortWithSourceBackedFloorTrapDiagnosticAsMissing()
    {
        var info = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.LayTrap, spellId: 14_250);

        Assert.Equal(LegacyMagicSortCapability.FloorTrapRuntime, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.MissingRuntimeNotModeled, info.Status);
        Assert.False(info.Supported);
        Assert.Equal("LayTrap(21)", info.SortName);
        Assert.Equal("lay-trap-floor-effect-runtime", info.NextSeam);
        Assert.Contains("magictype.txt DB row for TideTrap 14250 is sort 21 LayTrap", info.Reason);
        Assert.Contains("decompiled LayTrap.cs has no direct case 14250", info.Reason);
        Assert.Contains("source-backed TideTrap runtime is reached through MoveLine spell 11110", info.Reason);
        Assert.Contains("equipped TideTrap plus learned 14250 creates the floor spell", info.Reason);
        Assert.Contains("ThreadPool case 14250 owns the ClientFloorSpells lifecycle", info.Reason);
        Assert.Contains("14250 TideTrap", info.SourceEvidence);
        Assert.Contains("MoveLine.cs", info.SourceEvidence);
        Assert.Contains("ThreadPool.cs case 14250", info.SourceEvidence);
        Assert.Contains("TideTrap parity on the MoveLine 11110 plus ThreadPool case 14250 path", info.SuggestedNextOwner);
        Assert.Contains("do not implement a nonexistent direct LayTrap.Execute case 14250", info.SuggestedNextOwner);
        Assert.DoesNotContain("floor-effect item creation/lifetime/invoke routing", info.SuggestedNextOwner);
        Assert.Contains(
            "magicSort=LayTrap(21) spellId=14250 capability=floorTrapRuntime status=missingRuntimeNotModeled supported=false nextSeam=lay-trap-floor-effect-runtime",
            info.Diagnostic);
    }

    [Theory]
    [InlineData(4_000)]
    [InlineData(12_090)]
    [InlineData(12_840)]
    public void ClassifyCallPetExamplesAsPartiallyModeled(ushort spellId)
    {
        var info = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)23, spellId);

        Assert.Equal(LegacyMagicSortCapability.SummonPet, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("CallPet(23)", info.SortName);
        Assert.Equal("summon-pet-runtime", info.NextSeam);
        Assert.Contains("ShadowClone and Thundercloud", info.Reason);
        Assert.Contains("Other CallPet sort-23 spell IDs fall through", info.Reason);
        Assert.Contains("sort 72 Summon rows are source-silent", info.Reason);
        Assert.Contains("CallPet.cs cases 12090 and 12840", info.SourceEvidence);
        Assert.Contains("no switch case after CanUseSpell.Verified", info.SourceEvidence);
        Assert.Contains("m_ID 3843", info.SourceEvidence);
        Assert.Contains("m_ID 940", info.SourceEvidence);
        Assert.Contains("No source-backed summon owner remains", info.SuggestedNextOwner);
        Assert.Contains("live capture", info.SuggestedNextOwner);
    }

    [Theory]
    [InlineData(1_050)]
    [InlineData(1_100)]
    [InlineData(16_610)]
    public void ClassifyReviveSupportExamplesAsPartiallyModeled(ushort spellId)
    {
        var info = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.DetachStatus, spellId);

        Assert.Equal(LegacyMagicSortCapability.ReviveSupport, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("DetachStatus(7)", info.SortName);
        Assert.Equal("magic-revive-support-runtime", info.NextSeam);
        Assert.Contains("magic-cast Revive/Pray", info.Reason);
        Assert.Contains("DetachStatus.cs cases 1050, 1100, and 16610", info.SourceEvidence);
        Assert.Contains("Mercy Prayer", info.SuggestedNextOwner);
    }

    [Theory]
    [InlineData(1_040)]
    [InlineData(12_280)]
    public void ClassifyDispatchXpExamplesAsPartiallyModeled(ushort spellId)
    {
        var info = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.DispatchXp, spellId);

        Assert.Equal(LegacyMagicSortCapability.XpSkill, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("DispatchXp(11)", info.SortName);
        Assert.Equal("xp-skill-runtime", info.NextSeam);
        Assert.Contains("Roar and DragonRoar", info.Reason);
        Assert.Contains("DispatchXp.cs handles DragonRoar", info.SourceEvidence);
        Assert.Contains("global prechecks", info.SuggestedNextOwner);
    }

    [Fact]
    public void ClassifyGaleBombBombPirteMonsterPlayerSobAndPushbackSliceAsPartiallyModeledPirateSlice()
    {
        var info = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)60, spellId: 11_070);

        Assert.Equal(LegacyMagicSortCapability.PirateSpecial, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("BombPirte(60)", info.SortName);
        Assert.Equal("pirate-special-runtime", info.NextSeam);
        Assert.Contains("GaleBomb/BombPirte", info.Reason);
        Assert.Contains("spell 11070", info.Reason);
        Assert.Contains("radius-6 visible-monster physical damage", info.Reason);
        Assert.Contains("radius-6 visible-player physical damage divided by 2", info.Reason);
        Assert.Contains("BombPirte 11070 player Physical.OnPlayer WildPhoenix defender hook before /2 scaling", info.Reason);
        Assert.Contains("radius-6 visible-SOB damage", info.Reason);
        Assert.Contains("shared level-based target cap (3/4/5)", info.Reason);
        Assert.Contains("Acalanatha/NatureShield player-target rune avoidance", info.Reason);
        Assert.Contains("source-shaped monster/player pushback", info.Reason);
        Assert.Contains("blocked-first-tile original-coordinate behavior", info.Reason);
        Assert.Contains("GuildWar endpoint rejection/pullback", info.Reason);
        Assert.Contains("map 1038/3868 SOB return", info.Reason);
        Assert.Contains("protect/AttackStamp", info.Reason);
        Assert.Contains("rune avoidance", info.Reason);
        AssertContainsModeledSharedPlayerCanAttackGates(info.Reason);
        AssertContainsModeledSharedSobCanAttackGates(info.Reason);
        AssertContainsCanAttackPlayerRelationGateReview(info.Reason);
        AssertContainsRemainingCanAttackNpcGaps(info.Reason);
        Assert.DoesNotContain("exact Acalanatha/NatureShield rune behavior", info.Reason);
        Assert.DoesNotContain("terrain pushback validation", info.Reason);
        Assert.DoesNotContain("map-specific SOB/GuildWar edge cases", info.Reason);
        Assert.DoesNotContain("monster-only", info.Reason);
        Assert.DoesNotContain("player/SOB edge behavior", info.Reason);
        Assert.DoesNotContain("pushback and floor-check side effects", info.Reason);
        Assert.Contains("BombPirte.Execute", info.SourceEvidence);
        Assert.Contains("Flags.SpellID.GaleBomb = 11070", info.SourceEvidence);
        Assert.Contains("shared level target cap of 3/4/5", info.SourceEvidence);
        Assert.Contains("radius-6 monster/player/SOB loops", info.SourceEvidence);
        Assert.Contains("player physical damage /2", info.SourceEvidence);
        Assert.Contains("BombPirte.cs lines 118-120 call Physical.OnPlayer before the /2 scaling step", info.SourceEvidence);
        Assert.Contains("Physical.cs applies target.Owner.CombatGear.WildPhoenix()", info.SourceEvidence);
        Assert.Contains("CheckGuildWar floor validation", info.SourceEvidence);
        Assert.Contains("maps 1038/3868 returning before SOB damage", info.SourceEvidence);
        Assert.Contains("Acalanatha/NatureShield rune checks", info.SourceEvidence);
        Assert.Contains("other pirate-special handlers", info.Reason);
        Assert.DoesNotContain("exact rune behavior", info.SuggestedNextOwner);
        Assert.DoesNotContain("terrain pushback validation", info.SuggestedNextOwner);
        Assert.DoesNotContain("map-specific SOB/GuildWar edge cases", info.SuggestedNextOwner);
        Assert.DoesNotContain("player/SOB edge behavior", info.SuggestedNextOwner);
        Assert.DoesNotContain("finish BombPirte edge effects", info.SuggestedNextOwner);
        AssertContainsCanAttackPlayerRelationGateReview(info.SuggestedNextOwner);
        AssertContainsRemainingCanAttackNpcGaps(info.SuggestedNextOwner);
        Assert.Contains(
            "magicSort=BombPirte(60) spellId=11070 capability=pirateSpecial status=partiallyModeled supported=true nextSeam=pirate-special-runtime",
            info.Diagnostic);
    }

    [Fact]
    public void ClassifyRemainingBombPirteSpellKeepsGaleBombSliceExpanded()
    {
        var info = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)60, spellId: 11_071);

        Assert.Equal(LegacyMagicSortCapability.PirateSpecial, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.MissingRuntimeNotModeled, info.Status);
        Assert.False(info.Supported);
        Assert.Equal("BombPirte(60)", info.SortName);
        Assert.Contains("BombPirte 11070 radius-6 monster, player /2, SOB damage, shared target cap", info.Reason);
        Assert.Contains("player-target WildPhoenix defender hook before /2 scaling", info.Reason);
        Assert.Contains("source-shaped monster/player pushback", info.Reason);
        Assert.Contains("blocked-first-tile original-coordinate behavior", info.Reason);
        Assert.Contains("GuildWar endpoint rejection/pullback", info.Reason);
        Assert.Contains("map 1038/3868 SOB return", info.Reason);
        Assert.Contains("protect/AttackStamp", info.Reason);
        Assert.Contains("rune avoidance", info.Reason);
        Assert.DoesNotContain("terrain pushback validation", info.Reason);
        Assert.DoesNotContain("map-specific SOB/GuildWar edge cases", info.Reason);
        Assert.DoesNotContain("production-lane pushback slice", info.Reason);
        Assert.DoesNotContain("BombPirte 11070 monster-only slice", info.Reason);
    }

    [Theory]
    [InlineData(56, 0, "ScurvyBomb(56)")]
    [InlineData(56, 11_041, "ScurvyBomb(56)")]
    [InlineData(57, 0, "CannonBarrage(57)")]
    [InlineData(62, 11_101, "AddBlackSpot(62)")]
    public void ClassifyRemainingKnownPirateSpellSortsFromLegacyDataAsMissing(
        int sortValue,
        ushort spellId,
        string expectedSortName)
    {
        var info = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)sortValue, spellId);

        Assert.Equal(LegacyMagicSortCapability.PirateSpecial, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.MissingRuntimeNotModeled, info.Status);
        Assert.False(info.Supported);
        Assert.Equal(expectedSortName, info.SortName);
        Assert.Equal("pirate-special-runtime", info.NextSeam);
        AssertContainsModeledSharedPlayerCanAttackGates(info.Reason);
        AssertContainsModeledSharedSobCanAttackGates(info.Reason);
        AssertContainsCanAttackPlayerRelationGateReview(info.Reason);
        AssertContainsRemainingCanAttackNpcGaps(info.Reason);
        Assert.DoesNotContain("broader CanAttack side-effect matrix", info.Reason);
        Assert.Contains("Pirate cases dispatch", info.SourceEvidence);
        AssertContainsCanAttackPlayerRelationGateReview(info.SuggestedNextOwner);
        AssertContainsRemainingCanAttackNpcGaps(info.SuggestedNextOwner);
    }

    [Fact]
    public void ClassifyKnownPirateSpellIdWhenSortIsUnknown()
    {
        var info = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)0, spellId: 11_120);

        Assert.Equal(LegacyMagicSortCapability.PirateSpecial, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.MissingRuntimeNotModeled, info.Status);
        Assert.False(info.Supported);
        Assert.Equal("SourceSilent(0)", info.SortName);
        Assert.Equal("pirate-special-runtime", info.NextSeam);
    }

    [Theory]
    [InlineData(13_060, "rectangle-13060-runtime", "width-2 line targeting", "family 16 base 13040", "CombatGearAttack.cs case 13060", "WorldPacketHandlers.CombatGear.DirectLineAttacks.cs")]
    [InlineData(15_690, "rectangle-15690-runtime", "100-degree distance-5 sector", "family 9 base 15670", "CombatGearAttack.cs case 15690", "WorldPacketHandlers.CombatGear.SectorAttacks.cs")]
    public void ClassifyExactD13RectangleSelectorsAsSourceBackedPartialRuntime(
        ushort spellId,
        string expectedNextSeam,
        string expectedGeometry,
        string expectedSelectorBase,
        string expectedSourceCase,
        string expectedLocalOwner)
    {
        var info = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.Rectangle, spellId);

        Assert.Equal(LegacyMagicSortCapability.SpecialCombatDamage, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.PartiallyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("Rectangle(87)", info.SortName);
        Assert.Equal(expectedNextSeam, info.NextSeam);
        Assert.Contains($"exact sort 87/Rectangle plus spell {spellId}", info.Reason);
        Assert.Contains(expectedGeometry, info.Reason);
        Assert.Contains("generic Rectangle and every other Rectangle spell ID stay unsupported", info.Reason);
        Assert.Contains(expectedSelectorBase, info.SourceEvidence);
        Assert.Contains("FUN_00ACD62B adds selector index times 10", info.SourceEvidence);
        Assert.Contains("client plaintext send hook outbound and the client receive gates inbound", info.SourceEvidence);
        Assert.Contains("client-outbound/server-inbound physical CMsgInteract opcode 2135 AttackType 2 Damage", info.SourceEvidence);
        Assert.Contains("server-outbound/client-inbound CMsgMagicEffect opcode 2239", info.SourceEvidence);
        Assert.Contains("opcode 2239 is neither client-outbound nor a client-selected attack contract", info.SourceEvidence);
        Assert.Contains("7770 server is not protocol opcode, layout, direction, or order authority", info.SourceEvidence);
        Assert.Contains("matching decompiled source artifacts are gameplay evidence only", info.SourceEvidence);
        Assert.Contains(expectedSourceCase, info.SourceEvidence);
        Assert.Contains(expectedLocalOwner, info.SourceEvidence);
        Assert.Contains(
            $"magicSort=Rectangle(87) spellId={spellId} capability=specialCombatDamage status=partiallyModeled supported=true nextSeam={expectedNextSeam}",
            info.Diagnostic);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13_061)]
    public void ClassifyGenericAndAdjacentRectangleIdsAsUnsupported(ushort spellId)
    {
        var info = LegacyMagicSortCapabilities.Classify(LegacyMagicSort.Rectangle, spellId);

        Assert.Equal(LegacyMagicSortCapability.SpecialCombatDamage, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.MissingRuntimeNotModeled, info.Status);
        Assert.False(info.Supported);
        Assert.Equal("Rectangle(87)", info.SortName);
        Assert.Equal("damage-on-monster-special-sort-runtime", info.NextSeam);
        Assert.DoesNotContain("bounded exact sort 87/Rectangle", info.Reason);
        Assert.DoesNotContain("D13 client ._cache_Conquer.exe", info.SourceEvidence);
    }

    [Fact]
    public void KnownSortInventoryDistinguishesSupportedPartialMissingAndSourceSilentRows()
    {
        Assert.Contains(
            LegacyMagicSortCapabilities.KnownSorts,
            info => info.SortName == "Attack(1)" &&
                info.Status == LegacyMagicModelingStatus.PartiallyModeled &&
                info.Capability == LegacyMagicSortCapability.VisibleMonsterDamage);
        Assert.Contains(
            LegacyMagicSortCapabilities.KnownSorts,
            info => info.SortName == "AttachStatus(6)" &&
                info.Status == LegacyMagicModelingStatus.PartiallyModeled &&
                info.Capability == LegacyMagicSortCapability.AttachStatusBuff);
        Assert.Contains(
            LegacyMagicSortCapabilities.KnownSorts,
            info => info.SortName == "SourceSilent(0)" &&
                info.Status == LegacyMagicModelingStatus.SourceSilentNoOp &&
                info.Capability == LegacyMagicSortCapability.SourceSilentNoOp);
        Assert.Contains(
            LegacyMagicSortCapabilities.KnownSorts,
            info => info.SortName == "CallPet(23)" &&
                info.Status == LegacyMagicModelingStatus.PartiallyModeled &&
                info.Capability == LegacyMagicSortCapability.SummonPet);
        Assert.Contains(
            LegacyMagicSortCapabilities.KnownSorts,
            info => info.SortName == "DispatchXp(11)" &&
                info.Status == LegacyMagicModelingStatus.PartiallyModeled &&
                info.Capability == LegacyMagicSortCapability.XpSkill);
        Assert.Contains(
            LegacyMagicSortCapabilities.KnownSorts,
            info => info.SortName == "LayTrap(21)" &&
                info.Status == LegacyMagicModelingStatus.MissingRuntimeNotModeled &&
                info.Capability == LegacyMagicSortCapability.FloorTrapRuntime &&
                info.NextSeam == "lay-trap-floor-effect-runtime");
        Assert.Contains(
            LegacyMagicSortCapabilities.KnownSorts,
            info => info.SortName == "PhysicalSpells(55)" &&
                info.Status == LegacyMagicModelingStatus.PartiallyModeled &&
                info.Capability == LegacyMagicSortCapability.SpecialCombatDamage &&
                info.NextSeam == "physical-spells-combat-runtime");
        Assert.Contains(
            LegacyMagicSortCapabilities.KnownSorts,
            info => info.SortName == "AttackStatus(16)" &&
                info.Capability == LegacyMagicSortCapability.SpecialCombatDamage);
        Assert.Contains(
            LegacyMagicSortCapabilities.KnownSorts,
            info => info.SortName == "Shock(34)" &&
                info.Status == LegacyMagicModelingStatus.PartiallyModeled &&
                info.Capability == LegacyMagicSortCapability.SpecialCombatDamage &&
                info.NextSeam == "shock-combat-runtime");
        Assert.Contains(
            LegacyMagicSortCapabilities.KnownSorts,
            info => info.SortName == "Strike(81)" &&
                info.Status == LegacyMagicModelingStatus.MissingRuntimeNotModeled &&
                info.Capability == LegacyMagicSortCapability.SpecialCombatDamage &&
                info.NextSeam == "source-triage");
        Assert.Contains(
            LegacyMagicSortCapabilities.KnownSorts,
            info => info.SortName == "BreathFocus(70)" &&
                info.Status == LegacyMagicModelingStatus.FullyModeled &&
                info.Capability == LegacyMagicSortCapability.StaminaRestoreSupport &&
                info.NextSeam == "stamina-restore-runtime");
        Assert.Contains(
            LegacyMagicSortCapabilities.KnownSorts,
            info => info.SortName == "ScurvyBomb(56)" &&
                info.Status == LegacyMagicModelingStatus.MissingRuntimeNotModeled &&
                info.Capability == LegacyMagicSortCapability.PirateSpecial &&
                info.NextSeam == "pirate-special-runtime");
        Assert.Contains(
            LegacyMagicSortCapabilities.KnownSorts,
            info => info.SortName == "CannonBarrage(57)" &&
                info.Status == LegacyMagicModelingStatus.MissingRuntimeNotModeled &&
                info.Capability == LegacyMagicSortCapability.PirateSpecial &&
                info.NextSeam == "pirate-special-runtime");
        Assert.Contains(
            LegacyMagicSortCapabilities.KnownSorts,
            info => info.SortName == "BombPirte(60)" &&
                info.Status == LegacyMagicModelingStatus.PartiallyModeled &&
                info.Capability == LegacyMagicSortCapability.PirateSpecial &&
                info.NextSeam == "pirate-special-runtime");
    }

    [Fact]
    public void ClassifyUnknownSortAsUnsupportedSourceTriage()
    {
        var info = LegacyMagicSortCapabilities.Classify((LegacyMagicSort)999, spellId: 4_242);

        Assert.Equal(LegacyMagicSortCapability.UnknownUnsupported, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.MissingRuntimeNotModeled, info.Status);
        Assert.False(info.Supported);
        Assert.Equal("Unknown(999)", info.SortName);
        Assert.Equal("source-triage", info.NextSeam);
        Assert.Contains("No reviewed source evidence", info.SourceEvidence);
        Assert.Contains(
            "magicSort=Unknown(999) spellId=4242 capability=unknownUnsupported status=missingRuntimeNotModeled supported=false nextSeam=source-triage",
            LegacyMagicSortCapabilities.FormatDiagnostic((LegacyMagicSort)999, spellId: 4_242));
    }

    private static void AssertContainsModeledSharedPlayerCanAttackGates(string text)
    {
        Assert.Contains("modeled shared CanAttackPlayer gates", text);
        Assert.Contains("target CombatGear.CheckDamage reject", text);
        Assert.Contains("ImmortalForce/Overwhelm reject with direct ForbiddenPirate update", text);
        Assert.Contains(
            "direct 10 percent equipped-and-learned defender HellVortex 16370 counter reject/countercast",
            text);
        Assert.Contains(
            "source counter query X/Y 0/0 and a local active-caster/cascade-limit recursion safeguard",
            text);
        Assert.Contains("Solidness target-rune rejects for source spell ids 15940/3090/16550/16540/16520/16530/1165", text);
        Assert.Contains("attribute-backed tournament non-alive rejects for LastManStand/TopFight/KillerOfElite/ExtremePk/DragonWar/TeamDeathMatch", text);
        Assert.Contains("attribute-backed CTF map 2057 CaptureTheFlag.Attackable target reject", text);
        Assert.Contains("safe zones 10137/10250", text);
        Assert.Contains("map 700 DynamicID zero", text);
        Assert.Contains("invisible target", text);
        Assert.Contains("caster/target watching", text);
        Assert.Contains("NeptuneCurse", text);
        Assert.Contains("Protect", text);
        Assert.Contains("Capture PK target color (FlashingName/BlackName)", text);
        Assert.Contains("Team/Guild same-team/same-guild rejects", text);
        Assert.Contains("CS PK same-ServerID rejects", text);
        Assert.Contains("Union PK same-UnionUID/UnionID rejects", text);
        Assert.Contains("Jiang PK target JiangHuActive gate and source-priority Jiang flags", text);
        Assert.Contains("map 1082 same-garment reject", text);
        Assert.Contains("attribute-backed Team friend/guild-ally/clan same-or-ally rejects", text);
        Assert.Contains("attribute-backed Guild ally rejects", text);
        Assert.Contains("TeamQualifier map 700 same-team reject", text);
        Assert.Contains("attribute-backed map 1002 OnMyOwnServer/OnOwnServer reject", text);
        Assert.Contains("accepted player-target FlashingName side effect (30s RemoveOnDead)", text);
        Assert.Contains("ChillingSnow/xChillingSnow cold-time side effect", text);
        Assert.Contains("Constants.BlockAttackMap map/dynamic-ID edges", text);
        Assert.Contains("blocked maps 1004/1858/4000/4003/4006/4008/4009/4020 plus map 1002 unless Jiang PK", text);
    }

    private static void AssertContainsModeledSharedSobCanAttackGates(string text)
    {
        Assert.Contains("modeled shared CanAttackNpc gates", text);
        Assert.Contains("map 3844 always-allow", text);
        Assert.Contains("dead-SOB rejection outside map 3844", text);
        Assert.Contains("bounded NEWGUILDWAR GuildPillar/HonorPillar/ExorcimPillar/TenacityPillar authorization", text);
        Assert.Contains("bounded UnionWar UID 22348 pole authorization", text);
        Assert.Contains("rejects no union, zero pole HP, no guild, same winner union, Dead/Idle process", text);
        Assert.Contains("treats missing/default process as Dead", text);
        Assert.Contains("otherwise allows damage", text);
        Assert.Contains("bounded map 11083 PoleDomination/PoleDominationBI/PoleDominationDC/PoleDominationPC guild-pole authorization for UIDs 830/831/832/833", text);
        Assert.Contains("rejects no guild, zero pole HP, same winner guild, Dead/Idle process", text);
        Assert.Contains("treats default/missing process as Dead", text);
        Assert.Contains("bounded EliteGuildWar map 6521 guild-pole authorization for UIDs 6539/6540/6541", text);
        Assert.Contains("rejects no guild, zero pole HP, same guild name as pole owner name, and process not Alive", text);
        Assert.Contains("treats MsgEliteGuildWar default Proces as Dead", text);
        Assert.Contains("bounded MsgTrepilWar map 3838 UID 65046 guild-pole authorization", text);
        Assert.Contains("rejects no guild, zero pole HP, same guild name as pole name, and process not Alive", text);
        Assert.Contains("treats MsgTrepilWar constructor default Proces as Dead", text);
        Assert.Contains("bounded MsgTopPole map 1737 UID 65056 player-name pole authorization using Furnitures[Pole]", text);
        Assert.Contains("rejects no guild, zero pole HP, player.Name == pole.Name, and process not Alive", text);
        Assert.Contains("treats MsgTopPole static default Proces as Dead", text);
        Assert.Contains("bounded MsgEmperorWar map 10702 UID 6505 player-name pole authorization using Furnitures[Pole]", text);
        Assert.Contains("treats MsgEmperorWar static default Proces as Dead", text);
        Assert.Contains("bounded ChampionsOfWarr map 6546 UID 6547 guild-pole authorization using Furnitures[Pole]", text);
        Assert.Contains("rejects no guild, zero pole HP, same guild name as pole name, and process not Alive", text);
        Assert.Contains("treats default/missing process as Dead", text);
        Assert.Contains("bounded Guild6PoleWar map 10070 GuildPole[1-6] authorization for UIDs 100731/100732/100733/100734/100735/100736", text);
        Assert.Contains("rejects no guild, zero pole HP, same guild name as pole name, and process not Alive", text);
        Assert.Contains("treats default/missing process as Dead", text);
        Assert.Contains("bounded MsgWarOfPlayers map 6729 UID 6606 player-name pole authorization using Furnitures[Pole]", text);
        Assert.Contains("rejects zero pole HP, client.Player.Name == pole.Name, and process not Alive", text);
        Assert.DoesNotContain("6906", text);
        Assert.Contains("bounded ClassPoleWar map 10005 pole-family authorization", text);
        Assert.Contains("MsgWarriorGod UID 8001 IsWarrior", text);
        Assert.Contains("MsgArcherMaster UID 8002 IsArcher", text);
        Assert.Contains("MsgNinjaMaster UID 9803 IsNinja", text);
        Assert.Contains("MsgTrojanMaster UID 8005 IsTrojan", text);
        Assert.Contains("MsgTaoistMaster UID 8006 IsWater", text);
        Assert.Contains("MsgFireMaster UID 8007 IsFire", text);
        Assert.Contains("MsgPirateMaster UID 8008 IsPirate", text);
        Assert.Contains("MsgKungfuKing UID 8009 IsLee", text);
        Assert.Contains("MsgWindWalker UID 9011 IsWindWalker", text);
        Assert.Contains("MsgThnderStrike UID 90111 IsThunderStriker", text);
        Assert.Contains("MsgClassPolaWar UID 8012 no class gate", text);
        Assert.Contains("MsgMonkMaster UID 8004 IsMonk", text);
        Assert.Contains("wrong class family rejects before pole HP/name/process except MsgClassPolaWar", text);
        Assert.Contains("treats ClassPoleWar tournament default/missing Proces as Dead", text);
        Assert.Contains("bounded MsgWarFighters map 11073 UID 9406 player-name pole authorization using Furnitures[Pole]", text);
        Assert.Contains("treats MsgWarFighters default/missing Proces as Dead", text);
        Assert.DoesNotContain("22213", text);
        Assert.Contains("bounded MsgSchedules.SuperGuildWar map 3868 UID 820 guild-pole authorization using Furnitures[Pole]", text);
        Assert.Contains("rejects no guild, zero pole HP, same Winner.GuildID, and Dead/Idle process", text);
        Assert.Contains("treats MsgSuperGuildWar default/missing Proces as Dead with default Winner GuildID 0", text);
        Assert.DoesNotContain("25015", text);
        Assert.DoesNotContain("6591", text);
        Assert.DoesNotContain("8529", text);
        Assert.DoesNotContain("516076", text);
        Assert.DoesNotContain("516080", text);
        Assert.Contains("bounded GuildWarScore map 6784 UID 8447 guild-pole authorization", text);
        Assert.Contains("rejects no guild, zero pole HP, and process not Alive", text);
        Assert.Contains("treats default/missing process as Dead", text);
        Assert.Contains("bounded ClanWarScore map 3839 UID 8448 clan-pole authorization", text);
        Assert.Contains("rejects no clan, zero pole HP, and process not Alive", text);
        Assert.Contains("treats default/missing process as Dead", text);
        Assert.DoesNotContain("ClanWarScore map 3839 UID 6526", text);
        Assert.Contains("bounded ClanWar UID 890 pole authorization", text);
        Assert.Contains("bounded ClanDomination TC/PH/AP/DC/BR pole authorization for UIDs 1002/1011/1020/1000/1015", text);
        Assert.Contains("rejects no clan, zero pole HP, same winner clan, and Dead/Idle process", text);
        Assert.Contains("bounded NobilityPole map 11140 UID authorization for UIDs 22340/22341/22342/22343", text);
        Assert.Contains("required ranks King/Prince/Duke/Earl", text);
        Assert.Contains("rejects zero pole HP, player.Name == pole.Name, Dead process, and wrong nobility rank", text);
        Assert.Contains("treats MsgNobilityPole/MsgNobilityPole1/MsgNobilityPole2/MsgNobilityPole3 default Proces as Dead", text);
        Assert.Contains("static statue PK-mode gate", text);
        Assert.Contains("CaptureTheFlag base gate", text);
        Assert.Contains("runtime zero HitPoints", text);
        Assert.Contains("across player-magic SOB paths", text);
    }

    private static void AssertContainsCanAttackPlayerRelationGateReview(string text)
    {
        var review = GetCanAttackPlayerReviewNote(text);

        Assert.Contains("CanAttackPlayer relation-gate review", review);
        Assert.Contains("no remaining reviewed shared player-magic CanAttackPlayer relation gate gaps are currently identified", review);
        Assert.Contains("exact caller-specific CanAttackPlayer side effects stay tracked separately where needed", review);
        Assert.DoesNotContain("broader union" + " relations", text);
        Assert.DoesNotContain("HellVortex target fanout/recursive counter edges", text);
        Assert.DoesNotContain("HellVortex", review);
        Assert.DoesNotContain("CTF", review);
        Assert.DoesNotContain("team-qualifier", review);
        Assert.DoesNotContain("server-ownership", review);
        Assert.DoesNotContain("Constants.BlockAttackMap dynamic-ID edges", text);
        Assert.DoesNotContain("friends/allies/clans/unions", text);
        Assert.DoesNotContain("CS/server-ownership checks", text);
        Assert.DoesNotContain("Jiang flags", review);
        Assert.DoesNotContain("combat gear", review);
        Assert.DoesNotContain("Garment", review);
        Assert.DoesNotContain("ImmortalForce Overwhelm", review);
        Assert.DoesNotContain("Solidness", review);
        Assert.DoesNotContain("tournament", review);
        Assert.DoesNotContain("ChillingSnow side effects", text);
        Assert.DoesNotContain("ChillingSnow/FlashingName side effects", text);
    }

    private static string GetCanAttackPlayerReviewNote(string text)
    {
        var start = text.IndexOf("CanAttackPlayer relation-gate review", StringComparison.Ordinal);
        Assert.True(start >= 0, "Expected CanAttackPlayer relation-gate review text.");
        var npcStart = text.IndexOf("remaining CanAttackNpc source checks", start, StringComparison.Ordinal);
        if (npcStart > start)
        {
            return text[start..npcStart];
        }

        var end = text.IndexOf('.', start);
        return end < 0 ? text[start..] : text[start..end];
    }

    private static string GetRemainingCanAttackNpcGapSentence(string text)
    {
        var start = text.IndexOf("remaining CanAttackNpc source checks", StringComparison.Ordinal);
        Assert.True(start >= 0, "Expected remaining CanAttackNpc source checks text.");
        var end = text.IndexOf('.', start);
        return end < 0 ? text[start..] : text[start..end];
    }

    private static string GetRemainingKineticSparkUpdateBranchList(string text)
    {
        var start = text.IndexOf("remaining attacker-side MyCombatGear.Update branches", StringComparison.Ordinal);
        Assert.True(start >= 0, "Expected remaining attacker-side MyCombatGear.Update branches text.");
        var end = text.IndexOf("), remaining 10950", start, StringComparison.Ordinal);
        Assert.True(end >= 0, "Expected remaining KineticSpark Update branch list terminator.");

        return text[start..(end + 1)];
    }

    private static void AssertContainsRemainingCanAttackNpcGaps(string text)
    {
        var remaining = GetRemainingCanAttackNpcGapSentence(text);
        Assert.Contains("remaining CanAttackNpc source checks", remaining);
        Assert.Contains("exact CanAttackNpc side-effect behavior", remaining);
        Assert.Contains("caller-specific coverage", remaining);
        Assert.Contains("modeled shared player-magic SOB paths", remaining);
        Assert.DoesNotContain("later tournament/event-pole rules", remaining);
        Assert.DoesNotContain("large event-pole matrix", remaining);
        Assert.DoesNotContain("EmperorWar", remaining);
        Assert.DoesNotContain("MsgEmperorWar", remaining);
        Assert.DoesNotContain("10702", remaining);
        Assert.DoesNotContain("6505", remaining);
        Assert.DoesNotContain("TopPole", remaining);
        Assert.DoesNotContain("MsgTopPole", remaining);
        Assert.DoesNotContain("1737", remaining);
        Assert.DoesNotContain("65056", remaining);
        Assert.DoesNotContain("TrepilWar", remaining);
        Assert.DoesNotContain("MsgTrepilWar", remaining);
        Assert.DoesNotContain("3838", remaining);
        Assert.DoesNotContain("65046", remaining);
        Assert.DoesNotContain("elite", remaining);
        Assert.DoesNotContain("EliteGuildWar", remaining);
        Assert.DoesNotContain("MsgEliteGuildWar", remaining);
        Assert.DoesNotContain("nobility", remaining);
        Assert.DoesNotContain("NobilityPole", remaining);
        Assert.DoesNotContain("union", remaining);
        Assert.DoesNotContain("UnionWar", remaining);
        Assert.DoesNotContain("ChampionsOfWarr", remaining);
        Assert.DoesNotContain("Champions", remaining);
        Assert.DoesNotContain("4433", remaining);
        Assert.DoesNotContain("6546", remaining);
        Assert.DoesNotContain("6547", remaining);
        Assert.DoesNotContain("WarOfPlayers", remaining);
        Assert.DoesNotContain("MsgWarOfPlayers", remaining);
        Assert.DoesNotContain("6729", remaining);
        Assert.DoesNotContain("6606", remaining);
        Assert.DoesNotContain("6906", remaining);
        Assert.DoesNotContain("class-pole", remaining);
        Assert.DoesNotContain("class tournament", remaining);
        Assert.DoesNotContain("ClassPoleWar", remaining);
        Assert.DoesNotContain("ClassPolaWar", remaining);
        Assert.DoesNotContain("MsgWarriorGod", remaining);
        Assert.DoesNotContain("MsgArcherMaster", remaining);
        Assert.DoesNotContain("MsgNinjaMaster", remaining);
        Assert.DoesNotContain("MsgTrojanMaster", remaining);
        Assert.DoesNotContain("MsgTaoistMaster", remaining);
        Assert.DoesNotContain("MsgFireMaster", remaining);
        Assert.DoesNotContain("MsgPirateMaster", remaining);
        Assert.DoesNotContain("MsgKungfuKing", remaining);
        Assert.DoesNotContain("MsgWindWalker", remaining);
        Assert.DoesNotContain("MsgThnderStrike", remaining);
        Assert.DoesNotContain("MsgClassPolaWar", remaining);
        Assert.DoesNotContain("MsgMonkMaster", remaining);
        Assert.DoesNotContain("10005", remaining);
        Assert.DoesNotContain("8001", remaining);
        Assert.DoesNotContain("8002", remaining);
        Assert.DoesNotContain("9803", remaining);
        Assert.DoesNotContain("8005", remaining);
        Assert.DoesNotContain("8006", remaining);
        Assert.DoesNotContain("8007", remaining);
        Assert.DoesNotContain("8008", remaining);
        Assert.DoesNotContain("8009", remaining);
        Assert.DoesNotContain("9011", remaining);
        Assert.DoesNotContain("90111", remaining);
        Assert.DoesNotContain("8012", remaining);
        Assert.DoesNotContain("8004", remaining);
        Assert.DoesNotContain("WarFighters", remaining);
        Assert.DoesNotContain("MsgWarFighters", remaining);
        Assert.DoesNotContain("11073", remaining);
        Assert.DoesNotContain("9406", remaining);
        Assert.DoesNotContain("22213", remaining);
        Assert.DoesNotContain("SuperGuildWar", remaining);
        Assert.DoesNotContain("MsgSuperGuildWar", remaining);
        Assert.DoesNotContain("3868", remaining);
        Assert.DoesNotContain("820", remaining);
        Assert.DoesNotContain("25015", remaining);
        Assert.DoesNotContain("6591", remaining);
        Assert.DoesNotContain("8529", remaining);
        Assert.DoesNotContain("NEWGUILDWAR/event authorization", remaining);
        Assert.DoesNotContain("clan", remaining);
        Assert.DoesNotContain("ClanDomination", remaining);
        Assert.DoesNotContain("PoleDomination", remaining);
        Assert.DoesNotContain("guild-pole", remaining);
        Assert.DoesNotContain("GuildWarScore", remaining);
        Assert.DoesNotContain("6525", remaining);
        Assert.DoesNotContain("ClanWarScore", remaining);
        Assert.DoesNotContain("6526", remaining);
        Assert.DoesNotContain("3839", remaining);
        Assert.DoesNotContain("8448", remaining);
        Assert.DoesNotContain("6784", remaining);
        Assert.DoesNotContain("8447", remaining);
        Assert.DoesNotContain("statue PK", remaining);
        Assert.DoesNotContain("bare SOB gate call-site upgrades", remaining);
    }
}
