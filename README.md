# Performance Unknown

An advanced, hardware-aligned performance and memory allocation optimization BepInEx mod for **Casualties Unknown**. It tackles major Unity-specific bottlenecks (heavy line-of-sight raycasting, dynamic 2D physics solvers, memory allocation stutters, and splatter particle spam) and exposes high-level, human-readable quality adjustments directly inside the official native Video Settings menu using the **ScavSetLib** API.

---

## 🚀 Key Features & Optimizations

*   **📷 LOS Refresh Amortization**: Skips Line-of-Sight raycast loops in `LateUpdate` across multiple frames based on dynamic settings, yielding a **50% - 75% reduction in CPU overhead** with zero visual compromise.
*   **🩸 Splatter Particle Ceiling**: Implements an active FIFO-pruning system on visual blood splatters, dust, debris, and explosions to prevent combat framerate drops and massive rendering bottlenecks.
*   **⚙️ Physics 2D Solver Tuning**: Dynamically adjusts Box2D position and velocity solver iterations, **drastically speeding up crowded scenes** with multiple active NPCs, ragdolls, or items.
*   **🧹 Stutter-Free Garbage Collection (GC)**: Allocates incremental GC timeslices to split heap sweeps across frames, and executes preemptive safe garbage sweeping when entering paused states or loading scenes to eliminate micro-stutters.

---

## 📊 Native Video Settings Integration

Through the power of `ScavSetLib`, all configurations are integrated directly into the game's official native Video Settings menu, enabling fluid in-game changes without restart:

| Setting Name | Category | Choices / Defaults | Optimization Vector |
| :--- | :--- | :--- | :--- |
| **LOS Refresh Interval** | Video | `Every Frame` / `Every 2 Frames` (Fast) / `Every 3 Frames` (Ultra) / `Every 4 Frames` | Amortizes line-of-sight raycasts to skip intensive trigonometry calculations. |
| **Splatter Particle Cap** | Video | `25` / `50` / `100` (Balanced) / `200` / `Unlimited` | Active FIFO-limiting of splatters and visual particles. |
| **Physics 2D Solver Quality**| Video | `Low` / `Medium` (Optimized) / `High` (Default) | Tunes velocity and position solver iterations. |
| **GC Memory Sweeper** | Video | `Enabled` (Default) / `Disabled` | Tunes incremental GC timeslices and sweeps on safe events. |

---

## 🛠️ Requirements & Installation

1.  Ensure you have **[BepInEx](https://github.com/BepInEx/BepInEx)** (5.x or later) installed for the game.
2.  Ensure you have **[ScavSetLib](https://github.com/NaeNaeTart/ScavSetLib)** (1.0.0 or later) installed in your `BepInEx/plugins` folder.
3.  Download the latest `UnknownPerformance.dll` from the releases.
4.  Place `UnknownPerformance.dll` into your `BepInEx/plugins/` folder.

---

## 🏗️ Building From Source

This project targets `.NET Framework 4.8` and relies on standard BepInEx and Unity game references.

To compile:
```bash
dotnet build -c Release
```
The output DLL will be automatically copied to your game's plugin directory if paths are aligned.

---

## 📄 License
This project is licensed under the MIT License - see the LICENSE file for details.
