using System.Text;
using System.Text.Json.Nodes;
using WolfConquer.Core.World;
using Fixture = WolfConquer.Tests.LegacyFullAccountClassMutationTests.DurableClassFixture;

namespace WolfConquer.Tests;

// New v8 API tests: exclude these files when compiling the old-source baseline.
// Exact owned-file interruption/restart simulations, not process-kill or power-loss proof.
public sealed partial class LegacyCharacterSpellsJournalTests
{
    private static Task RunJournalAsync(Action<Fixture> body) =>
        LegacyFullAccountClassMutationTests.WithDurableClassAsync(fixture => { body(fixture); return Task.CompletedTask; });

    private static LegacyTradeJournalChange[] Changes(Fixture fixture)
    {
        var character = fixture.CharacterBytes;
        // A complete actual Character image, preserving every unrelated byte.
        var afterCharacter = Encoding.Latin1.GetBytes(Encoding.Latin1.GetString(character)
            .Replace("Class=15", "Class=1005", StringComparison.Ordinal));
        var spells = fixture.SpellBytes;
        var afterSpells = Convert.FromHexString("03000000").Concat(spells.Skip(4))
            .Concat(Convert.FromHexString("880500000000000000000000")).ToArray();
        Assert.NotEqual(fixture.Original.AccountId, fixture.Original.Character.Uid);
        return
        [
            new(LegacyTradeJournalFileKind.Character, fixture.Original.AccountId, character, afterCharacter),
            new(LegacyTradeJournalFileKind.Spells, fixture.Original.Character.Uid, spells, afterSpells),
        ];
    }

    private static LegacyTradeRecoveryState Recovery(Fixture fixture, string? spells = null) =>
        new(fixture.Users, null, null, null, null, spells ?? fixture.SpellsRoot, null);

    private static void RequireImages(Fixture fixture, byte[] character, byte[] spells)
    {
        Assert.Equal(character, fixture.CharacterBytes);
        Assert.Equal(spells, fixture.SpellBytes);
        fixture.ExpectImage(fixture.CharacterPath, character);
        fixture.ExpectImage(fixture.SpellPath, spells);
    }

    private static byte[] RememberPending(Fixture fixture)
    {
        var bytes = File.ReadAllBytes(fixture.PendingPath);
        fixture.ExpectImage(fixture.PendingPath, bytes);
        return bytes;
    }

    private static void WritePending(Fixture fixture, string text)
    {
        LegacyNativeDbReadOwnedFiles.OrdinaryChain(fixture.PendingPath);
        var bytes = new UTF8Encoding(false, true).GetBytes(text);
        using (var file = new FileStream(fixture.PendingPath, FileMode.Open, FileAccess.Write, FileShare.None))
        { file.SetLength(0); file.Write(bytes); file.Flush(true); }
        fixture.ExpectImage(fixture.PendingPath, bytes);
    }

    private static JsonObject Pending(Fixture fixture) => Assert.IsType<JsonObject>(JsonNode.Parse(File.ReadAllBytes(fixture.PendingPath)));

    private static void RequireNoExposure(Fixture fixture)
    {
        Assert.True(fixture.Store.TryGet(fixture.Connection, out var current));
        Assert.Same(fixture.Original, current);
        Assert.Empty(fixture.Sent);
        Assert.False(fixture.Disconnected);
    }
}
