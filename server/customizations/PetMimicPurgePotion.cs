using System;
using Server.Gumps;
using Server.Network;
using Server.Targeting;

namespace Server.Items;

public class PetMimicPurgePotion : Item
{
    [Constructible]
    public PetMimicPurgePotion() : base(0xF09)
    {
        Weight   = 1.0;
        Name     = "a mimic purge potion";
        Hue      = 1109; // deep blue-violet, distinct from standard potions
        LootType = LootType.Regular;
    }

    public PetMimicPurgePotion(Serial serial) : base(serial) { }

    public override void OnDoubleClick(Mobile from)
    {
        if (!IsChildOf(from.Backpack))
        {
            from.SendLocalizedMessage(1042001);
            return;
        }

        from.SendMessage(0x44, "Target your pet mimic to purge its accumulated stats.");
        from.Target = new PurgeTarget(this);
    }

    private class PurgeTarget : Target
    {
        private readonly PetMimicPurgePotion _potion;

        public PurgeTarget(PetMimicPurgePotion potion) : base(3, false, TargetFlags.None)
        {
            _potion = potion;
        }

        protected override void OnTarget(Mobile from, object targeted)
        {
            if (_potion.Deleted || !_potion.IsChildOf(from.Backpack))
            {
                from.SendMessage(0x22, "The potion is no longer available.");
                return;
            }

            if (targeted is not PetMimic mimic)
            {
                from.SendMessage(0x59, "That is not a pet mimic.");
                return;
            }

            if (mimic.RootParent != from)
            {
                from.SendMessage(0x22, "You must own that pet mimic.");
                return;
            }

            if (!mimic.HasAccumulatedStats)
            {
                from.SendMessage(0x59, "Your pet mimic is already dormant — there is nothing to purge.");
                return;
            }

            from.SendGump(new PetMimicPurgeConfirmGump(from, mimic, _potion));
        }

        protected override void OnTargetCancel(Mobile from, TargetCancelType cancelType)
        {
            from.SendMessage(0x59, "You decide not to use the purge potion.");
        }
    }

    public override void Serialize(IGenericWriter writer)
    {
        base.Serialize(writer);
        writer.Write(0);
    }

    public override void Deserialize(IGenericReader reader)
    {
        base.Deserialize(reader);
        _ = reader.ReadInt();
    }
}

public class PetMimicPurgeConfirmGump : Gump
{
    private const int GumpW = 380;
    private const int PadX  = 20;

    private readonly Mobile              _from;
    private readonly PetMimic            _mimic;
    private readonly PetMimicPurgePotion _potion;

    public PetMimicPurgeConfirmGump(Mobile from, PetMimic mimic, PetMimicPurgePotion potion)
        : base(100, 100)
    {
        _from   = from;
        _mimic  = mimic;
        _potion = potion;

        const int gumpH = 236;
        AddBackground(0, 0, GumpW, gumpH, 9200);

        AddLabel(GumpW / 2 - 80, 10, 0x386, "Pet Mimic — Purge Confirmation");

        AddLabel(PadX, 40, 0x020, "WARNING: This will permanently destroy:");
        AddLabel(PadX + 8, 58,  0x22, "• All accumulated stats (Str, Dex, Int, LRC, FC, FCR, DI)");
        AddLabel(PadX + 8, 74,  0x22, "• All accumulated resistances");
        AddLabel(PadX + 8, 90,  0x22, "• All accumulated skill bonuses");
        AddLabel(PadX + 8, 106, 0x22, "• Category lock — mimic returns to dormant state");

        AddLabel(PadX, 130, 0x44,  "Preserved: Health, max HP bonus from gems, form journal.");
        AddLabel(PadX, 148, 0x47E, "The active form will reset to dormant until re-awakened.");

        AddLabel(PadX, 168, 0x386, "This action cannot be undone.");

        const int btnY = 196;
        AddButton(PadX,       btnY, 4005, 4007, 1, GumpButtonType.Reply, 0);
        AddLabel(PadX + 35,   btnY + 2, 0x020, "Purge");

        AddButton(GumpW - 90, btnY, 4017, 4019, 0, GumpButtonType.Reply, 0);
        AddLabel(GumpW - 55,  btnY + 2, 0x455, "Cancel");
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (info.ButtonID != 1)
        {
            _from.SendMessage(0x59, "You decide not to purge the mimic.");
            return;
        }

        if (_mimic.Deleted || _potion.Deleted)
        {
            _from.SendMessage(0x22, "Something is no longer available.");
            return;
        }

        if (!_potion.IsChildOf(_from.Backpack))
        {
            _from.SendMessage(0x22, "The purge potion is no longer in your pack.");
            return;
        }

        var category = _mimic.LockedCategory; // capture before Purge() resets it

        _mimic.Purge(_from);
        _potion.Delete();

        _from.SendMessage(0x44, "Your pet mimic shudders as the potion takes hold, releasing all accumulated essence.");
        _from.SendMessage(0x59, "*BLAAAARGH*");
        Effects.PlaySound(_from.Location, _from.Map, PickPukeSound(_mimic));
        _from.FixedParticles(0x374A, 10, 15, 5028, EffectLayer.Head);

        SpawnVomitScatter(_from, category);
    }

