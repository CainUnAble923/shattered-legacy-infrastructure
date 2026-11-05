// cc-P795: the Drover's Handbook: stock book art, a Spellbook of its own type (Spellbook-drover-type.patch) with four pages from
// the free registry IDs 693 to 696 (pinned's last is 692, and the registry ends at 699). The client knows no such book, so a
// double-click opens DroversHandbookGump instead of the client's spellbook. A client "cast spell" macro by ID works too, as
// pinned's handler finds the book through GetTypeForSpell. Animal Trainers sell it, so an existing world has it at once: vendors
// rebuild their stock on load.
//
// The book comes with Corral. The other three pages come as scrolls, from the same vendor: drop one on the book (pinned's
// Spellbook.OnDragDrop). A scroll can also be read once, as any spell scroll can.
//
// Numbers (modernuo.json, written with these defaults on first start): clusterf.drover.corralDivisor, .corralSeconds,
// .corralAttemptGraceSeconds, .corralStamina, .calmBase, .calmDivisor, .calmStamina, .calmCooldownSeconds, .herdRadiusDivisor,
// .herdCountDivisor, .herdStamina, .lureRange (15 at most), .lureStamina, .castDelaySeconds, .range, .gainTop, .bookPrice,
// .calmScrollPrice, .herdScrollPrice, .lureScrollPrice. Calm, Herd and Lure: Chase's choice of 2026-11-05 ("Stronger").

using System;
using ModernUO.Serialization;
using Server.Commands;
using Server.Gumps;
using Server.Network;
using Server.Spells;
using Server.Spells.Drover;

namespace Server.Spells.Drover
{
    public static class DroverSettings
    {
        public const int CorralId = 693;
        public const int CalmId = 694;
        public const int HerdId = 695;
        public const int LureId = 696;
        public const int ScrollArt = 0x0E34;
        public const int ScrollHue = 0x8A5;

        public static double CorralDivisor { get; private set; } = 8.0;
        public static int CorralSeconds { get; private set; } = 60;
        public static int CorralAttemptGraceSeconds { get; private set; } = 30;
        public static int CorralStamina { get; private set; } = 5;
        public static double CalmBase { get; private set; } = 3.0;
        public static double CalmDivisor { get; private set; } = 20.0;
        public static int CalmStamina { get; private set; } = 10;
        public static int CalmCooldownSeconds { get; private set; } = 30;
        public static double HerdRadiusDivisor { get; private set; } = 25.0;
        public static double HerdCountDivisor { get; private set; } = 20.0;
        public static int HerdStamina { get; private set; } = 15;
        public static int LureRange { get; private set; } = 15;
        public static int LureStamina { get; private set; } = 10;
        public static double CastDelaySeconds { get; private set; } = 1.0;
        public static int Range { get; private set; } = 10;
        public static double GainTop { get; private set; } = 92.0;
        public static int BookPrice { get; private set; } = 2500;
        public static int CalmScrollPrice { get; private set; } = 500;
        public static int HerdScrollPrice { get; private set; } = 1000;
        public static int LureScrollPrice { get; private set; } = 1500;

        public static void Configure()
        {
            CorralDivisor = Math.Max(1.0, ServerConfiguration.GetOrUpdateSetting("clusterf.drover.corralDivisor", CorralDivisor));
            CorralSeconds = ServerConfiguration.GetOrUpdateSetting("clusterf.drover.corralSeconds", CorralSeconds);
            CorralAttemptGraceSeconds =
                ServerConfiguration.GetOrUpdateSetting("clusterf.drover.corralAttemptGraceSeconds", CorralAttemptGraceSeconds);
            CorralStamina = ServerConfiguration.GetOrUpdateSetting("clusterf.drover.corralStamina", CorralStamina);
            CalmBase = ServerConfiguration.GetOrUpdateSetting("clusterf.drover.calmBase", CalmBase);
            CalmDivisor = Math.Max(1.0, ServerConfiguration.GetOrUpdateSetting("clusterf.drover.calmDivisor", CalmDivisor));
            CalmStamina = ServerConfiguration.GetOrUpdateSetting("clusterf.drover.calmStamina", CalmStamina);
            CalmCooldownSeconds = ServerConfiguration.GetOrUpdateSetting("clusterf.drover.calmCooldownSeconds", CalmCooldownSeconds);
            HerdRadiusDivisor = Math.Max(1.0, ServerConfiguration.GetOrUpdateSetting("clusterf.drover.herdRadiusDivisor", HerdRadiusDivisor));
            HerdCountDivisor = Math.Max(1.0, ServerConfiguration.GetOrUpdateSetting("clusterf.drover.herdCountDivisor", HerdCountDivisor));
            HerdStamina = ServerConfiguration.GetOrUpdateSetting("clusterf.drover.herdStamina", HerdStamina);
            LureRange = Math.Clamp(ServerConfiguration.GetOrUpdateSetting("clusterf.drover.lureRange", LureRange), 1, 15);
            LureStamina = ServerConfiguration.GetOrUpdateSetting("clusterf.drover.lureStamina", LureStamina);
            CastDelaySeconds = ServerConfiguration.GetOrUpdateSetting("clusterf.drover.castDelaySeconds", CastDelaySeconds);
            Range = ServerConfiguration.GetOrUpdateSetting("clusterf.drover.range", Range);
            GainTop = ServerConfiguration.GetOrUpdateSetting("clusterf.drover.gainTop", GainTop);
            BookPrice = ServerConfiguration.GetOrUpdateSetting("clusterf.drover.bookPrice", BookPrice);
            CalmScrollPrice = ServerConfiguration.GetOrUpdateSetting("clusterf.drover.calmScrollPrice", CalmScrollPrice);
            HerdScrollPrice = ServerConfiguration.GetOrUpdateSetting("clusterf.drover.herdScrollPrice", HerdScrollPrice);
            LureScrollPrice = ServerConfiguration.GetOrUpdateSetting("clusterf.drover.lureScrollPrice", LureScrollPrice);

            SpellRegistry.Register(CorralId, typeof(CorralSpell));
            SpellRegistry.Register(CalmId, typeof(CalmSpell));
            SpellRegistry.Register(HerdId, typeof(HerdSpell));
            SpellRegistry.Register(LureId, typeof(LureSpell));
        }

