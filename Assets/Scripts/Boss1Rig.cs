using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Boss 1: PSD katmanlarindan kurulan kemik hiyerarsisi + nefes (idle) + AGIR YURUYUS + kilic darbesi (saldiri).
///
/// Saldiri: PlayAttack() ile tetiklenir.
///   Gerilme (kilic yukari/arkaya, govde yaylanir, titreme = parry uyarisi)
///   -> ani Darbe (kilicin keskin yuzu yere vurur) -> Bekleme -> Toparlanma.
///
/// Yuruyus: Rigidbody2D hizindan surulur (hiz yoksa yurumez). 2 bacak sirayla adim atar, sol kol/el (legBack + legBack_knee) bastona gibi yere dayanip karsi fazda sallanir,
/// ayaklar yere SABIT basar (IK), her adimda govde coker + kamera sarsilir.
///
/// Gercek dusman: 'makeEnemy' aciksa Boss1Enemy otomatik eklenir (EnemyController, Health, EnemyBalance...).
/// Resources/Boss1/boss1_rig.json + katman PNG'lerini okur.
/// </summary>
[DefaultExecutionOrder(-50)]
[DisallowMultipleComponent]
public class Boss1Rig : MonoBehaviour
{
    [Serializable] class BoneDef { public string name; public string parent; public float px; public float py; }
    [Serializable] class LayerDef { public string name; public string file; public float x, y, w, h; public int order; public string bone; }
    [Serializable] class RigDef { public float ppu; public float originX; public float originY; public BoneDef[] bones; public LayerDef[] layers; }

    [Header("Gorunum")]
    [Tooltip("1 = PSD boyutu. Boss'u buyutup kucultur.")]
    public float scale = 1f;
    [Tooltip("Sadece Boss1Enemy YOKKEN kullanilir: isaretliyse saga bakar. (Dusman olarak oynarken yon oyuncuya gore otomatik.)")]
    public bool flipX = false;
    public Color tint = Color.white;
    public Material material;
    public string sortingLayerName = "";
    public int sortingOrder = 4;

    [Header("Gercek dusman")]
    [Tooltip("Acik: oyun basinca Boss1Enemy otomatik eklenir -> boss oyuncuyu kovalar, vurur, parry edilir, hasar alir. " +
             "Kapali: eski 'sadece animasyon' hali.")]
    public bool makeEnemy = true;

    [Header("Nefes")]
    [Range(0.05f, 1.2f)] public float breathsPerSecond = 0.22f;
    [Range(0f, 0.08f)] public float chestScale = 0.022f;
    [Range(0f, 0.3f)] public float chestLift = 0.06f;
    [Range(0f, 6f)] public float chestTiltDeg = 1.2f;
    [Range(0f, 0.2f)] public float bellyLift = 0.03f;
    [Range(0f, 6f)] public float headNodDeg = 1.8f;
    [Range(0f, 8f)] public float armSwayDeg = 2.2f;
    [Range(0f, 8f)] public float swordSwayDeg = 1.4f;
    [Range(0f, 0.1f)] public float rootSquash = 0.012f;
    [Tooltip("Sol (arka) kolun nefesle hafif sallanmasi.")]
    [Range(0f, 6f)] public float armLSwayDeg = 1.6f;

    [Header("Vurus tepkisi")]
    [Range(0f, 20f)] public float flinchDeg = 7f;
    [Range(0f, 0.6f)] public float flinchShove = 0.18f;
    [Range(4f, 30f)] public float flinchStiffness = 16f;

    [Header("Pivot / sabit ayak")]
    [Tooltip("Cokme/uzama bu noktadan olur: ARKA AYAGIN ZEMIN NOKTASI (rig uzayi, birim). Ayaklar kaymaz.")]
    public Vector2 pivotBackFoot = new Vector2(2.27f, -0.05f);
    [Tooltip("Sol (arka) kol katmaninin omuz eklemi (govde/chest uzayi, birim).")]
    public Vector2 armLPivot = new Vector2(-0.1f, 2.0f);

    [Header("Agir yuruyus")]
    public bool walkEnabled = true;
    [Tooltip("Bir adimda ayagin yerde kat ettigi mesafe (rig birimi). Buyuk = uzun, yavas adim. Rig'in menzil siniri ~1.6.")]
    [Range(0.6f, 1.6f)] public float walkStride = 1.4f;
    [Tooltip("Adim fazinin ne kadari ayak havada (0.4 = ayak %40 havada, %60 yerde; 2 bacakli yurume icin en cok 0.5). Kucuk = daha agir, daha 'basarak' yuruyus.")]
    [Range(0.2f, 0.5f)] public float walkSwing = 0.4f;
    [Tooltip("Ayagin yerden kalkma yuksekligi (rig birimi).")]
    [Range(0f, 1f)] public float walkLift = 0.5f;
    [Tooltip("Yururken govdenin one egilmesi (derece).")]
    [Range(0f, 15f)] public float walkLeanDeg = 6f;
    [Tooltip("Her ayak basisinda govdenin cokmesi (0.045 = %4.5). Agirlik hissi.")]
    [Range(0f, 0.12f)] public float walkDip = 0.045f;
    [Range(0f, 10f)] public float walkSwordSwing = 3.5f;
    [Range(0f, 20f)] public float walkArmLSwing = 9f;
    [Tooltip("Her ayak basisinda kamera sarsintisi (CameraShake.Shake). 0 = kapali.")]
    [Range(0f, 1f)] public float stepShake = 0.12f;

