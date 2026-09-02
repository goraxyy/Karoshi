# Karoshi

> *karoshi (過労死) — Japanese: "death by overwork"*

A first-person convenience-store shift simulator built in **Unity 6**. You clock in, keep the shelves stocked, mop up messes, empty the trash, and serve customers before your shift timer runs out — all while an AI supervisor patrols the store, sabotages your shelves, and hunts you down if you slip up.

---

## 📦 About this repository

This repo is a **code-only mirror** of the project. It intentionally does **not** include the Unity scenes, prefabs, third-party asset packs, or `ProjectSettings` — just the C# scripts and the one custom shader under `Assets/!_Project/`, so the gameplay logic can be read, reviewed, or reused without pulling in the full asset library. It is not a drop-in, runnable Unity project on its own.

---

## 🎮 Gameplay Overview

- **Clock in/out** at the time **puncher** to start and end a shift
- **Shop-floor customers** spawn at the entrance, browse random shelves, take stock, queue at the cashier until served, sometimes leave a mess or use the trash bin, then leave
- **Restock shelves** — carry the stock crate and use it on any slot to refill the whole shelf unit; it never runs out
- **Mop up spills** customers leave behind — hold the interact key while carrying the mop; progress shows visibly on the mess itself
- **Empty the trash** — bag up a full bin, then drop the bag into the container out back
- **Put tools back** by looking at their snap point and interacting, from anywhere
- **Shift tasks** (mopping, trash, restocking) are tracked live and shown in an on-screen checklist (toggle with **F**); a shift can't be clocked out until they're all clear
- **Avoid the AI supervisor**, who patrols the store, randomly knocks stock off shelves, and chases you on sight
- **Manage your Burnout meter** — time and being chased drain it; coffee recovers it

---

## 🧠 Systems

### Enemy AI
A 4-state finite state machine (`EnemyAI.cs`):

| State | Behavior |
|-----------|--------------------------------------------------------------|
| `Patrol` | Walks between waypoints; randomly decides to sabotage shelves |
| `Sabotage` | Finds the nearest stocked shelf and ejects an item from it |
| `Chase` | Pursues the player using NavMesh pathfinding |
| `Search` | Moves to the player's last known position after losing sight |

Uses a cone-based vision system with raycast occlusion — hide behind a shelf and it can't see you.

### Customers (`Customernpc.cs`, `CustomerSpawner.cs`)
NavMesh-driven shoppers with their own routine: enter → visit a few random shelf points → take an item if one's in reach (dropping mess behind them some of the time) → queue at the cashier and wait for the player to serve them → maybe detour to the trash can → exit and despawn. Line-of-sight and path-completeness checks keep them from interacting with things through walls.

### Shelves & stocking (`ShelfSlot.cs`, `ShelfUnit.cs`, `StockCrate.cs`)
Each shelf is a unit of individual slots. A `ShelfUnit` tracks how many of its slots are empty and highlights itself while understocked. The stock crate is carried in the inventory; while it's the item in hand, interacting with any slot refills that entire shelf, and the crate is never used up.

### Cleaning (`Dirt.cs`)
Spills are capped per shift and require the mop — implemented via `IHoldInteractable`, a hold-to-complete interaction with visible shrink/fade progress on the mess.

### Trash (`Trashcan.cs`, `TrashBag.cs`, `TrashContainer.cs`)
Bins fill as customers use them. Interacting with a non-empty bin bags it up into a carryable `TrashBag`; the job is only finished once that bag is physically dropped into the container out back, whose trigger volume swallows it.

### Tools & snap points (`ItemHome.cs`, `ToolSnapPoint.cs`)
The mop and the stock crate are ordinary inventory items — droppable anywhere — but each has a home. Looking at its snap point and interacting recalls the tool from the floor or straight out of the inventory. The spot shows a marker only while its tool is missing, and its trigger switches off while the tool is home so it never blocks picking the tool back up.

