# CORE OVERLOAD - 3-Day Jam Plan (Theme: Switch)

## Goals
- Deliver a playable prototype where the player must switch between three zones to survive simultaneous waves.
- Prove the core tension: limited attention + manual trap activation + escalating pressure.
- Keep scope tight: minimalist visuals, few trap/enemy types, short session length.

## Milestones

### Day 1 - Core Systems (March 25)
- Set up project, scene flow, and input.
- Build three zones and switching mechanics.
- Implement basic enemy spawn and movement.
- Implement one trap type that requires manual activation.
- Run a full wave end-to-end (spawn -> combat -> victory).

### Day 2 - Depth + UX (March 26)
- Add 1-2 more traps and 1-2 enemy variants.
- Add resource/upgrade loop between waves.
- Add zone-specific visuals and color coding.
- Add minimal UI: zone status, trap cooldowns/charges, resources, wave timer.

### Day 3 - Polish + Packaging (March 27)
- Balance wave pacing and difficulty.
- Add VFX/screen shake/feedback for activations and hits.
- Add audio: 2-3 SFX + simple ambient loop.
- Fix bugs, QA pass, build/export.

## Task List

### Foundation
- Create project structure (Scenes, Scripts, Prefabs, Art, Audio).
- Define data containers for traps/enemies (ScriptableObjects or JSON).
- Set up a single main scene with three zones.

### Switching Mechanics
- Implement zone switching (keys, UI tabs, or minimap).
- Ensure only one zone is active/controllable at a time.
- Add quick swap shortcut and visual focus indicator.

### Enemies + Waves
- Implement enemy spawners in each zone.
- Implement enemy AI: move to core, attack, die.
- Create wave controller: simultaneous waves across zones.
- Add fail state if any zone core is destroyed.

### Traps
- Implement Trap A: manual activation, limited charges, cooldown.
- Implement Trap B: area burst (manual trigger).
- Implement Trap C: slow or stun (manual trigger).
- Add feedback: VFX, sound, screen shake, cooldown UI.

### Resources + Upgrades
- Reward resources per wave.
- Allow upgrades between waves: cooldown reduction, charges, damage.
- Add repair option for damaged cores/traps.

### UI/UX
- Zone status display (health, threat level).
- Trap panel: charges, cooldown timers, ready state.
- Resource count and upgrade buttons.
- Wave countdown and phase indicator.

### Visuals
- Minimalist high-contrast art style.
- Zone palettes:
  - Cryo Grid (Blue)
  - Magma Forge (Red/Orange)
  - Bio Sector (Green)
- Enemy colors/shapes and simple particles.

### Audio
- Switch sound, trap activation, enemy hit/death.
- Ambient loop per zone or a single global loop.

### Polish & QA
- Difficulty tuning: enemy HP, spawn rate, wave length.
- Performance check with 3 active zones.
- Fix priority bugs: input, switching, wave flow, fail states.
- Export build + create jam submission notes.

## Definition of Done
- Player can switch between three zones in real time.
- Traps require manual activation and meaningfully change outcomes.
- Waves escalate and can be won or lost.
- UI communicates zone danger and trap readiness.
- Build runs without critical bugs for a 5-10 minute session.
