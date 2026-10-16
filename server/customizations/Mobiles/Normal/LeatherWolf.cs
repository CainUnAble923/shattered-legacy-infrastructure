// ServUO: Mobiles/Normal/LeatherWolf.cs (cc-P129): LeatherWolf and LeatherWolfFellow, one file as in ServUO. Values
// verbatim; serialization by the generator. SetWeaponAbility(BleedAttack) is GetWeaponAbility.
//
// DEVIATION, small: ServUO's LeatherWolf is an IRepairableMobile (RepairResource = IronIngot), so a tinker can repair
// it as a golem. Pinned has no such interface: its Repair targets Golem by type (Engines/Craft/Core/Repair.cs:169) and
// its bandages likewise. So the leather wolf cannot be repaired with ingots here. Reverses when pinned's Repair gains
// an interface or a type list that ours can join. Reaches this creature only among the ports.
// Map.GetSpawnPosition(Location, 6) is pinned's Map.GetRandomNearbyLocation(Location, 6) (same job: a spawnable
// point within the range, the origin when none is found).
// The fellows and their timer are not persisted in ServUO either (its Serialize writes only the version); a wolf
// loaded from a save howls for a fresh pack on its next fight, as there.

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class LeatherWolf : BaseCreature
{
    private const int MaxFellows = 3;

    private readonly List<Mobile> _fellows = new();
    private Timer _fellowsTimer;

    [Constructible]
    public LeatherWolf() : base(AIType.AI_Melee, FightMode.Aggressor)
    {
        Body = 739;
        BaseSoundID = 0xE5;

        SetStr(104, 125);
        SetDex(102, 125);
        SetInt(20, 34);

        SetHits(291, 329);

        SetDamage(12, 23);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 40, 49);
        SetResistance(ResistanceType.Fire, 20, 29);
        SetResistance(ResistanceType.Cold, 30, 40);
        SetResistance(ResistanceType.Poison, 21, 29);
        SetResistance(ResistanceType.Energy, 20, 25);

        SetSkill(SkillName.MagicResist, 79.5, 94.9);
        SetSkill(SkillName.Tactics, 80.6, 89.4);
        SetSkill(SkillName.Wrestling, 70.9, 88.4);

        Fame = 4500;
        Karma = -4500;

        Tamable = false;
    }

    public override string CorpseName => "a wolf corpse";
    public override string DefaultName => "a leather wolf";

    public override WeaponAbility GetWeaponAbility() => WeaponAbility.BleedAttack;

    public override void OnDeath(Container c)
    {
        base.OnDeath(c);

        if (!Controlled && 0.2 > Utility.RandomDouble())
        {
            c.DropItem(new LeatherWolfSkin());
        }
    }

    public override void OnCombatantChange()
    {
        if (Combatant != null && _fellowsTimer == null)
        {
            _fellowsTimer = new InternalTimer(this);
            _fellowsTimer.Start();
        }
    }

    public void CheckFellows()
    {
        if (!Alive || Combatant == null || Map == null || Map == Map.Internal)
        {
            _fellows.ForEach(f => f.Delete());
            _fellows.Clear();

            _fellowsTimer?.Stop();
            _fellowsTimer = null;
        }
        else
        {
            for (var i = _fellows.Count - 1; i >= 0; i--)
            {
                if (_fellows[i].Deleted)
                {
                    _fellows.RemoveAt(i);
                }
            }

            var spawned = false;

            for (var i = _fellows.Count; i < MaxFellows; i++)
            {
                BaseCreature friend = new LeatherWolfFellow();

                friend.MoveToWorld(Map.GetRandomNearbyLocation(Location, 6), Map);
                friend.Combatant = Combatant;

                if (friend.AIObject != null)
                {
                    friend.AIObject.Action = ActionType.Combat;
                }

                _fellows.Add(friend);

                spawned = true;
            }

            if (spawned)
            {
                Say(1113132); // The leather wolf howls for help
                PlaySound(0xE6);
            }
        }
    }

    public List<Mobile> Fellows => _fellows;

    private class InternalTimer : Timer
    {
        private readonly LeatherWolf _owner;

        public InternalTimer(LeatherWolf owner) : base(TimeSpan.Zero, TimeSpan.FromSeconds(30.0))
        {
            _owner = owner;
        }

        protected override void OnTick()
        {
            _owner.CheckFellows();
        }
    }

    public override bool AlwaysMurderer => true;

    public override int Meat => 1;
    public override PackInstinct PackInstinct => PackInstinct.Canine;
    public override int Hides => 7;
    public override FoodType FavoriteFood => FoodType.Meat;

    public override void GenerateLoot()
    {
        AddLoot(LootPack.Meager, 2);
    }

    public override int GetIdleSound() => 1545;
    public override int GetAngerSound() => 1542;
    public override int GetHurtSound() => 1544;
    public override int GetDeathSound() => 1543;
}

[SerializationGenerator(0, false)]
public partial class LeatherWolfFellow : BaseCreature
{
    [Constructible]
    public LeatherWolfFellow() : base(AIType.AI_Melee)
    {
        Body = 739;
        BaseSoundID = 0xE5;

        SetStr(105, 115);
        SetDex(101, 114);
        SetInt(23, 34);

        SetHits(81, 110);

        SetDamage(9, 20);

        SetDamageType(ResistanceType.Physical, 100);

        SetResistance(ResistanceType.Physical, 36, 50);
        SetResistance(ResistanceType.Fire, 10, 18);
        SetResistance(ResistanceType.Cold, 23, 29);
        SetResistance(ResistanceType.Poison, 10, 17);
        SetResistance(ResistanceType.Energy, 10, 15);

        SetSkill(SkillName.MagicResist, 59.2, 75);
        SetSkill(SkillName.Tactics, 53.3, 64.8);
        SetSkill(SkillName.Wrestling, 64, 79);

        Fame = 2500;
        Karma = -2500;
    }

    public override string DefaultName => "a leather wolf";

    public override PackInstinct PackInstinct => PackInstinct.Canine;
}
