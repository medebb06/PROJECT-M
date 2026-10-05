# OYUN DEVİR ÖZETİ (yeni sohbete yapıştır / ekle)

## 0. Çalışma biçimi (önemli)
- Unity 6, 2D platformer + Sekiro tarzı denge (posture) dövüşü, URP 2D. Dil: Türkçe. Kısa, net cevap; tam dosya ver, yerelde değiştirdiysem "elle yama" notu yaz.
- **Claude Unity'de derleyemiyor/oynayamıyor.** Her değişiklikte parantez dengesi ve bağlantılar kontrol edilir, His ve denge testi bende.
- **Projedeki `.cs` dosyaları eski kopya.** Yeni sohbette ÖNCE güncel `.cs` dosyalarını yükle. Hata "CS0117/CS1061 üye yok" ise genelde ilgili dosyanın son hali projeye alınmamıştır.
- Unity 6'da obsolete olanlar kullanılmaz: `GetInstanceID`, `FindObjectsOfType` (yerine `FindFirstObjectByType`, `FindObjectsByType`).
- Ölçek: oyuncu canı **100**, posture 100, parry dengeye +50, düşman normal vuruşu −30. Sayılar benim tahminimden büyük; yüzde tabanlı yaz.

## 0.1 YENİ SOHBETE BAŞLARKEN (5 Ekim 2026 durumu)
- **Çalışma yolu:** Claude, kullanıcının bilgisayarındaki klasörlere bağlanarak çalışıyor. Bağlı klasörler: `D:\Oyun\PROJECT-M\Assets\Scripts` (scriptler BURAYA doğrudan, eskisinin üzerine yazılır), `D:\Oyun\PROJECT-M\Assets\Prefabs`, `C:\Users\PC\AppData\LocalLow\DefaultCompany\PROJECT M` (run_stats_v4.csv, Player.log), `D:\Oyun\PROJECT-M\Assets\Sprites\background`. Yeni sohbette bağlantı yoksa `device_request_folder_access` ile yeniden iste.
- **DİKKAT:** `Assets` içindeki bağlı bir klasör varken SendUserFile / outputs dosyaları o klasörün "Claude outputs" alt klasörüne düşer → Unity aynı sınıfı iki kez derler (CS0101). Script teslimi = `device_commit_files` ile Scripts'e yaz. (SkyBackdrop.cs şu an `Assets/Sprites/background/Claude outputs/` içinde, tek kopya; istenirse Scripts'e taşınabilir.)
- **Önce güncel dosyayı al:** Proje dokümanlarındaki .cs'ler eski olabilir; değişiklikten önce Scripts'ten stage edip ona yama yap.
- **Yeni alan adı kuralı:** Sahnedeki kayıtlı Inspector değerleri koddaki varsayılanları ezer; bir varsayılanın uygulanması gerekiyorsa alanın ADINI değiştir.
- **Denge verisi:** RunStats → `run_stats_v4.csv` (LocalLow). Ayar önerisinden önce son koşuları oku.

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

## 25. Daha çok düşman, ödül geçişte, parallax orman (24. adım, test bekliyor)
- RunManager (harita modu): düşman sayısı eski dalga ayarlarından bağımsız: `levelEnemiesBase` 6 + `levelEnemiesPerStage` 1/bölüm, en çok 16; nöbet noktası = toplam / `levelEnemiesPerPost` 3 (2–5 nokta); elit oda ×0.6. Toplam noktalara eşit dağılır, aynı anda doğar (bekleme yok), arena noktaları karıştırılır, noktalar biterse yanına 1.6 aralıkla. Arenalarda 4'er 'E' noktası.
- Charm seçimi bölüm içinde açılmaz (`rewardsOnTransition`): ödüller (normal/elit/boss/bonus) `pendingOffers` listesine birikir, banner "ÖDÜL ÇIKIŞTA → N CHARM"; çıkış kapısı seçilince (oda kapısı ya da BOSS kapısı) GrantPendingOffers. Güvenlik: her odadan önce de verilir. Haritasız modda eskisi gibi hemen. Dükkan/dinlenme aynen.
- JungleTheme: çiçek/mantar/sandık/kaya süsleri KALDIRILDI. PARALLAX: 3 çalı bandı (Uzak 0.75 / renk 4 / en yüksek zemin+6, Orta 0.5 / 3 / ort+3, Yakın 0.25 / 2 / ort+1; renk çarpanı ve sıra -30/-20/-12), her bant A/B iki sıra 8'lik çalı tepesi 6'şar kare aralıkla üst üste biner, altı bandın alt satırıyla haritanın dibine kadar dolu. Ağaçlar parallax 0.6 (ort+2, sıra -25). `JungleParallax` (DefaultExecutionOrder 10000): katman = başlangıç + (kamera − çapa) × factor; dikey 0. Tile yazımı toplu (SetTiles). Eski alan adları (treeOrder, bushOrder…) değişti.

## 26. Orman cilası (25. adım, test bekliyor)
- Ağaçlar: sağdaki parçalar (12. sütun yaprak/dal/kovan) gövdeye oturmuyordu → kaldırıldı. Gövde harita tepesinin `treeExtraHeight` 40 kare üstüne uzar (tepe görünmez), dibi ort. zemin `treeBase` −2. Sol dal (9,2-3) rastgele 0–`maxBranches` 2 adet.
- Dikey parallax: her bandın `verticalFactor`'ı (uzak 0.92, orta 0.85, yakın 0.7; ağaç 0.88): zıplayınca arka plan neredeyse oynamaz. Dikey çapa = kameranın haritaya geldiği ilk kare. Alan adı `bands` → `parallaxBands` (yeni varsayılanlar gelsin).
- Su: sayfadaki doğru satırlar (18 köpük, 19 köpük+su, 20 derin). Çukur duvar sütunlarının ARKASINA da su (sıra −1): yuvarlak kaya kenarlarıyla su arasında boşluk kalmaz.
- LevelGenerator: `visualDepth` 30 (zemin görselde aşağı uzar, düşme çizgisi aynı), uçlarda düz duvar yerine zemin `edgeExtension` 24 kare devam eder + görünmez BoxCollider2D engel.

