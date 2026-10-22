// ServUO: Services/Expansions/Time Of Legends/Mobiles/Infernus.cs (cc-P198). Values verbatim; serialization by the
// generator.
//
// OnActionCombat drops a trail of fire round its combatant every 10-15 seconds (combatant within 12 tiles): the
// centre tile always, each of the eight around it at 50%, where a mobile could stand. A FireItem lasts 30-40
// seconds, and anyone it may harm who walks onto it, or stands on it at a one-second tick, takes 50-85 fire damage
// from the Infernus.
//
// ServUO's FireItem writes nothing in Serialize (not even its base), so a world save mid-fight would not load it
// back. Here it serializes its base through the generator and deletes itself after load: the same outcome (the
// fire does not survive a restart) without a broken record. Its owner and end time are not saved.

using System;
using ModernUO.Serialization;
using Server.Collections;
using Server.Network;
using Server.Spells;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class Infernus : BaseCreature
{
    private DateTime _nextDrop;

    [Constructible]
    public Infernus() : base(AIType.AI_Melee)
    {
        Body = 0x9F;
        Hue = 1955;
        BaseSoundID = 278;

        SetStr(318, 400);
        SetDex(97, 115);
        SetInt(184, 266);

        SetDamage(11, 14);

        SetHits(202, 243);

        SetResistance(ResistanceType.Physical, 45, 55);
        SetResistance(ResistanceType.Fire, 80, 90);
        SetResistance(ResistanceType.Cold, 25, 35);
        SetResistance(ResistanceType.Poison, 40, 50);
        SetResistance(ResistanceType.Energy, 35, 45);

        SetDamageType(ResistanceType.Fire, 100);

        SetSkill(SkillName.MagicResist, 90, 100);
        SetSkill(SkillName.Tactics, 60, 70);
        SetSkill(SkillName.Wrestling, 80);

        PackGold(500, 600);

        Fame = 10000;
        Karma = -10000;
    }

    public override string CorpseName => "an infernus corpse";
    public override string DefaultName => "an infernus";

    public override int TreasureMapLevel => 5;

    public override void OnActionCombat()
    {
        var combatant = Combatant;

        if (Core.Now < _nextDrop || combatant == null || combatant.Deleted || combatant.Map != Map ||
            !InRange(combatant, 12))
        {
            return;
        }

        DropFire(combatant);

        _nextDrop = Core.Now + TimeSpan.FromSeconds(Utility.RandomMinMax(10, 15));
    }

    public void DropFire(Mobile m)
    {
        for (var x = m.X - 1; x <= m.X + 1; x++)
        {
            for (var y = m.Y - 1; y <= m.Y + 1; y++)
            {
                IPoint3D p = new Point3D(x, y, Map.GetAverageZ(x, y));
                SpellHelper.GetSurfaceTop(ref p);

                // ServUO's (x == 0 && y == 0) compares world coordinates, so the centre is not guaranteed: kept.
                if ((x == 0 && y == 0 || 0.5 > Utility.RandomDouble()) && Map.CanSpawnMobile(p.X, p.Y, p.Z))
                {
                    var item = new FireItem(this);
                    item.MoveToWorld(new Point3D(p), Map);
                }
            }
        }
    }

    [SerializationGenerator(0, false)]
    public partial class FireItem : Item
    {
        private DateTime _endTime;
        private TimerExecutionToken _timerToken;

        public FireItem(Infernus mobile) : base(0x19AB)
        {
            Movable = false;
            Owner = mobile;

            _endTime = Core.Now + TimeSpan.FromSeconds(Utility.RandomMinMax(30, 40));

            Timer.StartTimer(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), OnTick, out _timerToken);
        }

        public Infernus Owner { get; private set; }

        public override bool OnMoveOver(Mobile from)
        {
            if (Owner != null && Owner != from && SpellHelper.ValidIndirectTarget(Owner, from) &&
                Owner.CanBeHarmful(from, false))
            {
                Owner.DoHarmful(from);

                AOS.Damage(from, Owner, Utility.RandomMinMax(50, 85), 0, 100, 0, 0, 0);
                Effects.PlaySound(Location, Map, 0x1DD);
                // *The trail of fire scorches you, setting you ablaze!*
                from.PrivateOverheadMessage(MessageType.Regular, 0x20, 1156084, from.NetState);
            }

            return true;
        }

        private void OnTick()
        {
            if (_endTime < Core.Now)
            {
                Delete();
                return;
            }

            if (Map == null || Map == Map.Internal)
            {
                return;
            }

            using var queue = PooledRefQueue<Mobile>.Create();

            foreach (var m in Map.GetMobilesAt(Location))
            {
                queue.Enqueue(m);
            }

            while (queue.Count > 0)
            {
                OnMoveOver(queue.Dequeue());
            }
        }

        public override void OnAfterDelete()
        {
            base.OnAfterDelete();
            _timerToken.Cancel();
        }

        [AfterDeserialization(false)]
        private void AfterDeserialization() => Delete();
    }
}
