# PROJECT M — TASK HANDOFF

## Current task
Boss1 kılıç darbesi (saldırı) animasyonu eklendi ve revize edildi: 2026-10-16. Yalnızca `Assets/Scripts/Boss1Rig.cs` değişti. Unity'de DERLEME ve görsel test EDİLMEDİ.

### Boss1 saldırı animasyonu (2026-10-16, 2. sürüm)
- Boss1 Animator/.anim KULLANMIYOR; kemikleri `Boss1Rig.cs` kodla sürüyor (idle = prosedürel nefes). Saldırı da kodla.
- 3. sürüm: kılıç uçla saplanmaz, KESKİN yüzüyle ~15° yatık vurur (alt kenar zemine değer). Pozlar Python simülasyonuyla (rig geometrisinden) bulundu: strikeChest 18, strikeBelly 10, strikeArm 10, strikeSword 130.2, strikeCrouch 0.07. Unity'de DENENMEDİ.
- Zemin koruması (`keepBladeAboveGround`, `groundY` -0.06): bıçak dış çizgisi (BladeHull) her karede ölçülür; zemin çizgisinin altına inerse bilek yukarı kaldırılır. Impact sarsıntısı da bu korumadan geçer.
- Fazlar: Windup 0.7 sn (kılıç yukarı/arkaya, gövde geriye yaylanır) -> Tension 0.3 sn (tam gerili, titrer = parry uyarısı) -> Strike 0.09 sn (ani) -> Hold 0.25 -> Recover 0.8.
- API: `PlayAttack()`, `IsAttacking`, `TelegraphTime`, `AttackDuration`, eventler `AttackStrikeStarted`, `AttackHit` (uç yere vurdu), `AttackFinished`.
- Pivot: root çömelme/ölçek arka ayağın zemin noktası (`pivotBackFoot` = 2.27, -0.05) etrafında; ileri kayma (lunge) kaldırıldı, öne eğilme belly/chest ile.
- Sol kol: tek sprite (`armL_upper`), `armLPivot` etrafında döner (idle sallanma + saldırıda geriye/ileri).
- Önizleme: Play Mode'da Boss1Rig ⋮ menüsü "Test: Saldiri Oynat" veya `Auto Attack Every` = 2 (0 = kapalı).
- Değişiklik: `Cache()` artık aynı isimli "head"/"sword" için KEMİĞİ (ilk bulunan) tutar, sprite katmanını değil. Idle'da baş/kılıç sallanması artık doğru pivottan döner (hafif görsel fark olabilir).
- Boss'a AI/hasar/hitbox BAĞLANMADI: `PlayAttack()` yalnızca test menüsü/auto ile çağrılıyor.
- Yedek: `_backup_boss1_attack_1016/Boss1Rig.cs.txt` (orijinal, saldırısız hâl).
- Bilinen: `Resources/Boss1/boss1_rig.json` gerçekte `Assets/Sprites/Boss1/Resources/Boss1/` altında; "Yeniden Kur" bu yüzden çalışır, prefab zaten gömülü rig kullanıyor.
- Bilinen: Sol kol tek sprite olduğu için sadece sallanır; ayrı dirsek/el için PSD'de ayrı katman gerekir.

