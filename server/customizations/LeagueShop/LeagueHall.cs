// cc-P243 (F-13): the League Hall, house decor in the League shop. Notes: shard-migration
// notes/cc-P243-league-hall-decor-rewards.md.
//
// Nineteen pieces on the shop's Hall page (LeagueHallGump), each opened by AP and bought with Renown through P229's own
// LeagueShop.Purchase (AP checked, exactly the price taken, LifetimeRenown and League rank never moved):
//   display cases   RedRooster's, a shelf and a glass insert on one tile; the two parts take a dye tub apart, as he
//                   built them (LeagueHallComponent.Dye)
//   statues         Hestia's dragons and gargoyle
//   wall trophies   Hestia's and Archaaz's heads on plaques (EA-derived art: our PNG overrides only, as cc-P140's crow)
//   mounted heads   Archaaz's
// The art is our player package's 15592-15629 (registry.csv); LeagueHallArt gives the server the same tiledata.
//
// What the buyer gets is a deed (LeagueHallDeed) that places the piece in a house the buyer owns: pinned's addon
// placement (BaseAddonDeed's target, BaseAddon.CouldFit, which needs house.IsOwner), with pinned MinotaurStatue's
// facing choice before it (Items/Special/Veteran Rewards/MinotaurStatue.cs) and pinned TrophyDeed's wall rule for the
// trophies (Items/Skill Items/Carpenter Items/TaxidermyKit.cs: a north or west wall beside the spot). Chopping a placed
// piece gives its deed back (pinned BaseAddon.OnChop).
//
// F-13 guardrails: decor only. No item properties, no skill or stat effects. The deed is bound to the buying account
// (P79's AccountBinding): it cannot be traded, dropped or handed over, so nobody gets a piece their own AP has not opened.
// A placed piece stays in its house, and its deed comes back bound to the same account.
//
// Prices and AP are settings (clusterf.leagueHall.<key>.ap and .renown), the defaults below, proposed in the notes.

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using ModernUO.Serialization;
using Server.Gumps;
using Server.Items;
using Server.Mobiles;
using Server.Multis;
using Server.Network;
using Server.Spells;
using Server.Targeting;

namespace Server.Engines.LeagueRewards
{
    public enum LeagueHallKind
    {
        DisplayCase,
        Statue,
        WallTrophy,
        MountedHead
    }

    /// <summary>One part of a piece: its art, its offset, and whether it takes a dye tub on its own.</summary>
    public sealed record LeagueHallPart(int ItemID, bool Dyeable = false);

    /// <summary>One way a piece can stand: its label in the facing choice, its parts, and the wall it needs (none: Zero).</summary>
    public sealed record LeagueHallFacing(string Label, LeagueHallPart[] Parts, Point3D Wall);

    public sealed record LeagueHallPiece(string Key, string Label, LeagueHallKind Kind, int DefaultAp, int DefaultRenown,
        LeagueHallFacing[] Facings)
    {
        public string ApSetting => $"clusterf.leagueHall.{Key}.ap";
        public string RenownSetting => $"clusterf.leagueHall.{Key}.renown";

        public int Ap => ServerConfiguration.GetOrUpdateSetting(ApSetting, DefaultAp);
        public int Renown => ServerConfiguration.GetOrUpdateSetting(RenownSetting, DefaultRenown);
    }

    public static class LeagueHall
    {
        public const string KeyPrefix = "hall.";

        // pinned TrophyAddon: a north-wall trophy needs a wall at (x, y - 1), a west-wall one at (x - 1, y).
        public static readonly Point3D NorthWall = new(0, -1, 0);
        public static readonly Point3D WestWall = new(-1, 0, 0);

        private static LeagueHallFacing[] Case(int insertSouth, int insertEast) =>
        [
            new("Facing south", [new(LeagueHallArt.ShelfSouth, true), new(insertSouth, true)], Point3D.Zero),
            new("Facing east", [new(LeagueHallArt.ShelfEast, true), new(insertEast, true)], Point3D.Zero)
        ];

        private static LeagueHallFacing[] Floor(int south, int east) =>
        [
            new("Facing south", [new(south)], Point3D.Zero),
            new("Facing east", [new(east)], Point3D.Zero)
        ];

        private static LeagueHallFacing[] One(int id) => [new("As it stands", [new(id)], Point3D.Zero)];

        private static LeagueHallFacing[] Wall(int north, int west) =>
        [
            new("On a north wall", [new(north)], NorthWall),
            new("On a west wall", [new(west)], WestWall)
        ];

