// cc-P130: ServUO Scripts/Services/Pet Training/Gumps.cs -> ours, the seven training gumps rewritten for pinned's gump
// API (DynamicGump: BuildLayout(ref DynamicGumpBuilder), OnResponse(NetState, in RelayInfo)), the layouts entry for entry:
//   NewAnimalLoreGump (:12-447)        the lore gump with the training section, pages 1-8 and the advancement pages
//   PetTrainingProgressGump (:449-513) the real-time progress tracker for every pet in training
//   PetTrainingConfirmGump (:515-585)  yes / cancel with callbacks
//   PetTrainingOptionsGump (:587-1019) the ANIMAL TRAINING MENU, categories and selections
//   PetTrainingConfirmationGump (:1021-1646) TRAINING CONFIRMATION, the value slider, Train Pet / Add To Plan
//   PetTrainingPlanningGump (:1648-1772) the plan, ENABLE / DISABLE
//   PetTrainingInfoGump (:1774-1817)   the four pages of 1157558/1157563/1157552/1157553
// ServUO's BaseGump conveniences used here (User, Refresh, Color, Center, C32216, CenterLoc) are PetTrainingGump's
// members below. Dropped on the way: AddECHandleInput (an Enhanced Client workaround pinned's builder has not), the
// ML quest hooks on begin training and on a successful train (UsingAnimalLoreQuest, TeachingSomethingNewQuest: pinned
// has no ML quest system), and ServUO's GetTypeID override on the progress gump (pinned derives the id from the type).
// Where ServUO closes a gump by type before resending, every gump here is a Singleton, which pinned's SendGump honours
// (NetStateGumps.Send replaces the open one of the same type).

using System;
using System.Collections.Generic;
using System.Linq;
using Server.Gumps;
using Server.Items;
using Server.Network;
using Server.Spells;

namespace Server.Mobiles;

public abstract class PetTrainingGump : DynamicGump
{
    public const int CenterLoc = 1154645; // <center>~1_val~</center>

    public PlayerMobile User { get; }

    protected PetTrainingGump(PlayerMobile user, int x, int y) : base(x, y) => User = user;

    public override bool Singleton => true;

    /// <summary>ServUO BaseGump.Refresh: rebuild and resend in place.</summary>
    public void Refresh()
    {
        if (User?.NetState != null)
        {
            User.SendGump(this, true);
        }
    }

    public static int C32216(int c32)
    {
        c32 &= 0xFFFFFF;

        var r = ((c32 >> 16) & 0xFF) >> 3;
        var g = ((c32 >> 08) & 0xFF) >> 3;
        var b = ((c32 >> 00) & 0xFF) >> 3;

        return (r << 10) | (g << 5) | (b << 0);
    }

    protected static string Color(string color, string str) => $"<basefont color={color}>{str}";

    protected static string ColorAndCenter(string color, string str) => $"<center><basefont color={color}>{str}</center>";

    protected static string Center(string str) => $"<CENTER>{str}</CENTER>";

    protected static void AddText(ref DynamicGumpBuilder builder, int x, int y, int w, int h, TextDefinition text, int color = 0)
    {
        if (text == null)
        {
            return;
        }

        if (text.Number > 0)
        {
            if (color != 0)
            {
                builder.AddHtmlLocalized(x, y, w, h, text.Number, color);
            }
            else
            {
                builder.AddHtmlLocalized(x, y, w, h, text.Number);
            }
        }
        else if (text.String != null)
        {
            builder.AddHtml(x, y, w, h, text.String);
        }
    }
}

public class NewAnimalLoreGump : PetTrainingGump
{
    private const int Label = 0xF424E5;

    public BaseCreature Creature { get; }

    public NewAnimalLoreGump(PlayerMobile pm, BaseCreature bc) : base(pm, 250, 50) => Creature = bc;

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        var profile = PetTrainingHelper.GetAbilityProfile(Creature);
        var trainProfile = PetTrainingHelper.GetTrainingProfile(Creature, true);

        builder.AddPage(0);
        builder.AddBackground(0, 24, 310, 325, 0x24A4);
        builder.AddHtml(47, 32, 210, 18, ColorAndCenter("#000080", Creature.Name));

        builder.AddButton(140, 0, 0x82D, 0x82D, 0);

        builder.AddImage(40, 62, 0x82B);
        builder.AddImage(40, 258, 0x82B);

        if (Creature.Controlled && Creature.ControlMaster == User)
        {
            builder.AddImage(28, 272, 0x826);

            var def = PetTrainingHelper.GetTrainingDefinition(Creature);

            if (trainProfile is { HasBegunTraining: true } && def != null && def.Class != Class.Untrainable)
            {
                builder.AddImage(53, 290, 0x805);

                builder.AddHtmlLocalized(47, 270, 160, 18, 1157491, 0xC8); // Pet Training Progress:

                var progress = trainProfile.TrainingProgressPercentile * 100;

                if (progress >= 1)
                {
                    builder.AddBackground(53, 290, (int)(109.0 * (progress / 100)), 11, 0x806);
                }

                builder.AddHtml(162, 285, 50, 18, FormatDouble(progress, false, true));
                builder.AddButton(215, 288, 0x15E1, 0x15E5, 1);
                builder.AddTooltip(1157568); // View real-time training progress.

                if (trainProfile.CanApplyOptions)
                {
                    builder.AddButton(53, 305, 0x837, 0x838, 2);
                    builder.AddHtmlLocalized(73, 300, 160, 18, 1157492); // Pet Training Options
                }

                builder.AddButton(250, 280, 0x9AA, 0x9A9, 3);
                builder.AddTooltip(1158013); // Cancel Training Process. All remaining points will be removed.
            }
            else if (Creature.ControlSlots < Creature.ControlSlotsMax)
            {
                builder.AddHtmlLocalized(47, 270, 160, 18, 1157487, 0xC8); // Begin Animal Training
                builder.AddButton(53, 288, 0x837, 0x838, 4);
            }
        }

        builder.AddPage(1);

        builder.AddImage(28, 76, 0x826);
        builder.AddHtmlLocalized(47, 74, 160, 18, 1049593, 0xC8); // Attributes

        builder.AddHtmlLocalized(53, 92, 160, 18, 1049578, Label); // Hits
        builder.AddHtml(180, 92, 75, 18, FormatAttributes(Creature.Hits, Creature.HitsMax));

        builder.AddHtmlLocalized(53, 110, 160, 18, 1049579, Label); // Stamina
        builder.AddHtml(180, 110, 75, 18, FormatAttributes(Creature.Stam, Creature.StamMax));

        builder.AddHtmlLocalized(53, 128, 160, 18, 1049580, Label); // Mana
        builder.AddHtml(180, 128, 75, 18, FormatAttributes(Creature.Mana, Creature.ManaMax));

        builder.AddHtmlLocalized(53, 146, 160, 18, 1028335, Label); // Strength
        builder.AddHtml(180, 146, 75, 18, FormatStat(Creature.Str));

        builder.AddHtmlLocalized(53, 164, 160, 18, 3000113, Label); // Dexterity
        builder.AddHtml(180, 164, 75, 18, FormatStat(Creature.Dex));

        builder.AddHtmlLocalized(53, 182, 160, 18, 3000112, Label); // Intelligence
        builder.AddHtml(180, 182, 75, 18, FormatStat(Creature.Int));

        var bd = BaseInstrument.GetBaseDifficulty(Creature);

        if (Creature.Uncalmable)
        {
            bd = 0;
        }

        builder.AddHtmlLocalized(53, 200, 160, 18, 1070793, Label); // Barding Difficulty
        builder.AddHtml(180, 200, 75, 18, FormatDouble(bd));

        builder.AddImage(28, 220, 0x826);

        builder.AddHtmlLocalized(47, 220, 160, 18, 1049594, 0xC8); // Loyalty Rating
        builder.AddHtmlLocalized(
            53, 236, 160, 18, !Creature.Controlled || Creature.Loyalty == 0 ? 1061643 : 1049595 + Creature.Loyalty / 10, Label
        );

        builder.AddButton(240, 328, 0x15E1, 0x15E5, 0, GumpButtonType.Page, 2);
        builder.AddButton(217, 328, 0x15E3, 0x15E7, 0, GumpButtonType.Page, Pages(profile));

        builder.AddPage(2);

        builder.AddImage(28, 76, 0x826);
        builder.AddHtmlLocalized(47, 74, 160, 18, 1049593, 0xC8); // Attributes

        // ServUO: RegenRates.HitPointRegen(Creature) and its stam and mana twins, the item bonus plus the profile's
        // trained regeneration (our RegenRates.cs adds the latter the same way).
        builder.AddHtmlLocalized(53, 92, 160, 18, 1075627, Label); // Hit Point Regeneration
        builder.AddHtml(180, 92, 75, 18, FormatStat(AosAttributes.GetValue(Creature, AosAttribute.RegenHits) + PetTrainingHooks.RegenHits(Creature)));

