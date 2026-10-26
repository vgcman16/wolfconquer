using System.Runtime.ExceptionServices;
using System.Text.Json;
using Metadata = WolfConquer.Core.World.LegacyNativeNinjaFile;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeNinjaMetadataLoadTests
{
    private static void RunOwnedMetadata(Action<OwnedFiles> body)
    {
        OwnedFiles? files = null;
        var errors = new List<Exception>();
        try { files = new(); body(files); }
        catch (Exception error) { errors.Add(error); }
        if (files is not null)
            try { files.Dispose(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException("Native Ninja table action and owned cleanup failed.", errors);
    }

    private static void AssertOldMetadataPair(OwnedFiles files)
    {
        Assert.Same(files.OldTypes, Metadata.gouyu_type);
        Assert.Same(files.OldImmortals, Metadata.gouyu_immortals);
        var type = Assert.Single(Metadata.gouyu_type);
        var immortal = Assert.Single(Metadata.gouyu_immortals);
        Assert.Same(files.OldTypeRow, type.Value); Assert.Same(files.OldImmortalRow, immortal.Value);
        Assert.Equal(9001U, type.Key); Assert.Equal(9002U, immortal.Key);
        Assert.Equal(new long[] { 9001, 999, 0, 0, 0, 0, 0, 0, 0, 0, 123, 0 },
            new long[] { type.Value.ID, type.Value.ItemID, type.Value.Level, type.Value.Exp, type.Value.UK1,
                type.Value.UK2, type.Value.magic1, type.Value.magic2, type.Value.Power, type.Value.Damage, type.Value.Score, type.Value.UK5 });
        Assert.Equal(new uint[] { 9002, 0, 456, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
            new[] { immortal.Value.Level, immortal.Value.U1, immortal.Value.MaxHP, immortal.Value.PAttack, immortal.Value.PDefense,
                immortal.Value.MDefense, immortal.Value.Chance, immortal.Value.Seconds, immortal.Value.Rate, immortal.Value.MovementSpeed,
                immortal.Value.U13, immortal.Value.U14, immortal.Value.P });
    }

    private sealed class NinjaMetadataSnapshot
    {
        private readonly Dictionary<uint, Metadata.NinjaSprintItem> types = Metadata.gouyu_type;
        private readonly Dictionary<uint, Metadata.Item> immortals = Metadata.gouyu_immortals;
        private readonly KeyValuePair<uint, Metadata.NinjaSprintItem>[] typeRows = Metadata.gouyu_type.ToArray();
        private readonly KeyValuePair<uint, Metadata.Item>[] immortalRows = Metadata.gouyu_immortals.ToArray();
        private readonly string before = MetadataState();

        internal void RequireUnchanged()
        {
            Assert.Same(types, Metadata.gouyu_type); Assert.Same(immortals, Metadata.gouyu_immortals);
            Assert.Equal(typeRows.Select(row => row.Key), types.Keys);
            Assert.Equal(immortalRows.Select(row => row.Key), immortals.Keys);
            foreach (var row in typeRows) Assert.Same(row.Value, types[row.Key]);
            foreach (var row in immortalRows) Assert.Same(row.Value, immortals[row.Key]);
            Assert.Equal(before, MetadataState());
        }

        private static string MetadataState() => JsonSerializer.Serialize(new { Metadata.gouyu_type, Metadata.gouyu_immortals },
            new JsonSerializerOptions { IncludeFields = true });
    }
}
