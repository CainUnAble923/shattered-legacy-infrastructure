// ServUO: Services/Seasonal Events/TreasuresOfSorceresDungeon/Creatures/GarishGingerman.cs (cc-P408), the seventh
// boss of the Treasures of Sorcerer's Dungeon rotation.
//
// The port recipe: base(..., 10, 1, 0.2, 0.4) is base(AI_Mage) (Q-008); SetMagicalAbility(MageryMastery) goes to the
// cc-P121 ability profile, as ServUO's does (pinned MageAI reads it through cc-P121's patch, as StygianDrake's).

using ModernUO.Serialization;
using Server.Mobiles;

namespace Server.Engines.SorcerersDungeon;

[SerializationGenerator(0, false)]
public partial class GarishGingerman : BaseCreature
{
    [Constructible]
    public GarishGingerman() : base(AIType.AI_Mage)
    {
        Name = "a garish gingerman";

        Body = 14;
        BaseSoundID = 268;
        Hue = 1461;

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

        SetMagicalAbility(MagicalAbility.MageryMastery);
    }

    public override string CorpseName => "a garish gingerman corpse";
    public override bool AlwaysMurderer => true;
    public override Poison PoisonImmune => Poison.Deadly;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.UltraRich, 2);
    }
}
