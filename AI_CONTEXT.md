# PROJECT M — PROJECT CONTEXT

## Project overview
- Engine: Unity
- Game: 2D action platformer
- Theme: Dark samurai / ronin combat
- Primary goal: A small, polished game with responsive, satisfying combat.
- Main priorities: Input responsiveness, combat feel, clear enemy telegraphs, readable feedback.

## Core combat direction
- Parry is a core defensive mechanic.
- Enemy posture/balance is important to combat.
- Executions are used against vulnerable enemies.
- Dash and block support combat and positioning.
- Combat should reward timing and deliberate decisions.
- Avoid unnecessary systems that do not meaningfully improve moment-to-moment gameplay.

## Architecture
- Use the existing player state machine and combat architecture.
- Player combat has used ICombatState, AttackState, and PlayerCombatController.
- Enemy behavior has used EnemyController and IEnemyState.
- Treat these names as historical context: inspect the actual project before relying on them.
- Preserve working code and existing serialized references.

## Design decisions
- Prioritize a small number of meaningful mechanics.
- Enemy encounters should be readable and coordinated.
- Avoid overwhelming the player with simultaneous enemy attacks.
- Do not reintroduce removed progression, shop, charm, weapon, or procedural-generation systems without explicit approval.

## Development workflow
- Inspect before editing.
- Make focused changes.
- Explain required Unity Inspector setup.
- Test when possible.
- Record task results in AI_HANDOFF.md.

## Important
This document contains project direction and historical context, not a guarantee that every listed script or feature currently exists.
Inspect the current repository and Unity project before modifying code.
When current files conflict with this document, report the discrepancy and confirm before making destructive changes.

## Art pipeline (3D render -> 2D sprite) — kalıcı karar (Ekim 2026)
- Karakterler hariç dünyadaki her şey (ağaç, çadır, kaya, çit, kulübe...) Blender'da 3D modellenip 2D render olarak oyuna konur. Render gri/nötr alınır (şeffaf arka plan), renk Python ile sprite'a bake edilir.
- Betikler: `Assets/Sprites/_ArtPipeline/tree_colorize.py` (ağaç) ve `tent_colorize.py` (çadır/prop). Yeni modelde bunlar kopyalanıp maskeler/paletler değiştirilir.
- AĞAÇ STİLİ (sahnedeki TÜM ağaçlara uygulanacak): taç = 3 büyük, birbirinin üstüne binen yumuşak geçişli sulu boya katmanı (sert katman sınırı yok, geçişler yumuşatılmış), çok az gürültü/tane, silüet çevresinde koyu halka/outline YOK. Gövde = nötr kahverengi, çevresinde outline ve ay-kenar ışığı izi YOK (rim maskesi sadece tacı kapsar). Renk rampası taç: (18,70,38) -> (40,122,56) -> (112,180,86) civarı, ışık sağ üstten.
- Gece/gündüz uyumu + rüzgâr: ağaçlar `M_Tree_Coloured` materyalini (ProjectM/SilhouetteRim, M_Tree_Wind kopyası: normal 0, rim 0.18, wind 0.55, haze 0.06) kullanır. Bu shader TimeOfDay'in global bodyTint/bodyLift/haze değerlerini uygular, kenar ışığını ve tepe sallanmasını kendi verir. Bu yüzden ağaçta ayrıca `~moonrim`, `FenceMoonGlow` ve `TreeSway` KULLANILMAZ (çift ışık/çift sallantı olur). `TreeSway.cs` yedek olarak dosyada duruyor.
- Sprite ayarları: ppu 80 (ağaç), Single, BottomCenter, FullRect, Uncompressed. Materyal: ağaç `M_Tree_Coloured`; çit/kaya/çadır gibi proplar `M_SpriteLitGray_Near` (ay kenarı için `~moonrim` çocuğu + `FenceMoonGlow`).
- Derinlik: sorting order < 10 olan nesneler zemin dudağının (y≈-4.9) arkasında kalır; ağaç tabanı bu yüzden order 8'de.
