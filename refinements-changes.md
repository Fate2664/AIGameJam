# Refinements & Changes Log

Purpose: Running log of scope shifts and design decisions during the jam.

## 2026-03-25
- Decision: Keep scope to a playable 3-zone prototype focused on the switching loop.
- Scope: Minimalist visuals, limited trap/enemy types, short session length prioritized over feature breadth.
- Decision: Build the placement grid as a fixed Nova UI layout with one persistent cell per grid position instead of a recycled `ListView`.
- Reason: For jam scope, persistent cells are simpler for click placement and child item visuals than virtualized item views.
- Implementation: `GridManager` owns placement state and item instances; `CellVisuals` only handles visual state and an item anchor.
- Implementation: Added `CellGridGenerator` to build evenly spaced cell layouts from a prefab and skip reserved rectangular areas inside the generated grid.
- Fix: `CellGridGenerator` now writes Nova layout positions on each spawned cell so the cells do not stack at one transform position.
- Fix: `CellGridGenerator` now adds a UIBlock to its generated grouping object so Nova gesture events can reach the spawned cells through a connected hierarchy.
- Implementation: Replaced rectangle-only exclusions with per-cell exclusions and an inspector grid so individual cells can be toggled off before play.
- Fix: Loadout budget enforcement now blocks click placement as well as drag placement, and budget is deducted from every successful placement through the grid placement event.

## 2026-03-26
- Decision: Implement the loadout as a Nova `ListView` backed by `LoadoutItemDefinition` ScriptableObjects.
- Reason: The loadout benefits from data binding and reusable visuals, while the grid still needs persistent cells for placement state.
- Implementation: `LoadoutMenu` manages drag state, preview visuals, and optional budget enforcement; `GridManager` exposes hover/drop state without owning loadout data.
- Fix: `GridManager` now tracks the selected `LoadoutItemDefinition` as the source of truth, so dragging or selecting a new loadout item updates the prefab that gets placed on the grid.
- Implementation: Placement rotation is now owned by `GridManager`, driven by `RotateLeft`/`RotateRight` input, and applied in 90-degree Z-axis steps to both drag preview and placed items.
- Constraint: `LoadoutItemDefinition` now includes `CanRotate`, and `GridManager` ignores rotation input plus forces zero rotation for non-rotatable items such as traps.
- Implementation: Added a modular `WaveController` with a public `StartNextWave()` entry point so UI can start enemy waves without coupling spawn logic into the grid system.
- Implementation: `GridManager` now supports a placement phase toggle; when a wave starts it hides only empty cell backgrounds, keeps occupied cells visible with placed items intact, and blocks further cell interaction until placement resumes.
- Fix: Wave phase now hides the background for occupied cells as well, so only the placed item visuals remain visible during combat while the placement grid fully disappears.
- Implementation: Added a modular enemy authoring pipeline built around `EnemyDefinition` ScriptableObjects, `EnemyStats`, and the `IEnemy`/`IEnemyDefinition` abstractions so enemy data is decoupled from wave spawning.
- Implementation: Added a reusable `EnemyActor` runtime component and generic enemy prefab so base, tank, and boss enemies can share behaviour while swapping stats and sprites through assets.
- Implementation: `WaveController` now supports definition-driven spawning, uses a real scene spawn point by default for prefab-backed enemies, and the scene is configured with example base, tank, and boss waves.
- Implementation: `EnemyAI` now reads movement speed from the active enemy definition when available, keeping movement tuning inside the data asset instead of hardcoded per-scene values.
- Implementation: Added an `EnemyMovementProfile` to enemy definitions so each enemy type can have its own movement sway and noise settings without hardcoding them in the AI.
- Fix: `EnemyAI` now applies a subtle per-enemy steering wobble while still moving at constant speed, which breaks up the straight-line look without losing pathfinding.
- Fix: Enemy colliders now ignore each other at runtime while remaining solid against the rest of the world, so enemies can overlap instead of queueing on each other.
- Implementation: `WaveController` now supports collider-driven spawn perimeters, so each spawn can use a `Collider2D` area and enemies will spawn at random valid points inside that perimeter.
- Implementation: `WaveController` auto-discovers a `Collider2D` on the assigned spawn transform as a convenience, and the current scene `SpawnPoint` now includes a trigger `BoxCollider2D` perimeter for enemy spawning.
- Implementation: `WaveController` spawn entries now support multiple `Collider2D` spawn areas, with each enemy randomly selecting one valid area before sampling a spawn position inside it.
