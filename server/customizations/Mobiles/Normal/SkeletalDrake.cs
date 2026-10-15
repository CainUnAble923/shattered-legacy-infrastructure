// ServUO: Mobiles/Normal/SkeletalDrake.cs (cc-P119). Values verbatim; serialization by the generator; range and speed constructor arguments
// dropped per the port recipe (both fidelity traps).

/* Based on Skeletal Dragon, still have to get detailed info on Skeletal Drake
2017/4/09 update: brought more into line with the one extant data point*/
using ModernUO.Serialization;
namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class SkeletalDrake : BaseCreature
{
    [Constructible]
    public SkeletalDrake()
        : base(AIType.AI_Mage /* ServUO: AI_NecroMage (Q-050) */)
    {
        Body = 104;
        Hue = 2101;
        BaseSoundID = 0x488;

        SetStr(600, 700);
        SetDex(70, 100);
        SetInt(300, 400);

        SetHits(300, 400);

        SetDamage(29, 35);

        SetDamageType(ResistanceType.Physical, 75);
        SetDamageType(ResistanceType.Fire, 25);

        SetResistance(ResistanceType.Physical, 75, 80);
        SetResistance(ResistanceType.Fire, 40, 60);
        SetResistance(ResistanceType.Cold, 40, 60);
        SetResistance(ResistanceType.Poison, 70, 80);
        SetResistance(ResistanceType.Energy, 40, 60);

        SetSkill(SkillName.EvalInt, 45, 60);
        SetSkill(SkillName.Magery, 50, 65);
        SetSkill(SkillName.MagicResist, 75, 90);
        SetSkill(SkillName.Tactics, 70, 85);
        SetSkill(SkillName.Wrestling, 60, 75);

        Fame = 15000;
        Karma = -15000;

        VirtualArmor = 80;
    }

    public override string CorpseName => "a skeletal drake corpse";
    public override string DefaultName => "a skeletal drake";

    public override bool AutoDispel => true;
    public override bool BleedImmune => true;
    public override bool ReacquireOnMovement => true;
    public override int Hides => 20;
    public override int Meat => 19; // ServUO: "where's it hiding these? :)"
    public override HideType HideType => HideType.Barbed;
    public override OppositionGroup OppositionGroup => OppositionGroup.FeyAndUndead;
    public override Poison PoisonImmune => Poison.Lethal;
    // ServUO's Tribe => TribeType.Undead is pinned's OppositionGroup, already given above (AGENTS.md, equivalents).

    public override void GenerateLoot()
    {
        AddLoot(LootPack.FilthyRich, 2);
        AddLoot(LootPack.Gems, 4);
    }
}
