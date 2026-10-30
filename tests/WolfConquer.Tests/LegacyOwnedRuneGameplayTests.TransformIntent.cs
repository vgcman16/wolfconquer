using System.Buffers;
using System.Text.Json.Nodes;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;
using F = WolfConquer.Tests.LegacyOwnedRuneGameplayFixture;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedRuneGameplayTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task ActualCurrentTransformRuneClampsAndDropPreservesBeforeTheOriginalReplies(bool drop) => Run(async f =>
    {
        await PrepareTransientRuneTransform(f);
        var before = f.Current; var player = f.Native.Player!;
        long expectedHp = drop ? 100000 : 3000;
        var expected = ExpectedTransientRuneImages(f, drop);
        var ledger = File.ReadAllBytes(f.Data.State); int first = f.Sent.Count, observed = 0;
        f.BeforeSend = _ =>
        {
            foreach (var pair in expected) Assert.Equal(pair.Value, File.ReadAllBytes(pair.Key));
            Assert.Equal(ledger, File.ReadAllBytes(f.Data.State)); Assert.Equal(expectedHp, f.Current.Character.MinHitPoints);
            Assert.Equal(checked((int)expectedHp), player.HitPoints); f.AssertConverged(); f.AssertMarker(""); observed++;
        };
        try { await f.DispatchAsync(TransientRuneRequest(drop)); } finally { f.BeforeSend = null; }
        Assert.Equal(drop ? 2 : 6, observed); Assert.Equal(first + observed, f.Sent.Count);
        Assert.Equal(new MsgActionPacket { ObjId = F.Uid, DwParam = F.FirstSpell, Type = (MsgActionType)109 }.ToFrameBytes(), f.Sent[first]);
        Assert.Equal(drop ? new MsgItemUsagePacket(F.First, 0, MsgItemUsageAction.RemoveInventory, 0, 0, 0, 0).ToFrameBytes()
            : F.Unequip().ToFrameBytes(), f.Sent[first + 1]);
        if (!drop)
        {
            Assert.True(LegacyMsgUpdatePacket.TryParse(TransientRuneFrame(f.Sent[first + 2]), out var maximum));
            Assert.Equal(LegacyMsgUpdateDataType.MaxHitpoints, Assert.Single(maximum.Updates).Type);
            Assert.Equal(3000UL, maximum.Updates[0].Value);
            Assert.True(LegacyMsgUpdatePacket.TryParse(TransientRuneFrame(f.Sent[first + 3]), out var current));
            Assert.Equal(new[] { LegacyMsgUpdateDataType.MaxHitpoints, LegacyMsgUpdateDataType.Hitpoints }, current.Updates.Select(row => row.Type));
            Assert.All(current.Updates, row => Assert.Equal(3000UL, row.Value));
        }
        Assert.Equal(before.Character.Money, f.Current.Character.Money);
        Assert.Equal(before.Character.ConquerPoints, f.Current.Character.ConquerPoints);
        var recovered = f.Data.Files(); await f.CloseAsync(); f.Open(); f.Data.AssertFiles(recovered);
    });

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public Task ActualTransientTransformRuneIntentRecoversEveryRealReplacementPrefix(int prefix) => Run(async f =>
    {
        var capture = await StageTransientRuneTransform(f, prefix);
        var paths = new[] { f.CharacterPath, f.ItemsPath, f.SpellsPath };
        for (var index = 0; index < paths.Length; index++)
            Assert.Equal(index < prefix ? capture[paths[index]] : f.Before[paths[index]], File.ReadAllBytes(paths[index]));
        await RecoverTransientRuneTransform(f, capture);
    });

    [Fact]
    public Task ActualFlushedDropTransformStageCarriesInputsWithoutPersistingTransientAttributes() => Run(async f =>
    {
        var capture = await StageTransientRuneTransform(f, 3, flushed: true, drop: true);
        var record = JsonNode.Parse(File.ReadAllBytes(f.PendingPath))!;
        var transaction = record["TransactionId"]!.GetValue<string>();
        foreach (var pair in capture)
        {
            Assert.Equal(f.Before[pair.Key], File.ReadAllBytes(pair.Key));
            Assert.Equal(pair.Value, File.ReadAllBytes(pair.Key + ".managed-trade-" + transaction + ".tmp"));
        }
        await RecoverTransientRuneTransform(f, capture);
        Assert.All(capture.Keys, path => Assert.False(File.Exists(path + ".managed-trade-" + transaction + ".tmp")));
    });

    [Theory]
    [InlineData("missing")] [InlineData("duplicate-alias")] [InlineData("changed-derived-cap")]
    public Task GenuineTransformIntentRefusesMissingAmbiguousOrWrongOutcomeInputs(string fault) => Run(async f =>
    {
        await StageTransientRuneTransform(f, 3); await f.CloseAsync();
        var record = JsonNode.Parse(File.ReadAllBytes(f.PendingPath))!.AsObject();
        var operation = record["RuneOperation"]!.AsObject();
        if (fault == "missing") operation.Remove("Transform");
        else
        {
            var fields = operation["Transform"]!["Fields"]!.AsArray();
            var field = fields.Single(row => row!["Key"]!.GetValue<string>() == LegacyTransformState.MaxHitpointsAttribute)!;
            if (fault == "changed-derived-cap") field["Value"] = "4000";
            else fields.Insert(fields.IndexOf(field) + 1, new JsonObject
                { ["Key"] = LegacyTransformState.MaxHitpointsAttribute, ["Value"] = "3000" });
        }
        File.WriteAllText(f.PendingPath, record.ToJsonString()); f.AssertStartupRefused();
    });

    private static async Task PrepareTransientRuneTransform(F f)
    {
        await f.DispatchAsync(F.Equip()); f.AssertMarker("14160:16");
        var admitted = f.Current;
        f.Store.Set(f.Connection, admitted with { Character = admitted.Character with { MinHitPoints = 100000 } });
        var highHp = f.Current;
        var transformed = LegacyTransformState.Apply(highHp.Character, 207, 3000, DateTimeOffset.UtcNow.AddHours(1))
            with { MinHitPoints = highHp.Character.MinHitPoints };
        f.Store.Set(f.Connection, highHp with { Character = transformed });
        Assert.Equal(100000L, f.Current.Character.MinHitPoints); Assert.Equal(100000, f.Native.Player!.HitPoints);
        Assert.True(LegacyTransformState.TryGetActiveMaxHitpoints(f.Current.Character, out var cap)); Assert.Equal(3000U, cap);
        var saved = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(f.CharacterPath), out _));
        Assert.Equal(100000L, saved.MinHitPoints); Assert.False(LegacyTransformState.TryGetActiveMaxHitpoints(saved, out _));
        Assert.False(saved.Attributes.ContainsKey(LegacyTransformState.MaxHitpointsAttribute));
        Assert.False(saved.Attributes.ContainsKey(LegacyTransformState.MaxHitpointsAliasAttribute));
        var dependency = LegacyRuneTransformDependency.Capture(f.Current.Character); dependency.Validate();
        var exposed = dependency.Fields; exposed[0] = new("Unknown", "cannot rewrite captured fields");
        Assert.True(dependency.SameAs(LegacyRuneTransformDependency.Capture(f.Current.Character)));
        var absent = LegacyRuneTransformDependency.Capture(saved);
        var cleared = absent.ApplyTo(f.Current.Character);
        Assert.False(LegacyTransformState.TryGetActiveMaxHitpoints(cleared, out _));
        var empty = new LegacyRuneTransformDependency([]); empty.Validate(); Assert.Empty(empty.Fields);
        Assert.False(LegacyTransformState.TryGetActiveMaxHitpoints(empty.ApplyTo(f.Current.Character), out _));
    }

    private static PacketFrame TransientRuneFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

    private static MsgItemUsagePacket TransientRuneRequest(bool drop) => drop
        ? new(F.First, 0, MsgItemUsageAction.DropRune, 1234, 22, 33, 44) : F.Unequip();

    private static Dictionary<string, byte[]> ExpectedTransientRuneImages(F f, bool drop)
    {
        var before = f.Current;
        var old = File.ReadAllBytes(f.CharacterPath); var text = CharacterSnapshotFileEncoding.Decode(old, out var encoding);
        Assert.Contains("MinHitPoints=100000\r\n", text); Assert.Contains("ManagedRuneSpellOrigins=14160:16\r\n", text);
        var hpText = drop ? text : text.Replace("MinHitPoints=100000\r\n", "MinHitPoints=3000\r\n", StringComparison.Ordinal);
        var afterText = hpText.Replace("ManagedRuneSpellOrigins=14160:16\r\n", "ManagedRuneSpellOrigins=\r\n", StringComparison.Ordinal);
        var items = before.PlayerItems with { RuneItems = before.PlayerItems.RuneItems
            .Where(row => !drop || row.Uid != F.First).Select(row => row.Uid == F.First ? row.WithPosition(212) : row)
            .OrderBy(row => row.Position).ThenBy(row => row.Position is 211 or 212 ? 0u : row.Uid).ToArray() };
        return new(StringComparer.OrdinalIgnoreCase)
        {
            [f.CharacterPath] = encoding.GetPreamble().Concat(encoding.GetBytes(afterText)).ToArray(),
            [f.ItemsPath] = PlayerItemSnapshotLoader.ToLegacyBytes(items),
            [f.SpellsPath] = f.OriginalSpells.ToArray(),
        };
    }

    private static async Task<Dictionary<string, byte[]>> StageTransientRuneTransform(F f, int boundary, bool flushed = false, bool drop = false)
    {
        await PrepareTransientRuneTransform(f); var expected = ExpectedTransientRuneImages(f, drop);
        var original = f.Current; int first = f.Sent.Count;
        f.Before = expected.Keys.ToDictionary(path => path, path => (byte[]?)File.ReadAllBytes(path), StringComparer.OrdinalIgnoreCase);
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(f.Native.OrdinarySpellBinding);
        var nativeMap = f.Native.MySpells!.ClientSpells; var nativeItems = binding.Items.Image;
        using (var fault = flushed ? LegacyJournalInterruption.StopAfterStageFlushed(boundary) : LegacyJournalInterruption.StopAfter(boundary))
        {
            f.Handler.ItemSpellJournalInterruption = fault;
            try { await f.DispatchAsync(TransientRuneRequest(drop)); } finally { f.Handler.ItemSpellJournalInterruption = null; }
            Assert.True(fault.PendingObserved, f.Log.ToString());
            if (!flushed) Assert.Equal(Enumerable.Range(0, boundary + 1), fault.Visited);
        }
        Assert.True(f.Runtime.Recovery.IsBlocked); Assert.Same(original, f.Current); Assert.Equal(first, f.Sent.Count);
        Assert.False(f.Store.IsBoundSpellOwnerCurrent(binding)); Assert.Same(nativeMap, f.Native.MySpells.ClientSpells);
        Assert.Same(nativeItems, binding.Items.Image); Assert.Equal(f.Registry, File.ReadAllBytes(f.Data.State));
        var record = JsonNode.Parse(File.ReadAllBytes(f.PendingPath))!; Assert.Equal(drop ? 15 : 14, record["Version"]!.GetValue<int>());
        var fields = record[drop ? "RuneContainer" : "RuneOperation"]!["Transform"]!["Fields"]!.AsArray();
        var captured = LegacyRuneTransformDependency.Capture(original.Character).Fields;
        Assert.Equal(captured.Select(row => row.Key), fields.Select(row => row!["Key"]!.GetValue<string>()));
        Assert.Equal(captured.Select(row => row.Value), fields.Select(row => row!["Value"]!.GetValue<string>()));
        Assert.Equal(3, record["Changes"]!.AsArray().Count);
        foreach (var change in record["Changes"]!.AsArray())
        {
            var path = (LegacyTradeJournalFileKind)change!["Kind"]!.GetValue<int>() switch
            {
                LegacyTradeJournalFileKind.Character => f.CharacterPath,
                LegacyTradeJournalFileKind.Items => f.ItemsPath,
                LegacyTradeJournalFileKind.Spells => f.SpellsPath,
                _ => throw new InvalidDataException("Unexpected actual transform Rune target."),
            };
            Assert.Equal(f.Before[path], Convert.FromBase64String(change["Before"]!.GetValue<string>()));
            Assert.Equal(expected[path], Convert.FromBase64String(change["After"]!.GetValue<string>()));
        }
        return expected;
    }

    private static async Task RecoverTransientRuneTransform(F f, Dictionary<string, byte[]> expected)
    {
        await f.CloseAsync(); f.Runtime = f.Open();
        foreach (var pair in expected) Assert.Equal(pair.Value, File.ReadAllBytes(pair.Key));
        var recordedHp = CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(expected[f.CharacterPath], out _)).MinHitPoints;
        Assert.Equal(recordedHp, CharacterSnapshotParser.ParseText(CharacterSnapshotFileEncoding.Decode(
            File.ReadAllBytes(f.CharacterPath), out _)).MinHitPoints);
        Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        Assert.Equal(f.Registry, File.ReadAllBytes(f.Data.State));
        var recovered = f.Data.Files(); await f.CloseAsync(); f.Runtime = f.Open(); f.Data.AssertFiles(recovered);
        // Recovery preserves the exact recorded HP. A subsequent real login
        // independently projects the ordinary330 maximum without transient state.
        await f.LoginOpenedFrameAsync();
        Assert.Equal(330L, f.Current.Character.MinHitPoints); Assert.Equal(330, f.Native.Player!.HitPoints);
        Assert.False(LegacyTransformState.TryGetActiveMaxHitpoints(f.Current.Character, out _));
        f.AssertConverged(); f.AssertMarker("");
        Assert.Equal(expected[f.ItemsPath], File.ReadAllBytes(f.ItemsPath));
        Assert.Equal(expected[f.SpellsPath], File.ReadAllBytes(f.SpellsPath));
        Assert.Equal(f.Registry, File.ReadAllBytes(f.Data.State));
    }
}