        // In AP tier order, then price. The defaults are proposals against P229's starter table (notes).
        public static readonly LeagueHallPiece[] Pieces =
        [
            new("case.glass", "Display case: glass", LeagueHallKind.DisplayCase, 250, 150, Case(0x3CF0, 0x3CF1)),
            new("case.books", "Display case: books", LeagueHallKind.DisplayCase, 250, 200, Case(0x3CEA, 0x3CEB)),
            new("case.gems", "Display case: gems", LeagueHallKind.DisplayCase, 250, 200, Case(0x3CEE, 0x3CEF)),
            new("case.huedbooks", "Display case: dyeable books", LeagueHallKind.DisplayCase, 250, 250, Case(0x3CEC, 0x3CED)),
            new("trophy.skull", "Wall trophy: skull", LeagueHallKind.WallTrophy, 250, 200, Wall(0x3CFE, 0x3CFF)),
            new("head.oni", "Mounted head: oni", LeagueHallKind.MountedHead, 500, 250, Floor(0x3D0C, 0x3D0D)),
            new("trophy.minotaur", "Wall trophy: minotaur", LeagueHallKind.WallTrophy, 500, 300, Wall(0x3CF8, 0x3CF9)),
            new("trophy.sabertooth", "Wall trophy: sabertooth", LeagueHallKind.WallTrophy, 500, 300, Wall(0x3CFA, 0x3CFB)),
            new("trophy.troglodyte", "Wall trophy: troglodyte", LeagueHallKind.WallTrophy, 500, 300, Wall(0x3CFC, 0x3CFD)),
            new("statue.gargoyle", "Statue: grinning gargoyle", LeagueHallKind.Statue, 500, 350, One(0x3CF7)),
            new("head.titan", "Mounted head: titan", LeagueHallKind.MountedHead, 1000, 300, Floor(0x3D0A, 0x3D0B)),
            new("trophy.oni", "Wall trophy: oni", LeagueHallKind.WallTrophy, 1000, 450, Wall(0x3D02, 0x3D03)),
            new("trophy.ophidian", "Wall trophy: ophidian matriarch", LeagueHallKind.WallTrophy, 1000, 450, Wall(0x3D04, 0x3D05)),
            new("trophy.terathan", "Wall trophy: terathan matriarch", LeagueHallKind.WallTrophy, 1000, 450, Wall(0x3D06, 0x3D07)),
            new("trophy.paroxysmus", "Wall trophy: paroxysmus", LeagueHallKind.WallTrophy, 1000, 500, Wall(0x3D08, 0x3D09)),
            new("statue.dragon", "Statue: winged dragon", LeagueHallKind.Statue, 1000, 600, Floor(0x3CF2, 0x3CF3)),
            new("statue.darkdragon", "Statue: dark dragon", LeagueHallKind.Statue, 1000, 600, Floor(0x3CF4, 0x3CF5)),
            new("trophy.titan", "Wall trophy: titan", LeagueHallKind.WallTrophy, 1750, 600, Wall(0x3D00, 0x3D01)),
            new("statue.jeweled", "Statue: jeweled dragon", LeagueHallKind.Statue, 1750, 1000, One(0x3CF6))
        ];

        public static LeagueHallPiece? Find(string key) => Pieces.FirstOrDefault(p => p.Key == key);

        /// <summary>The Hall's rows as shop entries, priced from the settings now, ordered by AP tier then price.</summary>
        public static LeagueShopEntry[] Stock() =>
            Pieces
                .Select(p => (Piece: p, Ap: p.Ap, Renown: p.Renown))
                .OrderBy(r => r.Ap)
                .ThenBy(r => r.Renown)
                .Select(r => new LeagueShopEntry(KeyPrefix + r.Piece.Key, r.Piece.Label, r.Ap, r.Renown, LeagueShopKind.Decor,
                    r.Piece.Facings[0].Parts[0].ItemID, 0, MakeDeed(r.Piece)))
                .ToArray();

        public static LeagueShopEntry? FindEntry(string key) => Stock().FirstOrDefault(e => e.Key == key);

        private static Func<PlayerMobile, List<Item>> MakeDeed(LeagueHallPiece piece) =>
            pm => [new LeagueHallDeed(piece.Key, pm.Account?.Username ?? "")];
    }

    /// <summary>
    /// The Hall art's tiledata on the server (the server reads the unpatched EA tiledata, where these are "UNUSED" slots:
    /// see ShatteredLegacyArt). The values are records.json's: player-package\tests\Build-UoOverrides.Tests.ps1 compares them.
    /// </summary>
    public static class LeagueHallArt
    {
        public sealed record Record(int Id, string Name, TileFlag Flags, int Height);

