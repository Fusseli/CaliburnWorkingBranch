# PR36 — Melee kiting + tank debuffing (apply ON TOP of PR35)

## Symptom

Melee classes kite constantly. Valewalkers, Thanes and other casting/melee
hybrids run back and forth instead of fighting.

## Root cause: "can kite" was asking the wrong question

```csharp
bool isCaster = (_body.CanCastHarmfulSpells || _brain.IsHealer) && !_brain.IsBowUser;
```

and `CanCastHarmfulSpells` is only:

```csharp
public bool CanCastHarmfulSpells => (HarmfulSpells != null && HarmfulSpells.Count > 0);
```

**Every mimic has a harmful spell.** Mimsies are built from their class specs, and
a *weapon* spec contributes a short-range harm — so an Armsman passes the test as
readily as a Cabalist. PR26 had already excluded bow users from this gate, which
is why archers stopped kiting and melee did not.

## The fix: ask whether the attack actually reaches past melee

**1. `MimicBrain.IsMeleeArmed`** — promoted from the expression `FlankAvailable`
already used, so flank and kite are the same question and cannot drift:

```csharp
public bool IsMeleeArmed
    => Body?.ActiveWeapon != null
    && Body.ActiveWeapon.Item_Type != (int)eInventorySlot.DistanceWeapon;
```

**2. `MimicBrain.HasRangedOffensive`** — the honest discriminator:

```csharp
public const int KITE_MIN_OFFENSIVE_RANGE = 1500;

public bool HasRangedOffensive
    => Body?.HarmfulSpells != null
    && Body.HarmfulSpells.Any(s => s != null && s.Range >= KITE_MIN_OFFENSIVE_RANGE);
```

A weapon spec's harm fails it; a caster spec's nuke passes it. That is what
separates a Thane from an Armsman — and it needs no class table, because the
mimics already use their real class spells. The threshold is a `const` rather than
a derived value so it can be tuned live without a database query.

**3. The kite gate becomes range-based, and stops when already adjacent**

```csharp
bool isCaster = (_brain.HasRangedOffensive || _brain.IsHealer) && !_brain.IsBowUser;
...
if (pressured && targetLiving != null
    && _body.IsWithinRadius(targetLiving, _body.attackComponent.AttackRange))
    pressured = false;
```

The second condition is your "kite only at range": a hybrid that closed the
distance fights instead of running, and kites once it cannot reach. Pure melee
classes fail the range test outright and never kite at all.

**4. `DebuffAvailable` excludes melee-armed bots too**

PR26 only excluded bow users. A weapon spec can contribute a debuff-flagged
harmful spell, so a tank could spend its tick debuffing rather than holding
threat. `canDebuff` itself is left alone — "can this bot debuff" is still true;
what was wrong was asking a melee bot to spend its time on it.

## Files

| File | Baseline | Change |
|---|---|---|
| `GameServer/custom/MimicNPC/ai/MimicBrain.cs` | PR35 | `IsMeleeArmed`, `HasRangedOffensive`, `KITE_MIN_OFFENSIVE_RANGE` |
| `GameServer/custom/MimicNPC/ReGoap/Sensors/TacticalSensor.cs` | PR35 | kite gate, adjacency rule, debuff gate, shared `meleeArmed` |
| `Tests/Unit/GoapTests.cs` | PR35 | 2 new tests |

> **Baseline note.** An earlier build of this patch based `TacticalSensor.cs` on
> **PR32** on the mistaken assumption that PR33–35 had not touched it. They had — PR35
> changed that file's song call site — so the mistake silently reverted PR33/35's song
> gate and failed to compile (`IsInRegenWindow` missing, `GetBestSong` overload). The
> correct baseline is **PR35 for all three files**, verified by scanning every prior
> patch. Worth knowing if you ever rebase this branch: **compute the baseline per file
> across the whole chain, not against the previous patch.**

## Apply order

Full-file snapshots — order matters. PR36 stacks on PR35 → … → export.

```powershell
$base = "DO NOT DELETE OR CHANGE\NewPR_Integrations"
$repo = "<YourCaliburn>"
Copy-Item "$base\Fusseli_MimicGOAP_FromWarlock_Patch_Export\*" $repo -Recurse -Force
# … PR22 through PR35 in order …
Copy-Item "$base\Caliburn_PR36_MeleeKiting\*"                 $repo -Recurse -Force
```

Full server restart.

## Tests

2 new, **121 total**. Both pin that the two predicates exist, that `IsMeleeArmed`
is shared rather than duplicated, and that the threshold is a tunable const
rather than a value guessed into a comparison.

## Verify

```powershell
dotnet build GameServer/GameServer.csproj            # expect 0 errors
dotnet test Tests/Tests.csproj --filter 'FullyQualifiedName~DOL.Tests.Unit.GoapTests|FullyQualifiedName~DOL.Tests.Unit.RoleElectionTests'
# expect 121/121 green
```

1. **Armsman / Warrior / Blademaster** in a fight → **stays and fights**, no running
2. **Thane / Valewalker** → fights when adjacent, kites when it cannot reach
3. **Cabalist / Cleric / Wizard** (staff) → still kites, unchanged
4. **Archer** → still holds its range, never kites
5. **Tank** → no longer debuffs, holds threat
6. If hybrids kite *too little*, raise `KITE_MIN_OFFENSIVE_RANGE`; if melees still
   leak through, lower it. One constant, no rebuild needed to re-tune.

## Known limits

- The threshold is tuned by judgement, not measured. If your weapon-spec harms turn
  out to reach past 1500, an Armsman could still pass — adjust the const.
- **The debuff root cause is still open.** This stops melee bots *spending their
  time* on debuffs, but not *why a Scout has a debuff-flagged spell at all* — its
  spec list is `BowSpec`/`Shields`/`Stealth` with no debuff line, and the archery
  spells are `SpeedEnhancement`-type. Worth a look.
- **`SkillBase` RA log spam** (15 of 22 realm abilities point at classes that do
  not exist — pre-existing, hits players too, wastes no rank points) and the
  **traceability list** (`MimicNPCInventory.cs` + `ClassSpecs/*.cs` exist only in
  the export) both remain open.