// cc-P860: the ridable polar bear, a live tamable mount of our own and the source animal for the ice bloodline in pet
// breeding (Chase 11-08, from P853's assessment, item 2: an ice line with no new art). After Milva's "Tamable Ridable
// Polar Bear" (servuo.dev, 2013; reference only, D:\UO\third-party\code\ridable-polar-bear), rebuilt here in our style.
//
// Stock art only: body 213 (the polar bear, typed Animal in pinned's bodyTable.cfg) and mount item 0x3EC5 (the ethereal
// polar bear's, pinned Ethereals.cs; the client draws it as body 213 ridden). Pinned's shrink.json gives body 213 the
// figurine 0x20E1, so the leash and the stable work as for any polar bear.
//
// The numbers are the polar bear's (pinned PolarBear.cs and the download agree on strength, hits, damage, resists and
// skills). Ours differ from the download in three places: an animal's AI and only fights back (pinned's polar bear;
// the download's was melee and attacked the nearest thing), a tame of 59.1 (the download's 29.1 was a horse's, below a
// plain polar bear's 35.1; a mount that carries a bloodline should take some taming), and one control slot as both have.
// Where it lives: RidablePolarBearDens (Dagger Isle's snow, seeded on every world load).

using ModernUO.Serialization;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class RidablePolarBear : BaseMount
{
    public const int BodyId = 213;
    public const int MountItemId = 0x3EC5;
    public const double TameSkill = 59.1;
    public const string Credit = "Milva";

    [Constructible]
    public RidablePolarBear() : base(BodyId, MountItemId, AIType.AI_Animal, FightMode.Aggressor)
    {
        BaseSoundID = 0xA3;

        SetStr(116, 140);
        SetDex(81, 105);
        SetInt(26, 50);

        SetHits(70, 84);
        SetMana(0);

        SetDamage(7, 12);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 25, 35);
        SetResistance(ResistanceType.Cold, 60, 80);
        SetResistance(ResistanceType.Poison, 15, 25);
        SetResistance(ResistanceType.Energy, 10, 15);

        SetSkill(SkillName.MagicResist, 45.1, 60.0);
        SetSkill(SkillName.Tactics, 60.1, 90.0);
        SetSkill(SkillName.Wrestling, 45.1, 70.0);

        Fame = 1500;
        Karma = 0;

        VirtualArmor = 18;

        Tamable = true;
        ControlSlots = 1;
        MinTameSkill = TameSkill;
    }

    public override string DefaultName => "a ridable polar bear";
    public override string CorpseName => "a polar bear corpse";

    public override int Meat => 2;
    public override int Hides => 16;
    public override FoodType FavoriteFood => FoodType.Fish | FoodType.FruitsAndVeggies | FoodType.Meat;
    public override PackInstinct PackInstinct => PackInstinct.Bear;

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add($"After {Credit}'s ridable polar bear");
    }
}
