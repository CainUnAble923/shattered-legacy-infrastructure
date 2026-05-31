using ModernUO.Serialization;
using Server.Items;

namespace Server.Mobiles;

// ─────────────────────────────────────────────────────────────────────────────
// ClusterFGargoyleVendors — Gargoyle-bodied vendor wrappers for Royal City
//
// Each class subclasses the matching human vendor and overrides InitBody() to
// apply a gargoyle body (666 male / 667 female), a gargoyle skin hue, and a
// name drawn from the "gargoyle vendor" name list.
//
// These are used by ClusterFRoyalCitySeeder to populate Ter Mur's Royal City
// with species-appropriate NPCs instead of the default human vendors.
//
// Clothing (InitOutfit) intentionally falls through to the base vendor to
// preserve inventory/trade stock. Visually the clothing will be on a gargoyle
// body — this may show minor rendering oddities but is functionally correct.
// ─────────────────────────────────────────────────────────────────────────────

// ── Shared helper ─────────────────────────────────────────────────────────────

internal static class GargoyleVendorHelper
{
    /// <summary>Applies gargoyle body, skin hue, and name to a mobile after InitBody().</summary>
    public static void Apply(Mobile m)
    {
        m.Hue  = Race.Gargoyle.RandomSkinHue();
        m.Body = m.Female ? 667 : 666;
        m.Name = NameList.RandomName("gargoyle vendor");
    }
}

// ── Trade vendors ─────────────────────────────────────────────────────────────

[SerializationGenerator(0, false)]
public partial class GargoyleAlchemist : Alchemist
{
    [Constructible] public GargoyleAlchemist() {}
    public GargoyleAlchemist(Serial s) : base(s) {}
    public override void InitBody() { base.InitBody(); GargoyleVendorHelper.Apply(this); }
    private void Deserialize(IGenericReader reader, int version) {}
}

[SerializationGenerator(0, false)]
public partial class GargoyleAnimalTrainer : AnimalTrainer
{
    [Constructible] public GargoyleAnimalTrainer() {}
    public GargoyleAnimalTrainer(Serial s) : base(s) {}
    public override void InitBody() { base.InitBody(); GargoyleVendorHelper.Apply(this); }
    private void Deserialize(IGenericReader reader, int version) {}
}

[SerializationGenerator(0, false)]
public partial class GargoyleArchitect : Architect
{
    [Constructible] public GargoyleArchitect() {}
    public GargoyleArchitect(Serial s) : base(s) {}
    public override void InitBody() { base.InitBody(); GargoyleVendorHelper.Apply(this); }
    private void Deserialize(IGenericReader reader, int version) {}
}

[SerializationGenerator(0, false)]
public partial class GargoyleArmorer : Armorer
{
    [Constructible] public GargoyleArmorer() {}
    public GargoyleArmorer(Serial s) : base(s) {}
    public override void InitBody() { base.InitBody(); GargoyleVendorHelper.Apply(this); }
    private void Deserialize(IGenericReader reader, int version) {}
}

[SerializationGenerator(0, false)]
public partial class GargoyleBaker : Baker
{
    [Constructible] public GargoyleBaker() {}
    public GargoyleBaker(Serial s) : base(s) {}
    public override void InitBody() { base.InitBody(); GargoyleVendorHelper.Apply(this); }
    private void Deserialize(IGenericReader reader, int version) {}
}

[SerializationGenerator(0, false)]
public partial class GargoyleBanker : Banker
{
    [Constructible] public GargoyleBanker() {}
    public GargoyleBanker(Serial s) : base(s) {}
    public override void InitBody() { base.InitBody(); GargoyleVendorHelper.Apply(this); }
    private void Deserialize(IGenericReader reader, int version) {}
}

