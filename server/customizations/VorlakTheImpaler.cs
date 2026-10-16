using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

/// <summary>
/// Vorlak the Impaler — the undead warlord who led the assault on Haven, the shard's own Old Haven mini-boss.
/// Was DrelgorTheImpaler until cc-P129 (Chase, 2026-10-15: "rename our Drelgor for use elsewhere, he is not the
/// right mob, but is a good miniboss"); ServUO's Drelgor, the "Cleansing Old Haven" quest target, is now ported
/// under that name (Mobiles/Named/Drelgor.cs). Design, loot and abilities unchanged; the TypeAlias loads every saved
/// DrelgorTheImpaler and resolves the old name in spawner entries (AssemblyHandler.FindTypeByName reads aliases).
/// Moderately tough so new players can eventually defeat him.
/// </summary>
[TypeAlias("Server.Mobiles.DrelgorTheImpaler")]
[SerializationGenerator(0, false)]
public partial class VorlakTheImpaler : BaseCreature
{
    [Constructible]
    public VorlakTheImpaler() : base(AIType.AI_Melee, FightMode.Closest, 10, 1)
    {
        Name  = "Vorlak the Impaler";
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

    public override string CorpseName  => "Vorlak's corpse";
    public override bool   AlwaysMurderer => true;
    public override bool   ShowFameTitle  => false;
    public override bool   IgnoreYoungProtection => true;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Rich);
        AddLoot(LootPack.Average);
    }
}
