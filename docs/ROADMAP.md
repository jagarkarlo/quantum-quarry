# Expansion Roadmap

QuantumQuarry will grow in small, playable milestones. Each milestone must compile in Unity `2022.3.12f1`, pass the project validator, and receive a Play Mode smoke test before the next one starts.

## 1. Quantum Stability

- Implemented: three persistent stability points, contact damage, knockback, and one second of hit invulnerability.
- Implemented: enemies deal one damage, spikes deal two, and drowning drains half a point after breath expires.
- Implemented: critical stability activates overdrive and doubles collected coin value.
- Implemented: buoyant swimming, level-scaled breath duration, and lethal lava in the final level.
- Implemented: two purchasable Store armor tiers that reduce incoming Stability damage (never to zero), per-run damage statistics (hits taken, Stability lost) surfaced on the Game Over and Victory screens, and a dedicated hit-flash separate from invulnerability blinking.
- Implemented: the stabilization pickup prefab is placed in Level 6, the Store armor button is wired, and the project validator checks both.
- Next: Quarry Pressure (see below).

## 2. Quarry Pressure

- Implemented in source: carried base ore raises pressure at 500, 1500, and 3000; subsequent pickups receive x1.25, x1.5, and x1.75 rewards. Critical Stability doubles those rewards.
- Implemented in source: pressure increases enemy perception; a persisted seed selects swift-pursuit modifiers. Pulse vents use a safe warning phase before damage.
- Implemented in source: a separate pressure HUD, threshold notices, and exact coin reward previews.
- Implemented in source: Store entry, level exits, victory, and banking checkpoints deposit carried rewards and reset pressure. Death and manual level reset discard unbanked ore. Existing banked balances and level unlocks are preserved.
- Implemented tooling: original checkpoint/vent pixel art and an Editor command that creates reusable prefabs without overwriting existing assets.
- Verified outside Unity: deterministic C# rules, session transitions with Unity test doubles, artwork dimensions/palette, and C# 9 syntax.
- Verified in Unity 2022.3.12f1 on 2026-09-21: package restoration, API compilation, custom sprite/prefab generation, strict custom-prefab validation, project validation, and a Windows x64 build of all 11 enabled scenes.
- Authored pilot: Level 4 has an optional bank, a warning-labelled exit vent, and a 1550-base-ore pickup route. Persistent collectibles are hidden and suspended in menus/Store without respawning on return.
- Verified in the isolated Windows player: 144 controlled runtime checks and 18 screenshots covering all 11 scenes, including banking, vent phases/contact damage, pause, invisibility, death/reset, Store round trips, persistent-level transitions, and Pressure HUD bounds at three resolutions.
- Pending milestone gate: manually complete the pilot route without upgrades, tune the risk/reward encounter, and finish the remaining [pressure smoke test](DEVELOPMENT_WORKFLOW.md#quarry-pressure-validation). Automated repositioning and seeded ore are not proof of full-route playability or enemy encounter balance. Do not start the next milestone before this gate passes.
- Future refinement: an in-game seed selector and authored campaign encounter tuning. The current reproducibility contract covers modifier selection and local pulse cycles, not full physics replay.

## 3. Store and inventory

- Stack purchased power-up duration instead of overwriting repeated purchases.
- Show prices, queued duration, affordability, and purchase feedback.
- Preserve existing PlayerPrefs saves through legacy queue migration.
- Next: replace hard-coded products with ScriptableObject catalog entries and add permanent upgrade tiers.

## 4. Progression and replayability

- Add permanent upgrades, unlock conditions, and a resettable profile.
- Add level medals for completion time, coins, and damage taken.
- Introduce daily or seeded challenge rules without requiring an online service.

## 5. Level format

- Define a versioned, serializable level document independent of Unity scenes.
- Support terrain, spawn, exit, coins, enemies, hazards, and moving platforms.
- Validate reachability, required objects, bounds, and supported content versions.

## 6. In-game level creator

- Add grid painting, erase, select, move, undo, and redo tools.
- Provide palettes for terrain, hazards, collectibles, enemies, and gameplay objects.
- Include playtest mode that starts from the editor and returns without losing edits.
- Save named local drafts and generate a preview image.

## 7. Sharing and discovery

- Export and import validated level files with checksums and size limits.
- Add local browsing, filtering, favorites, and completion records.
- Consider an optional moderated online level service only after the offline workflow is stable.

## 8. Advanced systems

- Add enemy archetypes that share the current state-machine foundation.
- Add ghost replays, speedrun splits, accessibility options, and remappable controls.
- Add procedural challenge generation that uses the same validator as user-created levels.

Machine learning is not required for these features. Deterministic AI, seeded generation, and strong validation will make the game more reliable and easier to test. ML can be explored later for level ranking or generation assistance only if it provides a measurable improvement.

## Recommended delivery order

Planning update: 2026-09-11. The ideas below are proposals, not implemented features or delivery commitments. Sections above group the roadmap by system; they do not require finishing every Store or progression feature before starting the creator.

1. Finish the Quarry Pressure Unity gate: generate and place the custom elements, check HUD readability, tune encounters, and validate a desktop build. Source tests alone do not establish a playable milestone.
2. Add level results and a small pressure challenge room to validate the risk/reward loop with actual players.
3. Build the custom-level data model and isolated playtest session, then the offline creator MVP described below. Keep the six existing campaign scenes working unchanged.
4. Add the local level library and safe file sharing, followed by new creator objects and enemy types.
5. Expand permanent progression and challenge modes after save ownership is explicit. Online discovery and full replay simulation remain later projects.

## Game improvements worth building

| Priority | Proposal | Player-facing result | Scope and acceptance |
| --- | --- | --- | --- |
| High | Level results and personal bests | A reason to replay all six levels | Show completion time, banked ore, lost ore, and damage; award separate medals with fixed per-level targets. Pause and Store time are excluded from the timer. Store records by stable level ID, not scene index. |
| High | Ore-vault challenge rooms | A visible choice between banking safely and carrying enough ore to open a bonus route | Build one short optional room with an ore vault, pressure gate, safe banking route, and vent warning space. Telegraph the entry requirement and payout. Verify it can be completed without purchased upgrades. |
| High | Accessibility and controls | More comfortable and readable play | Rebinding with persistence and reset, independent audio sliders, reduced hit flashes, and warnings that use shape/text as well as color. Include keyboard and gamepad menu navigation. |
| High | Profile and run-state separation | Reliable progression without save surprises | Separate permanent unlocks, campaign run state, settings, and disposable creator playtests. Migrate existing PlayerPrefs; resetting a run must not erase the profile. Separate full-profile deletion behind confirmation. |
| Medium | Distinct enemy roles | Encounters that differ by behavior rather than just speed | Add a stationary sentry with a visible aim warning, then a shielded patrol vulnerable from behind. Reuse the existing AI controller where appropriate, but do not compromise line of sight or invisibility behavior. Create distinct prefab silhouettes. |
| Medium | Pressure-powered world objects | Routes that react to the player's carried ore | Add a pressure gauge, threshold-operated door, and timed vent variants. Define whether each door latches or reopens after banking, and prevent closures from trapping or crushing the player. Expose only validated settings to creators. |
| Medium | Seeded challenge board | Repeatable short challenges without a server | Choose a seed and explicit rules, such as no Store or a target banked score. Keep challenge records separate from campaign saves. A date-derived daily seed is a convenience, not an anti-cheat system. |
| Medium | Mining and movement objects | New ways to build platforming routes | Start with breakable ore crates and a directional jump pad, using original sprites, clear collision bounds, and reusable prefabs. Define bullet/power-up interactions and test platform momentum before adding them to the creator palette. |
| Later | Speedrun ghost | Race a previous successful run | Begin with sampled position/animation playback that cannot affect physics. Version recordings with the level content hash; do not promise deterministic input replay from the current Rigidbody2D controller. |

Suggested first custom asset set: an ore vault, a pressure gauge/door pair, a sentry, a breakable ore crate, and a jump pad. Build and validate one playable interaction at a time instead of creating an unused asset collection.

## Level creator implementation plan

### What already helps

- Unity Tilemaps, the 2D toolchain, Input System, Cinemachine, and TextMesh Pro are already available. Reuse Unity's rendering and physics rather than building another engine.
- Existing player, terrain, coins, enemies, platforms, exits, and pressure elements supply the initial palette's building blocks.
- The project already has an Editor validator and standalone rule-test runner. Extend these with document round-trip and invalid-input tests, then add Unity tests for placement and playtest transitions.

### Boundaries to fix first

- `LevelSelector` and `LevelExit` construct campaign scene names from level numbers. A custom level needs a stable document ID and a completion route back to the creator or library, without unlocking campaign levels.
- `GameSession` persists run state in PlayerPrefs and survives scene changes. Creator playtests must use disposable state and must never award campaign coins, consume purchased inventory, alter records, or clear the player's save. An explicit session mode/state owner is safer than temporarily overwriting and restoring global keys.
- `PlayerRespawn` uses shared Store-return keys, while player/enemy difficulty and liquid behavior currently depend on campaign scene names. Pass explicit level settings into custom runs; do not rename custom scenes to imitate campaign levels.
- Author new levels as data rendered into one reusable custom-play scene. Do not generate a Unity scene asset for every player-created level, require UnityEditor APIs in a build, or require creators to install Unity.

### Phased scope and effort

Estimates are engineering effort for one developer familiar with this project, including implementation, integration, and tests. They assume access to licensed Unity 2022.3.12f1, existing assets, a desktop keyboard/mouse creator, and prompt design decisions. These are planning ranges, not measured delivery forecasts; allow additional time for unfamiliar Unity APIs, usability feedback, and platform-specific defects.

| Phase | Deliverable | Additional effort |
| --- | --- | --- |
| A. Playable prototype | Versioned JSON document, allowlisted tile/object IDs, bounded grid, one spawn and exit, basic ground/coin palette, paint/erase, runtime loading, and an isolated playtest/return loop | 24-40 hours |
| B. Usable editing | Selection and move, grouped stroke undo/redo, pan/zoom, grid snapping, object property inspector, basic enemy/hazard palette, document validation, and consistent tool/input focus | 28-48 hours |
| C. Reliable offline MVP | Named save/load, autosave recovery, unsaved-change handling, local thumbnails/library, limited JSON import/export, regression tests, and desktop usability/build checks | 32-56 hours |
| D. Expanded creator | Rectangular fill, copy/paste, moving-platform paths, additional pressure objects, templates, gamepad editing, stronger validation, and accessibility polish | 60-120 hours |

Prototype: **24-40 hours**. Offline MVP through phase C: **84-144 hours total**, roughly **3-5 weeks at 30 focused hours/week**, or **9-15 weeks at 10 hours/week**. Expanded creator through phase D: **144-264 hours total**, roughly **5-9 focused weeks**. The MVP excludes online accounts, uploads, moderation, multiplayer, arbitrary scripts/assets, campaign-scene conversion, and guaranteed automatic solvability proofs. A polished online service needs its own scope and estimate.

### First playable scope

- A bounded grid, initially at most 128x64 cells, with ground, spawn, exit, and coins. Introduce existing enemies and spike hazards after the basic playtest loop passes; water, moving-platform paths, and advanced pressure objects can follow.
- A compact toolbar with paint, erase, select, undo/redo, save, and playtest; an object palette; a properties panel; an unobstructed level canvas. Right-side panels must not intercept world placement incorrectly, and dragging a stroke counts as one undo action.
- Store `schemaVersion`, stable level ID, title, bounds, tiles, object instances, and explicit gameplay settings. Resolve allowlisted catalog IDs to tiles/prefabs through project-owned assets. Reject duplicate IDs, unsupported versions, invalid coordinates, and unknown content before instantiation.
- Save under `Application.persistentDataPath` using application-owned filenames derived from validated IDs, not user-supplied paths. Use temporary-file replacement with a recoverable previous copy; test behavior on the actual desktop target.
- Import only level data, never code, arbitrary prefab paths, remote URLs, or user-supplied asset bundles. Initial limits: 1 MiB input, 8192 grid cells, 512 objects, bounded strings, and bounded settings. Checksums detect accidental corruption, not trustworthiness.
- Playtest a snapshot of the draft. Return to the identical editable document, selection, and camera without saving playtest damage, removed coins, or object movement back into the draft.

### Acceptance gates

1. Build a small level, play it, finish or die, and return to the same draft. Campaign coins, lives, unlocks, armor, queued power-ups, and Store-return state must remain unchanged.
2. Undo and redo a multi-cell paint stroke, object move, and deletion. Save/load round-trips preserve object IDs and settings; unsupported document versions show a clear error without replacing the open draft.
3. Reject missing/duplicate spawn or exit, blocked spawn space, out-of-bounds objects, invalid numeric values, oversized files, unsupported IDs, and unsafe paths. Display errors at their locations where possible.
4. Distinguish structural validity from solvability. A flood fill is not proof of reachability for jumping, moving platforms, swimming, or power-ups. Start with warnings plus manual completion; bind any local completion badge to the current content hash, and invalidate it on gameplay edits.
5. Close/reopen after saving, recover from an interrupted save, cancel an unsaved exit, and verify imported malformed files cannot damage existing drafts.
6. Verify placement, dragging, camera movement, UI focus, and readable layout in an actual desktop build. Run the existing campaign smoke tests before declaring the creator milestone complete.