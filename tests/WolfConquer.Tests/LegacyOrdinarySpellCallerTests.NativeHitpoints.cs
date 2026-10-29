using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;
using ItemFixture = WolfConquer.Tests.WorldServerFrameHandlerTests.NativeItemSpellCallerFixture;

namespace WolfConquer.Tests;

public sealed partial class LegacyOrdinarySpellCallerTests
{
    [Fact]
    public async Task ActualOwnedRuneHpClampCommitsCompleteSparseCharacterItemsAndSpellsBeforeFirstOutput()
    {
        var transform = new TransformInfo(1350, 0, "DivineHare", 207, 3000,
            "fixture://owned-v9-TransformInfo.txt", LineNumber: 1, RawLine: "1350 0 DivineHare 207 3000",
            Fields: ["1350", "0", "DivineHare", "207", "3000"]);
        const string opaqueComment = "; native HP v9 opaque comment: retain exactly";
        const string opaqueAttribute = "OwnedNativeHpV9Opaque=retained-uninterpreted-value";
        await WorldServerFrameHandlerTests.WithNativeItemSpellCallerAsync(true, async f =>
        {
            await f.Login();
            Assert.DoesNotContain("PARITY-BUG", f.Log.ToString(), StringComparison.Ordinal);
            Assert.DoesNotContain("runtime-exception", f.Log.ToString(), StringComparison.Ordinal);
            var binding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Native.OrdinarySpellBinding);
            var player = Assert.IsType<LegacyNativePlayerRuntime>(f.Current.NativePlayer);
            Assert.Same(f.Store, binding.Store); Assert.Same(f.Connection, binding.Connection);
            Assert.Same(f.Native, binding.Client); Assert.Same(player, binding.Player);
            var admitted = f.Current;
            f.Store.Set(f.Connection, admitted with { Character = admitted.Character with { MinHitPoints = 100000 } });
            var highHp = f.Current;
            Assert.Equal(100000L, highHp.Character.MinHitPoints); Assert.Equal(100000, player.HitPoints);
            // The ordinary maximum intentionally floors at current HP. Only an
            // actual active transform has the existing lower-maximum contract.
            Assert.False(LegacyTransformState.TryGetActiveMaxHitpoints(highHp.Character, out _));
            Assert.Equal(100000U, LegacyMaxHitpointsCalculator.CalculateRuntimeMaxHitpoints(highHp.Character, 100000,
                highHp.PlayerItems, highHp.CombatGear ?? CombatGearSnapshot.Empty, f.Catalog, highHp.AlternateEquipmentActive));
            Assert.True(f.Catalog.TransformInfoByTransformIdAndLevel.TryGetValue((1350, 0), out var indexedTransform));
            var metadata = Assert.IsType<TransformInfo>(indexedTransform);
            Assert.Equal(transform, metadata); Assert.Equal(207, metadata.Lookface); Assert.Equal(3000, metadata.MaxLife);
            // Use the production transform projector with actual catalog values,
            // then its metadata-first publication shape that retains prior HP.
            var transformed = LegacyTransformState.Apply(highHp.Character, checked((uint)metadata.Lookface),
                checked((uint)metadata.MaxLife), DateTimeOffset.UtcNow.AddHours(1)) with { MinHitPoints = highHp.Character.MinHitPoints };
            f.Store.Set(f.Connection, highHp with { Character = transformed });
            var before = f.Current;
            Assert.Equal(100000L, before.Character.MinHitPoints); Assert.Equal(100000, player.HitPoints);
            Assert.True(LegacyTransformState.TryGetActiveMaxHitpoints(before.Character, out var transformMaximum));
            Assert.Equal(3000U, transformMaximum); Assert.False(LegacyTransformState.IsExpired(before.Character, DateTimeOffset.UtcNow));
            Assert.Equal(100000L, CharacterSnapshotParser.ParseText(
                CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(f.CharacterPath), out _)).MinHitPoints);
            Assert.Same(binding, f.Native.OrdinarySpellBinding); Assert.False(binding.IsRetired);
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding)); Assert.False(f.Recovery.IsBlocked);
            CheckItemNative(f);

            // ExpBallUsed really has an optional zero parser default. Required
            // Face/Haire/FirstClass cannot be omitted from a valid source image.
            Assert.Equal(0L, before.Character.ExpBallUsed);
            var completeText = CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(f.CharacterPath), out var encoding);
            var omittedLine = Assert.Single(completeText.Split('\n'), line => line.TrimEnd('\r') == "ExpBallUsed=0");
            var sparseText = completeText.Replace(omittedLine + "\n", "", StringComparison.Ordinal);
            Assert.NotEqual(completeText, sparseText);
            var sparse = encoding.GetPreamble().Concat(encoding.GetBytes(sparseText)).ToArray();
            File.WriteAllBytes(f.CharacterPath, sparse);
            var parsedSparse = CharacterSnapshotParser.ParseText(sparseText);
            Assert.True(WorldPacketHandlers.BoundHitpointsInputs.ScalarsEqual(before.Character, parsedSparse));
            Assert.False(CharacterSnapshotScalarPersistence.HasPersistedScalarChanges(before.Character, parsedSparse));
            Assert.False(parsedSparse.Attributes.ContainsKey("ExpBallUsed"));
            Assert.Equal("retained-uninterpreted-value", parsedSparse.Attributes["OwnedNativeHpV9Opaque"]);
            Assert.Contains(opaqueComment, sparseText);

            var expectedItems = before.PlayerItems with
            {
                RuneItems = before.PlayerItems.RuneItems.Select(row => row.Uid == ItemFixture.ItemUid ? row.WithPosition(212) : row).ToArray()
            };
            var expectedHp = LegacyMaxHitpointsCalculator.CalculateRuntimeMaxHitpoints(before.Character, 100000,
                expectedItems, before.CombatGear ?? CombatGearSnapshot.Empty, f.Catalog, before.AlternateEquipmentActive);
            Assert.Equal(3000U, expectedHp);
            Assert.InRange(expectedHp, 1U, 99999U);
            var expectedCharacter = LegacyBoundHitpointsScalarWrite.ProjectAfterImage(sparse,
                before.Character with { MinHitPoints = expectedHp });
            var expectedItemImage = PlayerItemSnapshotLoader.ToLegacyBytes(expectedItems);
            var oldSpells = File.ReadAllBytes(f.SpellsPath);
            var expectedSpellImage = Convert.FromHexString("01000000").Concat(oldSpells.Skip(16)).ToArray();
            int first = f.Sent.Count; bool observed = false;
            f.OnSend = sentFrame =>
            {
                if (observed) return;
                observed = true;
                var committed = f.Current;
                Assert.NotSame(before, committed); Assert.Equal(first, f.Sent.Count);
                Assert.Equal(expectedCharacter, File.ReadAllBytes(f.CharacterPath));
                Assert.Equal(expectedItemImage, File.ReadAllBytes(f.ItemsPath));
                Assert.Equal(expectedSpellImage, File.ReadAllBytes(f.SpellsPath));
                Assert.Equal((long)expectedHp, committed.Character.MinHitPoints);
                Assert.Equal(checked((int)expectedHp), player.HitPoints);
                Assert.Equal((long)expectedHp, CharacterSnapshotParser.ParseText(
                    CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(f.CharacterPath), out _)).MinHitPoints);
                Assert.True(player.Alive); Assert.False(player.NotLife);
                Assert.Same(binding, f.Native.OrdinarySpellBinding); Assert.False(binding.IsRetired);
                Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding)); Assert.False(f.Recovery.IsBlocked);
                Assert.True(f.Recovery.IsReadyForCharacterItemsSpells(f.Users, f.Items, f.Spells));
                Assert.False(LegacyTradeJournal.HasPending(f.Users)); CheckItemNative(f);
                var savedText = CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(f.CharacterPath), out _);
                Assert.Contains(opaqueComment, savedText); Assert.Contains(opaqueAttribute, savedText);
                Assert.Contains("ExpBallUsed=0", savedText);
                Assert.Equal((ushort)212, Assert.Single(committed.PlayerItems.RuneItems, row => row.Uid == ItemFixture.ItemUid).Position);
                Assert.DoesNotContain(committed.PlayerSpells.Spells, row => row.Id == 14160);
            };
            await f.Send(RuneRequest()); f.OnSend = null;
            Assert.True(observed, f.Log.ToString());
            Assert.Equal(6, f.Sent.Count - first);
            Assert.True(MsgActionPacket.TryParse(Frame(f.Sent[first]), out var removed));
            Assert.Equal((MsgActionType)109, removed.Type); Assert.Equal(14160U, removed.DwParam);
            Assert.Equal(RuneRequest(), f.Sent[first + 1]);
            Assert.True(LegacyMsgUpdatePacket.TryParse(Frame(f.Sent[first + 2]), out var maximum));
            Assert.Equal(ItemFixture.Uid, maximum.Uid);
            Assert.Equal(LegacyMsgUpdateDataType.MaxHitpoints, Assert.Single(maximum.Updates).Type);
            Assert.Equal((ulong)expectedHp, maximum.Updates[0].Value);
            Assert.True(LegacyMsgUpdatePacket.TryParse(Frame(f.Sent[first + 3]), out var current));
            Assert.Equal(ItemFixture.Uid, current.Uid); Assert.Equal(2, current.Updates.Count);
            Assert.Equal(new[] { LegacyMsgUpdateDataType.MaxHitpoints, LegacyMsgUpdateDataType.Hitpoints }, current.Updates.Select(row => row.Type));
            Assert.All(current.Updates, row => Assert.Equal((ulong)expectedHp, row.Value));
            Assert.True(LegacyPlayerStatusPacket.TryParse(Frame(f.Sent[first + 4]), out var status));
            Assert.Equal(ItemFixture.Uid, status.Uid);
            Assert.True(LegacyUserTotalRefineLevPacket.TryParse(Frame(f.Sent[first + 5]), out var refine));
            Assert.Equal(ItemFixture.Uid, refine.Uid);
            Assert.Same(binding, f.Native.OrdinarySpellBinding); Assert.False(binding.IsRetired);
            Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding)); Assert.False(f.Recovery.IsBlocked);
            Assert.False(LegacyTradeJournal.HasPending(f.Users));
            Assert.Equal((long)expectedHp, f.Current.Character.MinHitPoints); Assert.Equal(checked((int)expectedHp), player.HitPoints);
            Assert.Equal(before.Character.Money, f.Current.Character.Money);
            Assert.Equal(before.Character.ConquerPoints, f.Current.Character.ConquerPoints);
            Assert.Equal(0xA5, File.ReadAllBytes(f.SpellsPath)[15]);
        }, transformInfo: [transform], characterLines: [opaqueComment, opaqueAttribute]);
    }
}
