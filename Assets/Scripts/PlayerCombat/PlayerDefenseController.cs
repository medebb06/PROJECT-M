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

    public bool IsDefending
    {
        get
        {
            return IsParrying ||
                   IsBlocking ||
                   Input.GetMouseButton(1);
        }
    }

    private IPlayerDefenseState currentState;

    private float blockInputTimer;

    public bool IsBlocking { get; private set; }
    public bool IsParrying { get; private set; }

    public float ParryWindow => parryWindow;

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
        {
            StartDefense();
        }

        if (Input.GetMouseButtonUp(1))
        {
            StopDefense();
        }
    }

    private void StartDefense()
    {
        if (player == null)
            return;

        if (!player.IsGrounded())
            return;

        // Savunmaya girerken mevcut yatay hareketi kes.
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
    }

    public bool CanParry()
    {
        return IsParrying;
    }

    public bool CanBlock()
    {
        return IsBlocking;
    }

    // =========================================================
    // PARRY FEEDBACK
    // =========================================================

    public void PlayParryFeedback()
    {
        if (combatFeedback == null)
            return;

        combatFeedback.PlayParryImpact();
    }
}