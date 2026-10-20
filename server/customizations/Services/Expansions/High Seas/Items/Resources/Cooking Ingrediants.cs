// cc-P193: ServUO pub57 Scripts/Services/Expansions/High Seas/Items/Resources/Cooking Ingrediants.cs -> ours, the High
// Seas fishing port. ServUO's own misspelt file name is kept so the port list's stem proxy counts the file.
//
// Four leaf items: the seasoning, sauce, truffle and ginger are the fish pies' ingredients (the pies and their Cooking
// recipes are left for the next prompt, so until then these are loot and staff items). ServUO's fifth, FishOilFlask (it
// recharges an Oracle of the Sea), is not ported: pinned already declares a FishOilFlask (Items/Misc/OilFlask.cs), a
// duplicate-type build failure, and the Oracle it charges comes with the next prompt, which decides the name then.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class MentoSeasoning : Item
{
    [Constructible]
    public MentoSeasoning() : base(2454) => Hue = 95;

    public override int LabelNumber => 1116299;
}

[SerializationGenerator(0, false)]
public partial class SamuelsSecretSauce : Item
{
    [Constructible]
    public SamuelsSecretSauce() : base(2463) => Hue = 1461;

    public override int LabelNumber => 1116338;
}

[SerializationGenerator(0, false)]
public partial class DarkTruffle : Item
{
    [Constructible]
    public DarkTruffle() : base(3352) => Hue = 1908;

    public override int LabelNumber => 1116300;
}

[SerializationGenerator(0, false)]
public partial class FreshGinger : Item
{
    [Constructible]
    public FreshGinger() : base(11235)
    {
    }

    public override int LabelNumber => 1031235;
}
