using System.Collections.Concurrent;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCreatePrestigeTests
{
    private sealed class PrestigeClientOptionalOwnerImage
    {
        private readonly LegacyNativeChiRuntime? chi;
        private readonly LegacyNativeInnerPowerRuntime? inner;
        private readonly LegacyNativeJiangHu? jiang;
        private readonly uint? chiUid;
        private readonly uint? innerUid;
        private readonly uint? jiangUid;
        private readonly uint? jiangStrength;
        private readonly LegacyNativeChiRuntime.ChiPower[] powers;
        private readonly (LegacyNativeChiRuntime.ChiPower Power, LegacyNativeChiRuntime.ChiAttribute[]? Attributes,
            LegacyNativeChiRuntime.ChiAttribute[]? Values, bool Unlocked, int Rank, uint Uid)[] powerValues;
        private readonly LegacyNativeSubClassRuntime? subclass;
        private readonly LegacyNativeWardrobeRuntime? wardrobe;
        private readonly object? wardrobeOwner;
        private readonly ConcurrentDictionary<uint, LegacyNativeGameItemRuntime>[]? wardrobeItems;
        private readonly (ConcurrentDictionary<uint, LegacyNativeGameItemRuntime> Slot,
            KeyValuePair<uint, LegacyNativeGameItemRuntime>[] Rows)[] wardrobeSlots;

        internal PrestigeClientOptionalOwnerImage(LegacyNativeGameClientRuntime client)
        {
            chi = client.Player.MyChi;
            inner = client.Player.InnerPower;
            jiang = client.Player.MyJiangHu;
            chiUid = chi?.UID;
            innerUid = inner?.UID;
            jiangUid = jiang?.UID;
            jiangStrength = jiang?.Inner_Strength;
            powers = chi?.ToArray() ?? [];
            powerValues = powers.Select(power => (power, power.Attributes, power.Attributes?.ToArray(),
                power.UnLocked, power.Rank, power.UID)).ToArray();
            subclass = client.Player.SubClass;
            wardrobe = client.MyWardrobe;
            wardrobeOwner = wardrobe?.Owner;
            wardrobeItems = wardrobe?.Items;
            wardrobeSlots = wardrobeItems?.Select(slot => (slot, slot.ToArray())).ToArray() ?? [];
        }

        internal void RequireUnchanged(LegacyNativeGameClientRuntime client)
        {
            Assert.Same(chi, client.Player.MyChi);
            Assert.Same(inner, client.Player.InnerPower);
            Assert.Same(jiang, client.Player.MyJiangHu);
            Assert.Equal(chiUid, client.Player.MyChi?.UID);
            Assert.Equal(innerUid, client.Player.InnerPower?.UID);
            Assert.Equal(jiangUid, client.Player.MyJiangHu?.UID);
            Assert.Equal(jiangStrength, client.Player.MyJiangHu?.Inner_Strength);
            var currentPowers = client.Player.MyChi?.ToArray() ?? [];
            Assert.Equal(powers.Length, currentPowers.Length);
            for (var index = 0; index < powers.Length; index++) Assert.Same(powers[index], currentPowers[index]);
            foreach (var saved in powerValues)
            {
                Assert.Same(saved.Attributes, saved.Power.Attributes);
                Assert.Equal(saved.Values, saved.Power.Attributes);
                Assert.Equal(saved.Unlocked, saved.Power.UnLocked);
                Assert.Equal(saved.Rank, saved.Power.Rank);
                Assert.Equal(saved.Uid, saved.Power.UID);
            }
            Assert.Same(subclass, client.Player.SubClass); // No UID/owner binding is defined by this type.
            Assert.Same(wardrobe, client.MyWardrobe);
            Assert.Same(wardrobeOwner, client.MyWardrobe?.Owner);
            Assert.Same(wardrobeItems, client.MyWardrobe?.Items);
            if (wardrobeItems is null) return;
            Assert.Equal(wardrobeSlots.Length, wardrobeItems.Length);
            for (var index = 0; index < wardrobeSlots.Length; index++)
            {
                var saved = wardrobeSlots[index];
                Assert.Same(saved.Slot, wardrobeItems[index]);
                Assert.Equal(saved.Rows.Select(row => row.Key).Order(), saved.Slot.Keys.Order());
                foreach (var row in saved.Rows)
                {
                    Assert.True(saved.Slot.TryGetValue(row.Key, out var item));
                    Assert.Same(row.Value, item);
                }
            }
        }
    }
}