[SerializationGenerator(0, false)]
public partial class GargoyleBarkeeper : Barkeeper
{
    [Constructible] public GargoyleBarkeeper() {}
    public GargoyleBarkeeper(Serial s) : base(s) {}
    public override void InitBody() { base.InitBody(); GargoyleVendorHelper.Apply(this); }
    private void Deserialize(IGenericReader reader, int version) {}
}

[SerializationGenerator(0, false)]
public partial class GargoyleButcher : Butcher
{
    [Constructible] public GargoyleButcher() {}
    public GargoyleButcher(Serial s) : base(s) {}
    public override void InitBody() { base.InitBody(); GargoyleVendorHelper.Apply(this); }
    private void Deserialize(IGenericReader reader, int version) {}
}

[SerializationGenerator(0, false)]
public partial class GargoyleCarpenter : Carpenter
{
    [Constructible] public GargoyleCarpenter() {}
    public GargoyleCarpenter(Serial s) : base(s) {}
    public override void InitBody() { base.InitBody(); GargoyleVendorHelper.Apply(this); }
    private void Deserialize(IGenericReader reader, int version) {}
}

[SerializationGenerator(0, false)]
public partial class GargoyleCobbler : Cobbler
{
    [Constructible] public GargoyleCobbler() {}
    public GargoyleCobbler(Serial s) : base(s) {}
    public override void InitBody() { base.InitBody(); GargoyleVendorHelper.Apply(this); }
    private void Deserialize(IGenericReader reader, int version) {}
}

[SerializationGenerator(0, false)]
public partial class GargoyleCustomHairstylist : CustomHairstylist
{
    [Constructible] public GargoyleCustomHairstylist() {}
    public GargoyleCustomHairstylist(Serial s) : base(s) {}
    public override void InitBody() { base.InitBody(); GargoyleVendorHelper.Apply(this); }
    private void Deserialize(IGenericReader reader, int version) {}
}

[SerializationGenerator(0, false)]
public partial class GargoyleFarmer : Farmer
{
    [Constructible] public GargoyleFarmer() {}
    public GargoyleFarmer(Serial s) : base(s) {}
    public override void InitBody() { base.InitBody(); GargoyleVendorHelper.Apply(this); }
    private void Deserialize(IGenericReader reader, int version) {}
}

[SerializationGenerator(0, false)]
public partial class GargoyleFurtrader : Furtrader
{
    [Constructible] public GargoyleFurtrader() {}
    public GargoyleFurtrader(Serial s) : base(s) {}
    public override void InitBody() { base.InitBody(); GargoyleVendorHelper.Apply(this); }
    private void Deserialize(IGenericReader reader, int version) {}
}

[SerializationGenerator(0, false)]
public partial class GargoyleHealer : Healer
{
    [Constructible] public GargoyleHealer() {}
    public GargoyleHealer(Serial s) : base(s) {}
    public override void InitBody() { base.InitBody(); GargoyleVendorHelper.Apply(this); }
    private void Deserialize(IGenericReader reader, int version) {}
}

[SerializationGenerator(0, false)]
public partial class GargoyleHerbalist : Herbalist
{
    [Constructible] public GargoyleHerbalist() {}
    public GargoyleHerbalist(Serial s) : base(s) {}
    public override void InitBody() { base.InitBody(); GargoyleVendorHelper.Apply(this); }
    private void Deserialize(IGenericReader reader, int version) {}
}

[SerializationGenerator(0, false)]
public partial class GargoyleInnKeeper : InnKeeper
{
    [Constructible] public GargoyleInnKeeper() {}
    public GargoyleInnKeeper(Serial s) : base(s) {}
    public override void InitBody() { base.InitBody(); GargoyleVendorHelper.Apply(this); }
    private void Deserialize(IGenericReader reader, int version) {}
}

[SerializationGenerator(0, false)]
public partial class GargoyleJeweler : Jeweler
{
    [Constructible] public GargoyleJeweler() {}
    public GargoyleJeweler(Serial s) : base(s) {}
    public override void InitBody() { base.InitBody(); GargoyleVendorHelper.Apply(this); }
    private void Deserialize(IGenericReader reader, int version) {}
}

