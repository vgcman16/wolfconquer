using System.Text;
using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

internal partial class LegacyNativeMyCombatGear
{
    private const int MailboxGearMaximumImage = 16 * 1024 * 1024;

    private static byte[] MailboxGearImage(CombatGearSnapshot snapshot)
    {
        var image = new UTF8Encoding(false, true).GetBytes(CombatGearSnapshotLoader.ToLegacyText(snapshot));
        if (image.Length is 0 or > MailboxGearMaximumImage) throw MailboxRewardUnavailable();
        return image;
    }

    private static byte[] CopyMailboxGearImage(CombatGearSnapshot source, byte[] image)
    {
        if (image is null || image.Length is 0 or > MailboxGearMaximumImage) throw MailboxRewardUnavailable();
        byte[] copy = image.ToArray();
        if (!MailboxGearImage(source).AsSpan().SequenceEqual(copy) || !copy.AsSpan().SequenceEqual(image))
            throw MailboxRewardUnavailable();
        return copy;
    }

    private static CombatGearSnapshot CloneMailboxGearSnapshot(CombatGearSnapshot source)
    {
        if (source is null || source.Items is null || source.AirPowers is null || source.BlackJadeBag is null ||
            source.BlackJadeBag.BlackJades is null || source.Count != source.Items.Count || source.Count < 0 ||
            source.Count > MailboxGearMaximumImage || source.BlackJadeBag.Count != source.BlackJadeBag.BlackJades.Count ||
            source.BlackJadeBag.Count is < 0 or > MailboxGearMaximumImage)
            throw MailboxRewardUnavailable();
        var keys = new HashSet<uint>();
        var items = new CombatGearSnapshotItem[source.Items.Count];
        for (int index = 0; index < items.Length; index++)
        {
            var item = source.Items[index];
            if (item is null || !keys.Add(item.ItemID) || item.Animas is null || item.Animas.Count != 2 ||
                item.Animas[0] is null || item.Animas[1] is null || item.Animas[0].Count != 7 || item.Animas[1].Count != 7 ||
                item.BlackJades is null || item.BlackJades.Count != 6) throw MailboxRewardUnavailable();
            items[index] = item with { Animas = [item.Animas[0].ToArray(), item.Animas[1].ToArray()],
                BlackJades = item.BlackJades.ToArray() };
        }
        var jades = source.BlackJadeBag.BlackJades.ToArray();
        if (jades.Distinct().Count() != jades.Length) throw MailboxRewardUnavailable();
        return source with { AirPowers = source.AirPowers with { }, Items = items,
            BlackJadeBag = new(source.BlackJadeBag.Count, jades) };
    }
}
