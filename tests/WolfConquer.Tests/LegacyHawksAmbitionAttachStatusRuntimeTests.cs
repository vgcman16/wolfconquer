namespace WolfConquer.Tests;

[Collection("NativeItemCounterSourcePrerequisite")]
public sealed class LegacyOwnedHawksAmbitionAttachStatusRuntimeTests
{
    [Fact]
    public Task HandlerHawksAmbition15920SelfStatusSendsSourceInteractionArchiveAnimationEquipmentAndProgress() =>
        LegacyMagicAttackRuntimeTests.RunOwnedHawksAmbitionAttachStatusAsync();
}
