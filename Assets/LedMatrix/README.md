# LED Matrix demo v1

Open `Assets/Scenes/3dLedMatrix.unity`, then **Tools > LED Matrix > Control Panel**.
The scene includes a project-generated, UV-textured animated FBX (source frames 0–59 inclusive, 30 fps).

## Realtime

Enter Play Mode for Spout transmission. In TouchDesigner, select `3dLedMatrix_Atlas` in a Syphon Spout In TOP. Both applications run on the same Windows computer and GPU. KlakSpout 2.0.6 is installed through UPM.

### Artist workflow and playback controls

In Edit Mode the Scene view shows the matrix, its bounds and the FBX without needing the LED Matrix panel to remain open. Use **Tools > LED Matrix > Control Panel** for content and display settings. Select **模型摆放** to manipulate its Transform in Scene/Inspector; save the scene before entering Play Mode. Open **Unity 动画时间轴**, select the animation root and use the native Animation window to scrub and play the clip in Edit Mode. LED output follows the evaluated pose. `Playing` in the panel controls the Animation window's current preview in Edit Mode and pauses/resumes the scene Animator in Play Mode. Unity's top Play/Pause controls the entire Play Mode; pausing it freezes the whole Unity player. Spout sends during Play Mode. Changes made while Play Mode is running are temporary under normal Unity rules.

While previewing an animation, the tool pins the Unity Animation window to its current FBX so selecting and transforming the separate placement parent preserves the current animation time and pose. Scrub to the start or end first, then select **模型摆放** and adjust position, rotation and scale. The **解除动画时间轴自动锁定** button releases only a lock the tool created; end preview and start it again to enable auto-lock again. Existing manual locks remain under the user's control. If this Unity version does not expose the lock internally, the panel shows a message to use the Animation window's own lock button instead.

The current model uses `Assets/Animations/Qilin Animator Controller.controller` and `Assets/Animations/Qilin Animation.anim`. The latter was empty and has been populated with the 570 editable curves from the imported 15-second FBX clip, retaining the existing controller and clip asset identity. Runtime animation is evaluated by that Animator Controller; matrix output samples the evaluated scene pose in LateUpdate. Qilin's source remains at `Assets/Models/02qilin/qilin_15s_.fbx`.

The panel maintains a scene-serialized list of content. **当前内容** selects which independent scene object is active and automatically updates Content Root, Animation Root and Animation Clip together. Existing volume sources under the matrix are registered on first opening the panel, preserving their scene instances and placement. Use **添加 FBX 模型** to instantiate another model without replacing the others; its first imported animation is selected when available and a controller is created only when needed. **添加已有场景内容** registers an existing placement root; FBX meshes get a volume source if needed. **从列表移除当前内容** removes the entry but retains the scene object. These edits and selection changes support Unity Undo/Redo. Save the scene in Edit Mode to retain the list, selection, transforms and references.

Static meshes can have no Animation Clip; their Transform and supported base material color/texture can still change live. Models need Read/Write enabled for mesh sampling; registration enables it on imported model assets. To use a custom Animator Controller, assign it to the model's Animator and manage its states in the Animator window; the tool preserves non-generated controllers. Animation Clip in the panel is used by edit preview and baking, whereas Play Mode follows the Animator Controller. If the controller's default clip differs, the panel warns and offers to use that default for preview/baking. This does not cover other states, transitions or Blend Trees.

Other runtime components can call `matrix.SwitchContent("qilin_15s_")` or `matrix.SwitchContent(index)`; both return false for invalid choices or during a bake, without changing the current selection. Names are case-sensitive and must be unique. Query `Contents`, `ActiveContentIndex` and `ActiveContent`, or subscribe to `ContentChanged`. A successful switch enables the selected placement root, disables the other registered roots, updates the three active references and resets the matrix sample time; Animator playback on reactivation follows Unity's Animator settings. Register scene objects with `AddContent(name, contentRoot, animationRoot, clip)` if a component supplies them at runtime; roots must be distinct, non-overlapping scene objects. A static or custom source may pass null for the animation root and clip.

Future VFX Graph and shader effects can be registered as scene content when they have a compatible `VolumeSource` adapter that writes the matrix-local 3D color/occupancy data. Selecting an arbitrary VFX or shader alone does not voxelize it; those adapters and baking their effects are not implemented yet.

