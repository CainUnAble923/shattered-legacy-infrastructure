// cc-P264, companions B1: lineage ids. Every companion gets one at creation, and no id is ever handed out twice, even
// after the companion is deleted or the server restarts (P41 section 8: a Serial can be reissued after a restart, and a
// Mobile reference reads back null once the parent is gone). Parents are recorded by lineage id for the same reason.
//
// The counter lives in its own save file (Saves/Companions, pinned's GenericPersistence). Mobiles load first (priority
// 1, World.cs) and each companion reports its id as it loads (Observe), so a lost or older counter file can never
// hand out an id a live companion already holds: the counter is the higher of the file and the highest id seen.
// The lineage registry that outlives deletion (names, parents, generation) is breeding's, in its own chat.

#nullable enable

using System;
using System.IO;

namespace Server.Companions;

public class CompanionLineage : GenericPersistence
{
    // 0: cc-P264.
    public const int CurrentVersion = 0;

    private static CompanionLineage? _instance;
    private static long _next = 1;

    public CompanionLineage() : base("Companions", 10)
    {
    }

    public static CompanionLineage Instance => _instance ??= new CompanionLineage();

    public static void Configure() => _ = Instance;

    /// <summary>The id the next companion will get.</summary>
    public static long PeekNext => _next;

    /// <summary>A new lineage id, never handed out before.</summary>
    public static long NextId() => _next++;

    /// <summary>A loaded companion's id: the counter moves past it.</summary>
    public static void Observe(long id)
    {
        if (id >= _next)
        {
            _next = id + 1;
        }
    }

    /// <summary>For tests: the counter as a fresh server would start it.</summary>
    internal static void ResetForTests(long next = 1) => _next = next;

    public override void Serialize(IGenericWriter writer)
    {
        writer.WriteEncodedInt(CurrentVersion);
        writer.Write(_next);
    }

    public override void Deserialize(IGenericReader reader)
    {
        var version = reader.ReadEncodedInt();
        if (version is < 0 or > CurrentVersion)
        {
            throw new InvalidDataException(
                $"CompanionLineage version {version} is not one this build reads (0 to {CurrentVersion})."
            );
        }

        _next = Math.Max(_next, reader.ReadLong());
    }
}
