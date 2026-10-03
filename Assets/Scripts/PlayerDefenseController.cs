using UnityEngine;

public class PlayerDefenseController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerController player;
    [SerializeField] private GameObject defenseVisual;
    [SerializeField] private CombatImpactFeedback combatFeedback;

    [Header("Parry")]
    [SerializeField] private float parryWindow = 0.12f;
    [SerializeField] private GameObject parryVisual;

    private IPlayerDefenseState currentState;
    private float blockInputTimer;

    public bool IsBlocking { get; private set; }
    public bool IsParrying { get; private set; }

    public bool IsDefending
    {
        get
        {
            return IsParrying ||
                   IsBlocking ||
                   Input.GetMouseButton(1);
        }
    }

    // Charm'lar (Kusursuz Zamanlama) pencereyi genişletebilir.
    public float ParryWindow =>
        Mathf.Max(
            0.02f,
            PlayerStats.GetOr(StatType.ParryWindow, parryWindow)
        );

    void Awake()
    {
        if (player == null)
            player = GetComponent<PlayerController>();

        if (combatFeedback == null)
            combatFeedback =
                GetComponent<CombatImpactFeedback>();

        if (defenseVisual != null)
            defenseVisual.SetActive(false);

        if (parryVisual != null)
            parryVisual.SetActive(false);
    }

    void Update()
    {
        HandleInput();
        currentState?.Tick();
    }

    private void HandleInput()
    {
        if (Input.GetMouseButtonDown(1))
            StartDefense();

        if (Input.GetMouseButtonUp(1))
            StopDefense();
    }

    private void StartDefense()
    {
        if (player == null)
            return;

        // Posture break sırasında yeni
        // block/parry başlatılamaz.
        if (!player.canControl)
            return;

        if (player.inputLocked)
            return;

        if (!player.IsGrounded())
            return;

        if (player.rb != null)
        {
            player.rb.linearVelocity =
                new Vector2(
                    0f,
                    player.rb.linearVelocity.y
                );
        }

        ChangeState(
            new PlayerParryState(
                this,
                player
            )
        );
    }

    private void StopDefense()
    {
        if (currentState == null)
            return;

        ChangeState(null);
    }

    public void ChangeState(
        IPlayerDefenseState newState
    )
    {
        currentState?.Exit();

        currentState = newState;

        if (currentState != null)
            currentState.Enter();
    }

    public void SetParrying(bool value)
    {
        IsParrying = value;

        if (parryVisual != null)
            parryVisual.SetActive(value);
    }

    public void SetBlocking(bool value)
    {
        IsBlocking = value;

        if (defenseVisual != null)
            defenseVisual.SetActive(value);

        if (player != null)
            player.SetBlockingAnimation(value);
    }

    public bool CanParry()
    {
        return IsParrying;
    }

    public bool CanBlock()
    {
        return IsBlocking;
    }

    public void PlayParryFeedback()
    {
        if (player != null)
            player.PlayParryAnimation();

        if (combatFeedback != null)
            combatFeedback.PlayParryImpact();
    }

    public void HandleBlockHit(
        Vector2 hitDirection,
        int postureDamage
    )
    {
        // Kan Bedeli charm'ı: block posture yerine CAN yer.
        float healthCost =
            PlayerStats.GetOr(StatType.BlockHealthCost, 0f);

        if (healthCost > 0f)
        {
            PayBlockWithHealth(healthCost, hitDirection);
            return;
        }

        PlayerPosture posture =
            GetComponent<PlayerPosture>();

        if (posture == null)
        {
            Debug.LogWarning(
                "PlayerDefenseController: " +
                "PlayerPosture bulunamadı!"
            );

            return;
        }

        posture.TakePostureDamage(
            postureDamage
        );

        Debug.Log(
            "PLAYER BLOCK → POSTURE -" +
            postureDamage +
            " | CURRENT: " +
            posture.CurrentPosture +
            "/" +
            posture.MaxPosture
        );

        if (posture.IsBroken)
        {
            Debug.Log(
                "PLAYER POSTURE BROKEN!"
            );

            // Önce mevcut block/parry'yi kapat.
            ChangeState(null);

            if (player != null)
            {
                player.ApplyPostureBreak(
                    hitDirection
                );
            }
        }
    }

    // Block bedeli candan ödenir. ÖLDÜRMEZ: en az 1 can bırakır
    // (block'lamak hiçbir zaman ölüm nedeni olmasın).
    private void PayBlockWithHealth(
        float healthCost,
        Vector2 hitDirection
    )
    {
        Health health = GetComponent<Health>();

        if (health == null || health.IsDead)
            return;

        int amount =
            Mathf.Max(
                1,
                Mathf.RoundToInt(health.MaxHealth * healthCost)
            );

        amount =
            Mathf.Min(amount, health.CurrentHealth - 1);

        if (amount <= 0)
            return;

        health.TakeDamage(amount);

        CombatEvents.RaisePlayerDamaged(
            new PlayerDamageReport
            {
                amount = amount,
                healthAfter = health.CurrentHealth,
                maxHealth = health.MaxHealth,
                lethal = false,
                kind = PlayerHitKind.BlockCost,
                source = null,
                direction = hitDirection
            }
        );

        Debug.Log(
            "PLAYER BLOCK → KAN BEDELİ -" + amount + " can"
        );
    }
}
