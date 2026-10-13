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
        bool atkWasOn, atkHoldIdle; float atkFade, atkHoldT, atkRetT;
        RonniSkinRig fadeTop; int fadeTopBase;
        void RestoreFadeTop() { if (fadeTop != null) { fadeTop.SetSorting(fadeTop.sortingLayerName, fadeTopBase); fadeTop = null; } }
        [Tooltip("Smear izinin uzunluğu (yayın oranı): kılıç ucunun arkasında bu kadar iz kalır.")] public float smearTrail = 0.55f;
        [Tooltip("İzin kuyruğunun tamamen silinmeye başladığı klip zamanı.")] public float smearEndStart = 0.60f;
        [Tooltip("Smear yönünü yatay çevirir (sağdan sola açılır, sağ taraftan solar).")] public bool smearFlip2 = true;
        static readonly float[] TipT = { 0.396f, 0.43f, 0.45f, 0.47f, 0.49f, 0.506f, 0.526f, 0.543f, 0.563f, 0.582f, 0.60f, 0.62f };
        static readonly float[] TipU = { 0f, 0.02f, 0.11f, 0.2f, 0.3f, 0.45f, 0.53f, 0.69f, 0.86f, 0.905f, 0.98f, 1f };
        // Smear: kılıç ucunu takip eden ay şeklinde iz (baş = ucun konumu, kuyruk solar)
        void SmearWin(float ph)
        {
            float head = 0f;
            if (ph > TipT[0])
            {
                head = 1f;
                for (int i = 1; i < TipT.Length; i++)
                    if (ph <= TipT[i]) { head = Mathf.Lerp(TipU[i - 1], TipU[i], (ph - TipT[i - 1]) / (TipT[i] - TipT[i - 1])); break; }
            }
            float k = Mathf.Clamp01((ph - smearEndStart) / 0.18f); k = k * k * (3f - 2f * k);
            float tail = Mathf.Max(head - smearTrail, k);
            if (ph <= TipT[0]) tail = head = 0f;
            Color ct = new Color(smearGlow, smearGlow, smearGlow, 0f), ch = new Color(smearGlow, smearGlow, smearGlow, 1f);
            if (smearFlip2) rigAtk.SetPartWindow("atk_smear", 1f - head, 1f - tail, ch, ct, true);
            else rigAtk.SetPartWindow("atk_smear", tail, head, ct, ch);
        }
        float AtkRemap(float p)
        {
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
        void StartDef(string c) { if (anim.HasClip(c)) { defClip = c; defStart = Time.time; } }
        void UpdateCombat()
        {
            atkClip = null;
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
            if (posture != null && posture.IsBroken && !wasBroken) StartDef("guard_break");
            wasBroken = posture != null && posture.IsBroken;
            if (OldParryPlaying() && defClip != "parry_success" && defClip != "guard_break") StartDef("parry_success");
            else if (postureHit && (blocking || parrying) && defClip != "guard_break") StartDef("block_hit");
            else if (parrying && !wasParrying && defClip != "parry_success") StartDef("parry");
            wasParrying = parrying;
            if (defClip != null || blocking || parrying) { anim.swordReady = true; shClip = null; }
            if (defClip != null)
            {
                float dt0 = Time.time - defStart;
                if (dt0 < anim.ClipDuration(defClip)) { anim.SetAction(defClip, dt0); return; }
                defClip = null;
            }
            if (blocking || parrying) { anim.SetAction("block", Time.time); return; }

            // ---- sheathe / draw on key (grounded, idle) ----
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
                    bool atkOn = mode == 0 && atkClip == "attack1" && playerAtk != null && playerAtk.HasClip("attack1");
                    if (atkOn)
                    {
                        rig.SetShown(false); if (rigIdle != null) rigIdle.SetShown(false); idleNow = false;
                        if (!atkWasOn) rigAtk.SetColor(originalSprite != null ? originalSprite.color : Color.white, true); atkWasOn = true; atkHoldIdle = true; atkFade = 0f; atkHoldT = 0f; atkRetT = 0f; rigAtk.SetShown(true); playerAtk.clip = "attack1"; playerAtk.manual01 = AtkRemap(Mathf.Clamp01(atkProg)); SmearWin(playerAtk.manual01);
                    }
                    else
                    {
                        Color bc = originalSprite != null ? originalSprite.color : Color.white;
                        if (atkWasOn) { atkWasOn = false; atkHoldIdle = true; atkHoldT = atkHoldTime3; atkRetT = 0f; atkFade = 0f; }
                        bool atkInterrupt = mode != 0 || atkClip != null || Mathf.Abs(player.moveInput) >= 0.1f;
                        if (atkInterrupt && (atkHoldT > 0f || atkRetT > 0f)) { atkHoldT = 0f; atkRetT = 0f; atkFade = atkCrossfade; }
                        if (atkHoldT > 0f || atkRetT > 0f)
                        {
                            float ph;
                            if (atkHoldT > 0f) { atkHoldT -= Time.deltaTime; float hu = 1f - Mathf.Clamp01(atkHoldT / Mathf.Max(0.01f, atkHoldTime3)); hu = hu * hu * (3f - 2f * hu); ph = Mathf.Lerp(atkSettlePhase, atkHoldPhase, hu); if (atkHoldT <= 0f) atkRetT = atkReturnTime3; }
                            else { atkRetT -= Time.deltaTime; float u = 1f - Mathf.Clamp01(atkRetT / Mathf.Max(0.01f, atkReturnTime3)); u = u * u * (3f - 2f * u); ph = Mathf.Lerp(atkHoldPhase, 1f, u); }
                            rig.SetShown(false); if (rigIdle != null) rigIdle.SetShown(false); idleNow = false;
                            rigAtk.SetShown(true); playerAtk.clip = "attack1"; playerAtk.manual01 = ph; SmearWin(ph);
                            atkHoldIdle = true;
                        }
                        else
                        if (atkFade > 0f && mode == 0 && atkCrossfade > 0f)
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
                        else { if (atkFade > 0f) { atkFade = 0f; } RestoreFadeTop(); rigAtk.SetShown(false); (idleNow ? rigIdle : rig).SetColor(bc, true); }
                    }
                }
                UpdateWait(idleNow);
                int vid = mode == 0 ? 0 : (right ? 2 : 1);
                if (vid != visualId) { if (visualId >= 0) popT = 0f; visualId = vid; }
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
            if (firefly != null) firefly.Cancel();
            if (hidden != null) foreach (var r in hidden) if (r != null) r.forceRenderingOff = false;
        }
    }
}
