using ModernUO.Serialization;
using Server.Network;

namespace Server.Items;

/// <summary>
/// A physical essence distilled from a magical property on an item.
///
/// Obtained by:
///   - Extracting a specific property from a magic item (removes the property from the item)
///   - Full disenchanting (random chance per property)
///   - Crafting at the Artificers' Order (once the property is mastered)
///
/// Used by:
///   - Imbuing a property onto a new item (consumed until the Artificers' member has
///     used it enough times to "master" that property; after mastery no essence is required)
///   - Trading / selling to other Artificers' members
///
/// PropertyKey matches ImbuePropertyDef.Name in ImbueCatalogue exactly.
/// </summary>
[SerializationGenerator(0, false)]
public partial class PropertyEssence : Item
{
    [SerializableField(0)]
    private string _propertyKey;

    [Constructible]
    public PropertyEssence() : base(0x26B4)
    {
        _propertyKey = "Unknown";
        Weight       = 0.1;
        Hue          = HueForKey(_propertyKey);
    }

    public PropertyEssence(string propertyKey) : base(0x26B4)
    {
        _propertyKey = propertyKey;
        Weight       = 0.1;
        Hue          = HueForKey(propertyKey);
    }

    public override string DefaultName => $"Essence of {_propertyKey}";

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add(1049644, _propertyKey); // "~1_val~"
    }

    public override void OnSingleClick(Mobile from)
    {
        from.NetState.SendMessage(Serial, ItemID, MessageType.Label, 0x3B2, 3, true, null, "", DefaultName);
    }

    // ── Hue by property group ─────────────────────────────────────────────────

    private static int HueForKey(string key)
    {
        // Weapon hit procs → orange-red
        if (key.StartsWith("Hit ")) return 0x21;
        // Defenses / resist → blue
        if (key.Contains("Resist") || key.Contains("Defense")) return 0x5;
        // Stats → green
        if (key.StartsWith("Bonus") || key.Contains("Regen")) return 0x3A;
        // Skills → yellow-gold
        if (key.Contains("Skill") || key.Contains("Spell") || key.Contains("Cast")) return 0x8A;
        // Slayer → purple
        if (key.StartsWith("Slayer:")) return 0x22;
        // Default → soft white
        return 0x47E;
    }

    private void Deserialize(IGenericReader reader, int version) { }
}
