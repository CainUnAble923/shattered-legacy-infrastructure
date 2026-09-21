// ServUO: Mobiles/Named/Mistral.cs (CC6 batch 5). Values verbatim; serialization by the generator. One of the four
// Labyrinth named (shared/malas/Labyrinth.json spells it "mistral"). Dropped: nothing.

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class Mistral : BaseCreature
{
    [Constructible]
    public Mistral() : base(AIType.AI_Mage)
    {
        Body = 13;
        Hue = 924;
        BaseSoundID = 263;

        SetStr(134, 201);
        SetDex(226, 238);
        SetInt(126, 134);

        SetHits(386, 609);

        SetDamage(17, 20); // Erica's

        SetDamageType(ResistanceType.Energy, 20);
        SetDamageType(ResistanceType.Cold, 40);
        SetDamageType(ResistanceType.Physical, 40);

        SetResistance(ResistanceType.Physical, 55, 64);
        SetResistance(ResistanceType.Fire, 36, 40);
        SetResistance(ResistanceType.Cold, 33, 39);
        SetResistance(ResistanceType.Poison, 30, 39);
        SetResistance(ResistanceType.Energy, 49, 53);

        SetSkill(SkillName.EvalInt, 96.2, 97.8);
        SetSkill(SkillName.Magery, 100.8, 112.9);
        SetSkill(SkillName.MagicResist, 106.2, 111.2);
        SetSkill(SkillName.Tactics, 110.2, 117.1);
        SetSkill(SkillName.Wrestling, 100.3, 104.0);

        Fame = 4500;
        Karma = -4500;

        VirtualArmor = 40;

        for (var i = 0; i < Utility.RandomMinMax(0, 1); i++)
        {
            PackItem(Loot.RandomScroll(0, Loot.ArcanistScrollTypes.Length, SpellbookType.Arcanist));
        }
    }

    public override string CorpseName => "a mistral corpse";
    public override string DefaultName => "Mistral";

    public override bool GivesMLMinorArtifact => true;

    public override double DispelDifficulty => 117.5;
    public override double DispelFocus => 45.0;
    public override bool BleedImmune => true;
    public override int TreasureMapLevel => 2;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Average);
        AddLoot(LootPack.Meager);
        AddLoot(LootPack.LowScrolls);
        AddLoot(LootPack.MedScrolls);
    }
}
