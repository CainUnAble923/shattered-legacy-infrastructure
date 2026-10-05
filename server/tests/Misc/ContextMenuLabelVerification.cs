// ContextMenuLabelVerification.cs
//
// cc-P56 Part C (bug-list D79, Chase 2026-10-05): context-menu entries the client showed with the wrong word. Each
// number is read from the client's own Cliloc.enu (EA's D:\UO\UltimaOnlineVanilla, 124,000 entries, with
// shard-migration notes/cc-P29-tools/cliloc.py). Pinned writes an entry's Number to the client as is, after adding
// 3,000,000 to a number at or below 0x7FFF (Server/ContextMenus/ContextMenuEntry.cs:39-48), so the Number is what the
// client looks up. Notes: shard-migration notes/cc-P56-smith-economy-and-labels.md, Part C.
//
// Facts:
//   C1. The pack mule's breed entry is 3006132 "Use" (no stock cliloc says "Breed"), not 3006131 "Close".
//   C2. The trash bag's dump entry is 1151316 "Clean Up Britannia", not 3006146 "Talk".
//   C3. The pet mimic's status entry is 3000132 "Status", not 3006146 "Talk"; its feed entry stays 3006135 "Eat".

using System;
using System.Linq;
using Server;
using Server.Collections;
using Server.ContextMenus;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class ContextMenuLabelVerification
{
    private readonly ITestOutputHelper _out;

    public ContextMenuLabelVerification(ITestOutputHelper output) => _out = output;

    private static readonly Point3D Spot = new(1480, 1770, 0);

    private static ContextMenuEntry[] Entries(IEntity target, Mobile from)
    {
        var list = PooledRefList<ContextMenuEntry>.Create();
        try
        {
            switch (target)
            {
                case Item item:
                    item.GetContextMenuEntries(from, ref list);
                    break;
                case Mobile mobile:
                    mobile.GetContextMenuEntries(from, ref list);
                    break;
            }

            var result = new ContextMenuEntry[list.Count];
            for (var i = 0; i < list.Count; i++)
            {
                result[i] = list[i];
            }

            return result;
        }
        finally
        {
            list.Dispose();
        }
    }

    private static (PlayerMobile Pm, NetState Ns) Player()
    {
        var pm = new PlayerMobile { Player = true };
        pm.AddItem(new Backpack());
        pm.MoveToWorld(Spot, Map.Trammel);
        var ns = PacketTestUtilities.CreateTestNetState();
        pm.NetState = ns;
        ns.Mobile = pm;
        return (pm, ns);
    }

    private static void Done(PlayerMobile pm, NetState ns)
    {
        foreach (var item in pm.Backpack.Items.ToList())
        {
            item.Delete();
        }

        pm.NetState = null;
        ns.Mobile = null;
        pm.Delete();
    }

    private string Show(ContextMenuEntry[] entries) =>
        string.Join(", ", entries.Select(e => $"{e.GetType().Name} {e.Number}"));

    // ---------------------------------------------------------------- C1

    [Fact]
    public void ThePackMulesBreedEntryIsNotClose()
    {
        var (pm, ns) = Player();
        var mule = new PackMule();
        try
        {
            mule.MoveToWorld(new Point3D(Spot.X + 1, Spot.Y, Spot.Z), Map.Trammel);
            mule.SetControlMaster(pm);

            var entries = Entries(mule, pm);
            _out.WriteLine($"pack mule: {Show(entries)}");

            var breed = Assert.Single(entries, e => e is PackMule.PackMuleBreedEntry);
            Assert.Equal(3006132, breed.Number); // "Use"
            Assert.DoesNotContain(entries, e => e.Number == 3006131); // "Close"
        }
        finally
        {
            mule.Delete();
            Done(pm, ns);
        }
    }

    // ---------------------------------------------------------------- C2

    [Fact]
    public void TheTrashBagsDumpEntryNamesCleanUpBritannia()
    {
        var (pm, ns) = Player();
        var bag = new TrashBag();
        try
        {
            pm.Backpack.DropItem(bag);

            var entries = Entries(bag, pm);
            _out.WriteLine($"trash bag: {Show(entries)}");

            var dump = Assert.Single(entries, e => e is TrashBag.DumpEntry);
            Assert.Equal(1151316, dump.Number); // "Clean Up Britannia"
            Assert.DoesNotContain(entries, e => e.Number == 3006146); // "Talk"
        }
        finally
        {
            bag.Delete();
            Done(pm, ns);
        }
    }

    // ---------------------------------------------------------------- C3

    [Fact]
    public void ThePetMimicsStatusEntryIsStatus()
    {
        var (pm, ns) = Player();
        var mimic = new PetMimic();
        try
        {
            pm.Backpack.DropItem(mimic);

            var entries = Entries(mimic, pm);
            _out.WriteLine($"pet mimic: {Show(entries)}");

            var status = Assert.Single(entries, e => e is PetMimic.CheckStatusEntry);
            Assert.Equal(3000132, status.Number); // "Status"
            Assert.DoesNotContain(entries, e => e.Number == 3006146); // "Talk"
            Assert.Contains(entries, e => e.Number == 3006135);       // "Eat", unchanged
        }
        finally
        {
            mimic.Delete();
            Done(pm, ns);
        }
    }
}
