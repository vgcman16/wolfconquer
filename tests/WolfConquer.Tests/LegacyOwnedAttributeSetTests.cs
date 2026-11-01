using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyOwnedAttributeSetTests
{
    [Fact]
    public Task NewerSavedCharacterRefusesBeforeAttributeDebitOrOutput()
    {
        var character = OwnedEquipmentBehaviorFixture.Character(classCode: 14005, agility: 0,
            strength: 0, minHitPoints: 200) with { Vitality = 0, Spirit = 10, AttributePoints = 12 };
        var input = new OwnedEquipmentBehaviorInput(character, WolfConquer.Core.Data.PlayerItemSnapshot.Empty);
        return OwnedEquipmentBehaviorFixture.RunAsync(input, OwnedEquipmentBehaviorFixture.Catalog(), async f =>
        {
            var before = f.Current(); var binding = before.NativeGameClient!.OrdinarySpellBinding!;
            string path = f.CharacterPath();
            var bytes = File.ReadAllBytes(path);
            string text = WolfConquer.Core.Data.CharacterSnapshotFileEncoding.Decode(bytes, out var encoding);
            const string oldMoney = "\r\nMoney=98602439956\r\n";
            Assert.Equal(2, text.Split(oldMoney, StringSplitOptions.None).Length);
            text = text.Replace(oldMoney, "\r\nMoney=98602439955\r\n", StringComparison.Ordinal);
            File.WriteAllBytes(path, encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray());
            var newer = f.Data.Files(); int first = f.ActorFrames.Count;
            await f.SendAsync(OwnedEquipmentBehaviorFixture.Actor, new LegacyAttributeSetPacket(0, 0, 0, 10).ToFrameBytes());
            Assert.Same(before, f.Current()); Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
            Assert.Equal((ushort)10, binding.Player.Spirit); Assert.Equal((ushort)12, binding.Player.Atributes);
            Assert.Equal(first, f.ActorFrames.Count); f.Data.AssertFiles(newer);
            Assert.False(f.Recovery.IsBlocked);
        });
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task MissingOrChangedManaCacheIsRebuiltByActualAllocationQuery(bool missing)
    {
        var character = OwnedEquipmentBehaviorFixture.Character(classCode: 14005, agility: 0,
            strength: 0, minHitPoints: 200) with { Vitality = 0, Spirit = 10, AttributePoints = 12 };
        var input = new OwnedEquipmentBehaviorInput(character, WolfConquer.Core.Data.PlayerItemSnapshot.Empty);
        return OwnedEquipmentBehaviorFixture.RunAsync(input, OwnedEquipmentBehaviorFixture.Catalog(), async f =>
        {
            var initial = f.Current(); var overlay = LegacyPhysicalStatusOverlay.ForOwner(initial);
            Assert.NotNull(overlay.MaxMana);
            // Declared stale prestate through the actual Store. The real
            // 2203 query must rebuild it, rather than accept its old value.
            f.Store.Set(f.Connection(OwnedEquipmentBehaviorFixture.Actor), initial with
            { PhysicalStatusOverlay = overlay with { MaxMana = missing ? null : checked(overlay.MaxMana.Value + 1) } });
            var before = f.Current(); var binding = before.NativeGameClient!.OrdinarySpellBinding!;
            var files = f.Data.Files(); int first = f.ActorFrames.Count;
            var expected = files.ToDictionary(row => row.Key, row => row.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
            string key = Path.GetRelativePath(f.Data.Root, f.CharacterPath());
            string text = WolfConquer.Core.Data.CharacterSnapshotFileEncoding.Decode(expected[key], out var encoding);
            Assert.Contains("\r\nSpirit=10\r\n", text); Assert.Contains("\r\nAtributes=12\r\n", text);
            text = text.Replace("\r\nSpirit=10\r\n", "\r\nSpirit=20\r\n", StringComparison.Ordinal)
                .Replace("\r\nAtributes=12\r\n", "\r\nAtributes=2\r\n", StringComparison.Ordinal);
            expected[key] = encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();
            await f.SendAsync(OwnedEquipmentBehaviorFixture.Actor, new LegacyAttributeSetPacket(0, 0, 0, 10).ToFrameBytes());
            Assert.NotSame(before, f.Current()); Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
            Assert.Equal((ushort)20, binding.Player.Spirit); Assert.Equal((ushort)2, binding.Player.Atributes);
            Assert.True(LegacyEquipmentHitpointsStatus.TryGetForOwner(f.Current(), out var hp)); Assert.Equal(60U, hp);
            Assert.Equal(600U, LegacyPhysicalStatusOverlay.ForOwner(f.Current()).MaxMana);
            AssertCompleteAllocationQuery(f.ActorFrames.Skip(first).ToArray(), maximum: 60, allocatedSpirit: 10);
            f.Data.AssertFiles(expected);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ActualAdmissionRefusesOverspendOrLockedCharacterWithoutEffects(bool locked)
    {
        var character = OwnedEquipmentBehaviorFixture.Character(classCode: 14005, agility: 0,
            strength: 0, minHitPoints: 200) with { Vitality = 0, Spirit = 10, AttributePoints = 12 };
        var input = new OwnedEquipmentBehaviorInput(character, WolfConquer.Core.Data.PlayerItemSnapshot.Empty);
        return OwnedEquipmentBehaviorFixture.RunAsync(input, OwnedEquipmentBehaviorFixture.Catalog(), async f =>
        {
            var current = f.Current(); var binding = current.NativeGameClient!.OrdinarySpellBinding!;
            var before = f.Data.Files(); int frames = f.ActorFrames.Count;
            if (locked)
            {
                using (new FileStream(f.CharacterPath(), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
                    await f.SendAsync(OwnedEquipmentBehaviorFixture.Actor, new LegacyAttributeSetPacket(0, 0, 0, 10).ToFrameBytes());
            }
            else await f.SendAsync(OwnedEquipmentBehaviorFixture.Actor, new LegacyAttributeSetPacket(0, 0, 0, 13).ToFrameBytes());
            Assert.Same(current, f.Current()); Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
            Assert.Equal((ushort)10, binding.Player.Spirit); Assert.Equal((ushort)12, binding.Player.Atributes);
            Assert.Equal(frames, f.ActorFrames.Count); f.Data.AssertFiles(before);
        });
    }

    [Fact]
    public Task MalformedRequiredWeaponMetadataRefusesBeforeAttributeDebit()
    {
        const uint weaponType = 410095;
        var fields = Enumerable.Repeat("0", 66).ToArray();
        fields[0] = weaponType.ToString(System.Globalization.CultureInfo.InvariantCulture);
        fields[1] = "OwnedAttributeWeapon";
        var item = new WolfConquer.Core.Data.ItemType((int)weaponType, fields[1], fields);
        var weapon = new WolfConquer.Core.Data.PlayerItemSnapshotItem
            { Uid = 2_100_001, ItemId = weaponType, Position = 4, Durability = 100, MaximumDurability = 100 };
        var character = OwnedEquipmentBehaviorFixture.Character(classCode: 14005, agility: 0,
            strength: 0, minHitPoints: 200) with { Vitality = 0, Spirit = 10, AttributePoints = 12 };
        var input = new OwnedEquipmentBehaviorInput(character, new([], [], [], [weapon], []));
        return OwnedEquipmentBehaviorFixture.RunAsync(input, OwnedEquipmentBehaviorFixture.Catalog(item), async f =>
        {
            var before = f.Current(); var binding = before.NativeGameClient!.OrdinarySpellBinding!;
            var files = f.Data.Files(); int first = f.ActorFrames.Count;
            fields[WolfConquer.Core.Data.ItemType.FrequencyFieldIndex] = "invalid";
            await f.SendAsync(OwnedEquipmentBehaviorFixture.Actor, new LegacyAttributeSetPacket(0, 0, 0, 10).ToFrameBytes());
            Assert.Same(before, f.Current()); Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
            Assert.Equal((ushort)10, binding.Player.Spirit); Assert.Equal((ushort)12, binding.Player.Atributes);
            Assert.Equal(first, f.ActorFrames.Count); f.Data.AssertFiles(files);
        });
    }

    [Fact]
    public Task EchoSendFailureFencesCommittedAllocationWithoutRollbackOrRetry()
    {
        var character = OwnedEquipmentBehaviorFixture.Character(classCode: 14005, agility: 0,
            strength: 0, minHitPoints: 200) with { Vitality = 0, Spirit = 10, AttributePoints = 12 };
        var input = new OwnedEquipmentBehaviorInput(character, WolfConquer.Core.Data.PlayerItemSnapshot.Empty);
        return OwnedEquipmentBehaviorFixture.RunAsync(input, OwnedEquipmentBehaviorFixture.Catalog(), async f =>
        {
            var before = f.Current(); var binding = before.NativeGameClient!.OrdinarySpellBinding!;
            var expected = f.Data.Files().ToDictionary(row => row.Key, row => row.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
            string key = Path.GetRelativePath(f.Data.Root, f.CharacterPath());
            string text = WolfConquer.Core.Data.CharacterSnapshotFileEncoding.Decode(expected[key], out var encoding);
            Assert.Contains("\r\nSpirit=10\r\n", text); Assert.Contains("\r\nAtributes=12\r\n", text);
            text = text.Replace("\r\nSpirit=10\r\n", "\r\nSpirit=20\r\n", StringComparison.Ordinal)
                .Replace("\r\nAtributes=12\r\n", "\r\nAtributes=2\r\n", StringComparison.Ordinal);
            expected[key] = encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();
            int first = f.ActorFrames.Count;
            f.BeforeSend = (uid, bytes) =>
            {
                if (uid != OwnedEquipmentBehaviorFixture.Actor ||
                    !LegacyAttributeSetPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out _)) return;
                f.Data.AssertFiles(expected); Assert.Equal((ushort)20, binding.Player.Spirit);
                Assert.Equal((ushort)2, binding.Player.Atributes);
                throw new InvalidOperationException("owned-attribute-echo-send-failure");
            };
            try
            {
                var error = await Assert.ThrowsAsync<InvalidOperationException>(() => f.SendAsync(
                    OwnedEquipmentBehaviorFixture.Actor, new LegacyAttributeSetPacket(0, 0, 0, 10).ToFrameBytes()));
                Assert.Equal("owned-attribute-echo-send-failure", error.Message);
            }
            finally { f.BeforeSend = null; }
            Assert.True(f.Recovery.IsBlocked); Assert.True(binding.IsRetired);
            Assert.False(f.Store.TryGet(f.Connection(OwnedEquipmentBehaviorFixture.Actor), out _));
            Assert.Equal(first, f.ActorFrames.Count); f.Data.AssertFiles(expected);
            // The same fenced owner cannot replay the spend or emit success.
            try { await f.SendAsync(OwnedEquipmentBehaviorFixture.Actor, new LegacyAttributeSetPacket(0, 0, 0, 10).ToFrameBytes()); }
            catch (InvalidOperationException error) { Assert.Equal("owned-attribute-echo-send-failure", error.Message); }
            Assert.Equal(first, f.ActorFrames.Count); f.Data.AssertFiles(expected);
        });
    }

    [Fact]
    public Task AdmissionHydratesAllNativeAttributesAndRejectsUnownedChanges()
    {
        var character = OwnedEquipmentBehaviorFixture.Character(classCode: 14005, agility: 4,
            strength: 3, minHitPoints: 200) with { Vitality = 5, Spirit = 6, AttributePoints = 7 };
        var input = new OwnedEquipmentBehaviorInput(character, WolfConquer.Core.Data.PlayerItemSnapshot.Empty);
        return OwnedEquipmentBehaviorFixture.RunAsync(input, OwnedEquipmentBehaviorFixture.Catalog(), f =>
        {
            var before = f.Current(); var binding = before.NativeGameClient!.OrdinarySpellBinding!;
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
            Assert.Equal(((ushort)3, (ushort)4, (ushort)5, (ushort)6, (ushort)7),
                (binding.Player.Strength, binding.Player.Agility, binding.Player.Vitality, binding.Player.Spirit, binding.Player.Atributes));
            var files = f.Data.Files(); int frames = f.ActorFrames.Count;
            Assert.Throws<InvalidOperationException>(() => binding.Player.Strength = 8);
            Assert.Throws<InvalidOperationException>(() => binding.Player.Agility = 8);
            Assert.Throws<InvalidOperationException>(() => binding.Player.Vitality = 8);
            Assert.Throws<InvalidOperationException>(() => binding.Player.Spirit = 8);
            Assert.Throws<InvalidOperationException>(() => binding.Player.Atributes = 8);
            Assert.Throws<InvalidOperationException>(() => f.Store.Set(f.Connection(OwnedEquipmentBehaviorFixture.Actor),
                before with { Character = before.Character with { Spirit = 8 } }));
            Assert.Same(before, f.Current()); Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
            Assert.Equal(((ushort)3, (ushort)4, (ushort)5, (ushort)6, (ushort)7),
                (binding.Player.Strength, binding.Player.Agility, binding.Player.Vitality, binding.Player.Spirit, binding.Player.Atributes));
            Assert.Equal(frames, f.ActorFrames.Count); f.Data.AssertFiles(files);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public void AllocationCalculationRefusesOverflowAndInsufficientOrInvalidSavedPoints()
    {
        var before = OwnedEquipmentBehaviorFixture.Character(agility: 0, strength: 0) with
            { Vitality = 0, Spirit = 10, AttributePoints = 12 };
        Assert.True(LegacyOwnedAttributeSetProjection.TryCreate(before, new(0, 0, 0, 10), out var after));
        Assert.Equal(new LegacyOwnedAttributeSetProjection(0, 0, 0, 20, 2), after);
        Assert.False(LegacyOwnedAttributeSetProjection.TryCreate(before, new(uint.MaxValue, uint.MaxValue, 0, 0), out _));
        Assert.False(LegacyOwnedAttributeSetProjection.TryCreate(before, new(0, 0, 0, 13), out _));
        Assert.False(LegacyOwnedAttributeSetProjection.TryCreate(before with { AttributePoints = -1 }, new(0, 0, 0, 1), out _));
        Assert.False(LegacyOwnedAttributeSetProjection.TryCreate(before with { Spirit = ushort.MaxValue }, new(0, 0, 0, 1), out _));
    }

    [Fact]
    public void ProjectionRetainsCaseDistinctOpaqueKeysAndRejectsOnlyOwnedDuplicates()
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        { ["Other"] = "one", ["other"] = "two", ["Vitaliti"] = "5", ["Vitality"] = "opaque", ["Spirit"] = "6" };
        var before = OwnedEquipmentBehaviorFixture.Character(agility: 0, strength: 0) with
            { Vitality = 5, Spirit = 6, AttributePoints = 7, Attributes = values };
        Assert.True(LegacyOwnedAttributeSetProposal.TryCreate(before, new(0, 0, 0, 1), out var plan));
        Assert.NotNull(plan); Assert.Equal("one", plan!.After.Attributes["Other"]);
        Assert.Equal("two", plan.After.Attributes["other"]); Assert.Equal("opaque", plan.After.Attributes["Vitality"]);
        Assert.Equal("5", plan.After.Attributes["Vitaliti"]); Assert.Equal("7", plan.After.Attributes["Spirit"]);
        Assert.True(plan.IsCurrentPlain());
        values.Add("spirit", "6");
        Assert.False(plan.IsCurrentPlain());
        Assert.False(LegacyOwnedAttributeSetProposal.TryCreate(before, new(0, 0, 0, 1), out _));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task AdmittedAllocationPreservesOpaqueCaseVariantsAndRefusesOwnedAmbiguity(bool duplicateSpirit)
    {
        var character = OwnedEquipmentBehaviorFixture.Character(classCode: 14005, agility: 0,
            strength: 0, minHitPoints: 200) with { Vitality = 0, Spirit = 10, AttributePoints = 12 };
        var input = new OwnedEquipmentBehaviorInput(character, WolfConquer.Core.Data.PlayerItemSnapshot.Empty);
        return OwnedEquipmentBehaviorFixture.RunAsync(input, OwnedEquipmentBehaviorFixture.Catalog(), async f =>
        {
            var initial = f.Current();
            var fields = new Dictionary<string, string>(initial.Character.Attributes, StringComparer.Ordinal)
                { ["Other"] = "one", ["other"] = "two" };
            if (duplicateSpirit) fields["spirit"] = "10";
            f.Store.Set(f.Connection(OwnedEquipmentBehaviorFixture.Actor),
                initial with { Character = initial.Character with { Attributes = fields } });
            var before = f.Current(); var binding = before.NativeGameClient!.OrdinarySpellBinding!;
            var files = f.Data.Files(); int first = f.ActorFrames.Count;
            await f.SendAsync(OwnedEquipmentBehaviorFixture.Actor, new LegacyAttributeSetPacket(0, 0, 0, 10).ToFrameBytes());
            var after = f.Current();
            Assert.Equal("one", after.Character.Attributes["Other"]); Assert.Equal("two", after.Character.Attributes["other"]);
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
            if (duplicateSpirit)
            {
                Assert.Same(before, after); Assert.Equal((ushort)10, binding.Player.Spirit);
                Assert.Equal(first, f.ActorFrames.Count); f.Data.AssertFiles(files);
            }
            else
            {
                Assert.Equal(20L, after.Character.Spirit); Assert.Equal((ushort)20, binding.Player.Spirit);
                var expected = files.ToDictionary(row => row.Key, row => row.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
                string key = Path.GetRelativePath(f.Data.Root, f.CharacterPath());
                string text = WolfConquer.Core.Data.CharacterSnapshotFileEncoding.Decode(expected[key], out var encoding);
                text = text.Replace("\r\nSpirit=10\r\n", "\r\nSpirit=20\r\n", StringComparison.Ordinal)
                    .Replace("\r\nAtributes=12\r\n", "\r\nAtributes=2\r\n", StringComparison.Ordinal);
                expected[key] = encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();
                f.Data.AssertFiles(expected);
            }
        });
    }

    [Fact]
    public Task ImmediateStatusAfterAllocationRetainsTheFullQueryManaResult()
    {
        var character = OwnedEquipmentBehaviorFixture.Character(classCode: 14005, agility: 0,
            strength: 0, minHitPoints: 200) with { Vitality = 0, Spirit = 10, AttributePoints = 12 };
        var input = new OwnedEquipmentBehaviorInput(character, WolfConquer.Core.Data.PlayerItemSnapshot.Empty);
        return OwnedEquipmentBehaviorFixture.RunAsync(input, OwnedEquipmentBehaviorFixture.Catalog(), async f =>
        {
            var binding = f.Current().NativeGameClient!.OrdinarySpellBinding!;
            f.ActorFrames.Clear();
            await f.SendAsync(OwnedEquipmentBehaviorFixture.Actor, new LegacyAttributeSetPacket(0, 0, 0, 10).ToFrameBytes());
            Assert.Equal((ushort)20, binding.Player.Spirit); Assert.Equal(20L, f.Current().Character.Spirit);
            var allocationFrames = f.ActorFrames.Select(OwnedEquipmentBehaviorFixture.ReadFrame).ToArray();
            AssertCompleteAllocationQuery(f.ActorFrames, maximum: 60, allocatedSpirit: 10);
            Assert.Equal(LegacyPlayerStatusPacket.Opcode, allocationFrames[3].Header.Opcode);
            Assert.True(LegacyMsgUpdatePacket.TryParse(allocationFrames[4], out var maximum));
            Assert.Contains(maximum.Updates, row => row.Type == LegacyMsgUpdateDataType.MaxHitpoints && row.Value == 60);
            int first = f.ActorFrames.Count;
            await f.SendAsync(OwnedEquipmentBehaviorFixture.Actor,
                new LegacyPlayerStatusQueryPacket(OwnedEquipmentBehaviorFixture.Actor).ToFrameBytes());
            var status = Assert.Single(f.ActorFrames.Skip(first).Select(OwnedEquipmentBehaviorFixture.ReadFrame)
                .Where(frame => frame.Header.Opcode == LegacyPlayerStatusPacket.Opcode)
                .Select(frame => { Assert.True(LegacyPlayerStatusPacket.TryParse(frame, out var value)); return value; }));
            Assert.Equal(60U, status.MaxHP);
            Assert.Equal(600U, status.MaxMana);
        });
    }

    [Fact]
    public Task TwoValidAllocationsEachRunTheFullQueryThenOverspendRefuses()
    {
        var character = OwnedEquipmentBehaviorFixture.Character(classCode: 14005, agility: 0,
            strength: 0, minHitPoints: 200) with { Vitality = 0, Spirit = 10, AttributePoints = 12 };
        var input = new OwnedEquipmentBehaviorInput(character, WolfConquer.Core.Data.PlayerItemSnapshot.Empty);
        return OwnedEquipmentBehaviorFixture.RunAsync(input, OwnedEquipmentBehaviorFixture.Catalog(), async f =>
        {
            var initial = f.Current(); var binding = initial.NativeGameClient!.OrdinarySpellBinding!;
            var files = f.Data.Files();
            var expected = files.ToDictionary(row => row.Key, row => row.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
            string key = Path.GetRelativePath(f.Data.Root, f.CharacterPath());
            string text = WolfConquer.Core.Data.CharacterSnapshotFileEncoding.Decode(expected[key], out var encoding);
            Assert.Contains("\r\nSpirit=10\r\n", text); Assert.Contains("\r\nAtributes=12\r\n", text);
            text = text.Replace("\r\nSpirit=10\r\n", "\r\nSpirit=21\r\n", StringComparison.Ordinal)
                .Replace("\r\nAtributes=12\r\n", "\r\nAtributes=1\r\n", StringComparison.Ordinal);
            expected[key] = encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();
            f.ActorFrames.Clear();
            await f.SendAsync(OwnedEquipmentBehaviorFixture.Actor, new LegacyAttributeSetPacket(0, 0, 0, 10).ToFrameBytes());
            await f.SendAsync(OwnedEquipmentBehaviorFixture.Actor, new LegacyAttributeSetPacket(0, 0, 0, 1).ToFrameBytes());
            Assert.Equal((ushort)21, binding.Player.Spirit); Assert.Equal((ushort)1, binding.Player.Atributes);
            Assert.Equal(21L, f.Current().Character.Spirit); Assert.Equal(1L, f.Current().Character.AttributePoints);
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding)); f.Data.AssertFiles(expected);
            Assert.Equal(18, f.ActorFrames.Count);
            AssertCompleteAllocationQuery(f.ActorFrames.Take(9).ToArray(), maximum: 60, allocatedSpirit: 10);
            AssertCompleteAllocationQuery(f.ActorFrames.Skip(9).ToArray(), maximum: 63, allocatedSpirit: 1);
            var current = f.Current(); int first = f.ActorFrames.Count;
            await f.SendAsync(OwnedEquipmentBehaviorFixture.Actor, new LegacyAttributeSetPacket(0, 0, 0, 2).ToFrameBytes());
            Assert.Same(current, f.Current()); Assert.Equal(first, f.ActorFrames.Count); f.Data.AssertFiles(expected);
            await f.SendAsync(OwnedEquipmentBehaviorFixture.Actor,
                new LegacyPlayerStatusQueryPacket(OwnedEquipmentBehaviorFixture.Actor).ToFrameBytes());
            var status = Assert.Single(f.ActorFrames.Skip(first).Select(OwnedEquipmentBehaviorFixture.ReadFrame)
                .Where(frame => frame.Header.Opcode == LegacyPlayerStatusPacket.Opcode)
                .Select(frame => { Assert.True(LegacyPlayerStatusPacket.TryParse(frame, out var value)); return value; }));
            Assert.Equal(63U, status.MaxHP); Assert.Equal(630U, status.MaxMana);
        });
    }

    [Fact]
    public Task ActualAdmittedAllocationPublishesNativeSessionAndSavedCharacterBeforeEcho()
    {
        var character = OwnedEquipmentBehaviorFixture.Character(classCode: 14005, agility: 0,
            strength: 0, minHitPoints: 200) with { Vitality = 0, Spirit = 10, AttributePoints = 12 };
        var input = new OwnedEquipmentBehaviorInput(character, WolfConquer.Core.Data.PlayerItemSnapshot.Empty);
        return OwnedEquipmentBehaviorFixture.RunAsync(input, OwnedEquipmentBehaviorFixture.Catalog(), async f =>
        {
            var before = f.Current();
            var binding = before.NativeGameClient!.OrdinarySpellBinding!;
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
            Assert.Equal((ushort)10, binding.Player.Spirit);
            Assert.Equal((ushort)12, binding.Player.Atributes);
            var files = f.Data.Files();
            var expected = files.ToDictionary(row => row.Key, row => row.Value.ToArray(), StringComparer.OrdinalIgnoreCase);
            string key = Path.GetRelativePath(f.Data.Root, f.CharacterPath());
            string text = WolfConquer.Core.Data.CharacterSnapshotFileEncoding.Decode(expected[key], out var encoding);
            Assert.Contains("\r\nSpirit=10\r\n", text);
            Assert.Contains("\r\nAtributes=12\r\n", text);
            text = text.Replace("\r\nSpirit=10\r\n", "\r\nSpirit=20\r\n", StringComparison.Ordinal)
                .Replace("\r\nAtributes=12\r\n", "\r\nAtributes=2\r\n", StringComparison.Ordinal);
            expected[key] = encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray();
            int first = f.ActorFrames.Count; int observed = 0;
            f.BeforeSend = (uid, bytes) =>
            {
                if (uid != OwnedEquipmentBehaviorFixture.Actor ||
                    !LegacyAttributeSetPacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(bytes), out _)) return;
                observed++;
                var current = f.Current();
                Assert.Equal(20, current.Character.Spirit); Assert.Equal(2, current.Character.AttributePoints);
                Assert.Equal((ushort)20, binding.Player.Spirit); Assert.Equal((ushort)2, binding.Player.Atributes);
                Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
                f.Data.AssertFiles(expected);
            };
            try { await f.SendAsync(OwnedEquipmentBehaviorFixture.Actor, new LegacyAttributeSetPacket(0, 0, 0, 10).ToFrameBytes()); }
            finally { f.BeforeSend = null; }
            Assert.Equal(1, observed);
            f.Data.AssertFiles(expected);
            AssertCompleteAllocationQuery(f.ActorFrames.Skip(first).ToArray(), maximum: 60, allocatedSpirit: 10);
            var committed = f.Current(); int committedFrames = f.ActorFrames.Count;
            await f.SendAsync(OwnedEquipmentBehaviorFixture.Actor, new LegacyAttributeSetPacket(0, 0, 0, 10).ToFrameBytes());
            Assert.Same(committed, f.Current()); Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
            Assert.Equal((ushort)20, binding.Player.Spirit); Assert.Equal((ushort)2, binding.Player.Atributes);
            Assert.Equal(committedFrames, f.ActorFrames.Count); f.Data.AssertFiles(expected);
        });
    }

    private static void AssertCompleteAllocationQuery(IReadOnlyList<byte[]> frames, byte maximum, byte allocatedSpirit)
    {
        // Original MsgAtributeSet echoes, then QueryEquipment(CallItems:false).
        // AddRuneAtributes unconditionally assigns HP += BloodTideHP (zero here):
        // its two updates precede status, the final clamp pair, ordinary pair,
        // and refinement. These fixture stats have no other emitting phases.
        var parsed = frames.Select(OwnedEquipmentBehaviorFixture.ReadFrame).ToArray();
        Assert.Equal(new ushort[] { 2203, 2176, 2176, 2301, 2176, 2176, 2176, 2176, 2128 },
            parsed.Select(frame => frame.Header.Opcode));
        byte[] echo = [0x18, 0x00, 0x9B, 0x08,
            0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, allocatedSpirit, 0, 0, 0];
        Assert.Equal(echo, frames[0]);

        // Independent D13 protobuf bytes: UID1293600 -> A0 FA 4E; MaxHP is
        // type1; HP type0 is omitted. Initial login clamps fixture HP200 to
        // Spirit10 * 3 = 30 (1E), which both later allocations retain.
        byte[] maxFrame = [0x0E, 0x00, 0x80, 0x08, 0x08, 0xA0, 0xFA, 0x4E,
            0x12, 0x04, 0x08, 0x01, 0x10, maximum];
        byte[] pairFrame = [0x12, 0x00, 0x80, 0x08, 0x08, 0xA0, 0xFA, 0x4E,
            0x12, 0x04, 0x08, 0x01, 0x10, maximum, 0x12, 0x02, 0x10, 0x1E];
        foreach (int index in new[] { 1, 4, 6 }) Assert.Equal(maxFrame, frames[index]);
        foreach (int index in new[] { 2, 5, 7 }) Assert.Equal(pairFrame, frames[index]);
        Assert.True(LegacyPlayerStatusPacket.TryParse(parsed[3], out var status));
        Assert.Equal((uint)maximum, status.MaxHP);
        Assert.Equal((uint)maximum * 10, status.MaxMana); // Class14005: Spirit * 30.
        Assert.Equal((uint)maximum / 3 + 1, status.MagicAttack);
    }
}
