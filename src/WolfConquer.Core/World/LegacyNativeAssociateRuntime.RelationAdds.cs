using WolfConquer.Core.Data;

namespace WolfConquer.Core.World;

// Whole native Associate method family, reusing incoming genuine physical owners.
internal partial class LegacyNativeAssociateRuntime
{
    public partial class MyAsociats
    {
		public void AddPartener(LegacyNativeGameClientRuntime Owner, LegacyNativePlayerRuntime client)
		{
			if (AllowAdd(3, client.UID, 10))
			{
				Member member = new Member
				{
					UID = client.UID,
					Name = client.Name,
					Class = client.Class,
					Signature = client.Signature,
					Mesh = client.Mesh,
					Level = client.Level,
					NobilityRank = (uint)client.NobilityRank,
					Timer = (ulong)DateTime.Now.AddDays(3.0).Ticks
				};
				Add(3, member);
			}
			else
			{
				Owner.SendSysMesage("Sorry, you used the limit of partener`s");
			}
		}

		public void AddAprrentice(LegacyNativeGameClientRuntime Owner, LegacyNativePlayerRuntime client)
		{
			if (AllowAdd(5, client.UID, (byte)LegacyNativeTutorInfo.AddAppCount(Owner)))
			{
				if (OnlineApprentice.TryAdd(client.UID, client.Owner))
				{
					Member member = new Member
					{
						UID = client.UID,
						Name = client.Name,
						Class = client.Class,
						Signature = client.Signature,
						Mesh = client.Mesh,
						Level = client.Level,
						NobilityRank = (uint)client.NobilityRank
					};
					Add(5, member);
				}
			}
			else
			{
				Owner.SendSysMesage("Sorry, you used the limit of aprrentice`s");
			}
		}

		public void AddMentor(LegacyNativeGameClientRuntime Owner, LegacyNativePlayerRuntime client)
		{
			if (AllowAdd(4, client.UID, 1))
			{
				Member member = new Member
				{
					UID = client.UID,
					Name = client.Name,
					Class = client.Class,
					Signature = client.Signature,
					Mesh = client.Mesh,
					Level = client.Level,
					NobilityRank = (uint)client.NobilityRank
				};
				Add(4, member);
			}
		}

		public void AddFriends(LegacyNativeGameClientRuntime Owner, LegacyNativePlayerRuntime client)
		{
			if (AllowAdd(1, client.UID, 50))
			{
				Member member = new Member
				{
					UID = client.UID,
					Name = client.Name,
					Class = client.Class,
					Signature = client.Signature,
					Mesh = client.Mesh,
					Level = client.Level,
					NobilityRank = (uint)client.NobilityRank
				};
				Add(1, member);
			}
			else
			{
				Owner.SendSysMesage("Sorry, you used the limit of frinds");
			}
		}
    }
}
