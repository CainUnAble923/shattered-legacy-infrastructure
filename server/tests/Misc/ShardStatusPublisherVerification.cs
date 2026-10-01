// The shard's public status file: shape, who counts, cap and order, and the atomic write.
//
// server/tests/<path> is mirrored into Projects/UOContent.Tests/Tests/<path> by
// docker/uo/apply-patches.sh and run as a gate by docker/uo/build.sh.
//
// The file is served to the public at /api/status.json (docker/uo-status/README.md), so the
// shape fact asserts the exact key set at every level: a field added here without the README
// changing first fails the build. The filtering fact drives real test NetStates of the same kind
// NetState.Instances holds, because "in the world" is decided per connection, not per mobile.
// The write fact is the one nginx depends on: see its own comment.
//
// All four were watched red against a stub before the implementation went in. See
// shard-migration/notes/cc-P6-status-publisher.md.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Server;
using Server.Misc;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class ShardStatusPublisherVerification
{
    private static string[] Keys(JsonElement e) => e.EnumerateObject().Select(p => p.Name).ToArray();

    private static DateTime ParseUtc(JsonElement e)
    {
        Assert.Equal(JsonValueKind.String, e.ValueKind);
        var t = DateTime.ParseExact(
            e.GetString()!,
            "yyyy-MM-dd'T'HH:mm:ss'Z'",
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal
        );
        Assert.Equal(DateTimeKind.Utc, t.Kind);
        return t;
    }

    [Fact]
    public void TheDocumentIsSchemaOneWithEveryFieldTypedAndNothingElse()
    {
        var generated = new DateTime(2026, 9, 29, 17, 30, 0, DateTimeKind.Utc);
        var started = generated.AddSeconds(-98765);
        var saved = generated.AddMinutes(-10);

        var bytes = ShardStatusPublisher.Render(generated, started, 98765, 3, ["Bram", "Cain", "\u00c9lowen"], saved, "2026.09.30.2");

        // ASCII only, whatever a character is called: the encoder escapes the E-acute.
        Assert.All(bytes, b => Assert.True(b < 0x80, $"non-ASCII byte 0x{b:X2}"));

        using var doc = JsonDocument.Parse(bytes);
        var root = doc.RootElement;

        Assert.Equal(new[] { "schema", "generatedAt", "shard", "players", "world" }, Keys(root));
        Assert.Equal(JsonValueKind.Number, root.GetProperty("schema").ValueKind);
        Assert.Equal(1, root.GetProperty("schema").GetInt32());
        Assert.Equal(generated, ParseUtc(root.GetProperty("generatedAt")));

        var shard = root.GetProperty("shard");
        Assert.Equal(new[] { "name", "startedAt", "uptimeSeconds", "version" }, Keys(shard));
        Assert.Equal("2026.09.30.2", shard.GetProperty("version").GetString()); // F-15 (cc-P22)
        Assert.Equal("Shattered Legacy", shard.GetProperty("name").GetString());
        Assert.Equal(started, ParseUtc(shard.GetProperty("startedAt")));
        Assert.Equal(98765, shard.GetProperty("uptimeSeconds").GetInt64());

        var players = root.GetProperty("players");
        Assert.Equal(new[] { "count", "names" }, Keys(players));
        Assert.Equal(3, players.GetProperty("count").GetInt32());
        Assert.Equal(JsonValueKind.Array, players.GetProperty("names").ValueKind);
        Assert.Equal(
            new[] { "Bram", "Cain", "\u00c9lowen" },
            players.GetProperty("names").EnumerateArray().Select(n => n.GetString()).ToArray()
        );

        var world = root.GetProperty("world");
        Assert.Equal(new[] { "lastSaveAt" }, Keys(world));
        Assert.Equal(saved, ParseUtc(world.GetProperty("lastSaveAt")));

        // Before the first save of a process the time is unknown: the key stays, the value is null.
        using var unsaved = JsonDocument.Parse(ShardStatusPublisher.Render(generated, started, 0, 0, [], null));
        var lastSave = unsaved.RootElement.GetProperty("world").GetProperty("lastSaveAt");
        Assert.Equal(JsonValueKind.Null, lastSave.ValueKind);
        // A build with no version says so with null, not a missing key (F-15).
        Assert.Equal(JsonValueKind.Null, unsaved.RootElement.GetProperty("shard").GetProperty("version").ValueKind);
        Assert.Empty(unsaved.RootElement.GetProperty("players").GetProperty("names").EnumerateArray());
    }

    private static PlayerMobile InWorld(string name)
    {
        var pm = new PlayerMobile { Player = true, Name = name };
        pm.MoveToWorld(new Point3D(1000, 1000, 0), Map.Trammel);
        return pm;
    }

    [Fact]
    public void StaffHiddenAndCharacterlessConnectionsAreNeitherCountedNorNamed()
    {
        var player = InWorld("Visible");

        var staff = InWorld("Staffer");
        staff.AccessLevel = AccessLevel.GameMaster;

        var hidden = InWorld("Hider");
        hidden.Hidden = true;

        // Attach connections only after the state is set, as a login would find it.
        using var nsPlayer = PacketTestUtilities.CreateTestNetState();
        using var nsStaff = PacketTestUtilities.CreateTestNetState();
        using var nsHidden = PacketTestUtilities.CreateTestNetState();
        using var nsSelect = PacketTestUtilities.CreateTestNetState(); // at character select: no Mobile

        var pairs = new (PlayerMobile Mobile, NetState State)[] { (player, nsPlayer), (staff, nsStaff), (hidden, nsHidden) };

        try
        {
            foreach (var (m, ns) in pairs)
            {
                m.NetState = ns;
                ns.Mobile = m; // PlayCharacter sets both (IncomingAccountPackets.cs:261-262)
            }

            NetState[] states = [nsPlayer, nsStaff, nsHidden, nsSelect];

            // These are the same objects production enumerates, not stand-ins for them.
            Assert.All(states, ns => Assert.Contains(ns, NetState.Instances));
            Assert.Null(nsSelect.Mobile);

            var (count, names) = ShardStatusPublisher.Players(ShardStatusPublisher.InWorld(states));

            Console.WriteLine($"count={count} names=[{string.Join(", ", names)}]");
            Assert.Equal(1, count);
            Assert.Equal(new[] { "Visible" }, names);
        }
        finally
        {
            foreach (var (m, _) in pairs)
            {
                m.NetState = null;
                m.Delete();
            }
        }
    }

    [Fact]
    public void SixtyPlayersCountSixtyAndNameTheFirstFiftyCaseInsensitively()
    {
        // Alternating case, so an ordinal sort (every capital before every lower-case letter) and a
        // case-insensitive one give different lists. Shuffled with a fixed seed.
        var all = Enumerable.Range(0, 60).Select(i => (i % 2 == 0 ? "p" : "P") + i.ToString("D2")).ToArray();
        var rng = new Random(6);
        var shuffled = all.OrderBy(_ => rng.Next()).ToArray();
        var mobiles = shuffled.Select(n => new PlayerMobile { Player = true, Name = n }).ToList();

        var twins = new[] { new PlayerMobile { Player = true, Name = "Bram" }, new PlayerMobile { Player = true, Name = "bram" } };

        try
        {
            var (count, names) = ShardStatusPublisher.Players(mobiles);

            Assert.Equal(60, count);
            Assert.Equal(50, names.Count);
            Assert.Equal(all.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).Take(50).ToArray(), names);
            Assert.Equal("p00", names[0]);
            Assert.Equal("P49", names[^1]);

            // Two characters, one name: both counted, one name.
            var (twinCount, twinNames) = ShardStatusPublisher.Players(twins);
            Assert.Equal(2, twinCount);
            Assert.Single(twinNames);
        }
        finally
        {
            foreach (var m in mobiles.Concat(twins))
            {
                m.Delete();
            }
        }
    }

    // nginx opens status.json and reads it. If the publisher rewrote the file in place, a read that
    // overlapped the write would get part of one version and part of the other. Replacing it by
    // rename means the open file is never modified: a reader that opened the old one keeps reading
    // the whole old one, and the next open gets the whole new one. That is what this asserts, with a
    // reader held open across the write, because it is deterministic where a racing reader is not.
    [Fact]
    public void TheFileIsReplacedWholeNeverRewrittenInPlaceAndLeavesNoTemp()
    {
        var root = Path.Combine(Path.GetTempPath(), "sl-status-" + Guid.NewGuid().ToString("N"));
        var dir = Path.Combine(root, "status"); // does not exist yet: the publisher creates it
        var path = Path.Combine(dir, "status.json");

        var oldBytes = Encoding.ASCII.GetBytes("{\"v\":\"old\",\"pad\":\"" + new string('o', 64 * 1024) + "\"}");
        var newBytes = Encoding.ASCII.GetBytes("{\"v\":\"new\",\"pad\":\"" + new string('n', 48 * 1024) + "\"}");

        try
        {
            ShardStatusPublisher.WriteAtomically(path, oldBytes);
            Assert.Equal(oldBytes, File.ReadAllBytes(path));

            byte[] seenByOpenReader;
            using (var reader = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                var head = new byte[16];
                Assert.Equal(16, reader.Read(head, 0, head.Length)); // mid-read when the write lands

                ShardStatusPublisher.WriteAtomically(path, newBytes);

                using var rest = new MemoryStream();
                rest.Write(head, 0, head.Length);
                reader.CopyTo(rest);
                seenByOpenReader = rest.ToArray();
            }

            Console.WriteLine($"open reader saw {seenByOpenReader.Length} bytes; old={oldBytes.Length} new={newBytes.Length}");
            Assert.Equal(oldBytes, seenByOpenReader);
            Assert.Equal(newBytes, File.ReadAllBytes(path));

            Assert.False(File.Exists(path + ".tmp"), "status.json.tmp left behind");
            Assert.Equal(new[] { "status.json" }, Directory.GetFiles(dir).Select(Path.GetFileName).ToArray());
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
