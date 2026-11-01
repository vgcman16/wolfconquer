namespace WolfConquer.Core.World;

internal static partial class LegacyNativeMagicQueryClient
{
    internal sealed class AttackRangeCache
    {
        private readonly AttackSpeedCache source;
        private AttackRangeCache(AttackSpeedCache source) => this.source = source;
        internal static AttackRangeCache PrepareUnpublished(AttackSpeedCache source) => new(source);
        internal ushort Right => source.RightRange;
        internal ushort Left => source.LeftRange;

        internal bool IsCurrentPlain(LegacyOrdinarySpellBinding binding) =>
            ReferenceEquals(source.Range, this) && source.IsCurrentPlain(binding) &&
            ReferenceEquals(binding.Client.Equipment, binding.Items.Equipment) &&
            binding.Items.Equipment.SizeAdd == 0;

        internal int GetAttackRange(int targetSizeAdd)
            => CalculateAttackRange(Right, Left, targetSizeAdd);
    }

    internal static bool TryReadAttackRangeCache(LegacyOrdinarySpellBinding binding, out AttackRangeCache? cache)
    {
        cache = TryReadAttackSpeedCache(binding, out var speed) ? speed!.Range : null;
        return cache is not null && cache.IsCurrentPlain(binding);
    }

    // Pure arithmetic only; its result is no current cache/entity/attack grant.
    internal static int CalculateAttackRange(ushort right, ushort left, int targetSizeAdd)
    {
        if (targetSizeAdd is < sbyte.MinValue or > sbyte.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(targetSizeAdd));
        int range = right != 0 && left != 0 ? (right + left) / 2 :
            right != 0 ? right : left != 0 ? left : 1;
        return checked(range + (targetSizeAdd + 1) / 2);
    }
}
