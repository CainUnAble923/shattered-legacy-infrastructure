// cc-P123: the bulk order book learns the six 2016 crafts. Pinned's book stores and rebuilds smith and tailor deeds only
// (BOBSmallEntry, BOBLargeEntry), names smith and tailor materials only (BOBGump.GetMaterialName) and filters by those two
// crafts (BOBGump.CheckFilter, BOBFilterGump); BOB-all-crafts.patch sends each of them here for any other craft. Ported
// from ServUO pub57 Scripts/Services/BulkOrders/Books/ (BOBSmallEntry and BOBLargeEntry's Reconstruct, BOBGump
// GetMaterialName :539-618 and CheckFilter :307-388, BOBFilterGump m_MaterialFiltersNew :33-116).
//
// The filter's numbers. A saved book or player filter keeps its number, and pinned's 0..16 (all, smith, tailor, then smith
// and tailor materials) mean what they meant. ServUO renumbers its new-system list (crafts 1..8, smith metals 9..17,
// tailor 18..22); this keeps pinned's 0..16 and gives the six new crafts 17..22, then ServUO's own 23..45 for tinkering's
// metals and carpentry's and fletching's woods. The smith metal filters (3..11) now also check the craft, as ServUO's
// do, because tinkering deeds are made of the same metals.

using System;
using Server.Items;

namespace Server.Engines.BulkOrders;

public static class BulkOrderBooks
{
    // -- The filter (BOBGump.CheckFilter for the numbers past pinned's 16) ---------------------------------------------

    public const int TinkeringFilter = 17;
    public const int CarpentryFilter = 18;
    public const int FletchingFilter = 19;
    public const int AlchemyFilter = 20;
    public const int InscriptionFilter = 21;
    public const int CookingFilter = 22;

    public static bool CheckFilter(int filter, BulkMaterialType mat, BODType deedType) =>
        filter switch
        {
            TinkeringFilter   => deedType == BODType.Tinkering,
            CarpentryFilter   => deedType == BODType.Carpentry,
            FletchingFilter   => deedType == BODType.Fletching,
            AlchemyFilter     => deedType == BODType.Alchemy,
            InscriptionFilter => deedType == BODType.Inscription,
            CookingFilter     => deedType == BODType.Cooking,
            >= 23 and <= 31   => deedType == BODType.Tinkering && mat == TinkerMaterial(filter),
            >= 32 and <= 38   => deedType == BODType.Carpentry && mat == WoodMaterial(filter - 32),
            >= 39 and <= 45   => deedType == BODType.Fletching && mat == WoodMaterial(filter - 39),
            _                 => true
        };

    private static BulkMaterialType TinkerMaterial(int filter) =>
        filter == 23 ? BulkMaterialType.None : BulkMaterialType.DullCopper + (filter - 24);

    // ServUO's order: none, oak, ash, yew, bloodwood, heartwood, frostwood (BOBGump.cs:357-388).
    private static BulkMaterialType WoodMaterial(int offset) =>
        offset switch
        {
            1 => BulkMaterialType.OakWood,
            2 => BulkMaterialType.AshWood,
            3 => BulkMaterialType.YewWood,
            4 => BulkMaterialType.Bloodwood,
            5 => BulkMaterialType.Heartwood,
            6 => BulkMaterialType.Frostwood,
            _ => BulkMaterialType.None
        };

    /// <summary>The filter gump's rows past pinned's two crafts: ServUO's m_MaterialFiltersNew, with this file's numbers.</summary>
    public static readonly int[,] NewCraftFilters =
    {
        { 1044097, TinkeringFilter }, // Tinkering
        { 1062226, 23 },              // Iron
        { 1018332, 24 },              // Dull Copper
        { 1018333, 25 },              // Shadow Iron
        { 1018334, 26 },              // Copper
        { 1018335, 27 },              // Bronze
        { 0, 0 },                     // --Blank--
        { 1018336, 28 },              // Golden
        { 1018337, 29 },              // Agapite
        { 1018338, 30 },              // Verite
        { 1018339, 31 },              // Valorite
        { 0, 0 },                     // --Blank--
        { 1044071, CarpentryFilter }, // Carpentry
        { 1079435, 32 },              // Wood
        { 1071428, 33 },              // Oak
        { 1071429, 34 },              // Ash
        { 1071430, 35 },              // Yew
        { 0, 0 },                     // --Blank--
        { 0, 0 },                     // --Blank--
        { 1071431, 36 },              // Bloodwood
        { 1071432, 37 },              // Heartwood
        { 1071433, 38 },              // Frostwood
        { 0, 0 },                     // --Blank--
        { 0, 0 },                     // --Blank--
        { 1044068, FletchingFilter }, // Fletching
        { 1079435, 39 },              // Wood
        { 1071428, 40 },              // Oak
        { 1071429, 41 },              // Ash
        { 1071430, 42 },              // Yew
        { 0, 0 },                     // --Blank--
        { 0, 0 },                     // --Blank--
        { 1071431, 43 },              // Bloodwood
        { 1071432, 44 },              // Heartwood
        { 1071433, 45 },              // Frostwood
        { 0, 0 },                     // --Blank--
        { 0, 0 },                     // --Blank--
        { 1044060, AlchemyFilter },   // Alchemy
        { 0, 0 }, { 0, 0 }, { 0, 0 }, { 0, 0 }, { 0, 0 },
        { 1044083, InscriptionFilter }, // Inscription
        { 0, 0 }, { 0, 0 }, { 0, 0 }, { 0, 0 }, { 0, 0 },
        { 1044073, CookingFilter },   // Cooking
        { 0, 0 }, { 0, 0 }, { 0, 0 }, { 0, 0 }, { 0, 0 }
    };