        public static void Initialize()
        {
            CommandSystem.Register("Corral", AccessLevel.Player, OnCorral);
            CommandSystem.Register("Calm", AccessLevel.Player, OnCalm);
            CommandSystem.Register("Herd", AccessLevel.Player, OnHerd);
            CommandSystem.Register("Lure", AccessLevel.Player, OnLure);
        }

        [Usage("Corral")]
        [Description("Casts Corral from your Drover's Handbook. For a macro.")]
        [ShardCommand(CommandCategory.Player)]
        private static void OnCorral(CommandEventArgs e) => Items.DroversHandbook.Cast(e.Mobile, CorralId);

        [Usage("Calm")]
        [Description("Casts Calm from your Drover's Handbook. For a macro.")]
        [ShardCommand(CommandCategory.Player)]
        private static void OnCalm(CommandEventArgs e) => Items.DroversHandbook.Cast(e.Mobile, CalmId);

        [Usage("Herd")]
        [Description("Casts Herd from your Drover's Handbook. For a macro.")]
        [ShardCommand(CommandCategory.Player)]
        private static void OnHerd(CommandEventArgs e) => Items.DroversHandbook.Cast(e.Mobile, HerdId);

        [Usage("Lure")]
        [Description("Casts Lure from your Drover's Handbook. For a macro.")]
        [ShardCommand(CommandCategory.Player)]
        private static void OnLure(CommandEventArgs e) => Items.DroversHandbook.Cast(e.Mobile, LureId);
    }
}

namespace Server.Items
{
    [SerializationGenerator(0, false)]
    public partial class DroversHandbook : Spellbook
    {
        public const int FirstSpell = 693;
        public const ulong CorralPage = 1;
        public const ulong AllPages = 15;

        [Constructible]
        public DroversHandbook(ulong content = CorralPage) : base(content, 0x0FEF) => Layer = Layer.Invalid;

        public override string DefaultName => "the Drover's Handbook";
        public override SpellbookType SpellbookType => SpellbookType.Drover;
        public override int BookOffset => FirstSpell;
        public override int BookCount => 4;

        public override void OnDoubleClick(Mobile from)
        {
            if (!IsChildOf(from.Backpack))
            {
                from.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
                return;
            }

            from.SendGump(new DroversHandbookGump(this));
        }

        /// <summary>Casts a page: from the gump or a command, so always through the holder's own book.</summary>
        public static bool Cast(Mobile from, int spellId)
        {
            var book = Find(from, spellId);

            if (book is not DroversHandbook)
            {
                from.SendMessage("You need that page in a Drover's Handbook in your pack.");
                return false;
            }

            return SpellRegistry.NewSpell(spellId, from, null)?.Cast() == true;
        }
    }

    public class DroversHandbookGump : Gump
    {
        private static readonly (int Id, string Name, string Text)[] Pages =
        [
            (DroverSettings.CorralId, "Corral", $"A wild animal is easier for your next taming attempt: Herding / {DroverSettings.CorralDivisor:0.#}, for {DroverSettings.CorralSeconds} seconds."),
            (DroverSettings.CalmId, "Calm", $"A wild animal stops fighting for {DroverSettings.CalmBase:0.#} seconds + Herding / {DroverSettings.CalmDivisor:0.#}. Not the same one again for {DroverSettings.CalmCooldownSeconds} seconds."),
            (DroverSettings.HerdId, "Herd", $"The wild animals around the one you choose walk to a spot you pick: tiles Herding / {DroverSettings.HerdRadiusDivisor:0.#}, animals Herding / {DroverSettings.HerdCountDivisor:0.#}."),
            (DroverSettings.LureId, "Lure", $"One wild animal up to {DroverSettings.LureRange} tiles away walks to you, seen or not.")
        ];

        private readonly DroversHandbook _book;

        public override bool Singleton => true;

        public DroversHandbookGump(DroversHandbook book) : base(120, 80)
        {
            _book = book;

            const int width = 420;
            var height = 70 + 72 * Pages.Length;
            AddPage(0);
            AddBackground(0, 0, width, height, 9270);
            AddAlphaRegion(10, 10, width - 20, height - 20);
            AddHtml(20, 18, width - 40, 22, "<CENTER><BASEFONT COLOR=#E8D8A0>The Drover's Handbook</BASEFONT></CENTER>");

            for (var i = 0; i < Pages.Length; i++)
            {
                var (id, name, text) = Pages[i];
                var y = 50 + i * 72;
                var known = book.HasSpell(id);

                if (known)
                {
                    AddButton(22, y + 2, 4005, 4007, id);
                }

                AddHtml(58, y, width - 80, 22, $"<BASEFONT COLOR=#{(known ? "FFFFFF" : "808080")}>{name}</BASEFONT>");
                AddHtml(58, y + 20, width - 80, 50, $"<BASEFONT COLOR=#{(known ? "C8C8C8" : "808080")}>{text}</BASEFONT>");
            }
        }

        public override void OnResponse(NetState sender, in RelayInfo info)
        {
            var from = sender.Mobile;

            if (info.ButtonID == 0 || from == null || _book.Deleted || !_book.IsChildOf(from.Backpack))
            {
                return;
            }

            DroversHandbook.Cast(from, info.ButtonID);
        }
    }
}
