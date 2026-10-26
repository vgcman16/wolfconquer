using System.Text;

namespace WolfConquer.Core.World;

public sealed partial class LegacyMailboxStore
{
    private readonly Dictionary<uint, List<LegacyMailboxEntry>> mailboxByPlayerUid = [];
    private readonly string rootDirectory;
    private readonly Encoding encoding;
    private readonly Func<DateTime> nowProvider;

    public LegacyMailboxStore(
        string rootDirectory,
        Encoding? encoding = null,
        Func<DateTime>? nowProvider = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);

        this.rootDirectory = rootDirectory;
        this.encoding = encoding ?? Encoding.ASCII;
        this.nowProvider = nowProvider ?? (() => DateTime.Now);
    }

    private IReadOnlyList<LegacyMailboxEntry> ListCore(uint playerUid)
    {
        if (!mailboxByPlayerUid.TryGetValue(playerUid, out var entries))
        {
            return Load(playerUid);
        }

        var liveEntries = entries.Where(entry => entry.IsLive(nowProvider())).ToList();
        mailboxByPlayerUid[playerUid] = liveEntries;
        return liveEntries.ToArray();
    }

    private void SaveCore(uint playerUid)
    {
        var entries = List(playerUid);
        var path = GetMailboxPath(playerUid);
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream, encoding, leaveOpen: false);

        writer.Write(entries.Count);
        foreach (var entry in entries)
        {
            WriteEntry(writer, entry);
        }
    }

    private uint GetNextIdCore(uint playerUid)
    {
        var usedIds = List(playerUid).Select(entry => entry.Id).ToHashSet();
        for (var id = 1U; id < uint.MaxValue; id++)
        {
            if (!usedIds.Contains(id))
            {
                return id;
            }
        }

        throw new InvalidOperationException("No free legacy mailbox ids remain.");
    }

    private LegacyMailboxEntry AddCore(uint playerUid, LegacyMailboxEntry entry)
    {
        var entries = List(playerUid).ToList();
        var entryWithId = entry.Id == 0
            ? CopyWithId(entry, GetNextId(playerUid))
            : entry;

        entries.RemoveAll(existing => existing.Id == entryWithId.Id);
        entries.Add(entryWithId);
        mailboxByPlayerUid[playerUid] = entries;
        return entryWithId;
    }

    private bool TryGetCore(uint playerUid, uint mailId, out LegacyMailboxEntry entry)
    {
        entry = List(playerUid).FirstOrDefault(mail => mail.Id == mailId) ?? new LegacyMailboxEntry();
        return entry.Id != 0;
    }

    private bool TryReplaceCore(uint playerUid, LegacyMailboxEntry entry)
    {
        var entries = List(playerUid).ToList();
        var index = entries.FindIndex(existing => existing.Id == entry.Id);
        if (index < 0)
        {
            return false;
        }

        entries[index] = entry;
        mailboxByPlayerUid[playerUid] = entries;
        return true;
    }

    private bool TryRemoveIfEmptyCore(uint playerUid, uint mailId, out LegacyMailboxEntry entry)
    {
        var entries = List(playerUid).ToList();
        var index = entries.FindIndex(existing => existing.Id == mailId);
        if (index < 0)
        {
            entry = new LegacyMailboxEntry();
            return false;
        }

        entry = entries[index];
        if (!entry.CanDelete)
        {
            return false;
        }

        entries.RemoveAt(index);
        mailboxByPlayerUid[playerUid] = entries;
        return true;
    }

    private bool TryClaimMoneyCore(
        uint playerUid,
        uint mailId,
        out ulong money,
        out uint eMoney,
        out LegacyMailboxEntry entry)
    {
        if (!TryGet(playerUid, mailId, out var existing))
        {
            money = 0;
            eMoney = 0;
            entry = new LegacyMailboxEntry();
            return false;
        }

        money = existing.Money;
        eMoney = existing.EMoney;
        entry = existing.WithClaimedMoney();
        return TryReplace(playerUid, entry);
    }

    private bool TryClearFlagCore(
        uint playerUid,
        uint mailId,
        bool packageClaimSucceeded,
        out LegacyMailboxEntry entry)
    {
        if (!packageClaimSucceeded || !TryGet(playerUid, mailId, out var existing))
        {
            entry = new LegacyMailboxEntry();
            return false;
        }

        entry = existing.WithClearedFlag();
        return TryReplace(playerUid, entry);
    }

    private bool TryClearItemIdCore(
        uint playerUid,
        uint mailId,
        bool itemClaimSucceeded,
        out LegacyMailboxEntry entry)
    {
        if (!itemClaimSucceeded || !TryGet(playerUid, mailId, out var existing))
        {
            entry = new LegacyMailboxEntry();
            return false;
        }

        entry = existing.WithClearedItemId();
        return TryReplace(playerUid, entry);
    }

    public string GetMailboxPath(uint playerUid)
    {
        return Path.Combine(rootDirectory, "PlayersMailList", $"{playerUid}.bin");
    }

    private static LegacyMailboxEntry CopyWithId(LegacyMailboxEntry entry, uint id)
    {
        return new LegacyMailboxEntry
        {
            Title = entry.Title,
            SenderName = entry.SenderName,
            Content = entry.Content,
            Id = id,
            Money = entry.Money,
            EMoney = entry.EMoney,
            ItemId = entry.ItemId,
            EmoneyRecordType = entry.EmoneyRecordType,
            Flag = entry.Flag,
            Timer = entry.Timer
        };
    }

    private LegacyMailboxEntry ReadEntry(BinaryReader reader)
    {
        return new LegacyMailboxEntry
        {
            Title = ReadFixedString(reader, LegacyMailboxEntry.TitleByteLength),
            SenderName = ReadFixedString(reader, LegacyMailboxEntry.SenderNameByteLength),
            Content = ReadFixedString(reader, LegacyMailboxEntry.ContentByteLength),
            Id = reader.ReadUInt32(),
            Money = reader.ReadUInt64(),
            EMoney = reader.ReadUInt32(),
            ItemId = reader.ReadUInt32(),
            EmoneyRecordType = reader.ReadUInt32(),
            Flag = reader.ReadUInt32(),
            Timer = reader.ReadInt64()
        };
    }

    private void WriteEntry(BinaryWriter writer, LegacyMailboxEntry entry)
    {
        WriteFixedString(writer, entry.Title, LegacyMailboxEntry.TitleByteLength);
        WriteFixedString(writer, entry.SenderName, LegacyMailboxEntry.SenderNameByteLength);
        WriteFixedString(writer, entry.Content, LegacyMailboxEntry.ContentByteLength);
        writer.Write(entry.Id);
        writer.Write(entry.Money);
        writer.Write(entry.EMoney);
        writer.Write(entry.ItemId);
        writer.Write(entry.EmoneyRecordType);
        writer.Write(entry.Flag);
        writer.Write(entry.Timer);
    }

    private string ReadFixedString(BinaryReader reader, int byteLength)
    {
        var bytes = reader.ReadBytes(byteLength);
        var stringLength = Array.IndexOf(bytes, (byte)0);
        if (stringLength < 0)
        {
            stringLength = bytes.Length;
        }

        return encoding.GetString(bytes, 0, stringLength);
    }

    private void WriteFixedString(BinaryWriter writer, string value, int byteLength)
    {
        var buffer = new byte[byteLength];
        var bytes = encoding.GetBytes(value);
        Array.Copy(bytes, buffer, Math.Min(bytes.Length, buffer.Length));
        writer.Write(buffer);
    }
}
