using UnityEngine;

namespace RonniSkin
{
    /// Puts Ronni on the existing Player: reads PlayerController state (move, grounded,
    /// facing, defend), hides the old sprite, matches its size/position/sorting and
    /// mirrors its blink / hit-flash color onto Ronni.
    [DefaultExecutionOrder(-200)]
    [RequireComponent(typeof(RonniSkinRig), typeof(RonniSkinAnimator))]
    public class RonniPlayerBridge : MonoBehaviour
    {
        // Ronni'yi sahnedeki props/zemin/Demirci sirasinin uzerine tasir; dusmanlar (30-31) bunun ARKASINDA, sahnenin onunde kalir.
        public const int OrderBoost = 30;

        public PlayerController player;
        [Tooltip("Old sprite to hide (defaults to PlayerController.playerSprite).")]
        public SpriteRenderer originalSprite;
        public bool hideOriginal = true;
        public bool matchOriginalSize = true;
        [Tooltip("Extra size multiplier on top of the auto match.")]
        public float sizeMultiplier = 1f;
        public Vector2 offset = Vector2.zero;
        public bool mirrorSpriteColor = true;
        [Header("Sheathe")]
        [Tooltip("Key that sheathes / draws the katana while idle.")] public KeyCode sheatheKey = KeyCode.Q;
        [Tooltip("Start the level with the katana drawn (held in the back hand).")] public bool startDrawn = false;
        string shClip; float shStart;
        [Tooltip("Test: always play the run animation.")] public bool forceRun = false;
        [Tooltip("Use the side-profile drawing (Resources/RonniSkinB) while running.")] public bool useProfileRun = true;
        [Tooltip("Use the side-profile drawing (facing left) while in the air.")] public bool useProfileJump = true;
        [Tooltip("Koşu çizimi klasörü (Resources/<klasör>, ör. RonniRun = Koşu2.psd). Boş bırakılırsa eski profil çizimi koşar.")]
        public string runFolder = "RonniRunVivid";
        [Tooltip("Koşu çiziminin boyu (1 = Ronni ile aynı boy).")] public float runScale = 1f;
        [Header("Idle (nefes + rüzgar)")]
        [Tooltip("Dururken görünen çizim klasörü (Resources/<klasör>, IDLE.psd). Boş bırakılırsa eski ön görünüm idle'ı kullanılır.")]
        public string idleFolder = "RonniIdle";
        [Tooltip("Idle çiziminin boyu (1 = Ronni ile aynı boy).")] public float idleScale = 1f;
        RonniSkinRig rigIdle; RonniClipPlayer playerIdle; Vector3 baseIdle = Vector3.one;
        [Header("Kılıç çekik idle (R ile kılıç çekilince)")]
        [Tooltip("Kılıç çekikken dururken görünen çizim klasörü (Resources/<klasör>, 'idle' klibi). Boş = eski RonniAtk 'idle_drawn' klibi.")]
        public string drawnIdleFolder = "RonniIdleKatana";
        RonniSkinRig rigDrawn; RonniClipPlayer playerDrawn; Vector3 baseDrawn = Vector3.one; bool drawnWant;
        [Tooltip("Kılıcı koyarken (R / rage) kılıç çekik idle çizimiyle tenka chiburi + sakate nōtō oynat (RonniIdleKatana 'tenka' klibi). Kapalı = eski RonniAtk chiburi.")]
        public bool useTenka = true;
        RonniTenka tenka; bool tenkaOn, tenkaFrame; float tenkaPh;
        [Header("Rig saldırı prototipi")]
        [Tooltip("Attack1'i el çizimi kareler yerine RonniIdleKatana rig'inin 'attack1' klibiyle oynat (prototip). Kapalı = eski kare kare saldırı.")] public bool rigAttack1Proto = false;
        float rigAtkT = -1f, rigAtkLastProg, drawnFadeDefault = -1f, hatSuppress = -1f; bool rigAtkBlock;
        const float RigAtk1Dur = 0.9f;   // klip süresi (TenkaClipRebuild.RunAttack1 ile aynı)
        bool RigAtk1Ready() { return rigAttack1Proto && rigDrawn != null && rigDrawn.IsBuilt && playerDrawn != null && playerDrawn.HasClip("attack1"); }
        [Header("Yerinde zıplama (idle gövde + zıplama bacakları)")]
        [Tooltip("Yatay hareket olmadan zıplarken kullanılan karma çizim (Resources/<klasör>, 'air' klibi). Boş = kapalı.")] public string standJumpFolder = "RonniJump";
        [Tooltip("Saldırı smear parlaklığı (HDR; Impact Mat _Color ≈ 8.5). Bloom varsa parlar.")] public float smearGlow = 6f;
        [Tooltip("Saldırı ritmi: vuruş (kesme) anı hızlanması bu ilerleme aralığında olur.")] public float atkCutStart = 0.42f, atkCutEnd = 0.56f;
        [Tooltip("Klibin kesme aralığı (klip zamanı 0..1).")] public float atkClipCutA = 0.38f, atkClipCutB = 0.64f;
        // yavaş başla -> kesmede hızlan -> çok yavaş bitir
        [Tooltip("Saldırı bitince idle/sonraki görünüme geçiş (çapraz solma) süresi, sn.")] public float atkCrossfade = 0f;
        [Tooltip("Saldırı bitince, yeni girdi yoksa kesme pozunda bekleme süresi (sn).")] public float atkHoldTime3 = 0.5f;
        [Tooltip("Bekleme pozu (klip zamanı 0..1; kesme sonrası duruş).")] public float atkHoldPhase = 0.80f;
        [Tooltip("Bekledikten sonra idle duruşa dönüş süresi (sn).")] public float atkReturnTime3 = 0.8f;
        [Tooltip("Vuruş bitişinde ulaşılan poz; buradan bekleme pozuna yavaşça geçilir.")] public float atkSettlePhase = 0.68f;
        [Tooltip("Attack2: kesim bitişinde ulaşılan klip zamanı / bekleme pozu klip zamanı.")] public float atkSettle2 = 0.55f, atkHold5x = 0.40f;
        [Tooltip("Attack2 bitiş bekleme süresi (sn).")] public float atkHoldTime5 = 2.2f;
        [Tooltip("Attack2: son pozda toparlanmadan önce kısa bekleme (sn).")] public float atkEndPause2 = 0.8f;
        [Tooltip("Attack2: bekleme sonrası kılıcı geri çekip idle'a dönüş süresi (sn). Büyük = daha yavaş.")] public float atkReturnTime5 = 3.5f;
        string lastAtkClip = "attack1";
        [Header("Attack1 kare kare (Animator: Sprites/Player/Attack1.anim)")]
        [Tooltip("Attack1'de rig yerine Animator'ün oynattığı kare kare çizimi (Attack1HD_*) göster.")] public bool frameAttack1 = true;
        [Tooltip("Attack2'de rig yerine Animator'ün oynattığı kare kare çizimi (Attack2HD_*) göster.")] public bool frameAttack2 = true;
        [Tooltip("Attack3'te rig yerine Animator'ün oynattığı kare kare çizimi (Attack3HD_*) göster.")] public bool frameAttack3 = true;
        [Tooltip("Attack4'te rig yerine Animator'ün oynattığı kare kare çizimi (Attack4HD_*) göster.")] public bool frameAttack4 = true;
        [Tooltip("Kare kare çizimin boyu: saldırı rig pikseli / sayfa pikseli (sayfa tam çözünürlük). Büyük = daha büyük karakter.")] public float frameAtkPxScale = 3.7f;
        [Tooltip("Kare kare çizimin ayak noktasına göre kayması (birim, sağa bakarken).")] public Vector2 frameAtkOffset = Vector2.zero;
        [Tooltip("Rig sıralamasına eklenen sıra (rig parçalarının önünde kalsın).")] public int frameAtkOrder = 60;
        SpriteRenderer frameSR; Sprite frameLast; bool lastAtkFrames, frameWant;
        [Header("Kare kare squash & stretch")]
        [Tooltip("Saldırı karelerinde squash/stretch (pivot ayakta: karakter yere basılı esner).")] public bool frameSquash = true;
        [Tooltip("Genel şiddet çarpanı (0 = kapalı, 1 = varsayılan, 2 = çok abartılı).")] [Range(0f, 2f)] public float squashAmount = 1f;
        [Tooltip("Hedef şekle oturma hızı. Büyük = daha sert/anlık.")] public float squashSpeed = 26f;
        Vector2 sqCur = Vector2.one; bool sqWas;
        [Tooltip("Saldırı bitince son kare, altındaki kılıçlı idle'ın üstünde bu sürede (sn) yavaşça kaybolur. 0 = anında geçiş.")] public float frameFadeOut = 0.16f;
        [Tooltip("Kaybolurken son karenin düz duruşa oturma miktarı (squash'ı yumuşakça 1'e indirir).")] public bool frameFadeSettle = true;
        float frameFadeT; bool frameFading;
        [Header("Smear parlaması (Impact gibi)")]
        [Tooltip("Kare kare saldırılardaki smear'ın üstüne toplamalı HDR parlama (Resources/AtkGlow). Bloom ile parlar.")] public bool frameSmearGlow = true;
        [Tooltip("Smear karesi bitince smear sis gibi genişleyip soluklaşarak dağılsın (sonraki saldırıdan bağımsız).")] public bool smearDissolve = false;
        [Header("Attack4 geçiş yumuşatma")]
        [Tooltip("Attack4 karelerinin arasında önceki karenin soluk bir izi kalır: sert kare atlamaları (özellikle dönüş) akıcı görünür.")] public bool frameBlendAtk4 = true;
        [Tooltip("Geçiş izinin kaybolma süresi (sn).")] [Range(0.02f, 0.2f)] public float frameBlendTime = 0.07f;
        [Tooltip("Geçiş izinin başlangıç opaklığı.")] [Range(0f, 1f)] public float frameBlendAlpha = 0.55f;
        Sprite blendPrev; Vector3 blendPos, blendScale; bool blendFlip; int blendOrder, blendLayer;
        [Tooltip("Dağılma süresi (sn).")] [Range(0.1f, 2f)] public float dissolveLife = 0.5f;
        [Tooltip("Dağılırken büyüme çarpanı (1 = hiç büyümez, 1.1 = %10).")] [Range(1f, 2f)] public float dissolveGrow = 1.1f;
        [Tooltip("Başlangıç opaklığı.")] [Range(0f, 1f)] public float dissolveAlpha = 0.7f;
        [Tooltip("Dağılırken yukarı süzülme (dünya birimi).")] [Range(0f, 1f)] public float dissolveRise = 0.1f;
        [Tooltip("Sis katmanı (daha büyük, soluk ikinci kopya).")] public bool dissolveHaze = true;
        [Tooltip("Sis katmanının ek büyümesi (ana büyümeye göre çarpan).")] [Range(1f, 2f)] public float dissolveHazeGrow = 1.12f;
        SmearGhostSpawner ghostSp;
        [Tooltip("Smear parlama rengi (HDR). Impact Mat ≈ 8.5 beyaz; burada hafif mavi.")] [ColorUsage(true, true)] public Color frameSmearColor = new Color(1.2f, 2.2f, 4.5f, 1f);
        SpriteRenderer glowSR; Material glowMat; readonly System.Collections.Generic.Dictionary<string, Sprite> glowSprites = new System.Collections.Generic.Dictionary<string, Sprite>();
        readonly System.Collections.Generic.HashSet<string> glowLoaded = new System.Collections.Generic.HashSet<string>();
        Sprite GlowFor(Sprite s)
        {
            if (s == null) return null;
            string n = s.name;   // "Attack2HD_3" -> sayfa "Attack2_glow", sprite "Attack2G_3"
            if (n.StartsWith("ParryHD_") || n.StartsWith("ParryRev_"))
            {
                string gk = n.StartsWith("ParryRev_") ? "ParryRevG_" + n.Substring(9) : "ParryG_" + n.Substring(8); Sprite pg;
                if (!glowSprites.TryGetValue(gk, out pg)) { pg = Resources.Load<Sprite>("AtkGlow/" + gk); glowSprites[gk] = pg; }
                return pg;
            }
            int u = n.IndexOf("HD_");
            if (u < 0) return null;
            string sheet = n.Substring(0, u) + "_glow";
            if (glowLoaded.Add(sheet))
                foreach (var g in Resources.LoadAll<Sprite>("AtkGlow/" + sheet)) glowSprites[g.name] = g;
            Sprite r; return glowSprites.TryGetValue(n.Substring(0, u) + "G_" + n.Substring(u + 3), out r) ? r : null;
        }
        void UpdateGlow(Sprite s, float alpha)
        {
            Sprite g = frameSmearGlow ? GlowFor(s) : null;
            if (g == null) { if (glowSR != null) glowSR.enabled = false; return; }
            if (glowSR == null)
            {
                var go = new GameObject("~atk_glow");   // "~": arka ışık / gölge kopyası almasın
                go.transform.SetParent(frameSR.transform, false);
                glowSR = go.AddComponent<SpriteRenderer>();
                if (smearDissolve) ghostSp = go.AddComponent<SmearGhostSpawner>();
                Shader gs = Resources.Load<Shader>("AtkGlow/SpriteGlowAdd");
                if (gs == null) gs = Shader.Find("ProjectM/SpriteGlowAdd");
                if (gs != null) { glowMat = new Material(gs) { name = "Ronni_AtkGlow" }; glowSR.sharedMaterial = glowMat; }
            }
            if (glowMat != null) glowMat.SetColor("_GlowColor", frameSmearColor);
            if (ghostSp != null) { ghostSp.life = dissolveLife; ghostSp.grow = dissolveGrow; ghostSp.startAlpha = dissolveAlpha; ghostSp.rise = dissolveRise; ghostSp.haze = dissolveHaze; ghostSp.hazeGrow = dissolveHazeGrow; }
            glowSR.enabled = true;
            glowSR.sprite = g;
            glowSR.flipX = frameSR.flipX;
            glowSR.sortingLayerID = frameSR.sortingLayerID; glowSR.sortingOrder = frameSR.sortingOrder + 1;
            glowSR.color = new Color(1f, 1f, 1f, alpha);
        }
        // [saldırı, kare] = (x, y) ölçek. Attack2-4: kare 1 hazırlık (bas), 2 vuruş (vuruş yönünde uza), 3 devam, 4 otur. Attack1 6 karelidir (bkz. satır).
        static readonly Vector2[,] SquashTable =
        {
            { new Vector2(0.96f, 1.07f), new Vector2(1.14f, 0.88f), new Vector2(1.08f, 0.93f), new Vector2(1.28f, 0.84f), new Vector2(0.92f, 1.11f), new Vector2(1.03f, 0.97f) },   // Attack1 (6 kare): 1 yükselen kılıç (uza), 2 çömel (güçlü bas), 3 geçiş (hâlâ basık), 4 vuruş (sert yatay uza), 5 smear yayı (yukarı uza), 6 otur
            { new Vector2(1.08f, 0.92f), new Vector2(1.22f, 0.88f), new Vector2(0.93f, 1.10f), Vector2.one, Vector2.one, Vector2.one },   // Attack2: öne uzanma -> yukarı yay
            { new Vector2(1.12f, 0.89f), new Vector2(1.26f, 0.86f), new Vector2(1.10f, 0.94f), Vector2.one, Vector2.one, Vector2.one },   // Attack3: gerilme -> saplama
            { new Vector2(1.09f, 0.91f), new Vector2(0.95f, 1.08f), new Vector2(1.24f, 0.87f), Vector2.one, Vector2.one, Vector2.one },   // Attack4: sıçrama -> dönüş -> inişte büyük kesim
        };
        // ---- Kare kare hareket akışı: sprite kareleri arasında karakter yaslanır, öne atılır, geri oturur (sprite'lara dokunmadan) ----
        [Header("Kare kare hareket akışı (lunge / yaslanma)")]
        [Tooltip("Saldırı karelerinde karakteri hazırlıkta hafif geri yaslayıp vuruşta öne atar, sonra yumuşakça oturtur.")] public bool frameFlow = true;
        [Tooltip("Genel şiddet çarpanı (0 = kapalı, 1 = varsayılan, 2 = abartılı).")] [Range(0f, 2f)] public float flowAmount = 1f;
        [Tooltip("Hedef pozisyona oturma hızı. Büyük = daha sert.")] public float flowSpeed = 24f;
        Vector3 flowCur;
        // [saldırı, kare] = (ileri kayma, yukarı kayma: sprite boyunun oranı; öne eğilme: derece). Kare 1 hazırlık (geri yaslan), 2 vuruş (öne at), 3 devam, 4 otur.
        static readonly Vector3[,] FlowTable =
        {
            { new Vector3(-0.07f, 0f, -8f), new Vector3(-0.07f, 0f, -6f), new Vector3(0f, 0f, 0f), new Vector3(0.14f, 0f, 6f), new Vector3(0.07f, 0f, 3f), new Vector3(0.015f, 0f, 0f), Vector3.zero },   // Attack1 (6 kare): yükselen kılıç + çömel (belirgin geri yaslanma), geçiş (nötr), vuruş (sert öne atılma), smear yayı, otur
            { new Vector3(-0.07f, 0f, -6f), new Vector3(0.15f, 0f, 9f),  new Vector3(0.095f, 0f, 4.5f),    new Vector3(0.018f, 0f, 0f), Vector3.zero, Vector3.zero, Vector3.zero },   // Attack2
            { new Vector3(-0.09f, 0f, -7f), new Vector3(0.19f, 0f, 10.5f),   new Vector3(0.13f, 0f, 6f),   new Vector3(0.022f, 0f, 0f), Vector3.zero, Vector3.zero, Vector3.zero },   // Attack3 (saplama)
            { new Vector3(-0.07f, 0f, -4.5f), new Vector3(0.07f, 0.06f, 0f), new Vector3(0.12f, 0.04f, 6f), new Vector3(0.185f, 0f, 11f), new Vector3(0.06f, 0f, 3f), Vector3.zero, Vector3.zero },   // Attack4 (sıçrama -> büyük kesim)
        };
        Vector3 FlowTarget(Sprite s)
        {
            if (!frameFlow || s == null) return Vector3.zero;
            string n = s.name;   // "Attack2HD_3"
            if (n.Length < 11 || !n.StartsWith("Attack")) return Vector3.zero;
            int a = n[6] - '1', k = n[n.Length - 1] - '1';
            if (a < 0 || a >= FlowTable.GetLength(0) || k < 0 || k >= FlowTable.GetLength(1)) return Vector3.zero;
            return FlowTable[a, k] * flowAmount;
        }
        Quaternion blendRot = Quaternion.identity;
        void OnEnable() { CombatImpactFeedback.AttackImpact += OnAttackImpact; }
        // Vuruş anı: donma sırasında görünsün diye anında uygula (sonra yumuşakça hedefe döner).
        void OnAttackImpact(int step, float kick)
        {
            if (!frameSquash || frameSR == null || !frameSR.enabled) return;
            // Vuruş darbesi: yaylı sapma. Hit-stop'ta (deltaTime 0) olduğu gibi DONAR, sonra yaylanarak oturur
            // (basık -> uzun -> hafif basık). Son kombo vuruşu daha güçlü.
            float k = kick * hitKickMul * (step >= 4 ? 1.35f : 1f) * squashAmount;
            k = Mathf.Min(k, hitKickMax);
            hitSpr = new Vector2(k, -k * 0.85f);
            hitSprVel = Vector2.zero;
            // B: vuruşta karakter öne itilir + öne eğilir; yay geri tepmeyle (recoil) oturur. Son kombo vuruşu en güçlüsü.
            float m = hitLungeMul * (step >= 4 ? 1.9f : 1f + 0.12f * Mathf.Max(0, step - 1));
            hitFlow = new Vector2(0.042f * m, 4.5f * m);
            hitFlowVel = Vector2.zero;
        }
        [Tooltip("Vuruşta öne itilme / eğilme / geri tepme çarpanı (0 = kapalı).")] public float hitLungeMul = 1f;
        Vector2 hitFlow, hitFlowVel;   // x: ileri kayma (sprite boyu oranı), y: öne eğilme (derece)
        [Header("Vuruş darbesi (yaylı squash)")]
        [Tooltip("Vuruş darbesi çarpanı (CombatImpactFeedback.hitKick x bu). 1 = eski, 2.5 = belirgin.")] public float hitKickMul = 2.5f;
        [Tooltip("Tek vuruşta en fazla ölçek sapması.")] public float hitKickMax = 0.28f;
        [Tooltip("Yay hızı (rad/s). Büyük = daha hızlı titrer.")] public float hitSprOmega = 30f;
        [Tooltip("Sönüm (0..1). Küçük = daha çok yaylanır.")] [Range(0.1f, 1f)] public float hitSprZeta = 0.34f;
        Vector2 hitSpr, hitSprVel;
        [Header("Parry darbesi (başarılı parry'de yaylı squash)")]
        [Tooltip("Başarılı parry'de darbe çarpanı (0 = kapalı).")] public float parryPopAmount = 1f;
        [Tooltip("Çarpma anı ezilme (x genişler, y basılır); yay bırakınca yukarı/ileri uzayıp oturur.")] public Vector2 parryPopSquash = new Vector2(0.2f, -0.17f);
        [Tooltip("Çarpmanın geri itmesi: (geri kayma = sprite boyu oranı, geriye eğilme derece). Yay bırakınca öne doğru savrulur.")] public Vector2 parryPopRecoil = new Vector2(-0.03f, -3.5f);
        void TickHitSpring()
        {
            if (hitSpr == Vector2.zero && hitSprVel == Vector2.zero && hitFlow == Vector2.zero && hitFlowVel == Vector2.zero) return;
            float dt = Mathf.Min(Time.deltaTime, 0.033f);
            if (dt <= 0f) return;   // hit-stop: sapma donuk kalır
            int steps = Mathf.Clamp(Mathf.CeilToInt(dt / 0.008f), 1, 5);
            float h = dt / steps, w2 = hitSprOmega * hitSprOmega, c = 2f * hitSprZeta * hitSprOmega;
            for (int i = 0; i < steps; i++)
            {
                hitSprVel += (-w2 * hitSpr - c * hitSprVel) * h;
                hitSpr += hitSprVel * h;
                hitFlowVel += (-w2 * 0.8f * hitFlow - c * 0.9f * hitFlowVel) * h;
                hitFlow += hitFlowVel * h;
            }
            if (hitFlow.sqrMagnitude < 1e-7f && hitFlowVel.sqrMagnitude < 1e-5f) { hitFlow = Vector2.zero; hitFlowVel = Vector2.zero; }
            if (hitSpr.sqrMagnitude < 1e-6f && hitSprVel.sqrMagnitude < 1e-4f) { hitSpr = Vector2.zero; hitSprVel = Vector2.zero; }
        }
        Vector2 SquashTarget(Sprite s)
        {
            if (guardSqOn) return guardSq;
            if (s != null && s.name == "DashHD_1") return DashSquashTarget();
            if (!frameSquash || s == null) return Vector2.one;
            string n = s.name;   // "Attack2HD_3"
            if (n.Length < 11) return Vector2.one;
            int a = n[6] - '1', k = n[n.Length - 1] - '1';
            if (a < 0 || a >= SquashTable.GetLength(0) || k < 0 || k >= SquashTable.GetLength(1)) return Vector2.one;
            return Vector2.one + (SquashTable[a, k] - Vector2.one) * squashAmount;
        }
        // ---- Parry kare kare (ParryHD_1: kilic dik, _2: smear'li, _3: smear'siz) ----
        [Header("Parry kare kare")]
        [Tooltip("Parry'de rig yerine el cizimi kareleri (Resources/ParryFrames) oynat.")] public bool frameParry = true;
        [Tooltip("1. kare (kilic dik) suresi (sn). Kucuk = cevik.")] public float parryF1Time = 0.045f;
        [Tooltip("2. kare (smear'li) suresi (sn).")] public float parryF2Time = 0.06f;
        [Tooltip("Parry karelerinin ek kaymasi (birim, sagabakarken). Pivot sol (arka) ayakta; ileri/geri ince ayar.")] public Vector2 parryOffset = Vector2.zero;
        [Tooltip("Basarili parry'den sonra son karede kalma suresi (sn).")] public float parryHoldAfterSuccess = 0.28f;
        [Tooltip("Parry = Attack2 karelerinin TERSTEN oynatilmasi (Resources/ParryFrames/ParryRev_1..4). Kapali = eski 3 kareli parry.")] public bool parryAttack2Reverse = false;
        [Tooltip("Ters Attack2 parry'sinde her karenin suresi (sn). Kucuk = cevik.")] public float parryRevFrameTime = 0.04f;
        Sprite[] parryRevSprites; bool parryRevLoaded;
        bool ParryRevOn() { if (!parryAttack2Reverse) return false; ParrySprite(0); return parryRevSprites != null; }
        float parryFrameT = -1f, parryFrameEnd; bool parrySuccPrev; Sprite[] parrySprites; bool parryLoaded;
        // ---- Block / parry v2: kare kare savunma (ParryHD_1 = koruma pozu) ----
        [Header("Block / Parry v2 (kare kare savunma)")]
        [Tooltip("Block ve parry denemesinde rig yerine el çizimi koruma karesi (ParryHD_1: kılıç dik, gövdenin önünde). Başarılı parry aynı kareden başlar: poz atlaması olmaz. Kapalı = eski rig block.")] public bool frameGuard = true;
        [Tooltip("Koruma pozuna geçişte ilk sıkışma (hazırlık) süresi (sn).")] public float guardRaiseTime = 0.08f;
        [Tooltip("Geçişin ilk anındaki sıkışma (x geniş, y basık).")] public Vector2 guardRaiseSquash = new Vector2(1.07f, 0.93f);
        [Tooltip("Koruma beklerken nefes / gerginlik genliği (ölçek oranı).")] [Range(0f, 0.04f)] public float guardBreath = 0.012f;
        [Tooltip("Nefes periyodu (sn).")] public float guardBreathPeriod = 1.4f;
        [Tooltip("Block'a vuruş gelince geri itilme (dünya birimi).")] public float guardHitPush = 0.16f;
        [Tooltip("Block vuruş tepkisi süresi (sn).")] public float guardHitTime = 0.26f;
        [Tooltip("Block vuruşunda anlık sıkışma (x, y ekleme).")] public Vector2 guardHitSquash = new Vector2(0.10f, -0.08f);
        [Tooltip("Block vuruşunda titreme genliği (dünya birimi).")] public float guardHitShake = 0.025f;
        [Tooltip("Parry tuşuna basınca (başarı beklenmeden) kısa yukarı esneme: block'tan ayırt edilsin.")] public Vector2 parryTryStretch = new Vector2(-0.06f, 0.08f);
        [Tooltip("Parry denemesi esneme süresi (sn).")] public float parryTryTime = 0.16f;
        [Tooltip("Başarılı parry kareleri: (kare1, kare2 smear, kare3) squash hedefleri.")] public Vector2[] parrySquash = { new Vector2(1.05f, 0.95f), new Vector2(1.13f, 0.91f), new Vector2(0.95f, 1.06f) };
        [Tooltip("Parry kareleri arasında önceki karenin soluk izi (geçiş yumuşar).")] public bool parryBlend = true;
        [Tooltip("Parry karelerini (ParryHD_*) saldırı kareleriyle aynı hizaya getiren yatay düzeltme (sayfa pikseli). ParryHD pivotu arka ayakta, saldırı kareleri gövde altında: -177 = kafa/gövde rig ile aynı yerde kalır, block/parry'de karakter öne kaymaz.")] public float parryAlignPx = -177f;
        bool guardWant, guardSqOn; float guardT = -1f, guardHitT = -1f, parryTryT = -1f, guardOffX; Vector2 guardSq = Vector2.one;
        [Header("Block (Spine rig: RonniIdleKatana 'block' / 'block_hit')")]
        [Tooltip("Block'u kılıç çekik idle rig'inde iki elli 'block' klibiyle oynat (kare kare koruma yerine).")] public bool rigBlock = true;
        [Tooltip("Block kaldırma hızı (1 = klip hızı).")] public float blockSpeed = 1f;
        [Tooltip("Bırakınca kılıcı indirme hızı (klip hızının katı).")] public float blockReleaseSpeed = 1.6f;
        public float blockClipDur = 0.5f, blockGuardT = 0.29f, blockHitDur = 0.2333f, blockSwapT = 0.0333f, blockFrontT = 0.1333f;
        static readonly string[] BlockFront = { "idle_kilic", "idle_kilic_tsuka", "idle_sol_kol_ic", "idle_sol_el", "idle_sol_kol" };
        static readonly string[] BlockOrig = { "idle_sag_kol", "idle_sag_kol_ic", "idle_sag_el" }, BlockCopy = { "blk_kol", "blk_kol_ic", "blk_el" };
        bool blockOn, blockFrame, blockSwapOn, blockFrontOn; float blockRelT = -1f, blockPh;
        bool BlockRigReady() { return rigBlock && rigDrawn != null && rigDrawn.IsBuilt && playerDrawn != null && playerDrawn.HasClip("block"); }
        void BlockLayers(bool swap, bool front)
        {
            if (rigDrawn == null) return;
            if (swap != blockSwapOn) { blockSwapOn = swap; for (int i = 0; i < BlockOrig.Length; i++) { rigDrawn.SetVisible(BlockOrig[i], !swap); rigDrawn.SetVisible(BlockCopy[i], swap); } }
            if (front != blockFrontOn) { blockFrontOn = front; rigDrawn.SetFrontParts(front ? BlockFront : null); }
        }
        void EndBlock()
        {
            blockOn = false; blockRelT = -1f; blockPh = 0f;
            BlockLayers(false, false);
            if (playerDrawn != null) { playerDrawn.clip = "idle"; playerDrawn.manual01 = -1f; }
        }
        bool GuardFrames() { return frameGuard && ParrySprite(0) != null && !ParryRevOn(); }
        Sprite ParrySprite(int i)
        {
            if (parryAttack2Reverse)
            {
                if (!parryRevLoaded)
                {
                    parryRevLoaded = true;
                    var rs = new Sprite[4]; bool ok = true;
                    for (int k = 0; k < 4; k++) { rs[k] = Resources.Load<Sprite>("ParryFrames/ParryRev_" + (k + 1)); if (rs[k] == null) ok = false; }
                    if (ok) parryRevSprites = rs;
                }
                if (parryRevSprites != null) return parryRevSprites[Mathf.Clamp(i, 0, 3)];
            }
            if (!parryLoaded)
            {
                parryLoaded = true;
                var ps = new Sprite[3];
                for (int k = 0; k < 3; k++) ps[k] = Resources.Load<Sprite>("ParryFrames/ParryHD_" + (k + 1));
                if (ps[0] != null && ps[1] != null && ps[2] != null) parrySprites = ps;
            }
            return parrySprites != null ? parrySprites[Mathf.Clamp(i, 0, 2)] : null;
        }
        int ParryIndex(float t) { if (ParryRevOn()) return Mathf.Min(3, (int)(t / Mathf.Max(0.005f, parryRevFrameTime))); return t < parryF1Time ? 0 : t < parryF1Time + parryF2Time ? 1 : 2; }
        bool FrameAtkActive()
        {
            string pfx = atkClip == "attack1" && frameAttack1 ? "Attack1HD" : atkClip == "attack2" && frameAttack2 ? "Attack2HD" : atkClip == "attack3" && frameAttack3 ? "Attack3HD" : atkClip == "attack4" && frameAttack4 ? "Attack4HD" : null;
            if (pfx == null || originalSprite == null) return false;
            Sprite s = originalSprite.sprite;
            if (s != null && s.name.StartsWith(pfx)) { frameLast = s; return true; }
            return lastAtkFrames && frameLast != null && frameLast.name.StartsWith(pfx);   // Animator klibi swing'den önce biterse son karede kal
        }
        void ShowFrame(Sprite s)
        {
            if (s == null) return;
            if (frameSR == null)
            {
                var go = new GameObject("Ronni_Atk1Frames");
                go.transform.SetParent(transform, false);
                frameSR = go.AddComponent<SpriteRenderer>();
                Shader sh = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (sh == null) sh = Shader.Find("Sprites/Default");
                if (sh != null) frameSR.sharedMaterial = new Material(sh) { name = "Ronni_Atk1Frames" };
                // Arka ışık (CharacterRim) yeni sprite'ı yarım saniye beklemeden hemen tanısın.
                var rimC = GetComponentInParent<CharacterRim>();
                if (rimC == null && player != null) rimC = player.GetComponentInChildren<CharacterRim>(true);
                if (rimC != null) rimC.RequestScan();
            }
            if (blendPrev != null && blendPrev != s && frameSR.enabled && ((frameBlendAtk4 && s.name.StartsWith("Attack4HD") && blendPrev.name.StartsWith("Attack4HD")) || (parryBlend && s.name.StartsWith("ParryHD_") && blendPrev.name.StartsWith("ParryHD_"))))
            {
                var ai = new GameObject("~atk_afterimage");
                ai.transform.position = blendPos; ai.transform.localScale = blendScale; ai.transform.rotation = blendRot;
                var air = ai.AddComponent<SpriteRenderer>();
                air.sprite = blendPrev; air.flipX = blendFlip; air.sharedMaterial = frameSR.sharedMaterial;
                air.sortingLayerID = blendLayer; air.sortingOrder = blendOrder - 1;
                ai.AddComponent<FrameAfterImage>().Init(air, frameBlendAlpha, frameBlendTime);
            }
            bool flowWasOn = frameSR.enabled;
            frameWant = true; frameFading = false;
            frameSR.enabled = true;
            frameSR.sprite = s;
            frameSR.flipX = originalSprite != null && originalSprite.flipX;
            frameSR.color = mirrorSpriteColor && originalSprite != null ? originalSprite.color : Color.white;
            RonniSkinRig r = rigAtk != null ? rigAtk : rig;
            frameSR.sortingLayerName = r.sortingLayerName; frameSR.sortingOrder = r.sortingOrderOffset + frameAtkOrder;
            // dünya boyu: 1 sayfa pikseli = frameAtkPxScale saldırı-rig pikseli
            float L = rig.unitsPerPixel * Mathf.Abs(r.transform.lossyScale.y) * frameAtkPxScale * s.pixelsPerUnit;
            Vector3 ls = transform.lossyScale;
            Vector2 sqT = SquashTarget(s);
            if (!sqWas) { sqCur = sqT; sqWas = true; }
            else sqCur = Vector2.Lerp(sqCur, sqT, 1f - Mathf.Exp(-squashSpeed * Time.deltaTime));
            TickHitSpring();
            frameSR.transform.localScale = new Vector3(L / (Mathf.Abs(ls.x) > 1e-6f ? ls.x : 1f) * sqCur.x * (1f + hitSpr.x), L / (Mathf.Abs(ls.y) > 1e-6f ? Mathf.Abs(ls.y) : 1f) * sqCur.y * (1f + hitSpr.y), 1f);
            UpdateGlow(s, frameSR.color.a);
            float fx = frameSR.flipX ? -1f : 1f;
            Vector2 off = frameAtkOffset; if (s.name.StartsWith("ParryHD_") || s.name.StartsWith("ParryRev_")) off += parryOffset; if (s.name.StartsWith("ParryHD_")) off.x += parryAlignPx * rig.unitsPerPixel * Mathf.Abs(r.transform.lossyScale.y) * frameAtkPxScale; off.x += guardOffX; guardOffX = 0f;
            float flowH = L * s.rect.height / Mathf.Max(1f, s.pixelsPerUnit);
            Vector3 flowT = FlowTarget(s);
            if (!flowWasOn) flowCur = Vector3.zero; else flowCur = Vector3.Lerp(flowCur, flowT, 1f - Mathf.Exp(-flowSpeed * Time.deltaTime));
            frameSR.transform.rotation = Quaternion.Euler(0f, 0f, -(flowCur.z + hitFlow.y) * fx);
            frameSR.transform.position = r.transform.position + new Vector3(off.x * fx + (flowCur.x + hitFlow.x) * fx * flowH, off.y + flowCur.y * flowH, 0f);
            blendPrev = s; blendRot = frameSR.transform.rotation; blendPos = frameSR.transform.position; blendScale = frameSR.transform.lossyScale; blendFlip = frameSR.flipX; blendOrder = frameSR.sortingOrder; blendLayer = frameSR.sortingLayerID;
        }
        bool atkWasOn, atkHoldIdle; float atkFade, atkHoldT, atkRetT;
        RonniSkinRig fadeTop; int fadeTopBase;
        void RestoreFadeTop() { if (fadeTop != null) { fadeTop.SetSorting(fadeTop.sortingLayerName, fadeTopBase); fadeTop = null; } }
        [Tooltip("Smear izinin uzunluğu (yayın oranı): kılıç ucunun arkasında bu kadar iz kalır.")] public float smearTrail = 0.55f;
        [Tooltip("İzin kuyruğunun tamamen silinmeye başladığı klip zamanı.")] public float smearEndStart = 0.60f;
        [Tooltip("Smear yönünü yatay çevirir (sağdan sola açılır, sağ taraftan solar).")] public bool smearFlip2 = true;
        static readonly float[] TipT2 = { 0.44f, 0.62f };
        static readonly float[] TipU2 = { 0f, 1f };
        static readonly float[] TipT = { 0.396f, 0.418f, 0.440f, 0.462f, 0.484f, 0.506f, 0.528f, 0.550f, 0.572f, 0.594f, 0.616f, 0.638f };
        static readonly float[] TipU = { 0.000f, 0.017f, 0.081f, 0.209f, 0.274f, 0.446f, 0.528f, 0.731f, 0.839f, 0.928f, 0.997f, 1.000f };
        // Smear: kılıç ucunu takip eden ay şeklinde iz (baş = ucun konumu, kuyruk solar)
        void SmearWin(float ph)
        {
            if (lastAtkClip == "attack2")
            {
                // ters yön: uç soldan sağa gider (attack1'in tersi), iz ucun arkasında
                float tau = Tau2(ph), u2 = 0f;
                if (tau > TipT[0]) { u2 = 1f; for (int i = 1; i < TipT.Length; i++) if (tau <= TipT[i]) { u2 = Mathf.Lerp(TipU[i - 1], TipU[i], (tau - TipT[i - 1]) / (TipT[i] - TipT[i - 1])); break; } }
                float tip = 1f - u2;
                float kk = Mathf.Clamp01((0.40f - tau) / 0.15f); kk = kk * kk * (3f - 2f * kk);
                float tl = Mathf.Max(tip - smearTrail, kk);
                Color c0 = new Color(smearGlow, smearGlow, smearGlow, 0f), c1 = new Color(smearGlow, smearGlow, smearGlow, 1f);
                if (tau >= 0.62f) tl = tip = 0f;
                rigAtk.SetPartWindow("atk_smear", tl, tip, c0, c1, true);
                return;
            }
            float[] tt = TipT, uu = TipU;
            float head = 0f;
            if (ph > tt[0])
            {
                head = 1f;
                for (int i = 1; i < tt.Length; i++)
                    if (ph <= tt[i]) { head = Mathf.Lerp(uu[i - 1], uu[i], (ph - tt[i - 1]) / (tt[i] - tt[i - 1])); break; }
            }
            float k = Mathf.Clamp01((ph - smearEndStart) / 0.18f); k = k * k * (3f - 2f * k);
            float tail = Mathf.Max(head - smearTrail, k);
            if (ph <= tt[0]) tail = head = 0f;
            Color ct = new Color(smearGlow, smearGlow, smearGlow, 0f), ch = new Color(smearGlow, smearGlow, smearGlow, 1f);
            if (smearFlip2) rigAtk.SetPartWindow("atk_smear", 1f - head, 1f - tail, ch, ct, true);
            else rigAtk.SetPartWindow("atk_smear", tail, head, ct, ch);
        }
        // attack2 = attack1'in tersi (ileri uzanmış duruştan hazır duruşa). Ritim: hafif geri çekiş -> hızlı ters kesim -> yavaş yerleşme
        float AtkRemap2(float p)
        {
            // çok yavaş geri çekiş (kılıç ortaya gelene kadar) -> hızlı ters kesim -> yerleşme
            if (p <= 0.58f) { float u = p / 0.58f; return 0.30f * Mathf.Pow(u, 1.3f); }
            if (p <= 0.72f) return Mathf.Lerp(0.30f, 0.46f, (p - 0.58f) / 0.14f);
            float v = (p - 0.72f) / 0.28f; return Mathf.Lerp(0.46f, atkSettle2, v);
        }
        static readonly float[] Tau2T = { 0f, 0.12f, 0.22f, 0.42f, 0.60f, 1f };
        static readonly float[] Tau2V = { 0.80f, 0.74f, 0.62f, 0.42f, 0.12f, 0f };
        static float Tau2(float ph)
        {
            for (int i = 1; i < Tau2T.Length; i++)
                if (ph <= Tau2T[i]) { float u = Mathf.Clamp01((ph - Tau2T[i - 1]) / (Tau2T[i] - Tau2T[i - 1])); u = u * u * (3f - 2f * u); return Mathf.Lerp(Tau2V[i - 1], Tau2V[i], u); }
            return Tau2V[Tau2V.Length - 1];
        }
        float AtkRemap(float p)
        {
            if (lastAtkClip == "attack2") return AtkRemap2(p);
            if (p <= atkCutStart) { float u = p / atkCutStart; return atkClipCutA * u * u; }
            if (p >= atkCutEnd) { float u = (p - atkCutEnd) / (1f - atkCutEnd); float e = 1f - (1f - u) * (1f - u) * (1f - u); return Mathf.Lerp(atkClipCutB, atkSettlePhase, e); }
            return Mathf.Lerp(atkClipCutA, atkClipCutB, (p - atkCutStart) / (atkCutEnd - atkCutStart));
        }
        [Tooltip("Zıplama anında yatay hız bundan küçükse 'yerinde zıplama' sayılır.")] public float standJumpMaxSpeed = 1.0f;
        RonniSkinRig rigJump; RonniClipPlayer playerJump; Vector3 baseJump = Vector3.one; bool standJump;
        [Header("Saldırı 1 (yeni çizim + kılıç izi)")]
        [Tooltip("Attack1 için karma çizim (Resources/<klasör>, 'attack1' klibi). Boş = eski ön görünüm saldırısı.")] public string attackFolder = "RonniAtk";
        RonniSkinRig rigAtk; RonniClipPlayer playerAtk; Vector3 baseAtk = Vector3.one; string atkClip; float atkProg;
        [Header("Bekleme (ateş böceği)")]
        [Tooltip("Idle'da hareketsiz kalınca arada bir elini uzatıp konan ateş böceğine bakar (RonniIdle 'wait' klibi).")] public bool fireflyWait = true;
        [Tooltip("Hareketsiz kaldıktan sonra bekleme animasyonunun başlaması için geçecek süre aralığı (sn).")] public Vector2 waitDelay = new Vector2(12f, 24f);
        float idleSince = -1f, waitAfter; bool waiting; RonniFirefly firefly;
        [Tooltip("Vertical speed that maps to full rise / full fall pose.")] public float airVelocityRange = 12f;
        int bMode, jumpCount; float airU;
        [Header("Koşudan durma")]
        [Tooltip("Koşu biterken kısa bir frenleme animasyonu (stop klibi) oynat.")] public bool stopAnimEnabled = true;
        [Tooltip("Frenleme animasyonunun süresi (sn).")] public float stopDuration = 0.25f;
        [Tooltip("Frenleme için koşudaki en düşük yatay hız.")] public float stopMinSpeed = 2f;
        [Tooltip("Frenlemeden idle'a çapraz geçiş süresi (sn). 0 = ani.")] public float stopFade = 0.14f;
        [Tooltip("Frenleme<->idle geçişindeki squash/stretch gücü (1 = normal, 0 = yok).")] [Range(0f, 1f)] public float stopPopAmount = 0.3f;
        float stopT = -1f, stopLastSpeed, stopFadeT = -1f, popAmt = 1f;
        RonniDashDust dashDust; bool dustPrev; bool dashPrev; float dashEndT = -10f;
        [Tooltip("Dash bitince koşuya geçerken idle'a düşmesin diye tutulan süre (sn).")] public float dashRunGrace = 0.25f;
        [Tooltip("Dash başında toz patlaması + dash boyunca yer çizgileri.")] public bool dashDustEnabled = true;
        [Tooltip("Dash'in yüzde kaçında iz yazmayı bıraksın (iz oraya kadar uzar, uç yerinde kalıp solar)."), Range(0.3f, 1f)] public float dashTrailEndFrac = 0.75f;
        [Header("Transitions")]
        [Tooltip("Squash 'turn' when switching between the front and side drawings.")] public float switchPopTime = 0.11f;
        [Tooltip("Landing squash duration.")] public float landTime = 0.16f;
        [Range(0f, 0.3f)] public float landSquash = 0.12f;
        int visualId = -1; float popT = 1f, landT = 1f; bool wasGrounded = true;
        Transform partsA; Vector3 baseBL = Vector3.one, baseBR = Vector3.one;
        RonniSkinRig rigB, rigBL, rigBR; RonniClipPlayer playerB, playerBL, playerBR; bool showingB; bool bRight;
        RonniSkinRig rigRun; RonniClipPlayer playerRun; Vector3 baseRun = Vector3.one;

