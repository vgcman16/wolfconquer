using System.Collections.Concurrent;
using System.Reflection;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Flag = WolfConquer.Core.World.LegacyNativeMsgUpdate.Flags;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeCombatCalculationTests
{
    public static IEnumerable<object[]> PotionPreparationFaults()
    {
        foreach (bool defense in new[] { false, true })
        foreach (bool present in new[] { false, true })
        foreach (string fault in new[] { "missing", "null", "small-message", "disposed-message" })
            yield return [defense, present, fault];
        yield return [false, false, "alias"];
        yield return [true, false, "alias"];
    }

    [Theory]
    [MemberData(nameof(PotionPreparationFaults))]
    public Task PotionPreparationFailurePreservesBothBonusesTimersAndRealStatusAndPriorFrames(
        bool defense, bool present, string fault) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        Assert.True(s.Player.AddFlag(Flag.TeamLeader, 23, true));
        var flag = defense ? Flag.MagicShield : Flag.Stigma;
        if (present) Assert.True(s.Player.BitVector.TryAdd((int)flag, 17, false, 3));
        s.Player.OnAttackPotionStamp = new(2001, 2, 3);
        s.Player.OnDefensePotionStamp = new(2002, 3, 4);
        var words = s.Player.BitVector.bits; var copied = words.ToArray();
        var registry = s.Player.BitVector.ArrayFlags; var entries = registry.ToArray();
        var fields = entries.Select(pair => (pair.Value.Key, pair.Value.Secounds, pair.Value.Timer,
            pair.Value.InvokerSecouds, pair.Value.TimerInvoker, pair.Value.RemoveOnDead)).ToArray();
        byte[][] frames = s.Queue.ToArray(); byte[][] bytes = frames.Select(frame => frame.ToArray()).ToArray();
        using (var pool = new PotionPoolFault(fault, present))
        {
            if (defense) s.Player.ActiveDefensePotion(4); else s.Player.ActiveAttackPotion(4);
        }
        Assert.False(s.Player.OnAttackPotion); Assert.False(s.Player.OnDefensePotion);
        Assert.Equal(new DateTime(2001, 2, 3), s.Player.OnAttackPotionStamp);
        Assert.Equal(new DateTime(2002, 3, 4), s.Player.OnDefensePotionStamp);
        Assert.Same(words, s.Player.BitVector.bits); Assert.Equal(copied, words);
        Assert.Same(registry, s.Player.BitVector.ArrayFlags); Assert.Equal(entries, registry.ToArray());
        Assert.Equal(fields, entries.Select(pair => (pair.Value.Key, pair.Value.Secounds, pair.Value.Timer,
            pair.Value.InvokerSecouds, pair.Value.TimerInvoker, pair.Value.RemoveOnDead)));
        Assert.Equal(frames.Length, s.Queue.Count);
        for (int i = 0; i < frames.Length; i++)
        {
            Assert.Same(frames[i], s.Queue.ElementAt(i)); Assert.Equal(bytes[i], s.Queue.ElementAt(i));
        }
    });

    [Theory]
    [InlineData(false, 0)] [InlineData(true, 0)]
    [InlineData(false, -2)] [InlineData(true, -2)]
    [InlineData(false, int.MinValue)] [InlineData(true, int.MinValue)]
    public Task NonpositivePotionDurationCannotGrantABonusOrStatus(bool defense, int minutes)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var words = s.Player.BitVector.bits;
        DateTime attack = s.Player.OnAttackPotionStamp, shield = s.Player.OnDefensePotionStamp;
        if (defense) s.Player.ActiveDefensePotion(minutes); else s.Player.ActiveAttackPotion(minutes);
        Assert.False(s.Player.OnAttackPotion); Assert.False(s.Player.OnDefensePotion);
        Assert.Equal(attack, s.Player.OnAttackPotionStamp); Assert.Equal(shield, s.Player.OnDefensePotionStamp);
        Assert.Same(words, s.Player.BitVector.bits); Assert.Empty(s.Player.BitVector.ArrayFlags); Assert.Empty(s.Queue);
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task MalformedPotionStatusRefusesBeforePotionTimerOrSuccessMessage(bool defense)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var words = s.Player.BitVector.bits;
        s.Player.BitVector.Add((int)Flag.TeamLeader);
        var before = words.ToArray();
        DateTime attack = s.Player.OnAttackPotionStamp, shield = s.Player.OnDefensePotionStamp;
        if (defense) s.Player.ActiveDefensePotion(4); else s.Player.ActiveAttackPotion(4);
        Assert.False(s.Player.OnAttackPotion); Assert.False(s.Player.OnDefensePotion);
        Assert.Equal(attack, s.Player.OnAttackPotionStamp); Assert.Equal(shield, s.Player.OnDefensePotionStamp);
        Assert.Same(words, s.Player.BitVector.bits); Assert.Equal(before, words);
        Assert.Empty(s.Player.BitVector.ArrayFlags); Assert.Empty(s.Queue);
    });

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task PreparedPotionMessageMatchesTheCompleteGenuineSystemTalkCodec(bool defense)
        => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        if (defense) s.Player.ActiveDefensePotion(30); else s.Player.ActiveAttackPotion(30);
        using var control = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE);
        string text = defense ? "Your defense will increase during the next 30 minutes." :
            "Your attack will increase during the next 30 minutes.";
        var actual = new LegacyNativeMsgMessage(text, LegacyNativeMsgMessage.MsgColor.red,
            LegacyNativeMsgMessage.ChatMode.Talk).GetArray(control);
        Assert.Equal(2, s.Queue.Count);
        Assert.Equal(NativeSpellTestFixture.Bytes(actual), s.Queue.Last());
    });

    private sealed class PotionPoolFault : IDisposable
    {
        private static readonly FieldInfo Bin = typeof(LegacyNativePacketRecycle).GetField("bin", BindingFlags.NonPublic | BindingFlags.Static)!;
        private readonly object? prior = Bin.GetValue(null);
        private readonly List<LegacyNativePacket> owned = [];

        internal PotionPoolFault(string fault, bool present)
        {
            if (fault == "missing") { Bin.SetValue(null, null); return; }
            if (fault == "null") { Bin.SetValue(null, new ConcurrentQueue<LegacyNativePacket>([(LegacyNativePacket)null!])); return; }
            var queue = new ConcurrentQueue<LegacyNativePacket>();
            if (!present)
            {
                var status = new LegacyNativePacket(LegacyNativePacket.MAX_SIZE); owned.Add(status); queue.Enqueue(status);
                if (fault == "alias") { queue.Enqueue(status); Bin.SetValue(null, queue); return; }
            }
            var message = new LegacyNativePacket(fault == "small-message" ? 1 : LegacyNativePacket.MAX_SIZE);
            owned.Add(message); if (fault == "disposed-message") message.Dispose();
            queue.Enqueue(message); Bin.SetValue(null, queue);
        }

        public void Dispose()
        {
            Bin.SetValue(null, prior);
            foreach (var packet in owned) packet.Dispose();
        }
    }
}
