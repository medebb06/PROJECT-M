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
