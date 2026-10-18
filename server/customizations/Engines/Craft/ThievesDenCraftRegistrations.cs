// cc-P142 (decision 4): the Thieves' Den's three recipes, each made from components that can only be stolen from
// monsters (Services/Monster Stealing/Items/StolenComponents.cs). The Den owns no craft skill (RecipeOwners.BySkill), so
// each entry sits in the craft system its skill belongs to and is gated as a pinned-style OWN recipe (AddRecipe, ids
// 9400 to 9402, a block of our own above the mule schematics' 9000 to 9173): ClusterFRecipeGate classifies an own
// recipe by its id (RecipeKind.OwnRecipe, key recipe:<id>), so the entry never enters the owning guild's Band V
// rotation or starter set (those draw RecipeKind.Scroll only, ClusterFRecipeRewards.ScrollKeys) and is learned only
// from a scroll, which monster stealing is the source of (StealLootTables.RecipeScroll). The recipe-gating system this
// needs is cc-P78's (ClusterFRecipeGate, ClusterFRecipeStore, the account recipe store), already built.
//
//   9400  Shadowsilk Cloak     Tailoring 100-130   14 cloth, 10 shadowsilk thread
//   9401  Thief's Smoke Bomb   Alchemy 100-130     1 eggs, 3 ember ash, 1 whisperleaf
//   9402  Pickpocket's Charm   Tinkering 100-130   2 gold ingots, 1 lucky fang, 2 shadowsilk thread
//
// 130 is the top a stock item teaches to in Tinkering (CraftCeilingsVerification K1, cc-P67: the chain from the stock
// top to 200 is the materials'), so an entry above it would open a gap the ceilings facts guard; the three share it.
//
// ServerStarted, after the stock systems' Initialize; the test host calls Register directly (ServerStarted never fires
// there). Idempotent: a second call finds the entries.

using System;
using Server.Items;

namespace Server.Engines.Craft;

public static class ThievesDenCraftRegistrations
{
    public const int ShadowsilkCloakRecipe = 9400;
    public const int ThiefsSmokeBombRecipe = 9401;
    public const int PickpocketsCharmRecipe = 9402;

    public const double MinSkill = 100.0;
    public const double MaxSkill = 130.0;

    private static bool _registered;

    public static void Configure()
    {
        EventSink.ServerStarted += Register;
    }

    public static void Register()
    {
        if (_registered)
        {
            return;
        }

        var tailoring = DefTailoring.CraftSystem;
        var alchemy = DefAlchemy.CraftSystem;
        var tinkering = DefTinkering.CraftSystem;

        if (tailoring == null || alchemy == null || tinkering == null)
        {
            Console.WriteLine("[ThievesDenCraftRegistrations] WARNING: a stock craft system is null; the Thieves' Den recipes are not craftable.");
            return;
        }

        _registered = true;

        if (tailoring.CraftItems.SearchFor(typeof(ShadowsilkCloak)) == null)
        {
            // Misc (pinned's cloak group, DefTailoring: 1015283).
            var index = tailoring.AddCraft(typeof(ShadowsilkCloak), 1015283, "shadowsilk cloak", MinSkill, MaxSkill, typeof(Cloth), 1044455, 14, 1044287);
            tailoring.AddRes(index, typeof(ShadowsilkThread), "shadowsilk thread", 10, 1044253);
            tailoring.AddRecipe(index, ShadowsilkCloakRecipe);
        }

        if (alchemy.CraftItems.SearchFor(typeof(ThiefsSmokeBomb)) == null)
        {
            // The stock Smoke Bomb's group (DefAlchemy.cs:260: 1044537).
            var index = alchemy.AddCraft(typeof(ThiefsSmokeBomb), 1044537, "thief's smoke bomb", MinSkill, MaxSkill, typeof(Eggs), 1044477, 1, 1044253);
            alchemy.AddRes(index, typeof(EmberAsh), "ember ash", 3, 1044253);
            alchemy.AddRes(index, typeof(Whisperleaf), "whisperleaf", 1, 1044253);
            alchemy.AddRecipe(index, ThiefsSmokeBombRecipe);
        }

        if (tinkering.CraftItems.SearchFor(typeof(PickpocketsCharm)) == null)
        {
            // Jewelry (DefTinkering: 1044049).
            var index = tinkering.AddCraft(typeof(PickpocketsCharm), 1044049, "pickpocket's charm", MinSkill, MaxSkill, typeof(GoldIngot), "gold ingots", 2, 1044253);
            tinkering.AddRes(index, typeof(LuckyFang), "lucky fang", 1, 1044253);
            tinkering.AddRes(index, typeof(ShadowsilkThread), "shadowsilk thread", 2, 1044253);
            tinkering.AddRecipe(index, PickpocketsCharmRecipe);
        }
    }
}