## 27. Hafif parallax (26. adım)
- 25b: bantlar kaybolmuştu → dikey çapa lobi kamerasından alınıyordu (x yakın, y 300 aşağı). Artık kamera hem x (30) hem y (20) yakınken alınır.
- 26: parallax çok güçlüydü → `backgroundBands` (yeni ad): yatay uzak 0.12 / orta 0.08 / yakın 0.04, ağaç `treeParallax` 0.1; dikey aynı (0.92/0.85/0.7). Uzak bant artık ortalama zemin +5 (en yüksek zemine göre değil; ileride en arkaya dağ gelecek). `bushColorVariation` 0 (bantlar tek renk). Parallax konumu piksele (1/PPU) yuvarlanır.
- Yırtılma önerisi: Tiles.png Filter Point, Compression None, Generate Mip Maps kapalı; Main Camera'ya Pixel Perfect Camera (Assets PPU 16) + CinemachineCamera'ya CinemachinePixelPerfect.
- 27: Ekran görüntüsünde çalı bantlarında satır aralarında ince gökyüzü renkli yatay çizgiler (tile dikiş boşluğu, kamera yarım pikselde). JungleTheme `seamOverlap` 0.02: Sprite.Create PPU = 16/(1+0.02) → tile'lar %2 büyük, komşuya biner. Kalıcı çözüm Pixel Perfect Camera.
- 28: Pixel Perfect Camera (öneri Reference Resolution 384×216 = 1080p'de 5x) ile çizgiler gitti ama parallax kamera dururken titriyordu. JungleParallax konumu artık URP `beginCameraRendering`'de (kameranın son konumu) hesaplanır, piksel yuvarlama kapalı (`JungleParallax.SnapToPixels`). Titreme sürerse: Player Rigidbody2D Interpolate, CinemachineBrain Update Method = Late Update / Blend Update = Late Update, Pixel Perfect Upscale Render Texture kapalı dene.
- 29: Bant altı dolgusu çalının alt satırını tekrar ettiği için dama tahtası görünüyordu → düz koyu renk dolgu (çalı renginin sayfadan ölçülen gölge tonu; beyaz sprite + Tile.color, bant tint'iyle çarpılır). Dikey parallax hâlâ fazlaydı → `verticalSameAsHorizontal` (varsayılan açık): dikey = yatay faktör (0.04–0.12).

## 28. Gökyüzü + Demirci (30. adım, test bekliyor)
- Kullanıcının D:\Oyun\PROJECT-M\Assets\Sprites\background klasörü (576x324, PPU 16, Point): 4 katmanlı gökyüzü seti. c* gündüz (c1 gök, c2 uzak bulut, c3 kümülüs, c4 alçak bulut; c5/c6 birleşik), a* gün batımı (a gradyan, a5 çizgi bulut, a4/a3/a2 bulutlar; 5 = a2 kopyası; a7/a8 birleşik), b* alacakaranlık (b2 gök+ay, b3/b4/b5 mor bulutlar; b6/b7 birleşik), gece (1 yıldızlı gök, 2 ay, 3 alt bulut, 4 üst bulut; orig/orig_big birleşik).
- SkyBackdrop (yeni, Managers'a eklenir; Reset/sağ tık ile klasörden otomatik dolar): kameraya sabit, yatayda sonsuz tekrar (Tiled SpriteRenderer, 3 genişlik), katman başına follow (1 = sabit, ~0.96 hafif parallax) + drift (rüzgâr), alt kenar = ekran altı + bottomOffset −2, sıra −500+. Perde 1 gündüz, 2 gün batımı, 3 alacakaranlık, boss = gece, lobi = gündüz. Pozisyon beginCameraRendering'de, piksele hizalı.
- LevelGenerator: Demirci NPC (şablon = `blacksmith` alanı ya da sahnede "Demirci" adlı obje; kopyası fizik bileşenleri silinerek doğar, ayakları zemine oturur, NpcFacePlayer ile oyuncuya döner). Dükkan geçişinde tezgah dikdörtgeni yerine demirci (yazılar başının üstünde, [W] aynı). Her dövüş bölümü başında oyuncunun 7 kare sağında, yakına gelince rastgele selam yazısı.
- 30b: gökyüzü ekranın sadece alt kısmında kalıyordu (görsel 20.25 birim, kamera görüşü daha yüksek). Katman, ekranı kaplayacak kadar TAM SAYI katla büyütülür (needed = ekran yüksekliği − bottomOffset).
- 31: Bant diplerindeki düz koyu şerit (ve yakın bant ile zemin arası boşluk) → her bant `bushRowsPerBand` 2 sıra (2 kare aşağı, 3 kare kaydırılmış, iç içe), düz dolgu sadece en alt sıranın altında; `bandHeightShift` −1. ÖN PLAN (yeni): kamerayla karakter arası koyu siluet çalı öbekleri (1–3 çalı, aralık 16–34) + %35 ihtimalle gövde; negatif parallax `foregroundFactor` −0.35 / dikey −0.08, renk (0.12,0.14,0.12), sıra zemin+100, taban ort. zemin −2.
- 32: Bant dipleri arasında gökyüzü üçgenleri (çalı alt köşeleri) → dolgu AYRI katmanda (bant sırası −1) ve en alt sıranın alt satırını da kapsar. Bantlar `bushBands` (yeni ad), yükseklik uzak 4 / orta 2 / yakın 0. ÖN PLAN Hollow Knight tarzı: dolgu yok, ekranın ALT KENARINA kilitli (JungleParallax.LockToScreenBottom; dikeyde kamerayla), yatay −0.35; iki sıra iç içe çok koyu çalı tepesi (0.07,0.08,0.08) + %25 ekranı boydan geçen gövde; `foregroundScreenOffset` −1.
- 33: Kullanıcının Scripts'inde eski sürümler vardı (JungleTheme 31, LevelGenerator 25 → demirci yoktu); gönderilen dosyalar bağlı 'background' klasörünün "Claude outputs" alt klasörüne düşüp Unity'de çift sınıf hatası yaptı. Artık D:\Oyun\PROJECT-M\Assets\Scripts klasörü bağlı: dosyalar DOĞRUDAN oraya yazılır (SendUserFile kullanma, Assets içine kopya düşer). SkyBackdrop.cs şu an Assets/Sprites/background/Claude outputs içinde (tek kopya).
- 33: Ön plan KAPALI (kod duruyor). Orman 4 bant (`forestBands`): uzak 0.16/renk4/+6, uzak-orta 0.12/renk3/+4, orta 0.08/renk2/+2, yakın 0.04/renk0 (yeşil, koyu tint)/0; her bant 2 sıra + arkasında alt satırı da kapsayan düz dolgu → bantlar arasında gökyüzü boşluğu kalmaz.
- 34: En üst bant ile altındaki arasında küçük gökyüzü üçgenleri → bant dolgusu en ÜST sıranın tabanına kadar çıkar, en üst sıra 'lift' (1 kare kaldırma) almaz.

## 29. Zorluk ayarı (35. adım, test bekliyor)
- Veri (run_stats_v4, harita modundaki son 9 koşu): hepsi Perde 1'in 1–3. bölümünde ölüm. Ölümcül hasarın çoğu normal vuruş (30) + engellenemez (prefab 75 → %40 tavana takılıyor = tek vuruşta 40). 100 canla 2–3 vuruş = ölüm; bölümde 6+ düşman, arada iyileşme yok.
- RunManager (yeni alan adları, sahnedeki eski değerler ezmesin): `mapEnemiesStart` 4, `mapEnemiesPerStage` 0.75, `mapEnemiesMax` 12, `mapEnemiesPerPost` 2 (aynı anda 2 düşman), nokta 2–6. `mapDamageByAct` {0.7, 0.85, 1} (normal + engellenemez), `mapUnblockableDamageMultiplier` 0.6 (75 → ~31 Perde 1), `mapUnblockableGrowthMultiplier` 0.5, `mapHealOnPostCleared` 0.12 (nöbet grubu bitince +%12 can, yeşil yazı).
- Dosyalar artık doğrudan D:\Oyun\PROJECT-M\Assets\Scripts'e yazılıyor; Prefabs ve LocalLow\DefaultCompany\PROJECT M (run_stats, Player.log) da bağlı.

## 30. İnfaz barı + Kalabalık düşman + oyuncu HUD (36. adım, test bekliyor)
- ExecuteMeter (yeni, kendiliğinden oluşur): öldürme +0.34 (Kalabalık +0.15), dengeyi kıran parry +0.08; koşu başı DOLU (startFull). Doluyken E: TryConsume → hedef "ölümcül" işaretlenir; EnemyExecuteState: normal düşman ölür, boss faz 1'de canı Phase2At eşiğine iner (sonra faz 2'ye geçer), faz 2'de ölür; "İNFAZ!" yazısı. İnfazla ölen düşman barı doldurmaz. Bar boşken E → "İNFAZ BARI DOLU DEĞİL". RunManager.BeginRun → ResetForRun.
- Öldürme kolaylığı: PlayerDamage: sersemlemiş düşmana can hasarı ×2 (`staggeredHealthMultiplier`). RunManager (harita): düşman canı ×0.7 (`mapEnemyHealthMultiplier`), sersemleme süresi ×1.4 (`mapStaggerDurationMultiplier`).
- KALABALIK (EnemyArchetypeType.Swarm, enum SONUNA): Enemy Prefab'ın koyu (0.55,0.5,0.62), ×0.8 küçük kopyası; can ×0.45, denge ×0.4, hasar ×0.6, hız ×1.15, savrulur ×1.4; hamleler Pençe / İkili Pençe / Atılma (CreateSwarmMoves). Her nöbet noktasına ayrıca 1–3 (+1 / 3 bölüm) kalabalık (`mapSwarmPerPostMin/Max`, `mapSwarmGrowthEveryStages`). archetypeTemplates 5.
- BossController.Phase2At eklendi.
- PlayerHud (yeni, kendiliğinden): SOL ALT köşede sabit CAN (kırmızı, beyaz iz, iyileşmede yeşil, %25 altı yanıp söner, "73 / 100") + İNFAZ (altın, 3 bölme, dolunca parlar + "[E] İNFAZ"). Eski HealthBarUI / HealthUnitsUI objelerini gizler.

## 31. Akış: yoğunluk + seri + devriye (37. adım, test bekliyor)
- Veri (21:50 koşusu): 10 dk, 141 öldürme (14/dk), Perde 3, normal saldırıların ~%90'ı karşılanmış; ölüm Ağır'ın engellenemez vuruşu. Karar: genel kolaylaştırma YOK, akış artırıldı.
- RunManager (yeni adlar): `mapSwarmMin/Max` 2–4 kalabalık / nokta; `mapHordeChance` 0.25 → SÜRÜ noktası (1 güçlü + 5–6 kalabalık, ilk nokta hariç); devriyeler `mapPatrolsBase` 1 + 1 / `mapPatrolGrowthEveryStages` 2 bölüm (en çok 4), her biri 1–2 kalabalık, ara parçaların düz zemininde (LevelGenerator.CreatePatrolPoints, arenadan ±3 ve başlangıç/çıkıştan uzak, en az 10 kare aralık); `mapAggroRange` 17 (MakeGuard). BeginRun → KillStreak.ResetForRun.
- KillStreak (yeni, kendiliğinden): 3 sn içinde art arda öldürme = seri; öldürme başına +%3 denge+can hasarı (PlayerStats çarpanı, en çok %30), her 5 öldürmede +%3 can. PlayerHud can barının üstünde "SERİ ×N  +X% HASAR" + kalan süre çizgisi.
- Ağır profil engellenemez hasar ×1.15 → ×0.85.


## 32. ÖZET: oyunun şu anki hali (37. adım sonrası)
- **Döngü:** Lobi → 3 perde × (oda kapıları: Dövüş / Elit / Dükkan / Dinlenme) → Boss → Zafer. Her dövüş odası rastgele orman haritası (Dead Cells tarzı parça birleştirme), nöbet noktaları + sürü noktaları + devriyeler; hepsi ölünce çıkış kapısı açılır. Charm seçimleri sadece kapıdan geçince (bölümler arası) ve dükkanda.
- **Dövüş:** Sekiro tarzı denge/parry; düşman tipleri Düellocu, Çevik, Ağır, Okçu (ayrı sprite: Duelist, Archer; Çevik/Ağır/Kalabalık renklendirilmiş kopya), Kalabalık (zayıf sürü). İnfaz barı (öldürmeyle dolar, dolunca tek vuruş / boss faz atlatma), seri öldürme (hasar bonusu + can), nöbet temizleyince +%12 can.
- **Görsel:** Jungle tileset (çim zemin, köprü, su), 4 katmanlı hafif parallax orman + ağaçlar, perdeye göre gökyüzü (gündüz / gün batımı / alacakaranlık / gece boss), Demirci NPC (dükkan + bölüm başı selam), Pixel Perfect Camera önerildi (384×216).
- **Arayüz:** Sol altta can + infaz barı + seri sayacı (PlayerHud), düşman başı çubukları, boss çubuğu, RunUI menüleri.
- **Son veri (21:50):** 10 dk, 141 öldürme, Perde 3, ölüm Ağır engellenemez. 37. adım (yoğunluk/seri/devriye) henüz test edilmedi.

## 33. SONRAKİ ADIM ÖNERİLERİ (öncelik sırasıyla)
1. **37. adımı test + veri:** birkaç koşu → CSV'den öldürme/dk, ölüm yeri, seri uzunluğu; sürü/devriye sayısını ayarla. (RunStats'a "en uzun seri" ve "infaz sayısı" sütunu eklenebilir.)
2. **Kalabalık için gerçek sprite + yeni düşman tipleri:** sprite gelince Kalabalık'ı ayrı prefab yap; sonra uçan düşman (yukarıdan dalış, zıplayarak vurulur), kalkanlı (önden vuruş işlemez → arkasına dash), patlayan / zıplayan sürü. Her biri farklı cevap istesin.
3. **Kapı ve kamp ateşi sprite'ları:** çıkış kapıları ve dinlenme alanındaki dikdörtgenler hâlâ placeholder. (Tileset'te ağaç kovuğu 13–14 × 0–3 geçici kapı olabilir.)
4. **Dağ katmanı:** gökyüzü ile uzak çalılar arasına (kullanıcı planlıyor); sprite gelince SkyBackdrop'a ya da JungleTheme'e katman.
5. **Ön plan (Hollow Knight tarzı) yeniden:** kod JungleTheme'de kapalı duruyor (`foreground`); daha iyi siluet sprite'larıyla tekrar denenebilir.
6. **Boss'lar:** her perdeye ayrı boss (şu an aynı boss + faz 2). Boss arenasına özel görsel / mekanik (platform, düşen kaya vb.).
7. **Ses ve his:** öldürme / infaz / seri sesleri, ekran sarsıntısı, infazda kısa yakın plan zoom.
8. **Meta ilerleme:** koşular arası kalıcı kilitler (MetaProgress var) → lobi'de demirciden kalıcı yükseltme satın alma.
9. **Charm dengesi:** son koşularda Zehir/Salgın yığınları çok güçlü görünüyor; CSV ile kontrol.
10. **Temizlik:** eski HealthBarUI/HealthUnitsUI, EnemyBalanceBar gibi kullanılmayan UI'ları sahneden kaldırmak; SkyBackdrop.cs'yi Scripts'e taşımak; Managers'taki EnemyController kalıntısını kontrol.

