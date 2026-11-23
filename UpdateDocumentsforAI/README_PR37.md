# PR37 — The AI can finally see crowd control (apply ON TOP OF PR36a)

## Symptom

Bots carry snares, stuns and roots and never use them. "Tanks should be snared/rooted"
was not implementable, because the bot did not know it *had* a snare.

## Root cause: the definition of "crowd control" was two spell types and a magnitude

The only definition in the codebase was an inline test in `MimicNPC.SortSpells()`:

```csharp
else if (spell.SpellType == eSpellType.Mesmerize ||
        (spell.SpellType == eSpellType.SpeedDecrease && spell.Value >= 99))
{
    CrowdControlSpells.Add(spell);
}
```

Everything else that controls fell through to `spell.IsHarmful` and was filed under
**`HarmfulSpells`**. So:

| Spell | `SpellType` | Old pool | Reality |
|---|---|---|---|
| AoE mezz | `Mesmerize` (47) | CC | correct |
| Hard root | `SpeedDecrease` (26), Value 100 | CC | correct |
| **Partial snare** | `SpeedDecrease` (26), Value 40-98 | **HarmfulSpells** | invisible |
| **Any stun** | `Stun` (42), `StyleStun` (72), … | **HarmfulSpells** | invisible |
| `DamageSpeedDecrease` (51) and the rest of the family | | **HarmfulSpells** | invisible |

Two things make this worse than it looks:

1. **`eSpellType` has no `Root` and no `Snare` member at all.** A root is a
   `SpeedDecrease` that reached 100%, a snare is one below it. That is why the old test
   needed its `Value >= 99` clause — and also why everything *under* that line was thrown
   away rather than classified.
2. **Only `SpeedDecrease` (26) was checked**, not `DamageSpeedDecrease` (51),
   `UnbreakableSpeedDecrease` (185), `StyleSpeedDecrease` (71) and the rest.

`CanCastCrowdControlSpells` is `CrowdControlSpells != null && Count > 0`, so a bot
holding only a snare reported **false** and was never offered for the CC role in
`MimicGroup.RoleCapabilities`.

## The fix: one classifier, and it is the only definition

`GameServer/custom/MimicNPC/CcClassifier.cs` — a pure static class:

```csharp
public static CcKind Classify(eSpellType type, double value)
{
    if (Mezzers.Contains(type))           return CcKind.Mezzer;
    if (Stuns.Contains(type))             return CcKind.Stun;
    if (SpeedDecreases.Contains(type))
        return value >= ROOT_VALUE_THRESHOLD ? CcKind.Root : CcKind.Snare;
    return CcKind.None;
}
```

`MimicNPC.SortSpells()` now calls `CcClassifier.IsCrowdControl(spell.SpellType, spell.Value)`
in place of the inline test. One definition, one place, and the pool widens to the whole
speed-decrease family plus every stun type.

Pure by design: `Classify(eSpellType, double)` takes primitives so every rule is
unit-testable without a live body. `Classify(Spell)` is the convenience overload.

### Deliberately *not* classified

`Fear` (224), `Silence` (233), `Amnesia` (91), `Nearsight` (80), `Charm` (40) stay
`CcKind.None` for now. Each would flip `CanCastCrowdControlSpells` true for any bot
carrying it and wake the CC goal for that bot. That is a behaviour change and it belongs
to **PR39** (the spell is *chosen* by kind), not to PR37, where the spell is only
classified. Until then those spells stay in `HarmfulSpells` exactly as before.

## Files

| File | Baseline | Change |
|---|---|---|
| `GameServer/custom/MimicNPC/CcClassifier.cs` | — | **new.** `CcKind`, the classifier, `IsAreaControl` |
| `GameServer/custom/MimicNPC/MimicNPC.cs` | PR36 | `SortSpells` CC branch reads the classifier |
| `Tests/Unit/GoapTests.cs` | PR36a | 3 new tests |

> **Why this patch does not touch `MimicBrain.cs`.** An earlier draft of this README
> claimed the `if (spell.CastTime < 5)` test inside `CheckSpells(Offensive)` was
> inverted. **It was not** — the inner `if` was the body of the outer, so it added only
> the fast casts, which is exactly what the comment above it says. The rewrite added
> braces and made the skip explicit, and changed no behaviour. The whole block is
> **deleted** in PR38 anyway, because that random roll was the engine of the
> queued-cast chain. Recorded here because a wrong claim in a patch README is worse than
> no claim.

## Apply order

Full-file snapshots — order matters. PR37 stacks on PR36a → … → export.

```powershell
$base = "DO NOT DELETE OR CHANGE\NewPR_Integrations"
$repo = "<YourCaliburn>"
Copy-Item "$base\Fusseli_MimicGOAP_FromWarlock_Patch_Export\*" $repo -Recurse -Force
# … PR22 through PR36, then PR36a_TestRepair, in order …
Copy-Item "$base\Caliburn_PR37_CcClassifier\*"                $repo -Recurse -Force
```

Full server restart. `CcClassifier.cs` is picked up by the default csproj glob — there is
no `<Compile>` list to update.

## Tests

3 new, **124 total**, all green.

- `CrowdControlIsClassifiedByKindNotByTwoSpellTypes`
- `APartialSpeedDecreaseIsASnareAndAFullOneIsARoot`
- `AreaControlIsToldApartFromSingleTarget`

## Verify

```powershell
dotnet build GameServer/GameServer.csproj            # expect 0 errors
dotnet test Tests/Tests.csproj --filter 'FullyQualifiedName~DOL.Tests.Unit.GoapTests|FullyQualifiedName~DOL.Tests.Unit.RoleElectionTests'
# expect 124/124
```

1. Whisper a mimic `CC` (`MimicNPC.cs:621`) — the list should now be non-empty for a bot
   that previously reported none, and should include its snares and stunts
2. A class that carries a snare only should now be **offered the CC role** by
   `MimicGroup.RoleCapabilities.CcScore()`
3. Melee bots should visibly use short control on their current target

## Known limits

- **PR37 alone does not make CC happen.** It only makes the AI able to see the spells.
  PR38 is what feeds it work — without PR38 the candidate set is still the player-BAF
  list, which is empty bot-against-bot.
- PR37 is a **preparatory step**, not a shippable state on its own. Apply it with PR38.
- `KITE_MIN_OFFENSIVE_RANGE` tuning is still open (PR36).
- The suite is green from PR36a onwards. Before that, 28 of 121 were failing — see
  `README_PR36a.md`.