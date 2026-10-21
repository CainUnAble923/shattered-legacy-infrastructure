// cc-P199: ServUO pub57 Scripts/Items/Resource/IGem.cs. An empty marker interface. In ServUO the eight classic gems
// (Amethyst, Citrine, Diamond, Emerald, Ruby, Sapphire, StarSapphire, Tourmaline) implement it, and its one reader is
// Mining.SendSuccessTo (ServUO Services/Harvest/Mining.cs:239), which sends 1112233 for a gem dug from a vein.
//
// Here nothing implements it: those gems are pinned's and are not edited, and pinned's Mining does not read it. It is
// ported so that later ports naming IGem compile. Harmless: it declares no members.

namespace Server
{
    public interface IGem
    {
    }
}
