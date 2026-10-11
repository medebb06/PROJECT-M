using UnityEngine;

/// Geçici teşhis aracı: Player objesine ekle. Ekranın sol üstünde oyuncunun
/// kritik durumunu gösterir ve değişimleri Console'a yazar.
/// Sorun çözülünce bu bileşeni sil.
[DefaultExecutionOrder(1000)]
public class PlayerDebugHud : MonoBehaviour
{
    PlayerController p;
    string lastState = "";
    bool lastInv, lastCtl, lastDash;
    float lastScale = 1f;
    GUIStyle style;

    void Awake()
    {
        p = GetComponent<PlayerController>();
        if (p == null) p = GetComponentInParent<PlayerController>();
        if (p == null) p = FindFirstObjectByType<PlayerController>();
    }

    void Update()
    {
        if (p == null) return;
        string st = p.stateMachine != null && p.stateMachine.CurrentState != null
            ? p.stateMachine.CurrentState.GetType().Name : "null";
        if (st != lastState || p.isInvincible != lastInv || p.canControl != lastCtl
            || p.isDashing != lastDash || !Mathf.Approximately(Time.timeScale, lastScale))
        {
            Debug.Log($"[PlayerDebug t={Time.unscaledTime:F2}] state={st} invincible={p.isInvincible} " +
                      $"(hitTimer={p.hitInvincibilityTimer:F2}) canControl={p.canControl} dashing={p.isDashing} timeScale={Time.timeScale:F3}");
            lastState = st; lastInv = p.isInvincible; lastCtl = p.canControl;
            lastDash = p.isDashing; lastScale = Time.timeScale;
        }
    }

    void OnGUI()
    {
        if (p == null) return;
        if (style == null) { style = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold }; style.normal.textColor = Color.yellow; }
        string st = p.stateMachine != null && p.stateMachine.CurrentState != null
            ? p.stateMachine.CurrentState.GetType().Name : "null";
        GUI.Label(new Rect(10, 10, 700, 130),
            $"state: {st}\ninvincible: {p.isInvincible}  (hitTimer {p.hitInvincibilityTimer:F2})\n" +
            $"canControl: {p.canControl}   dashing: {p.isDashing}   attackLocked: {p.isAttackLocked}\n" +
            $"timeScale: {Time.timeScale:F3}", style);
    }
}
