using System;
using System.Collections.Generic;
using ModernUO.Serialization;
using Server.Accounting;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;
using Server.Targeting;

namespace Server.Items;

// ─────────────────────────────────────────────────────────────────────────────
// OutridersCrook / JourneymansCrook / WardensCrook
//
// Three-tier Shepherd's Crook for the Rangers' League (Outriders).
//
// T1 — Outrider's Crook (issued on guild join):
//   • +5 Animal Taming, +5 Peacemaking (passive while in backpack)
//   • Instant Bond: once per 48 h
//   • Deliver Pet: use on a following pet to send it for a taming work order
//   • Shrink Pet:  use on a bonded, following pet → ceramic figurine in backpack
//
// T2 — Journeyman's Crook:
//   • +10 Animal Taming, +10 Peacemaking
//   • Instant Bond: once per 36 h
//   • All T1 abilities
//   • Peaceful Approach: activates a 60-second buff that bypasses the
//     "must subdue before taming" HP check for one taming attempt
//
// T3 — Warden's Crook:
//   • +15 Animal Taming, +10 Animal Lore, +15 Peacemaking
//   • Instant Bond: once per 24 h
//   • All T2 abilities
//   • Peaceful Taming (passive): subdue requirement permanently bypassed
//     while this crook is anywhere in the player's possession
// ─────────────────────────────────────────────────────────────────────────────

// ── T1 ────────────────────────────────────────────────────────────────────────

[SerializationGenerator(0, false)]
public partial class OutridersCrook : Item
{
    // ── Tier-overridable stats ────────────────────────────────────────────────

    protected virtual double TamingBonus      => 5.0;
    protected virtual double PeacemakingBonus => 5.0;
    protected virtual double AnimalLoreBonus  => 0.0;
    protected virtual TimeSpan BondCooldown   => TimeSpan.FromHours(48);
    protected virtual bool HasPeacefulApproach => false;
    protected virtual bool HasPeacefulTaming   => false;

    // ── Bond cooldown tracking ────────────────────────────────────────────────

    private DateTime _lastBondUse;      // not serialized — resets on restart (24-48h loss acceptable)

    public bool CanInstantBond => Core.Now >= _lastBondUse + BondCooldown;

    public TimeSpan BondTimeRemaining =>
        CanInstantBond ? TimeSpan.Zero
                       : (_lastBondUse + BondCooldown) - Core.Now;

    // ── Skill mod management ──────────────────────────────────────────────────

    // Non-serialised: always re-applied by ClusterFRangersSystem on WorldLoad
    private bool _modsApplied;

    private string ModKey(string suffix) => $"OutridersCrook_{Serial}_{suffix}";

    public void TryApplyMods()
    {
        if (_modsApplied) return;
        if (RootParent is not PlayerMobile pm) return;

        ApplyMods(pm);
    }

    protected virtual void ApplyMods(PlayerMobile pm)
    {
        // Remove first to avoid stacking on double-apply
        RemoveModsFrom(pm);

        pm.AddSkillMod(new DefaultSkillMod(SkillName.AnimalTaming, ModKey("T"), true, TamingBonus));
        pm.AddSkillMod(new DefaultSkillMod(SkillName.Peacemaking,  ModKey("P"), true, PeacemakingBonus));
        if (AnimalLoreBonus > 0.0)
            pm.AddSkillMod(new DefaultSkillMod(SkillName.AnimalLore, ModKey("L"), true, AnimalLoreBonus));

        _modsApplied = true;
    }

    protected void RemoveModsFrom(PlayerMobile pm)
    {
        pm.RemoveSkillMod(ModKey("T"));
        pm.RemoveSkillMod(ModKey("P"));
        pm.RemoveSkillMod(ModKey("L"));
        _modsApplied = false;
    }

    // ── Container lifecycle ───────────────────────────────────────────────────

    public override void OnAdded(IEntity parent)
    {
        base.OnAdded(parent);
        TryApplyMods();
    }

    public override void OnRemoved(IEntity parent)
    {
        base.OnRemoved(parent);

        // parent is the OLD container — find the player who owned it
        if (parent is Container c && c.RootParent is PlayerMobile pm)
            RemoveModsFrom(pm);
        else if (parent is PlayerMobile pm2)
            RemoveModsFrom(pm2);
    }

    // ── Construction / serialization ─────────────────────────────────────────