        builder.AddHtmlLocalized(53, 110, 160, 18, 1079411, Label); // Stamina Regeneration
        builder.AddHtml(180, 110, 75, 18, FormatStat(AosAttributes.GetValue(Creature, AosAttribute.RegenStam) + PetTrainingHooks.RegenStam(Creature)));

        builder.AddHtmlLocalized(53, 128, 160, 18, 1079410, Label); // Mana Regeneration
        builder.AddHtml(180, 128, 75, 18, FormatStat(AosAttributes.GetValue(Creature, AosAttribute.RegenMana) + PetTrainingHooks.RegenMana(Creature)));

        builder.AddButton(240, 328, 0x15E1, 0x15E5, 0, GumpButtonType.Page, 3);
        builder.AddButton(217, 328, 0x15E3, 0x15E7, 0, GumpButtonType.Page, 1);

        builder.AddPage(3);

        builder.AddImage(28, 76, 0x826);

        builder.AddHtmlLocalized(47, 74, 160, 18, 1061645, 0xC8); // Resistances

        builder.AddHtmlLocalized(53, 92, 160, 18, 1061646, Label); // Physical
        builder.AddHtml(180, 92, 75, 18, FormatElement(Creature.PhysicalResistance, null));

        builder.AddHtmlLocalized(53, 110, 160, 18, 1061647, Label); // Fire
        builder.AddHtml(180, 110, 75, 18, FormatElement(Creature.FireResistance, "#FF0000"));

        builder.AddHtmlLocalized(53, 128, 160, 18, 1061648, Label); // Cold
        builder.AddHtml(180, 128, 75, 18, FormatElement(Creature.ColdResistance, "#000080"));

        builder.AddHtmlLocalized(53, 146, 160, 18, 1061649, Label); // Poison
        builder.AddHtml(180, 146, 75, 18, FormatElement(Creature.PoisonResistance, "#008000"));

        builder.AddHtmlLocalized(53, 164, 160, 18, 1061650, Label); // Energy
        builder.AddHtml(180, 164, 75, 18, FormatElement(Creature.EnergyResistance, "#BF80FF"));

        builder.AddButton(240, 328, 0x15E1, 0x15E5, 0, GumpButtonType.Page, 4);
        builder.AddButton(217, 328, 0x15E3, 0x15E7, 0, GumpButtonType.Page, 2);

        builder.AddPage(4);

        builder.AddImage(28, 76, 0x826);

        builder.AddHtmlLocalized(47, 74, 160, 18, 1017319, 0xC8); // Damage

        builder.AddHtmlLocalized(53, 92, 160, 18, 1061646, Label); // Physical
        builder.AddHtml(180, 92, 75, 18, FormatElement(Creature.PhysicalDamage, null));

        builder.AddHtmlLocalized(53, 110, 160, 18, 1061647, Label); // Fire
        builder.AddHtml(180, 110, 75, 18, FormatElement(Creature.FireDamage, "#FF0000"));

        builder.AddHtmlLocalized(53, 128, 160, 18, 1061648, Label); // Cold
        builder.AddHtml(180, 128, 75, 18, FormatElement(Creature.ColdDamage, "#000080"));

        builder.AddHtmlLocalized(53, 146, 160, 18, 1061649, Label); // Poison
        builder.AddHtml(180, 146, 75, 18, FormatElement(Creature.PoisonDamage, "#008000"));

        builder.AddHtmlLocalized(53, 164, 160, 18, 1061650, Label); // Energy
        builder.AddHtml(180, 164, 75, 18, FormatElement(Creature.EnergyDamage, "#BF80FF"));

        builder.AddHtmlLocalized(53, 182, 160, 18, 1076750, Label); // Base Damage
        builder.AddHtml(180, 182, 75, 18, FormatDamage(Creature.DamageMin, Creature.DamageMax));

        builder.AddButton(240, 328, 0x15E1, 0x15E5, 0, GumpButtonType.Page, 5);
        builder.AddButton(217, 328, 0x15E3, 0x15E7, 0, GumpButtonType.Page, 3);

        builder.AddPage(5);

        builder.AddImage(28, 76, 0x826);

        builder.AddHtmlLocalized(47, 74, 160, 18, 3001030, 0xC8); // Combat Ratings

        AddSkill(ref builder, 92, 1044103, SkillName.Wrestling);
        AddSkill(ref builder, 110, 1044087, SkillName.Tactics);
        AddSkill(ref builder, 128, 1044086, SkillName.MagicResist);
        AddSkill(ref builder, 146, 1044061, SkillName.Anatomy);
        AddSkill(ref builder, 164, 1002082, SkillName.Healing);
        AddSkill(ref builder, 182, 1002122, SkillName.Poisoning);
        AddSkill(ref builder, 200, 1044074, SkillName.DetectHidden);
        AddSkill(ref builder, 218, 1002088, SkillName.Hiding);
        AddSkill(ref builder, 236, 1002118, SkillName.Parry);

        builder.AddButton(240, 328, 0x15E1, 0x15E5, 0, GumpButtonType.Page, 6);
        builder.AddButton(217, 328, 0x15E3, 0x15E7, 0, GumpButtonType.Page, 4);

        builder.AddPage(6);

        builder.AddImage(28, 76, 0x826);

        builder.AddHtmlLocalized(47, 74, 160, 18, 3001032, 0xC8); // Lore & Knowledge

        AddSkill(ref builder, 92, 1044085, SkillName.Magery);
        AddSkill(ref builder, 110, 1044076, SkillName.EvalInt);
        AddSkill(ref builder, 128, 1044106, SkillName.Meditation);
        AddSkill(ref builder, 146, 1044109, SkillName.Necromancy);
        AddSkill(ref builder, 164, 1002140, SkillName.SpiritSpeak);
        AddSkill(ref builder, 182, 1044115, SkillName.Mysticism);
        AddSkill(ref builder, 200, 1044110, SkillName.Focus);
        AddSkill(ref builder, 218, 1044114, SkillName.Spellweaving);
        AddSkill(ref builder, 236, 1044075, SkillName.Discordance);

        builder.AddButton(240, 328, 0x15E1, 0x15E5, 0, GumpButtonType.Page, 7);
        builder.AddButton(217, 328, 0x15E3, 0x15E7, 0, GumpButtonType.Page, 5);

        builder.AddPage(7);

        builder.AddImage(28, 76, 0x826);

        builder.AddHtmlLocalized(47, 74, 160, 18, 3001032, 0xC8); // Lore & Knowledge

        AddSkill(ref builder, 92, 1044112, SkillName.Bushido);
        AddSkill(ref builder, 110, 1044113, SkillName.Ninjitsu);
        AddSkill(ref builder, 128, 1044111, SkillName.Chivalry);

        builder.AddImage(28, 146, 0x826);

        builder.AddHtmlLocalized(47, 144, 160, 18, 1049563, 0xC8); // Preferred Foods

        var foodPref = 3000340;

        if ((Creature.FavoriteFood & FoodType.FruitsAndVeggies) != 0)
        {
            foodPref = 1049565; // Fruits and Vegetables
        }
        else if ((Creature.FavoriteFood & FoodType.GrainsAndHay) != 0)
        {
            foodPref = 1049566; // Grains and Hay
        }
        else if ((Creature.FavoriteFood & FoodType.Fish) != 0)
        {
            foodPref = 1049568; // Fish
        }
        else if ((Creature.FavoriteFood & FoodType.Meat) != 0)
        {
            foodPref = 1049564; // Meat
        }
        else if ((Creature.FavoriteFood & FoodType.Eggs) != 0)
        {
            foodPref = 1044477; // Eggs
        }

        builder.AddHtmlLocalized(53, 164, 160, 18, foodPref, Label);

        builder.AddImage(28, 182, 0x826);

        builder.AddHtmlLocalized(47, 182, 160, 18, 1049569, 0xC8); // Pack Instincts

        var packInstinct = 3000340;

        if ((Creature.PackInstinct & PackInstinct.Canine) != 0)
        {
            packInstinct = 1049570; // Canine
        }
        else if ((Creature.PackInstinct & PackInstinct.Ostard) != 0)
        {
            packInstinct = 1049571; // Ostard
        }
        else if ((Creature.PackInstinct & PackInstinct.Feline) != 0)
        {
            packInstinct = 1049572; // Feline
        }
        else if ((Creature.PackInstinct & PackInstinct.Arachnid) != 0)
        {
            packInstinct = 1049573; // Arachnid
        }
        else if ((Creature.PackInstinct & PackInstinct.Daemon) != 0)
        {
            packInstinct = 1049574; // Daemon
        }
        else if ((Creature.PackInstinct & PackInstinct.Bear) != 0)
        {
            packInstinct = 1049575; // Bear
        }
        else if ((Creature.PackInstinct & PackInstinct.Equine) != 0)
        {
            packInstinct = 1049576; // Equine
        }
        else if ((Creature.PackInstinct & PackInstinct.Bull) != 0)
        {
            packInstinct = 1049577; // Bull
        }