        public const int FirstId = 0x3CE8;
        public const int LastId = 0x3D0D;

        public const int ShelfSouth = 0x3CE8;
        public const int ShelfEast = 0x3CE9;

        // OSI's bookcase 0xA97 (Impassable | ArticleA, height 12) for the shelf; its inserts draw on the shelf's tile and
        // block nothing (ArticleA; Book 5 and 6 are partial hue, as their author made them).
        private const TileFlag Shelf = TileFlag.Impassable | TileFlag.ArticleA;
        private const TileFlag Insert = TileFlag.ArticleA;
        private const TileFlag InsertPh = TileFlag.ArticleA | TileFlag.PartialHue;

        // OSI's statue 0x1224 (ArticleA) with Impassable, height 20, as cc-P34's statue.
        private const TileFlag Statue = TileFlag.Impassable | TileFlag.ArticleA;

        // OSI's trophy 0x1E60 (ArticleA, height 0); the mounted heads stand 2 high.
        private const TileFlag Trophy = TileFlag.ArticleA;

        public static readonly Record[] Records =
        [
            new(0x3CE8, "display case", Shelf, 12), new(0x3CE9, "display case", Shelf, 12),
            new(0x3CEA, "case of books", Insert, 0), new(0x3CEB, "case of books", Insert, 0),
            new(0x3CEC, "case of books", InsertPh, 0), new(0x3CED, "case of books", InsertPh, 0),
            new(0x3CEE, "case of gems", Insert, 0), new(0x3CEF, "case of gems", Insert, 0),
            new(0x3CF0, "glass case", Insert, 0), new(0x3CF1, "glass case", Insert, 0),
            new(0x3CF2, "dragon statue", Statue, 20), new(0x3CF3, "dragon statue", Statue, 20),
            new(0x3CF4, "dragon statue", Statue, 20), new(0x3CF5, "dragon statue", Statue, 20),
            new(0x3CF6, "dragon statue", Statue, 20), new(0x3CF7, "gargoyle statue", Statue, 20),
            new(0x3CF8, "minotaur trophy", Trophy, 0), new(0x3CF9, "minotaur trophy", Trophy, 0),
            new(0x3CFA, "sabertooth trophy", Trophy, 0), new(0x3CFB, "sabertooth trophy", Trophy, 0),
            new(0x3CFC, "troglodyte trophy", Trophy, 0), new(0x3CFD, "troglodyte trophy", Trophy, 0),
            new(0x3CFE, "skull trophy", Trophy, 0), new(0x3CFF, "skull trophy", Trophy, 0),
            new(0x3D00, "titan trophy", Trophy, 0), new(0x3D01, "titan trophy", Trophy, 0),
            new(0x3D02, "oni trophy", Trophy, 0), new(0x3D03, "oni trophy", Trophy, 0),
            new(0x3D04, "ophidian trophy", Trophy, 0), new(0x3D05, "ophidian trophy", Trophy, 0),
            new(0x3D06, "terathan trophy", Trophy, 0), new(0x3D07, "terathan trophy", Trophy, 0),
            new(0x3D08, "paroxysmus trophy", Trophy, 0), new(0x3D09, "paroxysmus trophy", Trophy, 0),
            new(0x3D0A, "titan head", Trophy, 2), new(0x3D0B, "titan head", Trophy, 2),
            new(0x3D0C, "oni head", Trophy, 2), new(0x3D0D, "oni head", Trophy, 2)
        ];

        public static void Apply()
        {
            foreach (var r in Records)
            {
                TileData.ItemTable[r.Id] = new ItemData(r.Name, r.Flags, 255, 0, 0, 0, 0, r.Height);
            }
        }
    }

    /// <summary>The facing choice before placing, as pinned's RewardOptionGump offers MinotaurStatue's, with our own labels.</summary>
    public class LeagueHallFacingGump : Gump
    {
        public const int BtnFacingBase = 10;

        private readonly LeagueHallDeed _deed;

        public LeagueHallFacingGump(PlayerMobile pm, LeagueHallDeed deed, LeagueHallPiece piece) : base(120, 100)
        {
            pm.CloseGump<LeagueHallFacingGump>();
            _deed = deed;

            var h = 70 + piece.Facings.Length * 26;
            AddBackground(0, 0, 300, h, 9270);
            RequirementStyle.AddSolidPanel(this, 300, h);
            AddLabel(20, 16, RequirementStyle.HeadingHue, piece.Label);

            for (var i = 0; i < piece.Facings.Length; i++)
            {
                AddButton(20, 44 + i * 26, 4005, 4007, BtnFacingBase + i);
                AddLabel(56, 46 + i * 26, RequirementStyle.InfoHue, piece.Facings[i].Label);
            }
        }

