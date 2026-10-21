// ServUO: Services/Revamped Dungeons/Covetous Void Spawn/Items/VoidPoolRewards.cs (cc-P194).
//
// Vela's reward list (points cost per row is ServUO's) and the void artifact roll (one of five, to a random
// damager of a void spawn or of Cora).
//
// Left for the next prompt, and so NOT in the list yet (notes/cc-P194-port-wrong-and-covetous-void.md): the
// eight Harvest Maps (250 to 2000), the four Smelter's and four Woodsman's Talismans (50 to 500) and the Magical
// Fish Finder (250); each needs a hook into pinned Mining, Lumberjacking, ore smelting or the High Seas fishing
// table, and a reward that costs points and does nothing would be worse than one not yet sold. Gargish Hephaestus
// (2000) needs a LargePlateShield base pinned does not have. The cauldron deeds, the temporary forge deed and the
// four wearable artifacts are sold at ServUO's prices.

using System.Collections.Generic;
using Server.Items;

namespace Server.Engines.VoidPool;

public static class VoidPoolRewards
{
    public static List<CollectionItem> Rewards { get; private set; }

    public static void Initialize()
    {
        Rewards = new List<CollectionItem>
        {
            new(typeof(CauldronOfTransmutationDeed), 0x9ED, 1152656, (int)CraftResource.Bronze, 100),   // Bronze
            new(typeof(CauldronOfTransmutationDeed), 0x9ED, 1152657, (int)CraftResource.Gold, 200),     // Gold
            new(typeof(CauldronOfTransmutationDeed), 0x9ED, 1152658, (int)CraftResource.Agapite, 500),  // Agapite
            new(typeof(CauldronOfTransmutationDeed), 0x9ED, 1152659, (int)CraftResource.Verite, 1000),  // Verite

            new(typeof(CauldronOfTransmutationDeed), 0x9ED, 1152660, (int)CraftResource.AshWood, 100),    // Ash
            new(typeof(CauldronOfTransmutationDeed), 0x9ED, 1152661, (int)CraftResource.YewWood, 200),    // Yew
            new(typeof(CauldronOfTransmutationDeed), 0x9ED, 1152662, (int)CraftResource.Heartwood, 500),  // Heartwood
            new(typeof(CauldronOfTransmutationDeed), 0x9ED, 1152663, (int)CraftResource.Bloodwood, 1000), // Bloodwood

            new(typeof(TemporaryForgeDeed), 0xFB1, 1152682, 0, 250),

            new(typeof(BraceletOfProtection), 0x1086, 1152730, 1157, 1840),
            new(typeof(Hephaestus), 0x1B76, 1152909, 1910, 2000),
            new(typeof(BlightOfTheTundra), 0x26C2, 1152910, 1165, 2515),
            new(typeof(GargishBlightOfTheTundra), 0x090A, 1152910, 1165, 2515)
        };
    }

    public static Item DropRandomArtifact()
    {
        return Utility.Random(5) switch
        {
            0 => new PrismaticLenses(),
            1 => new Brightblade(),
            2 => new BraceletOfProtection(false),
            3 => new BlightOfTheTundra(false),
            4 => new Hephaestus(false),
            _ => null
        };
    }
}