    [Header("Saldiri sureleri (sn)")]
    [Tooltip("Kilici arkaya/yukari kaldirip gerilme. Oyuncunun 'parry hazirla' uyarisi.")]
    [Range(0.2f, 1.5f)] public float windupTime = 0.7f;
    [Tooltip("Tam gerili halde titreyerek bekleme: darbe hemen gelir. Parry penceresine hazirlik.")]
    [Range(0f, 0.8f)] public float tensionTime = 0.3f;
    [Tooltip("ANI darbe suresi. Kisa = sert/hizli.")]
    [Range(0.04f, 0.3f)] public float strikeTime = 0.09f;
    [Tooltip("Kilic ucu yere saplanmis bekleme.")]
    [Range(0f, 0.8f)] public float holdTime = 0.25f;
    [Tooltip("Kilici kaldirip dinlenme pozuna donus.")]
    [Range(0.1f, 2f)] public float recoverTime = 0.8f;

    [Header("Saldiri pozu: GERILME (derece, bone'a gore, boss sola bakarken)")]
    [Range(-40f, 5f)] public float windupChestDeg = -14f;
    [Range(-30f, 5f)] public float windupBellyDeg = -7f;
    [Range(-20f, 20f)] public float windupHeadDeg = -4f;
    [Tooltip("Omuz: kol yukari kalkar.")]
    [Range(-180f, 0f)] public float windupArmDeg = -69.5f;
    [Tooltip("Bilek: kilic govdenin arkasina dogru yatar.")]
    [Range(0f, 260f)] public float windupSwordDeg = 105.6f;
    [Tooltip("Sol kol (geriye).")]
    [Range(-40f, 40f)] public float windupArmLDeg = -14f;
    [Range(0f, 0.12f)] public float windupCrouch = 0.04f;

    [Header("Saldiri pozu: DARBE (kilicin keskin yuzu zemine yatik vurur)")]
    [Tooltip("Govde one yatar: eli zemine yaklastirir (kilic daha yatay olur).")]
    [Range(0f, 40f)] public float strikeChestDeg = 18f;
    [Range(0f, 30f)] public float strikeBellyDeg = 10f;
    [Range(-20f, 20f)] public float strikeHeadDeg = 5f;
    [Range(-90f, 40f)] public float strikeArmDeg = 10f;
    [Tooltip("Kilicin yatiklik acisi. Kucuk deger = daha yatay (zemine daha paralel). Zemin korumasi altina inmesini engeller.")]
    [Range(100f, 260f)] public float strikeSwordDeg = 130.2f;
    [Range(-40f, 40f)] public float strikeArmLDeg = 16f;
    [Tooltip("Cokme: eli yere indirir.")]
    [Range(0f, 0.15f)] public float strikeCrouch = 0.07f;

    [Header("Zemin korumasi")]
    [Tooltip("Acik: kilic hicbir karede zemin cizgisinin altina inmez (gerekirse bilek yukari kalkar).")]
    public bool keepBladeAboveGround = true;
    [Tooltip("Zemin cizgisi (rig uzayi). Ayaklarin altiyla ayni olmali.")]
    public float groundY = -0.06f;

    [Header("Saldiri hissi")]
    [Tooltip("Gerilme sonunda kol/govde titremesi (derece).")]
    [Range(0f, 4f)] public float windupTremble = 1.2f;
    [Tooltip("Gerilme bekleme suresince fazladan ne kadar daha gerilsin (0.06 = %6).")]
    [Range(0f, 0.2f)] public float tensionOvershoot = 0.06f;
    [Tooltip("Kilic ucu yere vurunca kisa sarsinti (derece).")]
    [Range(0f, 6f)] public float impactShakeDeg = 2.5f;
    [Tooltip("0 = kapali. >0 ise Play Mode'da bu kadar saniyede bir saldiri oynatir (sadece onizleme).")]
    [Range(0f, 8f)] public float autoAttackEvery = 0f;

    /// <summary>Ani darbe basladi (gerilme bitti). Ses/uyari kapatma buraya baglanabilir.</summary>
    public event Action AttackStrikeStarted;
    /// <summary>Kilic yere vurdu. Hasar/hitbox/kamera sarsintisi buraya baglanabilir.</summary>
    public event Action AttackHit;
    /// <summary>Saldiri animasyonu tamamen bitti (dinlenme pozuna donuldu).</summary>
    public event Action AttackFinished;
    /// <summary>Bir ayak yere basti (0 = on bacak, 1 = arka bacak). Adim sesi buraya baglanabilir.</summary>
    public event Action<int> FootStep;

    public bool IsAttacking { get { return atkState != AtkState.None; } }

    /// <summary>Kilic yere indi mi (darbe ani gecti)? Hasar ancak bundan sonra verilir.</summary>
    public bool StrikeLanded { get { return atkState == AtkState.Hold || atkState == AtkState.Recover; } }

