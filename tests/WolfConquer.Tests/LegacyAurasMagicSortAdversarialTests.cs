using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData(AuraTyrantSpellId, AuraTyrantFlag, (int)LegacyFlagIcon.TyrantAura)]
    [InlineData(AuraFeandSpellId, AuraFeandFlag, (int)LegacyFlagIcon.FeandAura)]
    [InlineData(AuraMetalSpellId, AuraMetalFlag, (int)LegacyFlagIcon.MetalAura)]
    [InlineData(AuraWoodSpellId, AuraWoodFlag, (int)LegacyFlagIcon.WoodAura)]
    [InlineData(AuraWaterSpellId, AuraWaterFlag, (int)LegacyFlagIcon.WaterAura)]
    [InlineData(AuraFireSpellId, AuraFireFlag, (int)LegacyFlagIcon.FireAura)]
    [InlineData(AuraEarthSpellId, AuraEarthFlag, (int)LegacyFlagIcon.EarthAura)]
    [InlineData(AuraMagicDefenderSpellId, AuraMagicDefenderFlag, 0)]
    public void CapabilityAcceptsOnlyReviewedAurasIds(
        ushort spellId,
        int statusFlag,
        int expectedIcon)
    {
        var info = LegacyMagicSortCapabilities.Classify(AurasMagicSort, spellId);

        Assert.Equal(LegacyMagicSortCapability.StateAndBuffRuntime, info.Capability);
        Assert.Equal(LegacyMagicModelingStatus.FullyModeled, info.Status);
        Assert.True(info.Supported);
        Assert.Equal("Auras(51)", info.SortName);

        var evidence = $"{info.Reason} {info.SourceEvidence}".ToLowerInvariant();
        Assert.Contains("auras.cs", evidence);
        Assert.Contains("player.addaura", evidence);
        Assert.Contains("threadpool.cs", evidence);
        Assert.Contains("d13", evidence);
        Assert.Contains($"0x{statusFlag:x}", evidence);
        if (expectedIcon != 0)
        {
            Assert.Contains("flagicon", evidence);
            Assert.Contains("2175", evidence);
        }

        Assert.DoesNotContain("7770", evidence);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(AuraTyrantSpellId - 1)]
    [InlineData(AuraTyrantSpellId + 1)]
    [InlineData(AuraFeandSpellId - 1)]
    [InlineData(AuraFeandSpellId + 1)]
    [InlineData(AuraMetalSpellId - 1)]
    [InlineData(AuraEarthSpellId + 1)]
    [InlineData(AuraMagicDefenderSpellId - 1)]
    [InlineData(AuraMagicDefenderSpellId + 1)]
    [InlineData(ushort.MaxValue)]
    public void CapabilityRejectsAdjacentAurasIds(ushort spellId)
    {
        var info = LegacyMagicSortCapabilities.Classify(AurasMagicSort, spellId);

        Assert.False(info.Supported);
        Assert.Equal(LegacyMagicModelingStatus.MissingRuntimeNotModeled, info.Status);
        Assert.Equal("Auras(51)", info.SortName);
    }

    [Theory]
    [InlineData(AuraTyrantSpellId)]
    [InlineData(AuraFeandSpellId)]
    [InlineData(AuraMetalSpellId)]
    [InlineData(AuraWoodSpellId)]
    [InlineData(AuraWaterSpellId)]
    [InlineData(AuraFireSpellId)]
    [InlineData(AuraEarthSpellId)]
    [InlineData(AuraMagicDefenderSpellId)]
    public void CapabilityDoesNotLeakAurasIdsIntoAdjacentSorts(ushort spellId)
    {
        Assert.False(LegacyMagicSortCapabilities.Classify((LegacyMagicSort)50, spellId).Supported);
        Assert.False(LegacyMagicSortCapabilities.Classify((LegacyMagicSort)52, spellId).Supported);
    }

    [Fact]
    public void AuraMagicDefender11200StaysSeparateFromAttachStatus30000AzureShield()
    {
        Assert.True(LegacyMagicSortCapabilities.Classify(AurasMagicSort, AuraMagicDefenderSpellId).Supported);
        Assert.False(LegacyMagicSortCapabilities.Classify(LegacyMagicSort.AttachStatus, AuraMagicDefenderSpellId).Supported);
        Assert.False(LegacyMagicSortCapabilities.Classify(AurasMagicSort, 30_000).Supported);
    }

    [Theory]
    [InlineData(AuraTyrantSpellId)]
    [InlineData(AuraFeandSpellId)]
    [InlineData(AuraMetalSpellId)]
    [InlineData(AuraWoodSpellId)]
    [InlineData(AuraWaterSpellId)]
    [InlineData(AuraFireSpellId)]
    [InlineData(AuraEarthSpellId)]
    public async Task OrdinaryAuraSelfCastMapsFlagStatIconLifetimeAndAnimationBeforeProgress(ushort spellId)
    {
        var descriptor = GetAuraDescriptor(spellId);
        using var result = await RunAuraCastAsync(spellId, PlayerUid);
        var caster = GetAuraCaster(result);

        AssertActiveAura(
            caster,
            descriptor,
            result.BeforeCast,
            result.AfterCast,
            AuraPrimaryDurationSeconds);
        AssertAuraStatusBonus(result.InitialCaster, caster, result.WorldData, descriptor);

        var icon = Assert.Single(ParseAuraFlagIcons(result.CasterSent));
        AssertAuraFlagIcon(icon, PlayerUid, descriptor.Icon);

        var animation = Assert.Single(ParseSpellAnimations(result.CasterSent));
        AssertOrdinaryAuraAnimation(animation, spellId, expectedTargets: []);
        var progress = Assert.Single(ParseLegacySpellPackets(result.CasterSent));
        Assert.Equal(spellId, progress.Id);
        Assert.Equal(AuraProgressDamage, progress.Experience);

        var statusIndex = AuraStatusFrameIndex(
            result.CasterSent,
            PlayerUid,
            values => IsLegacyStatusFlagSet(values, descriptor.StatusFlag));
        var iconIndex = AuraFlagIconFrameIndex(result.CasterSent, descriptor.Icon);
        var animationIndex = AuraAnimationFrameIndex(result.CasterSent, spellId);
        var progressIndex = AuraProgressFrameIndex(result.CasterSent, spellId);
        Assert.True(statusIndex < iconIndex);
        Assert.True(iconIndex < animationIndex);
        Assert.True(animationIndex < progressIndex);
    }

    [Fact]
    public async Task VisiblePrimaryGetsLongAuraButAnimationDoesNotQueuePrimaryAndIconGoesOnlyToOwner()
    {
        var descriptor = GetAuraDescriptor(AuraFireSpellId);
        var target = new AuraPeer(
            TargetPlayerUid,
            "PrimaryTarget",
            X: 832,
            Y: 584,
            MapId: 1_015,
            JoinTeam: false,
            Attributes: null);
        using var result = await RunAuraCastAsync(
            AuraFireSpellId,
            TargetPlayerUid,
            peers: [target]);
        var caster = GetAuraCaster(result);
        var primary = GetAuraPeer(result, TargetPlayerUid);

        AssertNoActiveAura(caster);
        AssertActiveAura(
            primary,
            descriptor,
            result.BeforeCast,
            result.AfterCast,
            AuraPrimaryDurationSeconds);
        AssertAuraStatusBonus(target.InitialSession!, primary, result.WorldData, descriptor);

        Assert.Empty(ParseAuraFlagIcons(result.CasterSent));
        var targetIcon = Assert.Single(ParseAuraFlagIcons(result.Peers[TargetPlayerUid].Sent));
        AssertAuraFlagIcon(targetIcon, TargetPlayerUid, descriptor.Icon);

        foreach (var sent in new[] { result.CasterSent, result.Peers[TargetPlayerUid].Sent })
        {
            AssertAuraStatusVector(sent, TargetPlayerUid, descriptor.StatusFlag, expectedSet: true);
            AssertOrdinaryAuraAnimation(
                Assert.Single(ParseSpellAnimations(sent)),
                AuraFireSpellId,
                expectedTargets: []);
        }

        Assert.Single(ParseLegacySpellPackets(result.CasterSent));
        Assert.Empty(ParseLegacySpellPackets(result.Peers[TargetPlayerUid].Sent));
    }

    [Fact]
    public async Task InvalidPrimaryStillSendsEmptySelfAnimationWithoutAuraIconOrProgress()
    {
        using var result = await RunAuraCastAsync(AuraWaterSpellId, AuraUnknownTargetUid);
        var caster = GetAuraCaster(result);

        AssertNoActiveAura(caster);
        Assert.Empty(ParseAuraFlagIcons(result.CasterSent));
        Assert.Empty(ParseLegacySpellPackets(result.CasterSent));
        Assert.DoesNotContain(
            ParseMsgUpdates(result.CasterSent),
            packet => packet.Updates.Any(entry =>
                entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                entry.Values is not null &&
                AuraStatusFlags.Any(flag => IsLegacyStatusFlagSet(entry.Values, flag))));
        AssertOrdinaryAuraAnimation(
            Assert.Single(ParseSpellAnimations(result.CasterSent)),
            AuraWaterSpellId,
            expectedTargets: []);
    }

    [Fact]
    public async Task InvalidPrimaryStillFansOutAcrossMapsAndAlienWorldWithStrictDistanceAndCurrentAuraGates()
    {
        var now = DateTimeOffset.UtcNow;
        var casting = GetAuraDescriptor(AuraTyrantSpellId);
        var prior = GetAuraDescriptor(AuraFeandSpellId);
        var sameExpiresAt = now.AddHours(1);
        var peers = new[]
        {
            new AuraPeer(
                TargetPlayerUid,
                "CrossMap",
                X: 831,
                Y: 584,
                MapId: 1_002,
                JoinTeam: true,
                Attributes: null),
            new AuraPeer(
                TargetPlayerUid + 1,
                "AlienWorld",
                X: 832,
                Y: 584,
                MapId: 10_428,
                JoinTeam: true,
                Attributes: CreateActiveAuraAttributes(prior, sameExpiresAt)),
            new AuraPeer(
                TargetPlayerUid + 2,
                "Boundary",
                X: 848,
                Y: 584,
                MapId: 1_015,
                JoinTeam: true,
                Attributes: null),
            new AuraPeer(
                TargetPlayerUid + 3,
                "SameAura",
                X: 833,
                Y: 584,
                MapId: 1_015,
                JoinTeam: true,
                Attributes: CreateActiveAuraAttributes(casting, sameExpiresAt)),
        };
        using var result = await RunAuraCastAsync(
            AuraTyrantSpellId,
            AuraUnknownTargetUid,
            peers: peers,
            createTeam: true);

        AssertNoActiveAura(GetAuraCaster(result));
        foreach (var uid in new[] { TargetPlayerUid, TargetPlayerUid + 1 })
        {
            var member = GetAuraPeer(result, uid);
            AssertActiveAura(
                member,
                casting,
                result.BeforeCast,
                result.AfterCast,
                AuraTeamDurationSeconds);
            var icon = Assert.Single(ParseAuraFlagIcons(result.Peers[uid].Sent));
            AssertAuraFlagIcon(icon, uid, casting.Icon);
        }

        var alien = GetAuraPeer(result, TargetPlayerUid + 1);
        Assert.False(HasLegacyStatusFlag(alien.Character.Attributes, prior.StatusFlag));
        AssertAuraStatusBonus(peers[1].InitialSession!, alien, result.WorldData, casting, prior);

        AssertNoActiveAura(GetAuraPeer(result, TargetPlayerUid + 2));
        var sameAura = GetAuraPeer(result, TargetPlayerUid + 3);
        Assert.True(HasLegacyStatusFlag(sameAura.Character.Attributes, casting.StatusFlag));
        Assert.Equal(
            sameExpiresAt.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            sameAura.Character.Attributes[RuntimeAuraExpiresAtUtcAttribute]);
        Assert.Empty(ParseAuraFlagIcons(result.CasterSent));
        Assert.Empty(ParseAuraFlagIcons(result.Peers[TargetPlayerUid + 2].Sent));
        Assert.Empty(ParseAuraFlagIcons(result.Peers[TargetPlayerUid + 3].Sent));
        Assert.Empty(ParseLegacySpellPackets(result.CasterSent));
        AssertOrdinaryAuraAnimation(
            Assert.Single(ParseSpellAnimations(result.CasterSent)),
            AuraTyrantSpellId,
            expectedTargets: [TargetPlayerUid, TargetPlayerUid + 1]);
    }

    [Fact]
    public async Task SameAuraPrimaryTeammateTogglesThenTeamFanoutReappliesShortAuraWithoutProgress()
    {
        var descriptor = GetAuraDescriptor(AuraMetalSpellId);
        var priorExpiry = DateTimeOffset.UtcNow.AddHours(1);
        var peerAttributes = CreateActiveAuraAttributes(descriptor, priorExpiry);
        peerAttributes["StatusFlags"] = BuildStatusFlags(descriptor.StatusFlag, AuraDeadFlag);
        var peer = new AuraPeer(
            TargetPlayerUid,
            "PrimaryTeammate",
            X: 831,
            Y: 584,
            MapId: 1_015,
            JoinTeam: true,
            Attributes: peerAttributes);

        using var result = await RunAuraCastAsync(
            AuraMetalSpellId,
            TargetPlayerUid,
            peers: [peer],
            createTeam: true);
        var teammate = GetAuraPeer(result, TargetPlayerUid);

        AssertNoActiveAura(GetAuraCaster(result));
        Assert.True(HasLegacyStatusFlag(teammate.Character.Attributes, AuraDeadFlag));
        AssertActiveAura(
            teammate,
            descriptor,
            result.BeforeCast,
            result.AfterCast,
            AuraTeamDurationSeconds);
        Assert.NotEqual(
            priorExpiry.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            teammate.Character.Attributes[RuntimeAuraExpiresAtUtcAttribute]);
        AssertAuraFlagIcon(
            Assert.Single(ParseAuraFlagIcons(result.Peers[TargetPlayerUid].Sent)),
            TargetPlayerUid,
            descriptor.Icon);
        Assert.Empty(ParseLegacySpellPackets(result.CasterSent));
        AssertOrdinaryAuraAnimation(
            Assert.Single(ParseSpellAnimations(result.CasterSent)),
            AuraMetalSpellId,
            expectedTargets: [TargetPlayerUid]);

        var statusPackets = ParseMsgUpdates(result.Peers[TargetPlayerUid].Sent)
            .Where(packet => packet.Uid == TargetPlayerUid &&
                packet.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag))
            .ToArray();
        Assert.Equal(2, statusPackets.Length);
        Assert.False(IsLegacyStatusFlagSet(Assert.Single(statusPackets[0].Updates).Values!, descriptor.StatusFlag));
        Assert.True(IsLegacyStatusFlagSet(Assert.Single(statusPackets[1].Updates).Values!, descriptor.StatusFlag));
    }

    [Fact]
    public async Task SamePrimaryAuraTogglesOffWithOnlyRemovalNoIconAndNoProgress()
    {
        var descriptor = GetAuraDescriptor(AuraMetalSpellId);
        using var result = await RunAuraCastAsync(AuraMetalSpellId, PlayerUid);
        var baseline = result.InitialCaster;
        ClearAuraSent(result);

        await RepeatAuraCastAsync(result, AuraMetalSpellId, PlayerUid);
        var toggled = GetAuraCaster(result);

        AssertNoActiveAura(toggled);
        AssertAuraStatusBonus(baseline, toggled, result.WorldData, expected: null);
        Assert.Empty(ParseAuraFlagIcons(result.CasterSent));
        Assert.Empty(ParseLegacySpellPackets(result.CasterSent));
        AssertOrdinaryAuraAnimation(
            Assert.Single(ParseSpellAnimations(result.CasterSent)),
            AuraMetalSpellId,
            expectedTargets: []);

        var statusPackets = ParseMsgUpdates(result.CasterSent)
            .Where(packet => packet.Uid == PlayerUid &&
                packet.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag))
            .ToArray();
        var removal = Assert.Single(statusPackets);
        var values = Assert.Single(removal.Updates).Values;
        Assert.NotNull(values);
        Assert.False(IsLegacyStatusFlagSet(values, descriptor.StatusFlag));
        Assert.True(AuraStatusFrameIndex(
                result.CasterSent,
                PlayerUid,
                vector => !IsLegacyStatusFlagSet(vector, descriptor.StatusFlag)) <
            AuraAnimationFrameIndex(result.CasterSent, AuraMetalSpellId));
    }

    [Fact]
    public async Task DifferentPrimaryAuraRemovesOldVectorAndStatBeforeAddingNewMetadata()
    {
        var oldAura = GetAuraDescriptor(AuraTyrantSpellId);
        var newAura = GetAuraDescriptor(AuraFeandSpellId);
        using var result = await RunAuraCastAsync(
            AuraTyrantSpellId,
            PlayerUid,
            learnedSpellIds: [AuraTyrantSpellId, AuraFeandSpellId]);
        var baseline = result.InitialCaster;
        ClearAuraSent(result);

        var repeat = await RepeatAuraCastAsync(result, AuraFeandSpellId, PlayerUid);
        var replaced = GetAuraCaster(result);

        Assert.False(HasLegacyStatusFlag(replaced.Character.Attributes, oldAura.StatusFlag));
        AssertActiveAura(
            replaced,
            newAura,
            repeat.BeforeCast,
            repeat.AfterCast,
            AuraPrimaryDurationSeconds);
        AssertAuraStatusBonus(baseline, replaced, result.WorldData, newAura);

        var oldRemovalIndex = AuraStatusFrameIndex(
            result.CasterSent,
            PlayerUid,
            values => !IsLegacyStatusFlagSet(values, oldAura.StatusFlag) &&
                !IsLegacyStatusFlagSet(values, newAura.StatusFlag));
        var newAddIndex = AuraStatusFrameIndex(
            result.CasterSent,
            PlayerUid,
            values => !IsLegacyStatusFlagSet(values, oldAura.StatusFlag) &&
                IsLegacyStatusFlagSet(values, newAura.StatusFlag),
            oldRemovalIndex + 1);
        var iconIndex = AuraFlagIconFrameIndex(result.CasterSent, newAura.Icon);
        var animationIndex = AuraAnimationFrameIndex(result.CasterSent, AuraFeandSpellId);
        var progressIndex = AuraProgressFrameIndex(result.CasterSent, AuraFeandSpellId);

        Assert.True(oldRemovalIndex < newAddIndex);
        Assert.True(newAddIndex < iconIndex);
        Assert.True(iconIndex < animationIndex);
        Assert.True(animationIndex < progressIndex);
        Assert.DoesNotContain(ParseAuraFlagIcons(result.CasterSent), icon => icon.Icon == oldAura.Icon);
    }

    [Fact]
    public async Task Aura11200RemovesRideThenTogglesFlag80WithAzureUpdateAnimationAndProgressOrder()
    {
        var rideExpires = DateTimeOffset.UtcNow.AddMinutes(5);
        var casterAttributes = new Dictionary<string, string>
        {
            ["StatusFlags"] = BuildStatusFlags(AuraRideFlag),
            [RuntimeAuraRideExpiresAtUtcAttribute] = rideExpires.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            [RuntimeAuraRideRemoveOnDeadAttribute] = bool.TrueString,
            [RuntimeAuraRideStampSecondsAttribute] = "0",
        };
        var observer = new AuraPeer(
            TargetPlayerUid,
            "Observer",
            X: 832,
            Y: 584,
            MapId: 1_015,
            JoinTeam: false,
            Attributes: null);
        using var result = await RunAuraCastAsync(
            AuraMagicDefenderSpellId,
            PlayerUid,
            casterAttributes: casterAttributes,
            peers: [observer]);
        var caster = GetAuraCaster(result);

        Assert.False(HasLegacyStatusFlag(caster.Character.Attributes, AuraRideFlag));
        Assert.True(HasLegacyStatusFlag(caster.Character.Attributes, AuraMagicDefenderFlag));
        Assert.False(HasLegacyStatusFlag(caster.Character.Attributes, AttachStatusAzureShieldFlag));
        Assert.False(caster.Character.Attributes.ContainsKey(RuntimeAuraRideExpiresAtUtcAttribute));
        Assert.False(caster.Character.Attributes.ContainsKey(RuntimeAuraRideRemoveOnDeadAttribute));
        Assert.False(caster.Character.Attributes.ContainsKey(RuntimeAuraRideStampSecondsAttribute));
        AssertAuraMagicDefenderMetadata(
            caster,
            result.BeforeCast,
            result.AfterCast,
            AuraMagicDefenderDurationSeconds);
        AssertNoAttachStatus30000Metadata(caster);
        Assert.Empty(ParseAuraFlagIcons(result.CasterSent));

        var animation = Assert.Single(ParseSpellAnimations(result.CasterSent));
        AssertAuraMagicDefenderAnimation(animation);
        Assert.Single(ParseLegacySpellPackets(result.CasterSent));
        var rideRemovalIndex = AuraStatusFrameIndex(
            result.CasterSent,
            PlayerUid,
            values => !IsLegacyStatusFlagSet(values, AuraRideFlag) &&
                !IsLegacyStatusFlagSet(values, AuraMagicDefenderFlag));
        var defenderAddIndex = AuraStatusFrameIndex(
            result.CasterSent,
            PlayerUid,
            values => !IsLegacyStatusFlagSet(values, AuraRideFlag) &&
                IsLegacyStatusFlagSet(values, AuraMagicDefenderFlag),
            rideRemovalIndex + 1);
        var azureIndex = AuraAzureShieldFrameIndex(
            result.CasterSent,
            expectedTime: AuraMagicDefenderDurationSeconds,
            expectedSpellLevel: AuraLearnedLevel);
        var animationIndex = AuraAnimationFrameIndex(result.CasterSent, AuraMagicDefenderSpellId);
        var progressIndex = AuraProgressFrameIndex(result.CasterSent, AuraMagicDefenderSpellId);
        Assert.True(rideRemovalIndex < defenderAddIndex);
        Assert.True(defenderAddIndex < azureIndex);
        Assert.True(azureIndex < animationIndex);
        Assert.True(animationIndex < progressIndex);

        AssertAuraStatusVector(result.Peers[TargetPlayerUid].Sent, PlayerUid, AuraRideFlag, expectedSet: false);
        AssertAuraStatusVector(result.Peers[TargetPlayerUid].Sent, PlayerUid, AuraMagicDefenderFlag, expectedSet: true);
        AuraAzureShieldFrameIndex(
            result.Peers[TargetPlayerUid].Sent,
            expectedTime: AuraMagicDefenderDurationSeconds,
            expectedSpellLevel: AuraLearnedLevel);

        ClearAuraSent(result);
        await RepeatAuraCastAsync(result, AuraMagicDefenderSpellId, PlayerUid);
        var toggled = GetAuraCaster(result);
        Assert.False(HasLegacyStatusFlag(toggled.Character.Attributes, AuraMagicDefenderFlag));
        Assert.False(HasLegacyStatusFlag(toggled.Character.Attributes, AttachStatusAzureShieldFlag));
        AssertNoAuraMagicDefenderMetadata(toggled);
        AssertNoAttachStatus30000Metadata(toggled);
        Assert.Empty(ParseAuraFlagIcons(result.CasterSent));
        AssertAuraMagicDefenderAnimation(Assert.Single(ParseSpellAnimations(result.CasterSent)));
        Assert.Single(ParseLegacySpellPackets(result.CasterSent));

        var defenderRemovalIndex = AuraStatusFrameIndex(
            result.CasterSent,
            PlayerUid,
            values => !IsLegacyStatusFlagSet(values, AuraMagicDefenderFlag));
        var azureResetIndex = AuraAzureShieldFrameIndex(
            result.CasterSent,
            expectedTime: 0,
            expectedSpellLevel: AuraLearnedLevel);
        animationIndex = AuraAnimationFrameIndex(result.CasterSent, AuraMagicDefenderSpellId);
        progressIndex = AuraProgressFrameIndex(result.CasterSent, AuraMagicDefenderSpellId);
        Assert.True(defenderRemovalIndex < azureResetIndex);
        Assert.True(azureResetIndex < animationIndex);
        Assert.True(animationIndex < progressIndex);
    }

    [Theory]
    [InlineData(AuraTyrantSpellId - 1)]
    [InlineData(AuraFeandSpellId + 1)]
    [InlineData(AuraEarthSpellId + 1)]
    [InlineData(AuraMagicDefenderSpellId + 1)]
    public async Task DispatchRejectsAdjacentAurasRowsAfterPreparingResourcesAndCooldownWithoutAuraOutput(ushort spellId)
    {
        using var result = await RunAuraCastAsync(
            spellId,
            PlayerUid,
            minMana: 500,
            casterStamina: 100,
            useMana: 7,
            useStamina: 5,
            coldTime: 300);
        var after = GetAuraCaster(result);

        Assert.Equal(493, after.Character.MinMana);
        Assert.Equal("95", after.Character.Attributes["Stamina"]);
        Assert.True(after.MagicCooldowns?.ContainsKey(spellId));
        Assert.Equal(result.InitialCaster.PlayerSpells.Spells, after.PlayerSpells.Spells);
        AssertNoActiveAura(after);
        Assert.False(HasLegacyStatusFlag(after.Character.Attributes, AuraMagicDefenderFlag));
        AssertNoAuraMagicDefenderMetadata(after);

        var staminaIndex = AuraFrameIndex(
            result.CasterSent,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                update.Uid == PlayerUid &&
                update.Updates.SequenceEqual(
                    [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Stamina, 95)]));
        var manaIndex = AuraFrameIndex(
            result.CasterSent,
            frame => LegacyMsgUpdatePacket.TryParse(frame, out var update) &&
                update.Uid == PlayerUid &&
                update.Updates.SequenceEqual(
                    [new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Mana, 493)]));
        var cooldownIndex = AuraFrameIndex(
            result.CasterSent,
            frame => LegacyMagicColdTimePacket.TryParse(frame, out var packet) &&
                packet.Spells.SequenceEqual([new LegacyMagicColdTimeSpell(spellId, 300)]));
        Assert.True(staminaIndex < manaIndex);
        Assert.True(manaIndex < cooldownIndex);

        Assert.DoesNotContain(
            ParseMsgUpdates(result.CasterSent).SelectMany(packet => packet.Updates),
            entry => entry.Type is LegacyMsgUpdateDataType.StatusFlag or LegacyMsgUpdateDataType.AzureShield);
        Assert.Empty(ParseAuraFlagIcons(result.CasterSent));
        Assert.Empty(ParseSpellAnimations(result.CasterSent));
        Assert.Empty(ParseLegacySpellPackets(result.CasterSent));
        Assert.Contains("unsupported", result.Log.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"spellId={spellId}", result.Log.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(AuraTyrantSpellId)]
    [InlineData(AuraMagicDefenderSpellId)]
    public async Task InsufficientAuraManaRejectsWithoutAnyMutation(ushort spellId)
    {
        using var result = await RunAuraCastAsync(
            spellId,
            PlayerUid,
            minMana: 99,
            useMana: 100,
            useStamina: 0);
        var after = GetAuraCaster(result);

        AssertAuraSessionUnchanged(result.InitialCaster, after);
        Assert.Empty(result.CasterSent);
        Assert.Contains("requiredMana=100", result.Log.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(AuraTyrantSpellId)]
    [InlineData(AuraMagicDefenderSpellId)]
    public async Task InsufficientAuraStaminaRejectsWithoutAnyMutation(ushort spellId)
    {
        using var result = await RunAuraCastAsync(
            spellId,
            PlayerUid,
            casterStamina: 9,
            useMana: 0,
            useStamina: 10);
        var after = GetAuraCaster(result);

        AssertAuraSessionUnchanged(result.InitialCaster, after);
        Assert.Empty(result.CasterSent);
        Assert.Contains("requiredStamina=10", result.Log.ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(AuraTyrantSpellId)]
    [InlineData(AuraMagicDefenderSpellId)]
    public async Task ActiveAuraCooldownRejectsWithoutAnyMutation(ushort spellId)
    {
        var readyAt = DateTimeOffset.UtcNow.AddMinutes(1);
        using var result = await RunAuraCastAsync(
            spellId,
            PlayerUid,
            useMana: 0,
            useStamina: 0,
            coldTime: 60_000,
            magicCooldowns: new Dictionary<ushort, DateTimeOffset>
            {
                [spellId] = readyAt,
            });
        var after = GetAuraCaster(result);

        AssertAuraSessionUnchanged(result.InitialCaster, after);
        Assert.Empty(result.CasterSent);
        Assert.Contains("cooldownRemainingMs=", result.Log.ToString(), StringComparison.Ordinal);
    }
}
