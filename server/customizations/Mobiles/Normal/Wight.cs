// ServUO: Mobiles/Normal/Wight.cs (cc-P119). Values verbatim; serialization by the generator; range and speed constructor arguments
// dropped per the port recipe (both fidelity traps).

/* Based on Wailing Banshee, still no infos on Wight, including correct body ID */
using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class Wight : BaseCreature
{
    [Constructible]
    public Wight()
        : base(AIType.AI_Mage /* ServUO: AI_NecroMage (Q-050) */)
    {
        Body = 252;
        Hue = 1153;
        BaseSoundID = 0x482;

        SetStr(150, 200);
        SetDex(50, 60);
        SetInt(150, 200);

        SetHits(150, 250);

        SetDamage(13, 20);

        SetDamageType(ResistanceType.Physical, 20);
        SetDamageType(ResistanceType.Cold, 80);
        
        SetResistance(ResistanceType.Physical, 35, 45);
        SetResistance(ResistanceType.Fire, 30, 40);
        SetResistance(ResistanceType.Cold, 50, 60);
        SetResistance(ResistanceType.Poison, 40, 50);
        SetResistance(ResistanceType.Energy, 50, 60);

        SetSkill(SkillName.MagicResist, 40.0, 50.0);
        SetSkill(SkillName.Tactics, 45.0, 55.0);
        SetSkill(SkillName.Wrestling, 50.0, 60.0);
        SetSkill(SkillName.Magery, 60.0, 80.0);
        SetSkill(SkillName.Meditation, 50.0, 60.0);
        SetSkill(SkillName.Necromancy, 40.0, 69.3);
        SetSkill(SkillName.SpiritSpeak, 40.8, 68.9);

        Fame = 1500;
        Karma = -1500;

        VirtualArmor = 19;
    }

    public override string CorpseName => "a wight corpse";
    public override string DefaultName => "a Wight";

    public override bool BleedImmune => true;

    // DEVIATION (proposed, unnumbered; notes/cc-P119): ServUO gives this creature SetWeaponAbility(MortalStrike) and
    // SetWeaponAbility(ColdWind), one picked per use. Pinned has no ColdWind weapon ability (WeaponAbility.cs, 32 slots;
    // the effect exists only inside stock LadyOfTheSnow.OnGaveMeleeAttack), so this Wight uses Mortal Strike every time
    // and never Cold Wind's 5-second cold drain. Reverses when a ColdWind WeaponAbility is added (ServUO
    // Abilities/ColdWind.cs, 100 lines, plus its Abilities slot); it reaches Wight and FrostMite.
    public override WeaponAbility GetWeaponAbility() => WeaponAbility.MortalStrike;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Meager);
    }
}
