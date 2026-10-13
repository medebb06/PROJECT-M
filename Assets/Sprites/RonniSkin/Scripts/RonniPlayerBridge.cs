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
        public string runFolder = "RonniRun";
        [Tooltip("Koşu çiziminin boyu (1 = Ronni ile aynı boy).")] public float runScale = 1f;
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
                }
            }
            if (hideOriginal && hidden != null) foreach (var r in hidden) if (r != null && !r.forceRenderingOff) r.forceRenderingOff = true;
            if (mirrorSpriteColor && originalSprite != null && rig.IsBuilt)
            {
                rig.SetColor(originalSprite.color, true);
                if (rigBL != null && rigBL.IsBuilt) rigBL.SetColor(originalSprite.color, true);
                if (rigBR != null && rigBR != rigBL && rigBR.IsBuilt) rigBR.SetColor(originalSprite.color, true);
                if (rigRun != null && rigRun.IsBuilt) rigRun.SetColor(originalSprite.color, true);
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
                    bMode = mode; bRight = right;
                    rigB = right ? rigBR : rigBL; playerB = right ? playerBR : playerBL;
                    if ((mode == 1 || mode == 3 || (mode == 2 && playerRun != null && playerRun.HasClip("air_a"))) && rigRun != null && rigRun.IsBuilt && playerRun != null) { rigB = rigRun; playerB = playerRun; }
                    if (mode != 0)
                    {
                        if (newJump) jumpCount++;
                        playerB.clip = mode == 3 ? "dash" : mode == 2 ? ((jumpCount % 2 == 1) ? "air_a" : "air_b") : "run";
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
                int vid = mode == 0 ? 0 : (right ? 2 : 1);
                if (vid != visualId) { if (visualId >= 0) popT = 0f; visualId = vid; }
            }
            ApplyTransitionScale();
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
        }

        void OnDisable()
        {
            if (hidden != null) foreach (var r in hidden) if (r != null) r.forceRenderingOff = false;
        }
    }
}
