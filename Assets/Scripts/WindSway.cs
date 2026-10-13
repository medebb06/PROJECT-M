using UnityEngine;

/// <summary>
/// RÜZGÂR — bu bitkinin (ağaç / çalı / ot / yaprak) kendi ritmi.
/// Asıl hareketi SilhouetteRim shader'ı yapar (kök sabit, tepe salınır, yapraklar titrer,
/// ışık eğilmeye göre değişir). Bu bileşen her objeye ayrı faz ve hız verir ve
/// objenin taban/yüksekliğini shader'a bildirir; ağacın gölgesine de aynısını uygular.
///
/// Genel rüzgâr gücü: [ATMOSPHERE] > AtmosphereLight > Wind Strength.
/// Salınım miktarı/hızı: materyalde (M_Rim_Tree / BackBush / GroundBush / Foreground > Wind).
/// </summary>
// Parallax (10000) kameradan sonra kaydırır; bu da ondan sonra çalışsın.
[DefaultExecutionOrder(10020)]
[ExecuteAlways]
[DisallowMultipleComponent]
public class WindSway : MonoBehaviour
{
    [Tooltip("Ritim fazı (radyan). -1 = konuma göre otomatik.")]
    public float phase = -1f;

    [Tooltip("Hız çarpanı. 0 = otomatik (0.8 – 1.25 arası, her objede farklı).")]
    [Range(0f, 2f)] public float speedMultiplier = 0f;

    private Renderer rend;
    private Renderer shadow;
    private MaterialPropertyBlock mpb;
    private float nextShadowCheck;
    private float autoPhase;
    private float autoSpeed;

    private static readonly int WindParamsId = Shader.PropertyToID("_WindParams");
    private static readonly int ShadowAmountId = Shader.PropertyToID("_ShadowWindAmount");
    private static readonly int ShadowSpeedId = Shader.PropertyToID("_ShadowWindSpeed");
    private static readonly int WindAmountId = Shader.PropertyToID("_WindAmount");
    private static readonly int WindSpeedId = Shader.PropertyToID("_WindSpeed");

    private void OnEnable()
    {
        rend = GetComponent<Renderer>();
        mpb = new MaterialPropertyBlock();

        // Konumdan kararlı rastgele değerler: her obje farklı ama her açılışta aynı.
        Vector3 p = transform.position;
        float hsh = Mathf.Abs(Mathf.Sin(p.x * 12.9898f + p.y * 78.233f) * 43758.5453f) % 1f;

        autoPhase = hsh * Mathf.PI * 2f;
        autoSpeed = Mathf.Lerp(0.8f, 1.25f, (hsh * 7.31f) % 1f);

        nextShadowCheck = 0f;
        shadow = null;
    }

    private void LateUpdate()
    {
        if (rend == null)
            return;

        Bounds b = rend.bounds;

        Vector4 wp = new Vector4(
            phase >= 0f ? phase : autoPhase,
            b.min.y,
            Mathf.Max(0.01f, b.size.y),
            speedMultiplier > 0f ? speedMultiplier : autoSpeed
        );

        rend.GetPropertyBlock(mpb);
        mpb.SetVector(WindParamsId, wp);
        rend.SetPropertyBlock(mpb);

        // Ağaç gölgesi (ProjectedShadow, sadece Play'de oluşur) da aynı ritimle sallansın.
        if (Time.unscaledTime >= nextShadowCheck)
        {
            nextShadowCheck = Time.unscaledTime + 1f;
            shadow = null;

            for (int i = 0; i < transform.childCount; i++)
            {
                Transform c = transform.GetChild(i);

                if (c.name == "~shadow")
                {
                    shadow = c.GetComponent<Renderer>();
                    break;
                }
            }
        }

        if (shadow != null)
        {
            Material m = rend.sharedMaterial;
            float amount = m != null && m.HasProperty(WindAmountId) ? m.GetFloat(WindAmountId) : 0f;
            float speed = m != null && m.HasProperty(WindSpeedId) ? m.GetFloat(WindSpeedId) : 1f;

            shadow.GetPropertyBlock(mpb);
            mpb.SetVector(WindParamsId, wp);
            mpb.SetFloat(ShadowAmountId, amount);
            mpb.SetFloat(ShadowSpeedId, speed);
            shadow.SetPropertyBlock(mpb);
        }
    }
}
