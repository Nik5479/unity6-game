using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// ROUND 6 (2026-11-10): the user's v1.0.7 phone verdict was "sab aar
/// paar ho raha hai" — everything clips through everything. The village
/// had almost no colliders (only feet-grounding was fixed in 5c).
/// Round 6 adds: static blocks for the temple bell tower, the solid
/// gate, Nandi, Matka, Basket, FallenPine, SmallGrave (plus the houses /
/// temple / haveli / walls / fences / well / carts / tree trunks that
/// were already FullBounds / Trunk / Block), and dynamic blockers for
/// NPCs, animals and the monster. This test proves, on the real build:
///   (a) StaticBlockers — CanMoveTo is FALSE inside House_2, the temple
///       bell tower, the gate, the well, and TRUE on the open chowk,
///       the north lane, and the 1.4 m gap between House_3 and the
///       temple (the new colliders must not seal the south lane).
///   (b) DynamicBlockers — Meera blocks the player: driving into her
///       stops ~0.9 m from her centre (her 0.45 m radius + the player's
///       0.45 m); CanMoveToDynamic at her position is false while
///       static CanMoveTo there is true (she is a soft, moving
///       blocker, not a wall).
///   (c) DoorEnterable — with the door CLOSED CanMoveTo at the doorway
///       is false; opening it via the real TryInteract path and driving
///       through puts the player INSIDE the house (IsInsidePlayerHouse),
///       and CanMoveTo at the doorway is true once open.
/// The test assembly cannot reference game classes (CS0246), so
/// VillageColliders is reached by reflection through the
/// PlayerController's assembly, the same way FeetOnStepsTest does it.
/// </summary>
public class ColliderTest
{
    private System.Type vcType;
    private MethodInfo canMoveTo;
    private MethodInfo canMoveToDynamic;

