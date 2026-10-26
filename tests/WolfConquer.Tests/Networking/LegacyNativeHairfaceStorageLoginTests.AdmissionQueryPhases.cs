using System.Buffers;
using System.Text;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHairfaceStorageLoginTests
{
    [Theory]
    [InlineData(0, 0u, 0L)]
    [InlineData(1, 24u, 1L)]
    public async Task NativeAdmissionPreservesRawHpBeforeActualQueryAndSavesOnlyTheModeledRuntimeProjection(
        int vitality, uint maximum, long hitpoints)
    {
        using var input = new LoginInput();
        PrepareAdmissionInput(input, starter: true);
        var text = File.ReadAllText(input.FilePath).Replace("Vitaliti=0\r\n",
            "Vitaliti=" + vitality + "\r\n", StringComparison.Ordinal);
        File.WriteAllText(input.FilePath, text);
        var committed = Encoding.UTF8.GetBytes(text.Replace("Online=7\r\n", "Online=1\r\n", StringComparison.Ordinal));
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectAdmissionPinWire(fixture);
            var observed = 0;
            ObserveRawAdmissionCommit(log, () =>
            {
                observed++;
                Assert.Equal(committed, File.ReadAllBytes(input.FilePath));
                var current = Assert.IsType<WorldSession>(handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection));
                Assert.Equal(1L, current.Character.MinHitPoints);
            });
            var admitted = Assert.IsType<WorldSession>((await SendLoginAsync(fixture, wire)).After);
            Assert.Equal(1, observed);
            Assert.Equal(hitpoints, admitted.Character.MinHitPoints);
            var expected = hitpoints == 1 ? committed :
                OrdinaryAdmissionImageExpectations.RuntimeHpSave(committed, hitpoints, 0, 0);
            Assert.Equal(expected, File.ReadAllBytes(input.FilePath));
            await fixture.CloseClientTransportAsync();
            var frames = wire.CopyCapturedServerFramesAtEof();
            AssertOneAdmissionAnswerOk(frames);
            var heroIndex = Array.FindIndex(frames, bytes => ReadOrdinaryHpFrame(bytes).Header.Opcode == LegacyProtoHeroInfoPacket.Opcode);
            Assert.True(heroIndex >= 0);
            Assert.True(LegacyProtoHeroInfoPacket.TryParse(ReadOrdinaryHpFrame(frames[heroIndex]), out var hero));
            Assert.Equal(1UL, hero.GetNumericField(18));
            var queryIndex = Array.FindIndex(frames, bytes => LegacyShowEquipmentPacket.TryParse(ReadOrdinaryHpFrame(bytes), out _));
            Assert.True(queryIndex > heroIndex);
            AssertNativeOrdinaryHpQuery(frames, queryIndex, maximum, hitpoints);
            Assert.Equal(expected, File.ReadAllBytes(input.FilePath));
        }, worldData: AdmissionCatalog());
    }

    private static void AssertNativeOrdinaryHpQuery(byte[][] frames, int start, uint maximum, long hitpoints)
    {
        Assert.InRange(start, 0, frames.Length - 10);
        Assert.True(LegacyShowEquipmentPacket.TryParse(ReadOrdinaryHpFrame(frames[start]), out var equipment));
        Assert.Equal(LegacyShowEquipmentPacket.ShowAction, equipment.Action);
        Assert.True(LegacyServerInfoPacket.TryParse(ReadOrdinaryHpFrame(frames[start + 1]), out _));
        Assert.True(LegacyPlayerStatusPacket.TryParse(ReadOrdinaryHpFrame(frames[start + 4]), out var status));
        Assert.Equal(Uid, status.Uid); Assert.Equal(maximum, status.MaxHP);
        foreach (var offset in new[] { 2, 5, 7 })
        {
            Assert.True(LegacyMsgUpdatePacket.TryParse(ReadOrdinaryHpFrame(frames[start + offset]), out var max));
            Assert.Equal(Uid, max.Uid);
            var update = Assert.Single(max.Updates);
            Assert.Equal(new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.MaxHitpoints, maximum), update);
            Assert.True(LegacyMsgUpdatePacket.TryParse(ReadOrdinaryHpFrame(frames[start + offset + 1]), out var pair));
            Assert.Equal(Uid, pair.Uid);
            var expectedHitpoints = offset == 2 ? 1UL : (ulong)hitpoints;
            Assert.Equal(new[] { new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.MaxHitpoints, maximum),
                new LegacyMsgUpdateEntry(LegacyMsgUpdateDataType.Hitpoints, expectedHitpoints) }, pair.Updates);
        }
        Assert.True(LegacyUserTotalRefineLevPacket.TryParse(ReadOrdinaryHpFrame(frames[start + 9]), out var refinement));
        Assert.Equal(Uid, refinement.Uid);
    }
    private static PacketFrame ReadOrdinaryHpFrame(byte[] bytes)
    {
        Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _));
        return frame;
    }

}
