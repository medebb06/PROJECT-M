using UnityEngine;
using UnityEngine.UI;

public class PlayerPostureBar : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerPosture posture;
    [SerializeField] private Slider slider;

    [Header("Visibility")]
    [SerializeField] private bool hideWhenFull = true;

    private Canvas canvas;

    private void Awake()
    {
        if (posture == null)
            posture = GetComponentInParent<PlayerPosture>();

        if (slider == null)
            slider = GetComponentInChildren<Slider>();

        canvas = GetComponent<Canvas>();
    }

    private void OnEnable()
    {
        if (posture != null)
            posture.OnPostureChanged += HandlePostureChanged;
    }

    private void OnDisable()
    {
        if (posture != null)
            posture.OnPostureChanged -= HandlePostureChanged;
    }

    private void Start()
    {
        if (posture == null)
            return;

        UpdateBar(
            posture.CurrentPosture,
            posture.MaxPosture
        );
    }

    private void HandlePostureChanged(
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
            hideWhenFull)
        {
            canvas.enabled =
                current < max;
        }
    }
}