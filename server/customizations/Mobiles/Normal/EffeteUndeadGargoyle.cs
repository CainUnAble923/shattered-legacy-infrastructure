// ServUO: Mobiles/Normal/EffeteUndeadGargoyle.cs (cc-P119). Values verbatim; serialization by the generator; range and speed constructor arguments
// dropped per the port recipe (both fidelity traps).

/* Based on Gargoyle, still no infos on Undead Gargoyle... Have to get also the correct body ID */
using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class EffeteUndeadGargoyle : BaseCreature
{
    [Constructible]
    public EffeteUndeadGargoyle()
        : base(AIType.AI_Melee)
    {
        Body = 722;
        BaseSoundID = 372;

        SetStr(60, 65);
        SetDex(60, 65);
        SetInt(30, 35);

        SetHits(65, 70);

        SetDamage(3, 7);

        SetDamageType(ResistanceType.Physical, 100);
       
        SetResistance(ResistanceType.Physical, 20);
        SetResistance(ResistanceType.Fire, 5, 10);
        SetResistance(ResistanceType.Cold, 25, 30);
        SetResistance(ResistanceType.Poison, 25);
        SetResistance(ResistanceType.Energy, 14, 15);

        SetSkill(SkillName.MagicResist, 50.0, 55.0);
        SetSkill(SkillName.Tactics, 50.0);
        SetSkill(SkillName.Wrestling, 50.0);

        Fame = 3500;
        Karma = -3500;

        VirtualArmor = 32;
    }

    public override string CorpseName => "an  effete undead gargoyle corpse";
    public override string DefaultName => "an Effete Undead Gargoyle";

    public override int TreasureMapLevel => 1;
    public override int Meat => 1;
    public override void GenerateLoot()
    {
        AddLoot(LootPack.Meager);
    }
}
