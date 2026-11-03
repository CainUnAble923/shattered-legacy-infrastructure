// cc-P786: the Halloween witch, a field boss for the Halloween window. The idea is from Milva's "Halloween Witch With
// Drop Items 2.0" (D:\UO\third-party\code\halloween-witch, reference only); the code, the numbers and the prizes' code
// are ours. Not ported, as P779 recommended: the Shroud of the Wicked Witch (13 in every resist, night sight, a frog
// form with +5 Str and Int that never expires), the witch's broom (self repair) and the witch house addon.
//
// The witch (HalloweenWitch): a mage of about 900 hits and Magery 110, every number from halloween-witch.json. Her
// clothes are not loot. When she dies her corpse holds one prize, drawn by weight (the Ouija board, the torture block
// deed or the gore rack deed), and her top damager (a pet's or summon's master standing in) gets Festival Tokens while
// a festival season is on (FestivalTokens' rule).
//
// Where she appears: HalloweenWitchSpawns (the wild spots) and the "witch" random encounter (cc-P776's engine, an
// EncounterKind.Boss), both only in the Halloween window.
//
// Saved types use the serialization generator (D40). Every art id was checked in our client
// (shard-migration notes/cc-P786-tools/art-check.txt).

#nullable enable

using System;
using ModernUO.Serialization;
using Server.Engines.RewardStores;
using Server.Items;

namespace Server.Mobiles
{
    [SerializationGenerator(0, false)]
    public partial class HalloweenWitch : BaseCreature
    {
        /// <summary>The wild spot she stands for (HalloweenWitchTable.Spots), or -1: an encounter's witch, or staff's.</summary>
        [SerializableField(0)]
        [SerializedCommandProperty(AccessLevel.GameMaster)]
        private int _spot = -1;

        /// <summary>The drop roll in [0, 1) and the chance roll. Tests replace them.</summary>
        public static Func<double> DropRoll = Utility.RandomDouble;

        public static Func<double> ChanceRoll = Utility.RandomDouble;

        [Constructible]
        public HalloweenWitch() : base(AIType.AI_Mage)
        {
            var t = HalloweenWitchTable.Current;
            Name = t.Name;
            Title = t.Title;
            Body = 0x191;
            Female = true;
            Hue = t.Hue;
            HairItemID = 0x203C;
            HairHue = 0x455;

            SetStr(t.StrMin, t.StrMax);
            SetDex(t.DexMin, t.DexMax);
            SetInt(t.IntMin, t.IntMax);
            SetHits(t.HitsMin, t.HitsMax);
            SetDamage(t.DamageMin, t.DamageMax);
            SetDamageType(ResistanceType.Physical, 50);
            SetDamageType(ResistanceType.Cold, 25);
            SetDamageType(ResistanceType.Poison, 25);

            SetResistance(ResistanceType.Physical, t.ResistMin, t.ResistMax);
            SetResistance(ResistanceType.Fire, t.ResistMin, t.ResistMax);
            SetResistance(ResistanceType.Cold, t.ResistMin, t.ResistMax);
            SetResistance(ResistanceType.Poison, t.ResistMin, t.ResistMax);
            SetResistance(ResistanceType.Energy, t.ResistMin, t.ResistMax);

            SetSkill(SkillName.Magery, t.Magery);
            SetSkill(SkillName.EvalInt, t.EvalInt);
            SetSkill(SkillName.Meditation, t.Meditation);
            SetSkill(SkillName.MagicResist, t.MagicResist);
            SetSkill(SkillName.Tactics, t.Tactics);
            SetSkill(SkillName.Wrestling, t.Wrestling);

            Fame = t.Fame;
            Karma = t.Karma;
            VirtualArmor = t.VirtualArmor;
            Tamable = false;

            // Her clothes are costume, not loot: not movable, so they stay off the corpse.
            EquipItem(new Robe(0x455) { Movable = false });
            EquipItem(new WizardsHat(0x455) { Movable = false });
            EquipItem(new Cloak(0x497) { Movable = false });
            EquipItem(new Shoes(0x455) { Movable = false });

            if (t.GoldMax > 0)
            {
                PackGold(t.GoldMin, t.GoldMax);
            }
        }

        public override string CorpseName => "a witch's corpse";
        public override bool AlwaysMurderer => true;
        public override bool CanRummageCorpses => false;
        public override bool ShowFameTitle => false;
        public override bool ClickTitle => true;

        public override void OnDeath(Container c)
        {
            base.OnDeath(c);

            if (MakeDrop(ChanceRoll(), DropRoll()) is { } prize)
            {
                c.DropItem(prize);
            }

            AwardTokens(FestivalTokens.TopDamager(this));
        }

