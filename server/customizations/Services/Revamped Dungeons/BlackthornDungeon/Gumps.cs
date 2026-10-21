// ServUO: Services/Revamped Dungeons/BlackthornDungeon/Gumps.cs (cc-P196). The Agent of the Crown's reward gump:
// the shard's BaseRewardGump (Gumps/BaseRewardGump.cs, cc-P194) over BlackthornRewards.Rewards, titled 1154516
// (Blackthorn Artifacts), paying with the Blackthorn points (Services/PointsSystems/BlackthornData.cs). ServUO's
// PointsSystem.Blackthorn is BlackthornData.Instance here (the P33 framework's shape).

using Server.Engines.Points;
using Server.Gumps;
using Server.Mobiles;

namespace Server.Engines.Blackthorn;

public class BlackthornRewardGump : BaseRewardGump
{
    public BlackthornRewardGump(Mobile owner, PlayerMobile user)
        : base(owner, user, BlackthornRewards.Rewards, 1154516)
    {
    }

    public override double GetPoints(Mobile m) => BlackthornData.Instance.GetPoints(m);

    public override void RemovePoints(double points) => BlackthornData.Instance.DeductPoints(User, points);
}