    /// <summary>
    /// Hasar kapisi: darbe animasyonu inmeden hasar cozulmesin. Animasyon yoksa (None/iptal) yeniden baslatir ve bekletir.
    /// </summary>
    public bool ReadyToHit()
    {
        if (StrikeLanded) return true;
        if (atkState == AtkState.None || atkState == AtkState.Cancel) PlayAttack();
        return false;
    }
    /// <summary>Saldiri baslayinca darbeye kadar gecen sure (oyuncunun parry icin goruntu uyarisi).</summary>
    public float TelegraphTime { get { return windupTime + tensionTime; } }
    /// <summary>Saldiri baslayinca kilicin yere VURDUGU ana kadar gecen sure. Dusmanin 'attackWarningTime'i bu olmali.</summary>
    public float ContactTime { get { return windupTime + tensionTime + strikeTime; } }
    public float AttackDuration { get { return windupTime + tensionTime + strikeTime + holdTime + recoverTime; } }

    // ---- Disaridan (Boss1Enemy) verilen durum ----
    /// <summary>Yon kaynagi: SpriteRenderer.flipX == true -> SOLA bakar (rig dogal olarak sola bakar). Bos = 'flipX' alani.</summary>
    [NonSerialized] public SpriteRenderer facingSource;
    /// <summary>false ise yurume animasyonu oynamaz (saldiri/sersem/vurulma sirasinda).</summary>
    [NonSerialized] public bool MoveAllowed = true;
    /// <summary>Sersemlemis (denge kirik) poz.</summary>
    [NonSerialized] public bool Staggered;
    /// <summary>Oldu: yikilma pozu.</summary>
    [NonSerialized] public bool Dead;

    // Kilic bicaginin dis cizgisi (sword kemigi uzayi, birim). boss1_sword.png'den turetildi.
    static readonly Vector2[] BladeHull =
    {
        new Vector2(0.95f, 1.26f), new Vector2(5.89f, 4.86f), new Vector2(6.52f, 4.95f),
        new Vector2(7.12f, 4.80f), new Vector2(7.12f, 4.69f), new Vector2(1.49f, 0.53f)
    };

    // Uzuvlar (rig uzayi, birim). Kalca/diz/bilek rest pozundan olculdu; kalca govdenin altinda gizli.
    // 2 bacak (legR on, legL arka) + sol kol (legBack = on kol, legBack_knee = el; adlari bacak gibi ama KOL).
    // 'center': adim araligi merkezi (rest'e gore x kaymasi). rigid = tek parca (kol). isArm = adim sesi/cokme tetiklemez.
    struct LegDef
    {
        public string up, lo, ft;
        public Vector2 hip, knee, ankle;
        public float center, phaseOff;
        public bool rigid, isArm;
    }

    static readonly LegDef[] Legs =
    {
        new LegDef { up = "legR_upper", lo = "legR_lower", ft = "footR",
            hip = new Vector2(0.9f, 3.1f), knee = new Vector2(-0.55f, 2.2f), ankle = new Vector2(-0.35f, 0.8f),
            center = -0.05f, phaseOff = 0f, rigid = false, isArm = false },
        new LegDef { up = "legL_upper", lo = "legL_lower", ft = "footL",
            hip = new Vector2(1.6f, 3.3f), knee = new Vector2(2.2f, 1.9f), ankle = new Vector2(2.3f, 0.8f),
            center = -0.6f, phaseOff = 0.5f, rigid = false, isArm = false },
        new LegDef { up = "legBack", lo = null, ft = "legBack_knee",
            hip = new Vector2(1.3f, 3.4f), knee = Vector2.zero, ankle = new Vector2(0.9f, 1.0f),
            center = 0f, phaseOff = 0.75f, rigid = true, isArm = true },
    };

    enum AtkState { None, Windup, Tension, Strike, Hold, Recover, Cancel }
    AtkState atkState = AtkState.None;
    float atkTimer, atkEndedAt = -999f, sinceHit = 999f;
    float lastK, lastS, cancelK, cancelS, startK, startS;
    const float CancelTime = 0.35f;

    readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
    readonly Dictionary<string, Vector3> restPos = new Dictionary<string, Vector3>();
    readonly Dictionary<string, Quaternion> restRot = new Dictionary<string, Quaternion>();
    float flinch, flinchVel, phase, animClock;

    [Header("Vurus hissi (squash/stretch + flas)")]
    [Tooltip("Vurulunca govde ne kadar ezilip esner (0 = kapali).")]
    [Range(0f, 0.4f)] public float squashAmount = 0.16f;
    [Range(6f, 40f)] public float squashStiffness = 20f;
    [Tooltip("Hasar alinca butun govdenin beyaz yanma suresi (sn, gercek zaman).")]
    [Range(0f, 0.3f)] public float flashTime = 0.09f;
    float sq, sqVel, flashTimer;
    bool flashBuilt, flashOn;
    SpriteRenderer[] flashSrs;
    Sprite[] origSprites, whiteSprites;
    float walkPhase, walkW, plantKick, staggerW, deadW;
    readonly float[] legU = { -1f, -1f, -1f };
    bool skipDt;
    Transform visual;
    Rigidbody2D body;
    bool built;

    void Awake()
    {
        EnsureBuilt();
        if (makeEnemy && Application.isPlaying && GetComponent<Boss1Enemy>() == null)
            gameObject.AddComponent<Boss1Enemy>();
    }

