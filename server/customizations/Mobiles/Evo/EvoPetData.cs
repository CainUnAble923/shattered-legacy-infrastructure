// cc-P65, F-35: evo pets, the data. Notes: shard-migration notes/cc-P65-evo-pets-framework.md.
//
// Every evo line and stage is a row here; EvoPet reads its line and stage from this table and nothing else. Adding a
// line or a stage is a row, no code. A stage whose art is not shipped yet is a row marked ArtPending: it is skipped
// (the pet steps past it, gates of the stage it enters still apply) until a later prompt ships the art and flips the
// flag. Numbers players feel and staff tune (XP to each stage, the XP rate) are server settings (EvoPetSettings), so
// they change without a build.
//
// Why a table in code, not a data file: the compiler checks every row, tests read it directly, and a row can carry its
// own comments (the kitsune's shapeshift rules below). Nothing here is player-facing text that wants translating.

using System;

namespace Server.Mobiles
{
    /// <summary>The five sounds of a stage, as Mobile's Get*Sound return them.</summary>
    public sealed record EvoSounds(int Idle, int Anger, int Attack, int Hurt, int Death)
    {
        // Mobile's defaults from a BaseSoundID: anger +0, idle +1, attack +2, hurt +3, death +4 (pinned Mobile.cs:8150-8195).
        public static EvoSounds FromBase(int baseId) => new(baseId + 1, baseId, baseId + 2, baseId + 3, baseId + 4);
    }

    /// <summary>
    /// One stage of a line. Bodies has one entry, or several when the owner picks (a gump, once, never changed after).
    /// Gates are on the stage being entered: XP (EvoPetSettings.XpToEnter), MinOwnerLoops, and BondTrial.
    /// </summary>
    public sealed record EvoStage(
        int Number,
        string Name,
        int[] Bodies,
        string[] BodyNames,
        EvoSounds Sounds,
        int MinOwnerLoops = 0,
        bool BondTrial = false,
        bool ArtPending = false,
        string[] Abilities = null // the line role's abilities at this stage: an empty slot until the abilities batch
    )
    {
        public bool HasPick => Bodies.Length > 1;
    }

    /// <summary>A line: a key ("dog"), a name, its role (abilities come later) and its stages, 1 up.</summary>
    public sealed record EvoLine(string Key, string Name, string Role, int StatueItemId, EvoStage[] Stages)
    {
        public EvoStage Stage(int number) =>
            number >= 1 && number <= Stages.Length ? Stages[number - 1] : null;

        /// <summary>The first stage above this one whose art is shipped, or null when there is none (its top for now).</summary>
        public EvoStage NextEnabled(int number)
        {
            for (var n = number + 1; n <= Stages.Length; n++)
            {
                if (!Stages[n - 1].ArtPending)
                {
                    return Stages[n - 1];
                }
            }

            return null;
        }
    }

    /// <summary>The stats of a stage. One curve for every line for now (F-35: stats per stage only).</summary>
    public sealed record EvoStageStats(
        int Str, int Dex, int Int, int Hits,
        int DamageMin, int DamageMax,
        int Physical, int Fire, int Cold, int Poison, int Energy,
        double Wrestling, double Tactics, double MagicResist, double Anatomy,
        int VirtualArmor
    );

    public static class EvoPetData
    {
        // Sounds by body, from the stock creature or ServUO class that uses it (notes section 2.3).
        private static readonly EvoSounds DogSounds = EvoSounds.FromBase(0x85);          // pinned Dog.cs:13
        private static readonly EvoSounds WolfSounds = EvoSounds.FromBase(0xE5);         // ServUO LeatherWolf.cs:24
        private static readonly EvoSounds CuSidheSounds = new(0x577, 0x578, 0x576, 0x576, 0x579); // pinned CuSidhe.cs:109-117
        private static readonly EvoSounds WindrunnerSounds = EvoSounds.FromBase(0xA8);   // ServUO Windrunner.cs:57
        private static readonly EvoSounds CatSounds = EvoSounds.FromBase(0x69);          // pinned Cat.cs:14
        private static readonly EvoSounds CubSounds = EvoSounds.FromBase(362);           // ServUO Eodon TigerCub, Creatures.cs:21
        private static readonly EvoSounds TigerSounds = new(0x673, 0x670, 0x672, 0x672, 0x671); // ServUO WildTiger.cs:83-86
        private static readonly EvoSounds LionSounds = EvoSounds.FromBase(0x3EF);        // ServUO Lion.cs:18
        private static readonly EvoSounds KitsuneSounds = new(0x4DD, 0x4DE, 0x4DC, 0x4DF, 0x4DB); // pinned BakeKitsune.cs:90-98
        private static readonly EvoSounds CraneSounds = EvoSounds.FromBase(0x4D7);       // pinned Crane.cs:12
        private static readonly EvoSounds PhoenixSounds = EvoSounds.FromBase(0x8F);      // pinned Phoenix.cs:13
        private static readonly EvoSounds HiryuSounds = new(0x4FD, 0x4FE, 0x4FC, 0x4FF, 0x4FB); // pinned LesserHiryu.cs:84-92

