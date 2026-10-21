// cc-P262 (spec section 4): toe rings, Plate Socks and the two joke toe rings.
//
// A toe ring is a jewel worn only in an accessory slot ([accessories), never on a paperdoll layer, and it counts only while
// the shoes layer is empty or holds Plate Socks (ClusterFAccessories.IsActive). It is a BaseJewel, so it takes the same
// properties (crafted, imbued, enhanced) as a ring. Tinkering makes it (AccessoryCraftRegistrations), under the recipe
// gate like every Tinkering item above skill 30. No toe ring art exists: it uses the ring graphic until Chase draws one.

using System;
using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class ToeRing : BaseJewel
{
    [Constructible]
    public ToeRing() : base(0x108a, Layer.Ring)
    {
    }

    public override double DefaultWeight => 0.1;

    public override string DefaultName => "a toe ring";

    public override int BaseGemTypeNumber => 1044176; // star sapphire, as a gold ring's

    // Never a paperdoll ring: only the accessory slots take it.
    public override bool CanEquip(Mobile from)
    {
        from.SendMessage("Toe rings go on toes. Use [accessories.");
        return false;
    }
}

/// <summary>A toe ring with no properties at all, and a smell of smoke (spec: a joke item).</summary>
[SerializationGenerator(0, false)]
public partial class SingedToeRing : ToeRing
{
    [Constructible]
    public SingedToeRing()
    {
        Hue = 0x455;
    }

    public override string DefaultName => "a singed toe ring";

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add(1070722, "It has seen things. Mostly lava."); // ~1_NOTHING~
    }
}

/// <summary>A toe ring whose hue changes every few minutes (clusterf.accessories.moodRingMinutes), worn or not.</summary>
[SerializationGenerator(0, false)]
public partial class MoodToeRing : ToeRing
{
    private static readonly int[] Moods = [0x21, 0x35, 0x59, 0x5F, 0x7E, 0x8A5, 0x482, 0x26];

    private TimerExecutionToken _timer;

    [Constructible]
    public MoodToeRing()
    {
        Hue = Moods[0];
        StartTimer();
    }

    public override string DefaultName => "a mood toe ring";

    private void StartTimer()
    {
        var every = TimeSpan.FromMinutes(Math.Max(1, AccessorySettings.MoodRingMinutes));
        Timer.StartTimer(every, every, ChangeMood, out _timer);
    }

    /// <summary>The next mood (tests call it; the timer calls it every few minutes).</summary>
    public void ChangeMood()
    {
        var i = Array.IndexOf(Moods, Hue);
        Hue = Moods[(i + 1 + Utility.Random(Moods.Length - 1)) % Moods.Length];
    }

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add(1070722, "Its mood is none of your business."); // ~1_NOTHING~
    }

    [AfterDeserialization]
    private void AfterDeserialization() => StartTimer();

    public override void OnDelete()
    {
        _timer.Cancel();
        base.OnDelete();
    }
}

/// <summary>Cosmetic footwear with no armour. Toe rings count under it.</summary>
[SerializationGenerator(0, false)]
public partial class PlateSocks : BaseShoes
{
    [Constructible]
    public PlateSocks() : base(0x170F)
    {
        Hue = 0x0B0C; // iron grey
    }

    public override double DefaultWeight => 1.0;

    public override string DefaultName => "plate socks";

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add(1070722, "Technically shoes. The System will allow it."); // ~1_NOTHING~
    }
}
