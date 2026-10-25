// cc-P610 (P533 U3): what keeps the tutorial's loans whole. Notes: shard-migration notes/cc-P610-fix-drafts-from-p533.md.
//
//   - TutorialLoanCloth and TutorialLoanLog: the loaned cloth and logs. They craft like any cloth or logs (the lesson's
//     skullcap and kindling), but scissors and an axe refuse them: cut into bandages or axed into boards they became
//     ordinary things the loan's return never saw (P523 #1). A stack lifted in part is split by Mobile.LiftItemDupe
//     into another of the same type and name, so the remainder refuses too.
//   - TutorialLoanBow and TutorialLoanArrow: the Archery lesson's bow fires its own arrows. A missed shot's recovery
//     (AmmoRecovery, by the bow's AmmoType) brings back loan arrows, which go when no lesson holds them; ordinary
//     arrows never came from the loan (P503 #2). Stock bows cannot fire loan arrows (they are not Arrow).
//   - TutorialLoanLedger: the loans that live on the player rather than in an item, the tithing points and the Hiding
//     base (Stealth reads the base), saved while a lesson holds them. Trials live in memory, so a restart lost the
//     record of what was lent and the player kept it (P503 #3, P291's Hiding loan in P514); once the world is up the
//     ledger gives back what is still out, once, keeping what the lesson spent or gained.
// Every loan item is also named "tutorial loaner: ..." and swept on load, as before (TutorialWingTrials.SweepLoaners).

#nullable enable

using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Items;
using Server.Mobiles;
using Server.SkillHandlers;

namespace Server.Tutorial
{
    [SerializationGenerator(0, false)]
    public partial class TutorialLoanCloth : Cloth, IScissorable
    {
        [Constructible]
        public TutorialLoanCloth(int amount = 1) : base(amount)
        {
        }

        bool IScissorable.Scissor(Mobile from, Scissors scissors)
        {
            from.SendMessage("This cloth is lent for the lesson's sewing. It cannot be cut.");
            return false;
        }

        [AfterDeserialization(false)]
        private void ClearAbandonedLesson() => Delete();
    }

    [SerializationGenerator(0, false)]
    public partial class TutorialLoanLog : Log
    {
        [Constructible]
        public TutorialLoanLog(int amount = 1) : base(amount)
        {
        }

        public override bool Axe(Mobile from, BaseAxe axe)
        {
            from.SendMessage("These logs are lent for the lesson's fletching. They cannot be cut into boards.");
            return false;
        }

        [AfterDeserialization(false)]
        private void ClearAbandonedLesson() => Delete();
    }

    [SerializationGenerator(0, false)]
    public partial class TutorialLoanBow : Bow
    {
        [Constructible]
        public TutorialLoanBow()
        {
        }

        public override Type AmmoType => typeof(TutorialLoanArrow);
        public override Item Ammo => new TutorialLoanArrow();

        [AfterDeserialization(false)]
        private void ClearAbandonedLesson() => Delete();
    }

    [SerializationGenerator(0, false)]
    public partial class TutorialLoanArrow : Item
    {
        [Constructible]
        public TutorialLoanArrow(int amount = 1) : base(0xF3F)
        {
            Stackable = true;
            Amount = amount;
            Name = TutorialWingTrials.LoanerPrefix + "arrow";
            LootType = LootType.Blessed;
        }

        public override double DefaultWeight => 0.1;

        // Recovered after a miss (AmmoRecovery makes a new one), or split from a stack: it stays only while a lesson
        // holds it (the trial adopts it), and goes otherwise. Checked after the add, once the item is placed.
        public override void OnAdded(IEntity parent)
        {
            base.OnAdded(parent);
            Timer.DelayCall(() => TutorialWingTrials.ClaimOrDrop(this));
        }

        [AfterDeserialization(false)]
        private void ClearAbandonedLesson() => Delete();
    }

    /// <summary>The loans that live on the player (tithing points, the Hiding base), saved while a lesson holds them.</summary>
    public static class TutorialLoanLedger
    {
        public sealed class Entry
        {
            public int TithingLoan;
            public int TithingBefore;
            public double HidingBefore = -1;
        }

