using System;
using ModernUO.Serialization;
using Server.Targeting;

namespace Server.Items
{
    // ServUO: Items/Artifacts/Decorative/LargeFishingNet.cs (CC9 batch 5). Targets a non-player corpse lying in
    // deep water and drags it to the user's feet half a second later. ServUO calls SOS.ValidateDeepWater for the
    // water test; pinned ModernUO's is private (Items/Skill Items/Fishing/Misc/SOS.cs:216), so its two
    // land-tile ranges are reproduced here. The two SendMessage strings are ServUO's own (no cliloc).
    [Flippable(7845, 7846)]
    [SerializationGenerator(0, false)]
    public partial class LargeFishingNet : Item
    {
        // SOS.WaterTiles, pinned ModernUO.
        private static readonly int[] WaterTiles =
        {
            0x00A8, 0x00AB,
            0x0136, 0x0137
        };

        [Constructible]
        public LargeFishingNet() : base(7845)
        {
        }

        public override int LabelNumber => 1149955;

        public override void OnDoubleClick(Mobile from)
        {
            if (IsChildOf(from.Backpack))
            {
                from.SendMessage("Target a corpse you'd like to net.");
                from.BeginTarget(10, false, TargetFlags.None, Net_OnTarget);
            }
        }

        public void Net_OnTarget(Mobile from, object targeted)
        {
            if (targeted is Corpse c && (c.Owner == null || !c.Owner.Player))
            {
                if (ValidateDeepWater(c.Map, c.X, c.Y))
                {
                    from.Animate(12, 5, 1, true, false, 0);
                    Timer.DelayCall(TimeSpan.FromSeconds(0.5), () => MoveCorpse(c, from));
                }
                else
                {
                    from.SendLocalizedMessage(1010485); // You can only use this in deep water!
                }
            }
            else
            {
                from.SendMessage("You can only net corpses!");
            }
        }

        private static void MoveCorpse(Corpse c, Mobile from)
        {
            if (c?.Deleted == false && from?.Deleted == false)
            {
                c.MoveToWorld(from.Location, from.Map);
            }
        }

        private static bool ValidateDeepWater(Map map, int x, int y)
        {
            var tileID = map.Tiles.GetLandTile(x, y).ID;
            var water = false;

            for (var i = 0; !water && i < WaterTiles.Length; i += 2)
            {
                water = tileID >= WaterTiles[i] && tileID <= WaterTiles[i + 1];
            }

            return water;
        }

        public override void AddNameProperties(IPropertyList list)
        {
            base.AddNameProperties(list);

            list.Add(1041645); // recovered from a shipwreck
        }
    }
}