        public override void OnResponse(NetState state, in RelayInfo info)
        {
            if (state.Mobile is not PlayerMobile pm || _deed.Deleted)
            {
                return;
            }

            var facing = info.ButtonID - BtnFacingBase;
            if (facing >= 0)
            {
                _deed.BeginPlacing(pm, facing);
            }
        }
    }
}

namespace Server.Items
{
    using Server.Engines.LeagueRewards;

    /// <summary>A part of a placed Hall piece. A display case's two parts take a dye tub apart (an owner or co-owner).</summary>
    [SerializationGenerator(0, false)]
    public partial class LeagueHallComponent : AddonComponent, IDyable
    {
        [SerializableField(0)]
        private bool _dyeable;

        [SerializableField(1)]
        private Point3D _wall;

        public LeagueHallComponent(int itemID, bool dyeable, Point3D wall) : base(itemID)
        {
            _dyeable = dyeable;
            _wall = wall;
        }

        public override bool NeedsWall => _wall != Point3D.Zero;

        public override Point3D WallPosition => _wall;

        public bool Dye(Mobile from, DyeTub sender)
        {
            if (Deleted || !_dyeable)
            {
                return false;
            }

            if (BaseHouse.FindHouseAt(this)?.IsCoOwner(from) != true)
            {
                from.SendLocalizedMessage(501023); // You must be the owner to use this item.
                return false;
            }

            Hue = sender.DyedHue;
            return true;
        }
    }

    /// <summary>A placed Hall piece: its parts in one facing. Each part keeps its own hue (a case's shelf and insert dye apart).</summary>
    [SerializationGenerator(0, false)]
    public partial class LeagueHallAddon : BaseAddon
    {
        [SerializableField(0)]
        [SerializedCommandProperty(AccessLevel.GameMaster)]
        private string _pieceKey;

        [SerializableField(1)]
        [SerializedCommandProperty(AccessLevel.GameMaster)]
        private int _facing;

        [SerializableField(2)]
        [SerializedCommandProperty(AccessLevel.GameMaster)]
        private string _boundAccount;

        public LeagueHallAddon(string pieceKey, int facing, string boundAccount)
        {
            _pieceKey = pieceKey;
            _facing = facing;
            _boundAccount = boundAccount;

            var piece = LeagueHall.Find(pieceKey);
            if (piece == null)
            {
                return;
            }

            var f = piece.Facings[Math.Clamp(facing, 0, piece.Facings.Length - 1)];
            foreach (var part in f.Parts)
            {
                AddComponent(new LeagueHallComponent(part.ItemID, part.Dyeable, f.Wall), 0, 0, 0);
            }
        }

        public override bool ShareHue => false;

        public override string DefaultName => LeagueHall.Find(_pieceKey)?.Label ?? "League Hall decor";

        public override BaseAddonDeed Deed => new LeagueHallDeed(_pieceKey, _boundAccount);
    }

    /// <summary>
    /// The deed the Hall sells: bound to the buying account, placed in a house that account's character owns. A piece with
    /// more than one facing asks which first.
    /// </summary>
    [SerializationGenerator(0, false)]
    public partial class LeagueHallDeed : BaseAddonDeed
    {
        [SerializableField(0)]
        [SerializedCommandProperty(AccessLevel.GameMaster)]
        private string _pieceKey;

        [SerializableField(1)]
        [SerializedCommandProperty(AccessLevel.GameMaster)]
        private string _boundAccount;

        private int _facing;

        public LeagueHallDeed(string pieceKey, string boundAccount)
        {
            _pieceKey = pieceKey;
            _boundAccount = boundAccount;
            LootType = LootType.Regular;
        }

        public LeagueHallPiece? Piece => LeagueHall.Find(_pieceKey);

        public override string DefaultName => $"a deed for {Piece?.Label ?? "League Hall decor"}";

        public override BaseAddon Addon => new LeagueHallAddon(_pieceKey, _facing, _boundAccount);

        public override void GetProperties(IPropertyList list)
        {
            base.GetProperties(list);
            list.Add("League Hall decor. Bound to its account; placed in a house you own.");
        }

