// cc-P801: the New Haven jail, where the "Name colors, reputation, and guards" lesson (reputation.wing) now happens. Chase,
// 11-05: "It has you run to a blank spot near bard guild, then healer, then plaza, with info in system messages. It's easy
// to miss what you're supposed to learn." and "A jail would be a good location for this." Notes: shard-migration
// notes/cc-P801-name-colors-lesson.md; the plan he chose: notes/cc-P801-name-colors-lesson-plan.md.
//
// One place, four parts, in order, and a window (TutorialJailGump) that stays open and always says what is next:
//   1. Names: click the five people (TutorialJailPerson); each says what their color means.
//   2. The crate: take the marked pouch and your own name shows gray, then put it back and it shows blue again.
//   3. The ledger: your own karma, fame and murder counts, with what moves each.
//   4. Guards: a thief only you can see stands in the hall; say "guards", a guard flashes in and cuts him down.
//
// Nothing is real but the colors, by design (the prompt: nobody gets a real murder count, criminal timer or karma loss):
//   - Name colors come from one line at the top of MobileNotoriety (Notoriety-lesson-shown-color.patch, ShownNotoriety), so
//     the client draws them as it draws any name. Black Tam has no kills, so real guards leave him be.
//   - The gray is only what the trainee sees of themself (source == target), for as long as Trial.JailGray is set. Mobile.
//     Criminal is never touched, so no timer, no guards, and nobody else sees it.
//   - Nothing can harm the people: they are IsInvulnerable, which pinned's CanBeHarmful refuses for players and creatures alike,
//     so no attack counts as anything. They are not Blessed: TazUO paints a blessed mobile solid gold unless its name is yellow
//     (the first client walk).
//   - A click reaches OnAosSingleClick, not OnSingleClick, on this shard (the first client walk): they answer both.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using ModernUO.Serialization;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Network;

namespace Server.Tutorial
{
    /// <summary>cc-P801: a lesson person whose name shows a set color, whatever they are (the top of MobileNotoriety).</summary>
    public interface IShownNotoriety
    {
        int ShownNotoriety { get; }
    }

    public static class TutorialJail
    {
        public const string Key = "reputation.wing";
        public const string ThiefStep = "guards";
        public const string CrateStep = "crate";
        public const string LedgerStep = "ledger";

        /// <summary>One person in the jail: who, where (from the post), and what the window says once they are met.</summary>
        public sealed record Person(
            string Step, string Color, int Hue, int Noto, string Name, string Title, int X, int Y, Direction Facing, string Line, string Meaning);

        public static readonly Person[] People =
        [
            new("yellow", "Yellow", 0x35, Notoriety.Invulnerable, "Warden Hale", "the warden", 1, 5, Direction.East,
                "Yellow means nobody can harm me. Shopkeepers, guards and guides like me wear it.",
                "Cannot be harmed: shopkeepers, guards, guides."),
            new("blue", "Blue", 0x59, Notoriety.Innocent, "Mirren", "visiting her brother", 6, 4, Direction.North,
                "Blue is an honest name, and yours is blue too. Attack me and you are the criminal.",
                "Honest. Attack one and you are the criminal."),
            new("gray", "Gray", 0x3B2, Notoriety.CanBeAttacked, "Finn", "the pickpocket", 2, 2, Direction.South,
                "Gray is a criminal for now, or someone you may attack freely. It wears off in a couple of minutes.",
                "A criminal for now, or fair game to attack."),
            new("orange", "Orange", 0x90, Notoriety.Enemy, "Sarn", "the captured raider", 5, 2, Direction.South,
                "Orange is an enemy of your guild. You may fight me and not be a criminal for it.",
                "An enemy of your guild: fight them freely."),
            new("red", "Red", 0x22, Notoriety.Murderer, "Black Tam", "the murderer", 9, 2, Direction.South,
                "Red is a murderer: five murder counts. Anyone may kill me, and guards cut me down on sight in town.",
                "A murderer: five counts. Anyone may kill them.")
        ];

        public static readonly (string Id, string Label)[] Steps =
        [
            .. People.Select(p => (p.Step, $"Click the person with a {p.Color.ToLowerInvariant()} name")),
            (CrateStep, "Take from the marked crate, then put it back"),
            (LedgerStep, "Read the warden's ledger"),
            (ThiefStep, "Call the guards on the thief")
        ];

