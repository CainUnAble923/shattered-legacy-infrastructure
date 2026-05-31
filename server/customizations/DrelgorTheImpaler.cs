using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

/// <summary>
/// Drelgor the Impaler — the undead warlord who led the assault on Haven.
/// Quest target for "Cleansing Old Haven" (Chivalry training). Moderately
/// tough so new players can eventually defeat him.
/// </summary>
[SerializationGenerator(0, false)]
public partial class DrelgorTheImpaler : BaseCreature
{
    [Constructible]
    public DrelgorTheImpaler() : base(AIType.AI_Melee, FightMode.Closest, 10, 1)
    {
        Name  = "Drelgor the Impaler";
        Body  = 0x190;          // Male humanoid
        Hue   = 0x8421;         // Undead grey
        BaseSoundID = 0x480;    // Undead sounds

        SetStr(180, 210);
        SetDex(80,  100);
        SetInt(80,  100);

        SetHits(450, 550);
        SetStam(80,  100);
        SetMana(80,  100);

        SetDamage(16, 24);

        SetDamageType(ResistanceType.Physical, 70);
        SetDamageType(ResistanceType.Cold,     30);

        SetResistance(ResistanceType.Physical, 40, 50);
        SetResistance(ResistanceType.Fire,     20, 30);
        SetResistance(ResistanceType.Cold,     55, 65);
        SetResistance(ResistanceType.Poison,   30, 40);
        SetResistance(ResistanceType.Energy,   30, 40);

        SetSkill(SkillName.Swords,       90.0, 100.0);
        SetSkill(SkillName.Tactics,      85.0, 95.0);
        SetSkill(SkillName.MagicResist,  75.0, 85.0);
        SetSkill(SkillName.Wrestling,    70.0, 80.0);
        SetSkill(SkillName.Anatomy,      70.0, 80.0);

        Fame = 8000;
        Karma = -8000;

        VirtualArmor = 45;

        AddItem(new PlateChest   { Movable = false, Hue = 0x835 });
        AddItem(new PlateLegs    { Movable = false, Hue = 0x835 });
        AddItem(new PlateArms    { Movable = false, Hue = 0x835 });
        AddItem(new PlateGloves  { Movable = false, Hue = 0x835 });
        AddItem(new PlateHelm    { Movable = false, Hue = 0x835 });
        AddItem(new VikingSword  { Movable = false, Hue = 0x835 });
        AddItem(new MetalShield  { Movable = false, Hue = 0x835 });
    }

    public override string CorpseName  => "Drelgor's corpse";
    public override bool   AlwaysMurderer => true;
    public override bool   ShowFameTitle  => false;
    public override bool   IgnoreYoungProtection => true;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Rich);
        AddLoot(LootPack.Average);
    }
}
