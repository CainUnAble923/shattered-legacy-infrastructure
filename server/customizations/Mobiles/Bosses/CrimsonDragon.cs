// ServUO: Mobiles/Bosses/CrimsonDragon.cs (cc-P129). The Blighted Grove peerless, on our BasePeerless (D-79,
// altar-less). Values verbatim; serialization by the generator; m_NextTerror is not persisted in ServUO either.
//
// SetSpecialAbility(DragonBreath): no stock breath, so the profile's pinned twin fires the default (fire) definition.
// Pinned's OnDamagedBySpell and OnGotMeleeAttack take the damage as a second argument (cc-P119's GargoyleGuardian);
// IsPlayer() is Player; String.Format is an interpolated string; the GoodiesTimer is the same class pinned's
// BaseChampion carries (Map.CanFit, Gold(min, max), EffectItem.Create). The ParrotItem is ported beside this file;
// the Crimson Cincture is pinned's.

using System;
using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class CrimsonDragon : BasePeerless
{
    public override bool GiveMLSpecial => false;

    private DateTime _nextTerror;

    [Constructible]
    public CrimsonDragon() : base(AIType.AI_Mage)
    {
        Body = 197;

        BaseSoundID = 362;
        SetStr(2034, 2140);
        SetDex(215, 256);
        SetInt(1025, 1116);

        SetHits(25000);

        SetDamage(8, 10);

        SetDamageType(ResistanceType.Physical, 50);
        SetDamageType(ResistanceType.Fire, 50);

        SetResistance(ResistanceType.Physical, 80, 85);
        SetResistance(ResistanceType.Fire, 100);
        SetResistance(ResistanceType.Cold, 50, 60);
        SetResistance(ResistanceType.Poison, 80, 85);
        SetResistance(ResistanceType.Energy, 80, 85);

        SetSkill(SkillName.EvalInt, 110.2, 125.3);
        SetSkill(SkillName.Magery, 110.9, 125.5);
        SetSkill(SkillName.MagicResist, 116.3, 125.0);
        SetSkill(SkillName.Tactics, 111.7, 126.3);
        SetSkill(SkillName.Wrestling, 120.5, 128.0);
        SetSkill(SkillName.Meditation, 119.4, 130.0);
        SetSkill(SkillName.Anatomy, 118.7, 125.0);
        SetSkill(SkillName.DetectHidden, 120.0);

        // ingredients
        PackResources(8);

        Fame = 20000;
        Karma = -20000;

        VirtualArmor = 70;

        SetSpecialAbility(SpecialAbility.DragonBreath);
    }

    // Body 197 is on pinned's invalid-body list (Server/Mobiles/Mobile.cs:232-239), so SafeBody would turn it into 0.
    // ServUO comments 197 out of the same list, "ML Dragon" (Server/Mobile.cs:9728). The faithful port of that core
    // change, scoped to this one type, as cc-P119's Turkey does for 95.
    public override int SafeBody(int body) => body == 197 ? body : base.SafeBody(body);

    public override string CorpseName => "a crimson dragon corpse";
    public override string DefaultName => "a crimson dragon";

    public override bool AlwaysMurderer => true;
    public override bool Unprovokable => true;
    public override bool BardImmune => true;
    public override bool ReacquireOnMovement => true;
    public override bool AutoDispel => true;
    public override bool Uncalmable => true;
    public override int Meat => 19;
    public override int Hides => 40;
    public override HideType HideType => HideType.Barbed;
    public override int Scales => 12;
    public override ScaleType ScaleType => (ScaleType)Utility.Random(4);
    public override FoodType FavoriteFood => FoodType.Meat;
    public override Poison PoisonImmune => Poison.Lethal;
    public override Poison HitPoison => Utility.RandomBool() ? Poison.Deadly : Poison.Lethal;
    public override int TreasureMapLevel => 5;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.AosSuperBoss, 8);
        AddLoot(LootPack.Gems, 12);
    }

    public override int GetIdleSound() => 0x2D3;
    public override int GetHurtSound() => 0x2D1;

    public override void OnDamagedBySpell(Mobile caster, int damage)
    {
        if (Map != null && caster != this && 0.50 > Utility.RandomDouble())
        {
            Map = caster.Map;
            Location = caster.Location;
            Combatant = caster;
            Effects.PlaySound(Location, Map, 0x1FE);
        }

        base.OnDamagedBySpell(caster, damage);
    }

    public override void OnMovement(Mobile m, Point3D oldLocation)
    {
        base.OnMovement(m, oldLocation);

        if (_nextTerror < Core.Now && m != null && InRange(m.Location, 3) && m.Player)
        {
            m.Frozen = true;
            // Terror slices into your very being, destroying any chance of resisting ~1_name~ you might have had
            m.SendLocalizedMessage(1080342, Name, 33);

            Timer.DelayCall(TimeSpan.FromSeconds(5), Terrorize, m);
        }
    }

    public override void OnGotMeleeAttack(Mobile attacker, int damage)
    {
        if (Map != null && attacker != this && 0.1 > Utility.RandomDouble())
        {
            if (attacker is BaseCreature pet)
            {
                if (pet.ControlMaster != null && attacker is Dragon or GreaterDragon or SkeletalDragon or WhiteWyrm or Drake)
                {
                    Combatant = null;
                    pet.Combatant = null;
                    Combatant = null;
                    pet.ControlMaster = null;
                    pet.Controlled = false;
                    attacker.Emote($"* {attacker.Name} decided to go wild *");
                }

                if (pet.ControlMaster != null && 0.1 > Utility.RandomDouble())
                {
                    Combatant = null;
                    pet.Combatant = pet.ControlMaster;
                    Combatant = null;
                    attacker.Emote($"* {attacker.Name} is being angered *");
                }
            }
        }

        base.OnGotMeleeAttack(attacker, damage);
    }

    public override bool OnBeforeDeath()
    {
        Hue = 16385;

        if (!NoKillAwards)
        {
            var map = Map;

            if (map != null)
            {
                for (var x = -7; x <= 7; ++x)
                {
                    for (var y = -7; y <= 3; ++y)
                    {
                        var dist = Math.Sqrt(x * x + y * y);

                        if (dist <= 12)
                        {
                            new GoodiesTimer(map, X + x, Y + y).Start();
                        }
                    }
                }
            }
        }

        return base.OnBeforeDeath();
    }

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        if (Utility.RandomDouble() < 0.6)
        {
            c.DropItem(new ParrotItem());
        }

        if (Utility.RandomDouble() < 0.025)
        {
            c.DropItem(new CrimsonCincture());
        }
    }

    private void Terrorize(Mobile m)
    {
        m.Frozen = false;
        m.SendLocalizedMessage(1005603); // You can move again!

        _nextTerror = Core.Now + TimeSpan.FromMinutes(5);
    }

    private class GoodiesTimer : Timer
    {
        private readonly Map _map;
        private readonly int _x;
        private readonly int _y;

        public GoodiesTimer(Map map, int x, int y) : base(TimeSpan.FromSeconds(Utility.RandomDouble() * 10.0))
        {
            _map = map;
            _x = x;
            _y = y;
        }

        protected override void OnTick()
        {
            var z = _map.GetAverageZ(_x, _y);
            var canFit = _map.CanFit(_x, _y, z, 6, false, false);

            for (var i = -3; !canFit && i <= 3; ++i)
            {
                canFit = _map.CanFit(_x, _y, z + i, 6, false, false);

                if (canFit)
                {
                    z += i;
                }
            }

            if (!canFit)
            {
                return;
            }

            var g = new Gold(300, 500);

            g.MoveToWorld(new Point3D(_x, _y, z), _map);

            if (0.5 >= Utility.RandomDouble())
            {
                switch (Utility.Random(3))
                {
                    case 0: // Fire column
                        {
                            Effects.SendLocationParticles(EffectItem.Create(g.Location, g.Map, EffectItem.DefaultDuration), 0x3709, 10, 30, 5052);
                            Effects.PlaySound(g.Location, g.Map, 0x208);

                            break;
                        }
                    case 1: // Explosion
                        {
                            Effects.SendLocationParticles(EffectItem.Create(g.Location, g.Map, EffectItem.DefaultDuration), 0x36BD, 20, 10, 5044);
                            Effects.PlaySound(g.Location, g.Map, 0x307);

                            break;
                        }
                    case 2: // Ball of fire
                        {
                            Effects.SendLocationParticles(EffectItem.Create(g.Location, g.Map, EffectItem.DefaultDuration), 0x36FE, 10, 10, 5052);

                            break;
                        }
                }
            }
        }
    }
}
