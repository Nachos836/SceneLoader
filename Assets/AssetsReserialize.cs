using UnityEditor;

internal static class AssetsReserialize
{
    [MenuItem("Build/Force Reserialize Assets")]
    private static void ForceReserializeAssets()
    {
        AssetDatabase.ForceReserializeAssets();
    }
}
