// ServUO: Mobiles/Bosses/StygianDragon.cs (CC6 batch 8, Part B). The Stygian Dragon, the Abyss's dragon boss, on
// BaseSABoss (the altar-less Peerless base, D-79: a walk-up fight). Body 826 is a bodyTable.cfg row. Its three
// timed attacks - the crimson meteor shower, the stygian fireball and the fire column - port whole, with the
// nested FireField item and its two timers.
//
// What changed, each proved against D:\UO\ModernUO-pinned:
//   SetSpecialAbility(DragonBreath)             MonsterAbilities.FireBreath through GetMonsterAbilities (Q-054, the
//                                               follow-up's shape). A keyword hit, not a loss.
//   SetWeaponAbility(Bladeweave / TalonStrike)  GetWeaponAbility, random of the two (the recipe).
//   DragonBlood => 48                           no property and no item in pinned (the recipe, Q-049); dropped.
//   ScreenLightFlash / Packet / NetState.Send   pinned has no Packet class; NetState.SendScreenEffect(LightFlash)
//                                               (OutgoingEffectPackets.cs:322) over Map.GetClientsInRange.
//   ColUtility.Free(list)                       a ServUO pooling helper; nothing to free.
//   Timer.DelayCall(TimeSpan, TimerStateCallback, object[])  a lambda over the three values.
//   Utility.GetDirection(this, Combatant)       pinned takes two points.
//   DateTime.UtcNow                             Core.Now.
//   FireField.Serialize writes NOTHING in ServUO (its comment: "Unsaved"), so a field caught by a save comes back
//   there as a headless item with no timer, forever. Here the generator saves it whole and it deletes itself on load
//   ([AfterDeserialization]): a bug that makes something broken, fixed (bug-list section 3 / D-4's rule), argued in
//   the note. The field's timer, owner and expiry are not persisted, exactly as ServUO's are not.
// ServUO Abilities/SlayerGroup.cs:503 and :528 list this type under ReptilianDeath and DragonSlaying; pinned's are
// static typeof lists that nothing registers into (D-86).

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Items;
using Server.Movement;
using Server.Network;
using Server.Spells;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class StygianDragon : BaseSABoss
{
    private DateTime _delay;

    // ServUO: SetSpecialAbility(SpecialAbility.DragonBreath) - pinned's FireBreath (Q-054).
    private static readonly MonsterAbility[] _abilities = { MonsterAbilities.FireBreath };
    public override MonsterAbility[] GetMonsterAbilities() => _abilities;

    [Constructible]
    public StygianDragon() : base(AIType.AI_Mage)
    {
        Body = 826;
        BaseSoundID = 362;

        SetStr(702);
        SetDex(250);
        SetInt(180);

        SetHits(30000);
        SetStam(431);
        SetMana(180);

        SetDamage(33, 55);

        SetDamageType(ResistanceType.Physical, 25);
        SetDamageType(ResistanceType.Fire, 50);
        SetDamageType(ResistanceType.Energy, 25);

        SetResistance(ResistanceType.Physical, 80, 90);
        SetResistance(ResistanceType.Fire, 80, 90);
        SetResistance(ResistanceType.Cold, 60, 70);
        SetResistance(ResistanceType.Poison, 80, 90);
        SetResistance(ResistanceType.Energy, 80, 90);

        SetSkill(SkillName.Anatomy, 100.0);
        SetSkill(SkillName.MagicResist, 150.0, 155.0);
        SetSkill(SkillName.Tactics, 120.7, 125.0);
        SetSkill(SkillName.Wrestling, 115.0, 117.7);

        Fame = 15000;
        Karma = -15000;

        VirtualArmor = 60;

        Tamable = false;
    }

    public override string CorpseName => "a stygian dragon corpse";
    public override string DefaultName => "Stygian Dragon";

    public override WeaponAbility GetWeaponAbility() =>
        Utility.RandomBool() ? WeaponAbility.Bladeweave : WeaponAbility.TalonStrike;

    public override Type[] UniqueSAList => new[]
    {
        typeof(BurningAmber), typeof(DraconisWrath), typeof(DragonHideShield), typeof(FallenMysticsSpellbook),
        typeof(LifeSyphon), typeof(GargishSignOfOrder), typeof(HumanSignOfOrder), typeof(VampiricEssence)
    };

    public override Type[] SharedSAList => new[]
    {
        typeof(AxesOfFury), typeof(SummonersKilt), typeof(GiantSteps),
        typeof(TokenOfHolyFavor)
    };

    public override bool AlwaysMurderer => true;
    public override bool Unprovokable => false;
    public override bool BardImmune => false;
    public override bool AutoDispel => !Controlled;
    public override int Meat => 19;
    public override int Hides => 30;
    public override HideType HideType => HideType.Barbed;
    public override int Scales => 7;
    public override ScaleType ScaleType => Body == 12 ? ScaleType.Yellow : ScaleType.Red;
    public override bool CanFlee => false;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.AosSuperBoss, 4);
        AddLoot(LootPack.Gems, 8);
    }

    public override void OnThink()
    {
        base.OnThink();

        if (Combatant == null)
        {
            return;
        }

        if (Core.Now > _delay)
        {
            switch (Utility.Random(3))
            {
                case 0: CrimsonMeteor(this, Combatant, 70, 125); break;
                case 1: DoStygianFireball(); break;
                case 2: DoFireColumn(); break;
            }

            _delay = Core.Now + TimeSpan.FromSeconds(Utility.RandomMinMax(30, 60));
        }
    }

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        c.DropItem(new StygianDragonHead());

        if (Paragon.ChestChance > Utility.RandomDouble())
        {
            c.DropItem(new ParagonChest(Name, 5));
        }
    }

    #region Crimson Meteor

    public static void CrimsonMeteor(Mobile owner, Mobile combatant, int minDamage, int maxDamage)
    {
        if (!combatant.Alive || combatant.Map == null || combatant.Map == Map.Internal)
        {
            return;
        }

        new CrimsonMeteorTimer(owner, combatant.Location, minDamage, maxDamage).Start();
    }

    public class CrimsonMeteorTimer : Timer
    {
        private readonly Mobile _from;
        private readonly Map _map;
        private int _count;
        private readonly int _maxCount;
        private bool _doneDamage;
        private Point3D _lastTarget;
        private readonly Rectangle2D _showerArea;
        private readonly List<Mobile> _toDamage;

        private readonly int _minDamage, _maxDamage;

        public CrimsonMeteorTimer(Mobile from, Point3D loc, int min, int max)
            : base(TimeSpan.FromMilliseconds(250.0), TimeSpan.FromMilliseconds(250.0))
        {
            _from = from;
            _map = from.Map;
            _count = 0;
            _maxCount = 25; // in ticks
            _lastTarget = loc;
            _doneDamage = false;
            _showerArea = new Rectangle2D(loc.X - 2, loc.Y - 2, 4, 4);

            _minDamage = min;
            _maxDamage = max;

            _toDamage = new List<Mobile>();

            foreach (var m in _map.GetMobilesInBounds(_showerArea))
            {
                if (m != from && _from.CanBeHarmful(m))
                {
                    _toDamage.Add(m);
                }
            }
        }

        protected override void OnTick()
        {
            if (_from == null || _from.Deleted || _map == null || _map == Map.Internal)
            {
                Stop();
                return;
            }

            if (0.33 > Utility.RandomDouble())
            {
                var field = new FireField(_from, 25, Utility.RandomBool());
                field.MoveToWorld(_lastTarget, _map);
            }

            var start = new Point3D();
            var finish = new Point3D();

            finish.X = _showerArea.X + Utility.Random(_showerArea.Width);
            finish.Y = _showerArea.Y + Utility.Random(_showerArea.Height);
            finish.Z = _from.Z;

            SpellHelper.AdjustField(ref finish, _map, 16, false);

            // objects move from upper right/right to left as per OSI
            start.X = finish.X + Utility.RandomMinMax(-4, 4);
            start.Y = finish.Y - 15;
            start.Z = finish.Z + 50;

            Effects.SendMovingParticles(
                new Entity(Serial.Zero, start, _map),
                new Entity(Serial.Zero, finish, _map),
                0x36D4, 15, 0, false, false, 0, 0, 9502, 1, 0, (EffectLayer)255, 0x100
            );

            Effects.PlaySound(finish, _map, 0x11D);

            _lastTarget = finish;
            _count++;

            if (_count >= _maxCount / 2 && !_doneDamage)
            {
                if (_toDamage != null && _toDamage.Count > 0)
                {
                    foreach (var mob in _toDamage)
                    {
                        var damage = Utility.RandomMinMax(_minDamage, _maxDamage);

                        _from.DoHarmful(mob);
                        AOS.Damage(mob, _from, damage, 0, 100, 0, 0, 0);

                        mob.FixedParticles(0x36BD, 1, 15, 9502, 0, 3, (EffectLayer)255);
                    }
                }

                _doneDamage = true;
                return;
            }

            if (_count >= _maxCount)
            {
                Stop();
            }
        }
    }

    #endregion

    #region Fire Column

    public void DoFireColumn()
    {
        var map = Map;

        if (map == null)
        {
            return;
        }

        var columnDir = Utility.GetDirection(Location, Combatant.Location);

        // ServUO: ScreenLightFlash.Instance sent to every client in Core.GlobalUpdateRange.
        foreach (var ns in map.GetClientsInRange(Location, Core.GlobalUpdateRange))
        {
            if (ns.Mobile != null)
            {
                ns.SendScreenEffect(ScreenEffectType.LightFlash);
            }
        }

        var x = X;
        var y = Y;
        var south = columnDir == Direction.East || columnDir == Direction.West;

        Movement.Movement.Offset(columnDir, ref x, ref y);
        var p = new Point3D(x, y, Z);
        SpellHelper.AdjustField(ref p, map, 16, false);

        var fire = new FireField(this, Utility.RandomMinMax(25, 32), south);
        fire.MoveToWorld(p, map);

        for (var i = 0; i < 7; i++)
        {
            Movement.Movement.Offset(columnDir, ref x, ref y);

            p = new Point3D(x, y, Z);
            SpellHelper.AdjustField(ref p, map, 16, false);

            fire = new FireField(this, Utility.RandomMinMax(25, 32), south);
            fire.MoveToWorld(p, map);
        }
    }

    #endregion

    #region Fire Field

    [SerializationGenerator(0, false)]
    public partial class FireField : Item
    {
        private readonly Mobile _owner;
        private readonly Timer _timer;
        private readonly DateTime _destroy;

        [Constructible]
        public FireField(Mobile owner, int duration, bool south) : base(GetItemID(south))
        {
            Movable = false;
            _destroy = Core.Now + TimeSpan.FromSeconds(duration);

            _owner = owner;
            _timer = Timer.DelayCall(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), OnTick);
        }

        private static int GetItemID(bool south) => south ? 0x398C : 0x3996;

        // ServUO saves nothing for this item and reads nothing back, leaving a headless item in the save. Here a
        // field that was caught by a save deletes itself on load: its timer, owner and expiry were never persisted.
        [AfterDeserialization(false)]
        private void AfterDeserialization()
        {
            Delete();
        }

        public override void OnAfterDelete()
        {
            _timer?.Stop();
        }

        private void OnTick()
        {
            if (Core.Now > _destroy)
            {
                Delete();
            }
            else
            {
                var list = new List<Mobile>();

                foreach (var m in GetMobilesInRange(0))
                {
                    if (m == null)
                    {
                        continue;
                    }

                    if (_owner == null || CanTargetMob(m))
                    {
                        list.Add(m);
                    }
                }

                foreach (var mob in list)
                {
                    DealDamage(mob);
                }
            }
        }

        public override bool OnMoveOver(Mobile m)
        {
            DealDamage(m);

            return true;
        }

        public void DealDamage(Mobile m)
        {
            if (m != _owner && (_owner == null || CanTargetMob(m)))
            {
                AOS.Damage(m, _owner, Utility.RandomMinMax(2, 4), 0, 100, 0, 0, 0);
            }
        }

        public bool CanTargetMob(Mobile m) =>
            m != _owner && _owner.CanBeHarmful(m, false) &&
            (m is PlayerMobile || m is BaseCreature bc && bc.GetMaster() is PlayerMobile);
    }

    #endregion

    #region Stygian Fireball

    public void DoStygianFireball()
    {
        if (Combatant == null || !InRange(Combatant.Location, 10))
        {
            return;
        }

        new StygianFireballTimer(this, Combatant);
        PlaySound(0x1F3);
    }

    private class StygianFireballTimer : Timer
    {
        private readonly StygianDragon _dragon;
        private readonly Mobile _combatant;
        private int _ticks;

        public StygianFireballTimer(StygianDragon dragon, Mobile combatant)
            : base(TimeSpan.FromMilliseconds(200), TimeSpan.FromMilliseconds(200))
        {
            _dragon = dragon;
            _combatant = combatant;
            _ticks = 0;
            Start();
        }

        protected override void OnTick()
        {
            _dragon.MovingParticles(_combatant, 0x46E6, 7, 0, false, true, 1265, 0, 9502, 4019, 0x026, 0);

            if (_ticks >= 10)
            {
                var damage = Utility.RandomMinMax(120, 150);
                var c = _combatant;
                var d = _dragon;

                Timer.DelayCall(TimeSpan.FromSeconds(.20), () =>
                {
                    d.DoHarmful(c);
                    AOS.Damage(c, d, damage, false, 0, 0, 0, 0, 0, 100, 0, false);
                });

                Stop();
            }

            _ticks++;
        }
    }

    #endregion
}
