// Shattered Legacy Test Kit: our own resources, placed in a test character's bank beside pinned's Test Center fill.
//
// Content registers entries here (TestCenterKitEntries.cs holds ours; TESTKIT.md says how to add one). Placing is
// gated on TestCenter.Enabled, read at the moment of placing: on the live shard the registry may hold entries, but
// nothing is ever placed. See shard-migration/notes/cc-P9-test-center.md.

using System;
using System.Collections.Generic;
using Server.Items;
using Server.Logging;

namespace Server.Misc;

public static class TestCenterKit
{
    private static readonly ILogger logger = LogFactory.GetLogger(typeof(TestCenterKit));

    public const string BagName = "Shattered Legacy Test Kit";

    // Non-stackable entries are placed one item per unit, so an entry asking for thousands would bury the bank.
    public const int MaxNonStackable = 25;

    public sealed class Entry
    {
        internal Entry(Func<Item> factory, int amount)
        {
            Factory = factory;
            Amount = amount;
        }

        public Func<Item> Factory { get; }

        public int Amount { get; }
    }

    private static readonly List<Entry> _entries = [];

    public static IReadOnlyList<Entry> Entries => _entries;

    // Register in Configure (see TestCenterKitEntries). amount is the stack size for a stackable item, or the number of
    // items for a non-stackable one (capped at MaxNonStackable when placed).
    public static Entry Register(Func<Item> factory, int amount)
    {
        ArgumentNullException.ThrowIfNull(factory);

        if (amount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "a kit entry needs an amount of at least 1");
        }

        var entry = new Entry(factory, Math.Min(amount, 60000));
        _entries.Add(entry);
        return entry;
    }

    public static bool Unregister(Entry entry) => _entries.Remove(entry);

    // Creation and [TCFill both land here. Returns the bag placed, or null when nothing was placed.
    // Never throws: a broken entry is logged and skipped, because this runs inside character creation.
    public static Bag PlaceKit(Mobile m)
    {
        if (!TestCenter.Enabled || m?.BankBox == null || _entries.Count == 0)
        {
            return null;
        }

        var bag = new Bag { Name = BagName };

        for (var i = 0; i < _entries.Count; i++)
        {
            AddEntry(bag, _entries[i]);
        }

        m.BankBox.DropItem(bag);
        return bag;
    }

    private static void AddEntry(Bag bag, Entry entry)
    {
        Item first;

        try
        {
            first = entry.Factory();
        }
        catch (Exception e)
        {
            logger.Error(e, "Test kit: an entry's factory threw; entry skipped");
            return;
        }

        if (first == null)
        {
            logger.Warning("Test kit: an entry's factory returned null; entry skipped");
            return;
        }

        if (first.Stackable)
        {
            first.Amount = entry.Amount;
            bag.DropItem(first);
            return;
        }

        var count = entry.Amount;

        if (count > MaxNonStackable)
        {
            logger.Warning(
                "Test kit: {Type} is not stackable and asks for {Amount}; placing {Cap}",
                first.GetType().Name,
                count,
                MaxNonStackable
            );
            count = MaxNonStackable;
        }

        bag.DropItem(first);

        for (var i = 1; i < count; i++)
        {
            try
            {
                var item = entry.Factory();

                if (item != null)
                {
                    bag.DropItem(item);
                }
            }
            catch (Exception e)
            {
                logger.Error(e, "Test kit: {Type} factory threw on item {Index}; stopping this entry", first.GetType().Name, i + 1);
                return;
            }
        }
    }

    // [TCFill: pinned's fill, then the kit, keeping the caller's caps.
    //
    // FillBankAOS SETS the power-scroll skills' caps to 120 and StatCap to 250 (TestCenter.cs:226-231). On this shard
    // ClusterFSkillCaps and ClusterFStatCaps hold them at 200 (300 until cc-P53) and 1500, so on an existing character pinned's fill would
    // LOWER them until the next login. Neither setter clamps the skill or stat values (Skills.cs:205-219,
    // Mobile.cs:1731-1743), so keeping the higher of before and after is enough, and needs no patch.
    public static void RefillBank(Mobile m)
    {
        if (!TestCenter.Enabled || m == null)
        {
            return;
        }

        var skills = m.Skills;
        var caps = new int[skills.Length];

        for (var i = 0; i < caps.Length; i++)
        {
            caps[i] = skills[i].CapFixedPoint;
        }

        var statCap = m.StatCap;

        TestCenter.FillBankbox(m);

        for (var i = 0; i < caps.Length; i++)
        {
            if (skills[i].CapFixedPoint < caps[i])
            {
                skills[i].CapFixedPoint = caps[i];
            }
        }

        if (m.StatCap < statCap)
        {
            m.StatCap = statCap;
        }

        PlaceKit(m);
    }
}
