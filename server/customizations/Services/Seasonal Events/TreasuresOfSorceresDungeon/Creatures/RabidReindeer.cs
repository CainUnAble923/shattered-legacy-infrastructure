// ServUO: Services/Seasonal Events/TreasuresOfSorceresDungeon/Creatures/RabidReindeer.cs (cc-P408), the sixth boss
// of the Treasures of Sorcerer's Dungeon rotation.
//
// The port recipe: base(..., 10, 1, 0.2, 0.4) is base(AI_Melee) (Q-008); SetSpecialAbility(VenomousBite) goes to the
// cc-P121 ability profile, as ServUO's does.

using ModernUO.Serialization;
using Server.Mobiles;

namespace Server.Engines.SorcerersDungeon;

[SerializationGenerator(0, false)]
public partial class RabidReindeer : BaseCreature
{
    [Constructible]
    public RabidReindeer() : base(AIType.AI_Melee)
    {
        Name = "a rabid reindeer";

        Body = 0xEA;
        Hue = 2707;

        SetStr(800);
        SetDex(150);
        SetInt(1200);

        SetHits(8000);

        SetDamage(22, 29);

        SetDamageType(ResistanceType.Physical, 50);
        SetDamageType(ResistanceType.Cold, 50);

        SetResistance(ResistanceType.Physical, 60, 70);
        SetResistance(ResistanceType.Fire, 40, 50);
        SetResistance(ResistanceType.Cold, 100);
        SetResistance(ResistanceType.Poison, 60, 70);
        SetResistance(ResistanceType.Energy, 60, 70);

        SetSkill(SkillName.Anatomy, 115, 120);
        SetSkill(SkillName.Poisoning, 120);
        SetSkill(SkillName.MagicResist, 115, 120);
        SetSkill(SkillName.Tactics, 100.0);
        SetSkill(SkillName.Wrestling, 120, 130);

        Fame = 12000;
        Karma = -12000;

        SetSpecialAbility(SpecialAbility.VenomousBite);
    }

    public override string CorpseName => "a rabid reindeers corpse";
    public override int Meat => 6;
    public override int Hides => 15;
    public override Poison HitPoison => Poison.Lethal;
    public override bool AlwaysMurderer => true;
    public override Poison PoisonImmune => Poison.Lethal;

    public override int GetAttackSound() => 0x82;
    public override int GetHurtSound() => 0x83;
    public override int GetDeathSound() => 0x84;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.UltraRich, 2);
    }
}
