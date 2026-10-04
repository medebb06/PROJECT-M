# OYUN DEVİR ÖZETİ (yeni sohbete yapıştır / ekle)

## 0. Çalışma biçimi (önemli)
- Unity 6, 2D platformer + Sekiro tarzı denge (posture) dövüşü, URP 2D. Dil: Türkçe. Kısa, net cevap; tam dosya ver, yerelde değiştirdiysem "elle yama" notu yaz.
- **Claude Unity'de derleyemiyor/oynayamıyor.** Her değişiklikte parantez dengesi ve bağlantılar kontrol edilir, His ve denge testi bende.
- **Projedeki `.cs` dosyaları eski kopya.** Yeni sohbette ÖNCE güncel `.cs` dosyalarını yükle. Hata "CS0117/CS1061 üye yok" ise genelde ilgili dosyanın son hali projeye alınmamıştır.
- Unity 6'da obsolete olanlar kullanılmaz: `GetInstanceID`, `FindObjectsOfType` (yerine `FindFirstObjectByType`, `FindObjectsByType`).
- Ölçek: oyuncu canı **100**, posture 100, parry dengeye +50, düşman normal vuruşu −30. Sayılar benim tahminimden büyük; yüzde tabanlı yaz.

## 1. Dosya haritası

**Oyuncu:** PlayerController (isInvincible = state bayrağı + `hitInvincibilityTimer`), PlayerMovement (coyote düzeltmesi), PlayerStateMachine, Grounded/Air/Jump/WallSlide/WallJump(flipX ile yön)/Dash(Dash layer, kaçış)/GroundSlam state'leri, PlayerHurtState (sersemleme + korumalı dönem + knockback yavaşlaması), PlayerDamageReceiver (hasar tepkisi, `HitSlowMotion` sınıfı, slow-mo profilleri), PlayerInvincibilityBlink (beyaz flaş + yanıp sönme), PlayerCombatController (kombo, `attack1-4BalanceDamage`, `attackHealthDamage`, CancelAttack), AttackState (oyuncu komboları → PlayerDamage), PlayerFinisher (execute), PlayerDefenseController/Parry/Block, PlayerPosture, ParryRiposte.

**Düşman:** EnemyController (ana merkez), EnemyAttackState, EnemyChaseState, EnemyIdleState, EnemyStaggerState, EnemyHitState, EnemyExecuteState, EnemyBalance, EnemyAttackCoordinator (ritim), EnemyDepthSorter, EnemyDangerIndicator, EnemyAttackTelegraph, EnemyAttackAudio, **EnemyTime** (düşman saati), EnemyStatus (zehir), `SpriteWhiteFlash.shader` (**Assets/Resources** içinde olmalı).

**Genel:** HitStop (global zaman, öncelik + rampa), CombatImpactFeedback, Health (`SetMaxHealth`, `SetRecoveryEnabled`).

**Hasar altyapısı:** DamageInfo, CombatEvents (olay merkezi; ayrıca PlayerDamaged/PlayerBlocked/AttackMissed/AttackInterrupted, `PlayerHitKind`, `PlayerDamageReport`), PlayerStats (değiştirici katmanı), PlayerDamage (tek hasar akışı), CombatDebugLogger (isteğe bağlı).

