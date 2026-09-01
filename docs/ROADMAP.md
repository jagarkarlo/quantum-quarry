# Expansion Roadmap

QuantumQuarry will grow in small, playable milestones. Each milestone must compile in Unity `2022.3.12f1`, pass the project validator, and receive a Play Mode smoke test before the next one starts.

## 1. Quantum Stability

- Implemented: three persistent stability points, contact damage, knockback, and one second of hit invulnerability.
- Implemented: enemies deal one damage, spikes deal two, and drowning drains half a point after breath expires.
- Implemented: critical stability activates overdrive and doubles collected coin value.
- Implemented: buoyant swimming, level-scaled breath duration, and lethal lava in the final level.
- Implemented: two purchasable Store armor tiers that reduce incoming Stability damage (never to zero), per-run damage statistics (hits taken, Stability lost) surfaced on the Game Over and Victory screens, and a dedicated hit-flash separate from invulnerability blinking.
- Implemented, pending Editor wiring: a `StabilizationPickup` component that restores Stability on contact and respawns after a cooldown. To finish this in the Unity Editor:
  1. Create a prefab combining a `SpriteRenderer`, a trigger `Collider2D`, and `StabilizationPickup`, following `Coin.prefab`'s structure.
  2. Place instances in one or more levels.
  3. Add a `Button_Armor` object to the Store scene, wired to `StoreManager.BuyArmor`, matching the existing Store buttons; the responsive layout already reserves its position and panel height.
  4. Once both exist, extend `QuantumQuarryProjectValidator` with a `ValidatePrefabComponent<StabilizationPickup>` check and a `ValidateButton(..., "Button_Armor", "BuyArmor", ...)` check, matching the existing patterns.
- Next: Quarry Pressure (see below).

## 2. Quarry Pressure

- Increase pressure as the player carries valuable ore without banking it.
- At deterministic thresholds, strengthen enemy perception, activate hazard pulses, and introduce encounter modifiers.
- Telegraph every pressure increase and expose the exact reward multiplier before the player commits.
- Let checkpoints and the Store bank ore, reset pressure, and preserve permanent progression.
- Use a seeded modifier schedule so difficult runs are reproducible and testable.

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