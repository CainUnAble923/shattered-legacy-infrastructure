// ServUO: Mobiles/Named/GrayGoblinMageRenowned.cs (CC6 batch 5). Values verbatim; serialization by the generator.
// ServUO carries a version-1 Deserialize fix-up (version 0 -> Body 723, Hue 1900) for old saves; none exist here, so
// version 0 with no fix-up (the MysticsGarb precedent).
// Dropped: AllureImmune => true. Its only ServUO reader is the Spellweaving Dryad Allure spell, which pinned ModernUO
// does not have (only the scroll), so the override has no reader here (note section 5; the IsArtifact shape).

using System;
using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class GrayGoblinMageRenowned : BaseRenowned
{
    [Constructible]
    public GrayGoblinMageRenowned() : base(AIType.AI_Mage)
    {
        Title = "[Renowned]";

        Body = 723;
        Hue = 1900;

        BaseSoundID = 0x600;

        SetStr(550, 600);
        SetDex(70, 75);
        SetInt(500, 600);

        SetHits(1100, 1300);

        SetDamage(5, 7);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 30, 35);
        SetResistance(ResistanceType.Fire, 45, 50);
        SetResistance(ResistanceType.Cold, 40, 50);
        SetResistance(ResistanceType.Poison, 40, 50);
        SetResistance(ResistanceType.Energy, 20, 25);

        SetSkill(SkillName.MagicResist, 120.0, 125.0);
        SetSkill(SkillName.Tactics, 95.0, 100.0);
        SetSkill(SkillName.Wrestling, 100.0, 110.0);
        SetSkill(SkillName.EvalInt, 100.0, 120.0);
        SetSkill(SkillName.Meditation, 100.0, 105.0);
        SetSkill(SkillName.Magery, 100.0, 110.0);

        Fame = 1500;
        Karma = -1500;

        VirtualArmor = 28;

        switch (Utility.Random(20))
        {
            case 0:
                PackItem(new Scimitar());
                break;
            case 1:
                PackItem(new Katana());
                break;
            case 2:
                PackItem(new WarMace());
                break;
            case 3:
                PackItem(new WarHammer());
                break;
            case 4:
                PackItem(new Kryss());
                break;
            case 5:
                PackItem(new Pitchfork());
                break;
        }

        PackItem(new ThighBoots());

        switch (Utility.Random(3))
        {
            case 0:
                PackItem(new Ribs());
                break;
            case 1:
                PackItem(new Shaft());
                break;
            case 2:
                PackItem(new Candle());
                break;
        }

        if (0.2 > Utility.RandomDouble())
        {
            PackItem(new BolaBall());
        }
    }

    public override string CorpseName => "Gray Goblin Mage [Renowned] corpse";
    public override string DefaultName => "Gray Goblin Mage";

    public override Type[] UniqueSAList => Array.Empty<Type>();
    public override Type[] SharedSAList => new[] { typeof(StormCaller), typeof(TorcOfTheGuardians), typeof(GiantSteps), typeof(CavalrysFolly) };

    public override int GetAngerSound() => 0x600;
    public override int GetIdleSound() => 0x600;
    public override int GetAttackSound() => 0x5FD;
    public override int GetHurtSound() => 0x5FF;
    public override int GetDeathSound() => 0x5FE;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.FilthyRich);
    }
}
