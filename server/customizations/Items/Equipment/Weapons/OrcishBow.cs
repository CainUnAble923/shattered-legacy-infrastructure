using ModernUO.Serialization;

namespace Server.Items
{
    // ServUO: Items/Equipment/Weapons/OrcishBow.cs (CC9 batch 3). A hued Bow with fixed attributes.
    // ServUO's BaseWeapon also emits 1151780 ("durability +~1_VAL~%") for DurabilityBonus, so on ServUO
    // this bow shows two durability lines; pinned ModernUO's BaseWeapon emits none, so here it shows
    // the one this override adds. Logged in shard-migration/notes/cc9-artifacts.md section 13.
    [SerializationGenerator(0, false)]
    public partial class OrcishBow : Bow
    {
        [Constructible]
        public OrcishBow()
        {
            Hue = 1107;
            Attributes.WeaponDamage = 25;
            WeaponAttributes.DurabilityBonus = 70;
        }

        public override int LabelNumber => 1153778; // an orcish bow

        public override void AddWeightProperty(IPropertyList list)
        {
            base.AddWeightProperty(list);
            list.Add(1060410, WeaponAttributes.DurabilityBonus.ToString()); // durability ~1_val~%
        }
    }
}
