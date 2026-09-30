using System;
using System.Collections.Generic;

namespace Server.Engines.Craft
{
    public enum CraftMarkOption
    {
        MarkItem,
        DoNotMark,
        PromptForMark
    }

    public class CraftContext
    {
        public CraftContext()
        {
            Items = new List<CraftItem>();
            LastResourceIndex = -1;
            LastResourceIndex2 = -1;
            LastGroupIndex = -1;
        }

        public List<CraftItem> Items { get; }

        public int LastResourceIndex { get; set; }

        public int LastResourceIndex2 { get; set; }

        public int LastGroupIndex { get; set; }

        public bool DoNotColor { get; set; }

        public CraftMarkOption MarkOption { get; set; }

        // Shattered Legacy, cc-P18 (F-1): Craft X. The run in progress, if any (ClusterFCraftRun.cs), and the
        // two toggles, remembered per player per craft system until the server restarts (not saved).
        public MakeXRun Run { get; set; }

        // The last run that ended, with its counts and summary.
        public MakeXRun LastRun { get; set; }

        public bool MakeXCountAttempts { get; set; }

        public bool MakeXExceptionalOnly { get; set; }

        // The two make-X patches (CraftGump-MakeXClear, CraftGumpItem-MakeX) clear these three when the player
        // starts a single craft by hand. Kept for them: clearing RepeatItem ends the run in progress.
        public int RepeatCount
        {
            get => Run?.Remaining ?? 0;
            set { }
        }

        public CraftItem RepeatItem
        {
            get => Run?.Item;
            set
            {
                if (value == null)
                {
                    ClusterFCraftRun.End(this, MakeXStop.OtherCraft);
                }
            }
        }

        public Type RepeatTypeRes
        {
            get => Run?.TypeRes;
            set { }
        }

        public CraftItem LastMade
        {
            get
            {
                if (Items.Count > 0)
                {
                    return Items[0];
                }

                return null;
            }
        }

        public void OnMade(CraftItem item)
        {
            Items.Remove(item);

            if (Items.Count == 10)
            {
                Items.RemoveAt(9);
            }

            Items.Insert(0, item);
        }
    }
}