### Shifts & tasks (`ShiftManager.cs`, `TaskManager.cs`, `Puncher.cs`)
`ShiftManager` runs the clock-in/clock-out lifecycle and shift timer. `TaskManager` mixes one counted quota — mop N spills, growing each shift — with two live state checks: shelves all stocked, and all trash disposed of. The state tasks read the world directly rather than a running tally, so they un-check themselves the moment a customer empties a shelf or uses a bin. Clocking out is blocked until everything reads clear.

### Interaction & highlighting (`PlayerInteract.cs`, `HighlightInteractable.cs`, `OutlineHighlight.cs`)
A shared `IInteractable` / `IHoverable` / `IHoldInteractable` interface set drives all player interactions. `OutlineHighlight` draws an inverted-hull yellow outline on lookat, either per-mesh (items, customers) or as a single bounding box (shelves, fixtures) depending on the object's geometry.

### Inventory & tools (`CarrySlot.cs`, `PlayerTools.cs`, `Inventoryui.cs`)
Shelf-stock items live in a 4-slot `CarrySlot` inventory with an on-screen icon per slot; bulkier one-at-a-time tools (mop, trash can) go through `PlayerTools`, which handles holding and returning them to their original transform.

---

## 🕹️ Controls

| Action | Key |
|------------|----------------------|
| Move | `WASD` |
| Sprint | `Left Shift` |
| Crouch | `Left Ctrl` |
| Jump | `Space` |
| Interact / hold-to-use | `E` |
| Drop held item/tool | `Q` |
| Toggle task list | `F` |
| Switch inventory slot | `1`–`4` / scroll wheel |
| Look | Mouse |

---

## 🏗️ Script Layout

```
Assets/!_Project/
├── _Core/Scripts/Runtime/
│   ├── BurnoutSystem.cs
│   ├── ShiftManager.cs
│   └── TaskManager.cs
├── _Game/
│   ├── AI/Scripts/
│   │   └── EnemyAI.cs
│   ├── Characters/
│   │   ├── Scripts/
│   │   │   ├── Customernpc.cs
│   │   │   ├── CustomerSpawner.cs
│   │   │   └── OutlineHighlight.cs
│   │   └── Shaders/
│   │       └── CustomerOutline.shader
│   ├── Items/Scripts/
│   │   ├── Item.cs
│   │   ├── ItemHome.cs                 # tools that return to a fixed spot
│   │   ├── PickupInteractable.cs
│   │   └── ToolSnapPoint.cs            # the spot itself; E here recalls the tool
│   ├── Level/
│   │   ├── Editor/
│   │   │   └── ShelfPrefabBuilder.cs   # editor tool: generates shelf model/stocked prefab variants
│   │   └── Scripts/
│   │       ├── AutoDoubleDoor.cs
│   │       ├── CoffeeMachine.cs
│   │       ├── Dirt.cs
│   │       ├── HighlightInteractable.cs
│   │       ├── HingeDoor.cs
│   │       ├── OneShotAudio.cs
│   │       ├── ParentMaterialController.cs
│   │       ├── Puncher.cs
│   │       ├── ShelfSlot.cs
│   │       ├── ShelfUnit.cs
│   │       ├── StockCrate.cs
│   │       ├── TrashBag.cs
│   │       ├── TrashContainer.cs       # trigger volume that swallows bags
│   │       └── Trashcan.cs
│   └── Player/Scripts/
│       ├── CarrySlot.cs
│       ├── Inventoryui.cs
│       ├── PlayerInteract.cs
│       ├── PlayerMotor.cs
│       ├── PlayerTools.cs
│       └── TaskListUI.cs
```

---

## ⚙️ Tech Stack

- **Engine:** Unity 6 (6000.x)
- **Language:** C#
- **AI Navigation:** Unity NavMesh / NavMeshAgent (`Unity.AI.Navigation`)
- **Rendering:** URP (Universal Render Pipeline), custom outline shader
- **UI:** TextMeshPro

---

## 📋 Roadmap

- [ ] Score/results screen between shifts
- [ ] Sound design pass
- [ ] More level layouts
- [ ] Player-facing penalty when caught by the supervisor
