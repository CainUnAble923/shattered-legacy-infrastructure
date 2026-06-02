using System;
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
    }

    public enum BulkGenericType
    {
        Iron,
        Cloth,
        Leather
    }

    public static class BGTClassifier
    {
        public static BulkGenericType Classify(BODType deedType, Type itemType)
        {
            if (deedType != BODType.Tailor)
            {
                return BulkGenericType.Iron;
            }

            return itemType?.IsSubclassOf(typeof(BaseArmor)) != false || itemType.IsSubclassOf(typeof(BaseShoes))
                ? BulkGenericType.Leather
                : BulkGenericType.Cloth;
        }
    }
}