    /// <summary>Pinned's smith-and-tailor rows followed by the six crafts' (the filter gump's material list).</summary>
    public static int[,] AllFilters(int[,] pinned)
    {
        var rows = pinned.GetLength(0) + NewCraftFilters.GetLength(0);
        var all = new int[rows, 2];

        for (var i = 0; i < rows; i++)
        {
            var (src, j) = i < pinned.GetLength(0) ? (pinned, i) : (NewCraftFilters, i - pinned.GetLength(0));
            all[i, 0] = src[j, 0];
            all[i, 1] = src[j, 1];
        }

        return all;
    }

    // -- Material names in the book (ServUO BOBGump.GetMaterialName :539-618) -------------------------------------------

    public static TextDefinition GetMaterialName(BulkMaterialType mat, BODType type, Type itemType) =>
        type switch
        {
            BODType.Tinkering when mat == BulkMaterialType.None &&
                                   BGTClassifier.Classify(type, itemType) == BulkGenericType.Wood => 1079435, // Wood
            BODType.Tinkering => mat switch
            {
                BulkMaterialType.DullCopper => 1018332,
                BulkMaterialType.ShadowIron => 1018333,
                BulkMaterialType.Copper     => 1018334,
                BulkMaterialType.Bronze     => 1018335,
                BulkMaterialType.Gold       => 1018336,
                BulkMaterialType.Agapite    => 1018337,
                BulkMaterialType.Verite     => 1018338,
                BulkMaterialType.Valorite   => 1018339,
                _                           => 1062226 // Iron
            },
            BODType.Carpentry or BODType.Fletching => mat switch
            {
                BulkMaterialType.AshWood   => 1071429,
                BulkMaterialType.YewWood   => 1071430,
                BulkMaterialType.Heartwood => 1071432,
                BulkMaterialType.Bloodwood => 1071431,
                BulkMaterialType.Frostwood => 1071433,
                BulkMaterialType.OakWood   => 1071428,
                _                          => 1079435 // Wood
            },
            _ => TextDefinition.Empty
        };

    // -- Deeds in and out of the book ----------------------------------------------------------------------------------

    /// <summary>The craft a deed going into the book is; pinned's default for a deed of none of the eight.</summary>
    public static BODType DeedTypeOf(Item bod, BODType fallback) => BulkOrderSystem.GetBODType(bod) ?? fallback;

    /// <summary>A small deed of the six crafts, rebuilt from its book entry (ServUO BOBSmallEntry.Reconstruct).</summary>
    public static SmallBOD ReconstructSmall(
        BODType type, int amountCur, int amountMax, Type itemType, int number, int graphic, bool reqExceptional,
        BulkMaterialType material
    ) =>
        type switch
        {
            BODType.Alchemy => new SmallAlchemyBOD(
                amountCur, amountMax, itemType, number, graphic, reqExceptional, material
            ),
            BODType.Inscription => new SmallInscriptionBOD(
                amountCur, amountMax, itemType, number, graphic, reqExceptional, material
            ),
            BODType.Tinkering => new SmallTinkerBOD(
                amountCur, amountMax, itemType, number, graphic, reqExceptional, material,
                SmallTinkerBOD.GemFromNumber(itemType, number) // the book keeps the jewel's name, which names its gem
            ),
            BODType.Cooking => new SmallCookingBOD(
                amountCur, amountMax, itemType, number, graphic, reqExceptional, material
            ),
            BODType.Fletching => new SmallFletchingBOD(
                amountCur, amountMax, itemType, number, graphic, reqExceptional, material
            ),
            BODType.Carpentry => new SmallCarpentryBOD(
                amountCur, amountMax, itemType, number, graphic, reqExceptional, material
            ),
            _ => null
        };

    /// <summary>A large deed of the six crafts, rebuilt from its book entry (ServUO BOBLargeEntry.Reconstruct).</summary>
    public static LargeBOD ReconstructLarge(
        BODType type, int amountMax, bool reqExceptional, BulkMaterialType material, LargeBulkEntry[] entries
    ) =>
        type switch
        {
            BODType.Alchemy     => new LargeAlchemyBOD(amountMax, reqExceptional, material, entries),
            BODType.Inscription => new LargeInscriptionBOD(amountMax, reqExceptional, material, entries),
            BODType.Tinkering => new LargeTinkerBOD(
                amountMax, reqExceptional, material, entries,
                entries.Length > 0
                    ? SmallTinkerBOD.GemFromNumber(entries[0].Details.Type, entries[0].Details.Number)
                    : GemType.None
            ),
            BODType.Cooking   => new LargeCookingBOD(amountMax, reqExceptional, material, entries),
            BODType.Fletching => new LargeFletchingBOD(amountMax, reqExceptional, material, entries),
            BODType.Carpentry => new LargeCarpentryBOD(amountMax, reqExceptional, material, entries),
            _                 => null
        };
}
