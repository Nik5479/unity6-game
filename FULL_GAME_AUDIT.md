# FULL GAME QUALITY AUDIT — THE FORGOTTEN VILLAGE
User order (2026-11-07 ~23:46 IST): "Ye note karlo" — strict full-game quality audit and complete bug fixing required.
Status: QUEUED — runs after the lighting + ground pass finishes, so the audit tests the latest state, not a mid-change state.

IMPORTANT: STRICT FULL GAME QUALITY AUDIT AND COMPLETE BUG FIXING REQUIRED.

I want you to inspect and test my ENTIRE Unity 6 URP game, not just make visual changes or fix a few selected problems.

My game must look and feel like a realistic, professional Indian open-world horror survival game — NOT a cartoon, NOT low-poly, NOT a cheap mobile game.

You are working inside Unity Editor on Linux without a dedicated GPU. Work carefully within CPU and memory limits. Do not pretend that something has been tested if it has not.

STRICT RULE:
DO NOT BUILD THE APK YET.
DO NOT DELETE EXISTING GAMEPLAY.
DO NOT BREAK EXISTING SCRIPTS, SCENES, CHARACTERS, MISSIONS OR CONTROLS.
First create a backup or Git commit. Inspect the complete project before modifying it.

## 1. COMPLETE PROJECT INSPECTION
- Inspect every scene, prefab, script, material, texture, animation, model and asset.
- Check all Console errors, warnings and runtime exceptions.
- Check missing references, missing scripts and broken prefab links.
- Check duplicate objects and duplicate components.
- Check incorrect object scales, rotations and positions.
- Check incorrect parent-child relationships.
- Check missing colliders, Rigidbody and CharacterController settings.
- Check NavMesh and navigation problems.
- Check broken materials, missing textures and shader errors.
- Check asset import settings and compression.
- Check scene loading, transitions and object initialization.
- Check all systems after Unity 6 migration.
- Never report success based only on a clean Console.

## 2. GRAPHICS — REMOVE THE CARTOON LOOK
Inspect every visual asset individually.
- Replace low-poly and cartoon-style trees, houses, rocks, roads and props.
- Use realistic Indian village assets.
- Trees must have natural branches, leaves, trunks and realistic proportions.
- Houses must have detailed walls, doors, windows, roofs and weathering.
- Ground must have natural dirt, mud, grass, stones and texture variation.
- Remove flat single-color materials.
- Check Albedo, Normal, Roughness/Smoothness and Ambient Occlusion maps.
- Correct texture tiling, UV mapping and texture stretching.
- Fix overly bright, dark, shiny or plastic-looking materials.
- Fix unrealistic skin, faces, hair, clothing and eyes.
- Check shadow quality, contact shadows, light direction and reflection.
- Fix unnatural fog, skybox, horizon and atmospheric lighting.
- Add realistic environmental details without excessive clutter.
- Use appropriate post-processing: color grading, exposure, ambient occlusion and anti-aliasing.
- Avoid excessive bloom, oversaturation and artificial outlines.
- Maintain consistent visual quality across all scenes.
Do not claim graphics are realistic just because you changed lighting settings. Actual models and materials must be inspected.

## 3. MODEL COLLISION AND OVERLAPPING
Check every model in the complete village.
- No character should pass through walls, trees, vehicles or other characters.
- No two models should incorrectly overlap.
- No floating objects.
- No objects buried inside the ground.
- No feet floating above or sinking into the ground.
- No trees or buildings intersecting roads incorrectly.
- No vehicles passing through walls or props.
- No weapons or accessories passing through character bodies.
- Check collider sizes, shapes, layers and collision matrices.
- Check trigger zones and interaction ranges.
- Check Rigidbody mass, drag, gravity and collision detection.
- Check CharacterController radius, height, center and step offset.
- Check animation root motion and collision synchronization.
Test collisions from different directions, heights and movement speeds.

