// ServUO: Services/Underworld/PuzzleRoom/MagicKey.cs (cc-P197). The magic key of the Puzzle Room. One stands
// unmovable beside the instruction book (placed by the generator; Lifespan 0, so it never decays): a player within
// three tiles who carries no magic key gets the OK/CANCEL gump (1113390, 1113391) and OK puts a fresh, movable key
// in the pack with a 1800-second life (1113389). When a carried key expires (Decay), the holder, if inside the
// Puzzle Room, is thrown to 1096-1098,1175-1177 with their pets and smoke, and every copper key, gold key, maze board
// and mastermind board in the pack is deleted; then the key itself (BaseDecayingItem.Decay: 1072515 and smoke).
// Saved: the span, as ServUO.
//
// One ServUO value is corrected: its m_PuzzleRoom is Rectangle2D(1234, 1234, 10, 10), a placeholder nowhere near
// the Puzzle Room (1089,1162 16x12, MazePuzzleItem.IsInPuzzleRoom), so its expulsion never fires though the book
// promises it ("If you fail, you will be expelled"). The room's own rectangle is used (the D-4 rule's broken
// kind; notes, Deviations).

using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class MagicKey : BaseDecayingItem
{
    [SerializableField(0)]
    private int _span;

    [Constructible]
    public MagicKey() : base(4114)
    {
        _span = 0;
        Movable = false;
    }

    public override int LabelNumber => 1024114; // magic key
    public override int Lifespan => _span;

    public override void OnDoubleClick(Mobile from)
    {
        if (RootParent != null || !from.InRange(GetWorldLocation(), 3) || Movable || IsLockedDown || IsSecure)
        {
            return;
        }

        if (from.Backpack != null && _span == 0)
        {
            var key = from.Backpack.FindItemByType<MagicKey>();

            if (key == null)
            {
                from.CloseGump<MagicKeyConfirmGump>();
                from.SendGump(new MagicKeyConfirmGump(this));
            }
        }
    }

    /// <summary>The confirm gump's OK: a fresh, movable, 1800-second key into the pack (1113389).</summary>
    public static MagicKey GiveKey(Mobile from)
    {
        var key = new MagicKey();
        from.AddToBackpack(key);

        key.Movable = true;
        key.StartTimer();

        from.SendLocalizedMessage(1113389); // As long as you carry this key, you will be granted access to the Puzzle Room.
        return key;
    }

    public override void StartTimer()
    {
        TimeLeft = 1800;
        _span = 1800;
        Movable = true;
        base.StartTimer();
        InvalidateProperties();
    }

    public override void Decay()
    {
        if (RootParent is Mobile m && m.Map != Map.Internal)
        {
            if (MazePuzzleItem.IsInPuzzleRoom(m))
            {
                var x = Utility.RandomMinMax(1096, 1098);
                var y = Utility.RandomMinMax(1175, 1177);
                var z = m.Map.GetAverageZ(x, y);

                var loc = m.Location;
                var p = new Point3D(x, y, z);
                BaseCreature.TeleportPets(m, p, Map.TerMur);
                m.MoveToWorld(p, Map.TerMur);

                Effects.SendLocationParticles(EffectItem.Create(loc, m.Map, EffectItem.DefaultDuration), 0x3728, 10, 10, 2023);
                Effects.SendLocationParticles(EffectItem.Create(p, m.Map, EffectItem.DefaultDuration), 0x3728, 10, 10, 5023);
            }

            var pack = m.Backpack;

            if (pack != null)
            {
                // cc-P610 (P491 #3): every bag in the pack, not its top level only (a key or board put in a bag kept the
                // session's progress); a board belongs to this key, or to none, to go; another key's board stays.
                var list = new List<Item>();
                foreach (var item in pack.FindItemsByType<Item>())
                {
                    if (item is CopperPuzzleKey or GoldPuzzleKey ||
                        item is MazePuzzleItem maze && (maze.Key == this || maze.Key == null) ||
                        item is MastermindPuzzleItem board && (board.Key == this || board.Key == null))
                    {
                        list.Add(item);
                    }
                }

                foreach (var item in list)
                {
                    item.Delete();
                }
            }
        }

        base.Decay();
    }

    private class MagicKeyConfirmGump : Gump
    {
        private readonly MagicKey _key;

        public MagicKeyConfirmGump(MagicKey key) : base(50, 50)
        {
            _key = key;

            AddPage(0);
            AddBackground(0, 0, 297, 115, 9200);

            AddImageTiled(5, 10, 285, 25, 2624);
            AddHtmlLocalized(10, 15, 275, 25, 1113390, 0x7FFF); // Puzzle Room Timer

            AddImageTiled(5, 40, 285, 40, 2624);
            AddHtmlLocalized(10, 40, 275, 40, 1113391, 0x7FFF); // Click CANCEL to read the instruction book or OK to start the timer now.

            AddButton(5, 85, 4017, 4018, 0);
            AddHtmlLocalized(40, 87, 80, 25, 1011012, 0x7FFF); // CANCEL

            AddButton(215, 85, 4023, 4024, 1);
            AddHtmlLocalized(250, 87, 80, 25, 1006044, 0x7FFF); // OK
        }

        public override void OnResponse(NetState state, in RelayInfo info)
        {
            if (info.ButtonID == 1)
            {
                GiveKey(state.Mobile);
            }
        }
    }
}
