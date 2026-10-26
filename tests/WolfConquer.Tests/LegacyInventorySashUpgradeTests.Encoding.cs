using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyInventorySashUpgradeTests
{
    [Theory]
    [InlineData("utf8-bom-crlf")]
    [InlineData("windows1252-lf-no-final-newline")]
    [InlineData("missing-count-cr-no-final-newline")]
    public Task RegisteredSashUpgradePreservesActualEncodingDelimitersAndUnrelatedBytes(string format) => RunAsync(async fixture =>
    {
        var images = fixture.SeedEncodedUpgrade(format);
        var before = fixture.Capture();
        var path = Path.GetRelativePath(fixture.Root, fixture.CharacterPath);
        Assert.Equal(images.Before, before.Files[path]);
        var observed = 0;
        fixture.SendObserver = _ =>
        {
            Assert.Equal(images.After, File.ReadAllBytes(fixture.CharacterPath));
            Assert.Equal(60, fixture.Current.Character.ConquerPoints);
            Assert.Equal("1", fixture.Current.Character.Attributes["InventorySashCount"]);
            observed++;
        };

        await fixture.InvokeAsync();

        Assert.Equal(3, observed); // Every real send must observe the already-saved exact image.
        var expectedAttributes = new Dictionary<string, string>(before.Owner.Character.Attributes, StringComparer.OrdinalIgnoreCase)
        { ["ConquerPoints"] = "60", ["InventorySashCount"] = "1" };
        Assert.Equal(expectedAttributes.OrderBy(pair => pair.Key, StringComparer.Ordinal),
            fixture.Current.Character.Attributes.OrderBy(pair => pair.Key, StringComparer.Ordinal));
        Assert.Equal(before.Owner.Character with { ConquerPoints = 60, Attributes = fixture.Current.Character.Attributes },
            fixture.Current.Character);
        Assert.Equal(before.Owner with { Character = fixture.Current.Character }, fixture.Current);
        Assert.Same(before.Owner.PlayerItems, fixture.Current.PlayerItems);
        var expectedFiles = new Dictionary<string, byte[]>(before.Files, StringComparer.OrdinalIgnoreCase)
        { [path] = images.After };
        fixture.AssertFiles(expectedFiles);
        var savedText = CharacterSnapshotFileEncoding.ReadAllText(fixture.CharacterPath, out var savedEncoding);
        Assert.Equal(images.AfterText, savedText);
        Assert.Equal(images.CodePage, savedEncoding.CodePage);
        Assert.Equal(images.Preamble, savedEncoding.GetPreamble());
        var reloaded = CharacterSnapshotParser.ParseText(savedText, fixture.CharacterPath);
        Assert.Equal(60, reloaded.ConquerPoints);
        Assert.Equal("1", reloaded.Attributes["InventorySashCount"]);
        Assert.Equal(before.Owner.Character.Attributes["SashUpgradeSentinel"], reloaded.Attributes["SashUpgradeSentinel"]);
        var frames = fixture.Sent.Skip(before.FrameCount).ToArray();
        Assert.Equal(3, frames.Length);
        Assert.Equal(new LegacyMsgUpdatePacket(Fixture.Uid, [new(LegacyMsgUpdateDataType.ConquerPoints, 60)]).ToFrameBytes(), frames[0]);
        Assert.Equal(new LegacyMsgUpdatePacket(Fixture.Uid, [new(LegacyMsgUpdateDataType.InventorySashMax, 300)]).ToFrameBytes(), frames[1]);
        Assert.Equal(new LegacyMsgUpdatePacket(Fixture.Uid, [new(LegacyMsgUpdateDataType.InventorySash, 1)]).ToFrameBytes(), frames[2]);
    });

    private sealed partial class Fixture
    {
        internal (byte[] Before, byte[] After, string AfterText, int CodePage, byte[] Preamble) SeedEncodedUpgrade(string format)
        {
            Seed();
            // Use the complete existing account seed, not a fabricated minimal
            // UID validator. Final readiness below follows the final format/reload.
            var lines = CharacterSnapshotFileEncoding.ReadAllLines(CharacterPath, out _).ToList();
            Assert.Single(lines, line => line == "ConquerPoints=150");
            Assert.Single(lines, line => line == "InventorySashCount=0");
            var sentinel = lines.FindIndex(line => line == "SashUpgradeSentinel=preserved");
            Assert.True(sentinel >= 0);
            Encoding encoding;
            string delimiter;
            bool terminalDelimiter;
            var missingCount = false;
            switch (format)
            {
                case "utf8-bom-crlf":
                    encoding = new UTF8Encoding(true, true);
                    delimiter = "\r\n";
                    terminalDelimiter = true;
                    lines[sentinel] = "SashUpgradeSentinel=雪 café";
                    break;
                case "windows1252-lf-no-final-newline":
                    // The real encoding owner above registers the genuine code-page provider.
                    encoding = Encoding.GetEncoding(1252, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
                    delimiter = "\n";
                    terminalDelimiter = false;
                    lines[sentinel] = "SashUpgradeSentinel=café € –";
                    break;
                case "missing-count-cr-no-final-newline":
                    encoding = new UTF8Encoding(false, true);
                    delimiter = "\r";
                    terminalDelimiter = false;
                    Assert.True(lines.Remove("InventorySashCount=0"));
                    missingCount = true;
                    break;
                default: throw new ArgumentOutOfRangeException(nameof(format));
            }
            // Independent finite oracle: change only the two known complete seed
            // rows, or insert the missing count before the known next section.
            // No production TryProject, Regex splitter or old UTF8-only helper.
            var afterLines = lines.Select(line => line switch
            {
                "ConquerPoints=150" => "ConquerPoints=60",
                "InventorySashCount=0" => "InventorySashCount=1",
                _ => line,
            }).ToList();
            if (missingCount)
            {
                var nextSection = afterLines.IndexOf("[Unrelated]");
                Assert.True(nextSection > 0);
                afterLines.Insert(nextSection, "InventorySashCount=1");
            }
            var suffix = terminalDelimiter ? delimiter : string.Empty;
            var beforeText = string.Join(delimiter, lines) + suffix;
            var afterText = string.Join(delimiter, afterLines) + suffix;
            var beforeBytes = encoding.GetPreamble().Concat(encoding.GetBytes(beforeText)).ToArray();
            var afterBytes = encoding.GetPreamble().Concat(encoding.GetBytes(afterText)).ToArray();
            File.WriteAllBytes(CharacterPath, beforeBytes);
            var decoded = CharacterSnapshotFileEncoding.ReadAllText(CharacterPath, out var detected);
            Assert.Equal(beforeText, decoded);
            Assert.Equal(encoding.CodePage, detected.CodePage);
            Assert.Equal(encoding.GetPreamble(), detected.GetPreamble());
            var character = CharacterSnapshotParser.ParseText(decoded, CharacterPath);
            Assert.Equal(150, character.ConquerPoints);
            Assert.Equal(!missingCount, character.Attributes.ContainsKey("InventorySashCount"));
            var itemBytes = File.ReadAllBytes(ItemPath);
            var parsedItems = new PlayerItemSnapshotLoader(Items).LoadByCharacterUid(Uid);
            Assert.Equal(itemBytes, PlayerItemSnapshotLoader.ToLegacyBytes(parsedItems));
            Session = Current with { Character = character, PlayerItems = parsedItems };
            Sessions.Set(Connection, Session);
            // No action has begun. Attach genuinely initialized recovery for the
            // final complete encoded files rather than inventing a ready flag.
            Recovery = new(Users, Items);
            Assert.True(Recovery.TryEnsureInitialized(out var reason), reason);
            Assert.True(Recovery.IsReadyFor(Users, Items));
            Context = Context with { Session = Session, TradeRecovery = Recovery };
            Assert.Equal(beforeBytes, File.ReadAllBytes(CharacterPath));
            Assert.Equal(itemBytes, File.ReadAllBytes(ItemPath));
            return (beforeBytes, afterBytes, afterText, encoding.CodePage, encoding.GetPreamble());
        }
    }
}
