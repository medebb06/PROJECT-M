# PROJECT M — TASK HANDOFF

## Current task
Mimari inceleme (salt okunur) tamamlandı: 2026-10-16. Oyun kodu, sahne, prefab ve ProjectSettings dosyalarına dokunulmadı.

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
- Derleme ve oynanış doğrulanmadı.
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
