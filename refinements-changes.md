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

## 2026-03-26
- Decision: Implement the loadout as a Nova `ListView` backed by `LoadoutItemDefinition` ScriptableObjects.
- Reason: The loadout benefits from data binding and reusable visuals, while the grid still needs persistent cells for placement state.
- Implementation: `LoadoutMenu` manages drag state, preview visuals, and optional budget enforcement; `GridManager` exposes hover/drop state without owning loadout data.
- Fix: `GridManager` now tracks the selected `LoadoutItemDefinition` as the source of truth, so dragging or selecting a new loadout item updates the prefab that gets placed on the grid.
- Implementation: Placement rotation is now owned by `GridManager`, driven by `RotateLeft`/`RotateRight` input, and applied in 90-degree Z-axis steps to both drag preview and placed items.
- Constraint: `LoadoutItemDefinition` now includes `CanRotate`, and `GridManager` ignores rotation input plus forces zero rotation for non-rotatable items such as traps.