        public const string DoneText =
            "That is what real guards do: in a guarded town, say guards and any criminal or murderer near you is cut down at once. " +
            "Trammel stops most attacks on other players; Felucca does not. Next: double-click Rook for another lesson.";

        public static Point3D At(int x, int y, int z = 0) => new(HavenSpots.JailPost.X + x, HavenSpots.JailPost.Y + y, HavenSpots.JailFloorZ + z);

        /// <summary>Where the lesson arms: the hall, inside the door.</summary>
        public static Point2D Place => new(HavenSpots.JailPost.X + 5, HavenSpots.JailPost.Y + 6);

        public static Point3D Hall => At(5, 6);
        public static Point3D Door => At(5, 8);
        public static Point3D CrateSpot => At(9, 7);
        public static Point3D LedgerSpot => At(2, 5, 6);
        public static Point3D ThiefSpot => At(6, 6);
        public static Point3D GuardSpot => At(7, 6);

        /// <summary>The name color a lesson shows, or null for the ordinary rules (the patch's one call).</summary>
        public static int? ShownNotoriety(Mobile source, Mobile target) => target switch
        {
            IShownNotoriety shown => shown.ShownNotoriety,
            PlayerMobile pm when source == target && TutorialWingTrials.Current(pm) is { JailGray: true } => Notoriety.Criminal,
            _ => null
        };

        // -- Seeding: on every world load, only what is missing (the building guide, section 7) --

        public static void Configure()
        {
            if (!ServerConfiguration.GetOrUpdateSetting("tutorial.seedMissingOnLoad", true)) { return; }
            EventSink.WorldLoad += () => Timer.DelayCall(TimeSpan.Zero, () => Console.WriteLine(Seed(false)));
        }

        private static bool Has(Map map, int id, Point3D at)
        {
            foreach (var item in map.GetItemsAt(at.X, at.Y))
            {
                if (item.ItemID == id && item.Z == at.Z) { return true; }
            }

            return false;
        }

        private static bool HasOfType<T>(Map map, Point3D at) where T : Item
        {
            foreach (var item in map.GetItemsAt(at.X, at.Y))
            {
                if (item is T) { return true; }
            }

            return false;
        }

        public static string Seed(bool dryRun)
        {
            var map = HavenSpots.Map;
            var placed = 0;
            foreach (var (id, x, y, z) in TutorialJailLayout.Pieces)
            {
                var at = At(x, y, z);
                if (Has(map, id, at)) { continue; }
                placed++;
                if (dryRun) { continue; }
                var piece = new Static(id);
                if (id is 0x0B98 or 0x0BD2) { piece.Name = "The New Haven Jail"; }
                piece.MoveToWorld(at, map);
            }

            if (!HasOfType<BaseDoor>(map, Door))
            {
                placed++;
                if (!dryRun) { new MetalDoor(DoorFacing.WestCCW).MoveToWorld(Door, map); }
            }

            if (!HasOfType<TutorialJailCrate>(map, CrateSpot))
            {
                placed++;
                if (!dryRun) { new TutorialJailCrate().MoveToWorld(CrateSpot, map); }
            }

            if (!HasOfType<TutorialJailLedger>(map, LedgerSpot))
            {
                placed++;
                if (!dryRun) { new TutorialJailLedger().MoveToWorld(LedgerSpot, map); }
            }

            var met = new HashSet<int>();
            foreach (var m in map.GetMobilesInRange(Hall, 12))
            {
                if (m is TutorialJailPerson p) { met.Add(p.Role); }
            }

            for (var i = 0; i < People.Length; i++)
            {
                if (met.Contains(i)) { continue; }
                placed++;
                if (dryRun) { continue; }
                var person = new TutorialJailPerson(i);
                person.MoveToWorld(At(People[i].X, People[i].Y), map);
                person.Home = person.Location;
                person.RangeHome = 0;
            }

            return $"New Haven jail (cc-P801){(dryRun ? " dry run" : "")}: {(dryRun ? "would place" : "placed")} {placed} pieces.";
        }
    }

    public static partial class TutorialWingTrials
    {
        public const string WardenTakeLine = "Hold there! That pouch is marked. Your name has turned gray. Click yourself and see. Now put it back.";
        public const string WardenBackLine = "Good. Back in the crate, and your name is blue again. Read my ledger, on the table here.";
        public const string WardenThiefLine = "Thief! There, in my hall! Say guards, and be quick about it!";
        public const string GuardCallLine = "Halt, thief!";

