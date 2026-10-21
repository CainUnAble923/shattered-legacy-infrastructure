// ServUO: Services/Revamped Dungeons/WrongDungeon/Items/StinkingCauldron.cs (cc-P194).
//
// Fezzik the Ogre Cook drops one of these where he stands (once every two minutes at most) when hit or hurt by a
// spell; ten seconds later it bursts into one or two Gooey Maggots and is gone. A cauldron that is loaded from a
// save bursts ten seconds after the load, as on ServUO.

using System;
using ModernUO.Serialization;
using Server.Mobiles;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class StinkingCauldron : Item
{
    [Constructible]
    public StinkingCauldron() : base(0x142A)
    {
        Movable = false;
        Hue = 363;
        Timer.DelayCall(TimeSpan.FromSeconds(10), GooeyMaggotsSpawn);
    }

    public override double DefaultWeight => 0.0;

    public override int LabelNumber => 1152163; // stinking cauldron

    public void GooeyMaggotsSpawn()
    {
        if (Deleted)
        {
            return;
        }

        if (Map != null && Map != Map.Internal)
        {
            var amount = Utility.RandomMinMax(1, 2);

            for (var i = 0; i < amount; ++i)
            {
                BaseCreature creature = new GooeyMaggots();
                creature.MoveToWorld(GetSpawnPosition(Location, Map, 2), Map);
                Effects.SendLocationParticles(EffectItem.Create(Location, Map, EffectItem.DefaultDuration), 0x3728, 8, 20, 5042);
            }
        }

        Delete();
    }

    // ServUO's Map.GetSpawnPosition(center, range): a random tile within range a mobile can stand on, else the
    // center. Pinned Map has no such helper.
    public static Point3D GetSpawnPosition(Point3D center, Map map, int range)
    {
        for (var i = 0; i < 10; i++)
        {
            var x = center.X + Utility.RandomMinMax(-range, range);
            var y = center.Y + Utility.RandomMinMax(-range, range);
            var z = map.GetAverageZ(x, y);

            if (map.CanSpawnMobile(x, y, center.Z))
            {
                return new Point3D(x, y, center.Z);
            }

            if (map.CanSpawnMobile(x, y, z))
            {
                return new Point3D(x, y, z);
            }
        }

        return center;
    }

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        Timer.DelayCall(TimeSpan.FromSeconds(10), GooeyMaggotsSpawn);
    }
}