## 4. PLAYER MOVEMENT AND GTA-STYLE CAMERA
Test all player controls.
- Idle, Walking, Running, Sprinting, Jumping, Crouching, Punching, Climbing, Turning left and right, Moving backward, Stopping immediately, Starting and stopping animations, Camera rotation, Camera zoom, Camera collision with walls and buildings.
Camera requirements:
- Smooth third-person GTA V-inspired camera.
- Correct camera follow distance and height.
- No camera shaking or sudden snapping.
- No camera passing through walls.
- No camera clipping through the player.
- No excessive camera movement.
- No sudden changes in field of view.
- Mobile touch camera must respond smoothly.

## 5. REALISTIC HUMAN ANIMATIONS
Inspect all 8 Mixamo animations and their transitions.
- Idle must look natural with subtle breathing.
- Walking must have natural human steps.
- Running must look like real human running.
- Sprinting must be faster and visually different from running.
- Jumping must have correct takeoff and landing.
- Crouching must look natural.
- Punching must connect with the target correctly.
- Climbing must match the actual obstacle.
- Hands and feet must not slide unnaturally.
- No sudden body twisting. No stretched limbs. No incorrect shoulder or elbow movement.
- No feet sliding on the ground. No animation freezing. No animation transition popping.
- Correct Animator Controller, Avatar and transitions.
- Correct animation speed for each movement.
- Check root motion versus in-place movement.
The character must not move like a robot.

## 6. NPC AND VILLAGE PEOPLE AI
Inspect every village NPC, including Pujari and Mukhiya.
- NPCs should stand naturally. NPCs should walk at realistic human speed.
- NPCs should not walk sideways or backwards unintentionally.
- NPCs should not walk through walls or each other.
- NPCs should not get stuck against objects. NPCs should avoid obstacles.
- NPCs should not suddenly teleport.
- NPCs should have correct turning and stopping animations.
- NPCs should face the player when talking.
- NPCs should maintain appropriate distance during conversation.
- Dialogue should trigger correctly.
- NPCs should not repeat broken or overlapping dialogue.
- NPCs should have correct day and night behaviour.
- NPCs should not disappear unexpectedly.
- Check NavMeshAgent radius, height, speed, acceleration and stopping distance.
NPC behaviour must feel like real village people, not randomly moving game bots.

## 7. MONSTER AND ANIMAL AI
Inspect HellDemon and Bhediya, plus all animals.
- Monster detection distance must be correct. Monster must detect the player reliably.
- Monster must chase using valid navigation. Monster must not walk through buildings or walls.
- Monster must not get stuck. Monster must attack at the correct distance.
- Monster attack animations must match attack timing.
- Monster must stop attacking when the player escapes.
- Monster must not teleport or slide.
- Monster must have correct idle, walk, run and attack states.
- Wolf movement must look natural.
- Animals must not float, overlap or move unnaturally.
- Check AI state transitions and detection logic.

## 8. LIGHTING AND ENVIRONMENT
Check the entire environment.
- Daytime lighting. Sunset lighting. Nighttime lighting. Moonlight.
- Street lamps. Temple lighting. Indoor lighting. Flashlight lighting. Monster lighting.
- Natural shadows. Fog and visibility. Wet ground reflections. Rain and weather effects.
- Light flickering. Light leaks through walls. Incorrect baked lighting.
- Missing light probes. Reflection probe placement.
No completely black characters, glowing white models or unnatural green patches.

## 9. AUDIO AND HORROR ATMOSPHERE
- Check background music. Check footsteps on different surfaces.
- Check doors opening and closing. Check knocking sounds. Check monster sounds.
- Check animal sounds. Check NPC voices and dialogue timing.
- Check sound volume and spatial positioning.
- No repeated audio overlap. No missing or broken audio.
- Check 3D audio distance and attenuation.
- Check horror ambience and silence between events.

## 10. GAMEPLAY AND INTERACTIONS
Test every interaction.
- Doors open and close correctly. Locks work correctly.
- Torch and flashlight work correctly. Inventory works correctly. Pickups work correctly.
- Items cannot be collected repeatedly by mistake.
- Mission triggers work. Dialogue choices work.
- Player health and damage work. Monster attacks cause correct damage.
- Death and respawn work. Save and load work. Pause and resume work.
- Game does not get stuck in menus. UI buttons respond correctly.
- All mission objectives can be completed.

