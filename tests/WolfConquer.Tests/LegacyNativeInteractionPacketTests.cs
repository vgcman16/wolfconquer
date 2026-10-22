using System.Buffers.Binary;
using System.Reflection;
using System.Text;
using WolfConquer.Core.Networking;
using WolfConquer.Core.World;
using Attack = WolfConquer.Core.World.LegacyNativeMsgAttackPacket;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyNativeInteractionPacketTests
{
    [Fact]
    public Task FullAttackIdEnumAndAttackObjKeepTheLiteralMutableSourceShape() => NativePacketTestThread.Run(() =>
    {
        Assert.Equal(typeof(uint), Enum.GetUnderlyingType(typeof(Attack.AttackID)));
        Assert.Null(typeof(Attack.AttackID).GetCustomAttribute<FlagsAttribute>());
        var members = typeof(Attack.AttackID).GetFields(BindingFlags.Static | BindingFlags.Public)
            .OrderBy(field => field.MetadataToken).ToArray();
        Assert.Equal(new[] { "None", "Physical", "Magic", "Archer", "RequestMarriage", "AcceptMarriage", "Death", "Reflect",
            "Dash", "UpdateHunterJar", "CounterKillSwitch", "Scapegoat", "FatalStrike", "InteractionRequest", "InteractionAccept",
            "InteractionRefuse", "InteractionEffect", "InteractionStopEffect", "InMoveSpell", "BlueDamage", "BackFire" }, members.Select(field => field.Name));
        Assert.Equal(new uint[] { 0, 2, 24, 28, 8, 9, 14, 26, 27, 36, 44, 43, 45, 46, 47, 48, 49, 50, 53, 55, 57 },
            members.Select(field => (uint)field.GetRawConstantValue()!));
        var holder = new Attack.AttackObj();
        Assert.Null(holder.User);
        Assert.Null(holder.Attack);
        var user = new LegacyNativeGameClientRuntime();
        var query = Query("all");
        holder.User = user;
        holder.Attack = query;
        query.Damage = 99;
        Assert.Same(user, holder.User);
        Assert.Same(query, holder.Attack);
        Assert.Equal(99, holder.Attack.Damage);
        Assert.Equal(new[] { "UID", "OpponentUID", "X", "Y", "AtkType", "Damage", "CombatGear", "KilledMonster", "SpellLevel",
            "SpellID", "ResponseDamage", "Effect", "Unknown" }, typeof(LegacyNativeInteractQuery)
            .GetFields(BindingFlags.Public | BindingFlags.Instance).OrderBy(field => field.MetadataToken).Select(field => field.Name));
    });

    // Expectations come from the complete attributed source contract, not an unrun DLL probe.
    [Theory]
    [InlineData("default")]
    [InlineData("all")]
    [InlineData("max")]
    [InlineData("negative")]
    public Task InteractionCreateWritesAllThirteenSourceFieldsWithRequiredZerosAndSignedInt32(string name) => NativePacketTestThread.Run(() =>
    {
        using var scope = new NativeInteractionTestScope();
        var query = Query(name);
        scope.Packet.Seek(900);
        Assert.Same(scope.Packet, scope.Packet.InteractionCreate(query));
        var frame = NativeInteractionTestScope.Frame(scope.Packet);
        Assert.Equal((ushort)2135, BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(2)));
        Assert.Equal(frame.Length, BinaryPrimitives.ReadUInt16LittleEndian(frame));
        Assert.Equal(4, scope.Packet.Position);
        var fields = NativeInteractionTestScope.Fields(frame[4..]);
        Assert.Equal(Expected(query), fields.Select(field => (field.Field, field.Value)).ToArray());
        Assert.Equal(name is "all" or "max" ? 13 : 10, fields.Length);
        if (name == "negative")
        {
            Assert.Equal(10, Assert.Single(fields, field => field.Field == 15).Bytes);
            Assert.Equal(10, Assert.Single(fields, field => field.Field == 17).Bytes);
        }
        if (name == "default")
            Assert.Equal("1C005708200028003800400068007800880100A80100C80100D00100", Convert.ToHexString(frame));
        if (name == "all")
            Assert.Equal("25005708200128023803400468187805800106880107900108980109A8010AC8010BD0010C", Convert.ToHexString(frame));
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task OptionalNativeSealRemainsOutsideHeaderLengthWithSameActualStreamAndResetCursor(bool sealedFrame) => NativePacketTestThread.Run(() =>
    {
        using var scope = new NativeInteractionTestScope(sealedFrame);
        scope.Packet.Size = 123;
        scope.Packet.Seek(900);
        Assert.Same(scope.Packet, scope.Packet.InteractionCreate(Query("default")));
        var frame = NativeInteractionTestScope.Frame(scope.Packet);
        Assert.Equal(28 + (sealedFrame ? 8 : 0), frame.Length);
        Assert.Equal((ushort)28, BinaryPrimitives.ReadUInt16LittleEndian(frame));
        Assert.Equal(4, scope.Packet.Position);
        if (sealedFrame) Assert.Equal(Encoding.ASCII.GetBytes("TQServer"), frame[^8..]);
    });

    [Fact]
    public Task MagicTypeSerializationDoesNotEncodeMutateOrReplaceAnyRawQueryField() => NativePacketTestThread.Run(() =>
    {
        using var scope = new NativeInteractionTestScope();
        var query = Query("all");
        query.Damage = 0x12345678;
        query.SpellID = 0xBEEF;
        var holder = new Attack.AttackObj { User = new(), Attack = query };
        var fields = typeof(LegacyNativeInteractQuery).GetFields(BindingFlags.Public | BindingFlags.Instance);
        var before = fields.Select(field => field.GetValue(query)).ToArray();

        scope.Packet.InteractionCreate(query);

        Assert.Equal(before, fields.Select(field => field.GetValue(query)).ToArray());
        Assert.Same(query, holder.Attack);
        Assert.NotNull(holder.User);
        Assert.False(holder.User.FullLoading);
        var original = NativeInteractionTestScope.Frame(scope.Packet);
        var encoded = NativeInteractionTestScope.Fields(original[4..]);
        Assert.Equal(0x12345678UL, Assert.Single(encoded, field => field.Field == 15).Value);
        Assert.Equal(0xBEEFUL, Assert.Single(encoded, field => field.Field == 19).Value);
        query.UID = 77;
        scope.Packet.InteractionCreate(query);
        Assert.Equal(77UL, Assert.Single(NativeInteractionTestScope.Fields(NativeInteractionTestScope.Frame(scope.Packet)[4..]),
            field => field.Field == 4).Value);
        Assert.Equal(1UL, Assert.Single(NativeInteractionTestScope.Fields(original[4..]), field => field.Field == 4).Value);
        Assert.Same(query, holder.Attack);
    });

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task NullOuterHasEmptyPayloadButStillResetsAndFinalizesTheActualPacket(bool sealedFrame) => NativePacketTestThread.Run(() =>
    {
        using var scope = new NativeInteractionTestScope(sealedFrame);
        scope.Packet.Seek(900);
        Assert.Same(scope.Packet, scope.Packet.InteractionCreate(null!));
        Assert.Equal(Convert.FromHexString("04005708" + (sealedFrame ? "5451536572766572" : "")),
            NativeInteractionTestScope.Frame(scope.Packet));
        Assert.Equal(4, scope.Packet.Position);
        Assert.Equal(sealedFrame ? 12 : 4, scope.Packet.Size);
    });

    [Fact]
    public Task NullStreamFaultsAtWriterResetEvenWithANullOuterQuery() => NativePacketTestThread.Run(() =>
    {
        LegacyNativePacket stream = null!;
        Assert.Throws<NullReferenceException>(() => stream.InteractionCreate(null!));
        var query = Query("all");
        Assert.Throws<NullReferenceException>(() => stream.InteractionCreate(query));
        Assert.Equal(1u, query.UID);
        Assert.Equal(5, query.Damage);
    });

    private static LegacyNativeInteractQuery Query(string name) => name switch
    {
        "default" => new(),
        "all" => new() { UID = 1, OpponentUID = 2, X = 3, Y = 4, AtkType = 24, Damage = 5, CombatGear = 6,
            KilledMonster = 7, SpellLevel = 8, SpellID = 9, ResponseDamage = 10, Effect = 11, Unknown = 12 },
        "max" => new() { UID = uint.MaxValue, OpponentUID = uint.MaxValue, X = ushort.MaxValue, Y = ushort.MaxValue,
            AtkType = ushort.MaxValue, Damage = int.MaxValue, CombatGear = ulong.MaxValue, KilledMonster = int.MaxValue,
            SpellLevel = ushort.MaxValue, SpellID = ushort.MaxValue, ResponseDamage = uint.MaxValue, Effect = uint.MaxValue, Unknown = uint.MaxValue },
        "negative" => new() { Damage = -1, KilledMonster = int.MinValue },
        _ => throw new ArgumentOutOfRangeException(nameof(name))
    };

    private static (int Field, ulong Value)[] Expected(LegacyNativeInteractQuery query)
    {
        var fields = new List<(int, ulong)> { (4, query.UID), (5, query.OpponentUID), (7, query.X), (8, query.Y),
            (13, query.AtkType), (15, unchecked((ulong)(long)query.Damage)) };
        if (query.CombatGear != 0) fields.Add((16, query.CombatGear));
        fields.Add((17, unchecked((ulong)(long)query.KilledMonster)));
        if (query.SpellLevel != 0) fields.Add((18, query.SpellLevel));
        if (query.SpellID != 0) fields.Add((19, query.SpellID));
        fields.Add((21, query.ResponseDamage));
        fields.Add((25, query.Effect));
        fields.Add((26, query.Unknown));
        return fields.ToArray();
    }
}
