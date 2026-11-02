using System.Globalization;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;
using Table = WolfConquer.Core.Data.LegacyNativeCombatGearTable;
using Jade = WolfConquer.Core.Data.LegacyNativeDaoqiDictTypeTable;
using Lottery = WolfConquer.Core.Data.LegacyNativeDictLotteryTable;
using Collection = WolfConquer.Core.Data.LegacyNativeDictCollectionTable;
using Rank = WolfConquer.Core.Data.LegacyNativeCombatGearRankTable;

namespace WolfConquer.Tests;

// NativeItemCounterSourcePrerequisite consumers only. This fixture obtains its
// capability through the real complete publisher, before ordinary admission.
// Neutral legacy19 input is deliberately separate from the unresolved22 schema.
internal sealed class NativeCombatGearProcFixture : IDisposable
{
    private readonly bool priorLoad;
    private readonly string priorLocation;
    private readonly Dictionary<uint, Dictionary<uint, Table.CQ>> priorGear;
    private readonly Dictionary<uint, Jade.CQ> priorJade;
    private readonly Dictionary<uint, Lottery.CQ> priorLottery;
    private readonly Dictionary<uint, Collection.CQ> priorCollection;
    private readonly Dictionary<Rank.Type, Rank.Rank> priorRanks;
    private readonly Dictionary<string, byte[]> inputs = new(StringComparer.OrdinalIgnoreCase);
    private readonly string directory;
    private readonly uint gearType, encodedSpell;
    private bool disposed;
    internal LegacyNativeCombatGearRuntime Runtime { get; private set; } = null!;

    internal NativeCombatGearProcFixture(string ownedRoot, uint gearType = 201, uint encodedSpell = 0)
    {
        this.gearType = gearType; this.encodedSpell = encodedSpell;
        LegacyNativeDbReadOwnedFiles.OrdinaryChain(ownedRoot);
        directory = Path.Combine(ownedRoot, "native-physical-proc-metadata");
        Assert.False(File.Exists(directory) || Directory.Exists(directory));
        lock (Table.MailboxLoadGate)
        {
            // Compatibility assignments cannot restore another live proof. Do
            // not take that ownership away from a different fixture/runtime.
            Assert.False(Table.TryCaptureMailboxLoadProof(out _));
            priorLoad = Table.m_Load; priorLocation = Table.Location;
            priorGear = Table.Info; priorJade = Jade.Info; priorLottery = Lottery.Info;
            priorCollection = Collection.Info; priorRanks = Rank.Ranks;
        }
    }

    internal void Load(WorldDataCatalog catalog)
    {
        Assert.Null(Runtime);
        Directory.CreateDirectory(directory);
        Write("combat_gear.txt", Legacy19Row(gearType, encodedSpell) + "\n");
        Write("daoqi_dict_type.txt", "");
        Write("dict_lottery.txt", "");
        Write("dict_collection.txt", "");
        Write("CombatGearRank.txt", "Count=0\n");
        var rank = Path.Combine(directory, "CombatGearRank.txt");
        Assert.True(LegacyNativeCombatGearRuntime.TryLoad(directory, rank, out var runtime, out var reason), reason);
        Runtime = Assert.IsType<LegacyNativeCombatGearRuntime>(runtime);
        Assert.True(Runtime.IsCurrentFor(catalog), "The actual legacy19 native and managed catalog images must align.");
    }

    internal static CombatGear[] CatalogRows(uint gearType = 201, uint encodedSpell = 0)
    {
        var row = Legacy19Row(gearType, encodedSpell);
        var fields = row.Split("@@", StringSplitOptions.None);
        Assert.Equal(19, fields.Length);
        var values = fields.Select(value => int.Parse(value, CultureInfo.InvariantCulture)).ToArray();
        return new[]
        {
            new CombatGear(values[0], values[1], values[2], values[3], values[4], values[5], values[6],
                values[7], values[7], values.Skip(8).Take(10).ToArray(), values[18], 0, 0, 0,
                "fixture://native-physical-proc-legacy19", 1, row, fields),
        };
    }

    internal static ItemType WithHitpoints(ItemType row, ushort hitpoints)
    {
        var fields = row.Fields.ToArray();
        fields[ItemType.ItemHPFieldIndex] = hitpoints.ToString(CultureInfo.InvariantCulture);
        return row with { Fields = fields };
    }

    private static string Legacy19Row(uint gearType, uint encodedSpell)
    {
        var values = new uint[19];
        values[0] = 1; values[1] = gearType; values[2] = 1; values[8] = encodedSpell;
        return string.Join("@@", values.Select(value => value.ToString(CultureInfo.InvariantCulture)));
    }

    private void Write(string leaf, string text)
    {
        var path = Path.Combine(directory, leaf);
        LegacyNativeDbReadOwnedFiles.OrdinaryChain(path);
        var bytes = new UTF8Encoding(false, true).GetBytes(text);
        using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        { output.Write(bytes); output.Flush(true); }
        inputs.Add(path, bytes);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        try
        {
            foreach (var (path, bytes) in inputs)
                Assert.Equal(bytes, File.ReadAllBytes(path));
        }
        finally
        {
            // Caller has drained actual transport/runtime first. Only restore
            // the previously unproved compatibility state, never a live proof.
            lock (Table.MailboxLoadGate)
            {
                Table.m_Load = priorLoad; Table.Location = priorLocation;
                Table.Info = priorGear; Jade.Info = priorJade; Lottery.Info = priorLottery;
                Collection.Info = priorCollection; Rank.Ranks = priorRanks;
            }
        }
        // Files remain with the owned fixture root for failure evidence. Its
        // guarded cleanup removes that root only after successful completion.
    }
}