        private static Trial? JailTrial(Mobile from) =>
            from is PlayerMobile pm && Current(pm) is { Key: TutorialJail.Key, Armed: true, Finished: false } trial ? trial : null;

        private static bool NamesDone(Trial trial) => TutorialJail.People.All(p => trial.Steps.Contains(p.Step));

        /// <summary>What the lesson window says to do now: every part ends with the next part said plainly.</summary>
        public static string JailNow(Trial trial)
        {
            if (trial.Finished) { return TutorialJail.DoneText; }
            if (!NamesDone(trial))
            {
                var left = TutorialJail.People.Where(p => !trial.Steps.Contains(p.Step)).Select(p => p.Color.ToLowerInvariant());
                return $"Click each person here once and read what they say. Still to meet: {string.Join(", ", left)}.";
            }

            if (trial.JailGray)
            {
                return "Your name is gray now. Click yourself to see it. No harm is done, this once. Double-click the marked crate to put the pouch back.";
            }

            if (!trial.Steps.Contains(TutorialJail.CrateStep))
            {
                return "You have met all five. Now see gray for yourself: double-click the marked crate in the corner of the hall and take the pouch.";
            }

            if (!trial.Steps.Contains(TutorialJail.LedgerStep))
            {
                return "Double-click the warden's ledger on his table to read your own karma, fame and murder counts.";
            }

            return "A thief is in the hall! Say guards out loud.";
        }

        /// <summary>A person was clicked: their color is ticked off.</summary>
        public static void JailMet(Mobile from, string step)
        {
            if (JailTrial(from) is { } trial) { Step(trial, step); }
        }

        private static TutorialJailPerson? Warden()
        {
            foreach (var m in Map.Trammel.GetMobilesInRange(TutorialJail.Hall, 12))
            {
                if (m is TutorialJailPerson { Role: 0 } warden) { return warden; }
            }

            return null;
        }

        private static void Overhead(PlayerMobile pm, string text, int hue)
        {
            if (pm.NetState is { } ns) { pm.PrivateOverheadMessage(MessageType.Regular, hue, false, text, ns); }
        }

        private static void SetGray(Trial trial, bool gray)
        {
            if (trial.JailGray == gray) { return; }
            trial.JailGray = gray;
            trial.Player.Delta(MobileDelta.Noto); // the client hears its own name's new color at once
        }

        /// <summary>The marked crate: takes the pouch, or puts it back.</summary>
        public static void JailCrate(Mobile from)
        {
            if (JailTrial(from) is not { } trial)
            {
                from.SendMessage("The crate is nailed shut. It is held for the warden's lesson.");
                return;
            }

            var pm = trial.Player;
            if (trial.JailGray)
            {
                JailPutBack(trial);
            }
            else if (trial.Steps.Contains(TutorialJail.CrateStep) || !NamesDone(trial))
            {
                Warden()?.SayTo(pm, trial.Steps.Contains(TutorialJail.CrateStep) ? "Leave that crate be. Once is enough." : "Not yet. Meet everyone here first.");
            }
            else
            {
                var pouch = new Pouch { Name = "marked pouch", Hue = 0x22 };
                Lend(trial, pouch);
                if (trial.LendFailed)
                {
                    trial.LendFailed = false;
                    Warden()?.SayTo(pm, "Your backpack is full. Make room, then try again.");
                    return;
                }

                SetGray(trial, true);
                Overhead(pm, "Your name is gray", 0x3B2);
                Warden()?.SayTo(pm, WardenTakeLine);
                Show(pm, trial);
            }
        }

        /// <summary>The pouch dropped on the crate.</summary>
        public static bool JailDropped(Mobile from, Item dropped)
        {
            if (JailTrial(from) is not { JailGray: true } trial || !trial.Loaners.Contains(dropped)) { return false; }
            JailPutBack(trial);
            return true;
        }

        private static void JailPutBack(Trial trial)
        {
            var pm = trial.Player;
            foreach (var item in trial.Loaners) { item.Delete(); }
            trial.Loaners.Clear();
            SetGray(trial, false);
            Overhead(pm, "Your name is blue", 0x59);
            Warden()?.SayTo(pm, WardenBackLine);
            Step(trial, TutorialJail.CrateStep);
        }

