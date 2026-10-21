// ServUO: Services/Underworld/PuzzleRoom/MazePuzzleItem.cs (cc-P197). The maze board (1113379, hue 914, blessed,
// 600 seconds) the west and east puzzle boxes hand a player with a magic key: double-clicked in the pack inside the
// Puzzle Room (1089,1162 16x12, Ter Mur) it opens the shard's six-by-six circuit puzzle (Gumps/Traps/CircuitTrap.cs,
// cc-P199, ServUO's ICircuitTrap: title 1153747, 1153749, decipherable). A wrong pick shocks the player: 75 energy
// plus 1-9 on the first tick and 75 / (tick - 1) after, on a timer that spaces the shocks 2, 4 then 6 seconds apart
// and stops at the tenth tick, with the lightning particles and 1114443. Solved, it hands a copper key, or a gold
// key when the pack already has the copper one (1113382), and deletes itself three seconds later. Saved: the key.
// ServUO's BaseGump.SendGump is pinned's SendGump on the player (the training kit's shape); ServUO's two CloseGump
// calls on the Khaldun chest gumps cannot name pinned's private nested classes (MastermindPuzzleItem's header).

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class MazePuzzleItem : BaseDecayingItem, ICircuitTrap
{
    private static readonly Rectangle2D _bounds = new(1089, 1162, 16, 12);

    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private MagicKey _key;

    private Timer _damageTimer;

    [Constructible]
    public MazePuzzleItem(MagicKey key) : base(0x2AAA)
    {
        Hue = 914;
        _key = key;
    }

    public List<int> Path { get; set; }
    public List<int> Progress { get; set; }
    public CircuitCount Count => CircuitCount.ThirtySix;
    public int GumpTitle => 1153747;       // <center>GENERATOR CONTROL PANEL</center>
    public int GumpDescription => 1153749; // <center>Close the Grid Circuit</center>
    public bool CanDecipher => true;

    public override int LabelNumber => 1113379; // Puzzle Board
    public override int Lifespan => 600;

    public override void OnDoubleClick(Mobile from)
    {
        if (!IsChildOf(from.Backpack))
        {
            from.SendLocalizedMessage(500325); // I am too far away to do that.
        }
        else if (from is PlayerMobile pm && IsInPuzzleRoom(from))
        {
            pm.SendGump(new CircuitTrapGump(pm, this));
        }
    }

    public static bool IsInPuzzleRoom(Mobile from) =>
        from.Map == Map.TerMur && _bounds.Contains(new Point2D(from.X, from.Y));

    public override void OnDelete()
    {
        if (RootParent is Mobile m && m.HasGump<CircuitTrapGump>())
        {
            m.CloseGump<CircuitTrapGump>();
        }

        base.OnDelete();
    }

    public void OnSelfClose(Mobile m)
    {
    }

    public void OnProgress(Mobile m, int pick)
    {
        m.PlaySound(0x1F5);
    }

    public void OnFailed(Mobile m)
    {
        DoDamage(m);
    }

    public void OnComplete(Mobile m)
    {
        m.PlaySound(0x3D);
        OnPuzzleCompleted(m);
    }

    public void DoDamage(Mobile m)
    {
        if (_damageTimer?.Running == true)
        {
            _damageTimer.Stop();
        }

        _damageTimer = new InternalTimer(this, m);
        _damageTimer.Start();
    }

    public void ApplyShock(Mobile m, int tick)
    {
        if (m == null || !m.Alive || Deleted)
        {
            _damageTimer?.Stop();
        }
        else
        {
            var damage = 75 / Math.Max(1, tick - 1) + Utility.RandomMinMax(1, 9);

            AOS.Damage(m, damage, 0, 0, 0, 0, 100);

            m.BoltEffect(0);

            m.FixedParticles(0x3818, 1, 11, 0x13A8, 0, 0, EffectLayer.CenterFeet);
            m.FixedParticles(0x3818, 1, 11, 0x13A8, 0, 0, EffectLayer.Waist);
            m.FixedParticles(0x3818, 1, 11, 0x13A8, 0, 0, EffectLayer.Head);
            m.PlaySound(0x1DC);

            m.LocalOverheadMessage(MessageType.Regular, 0x21, 1114443);           // * Your body convulses from electric shock *
            m.NonlocalOverheadMessage(MessageType.Regular, 0x21, 1114443, m.Name); // * ~1_NAME~ spasms from electric shock *
        }
    }

    private class InternalTimer : Timer
    {
        private readonly MazePuzzleItem _item;
        private readonly Mobile _from;
        private DateTime _nextDamage;
        private int _tick;

        public InternalTimer(MazePuzzleItem item, Mobile from) : base(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1))
        {
            _item = item;
            _from = from;
            _nextDamage = Core.Now;
            _tick = 0;

            item?.ApplyShock(from, 0);
        }

        protected override void OnTick()
        {
            _tick++;

            if (_from == null || _item == null || !_from.Alive || _item.Deleted)
            {
                Stop();
                return;
            }

            if (Core.Now > _nextDamage)
            {
                _item.ApplyShock(_from, _tick);

                int delay;

                if (_tick < 3)
                {
                    delay = 2;
                }
                else if (_tick < 5)
                {
                    delay = 4;
                }
                else
                {
                    delay = 6;
                }

                if (_tick >= 10)
                {
                    Stop();
                }
                else
                {
                    _nextDamage = Core.Now + TimeSpan.FromSeconds(delay);
                }
            }
        }
    }

    public void OnPuzzleCompleted(Mobile m)
    {
        if (m != null)
        {
            var pack = m.Backpack;

            if (pack != null)
            {
                var copperKey = pack.FindItemByType<CopperPuzzleKey>();
                var goldKey = pack.FindItemByType<GoldPuzzleKey>();

                if (copperKey == null)
                {
                    pack.DropItem(new CopperPuzzleKey());
                }
                else if (goldKey == null)
                {
                    pack.DropItem(new GoldPuzzleKey());
                }
                else
                {
                    return;
                }

                m.SendLocalizedMessage(1113382); // You've solved the puzzle!! An item has been placed in your bag.
            }
        }

        Timer.DelayCall(TimeSpan.FromSeconds(3), Delete);
    }
}
