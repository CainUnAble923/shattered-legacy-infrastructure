// ServUO: Services/Seasonal Events/TreasuresOfSorceresDungeon/Creatures/HeadlessElf.cs (cc-P408). One of the nine
// bosses the Treasures of Sorcerer's Dungeon spawner rotates through (Spawner.cs, its third entry).
//
// The port recipe: AI_Mage as written; base(..., 10, 1, 0.2, 0.4) is base(AI_Mage) because the speeds are dead in
// ServUO too (Q-008); SetMagicalAbility(WrestlingMastery) goes to the cc-P121 ability profile, as ServUO's does.
// Race.RandomSkinHue() reads the creature's own race, set to Elf on the line before, as in ServUO.

using ModernUO.Serialization;
using Server.Mobiles;

namespace Server.Engines.SorcerersDungeon;

[SerializationGenerator(0, false)]
public partial class HeadlessElf : BaseCreature
{
    [Constructible]
    public HeadlessElf() : base(AIType.AI_Mage)
    {
        Name = "a headless elf";
        Race = Race.Elf;
        Body = 31;

        Hue = Race.RandomSkinHue();
        BaseSoundID = 0x39D;

        SetStr(700, 800);
        SetDex(90, 100);
        SetInt(450, 500);

        SetHits(8000);

        SetDamage(21, 27);

        SetDamageType(ResistanceType.Physical, 60);
        SetDamageType(ResistanceType.Fire, 20);
        SetDamageType(ResistanceType.Energy, 20);

        SetResistance(ResistanceType.Physical, 70, 80);
        SetResistance(ResistanceType.Fire, 70, 80);
        SetResistance(ResistanceType.Cold, 60, 70);
        SetResistance(ResistanceType.Poison, 60, 70);
        SetResistance(ResistanceType.Energy, 60, 70);

        SetSkill(SkillName.Anatomy, 130, 140);
        SetSkill(SkillName.Poisoning, 120);
        SetSkill(SkillName.MagicResist, 130, 140);
        SetSkill(SkillName.Tactics, 130, 140);
        SetSkill(SkillName.Wrestling, 120, 130);
        SetSkill(SkillName.Parry, 20, 30);

        SetSkill(SkillName.Magery, 110, 120);
        SetSkill(SkillName.EvalInt, 110, 120);
        SetSkill(SkillName.Meditation, 110, 120);
        SetSkill(SkillName.Focus, 120, 130);

        Fame = 12000;
        Karma = -12000;

        SetMagicalAbility(MagicalAbility.WrestlingMastery);
    }

    public override string CorpseName => "the corpse of a headless elf";
    public override bool AlwaysMurderer => true;
    public override Poison PoisonImmune => Poison.Deadly;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.UltraRich, 2);
    }
}
