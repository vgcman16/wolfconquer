using System.Buffers.Binary;

namespace WolfConquer.Core.World;

public sealed partial class LegacyMailboxStore
{
    internal sealed partial class ClaimCapture
    {
        internal ClaimProjection ProjectPackages(IReadOnlyDictionary<uint, LegacyMailboxEntry> replacements)
        {
            var walletOnly = new Dictionary<uint, LegacyMailboxEntry>();
            foreach (var (id, next) in replacements)
            {
                var original = _rows.SingleOrDefault(row => row.Id == id) ??
                    throw new InvalidOperationException("Unknown mailbox package row.");
                if (next.Flag != 0 && next.Flag != original.Flag)
                    throw new InvalidOperationException("Invalid mailbox package flag projection.");
                walletOnly.Add(id, new LegacyMailboxEntry
                {
                    Id = next.Id, Title = next.Title, SenderName = next.SenderName, Content = next.Content,
                    Money = next.Money, EMoney = next.EMoney, ItemId = next.ItemId,
                    EmoneyRecordType = next.EmoneyRecordType, Timer = next.Timer, Flag = original.Flag,
                });
            }
            // Reuse the existing exact field whitelist for money, CP and attachment.
            var image = Project(walletOnly).Image;
            for (var index = 0; index < _rows.Length; index++)
                if (replacements.TryGetValue(_rows[index].Id, out var next))
                    BinaryPrimitives.WriteUInt32LittleEndian(image.AsSpan(checked(4 + index * ClaimEntrySize + 728)), next.Flag);
            var cache = _expected.Select(row => replacements.TryGetValue(row.Id, out var next) ? next : row).ToList();
            return new(this, image, cache);
        }
    }
}
