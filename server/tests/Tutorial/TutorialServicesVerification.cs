// cc-P610 (P533 U4): the elixir sale and the tutorial vet take money only for a service they actually give, in person.
// Notes: shard-migration notes/cc-P610-fix-drafts-from-p533.md.
//
// Facts:
//   1. The Rangers' guildmaster sells an elixir of rebirth only to a living member standing by a guildmaster, on the
//      guildmaster's map, checked when the button is pressed: a menu kept open while walking away, dying, or crossing
//      to another facet, or opened from the Guild Directory, sells nothing and takes nothing (P502 #1).
//   2. A full backpack takes no gold and leaves no elixir on the ground (P502 #2).
//   3. The prices are 375 gold for a member and 500 at an animal trainer, stated here, not read from the code (P502 #4).
//   4. The vet's offer, answered after the owner walked off with the pet, died, or changed facet, does nothing: no fee,
//      no skill loss. A paying owner who is not Young pays exactly ServUO's fee once; a Young owner pays nothing; an
//      owner without the gold gets nothing done (P514's P475/P480 findings, P502 #4).

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Server;
using Server.Accounting;
using Server.Gumps;
using Server.Items;
using Server.LootBoxes;
using Server.Mobiles;
using Server.Network;
using Server.Tests.Network;
using Server.Tutorial;
using Xunit;

namespace ShatteredLegacy.Tests;

[Collection("Sequential UOContent Tests")]
public class TutorialServicesVerification
{
    public TutorialServicesVerification()
    {
        ShardTestClock.Arm();
        ShardTestHost.EnsureAccounts();
    }

    private static readonly Point3D Spot = new(1730, 1730, 0);

    private sealed class Player : IDisposable
    {
        public readonly Account Account = new($"p610{Guid.NewGuid():N}"[..16], "p610-test-only");
        public readonly PlayerMobile Pm;
        public readonly NetState Ns;
        public readonly List<IEntity> Litter = new();

        public Player()
        {
            Pm = new PlayerMobile { Player = true, Race = Race.Human, Body = 0x190, Name = "Buyer", RawStr = 60, RawDex = 60, RawInt = 60 };
            Pm.Hits = Pm.HitsMax;
            Pm.AddItem(new Backpack());
            Account[0] = Pm;
            Pm.MoveToWorld(Spot, Map.Trammel);
            Ns = PacketTestUtilities.CreateTestNetState();
            Ns.Account = Account;
            Pm.NetState = Ns;
            Ns.Mobile = Pm;
        }

        public T Add<T>(T e) where T : IEntity
        {
            Litter.Add(e);
            return e;
        }

        public void Dispose()
        {
            ClusterFFanfare.FlushAll();
            foreach (var e in Litter)
            {
                e.Delete();
            }

            Pm.NetState = null;
            Ns.Mobile = null;
            Ns.Dispose();
            Pm.Delete();
            Accounts.Remove(Account);
        }
    }

    private static RelayInfo Press(int button) => new(button, default, default, default, default);

    private static int Bank(PlayerMobile pm) => Banker.GetBalance(pm);

    private static int Elixirs(PlayerMobile pm)
    {
        var n = 0;
        foreach (var _ in pm.Backpack!.FindItemsByType<ElixirOfRebirth>())
        {
            n++;
        }

        return n;
    }

    private static int ElixirsOnTheGround() => World.Items.Values.OfType<ElixirOfRebirth>().Count(e => !e.Deleted && e.Parent == null);

    private static void Member(Player p) =>
        ClusterFAccountPersistence.GetOrCreate(p.Account).GetOrCreateGuildData(p.Pm.Serial).JoinedGuilds.Add("rangers");

    private static OutridersGuildmaster Guildmaster(Player p)
    {
        var gm = p.Add(new OutridersGuildmaster());
        gm.MoveToWorld(new Point3D(Spot.X + 1, Spot.Y, Spot.Z), Map.Trammel);
        return gm;
    }

    // ---------------------------------------------------------------- 1, 2, 3

    [Fact]
    public void AMemberBuysAnElixirForExactly375GoldFromTheBankAndATrainerAsks500()
    {
        using var p = new Player();
        Member(p);
        Guildmaster(p);
        Banker.Deposit(p.Pm, 1000);

        new OutridersGuildmasterGump(p.Pm, OutridersGuildmasterGump.View.MemberDashboard).OnResponse(p.Ns, Press(34));
        Assert.Equal(1, Elixirs(p.Pm));
        Assert.Equal(625, Bank(p.Pm));

        var trainer = p.Add(new AnimalTrainer());
        var info = trainer.GetBuyInfo().OfType<GenericBuyInfo>().Single(i => i.Type == typeof(ElixirOfRebirth));
        Assert.Equal(500, info.Price);
    }

    [Theory]
    [InlineData("walked away")]
    [InlineData("died")]
    [InlineData("other facet")]
    [InlineData("no guildmaster")]
    [InlineData("not a member")]
    [InlineData("too little gold")]
    public void AnElixirIsNeverSoldWithoutTheGuildmasterOrToAnyoneWhoCannotBuy(string how)
    {
        using var p = new Player();
        if (how != "not a member")
        {
            Member(p);
        }

        if (how != "no guildmaster")
        {
            Guildmaster(p);
        }

        Banker.Deposit(p.Pm, how == "too little gold" ? 100 : 1000);
        var bank = Bank(p.Pm);
        var ground = ElixirsOnTheGround();
        var menu = new OutridersGuildmasterGump(p.Pm, OutridersGuildmasterGump.View.MemberDashboard); // opened by the guildmaster

        switch (how)
        {
            case "walked away":
                p.Pm.MoveToWorld(new Point3D(Spot.X + 20, Spot.Y, Spot.Z), Map.Trammel);
                break;
            case "died":
                p.Pm.Kill();
                break;
            case "other facet":
                p.Pm.MoveToWorld(Spot, Map.Felucca);
                break;
        }

        menu.OnResponse(p.Ns, Press(34));
        Assert.Equal(0, Elixirs(p.Pm));
        Assert.Equal(bank, Bank(p.Pm));
        Assert.Equal(ground, ElixirsOnTheGround());
    }

