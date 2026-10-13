#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Tools > Create Training Dummy : sahnenin görünen yerine bir dummy kurar.
public static class TrainingDummyCreator
{
    [MenuItem("Tools/Create Training Dummy")]
    public static void Create()
    {
        int enemyLayer = LayerMask.NameToLayer("Enemy");

        if (enemyLayer < 0)
        {
            Debug.LogError("'Enemy' layer'ı bulunamadı. Project Settings > Tags and Layers'a ekle.");
            return;
        }

        GameObject root = new GameObject("TrainingDummy");
        Undo.RegisterCreatedObjectUndo(root, "Create Training Dummy");

        // Konum: oyuncunun 3 birim sağı, zemine oturtulmuş (yoksa scene view merkezi).
        Vector3 pos = Vector3.zero;
        PlayerController player = Object.FindAnyObjectByType<PlayerController>();

        if (player != null)
            pos = player.transform.position + Vector3.right * 3f;
        else if (SceneView.lastActiveSceneView != null)
            pos = SceneView.lastActiveSceneView.pivot;

        pos.z = 0f;

        int groundMask = LayerMask.GetMask("Ground");
        RaycastHit2D hit = Physics2D.Raycast((Vector2)pos + Vector2.up * 6f, Vector2.down, 30f, groundMask);

        if (hit.collider != null)
            pos.y = hit.point.y;

        root.transform.position = pos;
        root.layer = enemyLayer;

        // Görsel: gövde + baş (yerleşik beyaz sprite, gri ton).
        Sprite sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");

        GameObject body = new GameObject("Body");
        body.transform.SetParent(root.transform, false);
        const float W = 7.5f;
        const float H = 15f;

        body.transform.localPosition = new Vector3(0f, H * 0.5f, 0f);

        // Sprite'ın kendi piksel boyutundan bağımsız: tam W x H dünya birimi.
        Vector2 spriteSize = sprite != null ? (Vector2)sprite.bounds.size : Vector2.one;
        body.transform.localScale = new Vector3(W / spriteSize.x, H / spriteSize.y, 1f);
        body.layer = enemyLayer;

        SpriteRenderer sr = body.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.drawMode = SpriteDrawMode.Simple;
        sr.color = new Color(0.62f, 0.55f, 0.45f);
        sr.sortingOrder = 5;

        // Çarpışma: oyuncu saldırısı bunu bulur.
        BoxCollider2D col = root.AddComponent<BoxCollider2D>();
        col.size = new Vector2(W, H);
        col.offset = new Vector2(0f, H * 0.5f);

        TrainingDummy dummy = root.AddComponent<TrainingDummy>();
        dummy.size = new Vector2(W, H);
        dummy.ApplySize();

        Selection.activeGameObject = root;
        EditorSceneManagerMarkDirty();

        Debug.Log("TrainingDummy oluşturuldu. Play'e bas, vur. T tuşu istatistikleri sıfırlar.");
    }

    private static void EditorSceneManagerMarkDirty()
    {
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }
}
#endif
