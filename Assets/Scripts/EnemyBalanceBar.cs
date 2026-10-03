using UnityEngine;
using UnityEngine.UI;

public class EnemyBalanceBar : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EnemyBalance balance;
    [SerializeField] private Slider slider;

    [Header("Visibility")]
    [SerializeField] private bool hideWhenEmpty = true;

    private Canvas canvas;

    private void Awake()
    {
        if (balance == null)
            balance = GetComponentInParent<EnemyBalance>();

        if (slider == null)
            slider = GetComponentInChildren<Slider>();

        canvas = GetComponent<Canvas>();
    }

    private void OnEnable()
    {
        if (balance != null)
            balance.OnBalanceChanged += HandleBalanceChanged;
    }

    private void OnDisable()
    {
        if (balance != null)
            balance.OnBalanceChanged -= HandleBalanceChanged;
    }

    private void Start()
    {
        if (balance == null)
            return;

        UpdateBar(
            balance.CurrentBalance,
            balance.MaxBalance
        );
    }

    private void HandleBalanceChanged(
        int current,
        int max
    )
    {
        UpdateBar(
            current,
            max
        );
    }

    private void UpdateBar(
        int current,
        int max
    )
    {
        if (slider != null)
        {
            float percent =
                max > 0
                    ? (float)current / max
                    : 0f;

            slider.value = percent;
        }

        if (canvas != null &&
            hideWhenEmpty)
        {
            canvas.enabled =
                current > 0;
        }
    }
}