[SerializationGenerator(0, false)]
public partial class GargoyleMage : Mage
{
    [Constructible] public GargoyleMage() {}
    public GargoyleMage(Serial s) : base(s) {}
    public override void InitBody() { base.InitBody(); GargoyleVendorHelper.Apply(this); }
    private void Deserialize(IGenericReader reader, int version) {}
}

[SerializationGenerator(0, false)]
public partial class GargoyleMinter : Minter
{
    [Constructible] public GargoyleMinter() {}
    public GargoyleMinter(Serial s) : base(s) {}
    public override void InitBody() { base.InitBody(); GargoyleVendorHelper.Apply(this); }
    private void Deserialize(IGenericReader reader, int version) {}
}

[SerializationGenerator(0, false)]
public partial class GargoyleProvisioner : Provisioner
{
    [Constructible] public GargoyleProvisioner() {}
    public GargoyleProvisioner(Serial s) : base(s) {}
    public override void InitBody() { base.InitBody(); GargoyleVendorHelper.Apply(this); }
    private void Deserialize(IGenericReader reader, int version) {}
}

[SerializationGenerator(0, false)]
public partial class GargoyleRealEstateBroker : RealEstateBroker
{
    [Constructible] public GargoyleRealEstateBroker() {}
    public GargoyleRealEstateBroker(Serial s) : base(s) {}
    public override void InitBody() { base.InitBody(); GargoyleVendorHelper.Apply(this); }
    private void Deserialize(IGenericReader reader, int version) {}
}

[SerializationGenerator(0, false)]
public partial class GargoyleScribe : Scribe
{
    [Constructible] public GargoyleScribe() {}
    public GargoyleScribe(Serial s) : base(s) {}
    public override void InitBody() { base.InitBody(); GargoyleVendorHelper.Apply(this); }
    private void Deserialize(IGenericReader reader, int version) {}
}

[SerializationGenerator(0, false)]
public partial class GargoyleTailor : Tailor
{
    [Constructible] public GargoyleTailor() {}
    public GargoyleTailor(Serial s) : base(s) {}
    public override void InitBody() { base.InitBody(); GargoyleVendorHelper.Apply(this); }
    private void Deserialize(IGenericReader reader, int version) {}
}

[SerializationGenerator(0, false)]
public partial class GargoyleTanner : Tanner
{
    [Constructible] public GargoyleTanner() {}
    public GargoyleTanner(Serial s) : base(s) {}
    public override void InitBody() { base.InitBody(); GargoyleVendorHelper.Apply(this); }
    private void Deserialize(IGenericReader reader, int version) {}
}

[SerializationGenerator(0, false)]
public partial class GargoyleTavernKeeper : TavernKeeper
{
    [Constructible] public GargoyleTavernKeeper() {}
    public GargoyleTavernKeeper(Serial s) : base(s) {}
    public override void InitBody() { base.InitBody(); GargoyleVendorHelper.Apply(this); }
    private void Deserialize(IGenericReader reader, int version) {}
}

[SerializationGenerator(0, false)]
public partial class GargoyleTinker : Tinker
{
    [Constructible] public GargoyleTinker() {}
    public GargoyleTinker(Serial s) : base(s) {}
    public override void InitBody() { base.InitBody(); GargoyleVendorHelper.Apply(this); }
    private void Deserialize(IGenericReader reader, int version) {}
}

[SerializationGenerator(0, false)]
public partial class GargoyleWaiter : Waiter
{
    [Constructible] public GargoyleWaiter() {}
    public GargoyleWaiter(Serial s) : base(s) {}
    public override void InitBody() { base.InitBody(); GargoyleVendorHelper.Apply(this); }
    private void Deserialize(IGenericReader reader, int version) {}
}

