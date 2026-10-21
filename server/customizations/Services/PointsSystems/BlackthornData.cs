// ServUO: Services/PointsSystems/BlackthornData.cs (cc-P196), on the shard's P33 points framework
// (Services/PointsSystems/PointsSystem.cs, shaped on ServUO's so this is one more subclass, as that file says).
//
// Two things in one system, as ServUO has them:
//   1. The reward points: one per Minax artifact handed to an Agent of the Crown (AgentOfTheCrown.AwardPoints,
//      "You have turned in ~1_COUNT~ artifacts bearing the crest of Minax", 1154518), spent on the crest items.
//   2. DungeonPoints, the per-player kill tally that rolls the Minax artifacts: every kill in the BlackthornDungeon
//      region by a player with looting rights adds fame / 2 x (1 + sqrt(luck) / 100) to the player's tally, and the
//      chance of an artifact is A x 10 ^ (B x tally) (A 0.000863316841, B 0.00000425531915: about 0.09 percent at
//      zero, 1 percent at 250,000, doubling every 70,700 points); a hit hands the player a Minax artifact
//      (MinaxArtifacts.CreateForKill) and empties the tally. The tally is saved with the system.
//
// Conversion notes: ServUO reaches ProcessKill through EventSink.OnKilledBy, which BaseCreature.OnDeath raises for
// every damager with looting rights (BaseCreature.cs:6361-6420); pinned has no such event, so the BlackthornDungeon
// region's OnDeath (Region.cs) walks pinned's GetLootingRights and calls ProcessKill for each, before the
// creature's own OnDeath, as ServUO's region hook also runs (Mobile.Kill: Region.OnDeath then OnDeath). RealLuck is
// pinned's Luck. The P33 framework has no loyalty gump, so ServUO's Name (null there too) and ShowOnLoyaltyGump
// (false) have nothing to read them.

using System;
using System.Collections.Generic;
using System.IO;
using Server.Engines.Blackthorn;
using Server.Mobiles;

namespace Server.Engines.Points;

public class BlackthornData : PointsSystem
{
    private const double A = 0.000863316841;
    private const double B = 0.00000425531915;

    private static BlackthornData _instance;

    public static BlackthornData Instance => _instance ??= new BlackthornData();

    public override PointsType Loyalty => PointsType.Blackthorn;
    public override bool AutoAdd => true;
    public override double MaxPoints => double.MaxValue;

    public Dictionary<Mobile, int> DungeonPoints { get; } = new();

    public static void Configure()
    {
        _ = Instance;
    }

    public override void SendMessage(PlayerMobile from, double old, double points, bool quest)
    {
        // You have turned in ~1_COUNT~ artifacts bearing the crest of Minax.
        from.SendLocalizedMessage(1154518, ((int)points).ToString());
    }

    /// <summary>The artifact chance at a tally (ServUO ProcessKill's formula), for the facts and the notes.</summary>
    public static double ChanceAt(int dungeonPoints) => A * Math.Pow(10, B * dungeonPoints);

    /// <summary>
    ///     ServUO BlackthornData.ProcessKill: a kill in the dungeon by a living player adds to the tally and rolls a
    ///     Minax artifact. Returns the artifact when one was given. <paramref name="forceDrop" /> is test support: it
    ///     makes the roll succeed.
    /// </summary>
    public Item ProcessKill(Mobile victim, Mobile damager, bool forceDrop = false)
    {
        if (victim is not BaseCreature bc || bc.Controlled || bc.Summoned || damager?.Alive != true || damager.Deleted)
        {
            return null;
        }

        var region = bc.Region;

        if (damager is not PlayerMobile pm || region?.IsPartOf("BlackthornDungeon") != true)
        {
            return null;
        }

        DungeonPoints.TryGetValue(pm, out var tally);

        var fame = bc.Fame / 2;
        var luck = Math.Max(0, pm.Luck);

        tally += (int)(fame * (1 + Math.Sqrt(luck) / 100));
        DungeonPoints[pm] = tally;

        if (!forceDrop && ChanceAt(tally) <= Utility.RandomDouble())
        {
            return null;
        }

        var item = MinaxArtifacts.CreateForKill(bc, pm);

        if (item == null)
        {
            return null;
        }

        pm.PlaySound(0x5B4);
        pm.SendLocalizedMessage(1062317); // For your valor in combating the fallen beast, a special artifact has been bestowed on you.

        if (!pm.PlaceInBackpack(item))
        {
            if (pm.BankBox?.TryDropItem(pm, item, false) == true)
            {
                pm.SendLocalizedMessage(1079730); // The item has been placed into your bank box.
            }
            else
            {
                pm.SendLocalizedMessage(1072523); // You find an artifact, but your backpack and bank are too full to hold it.
                item.MoveToWorld(pm.Location, pm.Map);
            }
        }

        DungeonPoints.Remove(pm);
        return item;
    }

    /// <summary>
    ///     The region's hook: every damager with looting rights on a creature killed in the dungeon is processed, as
    ///     ServUO's BaseCreature.OnDeath does through OnKilledBy.
    /// </summary>
    public void OnCreatureDeath(BaseCreature bc)
    {
        if (bc == null || bc.Deleted)
        {
            return;
        }

        var rights = BaseCreature.GetLootingRights(bc.DamageEntries, bc.HitsMax);

        for (var i = 0; i < rights.Count; i++)
        {
            var ds = rights[i];

            if (ds.m_HasRight)
            {
                ProcessKill(bc, ds.m_Mobile);
            }
        }
    }

    public override void Serialize(IGenericWriter writer)
    {
        base.Serialize(writer);

        writer.WriteEncodedInt(0); // version

        var live = new List<KeyValuePair<Mobile, int>>();

        foreach (var kvp in DungeonPoints)
        {
            if (kvp.Key?.Deleted == false && kvp.Value > 0)
            {
                live.Add(kvp);
            }
        }

        writer.WriteEncodedInt(live.Count);

        foreach (var (m, points) in live)
        {
            writer.Write(m);
            writer.Write(points);
        }
    }

    public override void Deserialize(IGenericReader reader)
    {
        base.Deserialize(reader);

        DungeonPoints.Clear();

        var version = reader.ReadEncodedInt();

        if (version != 0)
        {
            throw new InvalidDataException($"BlackthornData version {version} is not one this build reads (0).");
        }

        var count = reader.ReadEncodedInt();

        for (var i = 0; i < count; i++)
        {
            var m = reader.ReadEntity<Mobile>();
            var points = reader.ReadInt();

            if (m != null && points > 0)
            {
                DungeonPoints[m] = points;
            }
        }
    }
}
