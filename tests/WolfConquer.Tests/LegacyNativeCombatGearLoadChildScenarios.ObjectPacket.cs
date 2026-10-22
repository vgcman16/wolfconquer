using System.Runtime.InteropServices;
using System.Text.Json;
using ProtoBuf.Meta;
using Table = WolfConquer.Core.Data.LegacyNativeCombatGearTable;
using Jade = WolfConquer.Core.Data.LegacyNativeDaoqiDictTypeTable;
using Lottery = WolfConquer.Core.Data.LegacyNativeDictLotteryTable;
using CollectionTable = WolfConquer.Core.Data.LegacyNativeDictCollectionTable;
using Rank = WolfConquer.Core.Data.LegacyNativeCombatGearRankTable;
using Wire = WolfConquer.Core.World.LegacyNativeCombatGearWire;
using Gear = WolfConquer.Core.World.LegacyNativeMsgCombatGear;
using Opt = WolfConquer.Core.World.LegacyNativeMsgCombatGearOpt;
using Skin = WolfConquer.Core.World.LegacyNativeMsgCombatGearSkin;
using Dao = WolfConquer.Core.World.LegacyNativeMsgDaoQi;
using Air = WolfConquer.Core.World.LegacyNativeMsgAirPowerUp;
using Dict = WolfConquer.Core.World.LegacyNativeMsgDictLottery;

namespace WolfConquer.Tests;

