using WolfConquer.Core.Data;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Synthetic in-memory secondary-password records only, not login credentials,
// an authentication bypass, real accounts, filesystem writes or a live caller.
public sealed class LegacyNativeSecurityPersistenceTests
{
    [Fact]
    public void DefaultRecordKeepsTheNativeThreeValuesAndTrailingComma()
    {
        var user = new LegacyNativeGameClientRuntime();
        Assert.Equal("0,0,0,", LegacyNativeServerDatabasePersistence.GetSecurityPassword(user));
        Assert.False(user.Player.IsCheckedPass);
    }

    [Fact]
    public void WriterUsesRawUIntFieldsAndTicksRatherThanDateTimeBinaryKindBits()
    {
        var user = new LegacyNativeGameClientRuntime();
        user.Player.SecurityPassword = uint.MaxValue;
        user.Player.OnReset = uint.MaxValue;
        foreach (var kind in new[] { DateTimeKind.Unspecified, DateTimeKind.Utc, DateTimeKind.Local })
        {
            var date = DateTime.SpecifyKind(new DateTime(2020, 2, 3, 4, 5, 6), kind);
            user.Player.ResetSecurityPassowrd = date;
            Assert.Equal($"4294967295,4294967295,{date.Ticks},",
                LegacyNativeServerDatabasePersistence.GetSecurityPassword(user));
            Assert.Equal(kind, user.Player.ResetSecurityPassowrd.Kind);
        }
    }

    [Fact]
    public void FlagZeroIgnoresTheDateTokenAndKeepsTheOldPhysicalResetStampAndVerification()
    {
        var user = Seed();
        var old = user.Player.ResetSecurityPassowrd;
        LegacyNativeServerDatabasePersistence.LoadSecurityPassword("1234,0,not-a-date,", user);
        Assert.Equal(1234u, user.Player.SecurityPassword);
        Assert.Equal(0u, user.Player.OnReset);
        Assert.Equal(old, user.Player.ResetSecurityPassowrd);
        Assert.True(user.Player.IsCheckedPass);
    }

    [Fact]
    public void EveryResetFlagExceptExactlyOneSkipsDateParsingAndExpiry()
    {
        foreach (uint flag in new uint[] { 0, 2, 255, uint.MaxValue })
        {
            var user = Seed();
            var old = user.Player.ResetSecurityPassowrd;
            LegacyNativeServerDatabasePersistence.LoadSecurityPassword($"1234,{flag},9223372036854775807,", user);
            Assert.Equal(1234u, user.Player.SecurityPassword);
            Assert.Equal(flag, user.Player.OnReset);
            Assert.Equal(old, user.Player.ResetSecurityPassowrd);
            Assert.True(user.Player.IsCheckedPass);
        }
    }

    [Fact]
    public void FutureResetLoadsTheActualDateAndRetainsBothPasswordAndFlag()
    {
        var user = Seed();
        LegacyNativeServerDatabasePersistence.LoadSecurityPassword($"1234,1,{DateTime.MaxValue.Ticks},", user);
        Assert.Equal(1234u, user.Player.SecurityPassword);
        Assert.Equal(1u, user.Player.OnReset);
        Assert.Equal(DateTime.MaxValue, user.Player.ResetSecurityPassowrd);
        Assert.Equal(DateTimeKind.Unspecified, user.Player.ResetSecurityPassowrd.Kind);
        Assert.True(user.Player.IsCheckedPass);
    }

    [Fact]
    public void ExpiredOrMissingDateClearsPasswordAndFlagButRetainsLoadedStampAndVerification()
    {
        foreach (var line in new[] { "1234,1,0,", "1234,1,", "1234,1,bad-date," })
        {
            var user = Seed();
            LegacyNativeServerDatabasePersistence.LoadSecurityPassword(line, user);
            Assert.Equal(0u, user.Player.SecurityPassword);
            Assert.Equal(0u, user.Player.OnReset);
            Assert.Equal(DateTime.MinValue, user.Player.ResetSecurityPassowrd);
            Assert.True(user.Player.IsCheckedPass);
            Assert.Equal("0,0,0,", LegacyNativeServerDatabasePersistence.GetSecurityPassword(user));
        }
    }

