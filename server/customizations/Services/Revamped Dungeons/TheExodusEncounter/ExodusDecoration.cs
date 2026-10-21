// ServUO: Data/Decoration/Exodus/exodusdungeon.cfg (cc-P195), the file [GenExodusNexus runs through Decorate on
// Ilshenar: 39 ExodusNexus machines and one Keypunch Reader, carried as data (apply-patches.sh installs .cs files
// only, the cc-P193 and S5 finding; cc-P194 carried Wrong's maze the same way). The generator places them; a re-run
// deletes first, so nothing is skipped or doubled.

using System.Collections.Generic;
using Server.Items;

namespace Server.Engines.Exodus;

public static class ExodusDecoration
{
    /// <summary>"ExodusNexus 0x1" in the cfg: 39 tiles, each a Nexus machine with a fresh puzzle.</summary>
    public static readonly Point3D[] NexusPoints =
    {
        new(2059, 171, -27), new(1995, 51, -28), new(1995, 83, -27), new(2011, 67, -27), new(2027, 59, -27),
        new(2043, 43, -28), new(2059, 59, -28), new(2043, 75, -28), new(2043, 99, -27), new(2067, 91, -28),
        new(2059, 115, -27), new(2067, 139, -28), new(2043, 155, -27), new(2043, 187, -28), new(2027, 171, -27),
        new(2011, 163, -28), new(1995, 147, -28), new(1995, 179, -27), new(1963, 179, -27), new(1947, 163, -27),
        new(1915, 163, -28), new(1931, 179, -28), new(1963, 146, -28), new(1947, 139, -28), new(1947, 123, -28),
        new(1923, 139, -28), new(1923, 115, -27), new(1923, 91, -28), new(1947, 91, -27), new(1963, 83, -28),
        new(1947, 67, -28), new(1963, 51, -28), new(1931, 51, -28), new(1915, 67, -27), new(1947, 107, -28),
        new(2011, 131, -27), new(2027, 115, -28), new(2043, 131, -28), new(2011, 99, -28)
    };

    /// <summary>"KeypunchReader 0x280D" in the cfg.</summary>
    public static readonly Point3D KeypunchReaderLocation = new(1987, 88, -28);

    public static List<Item> Generate(Map map)
    {
        var placed = new List<Item>(NexusPoints.Length + 1);

        foreach (var p in NexusPoints)
        {
            var nexus = new ExodusNexus();
            WeakEntityCollection.Add(ExodusEncounter.CollectionKey, nexus);
            nexus.MoveToWorld(p, map);
            placed.Add(nexus);
        }

        var reader = new KeypunchReader();
        WeakEntityCollection.Add(ExodusEncounter.CollectionKey, reader);
        reader.MoveToWorld(KeypunchReaderLocation, map);
        placed.Add(reader);

        return placed;
    }
}
