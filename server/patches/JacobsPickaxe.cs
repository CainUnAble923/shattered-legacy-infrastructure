using ModernUO.Serialization;
using Server.Accounting;
using Server.Mobiles;

namespace Server.Items;

/// <summary>
/// Jacob's Pickaxe — legacy mining tool.
///
/// Phase 2F changes (from Phase 1 original):
///   - SkillBonuses: +5 Mining (was +10 — corrected to intended tier-1 value)
///   - UsesRemaining: 150 (was 20 — full durable tool)
///   - OnAdded: auto-unlocks the restoration registry entry "legacy.jacobs_pickaxe"
///     on first acquisition so the player can always restore it
///
/// Phase 2H (Durability MVP):
///   - Non-disposable: when the pickaxe exhausts (UsesRemaining hits 0), instead of
///     deleting the item, OnDelete creates an exhausted replacement in the player's pack.
///   - Exhausted state: tracked via Hue (0x0415 = exhausted, 0 = functional). Hue is
///     serialized by the base Item class — no migration file required.
///   - Player cannot mine with an exhausted pickaxe. OnDoubleClick shows a restoration
///     message pointing to the Miners' Compact Liaison.
///   - Registry: ClearActiveCopy is called on exhaustion so the player can request
///     a fresh restoration from the Miners' Compact.
///
/// Serialization: unchanged at version 0. Exhausted state is stored in the item Hue
/// property (a standard Item serializable field) — no custom serialization needed.
/// </summary>
[SerializationGenerator(0, false)]
public partial class JacobsPickaxe : Pickaxe
{
    private const int ExhaustedHue  = 0x0415; // Dull charcoal grey

    [Constructible]
    public JacobsPickaxe()
    {
        UsesRemaining = 150;
        LootType = LootType.Blessed;
        SkillBonuses.SetValues(0, SkillName.Mining, 5.0);
    }

    /// <summary>
    /// Whether this pickaxe is in the exhausted state. Backed by Hue — no extra field needed.
    /// </summary>
    public bool Exhausted
    {
        get => Hue == ExhaustedHue;
        set
        {
            Hue = value ? ExhaustedHue : 0;
            // Suppress the "uses remaining" tooltip while exhausted.
            // IUsesRemaining.ShowUsesRemaining is not virtual so we set it via interface cast.
            ((IUsesRemaining)this).ShowUsesRemaining = !value;
            InvalidateProperties();
        }
    }

    public override int LabelNumber => 1077758; // Jacob's Pickaxe

    /// <summary>
    /// Prevent equipping an exhausted pickaxe. This keeps the restoration active-copy
    /// safety scan to backpack-only and is the standard pattern for all tier tools.
    /// </summary>
    public override bool OnEquip(Mobile from)
    {
        if (Exhausted)
        {
            from.SendMessage(0x22,
                "Jacob's Pickaxe is exhausted and cannot be equipped. " +
                "Speak with the Miners' Compact Liaison in New Haven to restore it.");
            return false;
        }

        return base.OnEquip(from);
    }

    // ── Registry unlock on acquisition ────────────────────────────────────────

    /// <summary>
    /// Auto-unlock the restoration registry entry whenever Jacob's Pickaxe enters
    /// a player's possession. Idempotent — calling Unlock multiple times is safe.
    /// </summary>
    public override void OnAdded(IEntity parent)
    {
        base.OnAdded(parent);

        PlayerMobile? pm = null;

        if (parent is PlayerMobile directPm)
            pm = directPm;
        else if (parent is Container c && c.RootParent is PlayerMobile containerPm)
            pm = containerPm;

        if (pm?.Account is IAccount acct)
        {
            ClusterFRestorationRegistry.Unlock(acct, "legacy.jacobs_pickaxe", "item_acquisition");

            // Mark an active copy whenever a non-exhausted pickaxe enters the player's possession.
            // Exhausted replacement drops are skipped — ClearActiveCopy was already called in OnDelete
            // so the player can request restoration.
            if (!Exhausted)
            {
                var entry = ClusterFRestorationRegistry.GetEntry(acct, "legacy.jacobs_pickaxe");
                if (entry != null) entry.HasActiveCopy = true;
            }
        }
    }

    // ── Exhausted state ───────────────────────────────────────────────────────

    /// <summary>
    /// Prevents mining with an exhausted pickaxe. Sends a restoration message instead.
    /// </summary>
    public override void OnDoubleClick(Mobile from)
    {
        if (Exhausted)
        {
            from.SendMessage(0x22,
                "Jacob's Pickaxe is exhausted. Return to the Miners' Compact Liaison " +
                "in New Haven to have it restored.");
            return;
        }

        base.OnDoubleClick(from);
    }

    /// <summary>
    /// Shows "(Exhausted)" in item properties when the pickaxe is worn out.
    /// </summary>
    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);

        if (Exhausted)
            list.Add("(Exhausted — restore at Miners' Compact)");
    }

    // ── Durability MVP — non-disposable behaviour ─────────────────────────────

    /// <summary>
    /// When the pickaxe is deleted (typically from UsesRemaining hitting 0 during mining),
    /// create an exhausted replacement in the player's pack so the item is never lost.
    /// Also clears the active-copy registration so the player can request a restoration.
    /// </summary>
    public override void OnDelete()
    {
        // Only intercept if currently functional (not already exhausted — avoids loops)
        if (!Exhausted)
        {
            // Resolve the owning player. The pickaxe may be in the backpack (Parent is
            // Container) or equipped on a layer (Parent is PlayerMobile). Handle both.
            PlayerMobile? owner = null;
            Container?    pack  = null;

            if (Parent is Container directPack)
            {
                pack  = directPack;
                owner = directPack.RootParent as PlayerMobile;
            }
            else if (Parent is PlayerMobile layerOwner)
            {
                owner = layerOwner;
                pack  = layerOwner.Backpack;
            }

            if (owner != null && pack != null)
            {
                // Clear registry so the player can request a fresh restoration
                if (owner.Account is IAccount acct)
                    ClusterFRestorationRegistry.ClearActiveCopy(acct, "legacy.jacobs_pickaxe");

                // Drop an exhausted replacement so the item is never permanently lost
                var replacement = new JacobsPickaxe();
                replacement.Exhausted = true;
                pack.DropItem(replacement);

                owner.SendMessage(0x22,
                    "Jacob's Pickaxe has worn out. " +
                    "Find the Miners' Compact Liaison in New Haven to restore it.");
            }
        }

        base.OnDelete();
    }
}
