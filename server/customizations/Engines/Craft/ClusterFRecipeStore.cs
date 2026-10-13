// cc-P78 Part A (F-43): the account recipe store. Notes: shard-migration notes/cc-P78-recipe-gating.md.
//
// One store per account (ClusterFAccountData.LearnedRecipes, v17), never per character: learned recipes survive a loop
// and a change of character. A key names what was learned:
//   recipe:<id>                 one pinned Recipe id: OSI's ML recipes (pinned Recipes.cs, ids 0-602 in use) and our
//                               mule schematics (9000 + module * 10 + tier, MuleModuleCrafts.RecipeId). Reusing pinned's
//                               ids keeps pinned's RecipeScroll, loot and satchel recipes working.
//   book:<MainSkill>:<band>     a whole band book of one craft skill (Band I to IV).
//   item:<MainSkill>:<Type>     one item's single scroll (Band V and up, or an early single pattern).
//   flag:Masonry, flag:Glassblowing   pinned's two system books (PlayerMobile.Masonry / .Glassblowing).
//   map:<region>                a Cartography region mapped by exploring (the F-6 hook; nothing writes it yet).
//
// Pinned still checks a character's own recipes and flags (CraftItem.Craft, RecipeScroll, DefMasonry.CanCraft,
// DefGlassblowing.CanCraft). Sync keeps the two in step both ways: what a character learned the pinned way (a stock
// RecipeScroll, a Masonry book) goes onto the account, and what the account knows is written onto the character. It
// runs at login, whenever the craft menu draws, and on every Knows of a pinned recipe.

#nullable enable

using System;
using System.Collections.Generic;
using ModernUO.CodeGeneratedEvents;
using Server.Accounting;
using Server.Commands;
using Server.Engines.Craft;
using Server.Mobiles;
using Server.Targeting;

namespace Server
{
    public static class ClusterFRecipeStore
    {
        public const string FlagMasonry = "flag:Masonry";
        public const string FlagGlassblowing = "flag:Glassblowing";

        public static string RecipeKey(int recipeId) => $"recipe:{recipeId}";

        public static string BookKey(SkillName skill, int band) => $"book:{skill}:{band}";

        public static string ItemKey(SkillName skill, Type itemType) => $"item:{skill}:{itemType.Name}";

        public static string MapKey(string region) => $"map:{region}";

        public static bool TryRecipeId(string key, out int id)
        {
            id = 0;
            return key.StartsWith("recipe:", StringComparison.Ordinal) && int.TryParse(key.AsSpan(7), out id);
        }

        public static ClusterFAccountData? DataOf(Mobile m) =>
            m.Account is IAccount acct ? ClusterFAccountPersistence.GetOrCreate(acct) : null;

        public static void Configure()
        {
            foreach (var line in RecipeGatingTable.LoadOrWriteDefaults(RecipeGatingTable.FilePath))
            {
                Console.WriteLine($"[RecipeGating] {line}");
            }

            CommandSystem.Register("Recipes", AccessLevel.GameMaster, OnRecipesCommand);
            CommandSystem.Register("RecipeGating", AccessLevel.GameMaster, OnGatingCommand);
        }

        [OnEvent(nameof(PlayerMobile.PlayerLoginEvent))]
        public static void OnLogin(PlayerMobile pm) => Sync(pm);

        /// <summary>Whether this character's account knows the key. A pinned recipe on the character counts, and is copied.</summary>
        public static bool Knows(PlayerMobile pm, string key)
        {
            var data = DataOf(pm);

            if (TryRecipeId(key, out var id))
            {
                if (pm.HasRecipe(id))
                {
                    data?.LearnedRecipes.Add(key);
                    return true;
                }

                if (data?.LearnedRecipes.Contains(key) == true)
                {
                    pm.AcquireRecipe(id);
                    return true;
                }

                return false;
            }

            return data?.LearnedRecipes.Contains(key) == true;
        }

        /// <summary>Teaches the account (and this character, for pinned's own checks). False when it was already known.</summary>
        public static bool Learn(PlayerMobile pm, string key)
        {
            if (Knows(pm, key))
            {
                return false;
            }

            DataOf(pm)?.LearnedRecipes.Add(key);
            WriteToCharacter(pm, key);
            return true;
        }

        public static bool Forget(PlayerMobile pm, string key)
        {
            var removed = DataOf(pm)?.LearnedRecipes.Remove(key) == true;

            if (TryRecipeId(key, out var id) && pm.HasRecipe(id))
            {
                pm.RemoveRecipe(id);
                removed = true;
            }

            if (key == FlagMasonry && pm.Masonry)
            {
                pm.Masonry = false;
                removed = true;
            }

            if (key == FlagGlassblowing && pm.Glassblowing)
            {
                pm.Glassblowing = false;
                removed = true;
            }

            return removed;
        }