    /// <summary>Gorseli (kemikler + katmanlar) hazirlar. Birden fazla cagrilabilir.</summary>
    public void EnsureBuilt()
    {
        if (built) return;
        built = true;
        if (visual == null) visual = transform.Find("~boss");
        if (visual == null || visual.childCount == 0) Rebuild();
        else Cache();
        phase = UnityEngine.Random.value * 6.28f;
    }

    [ContextMenu("Yeniden Kur")]
    public void Rebuild()
    {
        built = true;
        Transform old = transform.Find("~boss");
        if (old != null)
        {
            if (Application.isPlaying) Destroy(old.gameObject);
            else DestroyImmediate(old.gameObject);
        }

        TextAsset json = Resources.Load<TextAsset>("Boss1/boss1_rig");
        if (json == null) { Debug.LogError("Boss1Rig: Resources/Boss1/boss1_rig.json bulunamadi."); return; }
        RigDef rig = JsonUtility.FromJson<RigDef>(json.text);

        GameObject v = new GameObject("~boss");
        v.transform.SetParent(transform, false);
        visual = v.transform;
        visual.localScale = new Vector3(flipX ? -scale : scale, scale, 1f);

        SortingGroup sg = v.AddComponent<SortingGroup>();
        sg.sortingLayerName = string.IsNullOrEmpty(sortingLayerName) ? "Default" : sortingLayerName;
        sg.sortingOrder = sortingOrder;

        // Kemikler (birim: kok uzayi, scale visual'de)
        bones.Clear();
        Dictionary<string, Vector2> world = new Dictionary<string, Vector2>();
        foreach (BoneDef b in rig.bones)
            world[b.name] = new Vector2((b.px - rig.originX) / rig.ppu, (rig.originY - b.py) / rig.ppu);

        foreach (BoneDef b in rig.bones) // sirali: ebeveyn once
        {
            GameObject go = new GameObject(b.name);
            Transform parent = string.IsNullOrEmpty(b.parent) ? visual : bones[b.parent];
            go.transform.SetParent(parent, false);
            Vector2 pw = string.IsNullOrEmpty(b.parent) ? Vector2.zero : world[b.parent];
            Vector2 lp = world[b.name] - pw;
            go.transform.localPosition = new Vector3(lp.x, lp.y, 0f);
            bones[b.name] = go.transform;
        }

        Material mat = material != null ? material : DefaultMaterial();

        foreach (LayerDef l in rig.layers)
        {
            Sprite sp = Resources.Load<Sprite>("Boss1/" + l.file);
            if (sp == null) { Debug.LogWarning("Boss1Rig: sprite yok: " + l.file); continue; }
            GameObject go = new GameObject(l.name);
            Transform bone = bones[l.bone];
            go.transform.SetParent(bone, false);
            Vector2 center = new Vector2((l.x + l.w * 0.5f - rig.originX) / rig.ppu, (rig.originY - (l.y + l.h * 0.5f)) / rig.ppu);
            Vector2 lp = center - world[l.bone];
            go.transform.localPosition = new Vector3(lp.x, lp.y, 0f);
            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sp;
            sr.color = tint;
            sr.sharedMaterial = mat;
            sr.sortingLayerName = string.IsNullOrEmpty(sortingLayerName) ? "Default" : sortingLayerName;
            sr.sortingOrder = l.order;
            go.layer = gameObject.layer;
        }
        v.layer = gameObject.layer;
        foreach (Transform t in bones.Values) t.gameObject.layer = gameObject.layer;
        Cache();
    }

    Material DefaultMaterial()
    {
        Shader sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        return new Material(sh) { name = "Boss1_Runtime" };
    }

    void Cache()
    {
        bones.Clear(); restPos.Clear(); restRot.Clear();
        if (visual == null) return;
        foreach (Transform t in visual.GetComponentsInChildren<Transform>(true))
        {
            if (t == visual) continue;
            // "head" ve "sword" hem kemik hem sprite katmani adi. Ilk (ust) olan KEMIK; katmanla ezme.
            if (bones.ContainsKey(t.name)) continue;
            bones[t.name] = t;
            restPos[t.name] = t.localPosition;
            restRot[t.name] = t.localRotation;
        }
    }

    void Set(string n, Vector3 dPos, float dRot, Vector3? scl = null)
    {
        Transform t;
        if (!bones.TryGetValue(n, out t)) return;
        t.localPosition = restPos[n] + dPos;
        t.localRotation = restRot[n] * Quaternion.Euler(0f, 0f, dRot);
        t.localScale = scl ?? Vector3.one;
    }

    // Katmani, ebeveyn uzayindaki 'pivot' noktasi etrafinda dondurur (kemigi olmayan katmanlar icin).
    void SetAround(string n, Vector2 pivot, float dRot)
    {
        Transform t;
        if (!bones.TryGetValue(n, out t)) return;
        Quaternion r = Quaternion.Euler(0f, 0f, dRot);
        Vector3 p = new Vector3(pivot.x, pivot.y, restPos[n].z);
        t.localPosition = p + r * (restPos[n] - p);
        t.localRotation = r * restRot[n];
    }

    // Bir katmani (kok uzayinda) 'pos' konumuna ve 'deg' dereceye koyar; w ile rest'e karistirir (0 = rest).
    void PoseLayer(string n, Vector2 pos, float deg, float w)
    {
        Transform t;
        if (n == null || !bones.TryGetValue(n, out t)) return;
        Vector3 rp = restPos[n];
        t.localPosition = Vector3.Lerp(rp, new Vector3(pos.x, pos.y, rp.z), w);
        t.localRotation = restRot[n] * Quaternion.Euler(0f, 0f, deg * w);
    }

