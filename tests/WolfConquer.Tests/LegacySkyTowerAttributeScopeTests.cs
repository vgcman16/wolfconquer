using WolfConquer.Core.World;

namespace WolfConquer.Tests;

// Real existing component ingress, dynamic registration and death lifecycle.
// This does not claim native ordinary admission or globally atomic combat.
public sealed partial class LegacySkyTowerAttributeScopeTests
{
    [Theory]
    [InlineData("SkyTowerActiveCompletionId")]
    [InlineData("SkyTowerCompletedChallengeId")]
    [InlineData("SkyTowerFloor")]
    [InlineData("SkyTowerAvailableAttempts")]
    public async Task AmbiguousCompletionEditCannotSuppressTheActuallyKilledObjective(string ownedKey)
    {
        using var f = await TowerFixture.EnterAsync();
        var fields = f.InstallOrdinalAttributes();
        string value = fields.TryGetValue(ownedKey, out var old) ? old : "0";
        fields[ownedKey] = value;
        fields.Add(ownedKey.ToLowerInvariant(), value);
        f.PublishInput(fields);
        var before = f.Current.Character.Attributes.Where(row => row.Key.StartsWith("SkyTower", StringComparison.OrdinalIgnoreCase)).ToArray();
        f.PrepareLastHit();
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => f.AttackAsync());
        Assert.Equal("characterAttributeOwnedFieldAmbiguous", error.Message);
        Assert.Empty(f.ProgressFrames());
        Assert.Equal("10", f.Current.Character.Attributes["SkyTowerAvailableAttempts"]);
        foreach (var row in before) Assert.Equal(row.Value, f.Current.Character.Attributes[row.Key]);
        Assert.Equal("upper", f.Current.Character.Attributes["Other"]);
        Assert.Equal("lower", f.Current.Character.Attributes["other"]);
        Assert.False(f.Monsters.TryGetVisibleInstance(f.Objective, DateTimeOffset.UtcNow, out _));
        Assert.Contains(f.Sent, bytes => IsObjectiveDeath(bytes, f.Objective.Uid));
        // The hit itself was accepted before completion preflight. Its genuine
        // death-removal/respawn lifecycle remains available: no hidden suppress.
        var future = DateTimeOffset.UtcNow.AddDays(1);
        Assert.True(f.Monsters.TryConsumeDeathRemovalInstance(f.Objective, future, out var removed));
        Assert.Equal(0U, removed.Hitpoints);
        Assert.True(f.Monsters.TryConsumeRespawnedInstance(f.Objective, future, out var respawned));
        Assert.Equal(f.Objective.MaxLife, respawned.Hitpoints);
        Assert.Empty(f.ProgressFrames());
        Assert.DoesNotContain("completed Sky Tower challenge", f.Log.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ValidCompletionPublishesOrdinalOpaqueDataBeforeProgressAndKeepsRespawnSuppressed()
    {
        using var f = await TowerFixture.EnterAsync();
        var fields = f.InstallOrdinalAttributes(); f.PublishInput(fields);
        var observations = 0;
        f.OnSend = bytes =>
        {
            if (!IsProgress(bytes)) return;
            observations++;
            AssertCompletedAttributes(f);
            Assert.False(f.Monsters.TryGetVisibleInstance(f.Objective, DateTimeOffset.UtcNow, out _));
        };
        f.PrepareLastHit();
        try { await f.AttackAsync(); } finally { f.OnSend = null; }
        Assert.Equal(2, observations);
        AssertCompletedAttributes(f);
        var progress = f.ProgressFrames(); Assert.Equal(2, progress.Length);
        // Independent existing D13 wire goldens, not a production serializer.
        Assert.Equal(new byte[] { 0x0D, 0x00, 0x4E, 0x09, 0x08, 0x00, 0x22, 0x05, 0x08, 0xDC, 0x03, 0x18, 0x01 }, progress[0]);
        Assert.Equal(new byte[] { 0x0D, 0x00, 0x4E, 0x09, 0x08, 0x01, 0x2A, 0x05, 0x08, 0xDC, 0x03, 0x10, 0x09 }, progress[1]);
        var future = DateTimeOffset.UtcNow.AddDays(1);
        Assert.True(f.Monsters.TryConsumeDeathRemovalInstance(f.Objective, future, out _));
        Assert.False(f.Monsters.TryConsumeRespawnedInstance(f.Objective, future, out _));
        Assert.Empty(f.Monsters.GetVisibleDynamicMonsters(InstanceMap, f.DynamicId, future));
        Assert.Contains("completed Sky Tower challenge", f.Log.ToString(), StringComparison.Ordinal);
    }

    private static void AssertCompletedAttributes(TowerFixture f)
    {
        var current = f.Current;
        var proof = LegacyNativeAttributeMapProof.CaptureRaw(current.Character.Attributes);
        Assert.Same(StringComparer.Ordinal, proof.CapturedComparer); Assert.True(proof.IsCurrentPlain());
        Assert.Equal("upper", current.Character.Attributes["Other"]);
        Assert.Equal("lower", current.Character.Attributes["other"]);
        Assert.Equal("476", current.Character.Attributes["SkyTowerActiveCompletionId"]);
        Assert.Equal("476", current.Character.Attributes["SkyTowerCompletedChallengeId"]);
        Assert.Equal("1", current.Character.Attributes["SkyTowerFloor"]);
        Assert.Equal("9", current.Character.Attributes["SkyTowerAvailableAttempts"]);
    }

    private static bool IsProgress(byte[] bytes) =>
        TowerFixture.ReadFrame(bytes).Header.Opcode == VisibleDx8D13InstanceInfoPacket.Opcode;

    private static bool IsObjectiveDeath(byte[] bytes, uint uid) =>
        MsgAttackPacket.TryParse(TowerFixture.ReadFrame(bytes), out var packet) &&
        packet.Query.AttackType == MsgAttackType.Death && packet.Query.OpponentUid == uid;
}
