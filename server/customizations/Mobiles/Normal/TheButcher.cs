// ServUO: Mobiles/Normal/TheButcher.cs (cc-P129). Values verbatim; serialization by the generator. AI_Necro is
// AI_Mage: pinned's MageAI casts Necromancy from skill (Q-050's finding for AI_NecroMage; pinned has no AI_Necro
// either). Packs the Jack O' Lantern carving kit (Items/Decorative/PumpkinCarvingKit.cs, ported beside it).

using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class TheButcher : BaseCreature
{
    [Constructible]
    public TheButcher() : base(AIType.AI_Mage)
    {
        Name = NameList.RandomName("daemon");
        Title = "the Butcher";
        Hue = 2075;
        Body = 306;
        BaseSoundID = 0x2A7;

        SetStr(250, 300);
        SetDex(90, 130);
        SetInt(800, 1000);

        SetHits(700, 1000);

        SetDamage(15, 27);

        SetDamageType(ResistanceType.Physical, 20);
        SetDamageType(ResistanceType.Cold, 40);
        SetDamageType(ResistanceType.Energy, 40);

        SetResistance(ResistanceType.Physical, 70, 80);
        SetResistance(ResistanceType.Fire, 25, 40);
        SetResistance(ResistanceType.Cold, 60, 70);
        SetResistance(ResistanceType.Poison, 60, 70);
        SetResistance(ResistanceType.Energy, 35, 40);

        SetSkill(SkillName.DetectHidden, 80.0);
        SetSkill(SkillName.Meditation, 120.0);
        SetSkill(SkillName.Poisoning, 160.0);
        SetSkill(SkillName.MagicResist, 180.0, 200.0);
        SetSkill(SkillName.Tactics, 100.0);
        SetSkill(SkillName.Wrestling, 75.0, 80.0);
        SetSkill(SkillName.Necromancy, 100.0, 110.0);
        SetSkill(SkillName.SpiritSpeak, 95.0, 105.0);

        Fame = 24000;
        Karma = -24000;

        VirtualArmor = 49;

        if (Utility.RandomDouble() < 0.2)
        {
            PackItem(new PumpkinCarvingKit());
        }
    }

    public override string CorpseName => "an butcher corpse";

    public override bool IgnoreYoungProtection => Core.ML;
    public override bool AutoDispel => true;
    public override bool BardImmune => !Core.SE;
    public override bool Unprovokable => Core.SE;
    public override bool AreaPeaceImmune => Core.SE;
    public override Poison PoisonImmune => Poison.Lethal;
    public override Poison HitPoison => 0.8 >= Utility.RandomDouble() ? Poison.Greater : Poison.Deadly;
    public override int TreasureMapLevel => 1;
    public override bool AlwaysMurderer => true;

    public override WeaponAbility GetWeaponAbility() =>
        Utility.RandomBool() ? WeaponAbility.MortalStrike : WeaponAbility.BleedAttack;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.UltraRich, 2);
    }
}
