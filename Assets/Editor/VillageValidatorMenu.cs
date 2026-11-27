using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools > Validate Village (DESIGN.md §7).
/// EditMode: validates the seeded layout data
/// (VillageValidator.ValidateLayoutData) — reports only, never modifies
/// the scene. PlayMode: validates the live built village
/// (VillageBuilder.ValidateVillageNow, which logs per check and fixes
/// floating/sunk objects — its normal runtime behaviour).
/// </summary>
public static class VillageValidatorMenu
{
    [MenuItem("Tools/Validate Village")]
    public static void ValidateVillage()
    {
        if (EditorApplication.isPlaying)
        {
            VillageBuilder[] builders = Object.FindObjectsByType<VillageBuilder>(FindObjectsSortMode.None);
            if (builders.Length == 0)
            {
                EditorUtility.DisplayDialog("Validate Village",
                    "No VillageBuilder in the scene — run the Main scene first.",
                    "OK");
                return;
            }
            int errors = builders[0].ValidateVillageNow();
            string msg = errors < 0
                ? "Validator threw an exception (see console)."
                : "Live village validation finished: " + errors + " error(s).\nSee the console for the per-check log.";
            EditorUtility.DisplayDialog("Validate Village (PlayMode)", msg, "OK");
            return;
        }
        VillageValidator.CheckResult r = VillageValidator.ValidateLayoutData();
        EditorUtility.DisplayDialog("Validate Village (EditMode)",
            "Seeded layout-data validation finished: " + r.checks + " check(s), "
            + r.errors + " error(s).\nSee the console for the per-check log.\n\nNothing was modified — EditMode validation only reports.",
            "OK");
    }
}
