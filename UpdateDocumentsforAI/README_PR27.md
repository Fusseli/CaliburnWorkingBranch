# PR27 — Archer kiting fix (apply ON TOP of PR26)

## Apply order

Full-file snapshots — order matters. PR27 stacks on PR26 → PR25 → PR24 → PR23 → PR22 → export.

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
```

Full server restart. Mimics respawn on restart anyway.

## Symptom

PR26 stopped melee mimics kiting, but **archers still showed `KiteGoal` and ran
back and forth while in melee.** Out of combat — "when I go backwards they follow
me without this flickering" — they behaved.

## Root cause: PR26 asked a momentary question

PR26 excluded bow users from kiting with:

```csharp
public bool IsBowUser => Body != null && Body.ActiveWeaponSlot == eActiveWeaponSlot.Distance;
```

`ActiveWeaponSlot` is **a momentary action state, not a loadout fact.** This class
rewrites it constantly:

```csharp
// MimicBrain.cs - before every archery cast, switch to the bow
if (spell.SpellType == eSpellType.Archery &&
    Body.ActiveWeaponSlot != eActiveWeaponSlot.Distance)
    Body.SwitchWeapon(eActiveWeaponSlot.Distance);
```

…and three paths switch it back to `Standard`/`TwoHanded` (the melee paths, and
the puller-reset path in `CheckPuller`). So an archer's slot flips several times
inside one fight. `TacticalSensor` samples it once per think, and got a coin
flip — which is precisely the reported split:

- **in melee** → melee paths had run, slot is `Standard`/`TwoHanded` → `IsBowUser`
  false → it kites → back-and-forth
- **out of combat** → the archer stayed in `Distance` → `IsBowUser` true →
  correctly excluded, follows cleanly

A value that changes within a single fight cannot answer a persistent question.

## The fix: ask about the loadout, not the current action

```csharp
public bool IsBowUser
    => Body != null
    && Body.Inventory != null
    && Body.Inventory.GetItem(eInventorySlot.DistanceWeapon) != null;
```

**This exact expression was already in this file**, 160 lines above, in
`PerformPull`:

```csharp
if (Body.Inventory.GetItem(eInventorySlot.DistanceWeapon) != null)
{
    Body.SwitchWeapon(eActiveWeaponSlot.Distance);   // ranged pull
}
else { /* melee pull */ }
```

So the correct "is this a ranged attacker" test has been here all along — used to
decide a *ranged pull* — and PR26 added a second, worse one next to it.
`PerformPull` now calls `IsBowUser`, so the class defines the term once.

PR26's `MimicNPC.SetRangedWeaponOnce` uses the same inventory test independently.
That one is left alone deliberately: it runs inside the constructor, while the
inventory is still being populated, and it lives in `MimicNPC` rather than
`MimicBrain`. Consolidating it would add a file and a constructor-order dependency
for no behavioural gain.

## Bonus fix: an always-true condition in `CheckPuller`

The reason the slot was flapping in the first place. `MimicBrain.cs:1466`:

```csharp
// before
if ((CharacterClass.ID != (int)eCharacterClass.Hunter ||
     CharacterClass.ID != (int)eCharacterClass.Ranger ||
     CharacterClass.ID != (int)eCharacterClass.Scout) &&
    ClassType != eClassType.ListCaster)

// after
if (CharacterClass.ID != (int)eCharacterClass.Hunter &&
    CharacterClass.ID != (int)eCharacterClass.Ranger &&
    CharacterClass.ID != (int)eCharacterClass.Scout &&
    ClassType != eClassType.ListCaster)
```

`A != Hunter || A != Ranger || A != Scout` is **always true** — no class can fail
all three comparisons simultaneously. The archer exclusion was dead code, so an
archer *puller* was switched to a melee slot on pull reset and had to switch back
to `Distance` before it could shoot. Pre-existing base-repo bug, not caused by any
patch in this chain.

Removing the churn is the actual root cause; the `IsBowUser` change makes the
symptom impossible even if some other path moves the slot later.

## Also fixed as a side effect

Crossbow and thrown-weapon mimics had the same defect for the same reason, since
`eSpellType.Archery` is not the only thing that puts a weapon in the distance slot.
They now also stop kiting and stop debuffing — correct, since they are physical
ranged attackers that want to hold their range.

## Files

| File | Baseline | Change |
|---|---|---|
| `GameServer/custom/MimicNPC/ai/MimicBrain.cs` | PR26 | `IsBowUser` reads the inventory; `PerformPull` reuses it; `CheckPuller` `\|\|` → `&&` |
| `Tests/Unit/GoapTests.cs` | PR26 | 3 new tests |

## Tests

3 new tests, **103 total**:

- `RangedAttackerTestReadsTheInventoryNotTheActiveSlot` — the predicate returns
  false with no inventory, and stays public so both callers share it
- `ActiveWeaponSlotIsNotAStableRangedSignal` — pins the slot value and documents
  why it cannot be used
- `PullerResetExcludesArchers` — truth table for the corrected archer exclusion

## Verify

```powershell
dotnet build GameServer/GameServer.csproj            # expect 0 errors
dotnet test Tests/Tests.csproj --filter 'FullyQualifiedName~DOL.Tests.Unit.GoapTests|FullyQualifiedName~DOL.Tests.Unit.RoleElectionTests'
# expect 103/103 green
```

Live — the archer case is the one that matters:

1. **Archer in melee.** No `KiteGoal`, no back-and-forth. `/mgoap status` shows an
   offensive goal. Watch it for a full fight, not just the opening — the old bug
   only appeared while the melee paths were running.
2. **Archer out of combat.** Still follows, still no kiting. This worked before too;
   it must not regress.
3. **Archer puller.** On `/mpull` and on pull reset it must not visibly change to a
   melee weapon. If the `&&` fix works it keeps the bow.
4. **Crossbow / thrown mimics**, if you use them — same expectation as archers.
5. **Casters must still kite.** That is the regression to watch, and the reason
   PR22 exists. If a caster stops kiting, `IsBowUser` is wrongly true for it.
6. **Armsman / Warrior** must still not kite (PR26 behaviour, unchanged).

## Known limits

- **Melee kiting is still not fixed.** A Warrior holding a harm still passes
  `CanCastHarmfulSpells` and kites. That needs a real "is this a ranged *caster*"
  test, which is a separate patch.
- `CanCastHarmfulSpells` remains a poor proxy for "can cast at range" — it is
  `HarmfulSpells.Count > 0`. It is now only used where a melee mimic is a genuine
  candidate, but it is the next thing to replace.
- The debuff *root cause* is still unexplained. A Scout's spec list is
  `BowSpec`, weapon, `Shields`, `Stealth` with no debuff line, and the archery
  spells are all `SpellType.Archery`/`Siege Shot` with no debuffs — so
  `HarmfulSpells.Any(s => s.IsDebuff)` should already have been false for an
  archer. This patch's exclusion is a **behavioural** guard, not a root-cause fix.
  Watch whether the same thing appears on a Warrior or Thane.