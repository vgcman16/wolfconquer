namespace WolfConquer.Core.World;

internal partial class LegacyNativeCombatGear
{
    public partial class Powers
    {
        // Native CombatGear336..359: normalization changes this physical Powers owner.
        public byte[] Send(LegacyNativeMsgAirPowerUp.Types type, uint UserId)
        {
            if (SenseLevel == 0)
            {
                SenseLevel = 1u;
            }
            LegacyNativeMsgAirPowerUp msgAirPowerUp = new LegacyNativeMsgAirPowerUp
            {
                Type = type,
                UserId = UserId
            };
            LegacyNativeMsgAirPowerUp.Power item = new LegacyNativeMsgAirPowerUp.Power
            {
                Id = Id,
                SenseLevel = SenseLevel,
                SenseAipPower = SenseAipPower,
                ArmedLevel = ArmedLevel,
                ArmedAipPower = ArmedAipPower,
                OverLoadLevel = OverLoadLevel,
                OverLoadAipPower = OverLoadAipPower,
                Points = Points
            };
            msgAirPowerUp.Powers.Add(item);
            return msgAirPowerUp;
        }

        // Native CombatGear362..377: unchecked addition precedes encoding and does not normalize Sense.
        public byte[] AddPoints(uint Points, uint UserId)
        {
            this.Points += Points;
            LegacyNativeMsgAirPowerUp msgAirPowerUp = new LegacyNativeMsgAirPowerUp
            {
                Type = LegacyNativeMsgAirPowerUp.Types.ADDPOINTS,
                UserId = UserId
            };
            LegacyNativeMsgAirPowerUp.Power item = new LegacyNativeMsgAirPowerUp.Power
            {
                Id = 7u,
                Points = this.Points
            };
            msgAirPowerUp.Powers.Add(item);
            return msgAirPowerUp;
        }
    }
}