    [UnityTest]
    public IEnumerator Round6CollidersBlockAndDoor()
    {
        if (!Application.isPlaying)
        {
            yield break;
        }

        yield return SceneManager.LoadSceneAsync("Main");

        yield return new WaitForSeconds(1f);
        float modelWait = 0f;
        while (GameObject.Find("ModelsLoaded") == null && modelWait < 150f)
        {
            yield return new WaitForSeconds(0.5f);
            modelWait += 0.5f;
        }
        Debug.Log("[ColliderTest] Waited " + modelWait + "s for models; loaded="
            + (GameObject.Find("ModelsLoaded") != null));
        yield return new WaitForSeconds(1.5f);

        var player = GameObject.Find("Player");
        Assert.IsNotNull(player, "Player not found in Main scene");
        var cam = Camera.main;
        Assert.IsNotNull(cam, "Main Camera not found in Main scene");
        Component pc = player.GetComponent("PlayerController") as Component;
        Assert.IsNotNull(pc, "PlayerController not found on Player");

        // VillageColliders lives in the game assembly; reach it through
        // the PlayerController's assembly.
        vcType = pc.GetType().Assembly.GetType("VillageColliders");
        Assert.IsNotNull(vcType, "VillageColliders type not found in game assembly");
        canMoveTo = vcType.GetMethod("CanMoveTo",
            BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(canMoveTo, "VillageColliders.CanMoveTo missing");
        canMoveToDynamic = vcType.GetMethod("CanMoveToDynamic",
            BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(canMoveToDynamic, "VillageColliders.CanMoveToDynamic missing");

        // ---- (a) Static blockers. pos.y is the player CENTRE (0.5 m
        // above ground), radius 0.45 m — the same values TryMove uses.
        Assert.IsFalse(CanMoveToAt(9f, 8f),
            "House_2 centre (9,8) is walkable — the player can still walk through the house");
        Assert.IsFalse(CanMoveToAt(2f, -10.5f),
            "Temple bell tower (2,-10.5) is walkable — the player can still walk through the tower");
        Assert.IsFalse(CanMoveToAt(0f, 20.5f),
            "Gate centre (0,20.5) is walkable — the player can still walk through the solid gate");
        Assert.IsFalse(CanMoveToAt(-4f, -3.5f),
            "Well (-4,-3.5) is walkable — the player can still walk through the well");
        Assert.IsTrue(CanMoveToAt(0f, 0f),
            "Chowk (0,0) is BLOCKED — a collider is sealing the open chowk");
        Assert.IsTrue(CanMoveToAt(0f, 10f),
            "North lane (0,10) is BLOCKED — a collider is sealing the main lane");
        Assert.IsTrue(CanMoveToAt(-3.5f, -12f),
            "Gap between House_3 and the temple (-3.5,-12) is BLOCKED — the new colliders sealed the south lane");
        Debug.Log("[ColliderTest] StaticBlockers PASS — houses/tower/gate/well block, chowk/lane/gap stay open");

        // ---- (b) Dynamic blockers (Meera).
        yield return DynamicBlockerPhase(player, cam, pc);

        // ---- (c) Door enterable.
        yield return DoorEnterablePhase(player, cam, pc);

        Debug.Log("[ColliderTest] PASS — static + dynamic blockers and the door all verified");
        yield return null;
    }

    // (b) Meera blocks the player. ALL movers are frozen first and
    // she is parked at (-2, 4) so the test is deterministic.
    // WHY: a roaming animal can stand on the test spot and trap the
    // player inside its blocker circle — every small step stays inside
    // the circle, so the player cannot move at all and the drive reads
    // as "failed", not as a blocker proof. The prime suspect is the
    // Horse (home (2,5), roam r3 covers the old test spot (2.0, 6.5),
    // exactly where the player was parked — build 42781150374 failed
    // with "did not move toward Meera at all (still 1.50 m away)").
    // The new spot (-2, 4) is outside every animal's roam radius and
    // every other NPC's waypoint path, so it is clear even before
    // freezing. The dynamic blocker stays registered while a routine
    // is disabled (registered in Start, not Update).
    private IEnumerator DynamicBlockerPhase(GameObject player, Camera cam, Component pc)
    {
        System.Type npcType = pc.GetType().Assembly.GetType("NpcRoutine");
        System.Type animalType = pc.GetType().Assembly.GetType("AnimalRoam");
        var frozen = new System.Collections.Generic.List<Behaviour>();
        foreach (System.Type t in new System.Type[] { npcType, animalType })
        {
            if (t == null) continue;
            foreach (Object o in Object.FindObjectsOfType(t))
            {
                Behaviour b = o as Behaviour;
                if (b != null && b.enabled) { b.enabled = false; frozen.Add(b); }
            }
        }
        Debug.Log("[ColliderTest] Froze " + frozen.Count + " movers (NPC routines + animal roams)");

        try
        {
            GameObject meera = GameObject.Find("Meera");
            Assert.IsNotNull(meera, "Meera not found for dynamic-blocker test");
            int dynCount = (int)vcType.GetProperty("DynamicBlockerCount",
                BindingFlags.Public | BindingFlags.Static).GetValue(null, null);
            Debug.Log("[ColliderTest] DynamicBlockerCount at test start: " + dynCount);
            Assert.GreaterOrEqual(dynCount, 3,
                "Too few dynamic blockers (" + dynCount + ") — NPCs/animals are not registering");

            meera.transform.position = new Vector3(-2f, 0f, 4f);
            meera.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            yield return null;
            yield return null;

            Vector3 meeraPos = meera.transform.position;
            // Her exact position: dynamic says NO, static says YES.
            bool dynAtHer = (bool)canMoveToDynamic.Invoke(null,
                new object[] { new Vector3(meeraPos.x, 0.5f, meeraPos.z), 0.45f, null });
            Assert.IsFalse(dynAtHer,
                "CanMoveToDynamic at Meera's position is TRUE — she does not block the player");
            bool statAtHer = (bool)canMoveTo.Invoke(null,
                new object[] { new Vector3(meeraPos.x, 0.5f, meeraPos.z), 0.45f });
            Assert.IsTrue(statAtHer,
                "Static CanMoveTo at Meera's position is FALSE — she is registered as a wall, not a soft blocker");

            // Drive into her from 1.5 m east. She must stop the player at
            // ~0.9 m (0.45 + 0.45); he must never reach her centre.
            player.transform.position = new Vector3(meeraPos.x + 1.5f, 0.5f, meeraPos.z);
            yield return null;
            yield return null;
            // Guard: the player must not be trapped at the start (inside
            // another blocker). If this fails the test spot is bad — the
            // drive would read as "failed" instead of proving the block.
            bool dynAtStart = (bool)canMoveToDynamic.Invoke(null,
                new object[] { new Vector3(player.transform.position.x, 0.5f, player.transform.position.z), 0.45f, null });
            Assert.IsTrue(dynAtStart,
                "Player start position is already blocked by another dynamic blocker — the test spot is occupied, not a Meera proof");
            FieldInfo moveField = pc.GetType().GetField("moveInput");
            Assert.IsNotNull(moveField, "moveInput field missing for dynamic-blocker drive");
            float yaw = cam.transform.eulerAngles.y * Mathf.Deg2Rad;
            Vector3 fwd = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
            Vector3 right = new Vector3(fwd.z, 0f, -fwd.x);
            Vector3 want = new Vector3(meeraPos.x - player.transform.position.x, 0f,
                meeraPos.z - player.transform.position.z);
            want.Normalize();
            float h = Vector3.Dot(want, right);
            float v = Vector3.Dot(want, fwd);
            float drive = 0f;
            while (drive < 1.5f)
            {
                moveField.SetValue(pc, new Vector2(h, v));
                yield return null;
                drive += Time.deltaTime;
            }
            moveField.SetValue(pc, Vector2.zero);
            float dist = Vector3.Distance(
                new Vector3(player.transform.position.x, 0f, player.transform.position.z),
                new Vector3(meeraPos.x, 0f, meeraPos.z));
            Debug.Log("[ColliderTest] Drove into Meera for 1.5 s from 1.5 m: final distance "
                + dist.ToString("F2") + " m (expect ~0.9 m — the blocker boundary)");
            Assert.GreaterOrEqual(dist, 0.8f,
                "Player got to " + dist.ToString("F2") + " m from Meera's centre — walked THROUGH her");
            Assert.Less(dist, 1.5f,
                "Player did not move toward Meera at all (still " + dist.ToString("F2") + " m away) — drive failed, not a blocker proof");
            Debug.Log("[ColliderTest] DynamicBlockers PASS — Meera blocks at ~0.9 m");
        }
        finally
        {
            foreach (Behaviour b in frozen) { if (b != null) b.enabled = true; }
        }
    }

    // (c) The doorway is a wall while the door is closed and open once
    // it opens; driving through the open door puts the player inside.
    // Follows the ScreenshotTest door walk-in (real TryInteract path).
    private IEnumerator DoorEnterablePhase(GameObject player, Camera cam, Component pc)
    {
        Component gm = FindComponent("GameManager");
        Assert.IsNotNull(gm, "GameManager not found for door test");
        GameObject doorObj = GameObject.Find("Door_Player");
        Assert.IsNotNull(doorObj, "Door_Player not found for door test");
        Component dc = doorObj.GetComponent("DoorController") as Component;
        Assert.IsNotNull(dc, "DoorController not found for door test");
        Vector3 doorCenter = (Vector3)dc.GetType().GetProperty("DoorCenter").GetValue(dc, null);
        bool isOpen = (bool)dc.GetType().GetProperty("IsOpen").GetValue(dc, null);
        Assert.IsFalse(isOpen, "Door is already open at test start — the closed-door assert cannot run");

        // Closed door = the doorway is a wall.
        Assert.IsFalse(CanMoveToAt(doorCenter.x, doorCenter.z),
            "Doorway is walkable with the door CLOSED — the closed-door block is missing");

        // Open via the real path (teleport 1.0 m east, prompt must be active).
        player.transform.position = new Vector3(doorCenter.x + 1.0f, 0.5f, doorCenter.z);
        yield return null;
        yield return null; // let MobileControls.Update run the prompt check
        Component ui = FindComponent("MobileControls");
        Assert.IsNotNull(ui, "MobileControls not found for door test");
        bool prompt = (bool)ui.GetType().GetProperty("DoorPromptActive").GetValue(ui, null);
        Assert.IsTrue(prompt, "Door prompt NOT active 1 m from the closed door");
        gm.GetType().GetMethod("TryInteract").Invoke(gm, null);
        yield return new WaitForSeconds(1.5f);
        isOpen = (bool)dc.GetType().GetProperty("IsOpen").GetValue(dc, null);
        Assert.IsTrue(isOpen, "Door did not open via TryInteract");

        // Open door = the doorway is clear.
        Assert.IsTrue(CanMoveToAt(doorCenter.x, doorCenter.z),
            "Doorway is still BLOCKED with the door OPEN — the closed block was not removed");

        // Walk inside: drive through the doorway (direction derived from
        // the door centre, not hardcoded).
        FieldInfo moveField = pc.GetType().GetField("moveInput");
        Assert.IsNotNull(moveField, "moveInput field missing for door walk-in");
        float yaw = cam.transform.eulerAngles.y * Mathf.Deg2Rad;
        Vector3 fwd = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
        Vector3 right = new Vector3(fwd.z, 0f, -fwd.x);
        Vector3 want = new Vector3(doorCenter.x - player.transform.position.x, 0f,
            doorCenter.z - player.transform.position.z);
        want.Normalize();
        float h = Vector3.Dot(want, right);
        float v = Vector3.Dot(want, fwd);
        float drive = 0f;
        while (drive < 2.5f)
        {
            moveField.SetValue(pc, new Vector2(h, v));
            yield return null;
            drive += Time.deltaTime;
        }
        moveField.SetValue(pc, Vector2.zero);
        System.Type gmType = FindType("GameManager");
        Assert.IsNotNull(gmType, "GameManager type not found for inside check");
        MethodInfo inside = gmType.GetMethod("IsInsidePlayerHouse",
            BindingFlags.Public | BindingFlags.Static);
        Assert.IsNotNull(inside, "IsInsidePlayerHouse not found");
        bool insideRes = (bool)inside.Invoke(null, new object[] { player.transform.position });
        Debug.Log("[ColliderTest] Door walk-in: player at " + player.transform.position + " inside=" + insideRes);
        Assert.IsTrue(insideRes,
            "Player did NOT get inside the house after driving through the open door (at " + player.transform.position + ")");
        Debug.Log("[ColliderTest] DoorEnterable PASS — closed blocks, open lets the player in");
    }

    // CanMoveTo at ground level (pos.y = player centre 0.5 m), radius 0.45 m.
    private bool CanMoveToAt(float x, float z)
    {
        return (bool)canMoveTo.Invoke(null,
            new object[] { new Vector3(x, 0.5f, z), 0.45f });
    }

    private static System.Type FindType(string name)
    {
        foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
        {
            var t = asm.GetType(name);
            if (t != null) return t;
        }
        return null;
    }

    private static Component FindComponent(string typeName)
    {
        System.Type t = FindType(typeName);
        if (t == null) return null;
        Object[] found = Object.FindObjectsOfType(t);
        if (found == null || found.Length == 0) return null;
        return found[0] as Component;
    }
}
