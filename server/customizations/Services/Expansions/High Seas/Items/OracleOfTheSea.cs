// cc-P269: ServUO pub57 Scripts/Services/Expansions/High Seas/Items/OracleOfTheSea.cs -> ours, High Seas fishing, second
// half. A spyglass with five uses that finds Charybdis: double-click it in the pack and, when the charybdis spawner has
// none summoned and its day is up, it names a spot at sea in Trammel or Felucca (in sextant coordinates) and uses one
// charge. Charybdis then rises for whoever casts a charybdis-baited pole into that spot with 100 Fishing and the Oracle in
// their pack (HighSeasFishing.TrySummonCharybdis). With no charges left, or outside the pack, it is a plain spyglass.
// Players get it as a profession fish quest reward (FishQuestHelper.GiveRewards).
//
// Changes from ServUO (shard-migration notes/cc-P269-high-seas-fishing-second-half.md):
// - ServUO shows the plain spyglass view for an empty Oracle (or one outside the pack) and then searches anyway, so an
//   Oracle with no charges kept finding Charybdis, its count going below zero ("-1 / 5"). Here the plain view ends it
//   (a bug with a broken result is fixed, docs/bug-list.md D-4 rule).

using ModernUO.Serialization;
using Server.Mobiles;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class OracleOfTheSea : Spyglass
{
    public static readonly int MaxUses = 5;

    [InvalidateProperties]
    [SerializedCommandProperty(AccessLevel.GameMaster)]
    [SerializableField(0)]
    private int _usesRemaining;

    [Constructible]
    public OracleOfTheSea()
    {
        Hue = 1265;
        _usesRemaining = MaxUses;
    }

    public override int LabelNumber => 1150184;

    public override void GetProperties(IPropertyList list)
    {
        base.GetProperties(list);
        list.Add(1060728, $"{_usesRemaining}\t{MaxUses}"); // ~1_val~ / ~2_val~
        list.Add(1150207, $"{1150208:#}\t");            // Searching for: ~1_token~
    }

    public override void OnDoubleClick(Mobile from)
    {
        if (_usesRemaining <= 0 || !IsChildOf(from.Backpack))
        {
            base.OnDoubleClick(from);
            return;
        }

        if (CharydbisSpawner.SpawnInstance?.TrySpawnCharybdis(from) == true)
        {
            UsesRemaining--;
        }
    }
}
