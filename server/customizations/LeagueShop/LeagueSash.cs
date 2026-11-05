// LeagueSash.cs
//
// cc-P800: the League sash grows with its wearer. Plan and Chase's answer in shard-migration notes/cc-P800-league-sash-plan.md
// and notes/cc-P800-league-sash.md.
//
// ONE PER CHARACTER. Elara gives the first free. The sash is blessed and bound to the character, never the account. A step
// is opened by the character's League rank and bought from Elara with spendable Renown (SpendRenown, so rank never falls).
// Each step changes its name, hue and title line and adds a small Renown bonus while worn. Never skill, stat or item
// properties.
//
// STORAGE. The item saves its owner and step. The account's FlagValues (the same per-character keys ClusterFLoops uses, no
// schema change) keep the character's step, so a lost sash can be replaced at that step, and the fraction of a Renown the
// bonus has not yet paid. A loop touches neither: the sash, its step and the flags all stay (F-12: League rank, Renown and
// achievements sit outside the loop).
//
// OLD SASHES. P79's BoundCosmeticSash labelled "League" converts in place after every world load and at its holder's login; a
// pass finding nothing changes nothing. The login pass is the one that counts for a character whose Account was not set when the
// load pass ran (seen on a real load, never in the test host). A second one held by a character becomes a keepsake labelled "old
// League". One still inside an unopened box converts as the box opens. One not held by a character of its account is left until
// it is.
//
// NUMBERS. Configuration/league-sash.json, written with the defaults on first start, read at start and by [LeagueSash reload.
// A bad file is refused whole, every error is printed, and the table in force is kept.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using ModernUO.CodeGeneratedEvents;
using ModernUO.Serialization;
using Server.Accounting;
using Server.Commands;
using Server.Engines.LeagueRewards;
using Server.Items;
using Server.Mobiles;
using Server.Network;
using Server.Tutorial;

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class LeagueSash : BodySash
    {
        public const string Refusal = "That sash is bound to its owner.";

        [SerializableField(0, setter: "private")]
        private Mobile _owner;

        [SerializableField(1, setter: "private")]
        private int _step;

        [Constructible]
        public LeagueSash() : this(null, 0)
        {
        }

        public LeagueSash(Mobile owner, int step)
        {
            _owner = owner;
            LootType = LootType.Blessed;
            SetStep(step);
        }

        /// <summary>The step's look. The character's record is LeagueSashSystem's.</summary>
        public void SetStep(int step)
        {
            Step = Math.Clamp(step, 0, LeagueSashTable.Current.MaxStep);
            Hue = LeagueSashTable.Current.HueOf(Step);
            InvalidateProperties();
        }

        public override string DefaultName => Step <= 0 ? "a League sash" : $"a {LeagueSashTable.Current.MetalNameOf(Step)} League sash";

        public override void GetProperties(IPropertyList list)
        {
            base.GetProperties(list);

            foreach (var line in Lines())
            {
                list.Add(line);
            }
        }

        /// <summary>What the sash says under its name.</summary>
        public List<string> Lines()
        {
            var table = LeagueSashTable.Current;
            var lines = new List<string> { $"Sash of {LeagueSashSystem.CitizenPhrase(Step)}, step {Step} of {table.MaxStep}" };

            if (table.BonusOf(Step) > 0)
            {
                lines.Add($"+{table.BonusOf(Step)}% Renown earned while worn");
            }

            if (_owner is PlayerMobile pm && ClusterFLoops.LoopOf(pm) is var loops and > 0)
            {
                lines.Add(loops == 1 ? "Worn through 1 loop" : $"Worn through {loops} loops");
            }

            lines.Add("Bound to this character");
            return lines;
        }

        public bool IsOwner(Mobile m) => m != null && m == _owner;

        private static bool Refuse(Mobile from)
        {
            from.SendMessage(0x22, Refusal);
            return false;
        }

        private bool MayRestIn(Item target) => target?.RootParent is Mobile holder && IsOwner(holder);

        public override bool AllowSecureTrade(Mobile from, Mobile to, Mobile newOwner, bool accepted) => Refuse(from);

        public override bool OnDroppedToWorld(Mobile from, Point3D p) => Refuse(from);

        public override bool OnDroppedToMobile(Mobile from, Mobile target) =>
            target == from && IsOwner(from) ? base.OnDroppedToMobile(from, target) : Refuse(from);

        public override bool OnDroppedInto(Mobile from, Container target, Point3D p) =>
            MayRestIn(target) ? base.OnDroppedInto(from, target, p) : Refuse(from);

        public override bool OnDroppedOnto(Mobile from, Item target) =>
            MayRestIn(target) ? base.OnDroppedOnto(from, target) : Refuse(from);

        public override bool CheckLift(Mobile from, Item item, ref LRReason reject)
        {
            if (from.AccessLevel < AccessLevel.GameMaster && !IsOwner(from))
            {
                reject = LRReason.CannotLift;
                return false;
            }

            return base.CheckLift(from, item, ref reject);
        }

        // The hue says the step, and bandages would end a sash the League keeps a record of.
        public override bool Dye(Mobile from, DyeTub sender) => Refuse(from);

        public override bool Scissor(Mobile from, Scissors scissors) => Refuse(from);
    }
}