        RonniSkinRig rig; RonniSkinAnimator anim;
        PlayerCombatController combat; PlayerDefenseController defense; Animator oldAnim;
        bool wasParrying, wasAttacking; float parryStart = -10f; int attackStep = 1, lastStep; float lastAttackEnd = -10f; float lastProgress;
        static readonly int[] AttackHashes = { Animator.StringToHash("Attack1"), Animator.StringToHash("Attack2"), Animator.StringToHash("Attack3"), Animator.StringToHash("Attack4") };
        static readonly int ParryHash = Animator.StringToHash("Parry");
        Renderer[] hidden; Renderer mainOld; bool aligned;
        const float RonniHeightPx = 3350f; // hat top to sole

        void Awake()
        {
            rig = GetComponent<RonniSkinRig>();
            anim = GetComponent<RonniSkinAnimator>();
            if (player == null) player = GetComponentInParent<PlayerController>();
            if (originalSprite == null && player != null) originalSprite = player.playerSprite;
            CollectOld();
            if (player != null)
            {
                combat = player.GetComponent<PlayerCombatController>() ?? player.GetComponentInChildren<PlayerCombatController>();
                defense = player.GetComponent<PlayerDefenseController>() ?? player.GetComponentInChildren<PlayerDefenseController>();
                posture = player.GetComponent<PlayerPosture>() ?? player.GetComponentInChildren<PlayerPosture>();
                if (posture != null) lastPosture = posture.CurrentPosture;
                if (player.modelPivot != null) oldAnim = player.modelPivot.GetComponent<Animator>();
                if (oldAnim == null) oldAnim = player.GetComponentInChildren<Animator>(true);
            }
            anim.swordReady = false;   // oyun kilicsiz (kinda) idle ile baslar; sahnedeki eski startDrawn degerini ezer
            if (mainOld != null)
            {
                rig.sortingLayerName = mainOld.sortingLayerName;
                rig.sortingOrderOffset = mainOld.sortingOrder + OrderBoost;
            }
        }