        builder.AddHtmlLocalized(53, 200, 160, 18, packInstinct, Label);

        builder.AddButton(240, 328, 0x15E1, 0x15E5, 0, GumpButtonType.Page, 8);
        builder.AddButton(217, 328, 0x15E3, 0x15E7, 0, GumpButtonType.Page, 6);

        if (Creature.Tamable)
        {
            builder.AddImage(28, 218, 0x826);

            builder.AddHtmlLocalized(47, 216, 160, 18, 1115783, 0xC8); // Pet Slots
            builder.AddHtml(53, 236, 80, 18, FormatPetSlots(Creature.ControlSlots, Creature.ControlSlotsMax));

            builder.AddHtmlLocalized(158, 236, 115, 18, 1157600, Creature.CurrentTameSkill.ToString("0.0"), Label);
            builder.AddTooltip(1157586);
        }

        builder.AddButton(240, 328, 0x15E1, 0x15E5, 0, GumpButtonType.Page, 8);
        builder.AddButton(217, 328, 0x15E3, 0x15E7, 0, GumpButtonType.Page, 6);

        builder.AddPage(8);

        builder.AddImage(28, 76, 0x826);

        builder.AddHtmlLocalized(47, 74, 160, 18, 3001032, 0xC8); // Lore & Knowledge

        if (profile != null)
        {
            var y = 92;

            foreach (var o in profile.EnumerateAllAbilities())
            {
                var loc = PetTrainingHelper.GetLocalization(o);

                if (loc?[0] == null)
                {
                    continue;
                }

                if (loc[0].Number > 0)
                {
                    builder.AddHtmlLocalized(53, y, 180, 18, loc[0].Number, Label);
                }
                else if (loc[0].String != null)
                {
                    builder.AddHtml(53, y, 180, 18, loc[0].String);
                }

                if (loc[1] != null && loc[1].Number > 0)
                {
                    builder.AddTooltip(loc[1].Number);
                }

                y += 18;
            }

            if (profile.Advancements != null)
            {
                builder.AddButton(240, 328, 0x15E1, 0x15E5, 0, GumpButtonType.Page, 9);
                builder.AddButton(217, 328, 0x15E3, 0x15E7, 0, GumpButtonType.Page, 7);

                var advpage = AdvPage(profile);

                var profileadvcount = profile.Advancements.Count - 1;

                var idx = 0;

                for (var p = 1; p <= advpage; p++)
                {
                    y = 92;

                    builder.AddPage(8 + p);

                    builder.AddImage(28, 76, 0x826);

                    builder.AddHtmlLocalized(47, 74, 160, 18, 1157505, 0xC8); // Pet Advancements

                    for (var i = profileadvcount; i >= 0; i--)
                    {
                        if (++idx > 9)
                        {
                            profileadvcount -= idx;
                            idx = 0;
                            break;
                        }

                        var adv = profile.Advancements[i];
                        var loc = PetTrainingHelper.GetLocalization(adv);
                        var skill = adv is SkillName; // ? "#228B22" : "#FF4500";

                        if (loc?[0] != null)
                        {
                            if (loc[0].Number > 0)
                            {
                                builder.AddHtmlLocalized(53, y, 180, 18, loc[0].Number, C32216(skill ? 0x008000 : 0xFF4500));

                                if (skill)
                                {
                                    var cap = Creature.Skills[(SkillName)adv].Cap;
                                    var text = $"<div align=right>{cap:F1}</div>";
                                    builder.AddHtml(180, y, 75, 18, text);
                                }
                            }
                            else if (loc[0].String != null)
                            {
                                builder.AddHtml(53, y, 180, 18, Color(skill ? "#008000" : "#FF4500", loc[0].String));
                            }
                        }

                        builder.AddTooltip(PetTrainingHelper.GetCategoryLocalization(adv));

                        y += 18;
                    }

                    builder.AddButton(240, 328, 0x15E1, 0x15E5, 0, GumpButtonType.Page, advpage == p ? 1 : 9 + p);
                    builder.AddButton(217, 328, 0x15E3, 0x15E7, 0, GumpButtonType.Page, 7 + p);
                }
            }
            else
            {
                builder.AddButton(240, 328, 0x15E1, 0x15E5, 0, GumpButtonType.Page, 1);
                builder.AddButton(217, 328, 0x15E3, 0x15E7, 0, GumpButtonType.Page, 7);
            }
        }
        else
        {
            builder.AddButton(240, 328, 0x15E1, 0x15E5, 0, GumpButtonType.Page, 1);
            builder.AddButton(217, 328, 0x15E3, 0x15E7, 0, GumpButtonType.Page, 7);
        }
    }

    private void AddSkill(ref DynamicGumpBuilder builder, int y, int cliloc, SkillName skill)
    {
        builder.AddHtmlLocalized(53, y, 160, 18, cliloc, Label);
        builder.AddHtml(180, y, 100, 18, FormatSkill(Creature, skill));
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var id = info.ButtonID;

        if (Creature == null || Creature.Map == null || Creature.Map == Map.Internal || !Creature.Alive ||
            Creature.Deleted || Creature.IsDeadBondedPet)
        {
            id = 0;
        }

        switch (id)
        {
            case 0:
                User.CloseGump<PetTrainingOptionsGump>();
                User.CloseGump<PetTrainingPlanningGump>();
                User.CloseGump<PetTrainingInfoGump>();
                User.CloseGump<PetTrainingConfirmGump>();
                User.CloseGump<PetTrainingConfirmationGump>();
                break;
            case 1: // training tracker
                User.CloseGump<PetTrainingProgressGump>();

                Timer.DelayCall(TimeSpan.FromSeconds(.5), () => User.SendGump(new PetTrainingProgressGump(User, Creature)));
                break;
            case 2: // pet training options
                {
                    if (User.HasGump<PetTrainingConfirmationGump>() ||
                        User.HasGump<PetTrainingOptionsGump>() ||
                        User.HasGump<PetTrainingPlanningGump>() ||
                        User.HasGump<PetTrainingConfirmGump>())
                    {
                        Refresh();
                        break;
                    }

                    var trainProfile = PetTrainingHelper.GetTrainingProfile(Creature, true);

                    if (trainProfile is { HasBegunTraining: true })
                    {
                        if (!trainProfile.HasRecievedControlSlotWarning)
                        {
                            trainProfile.HasRecievedControlSlotWarning = true;

                            Timer.DelayCall(
                                TimeSpan.FromSeconds(.5),
                                () =>
                                {
                                    User.SendGump(
                                        new PetTrainingConfirmGump(
                                            User,
                                            1157571,
                                            1157572,
                                            () =>
                                            {
                                                Refresh();
                                                User.CloseGump<PetTrainingOptionsGump>();
                                                User.SendGump(new PetTrainingOptionsGump(User, Creature));
                                            }
                                        )
                                    );
                                }
                            );
                        }
                        else
                        {
                            Timer.DelayCall(
                                TimeSpan.FromSeconds(.5),
                                () =>
                                {
                                    Refresh();
                                    User.CloseGump<PetTrainingOptionsGump>();
                                    User.SendGump(new PetTrainingOptionsGump(User, Creature));
                                }
                            );
                        }
                    }

                    break;
                }
            case 3: // cancel
                User.SendGump(
                    new PetTrainingConfirmGump(
                        User,
                        1153093,
                        1158019,
                        () => PetTrainingHelper.GetTrainingProfile(Creature, true)?.EndTraining()
                    )
                );
                break;
            case 4: // begin training
                PetTrainingHelper.GetTrainingProfile(Creature, true)?.BeginTraining();
                Refresh();
                break;
        }
    }

    public static int AdvPage(AbilityProfile profile)
    {
        var advcount = profile.Advancements.Count;

        return advcount % 9 == 0 ? advcount / 9 : advcount / 9 + 1;
    }

    public static int Pages(AbilityProfile profile)
    {
        if (profile?.Advancements == null || profile.Advancements.Count == 0)
        {
            return 8;
        }

        return 8 + AdvPage(profile);
    }

    private static string FormatSkill(BaseCreature c, SkillName name)
    {
        if (c.Skills[name].Base < 10.0)
        {
            return "<div align=right>---</div>";
        }

        return $"<div align=right>{c.Skills[name].Value:F1}/{c.Skills[name].Cap}</div>";
    }

    private static string FormatAttributes(int cur, int max) =>
        max == 0 ? "<div align=right>---</div>" : $"<div align=right>{cur}/{max}</div>";

    private static string FormatStat(int val) => val == 0 ? "<div align=right>---</div>" : $"<div align=right>{val}</div>";

    public static string FormatDouble(double val, bool dontshowzero = true, bool percentage = false)
    {
        if (dontshowzero)
        {
            return val == 0 ? "<div align=right>---</div>" : $"<div align=right>{val:F1}</div>";
        }

        if (percentage)
        {
            return $"<div align=right>{val:F1}%</div>";
        }

        return $"<div align=right>{val:F1}</div>";
    }

    public static string FormatElement(int val, string color)
    {
        if (color == null)
        {
            return val <= 0 ? "<div align=right>---</div>" : $"<div align=right>{val}%</div>";
        }

        return val <= 0
            ? $"<BASEFONT COLOR={color}><div align=right>---</div>"
            : $"<BASEFONT COLOR={color}><div align=right>{val}%</div>";
    }

    public static string FormatDamage(int min, int max) =>
        min <= 0 || max <= 0 ? "<div align=right>---</div>" : $"<div align=right>{min}-{max}</div>";

    public static string FormatPetSlots(int min, int max) => $"<BASEFONT COLOR=#57412F>{min} => {max}";
}

