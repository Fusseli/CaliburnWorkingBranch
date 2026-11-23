# PR26 — Bow-user combat behaviour + archer spawn error

Fixes three things reported from the test server. All three are the same class of
mistake: **asking a question of the wrong thing.** In each case a proxy was used
where the real signal was available, and in two of them it was the same enum
collision that PR25 had just fixed elsewhere.

## Apply order

Full-file snapshots — order matters. PR26 stacks on PR25 → PR24 → PR23 → PR22 → export.

```powershell
$base = "DO NOT DELETE OR CHANGE\NewPR_Integrations"
$repo = "<YourCaliburn>"
Copy-Item "$base\Fusseli_MimicGOAP_FromWarlock_Patch_Export\*" $repo -Recurse -Force
Copy-Item "$base\Caliburn_PR22_KiteFix\*"                    $repo -Recurse -Force
Copy-Item "$base\Caliburn_PR23_RezFix\*"                     $repo -Recurse -Force
Copy-Item "$base\Caliburn_PR24_CorpsePose\*"                 $repo -Recurse -Force
Copy-Item "$base\Caliburn_PR25_RAFix\*"                      $repo -Recurse -Force
Copy-Item "$base\Caliburn_PR26_ArcheryCombat\*"              $repo -Recurse -Force
```

Full server restart. Mimics must be respawned (a restart clears them anyway).

---

## 1. Melee bots and archers were kiting

**Reported:** melee-class mimics and archers showed `KiteGoal` and ran back and
forth while fighting. Casters kited correctly.

**Cause.** `TacticalSensor` decided who kites like this:

```csharp
bool isCaster = _body.CanCastHarmfulSpells || _brain.IsHealer;
```

and `GameNPC.CanCastHarmfulSpells` is only:

```csharp
public bool CanCastHarmfulSpells
    => (HarmfulSpells != null && HarmfulSpells.Count > 0);
```

"Has *any* harmful spell" is not "can cast at range". It is true for **every
archer** — a bow is an `eSpellType.Archery` spell, so it lands in the harmful pool
— and for **melee bots** that happen to hold a harm. Both therefore qualified as
"casters" and kited, then went back and forth because a melee bot re-closing on a
melee enemy is the worst possible kite target.

The sensor's own comment already stated the intent: *"Melee/tanks never kite:
without ranged casts, running only sheds aggro control."* The code just never
tested it.

**Fix.** One honest signal, `MimicBrain.IsBowUser`, reading the active weapon
**slot**, and bow users excluded from the kite gate:

```csharp
public bool IsBowUser
    => Body != null && Body.ActiveWeaponSlot == eActiveWeaponSlot.Distance;
```

```csharp
bool isCaster = (_body.CanCastHarmfulSpells || _brain.IsHealer) && !_brain.IsBowUser;
```

`KiteGoal`, `KiteToSafety` and `ShouldKeepKiting` are all untouched, so the goal
and the action stay in agreement — the gate lives in the sensor, which is where
PR14 originally put it.

**Deliberately not fixed here: melee kiting.** Excluding bow users leaves a melee
bot holding a harm still able to kite. Fixing that properly needs a real "is this a
ranged caster" test, which means classifying the equipped weapon by its
`eObjectType` — the same lesson as PR25, and worth its own patch so each change is
independently testable.

---

## 2. Archers were debuffing instead of shooting

**Reported:** archers frequently showed `DebuffGoal`.

**They shouldn't.** An archer is physical damage.

**Cause.** `DebuffAvailable` only asked whether the bot holds a debuff-flagged
spell:

```csharp
bool canDebuff = _body.HarmfulSpells?.Any(s => s != null && s.IsDebuff) == true;
```

If an archer's pool contains anything flagged `IsDebuff`, the goal went live. And
`DebuffPriorityGoal` scores **3.0** as the main assist (0.9 otherwise), which
outranks `ArrowTypeGoal` at 2.5 and `DealDamageGoal` at 2.0 — so the archer stopped
shooting to apply a debuff, and also outranked its own arrow-type selection.

**Fix.** Bow users are excluded outright:

```csharp
SetBool(DebuffAvailable, canAct && !_brain.IsBowUser && targetValid && canDebuff
    && !targetDebuffed && !_body.IsSilenced && !_brain.IsFleeing);
```

`DebuffPriorityGoal` itself is unchanged, so non-archer debuffers keep their exact
current behaviour and priorities.

---

## 3. `Inventory.AddItem -> Destination slot is not empty (13)` on archer spawn

**Reported:** this error, sometimes, with a stack through
`MimicNPC..ctor → SetRanged → MimicEquipment.SetRangedWeapon → AddItem`.

Slot 13 is `eInventorySlot.DistanceWeapon`.

