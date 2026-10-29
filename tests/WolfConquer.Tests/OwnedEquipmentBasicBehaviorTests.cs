using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class OwnedEquipmentBasicBehaviorTests
{
    private static PlayerItemSnapshot ParseItems(byte[] bytes) => PlayerItemSnapshotLoader.Parse(bytes);
    private static PacketFrame ReadFrame(byte[] bytes) => OwnedEquipmentBehaviorFixture.ReadFrame(bytes);
    private static LegacyGameItemPacket ParseLegacyGameItem(byte[] bytes)
    {
        Assert.True(LegacyGameItemPacket.TryParse(ReadFrame(bytes), out var packet));
        return packet;
    }
    private static void AssertItemUsage(byte[] bytes, MsgItemUsagePacket expected)
    {
        Assert.True(MsgItemUsagePacket.TryParse(ReadFrame(bytes), out var actual));
        Assert.Equal(expected, actual);
    }
    private static void AssertRealOwner(OwnedEquipmentBehaviorFixture f, uint uid)
    {
        var current = f.Current(uid);
        Assert.Equal((long)uid, current.AccountId);
        Assert.Equal((long)uid, current.Character.Uid);
        Assert.True(f.Recovery.IsReadyFor(f.Data.Users, f.Data.Items));
        Assert.True(f.Recovery.TryRequireLiveJournalClear(out var reason), reason);
        Assert.True(LegacySecondaryPasswordRuntime.IsAdmitted(f.Connection(uid), current));
        var client = Assert.IsType<LegacyNativeGameClientRuntime>(current.NativeGameClient);
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(client.OrdinarySpellBinding);
        Assert.Same(f.Store, binding.Store);
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(binding));
        Assert.False(current.LegacyTradeActive);
        Assert.True(File.Exists(f.CharacterPath(uid)));
        Assert.True(File.Exists(f.ItemPath(uid)));
    }

    private static void AssertSingleFailedRequirement(CharacterSnapshot character, ItemType item, string failed)
    {
        Assert.False(character.Reborn > 0 && character.Level >= 70 && item.RequiredLevel <= 70);
        var facts = new Dictionary<string, bool>
        {
            ["level"] = character.Level >= item.RequiredLevel,
            ["strength"] = character.Strength >= item.RequiredStrength && character.Agility >= item.RequiredAgility,
            ["gender"] = item.RequiredGender == 0 || character.Body / 1000 == item.RequiredGender,
            // These explicit input vectors contain class0 or WindWalker16000.
            ["class"] = item.RequiredClass == 0 || item.RequiredClass == 16000 && character.Class is >= 16000 and <= 16057,
        };
        Assert.Equal(failed, Assert.Single(facts, pair => !pair.Value).Key);
    }
}
