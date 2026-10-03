using UnityEngine;
using UnityEngine.UI;

public class HealthBarUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Health health;
    [SerializeField] private Slider slider;

    void Awake()
    {
        if (slider == null)
        {
            slider = GetComponent<Slider>();
        }
    }

    void Start()
    {
        if (health == null)
        {
            Debug.LogError(
                "HealthBarUI: Health referansı yok!",
                gameObject
            );

            return;
        }

        if (slider == null)
        {
            Debug.LogError(
                "HealthBarUI: Slider bulunamadı!",
                gameObject
            );

            return;
        }

        health.OnHealthChanged += UpdateBar;

        UpdateBar(
            health.CurrentHealth,
            health.MaxHealth
        );
    }

    void OnDestroy()
    {
        if (health != null)
        {
            health.OnHealthChanged -= UpdateBar;
        }
    }

    private void UpdateBar(int currentHealth, int maxHealth)
    {
        if (maxHealth <= 0)
            return;

        slider.minValue = 0;
        slider.maxValue = maxHealth;
        slider.value = currentHealth;

        Debug.Log(
            "HEALTH BAR UPDATE: " +
            currentHealth + " / " +
            maxHealth
        );
    }
}