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
        using var selectSql = select.CreateProviderCommand();
        Assert.Equal("SELECT * FROM `accounts` WHERE `ID` = @p0", selectSql.CommandText);
        Assert.Equal(id, Convert.ToUInt32(selectSql.Parameters[0].Value));
        foreach (long state in new long[] { 0, 1 })
        {
            using var update = new LegacyNativeMySqlCommand(LegacyNativeMySqlCommandType.UPDATE);
            update.Update("accounts").Set("State", state).Where("ID", id);
            using var updateSql = update.CreateProviderCommand();
            Assert.Equal("UPDATE `accounts` SET `State` = @p0 WHERE `ID` = @p1", updateSql.CommandText);
            Assert.Equal(state, updateSql.Parameters[0].Value);
            Assert.Equal(id, Convert.ToUInt32(updateSql.Parameters[1].Value));
        }
        // No reader constructor, Execute, connection getter or Banned12 call occurs here.
    }
}