    [Constructible]
    public OutridersCrook() : base(0xE81)
    {
        Weight    = 4.0;
        Movable   = true;
        LootType  = LootType.Blessed;
        Name      = "an Outrider's Crook";
        Hue       = 0x47E;   // deep forest green
    }

    private void Deserialize(IGenericReader reader, int version) { }

    // ── Double-click: open action gump ────────────────────────────────────────

    public override void OnDoubleClick(Mobile from)
    {
        if (from is not PlayerMobile pm) return;
        if (!IsChildOf(pm.Backpack))
        {
            pm.SendMessage("The crook must be in your backpack to use its abilities.");
            return;
        }
        pm.SendGump(new CrookActionsGump(this, pm));
    }

    // ── Deliver pet for work order ────────────────────────────────────────────

    public void BeginDeliver(PlayerMobile pm)
    {
        pm.SendMessage("Target the pet you wish to deliver for a contract.");
        pm.Target = new DeliverTarget(this, pm);
    }

    private class DeliverTarget : Target
    {
        private readonly OutridersCrook _crook;
        private readonly PlayerMobile   _pm;

        public DeliverTarget(OutridersCrook crook, PlayerMobile pm)
            : base(2, false, TargetFlags.None) { _crook = crook; _pm = pm; }

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (targeted is not BaseCreature pet)
            {
                _pm.SendMessage("That is not a creature.");
                return;
            }

            if (pet.ControlMaster != _pm || pet.ControlOrder != OrderType.Follow)
            {
                _pm.SendMessage("That creature must be following you to deliver it.");
                return;
            }

            var acct = _pm.Account as IAccount;
            if (acct == null) return;
            var data = ClusterFAccountPersistence.GetOrCreate(acct);

            var typeName = pet.GetType().Name;
            WorkOrderEntry? matchEntry = null;
            WorkOrderDef?   matchDef   = null;
            string?         matchKey   = null;

            foreach (var entry in data.ActiveWorkOrders)
            {
                var def = ClusterFWorkOrderSystem.Get(entry.DefKey);
                if (def == null || def.Type != WorkOrderType.TamingContract) continue;

                foreach (var req in def.Requirements)
                {
                    if (req is not WorkOrderTamingRequirement tr) continue;
                    if (tr.ItemType.Name != typeName)              continue;

                    entry.TamingProgress.TryGetValue(typeName, out var curr);
                    if (curr >= tr.Amount) continue;   // already satisfied

                    matchEntry = entry;
                    matchDef   = def;
                    matchKey   = typeName;
                    break;
                }

                if (matchEntry != null) break;
            }

            if (matchEntry == null || matchDef == null || matchKey == null)
            {
                _pm.SendMessage(0x22, $"You have no active taming contract for {typeName}s.");
                return;
            }

            // Deliver: credit progress, then delete the pet (given to the guild)
            matchEntry.TamingProgress.TryGetValue(matchKey, out var before);
            matchEntry.TamingProgress[matchKey] = before + 1;

            var req2 = (WorkOrderTamingRequirement)matchDef.Requirements
                .Find(r => r is WorkOrderTamingRequirement tr && tr.ItemType.Name == matchKey)!;

            pet.Delete();   // delivered to the guild

            var newCount = before + 1;
            _pm.SendMessage(0x44,
                $"You deliver {pet.Name} to the Rangers' League. Progress: {newCount}/{req2.Amount} {matchKey}s.");

            // Check if all requirements are now satisfied
            bool allDone = matchDef.Requirements.TrueForAll(r =>
            {
                if (r is not WorkOrderTamingRequirement tr2) return true;
                matchEntry.TamingProgress.TryGetValue(tr2.ItemType.Name, out var c);
                return c >= tr2.Amount;
            });

