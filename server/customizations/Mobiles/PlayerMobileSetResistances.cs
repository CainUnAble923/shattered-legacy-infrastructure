namespace Server.Mobiles
{
    /// <summary>
    ///     Mondain's Legacy armour set resistances.
    ///     <para>
    ///         ServUO applies these in <c>PlayerMobile.ComputeResistances</c>, replacing the item's own
    ///         resistance with <c>ISetItem.SetResistBonus</c> for every equipped set piece. ModernUO
    ///         computes resistances in <c>Server.Mobile.ComputeResistances</c>, which lives in the core
    ///         project and cannot see <see cref="ISetItem" />, so
    ///         <c>server/patches/Mobile-set-item-resistance-hook.patch</c> routes each item through a
    ///         virtual <c>GetItemResistance</c> and this override supplies the set-aware value.
    ///     </para>
    /// </summary>
    public partial class PlayerMobile
    {
        public override int GetItemResistance(Item item, ResistanceType type) =>
            item is ISetItem { SetEquipped: true } setItem
                ? setItem.SetResistBonus(type)
                : base.GetItemResistance(item, type);
    }
}