    /// <summary>
    /// Kilic darbesi animasyonunu baslatir. HER ZAMAN baslar: onceki saldiri/iptal bitmemisse mevcut pozdan
    /// yumusakca yeni gerilmeye gecer (eskiden 'false' donup animasyonsuz hasar atiliyordu).
    /// </summary>
    [ContextMenu("Test: Saldiri Oynat")]
    public bool PlayAttack()
    {
        startK = lastK; startS = lastS;
        atkState = AtkState.Windup;
        atkTimer = 0f;
        // Dusman durumu ayni karede Enter() ile baslatiyor ama sayaci bir sonraki karede dusurmeye basliyor:
        // animasyon bir kare onde gitmesin, darbe ani ile hasar ani ayni olsun.
        skipDt = true;
        return true;
    }

    /// <summary>Gerilme/bekleme asamasindaki saldiriyi yumusakca iptal eder (dusman sersemleyince / vurulunca). Darbe sonrasi iptal edilmez.</summary>
    public bool CancelAttack()
    {
        if (atkState != AtkState.Windup && atkState != AtkState.Tension) return false;
        cancelK = lastK; cancelS = lastS;
        atkState = AtkState.Cancel;
        atkTimer = 0f;
        return true;
    }

    /// <summary>Govdeyi ezip esnetir (yay).</summary>
    public void Squash(float strength)
    {
        sqVel += Mathf.Max(0f, strength) * squashStiffness * 0.75f;
    }

    /// <summary>Butun govde kisa sure beyaza doner (hasar flasi).</summary>
    public void Flash()
    {
        if (flashTime <= 0f) return;
        if (!flashBuilt) BuildWhiteSprites();
        flashTimer = flashTime;
    }

    // Her katmanin duz beyaz silueti (okunabilir olmayan doku icin RenderTexture uzerinden okunur).
    void BuildWhiteSprites()
    {
        flashBuilt = true;
        if (visual == null) return;
        flashSrs = visual.GetComponentsInChildren<SpriteRenderer>(true);
        origSprites = new Sprite[flashSrs.Length];
        whiteSprites = new Sprite[flashSrs.Length];
        var cache = new Dictionary<Sprite, Sprite>();
        for (int i = 0; i < flashSrs.Length; i++)
        {
            Sprite sp = flashSrs[i] != null ? flashSrs[i].sprite : null;
            origSprites[i] = sp;
            if (sp == null) continue;
            Sprite w;
            if (!cache.TryGetValue(sp, out w)) { w = MakeWhite(sp); cache[sp] = w; }
            whiteSprites[i] = w;
        }
    }

