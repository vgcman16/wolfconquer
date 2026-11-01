using System.Buffers.Binary;
using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedBasicPhysicalAutoAttackTests
{
    private const uint ProficiencyWeaponUid = 5_337_700, ProficiencyWeaponType = 410_009;

    private static Task RunProficiencyAsync(bool existing, Func<AttackFixture, Task> body)
    {
        var clock = new AttackClock();
        var actor = Saved(Actor, "ProficiencyOwner", 830, 5000, 300, false);
        var target = Saved(Target, "ProficiencyTarget", 831, 2000, 0, false);
        var items = new PlayerItemSnapshot([], [], [], new PlayerItemSnapshotItem[]
        {
            new() { Uid = ProficiencyWeaponUid, ItemId = ProficiencyWeaponType, Position = 4,
                Durability = 100, MaximumDurability = 100, StackSize = 1 },
        }, []);
        var image = ProficiencyBeforeImage(existing);
        var proficiencies = PlayerProficiencySnapshotLoader.Parse(image);
        var options = new LegacyRuntimeUpdateLoopOptions(false, TimeSpan.FromSeconds(1), clock,
            LegacySignedMillisecondClock.FromTimeProvider(clock, 10000));
        return OwnedEquipmentBehaviorFixture.RunWithProficiencyImageAsync(
            new(actor, items, Proficiencies: proficiencies),
            OwnedEquipmentBehaviorFixture.Catalog(OwnedEquipmentBehaviorFixture.Item(ProficiencyWeaponType)),
            options, image, async owner =>
            {
                var f = new AttackFixture(owner, clock);
                Assert.Equal(image, File.ReadAllBytes(ProficiencyPath(f)));
                var weapon = Assert.Single(f.Current().PlayerItems.EquipmentItems);
                Assert.Equal(ProficiencyWeaponUid, weapon.Uid); Assert.Equal(ProficiencyWeaponType, weapon.ItemId);
                Assert.Equal((ushort)4, weapon.Position);
                f.AssertCoherent(2000);
                AssertProficiencyMap(f, proficiencies);
                await f.SitAsync(); f.Clear();
                await body(f);
            }, new OwnedEquipmentBehaviorInput(target, PlayerItemSnapshot.Empty));
    }

    private static string ProficiencyPath(AttackFixture f) => Path.Combine(f.Owner.Data.Proficiencies,
        Actor.ToString(CultureInfo.InvariantCulture) + ".bin");

    // Independent saved20-byte records, including source-opaque offsets9..11.
    // These bytes enter the real loader BEFORE registry bootstrap/admission.
    private static byte[] ProficiencyBeforeImage(bool existing)
    {
        var bytes = new byte[existing ? 44 : 24];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, existing ? 2 : 1);
        int offset = 4;
        if (existing)
        {
            WriteProficiencyRecord(bytes.AsSpan(offset, 20), 410, 5, 0, 100, 1_600_000);
            bytes[offset + 9] = 0xA5; bytes[offset + 10] = 0xC7; bytes[offset + 11] = 0xD9;
            offset += 20;
        }
        WriteProficiencyRecord(bytes.AsSpan(offset, 20), 420, 5, 2, 321, 1_600_000);
        bytes[offset + 9] = 0x31; bytes[offset + 10] = 0xA7; bytes[offset + 11] = 0xF2;
        return bytes;
    }

    private static byte[] ProficiencyAfterImage(byte[] before, bool existing)
    {
        if (existing)
        {
            var result = (byte[])before.Clone();
            // Actual raw hit300 * source rate1000 / (level5+1) =50000.
            // Existing XP100→50100 is below the unchanged level5 threshold.
            BinaryPrimitives.WriteUInt32LittleEndian(result.AsSpan(16), 50_100);
            return result;
        }
        var added = new byte[before.Length + 20]; before.CopyTo(added, 0);
        BinaryPrimitives.WriteInt32LittleEndian(added, 2);
        WriteProficiencyRecord(added.AsSpan(before.Length, 20), 410, 0, 0, 0, 0);
        return added; // Source first Add410,0,0; no invented first-hit XP.
    }

    private static void WriteProficiencyRecord(Span<byte> row, uint id, uint level, byte previous, uint experience, uint needed)
    {
        row.Clear(); BinaryPrimitives.WriteUInt32LittleEndian(row, id);
        BinaryPrimitives.WriteUInt32LittleEndian(row[4..], level); row[8] = previous;
        BinaryPrimitives.WriteUInt32LittleEndian(row[12..], experience);
        BinaryPrimitives.WriteUInt32LittleEndian(row[16..], needed);
    }
}
