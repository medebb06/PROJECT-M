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
        [Tooltip("Key that sheathes / draws the katana while idle.")] public KeyCode sheatheKey = KeyCode.R;
        [Tooltip("Start the level with the katana drawn (held in the back hand).")] public bool startDrawn = true;
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
        [Tooltip("Smear karesi bitince smear sis gibi genişleyip soluklaşarak dağılsın (sonraki saldırıdan bağımsız).")] public bool smearDissolve = true;
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
        // [saldırı, kare] = (x, y) ölçek. Kare 1 hazırlık (bas), 2 vuruş (vuruş yönünde uza), 3 devam, 4 otur.
        static readonly Vector2[,] SquashTable =
        {
            { new Vector2(1.06f, 0.94f), new Vector2(1.12f, 0.92f), new Vector2(0.95f, 1.07f), Vector2.one },   // Attack1: yatay kesim -> yukarı yay
            { new Vector2(1.04f, 0.96f), new Vector2(1.14f, 0.91f), new Vector2(0.95f, 1.07f), Vector2.one },   // Attack2: öne uzanma -> yukarı yay
            { new Vector2(1.08f, 0.92f), new Vector2(1.18f, 0.90f), new Vector2(1.07f, 0.96f), Vector2.one },   // Attack3: gerilme -> saplama
            { new Vector2(1.05f, 0.95f), new Vector2(0.97f, 1.05f), new Vector2(1.16f, 0.90f), Vector2.one },   // Attack4: sıçrama -> dönüş -> inişte büyük kesim
        };
        void OnEnable() { CombatImpactFeedback.AttackImpact += OnAttackImpact; }
        // Vuruş anı: donma sırasında görünsün diye anında uygula (sonra yumuşakça hedefe döner).
        void OnAttackImpact(int step, float kick)
        {
            if (!frameSquash || frameSR == null || !frameSR.enabled) return;
            sqCur.x += kick * squashAmount;
            sqCur.y -= kick * 0.8f * squashAmount;
        }
        Vector2 SquashTarget(Sprite s)
        {
            if (!frameSquash || s == null) return Vector2.one;
            string n = s.name;   // "Attack2HD_3"
            if (n.Length < 11) return Vector2.one;
            int a = n[6] - '1', k = n[n.Length - 1] - '1';
            if (a < 0 || a >= SquashTable.GetLength(0) || k < 0 || k >= SquashTable.GetLength(1)) return Vector2.one;
            return Vector2.one + (SquashTable[a, k] - Vector2.one) * squashAmount;
        }
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
            frameSR.transform.localScale = new Vector3(L / (Mathf.Abs(ls.x) > 1e-6f ? ls.x : 1f) * sqCur.x, L / (Mathf.Abs(ls.y) > 1e-6f ? Mathf.Abs(ls.y) : 1f) * sqCur.y, 1f);
            UpdateGlow(s, frameSR.color.a);
            float fx = frameSR.flipX ? -1f : 1f;
            frameSR.transform.position = r.transform.position + new Vector3(frameAtkOffset.x * fx, frameAtkOffset.y, 0f);
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
            anim.swordReady = startDrawn;
            if (mainOld != null)
            {
                rig.sortingLayerName = mainOld.sortingLayerName;
                rig.sortingOrderOffset = mainOld.sortingOrder;
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

        bool wasDashing; float dashStart;
        PlayerPosture posture; int lastPosture; bool wasBroken; string defClip; float defStart;
        // --- parry / block (attack rig clips) ---
        [Header("Parry / Block (RonniAtk)")]
        public float parryRaise9 = 0.07f, parryHold5 = 0.35f, parryReturn5 = 0.30f, blockRaise2 = 0.18f, blockReturn2 = 0.3f;
        public float idleDrawnPeriod2 = 3.2f; bool idleDrawnOn;
        [Tooltip("Kılıcı koyma tuşunda oynayan chiburi + noto süresi (sn).")] public float chiburiDur = 2.6f; float chiburiT = -1f; bool bladeCut;
        // chiburi: kılıç kına ucundan girerken bıçağın kınının ağzından ötesi görünmez (klip fazı 0.74 -> 0.93)
        static float ChiBladeU(float p) { const float t0 = 0.74f, t1 = 0.93f; if (p <= t0) return 1f; if (p >= t1) return 0f; float u = (p - t0) / (t1 - t0); return 1f - u * u * (3f - 2f * u); }
        string defAtk; bool wasOldSucc; float defParryFrom; float defAtkT, defRelT, defFrom, defPhase, blockKick; bool defRel, defWasOn;
        static float Sm01(float x) { x = Mathf.Clamp01(x); return x * x * (3f - 2f * x); }
        void StartDef(string c) { if (anim.HasClip(c)) { defClip = c; defStart = Time.time; } }
        void UpdateCombat()
        {
            atkClip = null;
            if (player.isDashing || (combat != null && combat.IsAttacking)) { defAtk = null; chiburiT = -1f; }   // saldırı / dash savunma animasyonunu keser
            // ---- dash: quick low lunge, everything streams back ----
            bool dashing = player.isDashing;
            if (dashing && !wasDashing) dashStart = Time.time;
            wasDashing = dashing;
            if (dashing && playerRun != null && playerRun.HasClip("dash") && rigRun != null && rigRun.IsBuilt) return; // profil dash (mode 3) LateUpdate'te
            if (dashing && anim.HasClip("dash")) { anim.SetAction("dash", Time.time - dashStart); return; }

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
            if (playerAtk != null && playerAtk.HasClip("parry") && playerAtk.HasClip("block") && defClip != "guard_break")
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
            // kılıç çekikken koyma tuşu: chiburi (kan silkeleme) + noto (kınına koyma)
            if (chiburiT < 0f && shClip == null && Input.GetKeyDown(sheatheKey) && player.isGrounded && anim.swordReady && playerAtk != null && playerAtk.HasClip("chiburi")) chiburiT = 0f;
            if (chiburiT >= 0f)
            {
                if (Mathf.Abs(player.moveInput) >= 0.1f || !player.isGrounded) chiburiT = -1f;
                else
                {
                    chiburiT += Time.deltaTime;
                    if (chiburiT >= chiburiDur) { chiburiT = -1f; anim.swordReady = false; }
                    else { anim.swordReady = true; shClip = null; return; }
                }
            }
            if (shClip == null && Input.GetKeyDown(sheatheKey) && player.isGrounded && anim.HasClip("sheathe") && anim.HasClip("unsheathe"))
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
                    rig.SetSorting(best.sortingLayerName, best.sortingOrder);
                    if (rigBL != null) rigBL.SetSorting(best.sortingLayerName, best.sortingOrder);
                    if (rigBR != null && rigBR != rigBL) rigBR.SetSorting(best.sortingLayerName, best.sortingOrder);
                    if (rigRun != null) rigRun.SetSorting(best.sortingLayerName, best.sortingOrder);
                    if (rigIdle != null) rigIdle.SetSorting(best.sortingLayerName, best.sortingOrder);
                    if (rigJump != null) rigJump.SetSorting(best.sortingLayerName, best.sortingOrder);
                    if (rigAtk != null) rigAtk.SetSorting(best.sortingLayerName, best.sortingOrder);
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
                bool dashP = player.isDashing && playerRun != null && playerRun.HasClip("dash") && rigRun != null && rigRun.IsBuilt;
                int mode = dashP ? 3 : air ? 2 : run ? 1 : 0;
                if (dashDustEnabled && rigRun != null && rigRun.IsBuilt)
                {
                    if (dashDust == null) { dashDust = GetComponent<RonniDashDust>(); if (dashDust == null) dashDust = gameObject.AddComponent<RonniDashDust>(); }
                    dashDust.Init(player, rigRun);
                    bool dd = player.isDashing && dashP;
                    float dd01 = (Time.time - dashStart) / Mathf.Max(0.0001f, player.dashTime * DashState.DurationScale);
                    float ddir = player.GetDashDirection();
                    if (Mathf.Abs(ddir) < 0.01f) ddir = player.facingDir;
                    if (dd && !dustPrev) dashDust.Begin(ddir);
                    dashDust.Tick(dd && dd01 < dashTrailEndFrac, ddir);
                    dustPrev = dd;
                }
                if (mode != bMode || (mode != 0 && right != bRight))
                {
                    if (rigB != null) rigB.SetShown(false);
                    bool newJump = mode == 2 && bMode != 2;
                    if (newJump)
                    {
                        float vx0 = player.rb != null ? Mathf.Abs(player.rb.linearVelocity.x) : 0f;
                        standJump = rigJump != null && rigJump.IsBuilt && playerJump != null && playerJump.HasClip("air")
                                    && vx0 < standJumpMaxSpeed && Mathf.Abs(player.moveInput) < 0.1f;
                    }
                    bMode = mode; bRight = right;
                    rigB = right ? rigBR : rigBL; playerB = right ? playerBR : playerBL;
                    if ((mode == 1 || mode == 3 || (mode == 2 && playerRun != null && playerRun.HasClip("air_a"))) && rigRun != null && rigRun.IsBuilt && playerRun != null) { rigB = rigRun; playerB = playerRun; }
                    if (mode == 2 && standJump) { rigB = rigJump; playerB = playerJump; }
                    if (mode != 0)
                    {
                        if (newJump) jumpCount++;
                        playerB.clip = mode == 3 ? "dash" : mode == 2 ? (standJump ? "air" : ((jumpCount % 2 == 1) ? "air_a" : "air_b")) : "run";
                        playerB.manual01 = -1f;
                        playerB.Restart(0f); rigB.SetShown(true);
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
                    else airU = -1f;
                }
                // dururken (aksiyon / kılıç çekme yok) ön görünüm yerine nefes alan idle çizimi görünür
                if (atkHoldIdle && (atkClip != null || mode != 0 || !anim.ActionActive)) atkHoldIdle = false;
                bool idleNow = mode == 0 && rigIdle != null && rigIdle.IsBuilt && (!anim.ActionActive || atkHoldIdle) && shClip == null;
                if (rigIdle != null && rigIdle.IsBuilt) { rigIdle.SetShown(idleNow); rig.SetShown(mode == 0 && !idleNow); }
                if (rigAtk != null && rigAtk.IsBuilt)
                {
                    bool atkOn = mode == 0 && playerAtk != null && ((atkClip == "attack1" || atkClip == "attack2" || atkClip == "attack3") && playerAtk.HasClip(atkClip) || atkClip == "attack4" && FrameAtkActive());
                    string dClip = defAtk != null ? defAtk : (chiburiT >= 0f ? "chiburi" : null);
                    float dPh = defAtk != null ? defPhase : Mathf.Clamp01(chiburiT / Mathf.Max(0.1f, chiburiDur));
                    bool dOn = mode == 0 && dClip != null && atkClip == null && playerAtk != null && playerAtk.HasClip(dClip);
                    Color bcc = originalSprite != null ? originalSprite.color : Color.white;
                    if (bladeCut && !(dOn && dClip == "chiburi")) { rigAtk.SetPartWindow("atk_blade", 0f, 1f, bcc, bcc); bladeCut = false; }
                    if (dOn)
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
                            bool dIdle = mode == 0 && idleNow && anim.swordReady && playerAtk != null && playerAtk.HasClip("idle_drawn");
                            if (dIdle)
                            {
                                if (rigIdle != null) rigIdle.SetShown(false); rig.SetShown(false); idleNow = false;
                                if (!idleDrawnOn) rigAtk.SetColor(bc, true);
                                idleDrawnOn = true;
                                rigAtk.SetShown(true); playerAtk.clip = "idle_drawn";
                                playerAtk.manual01 = (Time.time % idleDrawnPeriod2) / idleDrawnPeriod2;
                                rigAtk.SetPartWindow("atk_smear", 0f, 0f, Color.clear, Color.clear);
                            }
                            else { idleDrawnOn = false; rigAtk.SetShown(false); (idleNow ? rigIdle : rig).SetColor(bc, true); }
                        }
                    }
                }
                UpdateWait(idleNow);
                int vid = mode == 0 ? 0 : (right ? 2 : 1);
                if (vid != visualId) { if (visualId >= 0) popT = 0f; visualId = vid; }
            }
            if (!frameWant && frameSR != null && frameSR.enabled)
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
            ApplyTransitionScale();
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
            if (popT < 1f) { float e = Mathf.SmoothStep(0f, 1f, popT); sx *= Mathf.Lerp(0.72f, 1f, e); sy *= Mathf.Lerp(1.04f, 1f, e); }
            if (landT < 1f) { float s = Mathf.Sin(Mathf.PI * landT) * (1f - 0.3f * landT); sy *= 1f - landSquash * s; sx *= 1f + landSquash * 0.65f * s; }
            var sc = new Vector3(sx, sy, 1f);
            if (partsA != null) partsA.localScale = visualId <= 0 ? sc : Vector3.one;
            if (rigBL != null) rigBL.transform.localScale = visualId == 1 ? Vector3.Scale(baseBL, sc) : baseBL;
            if (rigBR != null && rigBR != rigBL) rigBR.transform.localScale = visualId == 2 ? Vector3.Scale(baseBR, sc) : baseBR;
            if (rigRun != null) rigRun.transform.localScale = (visualId > 0 && rigB == rigRun) ? Vector3.Scale(baseRun, sc) : baseRun;
            if (rigIdle != null) rigIdle.transform.localScale = visualId <= 0 ? Vector3.Scale(baseIdle, sc) : baseIdle;
            if (rigAtk != null) rigAtk.transform.localScale = visualId <= 0 ? Vector3.Scale(baseAtk, sc) : baseAtk;
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