    [Fact]
    public void AFullBackpackTakesNoGoldAndDropsNoElixir()
    {
        using var p = new Player();
        Member(p);
        Guildmaster(p);
        Banker.Deposit(p.Pm, 1000);
        var pack = p.Pm.Backpack!;
        while (pack.TotalItems < pack.MaxItems)
        {
            pack.DropItem(new Item(0x1F14) { Stackable = false });
        }

        var ground = ElixirsOnTheGround();
        new OutridersGuildmasterGump(p.Pm, OutridersGuildmasterGump.View.MemberDashboard).OnResponse(p.Ns, Press(34));
        Assert.Equal(1000, Bank(p.Pm));
        Assert.Equal(0, Elixirs(p.Pm));
        Assert.Equal(ground, ElixirsOnTheGround());
    }

    // ---------------------------------------------------------------- 4

    private static (TutorialVeterinarian Vet, Horse Pet) DeadPetAtTheVet(Player p, bool young, int bankGold)
    {
        p.Pm.Young = young;
        if (bankGold > 0)
        {
            Banker.Deposit(p.Pm, bankGold);
        }

        var vet = p.Add(new TutorialVeterinarian());
        vet.MoveToWorld(new Point3D(Spot.X + 1, Spot.Y, Spot.Z), Map.Trammel);
        var pet = p.Add(new Horse());
        pet.MoveToWorld(new Point3D(Spot.X + 2, Spot.Y, Spot.Z), Map.Trammel);
        pet.SetControlMaster(p.Pm);
        pet.IsBonded = true;
        pet.Kill();
        pet.Corpse?.Delete(); // its body has faded
        Assert.True(pet.IsDeadBondedPet);
        Assert.True(vet.Offer(p.Pm));
        return (vet, pet);
    }

    private static double[] SkillBases(BaseCreature bc)
    {
        var bases = new double[bc.Skills.Length];
        for (var i = 0; i < bases.Length; i++)
        {
            bases[i] = bc.Skills[i].Base;
        }

        return bases;
    }

    [Theory]
    [InlineData("walked away with the pet")]
    [InlineData("died")]
    [InlineData("other facet")]
    [InlineData("vet gone")]
    public void TheVetsOfferAnsweredFromElsewhereDoesNothingAndTakesNothing(string how)
    {
        using var p = new Player();
        var (vet, pet) = DeadPetAtTheVet(p, young: false, bankGold: 50000);
        var offer = p.Pm.FindGump<TutorialVetResurrectGump>()!;
        var bank = Bank(p.Pm);
        var skills = SkillBases(pet);

        switch (how)
        {
            case "walked away with the pet":
                p.Pm.MoveToWorld(new Point3D(Spot.X + 40, Spot.Y, Spot.Z), Map.Trammel);
                pet.MoveToWorld(new Point3D(Spot.X + 41, Spot.Y, Spot.Z), Map.Trammel);
                break;
            case "died":
                p.Pm.Kill();
                break;
            case "other facet":
                p.Pm.MoveToWorld(Spot, Map.Felucca);
                pet.MoveToWorld(new Point3D(Spot.X + 1, Spot.Y, Spot.Z), Map.Felucca);
                break;
            case "vet gone":
                vet.Delete();
                break;
        }

        offer.OnResponse(p.Ns, Press(1));
        Assert.True(pet.IsDeadBondedPet);
        Assert.Equal(bank, Bank(p.Pm));
        Assert.Equal(skills, SkillBases(pet));
    }

    [Fact]
    public void AnOwnerWhoIsNotYoungPaysServUOsFeeExactlyOnce()
    {
        using var p = new Player();
        var (_, pet) = DeadPetAtTheVet(p, young: false, bankGold: 50000);
        var offer = p.Pm.FindGump<TutorialVetResurrectGump>()!;
        // ServUO Veterinarian.GetResurrectionFee: 100 + 1.1041 ^ MinTameSkill, from 100 to 30,000 (the horse: 29.1).
        var fee = Math.Clamp((int)(100 + Math.Pow(1.1041, 29.1)), 100, 30000);
        Assert.Equal(29.1, pet.MinTameSkill, 1);

        offer.OnResponse(p.Ns, Press(1));
        offer.OnResponse(p.Ns, Press(1)); // the same answer again
        Assert.False(pet.IsDeadBondedPet);
        Assert.Equal(50000 - fee, Bank(p.Pm));
    }

    [Fact]
    public void AYoungOwnerPaysNothingAndAnOwnerWithoutTheGoldGetsNothingDone()
    {
        using (var p = new Player())
        {
            var (_, pet) = DeadPetAtTheVet(p, young: true, bankGold: 0);
            p.Pm.FindGump<TutorialVetResurrectGump>()!.OnResponse(p.Ns, Press(1));
            Assert.False(pet.IsDeadBondedPet);
            Assert.Equal(0, Bank(p.Pm));
        }

        using (var p = new Player())
        {
            var (_, pet) = DeadPetAtTheVet(p, young: false, bankGold: 0);
            var skills = SkillBases(pet);
            p.Pm.FindGump<TutorialVetResurrectGump>()!.OnResponse(p.Ns, Press(1));
            Assert.True(pet.IsDeadBondedPet);
            Assert.Equal(skills, SkillBases(pet));
        }
    }
}