internal static partial class LegacyNativeCombatGearLoadChildScenarios
{
    // Reflection invokes only this entry, not xUnit or its parent collection fixture.
    public static string ExecuteObjectPacket(string scenario, string guard)
    {
        Require(!RuntimeTypeModel.Default.GetTypes().Cast<MetaType>().Any(), "Fresh child Default required.");
        using var s = new Scope();
        object? details = null;
        switch (scenario)
        {
            case "ColdGate":
            {
                foreach (var control in PacketControls())
                {
                    string state = PacketState(control.Message);
                    Require(Wire.EncodeObject(control.Message, control.Opcode) is null && ImplicitPacket(control.Message) is null,
                        "False gate must precede real serializer for all six message types.");
                    Require(state == PacketState(control.Message), "False wire gate mutated original public scalars.");
                }
                Require(Wire.EncodeObject(null!, 0) is null && Wire.EncodeObject(new object(), ushort.MaxValue) is null,
                    "False gate must precede null/type discovery.");
                Require(NullImplicitPackets().All(frame => frame is null), "Every null implicit conversion must honor the false gate first.");
                Require(!RuntimeTypeModel.Default.GetTypes().Cast<MetaType>().Any(), "Cold false gate must not discover/register/freeze any model.");
                Require(!Table.m_Load && ReferenceEquals(s.OldInfo, Table.Info) && ReferenceEquals(s.OldJade, Jade.Info) &&
                    ReferenceEquals(s.OldLottery, Lottery.Info) && ReferenceEquals(s.OldCollection, CollectionTable.Info) &&
                    Rank.Ranks.Count == 0 && s.Log.ToString() == "", "False gate must leave all ordinary state untouched.");
                break;
            }
            case "UnregisteredGear":
            case "UnregisteredSkin":
            case "UnregisteredOpt":
            case "UnregisteredDao":
            case "UnregisteredDict":
            {
                Table.m_Load = true; // Explicit isolated gate prestate, not registration/readiness.
                object message = scenario switch
                {
                    "UnregisteredGear" => new Gear(), "UnregisteredSkin" => new Skin(),
                    "UnregisteredOpt" => new Opt(), "UnregisteredDao" => new Dao(), _ => new Dict()
                };
                ushort opcode = message switch { Gear => 2420, Skin => 2422, Opt => 2421, Dao => 2451, _ => 2465 };
                string original = PacketState(message);
                var direct = ExpectUnregisteredPacketFailure(() => Wire.EncodeObject(message, opcode), message.GetType());
                var implicitFailure = ExpectUnregisteredPacketFailure(() => ImplicitPacket(message), message.GetType());
                Require(original == PacketState(message), "Unregistered failures mutated original public fields.");
                Require(ReferenceEquals(s.OldInfo, Table.Info) && ReferenceEquals(s.OldJade, Jade.Info) &&
                    ReferenceEquals(s.OldLottery, Lottery.Info) && ReferenceEquals(s.OldCollection, CollectionTable.Info) &&
                    Rank.Ranks.Count == 0 && s.Log.ToString() == "", "Serializer failure must not invoke loaders or publish metadata caches.");
                details = new { Direct = direct, Implicit = implicitFailure };
                break;
            }
            case "AttributedAirOnly":
            {
                Table.m_Load = true;
                var air = new Air { Type = Air.Types.PRACTICE, UserId = 17, PowerType = 701,
                    Powers = [new() { Id = 9, SenseLevel = 7, Points = uint.MaxValue }] };
                string original = PacketState(air);
                var powers = air.Powers; var power = powers[0];
                byte[] direct = Wire.EncodeObject(air, 2464);
                byte[] implicitFrame = air;
                Require(Convert.ToHexString(direct) == "0400A0095451536572766572" && direct.SequenceEqual(implicitFrame),
                    "ProtoContract-only Air has no inferred ProtoMembers: both paths must emit an empty real payload.");
                Require(original == PacketState(air) && ReferenceEquals(powers, air.Powers) && ReferenceEquals(power, air.Powers[0]),
                    "Attribute-only serialization must preserve all physical Air owners and scalars.");
                var meta = RuntimeTypeModel.Default.GetTypes().Cast<MetaType>().ToArray();
                Require(meta.Length == 1 && meta[0].Type == typeof(Air) && meta[0].GetFields().Length == 0,
                    "Air attributes must not invent native field registrations or discover its untagged child.");
                break;
            }
            case "RegisteredSixFrames":
            {
                Table.Load(s.Prefix);
                VerifyContracts();
                var frames = new Dictionary<string, string>();
                foreach (var control in PacketControls())
                {
                    string state = PacketState(control.Message);
                    object[] owners = PacketOwners(control.Message);
                    byte[] direct = Wire.EncodeObject(control.Message, control.Opcode);
                    byte[] implicitFrame = ImplicitPacket(control.Message);
                    Require(Convert.ToHexString(direct) == control.Frame && direct.SequenceEqual(implicitFrame),
                        "Genuine registered object/implicit full frame: " + control.Message.GetType().Name);
                    Require(state == PacketState(control.Message), "Serialization changed original public scalar/collection values.");
                    var retained = PacketOwners(control.Message);
                    Require(owners.Length == retained.Length && owners.Zip(retained).All(pair => ReferenceEquals(pair.First, pair.Second)),
                        "Serialization replaced the original collection or nested message owner.");
                    frames.Add(control.Message.GetType().Name, Convert.ToHexString(direct));
                }
                VerifyContracts();
                Require(Table.Info[2][3].ID == 1 && Rank.Ranks.Count == 18 && s.Log.ToString() == "", "Genuine loader prestate must remain intact.");
                details = frames;
                break;
            }
            case "RepeatedRegistration":
            {
                Table.Load(s.Prefix);
                Table.Load(s.Prefix); // Native second Load appends duplicate fields; rank.Create fails only afterward.
                VerifyContracts("RepeatedLoad");
                Require(s.Log.ToString().Contains("LegacyNativeCombatGearRankTable.Create", StringComparison.Ordinal) && Rank.Ranks.Count == 18,
                    "Genuine repeated loader must retain its real late rank failure.");
                var message = new Gear { UID = 17 };
                string original = PacketState(message);
                var direct = ExpectDuplicatePacketFailure(() => Wire.EncodeObject(message, 2420), typeof(Gear));
                var implicitFailure = ExpectDuplicatePacketFailure(() => ImplicitPacket(message), typeof(Gear));
                Require(original == PacketState(message), "Deferred duplicate failure changed the original message.");
                VerifyContracts("RepeatedLoad");
                details = new { Direct = direct, Implicit = implicitFailure };
                break;
            }
            case "NullObjects":
            {
                Table.m_Load = true;
                ushort[] opcodes = [2420, 2422, 2464, 2451, 2465, 2421];
                string[] frames = ["040074095451536572766572", "040076095451536572766572", "0400A0095451536572766572",
                    "040093095451536572766572", "0400A1095451536572766572", "040075095451536572766572"];
                var implicitFrames = NullImplicitPackets();
                for (int i = 0; i < opcodes.Length; i++)
                    Require(Convert.ToHexString(Wire.EncodeObject(null!, opcodes[i])) == frames[i] &&
                        implicitFrames[i] is not null && Convert.ToHexString(implicitFrames[i]!) == frames[i],
                        "Native null object must retain an empty payload and the exact requested opcode/seal.");
                Require(Convert.ToHexString(Wire.EncodeObject(null!, ushort.MaxValue)) == "0400FFFF5451536572766572",
                    "Null object path must preserve arbitrary caller ushort opcode.");
                Require(!RuntimeTypeModel.Default.GetTypes().Cast<MetaType>().Any(), "Null object serialization must not infer a declared message contract.");
                break;
            }
            default: throw new InvalidOperationException("Unknown object packet child scenario.");
        }
        return JsonSerializer.Serialize(new { Guard = guard, Scenario = scenario, Completed = true,
            FrameworkDescription = RuntimeInformation.FrameworkDescription,
            Data = new { Scenario = scenario, Passed = true, Loaded = Table.m_Load,
                ModelTypes = RuntimeTypeModel.Default.GetTypes().Cast<MetaType>().Select(meta => new
                { Type = meta.Type.FullName, Fields = meta.GetFields().Length }).ToArray(), Details = details } });
    }