        private static void WriteToCharacter(PlayerMobile pm, string key)
        {
            if (TryRecipeId(key, out var id))
            {
                pm.AcquireRecipe(id);
            }
            else if (key == FlagMasonry)
            {
                pm.Masonry = true;
            }
            else if (key == FlagGlassblowing)
            {
                pm.Glassblowing = true;
            }
        }

        /// <summary>Copies the character's pinned recipes and books onto the account, then the account's onto the character.</summary>
        public static void Sync(PlayerMobile pm)
        {
            var data = DataOf(pm);
            if (data == null)
            {
                return;
            }

            foreach (var id in Recipe.Recipes.Keys)
            {
                if (pm.HasRecipe(id))
                {
                    data.LearnedRecipes.Add(RecipeKey(id));
                }
            }

            if (pm.Masonry)
            {
                data.LearnedRecipes.Add(FlagMasonry);
            }

            if (pm.Glassblowing)
            {
                data.LearnedRecipes.Add(FlagGlassblowing);
            }

            foreach (var key in data.LearnedRecipes)
            {
                WriteToCharacter(pm, key);
            }
        }

        /// <summary>Every key there is today: every book, every scroll item, every pinned recipe, both system books.</summary>
        public static List<string> AllKeys()
        {
            var keys = new List<string> { FlagMasonry, FlagGlassblowing };

            foreach (var id in Recipe.Recipes.Keys)
            {
                keys.Add(RecipeKey(id));
            }

            foreach (var system in ClusterFRecipeGate.GatedSystems())
            {
                foreach (var band in ClusterFRecipeGate.BooksFor(system))
                {
                    keys.Add(BookKey(system.MainSkill, band));
                }

                foreach (var item in system.CraftItems)
                {
                    var c = ClusterFRecipeGate.Classify(system, item);
                    if (c.Kind == RecipeKind.Scroll && !keys.Contains(c.Key!))
                    {
                        keys.Add(c.Key!);
                    }
                }
            }

            return keys;
        }

        /// <summary>A short name for a key, for tooltips and lists ("Blacksmithy Band II patterns").</summary>
        public static string Describe(string key)
        {
            var parts = key.Split(':');
            var table = RecipeGatingTable.Current;

            switch (parts[0])
            {
                case "book" when parts.Length == 3 && Enum.TryParse<SkillName>(parts[1], out var skill) && int.TryParse(parts[2], out var band):
                    return $"{SkillTitle(skill)} Band {table.BandName(band)} patterns";
                case "item" when parts.Length == 3 && Enum.TryParse<SkillName>(parts[1], out var skill2):
                    return $"{ItemTitle(skill2, parts[2])} ({SkillTitle(skill2)})";
                case "recipe" when TryRecipeId(key, out var id) && Recipe.Recipes.TryGetValue(id, out var r):
                    return r.CraftItem.NameString ?? (r.CraftItem.NameNumber > 0 ? $"#{r.CraftItem.NameNumber}" : r.CraftItem.ItemType.Name);
                case "flag":
                    return parts.Length > 1 ? $"the {parts[1]} book" : key;
                case "map":
                    return parts.Length > 1 ? $"map of {parts[1]}" : key;
                case "imbue": // cc-P82: imbue knowledge shares the account's learned set
                    return ClusterFImbueKnowledge.Describe(key);
                default:
                    return key;
            }
        }

        public static string SkillTitle(SkillName skill) =>
            (int)skill < SkillInfo.Table.Length ? SkillInfo.Table[(int)skill].Name : skill.ToString();

        // A type name split into words ("PlatinumKryss" to "Platinum Kryss"), when the item has no name string.
        private static string ItemTitle(SkillName skill, string typeName)
        {
            var system = ClusterFRecipeGate.SystemFor(skill);
            if (system != null)
            {
                foreach (var item in system.CraftItems)
                {
                    if (item.ItemType.Name == typeName && !string.IsNullOrEmpty(item.NameString))
                    {
                        return item.NameString;
                    }
                }
            }

            var sb = new System.Text.StringBuilder();
            foreach (var ch in typeName)
            {
                if (char.IsUpper(ch) && sb.Length > 0)
                {
                    sb.Append(' ');
                }

                sb.Append(ch);
            }

            return sb.ToString();
        }

        // -- Staff commands -------------------------------------------------------------------------------------

