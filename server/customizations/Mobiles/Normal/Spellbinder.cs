// ServUO: Mobiles/Normal/Spellbinder.cs (CC6 batch 1). Values verbatim; serialization by the generator.
//
// DEVIATION: ServUO runs this on AI_Spellbinder (Mobiles/AI/Magical AI/SpellbinderAI.cs: a Magery caster that
// also heals through Necromancy's spirit speak, teleports and dispels). ModernUO's AIType has no Spellbinder AI,
// so it runs AI_Mage. FightMode.Aggressor is carried. The body is one of three ghost bodies at random, as in
// ServUO (Utility.RandomList exists unchanged).

using ModernUO.Serialization;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class Spellbinder : BaseCreature
{
    [Constructible]
    public Spellbinder() : base(AIType.AI_Mage, FightMode.Aggressor)
    {
        Body = Utility.RandomList(26, 50, 56);
        BaseSoundID = 0x482;

        SetStr(46, 70);
        SetDex(47, 65);
        SetInt(187, 210);

        SetHits(36, 50);

        SetDamage(3, 6);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 20, 30);
        SetResistance(ResistanceType.Cold, 15, 25);
        SetResistance(ResistanceType.Poison, 10, 20);

        SetSkill(SkillName.MagicResist, 35.1, 45.0);
        SetSkill(SkillName.Tactics, 35.1, 50.0);
        SetSkill(SkillName.Wrestling, 35.1, 50.0);

        Fame = 2500;
        Karma = -2500;

        VirtualArmor = 28;
    }

    public override string CorpseName => "a ghostly corpse";
    public override string DefaultName => "a spectral spellbinder";

    public override bool BleedImmune => true;
    public override Poison PoisonImmune => Poison.Regular;
    public override OppositionGroup OppositionGroup => OppositionGroup.FeyAndUndead;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Meager);
        PackItem(Loot.RandomWeapon());
    }
}
