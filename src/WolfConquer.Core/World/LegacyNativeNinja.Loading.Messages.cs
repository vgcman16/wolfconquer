namespace WolfConquer.Core.World;

internal partial class LegacyNativeNinja
{
    private sealed partial class LoadingInput
    {
        private Dictionary<uint, LegacyNativeNinjaFile.NinjaSprintItem>? scoreTable;
        private RankingMetadata[] scoreRows = [];

        private void PrepareMessages()
        {
            // Header4 + required Type/UID minimum4 +12 per nonnull row. This
            // transport minimum is not a client/gameplay cardinality rule.
            if (rows.Length > (1032 - 8) / 12)
                throw new InvalidDataException("Ninja full item rows cannot fit their minimum send envelope.");
            (scoreTable, scoreRows) = CaptureRankingMetadata(rows, null);
            var total = PrepareRankingScores(state, rows, scoreRows)[0];
            UserMessage = new()
            {
                Type = 0, UID = uid, Flag = state.Flag, Points = state.Exp,
                Fire = state.Fire, Water = state.Water, Earth = state.Earth, Wind = state.Wind,
                Lightning = state.Lightning, FireNew = state.tmp_Fire, WaterNew = state.tmp_Water,
                EarthNew = state.tmp_Earth, WindNew = state.tmp_Wind, LightningNew = state.tmp_Lightning,
                Score = total, FireMastery = state.FireMastery, WaterMastery = state.WaterMastery,
                EarthMastery = state.EarthMastery, WindMastery = state.WindMastery,
                LightningMastery = state.LightningMastery, UnlockMastery = state.UnlockMastery,
            };
            ItemMessage = new()
            {
                Type = LegacyNativeMsgNinja.MsgNinjaItem.TypeID.Load, UID = uid,
                Items = rows.Select(row => new LegacyNativeMsgNinja.MsgNinjaItem.Item
                { ID = row.Id, Level = row.Level, Position = row.Position, Points = row.Exp, UK = row.UK }).ToArray(),
            };
        }
    }
}