## 34. YARININ PLANI (4 Ekim, kullanıcı istedi) → 5 Ekim'de 38. adımda YAPILDI (bkz. 35)
1. Arayüz boyutu · 2. Başlangıç ekranı · 3. Yetenek sistemi (Q) · 4. Kalıcı gelişim (Öz + Demirci).

## 35. Ana menü + ayarlar + yetenek + kalıcı gelişim (38. adım, test bekliyor)
- 37. adım test edildi, şimdilik dokunulmuyor.
- **GameSettings (yeni, statik, PlayerPrefs):** UiScale (0.6–1.6) tüm OnGUI'lere çarpan (`GameSettings.GuiScale(bileşenÇarpanı)`: RunUI, PlayerHud, EnemyOverheadBars), WorldTextScale (CombatCallout.CreateText + CritFeedback TextMesh characterSize → İNFAZ!, DASH!, kapı yazıları, kritik), Volume (AudioListener), Fullscreen. Arayüz kaydırıcısı fare bırakılınca uygulanır (titreme olmasın).
- **Ana menü (RunUI, Lobi durumu):** sol tarafta koyu geçiş + "PROJECT M", maddeler BAŞLA / KALICI GELİŞİM (öz sayısı) / AYARLAR / ÇIKIŞ (W/S, ↑/↓, Enter, fare). BAŞLA: ısı açıksa Yeni Koşu paneli (eski lobi, ‹ GERİ), değilse direkt başlar. Arkada RunManager lobide rastgele orman haritası kurar (`menuBackgroundLevel`, Generate(seed,1,false) → demirci başta selam verir; gökyüzü lobi = gündüz). Sonuç ekranı düğmesi artık "ANA MENÜ".
- **Duraklatma (Esc, sadece Fighting):** DEVAM / AYARLAR / ANA MENÜ (iki kez bas onayı → RunManager.AbandonRun: istatistik + meta + öz kaydedilir, ResetRun, döngü lobiye). RunManager.SetPaused: HitStop temizler, timeScale 0'da tutar, canControl'ü saklar/geri verir; GuardFrozenTime bunu sayar; duraklamada InteractPressed (kapı/tezgah [W]) çalışmaz.
- **Yetenek (Q) — PlayerAbility (oyuncuya RunManager ekler) + AbilityTypes.cs (AbilityType enum + AbilityInfo):** tek yuva, bekleme süresi (Şok 10, Gölge 6, Buz 13, Alev 9 sn), parry −1.5 sn / öldürme −0.75 sn şarj. Seviye 1-3: +%25 güç, −%10 bekleme. Hasar max denge/canın YÜZDESİ, PlayerDamage hattından (DamageSource.Ability, enum SONUNA): kritik + charm'lar çalışır, riposte hakkı yemez (Parryriposte). Boss'a ×0.5.
  - Şok Dalgası (baştan açık): r 4.5, denge %30 / can %6, itme 9, 0.25 sn korunma, halka efekti.
  - Gölge Adım: 9 birimde en yakın düşman (baktığı yön öncelikli, |dy|≤4), ARKASINA ışınlan (zemin raycast + OverlapBox ground|wall; çukur/duvar → "YER YOK"), düşmana döner, denge %40 / can %10, 0.4 sn korunma, mor iz.
  - Buz Nefesi: EnemyTime.RequestRamp(3.5 sn, 0.35) (tüm düşmanlar), öndeki kutu (6×3) denge %20 + 1.5 sn saldırı başlatamaz.
  - Alev Dalgası: öndeki kutu (6.5×2.6) denge %10 + YANIK (EnemyStatus.ApplyBurn yeni, zehirden ayrı sayaç: denge %7/sn, can %4/sn, 4 sn; sersemlemiş düşmana can yanığı ×2).
  - Koşu başında RunState.AbilityOffer (enum SONUNA) kart seçimi (açık yetenek 1 ise direkt verilir + banner). Dükkanda: "Yükselt" (80) + açık başka bir yetenekle "Değiştir" (70, seviye korunur). ShopItemKind.Ability + ShopItem.ability/abilityUpgrade.
  - PlayerHud: infaz yazısının sağında [Q] yuvası (aşağıdan dolar, hazırken renkli nabız, kalan sn, seviye noktaları). PlayerHud lobide çizilmez.
