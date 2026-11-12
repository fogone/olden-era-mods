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

## Town scenes

```
oe-bundle city <scene bundle> <out bundle> <city.json>
```

A town is a scene of the game's kind: the buildings' manager (`BhBuildingsManager`) with a slot per building of the
city's data (by its `sceneSlot`, e.g. `Main`, `Tier_1`, `Build_Wall`), a building per level in each slot (`BhBuilding`
and a collider: the game shows the level built and opens its windows on a click), and the town camera (`CityCamera`:
the game's camera controller and its settings, the scene's camera under it tagged `MainCamera`, drawn first, the game's
interface over it). The tool makes them of the scene's objects:

```json
{
  "slots": {
    "Main":       ["townHall_1", "townHall_2", "townHall_3"],
    "Tier_1":     ["dwelling1", "dwelling1_upg"],
    "Build_Wall": ["wall_1", "wall_2", "wall_3"],
    "Treasury":   [""]
  },
  "camera": { "object": "TownCamera", "position": [-5.6, 2.4, -10], "size": 3 }
}
```

- `slots`: a slot's levels, each the name of the scene's object shown at that level; `""` is a level with nothing shown,
  a name repeated shows the same object. A slot needs as many levels as the building has in the city's data;
- `camera.object`: the scene's camera; `position` and `size` (an orthographic camera's half height) frame it when the
  scene's own values frame something else; `rotationSpeed`, `deltaHorizontalAngle`, `deltaVerticalAngle`,
  `maxMovementSpeed`, `transitionTime`, `cullingMask`: the controller's settings, the game's town values by default;
- `buildingHeight`: the buildings' height for the game's effects (2);
- `offset`: where the scene is put (`[0, -2000, 0]`: away from the world map, which the game keeps loaded under it).

See `docs/writing-a-mod.md`, "Town scenes".