        private static readonly Dictionary<Serial, Entry> _entries = new();
        private static GenericPersistence? _persistence;

        public static void Configure() => _persistence ??= new TutorialLoanLedgerPersistence();

        public static Entry? Find(PlayerMobile pm) => _entries.GetValueOrDefault(pm.Serial);

        public static void NoteTithing(PlayerMobile pm, int before, int loan)
        {
            var e = Open(pm);
            e.TithingBefore = before;
            e.TithingLoan = loan;
        }

        public static void NoteHiding(PlayerMobile pm, double before) => Open(pm).HidingBefore = before;

        /// <summary>The lesson gave its loans back: nothing is owed.</summary>
        public static void Settle(PlayerMobile pm) => _entries.Remove(pm.Serial);

        private static Entry Open(PlayerMobile pm)
        {
            if (!_entries.TryGetValue(pm.Serial, out var e))
            {
                _entries[pm.Serial] = e = new Entry();
            }

            return e;
        }

        /// <summary>After a restart: every loan still out, for a player with no lesson running, is given back once.</summary>
        public static int Reconcile()
        {
            var settled = 0;
            foreach (var (serial, e) in new List<KeyValuePair<Serial, Entry>>(_entries))
            {
                if (World.FindMobile(serial) is not PlayerMobile pm || pm.Deleted)
                {
                    _entries.Remove(serial);
                    continue;
                }

                if (TutorialWingTrials.Current(pm) != null)
                {
                    continue; // a lesson running now holds it
                }

                GiveBackTithing(pm, e.TithingBefore, e.TithingLoan);
                GiveBackHiding(pm, e.HidingBefore);
                _entries.Remove(serial);
                settled++;
            }

            return settled;
        }

        /// <summary>What the lesson spent came out of the loan first; only the rest of the loan is taken back.</summary>
        public static void GiveBackTithing(PlayerMobile pm, int before, int loan)
        {
            if (loan <= 0)
            {
                return;
            }

            var spent = Math.Clamp(before + loan - pm.TithingPoints, 0, loan);
            pm.TithingPoints = Math.Max(0, pm.TithingPoints - (loan - spent));
        }

        /// <summary>Back to the player's own Hiding base, keeping whatever the lesson gained above the lent requirement.</summary>
        public static void GiveBackHiding(PlayerMobile pm, double before)
        {
            if (before < 0)
            {
                return;
            }

            // Rounded to the tenth a skill base keeps: the sum can land a hair under it (6.1999...) and the base would
            // truncate a tenth of the player's gain away.
            var hiding = pm.Skills[SkillName.Hiding];
            hiding.Base = Math.Round(before + Math.Max(0, hiding.Base - Stealth.HidingRequirement), 1);
        }

        public static void Clear() => _entries.Clear();

        public static void Serialize(IGenericWriter writer)
        {
            writer.WriteEncodedInt(0); // version
            writer.WriteEncodedInt(_entries.Count);
            foreach (var (serial, e) in _entries)
            {
                writer.Write(serial);
                writer.Write(e.TithingLoan);
                writer.Write(e.TithingBefore);
                writer.Write(e.HidingBefore);
            }
        }

        public static void Deserialize(IGenericReader reader)
        {
            reader.ReadEncodedInt(); // version
            var count = reader.ReadEncodedInt();
            for (var i = 0; i < count; i++)
            {
                var serial = reader.ReadSerial();
                _entries[serial] = new Entry
                {
                    TithingLoan = reader.ReadInt(),
                    TithingBefore = reader.ReadInt(),
                    HidingBefore = reader.ReadDouble()
                };
            }
        }
    }

    public sealed class TutorialLoanLedgerPersistence : GenericPersistence
    {
        public TutorialLoanLedgerPersistence() : base("TutorialLoanLedger", 10)
        {
        }

        public override void Serialize(IGenericWriter writer) => TutorialLoanLedger.Serialize(writer);

        public override void Deserialize(IGenericReader reader) => TutorialLoanLedger.Deserialize(reader);
    }
}
