// ServUO: Services/Seasonal Events/TreasuresOfSorceresDungeon/Creatures/TwistedHolidayTree.cs (cc-P408), the fifth
// boss of the Treasures of Sorcerer's Dungeon rotation. Its name keeps ServUO's spelling, "a twisted holidy tree".
//
// The port recipe: base(..., 10, 1, 0.2, 0.4) is base(AI_Mage) (Q-008); SetAreaEffect(ExplosiveGoo) goes to the
// cc-P121 ability profile, as ServUO's does.

using ModernUO.Serialization;
using Server.Mobiles;

namespace Server.Engines.SorcerersDungeon;

[SerializationGenerator(0, false)]
public partial class TwistedHolidayTree : BaseCreature
{
    [Constructible]
    public TwistedHolidayTree() : base(AIType.AI_Mage)
    {
        Name = "a twisted holidy tree";

        Body = 47;
        BaseSoundID = 442;
        Hue = 1175;

        SetStr(400);
        SetDex(150);
        SetInt(1200);

        SetHits(8000);

        SetDamage(21, 27);

        SetDamageType(ResistanceType.Physical, 50);
        SetDamageType(ResistanceType.Energy, 50);

        SetResistance(ResistanceType.Physical, 60, 70);
        SetResistance(ResistanceType.Fire, 40, 50);
        SetResistance(ResistanceType.Cold, 80, 90);
        SetResistance(ResistanceType.Poison, 60, 70);
        SetResistance(ResistanceType.Energy, 100);

        SetSkill(SkillName.EvalInt, 200);
        SetSkill(SkillName.Magery, 120);
        SetSkill(SkillName.Meditation, 100);
        SetSkill(SkillName.MagicResist, 200);
        SetSkill(SkillName.Tactics, 100.0);
        SetSkill(SkillName.Wrestling, 120);

        Fame = 12000;
        Karma = -12000;

        SetAreaEffect(AreaEffect.ExplosiveGoo);
    }

    public override string CorpseName => "a twisted holiday tree corpse";
    public override bool AlwaysMurderer => true;
    public override Poison PoisonImmune => Poison.Deadly;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.UltraRich, 2);
    }
}