        /// <summary>The ledger was read: only after the crate does it count, and then the thief comes in.</summary>
        public static void JailLedger(Mobile from)
        {
            if (JailTrial(from) is not { } trial || !trial.Steps.Contains(TutorialJail.CrateStep) ||
                trial.Steps.Contains(TutorialJail.LedgerStep))
            {
                return;
            }

            var pm = trial.Player;
            var thief = new TutorialJailActor(pm, true);
            thief.MoveToWorld(TutorialJail.ThiefSpot, Map.Trammel);
            trial.Creatures.Add(thief);
            trial.JailThief = thief;
            Step(trial, TutorialJail.LedgerStep); // the window now says to call the guards
            thief.SayTo(pm, "*snatches a purse*");
            Warden()?.SayTo(pm, WardenThiefLine);
        }

        /// <summary>"guards", said out loud with the thief in the hall (EventSink.Speech). A staged strike, not a crime report.</summary>
        private static void OnJailSpeech(SpeechEventArgs e)
        {
            if (!e.Blocked && JailTrial(e.Mobile) is { JailThief: { Deleted: false } thief, JailStriking: false } trial &&
                e.Speech.Contains("guards", StringComparison.OrdinalIgnoreCase))
            {
                JailStrike(trial, thief);
            }
        }

        private static void Flash(Mobile m)
        {
            Effects.SendLocationParticles(EffectItem.Create(m.Location, m.Map, EffectItem.DefaultDuration), 0x3728, 10, 10, 5023);
            m.PlaySound(0x1FE);
        }

        private static void JailStrike(Trial trial, TutorialJailActor thief)
        {
            var pm = trial.Player;
            trial.JailStriking = true;
            var guard = new TutorialJailActor(pm, false);
            guard.MoveToWorld(TutorialJail.GuardSpot, Map.Trammel);
            guard.Direction = guard.GetDirectionTo(thief);
            trial.Creatures.Add(guard);
            Flash(guard);
            guard.SayTo(pm, GuardCallLine);
            Server.Timer.StartTimer(TimeSpan.FromSeconds(1), () =>
            {
                if (Current(pm) != trial || trial.Finished) { return; }
                guard.Animate(9, 7, 1, true, false, 0);
                guard.PlaySound(0x13C);
                thief.Animate(21, 6, 1, true, false, 0);
            });
            Server.Timer.StartTimer(TimeSpan.FromSeconds(3), () =>
            {
                if (Current(pm) != trial || trial.Finished) { return; }
                Flash(guard);
                Step(trial, TutorialJail.ThiefStep); // the last step: Finish takes the guard and the thief away
            });
        }

        /// <summary>A pouch lost another way (dropped, and taken back by the loaner rule): the name goes blue, nothing is earned.</summary>
        private static void JailTick(Trial trial)
        {
            if (trial.JailGray && trial.Loaners.Count == 0)
            {
                SetGray(trial, false);
                Show(trial.Player, trial);
            }
        }

        private static void EndJail(Trial trial)
        {
            SetGray(trial, false);
            trial.JailThief = null;
            trial.JailStriking = false;
        }

        /// <summary>[lesson: the jail's window again, with its step now.</summary>
        public static bool ReopenJail(PlayerMobile pm)
        {
            if (JailTrial(pm) is not { } trial) { return false; }
            Show(pm, trial);
            return true;
        }
    }

    /// <summary>The lesson window: stays until closed, and shows the five colors, what each means, and what to do now.</summary>
    public class TutorialJailGump : Gump
    {
        /// <summary>Right of the game view: the first client walk showed it over the hall, on the people to be clicked.</summary>
        public const int WindowX = 830;

        public const int BtnHint = 1;
        public const int BtnReset = 2;
        public const int BtnStop = 3;

        private readonly TutorialStation _station;

