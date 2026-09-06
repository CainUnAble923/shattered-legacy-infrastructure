using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles
{
    [SerializationGenerator(0, false)]
    public partial class Raptor : BaseCreature
    {
        private const int MaxFriends = 2;

        [SerializableField(0, setter: "private")]
        private bool _isFriend;

        private readonly List<Mobile> _friends = new();
        private FriendsTimer _friendsTimer;

        [Constructible]
        public Raptor(bool isFriend = false) : base(AIType.AI_Melee)
        {
            _isFriend = isFriend;

            Body = 730;

            SetStr(404, 471);
            SetDex(132, 155);
            SetInt(105, 145);

            SetHits(343, 400);

            SetDamage(11, 17);

            SetDamageType(ResistanceType.Physical, 100);

            SetResistance(ResistanceType.Physical, 45, 50);
            SetResistance(ResistanceType.Fire, 50, 60);
            SetResistance(ResistanceType.Cold, 40, 50);
            SetResistance(ResistanceType.Poison, 20, 30);
            SetResistance(ResistanceType.Energy, 30, 40);

            SetSkill(SkillName.MagicResist, 75.1, 90.0);
            SetSkill(SkillName.Tactics, 75.1, 100.0);
            SetSkill(SkillName.Wrestling, 70.1, 95.1);

            Fame = 7500;
            Karma = -7500;

            Tamable = !isFriend;
            MinTameSkill = 107.1;
            ControlSlots = 2;
        }

        public override string CorpseName => "a raptor corpse";
        public override string DefaultName => "a raptor";

        public override int TreasureMapLevel => 3;
        public override int Meat => 7;
        public override int Hides => 11;
        public override HideType HideType => HideType.Horned;
        public override PackInstinct PackInstinct => PackInstinct.Ostard;

        // ServUO: SetWeaponAbility(WeaponAbility.BleedAttack).
        public override WeaponAbility GetWeaponAbility() => WeaponAbility.BleedAttack;

        public override void GenerateLoot()
        {
            AddLoot(LootPack.Rich, 2);
        }

        public override int GetIdleSound() => 1573;
        public override int GetAngerSound() => 1570;
        public override int GetHurtSound() => 1572;
        public override int GetDeathSound() => 1571;

        public override void OnCombatantChange()
        {
            if (!_isFriend && !Controlled && Combatant != null && _friendsTimer == null)
            {
                _friendsTimer = new FriendsTimer(this);
                _friendsTimer.Start();
            }
        }

        public void CheckFriends()
        {
            if (!Alive || Combatant == null || Controlled || Map == null || Map == Map.Internal)
            {
                foreach (var f in _friends)
                {
                    f.Delete();
                }

                _friends.Clear();

                _friendsTimer.Stop();
                _friendsTimer = null;
                return;
            }

            var count = 0;

            for (var i = _friends.Count - 1; i >= 0; i--)
            {
                var friend = _friends[i];

                if (friend?.Deleted != false)
                {
                    _friends.RemoveAt(i);
                }
                else
                {
                    count++;
                }
            }

            for (var i = count; i < MaxFriends; i++)
            {
                BaseCreature friend = new Raptor(true);

                var loc = Location;
                var validLocation = false;

                for (var j = 0; !validLocation && j < 10; ++j)
                {
                    var x = X + Utility.Random(3) - 1;
                    var y = Y + Utility.Random(3) - 1;
                    var z = Map.GetAverageZ(x, y);

                    if (validLocation = Map.CanFit(x, y, Z, 16, false, false))
                    {
                        loc = new Point3D(x, y, Z);
                    }
                    else if (validLocation = Map.CanFit(x, y, z, 16, false, false))
                    {
                        loc = new Point3D(x, y, z);
                    }
                }

                friend.MoveToWorld(loc, Map);
                friend.Combatant = Combatant;

                if (friend.AIObject != null)
                {
                    friend.AIObject.Action = ActionType.Combat;
                }

                _friends.Add(friend);
            }
        }

        public override void OnDeath(Container c)
        {
            base.OnDeath(c);

            if (!Controlled && Utility.RandomDouble() < 0.25)
            {
                c.DropItem(new AncientPotteryFragments());
            }

            if (!Controlled && Utility.RandomDouble() <= 0.005)
            {
                c.DropItem(new RaptorClaw());
            }
        }

        // ServUO deletes summoned pack friends on deserialize so they do not persist across a
        // restart; only the parent raptor respawns them.
        [AfterDeserialization(false)]
        private void AfterDeserialization()
        {
            if (_isFriend)
            {
                Delete();
            }
        }

        private class FriendsTimer : Timer
        {
            private readonly Raptor _owner;

            public FriendsTimer(Raptor owner) : base(TimeSpan.Zero, TimeSpan.FromSeconds(30.0)) =>
                _owner = owner;

            protected override void OnTick()
            {
                _owner.CheckFriends();
            }
        }
    }
}
