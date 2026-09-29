# Shattered Legacy Test Kit

When you add a new resource, add one line to `TestCenterKitEntries.cs` in this folder:

    TestCenterKit.Register(() => new PlatinumIngot(), 5000);

That is the whole job. Every new test-shard character, and every `[TCFill`, gets a bag named
"Shattered Legacy Test Kit" in the bank holding one stack of each registered resource, at the amount
given. A non-stackable item is placed one per unit, up to 25 (asking for more logs a warning).

Nothing is placed unless `testCenter.enable` is true, so the line is safe on the live shard. Do not edit
pinned's `TestCenter.cs` for this. Background: `shard-migration/notes/cc-P9-test-center.md`.
