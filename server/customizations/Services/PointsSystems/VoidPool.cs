// ServUO: Services/PointsSystems/VoidPool.cs (cc-P194), on the shard's P33 points framework
// (Services/PointsSystems/PointsSystem.cs, shaped on ServUO's so this is one more subclass, as that file says).
//
// Void Pool reward points: awarded at the end of a battle for the waves a player helped fight back (the stage,
// doubled on Felucca), spent at Vela in Cove. ServUO's Name (1152733) and GetTitle (1152531, the loyalty gump's
// title) have no reader here: the P33 framework has no loyalty gump. The VoidPoolInfo context-menu entry ServUO
// keeps in this file is in VoidPoolController.cs beside the hook that adds it.

using Server.Mobiles;

namespace Server.Engines.Points;

public class VoidPool : PointsSystem
{
    private static VoidPool _instance;

    public static VoidPool Instance => _instance ??= new VoidPool();

    public override PointsType Loyalty => PointsType.VoidPool;
    public override bool AutoAdd => true;
    public override double MaxPoints => double.MaxValue;

    public static void Configure()
    {
        _ = Instance;
    }

    public override void SendMessage(PlayerMobile from, double old, double points, bool quest)
    {
        // For your participation in the Battle for the Void Pool on ~1_FACET~, you have received ~2_POINTS~ reward points. Any reward points you have accumulated may be redeemed by visiting Vela in Cove.
        from.SendLocalizedMessage(1152649, $"{from.Map}\t{points}");
    }
}
