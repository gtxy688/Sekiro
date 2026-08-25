using UnityEditor;
using UnityEngine;

public static class AttackTimelinePrefs
{
    const string PlayerGuidKey = "ARPG.AttackTimeline.PlayerPrefabGuid";
    const string BossGuidKey = "ARPG.AttackTimeline.BossPrefabGuid";

    public static GameObject PlayerPrefab
    {
        get { return Load(PlayerGuidKey); }
        set { Save(PlayerGuidKey, value); }
    }

    public static GameObject BossPrefab
    {
        get { return Load(BossGuidKey); }
        set { Save(BossGuidKey, value); }
    }

    static GameObject Load(string key)
    {
        string guid = EditorPrefs.GetString(key, "");
        if (string.IsNullOrEmpty(guid)) return null;
        string path = AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrEmpty(path)) return null;
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    static void Save(string key, GameObject prefab)
    {
        if (prefab == null)
        {
            EditorPrefs.DeleteKey(key);
            return;
        }
        string path = AssetDatabase.GetAssetPath(prefab);
        string guid = AssetDatabase.AssetPathToGUID(path);
        EditorPrefs.SetString(key, guid);
    }
}
