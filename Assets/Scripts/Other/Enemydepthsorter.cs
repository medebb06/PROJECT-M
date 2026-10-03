using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Düşmanlar üst üste bindiğinde okunabilir bir ÖN/ARKA düzeni verir.
/// Fiziksel çarpışma yok; sadece sprite çizim sırası (sorting order) değişir.
///
/// Öne gelenler (en önden arkaya):
///  1) Saldırı uyarısında olanlar: vuruşu en yakın olan en önde
///     (telegraph ve vuruş her zaman görünür kalır)
///  2) Saldırı sonrası recovery'dekiler (oyuncunun cezalandıracağı hedef)
///  3) Stagger'daki düşman (execute hedefi)
///  4) Diğerleri: oyuncuya yakın olan önde
///
/// Sahnede yoksa EnemyController kendiliğinden oluşturur.
/// </summary>
public class EnemyDepthSorter : MonoBehaviour
{
    private struct Entry
    {
        public EnemyController enemy;
        public float key;
    }

    private static EnemyDepthSorter instance;

    private readonly List<Entry> entries =
        new List<Entry>();

    private static readonly Comparison<Entry> ByKey =
        (a, b) => a.key.CompareTo(b.key);

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration
    )]
    private static void ResetStatics()
    {
        instance = null;
    }

    public static void EnsureExists()
    {
        if (instance != null)
            return;

        instance =
            FindFirstObjectByType<EnemyDepthSorter>();

        if (instance == null)
        {
            GameObject obj =
                new GameObject("EnemyDepthSorter");

            instance =
                obj.AddComponent<EnemyDepthSorter>();
        }
    }

    private void Awake()
    {
        if (
            instance != null &&
            instance != this
        )
        {
            Destroy(this);
            return;
        }

        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    // LateUpdate: düşman state'leri bu karenin Update'inde güncellendi.
    private void LateUpdate()
    {
        entries.Clear();

        List<EnemyController> all = EnemyController.All;

        for (int i = 0; i < all.Count; i++)
        {
            EnemyController enemy = all[i];

            if (
                enemy == null ||
                enemy.IsDead ||
                !enemy.useDepthSorting
            )
            {
                continue;
            }

            entries.Add(
                new Entry
                {
                    enemy = enemy,
                    key = DepthKey(enemy)
                }
            );
        }

        entries.Sort(ByKey);

        // index 0 = en önde (seviye 0 = orijinal sorting order).
        for (int i = 0; i < entries.Count; i++)
        {
            EnemyController enemy = entries[i].enemy;

            enemy.SetDepthLevel(
                Mathf.Min(i, enemy.depthMaxLevels)
            );
        }
    }

    // Küçük = daha önde.
    private static float DepthKey(EnemyController enemy)
    {
        float key;

        EnemyAttackState attack =
            enemy.CurrentState as EnemyAttackState;

        if (attack != null)
        {
            // Uyarıdakiler: vuruşu yaklaşan önde. Recovery: onların arkasında.
            key =
                attack.IsWindingUp
                    ? Mathf.Min(attack.RemainingWindup, 9f)
                    : 10f;
        }
        else if (enemy.IsStaggered)
        {
            key = 20f;
        }
        else
        {
            float distance = 0f;

            if (enemy.target != null)
            {
                distance =
                    Mathf.Abs(
                        enemy.transform.position.x -
                        enemy.target.position.x
                    );
            }

            key = 30f + distance;
        }

        // Eşit anahtarlarda sıra kareden kareye titremesin.
        return key + enemy.SpawnIndex * 0.0001f;
    }
}