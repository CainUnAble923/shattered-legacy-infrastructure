// ShardCommandAttribute.cs
//
// cc-P21. Every command this shard registers declares what kind of command it is, on its handler,
// beside [Usage] and [Description]:
//
//     [Usage("ClusterFSeedMineCamp [status|dryrun]")]
//     [Description("...")]
//     [ShardCommand(CommandCategory.WorldSetup, Rerun = CommandRerun.Skips, Shard = CommandShard.TestFirst,
//         DryRun = "dryrun", Summary = "Places the mine camp tents, props and NPCs.")]
//
// The person writing a command knows these facts when they write it; nobody reading the code later
// should have to re-derive them. Two things read this declaration:
//
//   - scripts/Shard-Console.ps1, whose World setup tab lists the world commands from it, grouped by
//     category, with the dry run and the re-run behaviour it declares. It reads the source text, so
//     keep each named argument a plain literal: no constants, no string concatenation.
//   - server/tests/Misc/CommandDeclarationVerification.cs, which fails the build for any command
//     registered without one, for a world command missing a fact, and for a DryRun that is not a
//     word of that command's own [Usage].
//
// ORDER IS NOT DECLARED HERE. Which world command runs before which is a relationship between
// commands, not a property of one, so it is one explicit list in the console
// ($script:WorldSetupOrder), with where each position came from.
//
// This file declares types only. It registers nothing and changes no behaviour.

using System;

namespace Server;

public enum CommandCategory
{
    // Builds content a world does not have yet (dungeons, spawners): on a fresh world, or once on a
    // live one that has never had it.
    WorldGeneration = 1,

    // Changes a world that already exists: places, moves, repairs or clears decoration and NPCs.
    WorldSetup,

    // Removes what a generation or setup command placed. Never part of a run sequence.
    WorldRemoval,

    // Testing and staff convenience. Changes a character or an account, not the world.
    DevTool,

    // Hands a player something: an item, an unlock, standing.
    Grant,

    // Read only. Reports and changes nothing.
    Diagnostic,

    // Players type it themselves. Exactly the Player-access commands.
    Player
}

// What running the command a second time does, with no argument (its default form). Declare what
// the source does, not what the note hoped for; when the two disagree, say Unverified and report it.
public enum CommandRerun
{
    Undeclared = 0,

    // Skips what is already there. Safe to run again.
    Skips,

    // Removes its own earlier placement and places it again. Safe, but resets what it placed.
    Replaces,

    // Places a second copy. Do not run twice.
    Duplicates,

    // Checks and refuses to run a second time.
    Refuses,

    // A removal: a second run deletes again whatever matches, including anything that came back.
    DeletesAgain,

    // Not established from the source.
    Unverified
}

// Which shard the command is meant for.
public enum CommandShard
{
    Undeclared = 0,

    // Either shard; nothing about it is specific to one.
    Any,

    // The test shard only. Never the live shard.
    TestOnly,

    // Rehearse on the test shard first, then the live one.
    TestFirst,

    // No note or source states it.
    Unverified
}

[AttributeUsage(AttributeTargets.Method)]
public sealed class ShardCommandAttribute : Attribute
{
    public ShardCommandAttribute(CommandCategory category) => Category = category;

    public CommandCategory Category { get; }

    // Required for WorldGeneration, WorldSetup and WorldRemoval; optional otherwise.
    public CommandRerun Rerun { get; set; }

    public CommandShard Shard { get; set; }

    // The exact argument that makes the command report without changing anything, e.g. "dryrun".
    // It must be a word of the command's own [Usage]. A world command sets this or NoDryRun.
    public string DryRun { get; set; }

    public bool NoDryRun { get; set; }

    // One line a tired person can read at 11pm. Required for world commands; others fall back to
    // [Description].
    public string Summary { get; set; }

    public bool ChangesTheWorld =>
        Category is CommandCategory.WorldGeneration or CommandCategory.WorldSetup or CommandCategory.WorldRemoval;
}
