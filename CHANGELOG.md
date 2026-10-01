# Shattered Legacy changelog

<!--
How this file works (F-15, cc-P22). This file is the only place patch notes are written.

- Newest first. One section per version, headed "## " and the version: a date, 2026.09.30. A second update on the
  same day is 2026.09.30.2, then .3. The newest section's version also goes in VERSION, one line.
- Under a version, up to three lists, each headed "### Added", "### Changed" or "### Fixed". Leave out a list with
  nothing in it.
- One player-facing line per item, starting "- ". Plain words: what a player sees, not how it was built. Internal
  detail (file names, tests, bug numbers) stays out.
- Anything else in this file, including this comment, is ignored.

Who reads it:
- The server, at start (server/customizations/ShardVersion.cs): the version in game ([version), the patch notes in
  the login bulletin once per account per version, and "version" in status.json. Docker copies this file and VERSION
  into the image, so a server shows the notes of the build it is running.
- The wiki page uo:patch_notes, generated from the same parse; scripts/Publish-PatchNotes.ps1 publishes it.

Example:

## 2026.10.02

### Added
- A Thieves' Den lookout sits by the fighting pit and points the way to the guildmaster.

### Fixed
- Restoring a Jacob's Pickaxe no longer refuses ingots kept in your bank.
-->

## 2026.10.01

### Added
- Wren, a Thieves' Den lookout, sits by the New Haven fighting pit and points the way to the guildmaster.
- "Combine this deed with contained items" on bulk order deeds: target a bag in your pack to combine everything in it.
- Your bank now holds 1,000 items.
- Patch notes: new updates show here when you log in, and [version shows them any time.
- The Reset Stone in New Haven is now a floating, shattered runestone. Accept the game update when it is offered to see it.

### Changed
- Guild upgrades, restorations and turn-ins now use materials from your bank as well as your pack, pack first.
- Restoring a Jacob's Pickaxe at tiers 1 and 2 costs much less.
- The Smith Seal catalog no longer sells power scrolls.

### Fixed
- The Guild Directory listed the Artificers' Order twice.
- "Show me the way" now points to the nearest guildmaster and names the town.
- Salvaging a smithed item can no longer return more ingots than it cost.
