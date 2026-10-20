// cc-P192 (Chase 2026-10-19 15:06: "the area around the fountain locations should not be markable"). SECRET: staff only;
// never on the wiki. Admin notes: shard-migration notes/admin/secrets.md.
//
// A square of FountainOfYouthSystem.NoMarkRadius tiles around the fountain, made when it is placed or loaded and removed
// when it leaves. It is a child of whatever region holds the fountain's tile, one priority above it, so it wins Region.Find
// there (Region.CompareTo, Region.cs:231-252) and hands every other rule to that parent (each Region virtual calls Parent,
// Region.cs:745-880; BaseRegion.CheckTravel likewise, BaseRegion.cs:171-180).
//
// What it refuses, to players (staff are exempt):
//   - Mark: the Mark spell asks the caster's own region (Mark.cs:38, :72 -> SpellHelper.CheckTravel, SpellHelper.cs:611-640).
//     So no rune is ever marked here, which also covers every rune trick (copying, runebooks: they carry a marked rune).
//   - Recall, Sacred Journey and Gate Travel INTO it (RecallTo / GateTo: Recall.cs:55, SacredJourney.cs:55, GateTravel.cs:47,
//     and the Bracelet of Binding, BraceletOfBinding.cs:212), in case a rune was marked before the fountain arrived.
//   - Houses: AllowHousing (HousePlacement.cs:126, HousePlacementTool.cs:245).
//   - Boats: the boat deed's region check, server/patches/BaseBoatDeed-fountain-sanctum.patch (BaseBoatDeed.cs:152-161 has
//     no region virtual, only type tests).
// The message is pinned's own default for each refusal (message left null, SpellHelper.cs:637-640), nothing that hints at
// a fountain. The region has no name, so nothing a player can read names it.

#nullable enable

using Server.Spells;

namespace Server.Regions;

public class FountainSanctumRegion : BaseRegion
{
    public FountainSanctumRegion(Map map, Region? parent, Rectangle3D area)
        : base(null, map, parent, (parent?.Priority ?? DefaultPriority) + 1, area)
    {
    }

    /// <summary>The square of <paramref name="radius"/> tiles around <paramref name="center"/> (Rectangle3D's end is exclusive).</summary>
    public static Rectangle3D Square(Point3D center, int radius) =>
        new(new Point3D(center.X - radius, center.Y - radius, MinZ), new Point3D(center.X + radius + 1, center.Y + radius + 1, MaxZ));

    /// <summary>Registers a sanctum around <paramref name="center"/>, under the region that holds it now.</summary>
    public static FountainSanctumRegion Create(Map map, Point3D center, int radius)
    {
        var parent = Find(center, map);
        var region = new FountainSanctumRegion(map, parent, Square(center, radius));
        region.Register();
        return region;
    }

    public override bool AllowHousing(Mobile from, Point3D p) => false;

    public override bool CheckTravel(Mobile m, Point3D newLocation, TravelCheckType travelType, out TextDefinition message)
    {
        message = null;

        if (m?.AccessLevel > AccessLevel.Player)
        {
            return base.CheckTravel(m, newLocation, travelType, out message);
        }

        switch (travelType)
        {
            case TravelCheckType.Mark:
                return false; // asked of the caster's own region: standing in here
            case TravelCheckType.RecallTo or TravelCheckType.GateTo when Contains(newLocation):
                return false;
        }

        return base.CheckTravel(m, newLocation, travelType, out message);
    }

    /// <summary>True when <paramref name="region"/> is a sanctum or inside one: the boat deed refuses there.</summary>
    public static bool RefusesBoats(Region? region) => region?.IsPartOf<FountainSanctumRegion>() == true;
}
