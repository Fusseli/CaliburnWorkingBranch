# PR25 — Realm-ability archetype fix (apply ON TOP of PR24)

## Apply order (important)

Full-file snapshots — order matters. PR25 stacks on PR24 → PR23 → PR22 → export.

```powershell
$base = "DO NOT DELETE OR CHANGE\NewPR_Integrations"
$repo = "<YourCaliburn>"
Copy-Item "$base\Fusseli_MimicGOAP_FromWarlock_Patch_Export\*" $repo -Recurse -Force
Copy-Item "$base\Caliburn_PR22_KiteFix\*"                    $repo -Recurse -Force
Copy-Item "$base\Caliburn_PR23_RezFix\*"                     $repo -Recurse -Force
Copy-Item "$base\Caliburn_PR24_CorpsePose\*"                 $repo -Recurse -Force
Copy-Item "$base\Caliburn_PR25_RAFix\*"                      $repo -Recurse -Force
```

Full server restart afterwards. The buyer re-runs on spawn, so bots must be
respawned (a restart clears runtime mimics anyway).

## Symptom

Archer mimic bots bought the wrong realm abilities — the melee/tank set
(Mastery of Pain, Mastery of Blocking, Mastery of Parrying, Physical Defense,
Dual Threat) instead of Falcon's Eye and Augmented Dexterity.

## Root cause

`MimicRABuyer.ArchetypeOf` decided "is this bot an archer?" like this:

```csharp
var weapon = mimic.ActiveWeapon;
if (weapon != null && weapon.Item_Type == (int)eInventorySlot.DistanceWeapon)
    return Archetype.Archer;
```

`DbInventoryItem.Item_Type` returns `Template.Item_Type`, an **`eObjectType`**
(a `byte`). `eInventorySlot.DistanceWeapon` is an **`eInventorySlot`** (an `int`).
Two different enums, and their numberings collide:

| constant | value |
|---|---|
| `eInventorySlot.DistanceWeapon` | **13** |
| `eObjectType.Axe` | **13** |
| `eObjectType.Longbow` | 9 |
| `eObjectType.CompositeBow` | 15 |
| `eObjectType.RecurvedBow` | 18 |
| `eActiveWeaponSlot.Distance` | 0x02 |

So the test effectively asked *"is this an axe?"*. A Scout/Hunter/Ranger holding
a Longbow (9) or CompositeBow (15) never matched, fell through to
`Archetype.Melee`, and got the tank ability set. Conversely anything holding an
axe was misclassified as an archer.

`eObjectType` has no dead/corpse-style "ranged" concept; the active weapon **slot**
is what actually means "this bot is shooting a bow".

## What this patch changes

### 1. Pure, testable decision

```csharp
public static Archetype ClassifyArchetype(bool isHealer, bool isListCaster, bool isRangedSlot)
{
    if (isHealer)   return Archetype.Healer;
    if (isListCaster) return Archetype.Caster;
    return isRangedSlot ? Archetype.Archer : Archetype.Melee;
}
```

Taking a plain `bool` for the ranged test makes the type confusion impossible to
reintroduce. Precedence is unchanged from the original: healer, then list caster,
then ranged, then melee.

### 2. `ArchetypeOf` reads the slot and delegates

```csharp
bool isRangedSlot = mimic.ActiveWeaponSlot == eActiveWeaponSlot.Distance;
```

Public signature unchanged, so no caller changes. The old `ActiveWeapon` /
`Item_Type` comparison is **deleted**, not kept alongside — leaving it would have
preserved the Axe→Archer misclassification. A `mimic == null` guard was added
because the old code would have thrown on a null body.

### Why extract instead of testing `ArchetypeOf` directly

`GameLiving.ActiveWeaponSlot` is backed by a `protected` field, and the test
helper `Identity()` returns an uninitialized object, so a test cannot put the
bot in the distance slot. This also matches the convention already used across
this codebase for exactly this reason — `ShouldKeepKiting`, `ShouldKeepFleeing`,
`RealmPointBudget`, `PlanPurchases`, `BuildText` are all pure-for-tests.

## Files

| File | Baseline it was taken from | Change |
|---|---|---|
| `GameServer/custom/MimicNPC/MimicRABuyer.cs` | the export (no patch touches it) | `ClassifyArchetype` + slot-based `ArchetypeOf` |
| `Tests/Unit/GoapTests.cs` | PR24 | 3 new tests |

`MimicRABuyer.cs` is byte-identical between PR19a and the export, so this bug is
in both — PR19a is not a workaround and needs no separate fix.

## Tests

3 new tests, **97 total**:

- `ArchetypeFollowsTheRangedWeaponSlot` — a ranged slot gives `Archer`, anything
  else `Melee`
- `HealerAndCasterWinOverTheRangedSlot` — pins the precedence, so a healer or
  caster holding a bow keeps its own ability set
- `RangedCheckCannotBeConfusedWithAnObjectTypeAgain` — regression guard on the
  exact enum collision that caused this, so it cannot be reintroduced

## Verify

```powershell
dotnet build GameServer/GameServer.csproj            # expect 0 errors
dotnet test Tests/Tests.csproj --filter 'FullyQualifiedName~DOL.Tests.Unit.GoapTests|FullyQualifiedName~DOL.Tests.Unit.RoleElectionTests'
# expect 97/97 green
```

Live: `/mra` on a Scout/Hunter/Ranger. After this patch the list should contain
**Falcons Eye** and **Augmented Dexterity**, and no longer contain Mastery of
Pain / Blocking / Parrying, Physical Defense or Dual Threat. Melee, caster and
healer bots must be unchanged.

## Not changed (deliberately)

- `MimicRABuyer.Priority` itself — untouched. 22 of its 23 KeyNames were
  verified against the `classxrealmability` table in your database dump (149
  distinct keys); only `"Avoid Pain"` is absent, a dormant Atlas entry that can
  never match and is harmless.
- The help text group-restriction wording.
- `Instantiate`'s group-add try/catch and `DismissOwnerBots` logout cost — both
  are unobserved hardening in code the export has already improved.
