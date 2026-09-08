namespace Server.Items
{
    // ServUO: Items/Equipment/Weapons/RandomWand.cs (CC9 batch 3). Not an item: a static helper that two
    // ServUO NPCs (Braen, Canir) call to hand out a random wand. Loot.RandomWand exists unchanged in
    // pinned ModernUO (Misc/Loot.cs:338), so this is the same two-line wrapper.
    public static class RandomWand
    {
        public static BaseWand CreateWand() => CreateRandomWand();

        public static BaseWand CreateRandomWand() => Loot.RandomWand();
    }
}
