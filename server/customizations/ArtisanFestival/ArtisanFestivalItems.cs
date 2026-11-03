// cc-P783: the Artisan Festival pieces as two item types, both data-driven from ArtisanFestivalTable by a saved piece key
// (no class per piece: Sareus's pack had 64). A lantern or sculpture is an ArtisanFestivalLight (pinned BaseLight: lit and
// unlit, never burning out); a pillow or tapestry is an ArtisanFestivalDecor. Either turns to its other facing when
// double-clicked in its owner's pack, as our LootDecor does (pinned's interior decorator only turns items whose flip
// IDs are fixed in an attribute). A light double-clicked where it stands lights or goes out. Notes in shard-migration
// notes/cc-P783-artisan-festival-decor.md. Both types are new at version 0 (D40: no older data to read).

using System;
using System.Linq;
using ModernUO.Serialization;
using Server.Commands;
using Server.Engines.ArtisanFestival;

namespace Server.Items
{
    /// <summary>Shared rules of the two piece types.</summary>
    public interface IArtisanPiece
    {
        string PieceKey { get; }
        int Facing { get; }
        bool Turn(Mobile from);
    }

    [SerializationGenerator(0, false)]
    public partial class ArtisanFestivalLight : BaseLight, IArtisanPiece
    {
        [SerializableField(0)]
        [SerializedCommandProperty(AccessLevel.GameMaster)]
        private string _pieceKey;

        [SerializableField(1)]
        [SerializedCommandProperty(AccessLevel.GameMaster)]
        private int _facing;

        [Constructible]
        public ArtisanFestivalLight() : this("honor-lantern")
        {
        }

        public ArtisanFestivalLight(string key) : base(ArtisanFestival.FacingOf(key, 0)?.Id ?? 0x9DE5)
        {
            _pieceKey = key;
            Duration = TimeSpan.Zero; // never burns out; a new piece starts unlit
            Light = ArtisanFestivalTable.Current.LightType;
            Weight = 2.0;
        }

        public ArtisanPiece Piece => ArtisanFestivalTable.Current.Piece(_pieceKey);

        public override string DefaultName => Piece?.Name ?? "an Artisan Festival piece";

        public override int LitItemID => ArtisanFestival.FacingOf(_pieceKey, _facing)?.Lit ?? ItemID;

        public override int UnlitItemID => ArtisanFestival.FacingOf(_pieceKey, _facing)?.Id ?? ItemID;

        string IArtisanPiece.PieceKey => _pieceKey;

        int IArtisanPiece.Facing => _facing;

        public override void GetProperties(IPropertyList list)
        {
            base.GetProperties(list);
            ArtisanFestival.AddProperties(Piece, list, true);
        }

        public bool Turn(Mobile from)
        {
            var count = Piece?.Facings.Count ?? 0;
            if (count < 2)
                return false;

            Facing = (_facing + 1) % count;
            ItemID = Burning ? LitItemID : UnlitItemID;
            return true;
        }

        public override void OnDoubleClick(Mobile from)
        {
            if (IsChildOf(from.Backpack))
            {
                if (!Turn(from))
                    from.SendMessage("It faces only one way. Set it down and double-click it to light it.");
                return;
            }

            base.OnDoubleClick(from);
        }
    }

    [SerializationGenerator(0, false)]
    public partial class ArtisanFestivalDecor : Item, IArtisanPiece
    {
        [SerializableField(0)]
        [SerializedCommandProperty(AccessLevel.GameMaster)]
        private string _pieceKey;

        [SerializableField(1)]
        [SerializedCommandProperty(AccessLevel.GameMaster)]
        private int _facing;

        [Constructible]
        public ArtisanFestivalDecor() : this("honor-pillow")
        {
        }

        public ArtisanFestivalDecor(string key) : base(ArtisanFestival.FacingOf(key, 0)?.Id ?? 0x9E5D)
        {
            _pieceKey = key;
            Weight = 2.0;
        }

