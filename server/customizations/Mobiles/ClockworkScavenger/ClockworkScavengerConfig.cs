// cc-P808: the Clockwork Scavenger's numbers. Notes: shard-migration notes/cc-P808-clockwork-scavenger.md.
//
// Configuration/clockwork-scavenger.json, written with the defaults on first start and read at start. A bad file is
// refused whole, every error is printed, and the defaults stay in force. Nothing else in the Scavenger holds a number.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Server.Mobiles
{
    public sealed class ScavengerPart
    {
        public string Type { get; set; } = "";
        public int Amount { get; set; }
        public string Label { get; set; } = "";
    }

    public sealed class ClockworkScavengerConfig
    {
        public const string FileName = "clockwork-scavenger.json";

        /// <summary>The Tinkers' Union rank index (ClusterFGuildSystem.GuildRankIndex) that opens the build quest. 2 is Journeyman.</summary>
        public int MinRank { get; set; } = ClusterFGuildSystem.RankJourneyman;

        /// <summary>What the frame needs in the pack to be assembled. Every part is a stock Tinkering part or ingot.</summary>
        public List<ScavengerPart> Parts { get; set; } =
        [
            new() { Type = "Gears", Amount = 10, Label = "gears" },
            new() { Type = "Springs", Amount = 10, Label = "springs" },
            new() { Type = "ClockParts", Amount = 5, Label = "clock parts" },
            new() { Type = "AxleGears", Amount = 2, Label = "axle with gears" },
            new() { Type = "IronIngot", Amount = 50, Label = "iron ingots" }
        ];

        public int Body { get; set; } = 0x30;   // the stock scorpion's body, in the hue below: a brass crawler
        public int Hue { get; set; } = 0x972;   // bronze
        public int ControlSlots { get; set; } = 1;
        public int TickSeconds { get; set; } = 3;
        public int CorpseRange { get; set; } = 2;  // tiles from the Scavenger to a corpse it loots
        public int OwnerRange { get; set; } = 12;  // it works only this close to its owner
        public int MaxPerTick { get; set; } = 5;   // items lifted per tick
        public int MuleRange { get; set; } = 12;   // Load Harness III: how near the mule must be
        public List<string> DefaultFilter { get; set; } = ["gold", "gems", "reagents"];

        public static ClockworkScavengerConfig Current { get; set; } = new();

        public static string FilePath => Path.Combine(Core.BaseDirectory, "Configuration", FileName);

        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never
        };

        public static string ToJson(ClockworkScavengerConfig c) => JsonSerializer.Serialize(c, Options);

        /// <summary>Every problem with the table, or none. A part must name a real item type and a positive amount.</summary>
        public List<string> Validate()
        {
            var errors = new List<string>();

            void Range(string name, int v, int min, int max)
            {
                if (v < min || v > max)
                {
                    errors.Add($"{name} is {v}; it must be {min} to {max}");
                }
            }

            Range("minRank", MinRank, 0, ClusterFGuildSystem.RankTop);
            Range("body", Body, 1, 0xFFFF);
            Range("hue", Hue, 0, 0xFFFF);
            Range("controlSlots", ControlSlots, 0, 5);
            Range("tickSeconds", TickSeconds, 1, 60);
            Range("corpseRange", CorpseRange, 1, 12);
            Range("ownerRange", OwnerRange, 1, 24);
            Range("maxPerTick", MaxPerTick, 1, 50);
            Range("muleRange", MuleRange, 1, 24);

            if (Parts == null || Parts.Count == 0)
            {
                errors.Add("parts is empty");
            }
            else
            {
                foreach (var p in Parts)
                {
                    if (p == null || PartType(p) is not { } t || !typeof(Item).IsAssignableFrom(t))
                    {
                        errors.Add($"part \"{p?.Type}\" is not an item type");
                    }
                    else if (p.Amount < 1 || p.Amount > 1000)
                    {
                        errors.Add($"part \"{p.Type}\" amount is {p.Amount}; it must be 1 to 1000");
                    }
                }
            }

            foreach (var c in DefaultFilter ?? [])
            {
                if (Array.IndexOf(MuleModules.PickupCategories, c) < 0)
                {
                    errors.Add($"defaultFilter \"{c}\" is not one of: {string.Join(", ", MuleModules.PickupCategories)}");
                }
            }

            return errors;
        }

        public static Type PartType(ScavengerPart p) =>
            string.IsNullOrWhiteSpace(p?.Type) ? null : AssemblyHandler.FindTypeByName(p.Type);

        /// <summary>Reads the file into Current, or writes the defaults when there is none. Returns the lines to print.</summary>
        public static List<string> LoadOrWriteDefaults(string path)
        {
            var lines = new List<string>();

            if (!File.Exists(path))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, ToJson(new ClockworkScavengerConfig()));
                Current = new ClockworkScavengerConfig();
                lines.Add($"Clockwork Scavenger: wrote the defaults to {path}");
                return lines;
            }

            ClockworkScavengerConfig read = null;

            try
            {
                read = JsonSerializer.Deserialize<ClockworkScavengerConfig>(File.ReadAllText(path), Options);
            }
            catch (JsonException ex)
            {
                lines.Add($"Clockwork Scavenger: {path} is not valid JSON ({ex.Message}); the defaults stay in force");
                return lines;
            }

            var errors = read?.Validate() ?? ["the file is empty"];

            if (errors.Count > 0)
            {
                lines.AddRange(errors.Select(e => $"Clockwork Scavenger: {path}: {e}"));
                lines.Add("Clockwork Scavenger: the file was refused whole; the defaults stay in force");
                return lines;
            }

            Current = read;
            return lines;
        }

        public static void Configure()
        {
            foreach (var line in LoadOrWriteDefaults(FilePath))
            {
                Console.WriteLine(line);
            }
        }
    }
}
