// cc-P269: ServUO pub57 Scripts/Services/Expansions/High Seas/Persistence.cs -> ours, High Seas fishing, second half.
// What High Seas keeps outside the world's items and mobiles: the charybdis spawner and the players' standings with the
// fish mongers (PlayerFishingEntry). ServUO's file also carried the Sea Market's boat restriction and the forged
// pardons; those systems are not ported, so nothing of theirs is written.
//
// Saved through pinned's GenericPersistence (Server/Serialization/GenericPersistence.cs) under "HighSeas", after the
// world's mobiles and items (priority 10, as ClusterFSeasonalState), so the saved Charybdis reference resolves.
// Versioned; an unknown version fails loudly.

using System.IO;
using Server.Engines.Quests;
using Server.Mobiles;

namespace Server.Items;

public class HighSeasPersistence : GenericPersistence
{
    public const int CurrentVersion = 0;

    private static HighSeasPersistence _instance;

    public HighSeasPersistence() : base("HighSeas", 10)
    {
    }

    public static void Configure()
    {
        _instance ??= new HighSeasPersistence();
    }

    public override void Serialize(IGenericWriter writer)
    {
        writer.WriteEncodedInt(CurrentVersion);
        CharydbisSpawner.SpawnInstance.Serialize(writer);
        PlayerFishingEntry.SaveAll(writer);
    }

    public override void Deserialize(IGenericReader reader)
    {
        var version = reader.ReadEncodedInt();

        if (version is < 0 or > CurrentVersion)
        {
            throw new InvalidDataException($"HighSeasPersistence version {version} is not one this build reads (0 to {CurrentVersion}).");
        }

        var spawner = new CharydbisSpawner();
        spawner.Deserialize(reader);
        CharydbisSpawner.SpawnInstance = spawner;
        PlayerFishingEntry.LoadAll(reader);
    }
}