[SerializationGenerator(0, false)]
public partial class GargoyleWeaponsmith : Weaponsmith
{
    [Constructible] public GargoyleWeaponsmith() {}
    public GargoyleWeaponsmith(Serial s) : base(s) {}
    public override void InitBody() { base.InitBody(); GargoyleVendorHelper.Apply(this); }
    private void Deserialize(IGenericReader reader, int version) {}
}

[SerializationGenerator(0, false)]
public partial class GargoyleWeaver : Weaver
{
    [Constructible] public GargoyleWeaver() {}
    public GargoyleWeaver(Serial s) : base(s) {}
    public override void InitBody() { base.InitBody(); GargoyleVendorHelper.Apply(this); }
    private void Deserialize(IGenericReader reader, int version) {}
}

[SerializationGenerator(0, false)]
public partial class GargoyleStoneCrafter : StoneCrafter
{
    [Constructible] public GargoyleStoneCrafter() {}
    public GargoyleStoneCrafter(Serial s) : base(s) {}
    public override void InitBody() { base.InitBody(); GargoyleVendorHelper.Apply(this); }
    private void Deserialize(IGenericReader reader, int version) {}
}

// ── Guildmasters ──────────────────────────────────────────────────────────────

[SerializationGenerator(0, false)]
public partial class GargoyleBlacksmithGuildmaster : BlacksmithGuildmaster
{
    [Constructible] public GargoyleBlacksmithGuildmaster() {}
    public GargoyleBlacksmithGuildmaster(Serial s) : base(s) {}
    public override void InitBody() { base.InitBody(); GargoyleVendorHelper.Apply(this); }
    private void Deserialize(IGenericReader reader, int version) {}
}

[SerializationGenerator(0, false)]
public partial class GargoyleHealerGuildmaster : HealerGuildmaster
{
    [Constructible] public GargoyleHealerGuildmaster() {}
    public GargoyleHealerGuildmaster(Serial s) : base(s) {}
    public override void InitBody() { base.InitBody(); GargoyleVendorHelper.Apply(this); }
    private void Deserialize(IGenericReader reader, int version) {}
}

[SerializationGenerator(0, false)]
public partial class GargoyleMageGuildmaster : MageGuildmaster
{
    [Constructible] public GargoyleMageGuildmaster() {}
    public GargoyleMageGuildmaster(Serial s) : base(s) {}
    public override void InitBody() { base.InitBody(); GargoyleVendorHelper.Apply(this); }
    private void Deserialize(IGenericReader reader, int version) {}
}

[SerializationGenerator(0, false)]
public partial class GargoyleTailorGuildmaster : TailorGuildmaster
{
    [Constructible] public GargoyleTailorGuildmaster() {}
    public GargoyleTailorGuildmaster(Serial s) : base(s) {}
    public override void InitBody() { base.InitBody(); GargoyleVendorHelper.Apply(this); }
    private void Deserialize(IGenericReader reader, int version) {}
}

[SerializationGenerator(0, false)]
public partial class GargoyleTinkerGuildmaster : TinkerGuildmaster
{
    [Constructible] public GargoyleTinkerGuildmaster() {}
    public GargoyleTinkerGuildmaster(Serial s) : base(s) {}
    public override void InitBody() { base.InitBody(); GargoyleVendorHelper.Apply(this); }
    private void Deserialize(IGenericReader reader, int version) {}
}

// ── Town Crier ─────────────────────────────────────────────────────────────────
// TownCrier sets Body/Name inline in its constructor rather than via a virtual
// InitBody() method, so we override via the constructor body instead.

[SerializationGenerator(0, false)]
public partial class GargoyleTownCrier : TownCrier
{
    [Constructible]
    public GargoyleTownCrier()
    {
        // base() already ran, setting a human body — override to gargoyle.
        GargoyleVendorHelper.Apply(this);
    }

    public GargoyleTownCrier(Serial s) : base(s) {}
    private void Deserialize(IGenericReader reader, int version) {}
}