            if (allDone)
                _pm.SendMessage(0x44, "All animals delivered! Open your Field Contracts to collect your reward.");
        }
    }

    // ── Shrink pet ────────────────────────────────────────────────────────────

    public void BeginShrink(PlayerMobile pm)
    {
        pm.SendMessage("Target the bonded pet you wish to store in a figurine.");
        pm.Target = new ShrinkTarget(this, pm);
    }

    private class ShrinkTarget : Target
    {
        private readonly OutridersCrook _crook;
        private readonly PlayerMobile   _pm;

        public ShrinkTarget(OutridersCrook crook, PlayerMobile pm)
            : base(2, false, TargetFlags.None) { _crook = crook; _pm = pm; }

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (targeted is not BaseCreature pet)
            {
                _pm.SendMessage("That is not a creature.");
                return;
            }

            var fig = ShrunkPet.TryShrink(_pm, pet);

            if (fig == null)
            {
                _pm.SendMessage(0x22, "That creature cannot be shrunk. It must be your bonded, following pet.");
                return;
            }

            _pm.Backpack!.DropItem(fig);
            _pm.SendMessage(0x44, $"{pet.Name} has been stored as a ceramic figurine.");
            Effects.PlaySound(_pm.Location, _pm.Map, 0x1FE);
        }
    }

    // ── Instant Bond ─────────────────────────────────────────────────────────

    public void BeginInstantBond(PlayerMobile pm)
    {
        if (!CanInstantBond)
        {
            pm.SendMessage(0x22, "Your crook's bonding power is still recovering.");
            return;
        }

        pm.SendMessage("Target the pet you wish to instantly bond.");
        pm.Target = new BondTarget(this, pm);
    }

    private class BondTarget : Target
    {
        private readonly OutridersCrook _crook;
        private readonly PlayerMobile   _pm;

        public BondTarget(OutridersCrook crook, PlayerMobile pm)
            : base(2, false, TargetFlags.None) { _crook = crook; _pm = pm; }

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (targeted is not BaseCreature pet)
            {
                _pm.SendMessage("That is not a creature.");
                return;
            }

            if (pet.ControlMaster != _pm)
            {
                _pm.SendMessage("That is not your pet.");
                return;
            }

            if (pet.IsBonded)
            {
                _pm.SendMessage("That creature is already bonded to you.");
                return;
            }

            if (!pet.Tamable)
            {
                _pm.SendMessage("This creature cannot be bonded.");
                return;
            }

            pet.IsBonded        = true;
            pet.BondingBegin    = DateTime.MinValue;
            _crook._lastBondUse = Core.Now;

            _pm.SendMessage(0x44, $"Your crook pulses with warm light — {pet.Name} bonds to you instantly.");
            Effects.PlaySound(_pm.Location, _pm.Map, 0x1F5);
        }
    }

    // ── Peaceful Approach ─────────────────────────────────────────────────────

    public void BeginPeacefulApproach(PlayerMobile pm)
    {
        pm.SendMessage("Target the creature you wish to approach peacefully.");
        pm.Target = new PeacefulApproachTarget(this, pm);
    }

    private class PeacefulApproachTarget : Target
    {
        private readonly OutridersCrook _crook;
        private readonly PlayerMobile   _pm;

        public PeacefulApproachTarget(OutridersCrook crook, PlayerMobile pm)
            : base(4, false, TargetFlags.None) { _crook = crook; _pm = pm; }

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (targeted is not BaseCreature bc || !bc.Tamable)
            {
                _pm.SendMessage("That creature cannot be peacefully approached for taming.");
                return;
            }

            ClusterFRangersSystem.GrantPeacefulApproach(_pm, TimeSpan.FromSeconds(60));

            _pm.SendMessage(0x44,
                "You extend your crook toward the beast and speak in a low, steady tone. " +
                "For the next 60 seconds you may attempt to tame without subduing it first.");
            Effects.PlaySound(_pm.Location, _pm.Map, 0x58B);
        }
    }

    // ── Action gump ───────────────────────────────────────────────────────────

    private class CrookActionsGump : Gump
    {
        private readonly OutridersCrook _crook;
        private readonly PlayerMobile   _pm;

        private const int W = 280;

        public CrookActionsGump(OutridersCrook crook, PlayerMobile pm) : base(180, 160)
        {
            _crook = crook;
            _pm    = pm;

            Closable   = true;
            Disposable = true;

            // ── Background ────────────────────────────────────────────────────
            AddBackground(0, 0, W, 220, 9270);
            AddAlphaRegion(6, 6, W - 12, 208);

            AddLabel(W / 2 - 60, 10, 1154, crook.Name.TrimStart('a', 'n', ' '));
            AddImageTiled(10, 28, W - 20, 1, 9304);

            var y = 36;

            // ── Deliver Pet ───────────────────────────────────────────────────
            AddButton(14, y, 4011, 4012, 1);
            AddLabel(40, y + 2, 999, "Deliver Pet for Contract");
            AddLabel(14, y + 18, 0x966, "  Tame any creature, have it follow you, then target it.");
            y += 44;
            AddImageTiled(10, y, W - 20, 1, 9304);
            y += 6;

            // ── Shrink Pet ────────────────────────────────────────────────────
            AddButton(14, y, 4011, 4012, 2);
            AddLabel(40, y + 2, 999, "Shrink Pet into Figurine");
            AddLabel(14, y + 18, 0x966, "  Must be bonded and following. Stores in backpack.");
            y += 44;
            AddImageTiled(10, y, W - 20, 1, 9304);
            y += 6;

            // ── Instant Bond ──────────────────────────────────────────────────
            if (crook.CanInstantBond)
            {
                AddButton(14, y, 4011, 4012, 3);
                AddLabel(40, y + 2, 999, "Instant Bond");
                AddLabel(14, y + 18, 0x966, "  Instantly bonds a tamed pet to you.");
            }
            else
            {
                var rem = crook.BondTimeRemaining;
                AddLabel(14, y + 2,  0x96D, $"Instant Bond — Ready in {(int)rem.TotalHours}h {rem.Minutes:D2}m");
                AddLabel(14, y + 18, 0x966, "  Bond cooldown not yet expired.");
            }
            y += 44;

            // ── Peaceful Approach (T2+) ───────────────────────────────────────
            if (crook.HasPeacefulApproach)
            {
                AddImageTiled(10, y, W - 20, 1, 9304);
                y += 6;
                AddButton(14, y, 4011, 4012, 4);
                AddLabel(40, y + 2, 999, "Peaceful Approach");
                AddLabel(14, y + 18, 0x966, "  60s buff: tame without subduing first.");
                y += 44;
            }
            else if (crook.HasPeacefulTaming)
            {
                AddImageTiled(10, y, W - 20, 1, 9304);
                y += 6;
                AddLabel(14, y + 2, 0x44, "Peaceful Taming (passive — always active)");
                y += 24;
            }
        }

        public override void OnResponse(NetState sender, in RelayInfo info)
        {
            if (!_crook.IsChildOf(_pm.Backpack)) return;

            switch (info.ButtonID)
            {
                case 1: _crook.BeginDeliver(_pm);         break;
                case 2: _crook.BeginShrink(_pm);          break;
                case 3: _crook.BeginInstantBond(_pm);     break;
                case 4: _crook.BeginPeacefulApproach(_pm); break;
            }
        }
    }

    // ── Properties display ────────────────────────────────────────────────────

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        if (TamingBonus      > 0) list.Add($"Animal Taming +{TamingBonus}");
        if (PeacemakingBonus > 0) list.Add($"Peacemaking +{PeacemakingBonus}");
        if (AnimalLoreBonus  > 0) list.Add($"Animal Lore +{AnimalLoreBonus}");

        if (CanInstantBond)
            list.Add("Instant Bond: Ready");
        else
        {
            var rem = BondTimeRemaining;
            list.Add($"Instant Bond: {(int)rem.TotalHours}h {rem.Minutes}m");
        }

        if (HasPeacefulTaming)
            list.Add("Peaceful Taming (passive)");
        else if (HasPeacefulApproach)
            list.Add("Peaceful Approach");
    }
}