- **Kalıcı gelişim — MetaProgress (aynı kayıt anahtarı, yeni alanlar):** essence, totalEssence, upgradeIds/Levels, selectedAbility. Koşu sonu öz = öldürme×0.25 + (perde−1)×20 + boss×25 + zafer 50, ×(1+0.2×ısı)×Öz Toplayıcı (RunManager.GrantEssence; sonuç ekranında "+N ÖZ (döküm)"). MetaUpgrades.All (Demirci sayfası): Dayanıklılık +10 can ×5, Nefeslenme nöbet iyileşmesi +%3 ×3, Cellat infaz dolumu +%15 ×3, Odaklanma yetenek bekleme −%8 ×3, Pazarlık dükkan −%8 ×3, Kese +25 başlangıç altını ×3, Öz Toplayıcı +%10 ×3, yetenek kilitleri Gölge Adım 60 / Buz 90 / Alev 90. BeginRun uygular (SetMaxHealth taban+bonus, Gold, Ability.Clear); ExecuteMeter.Add × ExecuteFillMultiplier; Price × ShopPriceMultiplier.
- Değişen/yeni dosyalar: GameSettings, AbilityTypes, PlayerAbility (yeni); Runui, Runmanager, RunTypes, MetaProgress, PlayerHud, Enemyoverheadbars, CombatCallout, Critfeedback, EnemyStatus, ExecuteMeter, Parryriposte, Damageinfo.
- Test listesi: menü gezinme + ayar kaydırıcıları; Esc duraklat → ana menü; yetenek seçimi (Kalıcı Gelişim'den Gölge Adım açınca seçim ekranı çıkar); Gölge Adım'ın duvar/çukur kenarında davranışı; Buz'un parry slow-mo ile birlikte his; öz miktarı (bir koşu ~40–120 hedef).
- 38b: Seçim ekranı hiç çıkmıyordu (sadece Şok açıktı → direkt veriliyordu). Artık koşu başında HER ZAMAN 4 kart; kilitliler gri + "KİLİTLİ, Kalıcı Gelişim ◆ bedel" (seçilemez). Gölge Adım da baştan açık (Demirci listesinden çıktı); Buz 90 / Alev 90 öz. RunManager `testUnlockAllAbilities` (Inspector, TEST): hepsi seçilebilir + dükkanda çıkar. Yardımcılar: RunManager.IsAbilityAvailable, MetaProgress.AbilityUnlockCost.

## 36. Denge: normal vuruş çok güçlüydü + boss'ta infaz barı (39. adım, test bekliyor)
- Veri (10:36 zafer koşusu, 16 dk, 310 öldürme): normal saldırıların %34'ü parry, %34'ü KESİLDİ (oyuncu vurarak bozuyor), yendi %8. Kritik 627/968 isabet (%65). Build: Ölümcül Darbe ×5, Ağır Darbe ×5, Keskin Gözler ×5, Keskin Uçlar ×4 → üstel yığılma (can ×4.5, kritik ×4.5 çarpan) → düşman saldıramadan ölüyor, parry/Q gereksiz.
- StatCharmEffect: çarpan artık DOĞRUSAL (1 + (m−1)×istif). Katalog: Keskin Gözler +%10 kritik / max 3, Ölümcül Darbe +0.35 / max 3, Keskin Uçlar +%15 denge / max 3, Ağır Darbe +%20 can / max 3. PlayerStats `critChanceCap` 0.5.
- RunManager (yeni adlar): `runAttackArmorPoint` 0 (eski runAttackCommitPoint 0.15): düşman saldırısı uyarı başladığı an zırhlı, vurarak kesilmez (beyaz flaş) → cevap parry/block/dash/Q. `runPlayerAttackBalanceMultiplier` 0.75 (PlayerDamage.AttackBalanceMultiplier, kombo+slam denge hasarı), `runParryBalanceMultiplier` 1.3 (ConfigureEnemy, enemy.parryBalanceDamage).
- PlayerAbility: parry şarjı 2.5 sn; Şok Dalgası ve Buz Nefesi zırhı DELER (kararlı saldırıyı keser, boss hariç, "KESİLDİ").
- ExecuteMeter boss kaynakları: boss dengesini kırmak +0.5 (vuruş ya da parry), boss'a her parry +0.08, boss'a can hasarı (max canın oranı ×0.6). Not: CombatEvents.BalanceBroken hiçbir yerde tetiklenmiyor (EnemyHit.brokeBalance + Parry kullanıldı).
- Ayar: hâlâ kolaysa armor 0 kalsın, runPlayerAttackBalanceMultiplier 0.6; çok zorsa runAttackArmorPoint 0.3. Hedef CSV: n_kesildi < %10, n_parry > %50.

## 37. Akış + harita + düşman çeşitliliği (40. adım, test bekliyor)
- Not: 38b'de gönderilen Runui.cs ve AbilityTypes.cs Scripts'te ESKİ haline dönmüştü (büyük ihtimalle açık IDE eski tamponu kaydetti). 40. adımda ikisi yeniden yazıldı. Claude dosya yazarken o dosyaları editörde kaydetme / "yeniden yükle" seç.
- **A · Hareket akışı**
  - AirAttackState (yeni, PlayerCombatController başlatır): havada saldırı ARTIK HEMEN çıkar. Yan vuruş (inişe kadar `maxAirAttacks` 2, düşüş `airAttackMaxFallSpeed` 3 ile yavaşlar). ↓ + saldırı = AŞAĞI vuruş (kutu `downAttackBoxSize`, denge ×1.2, DamageInfo.fromAbove): düşmana / oka / DİKENE / vazoya değerse POGO (`pogoBounceVelocity` 15), dash beklemesi + havada saldırı hakları yenilenir. Animator'da "AirAttack"/"DownAttack" state'i varsa onu, yoksa Attack1-2 / Attack3 oynar.
  - Kombo iptal: yeni `attackCancelPoint` 0.45 (comboCancelPoint ile küçük olan).
  - GROUND SLAM v2 (PlayerController yeni alanlar): yarıçap ≥ `slamMinRadius` 3, düşüş yüksekliğiyle güç ×1..×2 (`slamFullPowerDrop` 6), denge = max dengenin %18'i × güç, düşmanları GERİ İTER + havaya kaldırır (`slamPushForce` 10 / `slamLiftForce` 5), zırhı deler (boss hariç), halka efekti. Düşmana değerse oyuncu sıçrar (`slamBounceVelocity` 10) → havada zincir; değmezse eski kısa kilit. Yükselirken de ↓+Space ile slam (JumpState). Slam'in Space'i zıplama tamponunu temizler.
  - Geçişler (RunManager "Akış"): `clearedPause` 0.35 (1.0 idi), menü sonrası bekleme 0.1 (tampon CancelAttack ile temizlenir), haritadan haritaya kısa kararma (`transitionFadeOut` 0.12 / `transitionFadeIn` 0.25, RunUI.DrawFade), tek kapı (ÇIKIŞ/BOSS) içine girince [W]'siz geçer (`autoEnterSingleDoor`).
- **B · Harita çeşitliliği**
  - Yeni işaretler: C sandık, V vazo (%70 çıkar), ^ diken. LevelProps (yeni, LevelGenerator objesine eklenir): sandık vurunca ya da [W]/[↑]/[F] → altın 30 (+%25/perde) + %40 charm (çıkışta, "SANDIK: CHARM") / %30 +%15 can; vazo 2–6 altın; diken max canın %12'si + yukarı fırlatma, aşağı vuruşla pogo. Saldırılar LevelProps.HitArea çağırır (AttackState, AirAttackState, Slam). Sprite hücreleri Inspector'da (chest 18,17 2x2 / açık 20,17 / vazo 21,19 1x2 / diken 21,16) — yanlışsa oradan düzelt; JungleTheme.PropSprite (pivot alt-orta).
  - Yeni parçalar: Kule Yukarı/Aşağı (±5, platformlarla), Üst Yol (sandık, iki platform), Diken Çukuru, Dikenli Köprü, Vazolu Düz; arenalar: İki Katlı (platform + vazo), Dikenli Orta. Platformlar zeminden +2 (Platformlu Çukur ile aynı).
  - MEYDAN OKUMA: normal dövüş odası %30 (`challengeChance`, ilk oda hariç): süre = 25 + 5×düşman sn; içinde temizlersen bonus charm + 30 altın. HUD'da "SÜRE N sn".
- **C · Düşman çeşitliliği** (EnemyArchetypeType SONUNA: Shielded, Flyer, Bomber; şablonlar renklendirilmiş Enemy Prefab kopyası, archetypeTemplates 8)
  - KALKANLI (EnemyShield): önden gelen kombo/yan vuruşu engeller (oyuncuyu iter, "KALKAN"), 5 önden vuruş kalkanı 2.5 sn düşürür; arkadan, yukarıdan (pogo), slam, yetenek, parry geçer. Önde açık mavi çubuk görseli. Düellocu hamleleri.
  - UÇAN (EnemyFlyer): yerçekimi kapalı, başın 3.2 üstünde süzülür, saldırı uyarısında oyuncunun hizasına DALAR; sersem/ölü düşer. Kalabalık hamleleri, standby kapalı.
  - PATLAYAN (EnemyBomber): 2.2'ye yaklaşınca 0.9 sn fitil (kırmızı yanıp söner) → patlama r 2.6: oyuncuya max canın %20'si (engellenemez, dash'le kaçılır) + çevredeki düşmanlara denge %50 / can %25. Öldürülürse 0.35 sn sonra sadece düşmanlara patlar (zincir). Kalabalık doğarken perde başına %15/%25/%35 Patlayan.
  - Oda tipi zarı: perde başına Kalkanlı/Uçan ihtimali (0.1/0.1, 0.15/0.15, 0.2/0.2) `specialTypeChancePerAct`.
  - ELİT EKLERİ (`eliteAffixes`, oda başına bir ek, banner "ELİT • …"): HIZLI (hız ×1.3, uyarı/recovery ×0.8), ZIRHLI (denge ×1.6, az savrulur), KALKANLI, PATLAYICI (ölünce 0.5 sn sonra r 3 patlar, oyuncuya da vurur).
  - ExecuteMeter: Patlayan da kalabalık dolumu alır.
- Bilinen riskler: Uçan'ın kovalaması yere göre yazılmış ChaseState ile; garip hareket ederse EnemyFlyer hoverHeight/diveSpeed ayarla ya da specialTypeChancePerAct y=0. Sandık/vazo/diken hücreleri tahmin.

## 38. Dikey haritalar + yokuş + çim platform + basit gövdeler (41. adım, test bekliyor)
- Vuruş hızı geri alındı: 40. adımdaki `attackCancelPoint` kaldırıldı, kombo iptali yine sadece `comboCancelPoint`.
- UÇAN ve PATLAYAN kendi prefab'ı yoksa kodla çizilen gövde (EnemyShape, yeni): Uçan = gözlü yatay kapsül, Patlayan = gözlü daire + fitil. Ana SpriteRenderer'ın sprite'ı değişir, Animator kapanır.
- DİKEY ROTA (Dead Cells gibi aşağı-yukarı), LevelGenerator: her arenadan önce `verticalChance` 0.85 ile kat değişimi: hedef kat rastgele (en az `verticalMinChange` 8 fark, sınır ±`maxVerticalRange` 24), `maxClimbChunks` 3'e kadar dikey parça. Ara parçalar artık o anki kata göre kayar (driftBase).
- Yeni ChunkKind.Climb (enum SONUNA) parçaları (NoStretch: yatay genişletilmez): Duvar Bacası Yukarı (+10, 5 genişlik baca: asılı duvar ↔ yüksek blok arası DUVAR ZIPLAMA), Duvar Bacası Aşağı (−10, duvar kayması), Platform Merdiveni / İnişi (±8, çim platformlar +2 aralık), Yüksek / Alçalan Basamaklar (±6), Derin İniş (−14, düşüşü kesen iki platform). LevelChunk.stretch / NoStretch().
- YOKUŞ: yeni işaretler '/' (yukarı) '\\' (aşağı). Tile = tepe seti 5,1 / 8,1, sprite şeklinden çarpışma (Tile.ColliderType.Sprite); altı 5,2 / 8,2. Genişletmede '/' ilk kopyada eğim, sonrakiler düz. Parçalar: Yokuş Yukarı (+2), Yokuş Aşağı (−2), Tepe (yokuş). JungleTheme: SlopeTile / SlopeBaseTile, hücreler Inspector'da; yokuşun üstüne çim ucu konmaz.
- UÇAN PLATFORMLAR artık TAHTA DEĞİL: çim setinin yüzey satırı (0 sol / 1-3 orta / 4 sağ köşe) + üstte çim uçları ("Platform Çim Uçları" katmanı). İki yanı zeminli '=' hâlâ ip köprü.
- Kontrol: duvar kayma / zıplama için Player'ın Wall Mask'ı haritanın zemin katmanını içermeli. Yokuşta kayma/takılma olursa PlayerMovement'ta eğim desteği gerekebilir. Çok aşağıda arka plan bantlarının koyu dolgusu görünür (yeraltı hissi).
- 41b: Duvar bacası 2-3 zıplamada yetişemiyordu → baca 3 kare genişlik (5 idi), duvarlara yapışık 1 karelik çim tutunma çıkıntıları (her 3 karede bir, karşılıklı); yine +10 / −10. Arka plandaki boş koyu alan → JungleTheme ARKA DUVAR (backWall): her sütunda yakındaki (±5) en yüksek zeminin 7 üstüne kadar kaya deseni (2x2 desen 4-5 × 10-11, ara sıra 7,11 ve 9,10-11; tepe 7,10), zeminle birlikte durur (parallax yok), sıra −8 (çalı bantlarının önünde, zeminin arkasında). Ayarlar Inspector'da (backWallHeight, Window, Tint, hücreler).
- 42: Taş ARKA DUVAR kullanıcı isteğiyle KALDIRILDI (kod tamamen silindi). Not: 41b'de iki dosya Scripts'e eski haliyle yazılmıştı (aynı outputs yolu yeniden kullanılınca); artık her gönderimde ayrı klasör (outputs/vNN) + geri okuyup karşılaştırma.
- 43: UÇAN ve PATLAYAN şimdilik KAPALI (kafa karıştırıyordu): RunManager `enableFlyers` / `enableBombers` false (yeni alanlar). Kod duruyor (EnemyFlyer, EnemyBomber, EnemyShape); açınca geri gelir. Patlayıcı elit eki de Patlayan kapalıyken çıkmaz. Kalkanlı + diğer ekler (Hızlı/Zırhlı/Kalkanlı) aktif.
- 44: Dikey haritalar çok sıkışıktı → dar Climb parçaları (duvar bacaları, 3'lük platform merdiveni, 3'lük basamaklar, derin iniş) SİLİNDİ; yerine GENİŞ olanlar: Geniş Merdiven Yukarı/Aşağı (±8, 5'lik platformlar, +2 aralık), Geniş Basamaklar Yukarı/Aşağı (±6, 6 genişlik), Geniş Kule Yukarı/Aşağı (±10, karşılıklı 5'lik platformlar), Geniş İniş (−10). Duvar zıplaması GEREKMEZ. LevelGenerator yeni alan adları (eski değerler ezmesin): `verticalRouteChance` 0.6 (0 = dümdüz harita), `verticalStep` 6, `verticalRange` 14, `maxClimbChunks` 2.
- 45: PARRY SONRASI TEPKİ: (1) savunmadayken saldırı tamponu artık SİLİNMİYOR (eskiden parry penceresi açıkken basılan saldırı kayboluyordu). (2) Başarılı parry'de pencere `postParryGrace` 0.06 sn içinde kapanır. (3) Başarılı parry'den sonra `parryCancelTime` 0.45 sn içinde saldırı tuşu parry/block'u keser ve saldırı ANINDA başlar (PlayerDefenseController.RecentlyParried). Basılı tutmak hâlâ block (kombo güvenliği), boşa giden parry hâlâ saldırıyla kesilemez.
- 46: AKIŞ DÜZELTMELERİ (PlayerController "Akış v2" yeni alanlar):
  1) Tampon: dash / parry / saldırı tamponu en az `minInputBuffer` 0.3 sn (hasar sersemlemesi 0.22 sn tuş yutmasın).
  2) Kombo ortasında dönme: her yeni vuruş (yer ve hava) başlarken basılı yön tuşuna döner (PlayerCombatController.FaceInputDirection).
  3) Zıplama saldırıyı HER AN keser (vuruş karesini beklemez).
  4) Hasar sersemlemesinin ikinci yarısında (`hurtDashCancelAfter` 0.5) dash ile çıkılır; yön tuşu okunur, korumalı dönem sürer.
  5) Koşarken ilk vuruş momentum taşır: hız × `attackMomentumCarry` 0.12 (en çok `attackMomentumMax` 1 birim) vuruş ilerlemesine eklenir.
  6) İnişe 0.7 birimden yakınken yan hava vuruşu başlamaz → tampon bekler, yere değince YER kombosu çıkar (AboutToLand).
  7) Slam düşmana değmeden inince kilit `slamLandLock` 0.05 sn (0.12 idi).