    [Fact]
    public void MissingCommaAndMalformedUIntTokensUseNativeDefaultsWithoutResettingTheOldDate()
    {
        foreach (string? line in new string?[] { null, "", "1234", "-1,bad,0,", "4294967296,4294967296,0," })
        {
            var user = Seed();
            var old = user.Player.ResetSecurityPassowrd;
            LegacyNativeServerDatabasePersistence.LoadSecurityPassword(line!, user);
            Assert.Equal(0u, user.Player.SecurityPassword);
            Assert.Equal(0u, user.Player.OnReset);
            Assert.Equal(old, user.Player.ResetSecurityPassowrd);
            Assert.True(user.Player.IsCheckedPass);
        }
    }

    [Fact]
    public void InvalidDateBinaryFaultKeepsEarlierPasswordAndFlagWritesWithoutReplacingStamp()
    {
        var user = Seed();
        var old = user.Player.ResetSecurityPassowrd;
        Assert.Throws<ArgumentException>(() => LegacyNativeServerDatabasePersistence.LoadSecurityPassword(
            "1234,1,9223372036854775807,", user));
        Assert.Equal(1234u, user.Player.SecurityPassword);
        Assert.Equal(1u, user.Player.OnReset);
        Assert.Equal(old, user.Player.ResetSecurityPassowrd);
        Assert.True(user.Player.IsCheckedPass);
    }

    [Fact]
    public void ValidUtcBinaryInputLoadsKindButTheNextNativeSaveDropsOnlyTheKindBits()
    {
        var user = Seed();
        var future = DateTime.SpecifyKind(DateTime.MaxValue, DateTimeKind.Utc);
        LegacyNativeServerDatabasePersistence.LoadSecurityPassword($"1234,1,{future.ToBinary()},", user);
        Assert.Equal(future.Ticks, user.Player.ResetSecurityPassowrd.Ticks);
        Assert.Equal(DateTimeKind.Utc, user.Player.ResetSecurityPassowrd.Kind);
        string saved = LegacyNativeServerDatabasePersistence.GetSecurityPassword(user);
        Assert.Equal($"1234,1,{future.Ticks},", saved);
        LegacyNativeServerDatabasePersistence.LoadSecurityPassword(saved, user);
        Assert.Equal(future.Ticks, user.Player.ResetSecurityPassowrd.Ticks);
        Assert.Equal(DateTimeKind.Unspecified, user.Player.ResetSecurityPassowrd.Kind);
        Assert.Equal(1234u, user.Player.SecurityPassword);
        Assert.Equal(1u, user.Player.OnReset);
    }

    [Fact]
    public void NullClientAndNullPlayerRetainTheirNativeDereferenceFaults()
    {
        Assert.Throws<NullReferenceException>(() => LegacyNativeServerDatabasePersistence.GetSecurityPassword(null!));
        Assert.Throws<NullReferenceException>(() => LegacyNativeServerDatabasePersistence.LoadSecurityPassword(null!, null!));
        var user = new LegacyNativeGameClientRuntime { Player = null! };
        Assert.Throws<NullReferenceException>(() => LegacyNativeServerDatabasePersistence.GetSecurityPassword(user));
        Assert.Throws<NullReferenceException>(() => LegacyNativeServerDatabasePersistence.LoadSecurityPassword("1234,0,", user));
        Assert.Null(user.Player);
    }

    private static LegacyNativeGameClientRuntime Seed()
    {
        var user = new LegacyNativeGameClientRuntime();
        user.Player.SecurityPassword = 777;
        user.Player.OnReset = 888;
        user.Player.ResetSecurityPassowrd = new DateTime(2000, 1, 2, 3, 4, 5);
        user.Player.IsCheckedPass = true;
        return user;
    }
}
