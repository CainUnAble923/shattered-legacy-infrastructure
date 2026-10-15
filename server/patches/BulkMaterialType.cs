using System;
using Server.Engines.Craft;
using Server.Items;

namespace Server.Engines.BulkOrders
{
    public enum BulkMaterialType
    {
        None,
        DullCopper,
        ShadowIron,
        Copper,
        Bronze,
        Gold,
        Agapite,
        Verite,
        Valorite,
        Spined,
        Horned,
        Barbed,
        // Post-Valorite metals — ClusterF extension (appended; safe for existing serialized BODs)
        Platinum   = 12,
        Toxic      = 13,
        Blaze      = 14,
        Frost      = 15,
        Obsidian   = 16,
        Mythril    = 17,
        Adamantium = 18,
        Celestial  = 19,
        // ServUO's 2016 woods (cc-P122). ServUO numbers them 12..17, which this shard's metals already hold in saved
        // deeds, so they follow Celestial. Carpentry and Fletching deeds (P123) use them; the bribe rules name them now.
        OakWood    = 20,
        AshWood    = 21,
        YewWood    = 22,
        Heartwood  = 23,
        Bloodwood  = 24,
        Frostwood  = 25,
    }

    public enum BulkGenericType
    {
        Iron,
        Cloth,
        Leather,
        Wood // cc-P123: ServUO's fourth, for carpentry, fletching and wooden tinker work
    }

    public static class BGTClassifier
    {
        // cc-P123: ServUO pub57 BulkMaterialType.cs:37-70 for the six 2016 crafts. Smith and tailor read as before.
        public static BulkGenericType Classify(BODType deedType, Type itemType)
        {
            if (deedType == BODType.Tailor)
            {
                return itemType?.IsSubclassOf(typeof(BaseArmor)) != false || itemType.IsSubclassOf(typeof(BaseShoes))
                    ? BulkGenericType.Leather
                    : BulkGenericType.Cloth;
            }

            if (deedType == BODType.Tinkering && itemType != null)
            {
                if (itemType == typeof(Clock) || itemType.IsSubclassOf(typeof(Clock)))
                {
                    return BulkGenericType.Wood;
                }

                var item = DefTinkering.CraftSystem.CraftItems.SearchFor(itemType);
                var typeRes = item?.Resources.Count > 0 ? item.Resources[0].ItemType : null;

                if (typeRes == typeof(Board) || typeRes == typeof(Log))
                {
                    return BulkGenericType.Wood;
                }
            }
            else if (deedType is BODType.Fletching or BODType.Carpentry)
            {
                return BulkGenericType.Wood;
            }

            return BulkGenericType.Iron;
        }
    }
}