        public TutorialJailGump(TutorialWingTrials.Trial trial) : base(WindowX, 60)
        {
            _station = trial.Station;
            TutorialStyle.Panel(this, 440, 370);
            AddLabel(24, 18, TutorialStyle.HeadingHue, trial.Station.Title);
            AddHtml(24, 44, 392, 80, TutorialStyle.Html(TutorialWingTrials.JailNow(trial)), false, false);
            var y = 130;
            foreach (var p in TutorialJail.People)
            {
                var met = trial.Finished || trial.Steps.Contains(p.Step);
                AddLabel(24, y, p.Hue, p.Color);
                AddLabel(80, y, met ? TutorialStyle.TextHue : TutorialStyle.ActionHue, met ? p.Meaning : "Not met yet");
                y += 22;
            }

            y += 6;
            foreach (var (id, label) in TutorialJail.Steps.Skip(TutorialJail.People.Length))
            {
                var done = trial.Finished || trial.Steps.Contains(id);
                AddLabel(24, y, done ? TutorialStyle.DoneHue : TutorialStyle.TextHue, (done ? "Done: " : "To do: ") + label);
                y += 20;
            }

            y += 8;
            AddButton(24, y, 4005, 4007, BtnHint);
            AddLabel(60, y + 2, trial.Finished ? TutorialStyle.DoneHue : TutorialStyle.ActionHue, trial.Finished ? "Back to Rook" : "Hint and arrow");
            AddButton(24, y + 26, 4017, 4019, BtnReset);
            AddLabel(60, y + 28, TutorialStyle.TextHue, "Restart this lesson");
            if (!trial.Finished)
            {
                AddButton(230, y + 26, 4017, 4019, BtnStop);
                AddLabel(266, y + 28, TutorialStyle.TextHue, "Stop this lesson");
            }
        }

        public override void OnResponse(NetState sender, in RelayInfo info)
        {
            if (sender.Mobile is not PlayerMobile pm || !ClusterFTutorial.IsParticipant(pm)) { return; }
            switch (info.ButtonID)
            {
                case BtnHint when TutorialWingTrials.Current(pm) is null or { Finished: true }:
                    TutorialRecruiter.PointTo(pm);
                    break;
                case BtnHint:
                    ClusterFTutorial.RunStation(pm, _station);
                    break;
                case BtnReset:
                    TutorialWingTrials.Reset(pm, _station);
                    break;
                case BtnStop:
                    TutorialWingTrials.Cancel(pm, "Lesson stopped. Double-click Rook to start another lesson.");
                    break;
            }
        }
    }

    /// <summary>The warden's ledger: the reader's own karma, fame and murder counts, and what moves each.</summary>
    public class TutorialJailLedgerGump : Gump
    {
        public static string Page(PlayerMobile pm) =>
            $"<b>Your name in this ledger: {pm.Name}</b><br>Karma: {pm.Karma}. Fame: {pm.Fame}. Murder counts: {pm.Kills}.<br><br>" +
            "<b>Karma</b> is your good name. Killing evil creatures raises it; killing innocents lowers it.<br>" +
            "<b>Fame</b> is how widely you are known. Great deeds raise it, and dying lowers it. Karma and fame together give your title.<br>" +
            "<b>Murder counts</b> come from killing innocent players. Five turn your name red.<br><br>" +
            "<b>Black Tam: five counts.</b> One count fades after every 40 hours you are logged in. Until then, some healers will not help him.";

        public TutorialJailLedgerGump(PlayerMobile pm) : base(TutorialJailGump.WindowX, 440)
        {
            TutorialStyle.Panel(this, 440, 330);
            AddLabel(24, 18, TutorialStyle.HeadingHue, "The warden's ledger");
            AddHtml(24, 46, 392, 262, TutorialStyle.Html(Page(pm)), false, true);
        }
    }

    /// <summary>The marked crate: double-click to take the pouch and again to put it back, or drop the pouch on it.</summary>
    [SerializationGenerator(0, false)]
    public partial class TutorialJailCrate : Item
    {
        public TutorialJailCrate() : base(0x0E3D)
        {
            Name = "the marked crate";
            Hue = 0x22;
            Movable = false;
        }

        public override void OnDoubleClick(Mobile from)
        {
            if (!from.InRange(GetWorldLocation(), 3))
            {
                from.SendMessage("Move closer to the crate.");
                return;
            }

            TutorialWingTrials.JailCrate(from);
        }

        public override bool OnDragDrop(Mobile from, Item dropped) => TutorialWingTrials.JailDropped(from, dropped);
    }

    /// <summary>The warden's ledger on his table.</summary>
    [SerializationGenerator(0, false)]
    public partial class TutorialJailLedger : Item
    {
        public TutorialJailLedger() : base(0x0FEF)
        {
            Name = "the warden's ledger";
            Movable = false;
        }