The panel exposes LED counts X (length), Y (height), Z (depth), centre-to-centre spacing in mm for each axis and the voxel resolution multiplier. These three settings are drafts: edit them as needed, then click **重新生成矩阵** to apply all three together and recreate the matrix preview and RenderTexture. Invalid drafts leave the currently running matrix untouched. Applying a change is one Unity Undo step; Ctrl+Z and Ctrl+Shift+Z restore or redo the applied settings together. Draft edits are local to the panel until applied. The matrix uses a bottom-centred origin: X/Z centres run from `-(N-1)*spacing/2` to `+(N-1)*spacing/2`, and Y centres run from 0 to `(N-1)*spacing`. The volume includes half a cell beyond the outer LED centres. Defaults are X=16, Y=48, Z=16, 50 mm spacing, volume min=(-0.4,-0.025,-0.4), size=(0.8,2.4,0.8) m.

The voxel volume is point counts multiplied by the resolution multiplier in each axis. The atlas tile is X×Y counts multiplied by that multiplier. Atlas columns are ceil(sqrt(Z count)), rows are ceil(Z count/columns), and unused cells are opaque black. Each tile is an XY plane; layer numbers increase in local Z, left-to-right then top-to-bottom. X increases right and Y increases up. The panel shows current atlas grid and pixel dimensions. Defaults yield a 64×192×64 voxel volume and a 256×768 atlas of 4×4 tiles, each 64×192. Changing Z count can change the atlas grid and texture size; update TD's extraction layout accordingly. Bilinear depth interpolation samples between voxel centres when required. The atlas is point filtered and has no labels/borders.

TD samples the centre of each tile's X×Y LED cells. For LED indices x, y, z and atlas columns C / rows R, with bottom-left UV origin:

    u = ((z % C) + (x + 0.5) / X) / C
    v = ((R - 1 - floor(z / C)) + (y + 0.5) / Y) / R

Raw atlas bytes contain sRGB encoded RGB8, with alpha=255 and empty space black. Spout uses an sRGB view of those same bytes to avoid a second gamma conversion. Confirm red/green/blue, gray and Z-scan patterns when connecting a receiver. Gray test is linear 0.5, expected encoded value approximately 188/255. LED gamma/channel order/address mapping are handled by TD.

The local 3D preview samples the final atlas. Unlit LED markers use a faint visibility floor for orientation; this floor is not written to the output. Realtime UI reports measured atlas generation fps, not cumulative frame count or configured target fps. The target output rate is adjustable from 1–120 fps; this does not change the source Clip fps used for baking. Deadline scheduling avoids accumulating one Editor tick of delay on every output frame.

## FBX inputs

Select an imported FBX in the panel, then choose its AnimationClip. Read/Write is enabled on the selected ModelImporter. Position/scale the scene's `Content Placement` object to fit the matrix; the child Animator handles animation independently of placement. Root motion extraction is disabled, while authored transform curves are evaluated at the selected animation time.

The current source supports triangle MeshRenderers and SkinnedMeshRenderers, base-color texture/UV transform, material tint and static vertex colors. For skeletal meshes without BlendShapes, CPU skinning evaluates each imported bone influence directly in world space (bone.localToWorld × bindpose × restVertex), so nested FBX scales and the placement parent are applied exactly once. The existing BlendShape path still uses CPU BakeMesh and needs separate validation with scaled BlendShape assets. It does not reproduce arbitrary material graphs, lighting, transparency, procedural shaders or physics-driven animation. Multi-submesh geometry is supported. Surface collisions use deterministic packed-color ordering; blending is a future extension.

Surface voxelization uses triangle closest-point tests within a conservative voxel coverage radius. Surface thickness is configurable. Optional single-color interior fill uses all triangles of each closed renderer and Z-ray parity; non-closed, intersecting or degenerate meshes are outside its first-version guarantee. The parity implementation supports up to 64 distinct crossings per column and skips overflow/odd crossing columns.

## Bake

Exit Play Mode, select the source Clip, and click **Bake entire FBX clip to PNG sequence**. Choose an output parent directory. A new timestamped run directory is created so previous outputs cannot leave stale frames in a shorter sequence.

The importer Clip frame range is inclusive. For firstFrame..lastFrame, N=last-first+1; output frame k samples Clip local time k/sourceFPS. When importer frame metadata extends beyond the actual imported Clip duration, the last frame is capped to firstFrame + round(length*fps), avoiding repeated clamped tail frames. Non-FBX clips use round(length*fps)+1 samples. The final sample includes the end timestamp; depending on the Clip's loop setting, the endpoint may coincide with the first pose.

