// cc-P78 Part D (F-43): the pattern book (one craft skill, one band) and the account recipe scroll (one key).
// Using one teaches the whole account (ClusterFRecipeStore) and consumes it; one the account already knows is refused
// and kept. Unread ones are ordinary items: tradeable, droppable, sellable between players.
// Books are hued by band along the ore/metal ladder: I Dull Copper, II Copper, III Gold, IV Verite, V Valorite, past
// that Celestial (CraftResources.GetHue, the same ladder as the mule tiers).

#nullable enable

using ModernUO.Serialization;
using Server.Engines.Craft;
using Server.Mobiles;

namespace Server.Items;

public static class RecipeItemHues
{
    private static readonly CraftResource[] Ladder =
    [
        CraftResource.DullCopper, CraftResource.Copper, CraftResource.Gold, CraftResource.Verite, CraftResource.Valorite,
        CraftResource.Celestial
    ];

    public static int ForBand(int band) => CraftResources.GetHue(Ladder[System.Math.Clamp(band, 1, Ladder.Length) - 1]);

    /// <summary>Teaches the key to the user's account and deletes the item, or refuses and keeps it.</summary>
    public static void Use(Mobile from, Item item, string key)
    {
        if (from is not PlayerMobile pm)
        {
            return;
        }

        if (!item.IsChildOf(pm.Backpack))
        {
            pm.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
            return;
        }

        if (!ClusterFRecipeStore.Learn(pm, key))
        {
            pm.SendMessage(0x22, $"Your account already knows {ClusterFRecipeStore.Describe(key)}. You keep this.");
            return;
        }

        pm.SendMessage(0x44, $"You study it. Your account now knows {ClusterFRecipeStore.Describe(key)}.");
        pm.PlaySound(0x249);
        item.Delete();
    }
}

[SerializationGenerator(0, false)]
public partial class PatternBook : Item
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private SkillName _craftSkill;

    [SerializableField(1)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private int _band;

    [Constructible]
    public PatternBook(SkillName craftSkill = SkillName.Blacksmith, int band = 1) : base(0x0FF2)
    {
        _craftSkill = craftSkill;
        _band = band;
        Hue = RecipeItemHues.ForBand(band);
        Weight = 1.0;
    }

    public string Key => ClusterFRecipeStore.BookKey(_craftSkill, _band);

    public override string DefaultName =>
        $"a Band {RecipeGatingTable.Current.BandName(_band)} pattern book: {ClusterFRecipeStore.SkillTitle(_craftSkill)}";

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add($"Craft skill: {ClusterFRecipeStore.SkillTitle(_craftSkill)}");
        list.Add($"Band {RecipeGatingTable.Current.BandName(_band)}");
        list.Add("Teaches your account");
    }

    public override void OnDoubleClick(Mobile from) => RecipeItemHues.Use(from, this, Key);
}

[SerializationGenerator(0, false)]
public partial class AccountRecipeScroll : Item
{
    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private string _recipeKey;

    [Constructible]
    public AccountRecipeScroll(string recipeKey = "") : base(0x2831)
    {
        _recipeKey = recipeKey;
        Hue = RecipeItemHues.ForBand(5);
        Weight = 1.0;
    }

    public override string DefaultName => $"a recipe scroll: {ClusterFRecipeStore.Describe(_recipeKey)}";

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add("Teaches your account");
    }

    public override void OnDoubleClick(Mobile from) => RecipeItemHues.Use(from, this, _recipeKey);
}
