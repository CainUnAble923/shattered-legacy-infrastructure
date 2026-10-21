// cc-P233: a creature's AbilityProfile (and TrainingProfile) opens from [props.
//
// Why it did not: pinned's props gump draws a row's gold '>' only when the property can be modified, which it reads as
// "the attribute is not read-only, the viewer has its write level, and the property has a setter OR the attribute says
// canModify" (UOContent/Gumps/Props/PropsGump.cs:141, and the press is refused by the same rule at :210-213). Both
// profile properties on BaseCreature are get-only (BaseCreature.PetTraining.cs), so the row showed the profile's
// ToString ("...") with no arrow. The fix is `canModify: true` on the two [CommandProperty] attributes, which is what
// pinned does for its own [PropertyObject] members (Engines/ConPVP/Arena.cs:26, TournamentController.cs:26). The press
// then reaches the [PropertyObject] branch (PropsGump.cs:316-321) and opens a PropertiesGump on the profile.
//
// What these facts pin:
//  - the AbilityProfile row has the arrow, and pressing it opens a props gump whose object is the creature's profile;
//  - opening it on a stock creature writes nothing: the profile stays empty (natural abilities only), the store's save
//    carries no more entries than before, and a load drops it (cc-P121: an empty profile is not written);
//  - the TrainingProfile row has the arrow too; on an untrained creature the value is null, so the press re-sends the
//    creature's own page (pinned's null branch, PropsGump.cs:320) and no training record is created.
//
// The press is pinned's own code path, driven the way pinned's PropsGumpTextDefinitionTests drives it: a GameMaster
// with a test NetState, the gump built on the row's page, OnResponse with that row's button id. Each fact deletes what
// it made.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Server;
using Server.Gumps;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Xunit;
using Xunit.Abstractions;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class PropsAbilityProfileVerification
{
    private readonly ITestOutputHelper _out;

    public PropsAbilityProfileVerification(ITestOutputHelper output) => _out = output;

    // Mirrors PropertiesGump.MaxEntriesPerPage, which is private (PropsGump.cs:30).
    private const int MaxEntriesPerPage = 15;

    private static readonly FieldInfo ObjectField =
        typeof(PropertiesGump).GetField("m_Object", BindingFlags.Instance | BindingFlags.NonPublic);

    private static (PlayerMobile From, NetState State) NewStaff()
    {
        var ns = PacketTestUtilities.CreateTestNetState();
        var from = new PlayerMobile { Player = true, AccessLevel = AccessLevel.GameMaster, Name = "P233 GM" };
        from.MoveToWorld(new Point3D(1000, 1000, 0), Map.Felucca);
        from.NetState = ns;
        ns.Mobile = from;
        return (from, ns);
    }

    private static void Cleanup(PlayerMobile from, NetState ns, params Mobile[] others)
    {
        from.NetState = null;
        from.Delete();
        ns.Dispose();

        foreach (var m in others)
        {
            m?.Delete();
        }
    }

    // The row's index in the creature's full property list, and the page and button id the gump gives it
    // (PropsGump.cs:126-150: button id is the row's position on its page plus 3).
    private static (List<object> List, int Page, int ButtonId) FindRow(Mobile from, object o, string propName)
    {
        var probe = new RowProbeGump(from, o);
        var list = probe.List;
        var index = -1;

        for (var i = 0; i < list.Count; i++)
        {
            if (list[i] is PropertyInfo p && p.Name == propName)
            {
                index = i;
                break;
            }
        }

        Assert.True(index >= 0, $"{o.GetType().Name} has no visible '{propName}' row in [props");

        var page = index / MaxEntriesPerPage;
        return (list, page, index - page * MaxEntriesPerPage + 3);
    }

    private static bool RowHasArrow(Mobile from, object o, string propName)
    {
        var (list, page, buttonId) = FindRow(from, o, propName);
        var gump = new RowProbeGump(from, o, list, page);

        return gump.Entries.OfType<GumpButton>().Any(b => b.ButtonID == buttonId);
    }

    private static void PressRow(Mobile from, NetState ns, object o, string propName)
    {
        var (list, page, buttonId) = FindRow(from, o, propName);
        var gump = new RowProbeGump(from, o, list, page);

        gump.OnResponse(ns, new RelayInfo(buttonId, ReadOnlySpan<int>.Empty, ReadOnlySpan<ushort>.Empty,
            ReadOnlySpan<Range>.Empty, ReadOnlySpan<byte>.Empty));
    }

    private static object OpenedObject(NetState ns)
    {
        var opened = ns.FindGump<PropertiesGump>();
        return opened == null ? null : ObjectField.GetValue(opened);
    }

    // The store's save, and how many profiles it holds (AbilityProfileStore.Serialize: version, then the live count).
    private static int SavedProfileCount()
    {
        AbilityProfileStore.Configure();
        var buffer = new byte[1 << 20];
        var writer = new BufferWriter(buffer, true);
        AbilityProfileStore.Instance.Serialize(writer);
        writer.Flush();

        IGenericReader reader = new BufferReader(buffer);
        reader.ReadEncodedInt(); // version
        return reader.ReadEncodedInt();
    }

    private static byte[] SaveStore()
    {
        var buffer = new byte[1 << 20];
        var writer = new BufferWriter(buffer, true);
        AbilityProfileStore.Instance.Serialize(writer);
        writer.Flush();
        return buffer;
    }

    // ---------------------------------------------------------------------------------------------------------------
    // AbilityProfile
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheAbilityProfileRowCanBeOpened()
    {
        // The rule the gump applies (PropsGump.cs:141), read from the attribute it reads.
        var prop = typeof(BaseCreature).GetProperty(nameof(BaseCreature.AbilityProfile));
        Assert.NotNull(prop);
        var cpa = prop.GetCustomAttribute<CommandPropertyAttribute>();
        Assert.NotNull(cpa);
        Assert.False(cpa.ReadOnly);
        Assert.True(prop.CanWrite || cpa.CanModify, "AbilityProfile has no setter and its attribute does not say canModify, so [props draws no arrow");
        Assert.True(typeof(AbilityProfile).IsDefined(typeof(PropertyObjectAttribute), true));

        var (from, ns) = NewStaff();
        var dragon = new Dragon();

        try
        {
            dragon.MoveToWorld(new Point3D(1002, 1000, 0), Map.Felucca);

            Assert.True(RowHasArrow(from, dragon, nameof(BaseCreature.AbilityProfile)), "the AbilityProfile row has no arrow");
        }
        finally
        {
            Cleanup(from, ns, dragon);
        }
    }

    [Fact]
    public void PressingTheArrowOpensTheCreaturesProfile()
    {
        var (from, ns) = NewStaff();
        var dragon = new Dragon();

        try
        {
            dragon.MoveToWorld(new Point3D(1002, 1000, 0), Map.Felucca);

            PressRow(from, ns, dragon, nameof(BaseCreature.AbilityProfile));

            var opened = OpenedObject(ns);
            Assert.NotNull(opened);
            var profile = Assert.IsType<AbilityProfile>(opened);
            Assert.Same(dragon, profile.Creature);
            Assert.Same(PetTrainingHelper.GetAbilityProfile(dragon), profile);

            // What staff came to see: the stock dragon's natural abilities, with nothing stored.
            Assert.Contains("DragonBreath", profile.AbilityNames);
            _out.WriteLine($"opened: {profile.AbilityNames}");
        }
        finally
        {
            Cleanup(from, ns, dragon);
        }
    }

    [Fact]
    public void OpeningAStockCreaturesProfileWritesNothing()
    {
        var (from, ns) = NewStaff();
        var dragon = new Dragon();

        try
        {
            dragon.MoveToWorld(new Point3D(1002, 1000, 0), Map.Felucca);
            Assert.False(AbilityProfileStore.TryGet(dragon, out _), "a fresh stock dragon already has a stored profile");

            var savedBefore = SavedProfileCount();

            PressRow(from, ns, dragon, nameof(BaseCreature.AbilityProfile));
            Assert.NotNull(OpenedObject(ns));

            // The open path may make the in-memory entry [props shows (cc-P121), but it stays empty and is never saved.
            Assert.True(AbilityProfileStore.TryGet(dragon, out var profile));
            Assert.True(profile.IsEmpty, "opening the profile stored something on a stock creature");
            Assert.Equal(savedBefore, SavedProfileCount());

            // And a save-and-load drops it: a stock creature's profile does not exist on disk.
            var buffer = SaveStore();
            AbilityProfileStore.Instance.Deserialize(new BufferReader(buffer));
            Assert.False(AbilityProfileStore.TryGet(dragon, out _), "the empty profile came back from a load");
        }
        finally
        {
            Cleanup(from, ns, dragon);
        }
    }

    // ---------------------------------------------------------------------------------------------------------------
    // TrainingProfile
    // ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public void TheTrainingProfileRowCanBeOpenedAndMakesNoRecord()
    {
        var prop = typeof(BaseCreature).GetProperty(nameof(BaseCreature.TrainingProfile));
        Assert.NotNull(prop);
        var cpa = prop.GetCustomAttribute<CommandPropertyAttribute>();
        Assert.NotNull(cpa);
        Assert.True(prop.CanWrite || cpa.CanModify, "TrainingProfile has no setter and its attribute does not say canModify, so [props draws no arrow");
        Assert.True(typeof(TrainingProfile).IsDefined(typeof(PropertyObjectAttribute), true));

        var (from, ns) = NewStaff();
        var dragon = new Dragon();

        try
        {
            dragon.MoveToWorld(new Point3D(1002, 1000, 0), Map.Felucca);
            var recordsBefore = PetTrainingStore.Count;

            Assert.True(RowHasArrow(from, dragon, nameof(BaseCreature.TrainingProfile)), "the TrainingProfile row has no arrow");

            // Untrained: the value is null, so pinned re-sends the creature's own page and nothing is created.
            PressRow(from, ns, dragon, nameof(BaseCreature.TrainingProfile));
            Assert.Same(dragon, OpenedObject(ns));
            Assert.Null(PetTrainingStore.GetTrainingProfile(dragon, false));
            Assert.Equal(recordsBefore, PetTrainingStore.Count);
        }
        finally
        {
            Cleanup(from, ns, dragon);
        }
    }

    // Exposes the protected list so a fact can aim at one property's row, as pinned's PropsGumpTextDefinitionTests does.
    private sealed class RowProbeGump : PropertiesGump
    {
        public RowProbeGump(Mobile m, object o) : base(m, o)
        {
        }

        public RowProbeGump(Mobile m, object o, List<object> list, int page) : base(m, o, null, list, page)
        {
        }

        public List<object> List => m_List;
    }
}
