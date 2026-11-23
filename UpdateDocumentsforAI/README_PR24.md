# PR24 — Corpse model (apply ON TOP of PR23)

## What changed from the first attempt of this patch

The first version of this patch set a bit in the `ObjectUpdate` flags byte. **That
was wrong and it is recorded here so nobody tries it again.**

`SendObjectUpdate` (PacketLib168) assembles one flags byte. Earlier I read only
the middle of the method and concluded `0x04`/`0x08` were free. They are not. The
last two lines of that method assign them:

```csharp
//Dinberg:Instances - zoneskinID for positioning of objects clientside.
flags |= (byte)(((zone.ZoneSkinID & 0x100) >> 6) | ((targetZoneSkinId & 0x100) >> 5));
pak.WriteByte(flags);
```

`& 0x100` selects bit 8; `>> 6` moves it to bit 2 = **`0x04`**, `>> 5` to bit 3 =
**`0x08`**. The byte is fully allocated:

| bit | carries |
|---|---|
| `0x01` | CANTTARGET / STATUE |
| `0x02` | DONTSHOWNAME |
| `0x04` | `zone.ZoneSkinID` bit 8 |
| `0x08` | `targetZoneSkinId` bit 8 |
| `0x10` | underwater |
| `0x20` | flying |
| `0xC0` | realm |

1+2+4+8+16+32+192 = 255. **There is no free bit.**

**Observed on the test server:** with the flag enabled a dead mimic stood for a
second, vanished, reappeared for a second, vanished again, on repeat. Overwriting
`0x04` invents a zone-skin bit, so the client re-resolves the object, drops it and
re-creates it. It is also area-dependent — it only shows where
`ZoneSkinID & 0x100` is clear — and it makes the corpse much harder to click and
rez, so it was worse than cosmetic.

This patch therefore **reverts `PacketLib168.cs` to the export's version
untouched** and delivers the corpse a different way. There is no bit to try.

## Why a model, and why that works

A slain mob looks dead only because the client plays a fall animation **as the
object is removed**. `GameNPC.ProcessDeath` sends no pose packet at all; it ends
with `Delete()` (`GameNPC.cs:2971`) and the client animates the removal. PR16
corpses are deliberately never removed for 60 s, so they never fall. I checked
whether the `GameLivingEvent.Dying` notification was the missing trigger — it is
not: every subscriber of that event is a quest, bounty manager, necromancer pet
or keep hook, and none of them sends a fall animation. There is no server-side
"play death animation" for an NPC that stays in the world.

What *does* exist is a model change, and it needs no packet work:

```csharp
// GameNPC.cs
public override ushort Model
{
    set
    {
        base.Model = value;
        if (ObjectState == eObjectState.Active)
            foreach (GamePlayer player in GetPlayersInRadius(WorldMgr.VISIBILITY_DISTANCE))
                player.Out.SendModelChange(this, Model);
    }
}
```

`Size` broadcasts the same way. A lingering corpse is `Active`, so one assignment
reaches every client. Precedent: `SummonedDjinn` swaps between `VisibleModel` and
`InvisibleModel` at runtime and restores, exactly like this.

## Server properties (category `mimic`, all live-tunable)

| property | default | meaning |
|---|---|---|
| `mimic_corpse_model` | `2262` | model shown while dead. `0` = keep the mimic's own model |
| `mimic_corpse_size` | `0` | size sent with it. `0` = keep the mimic's own size |
| `mimic_corpse_hide_equipment` | `false` | hide worn gear while dead |

Candidates: **2262** (dead player skeleton, chosen by you) and, from the `mob`
table in your own database, three rows named `corpse` with Model **1431** (×2) and
**1433**, all at Size **62**. `mimic_corpse_size` defaults to `0` on purpose — the
authored size of 2262 is unknown, so I left it alone rather than guess twice. Try
`62` if the skeleton looks wrong-sized.

## Will the armour and weapons still show on the corpse?

**Yes, unless you turn on the toggle.** Two facts:

1. `SendModelChange` (0xDB) carries only OID, model and size — no equipment.
2. Every equipment packet is rebuilt from `living.Inventory.VisibleItems` **at
   send time** (`PacketLib1124.cs:60`, `168:1092`, `172:209`, `174:530`, `176:276`,
   `189:34`).

