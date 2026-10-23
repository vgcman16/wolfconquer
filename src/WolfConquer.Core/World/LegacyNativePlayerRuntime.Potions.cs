namespace WolfConquer.Core.World;

public sealed partial class LegacyNativePlayerRuntime
{
public void ActiveAttackPotion(int Timer)
	{
		OnAttackPotion = true;
		OnAttackPotionStamp = DateTime.Now.AddMinutes(Timer);
		AddFlag(LegacyNativeMsgUpdate.Flags.Stigma, 60, RemoveOnDead: true);
		Owner.SendSysMesage("Your attack will increase during the next 30 minutes.", LegacyNativeMsgMessage.ChatMode.Talk);
	}

public void ActiveDefensePotion(int Timer)
	{
		OnDefensePotion = true;
		OnDefensePotionStamp = DateTime.Now.AddMinutes(Timer);
		AddFlag(LegacyNativeMsgUpdate.Flags.MagicShield, 60, RemoveOnDead: true);
		Owner.SendSysMesage("Your defense will increase during the next 30 minutes.", LegacyNativeMsgMessage.ChatMode.Talk);
	}
}