namespace Server
{
    /// <summary>One step above the first sash: the metal rank that opens it, its Renown fee, its bonus while worn.</summary>
    public sealed record LeagueSashStep(string Metal, int Fee, int BonusPercent);

    /// <summary>The steps and effects: Configuration/league-sash.json.</summary>
    public sealed class LeagueSashTable
    {
        public const string FileName = "league-sash.json";

        public static string FilePath => Path.Combine(Core.BaseDirectory, "Configuration", FileName);

        public static LeagueSashTable Defaults { get; } = new(
            [
                new LeagueSashStep("DullCopper", 50, 1),
                new LeagueSashStep("Bronze", 250, 2),
                new LeagueSashStep("Verite", 1_000, 3),
                new LeagueSashStep("Blaze", 3_000, 4),
                new LeagueSashStep("Mythril", 6_000, 5),
                new LeagueSashStep("Celestial", 10_000, 6)
            ],
            LeagueShop.LeagueHue, 0x1F2, 0x376A, "built-in defaults"
        );

        public static LeagueSashTable Current { get; set; } = Defaults;

        private readonly List<LeagueSashStep> _steps;

        private LeagueSashTable(List<LeagueSashStep> steps, int firstHue, int sound, int effect, string source)
        {
            _steps = steps;
            FirstHue = firstHue;
            UpgradeSound = sound;
            UpgradeEffect = effect;
            Source = source;
        }

        public int FirstHue { get; }

        public int UpgradeSound { get; }

        public int UpgradeEffect { get; }

        public string Source { get; }

        public int MaxStep => _steps.Count;

        /// <summary>Step 1 upward; null for step 0 and past the top.</summary>
        public LeagueSashStep Row(int step) => step >= 1 && step <= _steps.Count ? _steps[step - 1] : null;

        private static LeagueRankInfo Rank(string metal)
        {
            foreach (var rank in ClusterFLeagueRanks.Ladder)
            {
                if (rank.MetalKey.Equals(metal, StringComparison.OrdinalIgnoreCase))
                {
                    return rank;
                }
            }

            return null;
        }

        private LeagueRankInfo RankOf(int step) => Row(step) is { } row ? Rank(row.Metal) : null;

        /// <summary>The ladder rank that opens a step: 1 (Iron) for the first sash.</summary>
        public int RankNeeded(int step) => RankOf(step)?.Index ?? 1;

        public int HueOf(int step) => RankOf(step)?.Hue ?? FirstHue;

        public string MetalNameOf(int step) => RankOf(step)?.MetalName ?? "League";

        public int FeeOf(int step) => Row(step)?.Fee ?? 0;

        public int BonusOf(int step) => Row(step)?.BonusPercent ?? 0;

        // -- The file ----------------------------------------------------------------------------------

        public static string ToJson(LeagueSashTable table)
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"about\": \"The League sash (cc-P800). firstHue: the first sash. upgradeSound, upgradeEffect: Elara's upgrade. ");
            sb.Append("steps: lowest first; metal is the League rank that opens the step (the step takes its name and hue), fee is the ");
            sb.Append("Renown Elara charges, bonusPercent is the Renown bonus while worn (0 turns it off). Edit and run [LeagueSash reload. ");
            sb.Append("A bad file is refused whole and the table in force is kept.\",\n");
            sb.Append($"  \"firstHue\": {table.FirstHue},\n  \"upgradeSound\": {table.UpgradeSound},\n  \"upgradeEffect\": {table.UpgradeEffect},\n");
            sb.Append("  \"steps\": [\n");

