using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class HealthUnitsUI : MonoBehaviour
{
    [Header("Health")]
    [SerializeField] private Health health;

    [Header("UI")]
    [SerializeField] private Transform unitContainer;
    [SerializeField] private Image unitPrefab;

    [Header("Sprites")]
    [SerializeField] private Sprite fullSprite;
    [SerializeField] private Sprite emptySprite;

    private readonly List<Image> units = new List<Image>();

    void Awake()
    {
        if (health == null)
        {
            health = GetComponentInParent<Health>();
        }
    }

    void OnEnable()
    {
        if (health != null)
        {
            health.OnHealthChanged += Refresh;
        }
    }

    void OnDisable()
    {
        if (health != null)
        {
            health.OnHealthChanged -= Refresh;
        }
    }

    void Start()
    {
        if (health == null)
        {
            Debug.LogError(
                "HealthUnitsUI: Health reference is missing!",
                gameObject
            );

            return;
        }

        if (unitContainer == null)
        {
            Debug.LogError(
                "HealthUnitsUI: Unit Container is missing!",
                gameObject
            );

            return;
        }

        if (unitPrefab == null)
        {
            Debug.LogError(
                "HealthUnitsUI: Unit Prefab is missing!",
                gameObject
            );

            return;
        }

        BuildUnits();

        Refresh(
            health.CurrentHealth,
            health.MaxHealth
        );
    }

    private void BuildUnits()
    {
        ClearUnits();

        for (int i = 0; i < health.MaxHealth; i++)
        {
            Image unit = Instantiate(
                unitPrefab,
                unitContainer
            );

            unit.gameObject.SetActive(true);

            units.Add(unit);
        }
    }

    private void Refresh(int currentHealth, int maxHealth)
    {
        if (units.Count != maxHealth)
        {
            BuildUnits();
        }

        for (int i = 0; i < units.Count; i++)
        {
            if (i < currentHealth)
            {
                units[i].sprite = fullSprite;
            }
            else
            {
                units[i].sprite = emptySprite;
            }
        }
    }

    private void ClearUnits()
    {
        for (int i = 0; i < units.Count; i++)
        {
            if (units[i] != null)
            {
                Destroy(units[i].gameObject);
            }
        }

        units.Clear();
    }
}