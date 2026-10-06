using UnityEngine;

/// <summary>
/// Execute (E). Tuş TAMPONLUDUR: düşman sersemlerken / saldırın sürerken /
/// hasar kilidi biterken basılan E kaybolmaz, ilk fırsatta çalışır.
/// Saldırı sırasında basılırsa saldırı iptal edilir.
/// </summary>
public class PlayerFinisher : MonoBehaviour
{
    [Header("Finisher")]
    [SerializeField] private float longReach = 9f;
    [SerializeField] private float forwardPriority = 1.5f;

    [Tooltip("Açıksa infaz düşmanın durumundan bağımsız HER ZAMAN vurur (saldırıda, sersemlemiş, hasarlı...). Kapalıysa açık anı tutturmak gerekir.")]
    [SerializeField] private bool alwaysHit = true;

    [Header("Çizgi alanı (basılı tuttukça büyür)")]
    [Tooltip("Hemen bırakınca vurulan çizgi uzunluğu.")]
    [SerializeField] private float lineMinLength = 5f;

    [Tooltip("En uzun basılı tutuşta çizgi uzunluğu.")]
    [SerializeField] private float lineMaxLength = 18f;

    [SerializeField] private Color lineColor = new Color(1f, 0.82f, 0.3f, 1f);

    [Header("Input")]
    [SerializeField] private KeyCode executeKey = KeyCode.E;

    [Tooltip("Execute tuşu bu kadar süre hatırlanır (sn).")]
    [Min(0f)]
    [SerializeField] private float inputBuffer = 0.25f;

    [Header("Basılı Tut (odak)")]
    [Tooltip("İnfaz için E'yi en az bu kadar (gerçek sn) basılı tut; daha erken bırakırsan iptal.")]
    [SerializeField] private float minHold = 0.3f;

    [Tooltip("Her EK parça için gereken ek basılı tutma süresi (gerçek sn). 1 parça = minHold, 2 = +bu, 3 = +2×bu.")]
    [SerializeField] private float segHold = 0.35f;

    [Tooltip("Odaklanırken dünyanın zaman hızı (ağır çekim).")]
    [Range(0.02f, 1f)]
    [SerializeField] private float focusStartScale = 0.04f;

    [Tooltip("Odak çok yavaş başlar, tutuldukça bu hıza doğru hızlanır (uzun odak = daha çok maruz kalma).")]
    [Range(0.02f, 1f)]
    [SerializeField] private float focusEndScale = 0.5f;

    [Tooltip("Hedef bu çarpanla menzili aşarsa odak iptal.")]
    [SerializeField] private float rangeSlack = 1.8f;

    private PlayerController player;
    private PlayerCombatController combat;

    private float bufferTimer;

    // ---- odak (basılı tutma) ----
    private bool charging;
    private float holdTime;
    private EnemyController chargeTarget;
    private int availSegs;

    /// <summary>HUD için: şu an odakta harcanacak parça sayısı (odakta değilse 0).</summary>
    public static int ChargingSegments { get; private set; }

    /// <summary>Son infazın çizgi uzunluğu (EnemyExecuteState okur).</summary>
    public static float LastLineRange { get; private set; } = 18f;

    // Basılı tutma süresine göre çizgi uzunluğu (3 parçaya ulaşma süresinde tam).
    private float LineLengthFor(float hold)
    {
        float full = minHold + (Mathf.Max(1, ExecuteMeter.Segments) - 1) * segHold;
        float t = Mathf.Clamp01(full > 0f ? hold / full : 1f);

        return Mathf.Lerp(lineMinLength, lineMaxLength, t);
    }

    // ---- görsel: büyüyen çizgi alanı ----
    private Transform lineRoot;
    private SpriteRenderer lineBody;
    private SpriteRenderer lineEdge;
    private static Sprite lineSprite;

    private void UpdateLineVfx(float dir, float length)
    {
        if (lineRoot == null)
        {
            if (lineSprite == null)
            {
                Texture2D tex = new Texture2D(1, 1);
                tex.SetPixel(0, 0, Color.white);
                tex.Apply();

                lineSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0f, 0.5f), 1f);
            }

            GameObject go = new GameObject("ExecuteLineVfx");
            go.hideFlags = HideFlags.HideInHierarchy;
            lineRoot = go.transform;

            Material mat = null;
            Shader sh = Shader.Find("Sprites/Default");