So swapping the model does not tell the client to stop drawing gear: it keeps its
equipment state and re-applies it to the new mesh. Whether that looks right on a
skeleton is client behaviour I cannot verify from the repository, so rather than
assume, `MimicNPCInventory.VisibleItems` is overridden to return nothing while the
owner is dead when `mimic_corpse_hide_equipment` is on:

```csharp
public override ICollection<DbInventoryItem> VisibleItems
{
    get
    {
        if (ServerProperties.Properties.MIMIC_CORPSE_HIDE_EQUIPMENT
            && _owner != null && !_owner.IsAlive)
            return Array.Empty<DbInventoryItem>();
        return base.VisibleItems;
    }
}
```

This is safe: `VisibleItems` is recomputed per packet and nothing caches it
(`MimicNPC.EffectiveOverallAF` sums over it on every access), so the gear returns
by itself the moment `m_isDead` goes false. `GameLivingInventory` has no owner of
its own, so `MimicNPCInventory` now takes the mimic in its constructor — the
single call site in `MimicNPC`'s ctor was updated.

Flip it live and pick whichever looks better. Default `false` so you see the
honest result first.

## Apply order

Full-file snapshots — order matters. PR24 stacks on PR23 → PR22 → export.

```powershell
$base = "DO NOT DELETE OR CHANGE\NewPR_Integrations"
$repo = "<YourCaliburn>"
Copy-Item "$base\Fusseli_MimicGOAP_FromWarlock_Patch_Export\*" $repo -Recurse -Force
Copy-Item "$base\Caliburn_PR22_KiteFix\*"                    $repo -Recurse -Force
Copy-Item "$base\Caliburn_PR23_RezFix\*"                     $repo -Recurse -Force
Copy-Item "$base\Caliburn_PR24_CorpsePose\*"                 $repo -Recurse -Force
```

Full server restart, then set the properties.

## Files

| File | Baseline | Change |
|---|---|---|
| `GameServer/custom/MimicNPC/MimicNPC.cs` | PR23 | `ApplyCorpseModel` / `RestoreLivingModel`, 2 call sites, inventory ctor |
| `GameServer/custom/MimicNPC/MimicNPCInventory.cs` | the export (no patch touched it) | `VisibleItems` override + owner ctor param |
| `GameServer/serverproperty/ServerProperties.cs` | base repo `660394d` | 3 properties, replacing the removed `mimic_corpse_pose` |
| `Tests/Unit/GoapTests.cs` | PR23 | 2 new tests replacing 3 obsolete ones |
| `GameServer/packets/Server/PacketLib168.cs` | the export, **byte-identical** | no change — included only to undo the first attempt |

**On `PacketLib168.cs`:** it is present but its content is exactly the export's, so
applying this patch to a clean export is a no-op for that file. It is kept purely
so that anyone who already applied the first version of PR24 gets the zone-skin
hack reverted instead of having to hunt for it. If you are starting from a clean
export you can delete it from this folder without any effect.

## Verify

```powershell
dotnet build GameServer/GameServer.csproj            # expect 0 errors
dotnet test Tests/Tests.csproj --filter 'FullyQualifiedName~DOL.Tests.Unit.GoapTests|FullyQualifiedName~DOL.Tests.Unit.RoleElectionTests'
```

Then live:

1. Kill a mimic. It should now show the skeleton (or whatever model you set) while
   dead, for the full 60 s window.
2. **No flicker.** If it still vanishes and reappears, a stale
   `mimic_corpse_pose = true` is still set in your config — clear it, that
   property no longer exists in code and the bit is no longer written.
3. Rezz it. It must come back looking like itself, with its armour and weapons,
   and must fight normally.
4. Let a corpse time out. It should despawn normally.
5. Toggle `mimic_corpse_hide_equipment` and confirm the gear appears/vanishes.
6. Confirm the corpse is still clickable and rezzable throughout.
7. Try a duel kill — the model must **not** flash, since duels delete immediately.

## Known limits

- A single model for every race and gender, so all corpses look the same. Fine for
  a corpse, but it is a deliberate trade.
- 2262 is chosen from the client model set, not verified by me. `1431`/`1433` are
  the alternatives from your `mob` table.
- The tests pin the property defaults and the lesson above. They cannot prove what
  the client draws — that is what step 1 is for.
