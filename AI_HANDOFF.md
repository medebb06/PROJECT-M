# PROJECT M — TASK HANDOFF

## Ağaç sprite'ları (2026-10-19)
- `Tree01` sahneye eklendi (Assets/Sprites/Tree: Tree_Sharp kullanımda, Tree_Soft ve Tree_Rim yedek/ay kenarı). Stil ve süreç AI_CONTEXT.md > 'Art pipeline' bölümünde; sahnedeki tüm ağaçlar bu stile çekilecek.
- Ağaç materyali `M_Tree_Coloured` (SilhouetteRim): gece rengi ve rüzgâr sallantısı diğer ağaçlarla aynı shaderdan gelir. `TreeSway.cs` ve `~moonrim` bu ağaçtan kaldırıldı. Gece görüntüsü doğrulandı; gündüz/akşam saatleri ekranda doğrulanamadı (SetHour görüntüye yansımadı).
- Ağacın konumu/ölçeği kullanıcı tarafından elle ayarlanıyor; bunlara dokunma.
- Çadır: `Tent` (kırmızı bez), `TentLife.cs` (sallantı SADECE Play modunda + sarı fener Light2D; sahnede edit modunda hareket etmez). `PlayOnlyWind.cs` kullanılmıyor (silinebilir). Eski `TentSway.cs` sahneden kaldırıldı, dosya duruyor.

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

## (2026-10-17, 40) Boss1: yeni saldirilar, ogrenilebilir dongu, faz 2, vurus agirligi, cezalandirma (DERLENMEDI / OYNANMADI)
- YENI HAMLELER (BuildComboMoveset artik 4 hamle kurar, EnemyMoveset uzerinden): "Boss1 Supurme" (MoveHitType.Sweep, uyari sweepWindup 1.15s): yerde yayilan alcak sok dalgasi, ZIPLA (dash korumaz; sweepHitHeight 2, sweepReachMultiplier 1 -> 24 birim; moveset.sweepIgnoresDash=true); ziplayarak gecince oyuncuya karsi vurus penceresi (counter.OpenJumpWindow) acilir. Gorsel: yeni Boss1Shock (sok cizgisi). "Boss1 Yakalama" (MoveHitType.Grab, grabWindup 1.35s, hazirlikta grabAdvance 26 ile ilerler): parry/blok islemez, DASH ile kacilir (kusursuz dash karsi vurus: UnblockableCounter). Vurusta BalanceBreakFx(label=null) halkasi. "Boss1 Aldatmaca" (Normal, fakeWindup 0.55 + rastgele 0..0.8s, parry odulu 1.6): ezbere parry`i cezalandirir. Danger label/indicator (ZIPLA!/DASH!) EnemyAttackState`ten otomatik. Configure: unblockableFrontOnly/ForwardReach(attackReach*0.95)/HitHeight(hitHeight+1).
- YETENEK DONGUSU (skillRotation): rastgele yerine OGRENILEBILIR sira. Faz1: Melee,Wave,Combo,Sweep,Wave,Melee,Grab,Leap; Faz2: Wave,Combo,Sweep,Wave,Grab,Combo,Leap,Wave. Siradaki yetenek `Expected`; SkillBlocked(s)= s != Expected. Moveset.weightModifier=MoveWeight (sadece siradaki hamle 1000, baska 0; aldatmaca Melee sirasinda fakeChance 0.45 ile). Siradaki wave/leap boss yakin vurusa girmisken pendingWave/pendingLeap ile vurusun yerine baslatilir. Siradaki yetenek patternPatience (9s) icinde gerceklesmezse atlanir. Durum degisimi TrackSkill/NoteSkill ile izlenir (expectedSince Awake/EndIntro/uyanista sifirlanir).
- FAZ 2 (can <= %50, phase2HealthPercent): boss Chase durumundayken BeginPhase2: yuruyus x1.2, waveCount 5, waveInterval<=0.7, waveCooldown x0.7, leapCooldown x0.8, rig windup/tension x0.88 recover x0.85, hamle uyarilari x0.85 (moveset yeniden kurulur), yeni dongu; Boss1RoarState: phase2RoarTime (1.7s) hareketsiz kukreme + "OFKE!" yazisi + halka + sarsinti, bu sure acik an (oyuncu icin firsat).
- VURUS AGIRLIGI (hitWeight): oyuncunun can vurusunda boss EnemyTime.RequestRamp(0.1s, 0.3) ile kisa "takilir" (oyuncu yavaslamaz), kod ile uretilen alcak tok ses (hitThudVolume 0.55), +kamera sarsintisi, flinch x1.3.
- CEZALANDIRMA (punishHitPercent 0.01 = max canin %1): boss toparlanirken (enemy.openUntil > EnemyTime.Now; saldiri sonrasi, ziplama inisi sonrasi, dalga sonrasi 0.8s, kukreme) duz vurus basina ek can hasari -> saldiri aralarina girmek odullu. Denge/stagger kurallari ayni.
- Savas suresi tahmini (oynanmadi): 6000 can; kirilma %20 (1200) + infaz %12 (720) ~ 3 dongu, her kirilma ~8 parry (maxBalance 240, parry 60*0.5) -> iyi oyunda ~3-4 dk. Cok uzun/kisa gelirse maxBalance, parryBalanceDamage, executeHealthPercent, punishHitPercent ile oyna.
- Dosyalar: Boss1Enemy.cs, BalanceBreakFx.cs (Play(pos, label=...) ; label null = yazisiz).

## (2026-10-17, 41) Boss geri ziplamasi haritadan dusmesin (DERLENMEDI / OYNANMADI)
- Boss1WaveState.ComputeHop: ziplama yolu her 2 birimde asagi RaycastAll ile taranir (GroundAt: trigger olmayan, dinamik olmayan, bossun cocugu olmayan collider); zemin biten yerde ziplama durur (kenardan 3 birim pay); 6 birimden kisaysa ziplama olmaz, boss oldugu yerden dalga atar. Inis yuksekligi landY zeminden okunur (yamuk zeminde de dogru yere iner), ziplama egrisi groundY->landY interpolasyonlu.

