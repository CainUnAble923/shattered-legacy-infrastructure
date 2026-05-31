using ModernUO.Serialization;
using Server.Commands;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;
using Server.Targeting;

namespace Server.Items;

/// <summary>
/// Dev Testing Crystal — GM-only testing aid.
///
/// When ACTIVE (green hue), all material, voucher, gold, ingot, skill, and
/// standing requirements are bypassed for:
///   - Guild work orders (Miners' Compact, Society of Smiths, and all future guilds)
///   - Regular crafting — skill and material requirements skipped entirely
///   - T1/T2 pickaxe restoration and upgrade flows
///
/// Registry unlock checks and guild membership checks are NOT bypassed — the
/// player still needs the actual unlock and guild join so the state machine is exercised.
///
/// When INACTIVE (red hue), the item is inert — no bypasses apply.
///
/// Double-click to open the toggle menu.
/// Blessed so it cannot be dropped or looted.
///
/// Admin command:
///   [GiveDevCrystal   — GM+, targets a player to deliver the crystal (starts active)
/// </summary>
[SerializationGenerator(0, false)]
public partial class DevTestingCrystal : Item
{
    private const int HueActive   = 0x0044; // Bright green
    private const int HueInactive = 0x0021; // Red

    [SerializableField(0)]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    private bool _active;

    [Constructible]
    public DevTestingCrystal() : base(0x1ECD)
    {
        Name     = "Dev Testing Crystal";
        Hue      = HueActive;
        LootType = LootType.Blessed;
        Weight   = 0.1;
        _active  = true; // Starts active so it works immediately when given
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add(_active
            ? "<BASEFONT COLOR=#44FF44>ACTIVE — all costs bypassed</BASEFONT>"
            : "<BASEFONT COLOR=#FF4444>INACTIVE — double-click to activate</BASEFONT>");
        list.Add("<BASEFONT COLOR=#888888>GM use only</BASEFONT>");
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (from is not PlayerMobile pm)
            return;

        if (!IsChildOf(pm.Backpack))
        {
            pm.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
            return;
        }

        pm.SendGump(new DevCrystalGump(this));
    }

    // ── Bypass check ──────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true if the player is carrying an active Dev Testing Crystal.
    /// Called by work order handlers, the craft system hook, and any guild
    /// interaction that should be bypassable during development.
    /// </summary>
    public static bool IsActive(PlayerMobile pm) =>
        pm.Backpack?.FindItemByType(typeof(DevTestingCrystal)) is DevTestingCrystal c && c._active;

    // ── Admin command ─────────────────────────────────────────────────────────

    public static void Configure()
    {
        CommandSystem.Register("GiveDevCrystal", AccessLevel.GameMaster, GiveCrystal_OnCommand);
    }

    [Usage("GiveDevCrystal")]
    [Description("Gives an active Dev Testing Crystal to the targeted player. Toggle on/off via double-click.")]
    private static void GiveCrystal_OnCommand(CommandEventArgs e)
    {
        e.Mobile.SendMessage("Target the player to give a Dev Testing Crystal to.");
        e.Mobile.Target = new GiveCrystalTarget();
    }

    private sealed class GiveCrystalTarget : Target
    {
        public GiveCrystalTarget() : base(12, false, TargetFlags.None) { }

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (targeted is not PlayerMobile pm)
            {
                from.SendMessage("That is not a player.");
                return;
            }

            if (pm.Backpack == null)
            {
                from.SendMessage($"{pm.Name} has no backpack.");
                return;
            }

            var crystal = new DevTestingCrystal();
            pm.Backpack.DropItem(crystal);
            from.SendMessage($"Dev Testing Crystal given to {pm.Name} (active).");
            pm.SendMessage(0x44, "A Dev Testing Crystal has been placed in your pack. It is active — all material costs are bypassed. Double-click to toggle on/off.");
        }
    }

    // ── Toggle Gump ───────────────────────────────────────────────────────────

    private sealed class DevCrystalGump : Gump
    {
        private const int BtnToggle = 1;

        private readonly DevTestingCrystal _crystal;

        public DevCrystalGump(DevTestingCrystal crystal) : base(200, 200)
        {
            _crystal = crystal;

            Closable   = true;
            Disposable = true;
            Resizable  = false;

            AddPage(0);
            AddBackground(0, 0, 270, 165, 9270);
            AddAlphaRegion(8, 8, 254, 149);

            // Title
            AddLabel(75, 16, 1153, "Dev Testing Crystal");
            AddImageTiled(10, 36, 250, 2, 9304);

            if (crystal._active)
            {
                // Active state
                AddLabel(105, 52, 68,  "● ACTIVE");
                AddLabel(38,  74, 999, "Work orders, crafting, and guild costs bypassed.");

                AddButton(68,  108, 4017, 4019, BtnToggle, GumpButtonType.Reply, 0);
                AddLabel(103, 110, 33,  "Deactivate");
            }
            else
            {
                // Inactive state
                AddLabel(100, 52, 33,  "● INACTIVE");
                AddLabel(72,  74, 999, "Normal costs apply.");

                AddButton(68,  108, 4005, 4007, BtnToggle, GumpButtonType.Reply, 0);
                AddLabel(103, 110, 68,  "Activate");
            }
        }

        public override void OnResponse(NetState sender, in RelayInfo info)
        {
            if (info.ButtonID != BtnToggle || sender.Mobile is not PlayerMobile pm)
                return;

            if (_crystal.Deleted || !_crystal.IsChildOf(pm.Backpack))
            {
                pm.SendMessage("The crystal is no longer in your backpack.");
                return;
            }

            _crystal._active = !_crystal._active;
            _crystal.Hue     = _crystal._active ? HueActive : HueInactive;
            _crystal.InvalidateProperties();

            if (_crystal._active)
                pm.SendMessage(0x44, "Dev Testing Crystal activated. All material costs are bypassed.");
            else
                pm.SendMessage(0x26, "Dev Testing Crystal deactivated. Normal costs apply.");

            pm.SendGump(new DevCrystalGump(_crystal));
        }
    }
}
