// ServUO: Services/Underworld/PuzzleRoom/MastermindPuzzleItem.cs (cc-P197). The mastermind board (1113379, hue
// 914, blessed) the north puzzle box hands a player with a gold key: a Khaldun-style PuzzleChest (pinned
// Engines/Khaldun/PuzzleChest.cs, the same five-cylinder guessing gump) that only opens inside the Puzzle Room
// (MazePuzzleItem.IsInPuzzleRoom). Solved (LockPick), it deletes itself three seconds later, expires the magic key
// (MagicKey.Decay, which also clears the other keys and boards) and puts an Experimental Gem with the solver as its
// Owner in the pack (1113382). It lives 600 seconds on a ten-second slice (1072517 "Lifespan"), then Decay: 1072515
// and smoke, like a BaseDecayingItem. GenerateTreasure gives it a fresh solution and no gold (ServUO's override).
//
// Saved: the key, as ServUO; the lifespan is not (ServUO's m_Lifespan is never written), so a board that survives
// a restart reads 0 and decays on its first slice, as ServUO's does. Pinned's PuzzleGump and StatusGump are private
// nested classes, so the two CloseGump calls ServUO makes on expiry cannot name them; an open gump's next click
// finds the board deleted and does nothing (D, notes).

using System;
using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class MastermindPuzzleItem : PuzzleChest
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private MagicKey _key;

    private Timer _timer;
    private int _lifespan;

    [Constructible]
    public MastermindPuzzleItem(MagicKey key) : base(0x2AAA)
    {
        Hue = 914;
        _key = key;
        _lifespan = 600;
        Movable = true;

        _timer = Timer.DelayCall(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10), Slice);

        LootType = LootType.Blessed;
    }

    public override int LabelNumber => 1113379; // Puzzle Board

    public int RemainingLifespan => _lifespan;

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);

        list.Add(1072517, _lifespan.ToString()); // Lifespan: ~1_val~ seconds
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (MazePuzzleItem.IsInPuzzleRoom(from))
        {
            base.OnDoubleClick(from);
        }
    }

    public virtual void Slice()
    {
        _lifespan -= 10;

        InvalidateProperties();

        if (_lifespan <= 0)
        {
            Decay();
        }
    }

    public virtual void Decay()
    {
        if (RootParent is Mobile parent)
        {
            if (Name == null)
            {
                parent.SendLocalizedMessage(1072515, "#" + LabelNumber); // The ~1_name~ expired...
            }
            else
            {
                parent.SendLocalizedMessage(1072515, Name); // The ~1_name~ expired...
            }

            Effects.SendLocationParticles(EffectItem.Create(parent.Location, parent.Map, EffectItem.DefaultDuration), 0x3728, 8, 20, 5042);
            Effects.PlaySound(parent.Location, parent.Map, 0x201);

            // ServUO: parent.CloseGump(typeof(PuzzleChest.PuzzleGump)) and StatusGump (private here; header).
        }
        else
        {
            Effects.SendLocationParticles(EffectItem.Create(Location, Map, EffectItem.DefaultDuration), 0x3728, 8, 20, 5042);
            Effects.PlaySound(Location, Map, 0x201);
        }

        _timer?.Stop();

        Delete();
    }

    public override void LockPick(Mobile from)
    {
        base.LockPick(from);

        Timer.DelayCall(TimeSpan.FromSeconds(3), Delete);

        _key?.Decay();

        var gem = new ExperimentalGem();
        gem.Owner = from;

        from.AddToBackpack(gem);

        from.SendLocalizedMessage(1113382); // You've solved the puzzle!! An item has been placed in your bag.
    }

    protected override void GenerateTreasure()
    {
        Solution = new PuzzleChestSolution(this);
    }

    public override void OnAfterDelete()
    {
        base.OnAfterDelete();
        _timer?.Stop();
        _timer = null;
    }

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        _timer = Timer.DelayCall(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(10), Slice);
    }
}
