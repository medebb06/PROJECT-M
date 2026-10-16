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
    [Range(0.6f, 1.6f)] public float walkStride = 1.6f;
    [Tooltip("Yuruyus animasyonunun hizi (1 = eski). Dusuk = daha yavas, dev adimlar. Hareket hizi/mesafe DEGISMEZ; sadece adimlar yavaslar (ayaklar biraz kayabilir).")]
    [Range(0.15f, 1f)] public float walkAnimSpeed = 0.28f;
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

    /// <summary>Saldiri stili: Slam = klasik ust darbe; Sweep = alcak supurme; Fake = titreyen gecikmeli darbe.</summary>
    public enum AttackStyle { Slam, Sweep, Fake }

    [Serializable]
    public struct Pose
    {
        public float chest, belly, head, arm, sword, armL, crouch;
        public Pose(float chest, float belly, float head, float arm, float sword, float armL, float crouch)
        {
            this.chest = chest; this.belly = belly; this.head = head; this.arm = arm;
            this.sword = sword; this.armL = armL; this.crouch = crouch;
        }
    }

    [Header("Saldiri stilleri (derece; Slam icin yukaridaki windup/strike alanlari kullanilir)")]
    [Tooltip("SUPURME hazirlik: cok egilir, kilic geride/alcakta, derin cokme.")]
    public Pose sweepWindupPose = new Pose(-24f, -12f, -8f, -30f, 95f, -10f, 0.10f);
    [Tooltip("SUPURME darbe: govde one atilir, kilic yere yakin genis savrulur.")]
    public Pose sweepStrikePose = new Pose(34f, 16f, 7f, 28f, 102f, 24f, 0.14f);
    [Tooltip("ALDATMACA: hazirlikta titreme carpani.")]
    [Range(1f, 5f)] public float fakeTrembleMul = 2.8f;

    AttackStyle style = AttackStyle.Slam;

    [Header("Goril kolu: OMUZ (armL_upper) -> KOL (legBack) -> EL (legBack_knee)")]
    [Tooltip("Her cift = (kol acisi, el acisi) derece, MUTLAK. Kol omuz ekleminden, el bilekten doner. NEGATIF = one/yukari (boss sola bakar), pozitif = geri.")]
    public Vector2 slamArmWindup = new Vector2(-52f, -20f);
    public Vector2 slamArmStrike = new Vector2(-4f, 4f);
    [Tooltip("Supurme: kol geride toplanir, darbede one dogru savrulur.")]
    public Vector2 sweepArmWindup = new Vector2(34f, 22f);
    public Vector2 sweepArmStrike = new Vector2(-38f, -8f);
    [Tooltip("Gogus vurusu: taban + vurus (kol, el).")]
    public Vector2 beatArmBase = new Vector2(-72f, -30f);
    public Vector2 beatArmSwing = new Vector2(48f, 20f);
    [Range(2, 10)] public int gorillaBeatCount = 5;
    [Tooltip("Ziplama inisi: baslangic (kol, el); sonra yere carpar.")]
    public Vector2 poundArm = new Vector2(-50f, -15f);
    [Tooltip("Omuz blogunun kolu takip orani.")]
    [Range(0f, 0.6f)] public float shoulderFollow = 0.3f;

    float beatLeft, beatTotal, poundBump;

    /// <summary>Sol kolla gogus vurusu (goril gibi): kukremede cagrilir.</summary>
    public void BeatChest(float seconds)
    {
        beatTotal = beatLeft = Mathf.Max(0.3f, seconds);
    }

    /// <summary>Sol kolu bir kez yere carptirir (ziplama inisi vb.).</summary>
    public void PoundGround()
    {
        poundBump = 1f;
    }

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
    /// <summary>Saldiri pozundayken de yuruyus/kosu bacak animasyonu oynasin (boss kosarak vururken).</summary>
    [NonSerialized] public bool RunWhileAttacking;
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

    float atkScale = 1f;          // >1: gerilme/darbe hizlanir (hizli vurus)
    float extraTension;           // gergin pozda ek bekleme (gecikmeli vurus)
    readonly List<SpriteRenderer> swordSrs = new List<SpriteRenderer>();
    Color swordTintColor = new Color(0.4f, 0.02f, 0.03f, 1f);
    float swordTintTarget, swordTintCur = -1f, swordTintSpeed = 7f;
    SpriteRenderer metalSr, coreSr, haloSr;
    bool metalBuilt;
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
    [Range(0f, 0.4f)] public float squashAmount = 0.2f;
    [Range(6f, 40f)] public float squashStiffness = 20f;
    [Tooltip("Hasar alinca butun govdenin beyaz yanma suresi (sn, gercek zaman).")]
    [Range(0f, 0.3f)] public float flashTime = 0.09f;
    float sq, sqVel, flashTimer;

    [Header("Ziplama saldirisi (Boss1Enemy yonetir)")]
    [Tooltip("Cokme/uzama (SetBaseSquash) siddeti: 1 = govde %35 ezilir/uzar.")]
    [Range(0f, 0.7f)] public float leapSquashAmount = 0.12f;
    float sqBase, sqBaseTarget, sqBaseSpeed = 5f;
    bool holdTension;
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

        swordSrs.Clear();
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
            if (l.bone == "sword" || l.name == "sword") swordSrs.Add(sr);
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
        style = AttackStyle.Slam;
        atkScale = 1f; extraTension = 0f;
        holdTension = false;
        startK = lastK; startS = lastS;
        atkState = AtkState.Windup;
        atkTimer = 0f;
        // Dusman durumu ayni karede Enter() ile baslatiyor ama sayaci bir sonraki karede dusurmeye basliyor:
        // animasyon bir kare onde gitmesin, darbe ani ile hasar ani ayni olsun.
        skipDt = true;
        return true;
    }

    /// <summary>
    /// Darbe 'contactSeconds' sonra insin: normalden KISAysa animasyon hizlanir (hizli vurus),
    /// UZUNsa gergin pozda ek sure bekler (gecikmeli vurus).
    /// </summary>
    public bool PlayAttack(float contactSeconds, bool slowRaise = false)
    {
        bool ok = PlayAttack();
        float baseT = ContactTime;
        if (contactSeconds > 0.05f)
        {
            if (contactSeconds < baseT) atkScale = Mathf.Clamp(baseT / contactSeconds, 1f, 4f);
            else if (slowRaise) atkScale = Mathf.Clamp(baseT / contactSeconds, 0.35f, 1f);   // yavas yavas kaldir
            else extraTension = contactSeconds - baseT;
        }
        return ok;
    }

    /// <summary>Stilli saldiri: Sweep / Fake ayri bir poz ve silueti ile oynar.</summary>
    public bool PlayAttack(float contactSeconds, bool slowRaise, AttackStyle attackStyle)
    {
        bool ok = PlayAttack(contactSeconds, slowRaise);
        style = attackStyle;
        return ok;
    }

    /// <summary>Kilic kemigi (efekt baglamak icin). Yoksa null.</summary>
    public Transform SwordBone { get { Transform t; return bones.TryGetValue("sword", out t) ? t : null; } }

    /// <summary>Kilic bicaginin ortasi (sword kemigi uzayi).</summary>
    public Vector3 SwordBladeCenter { get { return new Vector3(4.0f, 3.0f, 0f); } }

    /// <summary>Kilic katmanini koyu tona boyar (0 = normal, 1 = tam). Yumusakca gecer.</summary>
    public void SetSwordTint(Color c, float amount, float speed = 7f)
    {
        swordTintColor = c;
        swordTintTarget = Mathf.Clamp01(amount);
        swordTintSpeed = Mathf.Max(0.05f, speed);
        if (swordTintTarget > 0f) EnsureMetalOverlay();
    }

    // Kilic bicaginin METAL bolgesi (BladeHull; sap haric): parlak kirmizi-beyaz cekirdek + additive isima + yumusak hale.
    // (Oyuncunun kilicindaki "execute hazir" parlamasinin kirmizi karsiligi; duz/koyu boya degil.)
    void EnsureMetalOverlay()
    {
        if (metalBuilt) return;

        // Rig sahnede hazir kaydedilmisse Rebuild calismamis olabilir: kilic katmanini kemikten bul.
        if (swordSrs.Count == 0 || swordSrs[0] == null)
        {
            swordSrs.Clear();
            Transform sb = SwordBone;
            if (sb != null)
            {
                foreach (SpriteRenderer sr in sb.GetComponentsInChildren<SpriteRenderer>(true))
                    if (sr.sprite != null && sr.sprite.name.ToLowerInvariant().Contains("sword")) swordSrs.Add(sr);
                if (swordSrs.Count == 0)
                    foreach (SpriteRenderer sr in sb.GetComponentsInChildren<SpriteRenderer>(true))
                        if (sr.sprite != null) { swordSrs.Add(sr); break; }
            }
        }
        if (swordSrs.Count == 0 || swordSrs[0] == null || swordSrs[0].sprite == null)
        {
            Debug.LogWarning("Boss1Rig: kilic sprite'i bulunamadi, kirmizi kaplama yapilamadi (duz boyama kullanilacak).");
            return;
        }
        metalBuilt = true;

        SpriteRenderer baseSr = swordSrs[0];
        Sprite sp = baseSr.sprite;
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
        float[] mask = new float[px.Length];
        Vector2 lp = baseSr.transform.localPosition;
        float ppu = sp.pixelsPerUnit > 0f ? sp.pixelsPerUnit : 100f;
        Color dark = new Color(0.5f, 0f, 0.05f), hotC = new Color(0.82f, 0.02f, 0.1f);   // duz kivrik kirmizi (boya gibi)
        for (int y = 0; y < rh; y++)
        {
            for (int x = 0; x < rw; x++)
            {
                int i = y * rw + x;
                Color32 c = px[i];
                Vector2 bp = new Vector2((x + 0.5f - sp.pivot.x) / ppu + lp.x, (y + 0.5f - sp.pivot.y) / ppu + lp.y);
                if (c.a < 8 || !InBlade(bp)) { px[i] = new Color32(0, 0, 0, 0); continue; }
                float lum = Mathf.Max(c.r, Mathf.Max(c.g, c.b)) / 255f;
                Color o = Color.Lerp(dark, hotC, lum);
                px[i] = new Color32((byte)(o.r * 255f), (byte)(o.g * 255f), (byte)(o.b * 255f), c.a);
                mask[i] = c.a / 255f;
            }
        }
        t.SetPixels32(px);
        t.Apply();
        t.filterMode = src.filterMode;
        Vector2 pivot = new Vector2(sp.pivot.x / r.width, sp.pivot.y / r.height);
        Sprite core = Sprite.Create(t, new Rect(0, 0, rw, rh), pivot, sp.pixelsPerUnit, 0, SpriteMeshType.FullRect);

        // Hale: maskenin bulaniklastirilmis, kenarlara tasan kopyasi.
        int pad = Mathf.Max(12, Mathf.RoundToInt(Mathf.Max(rw, rh) * 0.06f));
        int hw = rw + pad * 2, hh = rh + pad * 2;
        float[] m = new float[hw * hh];
        for (int y = 0; y < rh; y++) for (int x = 0; x < rw; x++) m[(y + pad) * hw + x + pad] = mask[y * rw + x];
        int rad = Mathf.Max(2, pad / 2);
        for (int pass = 0; pass < 2; pass++) { m = BoxBlur(m, hw, hh, rad, true); m = BoxBlur(m, hw, hh, rad, false); }
        Color32[] hp = new Color32[hw * hh];
        for (int i = 0; i < hp.Length; i++)
        {
            float a = Mathf.Clamp01(m[i] * 2.6f);
            hp[i] = new Color32(255, 0, 0, (byte)(a * 255f));
        }
        Texture2D ht = new Texture2D(hw, hh, TextureFormat.RGBA32, false);
        ht.SetPixels32(hp); ht.Apply(); ht.filterMode = FilterMode.Bilinear;
        Vector2 hpv = new Vector2((sp.pivot.x + pad) / hw, (sp.pivot.y + pad) / hh);
        Sprite halo = Sprite.Create(ht, new Rect(0, 0, hw, hh), hpv, sp.pixelsPerUnit, 0, SpriteMeshType.FullRect);

        Shader add = Shader.Find("ProjectM/Boss1SpriteAdditive");
        Material addMat = null;
        if (add != null)
        {
            addMat = new Material(add) { name = "Boss1_SwordGlow" };
            addMat.SetFloat("_Intensity", 7f);   // Impact materyalinin HDR parlakligina (~8.6) yakin: bloom verir
        }

        haloSr = MakeOverlay("SwordHalo", baseSr, halo, baseSr.sortingOrder + 1, 1f, addMat);
        metalSr = MakeOverlay("SwordMetalRed", baseSr, core, baseSr.sortingOrder + 2, 1f, null);
        coreSr = MakeOverlay("SwordCoreGlow", baseSr, core, baseSr.sortingOrder + 3, 1f, addMat);
        haloSr.enabled = false; metalSr.enabled = false; coreSr.enabled = false;
        Debug.Log("[Boss1] kilic kirmizi kaplamasi hazir: sprite=" + sp.name + " " + rw + "x" + rh + " ppu=" + sp.pixelsPerUnit + " additiveShader=" + (add != null));
    }

    static float[] BoxBlur(float[] a, int w, int h, int rad, bool horiz)
    {
        float[] o = new float[a.Length];
        int len = horiz ? w : h, lines = horiz ? h : w;
        float inv = 1f / (rad * 2 + 1);
        for (int l = 0; l < lines; l++)
        {
            float sum = 0f;
            for (int k = -rad; k <= rad; k++) sum += Get(a, w, h, horiz, l, k);
            for (int k = 0; k < len; k++)
            {
                int idx = horiz ? l * w + k : k * w + l;
                o[idx] = sum * inv;
                sum += Get(a, w, h, horiz, l, k + rad + 1) - Get(a, w, h, horiz, l, k - rad);
            }
        }
        return o;
    }

    static float Get(float[] a, int w, int h, bool horiz, int line, int k)
    {
        if (k < 0 || k >= (horiz ? w : h)) return 0f;
        return horiz ? a[line * w + k] : a[k * w + line];
    }

    SpriteRenderer MakeOverlay(string n, SpriteRenderer baseSr, Sprite sp, int order, float scl, Material mat)
    {
        GameObject go = new GameObject(n);
        go.transform.SetParent(baseSr.transform, false);
        go.transform.localScale = new Vector3(scl, scl, 1f);
        go.layer = baseSr.gameObject.layer;
        SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sp;
        sr.sharedMaterial = mat != null ? mat : baseSr.sharedMaterial;
        sr.sortingLayerID = baseSr.sortingLayerID;
        sr.sortingOrder = order;
        return sr;
    }

    static bool InBlade(Vector2 p)
    {
        bool inside = false;
        for (int i = 0, j = BladeHull.Length - 1; i < BladeHull.Length; j = i++)
        {
            Vector2 a = BladeHull[i], b = BladeHull[j];
            if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                inside = !inside;
        }
        return inside;
    }

    /// <summary>Ziplama saldirisi: kilici kaldirip gerilir ve ReleaseStrike() cagrilana kadar o pozda bekler.</summary>
    public void PlayLeapTense()
    {
        PlayAttack();
        holdTension = true;
    }

    /// <summary>Beklemeyi birakir: darbe (Strike) hemen baslar.</summary>
    public void ReleaseStrike()
    {
        holdTension = false;
    }

    /// <summary>Govdeyi surekli ezik (+) ya da uzamis (-) tutar; hedefe 'speed' birim/sn ile gider.</summary>
    public void SetBaseSquash(float target, float speed)
    {
        sqBaseTarget = target;
        sqBaseSpeed = Mathf.Max(0.01f, speed);
    }

    /// <summary>Gerilme/bekleme asamasindaki saldiriyi yumusakca iptal eder (dusman sersemleyince / vurulunca). Darbe sonrasi iptal edilmez.</summary>
    public bool CancelAttack()
    {
        if (atkState != AtkState.Windup && atkState != AtkState.Tension) return false;
        holdTension = false;
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

        atkTimer += dt * ((atkState == AtkState.Windup || atkState == AtkState.Tension || atkState == AtkState.Strike) ? atkScale : 1f);
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
                if (atkTimer >= tensionTime + extraTension)
                {
                    if (holdTension)
                    {
                        atkTimer = tensionTime + extraTension;   // ziplama: gergin pozda bekle
                        break;
                    }
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
        if (swordTintCur < 0f) swordTintCur = 0f;
        swordTintCur = Mathf.MoveTowards(swordTintCur, swordTintTarget, dtR * swordTintSpeed);
        if (metalSr == null && swordSrs.Count > 0 && swordSrs[0] != null)
        {
            // Kaplama kurulamadiysa: kilic sprite'ini dogrudan kirmiziya boya (bicak beyaz, sap koyu: carpim dogru calisir).
            swordSrs[0].color = Color.Lerp(tint, new Color(0.85f, 0.02f, 0.1f, 1f), swordTintCur);
        }
        if (metalSr != null)
        {
            bool on = swordTintCur > 0.003f;
            metalSr.enabled = on; coreSr.enabled = on; haloSr.enabled = on;
            if (on)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 8f);
                metalSr.color = new Color(1f, 1f, 1f, swordTintCur);
                coreSr.color = new Color(1f, 0.02f, 0.04f, swordTintCur * (0.12f + 0.16f * pulse));
                haloSr.color = new Color(1f, 0f, 0.02f, swordTintCur * (0.22f + 0.2f * pulse));
            }
        }
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
        sqBase = Mathf.MoveTowards(sqBase, sqBaseTarget, sqBaseSpeed * Time.unscaledDeltaTime);
        float sqx = 1f + sq * squashAmount + sqBase * leapSquashAmount, sqy = 1f - sq * squashAmount - sqBase * leapSquashAmount;
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
        bool canWalk = walkEnabled && (MoveAllowed || RunWhileAttacking) && !Staggered && !Dead && (atkState == AtkState.None || RunWhileAttacking) && speed > 0.3f;
        walkW = Mathf.MoveTowards(walkW, canWalk ? 1f : 0f, dtR * (canWalk ? 4f : 6f));
        if (canWalk)
        {
            float strideWorld = Mathf.Max(0.1f, walkStride * Mathf.Abs(visual.lossyScale.y));
            // Yerdeki ayak (1 - walkSwing) faz kadar surede walkStride kadar geri kayar -> kayma yok.
            walkPhase = Mathf.Repeat(walkPhase + (1f - walkSwing) * speed / strideWorld * walkAnimSpeed * dtR, 1f);
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
            tremble = Mathf.Sin(animClock * 55f) * windupTremble * (style == AttackStyle.Fake ? fakeTrembleMul : 1f) * Mathf.Clamp01(k) * Mathf.Clamp01(k);
        float shake = (sinceHit < 0.6f) ? Mathf.Exp(-sinceHit * 14f) * Mathf.Sin(sinceHit * 60f) * impactShakeDeg : 0f;

        float lean = walkLeanDeg * w;
        // Stile gore hazirlik / darbe pozlari (Slam ve Fake: klasik alanlar).
        Pose wp = new Pose(windupChestDeg, windupBellyDeg, windupHeadDeg, windupArmDeg, windupSwordDeg, windupArmLDeg, windupCrouch);
        Pose sp = new Pose(strikeChestDeg, strikeBellyDeg, strikeHeadDeg, strikeArmDeg, strikeSwordDeg, strikeArmLDeg, strikeCrouch);
        if (style == AttackStyle.Sweep) { wp = sweepWindupPose; sp = sweepStrikePose; }

        float atkChest = wp.chest * k + sp.chest * s + tremble * 0.5f + shake * 0.6f
                         + lean + kick * 2.5f + staggerW * 10f + deadW * 28f;
        float atkBelly = wp.belly * k + sp.belly * s + lean * 0.5f + staggerW * 6f + deadW * 14f;
        float atkHead = wp.head * k + sp.head * s - lean * 0.6f + kick * 3f + staggerW * 12f + deadW * 20f;
        float atkArm = wp.arm * k + sp.arm * s + tremble + kick * 2f + w * stepWave * 1.5f + staggerW * 8f + deadW * 25f;
        float atkSword = wp.sword * k + sp.sword * s + tremble * 0.6f + shake
                         + w * walkSwordSwing * stepWave + kick * walkSwordSwing;
        float atkArmL = wp.armL * k + sp.armL * s - tremble * 0.8f
                        + w * walkArmLSwing * Mathf.Sin(walkPhase * 6.2832f + 1.57f) + staggerW * 10f + deadW * 15f;
        // ---- GORIL KOLU: omuz -> kol -> el (FK). Saldiri stiline, kukremeye ve inise gore (kol, el) acilari ----
        Vector2 armPose = Vector2.zero;
        if (style == AttackStyle.Slam || style == AttackStyle.Fake) armPose = slamArmWindup * k + slamArmStrike * s;
        else if (style == AttackStyle.Sweep) armPose = sweepArmWindup * k + sweepArmStrike * s;
        float armW = atkAmt;
        armPose += poundArm * poundBump;
        armW = Mathf.Max(armW, poundBump);
        poundBump = Mathf.MoveTowards(poundBump, 0f, dtR * 3.5f);
        float beat = 0f;
        if (beatLeft > 0f)
        {
            float prog = 1f - beatLeft / beatTotal;
            float env = Mathf.Clamp01(prog * 6f) * Mathf.Clamp01((1f - prog) * 6f);
            beat = Mathf.Abs(Mathf.Sin(prog * gorillaBeatCount * Mathf.PI)) * env;
            armPose += (beatArmBase + beatArmSwing * beat) * Mathf.Clamp01(env * 2f);
            armW = Mathf.Max(armW, Mathf.Clamp01(env * 2f));
            beatLeft -= dtR;
        }
        atkChest += beat * 2.5f;
        atkArmL += armPose.x * shoulderFollow * armW;   // omuz blogu kolu hafifce takip eder
        float atkCrouch = wp.crouch * k + sp.crouch * s + walkDip * kick * kick + staggerW * 0.05f + deadW * 0.12f;

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

        ApplyArmFk(armPose.x, armPose.y, Mathf.Clamp01(armW));

        if (keepBladeAboveGround) ClampBladeAboveGround(swordAngle, dir);
    }

    // ---- Sol kol FK: omuz (armL_upper'a bagli eklem) -> kol (legBack) -> el (legBack_knee) ----
    // Eklemler rig birimi (rest, PSD pikselinden): omuz J0 = kolun ust ucu, bilek W0 = kolun alt ucu.
    static readonly Vector2 ShoulderJ0 = new Vector2(0.96f, 4.53f);
    static readonly Vector2 WristW0 = new Vector2(0.74f, 0.98f);
    static readonly Vector2 ShoulderBlobCenter0 = new Vector2(0.425f, 5.01f);   // armL_upper rest merkezi
    const float HandGroundY = 0.04f;   // yumruk tabani (rest)
    // Elin gorunur kutusu (sprite merkezine gore, rig birimi): sol/sag/ust/alt
    static readonly Vector4 HandBox = new Vector4(-0.70f, 0.68f, 0.535f, -0.535f);

    /// <summary>w = 0: yuruyus/rest pozu aynen kalir; w = 1: (aArm, aHand) FK pozu. Omuz, gogus/omuz blogunu takip eder.</summary>
    void ApplyArmFk(float aArm, float aHand, float w)
    {
        if (w < 0.001f) return;
        Transform arm, hand, blob;
        if (!bones.TryGetValue("legBack", out arm) || !bones.TryGetValue("legBack_knee", out hand) || !bones.TryGetValue("armL_upper", out blob)) return;
        if (arm == null || hand == null || blob == null || arm.parent == null) return;

        // Omuz eklemi: omuz blogunun (armL_upper) simdiki donusunden.
        Vector3 jw = blob.TransformPoint(new Vector3(ShoulderJ0.x - ShoulderBlobCenter0.x, ShoulderJ0.y - ShoulderBlobCenter0.y, 0f));
        Vector3 jl3 = arm.parent.InverseTransformPoint(jw);
        Vector2 J = new Vector2(jl3.x, jl3.y);

        Vector2 restArm = restPos["legBack"], restHand = restPos["legBack_knee"];
        Vector2 armP = J + Rot2(restArm - ShoulderJ0, aArm);
        Vector2 W = J + Rot2(WristW0 - ShoulderJ0, aArm);
        Vector2 handP = W + Rot2(restHand - WristW0, aHand);

        // Yumruk zemine girmesin (parmak dislariyla basar): en alt kosesi HandGroundY'nin altina inmez.
        float lowest = float.MaxValue;
        float r = aHand * Mathf.Deg2Rad, c = Mathf.Cos(r), sn = Mathf.Sin(r);
        for (int i = 0; i < 4; i++)
        {
            float bx = (i & 1) == 0 ? HandBox.x : HandBox.y, by = (i & 2) == 0 ? HandBox.z : HandBox.w;
            lowest = Mathf.Min(lowest, handP.y + sn * bx + c * by);
        }
        if (lowest < HandGroundY) handP.y += HandGroundY - lowest;

        Quaternion armQ = restRot["legBack"] * Quaternion.Euler(0f, 0f, aArm);
        Quaternion handQ = restRot["legBack_knee"] * Quaternion.Euler(0f, 0f, aHand);
        arm.localPosition = Vector3.Lerp(arm.localPosition, new Vector3(armP.x, armP.y, arm.localPosition.z), w);
        arm.localRotation = Quaternion.Slerp(arm.localRotation, armQ, w);
        hand.localPosition = Vector3.Lerp(hand.localPosition, new Vector3(handP.x, handP.y, hand.localPosition.z), w);
        hand.localRotation = Quaternion.Slerp(hand.localRotation, handQ, w);
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
