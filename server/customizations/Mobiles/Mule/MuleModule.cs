// cc-P70, F-33 Parts A and B: the mule module item, how it is crafted, and the schematics that teach the recipes.
//
// One class for every module and tier (MuleModule: module id, tier). Crafted in the building guild's craft system
// (MuleModuleCrafts.Rows, P47 5.3's table and its proposals), each tier its own recipe, taught once by buying the guild's
// schematic (GuildSchematicsGump). Tier II is crafted from a tier I module of the same kind, III from II: a pinned craft
// matches resources by type only, so CraftItem-mule-module-tier.patch adds the one check (MuleModuleCrafts.CheckPreviousTier).
//
// Recipes are pinned's own (Recipe, PlayerMobile.AcquireRecipe), registered as quest recipes so no loot or satchel hands
// them out, ids 9000 + module x 10 + tier (stock stops at 602). F-43 (cc-P78) will move them to an account store.

using System;
using System.Collections.Generic;
using System.Linq;
using ModernUO.Serialization;
using Server.Engines.Craft;
using Server.Items;

namespace Server.Mobiles
{
    [SerializationGenerator(0, false)]
    public partial class MuleModule : Item, ICraftable
    {
        [SerializableField(0, setter: "private")]
        [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
        private MuleModuleId _moduleId;

        [SerializableField(1, setter: "private")]
        [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
        private int _tier;

        // Imbued Barding Socket only: the mule properties imbued into it (up to its tier), as "kind:value".
        [SerializableField(2, setter: "private")]
        private List<string> _socketList;

        // Salvage Bin II only: the categories its pickup keeps (MuleModules.PickupCategories).
        [SerializableField(3, setter: "private")]
        private List<string> _filterList;

        public IReadOnlyList<string> Filter => _filterList ??= new List<string>();

        public void ToggleFilter(string category)
        {
            _filterList ??= new List<string>();

            if (!_filterList.Remove(category))
            {
                _filterList.Add(category);
            }

            this.MarkDirty();
        }

        [Constructible]
        public MuleModule() : this(MuleModuleId.OreHopper, 1)
        {
        }

        [Constructible]
        public MuleModule(MuleModuleId moduleId, int tier) : base(MuleModuleData.Info(moduleId)?.ItemId ?? 0x0E76)
        {
            _socketList = new List<string>();
            _filterList = new List<string>();
            Set(moduleId, tier);
        }

        private void Set(MuleModuleId moduleId, int tier)
        {
            _moduleId = moduleId;
            _tier = Math.Clamp(tier, 1, MuleModuleData.MaxTier);
            ItemID = MuleModuleData.Info(moduleId)?.ItemId ?? 0x0E76;
            Hue = MuleModuleData.HueFor(_tier);
            Weight = 5.0;
            InvalidateProperties();
        }

        public MuleModuleInfo Info => MuleModuleData.Info(_moduleId);

        public string GuildKey => Info?.GuildKey;

        public override string DefaultName => $"{Info?.Name ?? "mule module"} {MuleData.GradeName(_tier)}";

        public override void GetProperties(IPropertyList list)
        {
            base.GetProperties(list);

            var info = Info;

            if (info == null)
            {
                return;
            }

            list.Add($"Mule module: {info.GuildName}, tier {MuleData.GradeName(_tier)}");
            list.Add(info.Effects[Math.Min(_tier, info.Effects.Length) - 1]);
            list.Add($"Needs a tier {_tier} or higher mule");

            if (info.StubNote != null)
            {
                list.Add($"Not yet: {info.StubNote}");
            }

            foreach (var s in Sockets)
            {
                list.Add($"Socket: {s.Kind} +{s.Value}");
            }
        }

        // --- Imbued Barding Socket's property sockets ------------------------------------------------------------------

        public IEnumerable<(string Kind, int Value)> Sockets =>
            (_socketList ?? new List<string>()).Select(s => s.Split(':')).Where(p => p.Length == 2 && int.TryParse(p[1], out _))
            .Select(p => (p[0], int.Parse(p[1])));

        /// <summary>Puts a mule property into a free socket (tier = sockets). False if full, unknown, or over its cap.</summary>
        public bool AddSocket(string kind, int value)
        {
            if (_moduleId != MuleModuleId.ImbuedBardingSocket || Sockets.Count() >= _tier ||
                !MuleModules.SocketCaps.TryGetValue(kind, out var cap) || value <= 0 || value > cap || Sockets.Any(s => s.Kind == kind))
            {
                return false;
            }

            _socketList ??= new List<string>();
            _socketList.Add($"{kind}:{value}");
            this.MarkDirty();
            InvalidateProperties();
            return true;
        }

        public int OnCraft(
            int quality, bool makersMark, Mobile from, CraftSystem craftSystem, Type typeRes, BaseTool tool,
            CraftItem craftItem, int resHue
        )
        {
            if (MuleModuleCrafts.Map.TryGetValue(craftItem, out var made))
            {
                Set(made.Id, made.Tier);
            }

            return 1;
        }

        [AfterDeserialization]
        private void AfterDeserialization()
        {
            _socketList ??= new List<string>();
            _filterList ??= new List<string>();
        }
    }

    /// <summary>How each module is crafted: P47 5.3's craft-by table and its proposals. Per tier I, II, III.</summary>
    public sealed record MuleModuleCraft(
        MuleModuleId Id,
        Func<CraftSystem> System,
        string SystemName,
        SkillName? Secondary,
        Type Res, string ResName, int[] ResAmount,
        Type Res2, string Res2Name, int[] Res2Amount,
        Type[] Part, string[] PartName, int[] PartAmount
    );

    public static class MuleModuleCrafts
    {
        public const string Group = "Mule Modules";

        // Main skill to craft: tier I 50, II 70, III 90 (to +40). Secondary: 30, 50, 70 (to +20).
        public static readonly double[] MinSkill = { 50.0, 70.0, 90.0, 100.0, 110.0 };
        public static readonly double[] MinSecondary = { 30.0, 50.0, 70.0, 80.0, 90.0 };

        // Monster parts: tier I a common one (bone, from skeletons), II a rarer one (daemon bone), III one per module
        // from pinned's ML and SA finds. None is invented; where a fitting drop does not exist yet, the notes list it.
        private static Type[] Parts(Type third) => new[] { typeof(Bone), typeof(DaemonBone), third };
        private static string[] PartNames(string third) => new[] { "bone", "daemon bone", third };
        private static readonly int[] PartAmounts = { 10, 5, 2 };

        public static readonly MuleModuleCraft[] Rows =
        {
            new(MuleModuleId.OreHopper, () => DefBlacksmithy.CraftSystem, "Blacksmithy", SkillName.Mining,
                typeof(IronIngot), "iron ingots", new[] { 20, 40, 80 }, typeof(Leather), "leather", new[] { 5, 10, 20 },
                Parts(typeof(DarkSapphire)), PartNames("dark sapphire"), PartAmounts),
            new(MuleModuleId.TimberRack, () => DefCarpentry.CraftSystem, "Carpentry", SkillName.Lumberjacking,
                typeof(Board), "boards", new[] { 30, 60, 120 }, typeof(IronIngot), "iron ingots", new[] { 5, 10, 20 },
                Parts(typeof(BarkFragment)), PartNames("bark fragment"), PartAmounts),
            new(MuleModuleId.ReinforcedSaddlebags, () => DefTailoring.CraftSystem, "Tailoring", null,
                typeof(Leather), "leather", new[] { 20, 40, 80 }, typeof(Cloth), "cloth", new[] { 20, 40, 80 },
                Parts(typeof(SwitchItem)), PartNames("switch"), PartAmounts),
            new(MuleModuleId.ClockworkLoadHarness, () => DefTinkering.CraftSystem, "Tinkering", null,
                typeof(IronIngot), "iron ingots", new[] { 15, 30, 60 }, typeof(Gears), "gears", new[] { 2, 4, 8 },
                Parts(typeof(EcruCitrine)), PartNames("ecru citrine"), PartAmounts),
            new(MuleModuleId.TrailwiseTack, () => DefTailoring.CraftSystem, "Tailoring", SkillName.AnimalLore,
                typeof(Leather), "leather", new[] { 20, 40, 80 }, typeof(Feather), "feathers", new[] { 10, 20, 40 },
                Parts(typeof(Turquoise)), PartNames("turquoise"), PartAmounts),
            new(MuleModuleId.FieldMedicPack, () => DefAlchemy.CraftSystem, "Alchemy", SkillName.Healing,
                typeof(Bandage), "bandages", new[] { 20, 40, 80 }, typeof(Ginseng), "ginseng", new[] { 10, 20, 40 },
                Parts(typeof(LuminescentFungi)), PartNames("luminescent fungi"), PartAmounts),
            new(MuleModuleId.ExpeditionBarding, () => DefBlacksmithy.CraftSystem, "Blacksmithy", SkillName.Tactics,
                typeof(IronIngot), "iron ingots", new[] { 30, 60, 120 }, typeof(Leather), "leather", new[] { 10, 20, 40 },
                Parts(typeof(BrilliantAmber)), PartNames("brilliant amber"), PartAmounts),
            new(MuleModuleId.ArcaneWardHarness, () => DefInscription.CraftSystem, "Inscription", SkillName.Magery,
                typeof(BlankScroll), "blank scrolls", new[] { 10, 20, 40 }, typeof(BlackPearl), "black pearl", new[] { 10, 20, 40 },
                Parts(typeof(BlueDiamond)), PartNames("blue diamond"), PartAmounts),
            new(MuleModuleId.SoulTether, () => DefInscription.CraftSystem, "Inscription", SkillName.SpiritSpeak,
                typeof(BlankScroll), "blank scrolls", new[] { 10, 20, 40 }, typeof(GraveDust), "grave dust", new[] { 10, 20, 40 },
                Parts(typeof(Taint)), PartNames("taint"), PartAmounts),
            new(MuleModuleId.FieldSmelterRig, () => DefBlacksmithy.CraftSystem, "Blacksmithy", SkillName.ArmsLore,
                typeof(IronIngot), "iron ingots", new[] { 25, 50, 100 }, typeof(SulfurousAsh), "sulfurous ash", new[] { 10, 20, 40 },
                Parts(typeof(FireRuby)), PartNames("fire ruby"), PartAmounts),
            new(MuleModuleId.SurveySaddleRoll, () => DefCartography.CraftSystem, "Cartography", SkillName.Fishing,
                typeof(BlankMap), "blank maps", new[] { 5, 10, 20 }, typeof(Leather), "leather", new[] { 5, 10, 20 },
                Parts(typeof(WhitePearl)), PartNames("white pearl"), PartAmounts),
            new(MuleModuleId.ShadowCloak, () => DefTailoring.CraftSystem, "Tailoring", SkillName.Hiding,
                typeof(Cloth), "cloth", new[] { 20, 40, 80 }, typeof(Nightshade), "nightshade", new[] { 10, 20, 40 },
                Parts(typeof(Corruption)), PartNames("corruption"), PartAmounts),
            new(MuleModuleId.SalvageBin, () => DefTinkering.CraftSystem, "Tinkering", null,
                typeof(IronIngot), "iron ingots", new[] { 15, 30, 60 }, typeof(Board), "boards", new[] { 10, 20, 40 },
                Parts(typeof(ParasiticPlant)), PartNames("parasitic plant"), PartAmounts),
            new(MuleModuleId.MinstrelsBells, () => DefTinkering.CraftSystem, "Tinkering", SkillName.Musicianship,
                typeof(IronIngot), "iron ingots", new[] { 10, 20, 40 }, typeof(Gears), "gears", new[] { 1, 2, 4 },
                Parts(typeof(PerfectEmerald)), PartNames("perfect emerald"), PartAmounts),
            new(MuleModuleId.TradersLedger, () => DefInscription.CraftSystem, "Inscription", null,
                typeof(BlankScroll), "blank scrolls", new[] { 10, 20, 40 }, typeof(Leather), "leather", new[] { 5, 10, 20 },
                Parts(typeof(Turquoise)), PartNames("turquoise"), PartAmounts),
            new(MuleModuleId.SteadyHoofWraps, () => DefTailoring.CraftSystem, "Tailoring", SkillName.Bushido,
                typeof(Leather), "leather", new[] { 15, 30, 60 }, typeof(Cloth), "cloth", new[] { 10, 20, 40 },
                Parts(typeof(EcruCitrine)), PartNames("ecru citrine"), PartAmounts),
            new(MuleModuleId.ImbuedBardingSocket, () => DefTinkering.CraftSystem, "Tinkering", SkillName.Imbuing,
                typeof(IronIngot), "iron ingots", new[] { 10, 20, 40 }, typeof(Amethyst), "amethysts", new[] { 5, 10, 20 },
                Parts(typeof(BlueDiamond)), PartNames("blue diamond"), PartAmounts)
        };

        public static MuleModuleCraft Row(MuleModuleId id) => Rows.FirstOrDefault(r => r.Id == id);

        public static int RecipeId(MuleModuleId id, int tier) => 9000 + (int)id * 10 + tier;

        /// <summary>CraftItem -> (module, tier), filled at registration; read by OnCraft and the tier check.</summary>
        public static readonly Dictionary<CraftItem, (MuleModuleId Id, int Tier)> Map = new();

        private static bool _registered;

        public static void Configure() => EventSink.ServerStarted += Register;

        /// <summary>Adds every module tier to its craft system. Idempotent; the test host calls it directly.</summary>
        public static void Register()
        {
            if (_registered)
            {
                return;
            }

            _registered = true;

            foreach (var row in Rows)
            {
                var system = row.System();
                var info = MuleModuleData.Info(row.Id);

                if (system == null || info == null)
                {
                    Console.WriteLine($"[MuleModuleCrafts] WARNING: no {row.SystemName} craft system; {row.Id} is not craftable.");
                    continue;
                }

                for (var tier = 1; tier <= MuleModuleData.MaxTier; tier++)
                {
                    var t = Math.Min(tier, row.ResAmount.Length) - 1;
                    var name = $"{info.Name} {MuleData.GradeName(tier)}";
                    var index = system.AddCraft(typeof(MuleModule), Group, name, info.ItemId,
                        MinSkill[tier - 1], MinSkill[tier - 1] + 40.0, row.Res, row.ResName, row.ResAmount[t], 1044037);

                    if (row.Secondary is { } secondary)
                    {
                        system.AddSkill(index, secondary, MinSecondary[tier - 1], MinSecondary[tier - 1] + 20.0);
                    }

                    system.AddRes(index, row.Res2, row.Res2Name, row.Res2Amount[t], 1044253);
                    system.AddRes(index, row.Part[t], row.PartName[t], row.PartAmount[t], 1044253);
                    system.SetItemHue(index, MuleModuleData.HueFor(tier));
                    system.ForceNonExceptional(index);
                    system.AddQuestRecipe(index, RecipeId(row.Id, tier));

                    Map[system.CraftItems[index]] = (row.Id, tier);
                }
            }
        }

        /// <summary>
        /// Called from CraftItem.ConsumeAttributes (CraftItem-mule-module-tier.patch). A module above tier I needs one of
        /// the same kind one tier lower in the crafter's pack; with consume (a successful craft) it is taken. True for
        /// anything else, and for staff with the testing crystal.
        /// </summary>
        public static bool CheckPreviousTier(Mobile from, CraftItem craftItem, bool consume, ref TextDefinition message)
        {
            if (!Map.TryGetValue(craftItem, out var made) || made.Tier <= 1 || from is PlayerMobile pm && DevTestingCrystal.IsActive(pm))
            {
                return true;
            }

            MuleModule previous = null;

            if (from.Backpack != null)
            {
                foreach (var m in from.Backpack.FindItemsByType<MuleModule>())
                {
                    if (m.ModuleId == made.Id && m.Tier == made.Tier - 1)
                    {
                        previous = m;
                        break;
                    }
                }
            }

            if (previous == null)
            {
                message = $"You need a {MuleModuleData.Info(made.Id)?.Name} {MuleData.GradeName(made.Tier - 1)} in your backpack to build on.";
                return false;
            }

            if (consume)
            {
                previous.Delete();
            }

            return true;
        }
    }
}
