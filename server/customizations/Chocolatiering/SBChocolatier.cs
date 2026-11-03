// cc-P785: the provisioner's chocolatiering stock, from Configuration/chocolatiering.json (ChocolatieringTable).

using System.Collections.Generic;
using Server.Engines.Craft;
using Server.Items;

namespace Server.Mobiles;

/// <summary>
/// What every provisioner also sells for chocolatiering (Provisioner-chocolatiering.patch adds it to InitSBInfo, which
/// pinned runs on every world load, so existing provisioners stock it too): the rows of chocolatiering.json.
/// </summary>
public class SBChocolatier : SBInfo
{
    public override IShopSellInfo SellInfo { get; } = new GenericSellInfo();

    public override List<GenericBuyInfo> BuyInfo { get; } = Build();

    public static List<GenericBuyInfo> Build()
    {
        var list = new List<GenericBuyInfo>();
        var table = ChocolatieringTable.Current;
        if (!table.Enabled)
        {
            return list;
        }

        foreach (var row in table.Provisioner)
        {
            if (row.ItemType == null)
            {
                continue;
            }

            var (name, art, hue) = Display(row.ItemType);
            list.Add(new GenericBuyInfo(name, row.ItemType, row.Price, row.Amount, art, hue));
        }

        return list;
    }

    private static (string Name, int Art, int Hue) Display(System.Type type)
    {
        if (typeof(ChocolateMold).IsAssignableFrom(type))
        {
            var fig = System.Enum.Parse<ChocolateFigureKind>(type.Name.Replace("ChocolateMold", ""));
            return (ChocolateMold.NameOf(fig), ChocolateMold.MoldArt, 1161);
        }

        if (type == typeof(SackOfSugar))
        {
            return ("1080003", 0x1039, 0); // Sack of sugar
        }

        if (type == typeof(Vanilla))
        {
            return ("1080009", 0xE2A, 0); // Vanilla
        }

        if (type == typeof(GoldFoil))
        {
            return ("a foil sheet", 0x9C48, 1281);
        }

        return (type.Name, 0x1F14, 0);
    }
}
