// ServUO: Services/Seasonal Events/TreasuresOfSorceresDungeon/Creatures/NightmareFairy.cs (cc-P408), the first boss
// of the Treasures of Sorcerer's Dungeon rotation.
//
// The port recipe: base(..., 10, 1, 0.2, 0.4) is base(AI_Mage) (Q-008); SetSpecialAbility(LifeLeech) and
// SetSpecialAbility(LifeDrain) go to the cc-P121 ability profile, as ServUO's do.

using ModernUO.Serialization;
using Server.Mobiles;

namespace Server.Engines.SorcerersDungeon;

[SerializationGenerator(0, false)]
public partial class NightmareFairy : BaseCreature
{
    [Constructible]
    public NightmareFairy() : base(AIType.AI_Mage)
    {
        Name = "a nightmare fairy";

        Body = 176;
        BaseSoundID = 0x467;
        Hue = 1910;

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

        SetSkill(SkillName.EvalInt, 120);
        SetSkill(SkillName.Magery, 120);
        SetSkill(SkillName.Meditation, 100);
        SetSkill(SkillName.MagicResist, 200);
        SetSkill(SkillName.Tactics, 100.0);
        SetSkill(SkillName.Wrestling, 120);

        Fame = 12000;
        Karma = -12000;

        SetSpecialAbility(SpecialAbility.LifeLeech);
        SetSpecialAbility(SpecialAbility.LifeDrain);
    }

    public override string CorpseName => "a nightmare fairys corpse";
    public override bool AlwaysMurderer => true;
    public override Poison PoisonImmune => Poison.Deadly;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.UltraRich, 2);
    }
}
