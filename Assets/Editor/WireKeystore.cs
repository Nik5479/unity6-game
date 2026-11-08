// One-shot editor script: wire the stable debug keystore so every future APK
// is signed with the SAME key (fixes the v1.0.0 -> v1.0.1 install conflict).
// Run once via: -executeMethod WireKeystore.Wire  (needs a Unity license).
// Keystore: ~/workspace/unity6-game-setup/debug.keystore (alias androiddebugkey,
// storepass/keypass "android", SHA-256 1f4e6a3d...). DO NOT push debug.keystore to GitHub.
using UnityEditor;
using UnityEngine;

public static class WireKeystore
{
    public static void Wire()
    {
        PlayerSettings.Android.keystoreName = "/home/hatch/workspace/unity6-game-setup/debug.keystore";
        PlayerSettings.Android.keystorePass = "android";
        PlayerSettings.Android.keyaliasName = "androiddebugkey";
        PlayerSettings.Android.keyaliasPass = "android";
        PlayerSettings.Android.useCustomKeystore = true;
        AssetDatabase.SaveAssets();
        Debug.Log("[Keystore] Stable debug keystore wired. useCustomKeystore=" + PlayerSettings.Android.useCustomKeystore);
    }
}