Each image is the complete atlas, named `frame_000000.png`, etc. `manifest.json` records source/Clip, source frame range, fps, dimensions, placement and conversion settings, saved count and completion state. PNG files contain the atlas's encoded bytes; TD must interpret them consistently with the live source because Unity's PNG encoder does not embed an ICC/gamma profile.

Baking samples each exact Clip time using Unity's editor AnimationMode, including when the Animation window was previewing and has disabled the scene Animator. It temporarily suspends and subsequently restores existing Animation-window preview and time; baking does not depend on real-time playback. Each sampled pose is converted and GPU-read back before the next sample. It may run slower than realtime without dropping frames. The panel and progress dialog display saved/total frames; cancel produces an incomplete manifest and restores preview. Do not edit source objects/Inspector conversion parameters during a bake. Script reload cancels the active run. PNG bake and live Spout share the same volume and atlas generator.

## Validation and version 2

`MatrixVerification.Run()` checks primary channels, alpha, gray encoding, all 16 Z layers, animated geometry, deterministic time sampling, closed-mesh fill and clearing vacated space. `VerifyBake(directory)` checks frame count/completion and exact pixel equality between a baked frame and live atlas.

Initial checks on the Quadro RTX 6000 / Direct3D 12 machine: the generated 60-frame FBX baked to 60 PNG files with exact live/baked RGBA equality. Closed-mesh fill increased occupied atlas pixels from 5,088 to 14,208 at the checked pose. With FBX animation, 3D preview and Spout active, a 10-second 60-fps-target Play Mode measurement achieved 59.66 atlas fps (CPU submission about 0.12 ms at the sampled frame; this is not GPU duration). This is a simple test asset, not a performance guarantee for complex FBXs or fill mode.

Spout sender transport buffer bytes match the generated atlas exactly after compensating its internal Y flip. Gray loopback returns 188/255. KlakSpout's own receiver applies an approximate GammaToLinearSpace shader conversion and showed up to 5/255 error in a coordinate-gradient roundtrip; that conversion is on the test receiver path, not in the outgoing transport texture. Final TouchDesigner receiver settings still need to be checked against the supplied calibration patterns.

Version 2 explicitly includes Unity-native realtime content: VFX Graph particles, Shader Graph and HLSL shader effects. `VolumeSource.WriteVolume` is the integration boundary. Each input needs an adapter supplying a 3D color/occupancy field or writing the shared volume. A normal screen-space shader image alone does not define interior 3D colors. These adapters are not implemented in version 1; they will reuse atlas generation, preview, Spout and baking.

Version 2 also requires direct Scene adjustment of imported FBX animation content: display the original FBX, the Matrix bounds and their overlay; adjust position, rotation and uniform size on an independent placement parent; keep animation on the child so it cannot overwrite placement; switch between FBX / LED points / overlay and persist the placement settings.

## Qilin realtime test

`Assets/Models/02qilin/qilin_15s_.fbx` is installed in the current scene with its provided `qilin_.png` texture. The active character has 75,214 triangles, including a skinned body and decorative mesh. A remote untextured `Box002` export helper is inactive in the scene instance; the source FBX was not edited. Read/Write is enabled for CPU geometry access.

The initial realtime test used spatial fitting that checked all 451 poses at 30 fps over the actual 15-second imported Clip. Its uniform placement scale was approximately 0.268421; the user may subsequently change placement in the scene.

Post-placement verification checked all 451 poses again: zero poses outside Matrix. A 20-second Play Mode run covering a full animation cycle with skeletal deformation, voxelization, 3D LED preview and Spout active averaged 59.40 atlas fps at a 60 fps target. CPU submission at the sampled frame was about 2.66 ms (not a GPU duration measurement); current Unity Console errors and warnings were zero. This tests surface mode at voxel resolution 64×192×64 and atlas 256×768.

The importer reports default source range 0–500 while the imported Clip is about 15 seconds at 30 fps; the bake caps the range to the actual Clip duration. In the 2026-11-20 test with the then-current scene placement, all 451 PNGs were produced, frames 0–9 were black (matching the same-time LED preview), and frames 225 and 400 contained distinct nonblack output. Five sampled PNGs were checked byte-for-byte against same-time previews, and cancellation restored the Animation window's previous time and selection.
