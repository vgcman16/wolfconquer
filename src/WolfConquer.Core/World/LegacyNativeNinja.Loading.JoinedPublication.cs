namespace WolfConquer.Core.World;

internal partial class LegacyNativeNinja
{
    private sealed partial class PreparedNinjaSequence
    {
        internal void PublishJoined(LegacyOrdinarySpellBinding binding, LoadingInput input,
            LegacyNativeSpellStorage.PreparedNinjaSpells? spells, bool badgeApplied)
        {
            IEnumerable<byte[]> Frames()
            {
                foreach (var output in outputs)
                {
                    if (!input.HasCommitted(badgeApplied) ||
                        spells is not null && !spells.HasCurrentCommittedGraph() || binding.IsRetired)
                        throw new LegacyCommittedSpellNotificationException(
                            new InvalidOperationException("Committed Ninja notification graph changed."));
                    yield return output.Frame;
                }
            }
            LegacyOrdinarySpellNotificationPublisher.Publish(binding, Frames());
        }
    }
}
