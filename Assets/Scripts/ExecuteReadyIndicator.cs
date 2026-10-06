using UnityEngine;

/// <summary>
/// İNFAZ HAZIR GÖSTERGESİ: infaz barı doluyken oyuncunun çevresinde beyaz bir ana hat
/// (outline) yanıp söner; yakında düşman varsa kafasının üstünde "E" işareti çıkar.
/// Kendi kendini kurar (oyuncuya eklenir).
/// </summary>
public class ExecuteReadyIndicator : MonoBehaviour
{
    [Header("Ana hat")]
    [Tooltip("Ana hat kalınlığı (sprite pikseli). 1 = tek piksel.")]
    public float outlinePixels = 1f;

    public Color outlineColor = new Color(1f, 1f, 1f, 1f);

    [Range(0f, 1f)] public float minAlpha = 0.55f;
    [Range(0f, 1f)] public float maxAlpha = 1f;
    public float pulseSpeed = 4f;

    [Header("E işareti")]
    [Tooltip("Düşman bu kadar yakındaysa 'E' işareti çıkar.")]
    public float markerRange = 4f;

    private PlayerController player;
    private SpriteRenderer source;

    private SpriteRenderer[] copies;
    private Transform root;

    private static readonly Vector2[] Dirs =
    {
        new Vector2(1, 0), new Vector2(-1, 0), new Vector2(0, 1), new Vector2(0, -1),
        new Vector2(0.7f, 0.7f), new Vector2(-0.7f, 0.7f), new Vector2(0.7f, -0.7f), new Vector2(-0.7f, -0.7f)
    };

    private float visible;     // 0..1 yumuşak
    private bool nearEnemy;

    private GUIStyle keyStyle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Boot()
    {
        if (FindFirstObjectByType<ExecuteReadyIndicator>() != null)
            return;

        new GameObject("ExecuteReadyIndicator").AddComponent<ExecuteReadyIndicator>();
    }

    private bool Bind()
    {
        if (player == null)
            player = FindFirstObjectByType<PlayerController>();

        if (player == null || player.playerSprite == null)
            return false;

        if (source != player.playerSprite)
        {
            source = player.playerSprite;
            BuildCopies();
        }

        return copies != null;
    }

    private void BuildCopies()
    {
        if (root != null)
            Destroy(root.gameObject);

        GameObject go = new GameObject("ExecuteOutline");
        root = go.transform;
        root.SetParent(source.transform, false);

        // Düz BEYAZ siluet: sprite renklerini yok sayan shader (ExecuteOutlineWhite.shader).
        // Bulunamazsa font shader'ı (aynı işi görür), o da yoksa varsayılan.
        Shader shader = Shader.Find("ProjectM/SpriteWhite");

        if (shader == null)
            shader = Shader.Find("GUI/Text Shader");

        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        Material mat = shader != null ? new Material(shader) : null;

        copies = new SpriteRenderer[Dirs.Length];

        for (int i = 0; i < Dirs.Length; i++)
        {
            GameObject c = new GameObject("o" + i);
            c.transform.SetParent(root, false);

            SpriteRenderer sr = c.AddComponent<SpriteRenderer>();

            if (mat != null)
                sr.sharedMaterial = mat;

            copies[i] = sr;
        }
    }

    private bool Ready()
    {
        if (RunUI.MenuVisible)
            return false;

        if (player == null || !player.canControl && !(player.stateMachine != null && player.stateMachine.CurrentState is PlayerExecuteChargeState))
            return false;

        Health h = player.GetComponent<Health>();

        if (h != null && h.IsDead)
            return false;

        // Sadece sersemlemiş bir düşman menzildeyken ve (bar dolu ya da ölümcül vuruş açıkken).
        return ExecuteMeter.CanExecute &&
               ExecuteMeter.Instance != null &&
               PlayerFinisher.AnyStaggeredInReach(player.transform.position, 9f);
    }

    private void LateUpdate()
    {
        if (!Bind())
            return;

        float target = Ready() ? 1f : 0f;

        visible = Mathf.MoveTowards(visible, target, Time.unscaledDeltaTime * 6f);

        bool show = visible > 0.01f && source.enabled && source.sprite != null;

        for (int i = 0; i < copies.Length; i++)
        {
            SpriteRenderer sr = copies[i];

            if (sr == null)
                continue;

            sr.enabled = show;

            if (!show)
                continue;

            sr.sprite = source.sprite;
            sr.flipX = source.flipX;
            sr.flipY = source.flipY;
            sr.sortingLayerID = source.sortingLayerID;
            sr.sortingOrder = source.sortingOrder - 1;

            float pulse = Mathf.Lerp(minAlpha, maxAlpha, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * pulseSpeed));

            Color c = outlineColor;
            c.a = pulse * visible;
            sr.color = c;

            // 1 sprite pikseli = 1 / pixelsPerUnit (sprite'ın yerel birimi): ölçekten bağımsız.
            float ppu = source.sprite.pixelsPerUnit > 0f ? source.sprite.pixelsPerUnit : 100f;

            Vector2 d = Dirs[i] * (outlinePixels / ppu);

            sr.transform.localPosition = new Vector3(d.x, d.y, 0f);
        }

        // Yakında düşman var mı? (E işareti için)
        nearEnemy = false;

        if (show)
        {
            for (int i = 0; i < EnemyController.All.Count; i++)
            {
                EnemyController e = EnemyController.All[i];

                if (e == null || e.IsDead)
                    continue;

                if (Vector2.Distance(player.transform.position, e.transform.position) <= markerRange)
                {
                    nearEnemy = true;
                    break;
                }
            }
        }
    }

    private void OnGUI()
    {
        if (Event.current.type != EventType.Repaint || visible <= 0.01f || !nearEnemy || player == null)
            return;

        Camera cam = Camera.main;

        if (cam == null)
            return;

        float topY = source != null ? source.bounds.max.y : player.transform.position.y + 1.5f;

        Vector3 sp = cam.WorldToScreenPoint(new Vector3(player.transform.position.x, topY + 0.45f, 0f));

        if (sp.z < 0f)
            return;

        if (keyStyle == null)
        {
            keyStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
        }

        float s = Mathf.Max(0.6f, Screen.height / 720f);

        float size = 22f * s;
        float bob = Mathf.Sin(Time.unscaledTime * 5f) * 3f * s;

        float x = sp.x - size * 0.5f;
        float y = Screen.height - sp.y - size * 0.5f + bob;

        Color old = GUI.color;

        float a = visible * (0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * pulseSpeed));

        // Çerçeve + kutu
        GUI.color = new Color(1f, 1f, 1f, a);
        GUI.DrawTexture(new Rect(x - 2f, y - 2f, size + 4f, size + 4f), Texture2D.whiteTexture);

        GUI.color = new Color(0.08f, 0.08f, 0.1f, 0.9f * visible);
        GUI.DrawTexture(new Rect(x, y, size, size), Texture2D.whiteTexture);

        keyStyle.fontSize = Mathf.RoundToInt(14f * s);

        GUI.color = new Color(1f, 1f, 1f, a);
        GUI.Label(new Rect(x, y, size, size), "E", keyStyle);

        GUI.color = old;
    }
}