public class PetTrainingProgressGump : PetTrainingGump
{
    public BaseCreature Creature { get; }

    public PetTrainingProgressGump(PlayerMobile pm, BaseCreature bc) : base(pm, 50, 200) => Creature = bc;

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        var pets = new List<BaseCreature>(
            User.AllFollowers.OfType<BaseCreature>().Where(p => p.TrainingProfile is { HasBegunTraining: true } && p.Map == User.Map)
        );

        if (pets.Count == 0)
        {
            builder.AddBackground(0, 24, 254, 170, 0x24A4);
            return;
        }

        if (pets.Contains(Creature) && pets[0] != Creature)
        {
            pets.Remove(Creature);
            pets.Insert(0, Creature);
        }

        var length = 254 + (pets.Count - 1) * 60;

        builder.AddBackground(0, 24, 254, length, 0x24A4);

        builder.AddHtmlLocalized(30, 32, 200, 18, 1114513, "#1157491", 0); // Pet Training Progress:

        builder.AddButton(120, 0, 0x82D, 0x82D, 0);

        for (var i = 0; i < pets.Count; i++)
        {
            var pet = pets[i];

            builder.AddHtml(53, 60 + 40 * i, 210, 18, Color("#000080", pet.Name));

            var trainProfile = PetTrainingHelper.GetTrainingProfile(pet);
            var progress = 0.0;

            builder.AddImage(53, 80 + 40 * i, 0x805);

            if (trainProfile != null)
            {
                progress = trainProfile.TrainingProgressPercentile * 100;

                if (progress >= 1)
                {
                    builder.AddBackground(53, 80 + 40 * i, (int)(109.0 * (progress / 100)), 11, 0x806);
                }
            }

            builder.AddHtml(162, 78 + 40 * i, 50, 18, NewAnimalLoreGump.FormatDouble(progress, false, true));
        }
    }
}

public class PetTrainingConfirmGump : PetTrainingGump
{
    private readonly int _title;
    private readonly int _body;

    private Action ConfirmCallback { get; }
    private Action CancelCallback { get; }

    public PetTrainingConfirmGump(PlayerMobile pm, int title, int body, Action confirmCallback, Action cancelCallback = null)
        : base(pm, 250, 50)
    {
        _title = title;
        _body = body;
        ConfirmCallback = confirmCallback;
        CancelCallback = cancelCallback;
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddBackground(0, 0, 454, 240, 0x24A4);

        builder.AddHtmlLocalized(0, 12, 454, 16, CenterLoc, $"#{_title}", 0xF424E5);
        builder.AddHtmlLocalized(55, 65, 344, 80, _body, C32216(0x8B0000));

        builder.AddButton(70, 150, 0x9CC8, 0x9CC7, 1);
        builder.AddHtml(70, 153, 126, 16, Center("Yes"));

        builder.AddButton(235, 150, 0x9CC8, 0x9CC7, 2);
        builder.AddHtml(235, 153, 126, 16, Center("Cancel"));
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        if (info.ButtonID == 1)
        {
            ConfirmCallback?.Invoke();
            OnConfirm();
        }
        else if (info.ButtonID == 2)
        {
            CancelCallback?.Invoke();
            OnCancel();
        }
    }

    public virtual void OnConfirm()
    {
    }

    public virtual void OnCancel()
    {
    }
}

public class PetTrainingOptionsGump : PetTrainingGump
{
    public BaseCreature Creature { get; }
    public int Category { get; private set; }

    public AbilityProfile AbilityProfile { get; }
    public TrainingProfile TrainingProfile { get; }
    public TrainingDefinition Definition { get; }

    public PetTrainingOptionsGump(PlayerMobile pm, BaseCreature bc) : base(pm, 40, 200)
    {
        Creature = bc;
        Category = 0;

        Definition = PetTrainingHelper.GetTrainingDefinition(Creature);
        AbilityProfile = PetTrainingHelper.GetAbilityProfile(Creature, true);
        TrainingProfile = PetTrainingHelper.GetTrainingProfile(Creature, true);
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddBackground(0, 0, 574, 560, 0x24A4);

        builder.AddHtmlLocalized(0, 11, 574, 18, 1157485); // <CENTER>ANIMAL TRAINING MENU</CENTER>

        builder.AddImageTiled(35, 40, 220, 440, 2624);
        builder.AddImageTiled(37, 42, 216, 436, 3004);

        builder.AddImageTiled(265, 40, 270, 440, 2624);
        builder.AddImageTiled(267, 42, 266, 436, 3004);

        builder.AddHtmlLocalized(35, 63, 220, 20, 1044010, 0); // <CENTER>CATEGORIES</CENTER>
        builder.AddHtmlLocalized(265, 63, 270, 20, 1044011, 0); // <CENTER>SELECTIONS</CENTER>

        builder.AddButton(35, 490, 0x9CC8, 0x9CC7, 0);
        builder.AddHtml(35, 493, 126, 20, Center("CANCEL"));

        builder.AddButton(220, 490, 0x9CC8, 0x9CC7, 1);
        builder.AddHtml(220, 493, 126, 20, Center("PLAN"));

        builder.AddButton(410, 490, 0x9CC8, 0x9CC7, 2);
        builder.AddHtml(410, 493, 126, 20, Center("INFO"));

        if (Definition == null || TrainingProfile == null)
        {
            return;
        }

        var y = 90;

        builder.AddButton(40, y, 4005, 4007, 3);
        builder.AddHtmlLocalized(75, y + 2, 150, 16, 3010049); // Stats
        y += 24;

        builder.AddButton(40, y, 4005, 4007, 4);
        builder.AddHtmlLocalized(75, y + 2, 180, 16, 1114254); // Resists
        y += 24;

        if (HasAvailable(PetTrainingHelper.MagicSkills))
        {
            builder.AddButton(40, y, 4005, 4007, 5);
            builder.AddHtmlLocalized(75, y + 2, 180, 16, 1157495); // Increase Magic Skill Caps
            y += 24;
        }

        if (HasAvailable(PetTrainingHelper.CombatSkills))
        {
            builder.AddButton(40, y, 4005, 4007, 6);
            builder.AddHtmlLocalized(75, y + 2, 180, 16, 1157496); // Increase Combat Skill Caps
            y += 24;
        }

        if (HasAvailable(Definition.MagicalAbilities))
        {
            builder.AddButton(40, y, 4005, 4007, 7);
            builder.AddHtmlLocalized(75, y + 2, 180, 16, 1157481); // Magical Abilities
            y += 24;
        }

        if (HasAvailable(Definition.SpecialAbilities))
        {
            builder.AddButton(40, y, 4005, 4007, 8);
            builder.AddHtmlLocalized(75, y + 2, 180, 16, 1157480); // Special Abilities
            y += 24;
        }

        if (HasAvailable(Definition.WeaponAbilities))
        {
            builder.AddButton(40, y, 4005, 4007, 9);
            builder.AddHtmlLocalized(75, y + 2, 180, 16, 1157479); // Special Moves
            y += 24;
        }

        if (HasAvailable(Definition.AreaEffects))
        {
            builder.AddButton(40, y, 4005, 4007, 10);
            builder.AddHtmlLocalized(75, y + 2, 180, 16, 1157482); // Area Effect Abilities
        }

        switch (Category)
        {
            case 0: BuildStatsPage(ref builder); break;
            case 1: BuildResistsPage(ref builder); break;
            case 2: BuildMagicSkillCapsPage(ref builder); break;
            case 3: BuildCombatSkillCapsPage(ref builder); break;
            case 4: BuildMagicalAbilitiesPage(ref builder); break;
            case 5: BuildSpecialAbilitiesPage(ref builder); break;
            case 6: BuildSpecialMovesPage(ref builder); break;
            case 7: BuildAreaEffectsPage(ref builder); break;
        }
    }

