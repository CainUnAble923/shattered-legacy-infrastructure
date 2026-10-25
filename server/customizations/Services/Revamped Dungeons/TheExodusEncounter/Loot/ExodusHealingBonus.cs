// cc-P610 (P488 #2): ServUO Items/Resource/Bandage.cs:495: a healer holding Asclepius or Gargish Asclepius heals with 15
// more Healing (the bandage's chance and its amount both read it). Pinned's bandage has no such hook; the one line that
// reads this is server/patches/Bandage-asclepius-bonus.patch. The amount's shard scaling (Bandage-heal-scaled-200)
// then applies to the sum, as it does to any Healing value.

namespace Server.Items;

public static class ExodusHealingBonus
{
    public const double Asclepius = 15.0;

    /// <summary>The healing bonus the healer's held staff gives a bandage: 15 for either Asclepius, else 0.</summary>
    public static double For(Mobile healer) =>
        healer?.FindItemOnLayer(Layer.TwoHanded) is Server.Items.Asclepius or GargishAsclepius ? Asclepius : 0.0;
}
