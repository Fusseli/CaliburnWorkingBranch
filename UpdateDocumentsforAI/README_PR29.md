# PR29 — Gear storage fix (apply ON TOP of PR28)

## Symptom

`/msave` succeeded, `/mbots` listed the bots, and `/mcall` brought them back —
but **without most of the gear. Only the weapon was visible.**

## Root cause: stored armour pointed at a template row that never existed

The database told the whole story. Every stored row had exactly one template
column populated, and the split matched the symptom precisely:

| Slots | Template column | Restored? |
|---|---|---|
| **10, 11, 12** (weapon, off-hand, two-hand) | `ITemplate_Id` set, `UTemplate_Id` NULL | ✅ yes |
| **21–29, 32–36** (armour, jewellery, cloak) | `UTemplate_Id` set, `ITemplate_Id` NULL | ❌ no |

So storage was never broken — PR28 fixed a real bug and the rows prove the write
half works. The failure was in how the rows resolve on load.

Mimic armour is built by `MimicEquipment` as:

```csharp
DbItemTemplate itemToCreate = new GeneratedUniqueItem(false, realm, charClass, level, objectType, slot);
GameInventoryItem item = GameInventoryItem.Create(itemToCreate);
living.Inventory.AddItem(slot, item);
```

And `GeneratedUniqueItem`'s constructor ends with (`GeneratedUniqueItem.cs:164`):

```csharp
//don't add to database implicitly, must be done explicitly
this.AllowAdd = false;
```

**Nothing ever does it explicitly**, so no `DbItemUnique` row is written. The worn
item simply carries a `UTemplate_Id` pointing at a row that does not exist.

`DbInventoryItem` then declares (`DbInventoryItem.cs:257`):

```csharp
[Relation(LocalField = "UTemplate_Id", RemoteField = "Id_nb", AutoLoad = true, AutoDelete = true)]
```

`AutoLoad` cannot resolve a row that isn't there, so `Template` stays `null`.
`new GameInventoryItem(item)` copies that `null`, and the restored item is a husk —
no model, no stats, invisible. Plain-template gear (weapons, shields) resolves
against the shop `ItemTemplate` table, which does exist, so it worked.

## The fix

**1. Persist the generated unique at save time** — the "explicitly" step
`GeneratedUniqueItem` asks for, done at the only moment that matters:

```csharp
public static void EnsureTemplatePersisted(DbInventoryItem item)
{
    if (item?.Template is not DbItemUnique unique)
        return;
    if (!NeedsTemplatePersist(true, unique.IsPersisted))
        return;
    unique.AllowAdd = true;
    try { GameServer.Database.AddObject(unique); }
    finally { unique.AllowAdd = false; }
}
```

Called per worn item in `StoreGear`. Deliberately **not** done at spawn time —
`MimicSpawner` would write ~14 rows per wild mimic. `AllowAdd` is restored in a
`finally` so the object keeps its "never add implicitly" contract.

**2. Skip unresolvable gear on recall** — an item whose `Template` is null is now
logged and skipped instead of being added silently, so `/mcall` never fills a bot
with invisible items.

**3. Log the counts** in `StoreGear` (stored *n* of *m*) and `Instantiate`
(restored *n* of *m*, skipped *k*). Diagnosing this took two SQL round trips
because nothing on the path reported a number.

## Files

| File | Baseline | Change |
|---|---|---|
| `GameServer/custom/MimicNPC/MimicSaveManager.cs` | PR28 | `NeedsTemplatePersist`, `EnsureTemplatePersisted`, `StoreGear` hook, `Instantiate` skip + logging |
| `Tests/Unit/GoapTests.cs` | PR28 | 2 new tests |

PR28's `RowStored` / `IsPersisted` work and its logging are carried forward
unchanged — this patch builds on PR28's version of the file, not the export's.

## Apply order

Full-file snapshots — order matters. PR29 stacks on PR28 → PR27 → … → export.

```powershell
$base = "DO NOT DELETE OR CHANGE\NewPR_Integrations"
$repo = "<YourCaliburn>"
Copy-Item "$base\Fusseli_MimicGOAP_FromWarlock_Patch_Export\*" $repo -Recurse -Force
# … PR22 through PR28 in order …
Copy-Item "$base\Caliburn_PR29_GearStorage\*"                  $repo -Recurse -Force
```

## Tests

2 new, **107 total**. They pin the decision (`NeedsTemplatePersist`'s truth table)
and document why. Deliberately **not** source-text assertions — this path needs a
live database, and the round trip below is the real test.

## Verify

```powershell
dotnet build GameServer/GameServer.csproj            # expect 0 errors
dotnet test Tests/Tests.csproj --filter 'FullyQualifiedName~DOL.Tests.Unit.GoapTests|FullyQualifiedName~DOL.Tests.Unit.RoleElectionTests'
# expect 107/107 green
```

Live, in this order:

1. `/mdelete` your three existing bots — their armour data is unrecoverable (the
   uniques were never written), and stale rows would otherwise be restored as husks
2. **Re-spawn a fresh mimic**, `/msave` it, `/mcall` it → **fully armoured**
3. `/mdismiss`, `/mcall` again → still armoured
4. **Restart the server**, `/mcall` again → *this is the one that matters most*, it
   proves the uniques really persisted rather than surviving in memory
5. `/mdelete` → confirm **both** the `inventory` and `itemunique` rows are gone
6. Check the log: `StoreGear: <name> -> <key>, stored N of M item(s)` and
   `Instantiate: <key> restored N of M item(s)`

Useful query afterwards:

```sql
SELECT OwnerID, ITemplate_Id, UTemplate_Id FROM inventory WHERE OwnerID LIKE 'mimic:%';
SELECT Id_nb FROM itemunique WHERE Id_nb LIKE 'Unique_%';
```

Every `UTemplate_Id` in the first result should now have a matching `Id_nb` in the
second.

## Known limits

- **Existing bots must be re-saved.** Rows written before this patch have dangling
  `UTemplate_Id`s and their gear data cannot be repaired — only re-saved.
- `/msave` now writes ~14 `itemunique` rows per bot. They are cleaned up by
  `/mdelete` and by re-saving over the same slot (`AutoDelete = true`), but they do
  live in `itemunique` for as long as the bot is stored — the same as a player who
  keeps a unique.
- `MimicManager` still generates armour in memory for *wild* spawner bots. That is
  deliberate: only player-saved bots need persistence, and persisting at spawn
  would flood the table.
- **Melee kiting, the debuff root cause, the `SkillBase` RA log spam and the
  traceability list remain open** — none affected by this patch.