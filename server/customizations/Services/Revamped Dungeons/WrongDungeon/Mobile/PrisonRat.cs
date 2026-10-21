// ServUO: Services/Revamped Dungeons/WrongDungeon/Mobile/PrisonRat.cs (cc-P194).

using ModernUO.Serialization;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class PrisonRat : BaseCreature
{
    [Constructible]
    public PrisonRat() : base(AIType.AI_Melee)
    {
        Body = 0xEE;
        Hue = 443;
        BaseSoundID = 0xCC;

        SetStr(9);
        SetDex(35);
        SetInt(7, 10);

        SetHits(50);
        SetStam(25);
        SetMana(7, 10);

        SetDamage(5, 8);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 5, 10);
        SetResistance(ResistanceType.Poison, 15, 25);
        SetResistance(ResistanceType.Energy, 5, 10);

        SetSkill(SkillName.MagicResist, 3.7, 20.7);
        SetSkill(SkillName.Tactics, 6.7, 17.0);
        SetSkill(SkillName.Wrestling, 9.1, 19.5);

        Fame = 150;
        Karma = -150;

        VirtualArmor = 6;
    }

    public override string CorpseName => "a rat corpse";
    public override string DefaultName => "a prison rat";

    public override int Meat => 1;
    public override FoodType FavoriteFood => FoodType.Fish;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Poor);
    }
}
