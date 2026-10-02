// ImbuingTableLabelVerification.cs
//
// cc-P29 Part F: the Artificers guildmaster's "Imbuing Table" context entry carries cliloc 1114267, "Imbue Item"
// in the client's Cliloc.enu (7.0.114.40 and EA's 7.0.117.0, read by notes/cc-P29-tools/cliloc.py, a port of
// pinned's own reader). It was 6277, sent as 3006277, which the client shows as "Salvage Ingots". Notes in
// shard-migration notes/cc-P29-defect-batch-2.md, Part F.
//
// Fact: the entry's number on the wire is 1114267, not a legacy 3000000-range number.

using System;
using System.Reflection;
using Server;
using Server.ContextMenus;
using Server.Mobiles;
using Xunit;

namespace ShatteredLegacy.Tests;

public class ImbuingTableLabelVerification
{
    [Fact]
    public void TheImbuingTableEntrySaysImbueItem()
    {
        var type = typeof(ArtificersGuildmaster).GetNestedType("ImbueTableEntry", BindingFlags.NonPublic);
        Assert.NotNull(type);

        var entry = (ContextMenuEntry)Activator.CreateInstance(type, nonPublic: true);
        Assert.Equal(1114267, entry.Number); // "Imbue Item"
        Assert.Equal(5, entry.Range);
    }
}
