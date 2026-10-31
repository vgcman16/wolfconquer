using System.Buffers.Binary;
using System.Globalization;
using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.Runtime;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyOwnedWindWalkerQueryTests
{
    private static async Task ExecuteActualWindDeathPublication(WindFixture f)
    {
        var frame = f.Owner.Handler; var connection = f.Owner.Connection(Uid); var before = f.Current;
        var contextFactory = typeof(WorldServerFrameHandler).GetMethod("CreateTradeDisconnectContext", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var context = Assert.IsType<WorldPacketHandlerContext>(contextFactory.Invoke(frame, [connection]));
        var options = ReadWindLifetimeField<LegacyRuntimeUpdateLoopOptions>(f, "_runtimeUpdateLoop");
        Assert.Same(f.Clock, options.TimeProvider);
        context = context with
        {
            Session = before, WorldData = ReadWindLifetimeField<WorldDataCatalog>(f, "_worldData"),
            TeamStates = ReadWindLifetimeField<TeamSessionStateStore>(f, "_teamStates"),
            RuneRankings = ReadWindLifetimeField<LegacyRuneRankingState>(f, "_runeRankings"),
            PlayerSpellsRoot = f.Owner.Data.Spells, PlayerProficienciesRoot = f.Owner.Data.Proficiencies,
            PlayerQuestsRoot = f.Owner.Data.Quests, ProtocolProfile = ConquerProtocolProfile.VisibleDx8D13,
            TimeProvider = options.TimeProvider, LegacyClock = options.LegacyClock,
        };
        Assert.Same(f.Store, context.Sessions); Assert.NotNull(context.TradeOperations);
        using var operation = await context.TradeOperations!.EnterSharedAsync();
        context = context with { FrameOperationLease = operation };
        var peer = f.Owner.Current(Peer);
        var scope = new LegacyEquipmentQueryPublicationScope(Uid, RequiredParticipant: new(f.Owner.Connection(Peer), peer));
        var causeType = typeof(WorldPacketHandlers).GetNestedType("LegacyNormalPlayerDeathCause", BindingFlags.NonPublic)!;
        var cause = Activator.CreateInstance(causeType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null, args: [checked((ushort)before.Character.X), checked((ushort)before.Character.Y), peer.Character.Uid, peer.Character.Name],
            culture: CultureInfo.InvariantCulture);
        var method = typeof(WorldPacketHandlers).GetMethod("ExecuteLegacyNormalPlayerDeathPhasesAsync", BindingFlags.Static | BindingFlags.NonPublic)!;
        var pending = Assert.IsType<ValueTask<LegacyEquipmentQueryPublicationResult>>(method.Invoke(null,
            [context, before, scope, f.Native.Player, cause, CancellationToken.None]));
        var result = await pending; Assert.True(result.Completed); Assert.Same(f.Current, result.Session);
    }

    private static T ReadWindLifetimeField<T>(WindFixture f, string name) => Assert.IsType<T>(
        typeof(WorldServerFrameHandler).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(f.Owner.Handler));

    private static void AssertWindLifetimeRow(WindFixture f, LegacyNativeStatusFlagsBigVector32.Flag row, DateTime timer, DateTime deadline)
    {
        AssertJoined(f, true); Assert.Same(row, f.Row); Assert.Equal(timer, row.Timer);
        Assert.Equal(deadline, row.Timer.AddSeconds(row.Secounds)); Assert.False(row.RemoveOnDead);
    }

    private static byte[] ReplaceWindLifetimeHp(byte[] before, long oldValue, long newValue)
    {
        string text = CharacterSnapshotFileEncoding.Decode(before, out var encoding);
        string line = "\r\nMinHitPoints=" + oldValue.ToString(CultureInfo.InvariantCulture) + "\r\n";
        Assert.Equal(2, text.Split(line, StringSplitOptions.None).Length);
        string after = text.Replace(line, "\r\nMinHitPoints=" + newValue.ToString(CultureInfo.InvariantCulture) + "\r\n", StringComparison.Ordinal);
        return encoding.GetPreamble().Concat(encoding.GetBytes(after)).ToArray();
    }

    private static void AssertWindLifetimeFiles(WindFixture f, Dictionary<string, byte[]> before, byte[] character)
    {
        var expected = before.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.OrdinalIgnoreCase);
        expected[Path.GetRelativePath(f.Owner.Data.Root, f.Owner.CharacterPath(Uid))] = character;
        AssertUnchangedSaved(f, expected);
        Assert.DoesNotContain("LegacyStatusFlagRemoveOnDead.194", CharacterSnapshotFileEncoding.Decode(character, out _), StringComparison.Ordinal);
    }

    private static void AssertWindLifetimeHpPair(IReadOnlyList<byte[]> frames, uint maximum)
    {
        Assert.True(LegacyMsgUpdatePacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(frames[0]), out var first));
        Assert.Equal(Uid, first.Uid); Assert.Equal(LegacyMsgUpdateDataType.MaxHitpoints, Assert.Single(first.Updates).Type);
        Assert.Equal((ulong)maximum, first.Updates[0].Value);
        Assert.True(LegacyMsgUpdatePacket.TryParse(OwnedEquipmentBehaviorFixture.ReadFrame(frames[1]), out var second));
        Assert.Equal(Uid, second.Uid); Assert.Equal(2, second.Updates.Count);
        Assert.Equal(LegacyMsgUpdateDataType.MaxHitpoints, second.Updates[0].Type); Assert.Equal((ulong)maximum, second.Updates[0].Value);
        Assert.Equal(LegacyMsgUpdateDataType.Hitpoints, second.Updates[1].Type); Assert.Equal((ulong)maximum, second.Updates[1].Value);
    }

    private static Dictionary<string, byte[]> ExpectedWindEquippedFiles(WindFixture f)
    {
        // Independent physical preimage projection for this actual move. The
        // real v2 equipment writer changes only the selected root Position
        // word at DBItem+12; physical order, children, perfection and every
        // opaque byte must survive. No actual AFTER bytes supply expectations.
        const int recordSize = 184, perfectionSize = 80;
        var expected = f.Owner.Data.Files();
        string path = Path.GetRelativePath(f.Owner.Data.Root, f.Owner.ItemPath(Uid));
        byte[] before = expected[path], after = before.ToArray();
        int count = BinaryPrimitives.ReadInt32LittleEndian(before); Assert.Equal(3, count);
        int offset = sizeof(int), selectedRoots = 0;
        var roots = new List<uint>();
        for (int index = 0; index < count; index++)
        {
            Assert.InRange(offset, sizeof(int), before.Length - recordSize);
            uint uid = BinaryPrimitives.ReadUInt32LittleEndian(before.AsSpan(offset)); roots.Add(uid);
            uint children = BinaryPrimitives.ReadUInt32LittleEndian(before.AsSpan(offset + 136));
            if (uid == Left)
            {
                selectedRoots++; Assert.Equal((ushort)0, BinaryPrimitives.ReadUInt16LittleEndian(before.AsSpan(offset + 12)));
                BinaryPrimitives.WriteUInt16LittleEndian(after.AsSpan(offset + 12), 5);
            }
            offset = checked(offset + recordSize + checked((int)children * recordSize));
        }
        Assert.Equal(new[] { BagItem, Left, Right }, roots); Assert.Equal(1, selectedRoots);
        Assert.InRange(offset, sizeof(int), before.Length - sizeof(int));
        int perfections = BinaryPrimitives.ReadInt32LittleEndian(before.AsSpan(offset)); Assert.True(perfections >= 0);
        Assert.Equal(before.Length, checked(offset + sizeof(int) + perfections * perfectionSize));
        expected[path] = after;
        return expected;
    }
}