    private void BuildStatsPage(ref DynamicGumpBuilder builder)
    {
        var y = 90;

        foreach (var i in Enum.GetValues<PetStat>())
        {
            var tp = PetTrainingHelper.GetTrainingPoint(i);

            builder.AddButton(275, y, 4005, 4007, 100 + (int)i);
            Record(ref builder, tp, 320, y + 2);

            y += 22;
        }
    }

    private void BuildResistsPage(ref DynamicGumpBuilder builder)
    {
        var y = 90;

        foreach (var i in Enum.GetValues<ResistanceType>())
        {
            var tp = PetTrainingHelper.GetTrainingPoint(i);

            builder.AddButton(275, y, 4005, 4007, 100 + (int)i);
            Record(ref builder, tp, 320, y + 2);

            y += 22;
        }
    }

    private void BuildMagicSkillCapsPage(ref DynamicGumpBuilder builder) =>
        BuildSkillCapsPage(ref builder, PetTrainingHelper.MagicSkills);

    private void BuildCombatSkillCapsPage(ref DynamicGumpBuilder builder) =>
        BuildSkillCapsPage(ref builder, PetTrainingHelper.CombatSkills);

    private void BuildSkillCapsPage(ref DynamicGumpBuilder builder, SkillName[] skills)
    {
        var y = 90;

        foreach (var skill in skills)
        {
            if (!PetTrainingHelper.CommonSkill(Creature, skill) && Creature.Skills[skill].Base <= 0 ||
                Creature.Skills[skill].Cap >= 120)
            {
                continue;
            }

            var tp = PetTrainingHelper.GetTrainingPoint(skill);

            builder.AddButton(275, y, 4005, 4007, 100 + (int)skill);
            Record(ref builder, tp, 320, y + 2);

            y += 22;
        }
    }

    private void BuildMagicalAbilitiesPage(ref DynamicGumpBuilder builder)
    {
        var y = 90;

        for (var i = 0; i < PetTrainingHelper.MagicalAbilities.Length; i++)
        {
            var abil = PetTrainingHelper.MagicalAbilities[i];
            var master = Creature.ControlMaster;

            if (abil == MagicalAbility.Chivalry && master != null && master.Karma < 0 ||
                abil == MagicalAbility.Necromancy && master != null && master.Karma > 0 ||
                abil == MagicalAbility.Necromage && master != null && master.Karma > 0)
            {
                continue;
            }

            if ((Definition.MagicalAbilities & abil) == 0 || AbilityProfile.HasAbility(abil) ||
                !AbilityProfile.CanChooseMagicalAbility(abil) ||
                (abil & MagicalAbility.Tokuno) != 0 && !AbilityProfile.TokunoTame)
            {
                continue;
            }

            var tp = PetTrainingHelper.GetTrainingPoint(abil);

            builder.AddButton(275, y, 4005, 4007, 100 + i);
            Record(ref builder, tp, 320, y + 2);

            y += 22;
        }
    }

    private void BuildSpecialAbilitiesPage(ref DynamicGumpBuilder builder)
    {
        var y = 90;

        for (var i = 0; i < SpecialAbility.Abilities.Length; i++)
        {
            var abil = SpecialAbility.Abilities[i];

            if (AbilityProfile.HasAbility(abil) || AbilityProfile.HasSpecialMagicalAbility() && !AbilityProfile.IsRuleBreaker(abil))
            {
                continue;
            }

            if (Definition.HasSpecialAbility(abil) ||
                AbilityProfile.HasAbility(MagicalAbility.Poisoning) && abil == SpecialAbility.VenomousBite)
            {
                var tp = PetTrainingHelper.GetTrainingPoint(abil);

                builder.AddButton(275, y, 4005, 4007, 100 + i);
                Record(ref builder, tp, 320, y + 2);

                y += 22;
            }
        }
    }

    private void BuildSpecialMovesPage(ref DynamicGumpBuilder builder)
    {
        var y = 90;

        for (var i = 0; i < Definition.WeaponAbilities.Length; i++)
        {
            var abil = Definition.WeaponAbilities[i];

            if (AbilityProfile.HasAbility(abil))
            {
                continue;
            }

            var tp = PetTrainingHelper.GetTrainingPoint(abil);

            builder.AddButton(275, y, 4005, 4007, 100 + i);
            Record(ref builder, tp, 320, y + 2);

            y += 22;
        }
    }

    private void BuildAreaEffectsPage(ref DynamicGumpBuilder builder)
    {
        var y = 90;

        for (var i = 0; i < AreaEffect.Effects.Length; i++)
        {
            var abil = AreaEffect.Effects[i];

            if (AbilityProfile.HasAbility(abil))
            {
                continue;
            }

            if (Definition.HasAreaEffect(abil) ||
                AbilityProfile.HasAbility(MagicalAbility.Poisoning) && abil == AreaEffect.PoisonBreath)
            {
                var tp = PetTrainingHelper.GetTrainingPoint(abil);

                builder.AddButton(275, y, 4005, 4007, 100 + i);
                Record(ref builder, tp, 320, y + 2);

                y += 22;
            }
        }
    }

    private static void Record(ref DynamicGumpBuilder builder, TrainingPoint tp, int x, int y)
    {
        if (tp?.Name == null)
        {
            builder.AddLabel(x, y, 0, "Null Training Point");
            return;
        }

        if (tp.Name.Number > 0)
        {
            builder.AddHtmlLocalized(x, y, 215, 20, tp.Name.Number, 0);
        }
        else if (tp.Name.String != null)
        {
            builder.AddLabel(x, y, 0, tp.Name.String);
        }
    }

    private bool HasAvailable(object o)
    {
        if (o == null)
        {
            return false;
        }

        if (o is MagicalAbility && Definition.MagicalAbilities != 0)
        {
            foreach (var ability in Enum.GetValues<MagicalAbility>())
            {
                if ((Definition.MagicalAbilities & ability) != 0 && !AbilityProfile.HasAbility(ability))
                {
                    return true;
                }
            }

            return false;
        }

        switch (o)
        {
            case SpecialAbility[] specials:
                {
                    if (!AbilityProfile.CanChooseSpecialAbility(specials))
                    {
                        return false;
                    }

                    if (AbilityProfile.HasAbility(MagicalAbility.Poisoning) && !AbilityProfile.HasAbility(SpecialAbility.VenomousBite))
                    {
                        return true;
                    }

                    foreach (var ability in specials)
                    {
                        if (!AbilityProfile.HasAbility(ability))
                        {
                            return true;
                        }
                    }

                    break;
                }
            case WeaponAbility[] weapons:
                {
                    if (!AbilityProfile.CanChooseWeaponAbility())
                    {
                        return false;
                    }

                    foreach (var ability in weapons)
                    {
                        if (!AbilityProfile.HasAbility(ability))
                        {
                            return true;
                        }
                    }

                    break;
                }
            case AreaEffect[] areas:
                {
                    if (!AbilityProfile.CanChooseAreaEffect())
                    {
                        return false;
                    }

                    if (AbilityProfile.HasAbility(MagicalAbility.Poisoning) && !AbilityProfile.HasAbility(AreaEffect.PoisonBreath))
                    {
                        return true;
                    }

                    foreach (var ability in areas)
                    {
                        if (!AbilityProfile.HasAbility(ability))
                        {
                            return true;
                        }
                    }

                    break;
                }
            case SkillName[] names:
                {
                    foreach (var name in names)
                    {
                        var skill = Creature.Skills[name];

                        if (skill.Base > 0 && skill.Cap < 120)
                        {
                            return true;
                        }
                    }

                    return false;
                }
        }

        return false;
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var id = info.ButtonID;

        if (id == 0)
        {
            return;
        }

        if (id == 1)
        {
            Timer.DelayCall(
                TimeSpan.FromSeconds(.5),
                () =>
                {
                    if (User.HasGump<NewAnimalLoreGump>())
                    {
                        Refresh();
                        User.CloseGump<PetTrainingPlanningGump>();
                        User.SendGump(new PetTrainingPlanningGump(User, Creature));
                    }
                }
            );
            return;
        }

        if (id == 2)
        {
            Timer.DelayCall(
                TimeSpan.FromSeconds(.5),
                () =>
                {
                    if (User.HasGump<NewAnimalLoreGump>())
                    {
                        Refresh();
                        User.CloseGump<PetTrainingInfoGump>();
                        User.SendGump(new PetTrainingInfoGump(User));
                    }
                }
            );
            return;
        }

        if (id <= 11)
        {
            Timer.DelayCall(
                TimeSpan.FromSeconds(.5),
                () =>
                {
                    if (User.HasGump<NewAnimalLoreGump>())
                    {
                        Category = id - 3;
                        Refresh();
                    }
                }
            );
            return;
        }

        if (Definition == null)
        {
            return;
        }

        TrainingPoint tp = null;
        var index = id - 100;

        switch (Category)
        {
            case 0: // stats
                tp = PetTrainingHelper.GetTrainingPoint((PetStat)index);
                break;
            case 1: // resists
                tp = PetTrainingHelper.GetTrainingPoint((ResistanceType)index);
                break;
            case 2: // mag skill
            case 3: // combat skill
                tp = PetTrainingHelper.GetTrainingPoint((SkillName)index);
                break;
            case 4: // mag abil
                if (index >= 0 && index < PetTrainingHelper.MagicalAbilities.Length)
                {
                    tp = PetTrainingHelper.GetTrainingPoint(PetTrainingHelper.MagicalAbilities[index]);
                }

                break;
            case 5: // spec abil
                if (index >= 0 && index < SpecialAbility.Abilities.Length)
                {
                    tp = PetTrainingHelper.GetTrainingPoint(SpecialAbility.Abilities[index]);
                }

                break;
            case 6: // spec moves
                if (index >= 0 && index < Definition.WeaponAbilities.Length)
                {
                    tp = PetTrainingHelper.GetTrainingPoint(Definition.WeaponAbilities[index]);
                }

                break;
            case 7: // area effects
                if (index >= 0 && index < AreaEffect.Effects.Length)
                {
                    tp = PetTrainingHelper.GetTrainingPoint(AreaEffect.Effects[index]);
                }

                break;
        }

        if (tp != null)
        {
            Timer.DelayCall(
                TimeSpan.FromSeconds(.5),
                () =>
                {
                    if (User.HasGump<NewAnimalLoreGump>())
                    {
                        User.CloseGump<PetTrainingConfirmationGump>();
                        User.SendGump(new PetTrainingConfirmationGump(User, Creature, tp));
                    }
                }
            );
        }
    }
}