            if (sh != null)
                mat = new Material(sh);

            lineBody = NewLinePart("Body", mat, 90);
            lineEdge = NewLinePart("Edge", mat, 91);
        }

        float h = EnemyExecuteState.LineHeight * 2f;
        float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 14f);

        Vector3 origin = transform.position;

        lineRoot.position = origin;
        lineRoot.localScale = new Vector3(dir < 0f ? -1f : 1f, 1f, 1f);

        lineBody.transform.localScale = new Vector3(length, h, 1f);
        lineBody.color = new Color(lineColor.r, lineColor.g, lineColor.b, 0.14f * pulse);

        lineEdge.transform.localPosition = new Vector3(length - 0.08f, 0f, 0f);
        lineEdge.transform.localScale = new Vector3(0.08f, h, 1f);
        lineEdge.color = new Color(1f, 0.95f, 0.7f, 0.85f);
    }

    private SpriteRenderer NewLinePart(string name, Material mat, int order)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(lineRoot, false);

        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = lineSprite;
        sr.sortingOrder = order;

        if (mat != null)
            sr.sharedMaterial = mat;

        return sr;
    }

    private void HideLineVfx()
    {
        if (lineRoot != null)
        {
            Destroy(lineRoot.gameObject);
            lineRoot = null;
        }
    }

    // Bu basılı tutma süresinde harcanacak parça (mevcut parçayla sınırlı).
    private int SegsFor(float hold)
    {
        int n = 1 + Mathf.FloorToInt(Mathf.Max(0f, hold - minHold) / segHold);

        return Mathf.Clamp(n, 1, Mathf.Max(1, availSegs));
    }

    // Eldeki parçaların tamamına ulaşılan süre.
    private float MaxHold => minHold + (Mathf.Max(1, availSegs) - 1) * segHold;

    private GUIStyle barStyle;

    private void Awake()
    {
        player =
            GetComponent<PlayerController>();

        combat =
            GetComponent<PlayerCombatController>();

        if (player == null)
        {
            Debug.LogError(
                "FINISHER: PlayerController bulunamadı!"
            );
        }
    }

    private void Update()
    {
        // ODAK: E basılı tutulurken.
        if (charging)
        {
            UpdateCharge();
            return;
        }

        if (Input.GetKeyDown(executeKey))
            bufferTimer = inputBuffer > 0f ? inputBuffer : 0.0001f;

        if (bufferTimer <= 0f)
            return;

        bufferTimer -= Time.unscaledDeltaTime;

        // Hurt / dash / ölüm gibi kontrolsüz durumlarda bekle (tampon).
        if (
            player == null ||
            !player.canControl ||
            player.inputLocked ||
            player.isDashing
        )
        {
            return;
        }

        // Odak için tuş BASILI olmalı (tamponlanmış kısa basış işe yaramaz).
        if (!Input.GetKey(executeKey))
        {
            bufferTimer = 0f;
            return;
        }

        if (TryBeginCharge())
            bufferTimer = 0f;
    }

    // E'ye basıldı: uygun hedef varsa odağı başlat.
    private bool TryBeginCharge()
    {
        EnemyController target =
            FindBestTarget();

        // Hedef yoksa tampon beklemeye devam eder (denge tam o an
        // kırılabilir).
        // Her an denenebilir: hedefin açık olması şart değil (tutturmak oyuncuya kalmış).
        if (target == null)
            return false;

        // İNFAZ BARI dolu değilse infaz yok (uyarı; tampon boşalır).
        if (!ExecuteMeter.CanExecute)
        {
            ExecuteMeter.WarnNotReady(target);
            return true;
        }

        // Saldırı sürüyorsa iptal et: odak anında başlasın.
        if (combat != null)
            combat.CancelAttack();

        charging = true;
        holdTime = 0f;
        chargeTarget = target;
        availSegs = Mathf.Max(1, ExecuteMeter.Instance != null ? ExecuteMeter.Instance.FullSegments : ExecuteMeter.Segments);
        ChargingSegments = 1;

        // Hedefe dön.
        float dir = Mathf.Sign(target.transform.position.x - transform.position.x);

        if (dir != 0f)
        {
            player.facingDir = dir;

            if (player.playerSprite != null)
                player.playerSprite.flipX = dir < 0f;
        }

        player.stateMachine.ChangeState(new PlayerExecuteChargeState(player));

        ExecuteCinematic.BeginFocus(player.transform, target.transform);

        return true;
    }

    private void UpdateCharge()
    {
        // Hedef kayboldu / ölü / sersemlik bitti / çok uzaklaştı: iptal.
        if (
            chargeTarget == null ||
            chargeTarget.IsDead ||
            !(player.stateMachine.CurrentState is PlayerExecuteChargeState) ||
            Vector2.Distance(transform.position, chargeTarget.transform.position) > longReach * rangeSlack
        )
        {
            CancelCharge();
            return;
        }

        float dt = Time.unscaledDeltaTime;

        holdTime += dt;

        // Odaklanma: dünya ağır çekimde (her kare yenilenir).
        float prog = Mathf.Clamp01(MaxHold > 0f ? holdTime / MaxHold : 1f);

        HitStop.Request(0.12f, Mathf.Lerp(focusStartScale, focusEndScale, prog * prog), 8);

        ChargingSegments = holdTime >= minHold ? SegsFor(holdTime) : 1;

        float lineDir = Mathf.Sign(chargeTarget.transform.position.x - transform.position.x);

        if (lineDir == 0f)
            lineDir = player.facingDir;

        UpdateLineVfx(lineDir, LineLengthFor(holdTime));

        ExecuteCinematic.SetFocusProgress(prog);

        bool held = Input.GetKey(executeKey);

        if (!held)
        {
            if (holdTime >= minHold)
                ReleaseCharge();
            else
            {
                EnemyController hint = chargeTarget;

                CancelCharge();

                CombatCallout.PopupAbove(
                    hint,
                    "BASILI TUT",
                    new Color(0.85f, 0.85f, 0.9f),
                    0.8f
                );
            }

            return;
        }

        // Eldeki tüm parçalar dolunca kendiliğinden infaz.
        if (holdTime >= MaxHold)
            ReleaseCharge();
    }

    // Bırakıldı: hedef AÇIK ANDAysa infaz; değilse tutmadı (bar boşa gider).
    private void ReleaseCharge()
    {
        EnemyController target = chargeTarget;
        int segs = SegsFor(holdTime);

        LastLineRange = LineLengthFor(holdTime);

        HideLineVfx();

        charging = false;
        chargeTarget = null;
        ChargingSegments = 0;

        if (target == null || !ExecuteMeter.TrySpend(target, segs))
        {
            ExecuteCinematic.CancelFocus();

            RestoreFromCharge();

            return;
        }

        if (alwaysHit || target.IsOpen)
        {
            // =================================================
            // TUTTU: EnemyExecuteState → hızlı geçiş + ağır çekim + hasar.
            // =================================================

            target.Execute(alwaysHit);

            return;
        }

        // =====================================================
        // TUTMADI: harcanan parçalar boşa gitti (kalanlar durur).
        // =====================================================

        ExecuteMeter.TakePower(target);

        ExecuteCinematic.CancelFocus();

        float dir = Mathf.Sign(target.transform.position.x - transform.position.x);

        if (dir == 0f)
            dir = player.facingDir;

        float gap = Mathf.Abs(target.transform.position.x - transform.position.x);

        // Düşmana doğru kısa bir hamle; ama içinden geçmez.
        float lunge = Mathf.Clamp(gap - 0.9f, 0.4f, 1.8f);

        player.stateMachine.ChangeState(
            new PlayerExecuteFailState(player, player.stateMachine, dir, lunge)
        );

        CombatCallout.PopupAbove(
            player,
            "TUTMADI!",
            new Color(1f, 0.35f, 0.3f),
            1.1f
        );

        if (CameraShake.Instance != null)
            CameraShake.Instance.Shake(0.3f);
    }

    private void CancelCharge()
    {
        charging = false;
        chargeTarget = null;
        ChargingSegments = 0;

        HideLineVfx();

        ExecuteCinematic.CancelFocus();

        RestoreFromCharge();
    }

    // Odak durumundan normal duruma dön.
    private void RestoreFromCharge()
    {
        if (player == null || player.stateMachine == null)
            return;

        if (!(player.stateMachine.CurrentState is PlayerExecuteChargeState))
            return;

        if (player.IsGrounded())
            player.stateMachine.ChangeState(new GroundedState(player, player.stateMachine));
        else
            player.stateMachine.ChangeState(new AirState(player, player.stateMachine));
    }

    private void OnDisable()
    {
        HideLineVfx();

        if (charging)
            CancelCharge();
    }

    // ---------------- ODAK ÇUBUĞU ----------------

    private void OnGUI()
    {
        if (!charging || chargeTarget == null || Event.current.type != EventType.Repaint)
            return;

        Camera cam = Camera.main;

        if (cam == null)
            return;

        Vector3 sp = cam.WorldToScreenPoint(chargeTarget.transform.position + Vector3.up * 2.6f);

        if (sp.z < 0f)
            return;

        if (barStyle == null)
        {
            barStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold
            };
        }

        float s = Mathf.Max(0.6f, Screen.height / 720f);

        float w = 170f * s;
        float h = 10f * s;

        float x = sp.x - w * 0.5f;
        float y = Screen.height - sp.y - h * 0.5f;

        float p = Mathf.Clamp01(MaxHold > 0f ? holdTime / MaxHold : 1f);
        bool ready = holdTime >= minHold;
        int segsNow = ready ? SegsFor(holdTime) : 0;

        Color old = GUI.color;

        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.DrawTexture(new Rect(x - 2f, y - 2f, w + 4f, h + 4f), Texture2D.whiteTexture);

        GUI.color = ready ? new Color(1f, 0.82f, 0.3f) : new Color(0.75f, 0.75f, 0.8f);
        GUI.DrawTexture(new Rect(x, y, w * p, h), Texture2D.whiteTexture);

        // Parça eşikleri (1., 2., 3. parça).
        GUI.color = new Color(1f, 1f, 1f, 0.8f);

        for (int i = 0; i < availSegs; i++)
        {
            float t = minHold + i * segHold;
            float tx = x + w * (MaxHold > 0f ? t / MaxHold : 0f);

            GUI.DrawTexture(new Rect(tx - 1f, y - 3f * s, 2f, h + 6f * s), Texture2D.whiteTexture);
        }

        barStyle.fontSize = Mathf.RoundToInt(13f * s);

        string label = ready ? "BIRAK → İNFAZ ×" + segsNow : "BASILI TUT";

        GUI.color = new Color(0f, 0f, 0f, 0.9f);
        GUI.Label(new Rect(x + 1f, y - 24f * s + 1f, w, 20f * s), label, barStyle);

        GUI.color = ready ? new Color(1f, 0.9f, 0.45f) : Color.white;
        GUI.Label(new Rect(x, y - 24f * s, w, 20f * s), label, barStyle);

        GUI.color = old;
    }

    private EnemyController FindBestTarget()
    {
        EnemyController bestTarget = null;

        float bestScore =
            float.MinValue;

        float facing =
            player != null &&
            player.facingDir < 0f
                ? -1f
                : 1f;

        Vector2 playerPosition =
            transform.position;

        // Sahnedeki aktif düşmanlar (FindObjectsByType'tan ucuz).
        for (int i = 0; i < EnemyController.All.Count; i++)
        {
            EnemyController enemy = EnemyController.All[i];

            if (enemy == null)
                continue;

            // Her canlı düşman denenebilir (ölü / infaz edilen hariç).
            if (enemy.IsDead || enemy.CurrentState is EnemyExecuteState)
                continue;

            Vector2 enemyPosition =
                enemy.transform.position;

            Vector2 offset =
                enemyPosition -
                playerPosition;

            float distance =
                offset.magnitude;

            // Finisher menzili dışında
            if (distance > longReach)
                continue;

            float directionToEnemy =
                Mathf.Sign(offset.x);

            if (directionToEnemy == 0f)
                directionToEnemy = facing;

            bool isInFront =
                directionToEnemy == facing;

            // Yakın enemy daha yüksek puan
            float distanceScore =
                1f -
                Mathf.Clamp01(
                    distance /
                    longReach
                );

            // Ön taraftaki enemy'ye öncelik
            float directionScore =
                isInFront
                    ? forwardPriority
                    : 0f;

            // Açık andaki düşmana hafif tercih (ama zorunlu değil).
            float score =
                directionScore +
                distanceScore +
                (enemy.IsOpen ? 0.4f : 0f);

            if (score > bestScore)
            {
                bestScore = score;
                bestTarget = enemy;
            }
        }

        return bestTarget;
    }
}