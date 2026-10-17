using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    [Theory]
    [InlineData((ushort)1_000, 100L, 300U, 360L)]
    [InlineData((ushort)120, 100L, 20U, 80L)]
    [InlineData((ushort)120, 120L, 0U, 80L)]
    [InlineData((ushort)120, 150L, 0U, 110L)]
    public async Task RootManualMpUsesEarnedMaximumAndAlwaysPublishesSetterBeforeOriginalCosts(
        ushort maximum, long currentMana, uint restored, long finalMana)
    {
        await using var fixture = new RootMagicEntryFixture(mana: currentMana,
            itemEffects: new(MaximumMana: maximum));
        await fixture.QueryRootItemEffectsAsync();
        Assert.Equal((uint)maximum, LegacyPhysicalStatusOverlay.ForOwner(fixture.Owner).MaxMana);
        Assert.Equal(202U, LegacyPhysicalStatusOverlay.ForOwner(fixture.Owner).RightWeaponItemEffect);
        Assert.Equal(currentMana, fixture.Owner.Character.MinMana);
        Assert.DoesNotContain(fixture.Owner.PlayerSpells.Spells, entry => entry.Id == 1_175);
        var start = fixture.Order.Count;
        await fixture.CastAsync(1_000);
        Assert.Equal(finalMana, fixture.Owner.Character.MinMana);
        Assert.Equal((ushort)1_175, fixture.Owner.LegacyRandomSpell);
        Assert.Equal(new[]
        {
            (LegacyMsgUpdateDataType.Mana, checked((ulong)(currentMana + restored))),
            (LegacyMsgUpdateDataType.Stamina, 90UL),
            (LegacyMsgUpdateDataType.Mana, checked((ulong)finalMana)),
        }, RootMagicEntryCosts(fixture.FramesSince(PlayerUid, start)));
        Assert.Empty(RootMagicEntryCosts(fixture.FramesSince(TargetPlayerUid, start)));
        foreach (var recipient in new[] { PlayerUid, TargetPlayerUid })
            AssertRootManualPrefixAnimation(Assert.Single(ParseSpellAnimations(fixture.FramesSince(recipient, start)),
                packet => packet.SpellId == 1_175), 1_175, 0, 830, 584, restored);
        Assert.Single(ParseSpellAnimations(fixture.FramesSince(PlayerUid, start)), packet => packet.SpellId == 1_000);
    }

    [Fact]
    public async Task RootManualMpSourceFiftyBoundaryRejectsAtFiftyWithoutSideEffectBook()
    {
        await using var fixture = new RootMagicEntryFixture(mana: 100, itemEffects: new(MpRoll: 50));
        await fixture.QueryRootItemEffectsAsync();
        var start = fixture.Order.Count;
        await fixture.CastAsync(1_000);
        Assert.Equal(60L, fixture.Owner.Character.MinMana);
        Assert.Equal((ushort)0, fixture.Owner.LegacyRandomSpell);
        fixture.AssertCostPairSince(start, 90, 60);
        foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
            Assert.DoesNotContain(ParseSpellAnimations(fixture.FramesSince(uid, start)), packet => packet.SpellId == 1_175);
        Assert.DoesNotContain(fixture.Owner.PlayerSpells.Spells, entry => entry.Id == 1_175);
    }

    [Theory]
    [InlineData(19U, 19U, true, true)]
    [InlineData(20U, 19U, false, true)]
    [InlineData(19U, 20U, true, false)]
    [InlineData(20U, 20U, false, false)]
    public async Task RootManualRingAndNeckUseAnyNonzeroCachedEffectAndNativeRateBoundary(
        uint ringRoll, uint neckRoll, bool ring, bool neck)
    {
        await using var fixture = new RootMagicEntryFixture(itemEffects:
            new(RightEffect: 0, RingEffect: 1, NeckEffect: 200, RingRoll: ringRoll, NeckRoll: neckRoll));
        await fixture.QueryRootItemEffectsAsync();
        Assert.Equal(1U, LegacyPhysicalStatusOverlay.ForOwner(fixture.Owner).RingItemEffect);
        Assert.Equal(200U, LegacyPhysicalStatusOverlay.ForOwner(fixture.Owner).NecklaceItemEffect);
        Assert.DoesNotContain(fixture.Owner.PlayerSpells.Spells, entry => entry.Id is 1_095 or 1_020);
        var start = fixture.Order.Count;
        await fixture.CastAsync(1_000);
        Assert.Equal(ring, HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 9));
        Assert.Equal(neck, HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 8));
        fixture.AssertCostPairSince(start, 90, 960);
        foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
        {
            var animations = ParseSpellAnimations(fixture.FramesSince(uid, start));
            Assert.Equal(ring ? 1 : 0, animations.Count(packet => packet.SpellId == 1_095));
            Assert.Equal(neck ? 1 : 0, animations.Count(packet => packet.SpellId == 1_020));
            foreach (var packet in animations.Where(packet => packet.SpellId is 1_095 or 1_020))
                AssertRootManualPrefixAnimation(packet, packet.SpellId, 4, 830, 584, 0);
        }
    }

    [Fact]
    public async Task RootManualWholeItemPrefixFinishesOwnerAndPeerPhasesBeforeOriginalCosts()
    {
        await using var fixture = new RootMagicEntryFixture(mana: 100,
            itemEffects: new(RingEffect: 1, NeckEffect: 200));
        await fixture.QueryRootItemEffectsAsync();
        var start = fixture.Order.Count;
        await fixture.CastAsync(1_000);
        Assert.Equal(new[]
        {
            $"{PlayerUid}:mana:400", $"{PlayerUid}:spell:1175", $"{TargetPlayerUid}:spell:1175",
            $"{PlayerUid}:bits:512", $"{TargetPlayerUid}:bits:512",
            $"{PlayerUid}:spell:1095", $"{TargetPlayerUid}:spell:1095",
            $"{PlayerUid}:bits:768", $"{TargetPlayerUid}:bits:768",
            $"{PlayerUid}:spell:1020", $"{TargetPlayerUid}:spell:1020",
            $"{PlayerUid}:stamina:90", $"{PlayerUid}:mana:360",
        }, RootManualItemPrefixTrace(fixture.Order.ToArray().Skip(start)));
        Assert.Equal(360L, fixture.Owner.Character.MinMana);
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 9));
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 8));
    }

    [Fact]
    public async Task RootManualMpCallbackUsesFreshQueriedRingButNativeRetainedNeck()
    {
        var armed = false;
        var entered = false;
        await using var fixture = new RootMagicEntryFixture(mana: 100,
            itemEffects: new(RingEffect: 1, NeckEffect: 200), onSend: async (current, uid, bytes) =>
            {
                if (!armed || entered || uid != PlayerUid || !RootManualItemScalar(bytes, LegacyMsgUpdateDataType.Mana, 400)) return;
                entered = true;
                await current.UnequipRootItemEffectAsync(6);
                await current.UnequipRootItemEffectAsync(2);
                Assert.Equal(0U, LegacyPhysicalStatusOverlay.ForOwner(current.Owner).RingItemEffect);
                Assert.Equal(200U, LegacyPhysicalStatusOverlay.ForOwner(current.Owner).NecklaceItemEffect);
            });
        await fixture.QueryRootItemEffectsAsync();
        var start = fixture.Order.Count;
        armed = true;
        await fixture.CastAsync(1_000);
        armed = false;
        Assert.True(entered);
        Assert.DoesNotContain(fixture.Owner.PlayerItems.EquipmentItems, item => item.Position is 6 or 2);
        Assert.False(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 9));
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 8));
        Assert.Equal(360L, fixture.Owner.Character.MinMana);
        foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
        {
            var animations = ParseSpellAnimations(fixture.FramesSince(uid, start));
            Assert.Single(animations, packet => packet.SpellId == 1_175);
            Assert.DoesNotContain(animations, packet => packet.SpellId == 1_095);
            Assert.Single(animations, packet => packet.SpellId == 1_020);
        }
    }

    [Fact]
    public async Task RootManualMpAndRingCaptureFramesBeforeTheirAwaitWhileNextPhaseUsesCurrentPosition()
    {
        var armed = false;
        var movedAtMana = false;
        var movedAtRing = false;
        await using var fixture = new RootMagicEntryFixture(mana: 100,
            itemEffects: new(RingEffect: 1, NeckEffect: 200), onSend: (current, uid, bytes) =>
            {
                if (!armed || uid != PlayerUid) return ValueTask.CompletedTask;
                if (!movedAtMana && RootManualItemScalar(bytes, LegacyMsgUpdateDataType.Mana, 400))
                {
                    movedAtMana = true;
                    current.MoveRootItemEffectOwner(831, 585);
                }
                else if (!movedAtRing && RootManualItemBits(bytes) == 512)
                {
                    movedAtRing = true;
                    current.MoveRootItemEffectOwner(833, 586);
                }
                return ValueTask.CompletedTask;
            });
        await fixture.QueryRootItemEffectsAsync();
        var start = fixture.Order.Count;
        armed = true;
        await fixture.CastAsync(1_000);
        armed = false;
        Assert.True(movedAtMana && movedAtRing);
        Assert.Equal(((ushort)833, (ushort)586), (fixture.Owner.Character.X, fixture.Owner.Character.Y));
        foreach (var uid in new[] { PlayerUid, TargetPlayerUid })
        {
            var animations = ParseSpellAnimations(fixture.FramesSince(uid, start));
            AssertRootManualPrefixAnimation(Assert.Single(animations, packet => packet.SpellId == 1_175), 1_175, 0, 830, 584, 300);
            AssertRootManualPrefixAnimation(Assert.Single(animations, packet => packet.SpellId == 1_095), 1_095, 4, 831, 585, 0);
            AssertRootManualPrefixAnimation(Assert.Single(animations, packet => packet.SpellId == 1_020), 1_020, 4, 833, 586, 0);
        }
        Assert.Equal(360L, fixture.Owner.Character.MinMana);
    }

    [Theory]
    [InlineData("range")]
    [InlineData("stamina")]
    public async Task RootManualItemPrefixRetainsEarlierEffectsWhenLaterNativeAdmissionFails(string denial)
    {
        await using var fixture = new RootMagicEntryFixture(mana: 100, stamina: denial == "stamina" ? 9UL : 100UL,
            itemEffects: new(RingEffect: 1, NeckEffect: 200));
        await fixture.QueryRootItemEffectsAsync();
        var start = fixture.Order.Count;
        await fixture.CastAsync(1_000, target: denial == "range" ? 9_999_998U : TargetPlayerUid);
        fixture.AssertProducedStamp(fixture.Now);
        Assert.Equal(400L, fixture.Owner.Character.MinMana);
        Assert.Equal(denial == "stamina" ? "9" : "100", fixture.Owner.Character.Attributes["Stamina"]);
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 9));
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 8));
        Assert.Equal(new[] { (LegacyMsgUpdateDataType.Mana, 400UL) },
            RootMagicEntryCosts(fixture.FramesSince(PlayerUid, start)));
        Assert.Equal(100_000L, fixture.Peer.Character.MinHitPoints);
        var animations = ParseSpellAnimations(fixture.FramesSince(PlayerUid, start));
        Assert.Equal(new ushort[] { 1_175, 1_095, 1_020 }, animations.Select(packet => packet.SpellId).ToArray());
        Assert.DoesNotContain(animations, packet => packet.SpellId == 1_000);
    }

    [Theory]
    [InlineData("passive")]
    [InlineData("book")]
    [InlineData("cadence")]
    public async Task RootManualEarlierNativeRootDenialCannotPublishNewItemPrefix(string denial)
    {
        await using var fixture = new RootMagicEntryFixture(mana: 100, passive: denial == "passive",
            omitSelectedLearnedInput: denial == "book", itemEffects: new(RingEffect: 1, NeckEffect: 200));
        await fixture.QueryRootItemEffectsAsync();
        if (denial == "cadence") await fixture.CastAsync(1_000);
        var before = fixture.Owner;
        var start = fixture.Order.Count;
        await fixture.CastAsync(1_000);
        Assert.Empty(RootMagicEntryCosts(fixture.FramesSince(PlayerUid, start)));
        Assert.Empty(ParseSpellAnimations(fixture.FramesSince(PlayerUid, start)));
        Assert.Equal(before.Character.MinMana, fixture.Owner.Character.MinMana);
        Assert.Equal(before.LegacyRandomSpell, fixture.Owner.LegacyRandomSpell);
        Assert.Equal(HasLegacyStatusFlag(before.Character.Attributes, 9), HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 9));
        Assert.Equal(HasLegacyStatusFlag(before.Character.Attributes, 8), HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 8));
    }

    [Theory]
    [InlineData("mana", false)]
    [InlineData("mana", true)]
    [InlineData("stigma-vector", false)]
    [InlineData("stigma-vector", true)]
    [InlineData("shield-animation", false)]
    [InlineData("shield-animation", true)]
    public async Task RootManualItemPrefixOriginalOwnerLossStopsAfterEnteredFrameWithoutReplacementMutation(
        string boundary, bool rebind)
    {
        var armed = false;
        var entered = false;
        var boundaryIndex = -1;
        WorldSession? replacement = null;
        await using var fixture = new RootMagicEntryFixture(mana: 100,
            itemEffects: new(RingEffect: 1, NeckEffect: 200), onSend: (current, uid, bytes) =>
            {
                if (!armed || entered || uid != PlayerUid) return ValueTask.CompletedTask;
                var hit = boundary switch
                {
                    "mana" => RootManualItemScalar(bytes, LegacyMsgUpdateDataType.Mana, 400),
                    "stigma-vector" => RootManualItemBits(bytes) == 512,
                    _ => LegacySpellAnimationPacket.TryParse(ReadFrame(bytes), out var packet) && packet.SpellId == 1_020,
                };
                if (!hit) return ValueTask.CompletedTask;
                entered = true;
                boundaryIndex = current.Order.Count - 1;
                replacement = current.RetireRootItemEffectSubject(PlayerUid, rebind);
                return ValueTask.CompletedTask;
            });
        await fixture.QueryRootItemEffectsAsync();
        var start = fixture.Order.Count;
        armed = true;
        await fixture.CastAsync(1_000);
        armed = false;
        Assert.True(entered && boundaryIndex >= start);
        Assert.Empty(fixture.Order.ToArray().Skip(boundaryIndex + 1));
        Assert.Equal(100_000L, fixture.Peer.Character.MinHitPoints);
        if (rebind)
        {
            Assert.Same(replacement, fixture.RootItemEffectSlot(PlayerUid));
            Assert.Equal((long)PlayerUid + 9_000, Assert.IsType<WorldSession>(replacement).Character.Uid);
        }
        else Assert.Null(fixture.RootItemEffectSlot(PlayerUid));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RootManualUnrelatedTargetLossStillOwesOwnItemPrefixBeforeOriginalRangeRejection(bool rebind)
    {
        var armed = false;
        var entered = false;
        WorldSession? replacement = null;
        await using var fixture = new RootMagicEntryFixture(mana: 100,
            itemEffects: new(RingEffect: 1, NeckEffect: 200), onSend: (current, uid, bytes) =>
            {
                if (!armed || entered || uid != PlayerUid || !RootManualItemScalar(bytes, LegacyMsgUpdateDataType.Mana, 400))
                    return ValueTask.CompletedTask;
                entered = true;
                replacement = current.RetireRootItemEffectSubject(TargetPlayerUid, rebind);
                return ValueTask.CompletedTask;
            });
        await fixture.QueryRootItemEffectsAsync();
        var start = fixture.Order.Count;
        armed = true;
        await fixture.CastAsync(1_000);
        armed = false;
        Assert.True(entered);
        Assert.Equal(400L, fixture.Owner.Character.MinMana);
        Assert.Equal("100", fixture.Owner.Character.Attributes["Stamina"]);
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 9));
        Assert.True(HasLegacyStatusFlag(fixture.Owner.Character.Attributes, 8));
        Assert.Equal(new ushort[] { 1_175, 1_095, 1_020 },
            ParseSpellAnimations(fixture.FramesSince(PlayerUid, start)).Select(packet => packet.SpellId).ToArray());
        if (rebind)
        {
            Assert.Same(replacement, fixture.RootItemEffectSlot(TargetPlayerUid));
            Assert.Equal(100_000L, Assert.IsType<WorldSession>(replacement).Character.MinHitPoints);
        }
        else Assert.Null(fixture.RootItemEffectSlot(TargetPlayerUid));
        // Fresh visible replacement may legitimately receive captured prefix traffic; no primary-target cancellation is invented.
    }

    private static bool RootManualItemScalar(byte[] bytes, LegacyMsgUpdateDataType type, ulong value) =>
        LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) && packet.Uid == PlayerUid &&
        packet.Updates.Any(entry => entry.Type == type && entry.Value == value);

    private static ulong? RootManualItemBits(byte[] bytes)
    {
        if (!LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) || packet.Uid != PlayerUid) return null;
        foreach (var entry in packet.Updates)
            if (entry.Type == LegacyMsgUpdateDataType.StatusFlag && entry.Values is { Count: > 0 } bits)
                return bits[0] & 768UL; // Only source Stigma9 and MagicShield8, not unrelated flags.
        return null;
    }

    private static string[] RootManualItemPrefixTrace(IEnumerable<(uint Recipient, byte[] Bytes)> frames)
    {
        var result = new List<string>();
        var enteredOriginalCosts = false;
        foreach (var entry in frames)
        {
            if (LegacySpellAnimationPacket.TryParse(ReadFrame(entry.Bytes), out var spell) && spell.SpellId is 1_175 or 1_095 or 1_020)
                result.Add($"{entry.Recipient}:spell:{spell.SpellId}");
            else if (RootManualItemBits(entry.Bytes) is { } bits && bits != 0)
                result.Add($"{entry.Recipient}:bits:{bits}");
            else if (entry.Recipient == PlayerUid && LegacyMsgUpdatePacket.TryParse(ReadFrame(entry.Bytes), out var update))
                foreach (var scalar in update.Updates)
                {
                    if (scalar.Type == LegacyMsgUpdateDataType.Stamina)
                    {
                        enteredOriginalCosts = true;
                        result.Add($"{entry.Recipient}:stamina:{scalar.Value}");
                    }
                    else if (scalar.Type == LegacyMsgUpdateDataType.Mana)
                    {
                        result.Add($"{entry.Recipient}:mana:{scalar.Value}");
                        if (enteredOriginalCosts) return result.ToArray();
                    }
                }
        }
        return result.ToArray();
    }

    private static void AssertRootManualPrefixAnimation(LegacySpellAnimationPacket packet,
        ushort id, byte level, ushort x, ushort y, uint amount)
    {
        Assert.Equal(PlayerUid, packet.AttackerUid);
        Assert.Equal(0U, packet.AttackedUid);
        Assert.Equal(id, packet.SpellId);
        Assert.Equal(level, packet.SpellLevel);
        Assert.Equal((x, y), (packet.X, packet.Y));
        Assert.Equal((ushort)0, packet.NextSpell);
        Assert.Equal((byte)0, packet.LevelHu);
        Assert.Equal((byte)0, packet.SpellEffect);
        var target = Assert.Single(packet.Targets);
        Assert.Equal(PlayerUid, target.Uid);
        Assert.Equal(amount, target.Damage);
        Assert.True(target.Hit);
        Assert.Equal(MsgAttackEffect.None, target.Effect);
        Assert.Equal(((ushort)0, (ushort)0), (target.NewX, target.NewY));
        Assert.Equal(0U, target.Unknown);
        Assert.Equal(0U, target.TailedBeast);
    }
}
