# Karoshi

> *karoshi (過労死) — Japanese: "death by overwork"*

A first-person stealth/simulation game built in **Unity 6** where you play as an overworked convenience store employee trying to survive the night shift — while an AI supervisor patrols the aisles, randomly sabotages your shelves, and hunts you down the moment you step out of line.

---

## 🎮 Gameplay Overview

You're trapped in a fluorescent-lit convenience store. Your job: stock the shelves before your shift ends. The catch: your manager isn't human, and they *will* find you.

- **Stock shelves** by picking up items and placing them in designated shelf slots
- **Avoid the AI supervisor** who patrols the store, randomly knocks products off shelves, and chases you on sight
- **Manage your Burnout meter** — being chased drains it faster, and running out means game over
- **Interact with the environment** — grab a coffee to recover, sneak through automatic doors, and crouch behind aisles to hide

---

## 🧠 AI System

The enemy AI operates on a **4-state finite state machine**:

| State | Behavior |
|-----------|--------------------------------------------------------------|
| `Patrol` | Walks between waypoints; randomly decides to sabotage shelves |
| `Sabotage` | Finds the nearest stocked shelf and ejects the item from it |
| `Chase` | Pursues the player using NavMesh pathfinding |
| `Search` | Moves to the player's last known position after losing sight |

The AI uses a **cone-based vision system** with raycasting for occlusion — if you're behind a shelf, it can't see you.

---

## 🕹️ Controls

| Action | Key |
|------------|----------------------|
| Move | `WASD` |
| Sprint | `Left Shift` |
| Crouch | `Left Ctrl` |
| Jump | `Space` |
| Interact | `E` (pick up / place items) |
| Look | Mouse |

---

## 🏗️ Project Structure

```
Assets/
├── !_Project/
│   ├── _Core/        # Game manager, scenes, core systems
│   ├── _Game/
│   │   ├── AI/       # EnemyAI state machine + NavMesh logic
│   │   ├── Player/   # PlayerMotor, PlayerInteract, CarrySlot, InventoryUI
│   │   ├── Items/    # Interactable item scripts
│   │   ├── Level/    # ShelfSlot, doors, CoffeeMachine, lighting
│   │   ├── Audio/    # Sound management
│   │   ├── UX/       # HUD and UI
│   │   └── VFX/      # Visual effects
│   └── _Art/         # Custom art assets
├── BrokenVector/               # Third-party asset
├── CrowArt - PBR Cardboard Box/
├── Low Poly Cartoon Food and Groceries Pack/
├── Pandazole_Ultimate_Pack/
└── Scalable Grid Prototype Materials/
```

---

## ⚙️ Tech Stack

- **Engine:** Unity 6 (6000.x)
- **Language:** C#
- **AI Navigation:** Unity NavMesh / NavMeshAgent
- **Input:** Unity Input System
- **Physics:** CharacterController + SphereCast for crouch detection
- **Rendering:** URP (Universal Render Pipeline)

---

## 🚀 Getting Started

1. Clone the repository:
   ```bash
   git clone https://github.com/goraxyy/Karoshi.git
   ```
2. Open the project in **Unity 6** (6000.x or later)
3. Open the main scene in `Assets/!_Project/_Core/Scenes/`
4. Press **Play**

> ⚠️ This project requires Unity 6. Opening in earlier versions may cause compatibility issues.

---

## 📋 Roadmap

- [ ] Burnout/penalty system when caught
- [ ] Multiple shifts with increasing difficulty
- [ ] Score/timer display on HUD
- [ ] Sound design pass
- [ ] More level layouts

---

## 📄 License

MIT — see [LICENSE](LICENSE) for details.
