using ModernUO.Serialization;

namespace Server.Items;

/// <summary>
/// Phantom weapon spawned and managed by PetMimic when locked to a weapon category.
/// Players cannot move or interact with this item — PetMimic owns its lifecycle.
/// Provides the correct weapon slot, skill routing, and combat animation for the mimic's form.
/// </summary>
[SerializationGenerator(0, false)]
public partial class PetMimicWeapon : BaseWeapon
{
    [SerializableProperty(0)]
    public Serial MimicSerial
    {
        get => _mimicSerial;
        set => _mimicSerial = value;
    }

    [SerializableProperty(1)]
    public MimicCategory Category
    {
        get => _category;
        set => _category = value;
    }

    public PetMimicWeapon(PetMimic mimic) : base(mimic.ActiveFormItemID)
    {
        _mimicSerial = mimic.Serial;
        _category    = mimic.LockedCategory;

        Layer    = _category == MimicCategory.WeaponArchery ? Layer.TwoHanded : Layer.OneHanded;
        Hue      = PetMimicArt.MimicHue;
        Name     = "a pet mimic";
        LootType = LootType.Blessed;
        Movable  = false;
    }

    public static void Configure()
    {
        EventSink.WorldLoad += OnWorldLoad;
    }

    // On world load, any mimic that was equipped with a weapon category but has no linked
    // phantom weapon (first deploy of this feature) gets one created here.
    private static void OnWorldLoad()
    {
        foreach (var mobile in World.Mobiles.Values)
        {
            if (mobile.FindItemOnLayer(Layer.Talisman) is PetMimic mimic
                && PetMimic.IsWeaponCategory(mimic.LockedCategory)
                && mimic.Weapon == null)
            {
                mimic.AttachWeapon(mobile);
            }
        }
    }

    // Called by AfterDeserialization to re-link to the owning mimic after world load.
    private PetMimic GetMimic() => World.FindItem(_mimicSerial) as PetMimic;

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        var mimic = GetMimic();
        if (mimic != null)
            mimic.SetWeapon(this);
        else
            Delete(); // mimic is gone, clean up the orphaned weapon
    }

    // Sync item graphic, category, and weapon hit-procs from mimic when the player switches forms.
    public void SyncFromMimic(PetMimic mimic)
    {
        ItemID    = mimic.ActiveFormItemID;
        Hue       = PetMimicArt.MimicHue;
        _category = mimic.ActiveFormCategory; // cache so fallback is correct if mimic is temporarily unavailable
        mimic.SyncWeaponAttributes(this);
        InvalidateProperties();
    }

    // Players cannot double-click this weapon directly.
    public override void OnDoubleClick(Mobile from) { }

    // ── BaseWeapon abstract requirements ─────────────────────────────────────
    // All category-dependent properties read the live ActiveFormCategory from the
    // owning mimic so they update automatically when the player switches forms.
    // _category is kept in sync by SyncFromMimic and serves as a fallback when
    // the mimic is temporarily unreachable (e.g. during world load).

    private MimicCategory LiveCategory => GetMimic()?.ActiveFormCategory ?? _category;

    // Range: bows use 10 (standard UO bow range), all melee forms use 1.
    public override int DefMaxRange => LiveCategory == MimicCategory.WeaponArchery ? 10 : 1;

    public override WeaponType DefType => LiveCategory switch
    {
        MimicCategory.WeaponMaceFighting => WeaponType.Bashing,
        MimicCategory.WeaponFencing      => WeaponType.Piercing,
        MimicCategory.WeaponArchery      => WeaponType.Ranged,
        _                                => WeaponType.Slashing
    };

    public override WeaponAnimation DefAnimation => LiveCategory switch
    {
        MimicCategory.WeaponSwordsmanship => WeaponAnimation.Slash1H,
        MimicCategory.WeaponMaceFighting  => WeaponAnimation.Bash1H,
        MimicCategory.WeaponFencing       => WeaponAnimation.Pierce1H,
        MimicCategory.WeaponArchery       => WeaponAnimation.ShootBow,
        _                                 => WeaponAnimation.Slash1H
    };

    public override SkillName DefSkill => LiveCategory switch
    {
        MimicCategory.WeaponSwordsmanship => SkillName.Swords,
        MimicCategory.WeaponMaceFighting  => SkillName.Macing,
        MimicCategory.WeaponFencing       => SkillName.Fencing,
        MimicCategory.WeaponArchery       => SkillName.Archery,
        _                                 => SkillName.Swords
    };

    public override int DefHitSound => LiveCategory switch
    {
        MimicCategory.WeaponMaceFighting => 0x233,
        MimicCategory.WeaponFencing      => 0x23C,
        MimicCategory.WeaponArchery      => 0x234,
        _                                => 0x23B
    };

    public override int DefMissSound => LiveCategory == MimicCategory.WeaponArchery ? 0x23A : 0x239;

    // ── Damage — delegate to mimic so it scales with forms eaten ─────────────
    // MinDamage/MaxDamage in AOS mode reads AosMinDamage/AosMaxDamage (both virtual).
    // OldMinDamage/OldMaxDamage serve as the non-AOS fallback.
    // Both paths delegate to the mimic so the same formula applies everywhere.
    public override int AosMinDamage => GetMimic()?.EffectiveWeaponMinDamage ?? ClusterFPetMimicSettings.WeaponBaseDamageMin;
    public override int AosMaxDamage => GetMimic()?.EffectiveWeaponMaxDamage ?? ClusterFPetMimicSettings.WeaponBaseDamageMax;
    public override int OldMinDamage => GetMimic()?.EffectiveWeaponMinDamage ?? ClusterFPetMimicSettings.WeaponBaseDamageMin;
    public override int OldMaxDamage => GetMimic()?.EffectiveWeaponMaxDamage ?? ClusterFPetMimicSettings.WeaponBaseDamageMax;

    public override int   OldStrengthReq => 10;
    public override int   OldSpeed       => 40;
    public override int   AosSpeed       => 40;
    public override float MlSpeed        => 3.00f; // must be non-zero; 0 maps to 1-hour GetDelay
    public override int   InitMinHits    => 255;
    public override int   InitMaxHits    => 255;
}
