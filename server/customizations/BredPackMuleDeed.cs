using System;
using ModernUO.Serialization;
using Server.Mobiles;

namespace Server.Items;

// -----------------------------------------------------------------------------------------------------------------------
// Bred Pack Mule Deed: a foal (cc-P69, F-33 Part D).
//
// The breeding system makes it the moment two mules breed, with the foal's genes already rolled (sex, stat quality,
// traits, family name, generation, parents), and keeps it on the internal map for the 24-hour gestation with the owner
// and the due time saved, so a restart no longer loses the foal. When it is due it goes to the owner's bank box.
// The deed shows the foal before it is used, and can be traded. Using it summons a bonded tier 1 mule with those genes.
// A deed from before cc-P69 (version 0) has no genes: it rolls fresh ones when used, as generation 1.
// -----------------------------------------------------------------------------------------------------------------------

[SerializationGenerator(1, false)]
public partial class BredPackMuleDeed : Item
{
    [SerializableField(0, setter: "private")]
    private bool _rolled;

    [SerializableField(1, setter: "private")]
    private bool _isMale;

    [SerializableField(2, setter: "private")]
    private int _qualityHits;

    [SerializableField(3, setter: "private")]
    private int _qualityStr;

    [SerializableField(4, setter: "private")]
    private int _qualityStam;

    [SerializableField(5, setter: "private")]
    private int _qualityResist;

    [SerializableField(6, setter: "private")]
    private int _qualityPack;

    [SerializableField(7, setter: "private")]
    private int[] _traits;

    [SerializableField(8, setter: "private")]
    private string _familyName;

    [SerializableField(9, setter: "private")]
    private int _generation;

    [SerializableField(10, setter: "private")]
    private uint _parent1;

    [SerializableField(11, setter: "private")]
    private uint _parent2;

    [SerializableField(12, setter: "private")]
    private int _inheritedTier5;

    // Gestation: who it goes to and when. Null recipient = delivered (or never pending).
    [SerializableField(13, setter: "private")]
    [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
    private DateTime _deliverAt;

    [SerializableField(14, setter: "private")]
    [SerializedCommandProperty(AccessLevel.GameMaster, readOnly: true)]
    private Mobile _recipient;

    [Constructible]
    public BredPackMuleDeed() : this(null)
    {
    }

    public BredPackMuleDeed(MuleGenes genes) : base(0x14F0)
    {
        Hue = 1154; // golden: bred stock at a glance
        LootType = LootType.Blessed;
        _traits = Array.Empty<int>();

        if (genes != null)
        {
            SetGenes(genes);
        }
    }

    public override string DefaultName => "a bred pack mule deed";

    public bool Pending => _recipient != null;

    public MuleGenes Genes =>
        new()
        {
            IsMale = _isMale,
            QualityHits = _qualityHits,
            QualityStr = _qualityStr,
            QualityStam = _qualityStam,
            QualityResist = _qualityResist,
            QualityPack = _qualityPack,
            Traits = MuleGenes.DecodeTraits(_traits),
            FamilyName = _familyName,
            Generation = _generation,
            Parent1 = _parent1,
            Parent2 = _parent2,
            InheritedTier5 = _inheritedTier5
        };

    private void SetGenes(MuleGenes g)
    {
        _rolled = true;
        _isMale = g.IsMale;
        _qualityHits = g.QualityHits;
        _qualityStr = g.QualityStr;
        _qualityStam = g.QualityStam;
        _qualityResist = g.QualityResist;
        _qualityPack = g.QualityPack;
        _traits = g.EncodeTraits();
        _familyName = g.FamilyName;
        _generation = g.Generation;
        _parent1 = g.Parent1;
        _parent2 = g.Parent2;
        _inheritedTier5 = g.InheritedTier5;
        this.MarkDirty();
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);

        if (!_rolled)
        {
            list.Add("A foal of unknown stock");
            return;
        }

        var g = Genes;
        list.Add($"A {(g.IsMale ? "male" : "female")} foal, tier 1, generation {g.Generation}");
        list.Add($"{(string.IsNullOrEmpty(g.FamilyName) ? "No family" : g.FamilyName)} line");
        list.Add($"Quality: hits {g.QualityHits}%, strength {g.QualityStr}%, stamina {g.QualityStam}%, resists {g.QualityResist}%, pack {g.QualityPack}%");
        list.Add($"Bloodline: {g.TraitsText()}");
    }

    // --- gestation -----------------------------------------------------------------------------------------------------

    /// <summary>Holds the deed on the internal map until it is due, then delivers it to the owner's bank.</summary>
    public void BeginGestation(Mobile owner, DateTime due)
    {
        DeliverAt = due;
        Recipient = owner;
        Internalize();
        Arm();
    }

    private void Arm()
    {
        var wait = _deliverAt - DateTime.UtcNow;
        Timer.DelayCall(wait > TimeSpan.Zero ? wait : TimeSpan.Zero, () => TryDeliver(DateTime.UtcNow));
    }

    /// <summary>Delivers the foal deed if it is due. True when it was delivered now.</summary>
    public bool TryDeliver(DateTime now)
    {
        if (Deleted || _recipient == null || now < _deliverAt)
        {
            return false;
        }

        var owner = _recipient;
        Recipient = null;

        if (owner.Deleted || owner.BankBox is not { } bank)
        {
            Delete(); // the owner is gone: so is the foal
            return false;
        }

        bank.DropItem(this);

        if (owner.NetState != null)
        {
            owner.SendMessage(0x44, "A bred pack mule deed has been delivered to your bank box. The lineage continues.");
            owner.PlaySound(0x3D);
        }

        return true;
    }

    // --- use -----------------------------------------------------------------------------------------------------------

    public override void OnDoubleClick(Mobile from)
    {
        if (from is not PlayerMobile pm || Pending)
        {
            return;
        }

        if (!IsChildOf(pm.Backpack))
        {
            pm.SendLocalizedMessage(1042001); // That must be in your pack for you to use it.
            return;
        }

        var genes = _rolled ? Genes : FreshForOldDeed();

        if (PackMule.SummonFor(pm, MuleData.MinTier, genes, "Your bred pack mule arrives, carrying the strength of its lineage.") != null)
        {
            Delete();
        }
    }

    private static MuleGenes FreshForOldDeed()
    {
        var g = MuleGenes.Fresh();
        g.Generation = 1;
        return g;
    }

    private void MigrateFrom(V0Content content)
    {
        _rolled = false;
        _traits = Array.Empty<int>();
    }

    [AfterDeserialization]
    private void AfterDeserialization()
    {
        _traits ??= Array.Empty<int>();

        if (_recipient != null)
        {
            Arm(); // a restart during gestation: the foal still comes
        }
    }
}
