using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles
{
    [SerializationGenerator(0, false)]
    public partial class Archaeosaurus : BaseCreature
    {
        [Constructible]
        public Archaeosaurus() : base(AIType.AI_Melee)
        {
            Body = 1287;
            BaseSoundID = 422;

            SetStr(405, 421);
            SetDex(301, 320);
            SetInt(201, 224);

            SetDamage(14, 16);

            SetHits(1818, 2500);

            SetResistance(ResistanceType.Physical, 2, 3);
            SetResistance(ResistanceType.Fire, 4, 5);
            SetResistance(ResistanceType.Cold, 2, 3);
            SetResistance(ResistanceType.Poison, 3, 4);
            SetResistance(ResistanceType.Energy, 3);

            SetDamageType(ResistanceType.Poison, 50);
            SetDamageType(ResistanceType.Fire, 50);

            SetSkill(SkillName.MagicResist, 100.0, 115.0);
            SetSkill(SkillName.Tactics, 90.0, 110.0);
            SetSkill(SkillName.Wrestling, 90.0, 110.0);
            SetSkill(SkillName.DetectHidden, 60.0, 70.0);
            SetSkill(SkillName.EvalInt, 95.0, 105.0);
            SetSkill(SkillName.Ninjitsu, 120.0);

            Fame = 8100;
            Karma = -8100;
        }

        public override string CorpseName => "an archaeosaurus corpse";
        public override string DefaultName => "an Archaeosaurus";

        public override int Meat => 1;
        public override int Hides => 7;
        public override int TreasureMapLevel => 1;

        // ServUO also sets DragonBlood => 6. ModernUO has neither the carving property nor a
        // DragonBlood item. Dropped. See notes/cc3-eodon.md.

        public override WeaponAbility GetWeaponAbility() =>
            Utility.RandomBool() ? WeaponAbility.BleedAttack : WeaponAbility.TalonStrike;

        public override void GenerateLoot()
        {
            AddLoot(LootPack.FilthyRich, 2);
        }
    }
}
