using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Runtime.ExceptionServices;
using System.Text.RegularExpressions;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedAdmissionFixtureTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OwnedFixtureAppearanceApiOrActualCliPublishesSupportedLookEightInActual2221FieldThree(bool useCli)
    {
        var options = await CreateCollectionStartupFixtureAsync(useCli);
        AssertOwnedAppearanceSaved(options, 1008);
        var before = Files(Destination);

        var frames = await CaptureOwnedAppearanceFrameAsync(options);

        AssertOwnedAppearanceHero(frames, 1_541_008UL, 8, [0x18, 0x90, 0x87, 0x5E]);
        AssertOwnedAppearanceSaved(options, 1008);
        AssertOwnedAppearanceParticipants(before);
        AssertInitialNamespace(Destination, options.ExpectedDomain);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OwnedFixtureAppearanceActualRawOrEncryptedProgramPublishesTheSameSupportedLookWithoutInventedEquipment(bool encrypted)
    {
        var options = await CreateCollectionStartupFixtureAsync(useCli: encrypted);
        AssertOwnedAppearanceSaved(options, 1008);
        OwnedHostSeed.Write(Destination);
        var before = Files(Destination);

        var frames = await CaptureOwnedAppearanceHostAsync(options, encrypted);

        AssertOwnedAppearanceHero(frames, 1_541_008UL, 8, [0x18, 0x90, 0x87, 0x5E]);
        AssertOwnedAppearanceSaved(options, 1008);
        AssertOwnedAppearanceParticipants(before);
        AssertInitialNamespace(Destination, options.ExpectedDomain);
    }

    [Fact]
    public async Task OwnedFixtureAppearanceExistingBody1003IsNotGloballyRemappedOrRewrittenAtAdmission()
    {
        var options = await CreateCollectionStartupFixtureAsync(useCli: false);
        var path = Path.Combine(options.UsersRoot, AccountUid + ".ini");
        var text = CharacterSnapshotFileEncoding.ReadAllText(path, out var encoding);
        Assert.Equal(1, Regex.Matches(text, "(?m)^Body=1008(?=\\r?$)").Count);
        text = Regex.Replace(text, "(?m)^Body=1008(?=\\r?$)", "Body=1003");
        File.WriteAllBytes(path, encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray());
        var before = Files(Destination);

        var frames = await CaptureOwnedAppearanceFrameAsync(options);

        AssertOwnedAppearanceHero(frames, 1_541_003UL, 3, [0x18, 0x8B, 0x87, 0x5E]);
        AssertOwnedAppearanceSaved(options, 1003);
        AssertOwnedAppearanceParticipants(before);
        var savedText = CharacterSnapshotFileEncoding.Decode(before[Path.GetRelativePath(Destination, path)], out var savedEncoding);
        Assert.Equal(1, Regex.Matches(savedText, "(?m)^Online=0(?=\\r?$)").Count);
        var expected = Regex.Replace(savedText, "(?m)^Online=0(?=\\r?$)", "Online=1");
        Assert.Equal(savedEncoding.GetPreamble().Concat(savedEncoding.GetBytes(expected)).ToArray(), File.ReadAllBytes(path));
    }

    private static void AssertOwnedAppearanceSaved(LegacyOwnedAdmissionOptions options, long body)
    {
        var loader = options.CreateSnapshotLoader();
        var saved = Assert.IsType<CharacterSnapshot>(loader.LoadByAccountId(AccountUid));
        Assert.Equal((long)AccountUid, saved.Uid);
        Assert.Equal((body, 154L, 27L), (saved.Body, saved.Face, saved.Hair));
        Assert.Equal((120L, 10L, 0L), (saved.Level, saved.Class, saved.Experience));
        Assert.Equal((100L, 50L, 0L, 0L), (saved.Money, saved.ConquerPoints, saved.BankConquerPoints, saved.BoundConquerPoints));
        var items = loader.LoadPlayerItemsByCharacterUid(AccountUid);
        var potion = Assert.Single(items.InventoryItems);
        Assert.Equal((ItemUid, 1_001_000u), (potion.Uid, potion.ItemId));
        Assert.Empty(items.EquipmentItems);
        Assert.Empty(items.WarehouseItems);
        Assert.Empty(loader.LoadCombatGearByCharacterUid(AccountUid).Items);
    }

    private void AssertOwnedAppearanceParticipants(Dictionary<string, byte[]> before)
    {
        var after = Files(Destination);
        Assert.Equal(before.Keys.Order(StringComparer.OrdinalIgnoreCase), after.Keys.Order(StringComparer.OrdinalIgnoreCase));
        var character = Path.Combine("Users", AccountUid + ".ini");
        foreach (var (path, bytes) in before)
            if (!string.Equals(path, character, StringComparison.OrdinalIgnoreCase)) Assert.Equal(bytes, after[path]);
        // Character HP/derived scalar writes belong to the later native query
        // phase; these tests pin appearance/economy above, all other files here.
    }

    private static async Task<byte[][]> CaptureOwnedAppearanceFrameAsync(LegacyOwnedAdmissionOptions options)
    {
        var loader = options.CreateSnapshotLoader();
        Assert.True(LegacyOwnedAdmissionRuntime.TryOpen(loader, options, out var opened, out var reason), reason);
        var runtime = Assert.IsType<LegacyOwnedAdmissionRuntime>(opened);
        var sent = new ConcurrentQueue<byte[]>();
        ConquerTcpConnection? connection = null;
        var failures = new List<Exception>();
        try
        {
            var handler = new WorldServerFrameHandler(loader, null, LegacyItemWorldDataFixture.Create([], []),
                LegacyRuntimeUpdateLoopOptions.Disabled, TextWriter.Null,
                protocolProfile: ConquerProtocolProfile.VisibleDx8D13, ownedAdmission: runtime);
            connection = new(null, new IPEndPoint(IPAddress.Loopback, 9958),
                (bytes, _) => { sent.Enqueue(bytes.ToArray()); return ValueTask.CompletedTask; });
            await handler.HandleFrameAsync(connection, CollectionStartupFrame(new LegacyMsgLoginGamePacket(AccountUid).ToFrameBytes()));
        }
        catch (Exception error) { failures.Add(error); }
        try { if (connection is not null) await connection.CompleteTransportCloseAsync(); } catch (Exception error) { failures.Add(error); }
        try { await runtime.DisposeAsync(); } catch (Exception error) { failures.Add(error); }
        RethrowOwnedAppearanceFailures(failures);
        return sent.ToArray();
    }

    private async Task<byte[][]> CaptureOwnedAppearanceHostAsync(LegacyOwnedAdmissionOptions options, bool encrypted)
    {
        var host = new OwnedWorldServerProcess(options, Destination, ConfigurationPath, encrypted);
        collectionHost = host; // Existing enclosing cleanup retains a still-running owned child.
        OwnedHostWireClient? client = null;
        byte[][] frames = [];
        var failures = new List<Exception>();
        try
        {
            await host.StartListeningAsync();
            client = await OwnedHostWireClient.ConnectAsync(host.Port, encrypted);
            await client.SendAsync(new LegacyMsgLoginGamePacket(AccountUid).ToFrameBytes());
            frames = await client.ReadThroughHeroAsync(throughOpcode: 2221);
            await host.ReadUntilAsync(line => line.Contains($"legacy MsgLoginGame Success key={AccountUid} uid={AccountUid}", StringComparison.Ordinal));
        }
        catch (Exception error) { failures.Add(error); }
        try { if (client is not null) await client.DisposeAsync(); } catch (Exception error) { failures.Add(error); }
        try { await host.DisposeAsync(); } catch (Exception error) { failures.Add(error); }
        RethrowOwnedAppearanceFailures(failures);
        return frames;
    }

    private static void AssertOwnedAppearanceHero(byte[][] frames, ulong mesh, ulong look, byte[] meshField)
    {
        Assert.NotEmpty(frames);
        Assert.Equal(LegacyMsgMessagePacket.AnswerOk().ToFrameBytes(), frames[0]);
        var frame = Assert.Single(frames, bytes => bytes.Length >= 4 && BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(2, 2)) == 2221);
        Assert.Equal(frame.Length, BinaryPrimitives.ReadUInt16LittleEndian(frame));
        var values = new Dictionary<int, ulong>();
        var offset = 4;
        while (offset < frame.Length)
        {
            var start = offset;
            var tag = ReadOwnedAppearanceVarint(frame, ref offset);
            Assert.NotEqual(0UL, tag);
            var field = checked((int)(tag >> 3));
            var wire = tag & 7;
            if (wire == 0)
            {
                Assert.True(values.TryAdd(field, ReadOwnedAppearanceVarint(frame, ref offset)));
                if (field == 3) Assert.Equal(meshField, frame.AsSpan(start, offset - start).ToArray());
            }
            else
            {
                Assert.Equal(2UL, wire);
                var length = ReadOwnedAppearanceVarint(frame, ref offset);
                Assert.InRange(length, 0UL, (ulong)(frame.Length - offset));
                offset += checked((int)length);
            }
        }
        Assert.Equal((ulong)AccountUid, values[1]);
        Assert.Equal(mesh, values[3]); // Native D13 parser field3/tag0x18, not the private hero parser.
        Assert.Equal(look, values[3] % 1000); // Native look setter receives mesh modulo1000.
        Assert.Equal(154UL, values[3] / 10000);
        Assert.Equal(27UL, values[4]);
        Assert.Equal(100UL, values[5]);
        Assert.Equal(50UL, values[6]);
        Assert.Equal(0UL, values[7]);
        Assert.Equal(120UL, values[21]);
        Assert.Equal(10UL, values[22]);
    }

    private static ulong ReadOwnedAppearanceVarint(byte[] bytes, ref int offset)
    {
        ulong value = 0;
        for (var index = 0; index < 10; index++)
        {
            Assert.InRange(offset, 0, bytes.Length - 1);
            var next = bytes[offset++];
            if (index == 9) Assert.InRange(next, (byte)0, (byte)1);
            value |= (ulong)(next & 127) << (index * 7);
            if ((next & 128) == 0) return value;
        }
        throw new InvalidDataException("Owned appearance hero has an unterminated varint.");
    }

    private static void RethrowOwnedAppearanceFailures(List<Exception> failures)
    {
        var distinct = new List<Exception>();
        foreach (var error in failures)
        {
            IEnumerable<Exception> parts = error is AggregateException aggregate ? aggregate.Flatten().InnerExceptions : new[] { error };
            foreach (var part in parts)
                if (!distinct.Any(previous => ReferenceEquals(previous, part))) distinct.Add(part);
        }
        if (distinct.Count == 1) ExceptionDispatchInfo.Capture(distinct[0]).Throw();
        if (distinct.Count > 1) throw new AggregateException("Owned appearance assertion/transport cleanup failed.", distinct);
    }
}
