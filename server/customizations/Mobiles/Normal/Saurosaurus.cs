using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles
{
    [SerializationGenerator(0, false)]
    public partial class Saurosaurus : BaseCreature
    {
        [Constructible]
        public Saurosaurus() : base(AIType.AI_Mage)
        {
            Body = 1291;
            BaseSoundID = 362;

            SetStr(802, 824);
            SetDex(201, 220);
            SetInt(403, 440);

            SetDamage(21, 28);

            SetHits(1321, 1468);

            SetResistance(ResistanceType.Physical, 75, 85);
            SetResistance(ResistanceType.Fire, 80, 90);
            SetResistance(ResistanceType.Cold, 45, 55);
            SetResistance(ResistanceType.Poison, 35, 45);
            SetResistance(ResistanceType.Energy, 45, 55);

            SetDamageType(ResistanceType.Physical, 100);

            SetSkill(SkillName.MagicResist, 70.0, 90.0);
            SetSkill(SkillName.Tactics, 110.0, 120.0);
            SetSkill(SkillName.Wrestling, 110.0, 130.0);
            SetSkill(SkillName.Anatomy, 50.0, 60.0);
            SetSkill(SkillName.DetectHidden, 80.0);
            SetSkill(SkillName.Parry, 80.0, 90);
            SetSkill(SkillName.Focus, 115.0, 125.0);

            Fame = 11000;
            Karma = -11000;

            Tamable = true;
            ControlSlots = 3;
            MinTameSkill = 102.0;
        }

        public override string CorpseName => "a saurosaurus corpse";
        public override string DefaultName => "a saurosaurus";

        public override bool CanAngerOnTame => true;
        public override bool StatLossAfterTame => true;
        public override int Meat => 5;
        public override int Hides => 11;
        public override int TreasureMapLevel => 2;

        // ServUO: SetWeaponAbility(ConcussionBlow).
        public override WeaponAbility GetWeaponAbility() => WeaponAbility.ConcussionBlow;

        // ServUO also calls SetSpecialAbility(SpecialAbility.TailSwipe) and
        // SetSpecialAbility(SpecialAbility.LifeLeech). Both go through PetTrainingHelper, which
        // ModernUO does not have, and ServUO's own source carries the comment
        // "// Missing: Life Leech, Tail Swipe ability". Dropped; see notes/cc3-eodon.md.
        //
        // ServUO also sets DragonBlood => 8. ModernUO has neither the carving property nor a
        // DragonBlood item. Dropped.

        public override void GenerateLoot()
        {
            AddLoot(LootPack.FilthyRich, 3);
        }
    }
}
