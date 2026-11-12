# oe-bundle

Writes prefabs of the game's kind into a mod's AssetBundle for Heroes of Might and Magic: Olden Era mods made with
`Nobirds.OldenEra.Modding.Sdk`. Not affiliated with the game's publishers.

The game takes a unit's models by path (`units/` + the unit view's `mesh` and `mapMesh`) and reads its own components on
them. From a model prefab of the bundle (an animated skinned mesh with an `Animator`) the tool makes the unit's battle
and map prefabs laid out as the game's, the game's components on them by readable names (the mod library puts the
running game's names in when it loads the bundle):

```
oe-bundle units <bundle> <out bundle> <unit.json>...
```

```json
{
  "model": "goblin/goblin.prefab",
  "battle": { "path": "units/myfaction/goblin", "turn": 60,
              "projectileSpawn": "RightHand", "projectileEnd": "Spine1", "shield": "Spine1", "shieldScale": 1 },
  "map":    { "path": "units/myfaction/goblin_map", "turn": 195, "scale": 0.96, "mirror": true, "questMarker": 1.55 }
}
```

- `model`: the model prefab, by the end of its path in the bundle;
- `battle.path`: the battle prefab, at `assets/resources/<path>`; the unit view's `"mesh": "myfaction/goblin"`. The game
  scales its first child by the view's `scale`; `turn` turns the model about the vertical axis (degrees: the game's
  models are turned +90 there, a model facing another way needs another turn); the points are the names of the
  model's objects (bones) projectiles start from, land at, and the shield is shown at;
- `map.path`: the map prefab; the view's `"mapMesh": "myfaction/goblin_map"`. Its size is the prefab's own: `scale`,
  mirrored like the game's (`mirror`), `turn`, and the quest marker's height.

The model is copied into each prefab; its meshes, materials, controller and avatar are shared. The bundle must be built
with type trees (Unity's default). See `docs/writing-a-mod.md`, "Models".
