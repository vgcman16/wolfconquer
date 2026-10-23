using System.Reflection;
using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeLoginWireTests
{
    [Fact]
    public void WholeAccountBanMethodKeepsItsPublicNativeSignatureWithoutBeingInvoked()
    {
        MethodInfo method = typeof(LegacyNativeGameClientRuntime).GetMethod("Banned12", BindingFlags.Public | BindingFlags.Instance)!;
        Assert.NotNull(method); Assert.Equal(typeof(void), method.ReturnType);
        ParameterInfo[] args = method.GetParameters(); Assert.Equal(3, args.Length);
        Assert.Equal(new[] { typeof(uint), typeof(string), typeof(bool) }, args.Select(x => x.ParameterType));
        Assert.Equal(new[] { "ID", "name", "unbanned" }, args.Select(x => x.Name));
        Assert.False(args[0].IsOptional); Assert.False(args[1].IsOptional);
        Assert.True(args[2].IsOptional); Assert.Equal(false, args[2].DefaultValue);
    }

    [Theory]
    [InlineData(0u)] [InlineData(1u)] [InlineData(uint.MaxValue)]
    public void GenuineAccountCommandsKeepTheExactSelectAndStateUpdateShapeWithoutExecution(uint id)
    {
        using var select = new LegacyNativeMySqlCommand(LegacyNativeMySqlCommandType.SELECT).Select("accounts").Where("ID", id);
        Assert.Equal("SELECT * FROM `accounts`WHERE `ID` = " + id, select.Command);
        foreach (long state in new long[] { 0, 1 })
        {
            using var update = new LegacyNativeMySqlCommand(LegacyNativeMySqlCommandType.UPDATE);
            update.Update("accounts").Set("State", state).Where("ID", id);
            Assert.Equal("UPDATE `accounts` SET `State` = " + state + " WHERE `ID` = " + id, update.Command);
        }
        // No reader constructor, Execute, connection getter or Banned12 call occurs here.
    }
}
