// ServUO: Services/Expansions/Time Of Legends/Mobiles/GreaterPhoenix.cs (cc-P198). Values verbatim; serialization by
// the generator. SetSpecialAbility(GraspingClaw) goes to the profile (cc-P121, BloodFox's shape). ServUO gives it no
// GenerateLoot, only the packed gold; so does this.

using ModernUO.Serialization;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class GreaterPhoenix : BaseCreature
{
    [Constructible]
    public GreaterPhoenix() : base(AIType.AI_Mage)
    {
        Body = 832;
        BaseSoundID = 0x8F;

        SetStr(332, 386);
        SetDex(97, 113);
        SetInt(182, 258);

        SetDamage(11, 14);

        SetHits(119, 240);

        SetResistance(ResistanceType.Physical, 45, 55);
        SetResistance(ResistanceType.Fire, 70, 80);
        SetResistance(ResistanceType.Cold, 20, 30);
        SetResistance(ResistanceType.Poison, 40, 50);
        SetResistance(ResistanceType.Energy, 35, 45);

        SetDamageType(ResistanceType.Physical, 40);
        SetDamageType(ResistanceType.Fire, 60);

        SetSkill(SkillName.Wrestling, 60, 77);
        SetSkill(SkillName.MagicResist, 90, 105);
        SetSkill(SkillName.Tactics, 50, 70);
        SetSkill(SkillName.Magery, 90, 100);
        SetSkill(SkillName.EvalInt, 90, 100);

        PackGold(500, 700);

        Fame = 10000;
        Karma = -10000;

        SetSpecialAbility(SpecialAbility.GraspingClaw);
    }

    public override string CorpseName => "a greater phoenix corpse";
    public override string DefaultName => "a greater phoenix";
}