## (2026-10-17, 42) Yeni boss saldirilari icin ayri animasyon stilleri (DERLENMEDI / OYNANMADI)
- Boss1Rig: AttackStyle {Slam, Sweep, Grab, Fake} + serilestirilebilir Pose struct (chest, belly, head, arm, sword, armL, crouch). Yeni PlayAttack(contactSeconds, slowRaise, style). Poz hesabi artik wp/sp (windup/strike) yapisindan okunur; Slam klasik windup*/strike* alanlarini kullanir (degismedi). Stil PlayAttack() ile Slam`e sifirlanir.
- SUPURME: hazirlik = cok geri egilir, kol alcakta (-30), kilic geride, derin cokme (0.10); darbe = govde one atilir (chest 34), el yere iner (arm 28, crouch 0.14), kilic YATAY (102; strikeSwordDeg 100 = en yatay) -> alcak, genis savrulma; zemin korumasi bileği kaldirir.
- YAKALAMA: hazirlik = geri yaylanir (chest -28, crouch 0.11), bos sol kol one uzanir (armL +34), kilic asagida/pasif; darbe = govde one firlar (chest 36), sol kol tam uzanir (+40).
- ALDATMACA: Slam pozu + hazirlik titremesi x2.8 (fakeTrembleMul) -> gerilmis kilic titrer, ne zaman ineceği belli olmaz.
- Not: acilar rig`in degerlerine gore tahmin (windup sword 105 / strike 130 yatiklik, armL negatif = geri); gorsel dogrulanmadi. Inspector`da Boss1Rig altinda "Saldiri stilleri" basligindan ayarlanir. Boss1Enemy.OnAttackAnimationRequested stili gecer.

## (2026-10-17, 44) HUD barlari: IMGUI -> overlay Canvas, ekrana gore konumlanma (DERLENMEDI / OYNANMADI)
- SORUN: can / execute / turuncu posture / boss barlari OnGUI (IMGUI) ile cizildigi icin Game view'da Unity UI'nin altinda kaliyordu.
- YENI HudCanvas.cs: kendi Screen Space Overlay Canvas'i (sortingOrder 20000: oyun UI'sinin ustunde, MainMenu 30000+ altinda, DontDestroyOnLoad, kendiliginden kurulur). API: HudCanvas.Fill(guiRect, renk) (IMGUI koordinati, sol-ust orijin), HudCanvas.Scale() (Screen.height/1080*MobileInput.HudMul), HudCanvas.SafeArea() (Screen.safeArea, IMGUI koordinati). Image havuzu; o karede cizilmeyenler Canvas.willRenderCanvases ile gizlenir.
- MinimalHealthBar, RageMeter (sadece bar kismi), PlayerPostureBar, Boss1Hud (sadece barlar; boss ISMI yazisi hala IMGUI) OnGUI yerine LateUpdate'te HudCanvas.Fill kullanir. Konum her karede hesaplanir (cache yok): sol-alt barlar safeArea.sol/alt'a gore, boss bari safeArea ortasi/ustune gore; genislik safe area'ya sigacak sekilde kisilir; sinematik serit ofseti (RunCameraZoom.TopBarPixels) aynen korundu. MainMenu.Active iken barlar cizilmez (health bar icin yeni).
- Not: RunCameraZoom letterbox serit IMGUI kaldi (depth -10000); barlar serit kadar kaydigi icin cakismaz. Serit canvas'in ustunde/altinda cikarsa RunCameraZoom da HudCanvas'a alinabilir.
- Dosyalar: HudCanvas.cs (YENI; Unity .meta'yi kendi uretir), MinimalHealthBar.cs, RageMeter.cs, PlayerPostureBar.cs, Boss1Hud.cs.

## (2026-10-17, 45) Boss1 test tuslari + olumsuz mod (DERLENMEDI / OYNANMADI)
- Sebep: kullanici Yakalama saldirisini goremedi (hizla oluyor / ole). Boss1Enemy.TestKeys() (sadece UNITY_EDITOR / DEVELOPMENT_BUILD, Update basinda): Keypad7 = Yakalama, 8 = Supurme, 9 = Aldatmaca, 4 = Kombo, 5 = Dalga, 6 = Ziplama, 1 = Duz vurus -> o yetenek siradaki (Expected) olur (ForceSkill: patIdx ayarlar, wave/leap cooldown`u sifirlar). Keypad0 = su an beklenen yetenegi KILITLE (surekli ayni yetenek) / kilidi ac. Keypad. (nokta) = OLUMSUZ mod (oyuncu canini her karede doldurur). Console`a [Boss1 TEST] log yazar.
- Not: boss hala mesafe/kosul saglanmadan yetenegi baslatmaz (Yakalama icin oyuncu menzilde olmali); 9 sn icinde olmazsa patternPatience siradakini atlar (kilit modu geri kurar).

- (45 duzeltme) Keypad calismadi (proje yeni Input System kullaniyor) -> UnityEngine.InputSystem.Keyboard ile harf tuslari: Y=Yakalama, U=Supurme, I=Aldatmaca, H=Kombo, J=Dalga, K=Ziplama, N=Duz vurus, L=yetenek kilidi, G=olumsuz mod. Paket/Active Input Handling `Input System`i icermiyorsa derleme hatasi verir (TrainingDummy zaten Keyboard.current kullaniyor).

## (2026-10-17, 46) Boss1: sol kol = OMUZ + KOL + EL, FK ile saldirilara/kukremeye/inise katildi (DERLENMEDI / OYNANMADI)
- PSD (kullanici) esleme: "sol ust kol" 1 = `armL_upper` (omuz blogu, chest kemigine bagli), "sol ust kol" 2 = `legBack` (uzun kol), "sol el" = `legBack_knee` (yumruk; parmak disiyla yere basar). Adlari bacak gibi ama KOL (Boss1Rig.Legs[2]); yuruyuste bastona gibi sallanma (UpdateLegs, rigid) AYNEN korundu.
- YENI Boss1Rig.ApplyArmFk(aArm, aHand, w): omuz eklemi = omuz blogunun (armL_upper) simdiki donusunden hesaplanir (ShoulderJ0 (0.96,4.53), blok merkezi (0.425,5.01), bilek WristW0 (0.74,0.98); PSD pikselinden olculdu, rig birimi) -> kol omuz etrafinda aArm, el bilek etrafinda aHand (mutlak aci) doner; w=0 yuruyus pozu kalir, w=1 FK. Yumruk alt kosesi HandGroundY (0.04) altina inmez (yere girmez). Omuz blogu kolu shoulderFollow (0.3) orani takip eder.
- Alanlar (Inspector "Goril kolu"): slamArmWindup (-52,-20)/slamArmStrike (-4,4), sweepArmWindup (34,22)/sweepArmStrike (-38,-8), grabArmWindup (-100,-40)/grabArmStrike (-84,-8), beatArmBase (-72,-30)/beatArmSwing (48,20)/gorillaBeatCount 5, poundArm (-50,-15). (kol, el) derece; NEGATIF = one/yukari (boss sola bakar), pozitif = geri. Yakalama parlamasi omuz+kol+eli boyar. BeatChest(sn) (giris + faz 2 kukremesi), PoundGround() (ziplama inisi) Boss1Enemy'den.
- Not: eklem noktalari ve aci isaretleri tahmin; gorsel dogrulanmadi. Kol yanlis noktadan donuyorsa ShoulderJ0/WristW0 sabitleri ayarlanir.
- Dosyalar: Boss1Rig.cs, Boss1Enemy.cs.

## (2026-10-17, 47) Boss1 Yakalama: Sekiro dev kavrama tarzi (hedefle + hizli baslayip yavaslayan kosu) (DERLENMEDI / OYNANMADI)
- MoveHit.Charge(focusFraction, maxSpeed) / chargeFocus (EnemyMoveset.cs): uyarinin ilk focusFraction kismi YERINDE HEDEF (EnemyAttackState.ChargeTowardTarget: her karede enemy.RetargetFacing, boss durur), kalani oyuncunun uzerine kosu: hiz v = v0*(1-q)^1.7 (hizli baslar, yavaslar). v0, kosu basladigi andaki oyuncu mesafesine gore hesaplanir (mesafe = v0*T/2.7 -> kosunun sonunda oyuncuya varir; azami advanceSpeed ile sinirli). Yon kosu basinda sabitlenir; oyuncu kosu basladiktan sonra dash/kacarsa bos gider. Eski Advance(sabit hiz) davranisi degismedi (chargeFocus 0).
- EnemyAttackState: Grab icin animasyon gecikmesi 0 (kol uzatma uyarinin basindan baslar; digerlerinde engellenemez vuruslar gecikmeli kalir). chargeInit her BeginWindup'ta sifirlanir.
- Boss1Enemy: yeni grabFocus (0.55), grabMove .Charge(grabFocus, max(grabAdvance*3, 80)). grabWindup 1.35 -> ~0.74 sn hedef, ~0.61 sn kosu. Dash ile kacma/karsi vurus kurali ayni.
- Boss1Rig: Yakalama pozu: kol one tam uzanir (grabArmWindup/Strike (-92,-6)), govde hafif geri yaylanir sonra one atilir (grabWindupPose/StrikePose guncellendi). NOT: prefab onceki Pose/Vector2 degerlerini serilestirdiyse yeni varsayilanlar gecmez -> Inspector'da Boss1Rig'de ilgili alanlari Reset edin.
- Dosyalar: EnemyMoveset.cs, EnemyAttackState.cs, Boss1Enemy.cs, Boss1Rig.cs.

- (47 teshis) EnemyAttackState.ChargeTowardTarget kosu basinda Console'a `[Yakalama] kosu basladi: mesafe/bosluk/v0/T` yazar. Log yoksa yeni kod calismiyor (derleme/yeni Unity'ye alinmamis); bosluk ~0 ise oyuncu boss govdesine cok yakin (kosacak yer yok); v0 > 0 ama boss kimildamiyorsa baska kod hizi eziyor.

## (2026-10-17, 48) Yakalama: yakindaysan geri acilip kosu, azami hiz 110 (DERLENMEDI / OYNANMADI)
- Console (kullanici): kosu basladi mesafe=7,5 bosluk=0,0 v0=0 -> oyuncu boss govdesine yakinken (stopDistance = 4,6+1,7+0,4 = ~6,7) kosacak yer yoktu; mesafe 14,8 bosluk 7,3 v0=32 olunca kosu/vurus calisti. (HudCanvas logu da geldi: ekran 1920x888, ilk bar rect y=855,x=23 -> canvas kuruldu.)
- COZUM: MoveHit.chargeRunUp (Charge(focus, maxSpeed, runUp=12)); hedefleme fazinda (p < focus) aralik < stopDistance + runUp ise boss oyuncudan uzaga GERI cekilir (hiz = eksik mesafe / kalan hedefleme suresi, <= 34), arkasinda zemin yoksa (RaycastAll 8 birim asagi) cekilmez (ucurumdan dusmesin). Kosu mesafesi artik koşu basinda olculur: bosluk = mesafe - stopDistance. Boss1Enemy: grabRunUp (12), azami kosu hizi max(grabAdvance*4, 110).
- Dosyalar: EnemyMoveset.cs, EnemyAttackState.cs, Boss1Enemy.cs.

## (2026-10-17, 51) Yakalama/Supurme menzile girmeden, uzaktan baslar (DERLENMEDI / OYNANMADI)
- Sorun: boss normal vurus menziline (attackRange / chaseStopDistance 15) girmeden saldiri baslatmiyordu, o yuzden yetenekler hep oyuncunun dibinde atiliyor, boss sabit duruyordu; oyuncu kacinca yuruyup yanina gelip tekrar ediyordu.
- Boss1Enemy.Update: siradaki yetenek (Expected) Yakalama veya Supurme ise, Chase durumunda ve oyuncu attackRange disinda, farSkillMaxDistance (28) icindeyse saniyede farSkillChancePerSecond (3) sansla EnemyAttackState baslatilir (TryRequestAttack + CanAttack). Hamle secilemezse (PickMove) Chase'e doner, 0,6 sn tekrar denemez. Yakalamada kosu mesafesi zaten oyuncuya gore hesaplanir (azami hiz 110 ~ 25 birim). Test: G + Y + L ile uzaktan baslamali.
- Dosya: Boss1Enemy.cs.

## (2026-10-17, 52) Yakalama: yururken/kosarak (kombo gibi), asil hata: LateUpdate hizi sifirliyordu (DERLENMEDI / OYNANMADI)
- ASIL HATA: Boss1Enemy.LateUpdate saldiri animasyonu (rig.IsAttacking) sirasinda boss hizini HER KARE sifirliyordu (sadece Kombo kosusu istisnaydi). EnemyAttackState'in Yakalama icin Update'te verdigi hiz (kosu / geri acilma) fizik adimindan once sifirlaniyordu -> boss hic hareket etmedi (log: v0 > 0 olsa da). FIX: st.MoveName == GrabName iken LateUpdate hizi sifirlamaz; rig.RunWhileAttacking = |vx| > 0.5 (bacaklar yurur).
- EnemyAttackState.ChargeTowardTarget hedefleme fazi: uzaktaysa (mesafe > stopDistance + chargeRunUp + 1) YURUYEREK/KOSARAK yaklasir (hiz min(30, advanceSpeed*0.3)), yakindaysa geri acilir, arada durur; sonra hizli baslayip yavaslayan kosu.
- Dosyalar: EnemyAttackState.cs, Boss1Enemy.cs.
- (52 duzeltme 2) Kullanici: "odaklanirken yerinde bekliyor". Neden: hedefleme fazinda geri acilma esigi (have < stop+runUp) ile yaklasma esigi (have > stop+runUp+1) arasinda OLU BOLGE vardi (runUp 12: mesafe 14,8 iki esik arasinda -> hic hareket yok). Simdi: want = stopDistance + chargeRunUp*0.5; have < want -> geri ac, have > want + 1.5 -> kosarak yaklas (olu bolge sadece 1,5 birim). grabRunUp varsayilan 5 (eski serilestirilmis 12 de calisir). Dosyalar: EnemyAttackState.cs, Boss1Enemy.cs.
- (52 duzeltme 3) Yakalama hedeflemede GERI ACILMA iptal edildi (kullanici: ustune kosunca geri kaciyordu). Yakindaysa boss yerinde durur/hedefler, uzaktaysa kosarak yaklasir. grabRunUp/chargeRunUp artik sadece "ne kadar yaklasinca dur" mesafesini belirler. Dosya: EnemyAttackState.cs.

## (2026-10-17, 53) Yakalama saldirisi donguden gecici olarak CIKARILDI (DERLENMEDI / OYNANMADI)
- Kullanici istegi: "yakalama saldirisini kaldir, sonra yapariz". Boss1Enemy Pattern1/Pattern2'de Skill.Grab yerine Skill.Melee kondu; Grab artik hicbir zaman "Expected" olmaz -> MoveWeight grabMove'u 0 verir, uzaktan baslatma (Expected == Grab) tetiklenmez, test tusu Y etkisiz. Yakalama kodu (grabMove, Charge/chargeFocus, ChargeTowardTarget, kol FK, LateUpdate Grab istisnasi) SILINMEDI: tekrar acmak icin Pattern dizilerine Skill.Grab geri konur. Bilinen eksikler: yakinda atilis cok kisa; koşu/hedefleme gorselde dogrulanmadi.
- Dosya: Boss1Enemy.cs.

## (2026-10-17, 54) Yakalama (Grab) tamamen silindi + kullanilmayan alanlar temizlendi (DERLENMEDI / OYNANMADI)
- Kullanici istegi: kullanilmayan ozellikleri / grab yetenegini sil. Silinen: Boss1Enemy (Skill.Grab, grabMove/GrabName, grabWindup/grabAdvance/grabFocus/grabRunUp, MoveWeight/TrackSkill/OnStrikeLanded/OnAttackAnimationRequested/LateUpdate'teki grab dallari, test tusu Y, uzaktan baslatmanin Grab kismi; kullanilmayan leapAirTime, waveBlockPostureMultiplier, roarSource). grabColor -> fakeColor (Aldatmaca ipucu rengi). Boss1Rig: AttackStyle.Grab, grabWindupPose/grabStrikePose/grabArmGlow/grabArmWindup/Strike, UpdateArmGlow (parlama). EnemyAttackState: ChargeTowardTarget/chargeInit/ChargeV, animationDelay Grab istisnasi. EnemyMoveset: MoveHit.chargeFocus/chargeRunUp/Charge(). KORUNAN: MoveHitType.Grab (diger dusmanlar kullaniyor), uzaktan Supurme baslatma (farSkill*), kol FK (omuz/kol/el), Boss1 kukreme gogus vurusu.
- Boss1Enemy 2629 -> 2600 satir: kalan buyuk kisim aktif yeteneklerin kendi siniflari (Boss1LeapState ~310, Boss1WaveState ~240, Boss1Wave ~190, Boss1SlowMo, Boss1Shock, Boss1RoarState). Ayri dosyalara bolmek (partial class) sonraki adim olabilir.
- Dosyalar: Boss1Enemy.cs, Boss1Rig.cs, EnemyAttackState.cs, EnemyMoveset.cs.

### 55 - Proje geneli kullanilmayan icerik analizi (SADECE ANALIZ, kod degismedi) (DERLENMEDI / OYNANMADI)
- Kullanici istegi: kullanilmayan/artik kullanilmayacak seyleri (level generator, jungle, tilsimlar vb.) sil. Kopru bbos silemedigi icin once referans analizi yapildi (script tip adlari + MyScene2/4 prefab GUID + RuntimeInitializeOnLoadMethod kokleri, erisilebilirlik).
- Level generator / jungle / tilsim (charm) sistemleri ZATEN silinmis (Phase1Stubs.cs 'Silinen Run/Level/Meta/Charm'). Geriye sadece kopru siniflari kaldi: LevelProps, MetaProgress, RunUI, RunManager, CharmUtil (Phase1Stubs.cs) - AirAttackState, AttackState, GroundSlamState, ExecuteMeter, PlayerAbility, BossController, EnemyBomber hala cagiriyor; silmek icin o cagrilarin once temizlenmesi gerek.
- KESIN YETIM (hicbir yerde referans yok, sahnede/prefab'ta yok, bootstrap yok) -> Unity'de silinebilir: AutoDestroyByAnimation, BossShadow, CameraZoom, Combatdebuglogger, DustA, DustFX, EnemyPosture, ForegroundSway, HitType, SimpleSpriteVFX (.cs + .meta). Ayrica EnemyAttackState.cs.bak, BossLog.txt, Claude outputs/MountainBackdrop.cs (93 byte stub).
- DOLASIK (kod baglantili, silmeden once kod duzenlemesi gerekir): eski boss sistemi BossController/BossBrain/BossAura/BossLeap/BossNova/BossSlam/BossSkillGate/BossStats (~20 cagri yeri: EnemyController, EnemyExecuteState, ExecuteMeter, PlayerFinisher, PlayerDamage, RageMeter, GroundSlamState, ...); eski dusman tipleri EnemyArcher/Bomber/Flyer/Shield/Shape/Projectile (EnemyArchetype, EnemyAttackState, AirAttackState, PlayerDamage uzerinden); PlayerAbility/AbilityTypes/PlayerWeapon/WeaponTypes; Phase1Stubs.
- NOT: HudCanvas, MinimalHealthBar, PlayerPostureBar, HealthOrbs, ExecuteReadyIndicator, BlockBreakDamage, Parryriposte, ThemeMusic kendi kendini baslatiyor (RuntimeInitializeOnLoadMethod) - SILINMEMELI.
- Dosya degisikligi yok.

### 56 - Eski boss sistemi / eski dusman tipleri / PlayerAbility-Weapon / Phase1Stubs baglantilarinin temizligi (DERLENMEDI / OYNANMADI)
- Kullanici istegi: eski BossController sistemi, EnemyArcher/Bomber/Flyer/Shield/Shape/Projectile, PlayerAbility/AbilityTypes/PlayerWeapon/WeaponTypes ve Phase1Stubs silinsin. Kopru dosya silemedigi icin SADECE cagiran satirlar temizlendi; asagidaki dosyalari Unity'de (.meta ile) kullanici silecek.
- SILINECEK (kullanici): BossController, BossBrain, BossAura, BossLeap, BossNova, BossSlam, BossSkillGate, BossStats, EnemyArcher, EnemyBomber, EnemyFlyer, EnemyShield, EnemyShape, EnemyProjectile, AbilityTypes, PlayerAbility, PlayerWeapon, WeaponTypes, Phase1Stubs (+ entry 55'teki yetim listesi).
- Duzenlenen dosyalar: AirAttackState, AttackState (LevelProps/PlayerWeapon/mermi kaldirildi), BlockBreakDamage, ParryMark, EnemyArchetype (Archer/Shield/Flyer/Bomber bilesen ekleme + EnemyShape kaldirildi; enum degerleri duruyor), EnemyAttackState (IsBossEnemy=false, FireShot silindi: MoveHitType.Shot vurusu artik bos gecer, moveset verisi duruyor), EnemyController, EnemyExecuteState, Enemyoverheadbars, ExecuteMeter, ExecuteReadyIndicator, GroundSlamState (CharmUtil yerine yerel EnemiesInRadius), PlayerCombatController (EffectiveHitBox=hitBoxSize, silah carpani yok), PlayerDamage, PlayerFinisher, RageMeter, EnemyStatus.
- DAVRANIS NOTU: BossController hic sahnede/prefab'ta yoktu; onun kontrolleri olan yerler Boss1Enemy'ye cevrildi (GroundSlam sabit denge hasari + zirh delme istisnasi, yanik hasari yari, ExecuteMeter boss dolumu, RageMeter 'lethal' degil, BlockBreak). IsBossEnemy (EnemyAttackState) ve EnemyController.isBossEnemy: false (eskiyle ayni).
- Kalan sadece yorum/tooltip metinlerinde RunManager/RunUI adi gecer. UnblockableCounter korundu.

## Animasyon akiciligi incelemesi - duzeltme 1 (2026-10-17)
- Kapsam: once SADECE inceleme yapildi (rapor kullaniciya sunuldu), sonra kullanici "tek tek yap" dedi. Bu girdi ilk duzeltmedir.
- Degisiklik: Assets/Animations/PlayerAnimator.controller, Attack3 -> Idle gecisi (id -6977304799891269468): m_ExitTime 0 -> 1. Baska hicbir satir degismedi (diff ile dogrulandi: tek satir).
- Neden: Attack3->Idle gecisi Exit Time 0 ve kosulsuzdu; Attack1/2/4 klip sonunu beklerken Attack3 hemen Idle'a fade oluyordu (Attack2 ile ayni deger: exit 1). 
- DURUM: UYGULANDI, Play Mode'da TEST EDILMEDI. Unity'de 1-2-3-4 kombosu denenmeli.
- Geri alma: ayni gecisin Exit Time degerini 0 yap (Animator penceresinde Attack3 -> Idle oku, Has Exit Time acik, Exit Time 0) veya Git ile PlayerAnimator.controller'i geri al (Git durumu bu oturumda goruntulenemedi).
- Siradaki (onay bekliyor, sirayla): 2) AirState/JumpState ApplyMovement Update -> FixedUpdate; sonra bulgular 3-9 (olcum gerektirenler once Play Mode gozlemi).
- Commit/push yapilmadi.

## Animasyon akiciligi incelemesi - duzeltme 2 (2026-10-17)
- Degisiklik: Assets/Scripts/AirState.cs ve JumpState.cs. `movement.ApplyMovement(movement.airControl)` cagrisi Update()'ten kaldirildi, FixedUpdate()'e tasindi (PlayerController.FixedUpdate -> stateMachine.FixedUpdate -> CurrentState.FixedUpdate zinciri dogrulandi).
- Neden: PlayerMovement.ApplyMovement Time.fixedDeltaTime ile ivme uyguluyor; Update'te her karede cagrilinca hava kontrolu kare hizina bagliydi (GroundedState zaten FixedUpdate'te).
- Dokunulmayanlar: GroundSlam/Dash/Land/WallSlide gecis kontrolleri Update'te kaldi; WallJumpState/WallSlideState degismedi. Update'teki bos kalan `PlayerMovement movement` yerel degiskeni (AirState) birakildi (derleme hatasi/uyarisi olmamali).
- DURUM: UYGULANDI, Unity'de derleme ve Play Mode'da TEST EDILMEDI.
- Beklenen yan etki: havada yon degistirme hissi degisir (60 FPS'te daha yumusak/yavas, yuksek FPS'te daha az sert). airControl degeri yeniden ayar isteyebilir.
- Geri alma: iki dosyada ApplyMovement cagrisini FixedUpdate'ten silip Update() basina (ground slam blogundan once) geri koy, ya da Git ile iki dosyayi geri al.
- Siradaki (onay bekliyor): bulgu 3-9.

## Animasyon akiciligi incelemesi - duzeltme 3: dash "balon" egrisi + cikis hizi (2026-10-17)
- Kullanici geri bildirimi: dash hizli firlasin, sona dogru cok yavaslasin (iki kez daha yavaslatildi).
- Degisiklik (DashState.cs): hiz egrisi simetrik can yerine v = ortalama*(m+1)*(1-p)^m*SmoothStep(p/0.04); yeni `DashDecayPower` = 3.5 (once 3). Sure (0.33 s) ve mesafe ayni. `DashBellPower`/`Gamma()` kullanilmiyor ama birakildi.
- Ek: DashState.Exit cikis hizi dash DOGAL bitince moveSpeed*0.2 (`DashExitSpeedNatural`); iptalle (ziplama/saldiri/parry) bitince eskisi gibi 0.9 (`DashExitSpeedCancel`). Neden: eskiden dash yavaslayip bitince hiz aninda 0.9*moveSpeed'e ziplardi. Yeni alan `endedByCancel`.
- UYARI: Bu oturumda DashState.cs ve bu dosya cihazda birkac kez eski haline dondu (gecikmeli yazma / editorde acik eski kopya). Visual Studio'da dosya acikken "reload" sec; degisiklikler bu girdiye gore dogrulanmali (DashDecayPower 3.5, DashExitSpeedNatural var mi).
- DURUM: UYGULANDI, Play Mode'da TEST EDILMEDI.
- Geri alma: curve'u eski formule (n=DashBellPower; norm=Gamma(2n+2)/Gamma(n+1)^2; curve=norm*p^n*(1-p)^n) ve Exit'te `0.9f` carpanina dondur.

## Animasyon akiciligi incelemesi - bulgu 3, 7 ve 8 kararlari (2026-10-17)
- Bulgu 3 (RonniPlayerBridge order -200 -> gorsel en fazla 1 kare geride): kod DEGISMEDI; duzeltme 3 scriptin (Bridge/SkinAnimator/ClipPlayer) siralamasini birlikte degistirmeyi gerektirir, kazanc 1 kare.
- Bulgu 7 (tamponlarda scaled/unscaled karisikligi): kod DEGISMEDI. Dash/savunma tamponu unscaled, saldiri/ziplama scaled. Sadece yavaslatma sirasinda fark eder; dash tamponundaki yorum bilincli secimi gosteriyor, hangi yonun dogru oldugu Play Mode'suz belli degil.
- Bulgu 8 (AttackState rb.position Update'te): kod DEGISMEDI. FixedUpdate'e tasimak (20 ms adim) 0.117 s'lik saldirida atilma/vurus zamanini kabalastirir; sicrama gozlemlenmedi.
- Siradaki: bulgu 9 (parry/block gorsel sureleri), bulgu 4 (ziplama/inis gorseli) gozlem bekliyor.
- Commit/push yapilmadi.

## Animasyon akiciligi incelemesi - duzeltme 4: saldiri kesilince yanlis poz (2026-10-17)
- Kullanici: hizli saldiri<->dash ve dash<->ziplama/kosu girislerinde bir an yanlis poz cikiyor; saldiri hemen ardindan dash atip birakinca idle'a gecerken yarim saniyeden kisa idle benzeri farkli bir poz gorunuyor.
- Dosya: Assets/Sprites/RonniSkin/Scripts/RonniPlayerBridge.cs
- (a) LateUpdate sonu: saldiri dash/ziplama/kosu ile kesilince son saldiri karesi (frameSR) 0.16 s boyunca o hareketin USTUNDE soluyordu. Simdi `if (!frameWant && frameSR.enabled && (bMode != 0 || player.isDashing))` ise kare aninda gizlenir; durağan idle'a donuste eski solma kalir.
- (b) UpdateCombat, profil dash erken `return`u (satir ~533): `anim.ClearAction()` eklendi. Neden: saldiri sirasinda acilan SetAction("attackN") dash boyunca kapanmiyordu (erken return ClearAction'a ulasmiyordu); dash bitince ActionActive ~0.2 s (actionBlendOut) acik kalip idleNow=false yaparak rigIdle yerine ana rig'i gosteriyordu.
- DURUM: UYGULANDI, Play Mode'da TEST EDILMEDI. Dash<->ziplama/kosu yanlis pozu icin ayri aday var (anim.grounded 1 kare gec -> kosu klibi Restart(0)); henuz DEGISTIRILMEDI.
- UYARI: Bu oturumda cihazdaki dosyalar (DashState.cs, bu dosya, RonniPlayerBridge.cs) birkac kez bir onceki surume dondu; Visual Studio'da acik/eski kopya suphesi. Degisikliklerin yerinde oldugunu dogrula.
- Geri alma: (a) ilk `if`i `if (!frameWant && frameSR != null && frameSR.enabled)` yap ve eklenen blok + `else` kelimesini sil; (b) `{ anim.ClearAction(); return; }` -> `return;`.
- Commit/push yapilmadi.

## Boss1 acik an (Sekiro gibi) - denge kirilinca hasar yok, kirmizi isik, uyari sesi yuvasi (2026-10-17)
- Istek: boss'un dengesi kirilinca can GITMESIN; sadece saldiriya/infaza ACIK olsun, uzerinde kirmizi isik surekli parlayip sonsun; acildigi an calacak uyari sesi (tik) icin yuva olsun. Aciktayken vurdukca can gitmesi zaten vardi (Boss1Enemy.staggerHitPercent 0.03, floor 0.12).
- EnemyBalance.cs: yeni `SetBreakHealthPercent(float)` (kirilinca giden can orani; diger dusmanlar degismedi, varsayilan breakHealthPercent 0.15 duruyor).
- Boss1Enemy.cs: yeni alanlar `postureBreakHealthPercent` (varsayilan 0), `openWarnSound` (AudioClip yuvasi), `openWarnVolume`; Awake'te `balance.SetBreakHealthPercent(postureBreakHealthPercent)`; boss IsStaggered olunca (yukselen kenar) `PlayOpenWarn()` bir kez 2D calar. Ses bos ise bir sey calmaz.
- Boss1Rig.cs: yeni `openGlow` ayarlari (renk, siddet, saniyede parlama sayisi, olcek) + `UpdateOpenGlow()` (LateUpdate'te): Staggered iken govdeyi saran kirmizi yumusak disk (Sprites/Default, alfa), parlayip sonuyor (gercek zaman).
- DURUM: UYGULANDI, DERLENMEDI / OYNANMADI. Ses dosyasi atanmadi (kullanici verecek).
- Geri alma: Boss1Enemy'de `postureBreakHealthPercent` = 0.15; Boss1Rig'de openGlow kapat (Inspector'da isaretini kaldir).

## Boss1 acik an - duzeltme (2026-10-17)
- Boss1Rig: kirmizi isik artik DUZ (flat) daire (duman/yumusak degil). Ayarlar: `openGlowSize` (govde yuksekliginin kati, 0.45), `openGlowYOffset` (0.1), `openGlowIntensity` 0.95. Eski `openGlowScale` kaldirildi.
- Boss1Enemy: `staggerHitPercent` varsayilan 0.03 -> 0.08 (Range 0-0.3); yeni `staggerFirstHitMultiplier` = 2 (boss acildiktan sonraki ILK duz vurus 2x: ~%16 max can; floor `staggerHitFloorPercent` 0.12 ayni). `staggerFirstHitPending` acilis aninda (IsStaggered yukselen kenar) kurulur.
- Not: staggerHitPercent sahnede serilestirilmemis gorunuyor (MyScene2'de aranip bulunmadi); boss baska sahnede/prefabta serilestirilmisse Inspector degeri varsayilani ezer.
- DURUM: UYGULANDI, DERLENMEDI / OYNANMADI.

## Boss1 agir darbe tepkisi (sendeleme + bagirma + isik soner)
- Boss1Enemy.cs: `heavyHitStumbleSpeed` (9). Ilk agir darbede (stagger-hit blogu, `first`): boss oyuncudan uzaga rb hiziyla sendeler (EnemyStaggerState hizi `knockbackDeceleration` ile yavaslatir), `rig.Flinch(3)`, `rig.Squash(0.6)`, `PlayRoar()`, `rig.OpenGlowSpent = true`.
- Boss1Rig.cs: `OpenGlowSpent` (NonSerialized) kirmizi isigi kapatir; yeni acik anda (Boss1Enemy rising edge) sifirlanir. `openGlowSize` 0.16 -> 0.09.
- Durum: yazildi, Unity'de derlenmedi/test edilmedi. Inspector'daki kayitli openGlowSize eski degeri koruyabilir.
- Geri alma: yukaridaki alanlari/blogu sil, openGlowSize'i 0.16 yap.

## Boss1 olumcul darbe = can cubugu sifirlanir, kukrerken dolar (Sekiro mantigi)
- Boss1Enemy.cs: faz 1'de ilk agir darbe `DeadlyBlowPhase2()` coroutine'ini baslatir: `Health.MinHealth=1` kilidi, can 1'e iner (olmez), `heavyHitSlowDuration` kadar (sendeleme+yavas cekim) bekler, `BeginPhase2()` (kukreme) cagrilir, `phase2RefillTime` (2.2 sn, gercek zaman) boyunca can SetHealth ile tam dolar, MinHealth eski degere doner. `phase2Pending` dogal %50 faz tetigini bu sirada engeller.
- Dogal faz 2 tetigi (can <= phase2HealthPercent, sersemlemeden) yedek olarak duruyor.
- Durum: yazildi, derlenmedi/test edilmedi.
- Geri alma: `DeadlyBlowPhase2` coroutine'ini ve cagrisini sil, eski `TakeDamage(toPhase)` mantigina don.

## Boss1 can ile oldurme (kacip vurma) + can=0 faz gecisi
- Sorun: duz vurus balance'a gider, can sadece `PlayerDamage.PostureToHealthRatio` (varsayilan 0) ile asinir -> posture kirmadan boss'a can hasari yoktu.
- EnemyBalance.cs: `chipRatioOverride` (NonSerialized, -1 = kapali). PlayerDamage.cs: chip orani olarak `balance.chipRatioOverride >= 0 ? override : PostureToHealthRatio`.
- Boss1Enemy.cs: `hitChipHealthRatio` (0.3: tum posture cubugu dolacak kadar vurus = max canin %30'u; faz 2'de posture olcegiyle carpilir). Awake'te `health.MinHealth = 1` (faz 1 kilidi). Update: can <= 1 ise `DeadlyBlowPhase2(0.4f)` -> faz 2 + can dolumu, sonra MinHealth = 0 (faz 2'de can 0 = olum). `phase2AtHalfHealth` (false) eski %50 tetigini kapatir. Posture'un cana gore hasar carpani zaten EnemyBalance.scaleByHealth ile var (0.6 -> 1.6).
- Durum: yazildi, derlenmedi/test edilmedi. Geri alma: hitChipHealthRatio = 0, phase2AtHalfHealth = true.

## Boss can azaltma + 4. vurus agirligi
- Boss1Enemy.cs: `maxHealth` 6000 -> 3000, `hitChipHealthRatio` 0.3 -> 0.5 (kacip vurma hizlansin; chip orani max cana yuzdelik oldugu icin asil kaldirac bu).
- PlayerDamage.cs: `FinalHitDamageMultiplier` = 1.6 (static, ResetStatics'te sifirlanir). `comboStep >= 4` Attack vurusunda denge hasari (sourceMultiplier) ve can hasari bununla carpilir.
- CombatImpactFeedback.cs: step >= 4 icin tabanlar: `finalHitMinFreeze` 0.13, `finalHitMinSlow` 0.1, `finalHitMinShake` 0.6 (dizilerdeki eski A4 degerleri bunun altindaysa yukselir; uzerindeyse korunur).
- Durum: yazildi, derlenmedi/test edilmedi. Havada saldiri (AirAttackState) bu yolu kullanmiyor, etkilenmedi. Sahnede kayitli maxHealth/chip degerleri ezebilir.
- Geri alma: maxHealth 6000, chip 0.3, FinalHitDamageMultiplier 1, finalHitMin* 0.

## Boss posture kilidi (can %50 ustunde) + iksir penceresi + iksir noktalari
- EnemyBalance.cs: `breakLockBelowHealth`, `breakLockCapFraction`, `lastOverflow`, `IsBreakLocked`. Kilitliyken (boss cani > esik) `AddBalanceDamage` cubugu `breakLockCapFraction` * max'ta tutar, sigmayan kisim `lastOverflow`. Yani posture kirilmaz.
- PlayerDamage.cs: chip can ilerlemesi `(balance delta + lastOverflow) / MaxBalance` (kilitliyken de duz vurus can goturur).
- Boss1Enemy.cs: `postureLockAboveHealth` 0.5, `postureLockCap` 0.75 (Awake'te balance'a verilir). Iksir penceresi: `potionWindowAfterCombo` 1.5, `potionWindowAfterLeap` 1.3, `potionWindowBelowHealth` 0.75. Bitirici kombo vurusu (OnStrikeLanded) ve ziplama-ezme cikisi (Leap Exit) `potionWindowPending` kurar; Update'te saldiri bitince, oyuncu cani < %75 ve iksir hakki varsa `enemy.StartAttackRecovery(sure)` + wave/leap/combo bekleme ileri alinir. Faz 2'de pencere x0.7.
- PlayerPotion.cs (mevcut sistem: F basili tut, 3 hak, 1.1 sn, %40 can): can cubugunun saginda 3 kirmizi nokta (`LateUpdate`, HudCanvas.Fill). Dolu = hak var, soluk = harcandi, icerken son nokta titrer. `MinimalHealthBar.LastRect/HasRect` eklendi.
- Durum: yazildi, derlenmedi/test edilmedi. Geri alma: postureLockAboveHealth = 0, potionWindow* = 0, nokta cizimi icin PlayerPotion.LateUpdate'i sil.


## 2026-10-18 - Hareket v2 (ziplama / hava kontrolu / duvar) + Tutorial_Arena
- PlayerMovement.cs: ziplama artik jumpHeight + timeToApex ile tanimli (v0=2h/t, g=2h/t^2); dusus fallGravityRatio, Space birakma jumpCutGravityRatio, minJumpHoldTime; hava kontrolu airAcceleration/airDeceleration/airTurnMultiplier; duvar: wallGrabMargin, wallSlideFallSpeed, wallJumpSpeedX, wallJumpHeightRatio, wallJumpLockTime, wallJumpBlendTime. Eski jumpForce/jumpForceOverride/riseMultiplier/fallMultiplier/jumpCut*/maxFallSpeed/wallJumpForceX/Y/wallSlideSpeed alanlari serilestirme icin duruyor ama KULLANILMIYOR (sahne/prefab degerleri artik ziplamayi etkilemez).
- IsTouchingWall: oyuncu govdesinin kenarindan 3 yukseklikte ray (olcekten bagimsiz).
- JumpState (dikey hiz dogrudan atanir), AirState (duvara yapisma kosulu), WallSlideState, WallJumpState, PlayerController.HandleJump (duvar yakininda yukselirken cift ziplama serbest) degisti.
- Yedek (eski dosyalar): proje kokunde _backup_before_movement_v2/ (6 dosya). Git ile de geri alinabilir.
- Olculen (Play, dogrudan state tetikleme): basili zipla yuk. 7.56, tepe 0.36 sn, havada 0.68 sn; kisa tik 4.77; duvara yapisma calisiyor; duvar zipla yatay -5.4 (girdisiz), dikey 7.4. Elle gercek girdiyle denenmedi.
- Yeni dosyalar: Assets/Scenes/Tutorial_Arena.unity, Assets/Scripts/Tutorial/{TutorialCheckpoint,TutorialKillZone,TutorialBossGate,TutorialLessonDuelist}.cs. Sahne Build Settings'te DEGIL.
- Not: Prefab-instance alanlarini kodla degistirirken SerializedObject kullan; dogrudan atama domain reload'da kaybolabiliyor.

- 2026-10-18 (Wall jump v3): WallJumpState artik DUZ CAPRAZ firlar (yercekimi kapali, sabit hiz wallJumpSpeedX/wallJumpRiseSpeed, en fazla wallJumpDiagonalMaxTime); karsida duvara degince yapisir. Duvarda kayma wallSlideFallSpeed=9, wallJumpSpeedX=34 (prefab). Oyun fontu: Resources/UI/Hoshiko_Satsuki.ttf (GameFont.cs; yedek Knight_Souls; ğ İ ı Ş ş icin Arial yedegi). Tutorial sahnesinde RunCameraZoom.showDebug kapatildi.


## Ledge Climb (2026-10-18)
- Yeni: Assets/Scripts/LedgeClimbState.cs. Otomatik, hizli kenar tirmanma (~0.2 sn, iki faz: duvar boyunca yukari, sonra kenarin ustune). Tirmanirken Rigidbody Kinematic + gravity 0, Exit'te geri yuklenir.
- PlayerMovement: 'Ledge Climb' basligi (ledgeClimbEnabled, ledgeClimbTime 0.2, ledgeReachAbove 0.2, ledgeMinAboveFeet 0.2, ledgeExitSpeedRatio 0.7, ledgeRegrabCooldown 0.2) + TryGetLedge(dir, out standPos).
- Tetikleyiciler: AirState (kenara dogru basarken), JumpState (vy < jumpForce*0.6 ve kenara basarken), WallSlideState (tepeye yakin tutununca, uzaga basmiyorsa), WallJumpState (0.06 sn sonra karsi kenara yetisince). Asagi (S) basiliyken yakalamaz.
- Animasyon: PlayerAnimationController.PlayLedgeClimb -> Animator'da 'LedgeClimb' state'i varsa o, yoksa 'Jump'. Su an LedgeClimb animasyonu yok.
- Yedek: _backup_before_ledge_climb/ (PlayerMovement, AirState, WallSlideState, JumpState, WallJumpState, PlayerController, PlayerAnimationController).
- Test (state'i dogrudan tetikleyerek, gercek klavye degil): duvarda kayma->tirmanma ve duvar ziplamasi->karsi kenar->tirmanma calisti, cok yuksek kenarda tirmanmadi. Klavyeyle (A/D basili) AirState/JumpState tetiklemesi test edilmedi.
- NOT: Diskteki WallJumpState su an v2 (capraz/miknatis v3 diskte yok; PlayerMovement'ta wallJumpRiseSpeed alani yok).

- Ledge Climb sıkılaştırma: artık SADECE kenara dogru yon tusuna basilirken VE kenara bakarken tutunur (notr / ters yon / arkasi donukken tutunmaz; WallSlide'da da ayni). Yakalama penceresi daralti: ledgeMinAboveFeet 0.6, ledgeReachAbove 0.05 (kenar goğus-kafa hizasinda olmali), duvar mesafesi govde kenari+0.2. Test: kenar ayaktan ~3.5-5.3 birim yukarida iken tutunur; 3.2 ve 5.5'te tutunmaz.

## Boss Ceviklik (2026-10-18)
- Boss1Rig.cs: saldiri animasyonu 'yapiskan' hissi giderildi. Windup artik EaseOut (aninda kalkmaya baslar; eskiden Smooth = yavas basla/yavas otur), Strike kubik EaseIn (daha ani), Recover hizli cikis (1-x)^2. slowRaise (kombo ilk vurus / Supurme / Aldatmaca) kilici YAVAS kaldirmak yerine hizla kaldirip vurus aniina kadar gergin pozda bekletir -> vurus zamanlamasi (parry/block penceresi) AYNI kaldi, sadece hareket sertlesti.
- Yedek: _backup_before_boss_agile/Boss1Rig.cs. Oyunda gorsel olarak test EDILMEDI (Tutorial_Arena'da Boss1 Play'de olusuyor/aktif degildi, derleme hatasiz).
- Gerekirse Boss1Enemy 'Ceviklik' alanlari (agileWindupTime/TensionTime/HoldTime/RecoverTime) ve strikeTime ile ince ayar.
- Boss hurtbox: Boss1Enemy.BuildHurtbox() govdeye 'BossHurtbox' adli trigger child ekler (hurtboxExtraSide 4, hurtboxExtraUp 2 dunya birimi). Fiziksel govde collider'i degismedi (oyuncuyu iten kisim ayni). Leap/Wave sirasinda tum child collider'lar zaten kapatiliyor -> hurtbox da kapanir. Test: oyuncu boss merkezinden 13 birim uzakta -> eski govde vurulmuyordu, yeni hurtbox vuruluyor. Yedek: _backup_before_boss_agile/Boss1Enemy.cs

## Dash FX (2026-10-18)
- Ozel dash animasyonu KAPATILDI: RonniPlayerBridge.useDashAnimation=false (varsayilan). Geri acmak icin bu bool'u true yap. Dash tozu (RonniDashDust) bu modda da calisir.
- Yeni: Assets/Scripts/DashFx.cs (DashFx static + DashGhostHost). DashState Enter -> ileri uzama (x1.45, y0.72, 0.18 sn); Exit -> sikisma (x0.8, y1.22, 0.16 sn); dash boyunca afterImageSpacing aralikla + basta + sonda Ronni mesh parcalarinin anlik kopyasi (soguk mavi-beyaz, 0.30 sn'de solar, gercek karakterin 100 order arkasinda). Ayarlar DashFx static alanlari (StartStretch, EndSquash, GhostLife, GhostColor, GhostAlpha).
- Player'daki SquashStretch bileseni KAPALI oldugu icin ona bagli degil; ModelPivot.localScale'i DashGhostHost surer.
- Yedek: _backup_before_dash_fx/ (DashState, SquashStretch, RonniPlayerBridge). SquashStretch.cs degismedi.
- Test: state dogrudan tetiklenerek; olcek ve iz sayisi dogrulandi, gorsel kalite goz ile DEGERLENDIRILMEDI.

## Dash gerilme karesi (2026-10-18) - Attack1 GERI ALINDI
- Kullanici PDF'indeki kare (ATTACK1_1: egilmis, el katanada) SADECE dash icindir. Attack1 eski haline donduruldu: Attack1.anim orijinal (yedek: _backup_before_attack1_windup/), SquashTable Attack1 satiri orijinal (1.06,0.94)/(1.12,0.92). Attack1HD_1_Windup.png tasindi.
- Yeni asset: Assets/Sprites/RonniSkin/Resources/DashFrames/DashHD_1.png (Single sprite 'DashHD_1', ppu 100, pivot ~(0.515,0.0043)).
- RonniPlayerBridge: yeni UpdateDashFrame() (LateUpdate sonunda). Dash sirasinda tum rigler gizlenir, frameSR'de DashHD_1 gosterilir; squash/stretch DashSquashTarget() ile: gerilme (dashCoilScale, dashCoilTime) -> firlama (dashStretchScale) -> gevseme -> son oturma (dashEndScale). Inspector'da 'Dash karesi' basligi altinda: frameDash (kapatirsan rig'li eski halde kalir). Dash bitince bMode=-99 ile rig secimi yeniden yapilir.
- DashFx: kare gosteriliyorsa after image o sprite'in kopyasi (SpriteRenderer) olur; mesh izi yedek olarak durur.
- Test (Play Mode, DashState dogrudan tetiklendi): dash sirasinda kare acik/rigler kapali, iz olusuyor, bitince rigler geri geliyor. Gorsel kalite goz ile DEGERLENDIRILMEDI, gercek klavye ile denenmedi.

## Dash izi + ledge sikilastirma + walljump yonu (2026-10-18)
- DashFx: iz daha belirgin (GhostAlpha 0.85, GhostLife 0.45, parlak mavi, GhostSpacing 0.035 sn; DashState bunu kullanir). Iz siralamasi karakterin HEMEN arkasi (GhostOrderBehind 1; eskiden -100 arka plana gidiyor olabilirdi).
- Ledge: PlayerMovement varsayilanlari ledgeReachAbove 0, ledgeMinAboveFeet 0.8 (sadece vucudun ust %20'si/el hizasi); duvar mesafesi extents+0.08. LedgeClimbState.TryStart: yukari hiz > 1.0 iken tutunmaz. Player.prefab (iki kopya) + Tutorial_Arena sahne ornegi serialize degerleri guncellendi.
- Walljump yonu: PlayerController.facingLockTimer (HandleFacing kilidi); WallJumpState.Enter en az 0.25 sn (lock+blend) kilitler. Duvara dogru tusa basilsa da karakter ziplama yonune bakar.
- Test: Play Mode'da WallJumpState dogrudan tetiklendi + moveInput duvara zorlandi -> facing/flipX ziplama yonunde kaldi. Ledge ve iz gorsel/gercek klavye ile DENENMEDI.

## Boss dalgasi parry -> posture (2026-10-18)
- Boss1Enemy.WaveReflectHit (geri seken dalga bossa carpinca) artik bossun denge (posture) cubugunu doldurur: enemy.parryBalanceDamage x waveReflectBalanceMultiplier (yeni, varsayilan 3.5) -> EnemyBalance.AddParryBalanceDamage. Kirilirsa sarsinti %40 daha guclu. waveReflectStopDistance 7 -> 2 (dalga bossa yaklasip carpar).
- Test (Play Mode, WaveReflectHit reflection ile cagrildi, tam canda): her carpma ~63/240 posture (iki carpma 126). Can kilidi (canda >%50 iken cubuk %75'te durur) AYNEN korundu.
- Gercek dalgayi parry'leyip bossa carpmasi elle OYNANARAK denenmedi.
- Not: Boss1Enemy.cs commit'i ilk seferde cihaza dusmedi; ikinci force commit ile dustu (cihazdaki dosya okunarak dogrulandi).

## Boss posture kilidi kaldirildi + dalga hasari dusuruldu (2026-10-18)
- Boss1Enemy.postureLockAboveHealth 0.5 -> 0 (kilit YOK: tam canda da posture dolar/kirilir). waveReflectBalanceMultiplier 3.5 -> 2.
- Test (Play Mode, WaveReflectHit reflection): tam canda her carpma 36/240, 7. carpmada kirildi. Elle oynanarak denenmedi.
- Not: Boss1Enemy.cs commit'i bir kez karisik icerikle dustu; son duzeltme cihazda dogrudan File.WriteAllText ile yapildi (cihaz dosyasi dogrulandi). outputs kopyasi gerekirse yeniden stage edilmeli.

## Boss arenasi + kapilar (2026-10-18)
- Yeni: Assets/Scripts/BossArena.cs. Boss1Enemy.Awake'te BuildArena() ile calisma zamaninda olusur (sahne kurulumu YOK, MyScene2'ye dokunulmadi). Alan = bossun baslangic x'i +/- arenaHalfWidth (varsayilan 100 -> test: -25..175). Boss1Enemy Inspector basligi 'Arena (dovus alani)': arenaEnabled, arenaHalfWidth, arenaCenterOffsetX.
- Boss uyanip intro basladiginda (CheckRoarTriggers -> arena.RequestClose) oyuncu alanin icindeyse iki ucta kapilar (Ground katmaninda collider + kirmizi levha) yukselir. Oyuncu da boss da cikamaz (collider + FixedUpdate'te rb.position clamp: dash icinden gecmesin). Boss olunce ya da oyuncu olunce (enemy.IsTargetDead) kapilar iner.
- Boss1Wave: alan disina cikan (parry'lenmemis) dalga yok edilir.
- Test (Play Mode): oyuncu bossa yaklastirilinca kapilar kapandi (Closed=true), oyuncu alan disina (x=200) isinlaninca ~170'e geri cekildi. Boss olumunde kapilarin inmesi, gorsel kalite ve gercek oynayis DENENMEDI.
- Not: device_commit_files Boss1Enemy.cs'i iki kez ust uste gondermek gerekti (ilki dusmedi); cihaz dosyasi okunarak dogrulandi.

## Sinematik sonrasi input kilidi (2026-10-18)
- Sebep: DashState canControl'u false yapiyor; boss intro'su baslarken oyuncu dash'teyse IntroRoutine bu false degeri 'eski deger' diye saklayip intro bitince geri yaziyordu -> kontrol kilitli kalirdi, boss vurusu/hasar sifirlayinca duzeliyordu.
- Duzeltme: Boss1Enemy.IntroRoutine: introPrevCan = pl.canControl || pl.isDashing.
- Test (Play Mode): oyuncu intro mesafesine isinlanip ayni anda DashState'e sokuldu; intro bitince canControl=True'ya dondu. Duzeltmeden ONCEKI davranis ayni testle olculmedi; gercek klavyeyle (zipla/dash sonra yaklas) denenmedi.

## Arena kucultuldu + RunCameraZoom debug yazisi kapatildi (2026-10-18)
- Boss1Enemy.arenaHalfWidth 100 -> 70 (toplam 140 birim; oyundaki kamera ortho ~18'de yaklasik 2 ekran). Alan 5..145 (boss x=75 merkez). Dalga mesafeleri alana sigsin diye kisaldi: waveMinDistance 65->48 (Configure tabani 60->40), waveHopTargetDistance 82->62 (taban 78->55), waveHopMaxDistance 58->42 (taban 55->38). Geri ziplama kapi collider'larina carpip kisalir.
- RunCameraZoom.showDebug artik [NonSerialized] (sahnede kayitli true degeri yoksayilir; sol ustteki 'RunCameraZoom: bekleme...' yazisi ve ilgili Debug.Log'lar kapali). Sahne dosyasina dokunulmadi.
- Test: Play Mode'da arena 5..145, showDebug=False dogrulandi. Dalga/zipla davranisi ve ekrandaki yazinin kayboldugu gozle kontrol EDILMEDI.

## Boss sayi raporu (2026-10-18) - SADECE olcum, kod degismedi
- Kullanici geri bildirimi: baski biraz fazla, dalgalar yeni oyuncu icin cok hizli ama pattern ogrenilebilir; posture kilidi kalkinca boss mantikli; ledge ve dash istenen gibi.
- Olculen (Play Mode, PlayerDamage.HitEnemy ile 0.33 sn aralikli kombo, boss mudahalesiz): tam canda posture 53 vurusta kirildi (~17.7 sn), kirilana kadar can 3000->1350 (chip). Sersemken vurus basina ~12 can. Parry: tam canda ~18 posture (13 parry), %50 canda ~33; geri seken dalga: tam canda 36 (7 carpma), %50 canda ~66.
- Dalga: hiz 145 u/sn, bossun 18 birim onunden dogar -> oyuncuya varis suresi 48 birimde ~0.21 sn, 70'te ~0.36, 100'de ~0.57. Parry penceresi 0.17 sn. Telegraph 1.4 sn, dalgalar arasi 0.8 sn, 3 dalga.

## Dalga yavaslatildi (2026-10-18)
- Boss1Enemy: waveSpeed 145->110 (Configure tabani 140->105), waveInterval 0.8->0.95 (Configure tavani 0.85->1.0), faz 2 araligi 0.7->0.8 (faz 2 yine faz 1'den hizli, 5 dalga). Varis suresi: 48 birimde ~0.27 sn, 70'te ~0.47, 100'de ~0.75. Play Mode'da degerler okundu; gercek dovusta hissi denenmedi.


### BG ağaç gövdeleri — KARAR: ESKİ HAL KALIYOR (2026-10-19)
- Arka plan BGTrunk_* sprite'ları (Assets/Sprites/Trees/BG, 24 dosya) ESKİ orijinal haliyle kalacak. 3D render'dan üretilen yeni gövdeler denendi, kullanıcı beğenmeyip eskilerini geri aldı.
- BGTrunk_* dokularına DOKUNMA: üzerine yazma, yeniden üretme veya değiştirmeyi teklif etme.
- Art pipeline BG gövdelerine uygulanmaz; sadece öne çıkan ağaçlara (Tree01 tarzı) ve prop'lara (çadır) uygulanır.


### Zemin çimeni (2026-10-19)
- ZeminCimen/Back (169 eski şerit) KAPATILDI (SetActive false, silinmedi); yerine ZeminCimen/Row: tek SpriteRenderer, Tiled mod, Assets/Sprites/Grass/Grass_Tile.png (kullanıcının 3D çimen render'ından, dikişsiz tile, ppu 466, FullRect), tek düzlem, scale 1.45, bottom y=-5.08, x -150..~662, M_Grass_Last + eski tint, order 6, WindSway. Betik: _ArtPipeline/grass_tile.py. Çimeni elle tek tek yerleştirme; Row'u uzat/kısalt.


## 2026-10-20 — Sahne sanatı: meşale, ev, zemin taşı, tekne, çalılar, parallax, derinlik blur
- Yeni sprite/materyaller: Torch(+Flame_0..7, TorchFire.cs), House, GroundStone_Tile, Boat, Bush A-D (M_Prop_Coloured_*). Üreticiler: Assets/Sprites/_ArtPipeline/*_gen.py.
- Işıklar gece/gündüz sabit parlak (TorchFire day=night=3.2, Tent 1.6). Parallax: demirci eşyaları/taş/çimen/ağaç/çadır oranları yeniden ayarlandı (Rebase gerekir).
- DERİNLİK BLUR: SilhouetteRim.shader ve SpriteLitGray.shader'a `_Blur` (ekran px) eklendi; Assets/Scripts/DepthBlur.cs (sahnede 'DepthBlur' objesi) sorting order'a göre MaterialPropertyBlock ile değer verir (bulut/dağ ~3, gövdeler 2-1.6, çalılar 1-0.35, order>=-15 net). Sağ tık > Apply Depth Blur. Doğrulandı: derleme hatasız, sahne görünümünde uzak katmanlar yumuşak. Doğrulanmadı: Game view / build performansı (SpriteLitGray blur'u 9 lit örnek, sadece dağlarda).
- Sahne kaydedilmedi; Prop_House sahnede eksik; tekne yerleştirilmedi.
- DepthBlur.cs artık ay kenar ışığını da sürer (driveRim): rimStops order->şiddet (yakın 1, uzak 0'a düşer), rimWidthScale 0.3 (kalınlık çarpanı), rimStrength. Sadece SilhouetteRim renderer'ları, MaterialPropertyBlock ile. Foreground blur: foregroundStrength.


## Art session 2 (2026-10-20)
- Tent: only cloth sways (M_Prop_Coloured _SwayAmp/_SwaySpeed), props static; rim 0.3 on tent/forge props, Tree01 0.1, bushes 0.
- Ground stone tile simplified/seamless + blur; player feet behind ground stone.
- More/lower bushes (FG_Bushes, 40 children).
- FG_RocksBranch (bottom-left, Environments order 100). Old FG_Blades/FG_GrassStrip removed.
- FG_Leaves: 46 upright leaf pieces from bushesforeground.pdf (Assets/Sprites/Foreground/Leaves/*_v3.png), 3 depth layers, ParallaxEffect + ForegroundSway (Play only) + DepthBlur stops orders 91-100.
- Verified: game-view screenshot shows leaves upright at bottom. Unverified: sway motion (Play only), cut-edge look at other camera positions. Scene UNSAVED.


## Temizlik (2026-10-20)
- Yedek: D:\Oyun\PROJECT-M_Silinenler (Scripts, Assets, Resources_*). Geri almak icin dosyalari Assets altina geri tasi.
- Silinen: execute/finisher (PlayerFinisher, EnemyExecuteState, ExecuteMeter, ExecuteCinematic, PlayerExecute*), Archer/Shield/Bomber/Flyer/Posture, RunManager koprusu (Phase1Stubs'ta sadece MetaProgress+CharmUtil kaldi), BossController ailesi, MainMenu, Tutorial*, Endless, Rage*, kullanilmayan sprite/ses/prefab/sahne/screenshot (~342 MB).
- MainMenu.Active cagrilari false/true ile degistirildi.
- Sahne kaydedilmemis olabilir: kontrol edip kaydet.


## Tenka chiburi: yarım ay savurma + hafif çömelme + sol el kınada (2026-10-23)
- İstek: RonniIdleKatana 'tenka' (kılıç koyma: chiburi + noto) klibinde (1) kına tarafındaki kol kınayı hafifçe tutsun, (2) hareket boyunca bacaklar hafif açılıp hafif çömelsin, (3) kılıç döndürme bitince yukarıdan aşağı tek bir yarım ay savurma yapıp sonra kınına koysun.
- Klip: `Assets/Sprites/RonniSkin/Resources/RonniIdleKatana/ronni_anims.json` içinde SADECE `tenka` klibi değişti (90 -> 103 kare, 3.0 -> 3.4382 sn). idle/block/parry_rig/block_hit aynen duruyor. Klip `Assets/Editor/TenkaClipRebuild.cs` ile üretildi (Tools > Ronni > Rebuild Tenka Clip (slash)); kaynak her zaman orijinal yedek `_backup_before_tenka_slash/ronni_anims.IdleKatana.json`, yani tekrar çalıştırmak üst üste binmez. Çömelme (55 px), açılma (90 px), savurma pozları betiğin başındaki alanlarda.
- Sol kol: armL1/armL2 iki kemikli IK ile elin kınadaki temas noktasını izler (kına eğilince el de yükselir, dirsek dışa bükülür); slL1/slL2 kolluk eşlik eder. İlk/son karelerde etki 0 (idle ile birebir).
- Bacaklar: klibe legR/legL/footR/footL + hip.y eklendi. Ayaklar yerinde kalır, bacaklar dışa dönüp en çok ~%7 kısalır (diz bükülmesi gibi), vuruş anında biraz daha derin, kılıç kına girince yavaşça kalkar.
- Savurma: dönüş (klip 0-1.21 sn) -> kılıç yukarı/arkaya kalkar -> kesme (~0.2 sn gerçek süre, ivmelenerek) -> vuruş anında kan damlası tetiği + parlama -> dinlen -> eski ters tutuş + noto. Klipte `fxGlint.sy = -1` = saat yönü tersine savurma işareti (smear ters yatar).
- Kod: `RonniPlayerBridge.cs`: yeni `tenkaSlashTime` (0.72 sn), TenkaPhase yeni klip sınırlarına göre yeniden yazıldı (`tenkaSpinTime` eski hızını korur: klipteki dönüş bölümüne orantılı uygulanır). `RonniTenka.cs`: smear `fxGlint.sy < -0.5` iken y ekseninde aynalanır; savurma sesi bekleme süresi 1.5 -> 0.45 sn (ikinci savurmada da ses çalsın).
- Toplam süre ~3.35 -> ~3.88 sn (Q tuşu, otomatik kılıç koyma ve BeginSheathe(dur) orantılı ölçeklenir).
- Yeni dosyalar: `Assets/Editor/TenkaClipRebuild.cs`, `Assets/Editor/TenkaPreview.cs` (edit modunda geçici önizleme, Tools > Ronni > Clear Tenka Preview). Yedek: `_backup_before_tenka_slash/` (klip + RonniPlayerBridge.cs + RonniTenka.cs).
- Doğrulandı: derleme hatasız; editör önizlemesiyle pozlar ekranda görüldü (kaldırma, kesme, vuruş, noto, çömelme, sol el kınada); ilk ve son kare eski kliple aynı; Play'de `BeginSheathe` ile tenka çalıştı (bacak/kol kemikleri döndü), konsolda hata yok.
- Doğrulanmadı: gerçek oyun hızında akıcılık ve ses; sağa bakan (aynalı) görünüm; kan damlası yönü; tüm klibin Game view'de kare kare izlenmesi (açılış menüsü ekranı kapladı). Sahne değişmedi, git commit/push YAPILMADI.
- Geri almak için: `_backup_before_tenka_slash` içindeki 3 dosyayı eski yerlerine kopyala (klip, RonniPlayerBridge.cs, RonniTenka.cs).

## Tenka düzeltmeleri + saldırı kare akışı (2026-10-23, devam)
- Tenka: kılıç kolla aynı hatta (G~95) kalkıyor, vuruşta bıçak koldan daha dikey (ucu yukarıda, `impact` G=45); omuz boşluğu kapandı (kolluk kökü omuzda, kolla geri çekilir); kını eğilirken biraz iner + sol el kınanın dibine kayar (IK, omuz ±25° sınırlı); savurma hızlandı: `tenkaSlashTime` alanı `tenkaSlashSec` olarak yeniden adlandırıldı (varsayılan 0.42; eski ad sahnede 0.72 olarak kayıtlıydı). Klip yine TenkaClipRebuild ile üretiliyor (yedek: _backup_before_tenka_slash).
- Saldırı kare akışı (A seçeneği): `RonniPlayerBridge.cs` ShowFrame'e `frameFlow` eklendi. El çizimi Attack1-4 sprite kareleri (frameAttack1-4 açık) değişmedi; karakter sprite'ı hazırlık karesinde hafif geri yaslanır, vuruş karesinde öne atılır ve öne eğilir, sonra yumuşakça oturur (FlowTable, sprite boyuna oranla; `flowAmount` şiddet, `flowSpeed` oturma hızı). Atak zincirinde offset kaldığı için bir sonraki atağa akarak geçer. Afterimage de aynı rotasyonu kullanır. Yedek: _backup_before_attack_flow/RonniPlayerBridge.cs.
- Doğrulandı: derleme hatasız. Doğrulanmadı: oyunda atak sırasında görsel/his (girdi enjekte edilemedi, açılış menüsü açık); FlowTable değerleri tahmini, `flowAmount` ile ayarlanır. Git commit/push yapılmadı.

## Attack1 rig prototipi (2026-10-23)
- Amaç: Attack1'i el çizimi kareler (Attack1_HD, kırmızı ceketli eski çizim) yerine RonniIdleKatana rig'iyle oynatmak. Eski kare sistemi duruyor; `RonniPlayerBridge.useRigAttack1` (varsayılan AÇIK) ile kapatılınca eski hâl döner.
- Klip: `RonniIdleKatana/ronni_anims.json` içine `attack1` eklendi (55 kare, 0.9 sn, 60 fps; vuruş ~0.115 sn = combat attack1HitTime). Üretici: `Assets/Editor/TenkaClipRebuildAttack1.cs` (Tools > Ronni > Rebuild Attack1 Rig Clip; ayarlar a1CrouchScale 1.7, a1LungeScale 1.15). Kaynak: orijinal tenka yedeğinin ilk karesi (kılıç çekik idle). Akış: çömel + kılıç geriye -> alçak yatay kesim -> yukarı yay (baş üstü) -> bekle -> idle'a dön. Sol el kınada (IK), kolluk kökü omuzda, saç/haori gecikmeli.
- Bridge: LateUpdate'te `rigAtkNow` dalı (tenka düzeniyle, bitince EndTenka idle'a döndürür). Kesilme: dash/hareket/guard/parry/başka saldırı. `RonniTenka.quiet` (attack rig'inde kılıç sesi/kan yok). Smear RonniTenka halkasıyla (fxTip.sy) çiziliyor; el çizimi smear/glow ve kare squash'ı bu atakta YOK.
- RunAttack1 ve Run (tenka) aynı JSON'a yazar: tenka yeniden üretilince attack1 korunur, tersi de geçerli. Yedek: _backup_before_rig_attack1 (bridge/RonniTenka/anims, attack1 öncesi).
- Doğrulandı: derleme hatasız; editör önizlemesiyle pozlar görüldü; Play'de (MobileInput ile saldırı basıldı) rig attack1 oynadı, düşman vuruldu, konsolda hata yok. Doğrulanmadı: kombo geçişi (Attack2 hâlâ eski kırmızı kareler, görünüm farkı var), havada/koşarken saldırı (mode!=0 eski yol), his/zamanlama, ses.
- Git commit/push YAPILMADI.

- (2026-10-23, sonra) Attack1 rig prototipi KULLANICI İSTEĞİYLE KAPATILDI: `useRigAttack1` varsayılan false (animasyon beğenilmedi, akışkanlık beğenildi). Eski el çizimi kare + frameFlow akışı aktif. Rig kodu ve `attack1` klibi duruyor, kullanılmıyor. Plan: kullanıcı yeni ara kareleri çizecek.

## Eski şapkalı rig (RonniSkin) kalıntıları temizlendi (2026-10-23)
- Bulgu: Ana rig `RonniSkin` (hasır şapkalı eski karakter) oyunda şu durumlarda görünüyordu: (1) Q ile kılıç çekme `unsheathe` (~0.5 sn), (2) denge kırılması `guard_break` (~0.45 sn + solma sırasında ~0.1 sn kare). Başka durumda şapkalı rig çıkmıyor (koşu/zıplama/dash RonniRunVivid/RonniJump, atak el çizimi kareler, blok/parry yeni rig). RonniSkinB/BR (şapkalı profil) sadece rigRun yoksa yedek; kullanılmıyor.
- Çözüm: `RonniIdleKatana/ronni_anims.json`a `unsheathe` (tenka kına sokma bölümünün tersi + idle'a oturma, sakate batto) ve `guard_break` (geriye savrulma, kılıç havaya, çömelme) klipleri eklendi (üretici: `Assets/Editor/TenkaClipRebuildExtras.cs`, menü Tools > Ronni > Rebuild Unsheathe + GuardBreak Clips). Bridge'te `rigActNow` dalı (rigAtkNow ile aynı dal) bu aksiyonları kılıç çekik rig'inde oynatır; `hatSuppress` aksiyon bitince eski rig'in solma karesini engeller.
- Denetim aracı: `Assets/Editor/RigAudit.cs` (Play modunda girdileri sırayla tetikler, hangi rig'in göründüğünü raporlar). Denetimde RonniSkin artık hiçbir adımda görünmedi.
- Doğrulanmadı: koşu/zıplama/dash durumları denetim girdisiyle tetiklenemedi (kod okunarak şapkasız rig kullandıkları görüldü); klip görünümü/his (özellikle guard_break) oyunda gerçek hızda izlenmedi. Yedek: _backup_before_hat_fix. Git commit/push YAPILMADI.

## Menü giriş sesi: Start'a basınca (2026-10-23)
- Kaynak: kullanıcının `preview_5.mp3` dosyası (5.96 sn). Kadın vokali 3.055 sn'de girip ~4.2 sn'ye kadar sürüyordu (spektrogramda harmonik çizgiler); vuruş 0.2-0.4 sn, kuyruğu 2.7 sn'de -60 dBFS altına iniyor. Kesim: ilk 2.85 sn, son 0.4 sn yumuşak (qsin) fade-out, 44.1 kHz stereo 16-bit WAV (mp3 yeniden sıkıştırılmadı). Dosya: `Assets/Resources/MenuSfx/menu_start.wav`.
- Kod: `MenuSfx.StartGame()` (yeni; `StartVolume` 1.0, menü tıklarının AudioSource'unu kullanır, ignoreListenerPause açık). `MainMenu.StartGame()` coroutine'inin ilk satırında çağrılır: Start'a basınca ses çalar, ekran 1.1 sn'de kararır, 0.45 sn sonra 1.4 sn'de açılır (zamanlama değişmedi). Duraklatma menüsünden 'Devam' ve ana menüye dönüş etkilenmez (yalnızca ana menü Start).
- Not: MenuSfx.Select() tıkı Start'a basınca da hâlâ çalıyor (kısa klik, ses ile üst üste). İstenirse Start için kapatılır.
- Doğrulandı: derleme hatasız; Play'de ActivateMain(0) ile Start tetiklendi, AudioSource çalıyor (isPlaying), menü kapandı, oyuncu kontrolü açıldı, konsolda hata yok. Doğrulanmadı: kulakla ses/zamanlama hissi. Yedek: _backup_before_start_sfx (MenuSfx.cs, MainMenu.cs). Git commit/push YAPILMADI.


## Ruh (soul) kılıç parlaması — materyal tabanlı (güncel)
- Eski çubuk/blob parlaması KALDIRILDI. Parlama artık kılıç rig parçasının (idle_kilic, atk_blade; kınlıyken idle_katana/katana/katana_sheathed daha hafif) mesh'ine EK 2. materyal (ProjectM/SoulBlade, additive) olarak çizilir -> animasyonu ve kılıç şeklini birebir izler.
- SoulSystem.cs: ScanSwordParts (0.5 sn'de bir parçaları bulup materyal ekler), PickLiveSword, RigSword (ruh çekim hedefi artık görünen kılıç parçasının mesh'inden), SetBladeGlow (_Glow/_Tint/_Core her kare). BladeGlowK ile parlaklık ayarlanır.
- Bilinen sınır: kare-sprite saldırılar (Attack1-4 HD, ParryHD, DashHD) tüm vücut sprite'ı olduğu için bıçak ayrı değil; o anlarda parlama kapalı, ruh hedefi eski sabit noktalara döner.
- Yeni idle denemesi (idle_katana_frames vb.) bırakıldı, eski idle geri getirildi; yukarıdaki 'Yeni kılıç çekik idle' bölümü GEÇERSİZ.
- Değişen dosyalar: Assets/Resources/Shaders/SoulBlade.shader (yeni), Assets/Scripts/SoulSystem.cs.
- Güncelleme (koşu): koşu rig'inde kılıç tek parça sprite (Ronni_Run/katana, koyu düz renk) olduğu için parlama düz mavi görünüyordu. SoulBlade.shader artık kırmızı sap ve siyah kulpu maskeler, kenar çizgisi (rim) ve akan dalga ile degrade verir; SheathGlowMul 0.85. Dosyalar: SoulBlade.shader, SoulSystem.cs.
- Not: play'den çıkar çıkmaz yapılan device_commit_files bazen kalıcı olmuyor; yazdıktan sonra dosya içeriğini doğrula.
- Ruh emilim sesi: Assets/Resources/SoulSfx/soul_absorb.mp3 (preview_8'den kadın sesi temizlenip kırpıldı, 2.4 sn). SoulSystem.cs bu klibi bellClip olarak yükler (Absorb'da calar, zincirde perde yükselir); dosya yoksa kodla üretilen çan kullanılır.
- soul_absorb.mp3 güncellendi: artık preview_9 sesi (1.7 sn; 1.2 kHz altı ve ünsüz patlaması kesildi). preview_8 sürümünün yerine geçti.
- Ruh emilim sesi artık rastgele: Resources/SoulSfx/soul_absorb_0..5.mp3 (preview_10'dan kadın sesi temizlenip 6 parçaya bölündü). SoulSystem.Absorb her emilimde birini seçer (arka arkaya aynı gelmez, perde hafif rastgele + zincirle yükselir). Eski tekli soul_absorb.mp3 artık kullanılmıyor (elle silinebilir).
- Kılıç sesleri: Resources/SwordSfx/sword_spin_0..2 (preview_12, chiburi dönüşü başlarken rastgele) ve sword_sheathe_0 (preview_11, bıçak kına girerken, tsuba tıkına denk). Kadın sesi kesildi (ses bandı kesimi). RonniPlayerBridge.PlaySwordSfx + SheatheSfxTime; seviye: spinSfxVolume/sheatheSfxVolume. Yedek: _backup_before_sword_sfx.
- Menü W/S gezinme sesi: Resources/MenuSfx/menu_nav.mp3 (preview_13, 0.2 sn tık). MenuSfx.Nav bunu çalar; seçim/tıklama hâlâ menu_click.
- Ses ayarı: ruh emilim sesi 0.55 -> 0.3 (SoulSystem.Absorb). sword_spin klipleri başlarından kırpıldı (1.2-1.3 sn), son patlama chiburi başladıktan ~0.85 sn sonra, yani savurma vuruşuna denk gelir.
- Saldırı squash/stretch güçlendirildi (RonniPlayerBridge): SquashTable ~%60-70 daha belirgin; FlowTable hazırlıkta daha geriye yaslanma (-0.07..-0.09) ve vuruşta daha ileri atılma (0.135..0.19, eğilme 9-11 derece); vuruş anı hitFlow 0.028->0.042, 3.2->4.5. Yedek: _backup_before_atk_stretch. Genel çarpanlar squashAmount/flowAmount inspector'dan ayarlanır.
- Ruh emilim sesi şimdilik KAPALI: SoulSystem.cs içinde const bool AbsorbSfxOn = false (true yapınca açılır). Klipler Resources/SoulSfx'te duruyor.
- Chiburi (Q) saldırı sırasında basılınca: Q anında combat.CancelAttack() (tampondaki/sıradaki saldırı silinir) + atkHoldT/atkRetT/atkFade/lastAtkFrames sıfırlanır; tenka karesi sırasında da bu durum ve saldırı kare sprite'ı (frameSR) temizlenir. Böylece chiburi bitince eski saldırının bir karesi oynamaz. RonniPlayerBridge (satır ~853-855, tenkaNow dalı). Yedek: _backup_before_chiburi_fix. Not: chiburi->idle geçişinde bir karelik renk tonu farkı gözlendi (frame-step), düzeltilmedi.


## Arkadan vuruş = direkt can hasarı
- Scripts/PlayerDamage.cs: `backHit` (Attack/Slam, düşman staggered değil, `info.direction.x * enemy.FacingDirection > 0` yani oyuncu düşmanın sırtında) ise posture katmanı atlanır, vuruş doğrudan CAN katmanına gider; "ARKADAN!" yazısı çıkar. Yedek: Assets/_VoiceBackup~/PlayerDamage.cs.before_backstab
- Test edilmedi (oyun içinde elle denenmedi); derleme temiz.
- Arkadan vuruş can hasarı x1.5 (`BackHitDamageMultiplier` sabiti, PlayerDamage.cs).

## Düşman saldırı kararında yön kilidi
- Scripts/Enemies/EnemyUnit.cs: `facingLocked` artık `lockFacingWhileAttacking` (varsayılan true) ile windup başından itibaren kilitli (eskiden yalnız commit sonrası). Çok vuruşlu saldırının ara vuruşlarında da FaceTarget çağrılmaz. Yedek: _VoiceBackup~/EnemyUnit.cs.before_facinglock. Işınlanma (teleportBehind) sonrası tek seferlik dönüş korunur. LegacyBoss/EnemyController'a dokunulmadı. Oyunda test edilmedi.


## Test tuşları temizlendi + P/O/I
- Kalan tek test tuşları (Scripts/Enemies/EnemyArena.cs): **P** = sonsuz düşman döngüsü aç/kapa (sağ-sol sırayla, rastgele tip; kapatınca kalan düşmanlar ölür), **O** = yetenek barı hep dolu, **I** = ölümsüzlük (Health.godMode + PlayerDamageReceiver.GodMode). Üstte durum yazısı çıkar.
- Kaldırılan tuşlar: EnemySpawner(P fare spawn), TeleportToMouse(T), RunCameraZoom(B), RainSystem(F3/F4), TimeOfDay(. , [ ]), MobileInput(F10), CameraZoom(Z/X), HordeMode(B/N/V/1-5), EncounterDirector(K), TrainingDummy(T), GiantWarriorEnemy TestKeys (#if false), EnemyArena eski tuşları (1-9, Tab, K, H, G, Y, U).
- Yedekler: Assets/_BackupTestKeys~/ (dosya adı = yol, '/' yerine '__').
- Oyun modunda reflection ile test edildi (spawn, god, bar, kapatma); gerçek tuş basışı denenmedi.
- TeleportToMouse (T = imlece ışınlan) kullanıcı isteğiyle GERİ ALINDI (yukarıdaki kaldırılanlar listesinden çıkar).