        public override void OnDoubleClick(Mobile from)
        {
            if (!AccountBinding.SameAccount(from, _boundAccount))
            {
                from.SendMessage(0x22, AccountBinding.Refusal);
                return;
            }

            if (!IsChildOf(from.Backpack))
            {
                from.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
                return;
            }

            if (Piece is not { } piece || from is not PlayerMobile pm)
            {
                return;
            }

            if (piece.Facings.Length > 1)
            {
                pm.SendGump(new LeagueHallFacingGump(pm, this, piece));
            }
            else
            {
                BeginPlacing(pm, 0);
            }
        }

        public void BeginPlacing(PlayerMobile pm, int facing)
        {
            if (Deleted || !IsChildOf(pm.Backpack) || Piece is not { } piece || facing >= piece.Facings.Length)
            {
                return;
            }

            _facing = facing;
            pm.SendMessage(0x44, $"Where in your house? ({piece.Facings[facing].Label.ToLowerInvariant()})");
            pm.Target = new PlaceTarget(this, facing);
        }

        /// <summary>
        /// Places the piece at the spot in the given facing: pinned BaseAddonDeed's target, step for step (the surface
        /// top, BaseAddon.CouldFit with its house-owner check, the same refusal lines). The deed is used up only on success.
        /// </summary>
        public AddonFitResult TryPlace(Mobile from, IPoint3D? p, int facing)
        {
            var map = from.Map;
            if (p == null || map == null || Deleted)
            {
                return AddonFitResult.Blocked;
            }

            if (!AccountBinding.SameAccount(from, _boundAccount))
            {
                from.SendMessage(0x22, AccountBinding.Refusal);
                return AddonFitResult.NotInHouse;
            }

            if (!IsChildOf(from.Backpack))
            {
                from.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
                return AddonFitResult.Blocked;
            }

            _facing = facing;
            var addon = Addon;
            SpellHelper.GetSurfaceTop(ref p);
            BaseHouse? house = null;
            var res = addon.CouldFit(p, map, from, ref house);

            switch (res)
            {
                case AddonFitResult.Valid:
                    addon.MoveToWorld(new Point3D(p), map);
                    house!.Addons.Add(addon);
                    Delete();
                    return res;
                case AddonFitResult.Blocked:
                    from.SendLocalizedMessage(500269); // You cannot build that there.
                    break;
                case AddonFitResult.NotInHouse:
                    from.SendLocalizedMessage(500274); // You can only place this in a house that you own!
                    break;
                case AddonFitResult.DoorTooClose:
                    from.SendLocalizedMessage(500271); // You cannot build near the door.
                    break;
                case AddonFitResult.NoWall:
                    from.SendLocalizedMessage(500268); // This object needs to be mounted on something.
                    break;
            }

            addon.Delete();
            return res;
        }

        private class PlaceTarget : Target
        {
            private readonly LeagueHallDeed _deed;
            private readonly int _facing;

            public PlaceTarget(LeagueHallDeed deed, int facing) : base(-1, true, TargetFlags.None)
            {
                _deed = deed;
                _facing = facing;
                CheckLOS = false;
            }

            protected override void OnTarget(Mobile from, object targeted) => _deed.TryPlace(from, targeted as IPoint3D, _facing);
        }

        // -- Bound to the buying account (P79's AccountBinding, as LootBox) --

        public override bool AllowSecureTrade(Mobile from, Mobile to, Mobile newOwner, bool accepted) => AccountBinding.Refuse(from);

        public override bool OnDroppedToWorld(Mobile from, Point3D p) => AccountBinding.Refuse(from);

        public override bool OnDroppedToMobile(Mobile from, Mobile target) =>
            target == from && AccountBinding.SameAccount(from, _boundAccount) ? base.OnDroppedToMobile(from, target) : AccountBinding.Refuse(from);

        public override bool OnDroppedInto(Mobile from, Container target, Point3D p) =>
            AccountBinding.MayRestIn(target, _boundAccount) ? base.OnDroppedInto(from, target, p) : AccountBinding.Refuse(from);

        public override bool OnDroppedOnto(Mobile from, Item target) =>
            AccountBinding.MayRestIn(target, _boundAccount) ? base.OnDroppedOnto(from, target) : AccountBinding.Refuse(from);

        public override bool CheckLift(Mobile from, Item item, ref LRReason reject)
        {
            if (from.AccessLevel < AccessLevel.GameMaster && !AccountBinding.SameAccount(from, _boundAccount))
            {
                reject = LRReason.CannotLift;
                return false;
            }

            return base.CheckLift(from, item, ref reject);
        }
    }
}
