using System.Reflection;
using System.Runtime.InteropServices;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeTeleportLifecycleTests
{
    [Fact]
    public void BracketLayoutAndProtectionMethodDeclarationsKeepNativeMetadata()
    {
        var bracket = typeof(LegacyNativeMsgElitePKBrackets).StructLayoutAttribute!;
        Assert.Equal(LayoutKind.Explicit, bracket.Value);
        Assert.Equal(0, bracket.Size);
        Assert.Equal(new ushort[] { 0, 3, 4, 5, 6, 7, 8, 9 },
            Enum.GetValues<LegacyNativeMsgElitePKBrackets.GuiTyp>().Select(x => (ushort)x));
        Assert.Equal(Enumerable.Range(0, 7).Select(x => (ushort)x),
            Enum.GetValues<LegacyNativeMsgElitePKBrackets.Action>().Select(x => (ushort)x));
        foreach (var row in new[]
        {
            (Name: "ProtectAttack", Result: typeof(void), Parameter: "StampMiliSecounds"),
            (Name: "ProtectJumpAttack", Result: typeof(void), Parameter: "Secounds"),
            (Name: "AllowAttack", Result: typeof(bool), Parameter: (string?)null)
        })
        {
            var method = typeof(LegacyNativePlayerRuntime).GetMethod(row.Name,
                BindingFlags.Instance | BindingFlags.NonPublic)!;
            Assert.NotNull(method);
            Assert.True(method.IsAssembly);
            Assert.Equal(row.Result, method.ReturnType);
            var parameters = method.GetParameters();
            if (row.Parameter == null)
            {
                Assert.Empty(parameters);
            }
            else
            {
                var parameter = Assert.Single(parameters);
                Assert.Equal(typeof(int), parameter.ParameterType);
                Assert.Equal(row.Parameter, parameter.Name);
            }
        }
    }
}
