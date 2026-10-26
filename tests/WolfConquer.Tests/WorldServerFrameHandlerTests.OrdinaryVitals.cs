using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class WorldServerFrameHandlerTests
{
    [Theory]
    [InlineData(1L, 1L)]
    [InlineData(29_836L, 29_836L)]
    [InlineData(61_258L, 29_836L)]
    public async Task HandlerOrdinaryAdmissionPreservesSavedHitpointsBeforeActualEquipmentQuery(
        long savedHitpoints, long runtimeHitpoints)
    {
        const uint account = 1_293_600;
        var usersRoot = Path.Combine(_usersRoot, "Users");
        Directory.CreateDirectory(usersRoot);
        WriteSnapshot(usersRoot, account, uid: account, classCode: 2_000,
            level: 110, reborn: 0, secondClass: 0, minHitPoints: savedHitpoints);
        var loader = new CharacterSnapshotLoader(usersRoot);
        using var admission = new OrdinaryLoginFixture(loader);
        using var log = new StringWriter();
        var handler = admission.CreateHandler(log);
        var sent = new List<byte[]>();
        var answerOk = LegacyMsgMessagePacket.AnswerOk().ToFrameBytes();
        var observedCommit = 0;
        ConquerTcpConnection? connection = null;
        connection = CreateSendingConnection(sent, (bytes, _) =>
        {
            if (bytes.Span.SequenceEqual(answerOk))
            {
                observedCommit++;
                // Observe the actual Store and file at the real send boundary.
                // The send callback does not install or change any state.
                var admitted = GetSession(handler, connection!);
                Assert.Equal(savedHitpoints, admitted.Character.MinHitPoints);
                var persisted = loader.TryLoadByAccountIdForAdmission(account).Snapshot;
                Assert.NotNull(persisted);
                Assert.Equal(savedHitpoints, persisted.MinHitPoints);
            }
            return ValueTask.CompletedTask;
        });

        await handler.HandleFrameAsync(connection,
            ReadFrame(new LegacyMsgLoginGamePacket(account).ToFrameBytes()));

        Assert.Equal(1, observedCommit);
        Assert.Equal(answerOk, sent[0]);
        // Field18 is the actual admission hero HP, before the live query.
        var heroBytes = Assert.Single(sent.Where(bytes =>
            ReadFrame(bytes).Header.Opcode == LegacyProtoHeroInfoPacket.Opcode));
        Assert.True(LegacyProtoHeroInfoPacket.TryParse(ReadFrame(heroBytes), out var hero));
        Assert.Equal((ulong)savedHitpoints, hero.GetNumericField(18));
        var heroIndex = sent.IndexOf(heroBytes);
        var queryIndex = sent.FindIndex(bytes =>
            LegacyShowEquipmentPacket.TryParse(ReadFrame(bytes), out _));
        Assert.True(queryIndex > heroIndex);
        // Literal fixture cap: 36*3 +447*24 +4*3500 Chi +5000 Dragon =29836.
        // Reuse the exact existing ten-frame query oracle, without filtering.
        var query = LegacySelectedEquipmentQueryAssertions.NeutralQuery(sent, queryIndex, account);
        Assert.Equal(29_836U, query.Status.MaxHP);
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(sent[queryIndex + 3]), out var beforeClamp));
        Assert.Equal((ulong)savedHitpoints, beforeClamp.Updates[1].Value);
        Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(sent[queryIndex + 8]), out var afterClamp));
        Assert.Equal((ulong)runtimeHitpoints, afterClamp.Updates[1].Value);
        Assert.Equal(runtimeHitpoints, GetSession(handler, connection).Character.MinHitPoints);
        await admission.AssertCommittedAndRepeatedAsync(handler, connection, sent,
            expectedRuntimeHitpoints: runtimeHitpoints);
    }
}