## Boss1 yürüyüş + gerçek düşman (2026-10-16) — DERLENMEDİ / OYNANMADI, kodla yazıldı
- İstek: ağır/sert yürüyüş, oyuncuyu takip, vurup hasar versin, parry edilsin, boss hasar alsın.
- YENİ `Assets/Scripts/Boss1Enemy.cs`: Boss1'i mevcut düşman sistemine bağlar (yeni savaş sistemi YOK). Awake'te: `TrainingDummy`'yi kaldırır (oyuncu vuruşunu yutuyordu: `TrainingDummy.TryHit` GetComponentInParent ile yakalar), `Rigidbody2D` (Dynamic, kütle 1000, gravity 3.5), `Health` (900), `EnemyBalance` (200), gizli `FacingProxy` SpriteRenderer, `EnemyController` ekler ve boss değerlerini verir (yürüme 4.5, durma mesafesi 15, menzil 24, vuruş yüksekliği 3.5, standby kapalı, engellenemez vuruş kapalı, parry denge hasarı 50, sersemleme 3.5 sn, savrulma x0.12, saldırı sırasında süper zırh).
- `attackWarningTime` her karede `Boss1Rig.ContactTime`'a (= windup+tension+strike = 1.09 sn) eşitlenir: kılıç yere vurduğu an = hasar/parry kararı anı. Parry yavaşlatması `EnemyTime` ile hem EnemyAttackState'i hem rig'i aynı yavaşlatır.
- Saldırı hasarı oyuncuya `PlayerDamageReceiver` ile gider; oyuncu hasarı oyunda `FixedHitPercent` (%20 max can) ile sabit, `attackDamage` değeri etkilemez.
- DEĞİŞTİ `Boss1Rig.cs` (yedek: `_backup_boss1_walk_1016/Boss1Rig.cs.txt`):
  - `makeEnemy` (varsayılan true): oyun başında Boss1Enemy otomatik eklenir -> prefab/sahne elle DEĞİŞTİRİLMEDİ. Kapatırsan eski "sadece animasyon" hali (ama TrainingDummy yoksa vurulamaz).
  - Yön: `flipX` yerine `facingSource` (EnemyController'ın yazdığı proxy flipX) -> `visual.localScale.x` aynalanır (açı çarpanı `dir` kaldırıldı, hep 1). Rig dogal olarak SOLA bakar; aynalama transform x=0 etrafında.
  - Ağır yürüyüş: 3 bacak (legR=ön, legL=arka, legBack=orta) ripple sırayla adım atar (swing 0.3). Ön/arka bacak 2 kemikli IK (ayak yere sabit basar, kayma yok), orta bacak tek parça. Faz hızdan sürülür: `faz += (1-swing)*hiz/(stride*dünyaÖlçeği)`. Her ayak basışında gövde çöker (`walkDip`), kafa/kılıç sarsılır, `CameraShake.Shake(stepShake)`, `FootStep(int)` eventi.
  - Yeni API: `ContactTime`, `CancelAttack()` (windup/tension'da yumuşak iptal), `Flinch(float)`, `OnParried(bool)`, `Staggered`, `Dead`, `MoveAllowed`, `EnsureBuilt()`.
  - Saldırı/idle zamanı `Time.time` yerine `EnemyTime.DeltaTime` (parry slow-mo'da animasyon da yavaşlar). `TrainingDummy` bağımlılığı kaldırıldı; vurulma tepkisi `CombatEvents.EnemyHit` ile (Boss1Enemy).
  - Sersemleme (denge kırık) ve ölüm pozları eklendi (basit, FK doğrulaması YAPILMADI).
- DEĞİŞTİ `EnemyController.cs` (yedek: `_backup_boss1_walk_1016/EnemyController.cs.txt`): sadece EKLEME — `public event Action AttackAnimationRequested` (PlayAttackAnimation başında tetiklenir), `public void SetFacingRenderer(SpriteRenderer)`. Mevcut davranış değişmedi.
- Doğrulama: yürüyüş bacak IK'sı Python FK simülasyonuyla çizilip kontrol edildi (ayaklar yerde/kopma yok). Unity'de derleme, çalıştırma, parry zamanlaması, çarpışma, ses: DOĞRULANMADI.
- Bilinen riskler / ayarlanacaklar:
  - Boss1 altında zemin Collider'ı yoksa düşer (Boss1Enemy 60 birim düşünce başa alır, uyarı yazar).
  - Vuruş alanı: ön tarafta 0-24 birim (kılıç görseli 9.5-23.5'e iner; gövde altındaki oyuncu da vurulur). İç ölü bölge yok.
  - `executeDistance` 5 verildi (infaz sırasında oyuncunun durma mesafesi; boss büyük). Infaz akışı boss'ta test edilmedi.
  - Boss'ta `EnemyHitFeedback` (ses/VFX), `EnemyBalanceBar`, `EnemyAttackAudio` YOK (prefab'a elle kopyalanabilir; Duelist'ten Copy/Paste Component Values).
  - `BossController` (faz 2, arena) bilerek eklenmedi (RunManager/Phase1Stubs bağımlılığı).
  - Oyuncu parry penceresi 0.17 sn; kılıcın yere vurduğu an hasar anıdır. Hissi `windupTime/tensionTime` ile ayarla; `attackWarningTime` otomatik uyar.

### Boss1 revizyon (2026-10-16, aynı gün 2) — DERLENMEDİ / OYNANMADI
- Düzeltme: boss'un 3 bacağı YOK. `legBack` + `legBack_knee` = sol KOL + EL. Yürüyüş artık 2 bacak (legR ön, legL arka; faz 0 / 0.5, swing 0.4) + sol kol/el karşı fazda (phaseOff 0.75, adım sesi/çökme tetiklemez, `isArm`). `FootStep(0/1)`.
- Vuruş zamanlaması: `PlayAttack()` ilk karenin dt'sini atlar (`skipDt`) -> rig, EnemyAttackState sayacıyla aynı karede darbe noktasına varır. Hasar anı = kılıcın yere indiği an (Contact = windup+tension+strike).
- Hız: `walkSpeed` 4.5 -> 8.5.
- YENİ `Boss1Hud.cs`: ekran üst ortasında ince kırmızı can çizgisi (gecikmeli koyu kırmızı parça) + altında sarı posture çizgisi (denge kırılınca beyaz). Boss1Enemy.showHud.
- Zorluk: can 2000, denge 240, parry denge hasarı 60 (oyun x0.5 ve cana göre x0.6-1.6 ile ölçekler -> ~13 parry), infaz can %12 (`executeHealthPercent`). Düz vuruş cana ~1 vurur; asıl hasar denge kırılınca (%15 can) + infaz.

### Boss1 hata düzeltmeleri (2026-10-16, 3) — DERLENMEDİ / OYNANMADI
- Menü: `MainMenu.Active` iken Boss1Enemy EnemyController'ı kapatır. `Boss1Rig.PlayAttack()` artık her zaman başlar (yumuşak geçişle).
- Kılıç kalkarken hasar: YENİ `EnemyController.AttackHitGate` (Func<bool>, opsiyonel) + `EnemyAttackState.Tick`'te `warningTimer<=0` sonrası kapı kontrolü. Boss1 kapıyı `Boss1Rig.ReadyToHit()`'e bağlar: darbe inmeden (Hold/Recover öncesi) hasar çözülmez; animasyon yoksa/iptal edilmişse yeniden başlatılır ve bekletilir. Diğer düşmanlarda kapı null -> davranış değişmez.
- Sesler: Boss1Enemy, `Resources/Endless/Duelist` prefab'ından (veya `soundSource` alanından) `EnemyHitFeedback` + `EnemyAttackAudio` bileşenlerini JsonUtility ile kopyalar, AudioSource referanslarını kendi kaynaklarına çevirir, EnemyController'ın AudioClip alanlarını kopyalar. Kaynak yoksa konsola uyarı yazar. Doğrulanmadı: kopyalanan VFX alanları, HitClickAudio'nun sahnede olması.
- Değişen: Boss1Rig.cs, Boss1Enemy.cs, EnemyController.cs (ek), EnemyAttackState.cs (ek, 3 satır).

### Boss1 ayar (2026-10-16, 4) — DERLENMEDİ / OYNANMADI
- `extraCooldown` 0.7 -> 2.0 (vuruşlar arası bekleme). Boss1Enemy.
- Sersemlemiş boss'a düz vuruş: `staggerHitPercent` (0.03 = max canın %3'ü/vuruş), `staggerHitFloorPercent` (0.12: düz vuruşlar canı bu seviyenin altına indiremez, infaz bitirir). `CombatEvents.EnemyHit` içinde ek `Health.TakeDamage`. "Tek vuruşta ölme"nin gerçek sebebi kodda bulunamadı (stagger çarpanı sadece x2); doğrulanmadı.

### Boss1 "parry sonrası tek vuruşta ölüyor" (2026-10-16, 5) — DERLENMEDİ / OYNANMADI
- Sebep: `ParryMark.cs` (ParryMarkSystem) parry yenen düşmanı 3.5 sn "açık" işaretleyip ilk düz vuruşta `TakeDamage(MaxHealth)` ile öldürüyor; yalnızca `BossController`'ı hariç tutuyordu. Boss1 hariç tutuldu (2 satır). Önceki stagger yüzdesi (4) ayarı geçerli.

### Boss1 HUD + stagger görünümü (2026-10-16, 6) — DERLENMEDİ / OYNANMADI
- `Boss1Hud.cs` yeniden yazıldı: uGUI yerine `OnGUI`, oyuncunun `MinimalHealthBar` stili (beyaz sade çizgi, soluk iz), ekran ÜST ORTA; üstte can, altında ince posture çizgisi (denge kırılınca tam parlak). Menüdeyken/ölünce solar. `RunCameraZoom.TopBarPixels` ve `MobileInput.HudMul` kullanır.
- Stagger "outline" şüphelisi: `EnemyController.solidWhiteFlash` (denge kıran vuruşta her katmanı düz beyaz silüete çevirir; katmanlı rig'de garip kenar çıkarır) Boss1 için kapatıldı (`Configure`). Kesin sebep doğrulanmadı; önceki ParryMark tint'i de zaten hariç tutuldu.

### Boss1 flaş (2026-10-16, 7) — DERLENMEDİ / OYNANMADI
- Kullanıcı: "beyaz parça parça yanıyor". `EnemyController.suppressFlash` (yeni, varsayılan false) true iken `PlayFlash` hiçbir şey yapmaz; Boss1Enemy true yapar. Kesin sebep (katman bazlı renk/materyal değişimi) doğrulanmadı. Vuruş geri bildirimi artık yalnızca rig sarsıntısı (Flinch).

### Boss1 ses + squash/stretch + hasar flaşı (2026-10-16, 8) — DERLENMEDİ / OYNANMADI
- Kullanıcının mp3'ünden (36 sn, 10 kükreme) 10 klip kesildi (fade-in 15 ms, fade-out ~0.3 sn, limiter): `Assets/Sprites/Boss1/Resources/Boss1/Sfx/boss1_roar_01..10.ogg`. Boss1Enemy her darbe başında (`Boss1Rig.AttackStrikeStarted`) rastgele birini çalar (art arda aynı olmaz, hafif pitch farkı). `roarClips` boşsa `Resources.LoadAll("Boss1/Sfx")`. Unity klasör/meta oluşturup içe aktarmalı.
- `Boss1Rig`: `Squash(strength)` (yay; visual ölçeği x genişler y kısalır; `squashAmount`, `squashStiffness`), `Flash()` (tüm katmanlar aynı karede düz beyaz sprite'a geçer; beyaz sprite'lar RenderTexture ile ilk kullanımda üretilir; `flashTime` 0.09). Boss1Enemy her `EnemyHit`'te ikisini çağırır; parry'de squash.
- Doğrulanmadı: beyaz sprite üretimi (sprite atlas/okunamayan doku), ölçek değişiminin yürüyüş adımına etkisi.

### Boss1 kamera sarsıntısı / duruş / menzil (2026-10-16, 9) — DERLENMEDİ / OYNANMADI
- `attackShake` (0.9): kılıç yere indiği anda (`Boss1Rig.AttackHit`) `CameraShake`. `LateUpdate`: rig saldırı animasyonundayken (sersemlemediyse) yatay hız sıfırlanır -> animasyon bitene kadar yürümez. `chaseRange` 120 -> 1000.

### Boss1 kükreme ayarı (2026-10-16, 10)
- Kükreme artık darbeden ÖNCE (saldırı gerilmesi başlarken, `OnAttackAnimationRequested`) çalar; ses 0.45, olasılık `roarChance` 0.35, en az `roarMinInterval` 8 sn arayla. DERLENMEDİ.

### Boss1 parry efekti/sesi (2026-10-16, 11) — DERLENMEDİ / OYNANMADI
- Parry efekti `CombatVFXManager` (sahnede tek) + `EnemyHitFeedback.PlayParry` ile çalışır; ses `EnemyHitFeedback.parrySound`. `Resources/Endless/Duelist.prefab` aslında `Assets/Prefabs/Duelist.prefab`'ın varyantı (425 B). Kaynak bulunamazsa yüklü başka bir `EnemyHitFeedback` aranır.
- Boss1Enemy oyun başında konsola `[Boss1] Ses kopyasi: ...` satırı yazar (kaynak, hitFeedback, parrySound, attackAudio, vfxManager). Kopya başarısızsa `PlayParryFallback()` aynı sesi + `CombatVFXManager.PlayParry` efektini kendisi oynatır.

### Boss1 parry güçlendirme (2026-10-16, 12) — DERLENMEDİ / OYNANMADI
- `parryBalanceDamage` 60 -> 110 (Boss1Enemy). Parry'de squash `parrySquash` (3, denge kırınca x1.4) Boss1Enemy.OnParry'den; Boss1Rig.OnParried içindeki eski Squash kaldırıldı; `squashAmount` 0.2.
- Parry efekti: `ParryVFX` kökü `parryEffectScale` (4) ile büyütülür (çarpışma noktası yakınındaki, ölçeği 1 olan yeni örnekler). ParryVFX.cs DEĞİŞMEDİ.

### Boss1 kükreme tetikleri / parry (2026-10-16, 13) — DERLENMEDİ / OYNANMADI
- Kükreme artık yalnızca: (1) boss ilk kez kamera görüş alanına girince (`CheckRoarTriggers`, menü kapalıyken), (2) ölünce. Saldırı kükremesi, `roarChance`/`roarMinInterval` kaldırıldı. Ses bağımsız 2D AudioSource ile çalar (boss yok edilse de biter). `roarVolume` 0.5.
- `parryBalanceDamage` 110 -> 170; `parryEffectScale` 4 -> 20.

### Boss1 giriş sahnesi + posture toparlanma (2026-10-16, 14) — DERLENMEDİ / OYNANMADI
- Giriş sahnesi (`Boss1Enemy.IntroRoutine`): boss ilk kez ekrana girince ya da oyuncu `introDistance` (45) içine gelince bir kez: oyuncu `canControl=false` + hız sıfır, `RunCameraZoom.ForceCinematic=true` (şeritler), `RunCameraZoom.FocusTarget` = boss gövdesi (kamera kayar + `FocusZoomIn` yakınlaşır), `introSettle` sn sonra kükreme + rig sarsıntısı/squash + kamera sarsıntısı, ardından `introHold` sn sonra oyun başlar (kilit/kamera eski haline). Giriş sürerken boss'un EnemyController'ı kapalı. `introCutscene=false` ise yalnız kükreme.
- DEĞİŞTİ `RunCameraZoom.cs` (additive): statik `FocusTarget`, `FocusZoomIn`; `RunCameraRoll` odak noktasına Lerp eder (Cinemachine'den sonra). DEĞİŞTİ `EnemyBalance.cs`: `SetRecovery(delay, speed)` eklendi.
- Posture toparlanma: `postureRecoveryDelay` 5 sn, `postureRecoverySpeed` 2 (oyun normali 1.5 sn / 20; cana göre çarpan 0.4-1.6 hâlâ geçerli).
- Doğrulanmadı: kameranın gerçekten boss'a kayması (rollHelper yolu), canControl=false'ın oyuncuyu tam durdurması.

### Boss1 HUD/sarsıntı/efekt ayarı (2026-10-16, 15) — DERLENMEDİ / OYNANMADI
- Boss barları giriş sahnesi bitene kadar gizli (`Boss1Hud.show`), çizgiler 2 px / 1.5 px (oyuncununki 4 px).
- Saldırı sarsıntısı: `attackShake` 2.5 + yeni `RunCameraZoom.AddShake(dünya birimi)` (doğrudan konum titremesi, ~0.14 sn üstel sönüm; `attackShakeWorld` çarpanı 0.5). Sebep: Cinemachine impulse (CameraShake) görünmüyordu; Instance/impulse atanmış mı doğrulanmadı.
- `parryEffectScale` 20 -> 10.

### Boss1 baş üstü bar + yavaş adım (2026-10-16, 16) — DERLENMEDİ / OYNANMADI
- `Enemyoverheadbars.cs`: Boss1 için baş üstü can/denge çubuğu çizilmez (ekran üstü bar kalır).
- `Boss1Rig.walkAnimSpeed` (0.4): yürüyüş fazı bu çarpanla ilerler -> adımlar çok daha yavaş, hareket hızı/mesafe aynı. `walkStride` 1.6 (IK menzil sınırı). Bilinen: yer ayağı gövdeyle tam eşit hızda geri kaymadığı için ayaklar bir miktar kayabilir (adım boyu IK ile sınırlı).

### Boss1 ZIPLAMA SALDIRISI (2026-10-16, 17) — DERLENMEDİ / OYNANMADI
- `Boss1Enemy.Boss1LeapState` (IEnemyState, iç sınıf): Crouch (kılıç kalkık + gövde ezik, `Boss1Rig.PlayLeapTense` gerilmede bekler) -> Rise (dik zıplayıp ekran dışına, oyuncunun/kameranın `leapApexAbove` üstü; colliderlar kapalı, rb Kinematic) -> Air (yerde kırmızı daire isareti oyuncuyu `leapTrackTime` sn izler, sonra KİLİTLENİR ve hızlı yanıp söner; boss x'i isaretin x'i) -> Fall (`leapFallTime` 0.1 sn, `ReleaseStrike` ile kılıç inişte iner) -> Land (hasar: yatay `leapRadius` 16 içinde ve ayak `leapHitHeight` altında; dash i-frame kaçırır; parry/blok YOK -> `PlayerHitKind.Unblockable`; `CameraShake` + `RunCameraZoom.AddShake`, squash). Sonra Chase'e döner.
- Tetik: `Boss1Enemy.Update`'te Chase iken oyuncu yatay `leapMinDistance`(12)..`leapMaxDistance`(90) arasında ve süre dolduysa saniyede `leapChancePerSecond` (0.35) olasılıkla; ilk ziplama `leapFirstDelay` 6 sn, sonra `leapCooldown` 9 sn. `leapEnabled=false` kapatır.
- Kayma önleme: `EnemyController.superArmorOverride` (yeni; `IsAttackCommitted` buna da bakar) true iken vuruş state'i bozmaz. Havadayken colliderlar kapalı = vurulamaz.
- `Boss1Rig`: `PlayLeapTense()`, `ReleaseStrike()`, `SetBaseSquash(target, speed)` + `leapSquashAmount`. `PlayAttack()` hold'u sıfırlar.
- Doğrulanmadı: ekran dışı yükseklik yeterli mi, yer yüksekliği (`groundY` = başlangıç y), isaret sorting'i (Default/40) zeminin üstünde mi, kameranın uçan boss'u takip edip etmediği, Kinematic<->Dynamic geçişi.

### Boss1 yakın mesafe ziplama (2026-10-16, 18) — DERLENMEDİ / OYNANMADI
- `leapMinDistance` 0. Yakında: normal vuruş başlarken (`OnAttackAnimationRequested`) ziplama hazırsa `leapCloseChance` (0.4) olasılıkla `pendingLeap`; ertesi karede `Update` AttackState'ten Boss1LeapState'e geçer (normal animasyon başlatılmaz). Not: AttackState'in ilk karesi (uyarı sesi) çalmış olabilir; istatistikte "saldırı kesildi" sayılabilir.

## Last completed work
- AI_CONTEXT.md, AI_RULES.md, AI_HANDOFF.md proje köküne kopyalandı (`D:\Oyun\PROJECT-M`). Eski `.md.txt` kopyaları `ProjectSettings/` içinde DURUYOR (silme aracı yoktu); elle silinmeli.
- Assets/Scripts, Player/Duelist/Boss1/Archer prefab'ları ve MyScene2.unity incelendi (scriptlerin yaklaşık yarısı satır satır, kalanı yalnızca dosya adı/boyut düzeyinde).

## Verified (dosyalardan okundu; derleme/oynanış test EDİLMEDİ)
- Unity 6000.5.10f1, URP 17.5.0, Input System 1.20.0. Tek sahne: `Assets/Scenes/MyScene2.unity` (build'de tek sahne).
- Git: dal `main`, remote `https://github.com/medebb06/PROJECT-M.git`. Uncommitted değişiklik durumu BİLİNMİYOR (git çalıştırılamadı).
- Oyuncu: `PlayerController` + `PlayerStateMachine` (`IPlayerState`: Enter/Exit/Update/FixedUpdate). State'ler: Grounded/Air/Jump/WallSlide/WallJump/Dash/AirAttack/Hurt/PostureBreak/Death/ExecuteCharge/ExecuteFail/Execute.
- Savaş: `PlayerCombatController` kendi `ICombatState` (Enter/Tick/Exit) ile `AttackState` / `AirAttackState` çalıştırır. Savunma: `PlayerDefenseController` kendi `IPlayerDefenseState` ile Parry -> Block. Execute: `PlayerFinisher`. Hasar akışı: `PlayerDamage`, `PlayerStats`, `CombatEvents`.
- Düşman: `EnemyController` (`IEnemyState`: Idle, Chase, Attack, Hit, Stagger, Execute) + `EnemyBalance` (posture) + `EnemyMoveset` + `EnemyArchetype` + `EnemyAttackCoordinator` (ritim: beat 1.15 sn, desen 1-1-1.5, en çok 2 eşzamanlı uyarı).
- RunManager GERÇEK DEĞİL: `Phase1Stubs.cs` içinde boş köprü (`RunManager`, `MetaProgress`, `RunUI`, `LevelProps`, `CharmUtil`). Run/Level/Meta/Charm sistemleri silinmiş.
- Oyun akışı: `MainMenu.cs` (GameBoot, GameFlow, ScreenFade, MainMenu), `EndlessManager` (sonsuz mod), `TutorialManager`. Oda/kapı/harita akışı yok.

## Not verified / Known issues
- Derleme ve oynanış doğrulanmadı (Boss1Rig saldırı değişikliği dahil).
- `OYUN_OZETI.md` (Assets/Scripts) eski: charm/RunManager/CharmCatalog anlatıyor, artık yok.
- `BossController` (+BossBrain/Slam/Nova/Leap) sahnede ve Boss1 prefab'ında bağlı görünmüyor; Boss1 prefab'ı `TrainingDummy + Boss1Rig` kullanıyor. Boss gerçekte oynanabilir mi: doğrulanmadı.
- Kod içinde silinmiş charm'lara eski yorumlar/çağrılar kaldı (EnemyExecuteState, PlayerDefenseController, EnemyStaggerState).
- `EnemyAttackState.cs.bak` ve `Assets/Scripts/Claude outputs/MountainBackdrop.cs` (93 baytlık yorum satırı) artık gereksiz; silinmedi.
- Sahnede yalnızca 1 `EnemyController` var; Player sahnede doğrudan mı prefab mı: kesinleşmedi.
- Animator controller / Spine / Ronin rig bağımlılıkları ayrıntılı denetlenmedi.

## Next task (öneri)
- Önce Unity'de derlemenin hatasız olduğunu doğrula, Git durumunu GitHub Desktop'tan kontrol et, AI_CONTEXT.md farklarını onayla.
- Sonra yol haritası Faz 1: combat'ı dondur, elle yapılmış graybox encounter akışı (Duel -> Multi -> Elite -> Mini Boss -> Boss) için basit bir encounter yöneticisi tasarla.

## Git status
- Dal: main. Son commit ve uncommitted değişiklikler: GitHub Desktop'tan kontrol edilip buraya yazılmalı.

## Boss1 ziplama duzeltmesi (2026-10-16, 19) — DERLENMEDİ / OYNANMADI
- Zıplama squash'ı azaltıldı: `Boss1Rig.leapSquashAmount` 0.35 -> 0.12, iniş `Squash` 1.3 -> 0.5.
- Uyarı alanı: zemine paralel düz kırmızı şerit (`leapStripHeight`), izometrik elips kalktı.
- Matematik (`Boss1LeapState.ComputeWindow`, `leapAutoRadius`): yarıçap = `dashDistance*DashState.DistanceScale*leapEdgeMargin(0.95)`; kilit penceresi = `leapReaction(0.3) + dashDur - leapFallTime`. Kilitlenince hemen dash atan oyuncu şeridin dışına ucu ucuna çıkar. Kapatılırsa `leapRadius`/`leapReaction` kullanılır.
- Odaklandıktan sonra `leapSlamCount`=3 ardışık iniş (2. ve 3.'de izleme `leapChainTrackTime` 0.3 sn, aralarda `leapChainGap`).
- Not: device'a commit bazen eski içeriği yazdı (önbellek); farklı stagedPath ile yazıp geri okuyarak doğrulandı.
## Boss1 agresiflik + uclu kombo (2026-10-16, 20) — DERLENMEDİ / OYNANMADI
- Agresiflik: `walkSpeed` 8.5->15, `chaseRange` 1000->5000, `extraCooldown` 2.0->1.2, `leapFirstDelay` 6->4, `leapCooldown` 9->6; `Boss1Rig.walkAnimSpeed` 0.4->0.28 (hizlanan hizla adim frekansi cok artmasin). Boss1Enemy sahnede serialize edilmisse bu varsayilanlar uygulanmaz, Inspector'dan elle ayarla.
- Blok = posture kirma: `blocksToBreakPosture`=2; `enemy.blockPostureDamage = ceil(player.MaxPosture / 2) + 1` (`RefreshBlockPosture`). Kombo blok posture carpani 1'e cekildi. Parry zorunlu.
- Yeni yetenek "Boss1 Uclu Kombo": runtime'da `EnemyMoveset` eklenir (tek hamle, `comboCooldown` 6 sn; hazir degilken klasik tek vurus). Vuruslar: 0.55 sn, 0.36 sn (hizli), 1.45 + rastgele 0..0.45 sn (GECIKMELI, hasar x1.3, parry odulu x1.5). Hasar/parry/blok mantigi mevcut EnemyAttackState kombo altyapisi.
- Rig: `Boss1Rig.PlayAttack(float contactSeconds)` — kisaysa gerilme/darbe hizlanir (`atkScale`), uzunsa gergin pozda bekler (`extraTension`). `SwordBone`, `SetSwordTint` eklendi.
- VFX: `Assets/Free Slash VFX/Prefabs/Impact.prefab` KOPYASI `Assets/Sprites/Boss1/Resources/Boss1/Vfx/Boss1SwordImpact.prefab` (Resources'tan yuklenebilsin diye). Kombo boyunca kilic kemigine baglanir, parcacik/materyal renkleri koyu kirmizi (`comboColor`) yapilir (TintVfx), kilic sprite'i de koyu tonlanir. Boyut: `comboVfxScale`.
- Dikkat: prefab kopyasinin .meta'si Unity tarafindan yeni GUID ile olusur. Efekt hic gorunmezse Console'da "kombo VFX'i yok" uyarisina bak.

## Boss1 kombo okunurlugu (2026-10-16, 21) — DERLENMEDİ / OYNANMADI
- Kilic METAL bolgesi (`BladeHull` poligonu, sap haric) runtime'da kirmiziya boyanan kaplama sprite'i + yumusak hale (`Boss1Rig.EnsureMetalOverlay`); kombo boyunca nabiz atar (oyuncudaki execute parlamasi mantigi, kirmizi). Oyuncudaki parlama sprite sanatindan geliyor, kodda yok.
- Kombo girisi: 1. vurus hazirligi `comboFirstWindup` 0.55 -> 1.8 sn; kilic YAVASCA kalkar (`PlayAttack(contact, slowRaise:true)` -> atkScale ~0.6) ve parlama 0.9/sn ile kademeli artar. Sonraki vuruslar hizli / gecikmeli kalir.
- `Boss1Rig.SetSwordTint(color, amount, speed)`: artik tum sword sprite'ini degil, yalniz kaplamayi surer.

## Boss1 kilic parlamasi duzeltmesi (2026-10-16, 22) — DERLENMEDİ / OYNANMADI
- Onceki koyu kirmizi kaplama "kan" gibi gorundu. Yerine: bicak metali parlak saf kirmizi (koyu 0.62 -> sicak 1.0 kirmizi, pembe yok) + additive cekirdek + bulanik hale. Additive shader: `Assets/Shaders/Boss1SpriteAdditive.shader` (`ProjectM/Boss1SpriteAdditive`, `_Intensity`=7, Impact materyali `_Color` ~8.6 HDR'ye yakin -> bloom). Shader bulunamazsa additive katmanlar normal materyalle (soluk) cizilir.
- Impact VFX prefab rengi `comboColor` = saf kirmizi (1,0,0.01); `TintVfx` materyal Color ozelliklerini yogunluk (HDR) korunarak kirmiziya cevirir.
- Impact shadergraph'i dogrudan kilic sprite'ina uygulanmadi (kendi _Main dokusunu cizer, bicak sekline oturmaz); bunun yerine ayni parlaklik/renk elle taklit edildi.

## Boss1 duz kirmizi bicak + kosarak kombo (2026-10-16, 23) — DERLENMEDİ / OYNANMADI
- Impact parcacik efekti kivilcim gibi gorundu: `comboUseParticles` (varsayilan KAPALI). Bicak metali artik duz kivrik kirmizi (0.5,0,0.05 -> 0.82,0.02,0.1; kilic dokusu zaten duz beyaz) + cok hafif additive isima/hale. Bicak maskesi cevrimdisi dogrulandi: opak piksellerin %81'i BladeHull icinde (sap haric).
- Uzaktan kosarak kombo: oyuncu vurus menzili (`attackReach`) disinda ve `comboRunMaxDistance` (70) icindeyken Chase'ten `EnemyAttackState` baslatilir (`comboRunChancePerSecond`); kombo hazirlik surelerince boss `comboRunSpeed` (26) ile `stopDistance`'a kadar kosar (LateUpdate hiz kontrolu), rig `RunWhileAttacking` ile bacak animasyonu oynar. Kombo secilemezse Chase'e geri doner.
- Not: 1. vurus hazirligi 1.8 sn: kosarken ~46 birim katedebilir.

## Boss1 kilic kirmizi fix + oyun fontu + boss adi (2026-10-16, 24) — DERLENMEDİ / OYNANMADI
- Kirmizi kilic: olasi kok neden = kilic katmani `swordSrs` Rebuild'de doldurulur; rig sahnede hazir kaydedildiyse bos kalir ve kaplama HIC olusmaz. `EnsureMetalOverlay` artik SwordBone altindan sprite'i bulur; olusmazsa dogrudan `sr.color` boyama fallback'i var. Basarida Console'a "[Boss1] kilic kirmizi kaplamasi hazir..." yazar, basarisizsa uyari verir.
- Font: `Knight_Souls.otf` -> `Assets/Resources/UI/Knight_Souls.otf`. Orijinalde Ğ ğ İ Ş ş YOKTU; fonttools ile mevcut aksan parcalarindan (cedilla, brevemod, dotaccentmod) uretilip eklendi (orijinal dosya degismedi). Font BUYUK HARFLIDIR (kucuk harfler kucuk-buyuk).
- `GameFont.cs` (yeni): `GameFont.Get()/Or()` + otomatik `GameFontApplier` (OnGUI icin `GUI.skin.font`, 0.5 sn'de bir tum UI Text ve TextMesh). `MainMenu`, `TutorialMode`, `CombatCallout`, `Critfeedback` ayrica GameFont'u tercih eder. Eski `Resources/UI/ProjectM_Serif_*` yedek kalir.
- Boss adi: `Boss1Enemy.bossName` = "Mustafa Golem". Giris sahnesinde kukreme basinda ekranin tam ortasinda buyuk (ekran yuksekliginin %10'u, `Boss1Hud.nameSize`) belirir, giris bitince solar.

## Boss1 ad yazisi stili + olum yavas cekimi (2026-10-16, 25) — DERLENMEDİ / OYNANMADI
- Ad yazisi (Elden Ring "ENEMY FELLED" tarzi): `Boss1Hud.nameSize` 0.10 -> 0.22 (ekran yuksekligi), harfler tek tek yerlestirilir (`nameTracking` 0.82 = sik harf araligi), arkada yumusak koyu serit, krem renk (0.93,0.86,0.68), yavas buyume. Genislik ekrana sigmazsa otomatik kucultulur.
- Olum yavas cekimi: boss olunce `Boss1SlowMo.Run` (`deathSlowScale` 0.15, `deathSlowHold` 0.9 sn, `deathSlowRelease` 0.8 sn; gercek zaman). `Time.timeScale` + `fixedDeltaTime` degisir; bagimsiz nesnede calistigi icin boss yok edilse de yarim kalmaz; MainMenu.Active (duraklatma) iken zamana dokunmaz.

## (2026-10-16, 26) Boss1 kombo parry okunurlugu + blok itmesi (DERLENMEDI / OYNANMADI)
- Kombo windup'lari: 1. 1.6s, 2. (hizli) 0.62s (eskiden 0.36s: parry sonrasi grace/input buffer yuzunden kacirilirdi), 3. 1.25s +0..0.2 rastgele.
- Boss1Enemy: PlayerBlocked'a abone olup oyuncuyu bossdan uzaga iter (blockKnockback, blockKnockbackDistance 4.5, blockKnockbackDuration 0.3). Vanilla PushPlayerBack |dx|>6 ise iptal ediyordu; bu yuzden boss icin ayri coroutine. Dash sirasinda itmez.

## (2026-10-16, 27) Boss1 ad yazisi: ENEMY FELLED'e daha yakin (DERLENMEDI / OYNANMADI)
- Boss1Hud: nameSize 0.34 (kocaman), nameTracking 0.70 (sikisik), altin renk (0.86,0.72,0.38), pop animasyonu kaldirildi (duz, sadece %3 yavas buyume), ince seffaf serit, sert golge yerine yumusak altin parlama.

## (2026-10-16, 28) Boss1: can 6000, dash ile yon degistirme, DALGA yetenegi (DERLENMEDI / OYNANMADI)
- Boss1Enemy.maxHealth 2000 -> 6000 (runtime eklenen bilesen oldugu icin varsayilan gecerli).
- Dash retarget (dashRetarget, dashRetargetMinWindup 0.18, dashRetargetGrace 0.3): EnemyAttackState sirasinda oyuncu dash'le (veya dash'ten <=0.3s sonra) arkaya gecerse, vurusa >0.18s varsa enemy.RetargetFacing().
- Dalga yetenegi: Boss1WaveState (Enter: odak, rig.PlayAttack(waveFocusTime 1.6, slowRaise), kilic kirmizi; her rig.AttackHit'te Boss1Wave.Spawn; sonraki darbe rig.PlayAttack(waveInterval 0.95) = esit RITIM; waveCount 3). Tetik: Chase durumunda oyuncu 32..140 birim uzaktaysa, waveChancePerSecond 0.3, ilk gecikme 5s, cooldown 9s. Oyuncu <20 birime gelirse iptal (Chase).
- Boss1Wave (yerde ilerleyen yarim ay sprite, procedural, waveSpeed 22, waveHeight 5): oyuncuya degince: isInvincible(dash) -> icinden gecer (RaiseDodge); CanParry -> boss dengesine parryBalanceDamage*0.8, RaiseParry (waveParryNow bayragi OnParry'deki boss tepkisini bastirir), efekt dalganin yerinde; CanBlock -> posture hasari x0.5 + RaisePlayerBlocked (blok itmesi calisir); aksi halde PlayerDamageReceiver.TakeDamage(Normal). Ziplayarak ustunden asilir (feet > groundY + waveHeight).
- Update: rig.CancelAttack kosulu waveActive ile korundu.

## (2026-10-16, 29) Boss1: yakindayken geri ziplayip dalga + posture kirilma merhameti (DERLENMEDI / OYNANMADI)
- Boss1WaveState'e Crouch->Hop->Focus fazlari: oyuncu waveMinDistance'tan yakindaysa (Chase'te, waveCloseChancePerSecond 0.5/sn) boss once geriye ziplar (kinematic + colliderlar kapali, parabolik; hedef mesafe waveHopTargetDistance 48, en fazla waveHopMaxDistance 40; rb.Cast ile duvar payi; <6 birim yer varsa ziplamaz, direkt odaklanir), sonra 3 ritimli dalga. waveAbortDistance 20 -> 8; oyuncu yakinsa dalga bossun hemen onunde dogar.
- Posture merhameti (postureBreakMercy 1.8s): oyuncu posture'u kirilinca (PlayerBlocked postureBroken ya da PlayerPosture.IsBroken kenar tespiti) boss EnemyAttackState/Boss1WaveState'ten Chase'e doner (kombo kalan vuruslari iptal), enemy.StartAttackRecovery(mercy), dalga/ziplama/kombo sayaclari ileri atilir; mercy suresince ucan dalgalar ve ziplama inisi oyuncuya hasar vermez (InMercy). Neden: oyunda her isabet oyuncunun max canin %20'si (PlayerDamageReceiver.FixedHitPercent); kirik oyuncu kombonun kalan vuruslarini yiyip hemen oluyordu.

## (2026-10-16, 30) Boss1 dalga: buyuk+hizli, parry'de renk degistirip geri seker; parry sesi yukseltildi (DERLENMEDI / OYNANMADI)
- waveSpeed 22 -> 55, waveHeight 5 -> 9, waveHitHalfWidth 2 -> 3.5.
- Parry'lenen dalga yok edilmez: reflected=true, yon ters, sprite cyan->beyaz (sprReflect), bossa dogru waveReflectSpeedMultiplier 0.8 hizla gider; bossa waveReflectStopDistance 7 kala WaveReflectHit (Flinch/Squash/Flash + parry efekti + sarsinti) ve yok olur. Boss dengesi parry aninda zaten dusuyor.
- EnemyHitFeedback: parryVolume (varsayilan 2, 0..3) + static PlayLoud(src, clip, vol): PlayOneShot olcegi 1'i asmadigi icin ayni klip katman katman calinir (2 = ~+6dB). PlayParry artik bunu kullanir; Boss1Enemy.PlayParryFallback da (tf.ParryVolume). Inspector'da gerekirse EnemyHitFeedback > Parry Volume ile ayarlanir (prefab'larda eski serilestirme varsayilani 2 olarak gelir).

## (2026-10-16, 31) Boss1: ritimli kombo + parry ipucu, ceviklik, dalga hizi (DERLENMEDI / OYNANMADI)
- Dalga hizi 55 -> 75.
- Ceviklik (agile): walkSpeed 15 -> 22, extraCooldown 1.2 -> 0.5, rig sureleri Configure'da ezilir (windup 0.6, tension 0.24, hold 0.12, recover 0.45, walkAnimSpeed 0.4), leapCrouchTime 1.1 -> 0.85, leapCooldown 6 -> 5, comboCooldown 6 -> 5, waveFocusTime 1.6 -> 1.4.
- Kombo yeniden: 1. vurus yavas kalkis (comboFirstWindup 1.5), sonra comboBeatHits-1 (varsayilan 3 -> 2 ek) vurus HEP AYNI ARALIKTA (comboFastWindup 0.8, sabit, retarget), en son GECIKMELI vurus (comboDelayedWindup 1.6, rastgele 0). Toplam 4 vurus. comboLastIndex = beats; OnStrikeLanded son adimi buna gore sayar.
- Parry ipucu: her vurustan comboCueLead (0.3s) once kilic sicak sari-beyaz parlar (SetSwordTint comboCueColor, hizli) + procedural 'tik' sesi (comboCueVolume 0.7); darbede kirmiziya geri doner. UpdateComboCue(), BuildCueClip().
- 2. vurusun parrylenmemesi icin olasi nedenler (kod okuyarak): onceki windup 0.36/0.62s cok kisaydi; parry sonrasi oyuncu tarafinda grace/whiff cezasi (0.3s) ve hit-stop araya giriyordu. Net kok neden OYNANARAK dogrulanmadi.

## (2026-10-16, 32) Parry hit-stop guclendirildi (DERLENMEDI / OYNANMADI)
- CombatImpactFeedback.PlayParryImpact: otherHitStopScale (0.25) yerine yeni parryHitStopScale (varsayilan 0.8) kullanir -> freeze ~0.10s + ~0.18s yavaslama (scale ~0.32). Eskiden parry freeze ~0.03s idi (hissedilmiyordu). Blok / denge kirma hala otherHitStopScale kullanir. PlayerDefenseController.PlayParryFeedback -> PlayParryImpact oldugu icin oyuncunun tum parry'lerinde (dalga dahil) calisir.

## (2026-10-16, 33) Boss1: daha agresif + yaklasmadan uyanmasin (DERLENMEDI / OYNANMADI)
- Beklemeler kisaldi: extraCooldown 0.5->0.15, agileHold 0.08, agileRecover 0.35, leapFirstDelay 2.5, leapCooldown 3.5, leapChancePerSecond 0.6, leapCloseChance 0.5, waveFirstDelay 3.5, waveCooldown 5, waveChancePerSecond 0.6, waveCloseChancePerSecond 0.7, comboCooldown 3, comboRunChancePerSecond 1.2, kombo kosusu icin ekstra bekleme 1.5->0.4, ziplama Exit min bekleme 3->1.5.
- Uyanma: Update'te `!seenRoared` iken enemy.enabled=false (kovalamaz). Eskiden oyun baslar baslamaz kovaliyordu cunku 'seen' kamera frustum'una girmesiyle de tetikleniyordu; yeni introSeeOnScreen (varsayilan false): sadece oyuncu introDistance (45) icine girince uyanir (intro + kukreme), sonra kovalar. seenRoared'da leap/wave sayaclari yeniden kurulur.

## (2026-10-16, 34) Oyuncu gecis / iptal denetimi + duzeltmeler (DERLENMEDI / OYNANMADI)
Denetim (kod okuyarak; oynanmadi). Mevcut iptal matrisi: Dash -> HER seyi keser (saldiri+savunma, Enter'da); Zipla -> saldiri/savunmayi HER AN keser; Parry/Blok tusu -> saldiriyi keser; Saldiri -> kombo iptal penceresi (comboCancelPoint 0.6); Dash Hurt'un 2. yarisinda cikis (hurtDashCancelAfter 0.5); Ground slam yukselirken de baslar.
Bulunan sorunlar ve duzeltmeler:
1. Dash ~0.33s boyunca canControl=false ve hiz egrisi cani (p^3(1-p)^3) -> son ~%30 yavas kuyruk, zipla/saldiri/parry ancak dash bitince calisiyordu. FIX: PlayerController.dashCancelPoint (0.7; 1=kapali). DashState.Update: dash sirasinda zipla/saldiri/parry istegi varsa ve ilerleme >= dashCancelPoint ise dash erken biter.
2. Zipla tamponu 0.15s, dash 0.33s: dash sirasinda basilan Space kayboluyordu. FIX: PlayerInputHandler: dash/kilit sirasinda jump tamponu max(jumpBufferTime, minInputBuffer 0.3).
3. TryAirJump (cift ziplama) havadaki saldiriyi (AirAttackState) iptal etmiyordu -> ust uste biniyordu. FIX: combat.CancelAttack().
4. Asagi+Space (slam) HandleJump->TryAirJump tarafindan once yakalaniyor, cift ziplama hakki yaniyor, ziplama sesi/impulsu cikiyordu sonra slam basliyordu. FIX: TryAirJump verticalInput<-0.5 && CanGroundSlam ise cikar.
5. Slam inis kilidinde (slamGroundLock) yerden zipla: tampon harcaniyor, JumpState.Enter guard'i geri atiyordu -> zipla yutuluyordu. FIX: HandleJump slamGroundLock'ta tamponu harcamaz.
Dokunulmayan (oynayarak karar verilmeli): dashCooldown 0.4s (Exit'ten sonra), parry sonrasi PlayerParryState.ShortenTo(0.06) + "parry penceresi aciksa yeni basisi yut" (TryStartParry), whiffPenalty 0.3s x0.5 pencere, DashState Gamma() sabit.

## (2026-10-16, 35) Boss kombo hizlandi, dalga cok kullanilir, ziplama teke, parry az posture + akis duzeltmeleri (DERLENMEDI / OYNANMADI)
- Boss1Enemy: comboFirstWindup 1.5->1.0, comboFastWindup 0.8->0.5, comboDelayedWindup 1.6->1.0, comboCueLead 0.3->0.22. Dalga: waveCloseChancePerSecond 0.7->1.5, waveChancePerSecond 0.6->1.0, waveCooldown 5->3, waveFirstDelay 3.5->2.5. leapSlamCount 3->1 (havaya ziplama tek inis). parryBalanceDamage 170->60 (parry basina ~30 denge; eskiden 85, ParryScale 0.5 ile).
- Akis duzeltmeleri (34'ten kalanlar): PlayerController.dashCooldownScale 0.5 (DashState.Exit: dashCooldown*scale, efektif 0.2s); PlayerDefenseController: whiffPenaltyTimeV2 0.2 / whiffWindowMultiplierV2 0.8 (eski alanlar artik kullanilmiyor, prefab degerleri ezilmesin diye yeni adla); TryStartParry: onceki parry penceresi BASARILI olduysa yeni basis yutulmaz, yeni parry baslar (zincirde 2. parry kacmasin).

## (2026-10-17, 36) Posture kirilma / parry hit-stop / dalga / boss hasari / oyuncu posture bari / son vurus itmesi (DERLENMEDI / OYNANMADI)
- ParryMark.cs (ParrySoftTarget): parry artik oyuncuyu dusmana DONDURMEZ; parry sonrasi 0.7s oto-yuz cevirme de durur (noFaceUntil).
- Dusman posture kirilma efekti: yeni BalanceBreakFx.cs (genisleyen halka + 16 isinsal kirik parcasi + ekran parlamasi + "DENGE KIRILDI!" yazisi, unscaled zaman); EnemyHitFeedback.PlayBalanceBreak cagirir. CombatImpactFeedback.PlayBalanceBreakImpact: tam donma >=0.22s + 1.0s SLOW-MO (0.06->1, power 2.2, yavas baslar hizlanir) + shake >=1.6 + punch. (Inspector degerleri alt sinir olarak Mathf.Max ile ezilir.)
- Parry hit-stop: PlayParryImpact = >=0.16s donma + >=0.55s ramp 0.08->1 power 2.6 (daha uzun, zamanla hizlanan).
- Boss posture kirilma can hasari: BlockBreakDamage Boss1Enemy da boss sayilir (bossPercent 0.05) + EnemyBalance.breakHealthPercent 0.15 = toplam ~%20.
- Boss1Enemy: yetenek dongusu (skillRotation): ayni yetenek (Kombo/Ziplama/Dalga) ust uste secilmez; durum degisimi Update`te izlenir (TrackSkill/NoteSkill); kombo hamlesinin cooldown`u kombodan sonra 9999 yapilir, baska yetenek gelince comboCooldown`a doner. Dalga, son dalgadan beri gecen her yetenek icin waveStaleBoost(0.6) kadar daha sik secilir. Dalga: waveSpeed 145 (Configure`da en az 140), waveMinDistance 65, waveInterval 0.8, waveCooldown 2.2, hop hedef 82/maks 58, dalga dogarken "DALGA!" yazisi + her atista sarsinti; dalga oyuncuya en az ~0.3s`de ulasir (yakinsa bossun onunde dogar). Dalga BLOKLANAMAZ: blok acikken bile blok iptal edilir ve hasar (PlayerHitKind.Unblockable) verir, sadece parry kurtarir. postureBreakMercy 0 (kapali; Configure sifirlar). bossHitPercent 0.34: boss isabeti oyuncunun max canin %34`u (3 hatada olum) - PlayerDamageReceiver.BossHitPercent (source Boss1Enemy ise).
- Oyuncu POSTURE BARI: yeni PlayerPostureBar.cs (otomatik kurulur, OnGUI), execute (RageMeter) barinin hemen altinda TURUNCU. PlayerPosture.BlockLocked: posture kirilinca bar tamamen dolana kadar blok VE parry yapilamaz (PlayerDefenseController.CanStartDefense). Kilitliyken dolum: brokenRecoveryDelay 0.8s, brokenRecoverySpeed 65/s (~3.9s toplam); normal dolum 25/s degismedi. Kilitliyken bar kirmizi-turuncu yanip soner.
- EnemyAttackState: 4+ vurusluk hamlenin SON vurusu (Boss1 haric) block/parry/hasar fark etmez oyuncuyu 0.75s`de yavasca ~4.8 birim iter (pushKb 32; denge kirildi/stagger ise itme yok). Oyuncunun posture`u kirik (IsBroken) iken menzil kontrolu 2.5x genisler (min 9 birim): savrulsa bile kombo vuruslari inmeye devam eder.
- Dosyalar: BalanceBreakFx(YENI), PlayerPostureBar(YENI), ParryMark, BlockBreakDamage, CombatImpactFeedback, EnemyHitFeedback, PlayerDamageReceiver, PlayerPosture, PlayerDefenseController, EnemyAttackState, Boss1Enemy.

## (2026-10-17, 37) Parry yavaslamasi sadece dusmanlar icin (DERLENMEDI / OYNANMADI)
- CombatImpactFeedback.PlayParryImpact: global HitStop (Time.timeScale) kaldirildi; yerine EnemyTime.RequestRamp(>=0.75s, baslangic 0.05, power 2.6). Oyuncu parry sirasinda normal hizda; dusman/boss/dalga (EnemyTime kullananlar) yavas baslayip hizlanarak normale doner. Balance-break slow-mo hala global (ayri istek). EnemyController.PlayParrySlowMotion profili ayrica calisir (en yavas olan gecerli).

## (2026-10-17, 38) Hasar sonrasi dokunulmazlik kaldirildi (DERLENMEDI / OYNANMADI)
- PlayerHurtState.Enter: hitInvincibilityTimer artik atanmiyor (static GrantPostHitInvincibility=false, geri acmak icin true). Sersemleme (lockDuration) ve knockback aynen duruyor. Diger kaynaklar (hava saldirisi 0.12s, parry odulu, ability, rage cikisi 0.35s, dash i-frame) DOKUNULMADI. Dikkat: sersemleme sirasinda ardisik kombo vuruslari artik isabet eder.

## (2026-10-17, 39) Hitbox / collider denetimi + duzeltmeler (DERLENMEDI / OYNANMADI)
Prefab verileri (Boss1.prefab, Player.prefab, Physics2DSettings): Boss1 kok scale 2, TEK BoxCollider2D offset(0,3.2) size(4.6,6.4) -> dunya 9.2x12.8; Player scale 1.5, CapsuleCollider2D dikey 2.29x3.17 -> dunya ~3.4x4.8; oyuncu saldiri kutusu hitBoxSize 10x4 (merkez AttackPoint 0.72*1.5), on erisim ~6.1; dashDistance 5; ziplama tepe ~4.2 (jumpForce 17, gravity 3.5). Katman matrisi: Dash katmani Enemy ile CARPISMIYOR (dash dusmanin icinden gecer), Player-Enemy carpisir.
Bulgular/duzeltmeler:
1. Dash (5 birim) 9.2 genis boss govdesinin icinde bitince layer geri donunce oyuncu cakisik kaliyor, fizik yavas/zipirdayarak itiyordu. FIX: yeni HitboxUtil.cs (SeparateHorizontally/EjectFromEnemies); DashState.Exit layer geri yuklendikten sonra EjectFromEnemies cagirir (en yakin yatay kenara aninda).
2. Boss ziplama inisinde boss collider`i oyuncunun ustune acilabiliyordu (kutle 1000 -> oyuncu sikisip firliyordu). FIX: Boss1LeapState inis hasarindan sonra HitboxUtil.SeparateHorizontally(oyuncu, boss collider).
3. Dalga carpismasi her karede sadece o anki x`e bakiyordu: 145 u/s hizda kare takilmasinda (100ms = 14.5 birim) dalga oyuncunun icinden "atlayip" gecebilirdi. FIX: supurmeli kontrol (onceki x -> simdiki x araligi) + pencereye oyuncu govde yaricapinin %40`i eklendi.
Bilgi (degistirilmedi): wave yuksekligi 9 ve ziplama tepe ~4.2 -> dalga/ziplama inisinden ziplayarak KACILMAZ (dash i-frame veya parry); Boss1 vurusu merkezden 24 birime kadar ve alt sinir yok (boss govdesine yapisan oyuncu da vurulur); boss stopDistance 15 vs oyuncu erisimi ~10.7 (boss govde kenari dahil) -> oyuncu 4 birim yaklasmali.
