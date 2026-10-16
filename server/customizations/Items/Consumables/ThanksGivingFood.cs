// cc-P128: ServUO pub57 Scripts/Items/Consumables/ThanksGivingFood.cs -> ours. The Thanksgiving table: a sweet potato
// pie, a basket of rolls and a turkey platter that each hand out pieces (a slice, a roll, a random roast) when
// double-clicked, deleting themselves when the last piece is taken, plus the seven single foods they give. The count
// of pieces left is saved.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public abstract partial class BaseComplexFood : Food
{
    [SerializableField(0, fieldChanged: nameof(OnPiecesLeftChanged))]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _piecesLeft;

    public abstract Food Piece { get; }
    public abstract int Pieces { get; }

    public BaseComplexFood(int id) : base(id)
    {
        Stackable = false;
        Weight = 1.0;

        _piecesLeft = Pieces;
        FillFactor = _piecesLeft;
    }

    private void OnPiecesLeftChanged(int oldValue, int newValue)
    {
        if (newValue == 0)
        {
            Delete();
        }
        else if (oldValue != newValue)
        {
            OnChanged();
            InvalidateProperties();
        }
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (!IsChildOf(from.Backpack))
        {
            return;
        }

        Item piece = Piece;

        if (piece == null)
        {
            return;
        }

        if (from.Backpack?.TryDropItem(from, piece, false) != true)
        {
            piece.MoveToWorld(from.Location, from.Map);
        }

        PiecesLeft--;
    }

    public virtual void OnChanged()
    {
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add(1153515, $"{_piecesLeft}"); // ~1_COUNT~ pieces
    }
}

[SerializationGenerator(0, false)]
public partial class SweetPotatoPie : BaseComplexFood
{
    [Constructible]
    public SweetPotatoPie() : base(19469)
    {
    }

    public override int LabelNumber => 1153514; // sweet potato pie
    public override Food Piece => new SliceOfPie();
    public override int Pieces => 6;

    public override void OnChanged()
    {
        if (PiecesLeft < Pieces && ItemID == 19469)
        {
            ItemID = 19458;
        }
    }
}

[Flippable(19459, 19560)]
[SerializationGenerator(0, false)]
public partial class SliceOfPie : Food
{
    [Constructible]
    public SliceOfPie() : base(19459)
    {
        Stackable = false;
        Weight = 1.0;
        FillFactor = 1;
    }

    public override int LabelNumber => 1153519; // slice of pie
}

[SerializationGenerator(0, false)]
public partial class MashedSweetPotatoes : Food
{
    [Constructible]
    public MashedSweetPotatoes() : base(19461)
    {
        Stackable = false;
        Weight = 1.0;
        FillFactor = 1;
    }

    public override int LabelNumber => 1153516; // mashed sweet potatoes
}

[Flippable(19371, 19372)]
[SerializationGenerator(0, false)]
public partial class BasketOfRolls : BaseComplexFood
{
    [Constructible]
    public BasketOfRolls() : base(19371)
    {
    }

    public override int LabelNumber => 1153518; // basket of rolls
    public override Food Piece => new DinnerRoll();
    public override int Pieces => 13;
}

[SerializationGenerator(0, false)]
public partial class DinnerRoll : Food
{
    [Constructible]
    public DinnerRoll() : base(2538)
    {
        Weight = 1.0;
        FillFactor = 1;
    }

    public override int LabelNumber => 1153520; // dinner roll
}

[Flippable(18824, 18831)]
[SerializationGenerator(0, false)]
public partial class TurkeyPlatter : BaseComplexFood
{
    [Constructible]
    public TurkeyPlatter() : base(18824)
    {
    }

    public override int LabelNumber => 1153517; // turkey platter

    public override Food Piece => Utility.Random(6) switch
    {
        1 => new RoastDuck(),
        2 => new RoastTurkey(),
        3 => new RoastChicken(),
        4 => new TurkeyLeg(),
        5 => new GibletGravey(),
        _ => new TurkeyDinner()
    };

    public override int Pieces => 8;
}

[SerializationGenerator(0, false)]
public partial class TurkeyDinner : Food
{
    [Constructible]
    public TurkeyDinner() : base(2479)
    {
        Weight = 1.0;
        FillFactor = 1;
    }

    public override int LabelNumber => 1153532; // Turkey Dinner
}

[Flippable(2487, 2488)]
[SerializationGenerator(0, false)]
public partial class RoastDuck : Food
{
    [Constructible]
    public RoastDuck() : base(2487)
    {
        Weight = 1.0;
        FillFactor = 1;
    }

    public override int LabelNumber => 1153505; // Roast Duck
}

[Flippable(2487, 2488)]
[SerializationGenerator(0, false)]
public partial class RoastTurkey : Food
{
    [Constructible]
    public RoastTurkey() : base(2487)
    {
        Weight = 1.0;
        FillFactor = 1;
    }

    public override int LabelNumber => 1153507; // Roast Turkey
}

[Flippable(2487, 2488)]
[SerializationGenerator(0, false)]
public partial class RoastChicken : Food
{
    [Constructible]
    public RoastChicken() : base(2487)
    {
        Weight = 1.0;
        FillFactor = 1;
    }

    public override int LabelNumber => 1153506; // Roast Chicken
}

[SerializationGenerator(0, false)]
public partial class TurkeyLeg : Food
{
    [Constructible]
    public TurkeyLeg() : base(5640)
    {
        Weight = 1.0;
        FillFactor = 1;
    }

    public override int LabelNumber => 1153508; // turkey leg
}

[SerializationGenerator(0, false)]
public partial class GibletGravey : Food
{
    [Constructible]
    public GibletGravey() : base(5634)
    {
        Weight = 1.0;
        FillFactor = 1;
    }

    public override int LabelNumber => 1153509; // Giblet Gravey
}
