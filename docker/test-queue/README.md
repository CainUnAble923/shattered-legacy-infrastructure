# Client test queue viewer

> **This runs on the Windows box (`192.168.1.58`), not on Haven,** because the file it shows lives
> here. LAN only: http://192.168.1.58:8093/ . **Never route it through NPM**; it is an internal
> checklist. Start or recreate it with `Start-TestQueue.ps1` in this folder.

A read-only page over `D:\UO\shard-migration\docs\client-test-queue.md`. It re-reads the file every
10 seconds, so a status changed in the file (by Chase or by any chat) shows up without a rebuild,
and the changed card flashes once. The page never writes to the file: the file's own header has the
rules for adding and settling items, and it stays the only record.

It parses the file's own shapes: `## A. ...` section headings, table rows starting `| T-NNN |`,
the status word in the second column (OPEN, PASS, FAIL, BLOCKED, MOOT), and result lines under
`## Results` shaped `- T-NNN, yyyy-mm-dd: text`. A row in another shape will not show; keep to the
file's format.