        private static string[] One(string name) => new[] { name };

        // Gates (F-35 overseer draft, Chase 2026-10-05): the first two step-ups are XP only; the second-to-last stage also
        // needs the owner to have looped once; the last also needs the owner at loop 5 or higher and the Bond Trial.

        public static readonly EvoLine Dog = new("dog", "Dog", "protector", 0x2588, new[]
        {
            new EvoStage(1, "pup", new[] { 217 }, One("dog"), DogSounds),
            new EvoStage(2, "young hound", new[] { 739 }, One("leather wolf"), WolfSounds),
            // The owner picks the breed (F-35 stage picks).
            new EvoStage(3, "hound", new[] { 1547, 1548, 1549, 1546, 1552, 1551 },
                new[] { "Alaskan malamute", "Great Dane", "Saint Bernard", "Newfoundland", "Rottweiler", "Black Russian terrier" },
                DogSounds),
            new EvoStage(4, "guardian hound", new[] { 277 }, One("cu sidhe"), CuSidheSounds, MinOwnerLoops: 1),
            new EvoStage(5, "windrunner", new[] { 1410 }, One("windrunner"), WindrunnerSounds, MinOwnerLoops: 5, BondTrial: true)
        });

        public static readonly EvoLine Cat = new("cat", "Cat", "striker", 0x211B, new[]
        {
            new EvoStage(1, "kitten", new[] { 201 }, One("cat"), CatSounds),
            new EvoStage(2, "cub", new[] { 1309 }, One("baby tiger"), CubSounds),
            new EvoStage(3, "great cat", new[] { 1254, 1426 }, new[] { "tiger", "lion" }, TigerSounds),
            new EvoStage(4, "sabertooth", new[] { 1416 }, One("sabertooth"), TigerSounds, MinOwnerLoops: 1),
            new EvoStage(5, "manticore", new[] { 1573 }, One("manticore"), LionSounds, MinOwnerLoops: 5, BondTrial: true)
        });

        public static readonly EvoLine Fox = new("fox", "Fox", "trickster", 0, new[]
        {
            new EvoStage(1, "kit", new[] { 1423 }, One("fox"), DogSounds),
            // L_Fox.vd, Mobile Megapack (third-party): no body number until a later prompt ships the art.
            new EvoStage(2, "fox", new[] { 0 }, One("fox (art pending)"), DogSounds, ArtPending: true),
            new EvoStage(3, "bake kitsune", new[] { 246 }, One("bake kitsune"), KitsuneSounds, MinOwnerLoops: 1),
            // Stage 4, the kitsune (F-35, decided 2026-10-05). Art pending: a woman form, female human body 401 wearing
            // the fox ears and one tail (the KitsuneEarsTail worn-item route), movement animations only.
            // Shapeshift rules for the build that switches her on (none of this is built yet):
            //   - the fox form (246, Bake Kitsune art) fights; the woman form is movement only (stand, walk, run);
            //   - never send an attack or cast animation while in woman form;
            //   - if she would die in woman form, shift to fox first so the fox's death animation plays;
            //   - auto-shift into fox when combat starts and back to woman a few seconds after it ends, with a smoke
            //     puff each way, plus a manual command for the owner;
            //   - always female; one tail for now (no tail growth).
            new EvoStage(4, "kitsune", new[] { 246 }, One("kitsune (art pending)"), KitsuneSounds, MinOwnerLoops: 5,
                BondTrial: true, ArtPending: true)
        });

        public static readonly EvoLine Bird = new("bird", "Bird", "scout", 0, new[]
        {
            new EvoStage(1, "chick", new[] { 254 }, One("crane"), CraneSounds),
            new EvoStage(2, "firebird", new[] { 832 }, One("phoenix bird"), PhoenixSounds),
            // L_Bird 01.vd and L_Griffin.vd, Mobile Megapack (third-party): art pending.
            new EvoStage(3, "great bird", new[] { 0 }, One("great bird (art pending)"), PhoenixSounds, ArtPending: true),
            new EvoStage(4, "griffin", new[] { 0 }, One("griffin (art pending)"), PhoenixSounds, MinOwnerLoops: 1,
                ArtPending: true),
            new EvoStage(5, "hiryu", new[] { 243 }, One("lesser hiryu"), HiryuSounds, MinOwnerLoops: 5, BondTrial: true)
        });

