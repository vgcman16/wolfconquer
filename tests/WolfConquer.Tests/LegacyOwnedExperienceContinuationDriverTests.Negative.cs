using System.Text;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedExperienceContinuationDriverTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ActualEarnedCheckpointWithTamperedSavedImageStillBlocksRecovery(bool corruptMarker) => Run(async (f, monster, catalog) =>
    {
        var before = f.Current();
        var binding = Assert.IsType<LegacyOrdinarySpellBinding>(before.NativeGameClient!.OrdinarySpellBinding);
        Dictionary<string, byte[]>? expected = null;
        int interrupted = 0;
        f.BeforeSend = (_, bytes) =>
        {
            if (binding.Player.Level != 139 || OwnedEquipmentBehaviorFixture.ReadFrame(bytes).Header.Opcode != LegacyPlayerStatusPacket.Opcode) return;
            interrupted++;
            expected = f.Data.Files();
            var key = Path.GetRelativePath(f.Data.Root, f.CharacterPath());
            var text = Encoding.UTF8.GetString(expected[key]);
            const string field = "ExperienceContinuation=";
            Assert.Contains(field, text, StringComparison.Ordinal);
            Assert.Equal(text.IndexOf(field, StringComparison.Ordinal), text.LastIndexOf(field, StringComparison.Ordinal));
            // Deliberate negative disk mutation after the genuine award and
            // first step. No saved value is used to mint an owner or receipt.
            var changed = Encoding.UTF8.GetBytes(corruptMarker
                ? text.Replace(field, field + "x", StringComparison.Ordinal)
                : text + "; changed after actual checkpoint\r\n");
            File.WriteAllBytes(f.CharacterPath(), changed);
            expected[key] = changed;
            throw new IOException("owned continuation marker corruption before output failure");
        };
        try { await ExpectHitFailure(f, monster); }
        finally { f.BeforeSend = null; }
        Assert.Equal(1, interrupted); Assert.NotNull(expected);
        Assert.True(f.Recovery.IsBlocked);
        Assert.False(f.Store.TryGet(f.Connection(OwnedEquipmentBehaviorFixture.Actor), out _));
        Assert.True(binding.IsRetired);
        f.Data.AssertFiles(expected);
        Assert.DoesNotContain("WorldServer sent legacy monster experience ", f.Log.ToString(), StringComparison.Ordinal);
    });
}
