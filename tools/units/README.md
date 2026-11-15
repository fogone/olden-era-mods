# Units on the game's skeletons

Scripts that make a new creature's mesh fit a game unit's skeleton, so that the game's own animations move it (see
`docs/writing-a-mod.md`, "A unit on a game unit's skeleton"). The game unit's export (a `.glb` with its skeleton,
skinned meshes and bind poses) is made from the game's files and stays out of the repository.

| Script | What |
|---|---|
| `render_pose.py` | Blender: a unit's `.glb` rendered in its bind pose from eight sides (`a000` ... `a315`, the camera's angle about the model), weapons hidden by name: the picture an image model repaints |
| `skin_to_game.py` | a model made from that picture (an image-to-3D tool's textured `.glb`) skinned to the unit's skeleton: a `.glb` in the game mesh's space with the unit's bones and bind poses, for `oe-bundle model` |
| `check_skin.py` | Blender: a skinned `.glb` rendered in its bind pose and with a few bones turned, to see the weights follow |

```
blender -b -P render_pose.py -- esquire.glb views --hide Shield Sword
python skin_to_game.py esquire.glb Swordsman_mesh goblin.glb goblin_skinned.glb --view 270
blender -b -P check_skin.py -- goblin_skinned.glb check.png
oe-bundle model <template bundle> Bundles\unit_goblin unit_goblin.json
```

`skin_to_game.py`:
- `--view <degrees>`: the side the picture showed the unit from (`a270` -> 270). An image-to-3D tool faces the model to
  the picture's camera; without it the best of four quarter turns by shape is taken, which may take the back for the
  front;
- `--also <mesh>...`: other skinned meshes of the unit on the same bones (a rider and his horse) the model covers too.

The model is stretched to the game mesh's box, so the picture must keep the host's pose and posture; the fit it prints
(the mean distance of the model to the game surface) says how well: the goblin on the esquire 0.03 of 1.6, on the
hunched troglodyte 0.15 of 1.3 (hands flattened in the animations).