**Cause.** `SetRanged()` loops over **every** ability the bot owns with no `break`
out of the loop:

```csharp
foreach (Ability ability in GetAllAbilities())
    switch (ability.KeyName)
    {
        case Weapon_Longbows:  MimicEquipment.SetRangedWeapon(this, eObjectType.Longbow); ...
        case Weapon_Archery when !ALLOW_OLD_ARCHERY:   // the archery redesign
            ... MimicEquipment.SetRangedWeapon(this, bow); ...
    }
```

Under `allow_old_archery = false` a bot can hold both `Weapon_Archery` **and** the
old bow ability, so **two** cases match and a bow is added twice. The second call
lands on an occupied distance slot. It fires per archer spawn, which is why it
looked intermittent — it only affects archer spawns.

`Slot.DISTANCEWEAPON` and `eInventorySlot.DistanceWeapon` are both 13, so the
`(eInventorySlot)item.Item_Type` cast in `MimicEquipment.AddItem` is correct; the
slot really is just already full. (Verified rather than assumed.)

**Fix.** Guard on the slot, not on the ability list, so every combination is
covered including the old-flag cases:

```csharp
private void SetRangedWeaponOnce(eObjectType bow)
{
    if (bow == eObjectType.GenericItem)
        return;
    if (Inventory.GetItem(eInventorySlot.DistanceWeapon) == null)
        MimicEquipment.SetRangedWeapon(this, bow);
    SwitchWeapon(eActiveWeaponSlot.Distance);
}
```

All four bow cases now go through it. The first bow placed is left alone — it was
already correct, which is why archer **damage and abilities worked fine** despite
the error. Only the redundant second add is suppressed, and the error stops.

`MimicEquipment`/`MimicManager.cs` is deliberately not touched: the guard belongs in
`MimicNPC`, which is the class that owns the ctor ordering, and it avoids changing
a shared base file that other NPC scripts use.

---

## Files

| File | Baseline | Change |
|---|---|---|
| `GameServer/custom/MimicNPC/ai/MimicBrain.cs` | PR23 | `IsBowUser` |
| `GameServer/custom/MimicNPC/ReGoap/Sensors/TacticalSensor.cs` | PR23 | bow users out of kiting and debuffing |
| `GameServer/custom/MimicNPC/MimicNPC.cs` | PR24 | `SetRangedWeaponOnce` guard |
| `Tests/Unit/GoapTests.cs` | PR25 | 3 new tests |

## Tests

3 new tests, **100 total**:

- `BowUsersAreNotKitingOrDebuffingCandidates` — pins that the exclusion is a sensor
  gate, so `KiteGoal` and the action stay in agreement
- `DebuffGoalIsGatedByTheSensorNotTheGoal` — a non-archer debuffer keeps scoring
  3.0 and still outranks damage, so nothing regressed
- `BowSlotIsTheOnlyReliableRangedSignal` — regression guard on the `Axe` ==
  `DistanceWeapon` collision, so this proxy can never come back

## Verify

```powershell
dotnet build GameServer/GameServer.csproj            # expect 0 errors
dotnet test Tests/Tests.csproj --filter 'FullyQualifiedName~DOL.Tests.Unit.GoapTests|FullyQualifiedName~DOL.Tests.Unit.RoleElectionTests'
# expect 100/100 green
```

Live:

1. Spawn a Scout/Hunter/Ranger. The slot-13 error must be gone from the log.
2. Archer in a fight: `/mgoap status` should show an offensive goal
   (`CastOffensiveSpell`, `ChooseArrowType`, `EngageTarget`), never
   `ApplyDebuff`. It should hold its range and not run.
3. Melee mimic in a fight: no `KiteGoal`, no back-and-forth.
4. Caster (e.g. a Cabalist or Heretic) under ranged pressure: **must still kite** —
   this is the regression to watch, and it is why PR22 fixed the hop loop.
5. Healer: unchanged.
6. Confirm archer damage is unchanged from before this patch.

## Known limits

- **Melee kiting is not fully fixed** — only bow users are excluded. See item 1.
- `MimicRABuyer.ArchetypeOf` and this patch now both key off the active weapon
  slot. That is deliberate duplication rather than a shared helper, because the
  buyer runs on spawn/realm-rank and the sensor runs every think; if a third caller
  ever needs it, promote it to a shared predicate.
- The `SkillBase` "Could not Instanciate Ability" warnings are **not** addressed
  here: they are a pre-existing base-repo/DB mismatch (the `ability.Implementation`
  column points at classes that do not exist), they affect players identically, and
  the RA buyer already skips them because `GetClassRealmAbilities` filters to
  `RealmAbility`. No rank points are wasted.
