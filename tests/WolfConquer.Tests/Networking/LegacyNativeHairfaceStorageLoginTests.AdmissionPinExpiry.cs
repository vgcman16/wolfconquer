using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHairfaceStorageLoginTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RejectedNativePrefixCannotPersistExpiredPinOrIssueAuthorizationOrAnswerOk(bool foreignHairface)
    {
        using var input = new LoginInput();
        WriteAdmissionPinExpiryInput(input, ExpiredAdmissionPinDeadline, malformedHairface: !foreignHairface);
        var files = CaptureAdmissionFiles(input);
        await WithAdmissionPinExpiryHandler(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectAdmissionPinWire(fixture);
            var native = birth.Connection.NativeGameClient!;
            LegacyNativeGameClientRuntime? foreign = null;
            if (foreignHairface)
            {
                var (_, other) = await ConnectBornAsync(fixture);
                foreign = other.Connection.NativeGameClient!;
                native.HairfaceStorage = new(foreign);
            }
            var beforePlayer = ObservePrefixPlayer(native.Player);
            var beforePin = AdmissionPinFields(native.Player);
            var beforeHairface = native.HairfaceStorage;
            var foreignPlayer = foreign is null ? null : ObservePrefixPlayer(foreign.Player);

            Assert.Null((await SendLoginAsync(fixture, wire)).After);

            Assert.Equal(beforePlayer, ObservePrefixPlayer(native.Player));
            Assert.Equal(beforePin, AdmissionPinFields(native.Player));
            Assert.Same(beforeHairface, native.HairfaceStorage);
            Assert.Null(native.Beasts);
            Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection));
            if (foreign is not null) Assert.Equal(foreignPlayer, ObservePrefixPlayer(foreign.Player));
            AssertAdmissionFiles(files);
            Assert.False(LegacyTradeJournal.HasPending(input.Users));
            Assert.DoesNotContain("ordinary admission committed key=", log.Text);
            await fixture.CloseClientTransportAsync();
            AssertNoAdmissionAnswerOk(wire.CopyCapturedServerFramesAtEof());
        });
    }

    [Fact]
    public async Task DeniedJointCharacterWriteRetainsExpiredPinAndCanRetryWithoutARejectedLoginAck()
    {
        using var input = new LoginInput();
        var before = WriteAdmissionPinExpiryInput(input, ExpiredAdmissionPinDeadline);
        var expected = ExpectedAdmissionPinExpiryBytes(before, ExpiredAdmissionPinDeadline, expired: true);
        var files = CaptureAdmissionFiles(input);
        await WithAdmissionPinExpiryHandler(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectAdmissionPinWire(fixture);
            var native = birth.Connection.NativeGameClient!;
            var beforePlayer = ObservePrefixPlayer(native.Player);
            var beforePin = AdmissionPinFields(native.Player);
            using (var denied = new FileStream(input.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Assert.Null((await SendLoginAsync(fixture, wire)).After);
                Assert.Equal(beforePlayer, ObservePrefixPlayer(native.Player));
                Assert.Equal(beforePin, AdmissionPinFields(native.Player));
                Assert.Null(native.Beasts); Assert.Null(native.HairfaceStorage);
                Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection));
                AssertAdmissionFiles(files);
                Assert.False(LegacyTradeJournal.HasPending(input.Users));
            }
            var observed = false;
            log.ObserveCommitted = () =>
            {
                observed = true;
                Assert.Equal(expected, File.ReadAllBytes(input.FilePath));
                var session = Assert.IsType<WorldSession>(handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection));
                AssertAdmissionPinBinding(birth.Connection, session, 0, 0, ExpiredAdmissionPinDeadline, verified: true);
            };
            Assert.IsType<WorldSession>((await SendLoginAsync(fixture, wire)).After);
            Assert.True(observed);
            Assert.Equal(expected, File.ReadAllBytes(input.FilePath));
            await fixture.CloseClientTransportAsync();
            var frames = wire.CopyCapturedServerFramesAtEof();
            AssertOneAdmissionAnswerOk(frames); // Both attempts, complete real socket output.
            Assert.Empty(AdmissionPinFrames(frames));
        });
    }

    [Fact]
    public async Task AcceptedExpiredPinIsDurableAndInitializedBeforeAnswerOkAndOldCredentialCannotReviveIt()
    {
        using var input = new LoginInput();
        var before = WriteAdmissionPinExpiryInput(input, ExpiredAdmissionPinDeadline);
        var expected = ExpectedAdmissionPinExpiryBytes(before, ExpiredAdmissionPinDeadline, expired: true);
        await WithAdmissionPinExpiryHandler(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectAdmissionPinWire(fixture);
            var commits = 0;
            log.ObserveCommitted = () =>
            {
                commits++;
                Assert.Equal(expected, File.ReadAllBytes(input.FilePath));
                var current = Assert.IsType<WorldSession>(handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection));
                AssertAdmissionPinBinding(birth.Connection, current, 0, 0, ExpiredAdmissionPinDeadline, verified: true);
            };
            var admitted = Assert.IsType<WorldSession>((await SendLoginAsync(fixture, wire)).After);
            Assert.Equal(1, commits);
            var native = birth.Connection.NativeGameClient!;
            Assert.Equal((0U, 0U, ExpiredAdmissionPinDeadline, true), AdmissionPinFields(native.Player));
            Assert.Same(admitted, (await SendLoginAsync(fixture, wire)).After);
            Assert.Equal(1, commits);
            await SendAdmissionPinPacket(fixture, wire, new(1, 999, 111)); // Verify is ignored when the PIN is now zero.
            Assert.Same(admitted, handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection));
            AssertAdmissionPinBinding(birth.Connection, admitted, 0, 0, ExpiredAdmissionPinDeadline, verified: true);
            Assert.Equal(expected, File.ReadAllBytes(input.FilePath));
            Assert.False(LegacyTradeJournal.HasPending(input.Users));
            await fixture.CloseClientTransportAsync();
            var frames = wire.CopyCapturedServerFramesAtEof();
            Assert.Equal(LegacyMsgMessagePacket.AnswerOk().ToFrameBytes(), frames[0]);
            AssertOneAdmissionAnswerOk(frames);
            Assert.Empty(AdmissionPinFrames(frames));
        });
    }

    [Fact]
    public async Task UnexpiredResetRetainsPasswordAndRequiresTheActualMatchingNewPasswordField()
    {
        using var input = new LoginInput();
        var deadline = new DateTime(2099, 1, 1, 0, 0, 0, DateTimeKind.Unspecified);
        var before = WriteAdmissionPinExpiryInput(input, deadline);
        var expected = ExpectedAdmissionPinExpiryBytes(before, deadline, expired: false);
        await WithAdmissionPinExpiryHandler(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectAdmissionPinWire(fixture);
            var admitted = Assert.IsType<WorldSession>((await SendLoginAsync(fixture, wire)).After);
            AssertAdmissionPinBinding(birth.Connection, admitted, 111, 1, deadline, verified: false);
            Assert.Equal((111U, 1U, deadline, false), AdmissionPinFields(birth.Connection.NativeGameClient!.Player));
            await SendAdmissionPinPacket(fixture, wire, new(1, 111, 999));
            AssertAdmissionPinBinding(birth.Connection, admitted, 111, 1, deadline, verified: false);
            await SendAdmissionPinPacket(fixture, wire, new(1, 999, 111));
            AssertAdmissionPinBinding(birth.Connection, admitted, 111, 1, deadline, verified: true);
            Assert.Same(admitted, (await SendLoginAsync(fixture, wire)).After);
            AssertAdmissionPinBinding(birth.Connection, admitted, 111, 1, deadline, verified: true);
            Assert.Equal(expected, File.ReadAllBytes(input.FilePath));
            await fixture.CloseClientTransportAsync();
            var frames = wire.CopyCapturedServerFramesAtEof();
            AssertOneAdmissionAnswerOk(frames);
            var pinFrames = AdmissionPinFrames(frames);
            Assert.Equal(3, pinFrames.Length);
            Assert.Equal(new LegacySecondaryPasswordPacket(5, 1, 0).ToFrameBytes(), pinFrames[0]);
            Assert.Equal(new LegacySecondaryPasswordPacket(6, 0, 0).ToFrameBytes(), pinFrames[1]);
            Assert.Equal(new LegacySecondaryPasswordPacket(5, 257, 0).ToFrameBytes(), pinFrames[2]);
        });
    }
}