// ── T2 ────────────────────────────────────────────────────────────────────────

[SerializationGenerator(0, false)]
public partial class JourneymansCrook : OutridersCrook
{
    protected override double TamingBonus       => 10.0;
    protected override double PeacemakingBonus  => 10.0;
    protected override TimeSpan BondCooldown    => TimeSpan.FromHours(36);
    protected override bool HasPeacefulApproach => true;

    [Constructible]
    public JourneymansCrook()
    {
        Name = "a Journeyman's Crook";
        Hue  = 0x481;   // deeper teal-green
    }

    private void Deserialize(IGenericReader reader, int version) { }
}

// ── T3 ────────────────────────────────────────────────────────────────────────

[SerializationGenerator(0, false)]
public partial class WardensCrook : JourneymansCrook
{
    protected override double TamingBonus       => 15.0;
    protected override double PeacemakingBonus  => 15.0;
    protected override double AnimalLoreBonus   => 10.0;
    protected override TimeSpan BondCooldown    => TimeSpan.FromHours(24);
    protected override bool HasPeacefulApproach => false;   // subsumed by passive
    protected override bool HasPeacefulTaming   => true;

    [Constructible]
    public WardensCrook()
    {
        Name = "a Warden's Crook";
        Hue  = 0x497;   // rich gold-bronze
    }

    private void Deserialize(IGenericReader reader, int version) { }
}
