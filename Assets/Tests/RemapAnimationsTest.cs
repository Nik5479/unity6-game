using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// OVERNIGHT R3 (EditMode only): runs the RemapMixamoClips editor tool
/// inside the CI editor (which holds the Unity license — this machine's
/// local license is dead) so the Sprint / CrouchWalk / Jump .anim clips
/// exist on disk for the BUILD step of the same CI job. The tool is
/// invoked by reflection because the test assembly cannot reference
/// editor code. The remap is deterministic (same FBX + same rig map =
/// same clips), so regenerating on every push is safe. If the remap
/// fails, this test fails — but the build still proceeds (the Tests step
/// is continue-on-error) and NileshAnimator falls back to whichever
/// clips do exist, so a remap failure can never break the game build.
/// OVERNIGHT R3b: the isPlaying guard had to GO. The CI runs this test
/// in the PlayMode phase (Assets/Tests is the PlayMode assembly), where
/// Application.isPlaying is true — so R3 SKIPPED the remap entirely and
/// NileshCrouchWalk / NileshJump .anim were never generated. PlayMode
/// tests execute INSIDE the Editor, so UnityEditor / AssetDatabase are
/// available and the remap can run here; the generated .anim files
/// persist on disk for the BUILD step of the same job.
/// </summary>
public class RemapAnimationsTest
{
    [Test]
    public void RemapMixamoClipsToNilesh()
    {
        Type remapType = null;
        foreach (Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            remapType = asm.GetType("RemapMixamoClips");
            if (remapType != null) break;
        }
        Assert.IsNotNull(remapType, "RemapMixamoClips editor tool not found in any assembly");
        MethodInfo remap = remapType.GetMethod("Remap", BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(remap, "RemapMixamoClips.Remap method not found");
        remap.Invoke(null, null);
        string[] names = { "NileshIdle", "NileshWalk", "NileshRun", "NileshSprint", "NileshCrouchWalk", "NileshJump" };
        foreach (string n in names)
        {
            AnimationClip clip = Resources.Load<AnimationClip>("Animations/" + n);
            Assert.IsNotNull(clip, "Remap did not produce Resources/Animations/" + n + ".anim");
            Debug.Log("[RemapTest] " + n + ": " + clip.length.ToString("F2") + "s");
        }
    }
}
