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
- Pending milestone gate: run the prefab builder in Unity 2022.3.12f1, place checkpoints/vents, validate the project and custom prefabs, and complete the [pressure smoke test](DEVELOPMENT_WORKFLOW.md#quarry-pressure-validation). Serialized pressure prefabs and placements are not yet committed. Do not start the next milestone before this gate passes.
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