**Charm / koşu:** CharmDefinition (`requiresAnyOf` ön koşul), CharmRunner + CharmUtil (charm'lara Tick, dash tespiti, yüzde denge hasarı), 21 charm: temel 7 + PARRY (Yankı, Kusursuz Zamanlama, Kesintisiz Ritim, Ezici Parry) + DASH (Gölge Adım, Rüzgar Kesiği, Fırsat Penceresi) + RİSK (Cam Top, Son Nefes, Kan Bedeli, Kusursuzluk) + ZEHİR (Zehirli Parry, Salgın*, Felç Edici Zehir*; * zehir kaynağı ister). Yeni StatType'lar: DamageTaken, ParryWindow, RiposteHits/Duration/Strength, BlockHealthCost. RunManager.StageStarted/StageCleared olayları, QueueBonusOffer. EnemyStatus.ExecuteMultiplier (Ezici Parry). CharmDefinition, CharmEffect, StatCharmEffect, PoisonCharmEffect, HealCharmEffect, CharmInventory, CharmCatalog (7 varsayılan charm), RunManager, RunUI (OnGUI prototip, TAB = canlı istatistik, charm durum metni), **RunStats** (koşu istatistikleri: saldırılara cevap dağılımı, hasar kaynağı, ölüm nedeni, bölüm tablosu, `persistentDataPath/run_stats.csv`).

## 2. Temel tasarım kuralları
- **İki katmanlı hasar:** düşman dengesi kırık değilse vuruş DENGEYE, kırıksa (stagger) CANA vurur. Execute (E) sersemlemiş düşmanı bitirir. Tüm oyuncu hasarı `PlayerDamage.HitEnemy`'den geçer: stat, kritik (olasılıksal yuvarlama), `CombatEvents.EnemyHit`.
- **Ritim koordinatörü:** düşman vuruş anları sıraya dizilir (Beat 0.9, desen 1-1-1-2, aynı anda en fazla 2 uyarı, hasar sonrası 0.6 nefes). Saatini `EnemyTime.Now` ile okur.
- **Standby / dağılma:** sıra bekleyen düşmanlar geride kademeli durur, saldırı halkasında iç/dış slot. Aralık genişliğe göre. Üst üste binme için `EnemyDepthSorter` (görsel katman).
- **Kararlı saldırı:** uyarının `attackCommitPoint` oranından sonra vurarak kesilmez (zırhlı). Koşuda RunManager bunu 0.15'e çeker.
- **Engellenemez vuruş:** parry/block işe yaramaz, direkt can hasarı (`unblockableDamage`, Inspector), sarı uyarı + "!" + yönlü uzun kutu (baktığı yöne, yön saldırı başında kilitlenir), kaçış: dash (+0.12 sn kaçış payı), düşmanın arkasına geçmek, kutunun üstünden zıplamak. Uyarı uzun (×1.6), animasyon vuruş anına hizalı, recovery ×1.6.
- **Normal saldırılar da yönlü** (`normalAttackFrontOnly`), yön kilidi tüm saldırılarda (`lockFacingDuringAttack`).
- **Parry:** düşmanların ZAMANI yavaşlar (`EnemyTime.RequestRamp`, SmoothStep, oyuncu/dünya normal; animasyon yavaşlar, sesler yavaşlamaz). Düşman prefab'ında `parrySlowMo` / `parryBreakSlowMo`. Ek olarak **riposte:** parry sonrası 2 sn / 3 vuruş denge ×2, can ×1.5, kritik +%50, +25 posture iadesi.
- **Düşman zamanı kuralı:** düşman kodunda `Time.deltaTime/Time.time` yerine `EnemyTime.DeltaTime/Now/Scale`. Execute bilerek gerçek zamanda.
- **Oyuncu vurulma tepkisi:** sersemleme 0.22 sn + korumalı dönem 0.7 sn (yanıp sönme), vurulma flaşı, slow-mo (normal / son can / ölümcül, rampalı).
- **Charm'lar:** istiflenir (aynı charm tekrar → güçlenir), `PlayerStats.AddModifier(owner, tip, ekleme, çarpan)`, olay merkezine abone olur, etkiler **yüzde tabanlı** (zehir max dengenin %'si, iyileşme max canın %'si). Zehir: önce dengeyi, denge kırıksa canı eritir; PlayerDamage hattından geçmez.
- **Koşu (deneme döngüsü, ileride Dead Cells tarzı 3 dk bölümlerle değişecek):** sonsuz; dalgalar (toplam düşman = taban × `stageLengthMultiplier` 1.5, dalgalara bölünür), düşman hızı/engellenemez ihtimali/can/normal hasar bölümle artar, başta + her bölüm sonu charm seçimi (oyun durur), yeniden başla Enter. Takılma kurtarma (15 sn ilerleme yoksa), `alwaysHunt`, koşuda can yenilenmesi KAPALI.

## 3. Kurulum notları
- Sahnede boş objeye **RunManager** ekle, **Enemy Prefab** alanına **Project penceresinden prefab** ata (sahne nesnesi değil; olursa gizli şablon kopyası çıkarır ve uyarır).
- `SpriteWhiteFlash.shader` → `Assets/Resources/`. `Dash` layer'ı olmalı ve düşmanlarla çarpışmamalı (arkasına dash için).
- Debug: `P` düşman doğur, `R` fareye respawn (RunManager bunu fark eder), `PlayerStats → Debug Log`, `CombatDebugLogger`.
- Prefab/scene'e kaydedilmiş Inspector değerleri koddaki yeni varsayılanları ezmez; yeni alanları elle gir.

## 4. Bilinen sınırlar / dikkat
- Oyuncu vuruşunun can hasarı `PlayerCombatController → Attack Health Damage` (varsayılan 1); düşman canı büyük ölçekteyse artır. Sayıların çoğu tahmin, his testi gerekli.
- Execute (Execute Damage) düşman canını aşınca tek vuruşta öldürmez; can hasarı charm'ları o zaman anlam kazanır.
- `Enemy.TakeDamage` (eski IDamageable yolu) PlayerDamage'a bağlı değil. Execute hasarı `EnemyHit` olayı üretmez (öldürme olayı üretir).
- Düşman `maxBalance` bölümle ölçeklenmiyor. RunUI OnGUI prototip.
- Koşuda "parry'e ihtiyaç duymuyorum" sorunu: vurarak iptal (commit), bedava can yenilenmesi, block'un ucuzluğu → riposte + anti-trade eklendi, **test edilmedi**.

## 5. Önerilen sıradaki işler
1. ~~Koşu istatistikleri + koşu sonu özeti~~ YAPILDI (RunStats). Test edilmedi; birkaç koşu oynayıp CSV'ye bakarak dengeyi ayarla.
2. ~~Charm derinliği~~ YAPILDI (14 davranış charm'ı). Test edilmedi; CSV: run_stats_v2.csv.
3. Parry'yi zorunlu kılan düşman: block'u kıran ağır saldırı, menzilli düşman.
4. Gerçek arayüz (posture/can çubuğu, charm ikonları, hasar sayıları).
5. Dead Cells tarzı 3 dk bölüm yapısı (dövüş içeriği ile koşu akışı ayrı kalmalı).

## 6. Düellocu prototipi (3. adım, test bekliyor)
- Sorun: tek saldırılı düşman → tek cevap (parry) → tekdüze. Karar: boss rush'tan önce 1v1 düello denemesi.
- `EnemyMoveset` (düşmana eklenir): hamleler = vuruş dizileri. Vuruş türleri: Normal (kırmızı, parry/block), Sweep (sarı + alçak mavi kutu, ZIPLA; dash korumaz), Grab (sarı + "!" + uzun kutu, dash/kaç). Varsayılan Düellocu: Tek Vuruş, Üçlü Kombo, Gecikmeli Vuruş (rastgele uyarı), Süpürme, Kombo+Süpürme, Yakalama.
- EnemyAttackState: moveset yoksa eski tek vuruş. Kombonun 2.+ vuruşu kesilemez, block'lanan ara vuruş düşmanı itmez. Sweep istatistikte "engellenemez" sayılır.
- EnemyAnimationDriver: Idle/Walk/Attack/Hurt (+ isteğe bağlı Sweep/Grab), ok çizmeden; uyarıda 'Hold Pose Time' karesinde donar, vuruş karesi hasara hizalı. PlayerDamageReceiver.TakeDamage'a `ignoreDashIFrames` eklendi.
- Bilinen: ritim koordinatörü hamlenin ilk uyarı süresini bilmiyor (1v1'de önemsiz).
- Test: RunManager'da baseEnemyCount=1, enemiesPerStage=0, stageLengthMultiplier=1, wavesPerStage=1, extraWaveEveryNStages=0; Enemy Prefab = Düellocu.

## 7. Sonraki adımlar (yapıldı, test bekliyor)
- Parry akışı: pencere bırakınca kapanmaz, tampon, havada savunma, zincir (ilk parry zamanlamalı, sonra abanma/basılı tutma parry), hafif spam cezası. Kombo ortasında parry slow-mo yok, kombo noktaları (EnemyComboIndicator).
- Tepki geçişi: tek tamponlar (dash/zıplama/saldırı/parry/execute), block'tan zıplama/dash, saldırı iptal penceresi (Combo Cancel Point), dash yönü giriş yönü.
- Düello profili + execute gelişimi (RunManager), Odak charm'ı, CritFeedback, kompakt RunUI.

## 8. Oyun döngüsü (koşu yapısı)
- Lobi (zorluk/Isı seçimi) → başlangıç charm'ı → 3 PERDE × (4 oda + BOSS) → ZAFER.
- Kapılar: Dövüş (charm) / Elit (2 charm + bol altın; can×1.6, hasar×1.2) / Dükkan (charm al-sil, iyileş, yenile) / Dinlenme (%40 iyileş ya da charm +1). Boss öncesi dükkan/dinlenme garantisi.
- Boss: BossController (faz 2 %50 can, EnemyMoveset.CreateBossPhase1/2Moves), can ×6 (+1.5/perde), her execute max canın %25'i.
- Altın: öldürme 8 (+%25/perde), execute +4, elit ×2.5, boss 60×perde, hasarsız oda +15, parry +1.
- MetaProgress (PlayerPrefs "projectm_meta_v1"): kilitler act2 (Ezici Parry, Rüzgar Kesiği), act3 (Kan Bedeli, Kusursuzluk), win1 (Salgın, Felç), exec50 (Son Nefes), parry150 (Odak). Isı 1-5 (N'de kazan → N+1).
- RunStats CSV: run_stats_v3.csv (zafer/perde/ısı/altın sütunları).

## 9. İlk koşu verisi + engellenemez karşılıklar (7. adım, test bekliyor)
- Veri (2 koşu): normal saldırılar çözülmüş (72'den 1 isabet). Ölümlerin TAMAMI engellenemez vuruştan: 16'dan 4 isabet, vuruş başına 62–83 hasar (2× Cam Top = alınan ×1.69), dash ile hiç kaçılmamış. Dövüş ~10 sn (2× Cam Top denge ×1.96).
- PlayerDamageReceiver: `maxSingleHitPercent` 0.4 (tek vuruş azami canın %40'ı).
- Cam Top: en fazla 1 yığın, denge ×1.25 (can ×1.4, alınan ×1.3 aynı).
- UnblockableCounter (düşmana otomatik eklenir): KUSURSUZ KAÇIŞ: yakalamaya son anda (perfectDodgeWindow 0.35 ya da "şimdi" işareti) DÜŞMANA DOĞRU dash → dengenin %50'si, düşman slow-mo, kombo kesilir, recovery ×1.5. ATLA-VUR: süpürmenin üstünden (menzil içinde) zıpla → "VUR!" + 1 sn pencere, ilk vuruş +%35 denge. Statik olay `CounterLanded`.
- EnemyDangerLabel: uyarı boyunca başın üstünde "DASH!" / "ZIPLA!", "şimdi" anında büyür + beyaz yanıp söner. CombatCallout: dünya üstü kısa yazılar.
- Oyuncunun havada saldırısı yok: havada basılan saldırı inişte çıkar (tampon kısa), ground slam de sayılır.
- RunStats CSV: run_stats_v4.csv (+ kusursuz_kacis, atla_vur). Inspector alan adı csvFile oldu.

## 10. Kombo parry dengesi (8. adım, test bekliyor)
- Sorun: zincirde basılı tutmak kombonun kalanını otomatik parry'liyordu (holdToParryInChain), pencere ×2.5 idi.
- Şimdi: basılı tutmak HER ZAMAN block (posture yer; kombo vuruşunda ×0.6). Her vuruş için zamanlamalı basmak gerekir.
- Zincir penceresi ×1.5 (`chainParryWindowMultiplier`). Boşa basma cezası zincirde de geçerli: boşa giden pencereden sonraki 0.3 sn içinde basılan parry yarım pencere (`whiffPenaltyTime`, `whiffWindowMultiplier`). Alan adları bilerek değişti (prefab'daki eski kolay değerler ezmesin).
- Ayar: zorsa chainParryWindowMultiplier 1.8 ya da whiffWindowMultiplier 0.7; kolaysa 1.2 / 0.4. Hedef: CSV'de kombo vuruşlarının çoğu parry, bir kısmı block.

## 11. Düşman başı çubukları (9. adım, test bekliyor)
- EnemyOverheadBars (kendiliğinden oluşur, OnGUI, GUI.depth 10 → menülerin arkasında): her normal düşmanın başının üstünde boss tarzı can (kırmızı) + denge (altın) çubuğu; can kaybı beyaz iz bırakır, yeni denge hasarı kısa süre beyaz; denge kırıkken çubuk yanıp söner (execute). Boss'ta yok (üstteki çubuk).
- Prefab'daki eski Slider çubuklarını (EnemyBalanceBar, HealthBarUI) Canvas'ını kapatarak gizler (`hideOldEnemyBars`).
- 10. adım: sadece vurulunca görünür (can azalır / denge artar → 3 sn görünür, 0.4 sn söner; denge kırıkken hep görünür, doğumdan sonraki 0.3 sn sayılmaz). Canı olmayan düşman çizilmez. `ReservedWorldHeight`: kombo noktaları (çubuk +0.15) ve DASH/ZIPLA yazısı (çubuk +0.5) çubuğun ÜSTÜNE yerleşir.

## 12. Düşman tipleri (11. adım, test bekliyor)
- EnemyArchetype (Düellocu / Çevik / Ağır) + ArchetypeProfile çarpanları (`overrideProfile` ile prefab başına).
  - Çevik: can ×0.75, denge ×0.75, hız ×1.3, hasar ×0.7, recovery ×0.8, parry dengesi ×0.75, block posture ×0.7, boyut ×0.92. Hamleler: Dürtme, Dörtlü Seri, Beşli Seri, Aldatmaca (gecikmeli 2. vuruş), Seri + Süpürme, Kapma (nadir yakalama). Kombo arası ~0.3 sn.
  - Ağır: can ×1.5, denge ×1.4, hız ×0.75, hasar ×1.5, yakalama ×1.15, recovery ×1.35, parry dengesi ×1.4, block posture ×1.8, az savrulur (×0.45), boyut ×1.15. Hamleler: Ağır Darbe, Gecikmeli Ezme, İkili Savuruş, Yakalama, Yer Süpürme, Darbe + Süpürme, Süpürme + Yakalama.
- EnemyMoveset.CreateQuickMoves / CreateHeavyMoves (+ sağ tık menüleri). EnemyBalance.SetMaxBalance eklendi.
- RunManager: `quickPrefab`, `heavyPrefab` (boşsa Enemy Prefab'ın renklendirilmiş kopyası, inaktif kök altında şablon), `archetypeWeightsPerAct` (P1 .6/.2/.2, P2 .35/.35/.3, P3 .3/.35/.35), ilk oda hep Düellocu, oda bannerında tip adı, 2+ düşmanlı odada sonrakiler karışık. Boss etkilenmez. `useArchetypes` kapatılırsa eski davranış.

## 13. Okçu (12. adım, test bekliyor; Adim12 zip = 11 + okçu)
- MoveHitType.Shot (enum SONUNA eklendi). EnemyAttackState: Shot engellenemez değil; vuruş anında `EnemyArcher.Fire` ok çıkarır, sonucu ok çözer.
- EnemyProjectile (fizik/katman gerektirmez, oyuncu collider sınırı + zemin linecast): dash → içinden geç (Dodged), parry → okçuya geri yansır (parry denge hasarı hemen + RaiseParry; yansıyan ok çarparsa ek ×0.6 denge, "YANSITILDI!"), block → posture (RaisePlayerBlocked), isabet → normal hasar. Iska → AttackMissed. Düşman zamanıyla uçar.
- EnemyArcher: arrowSpeed 12, maxAimAngle 15, kodla üretilen pixel ok (arrowSprite ile değiştirilebilir), retreatDistance 3.5 (LateUpdate, uçurum kontrolü).
- Okçu profili: can/denge ×0.7, attackRange = chaseStop = 7, useStandby kapalı (yakın dövüşçüyle aynı anda arkadan ok atar). Hamleler: Tek Ok, Çifte Ok, Gecikmeli Ok, Üçlü Yaylım (min 2.5-3 mesafe), Hançer ve Tekme Süpürme (yakında; menzil çarpanı 0.25/0.3).
- RunManager: archerPrefab, archerTint, ağırlıklar Vector4 (P1 .5/.2/.15/.15, P2 .3/.25/.25/.2, P3 .25×4). EnemyAnimationDriver: isteğe bağlı "Shoot" state + shootHitTime. Kombo noktası: ok = turuncu.

## 14. Zaferden sonra yeni koşu + ayrı prefab'lar (13. adım)
- RunManager: sonuç ekranında (ölüm/ZAFER) Enter/Space/R → yeni koşu (RunManager.Update yedek giriş). İstatistik/meta kaydı try/catch (FinishRunSafely). BEKÇİ: istek 2.5 sn karşılanmazsa döngü zorla yeniden başlar (HardRestart). ResetRun inputLocked'ı da açar.
- RunUI: sonuç ekranında tıklanabilir "YENİ KOŞU" düğmesi.
- EnemyArchetype.applyScale: kendi sprite'lı prefab atanınca RunManager kapatır (boyutu prefab belirler).
- Ayrı prefab yolu: temel düşman prefab'ından Prefab Variant + Animator Override Controller (state adları ortak: Idle/Walk/Attack/Hurt + Sweep/Grab/Shoot), collider, Attack Animation Hit Time, RunManager Quick/Heavy/Archer Prefab alanları.

## 15. Okçu cilası (14. adım)
- Kullanıcının Archer.prefab'ı: karakter ölçeği 2 (gövde ~4 birim), EnemyArchetype type=Archer ama Override Profile AÇIK ve mesafeler 0 → menzil 6 / durma 4 kalıyordu. Artık okçuda 0 mesafe = tip varsayılanı.
- Okçu mesafesi 12 / durma 11, geri çekilme 6. Hamle mesafeleri: ok ≥4.5 (yaylım ≥5), hançer/tekme ≤4.5 (hançer menzil ×0.4).
- EnemyArcher varsayılanları: ok hızı 24, ömür 2, ölçek 2, değme payı 0.25, maxAimAngle 0 (oklar DÜMDÜZ yatay).
- Geri dash: oyuncu 4.5'ten yakınsa her 0.4 sn %35 ihtimal, 5 sn bekleme, hız 22 × 0.25 sn + hafif sıçrama, iz (FadeAndDestroy), uçurum kontrolü; bitince saldırı beklemesi 0.15'e iner.
- Animasyon notu: drag ile oluşan klipler Loop Time açık gelir → Attack/Hurt kliplerinde KAPAT. Attack Animation Hit Time = okun bırakıldığı kare, Hold Pose Time = yayın gerili olduğu kare.

## 16. Animasyon sürücüsü düzeltmesi (15. adım)
- Kullanıcı okçuda Hold Pose Time = Attack Animation Hit Time = 0.8 girmişti → klip vuruş karesinde donuyordu (animasyon "oynamıyor").
- EnemyAnimationDriver: WindupStyle (Stretch varsayılan: saldırı klibi uyarıya yayılır, en fazla ×3 yavaş; Hold Pose Time sadece HoldPose stilinde), hit time klipten uzunsa klip uzunluğuna kırpar + uyarı, minHurtTime 0.25 (kısa darbede Hurt titremez).
- Kontrol listesi: Attack/Hurt kliplerinde Loop Time KAPALI; Attack Animation Hit Time = bırakma karesi / fps.

## 17. Oda takılması (16. adım)
- Belirti: "15 sn'dir ilerleme yok → kurtarma. Taşınan 0, silinen 0" — spawned listesinde canlı sayılan ama yakın ve Idle olmayan düşman var, oda bitmiyor.
- RunManager: kurtarma logu artık canlı sayılan her düşmanı yazar (ad, state, uzaklık, aktif, görünür, can, konum). 3. ilerlemesiz kontrolde (≈45 sn) ForceClearRoom: kalanları kaldırır, oda biter (kilitlenme yok). Asıl sebep log'dan bulunacak.

## 18. Ölüm sonrası yeni koşuda donma (17. adım)
- Belirti: ölünce yeni koşu → oda başlığı var, düşman gelmiyor, oyuncu Idle'da donuk, 15 sn uyarısı bile çıkmıyor → büyük ihtimalle Time.timeScale 0'da kalmış (SpawnWave'in WaitForSeconds'ı ve takılma sayacı da duruyor).
- HitStop: taban zaman 0 iken istek gelirse dönüş değeri 1 (0 hatırlanmaz).
- RunManager: PausedWait bitince HitStop.ClearAll; pausedByMenu bayrağı; GuardFrozenTime (menü dışında timeScale 1 sn'den uzun 0 → düzelt + uyarı); F2 = ekranda koşu teşhisi (durum, timeScale, düşman sayısı, oyuncu state/canControl); ResetRun yedekleri (isInvincible/isAttackLocked false, rb.simulated, combat.enabled, timeScale 1).

## 19. ASIL SEBEP: Managers objesinde EnemyController (18. adım)
- Log: "DÜŞMAN TİPİ: Düellocu → Managers" → sahnedeki Managers objesinde EnemyController (+ EnemyArchetype) vardı. ResetRun EnemyController.All'daki her objeyi sildiği için ölümden sonra Managers'ı (içindeki RunManager ve RunUI ile) siliyordu → arayüz yok, oyun kilitli. Erken çıkan boş gri çubuk da buydu (EnemyOverheadBars Managers'ı düşman sanıyordu).
- Çözüm: kullanıcı Managers'tan düşman bileşenlerini kaldıracak. RunManager artık kendisini/üst objesini silmez ve Start'ta uyarır.
- Düellocu prefab'ında Attack Animation Hit Time = 0 → klip uzunluğu (1.28) kullanılıyor, sürücü %85'e kırpıp uyarıyor; gerçek vuruş karesi girilmeli.

## 20. Rastgele harita (19. adım, test bekliyor)
- LevelChunkLibrary: ASCII parçalar (# zemin, . boş, E düşman noktası, P oyuncu, X çıkış). Başlangıç, 10 ara parça (düz, basamak ±1, merdiven ±2, çukur 3, basamak taşları, tepe, kaya, çukur+basamak), 3 arena, boss arenası, çıkış. Kurallar: basamak ≤2, çukur ≤3, yüzey üstü ≥7 boş.
- LevelGenerator (RunManager kendisi ekler): tohumla parça dizisi (yüzey yükseklikleri eşleşir, drift ±5), alt dolgu, uç duvarları, sahnedeki Tilemap'ten tile/katman/sıralama/composite kopyalar (üstü boş = çim, değil = toprak). Origin hücre (0,300). Arena kapıları (BoxCollider2D, zemin katmanı, kırmızı yarı saydam). Çukur: oyuncu son güvenli yere + %10 hasar; düşen düşman ölür. Kamera ışınlanmada PreviousStateIsValid=false.
- RunManager: useLevelGenerator, fixedSeed, exitWalkTimeout 60. Dövüş odası: PrepareLevel (tohum = RunSeed + Stage×7919, arena sayısı = WavesForStage, boss = 1 boss arenası) → her dalga için EnterArena (oyuncu girince kilit, düşmanlar arena noktalarında sırayla) → temizlenince kapılar açılır → WalkToExit. ResetRun haritayı siler (lobi eski zeminde). F2: tohum + parça dizisi.
- Dikkat: Cinemachine Confiner varsa harita sınır dışında kalır.

## 21. Harita cilası (20. adım)
- Tile ROLLERİ şablondan komşuya göre: yüzey orta / sol köşe / sağ köşe, iç orta / sol kenar / sağ kenar; en alt sıra (alt kenar) iç dolguya karışmaz. Boyamada aynı kurallarla seçilir (köşe tile'ı düz zeminin ortasına konmaz → çizgi/boşluk yok). Elle de atanabilir (topLeftTiles vb.).
- horizontalStretch 2: zeminli sütunlar 2 kat genişler, çukurlar aynı kalır (haritalar/arenalar 2 kat uzun). Ara parça 2–4, çıkıştan önce 1–2.

## 22. Nöbetçi düşmanlar + fiziksel kapılar + geçiş alanları (21. adım)
- Dövüş odası (harita): arenalar artık NÖBET noktası (kapı kilidi yok); düşmanlar harita kurulunca noktalarında doğar, alwaysHunt kapalı, chaseRange = postAggroRange 13 (yaklaşınca saldırır). Çıkış kapısı "KİLİTLİ • N düşman"; hepsi ölünce açılır. Çıkışta beklerken (exitPullDelay 8 sn) kalanlar oyuncuya ışınlanıp gelir.
- Oda seçimi: menü yerine çıkış alanında fiziksel kapılar (DÖVÜŞ / ELİT / DÜKKAN / DİNLENME, renkli), içinde dur + [W] / [↑] / [F]. Boss öncesi tek BOSS kapısı. Boss arenası hâlâ girince kilitlenir.
- Dükkan / Dinlenme: geçiş haritası (Başlangıç → ara → Dükkan/Kamp parçası 'S'/'R' → ara → Çıkış); tezgaha/ateşe gelip [W] ile mevcut dükkan/dinlenme ekranı açılır (bir kez); çıkışta sonraki kapılar.
- LevelChunkLibrary: ChunkKind.Shop/Rest, Çıkış parçası 16 genişlik. LevelGenerator: SetExitDoors/SetDoorsLocked/SetLockedText/DoorAt/NearExit/ClearDoors, CreateStand/PlayerAtStand/SetStandUsed, TextShadowSync.

## 23. Orman teması (22. adım, test bekliyor)
- Tileset: Assets/Sprites/Tile/Jungle/Tiles.png (400x400, 16 px, 25x25 hücre). Kullanıcının dilimlemesi Automatic (birleşik büyük parçalar) olduğu için ilk sürüm tanıyamadı ("sprite'ları yok/tanınmadı" uyarısı, şablondan dama tahtası zemin). 22b: tema sayfayı ÇALIŞMA ANINDA kendisi 16x16 keser (Sprite.Create, FullRect, pixelsPerUnit 16) → dilimleme önemsiz, sadece 'Sheet' = Tiles.png gerekir.
- Sayfa haritası: çim seti (0-4,0-4: satır 0 taşan çim uçları, 1 yüzey, 2-3 toprak, 4 alt kenar; sütun 0 sol, 1-3 orta, 4 sağ), kaya seti (0-4,5-9 aynı düzen), tahta platform (5-7,6), ip köprü (5-9,7-9: direk 5/9, ip satır 8, tahta satır 9), ağaç (gövde 10-11 × 0-9, yaprak 9/12 × 0-1, sol dal 9,2-3, sağ dal 12,5, kovan 12-13 × 6-8, küçük kovan 12,2-3), arka çalılar (17-24, 5 renk × 3 satır), su (6-9: 19 yüzey, 20 derin), şelale (3-5,17-20), kayalar (0-14,21-22, 3 adet 5x2), süsler (mantar 15,15 / 15,16 / 20,16, dev mantar 16-17 ve 18-19 × 15-16, mavi çiçek 15-16,17-18, lavanta 17,17-18, saz 16,18-21, kazık 21,16 / 22,15-16, çim tutamı 22,17, yapraklı bitki 20-21,22-23), sandık (18-21,17-18), vazolar (21-24,19-20), anahtar/altın/iksirler (15-19,20-24).
- JungleTheme (yeni bileşen, LevelGenerator ile aynı obje): Sheet = Tiles.png. Zemini komşuya göre boyar (köşe/kenar/iç, 6 iç varyasyon), çim uçlarını ayrı çarpışmasız katmana koyar, süsler (küçük/uzun/2x2), arka ağaçlar (harita tepesine kadar, dal/kovan), arka çalılar, kayalar, çukurlarda su. Katmanlar karartılmış renkli, çizim sırası zemine göre (ağaç −12, çalı −10, kaya −9, süs −2, çim ucu −1, su +30). Süsler çıkış alanına ve tezgah çevresine konmaz. Ayrı RNG (harita düzenini değiştirmez).
- LevelGenerator: `theme` alanı (boşsa aynı objede/sahnede aranır), tema varsa şablon tile'ları gerekmez. Yeni '=' işareti: TEK YÖNLÜ platform katmanı (TilemapCollider2D + Composite + PlatformEffector2D, zemin layer'ı). İki yanı zemin = ip köprü, değilse uçan tahta.
- LevelChunkLibrary: "Köprü" (4 kare çukur üstünde köprü) ve "Platformlu Çukur" (8 kare çukur, 2 kare yukarıda iki 3'lük tahta).
- Sonraki fikirler: kapı/tezgah için ağaç kovuğu (13-14,0-3) ve sandık sprite'ı, vazoları kırılabilir altın kutusu yapmak, perdeye göre tema.

## 24. Çubuklar görünmüyor + çıkış açılmıyor (23. adım)
- Muhtemel sebep: Managers'ta hâlâ EnemyController var. Harita y=300'de kurulunca LevelGenerator'ın "düşen düşman ölür" kontrolü y≈0'daki Managers'ı düşmüş düşman sandı → Health yoksa Destroy → RunManager (+ LevelGenerator, varsa EnemyOverheadBars) silindi; ekran görüntüsünde Managers ve EnemyOverheadBars Hierarchy'de yoktu. Döngü durunca kapı hiç açılmıyor.
- LevelGenerator: düşme kontrolü sadece önce haritada (düşme çizgisi üstünde) görülmüş düşmanlara uygulanır, kendisini taşıyan objeyi asla silmez; Generate'te EnemyOverheadBars yoksa yeniden kurar.
- RunManager: canlı sayımı Health.IsDead / CurrentHealth<=0 / inaktif objeleri saymaz. Kilitli çıkışta [W] → kalanlar hemen gelir + Console'a kimin kaldığı yazılır.
- Kullanıcı yine de Managers'tan EnemyController/EnemyArchetype'ı kaldırmalı.
