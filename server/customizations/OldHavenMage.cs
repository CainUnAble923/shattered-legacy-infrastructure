using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

/// <summary>
/// Spectral undead mage haunting the ruins of Old Haven.
/// Uses AI_Mage and casts 3rd–5th circle spells (Fireball, Lightning, etc.)
/// making it ideal for training Magic Resist. Low HP so players can survive
/// while the spells still land often enough to grant skill gains.
/// </summary>
[SerializationGenerator(0, false)]
public partial class OldHavenMage : BaseCreature
{
    [Constructible]
    public OldHavenMage() : base(AIType.AI_Mage, FightMode.Closest, 10, 1)
    {
        Name        = "an old haven mage";
        Body        = 0x190;    // Male humanoid
        Hue         = 0x47E;    // Spectral blue-grey
        BaseSoundID = 0x480;    // Undead sounds

        // Low physical stats — this creature lives to cast, not brawl.
        SetStr(60,  80);
        SetDex(60,  80);
        SetInt(130, 160);       // High Int → casts often and hits harder

        SetHits(90,  130);
        SetStam(60,  80);
        SetMana(220, 280);      // Big mana pool so it doesn't run dry

        SetDamage(4, 10);

        SetDamageType(ResistanceType.Physical, 20);
        SetDamageType(ResistanceType.Energy,   80);

        SetResistance(ResistanceType.Physical, 15, 25);
        SetResistance(ResistanceType.Fire,     10, 20);
        SetResistance(ResistanceType.Cold,     35, 45);
        SetResistance(ResistanceType.Poison,   15, 25);
        SetResistance(ResistanceType.Energy,   30, 40);

        // Magery 65–80 → casts circles 3–5 (Fireball, Lightning, Poison,
        // Energy Bolt).  EvalInt at the same level so spells aren't trivially
        // easy to resist.  Players with 0–70 Magic Resist will gain readily.
        SetSkill(SkillName.Magery,     65.0, 80.0);
        SetSkill(SkillName.EvalInt,    65.0, 80.0);
        SetSkill(SkillName.MagicResist, 55.0, 70.0);
        SetSkill(SkillName.Meditation,  80.0, 100.0); // Meditates to keep mana up
        SetSkill(SkillName.Tactics,     30.0, 40.0);
        SetSkill(SkillName.Wrestling,   30.0, 40.0);

        Fame  = 1500;
        Karma = -1500;

        VirtualArmor = 12;

        // Spectral blue-grey robe to sell the ghost look.
        AddItem(new Robe { Movable = false, Hue = 0x47E });
    }

    public override string CorpseName => "a spectral mage corpse";
    public override bool   AlwaysMurderer => true;
    public override bool   ShowFameTitle  => false;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Meager);
        AddLoot(LootPack.LowScrolls);
    }
}
