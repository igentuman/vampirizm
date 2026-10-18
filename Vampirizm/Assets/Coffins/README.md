# Coffin assets

Original procedural Blender assets created for Vampirizm, with no third-party assets or attribution requirements.

Run from the repository root (Blender 5.2.2 was used):

```sh
blender --background --python Vampirizm/Assets/Coffins/generate_coffins.py
```

- `wooden_coffin.blend`: open plank shell, 1,264 triangles.
- `vampire_coffin.blend`: ancient wood, iron trim, linked chains, red lining, bat crest and keyed lid, 6,004 triangles.
- `*.json`: runtime meshes with normals, UVs and material groups, in Unity metres. The vampire lid vertices are relative to the hinge at `(-0.60, 0.77, 0)`.
- `../wooden_coffin.png`, `../vampire_coffin.png`: transparent 256×256 hammer icons.
- `../tex_coffin_*.png`: 512×512 wood, ancient wood, iron and lining albedo textures.
- `vampire_coffin_open.png`: open-lid preview.

The .blend files use Blender's Z-up coordinates. Runtime JSON swaps Y/Z and reverses winding. The script exports the closed lid, then parents it to the hinge for the editable animation: open at frame 1, closed at 30–90, open at 120. In game, `CoffinBed` rotates the lid through 105 degrees over approximately one second using the existing player bed attachment synchronization. No new RPC or persisted occupancy flag is needed.

Models, icons and textures are embedded in `Vampirizm.dll`; Blender and the source files are not needed by players. `CoffinModels` uses a copy of the vanilla intact bed's material for game lighting, replaces all physical bed colliders, retains the PlayerBase trigger and creates an open collision shell. The moving lid is cosmetic and has no collider.

## In-game checks

Build and inspect both pieces under Hammer → Furniture. Check placement, rotations, repair/removal, refunded resources, icon visibility, comfort and save/reload. Verify that a human cannot claim them, then claim each as a vampire with shelter and fire. Complete both a daytime and nighttime sleep, checking +15/+55 blood and capacity clamping. Enter and leave without sleeping to check there is no reward. Check respawning and the sleeping pose inside the shell. With two clients, check that only the occupied coffin closes, waking/disconnecting opens it, and both viewers see the lid move.

The automated rule tests cover wake transitions and blood caps; these world, pose and multiplayer checks require Valheim.