            for (var i = 0; i < table._steps.Count; i++)
            {
                var s = table._steps[i];
                sb.Append($"    {{ \"metal\": \"{s.Metal}\", \"fee\": {s.Fee}, \"bonusPercent\": {s.BonusPercent} }}{(i < table._steps.Count - 1 ? "," : "")}\n");
            }

            sb.Append("  ]\n}\n");
            return sb.ToString();
        }

        private static int ReadInt(JsonElement el, string name, int min, int max, string where, List<string> errors)
        {
            if (el.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n) && n >= min && n <= max)
            {
                return n;
            }

            errors.Add($"{where}: \"{name}\" must be a whole number from {min} to {max}.");
            return min;
        }

        /// <summary>
        /// Reads a table, or returns null and fills errors; accepted whole or not at all. Checked: syntax; firstHue, sound and
        /// effect; at least one step; each metal is a ladder metal above Iron, in rising rank order; fee 0 or more; bonus 0 to 100.
        /// </summary>
        public static LeagueSashTable Parse(string json, string source, List<string> errors)
        {
            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            }
            catch (JsonException ex)
            {
                errors.Add($"not valid JSON: {ex.Message}");
                return null;
            }

            using (doc)
            {
                var root = doc.RootElement;

                if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("steps", out var steps) ||
                    steps.ValueKind != JsonValueKind.Array || steps.GetArrayLength() == 0)
                {
                    errors.Add("the file needs a \"steps\" array with at least one row.");
                    return null;
                }

                var firstHue = ReadInt(root, "firstHue", 0, 0xFFFF, "file", errors);
                var sound = ReadInt(root, "upgradeSound", 0, 0xFFFF, "file", errors);
                var effect = ReadInt(root, "upgradeEffect", 0, 0xFFFF, "file", errors);

                var rows = new List<LeagueSashStep>();
                var lastRank = 1;

                foreach (var el in steps.EnumerateArray())
                {
                    var where = $"step {rows.Count + 1}";

                    if (el.ValueKind != JsonValueKind.Object)
                    {
                        errors.Add($"{where}: not an object.");
                        rows.Add(null);
                        continue;
                    }

                    var metal = el.TryGetProperty("metal", out var m) && m.ValueKind == JsonValueKind.String ? m.GetString() : null;
                    var rank = Rank(metal ?? "");

                    if (rank == null)
                    {
                        errors.Add($"{where}: \"{metal}\" is not a League rank's metal.");
                    }
                    else if (rank.Index <= lastRank)
                    {
                        errors.Add($"{where}: {rank.MetalKey} must rank above the step before it.");
                    }
                    else
                    {
                        lastRank = rank.Index;
                    }

                    rows.Add(new LeagueSashStep(rank?.MetalKey ?? "", ReadInt(el, "fee", 0, int.MaxValue, where, errors),
                        ReadInt(el, "bonusPercent", 0, 100, where, errors)));
                }

                return errors.Count == 0 ? new LeagueSashTable(rows, firstHue, sound, effect, source) : null;
            }
        }

        public static List<string> LoadOrWriteDefaults(string path)
        {
            if (File.Exists(path))
            {
                return Reload(path);
            }

            Current = Defaults;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, ToJson(Defaults));
                return [$"{FileName} was missing; wrote the built-in defaults to {path}."];
            }
            catch (Exception ex)
            {
                return [$"{FileName} is missing and could not be written ({ex.Message}); using the built-in defaults."];
            }
        }

        /// <summary>Reads the file and puts it in force, or refuses it whole and keeps the table in force. Returns lines to report.</summary>
        public static List<string> Reload(string path)
        {
            var errors = new List<string>();
            LeagueSashTable table = null;

            try
            {
                table = Parse(File.ReadAllText(path), path, errors);
            }
            catch (Exception ex)
            {
                errors.Add($"could not read it: {ex.Message}");
            }

            if (table == null)
            {
                errors.Insert(0, $"{FileName} REFUSED, the table in force is kept ({Current.Source}):");
                return errors;
            }

            Current = table;
            return [$"{FileName} loaded: {table.MaxStep} steps."];
        }
    }

    public static class LeagueSashSystem
    {
        public const string OldLabel = "League";
        public const string KeepsakeLabel = "old League";

        public static void Configure()
        {
            foreach (var line in LeagueSashTable.LoadOrWriteDefaults(LeagueSashTable.FilePath))
            {
                Console.WriteLine($"[League] {line}");
            }

            CommandSystem.Register("LeagueSash", AccessLevel.GameMaster, OnCommand);

            // Items made during the load reach World.Items only after it.
            EventSink.WorldLoad += () => Timer.DelayCall(TimeSpan.Zero, () => ConvertOldSashes());
        }

        [Usage("LeagueSash [reload | step <n>]")]
        [Description("Lists the League sash steps. 'reload' reads Configuration/league-sash.json again; a bad file is refused whole and the table in force is kept. 'step <n>' sets your own sash to a step, with no fee.")]
        [ShardCommand(CommandCategory.Diagnostic, Shard = CommandShard.Any)]
        private static void OnCommand(CommandEventArgs e)
        {
            var from = e.Mobile;
            var word = e.Length >= 1 ? e.GetString(0).ToLowerInvariant() : "";

            if (word == "reload")
            {
                foreach (var line in LeagueSashTable.Reload(LeagueSashTable.FilePath))
                {
                    from.SendMessage(line);
                }
            }
            else if (word == "step" && e.Length >= 2 && from is PlayerMobile pm)
            {
                if (Find(pm) is { } own)
                {
                    SetCharacterStep(pm, own, e.GetInt32(1));
                    from.SendMessage($"Your sash is at step {own.Step}.");
                }
                else
                {
                    from.SendMessage("You have no League sash.");
                }

                return;
            }

            var table = LeagueSashTable.Current;
            from.SendMessage($"League sash ({table.Source}), {table.MaxStep} steps:");

            for (var s = 1; s <= table.MaxStep; s++)
            {
                from.SendMessage($"{s}: {table.MetalNameOf(s)}, {table.FeeOf(s):N0} Renown, +{table.BonusOf(s)}%");
            }
        }

        public static string CitizenPhrase(int step)
        {
            var metal = LeagueSashTable.Current.MetalNameOf(step);
            return $"{("AEIOU".Contains(metal[0]) ? "an" : "a")} {metal} Citizen";
        }

        // -- The character's record ----------------------------------------------------------------------

        private static string StepKey(Mobile m) => $"character:{(uint)m.Serial}:leagueSash";

        private static string CarryKey(Mobile m) => $"character:{(uint)m.Serial}:leagueSashCarry";

        private static ClusterFAccountData Data(Mobile m) => m?.Account is IAccount acct ? ClusterFAccountPersistence.GetOrCreate(acct) : null;

        private static bool ReadFlag(Mobile m, string key, out int value)
        {
            value = 0;
            return Data(m)?.FlagValues.TryGetValue(key, out var text) == true &&
                   int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
        }

        private static void SetCharacterStep(PlayerMobile pm, LeagueSash sash, int step)
        {
            sash.SetStep(step);

            if (Data(pm) is { } data)
            {
                data.FlagValues[StepKey(pm)] = sash.Step.ToString(CultureInfo.InvariantCulture);
            }
        }

        /// <summary>This character's step, kept apart from the item so a lost sash comes back where it was.</summary>
        public static int StepOf(PlayerMobile pm) =>
            ReadFlag(pm, StepKey(pm), out var step) ? Math.Min(step, LeagueSashTable.Current.MaxStep) : 0;

        /// <summary>Worn, carried at any depth, or in the bank.</summary>
        public static LeagueSash Find(PlayerMobile pm)
        {
            if (pm.FindItemOnLayer(Layer.MiddleTorso) is LeagueSash worn && worn.IsOwner(pm))
            {
                return worn;
            }

            return FindIn(pm.Backpack, pm) ?? FindIn(pm.FindBankNoCreate(), pm);
        }

        private static LeagueSash FindIn(Container pack, PlayerMobile pm)
        {
            if (pack == null)
            {
                return null;
            }

            foreach (var sash in pack.FindItemsByType<LeagueSash>())
            {
                if (sash.IsOwner(pm))
                {
                    return sash;
                }
            }

            return null;
        }

        /// <summary>The character has had their sash, whether or not it can be found now.</summary>
        public static bool Has(PlayerMobile pm) => ReadFlag(pm, StepKey(pm), out _) || Find(pm) != null;

        /// <summary>The highest step this character's rank opens.</summary>
        public static int OpenStep(PlayerMobile pm)
        {
            var table = LeagueSashTable.Current;
            var rank = ClusterFLeagueRanks.GetRank(pm);
            var open = 0;

            for (var s = 1; s <= table.MaxStep && table.RankNeeded(s) <= rank; s++)
            {
                open = s;
            }

            return open;
        }

        public static void Refresh(PlayerMobile pm) => Find(pm)?.InvalidateProperties();

        private static LeagueSash Create(PlayerMobile pm, int step)
        {
            var sash = new LeagueSash(pm, step);

            if (Data(pm) is { } data)
            {
                data.FlagValues[StepKey(pm)] = sash.Step.ToString(CultureInfo.InvariantCulture);
            }

            return sash;
        }

        // -- At Elara's ----------------------------------------------------------------------------------

        /// <summary>The first sash, or a lost one back at the character's step. Free. Refused while they hold it.</summary>
        public static LeagueSash Issue(PlayerMobile pm, out string message)
        {
            if (ClusterFLeagueRanks.GetRank(pm) <= 0)
            {
                message = "Register with the League first.";
                return null;
            }

            if (Find(pm) != null)
            {
                message = "You already have your League sash.";
                return null;
            }

            var replacing = Has(pm);
            var sash = Create(pm, StepOf(pm));
            ClusterFGuildPromotion.Deliver(pm, sash);
            message = replacing ? "The League replaces your sash." : "The League gives you your sash. It grows as you rise.";
            return sash;
        }

        /// <summary>
        /// Buys the next step. Everything is checked here, from the step the page was drawn at, so a stale page charges nobody.
        /// Renown comes from the balance only: LifetimeRenown, and so League rank, never falls.
        /// </summary>
        public static bool Upgrade(PlayerMobile pm, int fromStep, out string message)
        {
            var table = LeagueSashTable.Current;
            var sash = Find(pm);

            if (sash == null)
            {
                message = "You need your League sash with you.";
                return false;
            }

            var step = StepOf(pm);

            if (step != fromStep)
            {
                message = "That page is out of date.";
                return false;
            }

            var next = step + 1;

            if (next > table.MaxStep)
            {
                message = "Your sash has reached its last step.";
                return false;
            }

            if (ClusterFLeagueRanks.GetRank(pm) < table.RankNeeded(next))
            {
                message = $"That step opens at {ClusterFLeagueRanks.RankName(table.RankNeeded(next))}.";
                return false;
            }

            var fee = table.FeeOf(next);

            if (Data(pm)?.SpendRenown(fee) != true)
            {
                message = $"That step costs {fee:N0} Renown.";
                return false;
            }

            SetCharacterStep(pm, sash, next);
            pm.FixedParticles(table.UpgradeEffect, 9, 32, 5030, EffectLayer.Waist);
            pm.PlaySound(table.UpgradeSound);
            message = $"The League honors you: {sash.DefaultName}, step {next} of {table.MaxStep}.";
            return true;
        }

        // -- The bonus ---------------------------------------------------------------------------------

        /// <summary>The percent this character earns on top right now: only their own sash, only while worn.</summary>
        public static int WornBonus(PlayerMobile pm) =>
            pm?.FindItemOnLayer(Layer.MiddleTorso) is LeagueSash sash && sash.IsOwner(pm) ? LeagueSashTable.Current.BonusOf(sash.Step) : 0;

        /// <summary>
        /// Pays Renown earned by the character's own deeds, with the worn sash's bonus. A fraction of a Renown is kept toward
        /// the next grant, so small achievements count too. Only achievements and League jobs earn the bonus; Renown from
        /// another player (Commission) or staff never does.
        /// </summary>
        public static void Earn(ClusterFAccountData data, PlayerMobile pm, int amount, RenownSource source)
        {
            var percent = source is RenownSource.Achievement or RenownSource.LeagueJob ? WornBonus(pm) : 0;

            if (percent <= 0 || amount <= 0)
            {
                data.GrantRenown(amount, source);
                return;
            }

            ReadFlag(pm, CarryKey(pm), out var carry);
            var hundredths = (long)amount * percent + carry;
            data.FlagValues[CarryKey(pm)] = (hundredths % 100).ToString(CultureInfo.InvariantCulture);
            data.GrantRenown(amount + (int)(hundredths / 100), source);
        }

        // -- Old sashes and boxes ---------------------------------------------------------------------

        private static bool IsOld(Item item) => item is BoundCosmeticSash { Label: OldLabel };

        /// <summary>Whether a loot box may hold the sash for this character.</summary>
        public static bool BoxMayGive(PlayerMobile pm) => !Has(pm);

        /// <summary>A sash rolled for a box stays old until opened, so it goes to whoever opens it. Other items pass through.</summary>
        public static Item FromBox(PlayerMobile pm, Item item)
        {
            if (!IsOld(item))
            {
                return item;
            }

            if (Has(pm))
            {
                ((BoundCosmeticSash)item).Label = KeepsakeLabel;
                return item;
            }

            var sash = Create(pm, 0);
            item.Delete();
            return sash;
        }

        /// <summary>
        /// One old sash its character holds: becomes their sash where it lies, or a keepsake when they have theirs. True for the
        /// first.
        /// </summary>
        private static bool Convert(BoundCosmeticSash item, PlayerMobile pm)
        {
            if (Has(pm))
            {
                item.Label = KeepsakeLabel;
                item.InvalidateProperties();
                return false;
            }

            var sash = Create(pm, 0);

            if (item.Parent is Container pack)
            {
                var at = item.Location;
                item.Delete();
                pack.AddItem(sash);
                sash.Location = at;
            }
            else
            {
                item.Delete();
                pm.AddItem(sash);
            }

            return true;
        }

        private static bool HeldByItsAccount(BoundCosmeticSash item, out PlayerMobile pm)
        {
            pm = item.RootParent as PlayerMobile;
            return pm != null && AccountBinding.SameAccount(pm, item.BoundAccount);
        }

        /// <summary>
        /// Every old sash a character of its account holds, in serial order: the first becomes that character's sash where it
        /// lies, and the rest (or any when the character has theirs) become keepsakes. Run again it finds none. Returns how many
        /// became sashes.
        /// </summary>
        public static int ConvertOldSashes()
        {
            var old = new List<BoundCosmeticSash>();

            foreach (var item in World.Items.Values)
            {
                if (IsOld(item))
                {
                    old.Add((BoundCosmeticSash)item);
                }
            }

            int converted = 0, keepsakes = 0, left = 0;

            foreach (var item in old.OrderBy(i => i.Serial))
            {
                if (!HeldByItsAccount(item, out var pm))
                {
                    left++;
                }
                else if (Convert(item, pm))
                {
                    converted++;
                }
                else
                {
                    keepsakes++;
                }
            }

            if (old.Count > 0)
            {
                Console.WriteLine($"[League] Old sashes: {old.Count} found, {converted} converted, {keepsakes} keepsakes, {left} left for their login.");
            }

            return converted;
        }

        /// <summary>The same for one character, worn, carried or banked, as they log in.</summary>
        [OnEvent(nameof(PlayerMobile.PlayerLoginEvent))]
        public static void OnLogin(PlayerMobile pm)
        {
            var old = new List<BoundCosmeticSash>();

            if (pm.FindItemOnLayer(Layer.MiddleTorso) is BoundCosmeticSash worn)
            {
                old.Add(worn);
            }

            foreach (var pack in new[] { pm.Backpack, pm.FindBankNoCreate() })
            {
                if (pack != null)
                {
                    foreach (var item in pack.FindItemsByType<BoundCosmeticSash>())
                    {
                        old.Add(item);
                    }
                }
            }

            foreach (var item in old.OrderBy(i => i.Serial))
            {
                if (IsOld(item) && HeldByItsAccount(item, out _) && Convert(item, pm))
                {
                    Console.WriteLine($"[League] Converted {pm.Name}'s old sash at login.");
                }
            }
        }
    }
}
