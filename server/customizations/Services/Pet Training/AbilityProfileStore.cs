// cc-P121: where ability profiles live (Chase's decision 2a, 2026-10-15). ServUO keeps a creature's AbilityProfile in a
// BaseCreature field and writes it in BaseCreature's own save (ServUO BaseCreature.cs:2591-2606, read at :2945-2954).
// Here it is a side table we own, keyed by the creature, in its own save file (Saves/PetAbilityProfiles, pinned's
// GenericPersistence, the shape ImbueCustomStore uses), so BaseCreature's save format and version are untouched and a
// pinned bump cannot collide with it.
//
//  - A creature's entry is removed when it is deleted (CreatureDeletedEvent). Stabling internalises a creature and
//    transfer changes its master; neither deletes it, so its profile stays.
//  - A profile holding only natural abilities (AbilityProfile.IsEmpty) is not written: a stock creature asked for its
//    profile costs nothing at save.
//  - A world saved before this file existed has no PetAbilityProfiles.bin; GenericPersistence.Deserialize returns
//    without reading when the file is absent (Server/Serialization/GenericPersistence.cs:80-88), so it loads empty.
//  - Cost: the profile lives beside the creature, not in it. A world restored without this save file loses stored
//    profiles; constructors re-set the wild ones only for new spawns.

using System.Collections.Generic;
using System.IO;
using ModernUO.CodeGeneratedEvents;

namespace Server.Mobiles;

public class AbilityProfileStore : GenericPersistence
{
    // 0: cc-P121.
    public const int CurrentVersion = 0;

    private static AbilityProfileStore _instance;

    private static readonly Dictionary<BaseCreature, AbilityProfile> _profiles = new();

    public AbilityProfileStore() : base("PetAbilityProfiles", 10)
    {
    }

    public static void Configure()
    {
        _instance ??= new AbilityProfileStore();
    }

    /// <summary>The registered persistence (after Configure); tests drive its Serialize/Deserialize directly.</summary>
    public static AbilityProfileStore Instance => _instance;

    /// <summary>Stored profiles, natural-only ones included until the creature is deleted.</summary>
    public static int Count => _profiles.Count;

    public static bool TryGet(BaseCreature bc, out AbilityProfile profile)
    {
        if (bc != null)
        {
            return _profiles.TryGetValue(bc, out profile);
        }

        profile = null;
        return false;
    }

    public static AbilityProfile Get(BaseCreature bc, bool create)
    {
        if (bc == null)
        {
            return null;
        }

        if (!_profiles.TryGetValue(bc, out var profile) && create && !bc.Deleted)
        {
            _profiles[bc] = profile = new AbilityProfile(bc);
        }

        return profile;
    }

    [OnEvent(nameof(CreatureEvents.CreatureDeletedEvent))]
    public static void OnCreatureDeleted(BaseCreature bc) => _profiles.Remove(bc);

    public override void Serialize(IGenericWriter writer)
    {
        var live = new List<KeyValuePair<BaseCreature, AbilityProfile>>();

        foreach (var pair in _profiles)
        {
            if (!pair.Key.Deleted && !pair.Value.IsEmpty)
            {
                live.Add(pair);
            }
        }

        writer.WriteEncodedInt(CurrentVersion);
        writer.WriteEncodedInt(live.Count);

        foreach (var (creature, profile) in live)
        {
            writer.Write(creature);
            profile.Serialize(writer);
        }
    }

    public override void Deserialize(IGenericReader reader)
    {
        var version = reader.ReadEncodedInt();

        if (version is < 0 or > CurrentVersion)
        {
            throw new InvalidDataException(
                $"AbilityProfileStore version {version} is not one this build reads (0 to {CurrentVersion})."
            );
        }

        _profiles.Clear();

        var count = reader.ReadEncodedInt();

        for (var i = 0; i < count; i++)
        {
            var creature = reader.ReadEntity<BaseCreature>();
            var profile = new AbilityProfile(creature);
            profile.Deserialize(reader); // read in full either way, so the next entry starts in the right place

            if (creature is { Deleted: false })
            {
                _profiles[creature] = profile;
            }
        }
    }
}