    static Sprite MakeWhite(Sprite sp)
    {
        Texture2D src = sp.texture;
        Rect r = sp.textureRect;
        int rw = Mathf.Max(1, Mathf.RoundToInt(r.width)), rh = Mathf.Max(1, Mathf.RoundToInt(r.height));
        RenderTexture rt = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        Graphics.Blit(src, rt);
        RenderTexture prev = RenderTexture.active;
        RenderTexture.active = rt;
        Texture2D t = new Texture2D(rw, rh, TextureFormat.RGBA32, false);
        t.ReadPixels(new Rect(r.x, r.y, rw, rh), 0, 0);
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);
        Color32[] px = t.GetPixels32();
        for (int i = 0; i < px.Length; i++) { px[i].r = 255; px[i].g = 255; px[i].b = 255; }
        t.SetPixels32(px);
        t.Apply();
        t.filterMode = src.filterMode;
        Vector2 pivot = new Vector2(sp.pivot.x / r.width, sp.pivot.y / r.height);
        return Sprite.Create(t, new Rect(0, 0, rw, rh), pivot, sp.pixelsPerUnit, 0, SpriteMeshType.FullRect);
    }

    /// <summary>Oyuncu vurdu: govde sarsilir.</summary>
    public void Flinch(float strength)
    {
        flinchVel += Mathf.Max(0f, strength) * flinchStiffness * 1.4f;
    }

    /// <summary>Parry edildi: sert geri tepme.</summary>
    public void OnParried(bool brokeBalance)
    {
        Flinch(brokeBalance ? 2.4f : 1.6f);
        Squash(brokeBalance ? 1.6f : 1.1f);
        sinceHit = 0f;
    }

    static float EaseIn(float x) { x = Mathf.Clamp01(x); return x * x; }
    static float Smooth(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }

    // Saldiri fazini ilerletir. k = gerilme pozu miktari, s = darbe pozu miktari (0..1, k tension'da >1 olabilir).
    void TickAttack(float dt, out float k, out float s)
    {
        k = 0f; s = 0f;
        sinceHit += dt;
        if (atkState == AtkState.None)
        {
            if (autoAttackEvery > 0f && animClock - atkEndedAt >= autoAttackEvery) PlayAttack();
            if (atkState == AtkState.None) { lastK = 0f; lastS = 0f; return; }
        }

        if (skipDt) { skipDt = false; return; }

        atkTimer += dt;
        switch (atkState)
        {
            case AtkState.Windup:
                {
                    float wp = Smooth(atkTimer / windupTime);   // yavas basla, yavas otur: agir gerilme
                    k = Mathf.Lerp(startK, 1f, wp);
                    s = startS * (1f - wp);                     // onceki saldirinin darbe pozundan cikis
                }
                if (atkTimer >= windupTime) { atkState = AtkState.Tension; atkTimer = 0f; k = 1f; }
                break;
            case AtkState.Tension:
                k = 1f + tensionOvershoot * Smooth(atkTimer / Mathf.Max(0.01f, tensionTime));
                if (atkTimer >= tensionTime)
                {
                    atkState = AtkState.Strike; atkTimer = 0f;
                    if (AttackStrikeStarted != null) AttackStrikeStarted();
                }
                break;
            case AtkState.Strike:
            {
                float e = EaseIn(atkTimer / strikeTime);    // ani hizlanma
                float k0 = 1f + tensionOvershoot;
                k = k0 * (1f - e); s = e;
                if (atkTimer >= strikeTime)
                {
                    atkState = AtkState.Hold; atkTimer = 0f;
                    k = 0f; s = 1f;
                    sinceHit = 0f;
                    if (AttackHit != null) AttackHit();
                }
                break;
            }
            case AtkState.Hold:
                s = 1f;
                if (atkTimer >= holdTime) { atkState = AtkState.Recover; atkTimer = 0f; }
                break;
            case AtkState.Recover:
                s = 1f - Smooth(atkTimer / recoverTime);
                if (atkTimer >= recoverTime)
                {
                    atkState = AtkState.None; atkTimer = 0f; atkEndedAt = animClock;
                    s = 0f;
                    if (AttackFinished != null) AttackFinished();
                }
                break;
            case AtkState.Cancel:
            {
                float p = Smooth(atkTimer / CancelTime);
                k = cancelK * (1f - p); s = cancelS * (1f - p);
                if (atkTimer >= CancelTime)
                {
                    atkState = AtkState.None; atkTimer = 0f; atkEndedAt = animClock;
                    k = 0f; s = 0f;
                    if (AttackFinished != null) AttackFinished();
                }
                break;
            }
        }
        lastK = k; lastS = s;
    }

    static Vector2 Rot2(Vector2 v, float deg)
    {
        float r = deg * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
        return new Vector2(c * v.x - s * v.y, s * v.x + c * v.y);
    }

    static float Ang2(Vector2 v) { return Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg; }

    // Bacaklari yuruyus fazina gore yerlestirir. w = yuruyus agirligi (0 = rest). Ayak basinca plantKick'i tetikler.
    void UpdateLegs(float w)
    {
        for (int i = 0; i < Legs.Length; i++)
        {
            LegDef lg = Legs[i];
            float u = Mathf.Repeat(walkPhase + lg.phaseOff, 1f);

            // Ayak basma olayi: havadan yere gecis.
            if (!lg.isArm && legU[i] >= 0f && legU[i] < walkSwing && u >= walkSwing && w > 0.3f)
            {
                plantKick = 1f;
                if (stepShake > 0f && CameraShake.Instance != null) CameraShake.Instance.Shake(stepShake);
                if (FootStep != null) FootStep(i);
            }
            legU[i] = u;

            // Ayak hedefi (rig uzayi): havada arkadan one yay, yerde govdeyle birlikte geri kayar (sabit basar).
            float off, lift, toe;
            if (u < walkSwing)
            {
                float sw = u / walkSwing;
                off = walkStride * 0.5f - walkStride * Smooth(sw);
                lift = walkLift * (lg.isArm ? 0.8f : 1f) * Mathf.Sin(Mathf.PI * sw);
                toe = 12f * Mathf.Sin(2f * Mathf.PI * sw);      // topuk kalkar -> topuk basar
            }
            else
            {
                float q = (u - walkSwing) / (1f - walkSwing);
                off = -walkStride * 0.5f + walkStride * q;
                lift = 0f; toe = 0f;
            }
            Vector2 T = new Vector2(lg.ankle.x + lg.center + off, lg.ankle.y + lift);

            Vector2 H = lg.hip, A0 = lg.ankle;
            Vector2 restUp = restPos.ContainsKey(lg.up) ? (Vector2)restPos[lg.up] : Vector2.zero;
            Vector2 restFt = restPos.ContainsKey(lg.ft) ? (Vector2)restPos[lg.ft] : Vector2.zero;

            if (lg.rigid)
            {
                // Tek parca bacak: kalca etrafinda doner, sonra ayak zemine oturacak sekilde dikey kayar.
                float a = Ang2(T - H) - Ang2(A0 - H);
                Vector2 end = H + Rot2(A0 - H, a);
                Vector2 sh = new Vector2(0f, T.y - end.y);
                PoseLayer(lg.up, H + Rot2(restUp - H, a) + sh, a, w);
                PoseLayer(lg.ft, end + sh + Rot2(restFt - A0, toe), toe, w);
            }
            else
            {
                // Iki kemikli IK: kalca-diz-bilek. Diz, rest'teki tarafa bukulur.
                Vector2 K0 = lg.knee;
                Vector2 restLo = restPos.ContainsKey(lg.lo) ? (Vector2)restPos[lg.lo] : Vector2.zero;
                float L1 = (K0 - H).magnitude, L2 = (A0 - K0).magnitude;
                Vector2 d = T - H;
                float dist = Mathf.Clamp(d.magnitude, Mathf.Abs(L1 - L2) + 0.01f, L1 + L2 - 0.01f);
                float baseAng = Ang2(d);
                float cosA = (L1 * L1 + dist * dist - L2 * L2) / (2f * L1 * dist);
                float aHip = Mathf.Acos(Mathf.Clamp(cosA, -1f, 1f)) * Mathf.Rad2Deg;
                Vector2 ha = A0 - H, hk = K0 - H;
                float side = (ha.x * hk.y - ha.y * hk.x) >= 0f ? 1f : -1f;
                float thighAng = baseAng + side * aHip;
                Vector2 K1 = H + L1 * new Vector2(Mathf.Cos(thighAng * Mathf.Deg2Rad), Mathf.Sin(thighAng * Mathf.Deg2Rad));
                float a1 = thighAng - Ang2(K0 - H);
                float a2 = Ang2(T - K1) - Ang2(A0 - K0);
                PoseLayer(lg.up, H + Rot2(restUp - H, a1), a1, w);
                PoseLayer(lg.lo, K1 + Rot2(restLo - K0, a2), a2, w);
                PoseLayer(lg.ft, T + Rot2(restFt - A0, toe), toe, w);
            }
        }
    }

    void LateUpdate()
    {
        if (visual == null || bones.Count == 0) return;
        float dtR = Time.deltaTime;
        float dt = EnemyTime.DeltaTime;      // dusman zamani: parry yavaslatmasinda animasyon da yavaslar
        animClock += dt;

        // ---- Hasar flasi: butun govde ayni karede beyaz silüete doner ----
        if (flashTimer > 0f) flashTimer -= Time.unscaledDeltaTime;
        bool wantFlash = flashTimer > 0f && flashBuilt && flashSrs != null;
        if (wantFlash != flashOn && flashSrs != null)
        {
            flashOn = wantFlash;
            for (int i = 0; i < flashSrs.Length; i++)
            {
                if (flashSrs[i] == null || origSprites[i] == null) continue;
                flashSrs[i].sprite = (flashOn && whiteSprites[i] != null) ? whiteSprites[i] : origSprites[i];
            }
        }

        // ---- Yon: dogal olarak SOLA bakar. facingSource.flipX true = sola; false = saga ----
        bool faceRight = facingSource != null ? !facingSource.flipX : flipX;
        float sgn = faceRight ? -1f : 1f;
        float sc = Mathf.Abs(scale);

        // Squash/stretch yayi: pozitif = ezilir (yassilir, genisler), negatif = esner.
        sqVel += (-sq * squashStiffness * squashStiffness - sqVel * squashStiffness * 0.5f) * Time.unscaledDeltaTime;
        sq = Mathf.Clamp(sq + sqVel * Time.unscaledDeltaTime, -1.2f, 1.2f);
        float sqx = 1f + sq * squashAmount, sqy = 1f - sq * squashAmount;
        visual.localScale = new Vector3(sgn * sc * sqx, sc * sqy, 1f);
        const float dir = 1f;   // ayna visual'in x olceginde; aci/konum islemleri yerelde, tek yon

        // ---- Vurus (spring) ----
        flinchVel += (-flinch * flinchStiffness * flinchStiffness - flinchVel * flinchStiffness * 0.5f) * dt;
        flinch += flinchVel * dt;
        float f = Mathf.Clamp(flinch, -1.5f, 1.5f);

        float t = animClock * breathsPerSecond * 6.2832f + phase;
        float b = Mathf.Sin(t);               // nefes: -1..1
        float b2 = Mathf.Sin(t - 0.7f);       // gecikmeli (bas)
        float b3 = Mathf.Sin(t - 1.3f);       // daha gecikmeli (kol/kilic)
        float inhale = b * 0.5f + 0.5f;

        // ---- Saldiri pozu (Boss sola bakiyor: geri = saat yonu = negatif z) ----
        float k, s;
        TickAttack(dt, out k, out s);
        float atkAmt = Mathf.Clamp01(Mathf.Max(k, s));

        // ---- Yuruyus ----
        if (body == null) body = GetComponent<Rigidbody2D>();
        float speed = body != null ? Mathf.Abs(body.linearVelocity.x) : 0f;
        bool canWalk = walkEnabled && MoveAllowed && !Staggered && !Dead && atkState == AtkState.None && speed > 0.3f;
        walkW = Mathf.MoveTowards(walkW, canWalk ? 1f : 0f, dtR * (canWalk ? 4f : 6f));
        if (canWalk)
        {
            float strideWorld = Mathf.Max(0.1f, walkStride * Mathf.Abs(visual.lossyScale.y));
            // Yerdeki ayak (1 - walkSwing) faz kadar surede walkStride kadar geri kayar -> kayma yok.
            walkPhase = Mathf.Repeat(walkPhase + (1f - walkSwing) * speed / strideWorld * dtR, 1f);
        }
        plantKick *= Mathf.Exp(-dtR * 9f);
        if (walkW > 0.001f) UpdateLegs(walkW); else { for (int i = 0; i < legU.Length; i++) legU[i] = -1f; UpdateLegs(0f); }

        float w = walkW;
        float sway = Mathf.Sin(walkPhase * 6.2832f);
        float stepWave = Mathf.Sin(walkPhase * 6.2832f * 2f - 1.2f);   // 2 adim / dongu
        float kick = plantKick * w;

        staggerW = Mathf.MoveTowards(staggerW, Staggered && !Dead ? 1f : 0f, dtR * 5f);
        deadW = Mathf.MoveTowards(deadW, Dead ? 1f : 0f, dtR * 2.5f);

        float idleW = (1f - atkAmt) * (1f - w) * (1f - staggerW) * (1f - deadW);   // nefes sallanmasini kis
        float tremble = 0f;
        if (atkState == AtkState.Windup || atkState == AtkState.Tension)
            tremble = Mathf.Sin(animClock * 55f) * windupTremble * Mathf.Clamp01(k) * Mathf.Clamp01(k);
        float shake = (sinceHit < 0.6f) ? Mathf.Exp(-sinceHit * 14f) * Mathf.Sin(sinceHit * 60f) * impactShakeDeg : 0f;

        float lean = walkLeanDeg * w;
        float atkChest = windupChestDeg * k + strikeChestDeg * s + tremble * 0.5f + shake * 0.6f
                         + lean + kick * 2.5f + staggerW * 10f + deadW * 28f;
        float atkBelly = windupBellyDeg * k + strikeBellyDeg * s + lean * 0.5f + staggerW * 6f + deadW * 14f;
        float atkHead = windupHeadDeg * k + strikeHeadDeg * s - lean * 0.6f + kick * 3f + staggerW * 12f + deadW * 20f;
        float atkArm = windupArmDeg * k + strikeArmDeg * s + tremble + kick * 2f + w * stepWave * 1.5f + staggerW * 8f + deadW * 25f;
        float atkSword = windupSwordDeg * k + strikeSwordDeg * s + tremble * 0.6f + shake
                         + w * walkSwordSwing * stepWave + kick * walkSwordSwing;
        float atkArmL = windupArmLDeg * k + strikeArmLDeg * s - tremble * 0.8f
                        + w * walkArmLSwing * Mathf.Sin(walkPhase * 6.2832f + 1.57f) + staggerW * 10f + deadW * 15f;
        float atkCrouch = windupCrouch * k + strikeCrouch * s + walkDip * kick * kick + staggerW * 0.05f + deadW * 0.12f;

        // Root: cokme/nefes arka ayagin zemin noktasi etrafinda olceklenir (ayak kaymaz).
        float sx = 1f + rootSquash * 0.5f * b + atkCrouch * 0.35f;
        float sy = 1f - rootSquash * b - atkCrouch;
        Vector3 rootPivotShift = new Vector3(pivotBackFoot.x * (1f - sx), pivotBackFoot.y * (1f - sy), 0f);

        Set("root", new Vector3(f * flinchShove * 0.6f * dir, 0f, 0f) + rootPivotShift, 0f,
            new Vector3(sx, sy, 1f));
        Set("belly", new Vector3(0f, bellyLift * inhale, 0f), (-f * flinchDeg * 0.3f + atkBelly) * dir);
        Set("chest", new Vector3(sway * 0.07f * w, chestLift * inhale, 0f), (-chestTiltDeg * inhale * idleW + f * flinchDeg + atkChest) * dir,
            new Vector3(1f + chestScale * inhale, 1f + chestScale * 1.4f * inhale, 1f));
        Set("head", new Vector3(0f, 0.02f * b2, 0f), (headNodDeg * b2 * idleW - f * flinchDeg * 0.7f + atkHead) * dir);
        Set("shoulderR", Vector3.zero, (armSwayDeg * b3 * idleW + f * flinchDeg * 0.5f + atkArm) * dir);
        float swordAngle = swordSwayDeg * -b3 * idleW + atkSword;
        Set("sword", Vector3.zero, swordAngle * dir);
        SetAround("armL_upper", armLPivot, (armLSwayDeg * -b3 * idleW - f * flinchDeg * 0.4f + atkArmL) * dir);

        if (keepBladeAboveGround) ClampBladeAboveGround(swordAngle, dir);
    }

    // Bicagin en alcak noktasi (visual/rig uzayinda y).
    float LowestBladeY(Transform sw)
    {
        float low = float.MaxValue;
        for (int i = 0; i < BladeHull.Length; i++)
        {
            Vector3 w = sw.TransformPoint(new Vector3(BladeHull[i].x, BladeHull[i].y, 0f));
            float y = visual.InverseTransformPoint(w).y;
            if (y < low) low = y;
        }
        return low;
    }

    // Kilic zemin cizgisinin altina inerse bilegi (saat yonunde) yukari kaldirip tam cizgiye oturtur.
    void ClampBladeAboveGround(float swordAngle, float dir)
    {
        Transform sw;
        if (!bones.TryGetValue("sword", out sw)) return;
        if (LowestBladeY(sw) >= groundY) return;

        Quaternion rest = restRot["sword"];
        float lo = 0f, hi = 70f;
        for (int i = 0; i < 12; i++)
        {
            float mid = (lo + hi) * 0.5f;
            sw.localRotation = rest * Quaternion.Euler(0f, 0f, (swordAngle - mid) * dir);
            if (LowestBladeY(sw) < groundY) lo = mid; else hi = mid;
        }
        sw.localRotation = rest * Quaternion.Euler(0f, 0f, (swordAngle - hi) * dir);
    }
}