        public override void OnDoubleClick(Mobile from)
        {
            if (from is not PlayerMobile pm) { return; }
            if (!pm.InRange(GetWorldLocation(), 3))
            {
                pm.SendMessage("Move closer to the ledger.");
                return;
            }

            TutorialWingTrials.JailLedger(pm);
            pm.CloseGump<TutorialJailLedgerGump>();
            pm.SendGump(new TutorialJailLedgerGump(pm));
        }
    }
}

namespace Server.Mobiles
{
    using Server.Tutorial;

    /// <summary>cc-P801: one of the jail's five people: answers a click, shows their color, cannot be harmed, never walks.</summary>
    [SerializationGenerator(0, false)]
    public partial class TutorialJailPerson : BaseCreature, IShownNotoriety
    {
        [SerializableField(0)]
        private int _role;

        public TutorialJailPerson(int role) : base(AIType.AI_Animal, FightMode.None, 10, 1)
        {
            _role = role;
            var p = TutorialJail.People[role];
            Name = p.Name;
            Title = p.Title;
            Body = role == 1 ? 0x191 : 0x190;
            Female = role == 1;
            Hue = 0x83EA;
            Direction = p.Facing;
            CantWalk = true;
            SetStr(80);
            SetDex(80);
            SetInt(80);
            SetHits(150);
            AddItem(new Item(role == 1 ? 0x203C : 0x203B) { Movable = false, Hue = 0x455, Layer = Layer.Hair });
            AddItem(new Boots { Movable = false });
            switch (role)
            {
                case 0:
                    AddItem(new RingmailChest { Movable = false });
                    AddItem(new LongPants { Movable = false, Hue = 0x455 });
                    AddItem(new Cloak { Movable = false, Hue = 0x35 });
                    break;
                case 1:
                    AddItem(new PlainDress { Movable = false, Hue = 0x59 });
                    break;
                default:
                    AddItem(new Shirt { Movable = false, Hue = 0x3B1 });
                    AddItem(new ShortPants { Movable = false, Hue = 0x3B1 });
                    break;
            }
        }

        public int ShownNotoriety => TutorialJail.People[Role].Noto;
        public override bool IsInvulnerable => true;
        public override bool ClickTitle => true;

        private void Answer(Mobile from)
        {
            SayTo(from, TutorialJail.People[Role].Line);
            TutorialWingTrials.JailMet(from, TutorialJail.People[Role].Step);
        }

        public override void OnSingleClick(Mobile from)
        {
            base.OnSingleClick(from);
            Answer(from);
        }

        public override void OnAosSingleClick(Mobile from)
        {
            base.OnAosSingleClick(from);
            Answer(from);
        }

        public override void OnDoubleClick(Mobile from) => Answer(from);

        public override void GenerateLoot()
        {
        }
    }

    /// <summary>cc-P801: the thief and the guard of the last step: seen only by their trainee, gone with the lesson.</summary>
    [SerializationGenerator(0, false)]
    public partial class TutorialJailActor : BaseCreature, IPrivateMobile, IShownNotoriety
    {
        private readonly Mobile? _trainee;
        private readonly bool _thief;

        public TutorialJailActor(Mobile trainee, bool thief) : base(AIType.AI_Animal, FightMode.None, 10, 1)
        {
            _trainee = trainee;
            _thief = thief;
            Name = thief ? "a sneak thief" : "a New Haven guard";
            Body = 0x190;
            Hue = 0x83EA;
            CantWalk = true;
            SetStr(80);
            SetDex(80);
            SetInt(80);
            SetHits(150);
            if (thief)
            {
                AddItem(new Shirt { Movable = false, Hue = 0x455 });
                AddItem(new ShortPants { Movable = false, Hue = 0x455 });
                AddItem(new Bandana { Movable = false, Hue = 0x455 });
            }
            else
            {
                AddItem(new PlateChest { Movable = false });
                AddItem(new PlateLegs { Movable = false });
                AddItem(new PlateHelm { Movable = false });
                AddItem(new Halberd { Movable = false });
            }
        }

        public bool ShowsTo(Mobile viewer) => viewer == _trainee;
        public int ShownNotoriety => _thief ? Notoriety.Criminal : Notoriety.Invulnerable;
        public override bool IsInvulnerable => true;
        public override bool ClickTitle => false;

        public override void GenerateLoot()
        {
        }

        [AfterDeserialization(false)] private void ClearAbandonedLesson() => Delete();
    }
}