## 11. MOBILE UI AND CONTROLS
- Joystick must be correctly positioned and sized. Buttons must not overlap.
- Touch controls must respond reliably. UI must fit different Android screen sizes.
- Check portrait and landscape behaviour as designed.
- Check safe areas and screen edges. Text must remain readable.
- Dialogue must not cover important controls. Inventory icons must display correctly.
- No invisible buttons blocking input. Check multitouch.
- Check joystick dead zones and sensitivity. Check camera and movement touch conflicts.

## 12. LAG AND PERFORMANCE — VERY IMPORTANT
The game must be optimized for Android.
- Profile CPU and GPU timings where available.
- Identify expensive scripts and Update loops. Reduce unnecessary Instantiate and Destroy calls.
- Use object pooling. Optimize physics and collision checks.
- Reduce excessive NavMesh updates. Optimize NPC and monster AI update frequency.
- Use LOD for trees, houses, characters and props. Use occlusion culling where useful.
- Use GPU instancing where supported. Optimize draw calls and materials.
- Reduce unnecessary real-time lights. Bake static lighting where appropriate.
- Optimize shadows. Compress textures appropriately.
- Avoid loading unnecessarily large textures.
- Avoid memory leaks and garbage collection spikes. Avoid excessive particles.
- Optimize audio memory. Check scene loading and unloading.
- Check frame-time spikes and stuttering. Test long gameplay sessions for memory growth.
Do not increase graphics settings blindly. Maintain a realistic balance between visual quality and Android performance.

## 13. UNITY 6 AND LINUX CPU-ONLY WORKFLOW
Because there is no dedicated GPU:
- Do not run heavy AI model inference locally.
- Avoid unnecessarily high-quality realtime rendering in the Editor.
- Use CPU-friendly asset inspection and static analysis.
- Avoid multiple expensive editor processes.
- Keep texture and model import settings optimized.
- Use batch validation and automated tests when possible.
- Do not claim actual mobile GPU performance from Editor results.
- Separate Editor performance from Android device performance.
- If a test cannot run without a GPU or Android device, mark it NOT TESTED.

## 14. AUTOMATED TESTING AND COMPLETE QA
Create and run tests wherever possible:
- Unity EditMode tests. Unity PlayMode tests. Scene loading tests.
- Missing-reference tests. Prefab validation tests. Collider and overlap checks.
- Animator state tests. Navigation tests. Interaction tests. Save/load tests.
- UI interaction tests. Long-duration gameplay tests. Android build validation when authorized.
Run the game and test the entire playable route from start to finish.
Do not just inspect scripts. Actually run the game and observe the results.

## 15. FINAL STRICT REPORT
Before saying the project is complete, provide:
1. Total assets inspected. 2. Total models checked. 3. Total scripts checked.
4. Console errors before and after. 5. Bugs found. 6. Bugs fixed. 7. Bugs remaining.
8. Performance problems found and fixed. 9. Animation problems found and fixed.
10. NPC and monster AI problems found and fixed. 11. Graphics problems found and fixed.
12. Tests actually executed and their results. 13. Tests NOT executed and why.
14. List of files changed. 15. Backup or Git commit reference.
For every remaining issue, explain its location, cause and proposed fix.

ABSOLUTE RULES:
- Do not hide bugs. Do not fake test results. Do not mark untested features as passed.
- Do not remove working features to hide errors. Do not replace realistic assets with cartoon assets.
- Do not make random changes without checking the result.
- Do not build the APK until I explicitly authorize it.

FINAL GOAL: A stable, smooth, realistic Indian open-world horror survival game with natural human movement, intelligent NPCs, working monsters, correct collisions, no visible model intersections, no cartoon-like assets, no major lag, and no game-breaking bugs. Inspect EVERYTHING, including the smallest details. Fix problems systematically, then test again. Do not stop after fixing only the obvious issues.
