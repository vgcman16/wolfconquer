using System.Reflection;
using ProtoBuf;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeAttackDispatchTests
{
    [Fact]
    public void InteractionOwnerRetainsAllThirteenActualTagsRequiredFlagsAndNoSurrogateFields()
    {
        var type = typeof(LegacyNativeInteractQuery); Assert.NotNull(type.GetCustomAttribute<ProtoContractAttribute>());
        var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public).OrderBy(field => field.MetadataToken).ToArray();
        Assert.Equal(new[] { "UID", "OpponentUID", "X", "Y", "AtkType", "Damage", "CombatGear", "KilledMonster", "SpellLevel", "SpellID", "ResponseDamage", "Effect", "Unknown" }, fields.Select(field => field.Name));
        var attributes = fields.Select(field => field.GetCustomAttribute<ProtoMemberAttribute>()!).ToArray();
        Assert.Equal(new[] { 4, 5, 7, 8, 13, 15, 16, 17, 18, 19, 21, 25, 26 }, attributes.Select(attribute => attribute.Tag));
        Assert.Equal(new[] { true, true, true, true, true, true, false, true, false, false, true, true, true }, attributes.Select(attribute => attribute.IsRequired));
    }

    [Fact]
    public Task AttributedNativeSerializationMatchesPriorCompleteTypedWireForAllFieldsAndZeroBornRequiredFields() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope();
        foreach (var query in new[] { new LegacyNativeInteractQuery(), new LegacyNativeInteractQuery
        {
            UID = uint.MaxValue, OpponentUID = 22, X = ushort.MaxValue, Y = 65534, AtkType = 2, Damage = int.MinValue,
            CombatGear = ulong.MaxValue, KilledMonster = -1, SpellLevel = 321, SpellID = 123,
            ResponseDamage = uint.MaxValue, Effect = 12345678, Unknown = uint.MaxValue
        } })
        {
            s.Packet.InteractionCreate(query); byte[] typed = NativeSpellTestFixture.Bytes(s.Packet);
            s.Packet.InitWriter(); s.Packet.ProtoBufferSerialize(query); s.Packet.Finalize(2135);
            Assert.Equal(typed, NativeSpellTestFixture.Bytes(s.Packet));
            s.Packet.Interaction(out var result, null!); //Non-magic never reads client.
            Assert.Equal(query.UID, result.UID); Assert.Equal(query.Damage, result.Damage); Assert.Equal(query.CombatGear, result.CombatGear);
            Assert.Equal(query.KilledMonster, result.KilledMonster); Assert.Equal(query.SpellLevel, result.SpellLevel); Assert.Equal(query.Unknown, result.Unknown);
        }
    });

    [Fact]
    public Task ManualMagicNullClientFaultRetainsFullyDeserializedOutputAndNativeHeaderReadIgnoresOpcode() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var source = new LegacyNativeInteractQuery { UID = 17, AtkType = 24, SpellID = 111, Damage = -1 };
        s.Packet.InteractionCreate(source); s.Packet.Seek(2); s.Packet.Write((ushort)1234);
        LegacyNativeInteractQuery output = null!;
        Assert.Throws<NullReferenceException>(() => s.Packet.Interaction(out output, null!));
        Assert.NotNull(output); Assert.Equal((ushort)111, output.SpellID); Assert.Equal(-1, output.Damage);
        Assert.Equal(s.Packet.Size, s.Packet.Position); Assert.Empty(s.Queue);
        source.AtkType = 2; s.Packet.InteractionCreate(source); s.Packet.Seek(2); s.Packet.Write((ushort)1234);
        s.Packet.Interaction(out output, null!); Assert.Equal((ushort)2, output.AtkType); Assert.Equal((ushort)111, output.SpellID);
    });

    [Theory]
    [InlineData(false, 2)] [InlineData(true, 24)] [InlineData(false, 24)]
    public Task InteractionReadsRealProtobufIntoNewMutableOwnerAndDecodesOnlyManualMagic(bool autoAttack, ushort kind) => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var sent = new LegacyNativeInteractQuery
        {
            UID = 0x12345678, OpponentUID = 0x87654321, X = 0x1234, Y = 0x5678, AtkType = kind, Damage = -1,
            CombatGear = ulong.MaxValue, KilledMonster = int.MinValue, SpellLevel = 12, SpellID = 0xABCD,
            ResponseDamage = uint.MaxValue, Effect = uint.MaxValue, Unknown = 0x87654321
        };
        s.Packet.InteractionCreate(sent); s.Client.OnAutoAttack = autoAttack; s.Packet.Seek(113);
        s.Packet.Interaction(out var actual, s.Client); Assert.NotSame(sent, actual);
        Assert.Equal(sent.UID, actual.UID); Assert.Equal(sent.Damage, actual.Damage); Assert.Equal(sent.CombatGear, actual.CombatGear);
        Assert.Equal(sent.KilledMonster, actual.KilledMonster); Assert.Equal(sent.SpellLevel, actual.SpellLevel);
        Assert.Equal(sent.ResponseDamage, actual.ResponseDamage); Assert.Equal(sent.Effect, actual.Effect); Assert.Equal(sent.Unknown, actual.Unknown);
        if (kind == 24 && !autoAttack)
        {
            Assert.Equal(unchecked((ushort)(Rotate16((ushort)(sent.SpellID ^ sent.UID ^ 0x915D), 13) + 5310)), actual.SpellID);
            uint rotated = (sent.OpponentUID >> 13) | (sent.OpponentUID << 19);
            Assert.Equal(unchecked((rotated ^ sent.UID ^ 0x5F2D2463) + 2341516570u), actual.OpponentUID);
            Assert.Equal(unchecked((ushort)(Rotate16((ushort)(sent.X ^ sent.UID ^ 0x2ED6), 15) + 56594)), actual.X);
            Assert.Equal(unchecked((ushort)(Rotate16((ushort)(sent.Y ^ sent.UID ^ 0xB99B), 11) + 30430)), actual.Y);
        }
        else { Assert.Equal(sent.SpellID, actual.SpellID); Assert.Equal(sent.OpponentUID, actual.OpponentUID); Assert.Equal(sent.X, actual.X); Assert.Equal(sent.Y, actual.Y); }
        Assert.Equal(s.Packet.Size, s.Packet.Position); Assert.Empty(s.Queue); Assert.Equal(autoAttack, s.Client.OnAutoAttack);
    });

    [Fact]
    public Task CreateAutoAttackCopiesOnlyTenNativeFieldsExcludesOtherWireFieldsAndRetainsPublicationBeforeFault() => NativePacketTestThread.Run(() =>
    {
        using var s = new Scope(); var source = new LegacyNativeInteractQuery
        {
            AtkType = 24, Damage = -1, Effect = 7, OpponentUID = 31, ResponseDamage = 32, SpellID = 33, SpellLevel = 34,
            UID = 35, X = 36, Y = 37, CombatGear = ulong.MaxValue, KilledMonster = 99, Unknown = uint.MaxValue
        };
        LegacyNativeMsgAttackPacket.CreateAutoAtack(source, s.Client);
        var actual = s.Client.AutoAttack!; Assert.True(s.Client.OnAutoAttack); Assert.NotSame(source, actual);
        Assert.Equal((source.AtkType, source.Damage, source.Effect, source.OpponentUID, source.ResponseDamage),
            (actual.AtkType, actual.Damage, actual.Effect, actual.OpponentUID, actual.ResponseDamage));
        Assert.Equal((source.SpellID, source.SpellLevel, source.UID, source.X, source.Y), (actual.SpellID, actual.SpellLevel, actual.UID, actual.X, actual.Y));
        Assert.Equal(0ul, actual.CombatGear); Assert.Equal(0, actual.KilledMonster); Assert.Equal(0u, actual.Unknown);
        source.Damage = 9; Assert.Equal(-1, actual.Damage);
        Assert.Throws<NullReferenceException>(() => LegacyNativeMsgAttackPacket.CreateAutoAtack(null!, s.Client));
        Assert.True(s.Client.OnAutoAttack); Assert.NotSame(actual, s.Client.AutoAttack); Assert.Equal(0, s.Client.AutoAttack!.Damage);
        Assert.Empty(s.Queue);
    });

    private static ushort Rotate16(ushort input, int positions)
    {
        uint current = input;
        for (int index = 0; index < positions; index++) current = (current >> 1) | ((current & 1) << 15);
        return (ushort)current;
    }
}
