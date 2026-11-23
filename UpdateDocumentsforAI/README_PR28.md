# PR28 — Savebot storage fix (apply ON TOP of PR27)

## Symptom

`/msave` on a mimic failed with *"I could not store my gear. Please try again."*
(or "Could not store inventory"), and the bot was never saved. The only related
log line was a WARN, no ERROR:

```
[WARN] [DOL.Database.ObjectDatabase] SaveObjectRelations: DataObject ({ Local = DataObject: Inventory,
ObjectId{d6e7d42c-...}, Name{Adroit Chain Helm}, OwnerID{mimic:testmid:2:0}, SlotPosition{21},
Count{1}, Remote = DataObject: ItemUnique, ObjectId{d95603a6-...} }) not allowed to be added to Database
```

## Root cause: `AddObject`'s boolean does not mean "my row was inserted"

`StoreGear` trusted it:

```csharp
if (!GameServer.Database.AddObject(copy))
    throw new InvalidOperationException($"Could not store {item.Name} for {mimic.Name}");
```

But for a table **with relations** that boolean also folds in relation work
(`CoreDatabase/ObjectDatabase.cs:151`):

```csharp
if (tableHandler.HasRelations)
    success &= SaveObjectRelations(tableHandler, resultGrp.Select(obj => obj.DataObject));
```

`Inventory` has an `ItemUnique` relation. `SaveObjectRelations`
(`ObjectDatabase.cs:320`) groups relation targets by `IsPersisted`, then by
`Dirty` for already-persisted ones. An `ItemUnique` that **already exists and is
unchanged** therefore has `Dirty == false`, lands in the else branch, and gets:

```csharp
log.WarnFormat("SaveObjectRelations: DataObject ({0}) not allowed to be added to Database", obj);
success = false;
```

So `AddObject` returns **false even though the `Inventory` row was written
successfully** — and the WARN you saw is that exact event. Then:

1. `StoreGear` throws
2. its `catch` **deletes the copies it had just correctly stored**
3. `/msave` catches, deletes the `MimicSave` row
4. the player is told it failed

**Every save failed and rolled back cleanly.** Nothing was corrupted and no rows
were orphaned — which matches what you observed.

Items carrying an `ItemUnique` (your `Adroit Chain Helm` is one) are what trigger
this. A mimic in plain generated gear may have saved fine, which would explain why
this looked inconsistent.

## The fix — ask whether *my row* landed

`AddObjectImpl` sets `IsPersisted = true` only when the INSERT actually affected a
row (`CoreDatabase/SqlObjectDatabase.cs:174`). That is the honest signal.

```csharp
public static bool RowStored(bool isPersisted) => isPersisted;
```

```csharp
GameServer.Database.AddObject(copy);
if (!RowStored(copy.IsPersisted))
    throw new InvalidOperationException($"Could not store {item.Name} for {mimic.Name}");
added.Add(copy);
```

`RowStored` deliberately takes only the flag and ignores `AddObject`'s result, so
the wrong signal cannot creep back in.

## Also in this patch

**1. The two `DbMimicSave` saves normalised to the same check** (`RefreshActive`,
`DismissBot`). These were already correct — `DbMimicSave` declares no relations,
so its boolean is trustworthy — but leaving them different would plant the trap for
whoever edits this file next.

**2. `/msave`'s `AddObject(save)` kept as-is, with a comment.** It is genuinely
trustworthy for the same reason, and the comment says so explicitly so it does not
read as an oversight sitting next to `RowStored`.

**3. Exception logging added to every savebot catch block.** Previously a storage
failure produced a generic player message and **nothing in the server log** — which
is why this needed code archaeology instead of one log line:
- `MimicSaveManager.StoreGear` — was a bare `catch`
- `Commands.cs` `/msave` — was a bare `catch (Exception)`
- `Commands.cs` `/mdismiss` — was a bare `catch (Exception)`

Rollback behaviour is unchanged; it now only fires when a row genuinely failed.

## Files

| File | Baseline | Change |
|---|---|---|
| `GameServer/custom/MimicNPC/MimicSaveManager.cs` | the export | `RowStored`, `StoreGear` fix, 2 save sites, logging |
| `GameServer/custom/MimicNPC/Commands.cs` | the export | logging in `/msave` and `/mdismiss`, comment on `AddObject(save)` |
| `Tests/Unit/GoapTests.cs` | PR27 | 2 new tests |

Neither file had been touched since PR21, so both are based on the export.

## Apply order

Full-file snapshots — order matters. PR28 stacks on PR27 → PR26 → PR25 → PR24 →
PR23 → PR22 → export.

```powershell
$base = "DO NOT DELETE OR CHANGE\NewPR_Integrations"
$repo = "<YourCaliburn>"
Copy-Item "$base\Fusseli_MimicGOAP_FromWarlock_Patch_Export\*" $repo -Recurse -Force
Copy-Item "$base\Caliburn_PR22_KiteFix\*"                    $repo -Recurse -Force
Copy-Item "$base\Caliburn_PR23_RezFix\*"                     $repo -Recurse -Force
Copy-Item "$base\Caliburn_PR24_CorpsePose\*"                 $repo -Recurse -Force
Copy-Item "$base\Caliburn_PR25_RAFix\*"                      $repo -Recurse -Force
Copy-Item "$base\Caliburn_PR26_ArcheryCombat\*"              $repo -Recurse -Force
Copy-Item "$base\Caliburn_PR27_ArcherKite\*"                 $repo -Recurse -Force
Copy-Item "$base\Caliburn_PR28_SavebotStorage\*"             $repo -Recurse -Force
```

## Tests

2 new, **105 total**. They pin the *decision* (`RowStored`'s truth table) and
document why. They are deliberately **not** source-text or reflection assertions:
these paths need a live database, and a test that greps source or resolves the
repo from the assembly path would be brittle while only pretending to verify
something. The live `/msave` round trip below is the real test.

## Verify

```powershell
dotnet build GameServer/GameServer.csproj            # expect 0 errors
dotnet test Tests/Tests.csproj --filter 'FullyQualifiedName~DOL.Tests.Unit.GoapTests|FullyQualifiedName~DOL.Tests.Unit.RoleElectionTests'
# expect 105/105 green
```

Live:

1. `/msave` a mimic → **should now succeed**
2. `/mbots` → listed, in the expected slot
3. `/mcall <slot>` → the gear comes back on it
4. `/mdismiss`, then `/mcall` again → gear survives the round trip
5. **The `SaveObjectRelations ... not allowed to be added` WARN should disappear
   from your log.** It was the tell, and with the fix there is nothing to warn about
6. Save a second bot into another slot, then `/mdelete` one → the other is untouched
7. Log out and back in → `/mbots` still lists them, and `/mcall` still works
8. Save a mimic wearing an `ItemUnique` (the failing case) specifically

If a save *still* fails, the new logging will finally name the cause — that alone
is worth having.

## Known limits

- This is inference from your WARN plus the code, not an observed stack trace.
  Every link in the chain is verified, but if `/msave` still fails after this, the
  log line added in step 3 will say why.
- `GameServer.Database.AddObject`'s boolean semantics are a base-repo wart. Patching
  `ObjectDatabase` to separate "row inserted" from "relations settled" would fix it
  for every caller, but that is an engine-wide change and out of scope here.
- **Melee kiting, the debuff root cause, the `SkillBase` RA log spam and the
  traceability list all remain open** — see the earlier notes. None of them are
  affected by this patch.