        [Usage("Recipes <list | grant <key> | revoke <key> | learnall | forgetall>")]
        [Description("Targets a player and lists, grants or revokes recipes on that player's account (account-wide). Keys: recipe:<id>, book:<Skill>:<band>, item:<Skill>:<Type>, flag:Masonry, flag:Glassblowing. learnall teaches every key there is; forgetall empties the account's store.")]
        [ShardCommand(CommandCategory.Grant, Shard = CommandShard.TestOnly)]
        private static void OnRecipesCommand(CommandEventArgs e)
        {
            var verb = e.Length > 0 ? e.GetString(0).ToLowerInvariant() : "list";
            var key = e.Length > 1 ? e.GetString(1) : null;

            if (verb is not ("list" or "grant" or "revoke" or "learnall" or "forgetall") || verb is "grant" or "revoke" && key == null)
            {
                e.Mobile.SendMessage("Usage: [Recipes <list | grant <key> | revoke <key> | learnall | forgetall>");
                return;
            }

            e.Mobile.SendMessage("Target a player.");
            e.Mobile.BeginTarget(
                -1,
                false,
                TargetFlags.None,
                (from, targeted) =>
                {
                    if (targeted is not PlayerMobile pm || DataOf(pm) is not { } data)
                    {
                        from.SendMessage("That is not a player with an account.");
                        return;
                    }

                    foreach (var line in Run(pm, data, verb, key))
                    {
                        from.SendMessage(line);
                    }
                }
            );
        }

        public static List<string> Run(PlayerMobile pm, ClusterFAccountData data, string verb, string? key)
        {
            var lines = new List<string>();
            switch (verb)
            {
                case "grant":
                    lines.Add(Learn(pm, key!) ? $"Granted {key} ({Describe(key!)})." : $"{key} was already known.");
                    break;
                case "revoke":
                    lines.Add(Forget(pm, key!) ? $"Revoked {key}." : $"{key} was not known.");
                    break;
                case "learnall":
                    {
                        var n = 0;
                        foreach (var k in AllKeys())
                        {
                            if (Learn(pm, k))
                            {
                                n++;
                            }
                        }

                        lines.Add($"Taught {n} recipe keys; the account now knows {data.LearnedRecipes.Count}.");
                        break;
                    }
                case "forgetall":
                    {
                        var keys = new List<string>(data.LearnedRecipes);
                        foreach (var k in keys)
                        {
                            Forget(pm, k);
                        }

                        pm.ResetRecipes();
                        lines.Add($"Forgot {keys.Count} recipe keys.");
                        break;
                    }
                default:
                    {
                        Sync(pm);
                        var keys = new List<string>(data.LearnedRecipes);
                        keys.Sort(StringComparer.Ordinal);
                        lines.Add($"{pm.Name}'s account knows {keys.Count} recipe keys:");
                        foreach (var k in keys)
                        {
                            lines.Add($"  {k}  {Describe(k)}");
                        }

                        break;
                    }
            }

            return lines;
        }

        [Usage("RecipeGating [reload]")]
        [Description("Lists the recipe gating table, whether gating is on, and each gated craft skill's items per band and books after the merge rule. 'reload' reads Configuration/recipe-gating.json again; a bad file is refused whole and the table in force is kept.")]
        [ShardCommand(CommandCategory.Diagnostic, Shard = CommandShard.Any)]
        private static void OnGatingCommand(CommandEventArgs e)
        {
            if (e.Length >= 1 && e.GetString(0).Equals("reload", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var line in RecipeGatingTable.Reload(RecipeGatingTable.FilePath))
                {
                    e.Mobile.SendMessage(line);
                }
            }

            foreach (var line in Counts())
            {
                e.Mobile.SendMessage(line);
            }
        }

        /// <summary>Per gated craft skill: items free, per band, with own recipes, exempt; and its books after the merge rule.</summary>
        public static List<string> Counts()
        {
            var table = RecipeGatingTable.Current;
            var lines = new List<string>
            {
                $"Recipe gating {(ClusterFRecipeGate.Enabled ? "ON" : "OFF")} ({table.Source}). {table.Describe()}"
            };

            foreach (var system in ClusterFRecipeGate.GatedSystems())
            {
                var perBand = new int[table.Bands.Length + 1];
                int free = 0, own = 0, exempt = 0;

                foreach (var item in system.CraftItems)
                {
                    var c = ClusterFRecipeGate.Classify(system, item);
                    switch (c.Kind)
                    {
                        case RecipeKind.Free:
                            free++;
                            break;
                        case RecipeKind.OwnRecipe:
                            own++;
                            break;
                        case RecipeKind.Exempt:
                            exempt++;
                            break;
                        default:
                            perBand[table.BandOf(c.MinSkill)]++;
                            break;
                    }
                }

                var bands = new List<string>();
                for (var b = 1; b < perBand.Length; b++)
                {
                    if (perBand[b] > 0)
                    {
                        bands.Add($"{table.BandName(b)} {perBand[b]}");
                    }
                }

                var books = ClusterFRecipeGate.BooksFor(system).ConvertAll(table.BandName);
                lines.Add(
                    $"{SkillTitle(system.MainSkill)} ({RecipeOwners.GuildFor(system.MainSkill)}): {system.CraftItems.Count} items; " +
                    $"free {free}; {(bands.Count > 0 ? string.Join(", ", bands) : "no banded items")}; own recipe {own}; exempt {exempt}; " +
                    $"books {(books.Count > 0 ? string.Join(", ", books) : "none")}."
                );
            }

            return lines;
        }
    }
}
