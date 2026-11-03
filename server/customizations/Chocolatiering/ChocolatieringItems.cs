// cc-P785: the chocolatiering items pinned lacks. Sweet cocoa butter is ServUO's (pub57 Items/Consumables/
// Chocolatiering.cs:164, label 1156401, art 0x103D, no quality kept here: nothing reads it). The figures and molds are
// after Carlos_ar's Chocolatiering Adventure (D:\UO\third-party\code\chocolatiering, reference only): one figure class
// with its shape and chocolate saved, instead of the package's five near-identical classes, and no tarot card under the
// wrapper. Every art id was checked in our client's artLegacyMUL.uop (shard-migration notes/cc-P785-tools/
// art-check.txt). Saved types use the serialization generator (D40).

using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Engines.Craft;

namespace Server.Items;

/// <summary>Sweet cocoa butter: sugar and cocoa butter, warmed at an oven. The base of the nutcrackers.</summary>
[SerializationGenerator(0, false)]
public partial class SweetCocoaButter : Item
{
    [Constructible]
    public SweetCocoaButter() : base(0x103D) => Hue = 0x457;

    public override int LabelNumber => 1156401; // Sweet Cocoa butter

    public override double DefaultWeight => 1.0;
}

public enum ChocolateFigureKind
{
    Orc,
    Beetle,
    Bunny,
    Solen,
    Unicorn
}

/// <summary>
/// A chocolate figure in the shape of its mold, in dark, milk or white chocolate. Eaten like the nutcracker (a candy:
/// pinned's CandyCane), or kept on a shelf.
/// </summary>
[SerializationGenerator(0, false)]
public partial class ChocolateFigure : CandyCane, ICraftable
{
    [SerializableField(0, fieldChanged: nameof(OnFigureChanged))]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private ChocolateFigureKind _figure;

    [SerializableField(1, fieldChanged: nameof(OnChocolateChanged))]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private ChocolateNutcracker.ChocolateType _chocolate;

    [Constructible]
    public ChocolateFigure(
        ChocolateFigureKind figure = ChocolateFigureKind.Bunny,
        ChocolateNutcracker.ChocolateType chocolate = ChocolateNutcracker.ChocolateType.Milk
    ) : base(ArtOf(figure))
    {
        _figure = figure;
        _chocolate = chocolate;
        Hue = (int)chocolate;
    }

    /// <summary>The statuette art the package used for each shape (all present in our client).</summary>
    public static int ArtOf(ChocolateFigureKind figure) => figure switch
    {
        ChocolateFigureKind.Orc     => 0x20E0,
        ChocolateFigureKind.Beetle  => 0x260F,
        ChocolateFigureKind.Solen   => 0x2602,
        ChocolateFigureKind.Unicorn => 0x25CE,
        _                           => 0x99A4
    };

    public static string ChocolateName(ChocolateNutcracker.ChocolateType chocolate) => chocolate switch
    {
        ChocolateNutcracker.ChocolateType.Dark  => "dark",
        ChocolateNutcracker.ChocolateType.White => "white",
        _                                       => "milk"
    };

    public static string FigureName(ChocolateFigureKind figure) => figure.ToString().ToLowerInvariant();

    public override string DefaultName => $"a {ChocolateName(_chocolate)} chocolate {FigureName(_figure)}";

    private void OnFigureChanged(ChocolateFigureKind oldValue, ChocolateFigureKind newValue) => OnLookChanged();

    private void OnChocolateChanged(ChocolateNutcracker.ChocolateType oldValue, ChocolateNutcracker.ChocolateType newValue) =>
        OnLookChanged();

    private void OnLookChanged()
    {
        ItemID = ArtOf(_figure);
        Hue = (int)_chocolate;
        InvalidateProperties();
    }

    public int OnCraft(
        int quality, bool makersMark, Mobile from, CraftSystem craftSystem, System.Type typeRes, BaseTool tool,
        CraftItem craftItem, int resHue
    )
    {
        if (ChocolatieringRecipes.RecipeOf(craftItem) is { } r)
        {
            Figure = r.FigureKind;
            Chocolate = r.ChocolateKind;
        }

        return quality;
    }
}

/// <summary>A mold for chocolate figures, sold by provisioners. One is used up by each figure poured.</summary>
[SerializationGenerator(0, false)]
public abstract partial class ChocolateMold : Item
{
    public const int MoldArt = 0x2DB3;

    protected ChocolateMold() : base(MoldArt)
    {
        Hue = 1161;
        Stackable = true;
    }

    public abstract ChocolateFigureKind Figure { get; }

    public override string DefaultName => NameOf(Figure);

    public static string NameOf(ChocolateFigureKind figure) =>
        figure == ChocolateFigureKind.Orc ? "an orc chocolate mold" : $"a {ChocolateFigure.FigureName(figure)} chocolate mold";

    public override double DefaultWeight => 1.0;

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add("Used up by one chocolate figure (Cooking)");
    }
}

[SerializationGenerator(0, false)]
public partial class OrcChocolateMold : ChocolateMold
{
    [Constructible]
    public OrcChocolateMold(int amount = 1) => Amount = amount;

    public override ChocolateFigureKind Figure => ChocolateFigureKind.Orc;
}

[SerializationGenerator(0, false)]
public partial class BeetleChocolateMold : ChocolateMold
{
    [Constructible]
    public BeetleChocolateMold(int amount = 1) => Amount = amount;

    public override ChocolateFigureKind Figure => ChocolateFigureKind.Beetle;
}

[SerializationGenerator(0, false)]
public partial class BunnyChocolateMold : ChocolateMold
{
    [Constructible]
    public BunnyChocolateMold(int amount = 1) => Amount = amount;

    public override ChocolateFigureKind Figure => ChocolateFigureKind.Bunny;
}

[SerializationGenerator(0, false)]
public partial class SolenChocolateMold : ChocolateMold
{
    [Constructible]
    public SolenChocolateMold(int amount = 1) => Amount = amount;

    public override ChocolateFigureKind Figure => ChocolateFigureKind.Solen;
}

[SerializationGenerator(0, false)]
public partial class UnicornChocolateMold : ChocolateMold
{
    [Constructible]
    public UnicornChocolateMold(int amount = 1) => Amount = amount;

    public override ChocolateFigureKind Figure => ChocolateFigureKind.Unicorn;
}
