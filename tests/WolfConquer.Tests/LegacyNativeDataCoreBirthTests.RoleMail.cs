using System.Collections.Concurrent;
using System.Reflection;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeDataCoreBirthTests
{
    [Fact]
    public Task EntireRoleMailOwnerIsDistinctFromMailCodecAndKeepsNativePhysicalDictionaryAndPrivateOwner()=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();var mail=new LegacyNativeRoleMailRuntime(scope.Client);var other=new LegacyNativeRoleMailRuntime(scope.Client);
        Assert.Same(scope.Client,Owner(mail));Assert.Empty(mail.Mails);Assert.NotSame(mail.Mails,other.Mails);
        var row=new LegacyNativeMsgProficiency{ID=91};mail.Mails[91]=row;Assert.Same(row,mail.Mails[91]);Assert.Empty(other.Mails);
        var field=typeof(LegacyNativeRoleMailRuntime).GetField("Mails")!;
        Assert.Equal(typeof(ConcurrentDictionary<uint,LegacyNativeMsgProficiency>),field.FieldType);Assert.False(field.IsInitOnly);
        Assert.True(typeof(LegacyNativeRoleMailRuntime).GetField("Owner",BindingFlags.Instance|BindingFlags.NonPublic)!.IsPrivate);
        Assert.False(typeof(LegacyNativeRoleMailRuntime).IsAbstract);Assert.Empty(typeof(LegacyNativeRoleMailRuntime).GetMethods(BindingFlags.Public|BindingFlags.Instance|BindingFlags.DeclaredOnly));
        Assert.Null(scope.Client.MyMail);Assert.Empty(scope.Queue);
    });

    [Fact]
    public Task RoleMailNullOwnerIsStoredWithoutGuardOrImplicitClientSlotPublication()=>NativePacketTestThread.Run(()=>
    {
        using var scope=new Scope();var mail=new LegacyNativeRoleMailRuntime(null!);
        Assert.Null(Owner(mail));Assert.NotNull(mail.Mails);Assert.Empty(mail.Mails);Assert.Null(scope.Client.MyMail);Assert.Empty(scope.Queue);
    });
}