public class PetTrainingConfirmationGump : PetTrainingGump
{
    public BaseCreature Creature { get; }
    public TrainingPoint TrainingPoint { get; }

    public int Value { get; private set; }
    public int StartValue { get; private set; }

    public PetTrainingConfirmationGump(PlayerMobile pm, BaseCreature bc, TrainingPoint tp) : base(pm, 50, 200)
    {
        Creature = bc;
        TrainingPoint = tp;

        Value = tp.Start;
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        var profile = PetTrainingHelper.GetTrainingProfile(Creature, true);

        builder.AddBackground(0, 0, 574, 470, 0x24A4);
        builder.AddHtmlLocalized(0, 12, 574, 16, 1157486); // <CENTER>TRAINING CONFIRMATION</CENTER>

        builder.AddImageTiled(35, 40, 245, 140, 2624);
        builder.AddImageTiled(37, 42, 241, 136, 3004);

        builder.AddImageTiled(290, 40, 245, 140, 2624);
        builder.AddImageTiled(292, 42, 241, 136, 3004);

        builder.AddImageTiled(35, 190, 245, 140, 2624);
        builder.AddImageTiled(37, 192, 241, 136, 3004);

        builder.AddImageTiled(290, 190, 245, 140, 2624);
        builder.AddImageTiled(292, 192, 241, 136, 3004);

        builder.AddImageTiled(35, 340, 500, 60, 2624);
        builder.AddImageTiled(37, 342, 496, 56, 3004);

        builder.AddButton(40, 410, 0x9CC8, 0x9CC7, 1);
        builder.AddHtml(40, 413, 126, 20, Center("Back"));

        if (profile == null)
        {
            return;
        }

        var regular = profile.TrainingMode == TrainingMode.Regular;

        builder.AddButton(415, 410, 0x9CC8, 0x9CC7, regular ? 8 : 9);
        builder.AddHtml(415, 413, 126, 20, Center(regular ? "Train Pet" : "Add To Plan"));

        builder.AddHtmlLocalized(35, 55, 245, 20, CenterLoc, "#1114269", 0); // PROPERTY INFORMATION
        builder.AddHtmlLocalized(50, 85, 60, 16, 1114270); // Property:
        builder.AddHtmlLocalized(50, 105, 60, 16, 1114272); // Weight:

        if (TrainingPoint.Name != null)
        {
            if (TrainingPoint.Name.Number > 0)
            {
                builder.AddHtmlLocalized(120, 85, 200, 16, TrainingPoint.Name.Number);
            }
            else if (TrainingPoint.Name.String != null)
            {
                builder.AddLabel(120, 85, 0, TrainingPoint.Name.String);
            }
        }

        builder.AddLabel(120, 105, 0, TrainingPoint.Weight.ToString("0.0"));

        if (TrainingPoint.Description != null)
        {
            if (TrainingPoint.Description.Number > 0)
            {
                builder.AddHtmlLocalized(305, 55, 215, 115, TrainingPoint.Description.Number, true, true);
            }
            else if (TrainingPoint.Description.String != null)
            {
                builder.AddHtml(305, 55, 215, 115, TrainingPoint.Description.String, background: true, scrollbar: true);
            }
        }

        builder.AddHtmlLocalized(290, 205, 245, 20, CenterLoc, "#1113650", 0); // RESULTS

        var start = -1;
        var max = TrainingPoint.GetMax(Creature);
        PetTrainingHelper.GetStartValue(TrainingPoint, Creature, ref start);
        StartValue = start;

        if (StartValue > Value)
        {
            Value = StartValue;
        }

        if (Value > max)
        {
            Value = max;
        }

        var cost = PetTrainingHelper.GetTotalCost(TrainingPoint, Creature, Value, StartValue);
        var weight = TrainingPoint.Weight;

        if (CanAdjust() && cost > profile.TrainingPoints)
        {
            Value = StartValue + (int)(profile.TrainingPoints / weight);
        }

        if (StartValue > 0)
        {
            var valueWeight = Value * weight;
            var maxWeight = max * weight;
            var nonAdjustedWeight = (Value - StartValue) * weight;

            var originalValueWeight = valueWeight;

            if (TrainingPoint.TrainPoint is PetStat stat && stat <= PetStat.Mana)
            {
                double maxTotalCap = PetTrainingHelper.GetTrainingCapTotal(stat);
                double currentTotal = stat <= PetStat.Int
                    ? PetTrainingHelper.GetTotalStatWeight(Creature)
                    : PetTrainingHelper.GetTotalAttributeWeight(Creature);

                if (currentTotal >= maxTotalCap)
                {
                    valueWeight = 0;
                }
                else if (currentTotal + nonAdjustedWeight > maxTotalCap)
                {
                    valueWeight -= currentTotal + nonAdjustedWeight - maxTotalCap;
                }
            }
            else if (TrainingPoint.TrainPoint is ResistanceType resist)
            {
                double maxTotalCap = PetTrainingHelper.GetTrainingCapTotal(resist);
                double currentTotal = PetTrainingHelper.GetTotalResistWeight(Creature);

                if (currentTotal >= maxTotalCap)
                {
                    valueWeight = 0;
                }
                else if (currentTotal + nonAdjustedWeight > maxTotalCap)
                {
                    valueWeight -= currentTotal + nonAdjustedWeight - maxTotalCap;
                }
            }

            if (valueWeight > maxWeight)
            {
                valueWeight = maxWeight;
            }

            if (originalValueWeight != valueWeight)
            {
                Value = Math.Max(StartValue, (int)(valueWeight / weight));
            }
        }

        cost = PetTrainingHelper.GetTotalCost(TrainingPoint, Creature, Value, StartValue);
        var avail = profile.TrainingPoints - cost;

        builder.AddHtmlLocalized(35, 205, 245, 20, CenterLoc, "#1157493", 0); // REQUIREMENTS

        if (TrainingPoint.Requirements is { Length: > 0 })
        {
            for (var i = 0; i < TrainingPoint.Requirements.Length; i++)
            {
                var req = TrainingPoint.Requirements[i];

                if (req == null)
                {
                    continue;
                }

                AddText(ref builder, 45, 225 + i * 20, 190, 18, req.Name);

                builder.AddTooltip(1157523);

                var reqCost = req.Cost;

                if (req.Requirement is SkillName reqSkill && Creature.Skills[reqSkill].Value > 0)
                {
                    reqCost = 0;
                }

                builder.AddLabel(245, 225 + i * 20, 0x26, reqCost.ToString());
                builder.AddTooltip(1157523);
            }
        }
        else if (TrainingPoint.TrainPoint is SkillName)
        {
            var cliloc = Value switch
            {
                100 => 1049640,
                150 => 1049641,
                200 => 1049642,
                _ => 1049639
            };

            builder.AddHtmlLocalized(45, 225, 225, 60, cliloc, $"#{TrainingPoint.Name?.Number ?? 0}", 0);
        }

        builder.AddHtmlLocalized(305, 225, 145, 18, 1157490); // Avail. Training Points:
        builder.AddLabel(455, 225, avail <= 0 ? 0x26 : 0, avail.ToString());

        builder.AddHtmlLocalized(305, 245, 145, 18, 1113646); // Total Property Weight:
        builder.AddLabel(455, 245, 0, $"{(int)(Value * weight)}/{max * weight}");

        if (TrainingPoint.Name != null)
        {
            if (TrainingPoint.Name.Number > 0)
            {
                builder.AddHtmlLocalized(305, 265, 145, 18, TrainingPoint.Name.Number);
            }
            else if (TrainingPoint.Name.String != null)
            {
                builder.AddLabel(305, 265, 0, TrainingPoint.Name.String);
            }
        }

        if (TrainingPoint.TrainPoint is SkillName)
        {
            builder.AddLabel(455, 265, 0, (100.0 + (double)Value / 10).ToString("0.0"));
        }
        else
        {
            builder.AddLabel(455, 265, 0, StartValue > Value ? StartValue.ToString() : Value.ToString());
        }

        builder.AddHtmlLocalized(230, 352, 150, 18, 1113586); // Property Weight:
        builder.AddHtml(267, 373, 40, 18, Center(((int)(Value * weight)).ToString()));

        if (CanAdjust())
        {
            builder.AddButton(205, 375, 0x1464, 0x1464, 2);
            builder.AddButton(213, 375, 0x1466, 0x1466, 2);

            builder.AddButton(225, 375, 0x1464, 0x1464, 3);
            builder.AddButton(233, 375, 0x1466, 0x1466, 3);

            builder.AddButton(245, 375, 0x1464, 0x1464, 4);
            builder.AddButton(253, 375, 0x1466, 0x1466, 4);

            builder.AddButton(305, 375, 0x1464, 0x1464, 5);
            builder.AddButton(313, 375, 0x1466, 0x1466, 5);

            builder.AddButton(325, 375, 0x1464, 0x1464, 6);
            builder.AddButton(333, 375, 0x1466, 0x1466, 6);

            builder.AddButton(345, 375, 0x1464, 0x1464, 7);
            builder.AddButton(353, 375, 0x1466, 0x1466, 7);
        }
        else
        {
            builder.AddImage(205, 375, 0x1464);
            builder.AddImage(213, 375, 0x1466);

            builder.AddImage(225, 375, 0x1464);
            builder.AddImage(233, 375, 0x1466);

            builder.AddImage(245, 375, 0x1464);
            builder.AddImage(253, 375, 0x1466);

            builder.AddImage(305, 375, 0x1464);
            builder.AddImage(313, 375, 0x1466);

            builder.AddImage(325, 375, 0x1464);
            builder.AddImage(333, 375, 0x1466);

            builder.AddImage(345, 375, 0x1464);
            builder.AddImage(353, 375, 0x1466);
        }

        builder.AddLabel(207, 373, 0, "<");
        builder.AddLabel(211, 373, 0, "<");
        builder.AddLabel(215, 373, 0, "<");

        builder.AddLabel(228, 373, 0, "<");
        builder.AddLabel(232, 373, 0, "<");

        builder.AddLabel(251, 373, 0, "<");

        builder.AddLabel(312, 373, 0, ">");

        builder.AddLabel(329, 373, 0, ">");
        builder.AddLabel(333, 373, 0, ">");

        builder.AddLabel(348, 373, 0, ">");
        builder.AddLabel(352, 373, 0, ">");
        builder.AddLabel(356, 373, 0, ">");
    }

