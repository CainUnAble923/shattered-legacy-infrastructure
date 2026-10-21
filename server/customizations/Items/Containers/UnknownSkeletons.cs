// cc-P199: ServUO pub57 Scripts/Items/Containers/UnknownSkeletons.cs. Three skeletons (bone art 0xECA-0xED2, 35 stones,
// container gump 0x9) an ant lion carries 7% of the time (ServUO Mobiles/Normal/AntLion.cs:78-86); that hook is not made
// here (see the report).
//   UnknownBardSkeleton   200-400 gold, doublet and jester hat, 10-20 bandages, kilt or short pants (random nondyed
//                         hues), a bottle of ale, wine or liquor, and an instrument.
//   UnknownRogueSkeleton  leather chest, gloves and arms, a dagger, a 50-use shovel, 1-4 lockpicks, a torch or lantern,
//                         a ranged weapon (10%) or a weapon, and a level 3-5 Felucca treasure map.
//   UnknownMageSkeleton   a robe, sandals, a piece of jewelry, a quarter staff or gnarled staff, three reagents of 15-20,
//                         and three spell scrolls of 1-2 (each necromancy 25%, magery otherwise).
//
// ServUO's Name = "..." is DefaultName and its Weight = 35.0 is DefaultWeight. One difference: a scroll draw that comes
// back empty is skipped. ServUO draws the index with RandomMinMax(0, list length), one past the end (its Loot.RandomScroll,
// Misc/Loot.cs:930-960, the same in pinned Loot.cs:715-727), which returns null about one time in 65 for magery; ServUO
// then sets its Amount (UnknownSkeletons.cs:145), a null reference in the constructor.

using ModernUO.Serialization;

namespace Server.Items;

[SerializationGenerator(0, false)]
public partial class UnknownBardSkeleton : BaseContainer
{
    [Constructible]
    public UnknownBardSkeleton() : base(0xECA + Utility.Random(9))
    {
        DropItem(new Gold(Utility.RandomMinMax(200, 400)));
        DropItem(new Doublet(Utility.RandomNondyedHue()));
        DropItem(new JesterHat(Utility.RandomNondyedHue()));
        DropItem(new Bandage(Utility.RandomMinMax(10, 20)));

        switch (Utility.Random(2))
        {
            case 0:
                DropItem(new Kilt(Utility.RandomNondyedHue()));
                break;
            case 1:
                DropItem(new ShortPants(Utility.RandomNondyedHue()));
                break;
        }

        switch (Utility.Random(3))
        {
            case 0:
                DropItem(new BeverageBottle(BeverageType.Ale));
                break;
            case 1:
                DropItem(new BeverageBottle(BeverageType.Wine));
                break;
            case 2:
                DropItem(new BeverageBottle(BeverageType.Liquor));
                break;
        }

        DropItem(Loot.RandomInstrument());
    }

    public override int DefaultGumpID => 0x9;

    public override string DefaultName => "An Unknown Bard's Skeleton";

    public override double DefaultWeight => 35.0;
}

[SerializationGenerator(0, false)]
public partial class UnknownRogueSkeleton : BaseContainer
{
    [Constructible]
    public UnknownRogueSkeleton() : base(0xECA + Utility.Random(9))
    {
        DropItem(new LeatherChest());
        DropItem(new LeatherGloves());
        DropItem(new LeatherArms());
        DropItem(new Dagger());
        DropItem(new Shovel(50));
        DropItem(new Lockpick(Utility.RandomMinMax(1, 4)));

        if (Utility.RandomBool())
        {
            DropItem(new Torch());
        }
        else
        {
            DropItem(new Lantern());
        }

        if (0.1 >= Utility.RandomDouble())
        {
            DropItem(Loot.RandomRangedWeapon());
        }
        else
        {
            DropItem(Loot.RandomWeapon());
        }

        DropItem(new TreasureMap(Utility.RandomMinMax(3, 5), Map.Felucca));
    }

    public override int DefaultGumpID => 0x9;

    public override string DefaultName => "An Unknown Rogue's Skeleton";

    public override double DefaultWeight => 35.0;
}

[SerializationGenerator(0, false)]
public partial class UnknownMageSkeleton : BaseContainer
{
    [Constructible]
    public UnknownMageSkeleton() : base(0xECA + Utility.Random(9))
    {
        DropItem(new Robe(Utility.RandomNondyedHue()));
        DropItem(new Sandals());
        DropItem(Loot.RandomJewelry());

        if (Utility.RandomBool())
        {
            DropItem(new QuarterStaff());
        }
        else
        {
            DropItem(new GnarledStaff());
        }

        Item item;

        for (var i = 0; i < 3; i++)
        {
            item = Loot.RandomReagent();
            item.Amount = Utility.RandomMinMax(15, 20);
            DropItem(item);
        }

        for (var i = 0; i < 3; i++)
        {
            if (0.25 >= Utility.RandomDouble())
            {
                item = Loot.RandomScroll(0, Loot.NecromancyScrollTypes.Length, SpellbookType.Necromancer);
            }
            else
            {
                item = Loot.RandomScroll(0, Loot.RegularScrollTypes.Length, SpellbookType.Regular);
            }

            if (item == null)
            {
                continue;
            }

            item.Amount = Utility.RandomMinMax(1, 2);
            DropItem(item);
        }
    }

    public override int DefaultGumpID => 0x9;

    public override string DefaultName => "An Unknown Mage's Skeleton";

    public override double DefaultWeight => 35.0;
}
