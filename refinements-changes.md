# Refinements & Changes Log

Purpose: Running log of scope shifts and design decisions during the jam.

## 2026-03-25
- Decision: Keep scope to a playable 3-zone prototype focused on the switching loop.
- Scope: Minimalist visuals, limited trap/enemy types, short session length prioritized over feature breadth.
- Decision: Build the placement grid as a fixed Nova UI layout with one persistent plot per cell instead of a recycled `ListView`.
- Reason: For jam scope, persistent cells are simpler for click placement and child item visuals than virtualized item views.
- Implementation: `GridManager` owns placement state and item instances; `PlotVisuals` only handles visual state and an item anchor.
