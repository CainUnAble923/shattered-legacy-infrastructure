using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles
{
    [SerializationGenerator(0, false)]
    public partial class Dimetrosaur : BaseCreature
    {
        [Constructible]
        public Dimetrosaur() : base(AIType.AI_Melee)
        {
            Body = 1285;

            SetStr(526, 601);
            SetDex(166, 184);
            SetInt(373, 435);

            SetDamage(18, 21);
            SetHits(5300, 5400);

            SetResistance(ResistanceType.Physical, 80, 90);
            SetResistance(ResistanceType.Fire, 60, 70);
            SetResistance(ResistanceType.Cold, 60, 70);
            SetResistance(ResistanceType.Poison, 65, 75);
            SetResistance(ResistanceType.Energy, 65, 75);

            SetDamageType(ResistanceType.Physical, 90);
            SetDamageType(ResistanceType.Poison, 10);

            SetSkill(SkillName.MagicResist, 120.0, 140.0);
            SetSkill(SkillName.Tactics, 100.0, 120.0);
            SetSkill(SkillName.Wrestling, 115.0, 125.0);
            SetSkill(SkillName.Anatomy, 70.0, 80.0);
            SetSkill(SkillName.Poisoning, 85.0, 95.0);
            SetSkill(SkillName.DetectHidden, 70.0, 80.0);
            SetSkill(SkillName.Parry, 95.0, 105.0);

            Fame = 17000;
            Karma = -17000;

            Tamable = true;
            ControlSlots = 3;
            MinTameSkill = 102.0;
        }

        public override string CorpseName => "a dimetrosaur corpse";
        public override string DefaultName => "a dimetrosaur";

        public override bool CanAngerOnTame => true;
        public override bool StatLossAfterTame => true;
        public override int Meat => 1;
        public override int Hides => 11;
        public override HideType HideType => HideType.Spined;
        public override FoodType FavoriteFood => FoodType.FruitsAndVeggies;
        public override int TreasureMapLevel => 6;

        // ServUO: SetWeaponAbility(MortalStrike) + SetWeaponAbility(Dismount).
        public override WeaponAbility GetWeaponAbility() =>
            Utility.RandomBool() ? WeaponAbility.MortalStrike : WeaponAbility.Dismount;

        // Two ServUO behaviours have no ModernUO equivalent and are dropped. Both are recorded
        // in notes/cc3-eodon.md:
        //   SetAreaEffect(AreaEffect.PoisonBreath) - PetTrainingHelper area effects.
        //   OnAfterTame, which quartered Hits on first tame. ModernUO has no OnAfterTame hook;
        //   StatLossAfterTame above gives AnimalTaming's ScaleStats(0.50) instead.

        public override void GenerateLoot()
        {
            AddLoot(LootPack.UltraRich, 2);
        }

        public override int GetIdleSound() => 0x2C4;
        public override int GetAttackSound() => 0x2C0;
        public override int GetDeathSound() => 0x2C1;
        public override int GetAngerSound() => 0x2C4;
        public override int GetHurtSound() => 0x2C3;
    }
}