        void CollectOld()
        {
            Transform root = player != null && player.modelPivot != null ? player.modelPivot : transform.parent;
            if (root == null) { hidden = new Renderer[0]; return; }
            var list = new System.Collections.Generic.List<Renderer>();
            float best = -1;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r.transform.IsChildOf(transform)) continue;
                if (r is ParticleSystemRenderer || r is TrailRenderer || r is LineRenderer) continue;
                list.Add(r);
                bool usable = !(r is SpriteRenderer sr) || sr.sprite != null;
                float size = r.bounds.size.y;
                if (usable && r.enabled && r.gameObject.activeInHierarchy && size >= best) { best = size; mainOld = r; }
            }
            hidden = list.ToArray();
        }

        void Align(Bounds b)
        {
            float s = 1f;
            if (matchOriginalSize)
            {
                float parentScale = transform.parent != null ? Mathf.Abs(transform.parent.lossyScale.y) : 1f;
                s = b.size.y / (RonniHeightPx * rig.unitsPerPixel * parentScale) * sizeMultiplier;
            }
            float sign = transform.localScale.x < 0 ? -1 : 1;
            transform.localScale = new Vector3(s * sign, s, 1f);
            transform.position = new Vector3(b.center.x + offset.x, b.min.y + offset.y, transform.position.z);
        }

        void Start()
        {
            if (!useProfileRun) return;
            MakeProfile("RonniSkinB", "Ronni_B_Left", out rigBL, out playerBL);   // facing left: far hand holds the katana
            MakeProfile("RonniSkinBR", "Ronni_B_Right", out rigBR, out playerBR); // facing right: visible hand holds the katana
            if (rigBR == null) { rigBR = rigBL; playerBR = playerBL; }
            rigB = rigBL; playerB = playerBL;
            if (rigBL != null) baseBL = rigBL.transform.localScale;
            if (rigBR != null) baseBR = rigBR.transform.localScale;

            // Ayrı koşu çizimi (her iki yön için aynı çizim; sağa bakınca kök zaten aynalanır)
            if (!string.IsNullOrEmpty(runFolder))
            {
                MakeProfile(runFolder, "Ronni_Run", out rigRun, out playerRun);
                if (rigRun != null)
                {
                    rigRun.transform.localScale *= runScale;
                    baseRun = rigRun.transform.localScale;
                }
            }

            // Dururken nefes alan + rüzgarda savrulan idle çizimi
            if (!string.IsNullOrEmpty(idleFolder))
            {
                MakeProfile(idleFolder, "Ronni_Idle", out rigIdle, out playerIdle);
                if (rigIdle != null)
                {
                    rigIdle.transform.localScale *= idleScale;
                    baseIdle = rigIdle.transform.localScale;
                    playerIdle.clip = "idle";
                }
            }

            // Attack1: kılıç çekili karma çizim
            tenka = GetComponent<RonniTenka>();
            if (tenka == null) tenka = gameObject.AddComponent<RonniTenka>();
            if (!string.IsNullOrEmpty(drawnIdleFolder))
            {
                MakeProfile(drawnIdleFolder, "Ronni_DrawnIdle", out rigDrawn, out playerDrawn);
                if (rigDrawn != null)
                {
                    rigDrawn.transform.localScale *= idleScale;
                    baseDrawn = rigDrawn.transform.localScale;
                    playerDrawn.clip = "idle"; playerDrawn.autoGlance = false; playerDrawn.autoBlink = false;
                }
            }

            if (!string.IsNullOrEmpty(attackFolder))
            {
                MakeProfile(attackFolder, "Ronni_Attack", out rigAtk, out playerAtk);
                if (rigAtk != null)
                {
                    rigAtk.transform.localScale *= idleScale;
                    baseAtk = rigAtk.transform.localScale;
                    playerAtk.clip = "attack1"; playerAtk.autoGlance = false;
                }
            }

            // Yerinde zıplama: idle gövdesi + zıplama bacakları
            if (!string.IsNullOrEmpty(standJumpFolder))
            {
                MakeProfile(standJumpFolder, "Ronni_StandJump", out rigJump, out playerJump);
                if (rigJump != null)
                {
                    rigJump.transform.localScale *= idleScale;
                    baseJump = rigJump.transform.localScale;
                    playerJump.clip = "air"; playerJump.autoBlink = true; playerJump.autoGlance = false;
                }
            }
        }

        void MakeProfile(string folder, string goName, out RonniSkinRig r, out RonniClipPlayer p)
        {
            r = null; p = null;
            if (Resources.Load<TextAsset>(folder + "/ronni_rig") == null) return;
            var go = new GameObject(goName); go.SetActive(false);
            go.transform.SetParent(transform, false);
            r = go.AddComponent<RonniSkinRig>(); r.resourceFolder = folder;
            r.unitsPerPixel = rig.unitsPerPixel; r.skinGrid = rig.skinGrid;
            r.sortingLayerName = rig.sortingLayerName; r.sortingOrderOffset = rig.sortingOrderOffset;
            p = go.AddComponent<RonniClipPlayer>();
            go.SetActive(true);
            float k = r.HeightPx > 0 ? rig.HeightPx / r.HeightPx : 1f;
            go.transform.localScale = new Vector3(k, k, 1f);
            r.SetShown(false);
        }

        void Update()
        {
            if (player == null) return;
            bool defending = player.IsDefending();
            // otomatik kılıç koyma için son giriş anı (tuş, fare tuşu, dokunuş, hareket, havada olmak)
            if (lastInputT <= 0f || Input.anyKey || Input.touchCount > 0 || Mathf.Abs(player.moveInput) > 0.1f || !player.isGrounded || player.isDashing) lastInputT = Time.time;
            anim.grounded = player.isGrounded;
            anim.running = forceRun || (player.isGrounded && !defending && Mathf.Abs(player.moveInput) > 0.1f);
            if (forceRun) anim.grounded = true;
            if (Mathf.Abs(player.facingDir) > 0.01f) anim.faceRight = player.facingDir > 0f;
            UpdateCombat();
        }

        int OldAttackStep()
        {
            if (oldAnim == null || !oldAnim.isActiveAndEnabled) return 0;
            var nxt = oldAnim.GetNextAnimatorStateInfo(0); var cur = oldAnim.GetCurrentAnimatorStateInfo(0);
            for (int i = 0; i < 4; i++) if (nxt.shortNameHash == AttackHashes[i]) return i + 1;
            for (int i = 0; i < 4; i++) if (cur.shortNameHash == AttackHashes[i]) return i + 1;
            return 0;
        }

        bool OldParryPlaying()
        {
            if (oldAnim == null || !oldAnim.isActiveAndEnabled) return false;
            var nxt = oldAnim.GetNextAnimatorStateInfo(0); var cur = oldAnim.GetCurrentAnimatorStateInfo(0);
            return (nxt.shortNameHash == ParryHash) || (cur.shortNameHash == ParryHash && cur.normalizedTime < 0.15f);
        }

        [Tooltip("KAPALI (varsayilan): dash'te ozel dash animasyonu oynamaz; yerine squash/stretch + after image (DashFx) kullanilir.")] public bool useDashAnimation = false;
        bool wasDashing; float dashStart;
        PlayerPosture posture; int lastPosture; bool wasBroken; string defClip; float defStart;
        // --- parry / block (attack rig clips) ---
        [Header("Parry / Block (RonniAtk)")]
        public float parryRaise9 = 0.07f, parryHold5 = 0.35f, parryReturn5 = 0.30f, blockRaise2 = 0.18f, blockReturn2 = 0.3f;
        public float idleDrawnPeriod2 = 3.2f; bool idleDrawnOn;
        [Tooltip("Kılıcı koyma tuşunda oynayan chiburi + noto süresi (sn).")] public float chiburiDur = 2.0f; float chiburiT = -1f; bool bladeCut;
        float chiForcedDur = -1f;
        // Kılıç sesleri: Resources/SwordSfx/sword_spin_* (chiburi dönüşü) ve sword_sheathe_* (kına koyma). Her çalışta biri rastgele.
        AudioSource swordSfx; AudioClip[] spinClips, sheatheClips; int lastSpinSfx = -1, lastSheSfx = -1;
        public float spinSfxVolume = 0.7f, sheatheSfxVolume = 0.8f;
        void PlaySwordSfx(bool spin)
        {
            if (swordSfx == null)
            {
                swordSfx = gameObject.AddComponent<AudioSource>(); swordSfx.playOnAwake = false; swordSfx.spatialBlend = 0f;
                var all = Resources.LoadAll<AudioClip>("SwordSfx");
                var a = new System.Collections.Generic.List<AudioClip>(); var b = new System.Collections.Generic.List<AudioClip>();
                foreach (var c in all) { if (c == null) continue; if (c.name.StartsWith("sword_spin_")) a.Add(c); else if (c.name.StartsWith("sword_sheathe_")) b.Add(c); }
                spinClips = a.ToArray(); sheatheClips = b.ToArray();
            }
            var arr = spin ? spinClips : sheatheClips;
            if (arr == null || arr.Length == 0) return;
            int k = Random.Range(0, arr.Length);
            int last = spin ? lastSpinSfx : lastSheSfx;
            if (arr.Length > 1 && k == last) k = (k + 1) % arr.Length;
            if (spin) lastSpinSfx = k; else lastSheSfx = k;
            swordSfx.pitch = Random.Range(0.95f, 1.05f);
            swordSfx.PlayOneShot(arr[k], spin ? spinSfxVolume : sheatheSfxVolume);
        }
        // Kına koyma sesi: bıçak kına girerken başlar, tsuba oturunca (tık) biter (klip ~0.9 sn).
        float SheatheSfxTime()
        {
            float d = ChiDur;
            if (TenkaReady()) { float s = d / TenkaTotal; return Mathf.Max(0.05f, (TenkaSpinReal + tenkaSlashSec + tenkaRegripTime + tenkaInsertTime) * s - 0.85f); }
            return d * 0.7f;
        }
        float ChiDur => chiForcedDur > 0f ? chiForcedDur : (TenkaReady() ? TenkaTotal : chiburiDur);
        [Header("Tenka chiburi zamanlaması (sn)")]
        [Tooltip("Hazırlık + kılıcı elde çevirme (hızlı). Eski klipte dönüş bölümü 1.60 klip-sn idi; süre klipteki yeni dönüş bölümüne (1.21 klip-sn) orantılı uygulanır, yani hız eskisiyle aynı.")] public float tenkaSpinTime = 0.8f;
        [Tooltip("Dönüş bitince yukarıdan aşağı tek seferlik yarım ay savurma: kaldır + vur + dinlen (sn).")] public float tenkaSlashSec = 0.42f;
        [Tooltip("Kılıcı ters tutuşa çevirip kının ağzına getirme.")] public float tenkaRegripTime = 0.4f;
        [Tooltip("Bıçağı kına sokma (yavaş, gerçekçi nōtō).")] public float tenkaInsertTime = 1.7f;
        [Tooltip("Tsuba'nın oturması (tık) + elin dinlenmeye dönüşü.")] public float tenkaReleaseTime = 0.45f;
        [Header("Otomatik kılıç koyma")]
        [Tooltip("Kılıç çekikken, yerdeyken bir süre hiç giriş gelmezse tenka chiburi oynayıp kılıcı kınına koyar.")] public bool autoSheathe = true;
        [Tooltip("Girişsiz bekleme süresi (sn).")] public float autoSheatheDelay = 2.5f;
        float lastInputT;
        // Tenka klibi: 103 kare, 3.4382 klip-sn. Bölüm sınırları (klip sn): dönüş 0-1.2135, savurma (kaldır + vur + dinlen) 1.2135-2.0382, ters tutuş 2.0382-2.3882, kına sokma 2.3882-2.9181, tık+bırakma 2.9181-3.4382.
        // (Klibi TenkaClipRebuild üretir; sınırlar oradaki rapordan alındı.)
        const float TenkaClipDur = 3.4382f, TenkaClipSpin = 1.2135f, TenkaClipSlash = 2.0382f, TenkaClipRegrip = 2.3882f, TenkaClipInsert = 2.9181f;
        float TenkaSpinReal => tenkaSpinTime * (TenkaClipSpin / 1.60f);   // eski klipte dönüş bölümü 1.60 klip-sn idi: aynı hız
        float TenkaTotal => Mathf.Max(0.1f, TenkaSpinReal + tenkaSlashSec + tenkaRegripTime + tenkaInsertTime + tenkaReleaseTime);
        // Gerçek zaman -> tenka klip fazı.
        float TenkaPhase(float t, float dur)
        {
            float s = dur / TenkaTotal;
            float b1 = TenkaSpinReal * s, b2 = b1 + tenkaSlashSec * s, b3 = b2 + tenkaRegripTime * s, b4 = b3 + tenkaInsertTime * s;
            float ct;
            if (t <= b1) ct = Mathf.Lerp(0f, TenkaClipSpin, t / Mathf.Max(1e-4f, b1));
            else if (t <= b2) ct = Mathf.Lerp(TenkaClipSpin, TenkaClipSlash, (t - b1) / Mathf.Max(1e-4f, b2 - b1));
            else if (t <= b3) ct = Mathf.Lerp(TenkaClipSlash, TenkaClipRegrip, (t - b2) / Mathf.Max(1e-4f, b3 - b2));
            else if (t <= b4) ct = Mathf.Lerp(TenkaClipRegrip, TenkaClipInsert, (t - b3) / Mathf.Max(1e-4f, b4 - b3));
            else ct = Mathf.Lerp(TenkaClipInsert, TenkaClipDur, (t - b4) / Mathf.Max(1e-4f, dur - b4));
            return Mathf.Clamp01(ct / TenkaClipDur);
        }
        [Tooltip("Rage kilic koyma: donme/silkeleme kismi sureyin bu orani kadar (hizli).")] public float chiSpinTime = 0.3f;
        [Tooltip("Rage kilic koyma: donme kismi klibin bu fazinda biter; sonrasi kinina koyma (yavas).")] public float chiSpinPhase = 0.62f;
        // Rage'de zaman -> klip fazi: ilk kisim hizli, kinina koyma yavas. Normal (tus) kilic koymada dogrusal.
        float ChiPhase(float t01)
        {
            t01 = Mathf.Clamp01(t01);
            if (chiForcedDur <= 0f) return t01;
            float a = Mathf.Clamp(chiSpinTime, 0.05f, 0.9f), ph = Mathf.Clamp(chiSpinPhase, 0.1f, 0.95f);
            return t01 < a ? Mathf.Lerp(0f, ph, t01 / a) : Mathf.Lerp(ph, 1f, (t01 - a) / (1f - a));
        }
        /// <summary>Disaridan (Rage execute) kilic koyma animasyonunu verilen surede oynatir; hareket/zemin iptal etmez.</summary>
        public void CancelSheathe() { chiburiT = -1f; chiForcedDur = -1f; }
        public void BeginSheathe(float dur) { chiForcedDur = Mathf.Max(0.1f, dur); chiburiT = 0f; anim.swordReady = true; }
        bool TenkaReady() { return useTenka && rigDrawn != null && rigDrawn.IsBuilt && playerDrawn != null && playerDrawn.HasClip("tenka"); }
        /// Tenka bitti ya da kesildi: kılıç çekik idle'ı eski hâline getir; sonuna kadar oynadıysa normal idle'ı aynı fazdan başlat (dikişsiz geçiş).
        void EndTenka()
        {
            if (drawnFadeDefault >= 0f && playerDrawn != null) { playerDrawn.fadeTime = drawnFadeDefault; drawnFadeDefault = -1f; }
            bool done = tenkaPh > 0.97f;
            tenkaOn = false;
            if (tenka != null) tenka.Stop();
            if (playerDrawn != null) { playerDrawn.clip = "idle"; playerDrawn.manual01 = -1f; }
            if (done && playerIdle != null && playerIdle.HasClip("idle"))
            {
                playerIdle.clip = "idle";
                playerIdle.Restart(Mathf.Repeat(3.0f / 3.2f, 1f));
                playerIdle.ForceUpdate();
            }
        }
        // chiburi: kılıç kına ucundan girerken bıçağın kınının ağzından ötesi görünmez (klip fazı 0.74 -> 0.93)
        static float ChiBladeU(float p) { const float t0 = 0.74f, t1 = 0.93f; if (p <= t0) return 1f; if (p >= t1) return 0f; float u = (p - t0) / (t1 - t0); return 1f - u * u * (3f - 2f * u); }
        string defAtk; bool wasOldSucc; float defParryFrom; float defAtkT, defRelT, defFrom, defPhase, blockKick; bool defRel, defWasOn;
        static float Sm01(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
        void StartDef(string c) { if (anim.HasClip(c)) { defClip = c; defStart = Time.time; } }
        void UpdateCombat()
        {
            atkClip = null;
            guardWant = false;
            if (player.isDashing || (combat != null && combat.IsAttacking)) { defAtk = null; chiburiT = -1f; guardT = -1f; }   // saldırı / dash savunma animasyonunu keser
            // ---- dash: quick low lunge, everything streams back ----
            bool dashing = player.isDashing;
            if (dashing && !wasDashing) dashStart = Time.time;
            wasDashing = dashing;
            if (useDashAnimation && dashing && playerRun != null && playerRun.HasClip("dash") && rigRun != null && rigRun.IsBuilt) { anim.ClearAction(); return; } // profil dash (mode 3) LateUpdate'te; ClearAction: saldırıdan kalan eylem dash boyunca açık kalıp bitişte idle yerine ana rig'i gösteriyordu
            if (useDashAnimation && dashing && anim.HasClip("dash")) { anim.SetAction("dash", Time.time - dashStart); return; }

            // ---- attacks: timing from PlayerCombatController.AttackProgress ----
            bool attacking = combat != null && combat.IsAttacking;
            if (attacking)
            {
                float prog = combat.AttackProgress;
                bool newSwing = !wasAttacking || prog + 0.05f < lastProgress;
                if (newSwing)
                {
                    int s = OldAttackStep();
                    if (s == 0) s = (Time.time - lastAttackEnd > 0.6f) ? 1 : (lastStep % 4) + 1;
                    attackStep = s; lastStep = s;
                }
                else { int s = OldAttackStep(); if (s != 0 && prog < 0.25f) { attackStep = s; lastStep = s; } }
                lastProgress = prog;
                wasAttacking = true;
                anim.swordReady = true; shClip = null;   // attacking always draws the katana
                anim.SetAction("attack" + attackStep, prog);
                atkClip = "attack" + attackStep; atkProg = prog;
                return;
            }
            if (wasAttacking) { wasAttacking = false; lastAttackEnd = Time.time; }

            // ---- defence: guard break > successful parry > block hit > parry press > block hold ----
            int pv = posture != null ? posture.CurrentPosture : 0;
            bool postureHit = posture != null && pv < lastPosture;
            lastPosture = pv;
            bool blocking = defense != null && defense.IsBlocking;
            bool parrying = defense != null && defense.IsParrying;
            bool parryEdge = parrying && !wasParrying; bool oldSucc = OldParryPlaying();
            if (posture != null && posture.IsBroken && !wasBroken) StartDef("guard_break");
            wasBroken = posture != null && posture.IsBroken;
            if (OldParryPlaying() && defClip != "parry_success" && defClip != "guard_break") StartDef("parry_success");
            else if (postureHit && (blocking || parrying) && defClip != "guard_break") StartDef("block_hit");
            else if (parrying && !wasParrying && defClip != "parry_success") StartDef("parry");
            wasParrying = parrying;
            // ---- v2: kare kare koruma (block / parry denemesi) ----
            if (GuardFrames() || BlockRigReady())
            {
                if (defClip != "guard_break") defClip = null;   // eski parry / block_hit aksiyonları (ana rig) koruma karesinin altından/arkasından görünmesin
                bool g = blocking || parrying;
                if (g)
                {
                    guardWant = true; chiburiT = -1f;
                    if (guardT < 0f) guardT = 0f; else guardT += Time.deltaTime;
                    if (postureHit && !(posture != null && posture.IsBroken)) { guardHitT = 0f; sqCur.x += guardHitSquash.x * squashAmount; sqCur.y += guardHitSquash.y * squashAmount; }
                    if (parryEdge) parryTryT = 0f;
                }
                else guardT = -1f;
                if (guardHitT >= 0f) { guardHitT += Time.deltaTime; if (guardHitT > guardHitTime) guardHitT = -1f; }
                if (parryTryT >= 0f) { parryTryT += Time.deltaTime; if (parryTryT > parryTryTime) parryTryT = -1f; }
            }
            else { guardT = -1f; guardHitT = -1f; parryTryT = -1f; }
            bool pSucc = oldSucc && !parrySuccPrev; parrySuccPrev = oldSucc;
            if (pSucc && frameSquash && parryPopAmount > 0f) { hitSpr = parryPopSquash * parryPopAmount; hitSprVel = Vector2.zero; hitFlow = parryPopRecoil * parryPopAmount; hitFlowVel = Vector2.zero; }
            if (frameParry && ParrySprite(0) != null)
            {
                // Sadece BASARILI parry'de oynar (tusa basmak tek basina animasyon baslatmaz).
                if (pSucc) { parryFrameT = 0f; parryFrameEnd = (ParryRevOn() ? parryRevFrameTime * 3f : parryF1Time + parryF2Time) + parryHoldAfterSuccess; }
            }
            if (!GuardFrames() && !BlockRigReady() && playerAtk != null && playerAtk.HasClip("parry") && playerAtk.HasClip("block") && defClip != "guard_break")
            {
                defClip = null;   // eski parry / block_hit klipleri bunun üstüne binmesin
                float dtd = Time.deltaTime;
                bool succEdge = oldSucc && !wasOldSucc; wasOldSucc = oldSucc;
                if (succEdge) { defParryFrom = 0f; defAtk = "parry"; defAtkT = 0f; defRel = false; }
                else if (defAtk == null && (blocking || parrying)) { defAtk = "block"; defAtkT = 0f; defRel = false; }
                else if (defAtk == "block" && postureHit && (blocking || parrying)) blockKick = 1f;
                if (defAtk != null)
                {
                    defAtkT += dtd; blockKick = Mathf.Max(0f, blockKick - dtd / 0.25f);
                    if (defAtk == "parry")
                    {
                        float T1 = Mathf.Max(0.01f, parryRaise9), T2 = parryHold5, T3 = Mathf.Max(0.05f, parryReturn5);
                        if (defAtkT < T1) { float u = defAtkT / T1; defPhase = 1f - (1f - u) * (1f - u) * (1f - u); }
                        else if (defAtkT < T1 + T2) defPhase = 1f;
                        else
                        {
                            float u = Sm01((defAtkT - T1 - T2) / T3); defPhase = 1f - u;
                            if (u >= 1f) { if (blocking || parrying) { defAtk = "block"; defAtkT = 10f; defPhase = 0.6f; defRel = false; } else defAtk = null; }
                        }
                    }
                    else
                    {
                        if (!defRel && !blocking && !parrying) { defRel = true; defRelT = 0f; defFrom = defPhase; }
                        if (!defRel) defPhase = Mathf.Min(Sm01(defAtkT / Mathf.Max(0.01f, blockRaise2)) * 0.6f, 0.6f - 0.2f * blockKick);
                        else { defRelT += dtd; defPhase = Mathf.Lerp(defFrom, 0f, Sm01(defRelT / Mathf.Max(0.01f, blockReturn2))); if (defRelT >= blockReturn2) defAtk = null; }
                    }
                }
                if (defAtk != null) { chiburiT = -1f; anim.swordReady = true; shClip = null; return; }
            }
            else defAtk = null;
            if (defClip != null || blocking || parrying) { anim.swordReady = true; shClip = null; }
            if (defClip != null)
            {
                float dt0 = Time.time - defStart;
                if (dt0 < anim.ClipDuration(defClip)) { anim.SetAction(defClip, dt0); return; }
                defClip = null;
            }
            if (blocking || parrying) { anim.SetAction("block", Time.time); return; }

            // ---- sheathe / draw on key (grounded, idle) ----
            // kılıç çekikken uzun süre giriş yoksa: otomatik tenka chiburi + nōtō
            if (autoSheathe && chiburiT < 0f && shClip == null && defClip == null && defAtk == null && anim.swordReady && player.isGrounded && !player.isDashing
                && (combat == null || !combat.IsAttacking) && Mathf.Abs(player.moveInput) < 0.1f && Time.time - lastInputT >= autoSheatheDelay && TenkaReady())
            { chiburiT = 0f; chiForcedDur = -1f; if (combat != null) combat.CancelAttack(); atkHoldT = 0f; atkRetT = 0f; atkFade = 0f; lastAtkFrames = false; }
            // kılıç çekikken koyma tuşu: chiburi (kan silkeleme) + noto (kınına koyma)
            if (chiburiT < 0f && shClip == null && MobileInput.GetKeyDown(sheatheKey) && player.isGrounded && anim.swordReady && (TenkaReady() || (playerAtk != null && playerAtk.HasClip("chiburi")))) { chiburiT = 0f; chiForcedDur = -1f; if (combat != null) combat.CancelAttack(); atkHoldT = 0f; atkRetT = 0f; atkFade = 0f; lastAtkFrames = false; }
            if (chiburiT >= 0f)
            {
                if (chiForcedDur < 0f && (Mathf.Abs(player.moveInput) >= 0.1f || !player.isGrounded)) chiburiT = -1f;
                else
                {
                    float prevChi = chiburiT;
                    chiburiT += Time.deltaTime;
                    if (prevChi <= 0f) PlaySwordSfx(true);                                   // chiburi dönüşü başladı
                    float shT = SheatheSfxTime();
                    if (prevChi < shT && chiburiT >= shT) PlaySwordSfx(false);               // kılıç kına giriyor
                    if (chiburiT >= ChiDur) { chiburiT = -1f; chiForcedDur = -1f; anim.swordReady = false; }
                    else { anim.swordReady = true; shClip = null; return; }
                }
            }
            if (shClip == null && MobileInput.GetKeyDown(sheatheKey) && player.isGrounded && anim.HasClip("sheathe") && anim.HasClip("unsheathe"))
            {
                shClip = anim.swordReady ? "sheathe" : "unsheathe"; shStart = Time.time;
                anim.swordReady = shClip == "unsheathe";
            }
            if (shClip != null)
            {
                float ts = Time.time - shStart;
                if (ts < anim.ClipDuration(shClip)) { anim.SetAction(shClip, ts); return; }
                shClip = null;
            }

            anim.ClearAction();
        }

        void LateUpdate()
        {
            frameWant = false;
            if (!aligned && hidden != null && Time.frameCount > 1)
            {
                Renderer best = null; float bs = 0.01f;
                foreach (var r in hidden)
                {
                    if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                    if (r is SpriteRenderer sr && sr.sprite == null) continue;
                    if (r.bounds.size.y > bs) { bs = r.bounds.size.y; best = r; }
                }
                if (best != null)
                {
                    mainOld = best; Align(best.bounds); aligned = true;
                    rig.SetSorting(best.sortingLayerName, best.sortingOrder + OrderBoost);
                    if (rigBL != null) rigBL.SetSorting(best.sortingLayerName, best.sortingOrder + OrderBoost);
                    if (rigBR != null && rigBR != rigBL) rigBR.SetSorting(best.sortingLayerName, best.sortingOrder + OrderBoost);
                    if (rigRun != null) rigRun.SetSorting(best.sortingLayerName, best.sortingOrder + OrderBoost);
                    if (rigIdle != null) rigIdle.SetSorting(best.sortingLayerName, best.sortingOrder + OrderBoost);
                    if (rigDrawn != null) rigDrawn.SetSorting(best.sortingLayerName, best.sortingOrder + OrderBoost);
                    if (rigJump != null) rigJump.SetSorting(best.sortingLayerName, best.sortingOrder + OrderBoost);
                    if (rigAtk != null) rigAtk.SetSorting(best.sortingLayerName, best.sortingOrder + OrderBoost);
                }
            }
            if (hideOriginal && hidden != null) foreach (var r in hidden) if (r != null && !r.forceRenderingOff) r.forceRenderingOff = true;
            if (mirrorSpriteColor && originalSprite != null && rig.IsBuilt)
            {
                rig.SetColor(originalSprite.color, true);
                if (rigBL != null && rigBL.IsBuilt) rigBL.SetColor(originalSprite.color, true);
                if (rigBR != null && rigBR != rigBL && rigBR.IsBuilt) rigBR.SetColor(originalSprite.color, true);
                if (rigRun != null && rigRun.IsBuilt) rigRun.SetColor(originalSprite.color, true);
                if (rigIdle != null && rigIdle.IsBuilt) rigIdle.SetColor(originalSprite.color, true);
                if (rigDrawn != null && rigDrawn.IsBuilt) rigDrawn.SetColor(originalSprite.color, true);
                if (rigJump != null && rigJump.IsBuilt) rigJump.SetColor(originalSprite.color, true);
                if (rigAtk != null && rigAtk.IsBuilt) rigAtk.SetColor(originalSprite.color, true);
            }
            // side-profile drawing: run (left/right rigs) and jump/air (left rig, katana hand hidden)
            if (rigBL != null && rigBL.IsBuilt && rigBR != null && rigBR.IsBuilt)
            {
                bool right = anim.faceRight;
                bool air = useProfileJump && !anim.grounded && !anim.ActionActive;
                // dash bitiminden hemen sonra (moveInput henüz gelmediyse) idle'a düşmeden koşuda kal
                if (player.isDashing) dashPrev = true;
                else if (dashPrev) { dashPrev = false; dashEndT = Time.time; }
                bool afterDash = Time.time - dashEndT < dashRunGrace && anim.grounded && player.rb != null && Mathf.Abs(player.rb.linearVelocity.x) > 0.5f;
                bool run = useProfileRun && (anim.running || afterDash) && anim.grounded && !anim.ActionActive;
                bool dashP = useDashAnimation && player.isDashing && playerRun != null && playerRun.HasClip("dash") && rigRun != null && rigRun.IsBuilt;
                // koşu bitince kısa frenleme (stop klibi): idle'a doğrudan düşmez
                float stopSpd = player.rb != null ? Mathf.Abs(player.rb.linearVelocity.x) : 0f;
                bool canStop = stopAnimEnabled && playerRun != null && playerRun.HasClip("stop") && rigRun != null && rigRun.IsBuilt;
                if (run) { stopLastSpeed = Mathf.Max(stopSpd, stopLastSpeed * 0.9f); stopT = -1f; }
                else if (canStop && stopT < 0f && bMode == 1 && !air && !dashP && anim.grounded && !anim.ActionActive && atkClip == null && stopLastSpeed >= stopMinSpeed) stopT = 0f;
                if (stopT >= 0f)
                {
                    if (!canStop || air || dashP || anim.ActionActive || atkClip != null || Mathf.Abs(player.moveInput) > 0.1f) stopT = -1f;
                    else { stopT += Time.deltaTime; if (stopT >= stopDuration) { stopT = -1f; stopLastSpeed = 0f; } }
                }
                int mode = dashP ? 3 : air ? 2 : run ? 1 : stopT >= 0f ? 4 : 0;
                if (dashDustEnabled && rigRun != null && rigRun.IsBuilt)
                {
                    if (dashDust == null) { dashDust = GetComponent<RonniDashDust>(); if (dashDust == null) dashDust = gameObject.AddComponent<RonniDashDust>(); }
                    dashDust.Init(player, rigRun);
                    bool dd = player.isDashing && (dashP || !useDashAnimation);
                    float dd01 = (Time.time - dashStart) / Mathf.Max(0.0001f, player.dashTime * DashState.DurationScale);
                    float ddir = player.GetDashDirection();
                    if (Mathf.Abs(ddir) < 0.01f) ddir = player.facingDir;
                    if (dd && !dustPrev) dashDust.Begin(ddir);
                    dashDust.Tick(dd && dd01 < dashTrailEndFrac, ddir);
                    dustPrev = dd;
                }
                if (mode != bMode || (mode != 0 && right != bRight))
                {
                    bool toIdleFromStop = false;   // çapraz geçiş kapalı: iki çizim üst üste binip hayalet yapıyordu
                    popAmt = (bMode == 4 || mode == 4) ? stopPopAmount : 1f;
                    if (rigB != null && !toIdleFromStop) rigB.SetShown(false);
                    if (toIdleFromStop) stopFadeT = 0f;
                    else if (stopFadeT >= 0f) { stopFadeT = -1f; if (rigRun != null) { rigRun.SetShown(mode == 1 || mode == 3 || mode == 4 || (mode == 2 && !standJump)); } }
                    bool newJump = mode == 2 && bMode != 2;
                    if (newJump)
                    {
                        float vx0 = player.rb != null ? Mathf.Abs(player.rb.linearVelocity.x) : 0f;
                        standJump = rigJump != null && rigJump.IsBuilt && playerJump != null && playerJump.HasClip("air")
                                    && vx0 < standJumpMaxSpeed && Mathf.Abs(player.moveInput) < 0.1f;
                    }
                    bMode = mode; bRight = right;
                    rigB = right ? rigBR : rigBL; playerB = right ? playerBR : playerBL;
                    if ((mode == 1 || mode == 3 || mode == 4 || (mode == 2 && playerRun != null && playerRun.HasClip("air_a"))) && rigRun != null && rigRun.IsBuilt && playerRun != null) { rigB = rigRun; playerB = playerRun; }
                    if (mode == 2 && standJump) { rigB = rigJump; playerB = playerJump; }
                    if (mode != 0)
                    {
                        if (newJump) jumpCount++;
                        playerB.clip = mode == 3 ? "dash" : mode == 4 ? "stop" : mode == 2 ? (standJump ? "air" : ((jumpCount % 2 == 1) ? "air_a" : "air_b")) : "run";
                        playerB.manual01 = -1f;
                        playerB.Restart(0f); playerB.ForceUpdate(); rigB.SetShown(true);
                    }
                    rig.SetShown(mode == 0);
                }
                if (playerB != null)
                {
                    playerB.speed = anim.runSpeed;
                    if (mode == 2)
                    {
                        float vy = player.rb != null ? player.rb.linearVelocity.y : 0f;
                        float target = Mathf.Clamp01(0.5f - vy / (2f * airVelocityRange));
                        airU = airU < 0f ? target : Mathf.MoveTowards(airU, target, Time.deltaTime * 4f);
                        playerB.manual01 = airU;
                    }
                    else if (mode == 3)
                    {
                        airU = -1f;
                        playerB.manual01 = Mathf.Clamp01((Time.time - dashStart) / Mathf.Max(0.0001f, player.dashTime * DashState.DurationScale));
                    }
                    else if (mode == 4)
                    {
                        airU = -1f;
                        playerB.manual01 = Mathf.Clamp01(stopT / Mathf.Max(0.0001f, stopDuration));
                    }
                    else airU = -1f;
                }
                // dururken (aksiyon / kılıç çekme yok) ön görünüm yerine nefes alan idle çizimi görünür
                if (atkHoldIdle && (atkClip != null || mode != 0 || !anim.ActionActive)) atkHoldIdle = false;
                drawnWant = false;
                if (hatSuppress > 0f) hatSuppress -= Time.deltaTime;   // yeni rig'de oynayan aksiyon bitince eski şapkalı rig'in solma karesi görünmesin
                bool idleNow = mode == 0 && rigIdle != null && rigIdle.IsBuilt && (!anim.ActionActive || atkHoldIdle || hatSuppress > 0f) && shClip == null;
                if (rigIdle != null && rigIdle.IsBuilt) { rigIdle.SetShown(idleNow); rig.SetShown(mode == 0 && !idleNow); }
                if (rigAtk != null && rigAtk.IsBuilt)
                {
                    // ---- rig attack1 (prototip): durum
                    bool rigA1Want = mode == 0 && atkClip == "attack1" && RigAtk1Ready();
                    if (atkClip == null) rigAtkBlock = false;
                    if (atkClip != null && atkClip != "attack1") rigAtkT = -1f;
                    if (rigA1Want && !rigAtkBlock && (rigAtkT < 0f || atkProg + 0.05f < rigAtkLastProg)) rigAtkT = 0f;
                    if (atkClip != null) rigAtkLastProg = atkProg;
                    bool rigAtkNow = false;
                    if (rigAtkT >= 0f)
                    {
                        bool rigInt = mode != 0 || player.isDashing || guardWant || defAtk != null || chiburiT >= 0f || parryFrameT >= 0f || (atkClip == null && Mathf.Abs(player.moveInput) >= 0.1f) || !RigAtk1Ready();
                        if (rigInt) { rigAtkT = -1f; rigAtkBlock = atkClip != null; }
                        else { rigAtkT += Time.deltaTime; if (rigAtkT >= RigAtk1Dur) rigAtkT = -1f; else rigAtkNow = true; }
                    }
                    // ---- eski şapkalı RonniSkin rig'inde kalan iki aksiyon (kılıç çekme, denge kırılması) yeni rig'de oynasın
                    string rigActClip = null; float rigActPh = 0f;
                    if (!rigAtkNow && mode == 0 && rigDrawn != null && rigDrawn.IsBuilt && playerDrawn != null)
                    {
                        if (defClip == "guard_break" && playerDrawn.HasClip("guard_break")) { rigActClip = "guard_break"; rigActPh = Mathf.Clamp01((Time.time - defStart) / 0.45f); }
                        else if (shClip == "unsheathe" && playerDrawn.HasClip("unsheathe")) { rigActClip = "unsheathe"; rigActPh = Mathf.Clamp01((Time.time - shStart) / 0.5f); }
                    }
                    bool rigActNow = rigActClip != null;
                    bool frameClip = !rigAtkNow && (atkClip == "attack1" && frameAttack1) || (atkClip == "attack2" && frameAttack2) || (atkClip == "attack3" && frameAttack3) || (atkClip == "attack4" && frameAttack4);
                    // Kare kare saldırıda Animator'ın ilk sprite'ı henüz gelmediyse eski (rig) saldırı pozunu bir an bile gösterme.
                    bool atkOn = !rigAtkNow && mode == 0 && playerAtk != null && (frameClip ? FrameAtkActive() : ((atkClip == "attack1" || atkClip == "attack2" || atkClip == "attack3") && playerAtk.HasClip(atkClip)));
                    string dClip = defAtk != null ? defAtk : (chiburiT >= 0f ? "chiburi" : null);
                    float dPh = defAtk != null ? defPhase : (TenkaReady() ? TenkaPhase(chiburiT, ChiDur) : ChiPhase(chiburiT / Mathf.Max(0.1f, ChiDur)));
                    bool tenkaNow = mode == 0 && dClip == "chiburi" && defAtk == null && atkClip == null && TenkaReady();
                    bool dOn = !tenkaNow && mode == 0 && dClip != null && atkClip == null && playerAtk != null && playerAtk.HasClip(dClip);
                    // Kombo geçişi: yeni saldırının ilk sprite'ı henüz gelmediyse (kare kare) eski rig/idle pozu yerine son kareyi tut.
                    bool framePend = !rigAtkNow && mode == 0 && frameClip && !atkOn && frameLast != null && frameSR != null && frameSR.enabled && !frameFading;
                    Color bcc = originalSprite != null ? originalSprite.color : Color.white;
                    if (bladeCut && !(dOn && dClip == "chiburi")) { rigAtk.SetPartWindow("atk_blade", 0f, 1f, bcc, bcc); bladeCut = false; }
                    bool pfOn = false;
                    if (!guardWant && blockOn && blockRelT == -1f) blockRelT = 0f;
                    if (parryFrameT >= 0f)
                    {
                        if (mode != 0 || atkClip != null || player.isDashing || !frameParry) parryFrameT = -1f;
                        else
                        {
                            parryFrameT += Time.deltaTime;
                            if (parryFrameT >= parryFrameEnd) parryFrameT = -1f;
                            else pfOn = true;
                        }
                    }
                    if (pfOn)
                    {
                        rig.SetShown(false); if (rigIdle != null) rigIdle.SetShown(false); rigAtk.SetShown(false); idleNow = false;
                        atkWasOn = false; defWasOn = false;
                        int pi = ParryIndex(parryFrameT);
                        if (GuardFrames() && frameSquash && parrySquash != null && parrySquash.Length >= 3)
                        {
                            Vector2 pt = parrySquash[Mathf.Clamp(pi, 0, 2)];
                            if (pi >= 2) { float hold = Mathf.Max(0.01f, parryFrameEnd - parryF1Time - parryF2Time); float hu = Mathf.Clamp01((parryFrameT - parryF1Time - parryF2Time) / hold); pt = Vector2.Lerp(pt, Vector2.one, hu * hu * (3f - 2f * hu)); }
                            guardSq = Vector2.one + (pt - Vector2.one) * squashAmount; guardSqOn = true;
                        }
                        ShowFrame(ParrySprite(pi));
                        guardSqOn = false;
                    }
                    else if (BlockRigReady() && mode == 0 && atkClip == null && !player.isDashing && (guardWant || (blockOn && blockRelT >= 0f)))
                    {
                        // BLOCK: kılıç çekik idle rig'i, iki elle kılıç başın üstünde (Spine'da yapılan 'block' / 'block_hit')
                        rig.SetShown(false); if (rigIdle != null) rigIdle.SetShown(false); rigAtk.SetShown(false); idleNow = false;
                        atkWasOn = false; defWasOn = false; atkHoldT = 0f; atkRetT = 0f; atkFade = 0f; lastAtkFrames = false; idleDrawnOn = false;
                        if (!blockOn) { rigDrawn.SetColor(bcc, true); blockOn = true; blockPh = 0f; anim.swordReady = true; }
                        drawnWant = true; blockFrame = true;
                        string bcl = "block"; float bph;
                        if (guardWant)
                        {
                            blockRelT = -1f;
                            float gt = Mathf.Max(0f, guardT) * blockSpeed;
                            float hold = Mathf.Max(0.05f, blockClipDur - blockGuardT);
                            if (guardHitT >= 0f && playerDrawn.HasClip("block_hit") && gt >= blockGuardT)
                            { bcl = "block_hit"; bph = Mathf.Clamp01(guardHitT / Mathf.Max(0.01f, blockHitDur)); blockPh = blockGuardT; }
                            else { blockPh = gt < blockClipDur ? gt : blockGuardT + Mathf.Repeat(gt - blockGuardT, hold); bph = blockPh / blockClipDur; }
                        }
                        else
                        {
                            // bırakınca: kılıcı aynı yoldan geri indir
                            blockRelT += Time.deltaTime;
                            blockPh = Mathf.Max(0f, Mathf.Min(blockPh, blockGuardT) - Time.deltaTime * blockReleaseSpeed);
                            bph = blockPh / blockClipDur;
                            if (blockPh <= 0f) blockRelT = -2f;
                        }
                        playerDrawn.clip = bcl; playerDrawn.manual01 = bph;
                        float lt = bcl == "block" ? blockPh : blockClipDur;
                        BlockLayers(lt >= blockSwapT, lt >= blockFrontT);
                    }
                    else if (guardWant && mode == 0 && atkClip == null && !player.isDashing && GuardFrames())
                    {
                        // KORUMA: el çizimi koruma karesi (ParryHD_1) + hazırlık sıkışması + nefes + vuruş tepkisi + parry denemesi esnemesi
                        rig.SetShown(false); if (rigIdle != null) rigIdle.SetShown(false); rigAtk.SetShown(false); idleNow = false;
                        atkWasOn = false; defWasOn = false; atkHoldT = 0f; atkRetT = 0f; atkFade = 0f; lastAtkFrames = false;
                        float gt = Mathf.Max(0f, guardT);
                        float ru = Sm01(gt / Mathf.Max(0.01f, guardRaiseTime));
                        Vector2 sq = Vector2.Lerp(guardRaiseSquash, Vector2.one, ru);
                        float br = guardBreath * Mathf.Sin(gt * Mathf.PI * 2f / Mathf.Max(0.2f, guardBreathPeriod)) * ru;
                        sq += new Vector2(-br * 0.6f, br);
                        if (parryTryT >= 0f) { float pu = 1f - Mathf.Clamp01(parryTryT / Mathf.Max(0.01f, parryTryTime)); sq += parryTryStretch * pu * pu; }
                        if (guardHitT >= 0f)
                        {
                            float hu = Mathf.Clamp01(guardHitT / Mathf.Max(0.01f, guardHitTime));
                            // hızlı geri itilme, yaylanarak yerine oturma
                            float push = Mathf.Exp(-5f * hu) * Mathf.Cos(hu * Mathf.PI * 1.5f) * (1f - hu);
                            guardOffX = -guardHitPush * push + Mathf.Sin(guardHitT * 95f) * guardHitShake * (1f - hu) * (1f - hu);
                            sq += new Vector2(guardHitSquash.x, guardHitSquash.y) * (1f - hu) * (1f - hu) * (1f - hu);
                        }
                        guardSq = frameSquash ? Vector2.one + (sq - Vector2.one) * squashAmount : Vector2.one; guardSqOn = true;
                        ShowFrame(ParrySprite(0));
                        guardSqOn = false;
                    }
                    else if (framePend)
                    {
                        rig.SetShown(false); if (rigIdle != null) rigIdle.SetShown(false); rigAtk.SetShown(false); idleNow = false;
                        atkHoldIdle = true; atkFade = 0f; atkHoldT = 0f; atkRetT = 0f; lastAtkFrames = true;
                        ShowFrame(frameLast);
                    }
                    else if (tenkaNow)
                    {
                        // tenka chiburi + sakate nōtō: kılıç çekik idle çizimi üzerinde (aynı pozdan başlar, normal idle pozunda biter)
                        rig.SetShown(false); if (rigIdle != null) rigIdle.SetShown(false); rigAtk.SetShown(false); idleNow = false;
                        if (!tenkaOn) { rigDrawn.SetColor(bcc, true); tenkaOn = true; }
                        drawnWant = true; tenkaFrame = true; idleDrawnOn = false; atkWasOn = false; defWasOn = false;
                        atkHoldT = 0f; atkRetT = 0f; atkFade = 0f; lastAtkFrames = false;   // chiburi bitince eski saldırının tutma/dönüş karesi tekrar oynamasın
                        if (frameSR != null && frameSR.enabled) { frameFading = false; frameSR.enabled = false; if (glowSR != null) glowSR.enabled = false; }
                        playerDrawn.clip = "tenka"; playerDrawn.manual01 = dPh; tenkaPh = dPh;
                        if (tenka != null) { tenka.quiet = false; tenka.Drive(rigDrawn); }
                    }
                    else if (rigAtkNow || rigActNow)
                    {
                        // rig attack1 (prototip): kılıç çekik rig'inde 'attack1' klibi (tenka ile aynı düzen: bitince EndTenka idle'a döndürür)
                        rig.SetShown(false); if (rigIdle != null) rigIdle.SetShown(false); rigAtk.SetShown(false); idleNow = false;
                        if (!tenkaOn) { rigDrawn.SetColor(bcc, true); tenkaOn = true; }
                        drawnWant = true; tenkaFrame = true; idleDrawnOn = false; atkWasOn = false; defWasOn = false; atkHoldIdle = true;
                        if (rigActNow) hatSuppress = 0.25f;
                        if (drawnFadeDefault < 0f) drawnFadeDefault = playerDrawn.fadeTime;
                        playerDrawn.fadeTime = 0.02f;   // hazırlık karesi eski duruşla bulanmasın
                        playerDrawn.clip = rigAtkNow ? "attack1" : rigActClip; playerDrawn.manual01 = rigAtkNow ? Mathf.Clamp01(rigAtkT / RigAtk1Dur) : rigActPh; tenkaPh = 0f;
                        if (tenka != null) { tenka.quiet = true; tenka.Drive(rigDrawn); }
                    }
                    else if (dOn)
                    {
                        rig.SetShown(false); if (rigIdle != null) rigIdle.SetShown(false); idleNow = false;
                        if (!defWasOn) rigAtk.SetColor(originalSprite != null ? originalSprite.color : Color.white, true);
                        defWasOn = true; atkHoldIdle = true; atkFade = 0f; atkHoldT = 0f; atkRetT = 0f; atkWasOn = false;
                        rigAtk.SetShown(true); playerAtk.manual01 = dPh;
                        playerAtk.clip = dClip; rigAtk.SetPartWindow("atk_smear", 0f, 0f, Color.clear, Color.clear);
                        if (dClip == "chiburi") { float ub = ChiBladeU(dPh); if (ub < 0.999f) { rigAtk.SetPartWindow("atk_blade", 0f, ub, bcc, bcc); bladeCut = true; } else if (bladeCut) { rigAtk.SetPartWindow("atk_blade", 0f, 1f, bcc, bcc); bladeCut = false; } }
                    }
                    else if (atkOn)
                    {
                        defWasOn = false;
                        rig.SetShown(false); if (rigIdle != null) rigIdle.SetShown(false); idleNow = false;
                        if (!atkWasOn) rigAtk.SetColor(originalSprite != null ? originalSprite.color : Color.white, true); atkWasOn = true; atkHoldIdle = true; atkFade = 0f; atkHoldT = 0f; atkRetT = 0f; rigAtk.SetShown(true); lastAtkClip = atkClip; if (playerAtk.HasClip(atkClip)) playerAtk.clip = atkClip; playerAtk.manual01 = AtkRemap(Mathf.Clamp01(atkProg)); if (atkClip == "attack3" || atkClip == "attack4") rigAtk.SetPartWindow("atk_smear", 0f, 0f, Color.clear, Color.clear); else SmearWin(playerAtk.manual01);
                        if (FrameAtkActive()) { rigAtk.SetShown(false); ShowFrame(frameLast); lastAtkFrames = true; } else lastAtkFrames = false;
                    }
                    else
                    {
                        Color bc = originalSprite != null ? originalSprite.color : Color.white;
                        if (atkWasOn) { atkWasOn = false; atkHoldIdle = true; atkHoldT = lastAtkClip == "attack2" && !lastAtkFrames ? atkHoldTime5 : atkHoldTime3; atkRetT = 0f; atkFade = 0f; }
                        bool atkInterrupt = mode != 0 || atkClip != null || Mathf.Abs(player.moveInput) >= 0.1f;
                        if (atkInterrupt && (atkHoldT > 0f || atkRetT > 0f)) { atkHoldT = 0f; atkRetT = 0f; atkFade = atkCrossfade; }
                        if (atkHoldT > 0f || atkRetT > 0f)
                        {
                            float ph;
                            if (atkHoldT > 0f) { atkHoldT -= Time.deltaTime; float hu = 1f - Mathf.Clamp01(atkHoldT / Mathf.Max(0.01f, lastAtkClip == "attack2" ? atkHoldTime5 : atkHoldTime3)); if (lastAtkClip != "attack2") hu = hu * hu * (3f - 2f * hu); else hu = hu * hu; ph = Mathf.Lerp(lastAtkClip == "attack2" ? atkSettle2 : atkSettlePhase, lastAtkClip == "attack2" ? atkHold5x : atkHoldPhase, hu); if (atkHoldT <= 0f) atkRetT = (lastAtkClip == "attack2" ? atkReturnTime5 : atkReturnTime3) + (lastAtkClip == "attack2" ? atkEndPause2 : 0f); }
                            else { atkRetT -= Time.deltaTime; float pz = lastAtkClip == "attack2" ? atkEndPause2 : 0f; float rt = lastAtkClip == "attack2" ? atkReturnTime5 : atkReturnTime3; float u = Mathf.Clamp01((rt - atkRetT) / Mathf.Max(0.01f, rt)); u = u * u * (3f - 2f * u); ph = Mathf.Lerp(lastAtkClip == "attack2" ? atkHold5x : atkHoldPhase, 1f, u); }
                            rig.SetShown(false); if (rigIdle != null) rigIdle.SetShown(false); idleNow = false;
                            rigAtk.SetShown(true); if (playerAtk.HasClip(lastAtkClip)) playerAtk.clip = lastAtkClip; playerAtk.manual01 = ph; if (lastAtkClip == "attack2" || lastAtkClip == "attack3" || lastAtkClip == "attack4") rigAtk.SetPartWindow("atk_smear", 0f, 0f, Color.clear, Color.clear); else SmearWin(ph);
                            if (lastAtkFrames && frameLast != null) { Sprite cs = originalSprite != null ? originalSprite.sprite : null; if (cs != null && frameLast != null && cs.name.StartsWith(frameLast.name.Substring(0, 9))) frameLast = cs; rigAtk.SetShown(false); ShowFrame(frameLast); if (atkHoldT <= 0f) { atkRetT = 0f; lastAtkFrames = false; } }   // kare kare: son karede bekle, sonra direkt idle
                            atkHoldIdle = true;
                        }
                        else
                        if (atkFade > 0f && mode == 0 && atkCrossfade > 0f && !lastAtkFrames)
                        {
                            // eski görünüm opak kalır, yeni görünüm üstüne yavaşça belirir (yarı saydam üst üste binme "yok olma" yapıyordu)
                            atkFade -= Time.deltaTime;
                            float a = Mathf.Clamp01(atkFade / atkCrossfade); a = a * a * (3f - 2f * a);
                            RonniSkinRig other = idleNow ? rigIdle : rig;
                            if (fadeTop != other) { RestoreFadeTop(); fadeTop = other; fadeTopBase = other.sortingOrderOffset; other.SetSorting(other.sortingLayerName, fadeTopBase + 600); }
                            rigAtk.SetShown(true); rigAtk.SetColor(bc, true);
                            other.SetColor(new Color(bc.r, bc.g, bc.b, bc.a * (1f - a)), true);
                            if (atkFade <= 0f) { RestoreFadeTop(); other.SetColor(bc, true); rigAtk.SetShown(false); }
                        }
                        else
                        {
                            if (atkFade > 0f) { atkFade = 0f; }
                            RestoreFadeTop();
                            // kılıç çekikken (R ile / saldırı sonrası) dururken nefes alan "kılıç çekik idle"
                            bool useNewDrawn = rigDrawn != null && rigDrawn.IsBuilt;
                            bool dIdle = mode == 0 && idleNow && anim.swordReady && (useNewDrawn || (playerAtk != null && playerAtk.HasClip("idle_drawn")));
                            if (dIdle)
                            {
                                if (rigIdle != null) rigIdle.SetShown(false); rig.SetShown(false); idleNow = false;
                                bool firstDrawn = !idleDrawnOn;
                                if (firstDrawn) { if (useNewDrawn) rigDrawn.SetColor(bc, true); else rigAtk.SetColor(bc, true); }
                                idleDrawnOn = true;
                                if (useNewDrawn) { drawnWant = true; rigAtk.SetShown(false); } else { rigAtk.SetShown(true); playerAtk.clip = "idle_drawn"; }
                                if (!useNewDrawn) playerAtk.manual01 = (Time.time % idleDrawnPeriod2) / idleDrawnPeriod2;
                                if (firstDrawn) { if (useNewDrawn) { playerDrawn.clip = "idle"; playerDrawn.Restart(0f); playerDrawn.ForceUpdate(); } else playerAtk.ForceUpdate(); }
                                rigAtk.SetPartWindow("atk_smear", 0f, 0f, Color.clear, Color.clear);
                            }
                            else { idleDrawnOn = false; rigAtk.SetShown(false); (idleNow ? rigIdle : rig).SetColor(bc, true); }
                        }
                    }
                }
                // stop -> idle çapraz geçiş: eski (yan) çizim solar, idle belirir
                if (stopFadeT >= 0f)
                {
                    stopFadeT += Time.deltaTime;
                    float fu = Mathf.Clamp01(stopFadeT / Mathf.Max(0.001f, stopFade)); fu = fu * fu * (3f - 2f * fu);
                    Color fc = originalSprite != null ? originalSprite.color : Color.white;
                    if (fu >= 1f || mode != 0 || !idleNow)
                    {
                        stopFadeT = -1f;
                        if (rigRun != null && rigRun.IsBuilt) { rigRun.SetColor(fc, true); if (mode != 1 && mode != 3 && mode != 4 && !(mode == 2 && !standJump)) rigRun.SetShown(false); }
                        if (rigIdle != null && rigIdle.IsBuilt) rigIdle.SetColor(fc, true);
                    }
                    else
                    {
                        // Eski (yan) çizim opak kalır, idle üstüne yavaşça gelir: yarı saydam hayalet oluşmaz.
                        Color fi = fc; fi.a *= fu;
                        rigRun.SetShown(true); rigRun.SetColor(fc, true);
                        rigIdle.SetColor(fi, true);
                    }
                }
                if (rigDrawn != null && rigDrawn.IsBuilt) rigDrawn.SetShown(drawnWant);
                if (tenkaOn && !tenkaFrame) EndTenka();
                if (blockOn && (!blockFrame || blockRelT == -2f)) EndBlock();
                blockFrame = false;
                tenkaFrame = false;
                UpdateWait(idleNow);
                int vid = mode == 0 ? 0 : (right ? 2 : 1);
                if (vid != visualId) { if (visualId >= 0) popT = 0f; visualId = vid; }
            }
            if (!frameWant && frameSR != null && frameSR.enabled && (bMode != 0 || player.isDashing))
            {
                // Saldırı dash / zıplama / koşu ile KESİLDİYSE: son saldırı karesi o hareketin üstünde solmasın
                // (yanlış poz bir an görünüyordu); anında gizle. Solma yalnızca durağan idle'a dönüşte kalır.
                frameFading = false; frameSR.enabled = false; if (glowSR != null) glowSR.enabled = false;
            }
            else if (!frameWant && frameSR != null && frameSR.enabled)
            {
                // Çapraz geçiş: altta kılıçlı idle (rig) zaten görünüyor; son kare üstte yavaşça söner.
                if (!frameFading) { frameFading = frameFadeOut > 0.001f; frameFadeT = frameFadeOut; sqWas = false; }
                if (frameFading)
                {
                    frameFadeT -= Time.deltaTime;
                    float a = Mathf.Clamp01(frameFadeT / frameFadeOut); a = a * a * (3f - 2f * a);
                    Color c = mirrorSpriteColor && originalSprite != null ? originalSprite.color : Color.white;
                    c.a *= a; frameSR.color = c;
                    if (glowSR != null && glowSR.enabled) glowSR.color = new Color(1f, 1f, 1f, c.a);
                    if (frameFadeSettle)
                    {
                        Vector3 sc = frameSR.transform.localScale;
                        float k = 1f - Mathf.Exp(-squashSpeed * 0.5f * Time.deltaTime);
                        Vector2 nsq = Vector2.Lerp(sqCur, Vector2.one, k);
                        if (Mathf.Abs(sqCur.x) > 1e-4f && Mathf.Abs(sqCur.y) > 1e-4f) frameSR.transform.localScale = new Vector3(sc.x / sqCur.x * nsq.x, sc.y / sqCur.y * nsq.y, sc.z);
                        sqCur = nsq;
                    }
                    if (frameFadeT <= 0f) { frameFading = false; frameSR.enabled = false; if (glowSR != null) glowSR.enabled = false; }
                }
                else { frameSR.enabled = false; if (glowSR != null) glowSR.enabled = false; }
            }
            UpdateDashFrame();
            ApplyTransitionScale();
        }

        // ---- DASH KARESI: dash boyunca tek el cizimi (gerilmis poz, el katanada) + squash/stretch ----
        [Header("Dash karesi")]
        [Tooltip("Dash'te rig yerine Resources/DashFrames/DashHD_1 karesini goster (gerilme -> firlama squash/stretch ile).")] public bool frameDash = true;
        [Tooltip("Gerilme (squash) suresi: dash'in basinda kare ezilir.")] public float dashCoilTime = 0.09f;
        [Tooltip("Gerilme olcegi (x, y).")] public Vector2 dashCoilScale = new Vector2(1.10f, 0.86f);
        [Tooltip("Firlama (stretch) olcegi (x, y): ileri uzar, ince kalir.")] public Vector2 dashStretchScale = new Vector2(1.38f, 0.82f);
        [Tooltip("Dash sonunda son sikisma olcegi (x, y).")] public Vector2 dashEndScale = new Vector2(0.92f, 1.08f);
        Sprite dashSprite; bool dashSpriteLoaded, dashFrameOn; float dashFrameT0;
        Vector2 DashSquashTarget()
        {
            float total = Mathf.Max(0.05f, player.dashTime * DashState.DurationScale);
            float t = Time.time - dashFrameT0;
            if (t < dashCoilTime) return dashCoilScale;                                  // gerilme
            float p = Mathf.Clamp01((t - dashCoilTime) / Mathf.Max(0.05f, total - dashCoilTime));
            if (p > 0.8f) return dashEndScale;                                           // son oturma
            float relax = Mathf.SmoothStep(0f, 1f, p / 0.8f);                            // firlama -> gevseme
            return Vector2.Lerp(dashStretchScale, Vector2.one, relax * 0.85f);
        }
        void UpdateDashFrame()
        {
            bool want = frameDash && !useDashAnimation && player != null && player.isDashing;
            if (want && !dashSpriteLoaded) { dashSpriteLoaded = true; dashSprite = Resources.Load<Sprite>("DashFrames/DashHD_1"); }
            if (want && dashSprite == null) want = false;
            if (want)
            {
                if (!dashFrameOn) { dashFrameOn = true; dashFrameT0 = Time.time; sqWas = false; }
                if (rig != null) rig.SetShown(false);
                if (rigIdle != null) rigIdle.SetShown(false);
                if (rigAtk != null) rigAtk.SetShown(false);
                if (rigDrawn != null) rigDrawn.SetShown(false);
                if (rigRun != null) rigRun.SetShown(false);
                if (rigJump != null) rigJump.SetShown(false);
                if (rigBL != null) rigBL.SetShown(false);
                if (rigBR != null) rigBR.SetShown(false);
                ShowFrame(dashSprite);
            }
            else if (dashFrameOn)
            {
                dashFrameOn = false; sqWas = false;
                if (frameSR != null) { frameFading = false; frameWant = false; frameSR.enabled = false; }
                if (glowSR != null) glowSR.enabled = false;
                bMode = -99;   // rig secimini yeniden yaptir
            }
        }

        // Idle'da hareketsiz kalınca "wait" klibi (el uzat, ateş böceği konsun) arada bir oynar; oyuncu kıpırdarsa kesilir.
        void UpdateWait(bool idleNow)
        {
            if (rigIdle == null || playerIdle == null || !playerIdle.HasClip("wait")) return;
            if (!idleNow)
            {
                if (waiting) EndWait(true);
                idleSince = -1f;
                return;
            }
            if (idleSince < 0f) { idleSince = Time.time; waitAfter = Random.Range(waitDelay.x, waitDelay.y); }
            if (waiting) { if (playerIdle.ClipFinished) EndWait(false); return; }
            if (!fireflyWait || Time.time - idleSince < waitAfter) return;
            if (playerIdle.Phase01 > 0.03f) return;   // idle döngüsünün başında geç: dikişsiz
            Transform perch;
            if (!rigIdle.bones.TryGetValue("perch", out perch)) return;
            playerIdle.clip = "wait"; playerIdle.Restart(0f); waiting = true;
            if (firefly == null) { var go = new GameObject("Ronni_Firefly"); firefly = go.AddComponent<RonniFirefly>(); }
            float outward = Mathf.Sign(perch.position.x - rigIdle.transform.position.x); if (outward == 0f) outward = -1f;
            firefly.Begin(rigIdle, perch, outward);
        }

        void EndWait(bool abort)
        {
            waiting = false; idleSince = -1f;
            if (playerIdle != null) { playerIdle.clip = "idle"; playerIdle.Restart(0f); }
            if (abort && firefly != null) firefly.Cancel();
        }

        void ApplyTransitionScale()
        {
            if (partsA == null && rig != null && rig.IsBuilt) partsA = rig.transform.Find("Parts");
            bool g = anim.grounded;
            if (g && !wasGrounded) landT = 0f;
            wasGrounded = g;
            float dt = Time.deltaTime;
            popT += switchPopTime > 0f ? dt / switchPopTime : 1f;
            landT += landTime > 0f ? dt / landTime : 1f;
            float sx = 1f, sy = 1f;
            if (popT < 1f) { float e = Mathf.SmoothStep(0f, 1f, popT); sx *= Mathf.Lerp(Mathf.Lerp(1f, 0.72f, popAmt), 1f, e); sy *= Mathf.Lerp(Mathf.Lerp(1f, 1.04f, popAmt), 1f, e); }
            if (landT < 1f) { float s = Mathf.Sin(Mathf.PI * landT) * (1f - 0.3f * landT); sy *= 1f - landSquash * s; sx *= 1f + landSquash * 0.65f * s; }
            var sc = new Vector3(sx, sy, 1f);
            if (partsA != null) partsA.localScale = visualId <= 0 ? sc : Vector3.one;
            if (rigBL != null) rigBL.transform.localScale = visualId == 1 ? Vector3.Scale(baseBL, sc) : baseBL;
            if (rigBR != null && rigBR != rigBL) rigBR.transform.localScale = visualId == 2 ? Vector3.Scale(baseBR, sc) : baseBR;
            if (rigRun != null) rigRun.transform.localScale = (visualId > 0 && rigB == rigRun) ? Vector3.Scale(baseRun, sc) : baseRun;
            if (rigIdle != null) rigIdle.transform.localScale = visualId <= 0 ? Vector3.Scale(baseIdle, sc) : baseIdle;
            if (rigAtk != null) rigAtk.transform.localScale = visualId <= 0 ? Vector3.Scale(baseAtk, sc) : baseAtk;
            if (rigDrawn != null) rigDrawn.transform.localScale = visualId <= 0 ? Vector3.Scale(baseDrawn, sc) : baseDrawn;
            if (rigJump != null) rigJump.transform.localScale = (visualId > 0 && rigB == rigJump) ? Vector3.Scale(baseJump, sc) : baseJump;
        }

        void OnDisable()
        {
            CombatImpactFeedback.AttackImpact -= OnAttackImpact;
            if (firefly != null) firefly.Cancel();
            if (hidden != null) foreach (var r in hidden) if (r != null) r.forceRenderingOff = false;
        }
    }
}
