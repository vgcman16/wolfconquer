using System.Reflection;
using System.Reflection.Emit;
using WolfConquer.Core.World;
using Gear = WolfConquer.Core.World.LegacyNativeCombatGear;

namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed partial class LegacyNativeCombatGearEntryTests
{
    private static FieldInfo PlayerField(string name) => typeof(LegacyNativePlayerRuntime)
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)!;

    // Explicit physical MODEL prestates, not a setter/loader API or a writable-auto substitute.
    private static void PhysicalPrestate(LegacyNativePlayerRuntime player, uint classId, ushort level, uint hair)
    {
        PlayerField("_class").SetValue(player, classId);
        PlayerField("_level").SetValue(player, level);
        PlayerField("_hair").SetValue(player, hair);
    }

    private static LegacyNativeGameClientRuntime PopulatedOwner(uint uid = 17, uint classId = 2001)
    {
        var owner = new LegacyNativeGameClientRuntime();
        var player = owner.Player;
        PhysicalPrestate(player, classId, 140, 41123);
        player.UID = uid;
        player.Name = new string("Physical Hero".ToCharArray());
        player.Body = 1003;
        player.Face = 201;
        player.TransformationID = 3;
        player.SpecialTitleID = 901;
        player.SpecialWingID = 0;
        player.GarmentId = 902;
        player.ArmorId = 903;
        player.LeftWeaponAccessoryId = 904;
        player.RightWeaponAccessoryId = 905;
        player.MountArmorId = 906;
        player.WingId = 907;
        player.WingPlus = 17;
        return owner;
    }

    private static Gear.Weapon Equipped(Gear.TypeID id) => new() { ItemID = id, Equip = 1 };

    private sealed record IlMember(OpCode Op, MemberInfo? Member);

    // Read-only call/field identity control. Does not invoke decoded methods or pin IL byte layout.
    private static IlMember[] Members(MethodInfo method)
    {
        var bytes = method.GetMethodBody()!.GetILAsByteArray()!;
        var opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(field => (OpCode)field.GetValue(null)!)
            .GroupBy(op => unchecked((ushort)op.Value))
            .ToDictionary(group => group.Key, group => group.First());
        var result = new List<IlMember>();
        for (var offset = 0; offset < bytes.Length;)
        {
            ushort key = bytes[offset++];
            if (key == 0xfe)
            {
                if (offset >= bytes.Length) throw new InvalidOperationException("Incomplete IL opcode.");
                key = (ushort)(0xfe00 | bytes[offset++]);
            }
            var op = opcodes[key];
            var width = op.OperandType switch
            {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineI or OperandType.ShortInlineVar or OperandType.ShortInlineBrTarget => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI or OperandType.InlineBrTarget or OperandType.InlineField
                    or OperandType.InlineMethod or OperandType.InlineSig or OperandType.InlineString
                    or OperandType.InlineTok or OperandType.InlineType or OperandType.ShortInlineR => 4,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => SwitchWidth(bytes, offset),
                _ => throw new InvalidOperationException("Unsupported IL operand kind.")
            };
            if (width > bytes.Length - offset) throw new InvalidOperationException("Incomplete IL operand.");
            MemberInfo? member = op.OperandType == OperandType.InlineMethod
                ? method.Module.ResolveMethod(BitConverter.ToInt32(bytes, offset))
                : op.OperandType == OperandType.InlineField
                    ? method.Module.ResolveField(BitConverter.ToInt32(bytes, offset)) : null;
            result.Add(new IlMember(op, member));
            offset += width;
        }
        return result.ToArray();
    }

    private static int SwitchWidth(byte[] bytes, int offset)
    {
        if (bytes.Length - offset < 4) throw new InvalidOperationException("Incomplete IL switch count.");
        var count = BitConverter.ToInt32(bytes, offset);
        if (count < 0) throw new InvalidOperationException("Negative IL switch count.");
        return checked(4 + 4 * count);
    }
}