        public ArtisanPiece Piece => ArtisanFestivalTable.Current.Piece(_pieceKey);

        public override string DefaultName => Piece?.Name ?? "an Artisan Festival piece";

        string IArtisanPiece.PieceKey => _pieceKey;

        int IArtisanPiece.Facing => _facing;

        public override void GetProperties(IPropertyList list)
        {
            base.GetProperties(list);
            ArtisanFestival.AddProperties(Piece, list, false);
        }

        public bool Turn(Mobile from)
        {
            var count = Piece?.Facings.Count ?? 0;
            if (count < 2)
                return false;

            Facing = (_facing + 1) % count;
            ItemID = Piece.Facings[_facing].Id;
            return true;
        }

        public override void OnDoubleClick(Mobile from)
        {
            if (!IsChildOf(from.Backpack))
            {
                from.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
                return;
            }

            Turn(from);
        }
    }
}

namespace Server.Engines.ArtisanFestival
{
    using Server.Items;

    /// <summary>Makes the pieces, words their properties, and loads the table (cc-P783).</summary>
    public static class ArtisanFestival
    {
        public static void Configure()
        {
            foreach (var line in ArtisanFestivalTable.LoadOrWriteDefaults(ArtisanFestivalTable.FilePath))
                Console.WriteLine($"[ArtisanFestival] {line}");

            CommandSystem.Register("ArtisanFestivalStaff", AccessLevel.GameMaster, OnStaffCommand);
        }

        public static ArtisanFacing FacingOf(string key, int facing)
        {
            var piece = ArtisanFestivalTable.Current.Piece(key);
            return piece == null || piece.Facings.Count == 0 ? null : piece.Facings[Math.Clamp(facing, 0, piece.Facings.Count - 1)];
        }

        /// <summary>A new piece by its key: a light for a lantern or sculpture, decor otherwise; null for an unknown key.</summary>
        public static Item Create(string key)
        {
            var piece = ArtisanFestivalTable.Current.Piece(key);
            if (piece == null)
                return null;

            return piece.IsLight ? new ArtisanFestivalLight(piece.Key) : new ArtisanFestivalDecor(piece.Key);
        }

        public static void AddProperties(ArtisanPiece piece, IPropertyList list, bool light)
        {
            if (piece == null)
                return;

            list.Add($"Artisan Festival: {piece.Virtue}");
            var turn = piece.Facings.Count > 1 ? "double-click in your pack to turn it" : null;
            var lit = light ? "double-click where it stands to light it" : null;
            list.Add(string.Join("; ", new[] { "House decor", turn, lit }.Where(s => s != null)));
        }

        [Usage("ArtisanFestivalStaff [reload|status|give <key>]")]
        [Description("reload reads Configuration/artisan-festival.json again (a bad file is refused whole); status says the numbers in force; give puts a piece in your pack. The Festival Peddler's rows change at the next restart.")]
        [ShardCommand(CommandCategory.DevTool, Shard = CommandShard.Any)]
        private static void OnStaffCommand(CommandEventArgs e)
        {
            var mode = e.Length > 0 ? e.GetString(0).ToLowerInvariant() : "status";
            var t = ArtisanFestivalTable.Current;
            switch (mode)
            {
                case "reload":
                    foreach (var line in ArtisanFestivalTable.Reload(ArtisanFestivalTable.FilePath))
                        e.Mobile.SendMessage(line);
                    return;
                case "give" when e.Length > 1:
                    if (Create(e.GetString(1)) is { } item)
                        e.Mobile.AddToBackpack(item);
                    else
                        e.Mobile.SendMessage($"No piece \"{e.GetString(1)}\".");
                    return;
                default:
                    e.Mobile.SendMessage($"Artisan Festival ({t.Source}): {t.Pieces.Count} pieces, {t.Stock.Count} in the spring stock, " +
                                         $"the rest in the spring box slot at weight {t.BoxWeight} each.");
                    return;
            }
        }
    }
}
