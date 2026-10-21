// cc-P199: ServUO pub57 Scripts/Items/Functional/SeedBox/SeedEntry.cs. One slot of a seed box: the stack of seeds it
// holds (shown with its type) and which of the three slot pictures (gumps 2183-2185) the box gump draws behind it.
//
// The name: the shard already has a SeedEntry, ClusterFNewHavenSeeder.SeedEntry (customizations/ClusterFNewHavenSeeder.cs
// :275), an internal class nested in the New Haven NPC seeder that records where an NPC is placed. It is not this
// type and nothing outside the seeder can name it; this one is Server.Engines.Plants.SeedEntry, ServUO's namespace, so
// the two never meet. Pinned has no SeedEntry.
//
// Differences from ServUO:
// - The reader constructor and Serialize take pinned's IGenericReader and IGenericWriter; the format is ServUO's
//   (version 0, the seed, the picture). Pinned's generator saves a list of these through those two members (the
//   AccountComment shape, pinned Accounting/Account.cs:66).
// - An internal empty constructor: SeedBox saves an empty slot (ServUO writes a 0 flag for it, SeedBox.cs:345-347) as
//   an entry with no seed and picture 0, since pinned's generated list writer has no null slots.

namespace Server.Engines.Plants
{
    public class SeedEntry
    {
        public Seed Seed { get; set; }
        public int Image { get; set; }

        internal SeedEntry()
        {
        }

        public SeedEntry(Seed seed)
        {
            Seed = seed;

            seed.ShowType = true;

            Image = Utility.Random(2183, 3);
        }

        public SeedEntry(IGenericReader reader)
        {
            reader.ReadInt(); // version

            Seed = reader.ReadEntity<Item>() as Seed;
            Image = reader.ReadInt();
        }

        public void Serialize(IGenericWriter writer)
        {
            writer.Write(0); // version

            writer.Write(Seed);
            writer.Write(Image);
        }
    }
}
