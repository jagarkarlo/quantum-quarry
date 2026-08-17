# Expansion Roadmap

QuantumQuarry will grow in small, playable milestones. Each milestone must compile in Unity `2022.3.12f1`, pass the project validator, and receive a Play Mode smoke test before the next one starts.

## 1. Store and inventory

- Stack purchased power-up duration instead of overwriting repeated purchases.
- Show prices, queued duration, affordability, and purchase feedback.
- Preserve existing PlayerPrefs saves through legacy queue migration.
- Next: replace hard-coded products with ScriptableObject catalog entries and add permanent upgrade tiers.

## 2. Progression and replayability

- Add permanent upgrades, unlock conditions, and a resettable profile.
- Add level medals for completion time, coins, and damage taken.
- Introduce daily or seeded challenge rules without requiring an online service.

## 3. Level format

- Define a versioned, serializable level document independent of Unity scenes.
- Support terrain, spawn, exit, coins, enemies, hazards, and moving platforms.
- Validate reachability, required objects, bounds, and supported content versions.

## 4. In-game level creator

- Add grid painting, erase, select, move, undo, and redo tools.
- Provide palettes for terrain, hazards, collectibles, enemies, and gameplay objects.
- Include playtest mode that starts from the editor and returns without losing edits.
- Save named local drafts and generate a preview image.

## 5. Sharing and discovery

- Export and import validated level files with checksums and size limits.
- Add local browsing, filtering, favorites, and completion records.
- Consider an optional moderated online level service only after the offline workflow is stable.

## 6. Advanced systems

- Add enemy archetypes that share the current state-machine foundation.
- Add ghost replays, speedrun splits, accessibility options, and remappable controls.
- Add procedural challenge generation that uses the same validator as user-created levels.

Machine learning is not required for these features. Deterministic AI, seeded generation, and strong validation will make the game more reliable and easier to test. ML can be explored later for level ranking or generation assistance only if it provides a measurable improvement.