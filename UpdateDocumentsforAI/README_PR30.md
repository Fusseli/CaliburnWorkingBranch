# PR30 — Re-save gear fix (apply ON TOP of PR29)

## Symptom

- `/msave` a fresh mimic, `/mcall` → **full equipment** ✓
- `/mdismiss`, `/mcall` again → **everything gone except the weapon**

So the first save-and-recall worked; only the re-save cycle broke.

## Root cause: `AutoDelete` destroys the uniques on every re-save

`/msave` and `/mcall` were already correct after PR29. The only thing that differs
on a re-save is that `/mdismiss` runs `StoreGear` again over an **occupied** slot,
which ends by deleting the previously stored rows:

```csharp
foreach (string oldId in oldItemIds)
{
    var oldItem = GameServer.Database.FindObjectByKey<DbInventoryItem>(oldId);
    if (oldItem != null)
        GameServer.Database.DeleteObject(oldItem);   // ← this is the problem
}
```

`DbInventoryItem` declares its unique relation as
(`DbInventoryItem.cs:257`):

```csharp
[Relation(LocalField = "UTemplate_Id", RemoteField = "Id_nb", AutoLoad = true, AutoDelete = true)]
```

`AutoDelete = true` means *"all related Objects are deleted from Database when the
Object is deleted"* (`Relation.cs:47`), and `ObjectDatabase.cs:409` walks exactly
those relations on delete. So deleting the old rows **also deletes the
`DbItemUnique` rows that the rows just written still point at** — both point at the
same `Unique_xxx`, because the worn item's template was never changed.

Then the next `/mcall` finds dangling `UTemplate_Id`s again, `AutoLoad` cannot
resolve them, and the armour comes back as invisible husks. Plain-template gear
(weapons, shields) is untouched, which is why the weapon survived.

**The trap that hid it:** PR29's `EnsureTemplatePersisted` tested
`unique.IsPersisted`. `DeleteObject` removes the row from the database but **does
not clear `IsPersisted`** on the in-memory object, so the flag still said "fine,
nothing to do" and the one unique that needed rewriting was skipped.

**A correction to my earlier reasoning:** when reviewing PR29 I described
`AutoDelete = true` as a benefit, because it makes `/mdelete` clean up both tables.
It is true, and it is also exactly what caused this. I read the attribute's upside
without tracing its delete path.

## The fix

**1. Test the database, not the in-memory flag**

```csharp
public static bool EnsureTemplatePersisted(DbInventoryItem item)
{
    if (item?.Template is not DbItemUnique unique)
        return false;
    bool rowExists = GameServer.Database.FindObjectByKey<DbItemUnique>(unique.Id_nb) != null;
    if (!NeedsTemplatePersist(true, rowExists))
        return false;
    unique.AllowAdd = true;
    try { GameServer.Database.AddObject(unique); }
    finally { unique.AllowAdd = false; }
    return true;
}
```

Now returns `bool` so callers can count. `NeedsTemplatePersist`'s second parameter
is renamed `templateRowExists` so it cannot be misread as `IsPersisted`.

**2. A second persist pass, after the old-row deletion**

`StoreGear` now runs, in order:

1. read `oldItemIds`
2. per worn item: persist any missing unique, then write the new copy
3. delete the previously stored rows — which may auto-delete uniques
4. **per worn item: persist again**, rewriting whatever step 3 removed

Step 4 is idempotent (it tests whether the row exists), so it only rewrites what
is genuinely missing.

**The write-then-delete order is deliberately kept.** Deleting first would be a
simpler flow, but then a write failing partway would leave the bot with no stored
gear at all. As written, a partial failure leaves the previous gear on disk.

**3. A warning that names the cause**

If step 4 had to rewrite anything, the log says so explicitly:

```
StoreGear: <key> - deleting the previous stored rows auto-deleted N generated
unique(s) that the rows just written still reference; they have been rewritten.
If this appears on every save, the AutoDelete setting on
DbInventoryItem.UTemplate_Id is the cause.
```

That single line would have made this a log read instead of a build-and-test cycle.

## Files

| File | Baseline | Change |
|---|---|---|
| `GameServer/custom/MimicNPC/MimicSaveManager.cs` | PR29 | DB-existence check, second persist pass, warning |
| `Tests/Unit/GoapTests.cs` | PR29 | 1 new test, 1 repurposed |

## Tests

**107 total, unchanged from PR29** — one test was added
(`AutoDeletedUniqueIsRewrittenEvenThoughTheFlagSaysPersisted`) and PR29's
`UnresolvableStoredGearIsSkippedRatherThanAddedInvisible` was removed rather than
kept, because it asserted the same pure helper a second time and added no coverage.
So the count stays flat while the coverage actually improves.

## Apply order

Full-file snapshots — order matters. PR30 stacks on PR29 → PR28 → … → export.

```powershell
$base = "DO NOT DELETE OR CHANGE\NewPR_Integrations"
$repo = "<YourCaliburn>"
Copy-Item "$base\Fusseli_MimicGOAP_FromWarlock_Patch_Export\*" $repo -Recurse -Force
# … PR22 through PR29 in order …
Copy-Item "$base\Caliburn_PR30_ReSaveFix\*"                 $repo -Recurse -Force
```

## Verify

```powershell
dotnet build GameServer/GameServer.csproj            # expect 0 errors
dotnet test Tests/Tests.csproj --filter 'FullyQualifiedName~DOL.Tests.Unit.GoapTests|FullyQualifiedName~DOL.Tests.Unit.RoleElectionTests'
# expect 107/107 green
```

Live — **step 3 is the one that fails today:**

1. `/msave` a fresh mimic, `/mcall` → full armour
2. **`/mdismiss`, `/mcall` → full armour**
3. **Repeat 2 three or four times.** This is a re-save loop, so a partial fix shows up on the third cycle, not the first
4. Restart the server, `/mcall` → still armoured
5. `/mdelete` → both `inventory` and `itemunique` rows gone

Expect the auto-delete warning on `/mdismiss` and **no**
`has no resolvable template; skipping` on `/mcall`.

If the warning appears on *every* save even after this fix, that is expected — the
`AutoDelete` attribute is still doing its job. The fix makes it harmless.

## Known limits

- **~17 extra primary-key lookups per re-save** (one per item per pass). Nothing.
- If the same generated unique were ever shared between two bots, one bot's
  `/mdelete` would auto-delete it out from under the other. That cannot happen
  today — each generated unique belongs to one item on one bot — but the new
  warning is what would surface it if that ever changed.
- **Melee kiting, the debuff root cause, the `SkillBase` RA log spam and the
  traceability list remain open** — none affected by this patch.