    private bool CanAdjust() => TrainingPoint.GetMax(Creature) != TrainingPoint.Start;

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var max = TrainingPoint.GetMax(Creature);
        var adjustable = TrainingPoint.Start != max;

        switch (info.ButtonID)
        {
            case 0:
                break;
            case 1:
                Timer.DelayCall(
                    TimeSpan.FromSeconds(.5),
                    () =>
                    {
                        if (User.HasGump<NewAnimalLoreGump>())
                        {
                            User.SendGump(new PetTrainingOptionsGump(User, Creature));
                        }
                    }
                );
                break;
            case 2: // <<<
                if (adjustable)
                {
                    Value = TrainingPoint.Start;
                }

                Refresh();
                break;
            case 3: // <<
                if (adjustable)
                {
                    if (TrainingPoint.TrainPoint is SkillName)
                    {
                        Value = Math.Max(TrainingPoint.Start, Value - 50);
                    }
                    else
                    {
                        Value -= max / 10;
                    }
                }

                Refresh();
                break;
            case 4: // <
                if (adjustable)
                {
                    if (TrainingPoint.TrainPoint is SkillName)
                    {
                        Value = Math.Max(TrainingPoint.Start, Value - 50);
                    }
                    else if (TrainingPoint.Weight < 1.0)
                    {
                        Value -= (int)(TrainingPoint.Weight * 100);
                    }
                    else
                    {
                        Value--;
                    }
                }

                Refresh();
                break;
            case 5: // >
                if (adjustable)
                {
                    if (TrainingPoint.TrainPoint is SkillName)
                    {
                        Value = Math.Min(max, Value + 50);
                    }
                    else if (TrainingPoint.Weight < 1.0)
                    {
                        Value += (int)(TrainingPoint.Weight * 100);
                    }
                    else
                    {
                        Value++;
                    }
                }

                Refresh();
                break;
            case 6: // >>
                if (adjustable)
                {
                    if (TrainingPoint.TrainPoint is SkillName)
                    {
                        Value = Math.Min(max, Value + 50);
                    }
                    else
                    {
                        Value += max / 10;
                    }
                }

                Refresh();
                break;
            case 7: // >>>
                if (adjustable)
                {
                    Value = max;
                }

                Refresh();
                break;
            case 8: // train pet
                TrainPet();
                break;
            case 9:
                if (Value > StartValue)
                {
                    PetTrainingHelper.GetPlanningProfile(Creature, true)?.AddToPlan(
                        TrainingPoint.TrainPoint,
                        Value,
                        PetTrainingHelper.GetTotalCost(TrainingPoint, Creature, Value, StartValue)
                    );

                    User.SendLocalizedMessage(1157592); // Your selection has been added to your training plan.
                }

                Timer.DelayCall(
                    TimeSpan.FromSeconds(.5),
                    () =>
                    {
                        if (User.HasGump<NewAnimalLoreGump>())
                        {
                            Refresh();

                            var gump = User.FindGump<PetTrainingPlanningGump>();

                            if (gump != null)
                            {
                                gump.Refresh();
                            }
                            else
                            {
                                User.SendGump(new PetTrainingPlanningGump(User, Creature));
                            }
                        }
                    }
                );
                break;
        }
    }

    /// <summary>ServUO's case 8 (:1420-1520): the checks, then the confirm gump whose yes applies the point.</summary>
    public void TrainPet()
    {
        var profile = PetTrainingHelper.GetTrainingProfile(Creature, true);

        if (profile == null)
        {
            return;
        }

        var cost = PetTrainingHelper.GetTotalCost(TrainingPoint, Creature, Value, StartValue);

        Item scroll = null;

        if (!Creature.Controlled || User != Creature.ControlMaster)
        {
            User.SendLocalizedMessage(1114368); // This is not your pet!
        }
        else if (!Creature.InRange(User.Location, 12))
        {
            User.SendLocalizedMessage(1153204); // The pet is too far away from you!
        }
        else if (SpellHelper.CheckCombat(User) || SpellHelper.CheckCombat(Creature) ||
                 Creature.Aggressed.Count > 0 || Creature.Combatant != null)
        {
            User.SendLocalizedMessage(1156876); // Since you have been in combat recently you may not use this feature.
        }
        else if (!profile.HasIncreasedControlSlot && User.Followers >= User.FollowersMax)
        {
            // You do not have the available pet slots to train this pet. Please free up pet slots and try again.
            User.SendLocalizedMessage(1157498);
        }
        else if (cost > profile.TrainingPoints)
        {
            User.SendLocalizedMessage(1157500); // Your pet lacks the required points to complete this training.
        }
        else if (TrainingPoint.TrainPoint is SkillName skillName && !CheckPowerScroll(skillName, Value, ref scroll))
        {
            // You are unable to train your pet. The required powerscroll was not found in your main backpack.
            User.SendLocalizedMessage(1157499);
        }
        else if (StartValue >= Value)
        {
            User.SendLocalizedMessage(1157501); // Your pet looks to have already completed that training.
        }
        else if (PetTrainingHelper.CanControl(User, Creature, profile))
        {
            User.SendGump(
                new PetTrainingConfirmGump(
                    User,
                    1157502,
                    TrainingPoint.TrainPoint is MagicalAbility ? 1157566 : 1157503,
                    () => ApplyTraining(profile, cost, scroll),
                    () => ResendGumps(profile.HasBegunTraining)
                )
            );
        }
        else
        {
            User.SendLocalizedMessage(1157550); // You lack the taming skill required to train this creature.
        }
    }

    /// <summary>The confirm gump's yes: ServUO :1470-1510. Public so a test can take the same path without a client.</summary>
    public bool ApplyTraining(TrainingProfile profile, int cost, Item scroll)
    {
        if (!PetTrainingHelper.ApplyTrainingPoint(Creature, TrainingPoint, Value))
        {
            return false;
        }

        User.SendLocalizedMessage(1157497); // You successfully train the pet!

        if (TrainingPoint.TrainPoint is SkillName)
        {
            Effects.SendLocationParticles(
                EffectItem.Create(Creature.Location, Creature.Map, EffectItem.DefaultDuration), 0, 0, 0, 0, 0, 5060, 0
            );
            Effects.PlaySound(Creature.Location, Creature.Map, 0x243);

            Effects.SendMovingParticles(
                new Entity(Serial.Zero, new Point3D(Creature.X - 6, Creature.Y - 6, Creature.Z + 15), Creature.Map),
                Creature, 0x36D4, 7, 0, false, true, 0x497, 0, 9502, 1, 0, (EffectLayer)255, 0x100
            );
            Effects.SendMovingParticles(
                new Entity(Serial.Zero, new Point3D(Creature.X - 4, Creature.Y - 6, Creature.Z + 15), Creature.Map),
                Creature, 0x36D4, 7, 0, false, true, 0x497, 0, 9502, 1, 0, (EffectLayer)255, 0x100
            );
            Effects.SendMovingParticles(
                new Entity(Serial.Zero, new Point3D(Creature.X - 6, Creature.Y - 4, Creature.Z + 15), Creature.Map),
                Creature, 0x36D4, 7, 0, false, true, 0x497, 0, 9502, 1, 0, (EffectLayer)255, 0x100
            );

            Effects.SendTargetParticles(Creature, 0x375A, 35, 90, 0x00, 0x00, 9502, (EffectLayer)255, 0x100);

            scroll?.Delete();
        }
        else
        {
            Creature.PlaySound(0x1EB);
            Creature.FixedEffect(0x375A, 1, 11);
        }

        profile.OnTrain(User, cost);

        ResendGumps(profile.HasBegunTraining);

        return true;
    }

    private void ResendGumps(bool sendOptions)
    {
        Timer.DelayCall(
            TimeSpan.FromSeconds(.5),
            () =>
            {
                var lore = User.FindGump<NewAnimalLoreGump>();

                if (lore != null)
                {
                    lore.Refresh();
                }
                else
                {
                    User.SendGump(new NewAnimalLoreGump(User, Creature));
                }

                if (sendOptions)
                {
                    var options = User.FindGump<PetTrainingOptionsGump>();

                    if (options != null)
                    {
                        options.Refresh();
                    }
                    else
                    {
                        User.SendGump(new PetTrainingOptionsGump(User, Creature));
                    }
                }
            }
        );
    }

    private bool CheckPowerScroll(SkillName name, int value, ref Item scroll)
    {
        if (User.Backpack == null)
        {
            return false;
        }

        scroll = User.Backpack.Items.OfType<PowerScroll>().FirstOrDefault(ps => ps.Skill == name && ps.Value == 100 + value / 10);

        return scroll != null;
    }
}