## 39. Yön kararı: parry = ana mekanik → 1v1 ağırlıklı (47. adım, test bekliyor)
- Gerekçe: parry okumak/zamanlamak ister, kalabalıkta bu kaybolur (veri: kalabalıkta oyuncu vurarak kesiyor + hasar yığıyordu). Plan: (1) her karşılaşma küçük DÜELLO, (2) az ama belirgin düşman tipleri + imza saldırısı, (3) elit/boss gerçek düello, perde başına ayrı boss, (4) parry'yi besleyen ödüller (davranış charm'ları), (5) kısa, kararlı haritalar.
- 1. adım YAPILDI — RunManager "Düello karşılaşmaları" (`duelEncounters` açık): her nöbet noktası = 1 ANA düşman (kalabalık olmayan tip) + `duelSwarmChance` 0.6 ile 1..`duelSwarmMaxPerAct` {1,2,2} kalabalık; nöbet sayısı `duelPostsPerAct` {2,3,3}, elit odası `duelElitePosts` 2; sürü noktası yok; devriye `duelPatrols` 1 × 1 kalabalık; arenalar arası ara parça `duelMinFillers` 1 – `duelMaxFillers` 2 (LevelGenerator min/maxFillers'ı koşu sırasında ezer). Kapatınca eski kalabalık düzen.
- Sıradaki (2. adım): düşman tiplerine imza saldırısı + perde başına ayrı boss.
- 48 (2. adım, test bekliyor): 
  - İMZA SALDIRILARI (AttackMove.signature, EnemyMoveset.Signature(type)): Düellocu "Kılıç Dansı" (5: N,N,gecikmeli N,süpürme,N), Çevik "Fırtına" (6 hızlı, 5. gecikmeli), Ağır "Deprem" (N,süpürme,yakalama,gecikmeli ağır N), Okçu "Ok Yağmuru" (5 ok), Kalkanlı "Kalkan Hücumu" (yakalama-hücum + 2 N). Başlarken düşmanın üstünde adı (turuncu). Son Normal vuruşu parry'lemek dengeye ×3 (MoveHit.parryBalanceMultiplier / ParryReward, EnemyAttackState.HandleParry, "KUSURSUZ!"). EnemyArchetype.Apply her tipe (Kalabalık hariç) ekler.
  - PERDE BOSS'LARI (EnemyMoveset.CreateBossMoves(act, faz2), BossController.Setup(..., act)): 1 Kılıç Ustası = eski set; 2 Kızıl Düellocu = Çevik seti + Fırtına, hızlı (koşu ×1.25, recovery ×0.8), faz 2 "Kızıl Kasırga" (7 vuruş, yakalama dahil) + uyarılar ×0.88; 3 Gölge Efendisi = Ağır seti + Deprem + Kılıç Dansı, az savrulur, faz 2 "Gölge Zinciri" (6 vuruş, son parry ×3.5) + uyarılar ×0.85.
  - Düello ANA düşmanı güçlendi: can `duelMainHealthMultiplier` 1.4, denge `duelMainBalanceMultiplier` 1.3, recovery `duelMainRecoveryMultiplier` 0.8.
- 49: Harita yine DÜZ (kullanıcı isteği): LevelGenerator `verticalRoutes` false (kat değişimi / Climb parçaları kullanılmaz), Kule Yukarı/Aşağı ağırlığı 0. Kalanlar: yokuşlar (±2), küçük basamaklar, çim platformlar, sandık/vazo/diken, Üst Yol. Climb parçaları ve kodu duruyor (açınca geri gelir).

## 40. Silahlar + anlamlı build + risk/ödül (50. adım, test bekliyor)
- Gerekçe (son kazanılan koşu CSV'si): iyileşme 525 = alınan hasar 525 (gerilim yok), 1513 altın harcanmadı, 21 charm (karar yok).
- SİLAHLAR (WeaponTypes.cs, PlayerWeapon.cs; enum SONUNA ekle): sprite GEREKMEZ → aynı animasyonlar silah hızında oynar (Animator.speed = 1/Duration), vuruş anında silah renginde kodla çizilen hilal KESİK İZİ, vuruş kutusu/ileri hareket/hasar silaha göre.
  - Kılıç: dengeli (×1). Hançerler: süre ×0.68, menzil ×0.8, denge ×0.6, can ×0.65, +%15 kritik, riposte +2. Mızrak: süre ×1.1, menzil ×1.7 (yükseklik ×0.8), ilk vuruş atılır (ileri ×2.6). Büyük Kılıç: süre ×1.45, menzil ×1.35 (yük. ×1.3), denge ×1.9, can ×1.7, 4. vuruş şok dalgası (r 2.6, denge %20, itme).
  - Koşu başında SİLAH SEÇİMİ (RunState.WeaponOffer, yetenek seçiminden önce). Kılıç+Hançer açık; Mızrak 70 öz, Büyük Kılıç 90 öz (Demirci). Dükkanda başka silah kartı (90 altın).
  - PlayerDamage.WeaponBalanceMultiplier/WeaponHealthMultiplier sadece DamageSource.Attack'a uygulanır. PlayerCombatController.EffectiveHitBox.
- BUILD: charm YUVASI 6 (`charmSlots`). Dolunca yeni charm seçilirse RunState.CharmReplace ekranı: birini bırak ya da VAZGEÇ (0/Esc). Dükkanda yuva doluyken yeni charm alınamaz. Pasif stat charm'larının yığını düşürüldü (Keskin Gözler/Ölümcül Darbe/Keskin Uçlar/Ağır Darbe 2, Parry Şifası/Hasat 3).
- EFSANEVİ charm'lar (oynanışı değiştirir, CharmDefinition.legendary, ağırlık 0.25, tek yığın): Denge Patlaması (dengesi kırılan düşman çevreye %30 denge + itme), Hayalet Kılıç (4. vuruş 0.16 sn sonra tekrar), İnfazcı (infaz barı ×1.5, infaz öldürmesi %8 can + Q doldur), Kan Ritmi (her parry %3 can, ama oda arası/sonrası iyileşme YOK), Kusursuz Refleks (parry penceresi ×0.7, riposte ×1.6, parry Q bekleme −3 sn), Cam Kalp (max can ×0.7, her öldürme +%10 hasar en çok %60, kat başında sıfırlanır).
- RİSK/ÖDÜL: kat arası iyileşme ×0.4 (`betweenStageHealMultiplier`, boss hariç), oda sonrası ×0.5 (`postHealMultiplier`). LANETLİ SANDIK (mor, %45 ihtimalle odada ek): 50 altın + 2'li efsanevi seçim AMA lanet: 2 oda alınan hasar ×1.35. Dükkan: efsanevi (150), silah (90), laneti kaldır (60).
- UI: HUD charm panelinde silah adı + "charm n/6" + lanet satırı; efsanevi kartlar turuncu; dükkan özel satırı 2 sütun; dükkan paneli 600 yükseklik.
- Dokunulan: WeaponTypes, PlayerWeapon, 6 efekt (+ .asset gerekmez, katalog kodla oluşturur), PlayerDamage, PlayerCombatController, AttackState, AirAttackState, PlayerAnimationController, PlayerController, ExecuteMeter, PlayerAbility, Charmdefinition, Charmcatalog, Runmanager, RunTypes, MetaProgress, LevelProps, LevelGenerator, Runui.
- Ertelendi: oda çeşitliliği (4. madde).
