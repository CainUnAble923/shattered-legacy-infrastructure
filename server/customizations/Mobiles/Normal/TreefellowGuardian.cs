// ServUO: Mobiles/Normal/TreefellowGuardian.cs (cc-P119). Values verbatim; serialization by the generator; range and speed constructor arguments
// dropped per the port recipe (both fidelity traps).
//
// DEVIATION (D-41, cited, no new number): ServUO runs this on AI_Mystic; pinned has no Mysticism AI, so AI_Mage, as
// FairyDragon. It has Spellweaving and no Magery, so in practice it fights in melee. Its leaf item, TreefellowWood,
// is ported with it (Quests/TreefellowWood.cs, ServUO's path).

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class TreefellowGuardian : BaseCreature
{
    [Constructible]
    public TreefellowGuardian()
        : base(AIType.AI_Mage /* ServUO: AI_Mystic (D-41) */, FightMode.Evil)
    {
        Body = 301;

        SetStr(511, 695);
        SetDex(30, 55);
        SetInt(403, 491);

        SetHits(724, 900);

        SetDamage(12, 16);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 30, 35);
        SetResistance(ResistanceType.Cold, 50, 60);
        SetResistance(ResistanceType.Poison, 20, 30);
        SetResistance(ResistanceType.Energy, 80, 90);

        SetSkill(SkillName.MagicResist, 40.1, 55.0);
        SetSkill(SkillName.Tactics, 65.1, 90.0);
        SetSkill(SkillName.Wrestling, 65.1, 85.0);
        SetSkill(SkillName.Spellweaving, 120.0);

        Fame = 500;
        Karma = 1500;

        VirtualArmor = 24;
        PackItem(new Log(Utility.RandomMinMax(23, 34)));

        if (0.05 > Utility.RandomDouble())
        {
            PackItem(new TreefellowWood());
        }
    }

    public override string CorpseName => "a treefellow guardian corpse";
    public override string DefaultName => "a Treefellow Guardian";

    public override OppositionGroup OppositionGroup => OppositionGroup.FeyAndUndead;
    public override bool BleedImmune => true;
    public override WeaponAbility GetWeaponAbility() => WeaponAbility.Dismount; // ServUO: SetWeaponAbility

    public override int GetIdleSound() => 443;

    public override int GetDeathSound() => 31;

    public override int GetAttackSound() => 672;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Average);
    }
}