    // TODO: return mimic.Female ? 0x43F : 0x32D once gender is added to PetMimic
    private static int PickPukeSound(PetMimic mimic) =>
        Utility.RandomBool() ? 0x32D : 0x43F;

    // Scatter category-matched junk + dungeon acid puddles around the player.
    private static void SpawnVomitScatter(Mobile from, MimicCategory category)
    {
        var map = from.Map;
        if (map == null || map == Map.Internal)
            return;

        // Category-specific gear debris — the half-digested haul
        int[] ids   = VomitItemIDs(category);
        int   count = Utility.RandomMinMax(6, 9);

        for (var i = 0; i < count; i++)
        {
            int dx = Utility.RandomMinMax(-2, 2);
            int dy = Utility.RandomMinMax(-2, 2);

            var junk = new Static(ids[Utility.Random(ids.Length)])
            {
                Hue     = VomitHues[Utility.Random(VomitHues.Length)],
                Movable = false
            };
            junk.MoveToWorld(new Point3D(from.X + dx, from.Y + dy, from.Z), map);
            Timer.DelayCall(TimeSpan.FromSeconds(8.0), junk.Delete);
        }

        // Acid puddles — the digestive fluid that comes with the haul
        int puddles = Utility.RandomMinMax(3, 5);

        for (var i = 0; i < puddles; i++)
        {
            int dx = Utility.RandomMinMax(-3, 3);
            int dy = Utility.RandomMinMax(-3, 3);

            var puddle = new Acid();
            puddle.MoveToWorld(new Point3D(from.X + dx, from.Y + dy, from.Z), map);
            Timer.DelayCall(TimeSpan.FromSeconds(10.0), puddle.Delete); // puddles linger a bit longer
        }

        // Follow-up splat sound once everything lands
        Timer.DelayCall(TimeSpan.FromSeconds(0.3), () =>
            Effects.PlaySound(from.Location, map, 0x1FE));
    }

    // Acid-green palette for the splattered gear debris
    private static readonly int[] VomitHues = { 0x4B, 0x4E, 0x55, 0x59, 0x44 };

private static int[] VomitItemIDs(MimicCategory category) => category switch
    {
        MimicCategory.WeaponSwordsmanship => new[] { 0xF5E, 0xF4B, 0xF60, 0xF52, 0x13B0 },
        MimicCategory.WeaponMaceFighting  => new[] { 0xF5C, 0xF5D, 0x143D, 0xF5E, 0x13B0 },
        MimicCategory.WeaponFencing       => new[] { 0x1400, 0x1401, 0xF62, 0xF4F, 0xF51  },
        MimicCategory.WeaponArchery       => new[] { 0x13B2, 0x13FD, 0x13FC, 0xF3F, 0xF3E },
        MimicCategory.WeaponWrestling     => new[] { 0x1406, 0x1407, 0x1408, 0x1409, 0xF62 },
        MimicCategory.Shield              => new[] { 0x1B76, 0x1B78, 0x1B7A, 0x1B72, 0x1BC3 },
        MimicCategory.Jewelry             => new[] { 0x108A, 0x1086, 0x1088, 0x1084, 0x4C12 },
        MimicCategory.Armor               => new[] { 0x1415, 0x13BB, 0x13C4, 0x1410, 0x1411 },
        MimicCategory.Clothing            => new[] { 0x1F03, 0x1F04, 0x170B, 0x170D, 0x1516 },
        MimicCategory.Tool                => new[] { 0xE86,  0x13E3, 0x1039, 0x1052, 0xFBF  },
        _                                 => new[] { 0x1BC3, 0x122A, 0x122C, 0x122E, 0x171C }
    };
}
