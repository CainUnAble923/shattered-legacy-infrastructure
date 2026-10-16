// ServUO: Mobiles/Normal/Macaw.cs (cc-P129). Values verbatim; serialization by the generator. The vicious macaw that
// a gold-foil nest (Items/Internal/MacawSpawner.cs) looses on whoever picks the foil up; GoldFoil is its drop.
//
// ServUO declares this creature in namespace Server.Items (the file sits under Mobiles/Normal); it is Server.Mobiles
// here like every creature, which changes nothing a player or a spawner sees (lookups are by type name). The spawner
// link is not persisted in ServUO (its Serialize writes only the version; the spawner rewires it on load), and not
// here either. ServUO's Delete() override is pinned's OnAfterDelete.

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class Macaw : BaseCreature
{
    [CommandProperty(AccessLevel.GameMaster)]
    public MacawSpawner MacawSpawner { get; set; }

    [Constructible]
    public Macaw() : this(null)
    {
    }

    public Macaw(MacawSpawner spawner) : base(AIType.AI_Melee)
    {
        MacawSpawner = spawner;

        Body = 5;
        Hue = Utility.RandomBirdHue();

        SetStr(100, 150);
        SetDex(400, 500);
        SetInt(80, 90);

        SetHits(700, 800);

        SetDamage(15, 25);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 80, 90);
        SetResistance(ResistanceType.Fire, 60, 77);
        SetResistance(ResistanceType.Cold, 70, 85);
        SetResistance(ResistanceType.Poison, 55, 85);
        SetResistance(ResistanceType.Energy, 50, 60);

        SetSkill(SkillName.Wrestling, 120.0, 140.0);
        SetSkill(SkillName.Tactics, 120.0, 140.0);
        SetSkill(SkillName.MagicResist, 95.0, 105.0);

        Fame = 7000;
        Karma = -7000;
    }

    public override string DefaultName => "vicious macaw";

    public override int GetIdleSound() => 0x2EF;
    public override int GetAttackSound() => 0x2EE;
    public override int GetAngerSound() => 0x2EF;
    public override int GetHurtSound() => 0x2F1;
    public override int GetDeathSound() => 0x2F2;

    public override MeatType MeatType => MeatType.Bird;
    public override int Feathers => 32;
    public override int Meat => 1;
    public override FoodType FavoriteFood => FoodType.FruitsAndVeggies | FoodType.GrainsAndHay;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.FilthyRich, 2);
    }

    protected override void OnLocationChange(Point3D oldLocation)
    {
        base.OnLocationChange(oldLocation);

        if (MacawSpawner != null && !InRange(MacawSpawner.Location, 20))
        {
            MacawSpawner.Spawn.Remove(this);
            MacawSpawner = null;
        }
    }

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        if (0.25 > Utility.RandomDouble())
        {
            c.DropItem(new GoldFoil());
        }
    }

    public override void OnAfterDelete()
    {
        base.OnAfterDelete();

        MacawSpawner?.Spawn.Remove(this);
    }
}
