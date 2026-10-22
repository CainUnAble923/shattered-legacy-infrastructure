// cc-P368: the shard's own music tracks, one per client music ID. Notes: shard-migration
// notes/cc-P368-added-music-areas-monsters-bosses.md.
//
// A track here is one line the launcher adds to a copy of the player's Music\Digital\Config.txt ("<id> <file>,loop") and
// one mp3 the player package ships as app\music\<file>.mp3. The package's copy of this list is
// player-package\vendor\shattered-legacy-music\registry.csv (the same IDs, files and loop flags; the package test
// Music-Overrides.Tests.ps1 holds the two the same, the way the art records are held to registry.csv).
//
// IDs. Stock music is 0 to 104: MusicName.OldUlt01 to TheVesperMist (Server/Regions/Region.cs:13-119 at d4531cd9), and the
// client's Config.txt ends at "104 DungeonOutpost.mp3,loop". No stock ID is ever reassigned; ours start at 105. TazUO
// refuses an ID of 150 or more (AudioManager.PlayMusic: music >= Constants.MAX_MUSIC_DATA_INDEX_COUNT, which is 150, at
// client pin 3212623f), so 105 to 149 is the room: 45 tracks before a client patch is needed. Both ends are constants
// here and the tests hold every track inside them.
//
// Empty on purpose (2026-10-21): no track in Chase's Music Room notes (docs\music\music-notes.json) is marked Use yet.
// Adding one: copy the mp3 into the package's tracks\ folder with its Add-Track.ps1 (which writes the registry row and
// the SHA-256), then add the same row here, then its assignment in ShardMusicTable. Never reuse or renumber an ID.

#nullable enable

namespace Server.Music;

/// <summary>One shipped track: its client music ID, the file stem the package ships (app\music\&lt;File&gt;.mp3), whether the
/// client loops it, and the credit.</summary>
public sealed record ShardMusicTrack(int Id, string File, bool Loop, string Title, string Author);

public static class ShardMusicTracks
{
    /// <summary>The first ID after the stock list (104 is the last stock track).</summary>
    public const int FirstId = 105;

    /// <summary>The last ID TazUO plays (its MAX_MUSIC_DATA_INDEX_COUNT is 150).</summary>
    public const int LastId = 149;

    /// <summary>The last stock music ID (MusicName.TheVesperMist, Config.txt line 104).</summary>
    public const int LastStockId = 104;

    public static readonly ShardMusicTrack[] All = [];

    public static ShardMusicTrack? Find(int id)
    {
        foreach (var t in All)
        {
            if (t.Id == id)
            {
                return t;
            }
        }

        return null;
    }

    /// <summary>True for an ID in our range, whether or not a track is shipped for it yet.</summary>
    public static bool IsOurs(int id) => id >= FirstId && id <= LastId;
}