public class PetTrainingPlanningGump : PetTrainingGump
{
    public BaseCreature Creature { get; }

    public PetTrainingPlanningGump(PlayerMobile pm, BaseCreature bc) : base(pm, 50, 200) => Creature = bc;

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        var profile = PetTrainingHelper.GetTrainingProfile(Creature, true);

        builder.AddBackground(0, 0, 654, 524, 0x24A4);

        if (profile == null)
        {
            return;
        }

        var plan = profile.PlanningProfile;

        builder.AddButton(275, 35, 0x9CC8, 0x9CC7, 1);
        builder.AddHtml(275, 38, 126, 20, Center(profile.TrainingMode == TrainingMode.Planning ? "DISABLE" : "ENABLE"));

        builder.AddHtmlLocalized(0, 11, 654, 18, CenterLoc, "#1157591", 0xF424E5); // Pet Training Planning
        builder.AddHtmlLocalized(60, 60, 216, 18, 1044010, 0); // <CENTER>CATEGORIES</CENTER>
        builder.AddHtmlLocalized(275, 60, 216, 18, 1044011, 0); // <CENTER>SELECTIONS</CENTER>
        builder.AddHtmlLocalized(510, 60, 150, 18, 1113586, 0); // Property Weight:

        builder.AddHtmlLocalized(260, 80, 150, 16, 1157490, C32216(0x8B0000)); // Avail. Training Points:
        builder.AddLabel(510, 80, 0, profile.TrainingPoints.ToString());

        var y = 95;
        var total = 0;

        for (var i = 0; i < plan.Entries.Count; i++)
        {
            var entry = plan.Entries[i];

            builder.AddButton(25, y, 4017, 4019, i + 100);
            builder.AddHtmlLocalized(60, y, 200, 18, PetTrainingHelper.GetCategoryLocalization(entry.TrainPoint));

            var loc = PetTrainingHelper.GetLocalization(entry.TrainPoint);

            if (loc?[0] != null)
            {
                if (loc[0].Number > 0)
                {
                    builder.AddHtmlLocalized(260, y, 200, 18, loc[0].Number);
                }
                else if (loc[0].String != null)
                {
                    builder.AddHtml(260, y, 200, 18, loc[0].String);
                }
            }

            var value = entry.TrainPoint is SkillName ? entry.Value + 1000 : entry.Value;

            builder.AddLabel(460, y, entry.Value == 0 ? 0x27 : 0, value.ToString());
            builder.AddLabel(510, y, entry.Cost == 0 ? 0x27 : 0, $"-{entry.Cost}");

            total += entry.Cost;
            y += 22;
        }

        var remaining = profile.TrainingPoints - total;

        builder.AddHtmlLocalized(260, y, 200, 18, 1157599, C32216(0x8B0000)); // Remaining Training Points:
        builder.AddLabel(510, y + 5, remaining < profile.TrainingPoints ? 0x27 : 0, remaining.ToString());
    }

    public override void OnResponse(NetState sender, in RelayInfo info)
    {
        var id = info.ButtonID;

        if (id == 0)
        {
            return;
        }

        var profile = PetTrainingHelper.GetTrainingProfile(Creature, true);

        if (profile == null)
        {
            return;
        }

        var plan = profile.PlanningProfile;

        if (id == 1)
        {
            if (profile.TrainingMode == TrainingMode.Planning)
            {
                profile.TrainingMode = TrainingMode.Regular;
                User.SendLocalizedMessage(1157598); // You have disabled pet training planning.
            }
            else
            {
                profile.TrainingMode = TrainingMode.Planning;
                // You have enabled pet training planning. All pet training selections will be added to the planning gump
                // which will display up to 20 training options. For more details view the information gump.
                User.SendLocalizedMessage(1157597);
            }

            User.FindGump<PetTrainingConfirmationGump>()?.Refresh();

            Refresh();
        }
        else
        {
            id -= 100;

            if (id >= 0 && id < plan.Entries.Count)
            {
                plan.Entries.RemoveAt(id);
                User.SendLocalizedMessage(1157593); // Your selection has been removed to your training plan.
            }

            Refresh();
        }
    }
}

public class PetTrainingInfoGump : PetTrainingGump
{
    public PetTrainingInfoGump(PlayerMobile pm) : base(pm, 50, 200)
    {
    }

    protected override void BuildLayout(ref DynamicGumpBuilder builder)
    {
        builder.AddPage(0);
        builder.AddBackground(0, 0, 654, 500, 0x24A4);
        builder.AddHtmlLocalized(0, 45, 654, 16, CenterLoc, "#1157527", 0xF424E5); // Discovering Animal Training

        builder.AddPage(1);

        builder.AddHtmlLocalized(30, 75, 590, 380, 1157558, true, true);

        builder.AddButton(560, 45, 0x15E3, 0x15E7, 0, GumpButtonType.Page, 4);
        builder.AddButton(585, 45, 0x15E1, 0x15E5, 0, GumpButtonType.Page, 2);

        builder.AddPage(2);

        builder.AddHtmlLocalized(30, 75, 590, 380, 1157563, true, true);

        builder.AddButton(560, 45, 0x15E3, 0x15E7, 0, GumpButtonType.Page, 1);
        builder.AddButton(585, 45, 0x15E1, 0x15E5, 0, GumpButtonType.Page, 3);

        builder.AddPage(3);

        builder.AddHtmlLocalized(30, 75, 590, 380, 1157552, true, true);

        builder.AddButton(560, 45, 0x15E3, 0x15E7, 0, GumpButtonType.Page, 2);
        builder.AddButton(585, 45, 0x15E1, 0x15E5, 0, GumpButtonType.Page, 4);

        builder.AddPage(4);

        builder.AddHtmlLocalized(30, 75, 590, 380, 1157553, true, true);

        builder.AddButton(560, 45, 0x15E3, 0x15E7, 0, GumpButtonType.Page, 3);
        builder.AddButton(585, 45, 0x15E1, 0x15E5, 0, GumpButtonType.Page, 1);
    }
}
