using System.Globalization;
using WolfConquer.Core.Data;
using WolfConquer.Core.Packets;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyMagicAttackRuntimeTests
{
    private const uint DirectEarlyHawksWarmUid = TargetPlayerUid + 10;
    private const ushort DirectEarlyHawksSpell = 15_920;
    private const ushort DirectEarlyHawksUseRune = 21_730;
    private const int DirectEarlyHawksJadeId = 21_730_902;

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DirectEarlyHawksRemovalPrecedesShieldAndReadsAttackUpEarnedDuringItsVector(bool earnAttackUp)
    {
        var armed = false;
        var mutations = 0;
        using var fixture = new DirectEarlyShieldFixture(async (current, recipient, bytes) =>
        {
            if (!armed || recipient != TargetPlayerUid || !DirectEarlyHawksClearedVector(bytes) ||
                Interlocked.CompareExchange(ref mutations, 1, 0) != 0) return;
            var target = current.Slot(TargetPlayerUid);
            Assert.Equal(300, target.Character.MinHitPoints);
            Assert.False(HasLegacyStatusFlag(target.Character.Attributes, LegacyHawksAmbitionFlag));
            Assert.False(HasLegacyStatusFlag(target.Character.Attributes, LegacyLightningShieldActivatedFlag));
            Assert.False(target.Character.Attributes.ContainsKey(LegacyPlayerDeathState.DeadStampAttribute));
            Assert.False(HasLegacyStatusFlag(current.Slot(PlayerUid).Character.Attributes, LegacyAttackUpFlag));
            if (earnAttackUp)
            {
                // Real public outgoing receive -> Pirate Update -> DB-selected UseRune.
                // The status233 row is synthetic branch data; no status flag is seeded.
                await current.EquipAttackUpJadeAndWarmAsync();
                Assert.True(HasLegacyStatusFlag(current.Slot(PlayerUid).Character.Attributes, LegacyAttackUpFlag));
                Assert.Equal("1", current.Slot(PlayerUid).Character.Attributes["DirectEarlyHawksAttackUpPower"]);
                Assert.True(current.Slot(DirectEarlyHawksWarmUid).Character.MinHitPoints > 0);
            }
        }, hawks: true);
        await fixture.PrimeAsync();
        var start = fixture.Order.Count;
        armed = true;
        await fixture.HitAsync();
        armed = false;
        Assert.Equal(1, mutations);
        var frames = fixture.Order.ToArray().Skip(start).ToArray();
        var targetFrames = frames.Where(frame => frame.Recipient == TargetPlayerUid).Select(frame => frame.Bytes).ToArray();
        var hawksIndex = Array.FindIndex(targetFrames, DirectEarlyHawksClearedVector);
        var activationIndex = Array.FindIndex(targetFrames, bytes =>
            LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) && packet.Uid == TargetPlayerUid &&
            packet.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
                entry.Values is { } values && IsLegacyStatusFlagSet(values, LegacyLightningShieldActivatedFlag)));
        var azureIndices = Enumerable.Range(0, targetFrames.Length).Where(index =>
            LegacyMsgUpdatePacket.TryParse(ReadFrame(targetFrames[index]), out var packet) && packet.Uid == TargetPlayerUid &&
            packet.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.AzureShield)).ToArray();
        Assert.Equal(2, azureIndices.Length);
        Assert.True(hawksIndex >= 0 && hawksIndex < activationIndex && activationIndex < azureIndices[0]);
        Assert.True(azureIndices[0] < azureIndices[1]);
        for (var index = 0; index < azureIndices.Length; index++)
        {
            Assert.True(LegacyMsgUpdatePacket.TryParse(ReadFrame(targetFrames[azureIndices[index]]), out var packet));
            var entry = Assert.Single(packet.Updates);
            Assert.Equal(LegacyMsgUpdateDataType.AzureShield, entry.Type);
            Assert.Equal((uint)LegacyLightningShieldActivatedFlag, entry.Flag);
            Assert.Equal(index == 0 ? 20_000U : earnAttackUp ? 0U : 9_800U, entry.Damage);
            Assert.Equal(0U, entry.SpellLevel);
        }
        var firstHp = Array.FindIndex(targetFrames, bytes =>
            LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) && packet.Uid == TargetPlayerUid &&
            packet.Updates.Any(entry => entry.Type is LegacyMsgUpdateDataType.MaxHitpoints or LegacyMsgUpdateDataType.Hitpoints));
        Assert.True(firstHp > azureIndices[1]);
        Assert.Single(targetFrames.Take(activationIndex), DirectEarlyHawksClearedVector);
        var hp = ParseMsgUpdates(targetFrames).Where(packet => packet.Uid == TargetPlayerUid)
            .SelectMany(packet => packet.Updates).Where(entry => entry.Type == LegacyMsgUpdateDataType.Hitpoints)
            .Select(entry => entry.Value).ToArray();
        // (40401+399) /2 =20400; real Hawks50% first leaves10200. Fresh AttackUp doubles back to20400.
        // Source shield20000 therefore leaves9800/zero incoming, or leaves0/incoming400.
        Assert.Equal(earnAttackUp ? new ulong[] { 300, 300, 300, 0 } : new ulong[] { 300 }, hp);
        var targetAfter = fixture.Slot(TargetPlayerUid);
        Assert.Equal(earnAttackUp ? 0 : 300, targetAfter.Character.MinHitPoints);
        Assert.Equal(earnAttackUp, HasLegacyStatusFlag(targetAfter.Character.Attributes, 5));
        Assert.False(HasLegacyStatusFlag(targetAfter.Character.Attributes, LegacyHawksAmbitionFlag));
        Assert.Equal(earnAttackUp, HasLegacyStatusFlag(fixture.Slot(PlayerUid).Character.Attributes, LegacyAttackUpFlag));
        if (earnAttackUp)
        {
            Assert.Contains(ParseSpellAnimations(frames.Select(frame => frame.Bytes)),
                packet => packet.AttackerUid == PlayerUid && packet.SpellId == DirectEarlyHawksUseRune);
        }
        else
        {
            Assert.DoesNotContain(ParseSpellAnimations(frames.Select(frame => frame.Bytes)),
                packet => packet.SpellId == DirectEarlyHawksUseRune);
        }
        Assert.Equal(0, fixture.ScheduledTimers);
    }

    private static bool DirectEarlyHawksClearedVector(byte[] bytes) =>
        LegacyMsgUpdatePacket.TryParse(ReadFrame(bytes), out var packet) && packet.Uid == TargetPlayerUid &&
        packet.Updates.Any(entry => entry.Type == LegacyMsgUpdateDataType.StatusFlag &&
            entry.Values is { } values && !IsLegacyStatusFlagSet(values, LegacyHawksAmbitionFlag) &&
            !IsLegacyStatusFlagSet(values, LegacyLightningShieldActivatedFlag) && !IsLegacyStatusFlagSet(values, 5));

    private static MagicType DirectEarlyHawksUseRuneRow()
    {
        var row = CreateMagicRow(DirectEarlyHawksUseRune, sort: 6, useMana: 0, useStamina: 0,
            damage: 1, duration: 11, gDamage: 100, range: 18, coolDown: 0, coldTime: 0);
        var fields = row.Fields.ToArray();
        fields[3] = "DirectEarlyHawksAttackUpPower";
        fields[16] = "233";
        return row with { Name = fields[3], Fields = fields };
    }

    private static ItemType DirectEarlyHawksWarmItem(uint id, string name)
    {
        var row = DirectOverlayItem(id, name);
        var fields = row.Fields.ToArray();
        fields[18] = "50000"; // Each source ItemHP is ushort-safe; the two real items total100000.
        return row with { Fields = fields };
    }

    private static DaoqiDictType DirectEarlyHawksJade()
    {
        string[] fields =
        [
            DirectEarlyHawksJadeId.ToString(CultureInfo.InvariantCulture), "2", "24",
            (DirectEarlyHawksUseRune * 1_000).ToString(CultureInfo.InvariantCulture),
            "0", "0", "600", "0", "0", "0", "10", "0", "0", "10", "701", "",
        ];
        return new DaoqiDictType(DirectEarlyHawksJadeId, Type: 2, Level: 24,
            Values: fields.Skip(3).Take(12).Select(value =>
                int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : 0).ToArray(),
            SourceFile: "fixture://early-hawks-attack-up-daoqi", LineNumber: 1,
            RawLine: string.Join("@@", fields), Fields: fields);
    }
}
