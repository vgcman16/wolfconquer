using System.Buffers;
using System.Globalization;
using System.Net;
using System.Reflection;
using System.Runtime.ExceptionServices;
using WolfConquer.Core.Data;
using WolfConquer.Core.Networking;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;
using WolfConquer.Core.Runtime;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyOwnedActorScalarTests
{
    private const uint Uid = LegacyOwnedAdmissionRuntimeFixture.Uid;
    private static async Task Run(Func<ScalarFixture, Task> body, string dynamicId = "907", bool admit = true)
    {
        var f = new ScalarFixture(); var errors = new List<Exception>();
        try { await f.InitializeAsync(dynamicId, admit); await body(f); } catch (Exception error) { errors.Add(error); }
        try { await f.CloseAsync(); } catch (Exception error) { errors.Add(error); }
        try { f.DisposeMetadata(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count == 0) try { f.Data.Dispose(); } catch (Exception error) { errors.Add(error); }
        if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if (errors.Count > 1) throw new AggregateException(errors);
    }

    private sealed class ScalarFixture
    {
        internal readonly LegacyOwnedAdmissionRuntimeFixture Data = new(bootstrap: false);
        internal readonly StringWriter Log = new();
        internal readonly List<byte[]> Sent = [];
        private readonly CancellationTokenSource lifetime = new();
        private NativeLoginBeastMetadataScope? beasts;
        private Exception? observationFailure;
        internal LegacyOwnedAdmissionRuntime Runtime = null!;
        internal WorldServerFrameHandler Handler = null!;
        internal ConquerTcpConnection Connection = null!;
        internal Action<byte[]>? BeforeSend;
        internal Dictionary<string, byte[]> BeforeLogin = [];
        internal WorldSessionStore Store => Assert.IsType<WorldSessionStore>(typeof(WorldServerFrameHandler)
            .GetField("_sessions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(Handler));
        internal WorldSession Current => Assert.IsType<WorldSession>(Store.TryGet(Connection, out var current) ? current : null);
        internal LegacyNativeGameClientRuntime Native => Connection.NativeGameClient!;
        internal LegacyOrdinarySpellBinding Binding => Assert.IsType<LegacyOrdinarySpellBinding>(Native.OrdinarySpellBinding);
        internal async Task InitializeAsync(string rawDynamic, bool admit)
        {
            var text = CharacterSnapshotFileEncoding.Decode(File.ReadAllBytes(Data.UserPath), out var encoding);
            text = Replace(text, "Agility", "10", "36"); text = Replace(text, "Map", "1002", "1000");
            text = Replace(text, "MinHitPoints", "330", "1");
            text = text.Replace("KeepBytes=startup-owned-value\r\n", "KeepBytes=startup-owned-value\r\n" +
                "DynamicID=" + rawDynamic + "\r\nAction=250\r\nStamina=72\r\nMaxMana=50\r\nExpBallUsed=0\r\n" +
                "EpicTrojan=0/0/0/0/1/1/0/0/0/0/0/0/0/0/1/0/0/0/1/0/\r\n", StringComparison.Ordinal);
            File.WriteAllBytes(Data.UserPath, encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray());
            new PlayerItemSnapshotLoader(Data.Items).SaveByCharacterUid(Uid, PlayerItemSnapshot.Empty);
            var spells = PlayerSpellSnapshotLoader.ToLegacyBytes([new(6001, 4, 1111, 2, 3, 2)]); spells[15] = 0xA5;
            File.WriteAllBytes(Path.Combine(Data.Spells, Uid + ".bin"), spells);
            new PlayerQuestSnapshotLoader(Data.Quests).SaveByCharacterUid(Uid, PlayerQuestSnapshot.Empty);
            new CombatGearSnapshotLoader(Data.CombatGear).SaveByCharacterUid(Uid, CombatGearSnapshot.Empty);
            new LegacyMailboxStore(Data.Root).Save(Uid);
            beasts = new(1000); Data.Bootstrap();
            Assert.True(LegacyOwnedAdmissionRuntime.TryOpen(Data.Loader, Data.Options(), out var opened, out var reason), reason);
            Runtime = Assert.IsType<LegacyOwnedAdmissionRuntime>(opened); BeforeLogin = Data.Files();
            Handler = new(Data.Loader, null, OwnedEquipmentBehaviorFixture.Catalog(), LegacyRuntimeUpdateLoopOptions.Disabled,
                Log, protocolProfile: ConquerProtocolProfile.VisibleDx8D13, ownedAdmission: Runtime);
            Connection = new(null, new IPEndPoint(IPAddress.Loopback, 9958), (memory, token) =>
            {
                token.ThrowIfCancellationRequested(); var bytes = memory.ToArray();
                try { BeforeSend?.Invoke(bytes); }
                catch (Xunit.Sdk.XunitException error) { observationFailure ??= error; throw; }
                Sent.Add(bytes); return ValueTask.CompletedTask;
            }, _ => { lifetime.Cancel(); return ValueTask.CompletedTask; }, new LegacyNativeGameClientRuntime());
            await SendAsync(new LegacyMsgLoginGamePacket(Uid).ToFrameBytes());
            if (admit)
            {
                var expected = BeforeLogin.ToDictionary(row => row.Key, row => row.Value, StringComparer.OrdinalIgnoreCase);
                string key = Path.GetRelativePath(Data.Root, Data.UserPath);
                string before = CharacterSnapshotFileEncoding.Decode(expected[key], out var savedEncoding);
                before = Replace(before, "Online", "0", "1");
                expected[key] = savedEncoding.GetPreamble().Concat(savedEncoding.GetBytes(before)).ToArray();
                Data.AssertFiles(expected); AssertCoherent(this, 430, 380, 250);
            }
            else AssertUnadmitted(this);
            Sent.Clear();
        }
        internal async Task SendAsync(byte[] bytes)
        {
            await Handler.HandleFrameAsync(Connection, Read(bytes), lifetime.Token);
            if (observationFailure is { } error) ExceptionDispatchInfo.Capture(error).Throw();
        }
        internal async Task CloseAsync()
        {
            BeforeSend = null; var errors = new List<Exception>();
            if (Connection is not null)
            {
                try { await Connection.DisconnectAsync(); } catch (Exception error) { errors.Add(error); }
                try { await Connection.CompleteTransportCloseAsync(); } catch (Exception error) { errors.Add(error); }
            }
            if (Runtime is not null) try { await Runtime.DisposeAsync(); } catch (Exception error) { errors.Add(error); }
            lifetime.Dispose();
            if (errors.Count == 1) ExceptionDispatchInfo.Capture(errors[0]).Throw();
            if (errors.Count > 1) throw new AggregateException(errors);
        }
        internal void DisposeMetadata() { beasts?.Dispose(); beasts = null; Log.Dispose(); }
    }

    private static void AssertCoherent(ScalarFixture f, ushort x, ushort y, uint action)
    {
        var current = f.Current; var player = f.Native.Player;
        Assert.True(f.Store.IsBoundSpellOwnerCurrent(f.Binding)); Assert.True(player.HasOwnedActorScalarsAttachment);
        Assert.Equal((byte)2, player.Reborn); Assert.Equal((ushort)36, player.Agility);
        Assert.Equal(x, player.X); Assert.Equal(y, player.Y); Assert.Equal(1000U, player.Map); Assert.Equal(907U, player.DynamicID);
        var values = LegacyNativeActorScalarState.RequireCurrent(current.Character);
        Assert.Equal(new LegacyNativeActorScalarState.Values(2, 36, x, y, 1000, 907), values);
        Assert.Equal(action, (uint)player.Action); Assert.Equal(action, LegacyActionState.RequireCurrent(current.Character));
        Assert.Equal(1, player.HitPoints); Assert.Equal(50U, player.Mana); Assert.Equal((ushort)72, player.Stamina);
        Assert.Equal((1L, 50L), (current.Character.MinHitPoints, current.Character.MinMana));
        Assert.False(f.Native.FullLoading); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
        var saved = PlayerItemSnapshotLoader.ToLegacyBytes(PlayerItemSnapshotLoader.Parse(File.ReadAllBytes(Path.Combine(f.Data.Items, Uid + ".bin"))));
        Assert.Equal(saved, PlayerItemSnapshotLoader.ToLegacyBytes(current.PlayerItems));
        Assert.Equal(saved, PlayerItemSnapshotLoader.ToLegacyBytes(f.Binding.Items.Image.FinalSnapshot));
    }
    private static void AssertUnadmitted(ScalarFixture f)
    {
        f.Data.AssertFiles(f.BeforeLogin); Assert.False(f.Store.TryGet(f.Connection, out _)); Assert.Empty(f.Sent);
        Assert.Null(f.Native.OrdinarySpellBinding); Assert.Null(f.Native.MySpells); Assert.Null(f.Native.Inventory); Assert.Null(f.Native.Rune);
        var player = f.Native.Player; Assert.Equal(0U, player.UID); Assert.Equal((byte)0, player.Reborn);
        Assert.Equal((ushort)0, player.Agility); Assert.Equal((ushort)0, player.X); Assert.Equal((ushort)0, player.Y);
        Assert.Equal(0U, player.Map); Assert.Equal(0U, player.DynamicID); Assert.Equal((ushort)0, player.Px); Assert.Equal((ushort)0, player.Py);
        Assert.Equal(0, player.HitPoints); Assert.Equal(0U, player.Mana); Assert.Equal((ushort)0, player.Stamina);
        Assert.Equal(0U, (uint)player.Action); Assert.False(f.Native.FullLoading); Assert.False(LegacyTradeJournal.HasPending(f.Data.Users));
    }
    private static Dictionary<string, byte[]> MovedFiles(ScalarFixture f, Dictionary<string, byte[]> before, int x, int y)
    {
        var result = before.ToDictionary(row => row.Key, row => row.Value, StringComparer.OrdinalIgnoreCase);
        string key = Path.GetRelativePath(f.Data.Root, f.Data.UserPath);
        string text = CharacterSnapshotFileEncoding.Decode(result[key], out var encoding);
        text = Replace(text, "X", "430", x.ToString(CultureInfo.InvariantCulture));
        text = Replace(text, "Y", "380", y.ToString(CultureInfo.InvariantCulture));
        result[key] = encoding.GetPreamble().Concat(encoding.GetBytes(text)).ToArray(); return result;
    }
    private static string Replace(string text, string key, string before, string after)
    {
        string token = "\r\n" + key + "=" + before + "\r\n";
        Assert.Equal(2, text.Split(token, StringSplitOptions.None).Length);
        return text.Replace(token, "\r\n" + key + "=" + after + "\r\n", StringComparison.Ordinal);
    }
    private static PacketFrame Read(byte[] bytes)
    { Assert.True(PacketFrameDecoder.TryRead(new ReadOnlySequence<byte>(bytes), out var frame, out _, out _)); return frame; }
    private static byte[] Walk() => new MsgMovementPacket { Uid = Uid, Direction = 6, Running = MsgMovementMode.Walk,
        Timestamp = 123456, MapId = 1000 }.ToFrameBytes();
    private static byte[] Jump() => new MsgActionPacket { ObjId = Uid, Type = MsgActionType.Jump, Facing = 6,
        TargetPositionX = 432, TargetPositionY = 381, Timestamp = 123456 }.ToFrameBytes();
}