    private static object ExpectUnregisteredPacketFailure(Func<byte[]> operation, Type type)
    {
        Exception? failure = null;
        try { _ = operation(); } catch (Exception error) { failure = error.GetBaseException(); }
        Require(failure is InvalidOperationException && failure.Message.Contains(type.FullName!, StringComparison.Ordinal),
            "Unregistered native type must propagate its genuine serializer failure: " + type.FullName);
        return new { Type = failure!.GetType().FullName, failure.Message };
    }

    private static object ExpectDuplicatePacketFailure(Func<byte[]> operation, Type type)
    {
        Exception? failure = null;
        try { _ = operation(); } catch (Exception error) { failure = error.GetBaseException(); }
        Require(failure is InvalidOperationException && failure.Message.Contains("Duplicate field-number detected; 1 on: " + type.FullName,
            StringComparison.Ordinal), "Real duplicate metadata must propagate through the actual packet conversion.");
        return new { Type = failure!.GetType().FullName, failure.Message };
    }

    // State snapshots only, never packet expectations: include every actual public scalar/nested field.
    private static string PacketState(object value) => JsonSerializer.Serialize(value, value.GetType(),
        new JsonSerializerOptions { IncludeFields = true });

    private static byte[] ImplicitPacket(object message) => message switch
    {
        Gear gear => gear, Skin skin => skin, Air air => air, Dao dao => dao, Dict dict => dict, Opt opt => opt,
        _ => throw new ArgumentOutOfRangeException(nameof(message))
    };

    private static byte[]?[] NullImplicitPackets() => [(byte[])(Gear)null!, (byte[])(Skin)null!, (byte[])(Air)null!,
        (byte[])(Dao)null!, (byte[])(Dict)null!, (byte[])(Opt)null!];

    private static object[] PacketOwners(object message) => message switch
    {
        Gear gear => [gear.Items, gear.Items[0]], Skin skin => [skin.Weapon, skin.Weapon[0]],
        Opt opt => [opt.U, opt.SelectMaterial, opt.SelectMaterial[0]],
        Dao dao => [dao.Items, dao.Items[0], dao.Positions], Air air => [air.Powers, air.Powers[0]],
        Dict dict => [dict.Items2, dict.Items2[0]], _ => throw new ArgumentOutOfRangeException(nameof(message))
    };

    private sealed record PacketControl(object Message, ushort Opcode, string Frame);

    // Pinned real payload literals plus native Packet.cs's exact header and TQServer seal.
    private static PacketControl[] PacketControls() =>
    [
        new(new Gear { Items = [new()] }, 2420, "22007409080010001A180800100018002000280030003800400048005000580060005451536572766572"),
        new(new Opt { U = [0, 17, uint.MaxValue], SelectMaterial = [new()] }, 2421, "1E007509080010001800200028003000301130FFFFFFFF0F3A04080010005451536572766572"),
        new(new Skin { Weapon = [new()] }, 2422, "1A007609080012120800100018002000280030003800400048005451536572766572"),
        new(new Dao { Items = [new()], Positions = [0, 17, uint.MaxValue] }, 2451, "2A0093090800100018002000280030003800400048005000580060006A0208007000701170FFFFFFFF0F5451536572766572"),
        new(new Air { Powers = [new()] }, 2464, "1C00A0090800100018002210080010001800200028003000380040005451536572766572"),
        new(new Dict { Items2 = [new()] }, 2465, "0E00A109080010001A04080010005451536572766572")
    ];
}
