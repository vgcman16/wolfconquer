using WolfConquer.Core.World;
using Cache = WolfConquer.Core.World.LegacyNativeBeastsAtrribute;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeHairfaceStorageLoginTests
{
    [Theory]
    [InlineData("empty")]
    [InlineData("missing-first")]
    [InlineData("shuffled")]
    [InlineData("zero-required")]
    [InlineData("wrong-row-level")]
    [InlineData("fixed-overflow")]
    public async Task RealLoginRejectsIncompleteBeastProgressionBeforeAnyPrefixEffects(string invalid)
    {
        using var input = new LoginInput();
        var bytes = WritePrefixFields(input,
            "TailedBeastActivated=True\r\nTailedBeastTotalPoints=255\r\n");
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var rows = new Dictionary<byte, Cache.Attribute>();
            switch (invalid)
            {
                case "missing-first": rows.Add(2, new() { Level = 2, RequiredPoints = 10 }); break;
                case "shuffled":
                    rows.Add(2, new() { Level = 2, RequiredPoints = 20 });
                    rows.Add(1, new() { Level = 1, RequiredPoints = 10 }); break;
                case "zero-required": rows.Add(1, new() { Level = 1, RequiredPoints = 0 }); break;
                case "wrong-row-level": rows.Add(1, new() { Level = 2, RequiredPoints = 10 }); break;
                case "fixed-overflow": rows.Add(1, new() { Level = 1, RequiredPoints = 1 }); break;
            }
            Cache.Attributes = rows; // WithHandlerAsync owns/restores this exact metadata seam.
            var (wire, birth) = await ConnectBornAsync(fixture);
            var client = birth.Connection.NativeGameClient!;
            var before = ObservePrefixPlayer(client.Player);

            Assert.Null((await SendLoginAsync(fixture, wire)).After);

            Assert.Equal(before, ObservePrefixPlayer(client.Player));
            Assert.Null(client.Beasts);
            Assert.Null(client.HairfaceStorage);
            Assert.Equal(bytes, File.ReadAllBytes(input.FilePath));
            Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection));
            Assert.DoesNotContain("legacy MsgLoginGame Success", log.Text);
        });
    }

    [Theory]
    [InlineData("beast-metadata")]
    [InlineData("hairface-metadata")]
    [InlineData("beast-owner-state")]
    [InlineData("same-length-file")]
    public async Task PreparedPrefixRechecksActualMetadataOwnersAndExactSaveBeforeCommit(string drift)
    {
        using var catalog = new PrefixCatalogScope(SavedCatalogRow());
        using var input = new LoginInput();
        var bytes = WritePrefixFields(input, "HairfaceStorage=1/4201/0/0/\r\n");
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (_, birth) = await ConnectBornAsync(fixture);
            var client = birth.Connection.NativeGameClient!;
            var beasts = new LegacyNativeBeastsRuntime(client) { Points = 17 };
            client.Beasts = beasts;
            var before = ObservePrefixPlayer(client.Player);
            var snapshot = ReadPrefixSnapshot(input);
            Assert.True(LegacyNativeExistingCharacterPrefix.TryPrepare(birth.Connection, snapshot, input.Users,
                out var prepared, out var reason), reason);
            var expectedFile = bytes;
            switch (drift)
            {
                case "beast-metadata": Cache.Attributes[1].RequiredPoints++; break;
                case "hairface-metadata": LegacyNativeHairfaceStorageTable.Hairfaces[0].Cost++; break;
                case "beast-owner-state": beasts.Points++; break;
                case "same-length-file":
                    var timestamp = File.GetLastWriteTimeUtc(input.FilePath);
                    expectedFile = bytes.ToArray();
                    expectedFile[^3] ^= 1;
                    File.WriteAllBytes(input.FilePath, expectedFile);
                    File.SetLastWriteTimeUtc(input.FilePath, timestamp);
                    break;
            }

            Assert.False(prepared!.TryCommit(out var rejected, out _));

            Assert.Same(snapshot, rejected);
            Assert.Equal(before, ObservePrefixPlayer(client.Player));
            Assert.Same(beasts, client.Beasts);
            Assert.Equal(drift == "beast-owner-state" ? 18U : 17U, beasts.Points);
            Assert.Null(client.HairfaceStorage);
            Assert.Equal(expectedFile, File.ReadAllBytes(input.FilePath));
            Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection));
            Assert.DoesNotContain("legacy MsgLoginGame Success", log.Text);
        });
    }

    [Fact]
    public async Task DeniedExactOnlineReplacementCanRetryWithoutPublishingPartialNativeState()
    {
        using var input = new LoginInput();
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (_, birth) = await ConnectBornAsync(fixture);
            var client = birth.Connection.NativeGameClient!;
            var before = ObservePrefixPlayer(client.Player);
            var snapshot = ReadPrefixSnapshot(input);
            Assert.True(LegacyNativeExistingCharacterPrefix.TryPrepare(birth.Connection, snapshot, input.Users,
                out var prepared, out var reason), reason);
            using (var held = new FileStream(input.FilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                Assert.False(prepared!.TryCommit(out var rejected, out reason));
                Assert.StartsWith("snapshotWrite:", reason);
                Assert.Same(snapshot, rejected);
                Assert.Equal(before, ObservePrefixPlayer(client.Player));
                Assert.Null(client.Beasts);
                Assert.Null(client.HairfaceStorage);
                Assert.Equal(input.InitialBytes, File.ReadAllBytes(input.FilePath));
            }
            Assert.True(prepared!.TryCommit(out var accepted, out reason), reason);
            Assert.Equal(1L, accepted.Online);
            Assert.Equal(1U, client.Player.Online);
            AssertMarkedFile(input);
            Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection));
        });
    }

    [Fact]
    public async Task EmptySavedHairfaceDoesNotRequireAnUnrelatedOptionalCatalog()
    {
        var previous = LegacyNativeHairfaceStorageTable.Hairfaces;
        try
        {
            LegacyNativeHairfaceStorageTable.Hairfaces = null!;
            using var input = new LoginInput();
            await WithHandlerAsync(input, async (fixture, handler, log) =>
            {
                var (wire, birth) = await ConnectBornAsync(fixture);
                Assert.IsType<WorldSession>((await SendLoginAsync(fixture, wire)).After);
                AssertNewHairface(birth.Connection.NativeGameClient!);
                AssertMarkedFile(input);
            });
        }
        finally { LegacyNativeHairfaceStorageTable.Hairfaces = previous; }
    }

    [Fact]
    public async Task NativeOnlineCounterOverflowRefusesWithoutWrappingOrMarkingTheSave()
    {
        using var input = new LoginInput();
        await WithHandlerAsync(input, async (fixture, handler, log) =>
        {
            var (wire, birth) = await ConnectBornAsync(fixture);
            var client = birth.Connection.NativeGameClient!;
            client.Player.Online = uint.MaxValue;
            var before = ObservePrefixPlayer(client.Player);
            Assert.Null((await SendLoginAsync(fixture, wire)).After);
            Assert.Equal(before, ObservePrefixPlayer(client.Player));
            Assert.Null(client.HairfaceStorage);
            Assert.Null(client.Beasts);
            Assert.Equal(input.InitialBytes, File.ReadAllBytes(input.FilePath));
            Assert.Null(handler.ReadCurrentSessionForNativeOwnerObservation(birth.Connection));
            Assert.DoesNotContain("legacy MsgLoginGame Success", log.Text);
        });
    }
}