        public static readonly EvoLine[] Lines = { Dog, Cat, Fox, Bird };

        public static EvoLine Line(string key)
        {
            foreach (var line in Lines)
            {
                if (string.Equals(line.Key, key, StringComparison.OrdinalIgnoreCase))
                {
                    return line;
                }
            }

            return null;
        }

        // The stats curve, stage 1 to 5, the same for every line (placeholder until F-12 and play). Stage 1 is about a
        // town dog (pinned Dog.cs:15-35: Str 27-37, Dex 28-43, Int 29-37, hits 17-22, damage 4-7, MR/Tactics/Wrestling
        // about 20-31, armor 12); stage 5 about a Cu Sidhe (pinned CuSidhe.cs:28-55: Str 1200-1225, Dex 150-170,
        // Int 250-285, hits 1010-1275, damage 21-28, resists 50-65/25-45/70-85/30-50/70-85, Wrestling 90-97,
        // Tactics 90-99, MR 75-90, Anatomy 65-69), but on 1 follower slot (OSI's Cu Sidhe takes 4).
        // A line with fewer stages uses the curve by stage number.
        public static readonly EvoStageStats[] Curve =
        {
            new(32, 35, 30, 20, 4, 7, 12, 5, 5, 5, 5, 25.0, 25.0, 30.0, 0.0, 12),
            new(90, 70, 45, 70, 7, 11, 22, 12, 15, 12, 15, 50.0, 50.0, 45.0, 20.0, 20),
            new(240, 105, 90, 220, 11, 16, 32, 20, 30, 22, 30, 70.0, 70.0, 60.0, 40.0, 30),
            new(560, 135, 170, 520, 16, 22, 44, 28, 52, 32, 52, 85.0, 85.0, 72.0, 55.0, 40),
            new(1210, 160, 265, 1140, 21, 28, 57, 35, 77, 40, 77, 93.0, 95.0, 82.0, 67.0, 50)
        };

        public static EvoStageStats StatsFor(int stage) => Curve[Math.Clamp(stage, 1, Curve.Length) - 1];

        // Stage-1 hue choices: a short placeholder list for Chase to replace (notes section 7). 0 is natural.
        public static readonly (int Hue, string Name)[] Hues =
        {
            (0, "natural"),
            (0x455, "shadow"),
            (0x47E, "snow"),
            (0x2C, "rust"),
            (0x8A5, "sand"),
            (0x59B, "storm"),
            (0x21, "ember")
        };

        // The Echo's lines (F-35's system voice: the player's own looped self, dry and tired). Placeholders for Chase.
        public static readonly string[] StepUpLines =
        {
            "",
            "",
            "The Echo: Look at that. It grew. One of you had to.",
            "The Echo: Bigger teeth. Same appetite. You two suit each other.",
            "The Echo: You have done this before, you know. It was smaller last time.",
            "The Echo: There it is. The one that remembers every loop with you."
        };

        public const string NeedsLoopLine =
            "The Echo: It is ready. You are not. Loop {0} time{1} first, then we will talk.";

        public const string NeedsBondTrialLine =
            "The Echo: One more step, and it wants proof you will not leave it behind. (The Bond Trial is not built yet; staff can mark it done.)";

        public const string PickLine = "The Echo: It is changing. Choose what it becomes; there is no going back.";

        public const string TopForNowLine = "at its top for now";
    }

    /// <summary>Staff-tunable evo numbers, as server settings (modernuo.json), so they change without a build.</summary>
    public static class EvoPetSettings
    {
        // Total XP to enter each stage (stage 1 is where it starts). Placeholders until F-12: a rat kill is worth about
        // 26, a lich about 340 (EvoPet.XpForKill), so stage 2 is an evening of pests and stage 5 a long road.
        public static readonly int[] DefaultXpToEnter = { 0, 0, 500, 3_000, 13_000, 53_000 };

        public static void Configure()
        {
            for (var stage = 2; stage < DefaultXpToEnter.Length; stage++)
            {
                _ = XpToEnter(stage);
            }

            _ = XpRatePercent;
        }

        public static int XpToEnter(int stage) =>
            stage <= 1 ? 0 :
            ServerConfiguration.GetOrUpdateSetting(
                $"evoPets.xpToEnter.stage{stage}",
                stage < DefaultXpToEnter.Length ? DefaultXpToEnter[stage] : DefaultXpToEnter[^1] * (stage - DefaultXpToEnter.Length + 2)
            );

        /// <summary>All XP an evo pet earns is scaled by this, in percent (100 = as the formula gives).</summary>
        public static int XpRatePercent => ServerConfiguration.GetOrUpdateSetting("evoPets.xpRatePercent", 100);
    }
}