        /// <summary>The corpse's prize for the two rolls: none past the chance, else one drawn by weight.</summary>
        public static Item? MakeDrop(double chanceRoll, double dropRoll)
        {
            var t = HalloweenWitchTable.Current;
            if (t.Drops.Count == 0 || t.TotalWeight <= 0 || chanceRoll * 100.0 >= t.DropChancePercent)
            {
                return null;
            }

            var row = t.Draw(dropRoll);
            try
            {
                return row.ItemType == null ? null : Activator.CreateInstance(row.ItemType) as Item;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>The top damager's tokens. Returns the tokens given (none out of a festival season, FestivalTokens' rule).</summary>
        public static int AwardTokens(PlayerMobile? top)
        {
            var tokens = HalloweenWitchTable.Current.Tokens;
            if (top == null || tokens <= 0 || FestivalSeasons.Current() == null)
            {
                return 0;
            }

            FestivalTokens.Grant(top, tokens, "the witch is vanquished");
            return tokens;
        }

        public override void OnDelete()
        {
            HalloweenWitchSpawns.NoteGone(this);
            base.OnDelete();
        }
    }
}

namespace Server.Items
{
    /// <summary>
    /// The witch's Ouija board: a toy. Double-click it on the ground or in your pack for a ghostly light and a sound in
    /// a random colour, at most once every few seconds. No charges, no effect on anyone.
    /// </summary>
    [SerializationGenerator(0, false)]
    public partial class WitchOuijaBoard : Item
    {
        private static readonly int[] _hues = [1155, 1156, 1157, 1161, 1159, 1175, 1150, 1172, 1276, 1260];
        private static readonly int[] _sounds = [0x284, 0x47D, 0x474, 0x457, 0xB3, 0x3BD, 0x379, 0x37A, 0x181, 0x115];
        private static readonly int[] _effects = [0x3709, 0x374A, 0x3789, 0x36BD, 0x375A];

        private DateTime _next;

        [Constructible]
        public WitchOuijaBoard() : base(0xFAD) => Hue = 1137;

        public override string DefaultName => "a witch's Ouija board";

        public override double DefaultWeight => 3.0;

        public override void GetProperties(IPropertyList list)
        {
            base.GetProperties(list);
            list.Add("Halloween");
        }

        public override void OnDoubleClick(Mobile from)
        {
            if (!from.InRange(GetWorldLocation(), 2) || from.Map is not { } map || map == Map.Internal)
            {
                from.SendLocalizedMessage(500446); // That is too far away.
                return;
            }

            if (Core.Now < _next)
            {
                from.SendMessage("The planchette will not move again just yet.");
                return;
            }

            _next = Core.Now + TimeSpan.FromSeconds(5);
            from.SendMessage(0x21, "You place your hand on the board...");
            var at = GetWorldLocation();
            Effects.SendLocationEffect(new Point3D(at.X, at.Y, at.Z + 2), map, _effects.RandomElement(), 30, 10,
                _hues.RandomElement(), Utility.Random(3));
            Effects.PlaySound(at, map, _sounds.RandomElement());
        }
    }

    /// <summary>A torture block for a house (the stocks, with a bone pile). Stock art 0x126B-0x1273 and 0x1B09.</summary>
    [SerializationGenerator(0, false)]
    public partial class TortureBlockAddon : BaseAddon
    {
        [Constructible]
        public TortureBlockAddon()
        {
            AddComponent(new AddonComponent(0x126B), 1, 1, 0);
            AddComponent(new AddonComponent(0x126C), 1, 0, 0);
            AddComponent(new AddonComponent(0x1270), 0, 1, 0);
            AddComponent(new AddonComponent(0x126F), 0, 0, 0);
            AddComponent(new AddonComponent(0x1272), -1, 0, 0);
            AddComponent(new AddonComponent(0x1271), -1, 1, 0);
            AddComponent(new AddonComponent(0x1273), -1, -1, 0);
            AddComponent(new AddonComponent(0x126E), 0, -1, 0);
            AddComponent(new AddonComponent(0x126D), 1, -1, 0);
            AddComponent(new AddonComponent(0x1B09), 1, 1, 2);
        }

        public override BaseAddonDeed Deed => new TortureBlockDeed();
    }

    [SerializationGenerator(0, false)]
    public partial class TortureBlockDeed : BaseAddonDeed
    {
        [Constructible]
        public TortureBlockDeed()
        {
        }

        public override BaseAddon Addon => new TortureBlockAddon();

        public override string DefaultName => "a torture block deed";
    }

    /// <summary>A gore rack for a house (the rack, with blood, a whip, a torso and a rib cage). Stock art.</summary>
    [SerializationGenerator(0, false)]
    public partial class GoreRackAddon : BaseAddon
    {
        [Constructible]
        public GoreRackAddon()
        {
            AddComponent(new AddonComponent(0x1252), 0, 1, 0);
            AddComponent(new AddonComponent(0x1251), 1, 1, 0);
            AddComponent(new AddonComponent(0x1255), -1, 1, 0);
            AddComponent(new AddonComponent(0x1250), 1, 0, 0);
            AddComponent(new AddonComponent(0x166E), 0, 0, 3);
            AddComponent(new AddonComponent(0x1CE0), 1, 1, 5);
            AddComponent(new AddonComponent(0x1254), -1, 0, 0);
            AddComponent(new AddonComponent(0x1253), 0, 0, 0);
            AddComponent(new AddonComponent(0x122B), 1, 1, 3);
            AddComponent(new AddonComponent(0x124F), 2, 0, 0);
            AddComponent(new AddonComponent(0x1B17), 2, 0, 3);
            AddComponent(new AddonComponent(0x124E), 2, 1, 0);
            AddComponent(new AddonComponent(0x122C), 2, 1, 3);
        }

        public override BaseAddonDeed Deed => new GoreRackDeed();
    }

    [SerializationGenerator(0, false)]
    public partial class GoreRackDeed : BaseAddonDeed
    {
        [Constructible]
        public GoreRackDeed()
        {
        }

        public override BaseAddon Addon => new GoreRackAddon();

        public override string DefaultName => "a gore rack deed";
    }
}
