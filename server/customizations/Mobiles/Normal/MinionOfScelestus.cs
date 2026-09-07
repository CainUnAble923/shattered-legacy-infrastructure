// Ported from ServUO Scripts/Mobiles/Normal/MinionOfScelestus.cs.
//
// The Kill-objective target of the S5 quest spike. See notes/s5-quest-spike.md.
//
// Two deviations, both logged in notes/s5-quest-spike.md section 6:
//
//   D-S5-1  ServUO calls SetSpecialAbility(SpecialAbility.DragonBreath). SpecialAbility is
//           part of ServUO's Pet Training system, which ModernUO does not have and which
//           docs/tasks.md CC6 declines. The line is deleted per the Saurosaurus precedent:
//           a creature that loses one ability is still worth having.
//   D-S5-2  ServUO sets TaintedLifeAura, a BaseCreature flag that suppresses life leech
//           against the creature. ModernUO's BaseCreature has no such flag and adding it
//           would mean patching BaseWeapon and AOS. Dropped.
//
// Poison.Parasitic in ServUO is the parasitic family; ModernUO names its members
// individually in Misc/PoisonKinds.cs, so PoisonImmune uses LethalParasitic.

using System;
using ModernUO.Serialization;
using Server.Items;
using Server.Spells.Ninjitsu;

namespace Server.Mobiles
{
    [SerializationGenerator(0, false)]
    public partial class MinionOfScelestus : BaseCreature
    {
        private const int MaxWanderDistance = 45;

        private static readonly Type[] _documentTypes =
        {
            typeof(ChallengeRite), typeof(AnthenaeumDecree), typeof(LetterFromTheKing),
            typeof(OnTheVoid), typeof(ShilaxrinarsMemorial), typeof(ToTheHighScholar),
            typeof(ToTheHighBroodmother), typeof(ReplyToTheHighScholar), typeof(AccessToTheIsle),
            typeof(InMemory)
        };

        [Constructible]
        public MinionOfScelestus() : base(AIType.AI_Mage, FightMode.Weakest, 10)
        {
            Body = 9;
            BaseSoundID = 357;
            Hue = 1159;

            SetStr(375, 405);
            SetDex(175, 200);
            SetInt(200, 225);

            SetHits(30000);

            SetDamage(19, 21);

            SetDamageType(ResistanceType.Physical, 20);
            SetDamageType(ResistanceType.Cold, 20);
            SetDamageType(ResistanceType.Poison, 50);
            SetDamageType(ResistanceType.Energy, 10);

            SetResistance(ResistanceType.Physical, 55, 65);
            SetResistance(ResistanceType.Fire, 50);
            SetResistance(ResistanceType.Cold, 100);
            SetResistance(ResistanceType.Poison, 100);
            SetResistance(ResistanceType.Energy, 50);

            SetSkill(SkillName.MagicResist, 130.8, 140.0);
            SetSkill(SkillName.Tactics, 110.0, 120.0);
            SetSkill(SkillName.Wrestling, 110.2, 120.0);
            SetSkill(SkillName.Poisoning, 120.0);
            SetSkill(SkillName.Magery, 115.0, 125.0);
            SetSkill(SkillName.EvalInt, 100.0, 120.0);

            Fame = 12500;
            Karma = -12500;
        }

        public override string CorpseName => "a minion of scelestus corpse";
        public override string DefaultName => "a minion of scelestus";

        public override int TreasureMapLevel => 4;
        public override Poison PoisonImmune => Poison.LethalParasitic;
        public override Poison HitPoison => Poison.Lethal;
        public override bool ReacquireOnMovement => true;
        public override bool AcquireOnApproach => true;
        public override int AcquireOnApproachRange => 12;

        public override void GenerateLoot()
        {
            AddLoot(LootPack.SuperBoss);
            AddLoot(LootPack.UltraRich);
        }

        public override void OnGaveMeleeAttack(Mobile defender, int damage)
        {
            if (0.20 > Utility.RandomDouble() && (defender.Mounted || defender.Flying))
            {
                if (defender is PlayerMobile pm)
                {
                    if (AnimalForm.UnderTransformation(defender))
                    {
                        defender.SendLocalizedMessage(1114066, Name); // ~1_NAME~ knocked you out of animal form!
                    }
                    else if (defender.Mounted)
                    {
                        defender.SendLocalizedMessage(1040023); // You have been knocked off of your mount!
                    }

                    pm.SetMountBlock(BlockMountType.Dazed, TimeSpan.FromSeconds(10), true);
                }
                else if (defender.Mount != null)
                {
                    defender.Mount.Rider = null;
                }

                defender.PlaySound(0x140);
                defender.FixedParticles(0x3728, 10, 15, 9955, EffectLayer.Waist);
            }

            base.OnGaveMeleeAttack(defender, damage);
        }

        public override void OnDeath(Container c)
        {
            base.OnDeath(c);

            if (0.50 > Utility.RandomDouble())
            {
                var item = Loot.Construct(_documentTypes);

                if (item != null)
                {
                    c.DropItem(item);
                }
            }
        }

        public override void OnThink()
        {
            base.OnThink();

            if (this.GetDistanceToSqrt(Home) > MaxWanderDistance &&
                (Combatant == null || 0.01 > Utility.RandomDouble()))
            {
                foreach (var m in GetMobilesInRange(10))
                {
                    if (m.NetState != null)
                    {
                        m.SendMessage("The minion has returned to its home.");
                    }
                }

                FixedParticles(0x376A, 9, 32, 0x13AF, EffectLayer.Waist);
                MoveToWorld(Home, Map);
            }
        }
    }
}
