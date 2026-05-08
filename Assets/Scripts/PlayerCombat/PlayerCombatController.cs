using UnityEngine;
using System.Collections;

public class PlayerCombatController : MonoBehaviour
{
    public PlayerController player;
    public LayerMask enemyLayer;

    public float inputBufferTime = 0.2f;
    public float comboResetTime = 0.8f;

    private float bufferTimer;
    private float comboTimer;
    private int comboStep;

    [Header("Attack Range")]
    public Vector2 hitBoxSize = new Vector2(1.5f, 1.2f);

    [Header("Attack Move (Feel)")]
    public float attackMoveDistance = 0.25f;
    public float attackMoveSpeed = 6f;
    public AnimationCurve attackMoveCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Hit Stop")]
    public float hitStopTimeScale = 0.05f;
    public float hitStopDuration = 0.06f;

    private ICombatState currentState;

    Coroutine hitStopRoutine;

    void Update()
    {
        bufferTimer -= Time.deltaTime;
        comboTimer -= Time.deltaTime;

        if (Input.GetMouseButtonDown(0))
            bufferTimer = inputBufferTime;

        if (comboTimer <= 0)
            comboStep = 0;

        if (bufferTimer > 0 && currentState == null)
        {
            bufferTimer = 0;
            StartAttack();
        }

        currentState?.Tick();
    }

    void StartAttack()
    {
        Debug.Log("ATTACK START");

        comboStep = Mathf.Clamp(comboStep + 1, 1, 4);
        comboTimer = comboResetTime;

        currentState = new AttackState(
            player,
            enemyLayer,
            comboStep,
            OnAttackEnd,
            hitStopTimeScale,
            hitStopDuration,
            attackMoveDistance,
            attackMoveSpeed,
            attackMoveCurve
        );

        currentState.Enter();
    }

    void OnAttackEnd()
    {
        currentState = null;
    }

    public void DoHitStop(float duration, float timeScale)
    {
        if (hitStopRoutine != null)
            StopCoroutine(hitStopRoutine);

        hitStopRoutine = StartCoroutine(HitStopCoroutine(duration, timeScale));
    }

    IEnumerator HitStopCoroutine(float duration, float scale)
    {
        Time.timeScale = scale;

        yield return new WaitForSecondsRealtime(duration);

        Time.timeScale = 1f;
    }
}