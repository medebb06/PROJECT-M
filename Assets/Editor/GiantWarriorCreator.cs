#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Tools > Boss > Create Giant Warrior : oyuncunun sagina, zemine oturan, vurulabilir Boss 1 kurar.
public static class GiantWarriorCreator
{
    [MenuItem("Tools/Boss/Create Giant Warrior")]
    public static void Create()
    {
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer < 0) { Debug.LogError("'Enemy' layer'i yok."); return; }

        GameObject root = new GameObject("GiantWarrior");
        Undo.RegisterCreatedObjectUndo(root, "Create Giant Warrior");

        Vector3 pos = Vector3.zero;
        PlayerController player = Object.FindAnyObjectByType<PlayerController>();
        if (player != null) pos = player.transform.position + Vector3.right * 14f;
        else if (SceneView.lastActiveSceneView != null) pos = SceneView.lastActiveSceneView.pivot;
        pos.z = 0f;

        RaycastHit2D hit = Physics2D.Raycast((Vector2)pos + Vector2.up * 10f, Vector2.down, 60f, LayerMask.GetMask("Ground"));
        if (hit.collider != null) pos.y = hit.point.y;

        root.transform.position = pos;
        root.layer = enemyLayer;

        BoxCollider2D col = root.AddComponent<BoxCollider2D>();
        TrainingDummy dummy = root.AddComponent<TrainingDummy>();
        dummy.size = new Vector2(4.6f, 6.4f);
        dummy.sizeMultiplier = 1f;
        dummy.showStatsText = false;
        dummy.maxPosture = 400f;
        dummy.ApplySize();

        // Vurus seslerini bir dusman prefab'indan al
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            GameObject pf = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (pf == null) continue;
            EnemyHitFeedback fb = pf.GetComponentInChildren<EnemyHitFeedback>(true);
            if (fb == null) continue;
            if (fb.BalanceHitSounds != null && fb.BalanceHitSounds.Length > 0) dummy.balanceHitSounds = fb.BalanceHitSounds;
            if (fb.HealthHitSounds != null && fb.HealthHitSounds.Length > 0) dummy.healthHitSounds = fb.HealthHitSounds;
            if (fb.BalanceBreakSound != null) dummy.balanceBreakSound = fb.BalanceBreakSound;
            if (dummy.balanceHitSounds != null && dummy.balanceHitSounds.Length > 0) break;
        }

        GiantWarriorRig rig = root.AddComponent<GiantWarriorRig>();
        rig.material = GetOrCreateMaterial();
        rig.Rebuild();

        Selection.activeGameObject = root;
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("GiantWarrior olusturuldu. Boyut/nefes ayarlari GiantWarriorRig'de; 'Yeniden Kur' context menusu gorseli yeniler.");
    }

    static Material GetOrCreateMaterial()
    {
        const string path = "Assets/Sprites/GiantWarrior/M_GiantWarrior.mat";
        Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m != null) return m;
        Shader sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        m = new Material(sh) { name = "M_GiantWarrior" };
        AssetDatabase.CreateAsset(m, path);
        return m;
    }
}
#endif
