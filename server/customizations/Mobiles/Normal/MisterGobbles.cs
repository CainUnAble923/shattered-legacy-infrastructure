// cc-P859: Mister Gobbles, the rare tamable turkey the Horn of Plenty (Items/Thanksgiving/HornOfPlenty.cs) sometimes
// lets loose. From Nuggzy's Horn of Plenty v1.2 (credit Nuggzy), where he is his own creature; ServUO only renames a
// Turkey. Ours is our Turkey (cc-P119, ServUO's stats) under his own name and type, tamable and wild when he appears,
// so he gobbles until someone tames him. No stat or skill differences: nothing here is worth more than a turkey.

using ModernUO.Serialization;

namespace Server.Mobiles;

[SerializationGenerator(0, false)]
public partial class MisterGobbles : Turkey
{
    [Constructible]
    public MisterGobbles() : base(true)
    {
    }

    public override string DefaultName => "Mister Gobbles";
}
