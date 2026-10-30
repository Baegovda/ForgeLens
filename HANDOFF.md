> **For AI agents: 2026-10-29 20:50 KST — SM2 was transferred to a NEW Grok Bot: read [`docs/BOT_TRANSFER.md`](docs/BOT_TRANSFER.md) first** (BongThumbLive stays with the original bot).

> **Standing rules: `docs/RULES_CORE.md` (read once per session + after summary; then run `tools/re/pc/rules_check.ps1`). Subagent preamble: `docs/SUBAGENT_PREAMBLE.md`.**

> **Conversation notes & naming (2026-10-29):** in Korean chat use 기드라 = Ghidra and 레아 = REA. When the owner is away, PC jobs may run at High priority.
> Details: [`docs/BOT_TRANSFER.md` §11](docs/BOT_TRANSFER.md).

> **New assistant: start at [`docs/handoff/START_HERE.md`](docs/handoff/START_HERE.md)** (single entry point, 2026-10-29). Everything below is the request ledger and chronological log.

> **Then read [`AGENTS.md`](AGENTS.md)** (entry point, rules, user preferences), then `docs/handoff/` (STATUS, ROADMAP, WORKFLOW, RE_NOTES, FORMATS). Where this file's bootstrap below conflicts with them (push method, next step, owner), AGENTS.md / docs/handoff win. This file stays the chronological log: append a dated section per stage.

# 사용자 요청 대장 (User Request Ledger)

> 오너(Baegovda님)가 지금까지 한 **모든 요청과 상시 규칙**을 한곳에 모은 대장이에요. 새 요청이 오면 **바로 여기에 한 줄 추가**하고, 상태가 바뀌면 같은 줄을 고쳐요(오너 요청, 2026-10-29: "내가했던 요청들은 인수인계문서에 꼼꼼히 적어놓으렴").
> - 출처: 봇 메모리(오너 발언 요지), 이 파일의 단계별 기록, `AGENTS.md`, `docs/handoff/{STATUS,ROADMAP,WORKFLOW,RE_NOTES}.md`, box `/workspace/mtl_fix/*.md`(`overnight_decisions.md`, `datapars_progress.md`, `heuristics4_progress.md`), `/workspace/animthumb/REPORT.md`, `git log`. 중복은 합쳤어요.
> - 날짜는 KST(2026년). 커밋 sha는 `origin/cursor/sm2-pck-extractor-4420` 기준이에요.
> - 상태: **done**(완료, sha) · **in progress**(진행 중) · **pending**(대기, 아직 시작 전) · **future**(로드맵 밖, 요청 시에만) · **standing**(상시 규칙, 항상 적용).
> - 규칙이 서로 충돌하면 **날짜가 늦은 쪽이 이겨요**(예: R04가 AGENTS.md §3.2/§5의 "삭제는 건별 승인"을 테스트 자산·불필요 백업에 한해 대체).

## A. 상시 규칙 (Standing rules)

| # | 날짜 | 요청 (요지) | 업계 용어 — 뜻 | 상태 | 적용 위치 |
|---|---|---|---|---|---|
| R01 | 10/26 | 한국어 해요체로 짧게 답하고, 중간중간 보고(진행률 %, 추정 ETA) | Status reporting / progress % — 진행 상황 보고와 완료율 | standing | AGENTS.md §4 |
| R02 | 10/28 | 내 요청을 업계 용어로 바꿔서 일하고, 바꿀 때마다 용어를 짧게 설명 | Requirements translation + glossary — 요구사항을 표준 용어로 옮기고 뜻 풀이 | standing | 이 대장의 "업계 용어 — 뜻" 열 |
| R03 | 10/27 | 내 자산은 절대 지우지 말고, 덮어쓰기 전에 `.bakN`으로 백업(이미 있는 `.bakN`은 덮어쓰지 말고 다음 번호) | Non-destructive workflow / versioned backup — 원본 보존, 번호 붙은 백업 | standing | AGENTS.md §3.2; UE는 `_bakN` |
| R04 | 10/29 | 테스트 자산·더는 필요 없는 백업은 배치 끝에 한 번에 정리 삭제해도 됨 | Batch cleanup pass — 작업 묶음 끝 일괄 정리 | standing (첫 실행 done: 10/29 04:15–04:35, ≈159 GB 정리) | `/workspace/mtl_fix/overnight_decisions.md` "Item 4"; PC `Scripts\mtl_fix\ue_tests_cleanup.py` |
| R05 | 10/26 | `/Game/SM2_Work/Original/*`, `/Game/Work/*`는 건드리지 말고, `Lightning_01`은 절대 저장하지 않기 | Protected / read-only paths — 보호 경로(읽기 전용) | standing | AGENTS.md §3.3 |
| R06 | 10/26 | 작업은 PC `Desktop\SM2_Work`, UE `/Game/SM2_Work` 안에서만 | Sandboxed workspace — 작업 공간 격리 | standing | AGENTS.md §3.3, WORKFLOW.md |
| R07 | 10/27–28 | 논스톱으로 일하고, 먼저 실행한 뒤 결정사항은 나중에 한 번에 보고, 보류 질문은 맨 끝에 | Autonomous batch execution + decision log — 자율 일괄 실행과 결정 기록 | standing | `/workspace/mtl_fix/overnight_decisions.md` 방식 |
| R08 | 10/27 | 에디터 재시작은 묻지 말고 해도 됨(저장 먼저) | Pre-authorized restart — 사전 승인된 재시작 | standing | AGENTS.md §5 |
| R09 | 10/28 | UE가 죽으면 크래시 리포터 닫고, 리포트 읽고, `-AutoDeclinePackageRecovery`로 보이게 다시 띄우고 이어서 작업 | Crash recovery automation — 장애 자동 복구 | standing | `/workspace/animthumb/REPORT.md` "Final restart" (`btl_restart.ps1`) |
| R10 | 10/28 | 내가(에이전트가) 시작한 PIE/SIE는 항상 직접 종료 | Session hygiene (PIE/SIE teardown) — 플레이 세션 뒷정리 | standing | overnight_decisions.md "UE learnings" |
| R11 | 10/27 | 수정은 캐릭터별 땜질이 아니라 일반 규칙으로(브라우저만으로 export해도 똑같이 되게) | Generic / data-driven rules — 개별 예외 대신 공통 규칙 | standing | 물리: `Physics/PhysicsBake.cs`, SM2PhysicsSetup 플러그인 |
| R12 | 10/27 | 포맷 결론은 exe/셰이더/캡처 근거가 있어야 하고, 없으면 HEURISTIC으로 표시 | Evidence-based RE + provenance labels (PROVEN/INFERRED/HEURISTIC) — 근거 등급 표기 | standing | AGENTS.md §3.1, STATUS.md Heuristics |
| R13 | 10/28 | 인게임 캡처를 나한테 부탁하지 말고, 셰이더 번들·exe 정적 분석을 우선 | Static analysis first — 실행 없이 바이너리/셰이더 분석 | standing | mtl1-48 (30563e4) 정적 비교, SHADERS.md |
| R14 | 10/28 | 셰이더/머티리얼 공식은 캡처 비교 튜닝보다 셰이더 바이트코드 역분석으로 | Shader disassembly — 셰이더 바이트코드 역어셈블 | standing | shaders-1 / mtl1-38 (a7c22eb), `docs/handoff/SHADERS.md` |
| R15 | 10/27 | RE는 REA(github.com/morluto/rea)가 1순위, Ghidra headless는 대안, 설정은 핸드오프에 기록 | Toolchain policy: REA primary — 리버스 엔지니어링 도구 우선순위 | standing (설정 done: ddc9118, 28489a0) | WORKFLOW.md "REA (morluto/rea)", `tools/re/rea_daemon.py`, `tools/re/rea_call.py` |
| R16 | 10/29 | REA 전체 분석 결과를 재사용 가능한 "Symbol DB"로 (구 RE_MAP) | Symbol database — 함수·주소·문자열 색인 DB | standing (done: 463f383 → ebbe309 rename → 28489a0, 8bdd80a 재생성) | `docs/SYMBOL_DB/`, `tools/symbol_db/build_symbol_db.py`, `rea_string_xrefs.py` |
| R17 | 10/29 | 게임 데이터 파싱(예: 파워피스트 손 숨김 규칙)은 묻지 말고 알아서 | Autonomous data mining — 게임 데이터 해석 위임 | standing (예시 done: fcc16bf) | `Scripting/Sslb.cs`, `tools/re/sslb.py` |
| R18 | 10/27 | HANDOFF.md를 매 단계 갱신 | Living documentation — 상시 갱신 문서 | standing | 이 파일, STATUS.md |
| R19 | 10/27 | Niagara 변환은 맨 마지막, 그것도 물어본 뒤에만 | Deferred scope — 범위 보류 | standing (로드맵에서 제외: 7e561f4) | AGENTS.md §3.6, ROADMAP "Possible future" |
| R20 | 10/27 | Halo는 무시, SM2만 | Scope control — 범위 한정 | standing | AGENTS.md §1 (HaloCEA 테스트 실패 무시) |
| R21 | 10/27 | PC가 오프라인이면 주기적으로 재시도 | Retry with backoff — 주기적 재시도 | standing | — |
| R22 | 10/28 | 1시간마다 `sm2-20` 보고 루틴, 내용이 같으면 조용히(20분 → 1시간으로 변경) | Scheduled status digest + dedup — 정기 보고, 중복 억제 | superseded 10/30 → 20분마다, "자러간다" 하면 1시간 (RULES_CORE) | 봇 루틴 `sm2-20` (WORKFLOW.md §9, 10/29 1시간으로 정정) |
| R23 | 10/28 | push는 PC의 `push.ps1`로 | Release pipeline (patch → `git am -3` → ff-only push) — 배포 스크립트 | standing | PC `Desktop\SM2_Work\Scripts\mtl_fix\push.ps1` (`0*.patch` in `%USERPROFILE%`) |
| R24 | 10/27 | 신뢰할 수 없는 실행파일은 실행 금지 | Supply-chain safety — 출처 불명 바이너리 차단 | standing | — |
| R25 | 10/27 | 요청 없으면 Discord에서 아무것도 하지 않기 | No unsolicited external comms — 요청 없는 외부 발신 금지 | standing | — |
| R26 | 10/27 | 게임 중엔 포커스 뺏지 말고 hidden + Idle 우선순위면 무거운 작업도 OK, "게임끝"이면 PC 자유 | Background priority / non-intrusive ops — 백그라운드 저우선 실행 | hidden/no-focus standing; Idle part superseded 10/29 → 항상 High, 임의로 Idle 금지 (RULES_CORE) | AGENTS.md §3.4, WORKFLOW.md |
| R27 | 10/28 | 플러그인 옵션은 콘솔 명령 말고 전부 설정 UI에서, 즉시 적용·저장 | Settings UI (no console vars) — 설정 화면 기반 옵션 | standing | BongThumbLive 설정 페이지 |
| R28 | 10/26 | "실행해줘" = PC에서 Forge Lens 실행 | Command alias — 명령 단축어 | standing | `C:\Users\Revaptor_FX\SM2Extractor\ForgeLens.exe` |
| R29 | 10/27 | UE 목표는 Sequencer 시네마틱 렌더 → 천은 Chaos Cloth, 본 파츠는 본 물리 선호(실시간 성능보다 품질) | Offline cinematic quality — 오프라인 렌더 품질 우선 | standing | mtl1-10 Chaos Cloth 옵션 (9e99c31) |
| R30 | 10/28 | SM2 안티치트는 밴 위험 없음(멀티만 꺼짐) → 인게임 캡처/모드 작업 자체는 허용 (단, R13이 우선) | Risk acceptance (EAC) — 위험 수용 | standing | HANDOFF vfx3r/vfx3s |
| R31 | 10/29 | 내 요청은 인수인계 문서에 꼼꼼히 기록(업계 용어·뜻·상태 포함), 계속 최신으로 | Requirements traceability ledger — 요구사항 추적 대장 | standing (done: 이 섹션, `docs: user request ledger`) | 이 섹션 |
| R32 | 10/29 | HEURISTIC은 되도록 쓰지 말고, 모든 규칙은 먼저 게임 데이터/exe 근거로 정해요. 근거 찾기가 실패했을 때만 마지막 수단으로 HEURISTIC을 쓰고, 그때는 분명하게 표시해요 | Evidence-first rule derivation — 근거 우선 규칙 도출(데이터·exe·캡처 → 마지막에만 추정) | standing (fix54부터) | `docs/handoff/WORKFLOW.md` §0, 이 대장 "REA 일괄 처리 목록" |
| R33 | 10/29 | "봉썸라이브는 계속 작업할거고 sm2는 다른 그록봇 인계해서 작업할거야 그 봇이 마치 너가 그대로옮겨간것처럼 인수인계 파일 써넣어주고 프롬프트까지 줘" | Ownership transfer / bot handoff — SM2는 새 그록봇이 그대로 이어받고, BongThumbLive는 기존 봇이 계속 | standing (done: `docs/BOT_TRANSFER.md`, box `/workspace/handoff/`) | `docs/BOT_TRANSFER.md` |
| R34 | 10/29 | "새 봇이 다른 계정에 있어서 아예 컴퓨터환경도 다시 꾸려야할거야 이점도 다써놔" | Environment bootstrap / fresh-account provisioning — 새 봇은 다른 계정이라 박스·도구·로그인·메모리·루틴을 처음부터 다시 구성해야 하니 그 방법을 문서화 | done: `docs/BOT_TRANSFER.md` §0 step 0 (PC 연결, GitHub, 정확한 도구 버전 설치, exe/Ghidra/REA 재생성, box-only 스크립트·노트 커밋, 루틴, 메모리); 문서의 오너 이름 = Baegovda |
| R35 | 10/29 | "hologram_planet 애니가 localbake/hologram_planet_Skeleton을 쓰고 옆 메시는 같은 이름의 다른 스켈레톤을 써서 재생 불가 — 일회성 수정 말고 익스포트/임포트 규칙으로 고치고 다른 에셋도 확인" (BongThumbLive 봇 보고) | Split skeleton — 애니메이션과 메시가 서로 다른 스켈레톤을 쓰는 문제 (anim과 옆 메시는 항상 한 스켈레톤 공유) | done 10/29 ~23:00 KST: 규칙 = 애니 임포트는 옆 메시의 스켈레톤으로만(`sm2_anim_import`가 어긋나면 `SPLITSKEL` 경고), `ue_sani_fix.py` 메시 재임포트는 폴더의 기존 `{mesh}_Skeleton` 재사용; 복구 `tools/ue/ue_fix_split_skeletons.py scan|fix`; 전체 스캔 1건(hologram localbake) → 수리 후 0건 | `tools/ue/ue_fix_split_skeletons.py`, `test/python/test_split_skeleton.py`, 아래 "Split skeleton (R35)" |
| R36 | 10/29 | 레아 일괄 작업 1번: 리본 트레일 색이 어디서 정해지는지 찾아서 HEURISTIC 대신 게임과 똑같이 | Ribbon trail colour bake — 트레일 점 색 계산 재현 (페이드·나이 기반 그라디언트) | done 10/30 KST: FUN_141ab9b60 등 PROVEN, `SM2_VFX_TRAIL_FADE` 기본 ON, 색 = 점 나이 기준 | `EffectTrails.cs`, RE_NOTES "rea-batch 7" |
| R37 | 10/30 | 레아 일괄 작업 2번: 적응형 조명 배율을 게임과 똑같이 (clamp(avgLum, 0.05, 50)) 벤치·UE 익스포트에 적용 | Adaptive light multiplier — 자동 노출 평균 휘도로 조명 세기 보정 | done 10/30 KST: FUN_141e90cb0/FUN_141ee9e80/FUN_14224ecf0 PROVEN, FUN_141de8140는 1.0; 남은 HEURISTIC: DefaultAdaptive=true, 벤치 노출 1/250 | `CaptureRules.cs` `CaptureLightUnits.AdaptiveFactor`, RE_NOTES "rea-batch 7 item 2" |
| R38 | 10/30 | 레아/데이터 3번: 플레이어 사격 애니 클립을 무기별로 게임과 똑같이 고르기 | Morpheme network switch decode — 애니 그래프의 스위치/식 노드 해독 | done 10/30 KST: primaris.nmb의 wpn_type 스위치·Expression 해독(INFERRED), 무기별 tacticus_shtN_shoot_aim/blind 표; HEURISTIC: 미할당 기본값, op 95/98/c4, 헤비 무기 조준 | `PlayerFireClips.cs`, `PropAnimStates` 3b, `tools/re/nmb_reader.py`, `docs/handoff/FIRE_CLIP.md` |
| R39 | 10/30 | "분석캐시 … 까먹게 되서 또 읽을거아냐 … 새 도구도 항상 숙지하는거지?" + 시스템 개선(속도·토큰·품질) | RE toolbox — 상시 Ghidra 서버, 요약 출력, 분석 캐시 색인, 도구 목록 | done 10/30 KST: recall-before-decompile rule (BOT_TRANSFER §0), exe patch check, REA pinned 6.8.0, UNVERIFIED/VISUAL_CHECKS/BACKUPS | `tools/re/README.md` "RE toolbox", `tools/re/ghidra_serve/`, `docs/handoff/RE_CACHE/` |
| R40 | 10/30 | "시작하고" (질문 묶음 보내기 + .nmb 검증 자동화) | Query batching + Morpheme verification | done 10/30 KST: `gs.py batch` short-form queries, 20 queries ~0.2 s; `nmb_verify.py` 124 rows 0 mismatch, 1.4/1.51 relabelled HEURISTIC | `tools/re/README.md`, `docs/handoff/FIRE_CLIP_VERIFY.tsv` |
| R41 | 10/30 | "이것도 다 방지책 적용해둬" + 자동 시작은 하지 말고 수동 시작 | Prevention measures C1–C7 | done 10/30 KST: watchers (mem/disk/patch → alerts.txt, silent), REA version guard, push lock, K: backup + verify, CONCERNS.md; REA/GhidraServe manual start only (no logon task) | `docs/handoff/CONCERNS.md`, `tools/re/pc/` |
| R42 | 10/30 | "컴퓨터 로컬로 못했던것들 다하고, 푸시허가+구버전 ... 그냥 좀 지워줘 용량좀 줄이고싶다" | SM2_Work old-version cleanup + PC-local leftovers | done 10/30 KST: cleanup-plan pushed (2444424); ghidra_proj3.tar -> K:\SM2_Backup (880,998,400 B, sha256 599f7ff8… match); cat (a) 3,038 → 2,854 packages deleted (13,729.8 MB files; K: free +13,735.6 MB), 182 kept because non-deleted packages depend on them (titus_assault/hormagaunt mats+textures), 4 (d) + bak3 sm2d_white/_lin + Original/Work/PhysTestMap untouched; editor was closed; verify (headless commandlet, AssetRegistry hard+soft): 0 refs into deleted SM2_Work packages, 6 VfxUe materials load. Deleted directly after dependency-closure check (no _Archive move step). proj_sm2 not copied (GhidraServe kept running) | `docs/handoff/CLEANUP_DELETED.tsv`, `CLEANUP_PLAN.md` |
| R43 | 10/30 | BongThumbLive 봇 보고 재확인: "hologram_planet 애님과 메시가 이름 같은 다른 스켈레톤 — 일회성 말고 익스포터/임포트 규칙으로, SK_Sci_Fi_Cyborg_03_Skeleton도 확인" | Split skeleton follow-up — 같은 이름의 스켈레톤 충돌 방지 (skeleton identity rule) | done 10/30 KST: editor AssetRegistry audit (headless, editor closed) 39,224 anims / 2,473 meshes / 921 skeletons → 0 splits; localbake anim gone (R42), localbake mesh on Props skeleton. Rule: mesh imports reuse the one live `{mesh}_Skeleton` (same folder first, else the only one elsewhere), ≥2 same-named elsewhere → `SKELCOLLISION` error; anim import raises `SPLITSKEL` instead of printing. Cyborg_03 = third-party pack, skeleton asset missing, not SM2 data — left alone | `tools/ue/sm2_skeleton_rule.py`, `tools/ue/ue_skel_audit.py`, `test/python/test_skeleton_rule.py`, below "Split skeleton follow-up (R43)" |
| R44 | 10/30 | "이거 진짜 안까먹는 방법없어? ... 내 로컬에 저장해도상관없으니까 ... 방지책을정말확실하게" | Anti-forgetting: single rules file + session-start check | done 10/30 KST: `docs/RULES_CORE.md` (read once per session/after summary), `tools/re/pc/rules_check.ps1` (≤10 lines, UE route), `docs/SUBAGENT_PREAMBLE.md`, docs_check guard, PC copies in `Desktop\SM2_Work` | `docs/RULES_CORE.md` |
| R45 | 10/30 | "확인해야할것들 다 해봐" (t100u) | Verification sweep — 남은 확인사항 점검 | done 10/30 KST: bridge works only in a GUI `UE_VFX_57` editor (log: Native MCP on 127.0.0.1:3000/mcp, WS 8090/8091); commandlets skip it; the 'editor running' pids were another project (BLTest); rules_check now tells headless / EngineWire / bridge DOWN and ignores other projects; REA daemon = python 41888, 61672 = REA's Ghidra bridge child (java); R22/R26 marked superseded; disk-alert rule written | `tools/re/pc/rules_check.ps1`, `docs/RULES_CORE.md` |
| R46 | 10/30 | "작업 마저 해줘" (t102u) | UNVERIFIED U1-U11 real-test pass | done 10/30 KST: U10 PROVEN via EngineWire audit (0 splits); U1/U2 → INFERRED with new static evidence; U3/U4/U5/U7 narrowed; U6/U7 binding, U8/U9 V3 = game/user job | `docs/handoff/UNVERIFIED.md` |
| R47 | 10/30 | "언리얼 에디터 켜는거랑 mcp테스트하는건 너가 해." (t103u) | Live MCP test — 에디터 실행 + EngineWire/UEMCP 실호출 | done 10/30 05:00–05:07 KST: launched GUI `UE_VFX_57` (minimized, High, -AutoDeclinePackageRecovery, startup map OpenWorld template) pid 33536, left running; bridge log "Native MCP server started on 127.0.0.1:3000/mcp" 14 s after launch; read-only smoke (UE 5.7.4, /Game/SM2_Work 982 assets) via `uerun.ps1`→EngineWire 1.76 s (first call 11.0 s, also returned stale SKELAUDIT print lines), UEMCP 3.81 s after fix. Fixes: `uemcp_call.py` read args with utf-8 → BOM JSONDecodeError, now utf-8-sig (PC copy patched, `.bak1`); `rules_check.ps1` used Get-NetTCPConnection which returns nothing on this PC for a live listener → false "bridge DOWN", now netstat. U12 closed | `tools/ue/uemcp_call.py`, `tools/re/pc/rules_check.ps1`, UNVERIFIED U12 |
| R48 | 10/30 | 폴리싱 2) 어깨 장갑 좌우 색 덮어쓰기 (t108u 자율 진행) | Per-section colour override — 부위별 색 덮어쓰기 | done 10/30 KST: `ArmorColourSchemes.SectionOf` maps `*_shoulder_L/R`→LEFT/RIGHT_PAULDRON, hand→GAUNTLET, knee/shin→GREAVE, forearm→BRACER, jumppack/manipulator/lens. Affixes PROVEN (`character_body_section_library.sso`), name→side INFERRED (cls17 part names). Tests 16/16. Undo: revert commit |
| R49 | 10/30 | 폴리싱 3) 색 구성을 UE 마스터 머티리얼에 | Colour scheme in master material — 마스터 머티리얼 색 구성 | done 10/30 KST: `M_SM2` static switch `UseColourScheme` (default off) + importer reads the single `*.colour_scheme.*.json`. EngineWire test: M_SM2 compiled in `/Game/SM2_Work/Tests/r49_scheme`. Not yet run on a real character export. Undo: revert commit |
| R50 | 10/30 | 폴리싱 4) F43 .td 수치 파라미터 | Material scalar import — 재질 수치 반영 | done (data only) 10/30 KST: see `docs/handoff/POLISH_R48.md` |
| R51 | 10/30 | 폴리싱 5) 총구 화염 위치·조준 자세 | Muzzle offset / aim pose | done (switch, default off) 10/30 KST: `MUZZLE_AIM.tsv` + viewer `SM2_MUZZLE_SHOOTTIP=1` (+`SM2_MUZZLE_LOCATOR`) + `ue_r51_muzzle.py`; visual check pending owner (POLISH_R48) |
| R52 | 10/30 | 폴리싱 6) 다른 캐릭터 물리 | phys52 on other characters | done 10/30 KST via EngineWire, `_bak1` backups (POLISH_R48) |
| R53 | 10/30 | 폴리싱 7) 천 build28 | Cloth plugin verify | done 10/30 KST: 4/4 cloths converted on cc_sorcerer_exalted (EngineWire) |
| R54 | 10/30 | 폴리싱 8) 레벨·소품 배치 추출 | Level placement extraction | done (data) 10/30 KST: `tools/level/placement.py`, 5,898 objects; 10/30 11:40 KST follow-up (t110u): `placement_view.py` HTML (5,898 markers) + `ForgeLens --levelbench` real meshes (2,483 geom rows → 2,850 parts, 392 templates, 0 missing, 6 sky/backdrop >1000 m skipped, 14.6M tris, ~61 s on PC) + `ue_r54_level.py` (UE scratch map; import blocked: editor in PIE from another job); 10/30 13:10 KST (t114u/t115u): generic viewer click on any `*.cd_list` → `Level/LevelScene.cs` builds the whole level at runtime from the user's game files (no bundled assets, cache only `%LOCALAPPDATA%\ForgeLens\levels`); PC verified hub_battle_barge 444 s / story_intro 260 s / pvp_city 325 s, 20M-tri budget, textures+lights on; gaps: slow (no mesh cache), black untextured spheres, pvp_city close cam black, most lwi over budget |
| R55 | 10/30 | 폴리싱 9) `_bakN` 점검(삭제 없음) | Backup inventory | done: `docs/handoff/BAKN_LIST.tsv` |
| R56 | 10/30 | 폴리싱 10) proj_sm2 백업 | Ghidra project backup | done: K:\SM2_Backup\proj_sm2, sha manifests equal |
| R57 | 10/30 | "쓸 데 없는 건 지워도돼 ... 지울 수 있는 거 다 지워" | delete rule replaced + cleanup | done: rule text in RULES_CORE/AGENTS/BOT_TRANSFER/START_HERE; deleted 198 Tests/chars_cloth `.uasset.bak1` file copies (4,123,868,528 B) + r49_scheme M_SM2 (0 refs, fixpoint); K: free +4,124,577,792 B; kept Original/Work/Lightning_01, shared pre_step3/chars_phys skeletons, VfxUe_bak3 (referenced), R52 `_bak1` (sole backups). Undo: re-run extractor/ue_r53_cloth (files were unloadable copies) | `docs/handoff/CLEANUP_DELETED.tsv`, `tools/ue/ue_cleanup_refs.py` |
| R58 | 10/30 | "왜 파이썬이 소켓 생성을 막는 거야?" | SM2_shootTip/tip/tip_gl sockets on wpn_bolt_rifle_01 | done PROVEN: UE5.7 Python SkeletalMeshSocket SocketName/BoneName read-only; EngineWire animation_physics create_socket works (skeleton sockets, saved 11:17 KST, list_sockets shows 3). Undo: delete sockets on the skeleton | `tools/ue/ew_socket.py`, `tools/ue/ew_schema.py` |
| R59 | 10/30 | "예전 시퀀스 도구가 왜 없어져 ... 너가 만들어서 연결하면 되잖아" | F46-style fire sequence for bolt rifle | done PROVEN (EngineWire, editor): `/Game/SM2_Work/Sequences/BoltRifleFire/LS_arifle_bolt_rifle` from LS_TitusWalkFire (template untouched); aim pose = tacticus_wpn10_aim_hold (MUZZLE_AIM.tsv) swapped=1; 8 MuzzleFlash attached to SM2_shootTip (Niagara world pos == socket pos 903.29,30.64,198.89); `tip` flag verified (SM2_tip, 41.6 cm further). Sockets also added to the F46 rifle skeleton (additive). Screenshots `Desktop\SM2_Work\Shots\r59\`. Undo: delete BoltRifleFire folder / the 3 SM2_ sockets | `tools/ue/ue_seq_fire.py`, `test/python/test_seq_fire.py` |
| R60 | 10/30 | "P I E로 물리 테스트 ... 직접 보고 비교해서 판단해서 수정까지" | Simulate physics check R52 chars + R53 cloth | done (EngineWire, editor): scratch `/Game/SM2_Work/Tests/r60_pie/Map_PhysCheck2`, 4 actors (calgar no clip, sorcerer grab_loop, vortex attack_melee, r53 cloth sorcerer), 8 s oscillation + 3 s hold, physics vs post-process-off baseline, SceneCapture screenshots. Simulated bodies (describe_body): calgar 3, sorcerer 2, vortex 0, cloth 0 bones (cloth = verts). Result: no NaN, no explosion/flying parts in any capture (PROVEN for the captured frames); sorcerer scroll_jiggle added stretch 0.01 cm (constraint holds), swing up to 8.8 cm/frame at ~8 fps sampling = normal swing; calgar no motion because WorldAlpha 0 by design (mtl1-9) and no clip on its skeleton (calgar clip import failed: skeleton root `cc_calgar_Skeleton` vs clip ROOT); vortex has no simulated bodies -> nothing to break; cloth judged visually only (intact, no explosion). No parameter change needed. `vortex/cloth/calgar` numeric HEURISTIC. Shots PC `Desktop\SM2_Work\Shots\r60\` | `tools/ue/ue_pie_phys.py` |
| R61 | 10/30 | "SM2_Work 안은 전부 추출 작업 산물 — 마음대로 지우고/옮기고/덮어써도 됨, 밖은 절대 건드리지 말 것" (t112u) | asset rule rewrite | done: RULES_CORE/SUBAGENT_PREAMBLE/AGENTS/BOT_TRANSFER/START_HERE/HANDOFF; supersedes R03/R05/R57 for UE content (R03 `.bakN` stays for source/PC files) | this row |
| R62 | 10/30 | t112u: SM2_Work cleanup of _bakN/Tests leftovers | cleanup | done: 14 packages (Tests/r51, Tests/r53_cloth, Tests/r60_pie, 3 Characters `_bak1`; fixpoint ref check: 0 live referencers) + 17 `.uasset/.umap.bakN` file copies (PhysTestMap.umap.bak1-11, chars_phys/physics mesh copies, hologram_planet skeleton copies); K: free +485,355,520 B; BAKN_LIST now empty. `ue_cleanup_refs.py`: `bak` token, only `/Game/SM2_Work/` accepted (R61). Undo: re-run ue_phys52_all / ue_r53_cloth / ue_r51_muzzle | `docs/handoff/CLEANUP_DELETED.tsv` |
| R63 | 10/30 | "이거 사진들 이상해 뭘 말하고싶은건지" (t122u, R60 shots unreadable) + "천 물리" before/after | readable cloth check of the R53 cloth sorcerer | done (EngineWire, editor; Simulate stopped, r54_level reopened): `/Game/SM2_Work/Tests/r63_cloth/` = Characters/cc_sorcerer_exalted duplicate + create_chaos_cloth (4/4: cloak, skirt(+adds), tabard, beads; 6 clothing assets) + SM2 materials (30/30 slots), Map_Cloth (grey 0.18 floor, dir 1.5 + sky 1 + 2 point + 1 spot, manual exposure), A/B: same mesh cloth ON vs `disable_cloth_simulation`. Shake 6 s 90 cm 0.7 Hz + grab_loop clip. Plugin metrics (11,041 free particles): NaN 0 all phases; free particles >1 cm inside collision 206-370 (worst 6.2-14.0 cm); mean move from skinned pose rest 8.1 / motion 51.6-54.4 / settled 8.9 cm, worst 102.7 cm (motion). Shared config tune (cloak Subdivision 2->4, Iteration 1->2, Thickness 1->2) gave no clear gain (base pen 276-352). Visual: cloth swings and returns, no explosion/tearing; ON surface more wrinkled/darker than skinned; 2-3% particles clip (HEURISTIC judgement). Accidental `/Game/SM2` (95 files created by a mis-quoted import at 13:36, 0 external refs) deleted. Shots PC `Desktop\SM2_Work\Shots\r63\` (r63_cloth_sheet.png) | `tools/ue/ue_r63_cloth.py`, `tools/ue/r63_pass.ps1` |
| R64 | 10/30 | t123u: ForgeLens 뷰어에 언리얼식 플라이 카메라 (RMB look, WASD, Q/E, Shift, wheel speed, F) | Fly camera in viewer | done 14:20 KST: `MainWindow.FlyCam.cs`, `Fly` toggle (default ON for levels, OFF for meshes; off = old orbit). RMB look keeps the eye fixed, WASD/QE view-relative, Shift x4, wheel+RMB speed x1.25/notch, wheel alone dollies, F = level default persp / mesh fit; GPU path now honours the pivot. Verified PC real window via `SM2_FLYTEST=1` hook (same FlyMove/FlyLook code, no synthetic OS input): 3 levels, W 2 s moved exactly 2xspeed (hub 98.1 m at 49.0 m/s), look kept eye unchanged, Shift+D 1 s + E 0.5 s = 219 m, F restored start eye. Not tested: real mouse/keyboard input, single-mesh fly. Shots PC `Desktop\SM2_Work\Shots\r64\`. R54b viewer click-to-open re-measured same session (cold cache, real window): first 87.2-97.8 s, second (disk cache) 2.5-3.0 s; placed instances hub 106,160/112,256 (94.6%), story_intro 38,818/159,782 (24.3%), pvp_city 70,062/188,913 (37.1%) — the rest is the 20 M tri budget (shots `Shots\r54c\`) | `tools/re/pc/r54_viewer_test.ps1` (+SM2_FLYTEST) |
| R65 | 10/30 | t119u "언리얼에서 직접 확인, 어두우면 조명 설치" | hub_battle_barge level in UE, readable | done (EngineWire, editor, no PIE): `/Game/SM2_Work/Tests/r54_level/L_hub_battle_barge` had 2850 R54 actors / 386 meshes, but 1751 actors (205 meshes) imported with zero bounds (empty); `fixzero` re-import fixed 205/205 (still_zero 0). Lights: 2 directional (10 + 4, no shadows), sky light (cubemap, 4), 25 fill point, 1 spot, unbound PPV manual exposure; captures EV +1.5. Mean luma overview 10.5 / close1 24.5 / close2 28.7 (/255). Gaps: meshes untextured grey (OBJ has no materials); only 2850 of 7893 viewer parts exported by R54 levelbench. Shots PC `Desktop\SM2_Work\Shots\r64\level\` | `tools/ue/ue_r64_level.py`, `tools/ue/r64_level.ps1` |
| R66L | 10/30 | parent task (t125u-t127u): UE level must look like the viewer (textures, full parts, no void) | hub_battle_barge in UE, textured + full coverage | done 10/30 15:00 KST (EngineWire, editor, no PIE): `--levelbench ... 1` now writes per-part `usemtl` slots, `textures/*.png` (viewer-decoded albedo + normal, RGBA, <=512) and `materials_ue.tsv` (local `Exports\level\ue_r66_hub`, never committed). UE `/Game/SM2_Work/Tests/r54_level`: 702 textures, 2 masters `M_R66_Opaque/Masked` + 360 MIs (360/360 with albedo; tint off like the viewer), 1389 meshes (`Meshes66`, 0 zero-bound, 1535/1535 slots assigned), 1389 ISM actors = 106,160 instances (viewer 106,160/112,256; old R54 2850 actors + `Meshes` deleted). Sky: SkyAtmosphere + fog under the level; R64 skylight switched to DaylightAmbientCubemap + neutral light colour (sunset cubemap gave a rust cast). Captures EV -0.3 mean luma overview 73.1 / close 62-67 / viewer-cam persp 62.4, vclose 34.8 (viewer persp 71.9). Gaps: viewer-camera persp framing differs from the viewer shot (INFERRED backface/near-plane); no spec/metal maps; sky not visible (interior level); editor working set 23.7 GB after placement (20.7 GB before). Shots PC `Desktop\SM2_Work\Shots\r66_level\h\` | `tools/ue/ue_r66_level.py`, `tools/ue/r66_level.ps1`, `Bench/LevelBench.cs` |
| R66V | 10/30 | t129u 설명 간단·세련되게; t130u 메시에서도 언리얼식 이동 + 레벨 뷰포트처럼 카메라 속도 등 정보 표시 (ref t131u UE screenshot) | Viewer: fly on meshes, UE-style HUD, compact mesh note | done 15:04 KST: Fly now default ON for meshes too (LMB still orbits, toggle = orbit). HUD overlay (`MainWindow.Hud.cs`, `HUD` toggle / H): mode, eye Pos, Yaw/Pitch, FOV 45 (= renderer PI/4), key hints; top-right CAMERA SPEED m/s + log bar (0.05-2000), live on RMB+wheel, x4 shown with Shift. Mesh note = `ExportedMesh.Summary` (`TemplateMeshReader.BuildSummary`, same numbers), full `Message` behind `Details` + tooltip (CPU renderer no longer overwrites it). Verified PC real window (`SM2_FLYTEST=1 SM2_WINSHOT_CPU=1`, hook calls, no OS input) on cc_calgar: W 2 s moved 3.03 m at 1.51 m/s, look kept eye, RMB+wheel +3 → 2.95 m/s and HUD 3.0 m/s, F re-framed; Details + HUD-off shots; hub level HUD 49→96 m/s. Not tested: real mouse/keyboard, `dotnet test` (box restore hangs). Shots PC `Desktop\SM2_Work\Shots\r66\` | `r54_viewer_test.ps1 -Select/-Exe`, env `SM2_WINSHOT_CPU` |
| R67 | 10/30 | parent task: Calgar physics check (clip root-bone mismatch) | calgar clip + jiggle physics live | done 15:05 KST (EngineWire, editor, Simulate): cause PROVEN: `cc_calgar_Skeleton` bone order is `cc_calgar_Skeleton > cc_calgar > Object008B > ROOT > CENTRE`, so a standalone clip whose root is ROOT does not match; importing the clip from the same-hierarchy FBX `Exports\chars_cloth\cc_calgar\...\cc_calgar__calgar_run_fwd_calgar.fbx` onto the existing skeleton works: `/Game/SM2_Work/Tests/r67_phys/anims/cc_calgar__calgar_run_fwd_calgar` (32 frames, 1.067 s, 302 bone tracks / 304 bones). ROOT carried 227.8 cm run translation at 0.5 s and `force_root_lock` cannot lock it (bone 0 is not ROOT) -> ROOT translation keys zeroed in that test copy (`rootzero`). PA: 4 bodies, 3 simulated (order_jinggle_01, tassel_01_jiggle_01, tassel_02_jiggle_02), BACKup kinematic. Map_calgar ON (post-process ABP) vs OFF: jiggle orientation ON-vs-OFF 9.5-77.0 deg across rest/motion/settled, BACKup 0.00 deg, NaN 0. Shots PC `Shots\r67\calgar\base`, box `/workspace/shots/r67/calgar_sheet.png` | `tools/ue/ue_r67_phys.py`, `tools/ue/r67_pass.ps1` |
| R68 | 10/30 | parent task: Vortex 0 simulated bodies | vortex secondary physics | done 15:08 KST (EngineWire): cause PROVEN (data): `chars_phys52\cc_vortex_beast...physics.json` has jiggles 0, springDampers 0, cloths 8 (tenakle_L1-3/R1-3/M1-2_SIM, 32-48 verts) -> SetupFromJson makes 0 bodies by design; the tentacles are cloth. Fix: `/Game/SM2_Work/Tests/r67_phys/cc_vortex_beast_cloth` = duplicate + create_chaos_cloth: 8/8 converted, 8 clothing assets, 320 particles (274 free), NaN 0 in all phases, ON bounds X ext 689 -> 756-801 cm (tentacles move) vs OFF constant 689. Rigid bodies still 0 (correct for this data). Cloth collision only 3 remapped shapes (NECK/BACKup, HEURISTIC remap); collision >1 cm 0-60 free (worst 49.5 cm in motion), body-skin >1 cm 0-17 free (worst 13.9 cm). Characters/cc_vortex_beast itself unchanged. Shots box `/workspace/shots/r67/vortex_sheet.png` | same |
| R69 | 10/30 | parent task: cloth clipping 2-3% (R63) via larger capsules / backstop | sorcerer cloth variants | NOT fixed 15:25 KST (EngineWire): sidecar variants rebuilt with create_chaos_cloth into `/Game/SM2_Work/Tests/r67_cloth/` (ref, b0 = backstop 0, r13 = radius x1.3, r13b0, skin0) and swapped onto Map_Cloth R63_on/off; r63_pass twice each. Body-skin >1 cm (free of 11,041) rest/motion1/motion2/settled: ref 253/289-293/1518-1521/228-229, b0 244-245/297/1519-1520/244-251, r13b0 246-277/282-306/653-1529/173-252 = no consistent gain. Collision-inside rose with r13b0 (rest 448-542 vs 316). Plugin log PROVEN: rest-fit shrinks the enlarged capsules back (minRadiusScale 0.87-0.94 -> 0.65-0.72, same final radius), so radius scaling is a no-op for the binding shape. Config-only (bUseCCD off/on, CollisionThickness 3) also no clear gain. 99 PINNED particles are already >1 cm behind the body skin at rest -> part of the clipping is in the skinned cloth pose itself (INFERRED). skin0 (maxDistanceScaleCm 0) did not pin anything (key not used that way). Map_Cloth restored to the R63 mesh, CollisionThickness back to 2. Next: plugin-side fix (needs rebuild = editor restart) | `tools/ue/ue_r67_cloth.py` |
| R70 | 10/30 | t140u fly모드일 때 마우스 커서 안 보이게 | Viewer: UE-style hidden + clipped + re-centred cursor during RMB fly look | done 16:0x KST, build `Bin\ForgeLens_r70`: `MainWindow.FlyCam.cs` + `CursorLock` (Win32 ShowCursor/ClipCursor to the viewport/SetCursorPos warp to the press point, Avalonia Cursor None); look delta = physical px from anchor / RenderScaling (same feel as old DIP deltas on 125% and 100%; anchor stays in the viewport so no cross-monitor jump); restore cursor + position on RMB up, Window.Deactivated, LostFocus, PointerCaptureLost, Fly toggle off, FlyReset, Closing, ProcessExit (idempotent). FLYTEST cursor hook (PC real window, hook calls, cc_calgar, scaling 1.25): looking -> ourShowCount -1, clip = viewport [1525,274,2646,1789]; nudge -> delta (2,-20) phys (clip-limited), yaw/pitch changed by delta/1.25*0.005, re-read delta 0 (no drift); RMB-up/deactivate/toggle-off -> ShowCount 0, clip = full desktop, cursor back to (3823,1388). GetCursorInfo flags stayed 1 (window not foreground; no focus stealing) -> on-screen hiding NOT proven. First run the lock did not engage (cause not found; err logging added, second run OK). Not tested: real mouse, 100% monitor, the in-app shot showed another mesh loading. Shots PC `Shots\r70\` | env `SM2_FLYTEST` cursor lines |
| R71 | 10/30 | t142u Meshes에서 실제로 안 보이는 것(충돌 등)은 한 컬랩스로; t146u 하단 요약 항목별 색; t148u/t149u 창 제목 = "Forge Lens v<버전>" | Viewer: non-render part group, coloured summary, versioned title | done 10/30 ~16:10 KST: rule in ONE place `MeshToggle.IsNonRender` = reader HideReason `volume` (IndexMeshVisibility: `*cdt*`, `*_glr*`, `rb_*`, visibility_hidden/occluder/invisible-shadowcaster affixes) or `clothSim` (`*_SIM` shells) or `sfxSource`; `LOD`/`sfx`/`variant` stay on top (conservative). Group row `Collision / Helpers (N)` last, collapsed, checkbox shows/hides all, children selectable. cc_calgar PROVEN in the real window: top level 42 → 17 rows + group (25: 19 `*_cdt`, 6 `*_SIM`). Summary = coloured Runs (`MainWindow.SummaryColours.cs`, same text). Title from csproj `<Version>0.71.0</Version>` (bump = ledger id); git hash only in InformationalVersion/ProductVersion. Window title PROVEN `Forge Lens v0.71.0`. New `--meshparts <game> <regex> <out.tsv> [max]` inventory; `SM2_MESHGROUP_SHOT=1` hook (collapsed/expanded shots + child select); `tools/re/pc/r71_viewer_test.ps1`. Not tested: real mouse clicks, search filter of the tree (tree has no filter box), full-catalog inventory (first all-tpl run died without output). Shots PC `Desktop\SM2_Work\Shots\r71\`; build `Bin\ForgeLens_r71` | `MeshToggle.cs`, `MainWindow.SummaryColours.cs`, `AppVersion.cs`, `Bench/MeshPartsBench.cs` |
| R74 | 10/30 | t162u 포지렌즈 메모리 엄청 먹는데 원래 이래? (r70 PID 18520: WS 32 GB / private 114-127 GB) | Viewer memory: bounded level cache + GC trim + memory log | done 10/30 ~16:35 KST. Cause PROVEN (scripted soak, same 8 steps, real window, `SM2_MEMSOAK`): `LevelScene.Cache` (static) kept EVERY opened level forever (+~4.5-5 GB managed per level after full GC: 1.4 GB → 5.7 → 9.5 → 14.6 GB live for 3 levels), and the GC kept freed level/mesh arrays committed (LOH; 16 GB committed vs 6 GB live). Fix: level memory cache LRU default 0 (`SM2_LEVEL_MEM_CACHE`, 99 = old), re-open from `.flpack` disk cache; `MemoryLog.Trim` = one compacting full GC (LOH CompactOnce) when committed-live > 1 GB and > 50 % (30 s tick + 5 s after a level switch, `SM2_GC_TRIM=0` off; measured 534 ms pause); csproj Server GC off, Concurrent on, `System.GC.ConserveMemory=5`; `%LOCALAPPDATA%\ForgeLens\memory.log` line every 30 s (WS, private, managed, GC committed, levels cached). Soak before → after (peak private MB per step): calgar 4745→4376, sorcerer 5921→4456, vortex 5091→4259, hub 8175→7645, story_intro 12520→10553, pvp_city 18964→13082, calgar again 22723→11365 (end 22592→4453), hub again 22228→8584; whole run 22.7→12.7 GB peak. Open times same (models 3.7-8.8 s, levels 1.2-1.9 s from disk cache); re-opening a level is now 1.4 s (was 0.5 s memory hit). NOT reproduced: PID 18520's 114-127 GB with ~6 cores busy and private oscillating ±10 GB every 8 s (that is active churn, not only retention; process gone before a stack/gcdump could be taken); one level still needs 7.6-13 GB while shown (prepared copies + GL upload, renderer = other worker's files); thumbnail grid / Animate all / cloth not soaked; thumbnail time not measured (thumbnail code unchanged). Version not bumped (title owned by R71 worker) | `Level/LevelScene.cs`, `MemoryLog.cs`, `MainWindow.MemSoak.cs` (new), `App.axaml.cs`, csproj, `tools/re/pc/r74_memsoak.ps1`; PC `Shots\r74\soak_before.tsv`, `soak_after2.tsv` |
| R72 | 10/30 | t147u 볼텍스랑 소서러는 왜 텍스쳐가 없어 | UE: vortex/sorcerer untextured | done 16:45 KST (EngineWire, editor PID 79936 after the BongThumbLive session; viewer r71 checked first). Viewer: NOT a viewer bug - ForgeLens r71 shows both textured (`Textures 9/9` vortex, `17/17` sorcerer; shots PC `Shots\r72\viewer_*.png`). UE cause PROVEN: (1) `Characters/cc_vortex_beast` (24 slots) and `Characters/cc_sorcerer_exalted` (30 slots) had every slot = `WorldGridMaterial`, no MI/texture in the folder (uasset strings + `ue_r72_mats.py state`); the 10/26 layout verify listed MIs only for titus/hormagaunt/calgar/props, i.e. these two were imported mesh-only (`import_materials=False`) and the sidecar material step never ran for them (INFERRED history). (2) R67/R69 test duplicates (`Tests/r67_phys/cc_vortex_beast_cloth`, `Tests/r67_cloth/cc_sorcerer_exalted_{ref,b0,r13,r13b0,skin0}`) are copies of those meshes -> same WorldGrid (R63 copy only had MIs because R63 ran its own material import). (3) Running the sidecar import on the `chars_phys52` export still gives 0 textures: that export is DDS-only and UE 5.7 Interchange rejects it (`InterchangeDDSTranslator DDS DXGIFormat not supported : 98 : BC7_UNORM`). Sidecars themselves are complete (vortex 24 slots/86 maps, sorcerer 30/106, 0 missing). Fix: re-export `--tex-formats png` -> `Exports\r72\<n>` (34 / 64 png), `ue_r72_mats.py apply` -> Characters MIs assigned, Textures 33 / 62 assets; `copy` -> all 6 test meshes 24/24 or 30/30 slots. After: worldgrid 0 everywhere; MIs with a texture vortex 16/24 (other 8 = tentacle `_SIM` slots, INFERRED hidden cloth-sim master), sorcerer 30/30. Before/after: `Tests/r72_mats/Map_Mats` (grey 0.18 floor, dir 2 + sky (DaylightAmbient) 1 + point per actor, manual exposure EV 0, cloth off), shots PC `Shots\r72\ue\` sheet `r72_sheet.png`. Gaps: colour parity vs viewer/game not judged (sorcerer robe reads pink in UE vs blue-teal in the viewer shot - chameleon/colour-scheme path, unverified); floor overexposed at EV 0; texture count 33/34, 62/64 not explained; rule for future imports: use png exports for UE (chars_phys52 dds-only) | `tools/ue/ue_r72_mats.py` |
| R75 | 10/30 | t154u UE식 피킹(호버 외곽선, 클릭 선택, 트리 연동); t157u 오빗 회전 물리 + 관성; t159u 썸네일 텍스처, Kitbash/Meshes 한 패널 | Viewer picking, orbit physics + inertia, textured thumbnails, merged right panel | done 10/30 ~16:45 KST, build `Bin\ForgeLens_r75` (v0.75.0). **Picking** (`MainWindow.Pick.cs`, `MeshPick.cs` CPU ray vs the drawn `PreviewScene`, skinned batches posed per pick, unskinned 128-tri chunk boxes cached): hover = light-blue 2 px outline, LMB click (<5 px move) = orange 3 px outline + readout (name, tris, material, texture, node, pick ms), empty click deselects, Meshes tree expands/selects/scrolls; tree row → 3D outline. Outline = stencil ring in `GlPreview.DrawOutlines` (GPU only). PROVEN (hooks `SM2_PICKTEST`): cc_calgar ray pick median 2.0 ms (x20), click incl. tree sync 82-93 ms; hub_battle_barge (18.0M tris, CPU path) ray 2-3 ms, x20 incl. scene rebuild median 67 ms. Real-mouse hover/click/deselect also logged once (owner moving the mouse over a test window). **Orbit physics** (`MainWindow.OrbitPhysics.cs`, `PreviewSettings.PhysicsFrame/PhysicsClock`): sim world = inverse orbit yaw (rate-limit 8 rad/s), wall-clock stepping, 3 s settle; released drag spins with exp(-4 t) (`SM2_ORBIT_DAMPING`), any press stops; Orbit mode only; toggle `Orbit physics` default on. PROVEN `SM2_ORBITTEST` cc_calgar (8,409 cloth verts): fling 6 rad/s → mean cloth deviation 17.6 cm peak at 0.30 s (max 85.6 cm), 1.3 cm after 3.5 s; OFF same yaw path 0.00 cm. **Thumbnails**: cause PROVEN = ui-43 design `ThumbnailService.Model` rendered clay without maps (`ThumbSettings` had no Albedo); now `ResolvePreview` maps, cache key `r75-tex1` (old PNGs unused, not deleted). `--thumbbench`: clay 1.4-2.6 s vs textured 2.3-13.0 s per model (first time only, then disk cache). **Panel**: Meshes/Kitbash tabs → one panel, Kitbash collapsible on top (▸/▾ header, splitter), MESHES tree below. Nav helpers `nav_*`/`navmesh*` also go to the R71 group (HEURISTIC, name). Not tested: real-mouse orbit fling / fly+pick conflict by hand, outline on the CPU path (levels opened CPU on this PC → readout only), level parts in the Meshes tree (level tree has no part rows), jiggle-bone measurement (cloth only), constructor_tank thumbnail. Shots PC `Desktop\SM2_Work\Shots\{r72pick,r72orbit,r72thumb,r72kit}` | `MainWindow.Pick.cs`, `MeshPick.cs`, `GlPreview.cs`, `MainWindow.OrbitPhysics.cs`, `MeshPreviewScene.cs`, `ThumbnailService.cs`, `Bench/ThumbBench.cs`, `MainWindow.Kitbash.cs`/`.axaml` |
| R76 | 10/30 | t174u 모델 열기가 레벨보다 느린 이유 + 단축 | `--openbench` stage timing (PROVEN, 32 cores): calgar mesh read+decode 1.8-2.0 s, 49 textures 169 Mpix: archive read 0.8 s + pixel decode 3.5-3.9 s serial = 4.3-4.7 s → texture decode was the bulk; levels reuse the R66 disk cache so skip it. Fix: `ResolvePreview` decodes textures with Parallel.For (max cores/2, ≤8), same insert order; decoded bytes byte-identical (49/49, 43/43, 24/24). Resolve 4370→832 ms calgar, 2734→560 sorcerer, 2720→501 vortex. Real window (r74_memsoak, Shots\r77\A_soak_*.tsv): open 7.9→4.5 s calgar, 4.6→2.3 sorcerer, 4.4→1.6 vortex; peak private 5305→4800 / 5443→5360 / 5143→5138 MB (not worse). v0.76.0, Bin\ForgeLens_r76. Not done: mesh decode cache (~2 s calgar), lazy cloth setup not measured separately; no before/after screenshot (identity proven by byte compare instead) | done |
| R79 | 10/30 | t176u/t177u 언리얼식 UI(기즈모 제외) | Viewer UE-style viewport + outliner | done 10/30 ~17:40 KST, build `Bin\ForgeLens_r79` (v0.79.0; R77/R78 ids left to the R76 worker notes). New file `MainWindow.UeViewport.cs` (+ GlPreview `uView`/ExtraLines, GpuPreviewControl pass-through, 1-line hooks in ctor + LevelSelect). PROVEN in the real window (calgar, RTX 4090 via ANGLE D3D11, `SM2_UETEST`, shots `Desktop\SM2_Work\Shots\r79\win_calgar_ue_*`): toolbar View mode Lit/Unlit/Wireframe/Clay/Normals/UV checker (GPU only; CPU path = Wireframe only); Show menu Skeleton / Collision-Helpers group / Bounds (26 lines incl. grid) / Grid / Stats; stats overlay bottom right (tris, verts, frame ms, FPS or "on demand", cam speed); F frames the selected part (calgar_body: dist 4.69 -> 1.34), no selection = old FlyFrame; wheel zooms toward the cursor (Shift x4); Alt+RMB drag dolly; Ctrl+1..9 / 1..9 bookmarks per model; Meshes search box; right-click Hide / Show / Solo / Show all / Copy name / Copy asset path; Ctrl+Z/Y over visibility + selection (44->43 hide, solo 1, undo 43, undo 44, redo 43); F1 / ? help. Settings `%LOCALAPPDATA%\ForgeLens\viewport_r79.json`. Regression: SM2_PICKTEST + SM2_FLYTEST pass (pick 1.8 ms median, cursor lock released on rmb-up/deactivate/toggle). Not done: cloth-vertex Show, dockable panels + saved panel layout, separate Details panel (pick readout reused), eye glyph (checkbox kept). Already present before R79: tile size slider, type chips, favourites (ui-43), fly wheel speed HUD (R66). Untested: real mouse/keyboard input (methods driven by the hook), level CPU path. Stats tris (342,768) count visible parts across LOD0 + LOD levels + merged weapon, so it differs from the summary (171,201): not investigated. Also removed 3 stray merge-marker lines in tools/re/README.md (R75 rows kept). |
| R80 | 10/30 | t173u/t193u cloth clipping: fix the SHARED collision formula, verify 6-8 cloth characters with fast motions | viewer + UE cloth collision | partial 17:20 KST. Viewer (offline `--clothpen`, 6 models found of 10 asked: sorcerer_exalted, sorcerer, tech_priest, titus, calgar, chaplain x death/run/attack/idle, on-physics free particles inside the body mesh, mean): base 2.44 %, CCD+backstop 2.03 / 2.05 % (rerun), radius x0.75 4.66 %, x0.5 5.60 %, x1.15 1.96 %, x1.3 1.86 %; per model x1.15 helps sorcerer/tech_priest/chaplain but hurts titus 2.42->3.06 and calgar 1.47->1.80, so the shared default stays radius x1 + CCD + backstop (already default since R73, now pushed). Root cause PROVEN for UE: plugin rest-fit took the MIN scale over every free rest particle, so one deeply embedded particle shrank the whole capsule (R69 log 0.65-0.72) -> UE cloth collides against an undersized body = the x0.75 regime above. Fix (generic, all cloths): particles deeper than 0.5 r count as authored-inside and do not drive the fit, floor 0.9 (was 0.25), raw scale kept for remap rejection, log `deepIgnored`. Source updated in the installed plugin (`.bak24`) + repo; NOT BUILT: UBT/BuildPlugin fail with RulesError because `Engine/Plugins/Marketplace/BongLodge.bak_20261030_170508` duplicates the BongLodge module (other worker's backup), so no UE re-measure yet. Shots PC `Shots\r78\cloth\` (physbench off/base/ccdbs/x1.15 x sorcerer_exalted death, calgar run, titus run_melee, tech_priest death), box sheets `/workspace/r78/sheet_*.png`. Visual: sorcerer_exalted cloth still crumples in every variant (not solved by collision radius) | `ClothSolver.cs`, `SM2PhysicsSetupLibrary.cpp`, `tools/level/sky_env.py`, `tools/level/bc6hdr/` |
| R81 | 10/30 | t178u/t179u 포지렌즈 경량화 + 최적화 | Viewer idle CPU fix + lightweight audit | done (part) 10/30 ~18:05 KST, build `Bin\ForgeLens_r81` (v0.81.0). **Fixed (PROVEN A/B, minimized window, 40 s warm + 30 s sample, `tools/re/pc/r81_idle.ps1`):** the hidden LoadingOverlay's indeterminate ProgressBar kept animating, so the WinUI compositor ticked every vsync (`WinUiCompositorConnection.RunLoop` busy). Now `MainWindow.Lightweight.cs` turns IsIndeterminate on only while the overlay is visible. Idle CPU of one core: r79 16.8 % / 19.2 % -> r81 (= r80 build) 2.7 % / 2.4 % (r75 17.0 %). Idle private 2.43-2.64 GB unchanged, 283 threads unchanged (only 8 managed per dotnet-stack, rest native GPU/ANGLE/driver: INFERRED). Visual: r79 vs r80-build GPU frames (Lit, Clay, frame_selected) pixel-identical. R74 memsoak (same code): peak private 11.9 GB (R74 doc 12.7 GB), opens calgar 4.2 s / sorcerer 1.7 / vortex 1.5 (R76 not regressed), `Shots\r80\soak_r80.tsv (r81 code, built as r80)`. GPU: RTX 4090 via ANGLE D3D11 (renderer string, PROVEN). **Not done (reasons):** startup meshes are only thumbnail meshes (SkinWeights/Vector3 ~6 MB in the idle gcdump: INFERRED, gcdump lossy); idle GC heap 1.08 GB / 7.4 M objects is mostly the game catalog (not reworked); freeing GpuBatch.Source would not free RAM because the arrays are shared with the cached PreviewBatch / picking; 16-bit indices + smaller vertices + BC textures = renderer rework, not started; Parallel.For cap / thread pool: idle threads are native, no effect expected; disk-cache LRU needs deleting cache files (owner said delete nothing: needs approval); render scale at 4K not added. |
| R82 | 10/30 | t178u/t179u + R79 open items (viewer worker) | Viewer: summary tri count fix + 16-bit GPU indices | done (part) 10/30 ~17:43 KST, build `Bin\ForgeLens_r82` (v0.82.0). **(f) PROVEN (`--meshparts` cc_calgar, `Shots\r82\calgar_parts.tsv`):** the stats overlay was right, the summary was wrong. Every LOD 0 pool part of calgar is hidden (171,201 tris); the 44 drawn parts live in the LOD1 pool (342,768 tris, 251,794 verts). `TemplateMeshReader.BuildSummary` now shows the drawn set as `Shown · verts · tris · parts` when it differs from the LOD 0 pool (models whose drawn set = LOD 0 keep the old `LOD 0` text). Real window r82: summary `Shown 251,794 verts 342,768 tris 44 parts` = stats `342,768 / 251,794`. A first version with an extra `LOD 0 pool` item wrapped the summary and shrank the viewport (GPU 555x238 -> 555x218), so it was dropped. **16-bit indices:** `GlPreview.Upload` stores the IBO as ushort when the vertex pool has <= 65,536 verts (draws use UnsignedShort, offset x2). Calgar's drawn pool has 251,794 verts, so it stays 32-bit: GPU dedicated 1034 MB (r81) vs 1046 / 977 MB (r82, 2 runs) = no measurable change on calgar; sorcerer/vortex not measured. A/B script `tools/re/pc/r82_viewer_ab.ps1` (SM2_UETEST shots + private MB + GPU Process Memory counter + 20 s idle CPU): r81 private 4503 MB, idle 2.8 %; r82 4514 / 4388 MB, idle 1.6 / 1.3 % (same code path, noise not separated). All SM2_UETEST steps pass on r82 (view modes, bounds/grid 26 lines, hide 43 / solo 1 / undo 43,44 / redo 43 / show all 44). **Not proven:** pixel identity: the two runs opened at different DPI (GPU 555x238 vs 443x191), so frames could not be compared; wheel-zoom / frame-selection distances vary run to run within the same build (3.11/3.13, 1.34/1.23/1.35). **Not done:** (a) cloth-vertex Show, (b) Details panel, (c) eye icon, (d) dockable layout, (e) CPU view modes, (g) SendInput tests; smaller vertex format, BC/mip-limited textures, GC trim, catalog memory. 5.9 GB after reopening the last level: code check shows Recent is only a list (no startup auto-open, INFERRED from `RecentAssets` use), not measured. |
| R83 | 10/30 | t176u/t177u + R82 open UI items (viewer worker) | Viewer: Details panel, eye icons, dockable/collapsible saved layout, Show cloth vertices, CPU view modes, real SendInput test | done 10/30 ~18:25 KST, build `Bin\ForgeLens_r83` (v0.83.0). **(a)** Show menu "Cloth vertices (physics)": cyan cross on every physics-driven vertex of the frame (scene.VertexOverrides; GPU path, where the cloth runs). calgar: 8,409 drawn on, 0 off (`Shots\r83\uetest\win_calgar_r83_cloth_verts_on.png`). **(b)** DETAILS expander under the Meshes tree (own splitter, Copy button): name, pool (LOD n + pool verts), tris, used verts, index range, bounds, visible / hide reason, node + bone name, variant, blend, albedo / normal / spec (+channels), emissive, tint, td scalars; follows 3D pick, tree row, undo and deselect. **(c)** eye toggle per outliner row bound to the same `Visible` as the checkbox (checkbox kept). Real click on an eye: visible 44 -> 43, Ctrl+Z -> 44. **(d)** viewport toolbar "Panels" menu: outliner left / right, hide outliner (viewport takes the width: GPU 1167x965), Details on / off, reset; outliner width, Details height and toggles persist in viewport_r79.json and restore on start. **(e)** CPU path draws Unlit / Clay / Normals / UV checker with the GPU formulas (`PreviewSettings.ViewMode`, MeshPreview raster), shots `win_calgar_r83_cpu_view*_window.png`. **(g)** `tools/re/pc/r83_sendinput.ps1`: real Win32 SendInput into the real window + `SM2_INPUTPROBE` state line: hover, LMB select, empty-click deselect, Ctrl+Z / Ctrl+Y (selection), wheel x3 (dist 4.69 -> 3.20), Alt+LMB orbit (yaw +0.96..1.2 for 120 px), Alt+RMB dolly, Ctrl+1 / 1 bookmark (exact match), eye click + undo, F1 all PASS (`Shots\r83\sendinput*\sendinput_log.txt`). **Bug found by (g), fixed:** F with a selection framed the whole model in the real window (dist unchanged) because window tunnel KeyDown handlers registered later run first (PROVEN by the probe key log: the HUD handler's change was visible before the earlier handlers), so the Fly handler took F before UeKeyDown; SM2_UETEST called UeFrameSelection directly and passed. FlyKeyDown now frames the selection first; routed KeyDown F with Fly on: dist 4.686 -> 2.232. Not re-run with SendInput after the fix (see incident). **Incident:** at ~18:12 KST another worker's BLTest editor took the foreground during a SendInput run; the rest of that run's keys / clicks (wheel, Alt drags, Ctrl+1, Ctrl+Z, F1, H) went to BLTest (its log shows no undo transaction; its window placement test may be affected). The script now stops when the viewer loses the foreground. H via SendInput not proven (state read raced). **Other:** `SM2_WINRECT=x,y,w,h` pins the window (px) for A/B; `tools/re/pc/r83_viewer_ab.ps1` (pinned, viewport json restored). |
| R84 | 10/30 | t201u | Viewer: picking an animation plays it at once (no Space) | done 10/30 ~19:10 KST, build `Bin\ForgeLens_r84` (v0.84.0). `AnimChanged` now always plays a picked clip from t=0 (mouse row click, keyboard Down/Enter in the open list, Down on the closed box, wheel step); before only wheel steps did (ui-43 `_playOnLoad`). Model load / filter / context / HostVfx selections set `_suppressAnim`, so a model opened with no clip chosen does not play. Space still toggles pause/resume; Play/Pause button, scrub and loop unchanged. SM2_INPUTPROBE line gains `animbox animsel animn clip atime adur playing playbtn phys`. **Live (real SendInput, `tools/re/pc/r84_animautoplay.ps1`, Physics on), calgar + titus:** model load: atime 0, playing 0 (no auto-play); keyboard pick in the open list -> playing 1 / button "Pause" with no Space; Space -> playing 0, atime unchanged over 1 s; Space -> resumes; Down on the closed box while playing -> new clip from ~0 (calgar stop_walk 0.000 -> 0.923 in 1 s); mouse row click while playing -> new clip, +0.93..1.10 s per 1 s. Shots `Shots\r84\{calgar,titus}\pose_a/b/c/d.png` (calgar a vs b: different pose). **Not proven:** titus pose_b shows only the floor (camera / root motion of titus_radar_intro not checked); R79 picking / fly cam / bookmarks not re-run (code path untouched); double-click (ComboBox has none, a click already picks). |
| R85 | 10/30 | R82/R83 lightweight items (viewer worker) | GPU preview: packed vertices + BC1/BC3 preview textures | done 10/30 ~19:00 KST, build `Bin\ForgeLens_r85` (v0.85.0). Same build A/B by env switches, window pinned 2400x1500 (`SM2_WINRECT`), GPU frame read back in-process and pixel-diffed (`tools/re/pc/r85_light_ab.ps1`, `r85_pxdiff.py`; `Shots\r84\ab3`, `ab3b`, `ab2`). **Packed vertices** (pos f32x3, nrm snorm16, uv f32x2, joints u16, weights unorm16: 64 -> 44 B; `SM2_VTX64=1` = old): VBO calgar 15.4 -> 10.6 MB, sorcerer 9.1 -> 6.2, vortex 7.5 -> 5.2; frame diff 0.04-0.08 % px, max 32 summed abs. **BC1/BC3 textures** (own CPU encoder `Gpu/BcEncoder.cs`, CPU mips; `SM2_TEXBC=0` = old RGBA8): texture upload calgar 860.3 -> 116.3 MB, sorcerer 720.7 -> 110.5, vortex 512.0 -> 98.7; GPU dedicated (nvidia-smi) calgar 1160 -> 403 MB, sorcerer 1093 -> 398, vortex 793 -> 381; private calgar 4620 -> 3727, sorcerer 3939 -> 3078, vortex 3615 -> 3680 MB (1 run each, noisy); open 4.6/3.0/2.7 s -> 4.6/3.4/2.8 s (1 run). Frame diff 7.8-27.9 % px changed, mean abs 0.37-1.25, gt24 1.0-4.0 % (BC block error; side by side `Shots\r84\calgar_base_vs_bc_crop.png` looks the same by eye). **16-bit index (R82)**: no effect on calgar/sorcerer/vortex: IBO 3.9/2.0/2.2 MB in both, frame identical (all their pools are > 65,536 verts). **TexCap 2048** (`SM2_TEXCAP`): no texture of these models is > 2048, no change. **Not done:** cheaper GC trim, lazy/compact game file catalog (1.08 GB idle heap), idle 5.9 GB after reopening last level, re-run of the R83 SendInput test after the F fix. **Found:** at a 1600x900 window the GPU viewport is only 436x33 px (`Shots\r84\ab2\all_calgar_1\win_m.png`), the middle column squeezes it. |
| R86 | 10/30 | t200u | UE per-game roots `/Game/ForgeLens/<GAMECODE>/...` | done 10/30 ~19:45 KST. Rule: new imports go to `/Game/ForgeLens/<GAMECODE>/<Characters|Props|Animations|Materials|Tests|Levels|Textures>/<asset>` (SM2 -> `/Game/ForgeLens/SM2`, POE -> `/Game/ForgeLens/POE`). One table: `UnrealContentPath.GameRoots`/`Root()` (UnrealDirect.cs, viewer Send-to-Unreal default) = `tools/ue/fl_roots.py` (ue_bulk.py). Old root: env `FORGELENS_UE_ROOT=legacy` (SM2 -> `/Game/SM2_Work`) or `FORGELENS_UE_ROOT=/Game/<path>`, or type the path in the Content folder box. `/Game/ForgeLens`, `/Game/ForgeLens/<GAME>` and its category folders are refused as top-level; `/Game/SM2_Work/Original`, `/Game/Work` still refused. Existing `/Game/SM2_Work` assets NOT moved (3809 before). Live UE_VFX_57 test (ue_forge_direct.py, `Scripts\r84_root\new|legacy`): cc_calgar -> `/Game/ForgeLens/SM2/Characters/cc_calgar` 104 assets saved, SkeletalMesh loads, 44/44 slots resolved, 54/54 textures in folder; same into old path `/Game/SM2_Work/Tests/r84_legacy/cc_calgar` identical (SM2_Work 3809 -> 3913). Base-colour capture renders textured (`Shots\r84_root\front.png`). Not done: the one ADD clip failed to import on BOTH paths (path-independent, not investigated); the viewer GUI button itself not clicked (C# default unit-tested only); historic per-run scripts (tools/ue/ue_r5x/r6x, mtl_fix, tools/level/sky_env.py) keep their /Game/SM2_Work paths on purpose (they read existing assets). `ue_r84_roots.py count|check` verifies. |
| R87 | 10/30 | t178u/t179u + R85 open items (viewer worker, restarted after an interrupted run) | Viewer: 1600x900 layout, startup level defer, GC trim, compact catalog, BC quality check, real-input fling | done 10/30 ~20:00 KST, build `Bin\ForgeLens_r87` (v0.87.0; `Bin\ForgeLens_r86` = the interrupted run's untagged build of the same code before the rebase onto R86, not a release). **(1) 1600x900 squashed view FIXED** (`MainWindow.Layout86.cs`): windows < 1900 DIP get a smaller file-column star (saved ratio kept), toolbar stack in a ScrollViewer capped at 30 % of the viewer height, ViewBorder MinHeight 160, window MinWidth 960. SM2_WINRECT 1600x900 (125 % DPI = 1280x720 DIP): R85 GPU view 436x33 px -> r87 468x395 DIP (raw 585x494 px); 1100x640 DIP 339x307; 2400 wide unchanged (frame raw same size). `Shots\r86\layout\`, `Shots\r87\fling\start.png.txt`. **(2) GC trim after close** (`MemoryLog.cs`, default `GCCollectionMode.Aggressive`; `SM2_GC_TRIM=old|bg` = R74 / background): R74 memsoak (level->model switches) private after switching to a model: old 6354 / 6222 / 9156 MB -> aggressive 1994 / 2261 / 1600 MB; soak peak 10408 -> 9690 MB; trim pause old 245-581 ms -> 414-630 ms (UI stalls that long once per trim); bg mode freed little (committed 7641->6627). `Shots\r86\gc\`. **(3) catalog memory** (`GameCatalog.cs`: per-file name strings + 4 dictionaries -> 2 sorted index arrays, binary search, OrdinalIgnoreCase): `tools/re/pc/r86_catmem` 511,392 files: managed heap 222.8 -> 106.9 MB, private 407.8 -> 391.0 MB, load 3.11 s both, 3000-path self-check 0 bad, 12,000 lookups 8 -> 27 ms. Startup idle with nothing selected: private 758 -> 669 MB (1 run each). **(4) 5.9 GB idle = startup restore rebuilt the last LEVEL (PROVEN, `r86_idle.ps1`, 75 s no input):** r85 last=level 5835 MB, last=calgar 2515, none 758. Now a level restored at startup is selected but not built; an "Open last level" button in the viewport opens it (`MainWindow.StartupDefer.cs`; `SM2_REOPEN_LEVEL=1` = old). r86-code: last=level 589 MB; real click on the button -> level built, 5853 MB (`r86_deferclick.ps1`, `Shots\r86\idle\`). Models still reopen (2293 MB). **(5) real input:** `tools/re/pc/r87_fling.ps1` on r87: plain LMB fling 300 px / 80 ms -> yaw +1.20 rad at release, +1.20 rad coast after release, stopped within 650 ms (0 after); Alt+LMB fling +0.48 coast; wheel +3/-3 dist 4.686 -> 3.208 -> 4.506. R83 SendInput set re-run on r86-code (`Shots\r86\input\sendinput_log.txt`): hover, select, deselect, Ctrl+Z/Y, F with selection (dist 4.686 -> 2.232 = R83 fix confirmed), wheel, Alt orbit/dolly, bookmark exact, eye + undo, H, F1 all PASS. R84 auto-play on r86-code (`Shots\r86\autoplay\anim_log.txt`): load does not play; keyboard pick plays with no Space; Down while playing starts the next clip at 0.000; mouse row pick +0.925 s per 1 s. Space pause/resume NOT proven: the picked clip was 0.167 s long (atime read 0.000 both sides). r87 auto-play re-runs (3x) and the 2nd fling run ABORTED by the foreground guard (Chrome extension window, Grok Bot app took focus: owner active), so the final r87 binary has fling run 1 only; the 2nd aborted run did not restore the cursor. **(6) BC quality vs RGBA8** (same build, `SM2_TEXBC`, `Shots\r86\bc\summary.tsv`, `cmp_*_rgba_bc13_diffx4.png`): BC1/BC3 vs RGBA8 frame: calgar 22.8 % px differ, mean abs 1.17, >24 3.99 %, p99 57 (sum of 3 channels); sorcerer 15.0 % / 0.98 / 3.94 % / 55; vortex 10.1 % / 0.48 / 1.28 % / 29; by eye same (sheet). GPU dedicated 1016/856/668 -> 258/245/233 MB, private 4069/3358/3093 -> 3126/2688/3012 MB, open 4.7/3.4/2.8 -> 4.2/3.5/2.7 s (1 run). BC7 trial (`SM2_TEXBC=7`, BCnEncoder Fast): open 8.1-10.9 s, idle CPU 2853-2956 % right after open, vortex diff worse (mean 4.25) = REJECTED, env-only. Show menu "Compressed textures" toggles BC vs RGBA8 (persisted). **Not done this round:** thread cap and freeing CPU mesh copies (R81 analysis stands: idle threads native, arrays shared with picking; not re-measured); model-open profile not re-run (R76 --openbench stands). Tests: SM2.Assets 415/416 (EffectTests.HiddenSourceStillSpawnsAttachedChildren fails on the clean R86 tip too), Extractor 43/43. Distributed build folder has only exe/dll/json/pdb. |
| R88 | 10/30 | t220u (block owner input during real-input tests) + t221u (remove all Korean from the viewer UI) + R87 open input checks | Viewer retest on r87 with input blocked; English-only UI | done 10/30 ~20:20 KST, build `Bin\ForgeLens_r88` (v0.88.0). **(1) input blocking:** new `tools/input_block.ps1` (runs a test script with the owner's physical input blocked; 60 s watchdog kills the run = unblock; finally unblocks; cursor restored; covering windows minimised + restored, never closed, never UnrealEditor). `-Mode blockinput` (user32 BlockInput, self-elevated, UAC ConsentPromptBehaviorAdmin=0 so no prompt) works for keys/panels/wheel/combo picks but the 3D viewport ignored injected LMB + hover (r83 select/deselect/undo/F and fling all no-ops, `Shots\r88\input,input2,input3,fling,fling2`; cause not proven). Default `-Mode hook` (WH_MOUSE_LL/WH_KEYBOARD_LL drop events without the INJECTED flag, no elevation) works for everything. r83/r84/r87 scripts: `Focus` calls `InputBlockOn`; `Guard` re-focuses up to 4x (minimising a covering window) instead of aborting; r83/r87 log a hit-test at the viewport centre and reset stuck buttons. Rule added to RULES_CORE + SUBAGENT_PREAMBLE. **(2) r87 results (all PASS):** auto-play x3 (`Shots\r88\autoplay1-3`, BlockInput mode): load does not play; keyboard pick plays with no Space (+0.075 s on a 0.867 s clip near its end, +0.956 / +0.943 s per 0.9 s on the 3.333 s idle clip); Down while playing starts the next clip; mouse row pick +0.936/0.934/0.942 s per 1 s. **Space pause/resume PROVEN** on calgar_idle (3.333 s): pause delta 0.000 s over 1 s (playing 0), resume +0.867 / +0.874 s per 1 s (playing 1). R83 set (`Shots\r88\input4`, hook mode): hover, select, deselect, Ctrl+Z/Y, F (dist 4.686 -> 2.232), wheel, Alt orbit/dolly, bookmark exact, eye + undo, F1 on/off PASS. Fling (`Shots\r88\fling3`, hook): plain +1.20 rad at release, +1.20 coast, stopped by 650 ms; Alt +0.24 coast; wheel 4.686 -> 3.193 -> 4.486. Input blocked: 16.6+14.7+14.6 s (BlockInput autoplay) + 21.5+13.9+21.5+13.9+21.8 s (BlockInput, failed viewport runs) + 21.0+13.3 s (hook) = 173 s total; hook mode dropped 322 + 1754 physical events (owner was active). **(3) Korean removed (t221u):** `PropAnimStates.cs` prop condition labels (Auto label, 40 state labels: Idle, Charging, Tap (cylinder spin), Cooling, Shoot N, Last shot, Aux shot (augmented aim), Reload start/end, Full reload, Melee hit N, Hit stop, Teeth spin N, Teeth stop, Power on/off, Heat start/end, Light heat start, Heat hit, FX idle, Charge on/off/hold, Hit (no stack), Stack hit N, Init, Finisher N, Start, Stop, Open, Close, Used, Active, Screen on); `MainWindow.axaml` "Prop anim / 프롭 애님" -> "Prop anim", two tooltips "(재생 조건)" dropped; comments in UnrealDirect.cs, BrowserSettings.cs, ExportWindow.axaml, MainWindow.Direct.cs, MainWindow.PropAnim.cs, tests, and the embedded `tools/ue/ue_forge_direct.py` header. No language setting / localisation table exists. Check: src+test grep 0 Hangul; r88 ForgeLens.dll / LibSaber.SM2.Assets.dll contain none of the old strings (r87 did); r88 window shot `Shots\r88\ui_scan\start.png` shows "Prop anim". Not checked live: menus/dialogs/export window opened by hand (UIA tree is empty for this Avalonia app). Hangul still possible from game data names (not ours) and in tools/ / docs (not UI). Tests: SM2.Assets 415/416 (same known EffectTests failure). |

## B. 기능 요청 (Feature requests)

| # | 날짜 | 요청 (요지) | 업계 용어 — 뜻 | 상태 | 구현 위치 |
|---|---|---|---|---|---|
| F01 | 10/24 | SM2 리소스를 탐색기처럼 보고 미리보기한 뒤, 고른 것만 Unreal/Blender로 내보내기(통째 추출기 아님) | Asset browser + selective exporter — 선택형 에셋 브라우저/익스포터 | done (aa632fa…bedc6aa) | `src/LibSaber.SM2Browser`, `src/LibSaber.SM2Extractor` |
| F02 | 10/26 | 초기 오픈 항목: 타이라니드 색, 얼굴 투명 슬롯, 라이노 겹친 파츠, .sani 공백, push/테스트 | Bug triage backlog — 초기 결함 목록 | done (ef29997, 04db5dc, 09cae49, e732a67, 3558e4c) | HANDOFF "Open items", "2026-10-26 update" |
| F03 | 10/26 | 승인한 작업 순서(GPU 렌더러 → 애니 마무리 → 리타겟 → 머티리얼 재점검 → 벌크 검증 → 2차 물리 → 브라우저 VFX → 인게임 비교 → UE export 테스트 → 뷰어 다듬기) | Approved roadmap — 승인된 작업 순서 | done (VFX만 ≈95 %) | `docs/handoff/ROADMAP.md` 1–10 |
| F04 | 10/26 | 머티리얼을 인게임처럼(second spec 금속 틴트, aniso, blendMode, albedo tint) | Material parity (PBR/Substrate) — 게임과 같은 재질 재현 | done (19c7eb0, c1c4de0, e6cadfa) | `M_SM2`, `import_sm2_materials.py`, `tools/ue/ue_mat_apply.py` |
| F05 | 10/26 | .sani 애니메이션 디코딩·내보내기(루트모션, additive, 멀티클립) | Animation codec RE + export — 애니 포맷 해독 | done (b5b1225, e7278b0, 4b2cd67) | `Animation/SaniReader.cs`, `EtnaCurves.cs` |
| F06 | 10/26 | 마네킹 → SM2 리타겟 프로토타입 | IK Retargeting — 골격 간 모션 옮기기 | done 70 % (d2fafab, 05bb958, db3e4a5, 4df3cab); 손가락 컬 잔차 open | `Retarget/MannequinRetarget.cs`, `tools/ue/ue_sm2_retarget.py`, `ue_sm2_finger_curl.py` |
| F07 | 10/27 | 퓨리티 씰·천이 브라우저에서 인게임처럼 흔들리고 Unreal까지 이어지게 | Secondary physics (cloth / jiggle) — 2차 물리 | done + UE 검증 (642fb56, c556a97, 4df3cab) | `Physics/PhysicsBake.cs`, `PhysicsSidecar.cs` |
| F08 | 10/27 | Unreal export 시 물리 흔들림 설정이 자동 적용(작은 플러그인/EngineWire 괜찮음) | Auto physics setup on import (PhysicsAsset + post-process AnimBP) — 임포트 시 물리 자동 구성 | done (1ec13c4, 4e83d0a, bc1ac55) | `tools/ue/SM2PhysicsSetup/`, `tools/ue/ue_sm2_physics.py`, `ue_bulk.py` |
| F09 | 10/27 | 프로그램 이름 "Forge Lens" | Product branding — 제품명 | done (5ff18a4) | `ForgeLens.exe` (csproj AssemblyName) |
| F10 | 10/27 | 모던 다크 UI | Dark theme — 어두운 테마 | done (5ff18a4) | `src/LibSaber.SM2Browser/App.axaml` |
| F11 | 10/27 | UE5 마네킹 스켈레톤에 바로 얹히는 애니만 export(언리얼에서 리타겟 없이) — "Anim Only (Manny)"; 이후 "몸이 떠서 기울어짐" 신고 수정 | Export-time retarget bake — 내보낼 때 리타겟을 구워 넣기 | done (07bf169, e1fd655 root fix) | `--anim-only-manny`, `tools/ue/ue_import_manny.py` |
| F12 | 10/27 | 인게임과 나란히 비교(VFX·물리·머티리얼), 사용자 사진 6장 기준 | Side-by-side reference comparison — 기준 이미지 대조 | done (정적, 30563e4); 물리 모션 비교는 F45 | HANDOFF vfx3u, mtl1-48; `tools/vfxbench/side_by_side.py` |
| F13 | 10/27 | Niagara 없이 Unreal export 테스트 | Export smoke test — 내보내기 시험 | done (afcc556) | `tools/ue/ue_vfx_export_test.py`, `/Game/SM2_Work/VfxExport` |
| F14 | 10/28 | 인게임 캡처(RenderDoc식)를 에이전트가 직접 분석, Mod Loader 활용 | GPU frame capture analysis — 그래픽 프레임 캡처 해석 | done (e27737d mtl1-23, 94e77d0) | `tools/rdc`, STATUS mtl1-23 |
| F15 | 10/28 | VFX 볼 때 이펙트 전체가 화면에 들어오게 카메라 거리 자동 | Auto-framing (fit to bounds) — 경계 맞춤 카메라 | done (9de6033) | `EffectCamera.FitAll/FitBox`, Fit 버튼·F 키 |
| F16 | 10/28 | 데모 샘플 메시(구/평면)에 오버레이 셰이더로 표면 효과 미리보기 | Preview host mesh / overlay (decal) layer — 시연용 메시와 덧칠 레이어 | done (b5605ab, HEURISTIC) | `MainWindow.HostVfx.cs` |
| F17 | 10/28 | VFX 검은 사각형·옆으로 흐름, 티투스 흰 타바드(천 SIM 껍데기) 버그 수정 | Bug fix (alpha-less sprite, fly-by, cloth SIM shell) — 렌더 결함 수정 | done (9de6033) | `VfxDrawKinds.Resolve`, `TemplateMeshReader` |
| F18 | 10/28 | export에 In place / Root motion 선택(Anim Only (Manny) 포함) | Root motion extraction option — 이동량 제거/유지 선택 | done (9de6033) | Export 창 "Root" 콤보, CLI `--anim-root` |
| F19 | 10/28 | UI 직관적·깔끔하게, 파일 리스트 열 드래그로 폭 조절 + 저장(긴 PATH 다 보이게) | Resizable / persisted columns — 열 너비 조절·저장 | done (9de6033) | `FileColumns.cs`, `BrowserSettings.cs` |
| F20 | 10/28 | 기본은 미리보기 가능한 에셋만, "모든 파일 보기" 체크박스(상태 유지) | Asset type filter — 종류별 표시 필터 | done (9de6033) | `MainWindow.axaml.cs`, settings.json |
| F21 | 10/28 | 트리 펼침·선택 폴더/파일·스크롤·검색 상태 복원, 클릭/방향키로 즉시 미리보기 | Session state persistence — 마지막 화면 상태 복원 | done (9de6033) | `BrowserSettings.cs` |
| F22 | 10/28 | 애니 리스트 위 마우스 휠 = 이전/다음 클립 선택 후 바로 재생(필터 안에서) | Wheel navigation — 휠로 목록 이동 | done (2fcdc22) | `WheelComboBox.cs` |
| F23 | 10/28 | UE5 Content Browser 스타일 전면 개편(즐겨찾기, 폴더/카테고리 트리, 색·개수 있는 컬렉션, 썸네일 그리드, 루프 애니 썸네일, 필터, 검색), 즐겨찾기/컬렉션 저장 | UI overhaul (Content Browser pattern) — 언리얼식 에셋 브라우저 | done (eb95c03, 7d60486; ui-43) | `src/LibSaber.SM2Browser/ContentBrowser/`, `MainWindow.ContentBrowser.cs`, `Catalog/AssetCategories.cs` |
| F24 | 10/28 | Kitbasher처럼 .sso 의상에서 진영/클래스/프리셋/슬롯별 파츠 선택, 그대로 export | Modular character kitbashing — 조립식 파츠 구성 | done (68d38aa, 61815b7; ui-44) | `Customization/Kitbash.cs`, `MainWindow.Kitbash.cs` |
| F25 | 10/28 | Kitbash를 적/NPC 파츠로 확장 | Enemy/NPC variant support — 적 캐릭터 변형 | done (998a770; ui-45) | `MainWindow.Npc.cs`, `npc=` `--variant` |
| F26 | 10/28 | 다음으로 근접무기 스킨 선택(+ 스킨 재질) | Weapon skin variants — 무기 외형 선택 | done (998a770, c95af37, 30563e4) | `MeleeCustomization`, skin materials (td `materials` block) |
| F27 | 10/28 | 파츠 선택기에 artificer 프리셋 | Preset catalog (artificer) — 고급 외형 프리셋 목록 | pending | (예정) `Customization/Kitbash.cs` |
| F28 | 10/28 | 밤샘 TODO #1–7 질문 없이: BongThumbLive, VFX 블렌드 확인, 물리 육안 확인, 밑단 스파이크/카디안 손가락, 총 스킨 부착물, 근접 재질/손 숨김, 인게임 비교 | Overnight batch — 야간 일괄 작업 | done (2454ba3, 4df3cab, 2ce469a, c95af37, 30563e4) | overnight_decisions.md |
| F29 | 10/29 | 인게임 같은 컬러 스킴 선택(게임 데이터의 같은 이름·색), UI 신경 써서 | Colour scheme / palette swap — 색 구성 교체 | done (bddd777) | `Customization/ColourSchemes.cs`, `MainWindow.Colours.cs` |
| F30 | 10/29 | 이펙트 단독 ↔ 관련 모델 + 시전 애니와 함께 보기 토글(타이밍·위치는 게임 데이터) | In-context playback — 맥락 재생 | done (e0eb012); Shoot→muzzle 링크는 HEURISTIC | `Context/ContextIndex.cs`, `ContextResolver.cs`, `MainWindow.Context.cs` |
| F31 | 10/29 | 반대로 모델 선택 → 연결 애니 + 액션별 맞는 소품/총·이펙트를 정확한 위치·타이밍에, 후보가 여럿이면 고르기 | Reverse lookup / asset linkage — 역방향 연결 탐색 | done (e0eb012) | 같은 Context 모듈 (`SM2_CTX_WEAPON` 등) |
| F32 | 10/29 | 소품과 이펙트 따로 켜고 끄는 토글 | Visibility toggles — 표시 토글 | done (e0eb012) | `MainWindow.Context.cs` (`SM2_SHOW_PROPS/FX`) |
| F33 | 10/28 | 언리얼 라이브 썸네일 플러그인 이름 "BongThumbLive"(구 AnimThumbLive) | Plugin branding — 플러그인명 | done | `K:\Unreal Projects\_Plugins\BongThumbLive` (repo 밖) |
| F34 | 10/28 | 폴더 열면 애니 썸네일이 hover 없이 계속 재생 | Live thumbnails (auto-play) — 자동 재생 썸네일 | done | BongThumbLive; `/workspace/animthumb/REPORT.md` |
| F35 | 10/28 | 머티리얼·머티리얼 인스턴스 썸네일도 라이브 | Live material thumbnails — 재질 썸네일 실시간 | done | BongThumbLive |
| F36 | 10/28 | Niagara 썸네일도 라이브, 전체가 들어오게 프레이밍, 설정에서 on/off | Live Niagara thumbnails + framing — 이펙트 썸네일 | done | BongThumbLive |
| F37 | 10/28 | Cascade 썸네일도 라이브 | Live Cascade thumbnails — 구형 파티클 썸네일 | done | BongThumbLive |
| F38 | 10/28 | 설정 UI 항목 이름·설명 한국어, 타입별 라벨은 언리얼 Content Browser 타입 색 | Localization + type colour coding — 현지화, 타입 색 구분 | done | BongThumbLive 설정 (IDetailCustomization) |
| F39 | 10/28 | 전역 on/off, FPS, 재생 범위 모드(선택만/보이는 것만/폴더 전체), 기본 "보이는 것만" | Playback scope settings — 재생 범위 설정 | done (실제 모드: hover만/선택만/보이는 것 전부) | BongThumbLive 설정 |
| F40 | 10/28 | Hover FPS 따로(기본 60), 나머지는 Max FPS | Per-state frame-rate cap — 상태별 프레임 제한 | done (Hover 60, Max 20) | BongThumbLive 설정 |
| F41 | 10/28 | "보이는 것만" 모드에서 동시 재생 수 = 보이는 개수 자동(아이콘 크기 변경 포함), 수동 최대치는 고급 옵션 기본 0=자동, 대기 썸네일은 마지막 프레임 유지 | Adaptive concurrency + last-frame hold — 자동 동시성, 마지막 프레임 유지 | done | BongThumbLive ("틱당 최대 갱신 썸네일 수" 0=auto, no revert) |
| F42 | 10/28 | 프레임 캐시 방식(Cache FPS 기본 60, 작게), 캐시 크기 표시 + 캐시 지우기 버튼 | Frame cache (LRU) — 미리 렌더한 프레임 저장 | done (캐시 60 FPS, 256 MB LRU); "캐시 지우기" 버튼은 REPORT에 기록 없음 → 다음 작업 때 확인 | BongThumbLive |
| F43 | 10/28 | .td의 텍스처 아닌 파라미터도 임포터에 반영 | Material parameter import — 재질 수치 파라미터 반영 | pending | (예정) `import_sm2_materials.py` / sidecar |
| F44 | 10/29 | 디스크 정리(테스트 export·옛 빌드·전송 zip) | Storage cleanup — 저장 공간 정리 | done (≈159 GB, 10/29 04:35) | overnight_decisions.md "Item 4" |
| F45 | 10/27 | 물리 움직임을 게임과 비교 | Physics motion validation — 물리 거동 검증 | pending (캡처 없이 할 방법 필요, R13) | ROADMAP "Later / backlog" |
| F46 | 10/29 | 티투스 걸으며 사격 시퀀스(Sequencer, 본별 레이어 블렌드) | Layered blend per bone (upper/lower body) — 상하체 분리 블렌드 | done v2 (Gun_pose 부착, 총구 끝 머즐, 반동은 별도 additive 슬롯, 생성한 pitchUp35 조준 포즈); 남음: 머즐 플래시를 총구 앞·측면 각도로, 생성 조준 포즈 다듬기 → in progress | UE `/Game/SM2_Work` Sequencer; box `/workspace/titus_seq/` |
| F47 | 10/29 | 체인소드 sfx 검은 판 수정 + 캐릭터 컬러 스킴 스크린샷 | Bug fix (sfx black quads) + visual QA — 렌더 결함 수정, 결과 캡처 | done (mtl1-51): td `preset = "sfx"` 파츠("sfx")와 placeholder 재질 sfx 로케이터("sfxSource")는 기본 숨김, Meshes 탭 "FX parts" 토글로 켜면 가산(additive) sfx 패스로 그림, `SM2_SHOW_SFX_PARTS=1`은 예전 동작. 캐릭터 컬러 스킴 피커는 **미지원**(무기 전용 `wpn_` 게이트, 스킴 소스 = 무기 diffuse td `materials` 변형뿐; 아머는 `armor_color_library.sso` 레이어 파라미터라 소스·적용 경로 둘 다 없음) → 구현 대기 | `Meshes/MaterialMaps.cs` (HideSfxParts), `Gpu/GlPreview.cs` (PreviewBlend.Sfx), RE_NOTES "mtl1-51", box `/workspace/mtl_fix/mtl51/` |
| F48 | 10/29 | 티투스 물리 sim 본 전수 점검(앞치마 해골 장식 포함, 빠짐없이) | Sim-bone audit / coverage check — 시뮬 대상 누락 점검 | done (조사: box `/workspace/mtl_fix/titus_phys/REPORT.md`; 수정은 F52) | box `/workspace/mtl_fix/titus_phys/` |
| F49 | 10/29 | OOM 이후 REA 재개: 라이트 감쇠 `attenCurveParam`, `UseSpecMap` (10/29 재요청: "아까 하던 rea 작업 이거 중단된거 여유될때 잊지말고 해") | RE follow-up (resume after OOM) — 메모리 부족 후 분석 재개 | pending (phys52 동안 손대지 않음; 다음 여유 작업 1순위) | `heuristics4_progress.md` items 2·4, `tools/re/` |
| F50 | 10/29 | 노출(exposure) 규칙 증명 | sfx emissive exposure k = `adaptiveIntensity` (FUN_1424540e0) — 자동 노출 계수 | done (8bdd80a) | VFX 렌더 (`SM2_VFX_SFX_LUM_K`) |
| F51 | 10/29 | 파워피스트 손 숨김 규칙을 게임 스크립트에서 | SSLB bytecode rule (RIGHT_GAUNTLET = 6 PROVEN) — 컴파일된 스크립트에서 규칙 추출 | done (fcc16bf) | `Scripting/Sslb.cs`, `PowerFistHandRule.cs` |
| F52 | 10/29 | 제안한 두 수정을 모든 캐릭터에 일반 규칙으로 적용: (1) 플러그인이 jiggle을 `"Name"`(없으면 `"bone"`)으로 읽고, 건너뛴 jiggle은 missing으로 세고 로그, 플러그인 재빌드(에디터 재시작 사전 승인, 크래시 프로토콜) (2) `prop_motion_damper_controller`를 PhysicsParser에서 파싱(클라이언트 .cls에 없으면 authority .cls), sidecar에 내보내고, UE에서 스프링-댐퍼로 구현(추정치는 HEURISTIC 표기). 티투스 물리 다시 적용(.bakN 백업, 보호 경로·Lightning_01 금지, PIE/SIE 종료), 검증: jiggle 바디 34개, 걷기 클립에서 상자 흔들림 before/after 이미지·gif → `/workspace/mtl_fix/titus_phys/after/`, 다른 작업자 Forge Lens 배포와 충돌 없이 rebase + push.ps1(ff-only, force 금지), 메모리 적게, HANDOFF/문서 갱신 | Data-driven secondary-motion import (key fallback + spring-damper constraint) — 키 불일치 수정과 모션 댐퍼 이식 | done (phys52, 이 커밋; HANDOFF "Update phys52") | `SM2PhysicsSetupLibrary.cpp`, `Physics/PhysicsParser.cs`, `PhysicsLocator.cs`, `PhysicsSidecar.cs`, box `/workspace/mtl_fix/titus_phys/after/` |
| F53 | 10/29 | PIE를 멈추면 Message Log에 "Trying to simulate physics on ... but it has ComplexAsSimple collision" 경고 1,626개 → 물리 셋업/임포트에서 일반 규칙으로 수정(시뮬 바디는 ComplexAsSimple 금지, 컴포넌트 SimulatePhysics 켜지 않기), 짧은 PIE로 사라졌는지 확인(PIE는 항상 종료) | Collision trace flag (UseSimpleAsComplex) + no world-scene double simulation — 충돌 복잡도 플래그, 이중 시뮬 제거 | done (phys52, 이 커밋): 경고 0건, PIE 틱 ≈20 → ≈50 fps | `SM2PhysicsSetupLibrary.cpp` (`NormalizeBodies`, `FixWorldPhysics`), HANDOFF "Update phys52" |
| F54 | 10/29 | 캐릭터 컬러 스킴 피커 (인게임 챕터 이름·색) | Feature (in-game colour schemes on characters) — 게임 데이터 그대로의 이름/색으로 캐릭터 도색 미리보기·내보내기 | done (mtl1-52): `wpn_` 게이트 제거, 캐릭터는 `global_armor_color_pattern_library.sso` 104개 스킴(+섹션 오버라이드) → `armor_color_library.sso` 레이어(tint/patternTint/metalnessTint/metal·rough scale+bias), 이름은 `ui_heraldry_library.sso`→strings(97개 PROVEN, Champion 7개 HEURISTIC 표기). td에 `"<variant> CC"` 블록이 있는 재질만 CC 마스크 채널별로 _a 알베도에 CPU 베이크해요(셰이더 레이어 루프 모양 PROVEN, layer0..2=primary/secondary/tertiary INFERRED, 섹션↔파츠 매칭·isMetallic·패턴 텍스처 배열 HEURISTIC/미구현). 캠페인 티투스 `*_02` 아머는 게임 데이터에 CC 블록이 없어 고정 도색이라(PROVEN) 헬멧·acc·크럭스 견갑만 바뀌고, 오퍼레이션 아머(cc_sm_soldier_story)는 전체가 바뀌어요. 내보내기 시 `<tpl>.colour_scheme.<id>.json`(UE MI 파라미터) 생성. QA `SM2_COLOUR_DUMP` | `A/Customization/ArmorColourSchemes.cs`, `MainWindow.Colours.cs`, RE_NOTES "mtl1-52", box `/workspace/mtl_fix/mtl52/` |
| F55 | 10/29 | 트레일 fx 뷰어 표현(무기 휘두를 때 궤적, 맥락 재생에서 이벤트 구간만, Effects 토글 따름, 트레일 데이터 있는 근접무기 전부) | Ribbon trail (swept weapon trail) — 리본 트레일 | done (trail53, 이 커밋; HANDOFF "Update trail53"); 리본 생성 규칙 HEURISTIC, UE export 범위 밖, Niagara 안 씀 | `Effects/SweptTrails.cs`, `AnimEventVfxMap.cs` (TrailEdge), `EffectHost.cs`, `Effects/HostVfx.cs` |
| F56 | 10/29 | 프롭/무기 자체 애니메이션: tpl 내장 시퀀스 디코더(2,811개, 가시성·루트 포함), 프롭 단독 재생(tpl별 시퀀스 목록), 캐릭터 동작(사격/재장전/근접/충전, 체인소드 톱니)과 맥락 동기화, tpl 폴더 .sani 56개 클립 목록 포함, UE 내보내기 | Rigid prop animation (embedded node animation) — 메시 노드(오브젝트) 단위 키프레임 애니 | done (propanim, 이 커밋; HANDOFF "Update propanim"): 키 레이아웃 DATA-PROVEN(REA 다운), 가시성→scale 0·트리거 분기·장전 클립 판정 HEURISTIC | `Animation/TplAnimation.cs`, `PropAnimStates.cs`, `AnimEventVfxMap.cs` (EventAnim), Browser `MainWindow.PropAnim.cs`, Extractor `--anim seq:` |
| F57 | 10/29 | (F56 조정) 맥락 뷰에 "Prop anim / 프롭 애님" 토글(기본 ON, 저장) + 재생 조건 드롭다운: "Auto(캐릭터 동작 따라감)" 기본 + 게임 데이터의 무기 상태(IDLE, SHOOT_1/2/3, SHOOT_AUX, RELOAD_START/END/FULL, anim1–4, chain_anim 1–4단, 충전 on/off …), 데이터 이름 + 짧은 한글 라벨, 단독/맥락 뷰 모두 | Playback condition override — 재생 조건 강제 선택 | done (propanim): `PropAnim`/`PropCondition` 설정 저장, env `SM2_PROP_ANIM`/`SM2_PROP_COND` | `MainWindow.PropAnim.cs`, `MainWindow.axaml` (`CtxPropAnimToggle`, `CtxPropCondBox`, `PropCondBox`) |
| F58 | 10/29 | 인게임 체인소드 휘두르기 트레일은 저런 어두운 부채꼴이 아니에요 → 일반 규칙으로 수정(노멀맵 텍스처·distortion 트레일은 색으로 안 그림), 노란 additive 궤적이 밝은 호로 보이는지 확인 | Blend-state fix (additive trail) + distortion-trail culling — 트레일 블렌드 상태 교정, 굴절 트레일 색 패스 제외 | done (fix54, 이 커밋; HANDOFF "Update fix54"): 어두운 부채꼴의 주원인은 노란 트레일(add)이 straight-alpha로 그려져 회색 꼬리가 배경을 어둡게 한 것(캡처 PROVEN 상태 + selector INFERRED), trail_melee_dark(노멀맵) 제외는 INFERRED/HEURISTIC 표시. 실제 게임 화면 비교는 아직이에요 | `Effects/CaptureRules.cs` (`VfxBlendRules.ForTrail`), `Gpu/EffectRenderer.cs`, `Effects/EffectTrails.cs` (`SfxTextureUsage`), `Effects/HostVfx.cs` |
| F59 | 10/29 | REA 일괄 처리 목록(감쇠, UseSpecMap, 트레일, 스프링 댐퍼, 컬러 스킴 어두움, 블렌드/패턴/렌즈, 프롭 애님)을 근거 우선으로 처리 | Batch reverse engineering — 묶음 역공학(헤드리스 Ghidra) | 일부 done (rea-batch + rea-batch 2; HANDOFF "Update rea-batch", "Update rea-batch 2"): 패턴 텍스처 배열·렌즈 파라미터, 재장전 트랙/사격 간격, 라이트 1/(d²+P0.w) 감쇠, TRAIL_DEGR 의미까지 추가했어요; 댐퍼 PROVEN, 컬러 스킴 어두움 PROVEN 셰이더 + INFERRED enum, 트레일은 증거만(코드 변경 없음), 감쇠·UseSpecMap·프롭 애님은 open. rea-batch 3(HANDOFF "Update rea-batch 3"): 라이트 desc → 렌더 라이트 필드 매핑 PROVEN(attenCurveParam → +0x54), UseSpecMap 실제 플래그 값 DATA, fadeParams 의미 PROVEN(라이트닝 트레일)·리본은 opt-in HEURISTIC, trail_melee_dark가 게임에서 안 보이는 이유 DATA(무기 .cls 덮어쓰기). rea-batch 4(HANDOFF "Update rea-batch 4"): UseSpecMap 비트 의미 DATA + 임포터 수정, P0.w writer·리본 점 작성기는 시도 기록만 하고 open. level-lights(HANDOFF "Update level-lights"): 허브 레벨 라이트를 추출해서 armory 캡처 라이트 4개와 위치 0 m로 짝지었고, P0.w 계산 함수 FUN_14224f010과 CB 패커 FUN_14224ecf0를 찾았어요. attenCurveParam → P0.w 매핑은 PROVEN(코드 + DATA)이에요. 벤치·UE 내보내기에 반영했고, 라이트 색 디코드는 2.2 감마로 바꿨어요(DATA) | `Physics/PhysicsParser.cs`, `Physics/PhysicsSidecar.cs`, `Customization/ArmorColourSchemes.cs`, `MainWindow.Colours.cs` |
| F60 | 10/29 | 원클릭 내보내기를 옵션으로도 만들어줘 (기존 내보내기는 기본 그대로, 체크하면 .uproject + 콘텐츠 폴더로 바로 UE에 임포트, 진행·로그·성공/실패 표시, 기존 폴더는 _bakN 백업) | Direct-to-Unreal one-click export — 내보내기 직후 열린 에디터(EngineWire / Python Remote Execution) 또는 헤드리스 UnrealEditor-Cmd로 자동 임포트 | done (oneclick, 이 커밋; HANDOFF "Update oneclick"): 모델·클립·프롭 SEQ 애님·Manny 클립·머티리얼(import_sm2_materials)·물리(SM2PhysicsSetup)·컬러 스킴 태그. PC 테스트 프롭/캐릭터/Manny × EngineWire, 프롭 × Remote Execution, 프롭 × 헤드리스 전부 OK, _bak1~3 백업 확인했어요 | `Export/UnrealDirect.cs`, `Export/UnrealBridge.cs`, `tools/ue/ue_forge_direct.py`, `ExportWindow.axaml(.cs)` (DirectPanel), `UnrealImportWindow.axaml(.cs)`, `MainWindow.Direct.cs` |

## C. 미래 (Future, 로드맵 밖)

| # | 날짜 | 요청 (요지) | 업계 용어 — 뜻 | 상태 | 위치 |
|---|---|---|---|---|---|
| X01 | 10/27 | `.sfx` → Niagara 변환(물어본 뒤에만) | Niagara conversion — 언리얼 파티클 시스템으로 변환 | future | ROADMAP "Possible future development" |
| X02 | 10/28 | SM2 끝나면 다음은 Path of Exile 읽기 | Multi-game support (PoE) — 다른 게임 포맷 지원 | future | ROADMAP (추가 예정) |
| X03 | 10/27–28 | 그 다음 Unreal Engine 게임 읽기(.pak/IoStore) | UE game extraction — 언리얼 게임 에셋 추출 | future | ROADMAP "Possible future development" |

합계: 상시 규칙 35 · 기능 요청 60 · 미래 3 = **98건** (2026-10-29 21:10 KST 기준; R33 인계, R34 새 계정 환경 구성 추가).

---

# >>> NEW AGENT: START HERE (bootstrap) <<<

You were given only this repo link. Follow these steps in order before you do anything else. When you finish, you should be able to continue exactly where the previous agent stopped.

## 0. Read this whole file
- Then read the newest dated "update" sections at the bottom; they are the latest state.
- `git log --oneline -15` on branch `cursor/sm2-pck-extractor-4420` is the source of truth for what is done.
- The repo is https://github.com/Baegovda/ForgeLens (private; migrated 2026-10-29 from the public fork https://github.com/Baegovda/LibSaber, whose PR #1 stays there and was not migrated).

## 1. Required connectors / MCP: check each one and install it if it's missing
Check with GetMcpServerStatus / GetDynamicTools. Install missing ones with SearchPlugins and InstallPlugin, then authenticate with AuthenticateMcpServer.

| What | Name | Used for | If missing |
|---|---|---|---|
| REA (reverse engineering) | namespace `user-rea` (plugin "REA"; Ghidra, JADX and similar) | the REA-first rule, mandatory | **Obsolete (2026-10-27):** the REA tool is now https://github.com/morluto/rea on the box — see `docs/handoff/WORKFLOW.md` → REA (morluto/rea) |
| GitHub | `cursor-github` (read-only) | PR and commit reads | RequestScmConnect / InstallPlugin |
| Owner's PC shell | ListMachines, then the machine named **Revaptor-FX** (id `d3fb99ad-25ca-45c1-81af-41e5ca09104a` (old account; on a new account use the id ListMachines shows for Revaptor-FX) at the time of writing) | PowerShell, Unreal control, push | the owner has to register the PC |
| Unreal Editor | **EngineWire / MCP Automation Bridge 0.5.30** plugin in the project (https://github.com/ChiR24/Unreal_mcp), driven by `tools/ue/enginewire_call.py` via `uerun.ps1`; fallback **UEMCP** (`pip install uemcp`, https://github.com/ATDev-Inc/uemcp, `tools/ue/uemcp_call.py`) | import, materials, captures | see step 2 and WORKFLOW.md §5 |

## 2. Tools: check they exist, install them if missing
- **Box (shared Linux machine):**
  - Ghidra at `/home/box/ghidra`, plus `/home/box/tools/ghidra-nativeaot`.
  - `rizin` at `~/.local/bin/rizin`.
  - dotnet at `/home/box/tools/dotnet`.
  - Game exe at `/workspace/rea/sm2_retail.exe`.
  - RE outputs: `/workspace/rea/out` is empty now (the old SANI_FORMAT.md / decode_sani.py are gone). Ghidra project `/workspace/rea/ghidra_proj2` (program sm2) and decompiles in `/workspace/rea/dec/<addr>.c`; decompile more with `JAVA_HOME=/home/box/tools/jdk25 /home/box/ghidra/support/analyzeHeadless /workspace/rea/ghidra_proj2 sm2 -process sm2_retail.exe -noanalysis -readOnly -scriptPath /workspace/rea/ghidra_scripts -postScript Decomp.java /workspace/rea/dec <addrs>`.
  - Repo at `/workspace/LibSaber`.
  - If any of these are missing, reinstall them, or copy the exe from the PC (D:\Steam\steamapps\common\Space Marine 2). Copy it to C:\Users\Revaptor_FX first, because the D: path can be refused.
- **PC:**
  - Python 3.10 with `pip install uemcp mcp`.
  - Copy `tools/ue/enginewire_call.py` and `tools/ue/uemcp_call.py` to `C:\Users\Revaptor_FX\`, `tools/ue/uerun.ps1` to `Desktop\SM2_Work\Scripts\`.
  - Unreal 5.7.4 project at `K:\Unreal Projects\UE_VFX_57`. The editor must be open, and it listens on 127.0.0.1 only.
- **Smoke test:** `python enginewire_call.py` lists 23 tools (`system_control`, `manage_asset`, …); fallback `python uemcp_call.py` lists `ue_python`.

## 3. How to push: the box has no GitHub login, the PC does (gh as Baegovda)
1. Box: `git bundle create /workspace/sm2.bundle <last-pushed>..HEAD`
2. CopyFromBox to `C:\Users\Revaptor_FX\sm2.bundle`. Check the size; one copy silently delivered a stale file.
3. PC: `cd C:\Users\Revaptor_FX\ls_push; git fetch ..\sm2.bundle HEAD; git merge --ff-only FETCH_HEAD; git push origin HEAD`
   - If `ls_push` is missing, clone the branch into it first.
- Commit identity: `-c user.name="Cursor Agent" -c user.email=cursoragent@cursor.com`.

## 4. Working contract with the owner (Baegovda님, Korean)
- Reply in Korean, short and plain.
- Report at each stage.
- Do items one at a time, in order, and confirm before starting the next big step.
- The owner granted full permission to build, install, push and manage folders. Assets (R61, owner 10/30): everything under `/Game/SM2_Work/` (incl. `Original/*`, `_bakN`, Tests, hologram_planet) is generated by our extraction: delete, rename, move, re-import, overwrite freely, no asking, no `_bakN` needed (check live referencers before deleting; log deletions in `docs/handoff/CLEANUP_DELETED.tsv`). Never touch anything outside `/Game/SM2_Work/` (`/Game/Work/*`, the owner's own content, `Lightning_01`); if unsure, leave it and list it. Source code / PC files: back up as `.bakN` (next free N, never overwrite an existing one).
- **REA first, always.** Base every format or shader conclusion on exe addresses or strings, and cite them.
- Work only inside the PC's `Desktop\SM2_Work` and Unreal's `/Game/SM2_Work`.
- **Next step at handoff:** step 3, second specular (gold metallicTint) plus anisotropic material layers. Then step 4, the `dotnet test` restore hang. Niagara (.sfx to Niagara) is deferred.
- At the end of every stage, update this file, commit and push.

---

# HANDOFF — SM2 → Unreal asset pipeline (read this first)

For any agent continuing this work: read this whole file before you do anything.

## Owner and how to work with them
- The owner is Baegovda (GitHub name; address them as Baegovda님). They speak Korean, so reply in Korean.
  - They want a short progress report at every stage, not just at the end ("중간중간 보고").
  - When they say "실행해줘", launch `LibSaber.SM2Browser.exe` on their PC.
- **Investigate by reverse-engineering first (mandatory).** For any file-format, material or shader problem:
  - Reverse-engineer the game exe with REA/Ghidra/rizin before guessing from bytes.
  - Cite the addresses or strings you based each conclusion on.
  - Do not guess from bytes.
- **Goal:** an explorer-style GUI that browses Space Marine 2 resources, shows previews, and exports only the selected assets to Unreal or Blender. It is not a bulk extractor.

## Environment
- **Owner's PC (Windows, "Revaptor-FX")**
  - Tools: `C:\Users\Revaptor_FX\SM2Extractor` (`LibSaber.SM2Browser.exe` GUI, `LibSaber.SM2Extractor.exe` CLI).
  - Game: `D:\Steam\steamapps\common\Space Marine 2`.
    - Paths on D: can be refused. If so, copy the file to `C:\Users\Revaptor_FX\` first.
  - Reference viewer: IndexV2 at `C:\Users\Revaptor_FX\IndexV2`. It has no .sani decoder.
- **Unreal Engine 5.7.4** project at `K:\Unreal Projects\UE_VFX_57`. Log: `Saved\Logs\UE_VFX_57.log`.
  - **Editor control: EngineWire (primary, `enginewire_call.py`, http://127.0.0.1:3000/mcp + token) or UEMCP (fallback, `uemcp_call.py`).** Both accept the same `ue_python ue_args.json`. Run on the PC in PowerShell:
    `cd C:\Users\Revaptor_FX; $code=@' <python> '@; @{code=$code}|ConvertTo-Json|Set-Content -Encoding utf8 ue_args.json; & python enginewire_call.py ue_python ue_args.json` (fallback: `uemcp_call.py`)
  - **Screenshots** only work when the Level Editor viewport is visible. Moving the viewport camera from a script often does nothing.
  - **Importing animations.** Import animation-only FBX with the legacy importer:
    1. Run console command `Interchange.FeatureFlags.Import.FBX 0`.
    2. Import with FbxImportUI set to FBXIT_ANIMATION and the target skeleton.
    3. Set the flag back to 1.
    - Reason: Interchange in 5.7 has bug UE-307815.
- **Box (shared Linux machine):** repo clone at `/workspace/LibSaber`.
  - The game exe is at `/workspace/rea/sm2_retail.exe` (81 MB).
    - Use REA first via the persistent daemon (`tools/re/rea_daemon.py` + `tools/re/rea_call.py`, WORKFLOW.md "REA"); the old timeouts came from one-shot re-imports. Ghidra headless / rizin only as fallback.
  - The .sani spec now lives in code comments: src/LibSaber.SM2.Assets/Animation/EtnaCurves.cs and SaniReader.cs (the old `/workspace/rea/out/SANI_FORMAT.md` is gone). Python reference: py/sani.py.
  - Push: on the box `git bundle create /workspace/sm2.bundle <lastpushed>..HEAD`, CopyFromBox to C:\Users\Revaptor_FX\sm2.bundle, then in C:\Users\Revaptor_FX\ls_push: `git fetch ..\sm2.bundle HEAD; git merge --ff-only FETCH_HEAD; git push origin HEAD`.
- **Repo:** https://github.com/Baegovda/ForgeLens (private; old public fork https://github.com/Baegovda/LibSaber is kept as remote `libsaber-old`, its PR #1 was not migrated). Work is on branch `cursor/sm2-pck-extractor-4420`.

## Build and install
1. On the box: `dotnet publish` both projects to `/workspace/sm2cli` and `/workspace/sm2browser`.
2. Zip each output with Python.
3. Copy the zips to `C:\Users\Revaptor_FX\` on the PC.
4. On the PC: stop the browser process, then `Expand-Archive -Force` into `SM2Extractor`, then delete the zips.
- `dotnet test` hangs at package restore on the box.

## Current state (2026-10-26)
- **Meshes, skeletons, textures:** all work.
  - Exporting the 47 slots of the character cc_titus_assault gives 41 slots, because the cloth sim meshes are excluded.
- **Cloth sim meshes:** `*_SIM` parts are cloth simulation meshes and the game never renders them.
  - Evidence: `simMeshName` at 0x1435f1440, `renderMeshes`, and the nvcloth library.
  - They are excluded by default; `--include-cloth-sim` keeps them.
- **Texture channels:** the label `(a)` is a logical channel that the game remaps in the shader (`swizzleTexInShader` at 0x14233bf50).
  - For BC5 (DXN), Roughness is stored in g.
  - For BC4 (DXT5A), AO is stored in r.
  - The materials json records where each map is stored in a `stored` field.
- **Material script:** `src/**/import_sm2_materials.py`, which ships alongside each export.
  - Parents: M_SM2 (masked, two-sided), M_SM2_Translucent, M_SM2_ClothSimHidden.
  - Texture-to-mask links use the RGBA pin, because the RGB pin causes a compile error.
  - It assigns materials to both skeletal and static meshes.
- **Animation (.sani):** the class-A codec has been reverse-engineered.
  - Spec: `/workspace/rea/out/SANI_FORMAT.md`.
  - Key functions: reader 0x14222fe40, sampler FUN_142229bb0, root motion FUN_14222f1b0.
  - Rotation is stored as modified Rodrigues (three numbers that rebuild a rotation quaternion).
  - Idle, attack and run all work on cc_titus_assault_Skeleton.
- **Type tests:** Tyranid, weapon, prop and vehicle all import. Results are in `Desktop\SM2_TypeTest`.

## Open items (do them in order, one at a time)
1. **Tyranid color: resolved.** The grey look came from a capture taken before the textures had loaded; the diffuse textures themselves carry the real colors.
   - Commit ef29997 applies the material data's `albedo_tint` (stored as [A, R, G, B]) as the M_SM2 `AlbedoTint` parameter.
   - Not done yet: the second_spec (gold metallicTint) and anisotropic highlight layers.
2. **Transparent face slots.** Titus's head_01* slots are marked transparent. Confirm in the game what their alpha means before using the translucent parent on them.
3. **VFX.** There is no `.sfx` to Niagara conversion; files are extracted raw only.
4. **Rhino parts.** Check whether destroyed or variant parts are stacked on the hull.
5. **.sani gaps.** Done for SM2, see "sani gaps (2026-10-26)" and "sani gaps, part 2" below. v3 empty-name tracks bind by template animNmb, root rotation is applied, and unskinned tpl parts are world-baked and rigidly bound.
6. **Push and tests.** Push the local commits to the branch on Baegovda/ForgeLens and get the tests running.

## Rules
- Keep this file updated at the end of each stage: state, open items, commit hashes.
- Never touch the owner's original assets. All SM2 work lives under /Game/SM2_Work in the layout below ("UE layout (unified)").
- Before editing any of the owner's scripts or files, back them up as `.bakN`.

## 2026-10-26 update (Cursor Agent)
### Layout (all work lives here now; supersedes "never touch /Game/SM2")
- PC: `C:\Users\Revaptor_FX\Desktop\SM2_Work\{Exports\<asset>, Anim\Titus_Anim, Captures\{TypeTest,TypeTest_overview,Item2_head}, Raw\{TypeTest_raw,titus_td}, Scripts\}` (uemcp_call.py stays in home).
  - 10/29 disk cleanup: `Anim\Titus_Anim` was deleted (regenerable with `LibSaber.SM2Extractor export ... --anim`). See "SM2_Work cleanup (2026-10-29)" in "Update oneclick".
- UE: `/Game/SM2_Work/{Original/cc_titus_assault (old /Game/SM2), v4/cc_titus_assault, cc_titus_assault, cc_swarm_hormagaunt, refrt_imp_rhino_01, refrt_tyr_arch_column_01, wpn_bolter_heavy_01}`; 595 assets moved via rename_assets, 0 redirectors. (Superseded by "UE layout (unified)" below.)
- Capture: Scripts/ue_cap.py, two calls (capture, then export) - same-call export returns a stale RT.

### Item 2 RESOLVED - head_01* alpha
- Translucency is driven by the variant `glt.blendMode`, not `Transp(a)` in format_descr. Enum string table @0x144cc9190: opaque, blend, add, add_no_alpha, add_smooth_no_alpha, premul_a, mul, mul_inv (default opaque); "blendMode" @0x14355b708. akill @0x14364a800 (table 0x144ccc7e0), compared at 0x14173bb19/0x14173bb54.
- ch_titus_head.td default (skin) variant has no blendMode -> opaque; only `hair` variant is blend; ch_eye_shadow_01 = blend/glass.
- Exporter: MaterialMaps.BlendOf: non-opaque blendMode -> translucent, else AKill -> masked, else opaque; sidecar `blendMode`.
- Verified: head_01/head_01_7 -> M_SM2 (opaque), head_01_9/_2/_5/_10 -> M_SM2_Translucent, head_01_8 masked; Rhino glass still translucent; Hormagaunt head opaque; 41/41 slots imported.
- Visual: Captures/Item2_head/compare_iso.jpg (ue_cap.py head mode, `only,head` = every non-head slot gets /Game/SM2_Work/_Tools/M_Hide, masked opacity 0). Face, hair, eyes render complete in v4 and the new import; no holes in the skin. (M_SM2_ClothSimHidden still ghosts, do not use it for isolation.)

### Item 4 RESOLVED - Rhino stacked parts = LOD composite levels (not destroyed/variants)
- tpl object dump (rea/work/tpldump, objOBJ.Affixes): Object0001 = lod_compex_level+lod_composite_marker group; 2,3,4 = parts with `compound_skin_lod_index/index/0` + lod_composite_root_part_marker; group 5 -> parts 6,7,8 index 1; group 9 -> A,B index 2; C,D = markers with their own geometry (merged far proxies). All share one vertex pool, so the old export stacked 5 levels (52 slots, 219,544 tris) -> z-fighting.
- These affix names are not plaintext in sm2_retail.exe (searched; likely hashed), so evidence is the tpl data itself.
- Fix: IndexMeshVisibility.AddCompositeLods hides parts with index>0 and every marker sibling of an index-0 part (with descendants). refrt_imp_rhino_01 now 16 slots / 114,920 tris; _static.tpl uses the same scheme. Meshes without these affixes are unaffected.
- Verified: /Game/SM2_Work/refrt_imp_rhino_01_v2 (16 slots, 0 bad), Captures/Item4_rhino/compare.jpg (top = old stacked, bottom = fixed).
- Tools: tools/ue/ue_cap.py, ue_typetest.py (3rd arg = export folder name).

### Item 3 STEP 1 DONE - VFX .sfx -> Niagara-friendly param dump + textures
- Finding: every retail .sfx (2,381 under ssl/, 0 binary) is SSL text, not a binary particle blob. Node types from data and exe strings: CPU_EMITTER @0x14363bc80, GPU_EMITTER @0x14363bcf0, sfx_part @0x14363c148, sfx_group @0x143646930, nameTex @0x143644760, lwiGeom @0x143645a70, ageSpline @0x143644580, constantFlow @0x143644cf8, emitDir @0x1436445f0.
- Structure: root/sfx_group `list = {name = node}`, sfx_part `emitters2 = {...}`. Choice fields are tagged unions: `__type = "local__X"` -> only member X is live, the other stored alternatives are dead editor leftovers (e.g. emission.type local__constantFlow {x,y} = particles/s; explosion = burst). typeParams kind default = sprite, lwiGeom/geomPrimitive = mesh (nameGeomTpl). Splines flat [t,v,...] over normalized age, colors 0..255.
- Exporter: Effects/SslText.cs (parser), Effects/EffectSummary.cs (union resolve + per-emitter digest). `export --asset X.sfx` now writes X.sfx.json (resolved tree), X.niagara.json (sim CPU/GPU, renderer, blend, spawn, shape, cone, speeds, lifetime, size, color, gravity, emitScale/timeScale, textures, meshes, full), and exports all referenced textures (PNG/TGA/DDS) + resolves mesh tpl paths.
- Verified: 150 random .sfx parse with no failure; Exports/sfx_fx_iron_halo_25hp (10 emitters, 10 textures, 1 mesh), Exports/sfx_fx_helbrute_head_glow (5 emitters, 6 textures).
- Next for item 3: UE script that builds a Niagara system from niagara.json (sprite/mesh emitters with spawn rate, lifetime, size/color curves), and export the referenced geom tpls.

### Item 5 (part) RESOLVED - root-motion direction
- Data is consistent: run F4 root ends at source (0,0,+4.859) = +Z, the character front (Assumption: source +Z front). In the tpl the root node Object00C9 (#201) and ROOT (#202) have identity MatrixLT.
- UE evidence (AnimPose, WORLD): Interchange-imported skeleton ref has Object00C9/ROOT yaw 180; the legacy-importer anim had yaw 0, so the whole animated body faced -Y (eyes y=-54) and the root moved to y=-485.9. Import mismatch, not a decoder bug.
- Fix: tools/ue/ue_import_anim.py (also SM2_Work/Scripts) imports anim-only FBX with legacy FBX + import_rotation yaw 180. Result /Game/SM2_Work/Original/cc_titus_assault/Anim_yaw180/*: root yaw 180 = ref, eyes y=+54.5 (face +Y like the mesh), run root 0 -> y=+485.9. All 3 clips reimported. Old /Anim kept.
- Still open in item 5: F3/F7 second slot, version >= 3, F6 clips without .sani_data.

### Open
3 VFX (Niagara last; first browser playback as in-game), 5 .sani (done, see part 2), 6 tests (dotnet test hangs at restore; push now works via bundle).

## Bulk .sani validation (2026-10-26)
- Census of 23,227 .sani: v2 F6 sub2 20,244; v2 F2 sub2 1,223; v1 F6 787; v2 F6+F7 542; v2 F6 sub1 218; v1 F2 73; v3 F2+F3 61; v3 F6+F7 40; v2 F2+F3 21; v2 F2 sub1 18. F8 appears in 661 files.
- Decoder before: 21,488 OK / 1,739 FAIL. After: 23,126 OK / 101 FAIL (all v3).
- New: v1 accepted (FUN_14222fe40 defaults to sub 1), sub-1 raw-float codec (vtbl 0x1437e71c0; FUN_142228a70/228dd0/228e30/228ed0/227680), and F7 .sani_data = F2+F3 (F3 parsed, not applied).
- Remaining: v3 (101), F3 second slot not applied, v1 root motion (F4 without sub) not decoded, 1,405 clips with a rotation step over 90° (snap/hide?) not yet checked.
- UE numeric check, 5 clips (Titus, Hormagaunt, Sorcerer): no NaN, steps are plausible.
- ue_cap.py anim=@fraction: the capture scene keeps actors from earlier runs (overlapping meshes). Clean up the actors before trusting a visual comparison.

## GUI parity and animation export (2026-10-26, step 2)
- The browser and the extractor share one export path, AssetExporter.Export. That shared path covers skipping cloth sim meshes, BC5 B=0, stored channels, composite LOD hiding, and blendMode/albedoTint.
- The Export window adds:
  - an "Include *_SIM cloth" option
  - an Animations choice: None, Selected .sani rows, Preview clip, or Every matching clip
  - a Root motion checkbox
- New `Export/ExportPlan.Apply` turns the list selection into ExportSettings.Clips. Selected .sani files ride on the selected model as `{model}__{clip}.fbx` and are no longer written as standalone .anim.json.
- New `Animation/ClipResolver` is used by both the GUI and `--anim` (CLI).
- `ue_import_anim.py` is now an embedded resource and is written next to the anim FBX, the same way import_sm2_materials.py is.
- Verification: `tools/guiharness` runs ExportPlan.Apply + Export (the GUI code path) for cc_titus_assault.tpl with 2 clips. Its output is 281 files, all byte-identical (MD5) to two CLI `--anim` runs. In UE the clips import with the written ue_import_anim.py into /Game/SM2_Work/Anim_GUI, with no NaNs, and the title clip matches the CLI import (Anim_Bulk).
- ue_cap.py:
  - Actors are tagged SM2Cap, and a capture first clears tagged or nearby leftovers.
  - New "pose" mode (pose, then capture, then export, each a separate call) uses override_animation_data plus update-in-editor. The title clip now renders posed, where before it showed the bind pose.
  - Open: the go_to_jungle cinematic renders an empty frame at 0.5, so the framing or root-motion offset still needs checking.

## Second specular + anisotropic highlight (2026-10-26, open item 3 of the material list)
RE evidence (sm2_retail.exe, VA; rdata VA = file offset + 0x140001200):
- Strings: "second_spec" 0x1436daad8, "anisotropic_spec" 0x1436daac0, "metallicTint" 0x1436daab0, "anisotropy" 0x1436daa80, "extraAnisotropy" 0x1436daa90, "extraIntensity" 0x1436daaa0, "dirMap" 0x1437f92dc, "shift" 0x1437f92d4, "second_spec." 0x1437f9ea0, "anisotropic_spec." 0x1437f9ec0.
- glt field map FUN_1425c1d00: albedo_tint glt+0x30 (0x1425c2194), top-level metallicTint glt+0x40 (0x1425c21cb), second_spec sub-struct glt+0x94 (`add eax,0x94` 0x1425c282a), anisotropic_spec glt+0x108 (`add eax,0x108` 0x1425c2a5a).
- second_spec fields FUN_142576200: enabled +0, metallicTint +4, metalness.scale +0x14, .bias +0x18, roughness.scale +0x1c, .bias +0x20.
- anisotropic_spec fields FUN_142579ff0: enabled +0, angle +4, dirMap +8 (texture), anisotropy +0x18, intensity +0x1c, extra{anisotropy, intensity, shift} +0x20.
- Text reader FUN_1425b9500 (second_spec 0x1425ba12a, aniso 0x1425ba387): metallicTint read as a color and scaled (mulps 0x1425ba1fd). Legacy upgrade FUN_142621620 (~0x1426218b5) packs the tint as A<<24|R<<16|G<<8|B, so `.td` [A,R,G,B] = ARGB dword (same as albedo_tint).
- Permutation selection FUN_1423b7640: 0x1423b801f `aniso.enabled && anisotropy(glt+0x120) > 0.01` ([0x143c843ac] = 0.01) sets the aniso bit, and with dirMap bound (glt+0x110 != 0) also the dir-map bit (edi = 0x80000000); 0x1423b819a `second_spec.enabled (glt+0x94)` and no layer blend sets 0x200. Define names from FUN_1423b6260: GLT_USE_ANISO_SPEC 0x1437f88a0, GLT_USE_ANISO_SPEC_DIR_MAP 0x1437f88b8, GLT_USE_SECOND_SPEC 0x1437f89c0. Register/texture names FUN_1421b0bc0: PS_REG_GLT_ANISO_PARAMS 0x143729b28, GLT_TEX_ANISO_DIR 0x143729de0.
- Defaults (373/362 disabled full-dump blocks in the 18,713 retail .td): second_spec tint [0,255,255,255], metalness {1, 0}, roughness {0, 0.7}; aniso angle 90, anisotropy 0.5, intensity 1, extra {0.5, 1, 0.2}, dirMap "". enabled defaults false (the reader passes 0). Usage: second_spec enabled in 363 blocks / 244 files; aniso in 483 / 335; dirMap set in 77 (hair `_dm`, format "Diff(rgb)", usage "MD", neutral 127).
- Not in the exe: the HLSL itself. Lobe math below is an interpretation, not decoded.

Implemented:
- Meshes/SpecLayers.cs parses glt.second_spec / glt.anisotropic_spec of the variant (direct-child matching, so extra.* does not leak into top-level fields). MeshPart.SecondSpec/AnisoSpec (copied in Copy()). dirMap is a map with role `anisoDir`, exported linear.
- Sidecar (AssetExporter.WriteMaterialSidecar, shared by browser and CLI): `secondSpec {metallicTint RGBA, metalness{scale,bias}, roughness{scale,bias}}`, `anisotropicSpec {angle, anisotropy, intensity, dirMap, extra{anisotropy,intensity,shift}}`, null when disabled.
- import_sm2_materials.py: static switches UseSecondSpec, UseAnisoSpec, UseAnisoDirMap (all off = previous graph result). Second lobe: m2/r2 = saturate(packed * scale + bias), single lobe blended toward it by SecondSpecWeight (0.5) * m2, recolored by SecondSpecTint only when tint A > 0. Aniso: Anisotropy pin = AnisoAnisotropy, Tangent = (cos, sin, 0) of AnisoAngle or the AnisoDir RG*2-1, Specular = saturate(0.5 * AnisoIntensity). extra{} (shifted hair lobe) stays in the sidecar only.
- Checked: sidecar harness through WriteMaterialSidecar on real .td (hormagaunt torso, titus torso/hair/legs, calgar hair with dirMap) and the UE script against a recording mock of `unreal` (all pins/links non-null, per-instance switches and values). No game archives on the box, so no full export; `dotnet test` still hangs at restore (SpecLayersTests added, not run).
- To verify in UE: re-export a model (e.g. cc_swarm_hormagaunt, cc_titus_assault) with this build, run import_sm2_materials.py, check M_SM2 compiles (Project Settings > Rendering > Anisotropic Materials on), MI_* of hormagaunt torso has UseSecondSpec/UseAnisoSpec on, gold sheen on chitin, hair highlight stretched along strands; tune SecondSpecWeight.

## Step 3 PC verification (2026-10-26, second spec + aniso in UE)
Build/install: CLI + browser published from c493d33 (= origin 19c7eb0; win-x64 self-contained per pubxml) and installed to `C:\Users\Revaptor_FX\SM2Extractor` (old folder backed up as `SM2Extractor.bak1`, 1.19 GB).
Exports (new CLI, `Desktop\SM2_Work\Exports\step3_<asset>\tpl\<asset>.tpl\`, log `step3_<asset>.log` beside):
- cc_swarm_hormagaunt: 26 slots, secondSpec 8 / aniso 8 / dirMap 0 (head_01..04, hormagaunt_mesh torso, _2 legs, _6, blades_01). tint (0.988, 0.8, 0.522, A 1), metalness {0, 0.2}, roughness {0, 0.49}; aniso angle 90, anisotropy 0.4, intensity 3.
- cc_titus_assault: 41 slots, only head_01_2 (translucent) / head_01_8 = ch_titus_hair. tint (0.749, 0.827, 1, A 0), metalness {1, 0}, roughness {0, 0.67}; aniso 70 / 0.65 / 1. Armor has no second_spec/aniso in this tpl, so it keeps the old look.
- cc_calgar: 44 slots, secondSpec 7 / aniso 7 / dirMap 3 (calgar_head_01, _8, _10 -> `ch_calgar_head_hair_01_dm`, linear, present); cloak_REND_01/02 aniso 0.2 / intensity 1.5; head_01_2 angle 120 / intensity 20; head_01_7 angle 45 / intensity 10.
UE (5.7.4, Substrate on, `r.AnisotropicMaterials` = 1 already, no restart needed):
- `tools/ue/ue_step3.py <asset> import|verify` imports the FBX (skeletal, no materials) to `/Game/SM2_Work/step3/<asset>` and runs the exported import_sm2_materials.py; all instances assigned (26/26, 41/41, 44/44).
- verify: per-slot UseSecondSpec / UseAnisoSpec / UseAnisoDirMap match the sidecar for every slot, MISMATCH [] on all three. dirMap slots get AnisoDir = ch_calgar_head_hair_01_dm, the rest the default texture. Tint params are stored linear (gamma 2.2 of the sidecar sRGB, e.g. 0.8 -> 0.612).
- M_SM2 and M_SM2_Translucent compile: no Error lines in UE_VFX_57.log over the import window (792 shader jobs, 100% done); stats M_SM2 vs 179 / ps 161 / 5 samplers, M_SM2_Translucent vs 398 / ps 1214 / 9 samplers. Only warnings: FBX "no smoothing group" (known).
Captures (`Desktop\SM2_Work\Captures\Step3\`):
- Plain `ue_cap.py ... final` renders near-black at the capture spot (no lights there), so `tools/ue/ue_cap_lit.py` was added (copy of ue_cap.py plus three SM2Cap-tagged radius-limited point lights removed by `export`, manual exposure, `intensity=` / `ev=` / `off=<folder>` args). Unlit runs stay in `Step3\<name>`, lit ones in `Step3\lit\<name>` (intensity=80).
- True A/B: `tools/ue/ue_step3_ab.py <asset>` duplicates each instance with a layer on into `/Game/SM2_Work/step3/_ab_off/<asset>/<MI>_off` with all three switches off; `ue_cap_lit.py ... off=<that folder>` swaps them in on the capture actor only.
- Mean |diff| switches off vs on (0..255, front/side/back): hormagaunt 0.89 / 0.28 / 0.91 (3.6% / 1.2% / 3.9% pixels > 10), visible as extra sheen on chitin and legs. Titus full body 0 (hair hidden under the helmet). Titus head 0.20 / 0 / 0.19. Calgar body 0.70 / 1.40 / 0.44 (cloak). Calgar head 2.25 / 0 / 1.57: hair highlight clearly brighter and streaked.
- Old-import "before" (`/Game/SM2_Work/cc_*`) vs step3 also differs on Titus armor (2-3 mean), which comes from older importer changes, not the new layers; use the `_AB_switches` sets to judge this step.
- Head mode: the "side" camera sits inside the pauldron for Titus/Calgar and sees nothing; front/back are usable.
Open: tune SecondSpecWeight (0.5) / aniso Specular against game references; the extra{} hair lobe is still sidecar-only.

## sani gaps (2026-10-26)
All five gaps from item 5, done REA-first (Ghidra project /workspace/rea/ghidra_proj2, decompiles in /workspace/rea/dec).
- PC backup before install: C:\Users\Revaptor_FX\SM2Extractor.bak2 (old build). Raw file restored: Raw\sani_all\...\pve_relay_boss_intro_shot_01_char_vortex_beast1_vortex_beast.sani was overwritten by an old sanival call (TSV header, 92 bytes, kept as .sani.bak1) and restored from Raw\sani_all.zip (60,885 bytes). sanival now refuses an unknown flag / non-folder / non-.tsv output.
- Loader FUN_14222fe40 accepts version 1..3; F2/F6 = slot 0 (clip+0x60), F3/F7 = slot 1 (clip+0x68), F4 = root (FUN_142234f10); codec factory FUN_142234c00 reads the sub u16 only when version > 1.
1. **v3: FIXED.** vtbl 0x1437e74e0. Header FUN_14222d0d0 (8 counts nT nR nS nF cT cR cS cF), carve FUN_14222d550, planar T,R,S,F frames FUN_14222d6b0, frame size FUN_14222d7c0 = nT*4+nR*6+nS*6+nF*2 (sampler c810 is the reference). Name table FUN_1422336c0: bones (+24) then scalars (+28), interned by FUN_141c3fa90 (string-pool id; empty string -> id 0). Scalars go to AnimationClip.Scalars. v3/F7 .sani_data = F2 block + F3 block. Raw v3 sub-1 codec (vtbl 0x1437e72d0) not decoded (no retail file uses it).
   - Empty names: FIXED in part 2 (bind by template animNmb rank).
2. **F3/F7 second slot: FIXED (as additive flag).** Stats dumper FUN_141060280 prints HasAdditive (string 0x1435ef808) = clip+0x68 != 0. Numerically slot1 R(f) = R0(f)*inv(R0(0)) (max err 0.00x deg), T(f) = T0(f)-T0(0), scales 1 (cultist_stand_shoot, cadian_talk, door_mushroom close). Only slot 0 is exported; AnimationClip.HasAdditive is set.
3. **F6 without .sani_data: NO-OP.** Census: 0 such files in retail; preview fallback kept.
4. **v1 root motion: DECODED, partial in export.** F4 sub 1 vtbl 0x1437e77b8 ([1] FUN_14222ef50 exact, [2] FUN_14222ef80 lerp T + FUN_1427ae2c0 blend R, [3] FUN_14222f060 carve, [7] FUN_14222f0d0 load); size FUN_14222f140: float4 T[frames] then float4 R[frames] (w,x,y,z). Sub 2 root = vtbl 0x1437e7800. All 860 v1 files carry F4; 2 are non-identity (vortex_beast boss_intro / outro): T is all zero, R is non-identity (e.g. boss_intro frame 250 = (w .785, x -.366, y .354, z -.354)). ClipPose/FbxWriter/GltfWriter apply only root translation, so the FBX is byte-identical to the bak2 export (MD5 61234DEB...). Root rotation: APPLIED in part 2.
5. **Rotation steps > 90 deg: NOT A BUG.** FUN_1427ae2c0 is slerp-approx with hemisphere flip (dot<0 -> negate), same shortest path as UE; codes are unsigned code*scale+min (no wrap). Box sample: 1548 snaps vs 27 one-frame spikes, mostly attach/hand/prop/IK bones. PC: 1967 files have a step > 90 deg. No code change.
- Validation: PC Raw\sani_validation3.tsv 23,227 OK / 0 FAIL (v1 860, v2 22,266, v3 101); additive 664, scale tracks 101. Box /workspace/sani/val3.tsv 449/449 OK; v3 hologram_planet matches py/sani.py.
- Tests: test/LibSaber.SM2.Assets.Tests/SaniGapTests.cs (4 synthetic tests; pass in a shim runner on the box, real `dotnet test` still hangs at restore there).
- Tools: tools/sanival (`sanival <folder> <out.tsv>`, `--dump <file> <bone|index>`, `--root <file> [step]`), tools/ue/ue_sani_fix.py (import mesh + before/after anim; `check` prints AnimPose WORLD bone poses at 0/25/50/75/100%).
- UE (/Game/SM2_Work/sani_fix): door_mushroom (mesh + after/door_mushroom__close, 3.267 s; before = no clip at all) - flower_base_bind_01 moves (-500,-310,-265) -> (-387,-274,207), flower_base_bind_04 pitch -74 -> -18, no NaN. cc_vortex_beast before/after boss_intro (19 s) identical poses (see gap 4). Captures Captures\sani_fix\door_mushroom_{before_bind,after_f000,f050,f100} (box /workspace/sani/captures): camera ends up inside the door's hull, so they are not useful visually; the AnimPose numbers are the evidence.

## sani gaps, part 2 (2026-10-26)
CLI: box /workspace/pub3 -> PC C:\Users\Revaptor_FX\SM2Extractor3\LibSaber.SM2Extractor.exe. SM2Extractor2 = a04e0bc, and the older SM2Extractor is kept. Always pass the full `--anim tpl/<n>.tpl/animations/<clip>.sani`, because a short name matches by substring and exports many clips.
1. **v3 empty-name tracks: FIXED (a04e0bc).**
   - Exe: the `CONFIG.useSaniAnimationsInTpl` path FUN_141db55f0 -> FUN_142232230 takes the tpl objects with animNmb != -1, sorts them by animNmb (FUN_14127d230) and writes clip+0x50[i] = object id. No name is compared.
   - Code: Export/ClipBinding.cs and Bone.AnimIndex. When the counts do not match, it falls back to names. An explicit bone index is kept.
   - Logs: door_mushroom close 130/130 (was 128), hologram_planet anim 47/47 (was 0).
2. **Unskinned tpl parts: FIXED (this series).**
   - TemplateMeshReader baked unskinned parts with the node's LOCAL MatrixModel. ReadSkeleton proves MatrixModel is local (inverse binds pair only as world = local * parentWorld).
   - hprobe: hologram_planet has 63 of 65 geometry nodes with world != local, up to 8.72 m. Rhino has 0 of 10, so the change is a no-op there.
   - The parts are now baked with the composed world matrix, and each one gets a rigid skin weight (J0 = node, W0 = 1), so node tracks move it. Exporter note: "N unskinned parts are bound rigidly...".
   - UE: the old mesh shows one stray outer sphere (bind bounds sphere 52 m). The new one shows the planet with its rings (17 m), and the rings move between t = 0, 0.25 and 0.5 (9.5 to 11 % of pixels change).
3. **Root rotation (F4 R): APPLIED (a04e0bc).**
   - Exe: FUN_14222fe40 case 3 samples the root (FUN_142234f10, vtbl+8) at frame 0 and frame N-1. It uses FUN_1427aa8a0 (q inverse), FUN_1427ace20 (R^-1 v) and FUN_1427ad8b0 (q mul): root+8 = R0^-1 (T_last - T0), and root+0x14 = R0^-1 R_last. The result goes to clip+0x70. So p' = R p + T, which is `local * R * T` (row vectors) for parentless bones.
   - Code: ClipPose, FbxWriter (R key times) and GltfWriter (bakes T and R on parentless bones). The identity-R path is byte-identical.
   - Census (PC Raw\sani_root_census.tsv): 23,227 clips, 418 with R. 416 of them are turn clips whose angle matches the name. The other 2 are cinematics, pve_relay boss_intro 89.6 deg and outro 50 deg, both with T = 0.
   - Root motion is per controller: `MorphemeController.useAnimRootMotion` (+0x41, string 0x1435da538, registered at 0x140e4e9be in FUN_140e4b2f0). Cinematics presumably play without it: boss_intro ends tilted at z 805 with R, but in the idle stance without it. The exporter therefore warns for clips with R and suggests `--no-root-motion` for those 2 shots. There is no path heuristic, because the exe gives no proof of one.
   - vortex_beast attack_melee_180 (v2): CENTRE yaw f0/fN was 89/89 and is now 89/-91 (a true 180). The largest one-frame step drops from 79.1 cm to 24.9 cm.
   - No regression: the titus run clip FBX and every mesh FBX are byte-identical.
- UE additions:
  - Props/hologram_planet (rigid-bound mesh)
  - Animations/hologram_planet/hologram_planet__anim
  - Tests/sani_before/hologram_planet/localbake/* (old mesh + anim)
  - Animations/door_mushroom/door_mushroom__close (130/130; the old 128/130 clip is in Tests/sani_before/door_mushroom)
  - Animations/cc_vortex_beast/{boss_intro, attack_melee_180} (before copies in Tests/sani_before/cc_vortex_beast)
- Captures: tools/ue/ue_cap2.py. Each capture takes three calls (pose, capture, export). The centre is the posed bones, and the radius is max(bone spread, mesh bounds sphere). The camera sits at R/sin(fov/2), so it is outside the mesh. cdt/_glr/rb_/_lod/nav_silhouette slots are hidden. Output: PC Desktop\SM2_Work\Captures2 and box /workspace/sani/captures2 (12 sets x front/side/three_quarter).
- Known limits:
  - Affix-hidden volumes cannot be told apart by UE slot name (pass hide=).
  - There is no unit test for the world bake yet (needs a tpl fixture).
  - A 90 deg turn clip was not imported. The census and the 180 clip cover the rotation sign.

## UE layout (unified, 2026-10-26)
Everything our work created lives in `/Game/SM2_Work`. An inventory of /Game (255,440 assets) found no SM2 work outside it: no /Game/SM2, /Game/SM2_Test, /Game/SM2_vN or scratch/_calib folders remain. The `M_SM2` / `M_SM2_Translucent` / `M_SM2_ClothSimHidden` masters are written per asset by import_sm2_materials.py, so they stay next to that asset's instances. Different generations differ, so they are not merged.
```
/Game/SM2_Work/
  Materials/              M_Hide (capture helper, was _Tools)
  Original/cc_titus_assault/   owner's original import, untouched; /Game/Work/02/Lightning_01 references its mesh and Anim/run_fwd
  Characters/<asset>/     current import (mesh, Skeleton, masters, MIs, Textures/): cc_titus_assault, cc_swarm_hormagaunt, cc_calgar (was step3/*), cc_sorcerer_exalted, cc_vortex_beast
  Props/<asset>/          refrt_imp_rhino_01 (was refrt_imp_rhino_01_v2, fixed LOD), refrt_tyr_arch_column_01, wpn_bolter_heavy_01, door_mushroom, hologram_planet
  Animations/<asset>/     clips: cc_titus_assault/{Bulk,GUI,yaw180} (skeleton = Original/cc_titus_assault), cc_sorcerer_exalted/Bulk,
                          cc_swarm_hormagaunt/Bulk (skeleton = Tests/pre_step3/cc_swarm_hormagaunt), cc_vortex_beast, door_mushroom, hologram_planet (sani "after")
  Tests/<stage>/<asset>/  step3_ab_off (was step3/_ab_off), sani_before, pre_step3 (old root cc_titus_assault, cc_swarm_hormagaunt),
                          v4 (cc_titus_assault), rhino_stacked (old refrt_imp_rhino_01), typetest (future ue_typetest.py output)
```
- How it was moved: `tools/ue/ue_reorg_layout.py plan|apply`. It moved 827 assets with AssetTools.rename_assets, left 0 redirectors and saved everything. Afterwards the 41 old folders that were empty on disk were removed, and nothing was deleted. A disk backup from before the move is at `Desktop\SM2_Work\Backup\Content_SM2_Work.bak1` (2.7 GB).
- How it was verified: `tools/ue/ue_verify_layout.py`. It reports 963 assets, 0 redirectors and 0 missing /Game dependencies. Every mesh's MIs and parents resolve inside its own folder (Titus 41 slots, Hormagaunt 26, Calgar 44, Rhino 16, Column 11, Bolter 26), and every anim resolves its skeleton. All the masters have shader stats, i.e. they compile: M_SM2 161 ps instructions, Translucent 1214, ClothSimHidden 141.
- Scripts now targeting this layout (repo `tools/ue` and `Desktop\SM2_Work\Scripts`):
  - ue_step3.py → Characters|Props/<asset>.
  - ue_step3_ab.py → Tests/step3_ab_off/<asset>.
  - ue_sani_fix.py: mesh → Characters|Props/<asset>, "after" → Animations/<asset>, "before" → Tests/sani_before/<asset>. Optional export tags are args 4 and 5.
  - ue_typetest.py → Tests/typetest/<dir>.
  - ue_cap*.py → Materials/M_Hide.
  - ue_import_anim.py / ue_bulk_anim.py take the destination as an argument: pass Animations/<asset>/<set>.
  - The product default in import_sm2_materials.py stays /Game/SM2/<mesh> for end users, and our tools pass the folder explicitly.

## Material finishing (2026-10-26, SM2 only, Substrate)
Evidence (sm2_retail.exe + decoded PS bytecode; shaders in box `/workspace/mat/glsl/`):
- 1a Defines: GLT_USE_ANISO_SPEC / _DIR_MAP / GLT_USE_SECOND_SPEC = permutation bits 30 / 31 / 41 (FUN_1423b6260); gates in FUN_1423b7640 (aniso > 0.01, dirMap bound, second_spec enabled and no layer blend). SpecLayers.cs matches; the exporter does not check "no layer blend" (no effect on characters).
- 1b Second spec (PS 03840_0 non-aniso, 00004_1 aniso; cb6 OBJ_MTLBLEND_LAYER_CONST layer0 rows 6..21): m2 = saturate(row18.w*packedMetal + row7.w); tint = row18.xyz (alpha unused); F0_2 = flag32 (row21.x) ? tint*specRGB*m2 : lerp(0.04, tint*albedoLinear, m2); r2 = max(saturate(row19.x*packedRough + row19.y), 0.02); F = (1-Fc)*F0_2 + saturate(50*F0_2.g)*Fc. Added to base spec at weight 1, direct lights only. Non-aniso second lobe has no visibility term (D*F*NdotL, ~4x a standard lobe); the aniso one keeps 1/(4pi) (standard level).
- Aniso: packer FUN_1423ba150 (CB_STATIC_GLT): row5 = (cos, sin, -sin, cos) of clamp(angle,0,180)*deg2rad; row6 = (intensity, extra.intensity, k, k_extra) with k = 1 - clamp(a, 0, 0.95) (0x1423ba35c..3ec); row4.y = extra.shift (0x1423ba3f4). Shader: alphaT = (r*k)^2, alphaB = r^2, D = min(.., 128) * intensity. In aniso+second shaders the second lobe IS extra{} (tangent normalize(T + shift*N), k_extra, extra.intensity); without second spec extra{} is unused.
- 3 SecondSpecWeight does not exist in the game; removed. Gain fixed from the visibility difference: SecondSpecGain 4 (non-aniso) / 1 (aniso). Aniso intensity = raw glt value.
- Base F0 in game: lerp(0.16*reflectance^2, albedo, metal), reflectance = row8.w (source not traced; UE keeps 0.04).
- Unresolved: GBuffer PS 00165_2 reads rows 18/19 as a fresnel tint (layout may be per permutation); whether the deferred path carries the second lobe.
import_sm2_materials.py (graph, M_SM2 and M_SM2_Translucent):
- Base = SubstrateSlabBSDF: DiffuseAlbedo = albedo*(1-metal), F0 = lerp(0.04, albedo, metal) (x AnisoIntensity, saturated, when aniso), F90 1. Aniso remap A = (k^2-1)/(k^2+1), roughness' = r*sqrt((k^2+1)/2) (matches both game axes exactly with Substrate alphaT = a(1+A), alphaB = a(1-A)). Tangent = (cos, sin) of AnisoAngle or AnisoDir (UseAnisoDirMap).
- Second = Slab with albedo 0, F0 = saturate(F0_2 * SecondSpecGain * AnisoExtraIntensity), F90 = saturate(50*F0_2.g)*gain, r2 remapped by AnisoExtraAnisotropy, frame rotated by AnisoExtraShift (T2 = normalize(T + s*N), N2 = normalize(N - s*T)). SubstrateAdd(base, second) behind static switch UseSecondSpec -> Front Material. Translucent: SubstrateWeight(front, opacity). AO / WPO stay on root pins; legacy BaseColor/Metallic/... pins no longer linked.
- Params: AnisoAngle/Intensity/Anisotropy, AnisoExtraAnisotropy (0.5) / ExtraIntensity (1) / ExtraShift (0.2), SecondSpecGain, SecondSpecTint (linear), SecondMetalness/RoughnessScale/Bias. apply_spec_layers no longer clamps anisotropy.
UE verify (`tools/ue/ue_mat_apply.py <asset> apply|verify`, PC `Desktop\SM2_Work\Scripts`, into `/Game/SM2_Work/Characters/<asset>`):
- hormagaunt 26/26 (secondSpec 8), calgar 44/44 (7), titus 41/41 (2); switches/params/tint match the sidecar, MISMATCH [] on all. No Error lines in the log. Stats: M_SM2 vs 179 / ps 273 / 5 samplers (was 161); Translucent vs 398 / ps 1150 / 9.
- Backup before apply: `Desktop\SM2_Work\Backup\Characters_mat.bak1\` (robocopy of the three Characters folders).
- Captures (ue_cap_lit.py intensity=80): PC `Desktop\SM2_Work\Captures\Mat\{before,after}\<asset>_{body|head}\`; box `/workspace/mat/captures/compare_*.jpg` (top before, bottom after). Mean |diff| 0..255 front/side/back: hormagaunt body 8.2/2.1/7.5, calgar body 4.8/8.7/4.1, calgar head 4.3/6.5/3.6, titus body 2.9/1.8/2.2, titus head 1.8/3.8/2.3. The first after set was captured mid shader compile (grey checker); retaken after compile.
Caveats / next:
- Calgar hair (extra.intensity 20, saturated F0 -> 1) looks blotchy and too bright: UE also lights the second slab with IBL (game: direct only), and intensity can only go through F0 <= 1. Candidate fix: scale the second slab by a Substrate coverage or drop its IBL via a specular-only light-function trick; needs an in-game reference.
- Shift is a frame rotation approximation; DIR_MAP decode (RG*2-1) not verified against the shader.

## Calgar hair fix (2026-10-26, mat2)

Reference: in-game close-up (short grey buzz cut, matte, almost no aniso streak).
Compare image: box `/workspace/mat/captures/calgar_hair_ref_compare.jpg` (reference | before c1c4de0 | after).

Exe evidence (PS 00004_1 / 00042_0 GLSL):
- Base spec layer (`glt` level) metal/rough scale+bias, metallicTint, reflectance (F0 = lerp(0.16*refl^2, albedo, metal)):
  now parsed (`SpecLayers.Base()` -> `BaseLayer`), written to the sidecar as `baseLayer` and applied by the importer.
- Direct-light AO: every local light (diffuse + both spec lobes) is scaled by `saturate(2*AO - 1 + |NdotL|)` (00004_1 ~l.4167),
  AO = spec.B mixed by per-instance weights (`_66[_339*108+36..39]`; CPU writer not located).
- Dir-map tangent decode (00042_0 l.487-489, 2697-2716) matches the importer (no angle rotation when dirMap is present).

APPROXIMATION (clearly labelled in `import_sm2_materials.py`, `DirectAo` block):
- UE/Substrate has no per-light hook, so `occ` = NdotL-weighted mean of the game's factor over the hemisphere
  (closed form, AO>=0.5: 1-u^3/3, u=2-2AO; AO<0.5: 2/3-d+d^3/3, d=1-2AO) multiplies base diffuse, base F0/F90 and second-slab F0/F90.
- `DirectAoStrength` = 1 only on slots with an aniso dirMap (hair); 0 elsewhere, so other materials are unchanged.

Result: fewer white blotches, darker/more matte grey. Remaining differences:
- the UE aniso path is still brighter and more streaky than the game (A/B: `tools/ue/ue_hair_ab.py` modes nosecond/noaniso/nospec; nospec is the most matte);
- the capture rig has almost no IBL while the in-game shot is ambient-dominated;
- the hair cards read more voluminous than the in-game buzz cut;
- UE uses Smith V on both lobes (the game has no V on the second lobe); no per-slab IBL exclusion in Substrate;
- the rows 8/9 CPU packer is not located; the MetallicTint lerp is done in linear space;
- other characters (hormagaunt, titus) receive the baseLayer bias too: re-check them.

PC: content backup `Desktop\SM2_Work\Backup\Characters_mat2.bak1\cc_calgar`; scripts `.bak1/.bak2`; sidecar `.json.bak1`.
Apply: `ue_mat_apply.py cc_calgar apply`, wait ~170 s, `ue_tmp_stream.py` (FullyLoadUsedTextures), then `ue_cap_lit.py capture ... view=ref`.
Verify: `ue_mat_apply.py cc_calgar verify` -> `SLOTS 44 MIs 44 secondSpec 7 MISMATCH []`.

## Material recheck after baseLayer / DirectAo (2026-10-26, mat3)
Re-export with the current CLI (`C:\Users\Revaptor_FX\SM2Extractor6`, contains e6cadfa) into `Desktop\SM2_Work\Exports\mat3_<asset>\` (log `mat3_<asset>.log`; the step3_ exports are untouched): `LibSaber.SM2Extractor.exe export "D:\Steam\steamapps\common\Space Marine 2\client_pc" --asset tpl/<asset>.tpl --format fbx-unreal --output <dir>`. Exported import_sm2_materials.py is byte-identical to the repo one and to `Scripts\import_sm2_materials_mat.py`.
- Sidecar diff vs step3: only the new `baseLayer` key; maps unchanged. Hormagaunt: all 26 slots identity (scale 1 / bias 0, tint white, reflectance 0.5), so no expected change. Titus: 12 of 41 non-identity: roughness bias +0.12 (purity seals legs_01_2 / sholder_armor_R_01, eye head_01_3), +0.05 (helmet_01, helmet_01_3, jetpack_2), +0.02 (face head_01); hair head_01_2 / _8 metalness bias 0.15 + roughness bias 0.2 + metallicTint (0.898, 0.792, 0.675); jetpack_3 (sc_grey_90) roughness constant 1; eyelashes head_01_5 metal 0.96 / rough 1 constant; eye shadow head_01_10 metal 0 / rough 1 constant.
- UE: `ue_mat_apply.py <asset> apply mat3_` (new optional 3rd arg = export folder prefix, default `step3_`) into `/Game/SM2_Work/Characters/<asset>` (pre-apply copy: `Desktop\SM2_Work\Backup\Characters_mat3.bak1`). verify: hormagaunt 26/26, secondSpec 8, MISMATCH []; titus 41/41, secondSpec 2, MISMATCH [], the 12 BASE slots above print with the sidecar values. M_SM2 vs 179 / ps 298 / 5 samplers, M_SM2_Translucent vs 398 / ps 1174 / 9.
- Captures `Captures\Mat\mat3\<asset>_{body|head}` (ue_cap_lit.py intensity=80; head mode needs `head only,head` as args 5/6, otherwise arg 6 is read as the keep list and the helmet stays). The first set right after apply was grey checker (shaders still compiling) even with no ShaderCompileWorker left; retaken ~3 min later. Box: `/workspace/mat/captures/compare_mat3_*.jpg` (rows: mat1 after / mat3 / |diff| x4).
- Mean |diff| mat1-after vs mat3 (front/side/back): hormagaunt body 0.57 / 0.18 / 0.44 (slightly brighter chitin highlights, from the master graph changes since mat1, not baseLayer); titus body 0.13 / 0.06 / 0.13 (helmet/seals rougher); titus head 1.54 / 1.87 / 1.56, all on the hair: a little darker and warmer (albedo *= lerp(1, metallicTint, 0.15), rougher), which is what the PS 00004_1 cb6 rows 8/9 formula gives. No regression found, so no fix in this pass.
- Not checked: there is no Hormagaunt or Titus in-game reference on the box (only `/workspace/mat/ref/calgar_ingame_head.png`), so "vs in-game" is still open for these two. Open REA question: eyelashes (head_01_5, translucent) get metal 0.96 constant; whether the translucent hair/lash shader reads glt.metalness the same way as glt PS 00004_1 was not traced.

## Animation → UE: survey + fix 1/2 (2026-10-26, anim_fix12)

Survey report: box `/workspace/anim_survey/REPORT.md` (12 clips, 0 NaN, frame-drop fixed in d52abd2).
Before/after sheets and UE check output: box `/workspace/anim_survey/fix12/`. UE: `/Game/SM2_Work/Tests/anim_survey/fix12/<asset>/`.

1. **Import yaw: auto (no hardcoded 180).** `sm2_anim_import()` (same block in `tools/ue/ue_import_anim.py` (embedded in the exe), `ue_anim_survey.py`, `ue_sani_fix.py`)
   imports at yaw 0, compares skeleton bone 0 at t=0 with the skeleton ref pose (component space) and reimports with the
   90°-multiple difference if any. Fresh Interchange skeletons (old `_Skeleton` Null and new ROOT-rooted) → 0. The old
   `Original/cc_titus_assault` skeleton (needed 180) was not re-tested (read-only), the auto rule should pick 180 there.
2. **Root motion on bone 0 = ROOT.** Exe: the loader FUN_14222fe40 keeps the 0xF4 root track apart from the bone slots
   (slot 0 clip+0x60, slot 1 clip+0x68, root clip+0x70; case 3 builds root+8 = R0^-1(T_last-T0), root+0x14 = R0^-1 R_last via
   FUN_1427aa8a0/FUN_1427ad8b0; sampler FUN_14222f1b0, codec FUN_142234f10). It is an entity transform, not a skeleton
   bone, and the tpl root object (Object00C9 etc.) and ROOT have identity MatrixLT. Export choice: ROOT carries it.
   - `UnrealRoot.Promote` (fbx-unreal default; `--fbx-armature-null` = old hierarchy): ROOT becomes the only root, the tpl root object
     becomes its child (same world bind, bone order and skin indices unchanged), and no `{mesh}_Skeleton` Null is written.
     ClipPose applies 0xF4 to parentless bones, so the motion lands on ROOT = UE bone 0.
   - The import helper sets EnableRootMotion + RootMotionRootLock ANIM_FIRST_FRAME (`noroot` to skip).
   - glTF is unchanged: it has no Null, so its root bone (tpl root object) already carries the motion.
   - Existing skeletons from older exports have a different hierarchy, so re-export and re-import the mesh (new skeleton).
   - Verified (UE 5.7.4, `extract_root_track_transform`): Titus run ROOT 485.9 cm / 0.933 s, in place when extracted, facing +Y;
     Hormagaunt run 506.7 cm / 0.633 s (= 800 cm/s; the old 480 / 0.600 lost a frame); Vortex slam 488.2 cm + 180° turn.
     Before: bone 0 `_Skeleton` 0 cm, root motion off.
- Build: `C:\Users\Revaptor_FX\SM2Extractor5\LibSaber.SM2Extractor.exe`. Tests: SM2.Assets 52/52, SM2Extractor 35/35.
- Open: cinematic rebase (level-space c06/c10), `--anim-only` / multi-clip export, additive clip type, Mannequin RTG (see REPORT.md).

## SM2Browser v2 preview (2026-10-26, browser_v2)
Installed: `C:\Users\Revaptor_FX\SM2Browser_v2\LibSaber.SM2Browser.exe` (single-file win-x64; older builds kept as `.bakN`).
Bench: `LibSaber.SM2Browser.exe --bench <game> tpl/cc_titus.tpl <outdir> [frames]` renders old (Bench/MeshPreviewLegacy.cs) vs new
renderer from the same cameras and writes bench.txt + PNGs; `SM2_BENCH_TESS=1` adds the tessellation string scan.

Root causes in the old MeshPreview (all CPU, Titus LOD0 = 422,499 verts / 299,607 tris / 38 parts / 525 bones):
- Stutter: single-threaded raster of all 300k tris every frame via IReadOnlyList, new WriteableBitmap each frame, 33 ms timer
  advanced by a fixed step. Old: 221 ms/frame at 1100x1000 (4.5 fps), 263 ms at 1600x1200 (32-core PC).
- Grain/sand: nearest-neighbour texel fetch, no mips, affine UVs, no AA, rendered at DIPs capped 1100x1400 then stretched.
- Faceting: Fill/FillTextured shaded one colour per triangle; FillLit used normals[a] for the whole triangle; skinned normals
  were never transformed.
- Not tessellation: sm2_retail.exe parses `tessellation_parameters` in fcn.1421df540 (string 0x1436ea3f8, xref 0x1421df5c9;
  smooth = `pn_triangles` 0x1436ea3e0 xref 0x1421df6b3 | `phong` 0x1436ea3f0; displace `heightmap` 0x14364a858; `maxLevel`
  0x1436ea428; also `TESSELLATION: ` 0x1437eb300, `isAdaptiveSubdivision` 0x1437f5588, `USE_ADAPTIVE_SUBDIVISION` 0x1437f5318,
  `displacementMapping` 0x143634590). Scan of all 37,403 .td in the catalog: 0 contain tessellation/pn_triangles/phong/
  isAdaptiveSubdivision/displacementMapping (700 contain "heightmap" = hdetm detail maps). Titus tpl files: none (only
  "heightmap" in cc_titus.tpl). So Titus pauldrons are plain meshes (pre_order_shoulder_01 24,222 + 12,207 tris).
- FBX/glTF already write per-vertex normals (GltfWriter NORMAL accessor; FbxWriter LayerElementNormal ByPolygonVertex, from
  VertexSerializer.DecompressNormalFromInt16) — export was never faceted.

New renderer (src/LibSaber.SM2.Assets/Meshes/MeshPreview.cs): parallel skin (positions+normals), projection, chunked binning
into bands with sub-pixel cull, parallel band raster; perspective-correct barycentrics; interpolated vertex normals + per-pixel
TBN; per-part pre-resolved box-filtered MipChain with bilinear sampling and per-triangle LOD; Toksvig spec AA; 2x2 SSAA when
idle (AA toggle); renders at physical pixels. Prepared slice arrays cached per slice.
Numbers (PC, median): 1100x1000 new 1x 21 ms (47 fps; clear 2.6 / project 1.0 / bin 0.9 / raster 14.9), 1600x1200 26.5 ms
(38 fps); 2x2 SSAA still frame 54 / 69 ms. Playback of a Titus cinematic clip with In place: ~26 ms (39 fps), skin 1.7 ms.

UI: toolbar and anim bar are WrapPanels (no clipping at the default 1440x800); "AA" toggle + render stats; Anim "Filter clips"
TextBox (live, case-insensitive substring over the path, Bind pose always first, clips containing the model key - e.g. "titus"
- marked with a star and sorted first); "In place" (default on) rebases cinematic clips that start far from the origin (reference
bone = topmost bone displaced >= 50% of the max at t=0; cached per clip); Bind pose / model change resets clip, time label and
slider; playback is real-time; mouse wheel no longer changes AnimBox/LodBox/PartBox selection when closed; Meshes list is a
TreeView with _LODn variants nested (collapsed) under their base row (lowest LOD promoted if no base).
Next: a GPU path would be the real fix for >60 fps with SSAA.

## SM2Browser v3: GPU preview (2026-10-26, browser_v3)
- **Path:** `Gpu/GpuPreviewControl.cs` (Avalonia `OpenGlControlBase`) + `Gpu/GlPreview.cs` (Silk.NET.OpenGL 2.22).
  - Windows uses ANGLE (OpenGL ES 3 on Direct3D 11), Avalonia's default and the most robust driver path. Program.cs order: AngleEgl, Wgl, Software.
  - Shaders carry a runtime header: `#version 300 es` on ANGLE, `330 core` on desktop GL. The box runs desktop GL through Mesa llvmpipe; X11 options clear Avalonia's llvmpipe blacklist.
- **Scene:** `MeshPreview.BuildScene` (`MeshPreviewScene.cs`) shares slice, visibility, material, camera, light, In-place and skeleton logic with the CPU `Render`. The GPU draws exactly what the CPU would.
- **Buffers and skinning:**
  - Static interleaved VBO/IBO per batch (LOD/slice), uploaded once and cached by reference.
  - Skinning in the vertex shader: 4 weights; the bone palette is an RGBA32F texture (4×N, `texelFetch`), so there is no bone-count limit.
- **Materials:**
  - Per-pixel cotangent-frame normal mapping; spec map metal=r, rough=a|g, ao=b.
  - Toksvig gloss. Same light rig in linear light; sRGB encoded in the shader.
  - Albedo textures are `SRGB8_ALPHA8`, others RGBA8. Full mip chains, trilinear filtering, up to 16x aniso.
  - Uploads are budgeted to 120 ms per frame and continue on the next frame.
- **Frame:**
  - MSAA 8x (4x if the driver caps lower), resolved to an RGBA8 FBO and then blitted to Avalonia's framebuffer.
  - Wireframe draws a GL_LINES index buffer built lazily. Skeleton overlay has no depth test.
  - Renders on demand. During Play it renders on every compositor frame (vsync), sampling the clip at that frame's time.
- **UI:**
  - Toolbar `GPU` toggle (on by default). AA is disabled in GPU mode because MSAA is always on.
  - Stats read `GPU WxH MSAA nx · cpu ms · fps`; the tooltip gives the GL renderer/version.
  - Automatic CPU fallback when GL init fails, a frame throws, or no frame arrives within 2.5 s of showing the view. Avalonia never calls GL callbacks under software compositing; PreviewNote says why.
- **Bench:**
  - Run `LibSaber.SM2Browser.exe --glbench <game folder|synthetic> <model> <out> [frames]`. It writes glbench.txt and PNGs.
  - It measures uncapped (vsync-off) ms/frame at 1100×1000, 1600×1200, 1920×1080 and 2560×1440, for a static orbit and for clip playback, plus presented (vsync-on) fps.
  - On the PC: `Desktop\SM2_Work\run_glbench_v3.ps1`. Run it only when no game is running.
- **Box check (synthetic sphere, llvmpipe):**
  - The image matches the CPU renderer in orientation, colour and lighting, and skinning moves the mesh. Results are in `/workspace/browser_run_shots/v3/box_synthetic/`.
  - Box GPU numbers are software GL only: 14 ms at 1100×1000.
- **Install:** `C:\Users\Revaptor_FX\SM2Browser_v3\LibSaber.SM2Browser.exe` (MD5 2123211DEBDC00C2E902F78553B9D4A4). The first v3 build is kept as `.bak1`.
- **PC numbers (RTX 4090, ANGLE D3D11, MSAA 8x, aniso 16x; Titus LOD0, 300k tris, 525 bones):**
  - First frame including the texture upload: 374 ms.
  - Vsync off, static: 0.17 ms (1100×1000) to 0.26 ms (2560×1440).
  - Vsync off, playback: 0.3–1.2 ms. Playback cost is mostly CPU clip sampling plus the palette.
  - Vsync on at 1600×1001: 120 fps static and 120 fps playing (monitor refresh).
  - For comparison, CPU v2 ran 21–26 ms (38–47 fps).
  - Captures: `Desktop\SM2_Work\Captures\v3_gpubench` and `/workspace/browser_run_shots/v3/pc/`.
- **Open:**
  - The MainWindow GPU toggle and fallback have not been driven by hand on the PC. The bench exercises the same control and renderer from the same exe.
  - Wireframe on LOD0 (300k tris) is a solid silhouette, as on the CPU.

## Animation finishing (2026-10-26, anim_finish)

Base 62d96e7. PC build: `C:\Users\Revaptor_FX\SM2Extractor6\LibSaber.SM2Extractor.exe` (previous builds `.exe.bak1`, `.exe.bak2`).

New export flags
- `--anim` is repeatable; one value may also hold `;`/`,`-separated tokens, `@list.txt` (one per line, `#` comments), and globs
  (`*`, `?` over the whole .sani path, case-insensitive), e.g. `--anim "*jetpack_part_03_shot_03_char_titus*"`.
- `--anim-only`: writes only `{model}__{clip}.fbx` against the model's skeleton (no bind mesh, materials, textures). Requires `--anim`.
- `--anim-rebase first-frame|none` (default `first-frame`).

Rebase rule (ClipPose.Rebase)
- Stage 1, ROOT: if ROOT is off its bind (translation or heading) at frame 0, C = T(-p0)·RotY(-yaw)·T(pb) on every parentless local.
- Stage 2, CENTRE: if, after stage 1, CENTRE (pelvis) is still ≥ 2 m from its bind, the same C is applied below ROOT
  (to ROOT's children, in world space), so ROOT stays at its own track. v1 shots (c06 story_tower jetpack, c10 pve_relay boss_intro)
  keep ROOT at bind and put the level position on CENTRE. A 0.5 m crouch is a pose, not rebased. Only heading is removed; pitch/roll stay.
- Why default on: the game's loader `FUN_14222fe40` treats root motion relative to frame 0 (R0⁻¹(T_last−T0)); gameplay clips start
  at bind so it is a no-op for them. The removed placement goes to `{fbx}.rebase.json` (`sourceMeters`, `sourceYawDegrees`,
  `unrealLevelCm` = (x, z, y)·100, matches the survey's c06 CENTRE); place the actor there in Sequencer. `--anim-rebase none` keeps level space.
- Measured: c06 removed (-4705.916, 419.689, -1661.708) m, -55.5°; c10 removed (-43.253, -24.967, -29.468) m, -91.4°.

Timing (PC, anim-only, one run): titus 5 clips 8.3 s (~1.7 s/clip), vortex 2 clips 6.1 s; UE batch import 0.60–0.75 s/clip.
Old flow (full export + import per clip) was ~45 s/clip.

UE import (`tools/ue/ue_import_anim.py <fbx|folder> <mesh|skeleton> <dest> [yaw|auto] [rootmotion|noroot|autoroot]`)
- Folder: every `*__*.fbx` under it (recursive), one `import_asset_tasks` batch.
- Yaw auto: reads skeleton bone 0's reference yaw only and picks the 90° multiple that puts it at 180 (fresh ROOT-rooted and the
  old Null skeleton → 0; old Original skeleton → 180). The earlier "measure t0 of the first clip" probe was fooled by
  clips (vortex slam ROOT starts 90° off its bind; c10 after rebase) and is gone.
- Root motion: on (lock ANIM_FIRST_FRAME) for gameplay clips; off (REF_POSE) for additive and cinematic clips
  (cinematic = `.rebase.json` beside it, "cinematic" or `_shot_<n>` in the name) — those are for Sequencer.
- `__ADD` clips: AdditiveAnimType AAT_LOCAL_SPACE_BASE, BasePose ABPT_LOCAL_ANIM_FRAME, ref frame 0.
- Check: `tools/ue/ue_anim_finish_check.py [root]` (SEQ settings, ROOT/CENTRE/HEAD t0/end, ROOTTRACK, ADDMOVE).
  Result `/Game/SM2_Work/Tests/anim_finish2` (skeletons from anim_survey/fix12):
  c06 and c10: ROOT (0,0,0) yaw 180, CENTRE t0 at (0,-5,148) / (0,-52,280) cm; titus run root track 485.9 cm, melee 489.3, turn90 yaw 90;
  vortex slam 488.2 cm with yaw 0 import; blink: only lid bones move (31 bones, max upperLid04_R 18.2°).
  `/Game/SM2_Work/Tests/anim_finish` is the first (pre-fix) import, kept for comparison.

Additive (REA, sm2_retail.exe; Morpheme task registrar `FUN_142c961b0`, rdx = task fn, r8 = name)
- `TaskBlend2TransformBuffsAdditive` (string 0x143b7fee8, LEA 0x142c96b28) → `FUN_142cddd90`: T = T_base + w·T_add; rotation =
  fast polynomial slerp identity→q_add by w (constants 0x143b83080..0x143b830bc), composed with the base rotation.
- `TaskFeatherBlend2TransformBuffsAdditive` (0x143b7ff88, LEA 0x142c96ba4) → `FUN_142ce01c0` (per-bone weights).
- `HasAdditive` string 0x1435ef808, used by the dumper `FUN_141060280` (0x1410602f6, 0x141060455).
- Slot 1 (0xF3/0xF7, clip+0x68) is slot 0 relative to its own frame 0, so UE base = this clip's frame 0.
  Exported as `{model}__{clip}__ADD.fbx`. Face clips: Apply Additive, or Layered Blend per Bone from HEAD for full-pose face clips
  (lipsync c11 has no additive slot; treat it as a partial clip with a layered blend).

Open
- Multi-character shots: each actor is rebased on its own; restore relative placement from each `.rebase.json`.
- Additive rotation compose order (q_base·q_add vs q_add·q_base) is not proven from the decomp.
- c10 ROOT carries its own heading track (180 → 149°); harmless with root motion off.

## SM2Browser v4: anim ordering, Space, GPU blend passes (2026-10-26, browser_v4)

Install: `C:\Users\Revaptor_FX\SM2Browser_v4\LibSaber.SM2Browser.exe` (MD5 C7B88AF9E83CE31C510E6CDD50A17DD1; first v4 build kept as `.bak1`). v3 untouched.

**Space** toggles play/pause (tunnel KeyDown on the window). Ignored while a TextBox has focus (search, anim filter) or a ComboBox dropdown is open; needs a mesh, a clip with curves, and the Play button visible.

**Anim list order** (`Animation/CharacterAnimations.cs`): "Bind pose" first, then ★ main, ☆ cinematic, then the rest (each: same-model name first, then path). Status bar shows the ★/☆ counts and the source; the tooltip lists the classes.
- Data link, not names: `ssl/**/*.cls` (3471 text files) → class whose `geom = { nameTpl = "<tpl>" }` matches the model → walk the parent chain (trailing `__type = "<parent>"`) for `morphemeController.nameBundle` (Titus: story_titus → pc_marine_story_client → … → pc_marine_base_client, `nameBundle = "primaris"`) and `animSet` ("titus") → `morpheme/<bundle>/<bundle>.mr.resource` (JSON `res_desc_morpheme`), `linksAnimation` = ★ set (primaris: 1813 clips, tacticus + group_animations/tacticus_vs_*; calgar: 16).
- ☆ = clip names the character (whole-word token: animSet + tpl stem, e.g. `titus`) and its path is cinematic (`/cinematics/`, `cinematic`, `/scenes/`, `xscene`, `_shot_`, `/shot_`, `/story_`, `story_`), excluding `/interplay/` and lipsync. Titus: e.g. `cinematics/story_cannon/.../shot_01/..._char_titus_intercessor.sani`.
- No class/bundle → fallback: non-lipsync clips naming the token are ★.
- No per-template `tpl/<x>.tpl/animations` folder exists; the class cache is built once per catalog (parallel regex scan, ~72 MB).

**GPU blend passes** (`Gpu/GlPreview.cs`, `PreviewPartDraw.Blend/Tint/Aniso/SecondSpec/Center`), from the material (`blendMode` + diffuse alpha channel AKill/Transp):
1. opaque; 2. masked: alpha test at 0.5, sharpened `(a-0.5)/fwidth(a)+0.5`, `SAMPLE_ALPHA_TO_COVERAGE` under MSAA; 3. translucent: premultiplied `ONE, ONE_MINUS_SRC_ALPHA`, depth write off, depth LESS, sorted back-to-front by part centroid.
- Masked/translucent are two-sided (N flipped toward the eye). AlbedoTint applied.
- Titus head findings (`SM2_PARTS_ONLY=1 --glbench` dumps every part's blend/channels/alpha stats):
  - white/dark eye rims = `ch_eye_shadow_01` (96 tris, translucent, Transp(a), alpha mean 134) drawn opaque in v3; lashes `ch_eyelashes_02` (soft_cool, translucent, alpha mean 15) likewise; `ch_titus_head` variant "hair" (2539 tris, translucent) = hairline/brow shell.
  - hair = same 13,582 cards twice: variant "hard" masked (AKill) + variant "soft" translucent (blendMode=blend). Hard pass writes depth; soft pass with LESS only fills the cut-away fringes (standard two-pass hair).
  - hair shading: glt.anisotropic_spec (aniso 0.65, empty dirMap → default 90°, along B) + second spec on → Kajiya-Kay primary lobe on T and albedo-tinted secondary lobe on `normalize(T + shift*N)` (same as import_sm2_materials.py spec_layers), wrapped by N·L. Cotangent frame is derived from screen derivatives when the part has no normal map.
- Before/after (v3-style all-opaque vs v4): `/workspace/browser_run_shots/v4/cmp_{front,left,right,back}.png`, `head_*_{before,after}.png` (bench: helmet hidden, target = eye-part centroid). PC: `Desktop\SM2_Work\Captures\v4_gpubench`.
- Bench (RTX 4090, ANGLE D3D11, MSAA 8x): static 0.37–0.46 ms, playing 0.68–1.23 ms, vsync 116–120 fps.

Caveats / pending: CPU renderer still ignores blend (draws all opaque). In-app GUI captures of the sorted Titus list and the GPU toggle check in the real window were not driven (user active on the PC at Unreal Editor); verify by opening cc_titus.tpl → Animations. Space-key behaviour verified by code path only.

## Mannequin → SM2 retarget prototype (2026-10-26, retarget)
Tool: `tools/ue/ue_sm2_retarget.py` (UE 5.7 Python, run through uerun.ps1). Builds NEW assets only; the owner's
`IK_titus_deathwatch`, `IK_cadian_*` and the Mannequin assets are never edited (the batch step first copies the
Manny clips to `<dest>/../MannySrc`, so nothing is written into `/Game/Characters`; `redirectors /Game/Characters` = none).
- `mesh <fbx> <dest>`: import a fresh ROOT-rooted SM2Extractor6 export (`Exports\retarget\{cadian,titus}\tpl\...`).
- `rig <mesh> <dest> <name>` → `IK_SM2_<name>`: retarget root CENTRE, FBIK (start CENTRE), 28 chains named exactly like
  IK_Mannequin (Spine BACKdown→BACKup, Neck, Head, Clavicle, Arm ARM1→HAND, Leg LEG1→FOOT2, Thumb `x1_fing1→x1FINGeff`,
  metacarpals `xN_fing1`, fingers `xN_fing2→xNFINGeff`), goals Left/RightHandIK, Left/RightFootIK. Armor/cloth unmapped.
- `rtg <dest> <name>` → `RTG_Manny_to_<name>`: auto_map_chains EXACT, retarget pose `SM2_AutoAlign` (auto_align_all_bones,
  then finger bones reset to the SM2 bind pose, see below), then `configure_ops`.
- `batch <dest> <name> <anims...>` → `<dest>/Anims/<anim>_<name>`; an existing output is kept as `_bakN`.
- `eval <manny> <sm2>`: world-space hands/feet/head error vs Manny×pelvis ratio, spine twist, index curl, planted-foot speed, root disp.
- `fbx <anim folder> <out>`: FBX export of the retargeted clips (`Desktop\SM2_Work\Exports\retarget\anims\{cadian,titus}`).
- `config`, `unalign`, `fingers`, `info`, `redirectors`: repair / tuning / inspection.

UE 5.7 API gotchas found:
- `IKRetargetFactory` already creates the 6 default ops; `add_default_ops` on top duplicates them (guarded: only when 0 ops).
- Ops created before the IK rigs are assigned resolve no bones → retargeted clips had **0 root motion**. Fix: `run_op_initial_setup(i)` for every op (in `configure_ops`). After it: walk 450→471 cm, jog 1060→1110 cm (Cadian, ratio 1.047), Titus 694/1635 (ratio 1.542) = exactly Manny × pelvis-height ratio.
- `RootMotionOpSettings.source_root_bone/target_root_bone/target_pelvis_bone` are deprecated; even reading them raises.
- `chains_to_retarget` elements are returned by value: modify `ch[i]`, write `ch[i] = x`, then `set_settings`. (Before this fix every per-chain change — IK offsets, finger rotation mode — silently did nothing.)

Results (contact sheets Manny / Cadian / Titus rows: `/workspace/retarget/sheet_*.jpg`; Idle, Walk, Jog, Jump):
| check | Cadian | Titus |
|---|---|---|
| hands vs Manny×ratio | 5–8 % of pelvis height | 7–13 % |
| feet | 2–5 % | 2–5 % |
| spine twist |diff| mean | 0.1–1.6° | 0.1–2.3° |
| index curl vs Manny | −14…−20° (bind pose is 12° straighter) | ±4° |
| root motion | = Manny×ratio | = Manny×ratio |
| foot min z (bone) | +1–2 cm | +6–7 cm (boot sole thickness, bone above ground) |
| planted-foot slide, walk | ≈ expected (+3 %/+20 %) | ≈ expected |
| planted-foot slide, jog | 43 vs 25 cm/s expected | 97–105 vs 34–37 cm/s expected |
Finger curl fix: auto-align straightened SM2 fingers onto Manny's directions (retarget-pose curl ~18° vs Manny ref 42°), and
deltas are copied from that pose → every clip ~24° less curled. Resetting finger bones in the retarget pose to the SM2 bind
pose (`unalign`) fixed Titus (±4°) and halved Cadian's gap.

Open:
1. Jog foot sliding (Titus ~2.7× expected). IK is live (a +30 cm goal offset lifts the feet 30 cm) and pelvis
   `scale_horizontal` 1.0 is the minimum (0.65 → 220, 1.25 → 320 cm/s); `GENERATE_FROM_TARGET_PELVIS` and
   `affect_ik_horizontal` 0 change nothing. Next: put the leg goal on the ankle (FOOT1) instead of FOOT2, check the FBIK
   bone settings/iterations, or add a foot-planting/stride-warping step.
2. Cadian finger bind curl is 12° straighter than Manny: add a small retarget-pose curl on `x*_fing2/3` if it shows.
3. Feet hover 1–7 cm: that's bone height; check against the boot sole mesh before adding a static offset.

### Retarget 2: jog foot sliding fixed (2026-10-26, retarget2)
Cause (per-frame dump, `/workspace/retarget/rt_probe6.py`): the IK Goals op scales the source foot offset by the
**leg-chain** ratio while root/pelvis move by the **pelvis-height** ratio. Titus legs are relatively short
(leg ratio 1.396 vs pelvis 1.542), so the planted FOOT2 drifted ±8 cm with the stride. Earlier "IK has no effect"
tests were the by-value chain-settings bug, not the solver.
Fix (`leg_fix`, run by `configure_ops`, or `legfix <rtg> [ext] [z]`): leg chains `extension = pelvis ratio / leg ratio`
(Titus 1.104, Cadian 1.049) and `static_offset.z = (ext-1)·(hip z − FOOT2 z)` (Titus +13.3, Cadian +4.4 cm) because
extension also pulls the goal down along the leg. Pelvis `scale_horizontal` stays 1.0 (0.65/1.25 are worse).
Planted-foot speed now vs Manny×ratio: Titus jog 29/25 vs 37/34, walk 222/175 vs 223/177; Cadian jog 22/20 vs 25/23,
walk 151/138 vs 151/120. Root motion unchanged (= Manny×ratio). Feet min z: Titus +8 cm (was +6), Cadian +1.6.
Still open: Cadian index curl −14…−20° (bind 12° straighter than Manny; needs a per-bone retarget-pose curl offset whose
local bend axis has to be probed first); Titus foot lift could be scaled ~0.85 if the boots read as hovering.

## Bulk validation (2026-10-27, step 4, bulk)
Base b913484. Branch `bulk`. Tools in `tools/bulk/` (PC copies in `Desktop\SM2_Work\Scripts\bulk\`) and `tools/ue/ue_bulk.py` (`Scripts\ue_bulk.py`). The report is `/workspace/bulk/REPORT.md` with `contact_sheet.jpg`.
- **Batch:** `manifest.csv` (37 assets: SM, Tyranid, Chaos, Human, Vehicle, Prop/Weapon/Door; 1-2 loco/idle clips per skeleton). Run order:
  1. `bulk_export.ps1 [-Only a,b] [-Par N] [-Exe path]`: writes `Exports\bulk\<a>`. An existing folder is renamed to `<a>.bakN`. Writes `<a>.log` and merges `results.json`.
  2. `bulk_ue.ps1 [-Only] [-Mode import|check]`: runs `ue_bulk.py` per asset into `/Game/SM2_Work/Tests/bulk/<a>`. An existing folder is renamed to `_bakN`. Writes `Exports\bulk\<a>.ue.json`.
  3. `bulk_cap.ps1`: offscreen `ue_cap_lit.py` front/side/back captures into `Captures\bulk\<a>`. Don't name a variable `$c` there: PowerShell variables are case-insensitive, so `$c` clobbers `$C`.
  4. On the box: run `make_report.py`, then `contact_sheet.py`.
- **ue_bulk.py checks:**
  - bones, ref-pose NaN, bounds;
  - slot count vs sidecar, MI count;
  - MI vs sidecar: switches, baseLayer, second spec, aniso;
  - texture params equal to the first sidecar map of each role, sidecar missing maps, files on disk;
  - per clip: frames, root motion flag, root displacement/speed, NaN, max bone distance.
- **Fixed: glt.blendMode "none" was exported translucent.**
  - 218 slots were affected:
    - chimera hull;
    - `ch_undersuit_v1/v2` on champions and constructors;
    - `ch_lictor_legs_01`;
    - `awp_censers_03`.
  - A further 7 slots took a layer's nested `blendMode = "tex_blend"` (`ch_soldier_helmet_01`, ravenwing/lamenters helmets).
  - REA: string-to-enum 0x14230f690 runs over the 20-entry table at 0x144cc9180 (none, black, opaque, blend, add, ...); enum-to-string is 0x14230f710.
  - `MaterialMaps.BlendMode` now reads only the direct `glt` child. `BlendOf` treats none and opaque as opaque.
  - Covered by `BlendModeTests`.
- **Diagnostic:** the export log now names up to 8 unmatched clip tracks. The partial matches are expected:
  - tacticus clips on non-Titus marines match 110-238/282: face_bind bones;
  - swarm hormagaunt matches 62/101: tongue/eye bones;
  - `CharacterWorldSpaceTM` never matches.
- **Not bugs:**
  - **termagant_caps texture params at defaults:** a stale UE import from the first export. A fresh import is clean.
  - **Placeholder tpls:** `imp_*_dyn` doors are nav_silhouette boxes (8 verts); real doors are e.g. `imp_bur_door_03` and `refrt_imp_gate_door_02`, both added to the batch. `cc_rubric_marine` is grey-box `sc_grey_*`.
  - **Bounds:** the log's "File bounds" are whole-tpl bounds and include markers and collision.
- **Open:**
  - **Constructor tpls** (`cc_sm_constructor_*`, `cc_ch_constructor_soldier`) hold every customization variant:
    - 5,315 nodes and 274 textures;
    - 375-670 s per export, ~8-10 GB RAM, 9-20 GB on disk;
    - every texture is written 3x (dds+png+tga).
    - The UE import of the sniper crashed the editor (RHI breadcrumbs during texture builds).
    - **Done:** `--variant` and `--tex-formats`. See "Constructor tpls" below.
  - **Fixed: Tyranid monsters and walkers had no root motion in UE (`UnrealRoot.HasSingleRoot`).**
  - Affected: carnifex, lictor, hive tyrant, warrior, zoanthrope, dreadnought, helbrute.
  - Their tpl hierarchy is `ObjectNNNN → ObjectNNNN → CENTRE` with no `ROOT`. FindPromotable declined, so FbxWriter kept the `{mesh}_Skeleton` Null. That Null became bone 0, and the 0xF4 motion sat on bone 1.
  - Now a single parentless bone also drops the Null.
  - Verified on the PC (`Exports\bulk_fix` → `/Game/SM2_Work/Tests/bulk_fix`): dreadnought `walk_fwd_v02_5_2ms` gives 526 cm/s, matching its name.
  - Not re-exported: the non-animated single-root assets (chimera, servitor, servo skull, doors, weapons).
  - Tests: `UnrealRootTests` (+3). `AnimFbx_IsSkeletonOnlyAndTheCurvesMove` now uses two roots so it keeps the Null.
- **Checker and tools:**
  - `ue_bulk.py` counts `M_SM2_ClothSimHidden` (collision/nav slots) as hidden, not as a mismatch.
  - `ue_bulk.py` takes an optional set name: `import <asset> bulk_fix`.
  - In `make_report.py`, `meta_fix/` overrides `meta/`. A zero-displacement loco clip with ROOT as bone 0 is a WARN (in-place; rubric `rubric_fwd`, lesser sorcerer `walk_fwd`; not verified in REA).
- **Captures:**
  - Captures right after a batch import can show grey placeholder textures while textures compile. 7 assets were recaptured into `Captures\bulk_re`.
  - TODO: `ue_cap_lit.py` should wait for texture compilation.
- **Result** (`/workspace/bulk/REPORT.md`): 37 assets, PASS 31, WARN 3 (captain partial tracks, rubric/sorcerer in-place clips), FAIL 3 (constructors, not imported).
- **Disk:**
  - Cleanup was approved by the user. Deleted `Exports\bulk\*.bakN`: 36 folders, 61.1 GB. C: free went from 4.9 GB to 66.0 GB; it was ~50 GB after the bulk_fix imports and DDC.
  - Deleted 18 of the 24 `/Game/SM2_Work/Tests/bulk/*_bakN` folders through the editor API.
  - The other 6 (calgar, chaplain, intercessor, primaris_captain, titus_assault, titus_deathwatch) were reported in use because their new `_Skeleton` still referenced the renamed mesh. **Correction (2026-10-27):** all 6 were deleted with `Scripts\bulk\ue_del_bak.py` after a UE restart. They took ~0.26 GB on K:, not the ~2.2 GB estimated earlier. No `Tests/bulk/*_bakN` folders remain.

## Constructor tpls: `--variant` + `--tex-formats` (2026-10-27, constructor)
Base d0ad14f. Branch `constructor`. Patch: `/workspace/patches/constructor/`.
- **Problem:** `cc_sm_constructor_tank`, `cc_sm_constructor_sniper` and `cc_ch_constructor_soldier` are customization libraries, not characters. They hold every armor piece of every region.
  - Exports took 375-670 s and 12-23.5 GB, with every texture written 3x.
  - The UE import of the sniper aborted the editor.
- **REA (how the game hides armor):**
  - Geom script bindings:
    - HideObj `FUN_14133eae0`;
    - ShowObj `0x14133fec0` (a thunk to `FUN_141349c70`);
    - HideObjs `0x14133eaf0`;
    - ShowObjsOnly `0x141347dd0`.
  - HideObj and ShowObj loop over the names and call `FUN_14133e750(geom, name, show, isPattern, 0x140000)`.
  - `FUN_14133e750`:
    - It looks up the name through `FUN_141d912a0` and checks the flags at `node+0x18`.
    - It then calls `FUN_142216790(tree, node, 0x140000, hide)`. That walks the subtree with `FUN_14225c680` and sets or clears bits at `node+0x1c`.
    - So hiding is recursive: hiding a `_grp`/LOD group node hides everything under it.
    - With `isPattern` it glob-matches through `FUN_142781420`/`FUN_142781890` and applies `FUN_141d99490` per match.
  - Default look: `ssl/body/customization/progression/character_customization_outfit_state_release.sslbin` calls `SetActivePresetToDefault` next to the constant `"PREDEFINED_PRESET_1"`.
  - Team and class come from the loading-scene dummy. `ssl/loading_scene_logic/customizable_character_dummy_tank.cls` has `nameTpl="cc_sm_constructor_tank"`, `team="LOYALIST"`, `masteryType="PVE_TANK"`.
- **Code** (`Customization/SslText.cs`, `Customization/ArmorVariant.cs`, `AssetExporter.ApplyVariant`):
  - The customization library is the text SSL. Customizable objects are every region's `meshes`, including subtrees.
  - Hide set = (customizable − shown by the preset/overrides) ∪ `meshesToHide` of the shown regions ∪ `powerfist*` ∪ `-obj`, minus `+obj`.
  - The filter only hides parts. It never unhides parts already hidden as LOD, volume or cloth-sim (HideReason `variant`).
  - Appearance resolution: the dummy `.cls` (`CharacterFromDummyClass`), then the tpl name, then the `PVE_PVP_<X>` fallback.
  - Writes `{model}.variant.json`: team, appearance and its source, preset, regions, hidden/visible counts, and notes.
  - With a variant active, only textures referenced by visible parts are written. The tpl TexList and the per-part `Material.Slots` are skipped (`partsOnly`).
  - `MeshPart.Node` (a tpl node id) and `Bone.SourceName` were added so parts can be matched to object names.
- **CLI:**
  - `--variant <spec>`, with tokens split on `,` or `;`:
    - `default`;
    - `all` / `none`;
    - `preset=N` or `preset=PREDEFINED_PRESET_N`;
    - `appearance=TEAM/UID`;
    - `team=`;
    - `SECTION=uid` (a region override);
    - `+obj` / `-obj`.
  - Without `--variant`, constructor tpls get `default` (PREDEFINED_PRESET_1). `--variant all` keeps every piece, as before. Other tpls are unchanged.
  - `--tex-formats dds,png,tga|all`. The default stays dds+png+tga.
  - png-only is safe for UE: `import_sm2_materials.py` reads the sidecar `packing[].stored` and falls back to dds only for old sidecars. The FBX links png, then tga, then dds.
- **UE RHI crash:**
  - Report: `Saved\Crashes\UECC-Windows-CF5F62BC...`, 2026-10-26 23:44 KST. "Abort signal received" in D3D11, with `LogRHI: Error: Breadcrumbs 'RHIThread'`.
  - It happened during the sniper's material import, after 89 2K textures (each with a "Required Memory Estimate 351 MB"). The whole Python build ran inside one editor tick.
  - Cause: resource exhaustion from texture count and size, not a data bug.
  - Mitigation:
    - The variant filter (sniper 895 → 81 png).
    - `import_sm2_materials.py` calls `unreal.SystemLibrary.collect_garbage()` every `GC_EVERY = 32` texture imports.
  - The separate 00:43 crash (62B0) was an ensure in ForceDeleteObjects on `cc_termagant_bak1`, which is unrelated.
- **Re-export** (`tools/bulk/ctor_export.ps1 -InPlace`, exe10, default variant, `--tex-formats png`, 3 parallel) into `Exports\bulk_fix\<a>`:

  | tpl | before s | after s | before GB | after GB | textures before → after | FBX MB | hidden / visible parts |
  |---|---|---|---|---|---|---|---|
  | tank | 670.8 | 104.7 | 23.5 | 0.394 | 1031×3 → 69 png | 365 → 24.1 | 978 / 54 |
  | sniper | 513.8 | 102.7 | 19.8 | 0.413 | 895×3 → 81 png | 22.4 | 778 / 64 |
  | soldier | 375.5 | 64.6 | 12.1 | 0.276 | 520×3 → 53 png | 17.1 | 370 / 41 |

  - Times are wall-clock with 3 exports running in parallel. In earlier runs, the variant alone was 58-96 s.
  - Material sidecar slots after filtering: tank 54 (was 1032), sniper 64, soldier 41.
  - Validation on the real tpls (box console app): tank hid 565 of 620 customizable objects, sniper 442 of 498, soldier 237 of 286.
  - The sniper has 4 benign notes: its preset lists torso_chest4/5 and torso_l/r_pauldron, which its library doesn't have.
- **UE:** `ue_bulk.py import <a> bulk_fix` imports into `/Game/SM2_Work/Tests/bulk_fix/<a>`, then run the captures.
- **Tests:**
  - `ArmorVariantTests` (+9): SSL parse, default preset, preset 2 grp subtree and meshesToHide, overrides and forced objects, unknown region, no LOD unhide, request parse, appearance from cls/name, and `Part.Copy` keeping Node.
  - `VariantOptionTests` (+7).
  - Totals: SM2.Assets 81, SM2Extractor 42, LibSaber.Tests 5, all passing.
- **Cleanup (approved by the user, 2026-10-27 01:14 KST):**
  - `/Game/SM2_Work/_Legacy` (SM2_Test, SM2_v4, SM2) and `/Game/Test` held 0 files. Both were removed.
  - The registry check printed `GONE True True` (`Scripts\legacy\ue_rmpaths.py`).

## Secondary physics: browser + Unreal (2026-10-27, physics)
Base b7ae735. Patch: `/workspace/patches/physics/`. Captures: `/workspace/browser_run_shots/physics/`.

The goal is the motion the game shows on purity seals, tabards, cloth strips, cloaks, cables and dangling parts.
- The browser simulates it live.
- The exporter bakes the same simulation into the FBX, so Unreal plays it with no runtime setup.

### REA (sm2_retail.exe)
Address conversion: rdata VA = file offset + 0x140001200; .text VA = file offset + 0x140000a00. Decompiles are in `/workspace/rea/dec_phys*`.

**Where the data lives.** The character `.cls` (text SSL) carries it in two places:
- `prop_cloth_sim.clothes[]`: `simMeshName`, `renderMeshes[]`, `maxInfluenceDist`, `distanceRemapPower`, `useTopologicalDistance`, and `config`.
- `prop_phys_jiggle`, with one settings block per bone.

The field-name strings are at 0x1435f10c0..0x1435f1510. `PhysicsLocator` finds the `.cls` whose `nameTpl` is the model (Titus → `pc_marine_pve_story_titus_client.cls`, Calgar → `npc_calgar_marine_client.cls`).

**Jiggle (`PhysJiggleObject`).** `JiggleSolver` is an exact port of the step loop at 0x14147e5b5 (inside FUN_14147e500).
- Settings layout:
  - +0x10 stiffness, +0x14 mass, +0x18 damping, +0x1c gravity;
  - +0x20 offsetDirection, default (0,0,1) @0x143609db8;
  - +0x28 offsetDistance;
  - +0x30/+0x38 constraintMin/Max, defaults (-1,-1,-1)/(1,1,1) @0x143609e08/0x143609dd8;
  - +0x40 writeback flag.
- The step:
  - M = bindLocal × parentWorld, orthonormalised by FUN_140fd45e0. Target = M·(dir·dist).
  - Spring: v = (v + (t−p)·k/m·dt [+ gravity/m on y]) · exp(−damping·dt).
  - Per-axis clamp FUN_14171ad80 in the bone frame, scaled by dist (0x14147ea5b..0x14147eb3c).
  - Sentinel 888.888 @0x1435571a0 before the first step. Teleport check at 0x14147ed64.
  - The bone is re-aimed at the particle; it does not translate.

**Cloth config.** The struct is registered at 0x141090c39..0x141091847, and the constructor ends at 0x14108f119.
- Phases: the constructor at 0x141089140 gives stiffness 0.8, multiplier 1, compression 0.9 and stretch 1.1.
- Other blocks and defaults:
  - tether +0x40 (0.5, 1; 0x14108a660);
  - motion +0x4c (0.5, 1; 0x141089966);
  - separation +0x58 (off, 1, 1; 0x141089fe6);
  - selfCollision +0x64 (off, 0.5, 0; ctor 0x14108efce);
  - gravity +0x74 = (0, −9.8, 0) @0x14108f112;
  - solverFrequency +0xe0 = 30;
  - stiffnessFrequencyScale +0xe4 = 0.5;
  - environment +0xb0 (airDrag 0.5, airLift 0.5, winds 0; 0x14108f03a..0x14108f0a4).
- Collision shapes: capsules, spheres and boxes on bones.
- Sim mesh: the `*_SIM` shell.
  - COLOR0.r is the per-particle max distance; c ≤ 1e-4 means pinned (it follows skinning).
  - Render parts bind to the closest sim triangle (barycentric plus normal height). The weight is 1 within `maxInfluenceDist`, else (maxInfluenceDist/d)^distanceRemapPower.

### Browser (live)
- Code: `Physics/{PhysicsParser, PhysicsSetup, PhysicsLocator, JiggleSolver, ClothSolver, PhysicsRuntime}.cs`.
- `ClothSolver` is a PBD stand-in for NvCloth. It runs:
  - phases (V/H/bend/shear) with compression and stretch limits;
  - tethers and motion constraints (max distance);
  - capsule, sphere and box collision;
  - drag and lift wind with unit particle mass;
  - substeps = solver frequency.
- GPU preview: the `Physics` checkbox (shown when the model has a `.cls` setup).
  - It resets on clip change, loop and time jump-back.
  - About 0.7–0.9 ms per step, so 120 fps holds.
- Render parts are matched by exact name, falling back to the name without `_LODn`. `cc_calgar.tpl` only has `cloak_REND_01_LOD1` etc. (`ClothEntry.DrivenParts`).

### Export: baked into the FBX (`Physics/PhysicsBake.cs`, `Physics/PhysicsSidecar.cs`)
Unreal cannot be set up live from Python (see below), so the exporter bakes the simulation (on by default; `--no-physics` turns it off).
- **Jiggle:** the exact port runs over each clip at 60 Hz. Each jiggle bone's local transform is written as keys.
- **Cloth:** `AddClothBones` adds helper bones `{SimMesh}_physNN`.
  - There is one per free sim particle (`particlesPerBone` = 1, cap 1024 per cloth); pinned particles stay on skinning.
  - Each helper is parented to the particle's dominant skin bone and placed at the particle.
  - Every render vertex of the driven parts, in the base buffer and every LOD level, is re-skinned. Its closest sim triangle's corner bones get barycentric × influence weight; the original skin keeps the rest. The top 4 are kept and renormalised.
  - Per frame, each helper's rotation is a polar-decomposition fit (Müller, warm-started) over the particle and its one-ring. Translation = the simulated particle.
- **Clip settings:**
  - Preroll is 1.5 s, holding frame 0 so springs and cloth settle.
  - Baked locals are keyed by bone name (`AnimationClip.Baked` / `BakedBones`), so they survive `VisibleGeometry` and `UnrealRoot.Promote` copies.
- **`--anim-only` exports** add the same helper bones (they are deterministic: same tpl and `.cls` give the same names), so the clips carry both jiggle and cloth curves.
  - The UE skeleton needs the helpers, so a model exported before this patch must be re-exported and re-imported.
  - Without them, Unreal ignores the cloth tracks and keeps the jiggle tracks.
- **Sidecar `{model}.physics.json`:** the source values, what was baked per clip, and the Unreal mapping for a live setup. `ue_sm2_physics.py` is written next to it.

Measured on the box (PhysBench, `SM2_PHYS_BAKE=1`). "baked" is the export skeleton played as plain skinning with physics off, which is exactly what Unreal plays:

| Model / clip | Helpers | Jiggles | On vs off | Baked vs live | Reproduced |
|---|---|---|---|---|---|
| Titus run | 922 | 34 | 4.80 % px | 0.18 % | ≈96 % |
| Calgar convoy run (cloak, belts, ribbons) | 1294 | 3 | 3.02 % px | 0.78 % | ≈74 % |

Bake time: 0.5 s (Titus, 57 frames) and 3.0 s (Calgar, 399 frames).

FBX check with assimp (`/workspace/phys_export/titus`): the model FBX has 581 nodes. The clip has 368 animation channels with physics and 281 without (34 jiggles + helpers).

### Unreal mapping (sidecar `ue`, for a live AnimDynamics / Chaos Cloth setup)

| SM2 | UE |
|---|---|
| jiggle offsetDirection·offsetDistance | AnimDynamics PendulumOffset = −dir·d·100 cm |
| stiffness/mass | LinearSpringConstant = k/m |
| damping | LinearDampingOverride = damping |
| gravity/mass | GravityScale = (g/m)/−9.8 |
| constraintMin/Max | linear limit along the offset = c.z·d·100; angular limits = atan(c) in degrees |
| phases V/H × multiplier | Chaos Cloth EdgeStiffness = avg(V,H)·mult |
| bend | BendingStiffness |
| shear | AreaStiffness |
| tether stiffness/scale | TetherStiffness / TetherScale |
| damping | DampingCoefficient |
| gravity | GravityOverride (cm, axis-converted) |
| airDrag/airLift | Drag / Lift |
| linear/angular velocity scale | LinearVelocityScale / AngularVelocityScale |
| thickness, friction | CollisionThickness, FrictionCoefficient |
| solverFrequency | SubdivisionCount = ceil(freq/60) |
| COLOR0 max distance | MaxDistance mask (m → cm) |
| capsules/spheres/boxes | physics asset bodies (cm) |

### UE side (`tools/ue/ue_sm2_physics.py`, embedded and written next to the export)
- `... -run=pythonscript -script="ue_sm2_physics.py <physics.json> <mesh asset> [/Game/SM2_Work/Tests/physics] verify"` checks that every jiggle and helper bone is in the skeleton and prints `SM2PHYS` lines.
- `physasset` makes a physics asset through `SkeletalMeshEditorSubsystem.create_physics_asset`. It renames the result to a free `_N` name and never replaces an existing asset.
- These cannot be done from Python, which is why the default is the bake:
  - There is no clothing factory: "Create Cloth Asset from Selection" is UI only.
  - AnimGraph nodes (AnimDynamics, RigidBody) cannot be added.
  - Control Rig `RigUnit_PointSimulation` is disabled.

### Limits and open items
- NvCloth is approximated (PBD):
  - wind uses unit particle mass;
  - self-collision is skipped;
  - box collision assumes size is the full extent.
- Jiggle scalar defaults (when a `.cls` omits stiffness, mass or damping) are not verified. The teleport threshold is unknown.
- The CPU renderer is not physics-driven; only the GPU preview is.
- Baked clips are not loop-seamless (each clip is simulated from a settled frame 0).
- Helper bone count is high (one per free particle). Unreal handles it, but `AddClothBones(particlesPerBone, maxBonesPerCloth)` can trade fidelity for fewer bones (4 per bone, cap 64 → Calgar 1.7 % residual).
- **PENDING (PC was in use):**
  - Export on the PC with this build.
  - Import to `/Game/SM2_Work/Tests/physics`.
  - `ue_sm2_physics.py verify`.
  - Capture browser vs UE side by side on the same clip (titus `tacticus_run_v01_intercessor`).

## Browser VFX playback (2026-10-27, vfx)
Branch `vfx-work` (from 642fb56), patches in `/workspace/patches/vfx/`; not pushed. Niagara/UE export is out of scope.

### Code
- `src/LibSaber.SM2.Assets/Effects/`:
  - `SslText` parser
  - `EffectLibrary`: sfx_group instancing and `__type` prototype inheritance
  - `EffectDefinition`: emitters, lights, attached sub-emitters, alpha masks, draw target
  - `EffectSimulation`: fixed 60 Hz, seeded, deterministic, with backward seek replaying from 0
  - `EffectSprites`: camera-facing quads, sorting, flipbooks
  - `PartAliases`: flipbook grid
- `src/LibSaber.SM2Browser/Gpu/EffectRenderer.cs`: GPU pass with a lit ground grid, one draw per emitter batch with its blend state, HDR exposure + tonemap, soft particles, and an alpha-mask texture unit.
- `MainWindow.Vfx.cs`:
  - Selecting an `.sfx` (use the Effects filter + search box) plays it on the GPU preview.
  - VFX bar: Restart, scrub (pauses), Loop, Ground. Space and the Play button toggle play/pause. Drag orbits, wheel zooms.
  - Effects, textures and `part_aliases.ps` are read from the open catalog by basename.
- Dev mode without a game folder: `SM2_VFX_LOOSE="sfxDir|part_aliases.ps|texDir" SM2_VFX_OPEN=<name>`.
- Bench: `--vfxbench <sfxDir> <part_aliases.ps> <texDir> <out> name@sec[@distance]...` writes 4x2 sheets, perf numbers and a determinism check.
  - Env vars: `SM2_VFX_ONLY`/`SM2_VFX_SKIP` (emitter filters), `SM2_VFX_EXPOSURE`, `SM2_VFX_LIT`, `SM2_VFX_FRAMES=1`.
- Tests: `test/LibSaber.SM2.Assets.Tests/EffectTests.cs` (6) cover parse, group override, explosion count, constantFlow rate, genOnDie, determinism/seek, aliases and curves. The full suite passes (101).

### REA (sm2_retail.exe; VA = strings_ascii offset + 0x140000000)
- **Node types:** sfx_group @0x143646930, sfx_part @0x14363c148, sfx_trail @0x14363c280, CPU_EMITTER @0x14363bc80, GPU_EMITTER @0x14363bcf0.
- **part_aliases.ps** is read by 0x1418fe5c0. Keys: nFrameX @0x143647ea0, nFrameY @0x143647ea8, nFrameMax @0x143647eb0 (0 means X*Y; clamp 255), motionVectorScale @0x143647ec0, Mesh.Vertex0..6 @0x143647ed4.
- **blendType** parser is 0x141751cd0: blend=0, add=1, blend_add=2. **sorting** parser is 0x1417534f0. softDepth @0x143645530.
- **attached** @0x143644cd0 → genOnFly @0x143644b38 / genOnCDT @0x143644b48 / genOnDie @0x143644b58. Entries are `{name, numParts @0x143644b18}`. Error string @0x143648810 says LCS-mode emitters cannot be attached.
- **speedInherit** @0x143644878 is a {min,max} fraction of the parent velocity.
- **Untyped emitter entries** (5499) are CPU emitters only when they carry `__node`. Orphans are skipped (this removed the orange square in arifle).
- **Draw target:**
  - `drawInSimulationTex = drawInSimulationEnabled` draws into the wind/water sim only, not colour (grenade wind_expl/water_expl).
  - `animIn.channel = DISTORTION` is refraction only.
  - Neither is drawn.

### Coverage of the starter set (captures in `/workspace/browser_run_shots/vfx/`)
| effect | emitters | drawn | not drawn |
|---|---|---|---|
| fx_bolter_muzzle_client | 14 + 1 light | 12 sprite + light | 2 lwiGeom mesh |
| part_wpn_arifle_mzl_client_01 | 10 | 10 | 1 orphan |
| part_mg_frag_grenade_expl | 33 | 31 | 2 sim-only |
| part_mg_smoke_puff_01 | 3 | 3 | – |
| fx_cine_bolter_tracer | 1 | 1 | – |
| part_mag_ee_bolter_flesh | 9 | 6 | 3 mesh, 1 untyped |
| part_mag_bullet_mid_blood_red | 3 | 3 | 1 orphan |
| part_mag_blood_red | 27 | 27 | – |

Perf on the box (llvmpipe, 512²):
- sim ≤ 0.021 ms/step;
- sprite build 0.02–1.6 ms;
- GPU 2–25 ms (grenade spikes higher on llvmpipe);
- every effect is deterministic;
- the UI holds about 60 fps on the grenade.

### Limits / open
- Not rendered:
  - lwiGeom mesh particles;
  - sfx_trail*;
  - distortion/refraction;
  - wind/water sim draws;
  - additionalContrast;
  - GPU-only features (vector fields, depth collision).
- Heuristics:
  - size is treated as a radius;
  - ratioDependency is simplified;
  - blend_add is interpreted;
  - Exposure 1/250 and lit ×100 were tuned on corpus medians;
  - genOnFly is approximated (a burst for explosion targets, a per-parent rate for flow targets).
- Standalone sockets: a socketed effect is placed 1.2 m up, pointing +X. Attaching to a model bone is not wired up yet (`EffectInstance.Socket` hook exists).
- Grenade smoke reads a bit dark/blue. Not compared side by side with the game: this needs in-game captures on the PC when it is free.

## VFX 2: mesh particles + trails (2026-10-27, vfx2)
Based on 00d32bd. Patches in `/workspace/patches/vfx2/`. Captures in `/workspace/browser_run_shots/vfx2/` (before: `../vfx/`).

### What was added
- **lwiGeom mesh particles.** `Effects/EffectMeshParticles.cs` parses the particle into `MeshParticleDef` (REA: lwiGeom @0x143644870, parser 0x141760a45..0x141762f4d).
  - `nameGeomTpl` names the template. `typeInst` (enumeration/range/default) picks the sub-object: template parts grouped by node, in node order.
  - `initRotAxises.random`. `applyRotationFromEmitter`.
  - `sfxMtlOverrides`:
    - color (constExpr / ageSpline / ageSplineNew). Raw values are **R,G,B,A**. Evidence: the flesh splash `[255,44,3,155]` is blood red, and read as A,R,G,B it turns blue.
    - emissiveIntensity, colorTransparency.
  - Each instance's world matrix = scale(size×emitScale) · rotation (+Z aligned to velocity if `isAlignSpeed`) · translation.
- **Browser side** (`SM2Browser/Effects/EffectMeshes.cs`): an async cache of tpl→GPU meshes (TemplateMeshReader). The material is read from the texture `.td`:
  - `preset "sfx"`: premultiplied translucent, with `tint [A,R,G,B]` and `clipping.level`.
  - `shader_blood_splash` (solid_blood) and `game_material "flesh"` (flesh_material): the texture is only a placeholder, so they draw white × override colour. Flesh gets an assumed dark red; this is not REA-confirmed.
  - Everything else: opaque and lit.
- **Mesh shader** (`EffectRenderer` `_mesh`):
  - Lighting: key light plus ambient, plus the effect's point lights.
  - Emissive ×25 for sfx materials only.
  - Override colours are sRGB.
  - Hue-preserving tonemap, so saturated blood stays red instead of going orange.
- **sfx_trail ribbons** (`Effects/EffectTrails.cs`; REA strings 0x143645e08.., save serializer 0x1417b1031; the runtime builder was **not** traced):
  - One point per sim step at the parent, split by `maxSegLength`. Points die after `timeLifeSeg`; the length is clipped by `maxTrailLength`.
  - Width and colour are sampled along the ribbon.
  - Shapes: billboard/cylinder (camera-facing), tape, cross (2 planes), star (3 planes).
  - The union `__type` decides the shape (saves keep stale members).
  - Standalone preview flies the root along +X at 40 m/s.
  - `lighting.isEnabled` trails use the same LitScale as sprites.
- **Mesh sources:**
  - Bench: env `SM2_VFX_MESHES` or a sibling `mesh/` folder (`tpl/<name>.tpl/`).
  - UI loose mode: 4th field of `SM2_VFX_LOOSE`.
  - Catalog mode: by basename.
- **Tests:** `EffectGeomTrailTests` (3) cover lwiGeom parse + RGBA colour, mesh spawn/expiry and trail segmenting/expiry. The full suite passes (104).

### Coverage
| effect | before (vfx) | after (vfx2) |
|---|---|---|
| fx_bolter_muzzle_client | 12 sprite + light; 2 mesh not drawn | 12 sprite + 2 mesh + light |
| part_mag_ee_bolter_flesh | 6/9 (3 mesh + 1 untyped not drawn) | 6 sprite + 3 mesh (gibs + blood splashes); 1 untyped `emitter?` left |
| trail_bolter_tracer_autority / trail_stalker_bolter_tracer_client | not supported | drawn (star / billboard ribbons) |
| trail_bolter_smoke | not supported | simulated (139 pts), but nearly invisible: additive, colour ≤177/255 × intensity 2 |

### Known gaps / next
- Blood-splash VAT animation (`splash_5_v2_anim_data*`, `combineMtlAnimControl.animStage`) is not implemented, so the static rest mesh is drawn.
- Distortion pass, socket attach to the selected model in the UI, and the grenade smoke colour are not done.
- `smoke_spray_01` does not exist in retail (only `_02`/`_03`). It is genuinely missing in the game data, not an extraction miss.

## vfx3 (part 1): effects on model sockets + anim-event cues

- `EffectHost` (Assets/Effects): bone sockets from pose worlds (`SetPose`), `Attach(def, bone)`, wildcard `name*`; `sfx_source_pt` = root.
- Emission axis REA: emitter without `emitDir` (`parentArea.direction isEmitByNormal`) emits along the parent normal; on a bone that is
  bone **+Z** (`EffectDefinition.EmitByNormal`; wpn tip barrel = +Z, part_mzl_bolter flame_circle_2 z+, fx_jetpack_attack jets z+ on jet bones). No parent: +Y as before.
- Anim events: ETNA block 245 (`AnimEvents.Parse`, `AnimationClip.EventTracks`), times normalised 0..1. Event→VFX is data-driven in
  blueprint .sso graphs (`AnimEventVfxMap`: BulkVfxOnMorphemeEvents + exec BFS from MorphemeEvent nodes to sfx starters + string pipes).
  Map unions all blueprints -> per-blueprint source filter (UI "blueprints" box / `SM2_VFX_BP_FILTER`). `*_stop_effect` events not handled.
- UI: HostVfxBar (effect name, socket combo "Auto (nameParent)" / bones, Attach, Clear, Loop, Anim events, filter). Overlay via
  `GpuFrameRequest.Overlay`. Env: `SM2_MODEL_OPEN="tplDir[|sani]"`, `SM2_HOST_OPEN`, `SM2_HOST_EVENTS=1`, `SM2_HOST_LOD1`, `SM2_HOST_CAM`, `SM2_HOST_PLAY`, `SM2_HOST_DEBUG=1` (per-frame quad log).
- Bench: `--hostvfxbench <tplDir> <sani|-> <sfxDir> <aliases.ps> <texDir> <out> <effect|-> [bone|-] [seconds] [every]`.
- Known issues (todo, vfx3 item 5): fx_jetpack_attack sprites huge (ageSize spline 0..33 x scale; size treated as half-width) -> size semantics
  need REA; `degNear` near-camera fade not implemented; loose wpn_bolt_rifle_01 tpl decodes LOD1 parts + Object* proxies (drum shape);
  tacticus dash clip on cc_titus_assault leaves ROOT ~9.5 m away even with In place.

## vfx3 part 2 (Grok Bot, 2026-10-27)
- d6b28f4 missing-texture sprites are skipped (were white quads; this was the "oversized jetpack flame"; size = half-width is right).
- e5628ce distortion pass: `typeParams.distortion` (normal map, power ageSpline) + `visuals.sdr` DISTORTION. Scene grabbed once,
  quads redrawn with offset (RG*2-1)*power*alpha*`SM2_VFX_DISTORT` (default 0.04, heuristic). SELF_DISTORTION parsed only.
- 904110b anim-event stop cues: BlueprintNodeVfxStop / SfxLibraryStopsfx / StopPackOfVfx / StopAllSfx and duration-event
  OnDeactivate/OnExit set `EffectInstance.StopTime` (emission, trails, lights stop; live particles finish). Retail: 62 stop events.
- a8abb8c `optimization.degNear/degFar` linear ramp (radStart->radEnd) on size/intens/alpha/disp for sprites (`SM2_VFX_DEG=0` off).
  Field set confirmed in exe reflection strings; curve shape not REA'd. Not on trails/mesh particles yet.
- ac0a3d3 bolter drum: Object0080..0098 merged proxies (root children, no mesh name) hidden on templates with a _LOD chain.
  Note LOD0 of rigid weapons is spread over several vertex pools (by vertex format); the preview already draws visible parts of all pools.
- 8df6d52 In place: rebase reference must carry deforming bones (cam_anim was picked), gameplay clips get no rebase,
  `MeshPreview.RemoveRootMotion` strips ROOT XZ travel per frame.
- Todo: REA grenade smoke colour, trail_bolter_smoke faintness, ratio, blend_add, exposure/light scale, genOnFly;
  untyped flesh emitter (part_mag_ee_bolter_flesh); VAT blood splashes; SELF_DISTORTION render; degNear on trails/meshes.

## vfx3 part 3 (Grok Bot, 2026-10-27)
- 1f9ffce coverings: `em_covering_*` entries (no typeParams/visuals/emission, only `physics.cdt.coveringInteraction.radiusScale`)
  are surface painters (coveringMask layer of nearby surfaces), not particles. Parsed to `EffectDefinition.Coverings`
  (`CoveringDef`: kind, radiusScale, isVisible) and shown as a note in the VFX panel; no longer `Unsupported`.
  Corpus: 27 (blood r1 x15, water r1 x6, mud r1 x3, blood r0.4 x2, blood r0.6 x1). part_mag_ee_bolter_flesh = blood r0.4 on HEAD.
- 28d731d grenade smoke colour: `seq_fin_expl_g` is channel-packed (R dark shading, G mid, B hot/fire mask), so drawn as RGB it
  is blue. `visuals.textureTintControl` (@0x143645278, field +0x140, 0..1) is set to 0 on most of its users ->
  texture used as luminance (`EmitterDef.TextureTint`, sprite shader mix(lum, rgb, tint)); absent = 1 (unchanged).
  Capture: browser_run_shots/vfx3/tint/grenade_tint_cmp.png, grenade_tint_zoom.png.
- Not implemented (data noted): `additionalContrast` (0..400, 2969 non-zero) + `contrastColorKey` [A,R,G,B] (e.g. grenade
  37.53 / orange 246,102,14): looks like a hot-colour ramp on bright texels (fire core). Shader is GPU-side, not REA'd.
  `applyColorGradients` (default on, 61 x False) feeds PART_CPU_EMITTERS_COLOR_GRADIENTS; also GPU-side.
- trail_bolter_smoke: spawned by `EcsComponentPjlShotSfx.sfxTrailsGlow*Uids` on bolter_bullet (firearm_describer.sso), i.e.
  it follows the projectile. Faint by data: blend add, colour ARGB alpha<=113 (x alphaMult 2), rgb 38..177, intensity 1x2,
  texture trail_smoke_hard is a thin wisp (mean alpha 17), and degNear radStart 2 -> radEnd 20 scaleAlpha 0 fades it near
  the camera. Not a bug; tcIn scale [0.025,1] (Clamp) is not applied to trails (corpus: 0.2/0.1/0.01 x 1 common).
- VAT blood splashes (not implemented, findings): splashes_vtxa_lwi parts splasheNN map to solid_blood.td materials splash_NN
  (shaderType shader_blood_splash; floatParam0-2 bounds min (-0.5,-0.5,-0.0624), 3-5 scale (1/255,1/255,0.001397), 6 = 120,
  7 = 60/119, tex1 albedo = splash_5_v2_anim_data (pos), addInfo _n (normals), tex2 addInfo _d (precision low byte?)).
  Tri-soup meshes (splashe05 19086 verts); UV1 u = (col+0.5)/2400, v takes 8 values (119k+0.5)/951 = 8 row bands.
  The pct is downsampled (2400x471, `downsampled: true`), bands are 58.875 rows. Decoded positions at the band rows do not
  reproduce the rest mesh (best 0.10 m mean error over all perms/frames) and triangles are only coherent at some rows,
  so the layout/encoding is still unknown. splashe06/08/10/... are 1-tri placeholders.

## UE verification: constructors + physics (2026-10-27, ue_verify)
Base e4ceb7d (extractor built from it: PC `Desktop\SM2_Work\Bin\SM2Extractor_e4ceb7d`). Patches: `/workspace/patches/ue_verify/`.
Captures: PC `Desktop\SM2_Work\Captures\ue_verify\`, box `/workspace/browser_run_shots/ue_verify/`. UE 5.7.4, editor not restarted.
- **Constructors → `/Game/SM2_Work/Tests/bulk_fix/<a>`** (existing `Exports\bulk_fix` exports from 01:34, exe10 = same constructor code;
  default variant PREDEFINED_PRESET_1, png only), `ue_bulk.py import <a> bulk_fix`, one at a time:

  | tpl | mesh / mat / anim s | slots = MIs | textures | bones | errors | clip root speed |
  |---|---|---|---|---|---|---|
  | tank | 89.6 / 51.3 / 87.0 | 54 = 54 | 69 | 6104 | 0 | 520.6 cm/s |
  | sniper | 69.1 / 57.9 / 60.1 | 64 = 64 | 81 | 5315 | 0 | 520.6 cm/s |
  | soldier | 9.8 / 41.3 / 7.1 | 41 = 41 | 53 | 2328 | 0 | 520.6 cm/s |

  - No D3D11 RHI abort, no new crash report (last is the unrelated 00:43 62B0), no `LogRHI: Error` in the log. Editor peaked ~14 GB.
  - 0 material mismatches, 0 bad texture params, 0 missing files. Looks: one armor set each, no stacked pieces; tank and sniper
    bare-headed (`helmet_*_no_helmet`), sniper cloak, chaos soldier helmeted (variant.json regions match).
  - The bone count still includes the bones of hidden parts (harmless; only parts are hidden).
- **Physics → `/Game/SM2_Work/Tests/physics/<a>`**: fresh PC exports `Exports\physics\{cc_titus,cc_calgar}` (`--tex-formats png`,
  physics on; Titus `tacticus_run_v01_intercessor` 46 s, Calgar `calgar_last_stand_convoy_start_run_calgar` 40 s), then
  `ue_bulk.py import <a> physics`, then `ue_sm2_physics.py <json> <mesh> <folder> physasset`:
  - Titus: 1447 bones, 38/38 MIs; 34 jiggle (0 missing) + 922 helper bones (0 missing). Calgar: 1596 bones, 44/44 MIs; 3 + 1294 (0 missing).
  - `PA_cc_titus_sm2`, `PA_cc_calgar_sm2` created (not assigned).
  - Motion check `tools/ue/ue_phys_probe.py <json> <anim> [samples]` (local swing vs frame 0, per jiggle group / sim mesh):
    Titus 32/34 jiggles move (max 95°), every helper moves (tabards 25-42 cm, seals 26-38 cm); Calgar 3/3 jiggles, cloak
    1024 helpers up to 161 cm, belts/ribbons 41-52 cm. Frame captures (ue_cap_lit pose → capture → export) show the tabards,
    purity seals and the cloak following the run with no exploded vertices.
  - AnimDynamics / Chaos Cloth live setup: still not scriptable. 5.7 exposes `AnimGraphNode_AnimDynamics`, `ChaosClothAsset` etc. as
    classes, but no AnimGraph node or cloth-asset creation API (`SkeletalMeshEditorSubsystem` has only create/assign physics asset).
    The bake is what plays; the json `ue` blocks remain the manual recipe.
- **Fixes (this stage):**
  - `ue_sm2_physics.py` crashed with `KeyError: 'name'`: the sidecar writes the jiggle name as `Name` (C# member name). Reads both.
  - Clip import resampled the 60 Hz bake to the FBX's 30 fps (Titus 28 frames instead of 57 keys). The shared `sm2_anim_import`
    block (ue_import_anim.py, ue_anim_survey.py, ue_sani_fix.py) now sets `custom_sample_rate` from the `{model}.physics.json`
    `baked[].rate` for baked clips: Titus re-import → 56 frames / 57 keys (`Tests/physics/cc_titus/Anims60`).
    The `Anims` imports in `Tests/physics/*` are the 30 fps ones from before the fix.
  - `ue_cap_lit.py fit`: frames the whole skeletal mesh from its imported bounds. The actor bounds cropped the constructors to shin..shoulder.
- **Open:**
  - Calgar cape shows a diagonal red/white stripe pattern in UE. It is already in the no-physics bulk capture
    (`Captures\bulk\cc_calgar`), so it is a material/texture issue, not physics. Not investigated (REA the cloak material next).
  - A few spiky triangles on the Calgar cloak hem late in the clip (t≈0.9). Cloak has 1133 free particles but helpers are capped at 1024,
    and thin-strip clusters swing up to ~180° locally (polar fit on near-collinear particles). Matches the ≈74 % figure; not fixed.
  - Baked clips are still not loop-seamless; no in-game or browser side-by-side yet.

## vfx3 part 4: particle shader bytecode (Grok Bot, 2026-10-27 05:15 KST)

Notes only. No code changed this round.

**Where the shaders are**
- `default.pak` → `shader_cache/prebuild/pc/*.sdc`. Relevant files:
  - `common_pc_dx12_particles.sdc` (CPU sprites)
  - `common_pc_dx12_particles_gpu*.sdc`
  - `common_pc_dx12_trail.sdc`, `common_pc_dx12_lightning_trail.sdc`
  - `pc_dx12_sfx.sdc` (mesh particles: SFX_* tints/gradients)
  - `pc_dx12_combine_material.sdc`, `pc_dx12_distort.sdc`
- PC copy: `Desktop\SM2_Work\Raw\shaders_vfx\` (extracted with `SM2Extractor_e4ceb7d -f *particles*.sdc ...` from `D:\Steam\steamapps\common\Space Marine 2\client_pc\root\paks\client\default\default.pak`).
- Box copy: `/workspace/vfxsh/*.sdc`.

**How to unpack and read them**
- Unpack: `python3 /workspace/mat/sdcx.py X.sdc outdir` gives `NNNNN_k.dxbc` (DXIL containers, zlib records).
  - Box output: `/workspace/vfxsh/dx_*`.
  - `particles_gpu.sdc` (30 MB) has a different header and sdcx.py fails on it; not unpacked.
- Disassemble with names (cbuffer layouts, resource names): `LD_LIBRARY_PATH=/home/box/tools/dxc/lib /home/box/tools/dxc/bin/dxc -dumpbin f.dxbc`.
  - Output: `/workspace/vfxsh/ll_*`.
- Readable GLSL: `/home/box/tools/dxil-spirv/build/dxil-spirv f.dxbc --glsl --output f.glsl`.
  - Output: `/workspace/vfxsh/gl_*`.

**Sprite pixel shader**
- Reference shader: `particles 00114_2` (lit color PS). Per-emitter data is `PART_EMITTERS_SHARED_DATA`, a float4 buffer with 6 float4 per emitter, indexed by `TEXCOORD_2.x & 0xffff`.
- CPU-side mirror: a global array at `0x14400c7ec` with stride 0x60 and 6144 entries (init at `FUN_140175824`, upload near `0x141854211`). The packer has not been found yet.
- Shared-data slots, read from the shader:
  - `[1].rgb` = contrastColorKey in raw 0..255; `[1].w` = additionalContrast. Inferred from value ranges, but very likely.
  - `[0].z` = factor t, unconfirmed (see the formula below).
  - `[0].x` = alpha-mask softness.
  - `[2].x` = alpha-mask distortion amount.
  - `[3].x` = flags: 8 = distortion texture, 16 = alpha mask, 32 = no base, 64 = lit, 1024 = soft alpha.
  - `[4].x` = final intensity multiplier.
  - `[4].y` = flowmap strength.
  - `[4].w` = colour-gradient row (int; <0 = none).
- Formula, exact from the shader (tex = flipbook lerp of 2 frames by `fract(TEXCOORD_11.z)`, V = vertex colour TEXCOORD):
  ```
  if (row>=0) { l = dot(tex.rgb, (0.2127,0.7152,0.0722))*tex.a; tex = gradTex(l, (row+0.5)/1024) * l; }   // PART_CPU_EMITTERS_COLOR_GRADIENTS, 1024 rows
  boost = 1 + additionalContrast * saturate(1 - length(key.rgb/255 - tex.rgb));                         // contrast fire core
  c = tex.rgb * V.rgb * boost;  a = tex.a * V.a;
  rgb = c + t*(t*V.rgb*tex.a - c);   // t = shared[0].z
  rgb += litTerm*a; rgb *= maskAlpha * shared[4].x * ...;  alpha out = a*mask (premultiplied-style)
  ```
- What t means is still open. If t = textureTintControl, then tint=1 means vertex colour × alpha only, and tint=0 means the full texture colour. Under that reading, `seq_fin_expl_g` with tint 0 would render blue (its B channel is the hot fire channel; checked visually, BC7 so no channel swap).
- Most likely `_g` textures are meant for the colour-gradient path (`applyColorGradients` defaults to True; corpus has False ×61 only). But no gradient data was found anywhere: there is no `colorGradient` key in any .sfx/.td, and `part_data_color_gradients` / `partEMITTER_CPU_COLOR_GRADIENT_REF` is a runtime registry.
- So the current tint=0 → luminance heuristic (0010) stays. The next step is to find the gradient source: `colorGradient` field at sfx_part +0x138 (serializer `FUN_1417c6988`, type `FUN_142746030`) and `colorGradientRef` (0x143645f38, xrefs `0x1417aebbb`...).

**Next steps**
1. Implement the contrast boost above in `EffectRenderer` SpriteFs: uniforms `uKey` (contrastColorKey/255) and `uContrast`.
   - It is safe now, but should start behind `SM2_VFX_CONTRAST=1` until it is checked against the grenade.
2. Decide what t is by finding the CPU packer: search for writes of stride 0x60 into `0x14400c7ec`, or into the per-frame copy around `0x141854211`.
3. Trails: `trail.sdc` has `CB_PASS_TRAIL`, `CB_ANIM_TRAILS`, `TRAIL_DIFF`/`TRAIL_DISTORT`/`TRAIL_NORMAL`/`TRAIL_ALPHA_MASK_TEX`/`TRAIL_DYNAMIC_MASK`, `TRAIL_DEGR` (degNear?), `TRAIL_FLATLIGHTING`.
   - Read the tc-scale and degradation code there (`/workspace/vfxsh/gl_common_pc_dx12_trail`).
4. VAT (`shader_blood_splash`): probably in `pc_dx12_sfx.sdc` (mesh particle VS with `CB_OBJ_MATERIAL_DATA`).
   - Find a VS that reads two textures with `floatParam6`/`floatParam7`-like frame counts (120 / 60). Still unsolved.

## vfx3c: additionalContrast smoke boost (Grok Bot, 2026-10-27 14:00 KST)
- Box rebuilt from scratch today: .NET 8.0.427, Temurin 25 `/home/box/tools/jdk25`, Ghidra 12.2 `/home/box/ghidra`, rizin 0.9.1 `~/.local/bin`,
  dxc v1.9.2609 `/home/box/tools/dxc`, dxil-spirv built at `/home/box/tools/dxil-spirv/build`. Ghidra re-analysis of sm2_retail.exe running into
  `/workspace/rea/ghidra_proj2.gpr`, **program name now `sm2_retail.exe`** (log `/workspace/rea/ghidra_analyze.log`).
- **REA evidence:** strings `"additionalContrast"` @0x143645248, `"contrastColorKey"` @0x143645260 (next to `"textureTintControl"` @0x143645278).
  sfx_part typeParams serializer (0x14178d437 / 0x14178d48b): additionalContrast = float +0x12C (`FUN_142745110`), contrastColorKey = colour
  +0x130 (`FUN_1427456a0`). Same visuals block as textureTintControl, so read from `visuals.*`.
- **Code:** `EmitterDef.AdditionalContrast` (default 0, clamped >= 0) and `EmitterDef.ContrastColorKey` (raw RGB 0..255 from [A,R,G,B]; alpha dropped
  because the PS reads `PART_EMITTERS_SHARED_DATA[1].rgb` only). `EffectRenderer` SpriteFs: `uKey` (= key/255), `uContrast`,
  `tex *= 1 + uContrast * saturate(1 - length(uKey - tex))` — the particles.sdc PS formula from vfx3 part 4.
  Opt-in: `SM2_VFX_CONTRAST=1` (`EffectRenderer.ContrastEnabled`); off = unchanged output.
- **Heuristics:** the boost is applied to the tint-mixed texture (after the textureTintControl luminance heuristic) and before vertex colour/exposure;
  the gradient path (applyColorGradients) that precedes it in-game is still missing, so results on `_g` flipbooks are approximate.
  Shared-slot mapping ([1].rgb key, [1].w contrast) is still inferred from value ranges (vfx3 part 4).
- Tests: `ContrastTests` (3). SM2.Assets 116, SM2Extractor 42, LibSaber.Tests 5 — all pass.
- **sdcx.py recreated** at `/workspace/mat/sdcx.py` (outside the repo): scans zlib records, carves DXBC containers by header size, writes
  `NNNNN_k.dxbc`. The header/index is not decoded (heuristic); record numbers may differ from the lost original. It now also unpacks
  `particles_gpu.sdc`. Box results: particles 140 rec/541 containers, particles_gpu 385/832, trail 311/949, distort 6/24, sfx 661/1943
  in `/workspace/vfxsh/dx_*`; dxc -dumpbin and dxil-spirv read them (resource bindings show PART_EMITTERS_SHARED_DATA t8 and
  PART_CPU_EMITTERS_COLOR_GRADIENTS t12, space2).
- **Still needed:** browser capture of the grenade with SM2_VFX_CONTRAST=1 vs off, and an in-game comparison, before turning it on by default.
- Workflow notes: the user (2026-10-27) allows heavy PC work during gaming if it doesn't disrupt (hidden + idle). Push agent now `bc-65aed255-5102-5266-bcbd-f60b85b14984`.

## 2026-10-27 14:35 KST — REA tool = morluto/rea (install + tests)

- User: the REA tool for SM2 is **https://github.com/morluto/rea** (rea-agents 6.8.0; CLI `rea`, stdio MCP `rea mcp`, 229 tools). The old `user-rea` plugin note is obsolete.
- Installed on the box: clone `/home/box/tools/rea`, CLI `/home/box/tools/rea-cli/node_modules/.bin/rea`, private Node 24.22.1 `/home/box/tools/node24`, Ghidra **12.1.4** `/home/box/tools/ghidra_12.1.4_PUBLIC` (REA rejects 12.2). `/home/box/ghidra` 12.2 and `ghidra_proj2` untouched.
- Tests: doctor healthy with 12.1.4 + JDK 25; `/bin/ls` decompile OK (~15 s); `sm2_retail.exe` via existing-project reuse → `provider_timeout` at 120 s (project not yet saved by the running 12.2 analysis).
- Next: give REA a long `REA_GHIDRA_STARTUP_TIMEOUT_MS` (e.g. 6 h) or prepare a 12.1.4 project for reuse after the current 12.2 job ends. Details/MCP config: `docs/handoff/WORKFLOW.md`.

## 2026-10-27 (KST) — vfx3d: trail shader, shared-data packer, VAT search

**Trail VS (proven, `pc_dx12_trail.sdc` 00001_1, CB_PASS_TRAIL = cb4):**
- d = |vertex − camera| (cb1[3]). near = saturate((d−DEGR.y)·DEGR.x), far = saturate((d−DEGR.z)·DEGR.w).
- colour k = lerp(1, OFFSET.z, far)·lerp(MISC.w, 1, near); width = lerp(MISC.y,1,near)·w·lerp(1,MISC.z,far);
  rgb *= saturate(d²/(MISC.x+1e-4)); alpha = a·k·(width/max(width,pixelMin)).
- UV = perPointUV + MAPPING.zw + rot(...)·MAPPING.xy → texcoord scale DOES apply to trails.
- Implemented: TrailDef.TexScale (`transform.scale`, behind `SM2_VFX_TRAIL_TC=1`), DegNear/DegFar per-vertex fade
  (width/rgb/alpha; scale inside radStart for near, beyond radEnd for far), honours `EffectSprites.DistanceFades`.
- HEURISTIC: mapping of sfx JSON fields onto DEGR/MISC/OFFSET slots (not traced on the CPU side).

**PART_EMITTERS_SHARED_DATA packer (proven):**
- Init 0x140175820 ("part_cpu_data_shared"), buffer 0x14400c7ec stride 0x60, counter 0x14409c7ec, CS 0x14409c7f0, dirty 0x14400c4e8.
- Slot allocator 0x1417e4529..0x1417e47b3 calls packer FUN_1417e1e50 (call 0x1417e473e); upload memcpy 0x90000 @ 0x141854209.
- Packer: [0].z ← desc+0x4e8 (0x1417e21d1/0x1417e21d7) = typeParams(+0x3a8)+0x140 = **textureTintControl**;
  [1].xyz ← desc+0x4d8/4dc/4e0, [1].w ← desc+0x4d4 (0x1417e20d6..e6) = key/contrast.
- Exact shader formula (t=tint): hdr = c0 + t·(t·vColor.rgb·tex.a − c0), c0 = tex.rgb·boost·vColor.rgb.
  Behind `SM2_VFX_TINT_EXACT=1` because the gradient path is still missing.

**VAT:** not in `pc_dx12_sfx.sdc` (574 VS scanned; TEXCOORD1 + 2-texture VS are wind/foliage). Likely another .sdc. Open.

## 2026-10-27 (KST) — vfx3e: VAT blood-splash VS found, sprite corner math, leads

**VAT VS (proven):** in `pc_dx12_combine_material.sdc` (box unpack `/workspace/vfxsh/dx_pc_dx12_combine_material/`),
records 00000_0/00000_1/00001_1/00001_2 (VS; inputs V_POSITION int4, V_TEXCOORD0, V_TEXCOORD1, SV_InstanceID).
Exe permutation string `COMBINE_MATERIAL_SHADER0_BLOOD_SPLASH`. Not in pc_dx12_sfx.sdc (prior guess wrong).
- uv1 *= cb7[1].w; frame: flags&25==25 -> animStage·(frames−1) (combineMtlAnimControl); else per-object anim
  (start/len/rate, clamp or fract, optional reverse) or free-running frames·frac(seed/frames) + fp6·time (cb0[23].y).
- f0=floor(f), t=frac; v(f) = bands<2 ? −f/frames : uv1.y − frac(f/frames)/bands; samples rows f0 and f0+1, lerp t.
- pos = fp0..2 + rgb·255·fp3..5 (8-bit only; the `_d` texture is NOT read by the VS); normal = rgb·2.007874−1.007874.
- Then instance 3x4 transform. cb3 rows: [6].xyz min, [6].w/[7].xy scale, [7].z rate (fp6=120), [7].w frames (fp7),
  [8].z bands, [18].x pos tex, [19].y normal tex. HEURISTIC: row 6 = floatParam0 (inferred from matching values).
- Why earlier decode failed: rows are per-frame (1 texel row = 1/(frames·bands)), counted *downward from uv1.y*,
  and the shipped pct is downsampled (471 vs ~952 rows), so frames must be sampled with bilinear v, not band rows.
- Code: `Effects/VatDecode.cs` (pure helpers + tests). Not yet wired into the renderer.

**Sprite quad (proven shader-side, common_pc_dx12_particles 00024_0):** corners are ±1 (const arrays −1,−1,1,1 / −1,1,1,−1)
times per-vertex sizes V_TEXCOORD.y (x) and |V_TEXCOORD.z| (y), plus pivot offset 0.99608−2·colorAlpha.
So the size the CPU writes is a half-extent; whether CPU writes size or size/2 is NOT traced (open).

**degNear on sprites:** sprites are CPU-expanded; no shader proof. Data (degNear scaleAlpha 0, 2→20) only makes sense
with the trail-VS semantics, so the fix is behind `SM2_VFX_DEGNEAR_FIX=1` (HEURISTIC).

**Leads (not resolved, Ghidra headless died OOM before save — log line 326 — so rizin only):**
- Field-name string xrefs (all are JSON (de)serialisers, not runtime use): ratioDependency 0x143645320 (refs 0x141790409…),
  ratioXY 0x143644f28 (0x141786eb1…), blend_add 0x14363ab78 (0x141751d81, 0x14178e041…), genOnFly 0x143645d38 (0x1417acdc6,
  0x1417ae934 writes to [rsi+0x9b0]…). Next: follow these desc offsets into the runtime like the shared-data packer.
- exposure 1/250, light ×100, distortion strength: not traced this pass.

## 2026-10-27 (KST) — vfx3f: VAT hooked into the browser (SM2_VFX_VAT=1)
- Reader: `TemplateMeshReader` now keeps UV set 1 (`ExportedMesh.Uvs1`, × submesh UvScaling[1], no flip — HEURISTIC,
  mirrors the VS `uv1 * cb7[1].w`). EffectMesh vertices are pos/normal/uv/uv1 (stride 40, attrib 6 `aTex1`).
- `VatDecode.Parse(td)` reads floatParam0..7(,10) and the `*anim_data*` / `*anim_data*_n` textures from shader_blood_splash
  materials (HEURISTIC key/name matching; bands default 8). `EffectMeshMaterial.Vat` carries it.
- Mesh VS: with `SM2_VFX_VAT=1` and the position texture loaded, it reproduces the combine_material VS decode
  (two rows f0/f0+1, lerp, pos = min + rgb·255·scale, normal = rgb·2.007874 − 1.007874); textures via `LinearTexture` (units 3/4).
- Frame = `StageFrame(particle normalised age, frames)`, i.e. animStage := age/life — HEURISTIC (CPU source of animStage untraced).
- Not verified visually (no splash assets on the box); `EffectRenderer.VatDraws` counts VAT draws for checking.
  Possible mismatch: reader bakes node transforms into positions, VAT positions are object-local.

## vfx3g — 2026-10-27 (KST): VFX flag verification (headless `--vfxbench`, llvmpipe, box)
Captures: `/workspace/browser_run_shots/vfx3_verify/` (PC copy `Desktop\SM2_Work\Captures\vfx3_verify\`); OFF top / flag bottom.
- `SM2_VFX_VAT=1` — **was broken at 9474df0** (`splash_vat_broken_9474df0_cmp.png`: long spikes/streaks), **fixed here**
  (`splash_vat_fixed_cmp.png`: droplet spray that gathers into the splash shape; VatDraws(cum) = 59 on fx_mgs_hierophant_bomb_ee).
  Bugs fixed (all still behind the flag):
  1. Every part used the first td material (`splash_05`): solid_blood.td has one material per part (`splash_07` ↔ part
     `splashe07`, …) with its own bounds/frames/texture (`splash7_v2_anim_data`, 9, 12, 17, 18, 19). New
     `VatDecode.MaterialBlockFor(td, partName)` (HEURISTIC: name match only) + tests. Extra textures extracted to PC `Raw\vfx\tex8`.
  2. Channel order: the pct is ARGB8888 = DXGI 87 B8G8R8A8 (`TextureDecode`) and decodes to raw bytes → sample `.bgr`.
  3. Texel-exact `texelFetch` (1 column per vertex; bilinear u blended neighbours) and `fract(v)` wrap (band 0 goes below 0
     in the VS formula; wrap sampler assumed — HEURISTIC).
  Offline check (python, splashe05..18): decoded triangles stay coherent (edge p99 ≈ 1.2 cm vs rest 1.0 cm).
  Still open: frame source (animStage = age/life, heuristic); rest-pose match is approximate (median ≈ 3 cm); node-transform
  offset not observed at this camera.
- `SM2_VFX_CONTRAST=1` — frag grenade: very subtle (mean |Δ| 0.07/255), slightly crisper core/sparks; plausible, low impact.
- `SM2_VFX_TINT_EXACT=1` — frag grenade: visible change (mean |Δ| 0.46): first frames gain orange/blue tints in the fireball
  and smoke. Plausible per the REA'd tint, but the blue cast on early smoke looks suspicious — needs an in-game reference.
- `SM2_VFX_TRAIL_TC=1` / tint on `trail_stalker_bolter_smoke` — **inconclusive**: the bench emitter is stationary, the trail
  builds 6–45 quads but renders ~invisible (max pixel Δ 5). Needs a moving-emitter capture (bench has no motion path).
- `VfxBench` per-frame lines now print `vatDraws(cum)`; `GlPreview.VatDraws` exposes the counter.

## 2026-10-27 14:55 KST update — Unreal MCP switched to EngineWire (Cursor Agent)
- Installed **EngineWire / MCP Automation Bridge v0.5.30** (latest stable; https://github.com/ChiR24/Unreal_mcp, MIT). 0.6.0-beta-f (first release actually named EngineWire) is a prerelease and was skipped.
- No prebuilt binaries are released; built with `RunUAT BuildPlugin` (UE 5.7.4, VS 2022 17.14, 3 min 36 s, success) in `Desktop\SM2_Work\enginewire\built`, installed to `K:\Unreal Projects\UE_VFX_57\Plugins\McpAutomationBridge`. `.uproject` unchanged (plugin is EnabledByDefault).
- Native MCP HTTP on `127.0.0.1:3000/mcp` with capability token (`DefaultGame.ini`, backup `DefaultGame.ini.bak1`; token file `C:\Users\Revaptor_FX\.enginewire_token`, not in git).
- Verified after editor restart: 23 tools listed, `execute_python` listed 5164 assets under `/Game/SM2_Work`, wrong token → 401, `uerun.ps1 ue_verify_layout.py` runs via EngineWire in 1.6 s (UEMCP: 42 s).
- New `tools/ue/enginewire_call.py` and `tools/ue/uerun.ps1` (EngineWire first, UEMCP fallback). UEMCP stays installed; `Scripts\uerun_uemcp.ps1` / `uerun.ps1.bak1` keep the old runner. Node server 0.5.30 also installed in `enginewire\server` (optional).

## vfx3h (Ghidra REA, sm2_retail.exe, 2026-10-27)
Evidence from decompiling ghidra_proj3 (scripts /workspace/rea/scripts/DecompAt.java, DispScan.java). Docs only, no code change.
- genOnFly (PROVEN): emitter+0x9b0 is an array {ptr,count@+0x9b8} of 12-byte entries {key, emitterNameId@+4, numParts@+8} (parser FUN_1419b9a50 reads "numParts"). genOnCDT is +0x9c0 (16-byte entries), genOnDie is +0x9d0 (12-byte entries). Runtime: FUN_1417e4e10 (CPU) and FUN_1417e5060 (GPU) match each entry's nameId against child emitters (child+0x6bc CPU, +0x34 GPU) and attach the child to the parent. GPU attach is FUN_1417e5b70 with mode 0=fly/1=CDT/2=die, at most 3 slots at +600/+0x290, log "attached GPU emitter". CPU attach is FUN_1417e5a20, 12 slots at +200. So genOnFly is not a rate: it spawns child emitter `name` attached to each flying parent particle, numParts per parent (HEURISTIC that numParts is per-particle; not traced further).
- ratioDependency (PARTIAL): at emitter-desc +0xc0 (FUN_141796980 → FUN_141786e20): dependencyType at +0 ("none"=0, "Velocity"=1, "Age"=2; FUN_141787230, strings at 143567450/1436443ec), ratioXY curve at +8 (FUN_1417867c0; constExpr/ageSpline). Runtime consumer not located.
- blendType (PARTIAL): FUN_141751c70 gives blend=0, add=1, blend_add=2 (writer FUN_141752030 also has 3). The D3D12 blend-desc consumer was not located within the time box; the mapping in the existing code stays HEURISTIC.
- animStage: serialised in FUN_14179fdd0 group (FUN_14179edc0/FUN_14179f290 at desc+0x20). Runtime setter not located, so VatDecode.StageFrame age/life remains HEURISTIC.
- Exposure 1/250, light x100, distortion strength, and trail DEGR/MISC/OFFSET CPU mapping: NOT resolved (only shader define strings DISTORTION_* / EXPOSURE=%g found). These remain HEURISTIC.
- VFX estimate corrected to ≈ 72% (honest; earlier 80–82% figures were optimistic).

## vfx3i (Ghidra REA follow-up, 2026-10-27)
Scripts: /workspace/rea/scripts/Callers.java (new), DispScan.java. Outputs: /workspace/rea/vfxi_1.c, vfxi_2.c. Docs only. Nothing proven was worth a code change, so there is no env flag.
- Emitter desc layout (PROVEN): typeParams variant at emitter+0x1a8 (FUN_1417adea0 → FUN_1417a37c0). Type int at +0x1a8: 0=default (FUN_141793d40), 1=distortion (FUN_141796980), 2=lwiGeom (FUN_1417a1ef0). Payload at +0x1b0. Runtime code reads emitter desc via [inst+0x450].
- blendType (PARTIAL): default.visuals (FUN_14178ce70 at payload+0x1f8). blendType sits at emitter+0x48c, isBlendAlphaMultiply at +0x490. Only reader in 0x1417b0000–0x141a00000 is FUN_1417e1e50 (GPU emitter constants). It sets flag bit 0x20 when blendType==1 ("add") and clears bit 0x400 when isBlendAlphaMultiply && blendType!=1. blend_add (2) gets NO separate bit there, so it is handled like "blend" for these flags. The D3D12_BLEND_DESC builder is still not found; the current mapping stays HEURISTIC.
- ratioDependency (PROVEN for distortion): emitter+0x270 (payload+0xc0). FUN_1417e29b0 binds the pointer emitter+0x270 into a GPU curve slot (inst+0x240, tag 0x2000000000000), the same as the other curves at +0x508/+0x580. So ratioXY is evaluated on GPU. Emitter flag 0x1000 (inst+0x6b8) is set unless dependencyType==none, or (field[2]==0 and ratioXY ends ==1.0) (1417e3f73). HEURISTIC: it scales the sprite X/Y aspect by velocity or age.
- Distortion strength: distortion.power at payload+0x98 = emitter+0x248, curve parser FUN_14177c520. Reader FUN_1417daac0 references +0x248 but was not decoded (size 5033).
- animStage: in the lwiGeom payload, FUN_14179fdd0 at payload+0x178 = emitter+0x328 (+0x20 inside). FUN_1417e9480 writes inst+0x328 = 0 (an instance field, not proven to be animStage). The runtime setter is still unproven, so age/life remains HEURISTIC.
- Exposure 1/250, light ×100, trail DEGR/MISC/OFFSET: still NOT resolved. FUN_1417e1e50 writes 1/max(|x|,1e-6) of emitter+0x3a0 (default) or +0x1b0 into GPU constants (+4). That is a reciprocal, but its link to exposure is UNPROVEN.
- VFX estimate stays ≈ 72%; the genOnFly/ratio/blend layout knowledge does not yet change rendering.

## vfx3j (Ghidra REA follow-up, 2026-10-27)
Outputs: /workspace/rea/vfxj_1.c, vfxj_3.c. Docs only. Nothing new reached "proven + implementable", so this round has no code, env flag or tests.
- FUN_1417daac0 was a FALSE POSITIVE: its +0x248/+0x1a8 hits are RBP stack-frame slots (1417db5fa, 1417db3ed), not emitter fields.
- distortion.power has no CPU reader. FUN_1417e29b0 binds the whole typeParams payload to the GPU: for type 1 (distortion), slot inst+0x200 ← desc+0x1b8. For type 0, inst+0x200 ← desc+0x3a8 (visuals) and inst+0x220 ← desc+0x630. Plus inst+0x250 ← desc+0x310 (default only). So power, like ratioXY, is evaluated on the GPU from the uploaded curve block. The scalar meaning is still HEURISTIC.
- Blend: the particle shader permutation table FUN_142450650 defines SFX_ALPHA_BLEND (perm word +0x205 bit0) and SFX_BLEND_ADD_NO_ALPHA (bit1). Trails have the matching TRAIL_BLEND_ADD_NO_ALPHA (FUN_1424e32c0). HEURISTIC, link not traced: blend_add selects the "add, no alpha" variant (ONE/ONE ignoring src alpha) and blend selects ALPHA_BLEND. I still have not found the D3D12_BLEND_DESC builder. The full-binary +0x48c scan was skipped as too noisy; the only real reader is FUN_1417e1e50 (vfx3i).
- Trails (PROVEN wiring only): emitter+0x9e0/+0x9e8 is a list of 24-byte trail-name strings. At runtime FUN_1418bfcc0 matches a trail object's name (trail+0x10) and registers trail+0x178 into the emitter instance list inst+0x3d0. TRAIL_MISC/TRAIL_DEGR/TRAIL_OFFSET/TRAIL_MISC_2 are only shader-constant name registrations in FUN_1421ca290 (globals 145025180/1450251a8/145025220/1450252c0). The CPU packing of these constants was not reached.
- VFX estimate remains ≈ 72%.

## vfx3k (shader disassembly + Ghidra, box only, 2026-10-27)
Done on the box only: no game launch, nothing touching EAC. Tools: dxc -dumpbin (/workspace/vfxsh_dis), dxil-spirv --glsl (/workspace/vfxsh_glsl/trail), Ghidra scripts DataRefs.java/Callers.java/DispScan.java (outputs /workspace/rea/vfxk_*.c). Docs only. Nothing new reached "proven + not yet implemented", so this round has no code or env flag.
- CB_PASS_TRAIL layout (PROVEN, reflection in trail.sdc 00020_0, bound at cb4): MAPPING@0, ALPHA_MAPPING@16, ALPHA_MASK_PARAMS@32, COLOR_FADEOFF@48, MISC@64, DEGR@80, PS_REG_TRAIL_EMISSIVE@96, PS_REG_TRAIL_CONST_COLOR@112, OFFSET@128, SDR_MAPPING@144, SDR_ROTATION@160, FLATLIGHTING[4]@176, MISC_2@240, TRAIL_PROJ_PARAMS@256 (blendMaskScale/Bias, height, yOffset, normalMapScale, parallaxScale, zFadeScale, angleFadeScale/Bias, flags).
- Shader use (PROVEN, trail 00001_2 GLSL). d is the vertex→camera distance, as in our existing EffectTrails implementation.
  - The two ramps: rN = sat((d-DEGR.y)*DEGR.x), rF = sat((d-DEGR.z)*DEGR.w).
  - Width scales by lerp(MISC.y,1,rN)*lerp(1,MISC.z,rF). Colour/alpha scale by lerp(MISC.w,1,rN)*lerp(1,OFFSET.z,rF).
  - OFFSET.x is subtracted from TEXCOORD5.y, the along-trail coordinate.
  - MISC.x is a squared-distance normaliser: sat(|v|²/(MISC.x+1e-4)).
  - MISC_2.yzw is an axis vector used in cross products.
  - Most frequent component is OFFSET.w (not decoded).
- CPU side (PARTIAL): the CB_PASS_TRAIL name table is built only by FUN_1421ca290 (DataRefs found no other refs to 145025000..+0x300). So constants are packed into a struct and uploaded by layout, not written by name.
- Trail desc layout (PROVEN, FUN_14183b3a0): renderPass +0x84, isThinTrail +0x88, visuals +0x90, trailParams +0x280 (FUN_141839b50), physics +0x3b0, degNear +0x3e8, degFar +0x400 (FUN_14181ec90, 24 bytes each), degradationRadius +0x418, optimizeBranchQuality +0x41c, nTrailData +0x420.
  - Runtime: FUN_14187cf20 copies degNear/degFar fields to the render-side struct (+0x1f8..+0x218).
  - FUN_1418c09a0 computes (x-degFar[0])/(degFar[1]-degFar[0]), i.e. a linear start→end ramp. So DEGR.x/w = 1/(end-start) and DEGR.y/z = start is consistent (HEURISTIC, the final CB pack was not traced).
- Blend, distortion power, exposure/light: no progress beyond vfx3j. Variant-bit readers were not reached within the time box.
- VFX estimate remains ≈ 72%. The existing trail DEGR implementation matches the shader; only the slot→sfx-field assignment stays HEURISTIC.

## vfx3l (shader pass sfx/distort/particles + trail packing, box only, 2026-10-27)
Disassembly: /workspace/vfxsh_dis/{pc,pg,dx_pc_dx12_sfx,dx_pc_dx12_distort}, GLSL: /workspace/vfxsh_glsl/{pc,distort}. Ghidra: /workspace/rea/vfxl_1.c. Docs only; nothing new proven and implementable.
- pc_dx12_distort is the scene distortion pass (CB_PASS_DISTORTION.DISTORT_PARAMS@0; 00000_2 PS writes the constant (0.5,0.5,1,1) = neutral offset), not particle power.
- pc_dx12_sfx is the world/decal SFX material (SFX_DISTORT_PARAMS, SFX_DISTORT_*_LAYER, SFX_BLOOM_INTENSITY, SFX_TEX_DISTORT*), not particle emitters. SFX_BLEND_ADD_NO_ALPHA variants belong to it, so they do NOT prove the particle blend_add mapping.
- Particles: the CPU path has CB_PARTICLES (view/proj, FPS_PARAMS[2]@368, SHADING_DATA[4]@400) and CB_PARTICLES_DATA (PARTICLES_TEXTURE_DATA[3072], raw). The GPU path keeps emitter params in the untyped f32 buffer PART_GPU_EMITTERS_PART_SHARED_DATA (PART_GPU_EMITTER_STATE is 404 B: data[3], simulationMask, render, alive, ...). Particle per-emitter constants are unnamed, so distortion power/ratioXY slots can't be read from reflection.
- Exposure: particles read the global COMMON_HDR_EXPOSURE_DIV_AVG_LUM (CB_COMMON @336). So exposure is a runtime camera value, not a per-effect constant. The fixed 1/250 stays HEURISTIC.
- Trail packing: readers of render-side +0x1f8..+0x218 in 0x141800000–0x142400000 are FUN_14187e8b0 (lerp of degNear/degFar fields vs +0xec.., i.e. LOD/state blend) and FUN_14189db00 (defaults: +0x1fc=255.0, +0x20c=0.5), plus unrelated hits at 141a1/141ef/141f2. The CB_PASS_TRAIL pack was NOT found. The DEGR slot mapping stays HEURISTIC.
- VFX estimate remains ≈ 72%.

## vfx3m (2026-10-27 KST) — particle blend flags 0x20/0x400 (PROVEN)
- Chain: FUN_1417e1e50 builds 0x60 B/emitter record → FUN_1417e4420 copies to 0x14400c7ec+id*0x60 (dirty _DAT_14400c4e8|=1) → FUN_1418541b0 uploads 0x90000 B as t8,space2 PART_EMITTERS_SHARED_DATA (pc shaders index id*6+row). Row3.x = flags.
- bit 0x20 = blendType(+0x48c)==add; bit 0x400 = !(isBlendAlphaMultiply(+0x490) && blendType!=add).
- particles.sdc PS 00000_0: rgb *= vertexAlpha*(0x400 ? 1 : texAlpha'); outAlpha = 0x20 ? 0 : vertexAlpha*texAlpha'. blend_add has no bit (= blend).
- Implemented `EffectBlendRea` + `visuals.isBlendAlphaMultiply` parse; opt-in `SM2_VFX_BLEND_REA=1` (PSO ONE/INV_SRC_ALPHA is HEURISTIC, implied by add alpha=0). Tests: BlendReaTests.
- Open: other rows of the 0x60 record, distortion.power/ratioXY via GPU curve slots inst+0x200..0x250, trail DEGR CPU packing.

## vfx3n (2026-10-27 KST) — bench check of SM2_VFX_BLEND_REA + 0x60 record field map
- Bench (`--vfxbench`, llvmpipe, box): `/workspace/browser_run_shots/vfx3m_blend/{off,on}/`, `*_cmp.png` (OFF top / ON bottom).
  frag grenade mean |Δ| 1.02/255 (max 186): sparks and smoke much brighter/greyer with ON (non-alphaMultiply emitters no
  longer scale RGB by texture alpha). hierophant_bomb_ee: no change (mesh-dominated). Plausible per REA, but smoke looks
  overbright → keep opt-in until an in-game reference. Data: isBlendAlphaMultiply True in 3,373 of 15,168 emitter entries.
- Caveat: earlier HEURISTIC (vfx3l) that blend_add picks the SFX_BLEND_ADD_NO_ALPHA permutation (FUN_142450650) is NOT
  modelled by EffectBlendRea (flags-only; blend_add = blend there).
- Visuals parser FUN_14178ce70: visuals base = desc+0x3a8 (anchors: blendType +0xe4→0x48c, additionalContrast +0x12c→0x4d4).
  Field offsets (desc): lighting 0x478, domainLightingTessDensity 0x47c, lightingTranslucency 0x480, lightsSensitivity 0x484,
  ambientLight 0x488, blendType 0x48c, isBlendAlphaMultiply 0x490, usePixelFog 0x491, alphaIntensity 0x4d0, gloss 0x500,
  angleAlpha.isEnabled 0x560 / alphaSpline 0x568, alphaMask 0x578, sdr 0x628, projection {isEnabled 0x6d0, attenuation 0x6d4,
  fadeVerticalEdges 0x6d8, dist 0x6dc}, temporalAA 0x6e0.
- 0x60 record (FUN_1417e1e50 0x1417e1e50.., PROVEN CPU side): r0.x alphaMask-derived (+0x620, if alphaMask on); r0.y 1/max(|x|,1e-6)
  of desc+0x3a0 / +0x1b0; r0.z textureTintControl; r1.xyz contrastColorKey, r1.w additionalContrast; r2.x alphaMask-derived;
  r2.y domainLightingTessDensity; r2.z k/projection.dist; r2.w projection.attenuation; r3.x flags (0x20 add, 0x400 !alphaMul,
  plus lighting/gloss/sdr/alphaMask bits); r3.y flags2 (0x10 ← desc+0x8f); r3.z lightingTranslucency; r3.w fadeVerticalEdges
  (clamped); r4.x lightsSensitivity; r5.xy temporalAA.
- GPU side (pc 00000_0): reads r0.x (min with 1-texA, alpha-mask softness), r0.z, r1.xyzw, r2.x, r3.x, r4.x, r4.y, r4.w.
  r4.x multiplies the whole non-additive colour (line ~800) — semantics as "lightsSensitivity" HEURISTIC (could be a lit
  permutation); not implemented. r4.y/r4.w are written outside the decompiled window (not yet attributed).
- Not done this chunk: distortion.power/ratioXY via GPU curve slots, trail DEGR CPU packing.

## vfx3o (2026-10-27 KST) — curve slots, row 4 writers, softDepth reciprocal
- FUN_1417e29b0 (0x1417e29b0..0x1417e2b4f) "curve slots" are tagged resource pointers (ptr | kind<<48), i.e. texture
  bindings, not curves. default: inst+0x200 ← desc+0x3a8 (visuals.texture; kind 1 iff payload+0x1fa set), +0x210 ← 0x580
  (alphaMask tex, kind 3), +0x220 ← 0x630 (sdr), +0x230 ← 0x508 (gloss), +0x240 ← 0x270, +0x250 ← 0x310.
  distortion (type 1): only +0x240 ← desc+0x1b8 (distortion visuals.texture, payload+8). So distortion.power is NOT here.
- Correction to vfx3n: r0.y = 1/max(|softDepth|,1e-6) and flag 0x200 = softDepth<0 (distortion: payload+0 = softDepth, parser
  in vfxh_1.c ~2190; default +0x3a0 is the same field by analogy — HEURISTIC). The reciprocal is NOT exposure. Already
  implemented as soft particles (EmitterDef.SoftDepth).
- r0.w (+0xc) = count from desc+0x29c (distortion) / +0x738 (default, enabled by +0x734); r4.z (+0x48) = half2 of the
  normalised direction (+0x73c/+0x744 or default DAT_143638ad8) when count ≤ 1.
- r4.y (+0x44) = lookup(desc+0x318 name)+0x34 (fallback +0x1c) × desc+0x398, only if desc+0x308 (FUN_1417e1e50 ~0x1417e2333).
- r4.w (+0x4c) = colour-gradient LUT row (desc+0xa04 & 0xfffff) if visuals.applyColorGradients (desc+0x430), else -1.
  PS 00000_0 ~438–450: lum = dot(texRGB, Rec709)·texA; colour = gradLUT(u=lum, row)·lum. PROVEN, but
  applyColorGradients is False in all 61 occurrences in our sfx dump → not implemented (zero impact).
- distortion.power (payload+0x98 = desc+0x248, FUN_14177c520 parser): DispScan +0x248 in 0x1417b0000–0x141a00000 found a
  0/1-tagged scalar at desc+0x240/0x248 sampled from a 64-entry LUT in FUN_1417f0c60 → particle+0xe4, but the tag at 0x240
  overlaps distortion localOffsetX (payload+0x90), so it belongs to another typeParams member. Unresolved.
- Trail DEGR: the name handles 0x145025180/1a8/220/2c0 have only the registration WRITEs in FUN_1421ca290 (DataRefs);
  the packer reads them indirectly. Dead end statically.

## vfx3p/q (2026-10-27 KST) — distortion.power + trail DEGR: no progress (stop rule hit)
- distortion.power (desc+0x248): DispScan +0x248 over 0x141a00000–0x142600000. Float readers: 141e28110/141e28b00/141e29eb0/
  141e2c500 (unrelated objects), 1424540e0 (material UV-transform packer: 0x214/0x2ec/0x3bc/0x248 = 4 texture-transform
  slots of a material, not an emitter), 1423ba150. FUN_1417f0c60 (vfx3o) is gated by FUN_141a98e80 and loops desc+0x208/
  0x218/0x228 → lwiGeom member, not distortion. The distortion fullscreen shaders (dx_pc_dx12_distort) only read CB_COMMON.
  No distortion-power consumer found statically; mapping stays HEURISTIC.
- Trail DEGR: DataRefs over 0x145025000..+0x300 → only the WRITEs in FUN_1421ca290; callers of FUN_1418c09a0
  (1418beed0, 1418bf500, 1418c1940) update trail points (0x50-byte stride), not the CB. DispScan of 0x1418b0000–0x1418e0000
  for +0xf0/+0x100 stores (MISC_2@240) gave no CB_PASS_TRAIL writer. Constants are likely set by handle through a
  generic setter, so a capture is needed (not allowed: no game/EAC). DEGR slot mapping stays HEURISTIC.

## vfx3r (2026-10-27 KST) — in-game side-by-side prep (no injection; user screenshots)
- Bench captures: `/workspace/browser_run_shots/ingame_cmp/{off,blend,tint,vat,all}/<effect>_sheet.png`,
  contact sheet `ingame_cmp_contact.png` (rows = effects, cols = off/BLEND_REA/TINT_EXACT/VAT/all+CONTRAST, d = mean |Δ| vs off).
  PC copy: `Desktop\SM2_Work\Captures\ingame_cmp\`. User reference shots go to `Desktop\SM2_Work\Captures\ingame_ref\`.
- Effects: part_mg_frag_grenade_expl, fx_bolter_muzzle_client, part_mag_chainsaw_blood_splash_red_01,
  fx_mgs_hierophant_bomb_ee, trail_stalker_bolter_smoke, part_mg_candle_smoke_hub.
- Bench caveats: muzzle/blood/candle are tiny at the fixed bench camera; chainsaw blood + candle miss textures
  (part_splash_drops_01…, flame_sml_16x8); hierophant sheet renders empty in this run and VAT shows d=0 (needs the tex8/mesh set
  used in vfx3g) — fix the bench framing before comparing those.
- GPU-capture route (user accepted EAC risk, 2026-10-27): no official no-EAC switch exists (official mod support = "mods
  detected" mode, private lobbies, EAC still on). Least-invasive offline route without modifying files: run
  `client_pc\root\bin\pc\Warhammer 40000 Space Marine 2 - Retail.exe` directly (what the Nexus Vortex extension's
  "launch without EAC — offline only" tool does) via RenderDoc 1.46 portable (`Desktop\SM2_Work\Tools\RenderDoc_1.46_64`,
  signature Valid). Not launched by the agent.

## vfx3s (2026-10-27 15:40 KST) — bench fixes + GPU-capture attempt result
- **GPU capture attempt FAILED (EAC relaunch), 15:33 KST.** `renderdoccmd capture` on
  `client_pc\root\bin\pc\Warhammer 40000 Space Marine 2 - Retail.exe` (Steam running): the injected process (PID 38920) exits
  immediately and the game relaunches itself via `start_protected_game.exe` (PID 91044) → new Retail.exe child (PID 59596)
  + `EasyAntiCheat_EOS.exe` (PID 96252, 15:33:36). RenderDoc never attaches to the real process; no `.rdc`, trigger log empty.
  The "run Retail.exe directly = offline no-EAC" route from vfx3r is **disproved** for this build. Game closed, no bypass tried.
  Remaining options are user-side screenshots (F12) into `Captures\ingame_ref\`.
- **Incident:** the launch command created and immediately deleted an empty `steam_appid.txt.check` in the game root
  (`D:\Steam\steamapps\common\Space Marine 2\`). Verified absent afterwards; no game file was modified. Agents must not
  write probe files into the game folder.
- Textures: `part_splash_drops_01`, `part_splash_central_01`, `part_rs_glow_01` (PC `Raw\vfx\tex3\pct`) and new
  `flame_sml_16x8`, `part_splash_mid_03`, `part_seq_plasma_expl_01` (+ `_vectors`/`_am`/`_nm`; extracted read-only from the
  paks with `SM2Extractor_e4ceb7d extract -f`) → PC `Raw\vfx\tex9\`, box `/workspace/vfx3_raw/tex9/pct/` (with the tex3 mips).
  Only `gpu_emitter_texture` (GPU emitter placeholder) and mesh `sfx_geom_debr_expl_stones_small` are still missing.
- `EffectCamera.Fit` (EffectTextures.cs): samples the whole capture window, ignores particles below y = −0.5 (no floor in the
  bench; HEURISTIC), centres on the median with the 25..75 % box ×1.6, min distance 0.15. Hierophant bomb was empty because
  falling debris (CPU_EMITTER_9, y ≈ −280 m) and stone02_big mesh chunks (x ≈ +33 m) set the frame; now the core renders and
  VAT shows d = 0.48 (was 0). `SM2_VFX_FITDBG=1` prints the fit + per-emitter means.
- Bench needs `SM2_VFX_MESHES=/workspace/vfx3_raw/mesh1` (the vfx3r run missed splashes_vtxa_lwi etc.). Script:
  `tools/vfxbench/ingame_cmp.sh`. Re-rendered `/workspace/browser_run_shots/ingame_cmp/` (old run kept as `ingame_cmp.bak1`),
  PC `Captures\ingame_cmp\` (old → `ingame_cmp.bak1`).
- Still open (not framing): fx_bolter_muzzle_client and part_mg_candle_smoke_hub build **0 quads** (3/9 particles alive, all
  filtered in EffectSprites.Build); part_mag_chainsaw_blood_splash_red_01 spawns **0 particles** in 0.5 s (covering-triggered?).
  Those three rows of the contact sheet are empty and not comparable yet.

## vfx3t (2026-10-27 15:50 KST) — the 3 empty bench effects
- **Muzzle + candle (0 quads): bench camera inside degNear.** `SM2_VFX_SPRDBG=1` (new, EffectSprites reject reasons) showed
  every quad culled with alphaK = 0: their sprite emitters have `optimization.degNear radStart 6 → radEnd 2, scaleAlpha 0`
  (alpha 0 at ≤ 2 m) and the tight small-effect frame put the eye at < 2 m. Not a format bug. Fix (bench only):
  `EffectCamera.NearFadeClear` keeps the eye at max(radStart, radEnd)·1.05 of any fading sprite emitter (cap 20 m) and
  narrows the FOV to keep the frame (`EffectCamera.Fov`). Now muzzle 1–3 quads, candle 1–9 quads per frame.
- **Chainsaw blood (0 particles): isVisible.** The source `drops_source2` is `isVisible = False` (life 0.001 s, genOnDie →
  cpu_drops_2 ×50, cpu_drops_3 ×2), and `EffectInstance.Step` skipped emission for hidden emitters.
  Exe: `isVisible` (string 0x143645df0) is emitter desc **+0x88** (parser FUN_1417adea0, next to emitScale/isUseParentRotate);
  DispScan +0x88 over 0x1417d8000–0x141900000: only draw-submission gates read it (FUN_1417f71b0 @0x1417f71c5 and
  FUN_1417ee5c0 @0x1417ee5f4 return when 0; FUN_1417e9480 @0x1417ea4b9 checks the parent's flag for transforms). No emission
  path reads it → hidden emitters still simulate (PROVEN for the draw gate; "no emission reader" = scan result).
  Fix: hidden emitters with attached children keep emitting (`EffectInstance.HiddenSpawn`, default on,
  `SM2_VFX_HIDDEN_SPAWN=0` = old behaviour); they still never draw. Test `HiddenSourceStillSpawnsAttachedChildren`.
  The blood effect now spawns 259 particles but still draws nothing — **correctly**: all 4 emitters (incl. cpu_drops_2/3 and
  em_covering_blood) are `isVisible = False` in the data, so the exe's draw gate skips them too. Its row stays empty by design
  (template/disabled asset; the name in fx_huge_blood_explosion etc. is only an sfx_group node).
- Re-rendered ingame_cmp (previous run → `ingame_cmp.bak2`), PC `Captures\ingame_cmp\` (old → `ingame_cmp.bak2`).
- VFX estimate ≈ 73% (bench coverage, isVisible semantics proven; no format change).

## vfx3u (2026-10-27 15:55 KST) — in-game reference comparison
- Refs (user, photo mode, 3840×2160): box `/workspace/ingame_ref/`, PC `Captures\ingame_ref\`. Side-by-sides:
  `/workspace/browser_run_shots/ingame_cmp/side_by_side_{grenade_early,grenade_late,bolter_muzzle,bolter_trail,blood_vs_hierophant}.png`
  (left = in-game crop, right = bench frames off/blend/tint/vat/all).
- 154731/154743 = frag grenade (fireball+sparks / smoke+sparks): **BLEND_REA matches best** (bright yellow-white additive core and
  spark streaks; off/tint/vat are dull, ~10× fewer bright pixels). Still missing vs game: the big orange fireball volume, smoke
  density/persistence (game smoke is thick and lit; ours thin and gone by ~1.3 s), spark count/length (game sparks are long streaks).
- 154807 = bolter muzzle flash: game shows a large orange flash + flame tongues; bench shows ~nothing in every config →
  muzzle is still broken (flash is probably the sfx_geom_muzzle mesh emitters, `meshes 0` in the log). Open.
- 154833/154843 = bolter tracer (bright yellow line) + muzzle smoke wisp: tracer is a different effect (bolt projectile trail);
  trail_stalker_bolter_smoke (pink thin quad) is not comparable. Muzzle smoke wisp matches candle-like soft smoke qualitatively.
- 155018 = chainsaw execution blood (dark red, ~RGB 110/30/25, volumetric strands): not one of our bench effects
  (part_mag_chainsaw_blood_splash_red_01 is all-hidden). Nearest = hierophant VAT splash: shape type matches (strand splash) but
  ours is far too bright/orange (RGB ~156/115/53 incl. glow) → blood colour/lighting is wrong in all configs. Open.
- Tint/VAT/CONTRAST: no visible evidence either way at these references (d ≤ 0.5). Keep opt-in.
- **Default change:** `SM2_VFX_BLEND_REA` default ON (`=0` disables); test `DefaultOnUnlessDisabled`; ingame_cmp.sh sets
  `=0` for the off/tint/vat columns so the sheets stay comparable.
- VFX estimate ≈ 74%.

## vfx3v (2026-10-27 16:10 KST) — in-game gaps
- **Muzzle flash "missing" = capture sampling, not format.** fx_bolter_muzzle_client: 11 flame emitters live 0.030–0.045 s
  (SM2_VFX_SPRDBG `quad` lines), i.e. <3 steps at 60 Hz, and the 2 s/8-frame sheet first samples t=0.125 s. New
  `SM2_VFX_STEP_HZ` (default 60) + `name@0.08` at 480 Hz shows the flash (flame_circle/cross/front, blue_smoke, 2 meshes).
  Remaining diffs: core is blue-white (blue_smoke_01 rgb 70/252/555 Add) vs orange in game; flame_circle_1/2 mesh
  emitters have no texture (`tex ''`) and draw as grey columns → mesh material for sfx_geom_muzzle still unresolved (open).
- **Grenade fireball = same cause.** main_core_01..03 + flash_01 exist only for t < ~0.18 s; first 3 s-sheet frame is 0.1875 s.
  At 480 Hz/@0.32 the fireball is there; blend/all match in-game colour+size best (off/tint/vat too pale).
- **Smoke thin:** smoke emitters are lit ('Medium/Best Quality'); bench has no scene light and uses heuristic
  `LitScale`=100. SM2_VFX_LIT=400 gives in-game-like grey smoke. Not defaulted (heuristic, needs scene light REA).
- **Blood:** the earlier "too orange" was the hierophant *bomb*, wrong reference. part_mag_chainsaw_blood_splash_red_01 has
  all 3 emitters isVisible=false (+0x88, parser FUN_1417adea0) → presumably made visible by gameplay (HEURISTIC, toggle
  not traced). Debug `SM2_VFX_FORCE_VISIBLE=1` draws them: dark red (rgb ~30,0,0 lit) matches the in-game colour; shape is
  too blobby/large (cpu_drops_3 size ~19 m at n 0.84 — size curve or scale suspect, open).
- ingame_cmp.sh now also renders `<col>/early/` (480 Hz muzzle/grenade, force-visible blood) and runs
  tools/vfxbench/side_by_side.py. Debug `quad` lines in SPRDBG. VFX ≈ 77%.

## vfx3w (2026-10-27 KST)
- Muzzle mesh: tpl part Variant (shadingMtl) -> named td block (mzl_geom in water_drops_01.td: preset sfx, tint [255,237,72,18], emissive 111, softFreshnel power 5). EffectMeshes.Material now reads named block tex/tint/softFreshnel (SM2_VFX_MESH_MTL=0 disables). Fresnel alpha in mesh FS is heuristic. Texture "phantom_hands" not extracted -> white base. Open: flame_cross_4/6/8 grey columns (rgb ~26000 saturates).
- Hidden blood emitters: part_mag_chainsaw_blood_splash_red_01 is a template (isVisible=False); visible copies are part_mag_chainsaw_*_finisher_01 (nameParent sfx_source_pt) and parent emitters2 overrides (fx_huge_blood_explosion). Bench/side-by-side now uses warrior_finisher_01; FORCE_VISIBLE dropped there.
- Blood size: emitScale = emitter desc+0x70 (FUN_1417adea0), used in FUN_1417ecc30/1417f24a0/1417f3f60 as emission-count/threshold, not size (inconclusive) -> size unchanged. alphaMask.clip.power parsed (FUN_141782a60, desc+0x88) as EmitterDef.MaskClip, opt-in SM2_VFX_MASK_CLIP=1 (no visible change).
- Finisher mesh colour: sfxMtlOverrides color constExpr [255,44,3,155] + emissiveIntensity spline 10.8->0 -> renders bright red; in-game darker (scene lighting/exposure). Open.
- Lit smoke scene light: not traceable (runtime scene/sun); LitScale=100 stays heuristic (SM2_VFX_LIT=400 closer).

## vfx3x (2026-10-27 KST)
- phantom_hands (BC7A 2048², wispy smoke) + part_muzzle_side_01 extracted read-only (SM2Extractor_e4ceb7d extract -f) → PC `Raw\vfx\tex10\`, box `/workspace/vfx3_raw/tex10/`. Muzzle rings now textured: wispy and much dimmer than the old white-base orange rings; in-game glow likely from bloom (mzl_geom bloomIntensity 111, adaptiveIntensity), not simulated. UV scroll (overrideAffixScroll speedY 11) not applied. Open.
- Grey columns = flame_cross_6/8 (emitDir x+/x-, blend+isBlendAlphaMultiply, colorIntensity 55555, alphaClip age 0→1). Root cause: alpha-clip math. particles.sdc 00000_0 (vfxsh_glsl/pc/00000_0.glsl l.118-122, 466, 873-876): clip = flag0x400 ? 0 : TEXCOORD_2.y; ac = sat((tex.a*vertexA - clip)/(1 - 0.5*clip)); rgb *= sat(ac + flag0x400); SV_Target.w = add ? 0 : ac. Vertex alpha (~0.08) is inside the clip, so these burn out almost immediately. Implemented in sprite FS (BLEND_REA path), default on, SM2_VFX_CLIP_EXACT=0 disables. Same shader writes HDR unclamped (NaN/Inf guard only) → blending is in HDR; our per-fragment tonemap stays. SM2_VFX_HDR_PREMUL=1 (opt-in, HEURISTIC) tonemaps the premultiplied contribution instead; it over-brightens, left off.
- flame_cross_4 (nameParent "tip", isEmitByNormal, isAlignSpeed) streaked +Y with no host socket. EffectInstance.SocketForwardFallback (default on, SM2_VFX_SOCKET_FWD=0 disables, HEURISTIC): the effect root stands in for the socket → local +Z. Flips the bench muzzle direction on screen.

## vfx3y (2026-10-27 KST)
- Mesh UV scroll: sfx `overrideAffixScroll {override, speedX, speedY}` parsed by FUN_1425fd580 (shader desc +0xd8 speedX, +0xdc speedY; serializer FUN_1425ff110; strings @1437ffc18). EffectMeshMaterial.Scroll; mesh FS samples uv + scroll*time (EffectLayer.Time = effect time). Units UV/s HEURISTIC. Default on, SM2_VFX_MESH_SCROLL=0 disables. mzl_geom: speedY 11. Evidence output /workspace/rea/vfxy_1.c.
- Bench bloom (opt-in SM2_VFX_BLOOM=1, HEURISTIC, not the game's post chain): second mesh-only pass weighted min(1, bloomIntensity*SM2_VFX_BLOOM_GAIN[0.01]), 3x box blur radius SM2_VFX_BLOOM_RADIUS[10] px, added x SM2_VFX_BLOOM_STRENGTH[1.5] (Bench/BenchBloom.cs). Only meshes feed it (sprites have no bloomIntensity read yet). ingame_cmp.sh "all" column enables it. Muzzle rings now give a warm orange glow, closer to in-game; slight red haze over the whole tile from the large ring meshes.

## vfx3z (2026-10-27 17:15 KST)
- z1 Bench bloom: sprites now feed the bloom pass; only light above a bloom point blooms (BenchBloom, SM2_VFX_BLOOM_POINT
  default 0.45, HEURISTIC). Evidence: engine bloom is a scene HDR threshold bloom ("Bloom.BloomPoint" @0x14363dbf0,
  "COMMON_HDR_BLOOM_POINT" @0x1436e9f08); sprite emitters have no bloomIntensity (only sfx mesh materials, @0x1436446f0).
  The dim red haze from the big muzzle ring meshes is gone; the core keeps a warm orange glow.
- z2 Finisher blood: combine_material is a deferred G-buffer shader (dxil-spirv of pc_dx12_combine_material.sdc →
  /workspace/vfxsh_glsl/combine/, perm 00001_0: 5 RTs, instance colour /255 + sRGB→linear polynomial, emissive RT4 =
  CB_COMMON[22].x·mtl[8].x·material emissive, not the sfx tint). Brightness = scene light, so lit (non-sfx) meshes get
  bench light EffectRenderer.SurfLight 60 (was 200; SM2_VFX_SURF_LIGHT, HEURISTIC). Blood red R 186→111 vs ref ~107.
  Still more saturated than in-game (ref G/B ~60/44 from scene ambient/fog).
- z3 LitScale 100→400 (SM2_VFX_LIT). Lit particle shaders read only runtime light (COMMON_SUN_INTENSITY @0x1436e9ed0,
  COMMON_SKY_SH_HARMONICS @0x1436eb790, light grid) × COMMON_HDR_EXPOSURE_DIV_AVG_LUM @0x1436e9ee8; no per-effect
  constant exists. Value from the in-game refs: HEURISTIC.
- z4 Distortion: particles.sdc 00005_1 PS: o = (tex.rg·2.007874−1.007874)·TEXCOORD.w, mask = sat(10·(|o.x|+|o.y|)),
  RT = mask·(o·2+0.5); VS 00001_1 passes V_TEXCOORD.w through. Implemented (SM2_VFX_DISTORT_EXACT=0 = old radial edge).
  Power→V_TEXCOORD.w and the resolve scale (SM2_VFX_DISTORT 0.04) stay HEURISTIC.
- z5 Trail DEGR: CB_PASS_TRAIL member strings @0x14378f170..0x14378f288 have 0 code refs (ghidra_proj3 DataRefs) → CB
  filled by fixed layout. No new evidence; mapping stays HEURISTIC.
- z6 7 side-by-sides (side_by_side.py now also writes blood_vs_hierophant, bolter_smoke). Box old run →
  ingame_cmp.bak7; PC Captures\ingame_cmp old → next .bakN.
- Patches /workspace/patches/vfx3z/. VFX ≈ 79%.

## vfx4-0 (2026-10-27 17:30 KST) — roadmap change
- User removed Niagara conversion from the roadmap; it is kept only under ROADMAP → "Possible future development". AGENTS.md rule 6, STATUS, WORKFLOW updated.
- New order: vfx4-1 VFX heuristic reduction (finisher blood saturation, distortion power/0.04, trail DEGR), vfx4-2 UE export test (no Niagara) into /Game/SM2_Work, vfx4-3 browser viewer polish.

## vfx4-1 (2026-10-27 17:50 KST) — VFX heuristic reduction (Ghidra ghidra_proj3)
- Outputs /workspace/rea/vfx4_1.c (DispScan 0x248), vfx4_2.c (DecompAt strings), vfx4_3.c (DispScan trail offsets).
- **distortion.power → vertex alpha: PROVEN.** FUN_1417eeed0 writes colour (0,0,0,power(age)) for distortion emitters; define
  "DISTORTION_SCALE_CONST_MUL_VC_ALPHA" @0x1437f5a40. Current code already matches (r*alpha with alpha=alphaK); comment updated.
- **0.04 resolve strength: still HEURISTIC.** Only the CB names were found (CB_PASS_DISTORTION_POSTPROC @0x1437133f8, layout FUN_1421aa940).
- **Trail DEGR: still HEURISTIC** for the slot mapping. New proven desc offsets (degNear +0x3e8, degFar +0x400, degradationRadius +0x418,
  parser FUN_14183a330) and the desc→instance copy FUN_14187f1e0; the CB pack is still not traced.
- **Finisher blood saturation: still HEURISTIC** (scene ambient/fog; no exe evidence). SurfLight 60 unchanged.
- No behaviour change, so no new env flag. VFX ≈ 80%.

## vfx4-2 (2026-10-27 18:00 KST) — Unreal export test (no Niagara)
- PC exports (SM2Extractor_9474df0, fbx-unreal) → `Desktop\SM2_Work\Exports\ue_export\`: grenade, muzzle group (no own textures;
  textures from donors fx_cine_bolter_muzzle + part_mgs_cine_dread_window_chaos for phantom_hands), trail smoke, finisher blood,
  tpl sfx_geom_muzzle + splashes_vtxa_lwi. Texture list = the bench SPRDBG `tex` names + sfx nameTex.
- `tools/ue/ue_vfx_export_test.py` (import / capture / export, separate uerun calls; uerun args are ONE comma list, script path absolute):
  imports into `/Game/SM2_Work/VfxExport` (existing → `_bakN`; _bak1/_bak2 are earlier test runs), masters M_SM2Vfx_Add / M_SM2Vfx_Blend
  (unlit, two-sided; blend from the bench), MI per texture; muzzle mesh uses MI_phantom_hands tinted mzl_geom orange.
  Preview only (whole flipbook, no curves) = HEURISTIC, not the game shader.
- Verified: all 20 textures + 2 meshes render textured (`Desktop\SM2_Work\Captures\ue_export\ue_export_board.png`; first dark try
  kept as `.png.bak1`, fix = manual exposure). Blood mesh uses its FBX lit materials (red). Backdrop BlackUnlitMaterial did not load →
  grey sky background (cosmetic). Capture actors are SM2Cap-tagged and removed by `export`; the editor level was not saved.

## vfx4-3 — browser polish (done)
- MainWindow.axaml(.cs): search shortcuts, a clear button, the export button now follows the selection, and Ctrl+E exports. GameCatalog.Query matches all words.
- Tests: Assets 142 (SearchWordsTests added), Extractor 42, LibSaber 5. The GUI has not been run on the PC (build only).
## mtl1 (2026-10-27 18:20 KST) — sorcerer/hellbrute colours: glt.chameleon; cloth bake; lictor sway
- Cause: textures/sRGB were fine and no tintByMask/colorization is used; Thousand Sons `paint`/`blue`/`ruby` variants enable `glt.chameleon`, which we ignored.
- REA: parser FUN_142576f30 (enabled +0, color0..3 +4/+0x14/+0x24/+0x34 ×1/255 const _DAT_1435640d0, offset1 +0x44, offset2 +0x48, fresnelPwr +0x4c), descriptor FUN_142578190, writer FUN_142577110, glt parse FUN_1425bb840, regs FUN_1421b0bc0, define GLT_CHAMELEON FUN_1423b6260 (strings 0x1437f9de0, 0x143729b78/b98). Shader pc_dx12_glt.sdc PS 03558_0, CB_STATIC_GLT rows 10-13 colours, 14-15 offsets: t=pow(sat(N·V),r15.z); 3-step colour ramp (r14.xy, r14.zw, r15.xy); albedo *= lerp(ramp, 1, metal).
- Code: SpecLayers.Chameleon → MeshPart.Chameleon → sidecar `chameleon`; M_SM2 `UseChameleon` + ChameleonC0..3/R0/R1 (Custom node). (mtl1-18: packer FUN_1423ba150 traced = these thresholds; unset colour = black (0,0,0,0) per parser; colours treated sRGB.)
- PC: exports Exports\mtl_cham\{cc_sorcerer (+idle clip lesser_sorcerer_idle_lesser_sorcerer, 585 cloth helper bones baked), cc_hellbrute}; UE /Game/SM2_Work/Tests/mtl_cham/* (earlier import kept as _bak1). Captures Captures\mtl_fix\after_cham_*, lictor_sway_t*_*.png, lictor_face.jpg.
- Open: hellbrute after-capture looks greyer than the bulk "before" (check variant/texture vs bulk, and chameleon defaults); browser GL preview does not render chameleon yet (cloth/physics preview already from 642fb56).

## mtl1-B (2026-10-27 18:50 KST) — hellbrute grey fix
- Cause 1: the chameleon Custom HLSL node's pins likely did not bind from Python, so the paint went grey. Cause 2: PixelNormalWS in the Substrate front graph. Bulk and mtl_cham use the same variants and textures (hash-identical armor_01). Toggling UseChameleon off restored the bulk look, which isolated the problem to the chameleon graph.
- Fix: the ramp is now built from plain nodes. N·V uses the tangent-space normal (detail_switch) · CameraVector transformed to tangent space, matching the glt PS. R0.w is read from the vector param's "A" pin (the RGB pin has 3 components and gave a "Not enough components" compile error).
- Result: Captures\mtl_fix\after_cham2_cc_{hellbrute,sorcerer}_front.png. Front-facing paint gets color3 (dark teal) and grazing angles get the cyan C1/C2 ramp. No in-game hellbrute reference exists (Captures\ingame_ref has only marine shots). The threshold mapping and color0 = white remain heuristic.

## mtl1-A: SM2PhysicsSetup editor plugin (live physics on import)
- `tools/ue/SM2PhysicsSetup/` — UE 5.7 Editor C++ plugin. `unreal.SM2PhysicsSetupLibrary.setup_from_json(mesh, physics_json, dest)`
  builds `PA_<mesh>_SM2` (sphere body per cloth helper bone; pinned particles maxDistance==0 + anchor parent kinematic; ball joints
  swing 25°/twist 12.5° to nearest closer helper; jiggles jointed to skeleton parent) and a post-process AnimBP `ABP_<mesh>_SM2Phys`
  (LocalToComponent → RigidBody(PA, WorldSpace) → ComponentToLocal), then assigns both to the mesh. Constraint limits/body radii are HEURISTIC.
- Build: `RunUAT BuildPlugin -Plugin=…uplugin -Package=<tmp> -TargetPlatforms=Win64` (VS2022 BuildTools 14.44), copy the package (minus HostProject/Intermediate) to `<Project>/Plugins/`.
- `tools/ue/ue_bulk.py import` calls it when `<name>.physics.json` exists and saves PA/ABP/mesh; warns and skips if the plugin isn't loaded.
- STATUS: builds; installed in UE_VFX_57/Plugins; NOT yet verified in-editor (editor restart pending: dirty non-SM2 packages open).
- mtl1-A VERIFIED (2026-10-27): editor restarted (21 dirty pkgs backed up as `.uasset.bak1`/`.umap.bak1`, then saved), plugin loads
  (`unreal.SM2PhysicsSetupLibrary`). `ue_bulk.py import cc_lictor|cc_sorcerer mtl_phys` -> PA/ABP built and saved
  (lictor: simulated=398 kinematic=42 constraints=440 missingBones=0). `tools/ue/ue_phys_verify.py spawn|sample|cap|export|clear`
  compares twin actors (post-process ABP on/off) in the editor world: helper-bone deviation lictor 2–5 cm, sorcerer mean 7–11 / max 48 cm,
  time-varying, no NaN. Frames: `Captures/mtl_fix/phys_<model>_t{1,2,3}_{physON,animOnly}_{front,side}.png`. No joint tuning needed yet (HEURISTIC limits).
- KNOWN BUG (ue_bulk re-import): `rename_directory(folder -> _bakN)` on LOADED assets followed by importing into the same path yields a mesh
  with private references into `_bakN` (InterchangeAssetImportData / SkeletalMeshDescriptionBulkData) -> "Illegal reference to private object",
  the mesh can't be saved. Workaround used: import into a fresh set (`mtl_phys`, export folders copied). FIXED (mtl1-1): ue_bulk now duplicate_directory -> _bakN (saved), then reimports in place; mesh save is checked (error if it fails). Verified on mtl_phys/cc_lictor.
- PC `Scripts/ue_bulk.py` had diverged from the repo copy; the physics block was patched in on the PC (backup `ue_bulk.py.bak1`).

## mtl1-2: chameleon in the browser GL preview
- `PreviewPartDraw.Chameleon` (from MeshPart.Chameleon) -> GlPreview `SetChameleon` uniforms uCham/uChamC[4]/uChamR0/uChamR1; fragment:
  t = pow(sat(N_mapped·V), r15.z), 3-step ramp, base *= lerp(c, 1, metal) — same as pc_dx12_glt.sdc PS 03558_0 and the UE master.
  HEURISTIC packing identical to import_sm2_materials.chameleon_ramp (thresholds 0, offset1, offset2). CPU renderer unchanged.
- Check: `--glbench synthetic` now writes gpu_synthetic_chameleon.png (box: /workspace/browser_run_shots/cham/). Not yet installed on the PC.

## mtl1-3: customization parts (armor + weapon) and the browser part picker
- **Armor:** see "Constructor tpls" above (character_customization_library.sso, `--variant SECTION=uid`).
- **Weapons (new, data + ssl evidence; none of these field names are plaintext in sm2_retail.exe, so the selection logic is ssl-only):**
  - `ssl/weapons/creator/customization/firearm_skins_library.sso`: `skinsGroups[] = { firearmUid, skins{ skin_N = FirearmSkin{ geomCustomizerRegions[], material?, attachingActors{locator, actorName}?, overridedActorName? } } }`. 68 firearmUids. Accessors are in firearm_skin.sslbin: GetGeomCustomizerRegions, GetMaterial, GetAttachingActors, GetOverridedActorName.
  - `ssl/geom_customizer/geom_customizer_preset_firearm.sso`: `regions{ name = GeomCustomizerRegion{ meshes[] } }`, 109 regions. The meshes are tpl object names; LOD copies (`base_01_LOD1`) are sibling nodes.
  - prop_geom_customizer_equipment.sslbin: ShowActiveRegions = HideAll + Show(region). The region Show/Hide ends in the native Geom ShowObj/HideObj (0x14133fec0 / 0x14133eae0 → 0x14133e750).
  - tpl → uid: `ssl/weapons/common/firearm/mgs/<uid>/<uid>_client.cls` geom.nameTpl. Several uids share a tpl (aux_melta_bolter also names wpn_bolt_rifle_01), so the uid with the most skins wins.
- **Code:**
  - `Customization/WeaponVariant.cs`: WeaponCustomization and WeaponVariant.Apply.
  - `--variant skin=<key>[,regions=a|b|c][,+obj,-obj]`. Only for `wpn_*` tpls, and only when given; the default export is unchanged.
  - `AssetExporter.DescribeVariant` builds the slots.
  - ExportWindow part picker for the previewed model:
    - Armor: preset combo plus one combo per body section.
    - Weapon: skin combo plus one checkbox per region present in the tpl.
    - It builds the `--variant` spec.
- **HEURISTIC:** region `main` is always shown (equipment defaultRegion not decoded). Skin `material` and `attachingActors` are reported but not applied; this is mesh-only.
- **Tests** (PC `Desktop\SM2_Work\Exports\cust_test\*`):
  - wpn_bolt_rifle_01 → arifle_bolt_rifle (80 customizable objects):
    - skin=01_red: 12 visible parts.
    - skin=05_h: 10 visible parts.
    - Custom regions (base_02_01|base_02_02|bolt_01|magazine_01|sight_telescopic_01|cartridge_01): 13 visible parts.
  - cc_sm_constructor_tank:
    - Default: 54 visible parts.
    - HELMET=helmet_tank_relic_01, LEFT_PAULDRON=left_pauldron_tank_relic_01: 53 visible parts.
- **Install:** the browser and CLI are installed in `C:\Users\Revaptor_FX\SM2Extractor` (backups `*.exe.bak2`). This build also brings in the mtl1-2 chameleon preview.
- CopyFromBox fails for 72 MB files ("temporarily unreachable"). Split them into 20 MB chunks and concatenate on the PC.

## mtl1-4: heuristic refinements (2026-10-27 KST)
- **Physics joint angles (changed):**
  - Cloth helper swing limit is now DERIVED from NvCloth `maxDistanceMeters`, which is the motion-constraint radius r around the skinned position. A particle at pivot distance L can deviate by atan(r/L).
  - Clamped to 5..60° (the clamp is HEURISTIC). The twist limit is half the swing. With no data it falls back to 25°. Jiggles keep 15° (no constraint data in current exports).
  - Built into `Build\sm2phys\pkg` (previous build in `pkg_bak1`). **Not installed yet:** it needs an editor restart, and the VFX executor is using the editor. To install: copy `pkg` over `UE_VFX_57\Plugins\SM2PhysicsSetup` (back up as `.bakN` first), restart the editor, and re-run setup_from_json for the assets.
- **Blood saturation, distortion 0.04, trail DEGR slot mapping:** unchanged and still HEURISTIC.
  - vfx4-1 (VFX executor, same day) already re-checked the exe: only CB names / shader defines were found, with no CPU writer. A runtime capture is not allowed (EAC).
  - These live in VFX files, which this task must not touch.

## mtl1-5: batch characters with physics (2026-10-27 KST)
- **Set:** `Exports\chars_phys\<name>` → `/Game/SM2_Work/Tests/chars_phys/<name>` (mesh, materials, `PA_<name>_SM2`, `ABP_<name>_SM2Phys`). Imported with `ue_bulk.py import <name> chars_phys`. Physics comes from the currently installed plugin (old fixed limits of 25°/15°).

| name | cloths | jiggles | helpers | bones | slots |
|---|---|---|---|---|---|
| cc_calgar | 8 | 3 | 1294 | 1596 | 44 |
| cc_chaplain | 4 | 23 | 894 | 1396 | 34 |
| cc_commissar | 6 | 0 | 1764 | 1970 | 13 |
| cc_tech_priest | 4 | 7 | 2002 | 2684 | 41 |
| cc_sorcerer_exalted | 4 | 2 | 2614 | 3038 | 30 |
| cc_titus_assault | 6 | 34 | 922 | 1455 | 41 |

- All six imported with 0 errors, 0 material mismatches and 0 ref-pose NaNs.
- **Sim check is inconclusive:** these exports have no clips, so `ue_phys_verify` stays at t=0 with deviation 0 (no NaN). The editor does not advance the AnimBP without a playing clip. Next step: export one clip per character (or use a pose-tick) and re-sample.
- `ue_bulk`'s `physics` report string comes back empty through Python (out-param), although the PA and ABP are created.
- Customization visual check: box `/workspace/cust_test/{bolter_01red_vs_05h,bolter_01red_vs_custom,marine_default_vs_relic}.png` (numpy rasterizer of the exported glbs).

## mtl1-6: Forge Lens rename + dark theme (2026-10-27)
- User-facing name is now **Forge Lens**. The SM2Browser csproj has AssemblyName=ForgeLens and Product "Forge Lens", and the window titles changed to match. Namespaces, project folders, the app.manifest identity and the RecentAssets appdata folder (`LibSaber.SM2Browser`) are unchanged, so existing recents are kept.
- Install: `C:\Users\Revaptor_FX\SM2Extractor\ForgeLens.exe` (MD5 AE60BB09C98C1F1A5D797FADF5E57424). The old `LibSaber.SM2Browser.exe` is kept, with a copy at `.bak3`. When the user says "실행해줘" ("run it"), launch ForgeLens.exe.
- Theme (App.axaml only; no behaviour changes): Fluent Dark palette, greys #1E1F22/#252629/#2B2C30, one accent #E8892B. Buttons and inputs are 28 px with radius 3, toolbar and status bar have borders, column headers use the `header` class, and the Export button uses the `accent` class.
- Screenshots: `Captures\mtl_fix\ui_before.png` and `ui_after.png`, also on the box at /workspace/mtl_fix/. Captured with `Scripts\mtl_fix\wincap.ps1` (PrintWindow, off-screen, no focus; run it under powershell.exe 5.1).

## mtl1-7: SM2PhysicsSetup v2 installed + live sim check with clips (2026-10-27)
- **Plugin:** the new build (cloth swing = clamp(atan(maxDistCm/segment),5,60)°, DERIVED, with the clamp a HEURISTIC; twist = swing/2) is installed in `UE_VFX_57\Plugins\SM2PhysicsSetup`. The old copy moved to `Desktop\SM2_Work\Build\sm2phys\installed_SM2PhysicsSetup.bak1`; it sits outside Plugins so UE doesn't load a duplicate. `plug.py` → PLUG True.
- **Re-run:** done for the 6 chars_phys characters plus mtl_phys/cc_lictor and mtl_phys/cc_sorcerer with 0 errors. ue_bulk backed up the previous imports as `_bakN` (calgar `_bak1` + `_bak2`, lictor `_bak2`, the rest `_bak1`). The plugin does not overwrite, so new assets are named `PA_/ABP_<mesh>_SM2Phys_2`, and the mesh's PhysicsAsset/PostProcess ABP point to these new ones.
- **Clips (re-export with `--anim`; old export folders kept as `<name>.bak1`):** calgar `calgar_run_fwd`, chaplain `chaplain_idle_v01`, commissar `captain_idle_1_commissar`, tech_priest `tech_priest_censer_walk_loop`, sorcerer_exalted `exalted_sorcerer_dash_fwd`, titus_assault `tacticus_run_fwd_sound_intercessor`. All have nan=0.
- **Live sim (`tools/ue/ue_physlive.py`, Simulate-In-Editor):** the editor world doesn't tick in a hidden editor, so I use SIE. Sequence: `ue_phys_verify spawn` → `physlive cams` → `start` → `play` (re-plays the clip on the PIE copies, re-inits the off twin without the PP ABP, sets AlwaysTickPoseAndRefreshBones) → `sample` / `cap` → `stop` / `camclear`. Mean/max deviation in cm of all helper bones, physON vs animOnly:

| char | bones | mean | max | NaN |
|---|---|---|---|---|
| calgar (run) | 917 | 11–17 | 80–132 | no |
| chaplain (idle) | 732 | 8.8–9.0 | 46–50 | no |
| commissar (idle) | 921 | 2.8–3.6 | 28–53 | no |
| tech_priest (walk) | 1818 | 56–59 | 164–177 | no; **FLAG:** the robe drops about 1.4 m (helper z −88 vs +51) |
| sorcerer_exalted (dash 13 m/s) | 2406 | 69–137 | 208–467 | no; **FLAG:** world-space lag / drop on the root-motion dash |
| titus_assault (run) | 782 | 9–17 | 43–75 | no |

- PNGs: `Captures\mtl_fix\phys_<name>_live_{physON,animOnly}_{front,side}.png`. Side-by-side copies are on the box at `/workspace/mtl_fix/phys_live_<name>_cmp.png`.
- **Next:** for tech_priest and sorcerer_exalted, check that the top cloth bodies are kinematic and constrained to the parent, and add linear limits. Python can't edit constraints, so this has to go in the plugin cpp.
- The editor map in use, `/Game/Work/02/Lightning_01` (not ours), was dirtied by the temporary actors (all cleared). **Do not save it.**

## mtl1-8a: "Anim Only (Manny)" — clips written directly on SK_Mannequin (2026-10-27)
- **CLI:** `export ... --anim <clips> --anim-only-manny` (built-in SKM_Manny reference, `Retarget/SK_Mannequin.ref.json`, 161 bones, embedded) or `--anim-mannequin <ref.json>` (a dump from `tools/ue/ue_dump_skeleton.py`). Both imply anim-only and turn physics off. Output: `{model}__{clip}_Manny.fbx`, which already uses SK_Mannequin's bone names, hierarchy and ref pose. No IK Retargeter is needed in UE.
- **Forge Lens:** Export window checkbox **"Anim Only (Manny)"**.
- **Method (HEURISTIC, world-delta, `MannequinRetarget.cs`):** W_manny = Rref_manny · A · inv(Rref_sm2) · R_sm2(t).
  - A is the shortest arc from the Manny bone direction to the SM2 bone direction.
  - Root and pelvis translation are scaled by the pelvis-height ratio (titus 0.649).
  - Unmapped bones (twist, corrective) keep their ref locals. `ik_*` copy the foot and hand worlds.
  - World composition recurses because SM2 parents can come after their children (titus LEG2L = 4, parent LEG1L = 156).
  - `SM2_MANNY_DEBUG=1` prints DBG lines.
- **UE import:** `tools/ue/ue_import_manny.py <fbx|folder>` imports onto `/Game/Characters/Mannequins/Meshes/SK_Mannequin` into `/Game/SM2_Work/Anims_Mannequin`, keeping `_bakN` backups.
  - **Yaw is 0.** Auto would give 180 and turn the clip around.
  - A reimport reuses the yaw stored on the existing asset, so the script resets it first. This was the cause of the earlier "rotated 180°" result.
  - `ue_bulk.py import` picks up `*_Manny.fbx` automatically, imports them onto SK_Mannequin, and leaves them out of the SM2 clip import.
- **Verified (titus run / idle / attack), AnimPose world positions:** Manny vs SM2 ×0.649 at 8 bones × 4 times, mean 8.0–9.6 cm, max 14.8 cm. The remaining gap is proportions.
  - Plots: box `/workspace/mtl_fix/retarget_{run,idle,attack}.png` (red SM2, blue Manny, front and side).
  - The SceneCapture renders stayed on frame 0 because the editor world does not tick, so the plots are drawn from AnimPose instead.
- **Test assets:** `/Game/SM2_Work/Anims_Mannequin/SM2ref` (titus SM2 reference clips) and `Anims_Mannequin_y0` (an earlier test).
- **New test map:** `/Game/SM2_Work/Tests/PhysTestMap`, created with `phys_map.py`; it never saves Lightning_01. It is now the editor's current map.

## mtl1-8/9 physics: robe pins, CENTRE base-bone sim space, PhysTestMap (2026-10-27)
- **Plugin (cpp, installed in UE_VFX_57 and verified with PLUG True; old copy kept at `Build\sm2phys\installed_SM2PhysicsSetup.bak3`):**
  - Multi-particle helpers are now pinned by the maxDistance=0 index.
  - Every skeleton parent of a cloth pins its closest helper and gets a kinematic body.
  - The RigidBody node simulates in the CENTRE base-bone space with WorldAlpha 0 and DampingAlpha 0, plus velocity and acceleration caps (all HEURISTIC).
  - New UFUNCTIONs: `SetupFromJsonEx` (skip cloths), `TuneRigidBody`, and `CreateChaosCloth`.
- **Tuning:**
  - `ue_bulk.py` calls `TuneRigidBody` after setup, so an older installed binary gets the same defaults.
  - `tools/ue/ue_tune_rb.py` is the manual tuning script.
- **Live test:** runs in `/Game/SM2_Work/Tests/PhysTestMap` (`tools/ue/ue_phys_map.py`). Lightning_01 is never saved. The test twins use NO_COLLISION, so there is no ComplexAsSimple spam.
- **Measured, mean/max cm (physON vs animOnly, all helper bones):**

| char | mtl1-7 | now | NaN |
|---|---|---|---|
| calgar run | 11–17 / 80–132 | 11–13 / 87–121 | no |
| chaplain idle | 8.8–9.0 / 46–50 | 5.9–6.0 / 35 | no |
| commissar idle | 2.8–3.6 / 28–53 | 2.4–2.8 / 30–47 | no |
| titus run | 9–17 / 43–75 | 8–14 / 33–54 | no |
| tech_priest walk | 56–59 / 164–177 | 23–24 / 92–110 | no; **FLAG:** the robe still hangs about 70 cm low on helper 0 (constraint stiffness can't be set from Python) |
| sorcerer_exalted dash | 69–137 / 208–467 | 41–62 / 245–341 | no |

- An intermediate setting (WorldAlpha 1 / DampingAlpha 0.5) made the sorcerer explode (mean 234–611 cm, max 106 m). The 0/0 setting fixed it.
- PNGs: box `/workspace/mtl_fix/phys_v3_<char>_cmp.png`.
- `ue_mrq_warmup.py` sets the MRQ engine and render warm-up frames on a config (for cloth and RigidBody settling). It keeps a `_bakN` copy of the config.

## mtl1-10 Chaos Cloth option (opt-in, 2026-10-27)
- **How to run:**
  1. Export with `--include-cloth-sim`.
  2. Run `ue_bulk.py import <asset> <set> chaoscloth`, or `tools/ue/ue_chaos_cloth.py <asset> <set>` on its own.
- **What it does:**
  - Duplicates the mesh to `<asset>_ChaosCloth`. An existing copy is renamed `_bakN` first.
  - `CreateChaosCloth` turns each `_SIM` section into Chaos clothing data, pins the maxDistance=0 particles, and binds the render sections.
  - Every cloth that can't be converted stays on the bone method via `SetupFromJsonEx` (`<folder>/ChaosCloth/PA_*`, `ABP_*`).
  - The bone method stays the default.
- **Sim section match (HEURISTIC):**
  - First by slot name with ≥80% of vertices within 2 cm of a sim particle.
  - Otherwise the earlier geometry test (95% within 0.5 cm).
- **Converted:**
  - calgar: belt_01, belt_04
  - tech_priest: skirt_v7, tabards_bck_v4
  - sorcerer_exalted: skirt, tabard
- **FLAG:** the cloak / ribbon / sleeves / tabards_frt / beads sections have slot hits of 0.00–0.74 against simPositionsUeCm. The sim positions don't match the imported section (likely a different bind space or a welded/split topology), so these stay bone-based. Not yet investigated.
- **Live check:** PhysTestMap, SIE, `liveC.ps1`. Values are mean cm of the helper bones, physON vs animOnly. Converted cloths no longer drive their helper bones, so a lower number partly reflects that.
  - calgar: bone 11–13 → chaos 7–8
  - tech_priest: bone 23.5 → chaos 1.9–2.4. Visually, the robe no longer sags below the feet.
  - sorcerer: bone 55–63 → chaos 20–21
  - No NaN in any run.
  - PNGs: box `/workspace/mtl_fix/cloth_cmp_{calgar,tech_priest,sorcerer_exalted}.png` (top = bone method, bottom = Chaos).
- **MRQ:** before rendering cloth or RigidBody shots, run `tools/ue/ue_mrq_warmup.py <config> 32 8` so the sim settles (engine and render warm-up frames).

## mtl1-11: SIM section X-mirror fix and bone-mode chain-depth cap (2026-10-27)
- **Unmatched SIM sections. Cause measured with `CreateChaosCloth` DIAG lines (`tools/ue/ue_cdiag.py`):**
  - The sidecar's `simPositionsUeCm` is the imported section mirrored on X, with the same extents and Y/Z. Examples:
    - calgar ribbon_02_L: section centre (18.7, 21.2, 144.6) vs sim (-18.7, 21.2, 144.6), extents 2.7/3.2/17.6 on both, slot hit 0.00.
    - calgar cloak: centre (1.9, -39.3, 133.9) vs (-1.9, …), hit 0.74.
    - sorcerer beads: 22.6 vs -22.6, hit 0.01.
  - Only near-symmetric pieces had matched before.
  - **Fix, exporter:** `PhysicsSidecar` now writes `simPositionsSpace="ueMesh"` and positions with X negated.
  - **Fix, plugin:** for older exports it mirrors when that fits better, judged first by centroid, then by vertex hits.
  - **Result:**
    - calgar: 8/8 cloths converted (was 2)
    - tech_priest: 4/4 (was 2)
    - sorcerer_exalted: 4/4 (was 2)
    - Hits 1.00, worstMatch 0.00 cm.
  - Not yet measured: whether the other sidecar vectors (capsule offsets, wind) have the same X mirror.
- **Bone-mode tech_priest sag. Cause measured with `ue_bdiag.py` (DescribeBody, SetSimGravity):**
  - The skirt helpers formed long chains of 3.3 cm links.
  - Helper 0 dropped from rest z 49 to z -15. With gravity off it stayed at z 45.
  - A SLERP drive of 1e5 (`TuneConstraintDrive`, `ue_drive.py`) changed nothing in the RigidBody node, so stiffness alone can't fix it.
  - **Fix:** `MaxChainDepth=1` (HEURISTIC). A helper anchors only to a pinned helper or to its own kinematic skeleton parent. Its swing stays bounded by atan(maxDistance/L), the source NvCloth motion-constraint bound.
  - The depth-3 cap measured a 29 cm sag.
- **Live check:** PhysTestMap, SIE, after `ue_resetup.py`. Values are mean/max cm.

| char | before | now | NaN |
|---|---|---|---|
| tech_priest | 23.5 / 108 (h0 z -15 to -23) | 9.4–10.2 / 50–54 (h0 z 39–42 vs 49–51 anim) | no |
| sorcerer_exalted | 41–62 / 245–341 | 22–30 / 192–276 | no |
| calgar | 11–13 | 11.0–11.7 | no |
| chaplain | ≈6 | 7.9 (slightly worse, FLAG) | no |
| commissar | 2.4–2.8 | 2.8–3.0 | no |
| titus | 8–14 | 7.9–8.0 | no |

- New UFUNCTIONs: `TuneConstraintDrive`, `DescribeBody`, `SetSimGravity` (diagnostics).
- **mtl1-11 cloth compare:** PhysTestMap, chars_cloth, after re-running setup for the bone method.
  - PNGs: box `/workspace/mtl_fix/cloth_cmp_{calgar,tech_priest,sorcerer_exalted}.png` (top = bone, bottom = Chaos).
  - Bone mean cm: calgar 10.7–12.3, tech_priest 9.7–9.8 (h0 z 40–41 vs 49–50 anim), sorcerer 18–31.
  - Chaos: every cloth is converted, so the helper-bone deviation is 0 by construction and the check is visual only. The cloak trails behind on calgar's run in both modes.
  - The Chaos captures frame smaller (camera placed from the actor bounds; cloth bounds are larger). This is cosmetic. No NaN in any run.

## mtl1-12: chaplain regression, capture framing, collision/wind mirror, gravity restore (2026-10-27)
- **Chaplain:**
  - Cause: all 4 of chaplain's cloths have single-particle helpers only, and mtl1-11's MaxChainDepth=1 cap made them worse (≈6 → 7.9 cm).
  - Fix: the cap now applies only to cloths that have multi-particle helpers (`bAnyMulti`, HEURISTIC).
- **PhysTestMap re-check after `ue_resetup.py`, mean cm (no NaN):**
  - chaplain: 5.69–5.74 (fixed)
  - calgar: 12.3–14.7
  - commissar: 2.9–3.2
  - titus: 8.2–8.8
  - tech_priest: 12.8–13.2. h0 z stays at 38.5–39.1, so the skirt sag fix holds. The tabards and sleeves are uncapped again, which raises the mean from 9.7.
  - sorcerer: 18.1–22.2 (was 22–30)
- **Capture framing:** `ue_physlive.py cams` frames both twins from the base mesh asset bounds. The `_ChaosCloth` mesh has larger cloth bounds and used to frame smaller.
  - Re-done PNGs: box `/workspace/mtl_fix/cloth_cmp_{calgar,tech_priest,sorcerer_exalted}.png` (tag cl3, top = bone, bottom = Chaos, same framing).
- **Collision and wind vectors:**
  - `localWindCm` is a world-space vector, so it is X-mirrored the same way as the simPositions (measured mirror in mtl1-11).
    - Wind X is nonzero on 7 cloths, e.g. titus stamp_R_01 (600, 400, 200) and calgar cloak (-300, -200, 0).
  - Capsule, sphere and box `offset*Cm` are bone-local. The UE import is a reflection M, and bone frames conjugate by M, so local offsets map by the same M.
    - This part is DERIVED, not measured in UE.
    - 33 offsets with nonzero X across the 6 characters.
  - The sidecar now writes all of these through `UeMesh` (X negated), with `collision.space="ueMesh"`.
  - Gravity is -Z only and unchanged.
  - Neither the plugin nor CreateChaosCloth reads capsules or wind yet. Only `ue_sm2_physics.py` (manual Chaos config) does, so no runtime results change.
- **Gravity:** the diagnostic PAs (`PA_cc_tech_priest_SM2`, `_1`, `_2`, `_3`, plus `_4` and `_5`) have gravity back on (`ue_bdiag.py … 1`).
- **Old PA/ABP versions:** kept, nothing deleted.

## mtl1-13: Manny root fix, Chaos cloth collision, FOV-fit captures (2026-10-28)
- **Anim Only (Manny) fix (user report: body floating away from the root and tilted in the anim editor):**
  - Cause (measured with `tools/ue/ue_anim_probe.py`, AnimPose world): the imported `_Manny` clips had the **root at yaw 180**. The children worlds were right (pelvis z 83–94, feet z 11–45, moving +Y). The FBX import (yaw 0) turns an identity root key by 180. The anim editor's root-motion preview then moved the mesh against the root, which looked like a floating, offset body.
  - Fix in `MannequinRetarget.cs`:
    - The root key carries a compensating 180 yaw (`SM2_MANNY_ROOTFIX=0` disables it), so it imports as identity.
    - The root rotation is a yaw-only delta from the clip's first frame.
    - The root z is clamped to 0.
  - Verified after reimport (titus run/idle/attack, old assets kept as `_Manny_bak1..6`): root (0 0 0 | 0 0 0) at every sampled time, children unchanged.
  - Simulate-In-Editor renders (`tools/ue/ue_manny_cap.py`, ortho 300 cm, red line = ground): box `/workspace/mtl_fix/manny_fix_{run,idle,attack}.png`. Feet are on the ground (foot bone z 11–14 at rest) and the body is upright, facing and moving +Y.
  - The run's ~40° forward lean is in the source: SM2ref CENTRE→HEAD is 35°.
- **Chaos cloth collision (plugin cpp):**
  - `CreateChaosCloth` now builds `ChaosCloth/PA_<mesh>_ClothCollision` (kinematic bodies) from `ue.chaosCloth.collision` and assigns it to each Clothing Asset's PhysicsAsset:
    - capsules → Sphyl, or TaperedCapsule when the radii differ;
    - spheres and boxes → their matching shapes.
  - Exports without `collision.space="ueMesh"` are X-mirrored. This is **HEURISTIC**: it is derived from the mtl1-11 point mirror.
  - New UFUNCTION `MeasureClothPenetration(Comp, ThresholdCm)` counts sim particles that are deeper than the threshold inside the collision shapes.
  - Wind: no ChaosClothConfig property exists. It is **not applied yet**; the runtime `ChaosClothingInteractor.set_wind` is the plan.
- **Live result (PhysTestMap, `_ChaosCloth`, cl4):** no NaN. Particles more than 1 cm inside the collision, worst depth about 10–12 cm:

  | Character | Inside | Share |
  |---|---|---|
  | calgar | 110–186 / 3039 | 4–6% |
  | tech_priest | 45 / 1220 | 4% |
  | sorcerer_exalted | 55–81 / 2573 | 2–3% |

  - There is no collision-off baseline yet, so this is not yet proof that cloth stays outside the body.
- **Capture framing:** `ue_physlive.py cams` now fits FOV 30 to the bounds height, so the character fills the frame. Box `/workspace/mtl_fix/cloth_cmp_*.png`: top row is the mtl1-12 bone captures (old framing), bottom row is Chaos with collision (new framing). The old files are kept as `cloth_cmp_*_bak1.png`.

- 2026-10-28 mtl1-14 Manny axis fix: Mirror (S R S) flipped bone-local axes -> imported pelvis X pointed down (pitch -73 vs Manny +84), so Persona (skeleton translation retarget) twisted/floated. MannequinRetarget.BoneAxisFix (env SM2_MANNY_AXISFIX=0 to disable) pre-multiplies non-root frames by local Rz(180). Verified AnimPose: pelvis pitch +73/+84, root identity, run root along +Y. Persona capture blocked (editor in PIE, window stale).

- 2026-10-28 mtl1-14b: Manny verified via fixed-camera SIE side/front captures (tools/ue/mtl_fix/mcap3.ps1; camera not following pelvis, red line = ground z=0): run/idle/attack upright, feet z~11 (ankle), run faces +Y, AnimPose root along +Y. "play mode" error earlier was our own leftover SIE; pie.py ends PIE/SIE. Persona viewport camera not scriptable (opens inside mesh). Cloth: cpp10 (pinned/free pen split, CCD, thickness>=2) + clothwind.py (ChaosClothingInteractor.set_wind / WindDirectionalSource tag SM2Wind, Sequencer-friendly) + nocoll.py committed.

- 2026-10-28 mtl1-14c cloth: sorcerer Chaos+collision EXPLODED cloak with X-mirrored Spine capsule (r38/25, offset -20) -> sidecar collision.space=ueMesh (json .bak kept) fixes explosion (cloak still flares). pen free>1cm: coll 11-38 vs NoColl 69. Wind: interactor keyed by clothing asset name (SM2Cloth_<sim>_0), must be applied AFTER sim starts; sidecar localWindCm x1 explodes cloth (also with AirDensity 1.225e-6), x0.05 stable -> clothwind default scale 0.05. skirt_SIM interactor not found (TODO). Bone row recaptured (cl5bone). cloth_cmp_*.png rebuilt (bone/coll/noColl/wind). calgar/tech_priest wind row is an old run where wind was not applied.

- mtl1-14d: generic collision rule (boneLocal raw offsets + plugin rest-fit), sorcerer json reverted. Open: sorcerer cloak fan, first-cloth wind interactor missing. See STATUS.

- mtl1-14e: re-export sidecars with wind; first/large cloth not simulated = chunked render section (8-bit bone index) - needs Support16BitBoneIndex (approval) or plugin re-skin; cloth_cmp full regen. See STATUS.

- mtl1-14f: Support16BitBoneIndex=True (+ mesh reimport required), plugin waits async mesh build, exact slot match, one render section bound per cloth, restart editor after ChaosCloth rebuild. All cloths simulate; sorcerer stable. Pen table in STATUS.

- mtl1-15m: exporter BC5 fix (BGRA: zero B, not R) - spec metalness was 0 in every export since 37f43a1 -> paint slots grey (chameleon ramp on non-metal). RE-EXPORT needed. See STATUS.

- mtl1-15w: WindDirectionalSource drives Chaos cloth (calgar diff test) -> Sequencer can key it; clothwind src fixed. Mirror check cc_sorcerer/cc_chaplain: hit=1.00, no mirror needed, live stable. See STATUS.

- mtl1-16: M_SM2 `SpecGainToF0` (default 0). Aniso intensity, SecondSpecGain and ExtraIntensity no longer scale F0/F90; in the game they scale D (direct) only, and in UE they inflated IBL into a cyan mirror on the hellbrute paint. Remaining darker in-game tone is untraced (per-instance RGB _783). See STATUS.

- mtl1-16b: plugin SetClothConfigProps and a CreateChaosCloth SubdivisionCount 2 default (sidecar solverSubsteps). Sorcerer penetration is about -25% (pen table in STATUS); calgar is still 0.
- mtl1-16c: MRQ CLI render (hidden, -RenderOffscreen) of a Sequencer-keyed WindDirectionalSource drives Chaos cloth: calm frames change <200 px, windy frames 2.6-3.8k px. Tool: mrqwind.py. See STATUS.

- 2026-10-28 KST mtl1-17: degNear/degFar applied to lwiGeom mesh particles via shared `EffectSprites.DistanceFadeK` (see STATUS "mtl1-17"); bench no-regression; ROADMAP VFX item 7 left: SELF_DISTORTION render.

- 2026-10-28 KST mtl1-18: chameleon CPU path REA'd (parser defaults 0 -> unset colours black; packer FUN_1423ba150 = our thresholds, now exact via ChameleonSpec.Ramp). Hellbrute only darkens at rims; in-game navy is not a material constant -> per-instance _783 or viewer lighting. See STATUS "mtl1-18".

- mtl1-19 (2026-10-28): browser SELF_DISTORTION render (heuristic K=0.04, env SM2_VFX_SELF_DISTORT/_K); genOnFly runtime REA (matches current model); ROADMAP 1/2 checked, 3/5/6 skipped. Details: docs/handoff/STATUS.md "mtl1-19".
- mtl1-20 (2026-10-28): VAT animStage = combineMtlAnimControl.animStage spline over age (desc +0x178, FUN_14179fdd0) implemented; flesh covering classified (host-surface mask, not drawable standalone); blend_add/ratio/exposure/distortion still need a runtime GPU capture. Details: docs/handoff/STATUS.md "mtl1-20".
- mtl1-21 (2026-10-28): VFX view "Covering demo" host (plane/sphere, painted coveringMask, kind tint/gloss) for em_covering_* effects; DEMO/HEURISTIC, not game-accurate. Bench env SM2_VFX_COVERING=plane|sphere. Details: docs/handoff/STATUS.md "mtl1-21".

- mtl1-22: cleanup (~51 GB: PC 32.3 + UE 18.6 + box 0.6, log cleanup_del2.log); `redecode-bc5` verb + texreimp.py (449 previews, 266 UE textures); ChaosCloth rebuilt with SubdivisionCount 2 + new generic backstop (tech_priest pen 102-150 vs 115-152, small); MRQ CLI needs full object paths (mrqrender.ps1, verified LS_wind16.0000.png); ROADMAP future: UE multi-game extraction. Plugin installed (old copy Build\sm2phys\installed_SM2PhysicsSetup.bak2). FLAG: mtl15 cc_sorcerer converts 0 cloths. Editor relaunch after a kill shows a "Restore Packages" modal: move Saved\Autosaves\PackageRestoreData.json to .bakN first (only Tests packages were listed). CopyFromBox needs `computer_path` (else it lands in %USERPROFILE%).
- mtl1-23 (2026-10-28): RenderDoc ground truth applied as generic rules (CaptureRules.cs: VfxBlendRules, SfxExposure, DistortionRules, CaptureTonemap + GLSL). Browser: sfx meshes One/One + exposure comp, distortion 0.08/±0.02 (also SELF_DISTORTION), capture Hable tonemap (VFX default, model preview opt-in), GlPreview metal/rough scale-bias + metallicTint. Hellbrute: game armour = texture with default variant; our bright cyan = blue variant on slots the game does not draw with it (needs .pak to confirm). Opt-outs: SM2_VFX_TONEMAP=exp, SM2_VFX_SFX_ADD=0, SM2_VFX_SFX_LUM_K, SM2_VFX_DISTORT, SM2_VFX_SELF_DISTORT_K. Details: docs/handoff/STATUS.md "mtl1-23".
  - LEAD (armory customization, frame 128197, marine eid 10623): per-instance colours in LWI_INST_DATA_BUF (= GPU_INST_DATA_BUFFER, t109 space1) at float4 index base = INST_DATA_OFFSETS[3].w * inst + OBJ_DATA[0].w (7024 = customization index) + LWI_PARAMS.w (110592). Record: +21..23 = (0,.095,.145), (.151,.244,.622), (.229,.264,.276); +28..30 = (.008,.337,.412), (.263,.329,.482), (.51,.545,.557); +12/13 = (1,0,1,0), +14 = (0,.4,0,.4). GLT_CUSTOMIZATION_LAYERS_ENABLED 0, TILING 2. Slot -> _cc mask channel mapping is HEURISTIC (not traced). Hellbrute has OBJ_DATA[0].w = -1 (no customization).
  - LEAD: DYNAMIC_GLT_BUFFER[inst].rgb (per-instance albedo multiplier in PS 29540/03558_0) was not dumped; dump it in the next capture.
- mtl1-24 (2026-10-28): Hellbrute section -> capture draw map (68141): 31/31 sections match a GBUFFER draw by exact index count; every blue section is drawn with a chameleon PS (19112/22961), default with 29540. Our per-section variant (ShadingMaterialMaterial) is already the game's rule, so no exporter change; the mtl1-23 "blue not drawn" note was wrong (draws 3130-3452). Tools: `tools/sections_dump` (box, loose .tpl -> TSV), `tools/rdc/map_sections_to_draws.py`. Image /workspace/mtl_fix/mtl23/hellbrute_ue_after.png. Remaining blue-panel brightness = shading/lighting (HEURISTIC), see STATUS mtl1-24.

## Update mtl1-42 (2026-10-28 KST): VFX black squares + sideways fly-by, Fit camera, cloth SIM shells in preview, export In place, file list columns / Show all files
- **Black squares (HEURISTIC):** GPU streak emitters classify as `Spark` (opaque + alpha test). When the texture has no alpha coverage (e.g. `part_rs_glow_01`, `flare_common`: alpha ≈255 everywhere), the whole quad passed the alpha test and drew as a black/dark square (fx_mgs_zoanthrope_charging at 1.55 s). `VfxDrawKinds.Resolve(kind, textureHasAlpha)` now draws such sparks as `CpuParticle` (normal blend, colour-driven). Used by the browser (`EffectRenderer.ResolveKind`), `VfxFrameExport` and `VfxUeExport`. Disable with `SM2_VFX_SPARK_ALPHA=0`. Scan (all .sfx with textures on the box): **59 emitters in 48 effects** affected (229 more spark emitters have textures missing on the box, not judged). Dark-tile bench: **19 → 2** effects (the 2 left are legitimate: hierophant_bomb_ee splatter, exalted_sorcerer_ground `smoke_dark` trails); control set 6 → 6. Image: box /workspace/fix42b_shots/cmp_charging.png (top before, bottom after).
- **Sideways motion (root cause):** `EffectInstance.StandaloneMotion` (the preview fly-by that moves an effect so its trails draw) ran for every effect with trails and snapped back every 0.4 s, so particle-attached trails (`trails { partATTACHED_TRAIL { name } }`) made the whole effect slide left/right (fx_mgs_zoanthrope_lightnings). It now flies only when the effect has root trails (`EffectDefinition.RootTrails`, i.e. trails not attached to a particle). **108 of 289** trail effects now stay still. Disable the fly-by entirely with `SM2_VFX_FLYBY=0`. Image: /workspace/fix42b_shots/cmp_lightnings.png.
- **VFX camera auto-frame:** `EffectCamera.FitAll/FitBox` frames the simulated bounds (sampled over the clip) when an effect opens; **Fit** button + **F** key re-frame.
- **cc_titus white tabard (LOD0 only):** cloth `*_SIM` shells (`_SIM(_|$)`, carry `__default_material__`, the game never draws them) were visible because the LOD0 "all parts" path adds every level with visible parts and the SIM shells live in the LOD5 (titus/calgar) / LOD8 (sorcerer) pool. `TemplateMeshReader` now hides them (reason `clothSim`, toggle tooltip explains). Before: titus 6, calgar 8, sorcerer_exalted 4 visible SIM shells → 0. The UE export already hid them (`--include-cloth-sim` re-shows).
- **Physics drift with In place:** `MeshPreviewScene` now steps physics after the In-place rebase / RemoveRootMotion, so cloth/seals simulate in display space. UE bake samples `ClipPose`, unaffected unless the export In place option is chosen (then it bakes in place consistently).
- **Export In place vs Root motion:** `AnimationClip.InPlace` → `ClipPose.Apply` removes the ROOT (else first parentless bone) horizontal XZ world travel since frame 0 from parentless locals (height + turns kept; same as the preview's In place). Browser Export window "Root" combo (Root motion (default) / In place), CLI `--anim-root <motion|in-place>` (export only), applies to normal, multi-clip and Anim Only (Manny) exports. Default unchanged (root motion).
- **UI:** mouse wheel over a closed Anim/LOD/Part combo no longer changes the selection. File list: resizable columns (header splitters; TYPE/SIZE/ARCHIVE widths persisted, PATH takes the rest), full-path tooltip, middle ellipsis (`PrefixCharacterEllipsis`; Avalonia 11.2 has no PathSegmentEllipsis — it throws at startup), pane split persisted, tab underline, section labels, dimmed secondary text. **Show all files** (persisted, `BrowserSettings.ShowAllFiles`): off = only previewable types (`AssetClassification.IsPreviewable`: .tpl, .pct/.pct.resource, .sani, .sfx) in folder view and search when the type filter is All; status bar reports "N auxiliary files hidden". Screenshot: /workspace/fix42b_shots/ui_after.png.
- **Browser state memory:** settings.json now also keeps the expanded folder-tree nodes, selected folder and file (with archive), file list scroll, search text, type filter and Include subfolders (plus the column/pane widths above); restored when the last game folder reopens (saved scroll kept while the selected file stays on screen; the restored row gets keyboard focus). Clicking a row or moving with Up/Down previews it immediately (Ctrl/Shift-adding rows previews the newest one). Default column widths TYPE 72 / SIZE 64 / ARCHIVE 112, file pane 1.35*. Checked under Xvfb with a synthetic pak: /workspace/fix42b_shots/ui_restore.png, ui_arrow.png.
- Tests: SM2.Assets 218 pass (+17 Mtl42Tests), Extractor 43 (+1 `--anim-root`).

## Update ui-43 (2026-10-28 KST): Forge Lens Content Browser (UE5-style) + Anim wheel fix
- **Quick fix (2fcdc22):** the mouse wheel over a *closed* Anim / LOD / Part combo now steps to the previous/next row (respecting the Anim filter), and a wheel-picked clip plays. This replaces the mtl1-42 "wheel blocked" behaviour.
- **Categories (4c1b5e8, `SM2.Assets/Catalog/AssetCategories.cs`, 13 tests):**
  - Builds the category tree from data where a link exists: `ssl/**/*.cls` `geom.nameTpl` plus the `__type` parent chain for templates; Morpheme bundle `linksAnimation` for clips; folders otherwise.
  - Clip owner model: `OwnerModel` is used for the animation thumbnails.
  - **RULE:** every visualizable asset is reachable. The tree has **All** (the default) and anything unclassified lands in **Other**; a test enforces this.
  - Name-based placements are **HEURISTIC** and flagged in three ways: an `H` badge on the node, the node tooltip, and a "Category: reason" line in the tile context menu.
- **UI (`src/LibSaber.SM2Browser/ContentBrowser/*`, `MainWindow.ContentBrowser.cs`):**
  - **Left panel:**
    - Favorites: pin a folder with ★ Pin or by right-clicking it; remove from the right-click menu.
    - Tree toggle: **Folders | Categories**.
    - Collections: + New; add assets by drag-drop of tiles/rows or "Add to collection ▸"; right-click to Rename, set the Colour, or Delete (with confirm).
    - Favorites and collections save to settings.json immediately.
  - **Centre:**
    - Filter chips: All / Models / Animations / Effects / Textures / Text (they drive the old type filter) plus search (Ctrl+F).
    - **Grid** (virtualized ListBox of tile rows, tile size slider 64–256) or **List** (the old file list with its resizable columns).
    - Grid selection mirrors the list: Ctrl / Shift / Ctrl+A, arrows, Home/End, PgUp/PgDn, Delete (removes from the current collection). Selecting a tile previews it immediately in the unchanged viewer.
  - **Thumbnails (`ThumbnailService`):**
    - Rendered in the background and disk-cached at `%LOCALAPPDATA%/LibSaber/SM2Browser/thumbs/<sha1[..2]>/<sha1>.png`. The key is `Version|path|archive|size|ext`, so bump `Version` to invalidate the cache.
    - Models and textures are stills. Animations and effects are 10-frame strips that loop on hover; **Animate all** loops every visible tile.
  - **Persisted state:** tree mode, selected category or collection, Grid/List, tile size and Animate all. These sit alongside the mtl1-42 state (columns, panes, folder, file, scroll, search, filter, subfolders, Show all files).
  - Every existing feature is kept: export window, VFX bar, Physics, LOD/Part, In place/Root, Show all files, Recent.
- **HEURISTIC list:**
  1. Faction keyword matching (Tyranids / Chaos / Humanoid / Space Marine) on the class chain or path.
  2. Name-prefix rules for templates with no class (`refrt_`, `dstr_`, `wpn_`, `cc_`…).
  3. "By action" clip groups from name tokens.
  4. Texture map-type groups from name suffix (`_nm`, `_spec`, `_em`…).
  5. Effect thumbnails are a CPU sprite splat of the simulated particles (no lights, meshes or distortion), not the real renderer; a soft disc stands in for a missing texture.
  6. Model thumbnails are a CPU clay render (MeshPreview, untextured, for speed).
  7. Animation thumbnails use the bundle's owner model. A clip with no owner shows a glyph.
- **Screenshots (box, Xvfb, synthetic dev pak `/workspace/ui43dev/game`):** `/workspace/browser_run_shots/ui43/`
  - `01_folders_grid.png`, `01b_hover.png`
  - `02_categories_synced_kills.png`, `03_categories_models.png`
  - `04_effects_hover.png`, `05_collection.png`
  - `06_context_menu.png`, `07_list_view.png`, `08_grid_back.png`
- **Not verified:** close-time state saving under Xvfb, because there is no WM and the window can't be closed there. Favorites and collections save immediately and were checked in settings.json.
- **Dev aid:** `FL_THUMBDBG=1` prints thumbnail failures and effect-splat diagnostics.
- **Avalonia gotchas:**
  - `Border.tile` styles don't match a Border subclass unless `StyleKeyOverride => typeof(Border)`.
  - `CroppedBitmap` must be created on the UI thread.
  - An ItemsControl with a custom ScrollViewer did not virtualize; use ListBox.
  - A ListBox ItemsSource that implements IList must return -1 from IndexOf for stale items.
- **Next:** the Kitbasher-style parts picker — done in ui-44 below.

## Update ui-44 (2026-10-28 KST): Forge Lens Kitbasher (customization parts picker)
- **Data (acdcf79, `SM2.Assets/Customization/Kitbash.cs`, 4 tests in `KitbashTests.cs`):**
  - `KitbashLibrary.From(CustomizationLibrary)` reads `character_customization_library.sso` like the Kitbasher add-on: `appearancesByTeams → team → customizationPresets → class → armorPresets / bodySections.SLOT.regions → meshes (+ equipmentActorMeshes)` and the team's `sharedOutfitParts.outfitParts.SLOT.regions`.
  - Presets: `PREDEFINED_PRESET_N` with `bodySectionRegionUids {SLOT: {__value}}`; champion presets (`isChampion`, `dlcLock.dlcUid`, `skinOverrides … PveSkinChampion0N`) have no regions.
  - `Options(class, slot, classOnly)`: the class's regions, then shared regions. With classOnly a shared region is offered when it is `isForAllCharacters`, has no `appearanceUids`, or lists the class (`PVE_PVP_X` = `PVP_X` = `PVE_X`).
  - `Spec(...)` builds the exporter's `--variant` string: `appearance=TEAM/CLASS,preset=N,SLOT=uid|none,…`. **`SLOT=none` is new in `ArmorVariant`**: it removes the slot, so every object of that slot's regions hides. Empty mesh names in the library (CHAOS engineer left pauldron) are skipped instead of noted.
- **UI (aece011, `MainWindow.Kitbash.cs`, right column tab "⚒ Kitbash" next to "Meshes"):**
  - Faction / Class / Preset combos, then one combo per slot (add-on slot order) with "— empty —" first. Badges: `shared`, rarity (non-common), `hidden` (isHidden), `⚠ not in tpl`. The tooltip lists the objects, rarity, source and the appearanceUids.
  - Filters: **This class only** (default on) and **Parts not in model** (default off: parts none of whose objects are bones of the loaded tpl are hidden from the lists; a chosen one stays).
  - Picking a class finds its tpl (`KitbashLibrary.TemplateCandidates` → `catalog.FindByFileName`) and previews it through the normal model path (`PreviewFileAsync`). Every change restores the tpl's load-time visibility (cloth SIM shells stay off) and re-applies `ArmorVariant.Apply` with the spec, so the preview is exactly the export filter. The MESHES tree reflects it.
  - Buttons: **Preset parts** (reset the slots), **Randomize** (skips hidden and not-in-tpl parts), **Export combination…**. The export opens the normal export dialog with the combination shown read-only, sets `ExportSettings.Variant` = the spec and `PartVisibility` = the preview, and exports only the class tpl.
  - The status card shows filled slots / shown / hidden surfaces. Notes list library uids that don't resolve, and a count of picked objects missing from the tpl.
  - Persisted (settings.json): tab open, team, class, preset, slot picks, both filters.
  - Also: the preview note under the viewer is capped at 5 lines (full text in its tooltip). The constructor tpl note was long enough to squeeze the viewport to nothing.
- **Verified (box):**
  - Tank constructor with preset 1: preview and export both hid 978 parts and showed 54. Kitbash `CHEST=none,HELMET=helmet_tank_relic_01` export hid 996 and showed 36 (`AssetExporter.Export`, dev pak).
  - CHAOS engineer constructor loads and fills 10/10 slots.
  - Real library: LOYALIST PVE_PVP_TANK has 10 presets; its HELMET has more than 7 class-filtered options.
- **HEURISTIC / limits:**
  1. STORY_X classes use `cc_sm_constructor_x` (then `cc_sm_x_story`), on the assumption that their regions name the same objects.
  2. A champion preset maps to `cc_{sm|ch}_champion_{x}_{NN}` from `PveSkinChampionNN`. It is shown and exported complete (no slots). When that tpl is missing, the constructor is shown with the first preset that lists regions.
  3. Shared-part filtering by `appearanceUids` follows the add-on. In the tank constructor, about half of the shared-part objects are not bones of that tpl (for example `helmet_E_lamenters_grp` is only in the engineer constructor). The game may compose those from other tpls; we don't, and the lists mark them `⚠ not in tpl`.
  4. Vanity combat shields are `equipmentActorMeshes` (a separate actor), so they show `⚠ not in tpl`.
- **Screenshots (box, Xvfb, dev pak `/workspace/ui43dev/game` now also holding the real tank and engineer constructors):** `/workspace/browser_run_shots/ui44/`
  - `01_kitbash_tank_preset1.png`, `02_helmet_dropdown.png`, `03_helmet_relic.png`
  - `04_random_combo.png`, `05_chest_empty.png`
  - `06_chaos_no_tpl.png`, `07_chaos_engineer.png`, `08_export_combination.png`
- Tests: SM2.Assets 237, Extractor 43, LibSaber.Tests 5, python 8 — all pass. HaloCEA is still out of scope: it needs a Halo install path.

## Update ui-45 (2026-10-28 KST): Forge Lens Kitbash — Enemy/NPC mode (inst_constructor) + melee region skins
- **NPC data (`SM2.Assets/Customization/InstConstructor.cs`, `InstConstructorLibrary.Load(catalog, store)`, cached per catalog, ~0.3 s):**
  - `ssl/inst_constructor/inst_constructor_templates.sso`: 75 templates. `bodyLayers` = slots; each `templateVariants[]` entry links `template.__type = InstConstructorMeshTemplatesXxx` and an optional `probability`.
  - `inst_constructor_mesh_templates.ssolib`: 413 mesh templates → `geomObjNames`. A link resolves by dropping the `InstConstructorMeshTemplates` prefix and comparing lowercase without `_` (**HEURISTIC**): 835/835 links resolve. `EMPTY` is the only template with no objects.
  - Weights (**HEURISTIC with evidence**): 397 layers have no probabilities, 47 mixed, 36 all set. `inst_constructor_mesh_template_variant_release.sslbin` stores float 1.0 right after the probability field (offset 0x26F), and the game names `GetElementIndexByDistribution` / `CreateProbabilityDistributionArray`. So a missing probability = weight 1.0, normalized by the sum (Cadian helmet [1.0, 0.2] → 83 % / 17 %). All-missing = equal weight.
  - Rules (**HEURISTIC**): a layer is off when its `requiredLayer` is not filled (a non-empty mesh template in an active layer) or its `disabledByLayer` is filled. Resolved iteratively. Example: Cadian `helmet=none` turns `head`/`helmet_accessories` off and `headWithHair` on.
  - Model: `.cls` `prop_inst_constructor_project.fixedTemplateId` votes first (37 ids in 212 constructor .cls). Then name candidates `cc_{n}`, `cc_constructor_{n}`, `cc_{n}_constructor`, trailing words dropped, gaunt→gant, underscore-insensitive against every tpl name (**HEURISTIC**). Then the model of the template this one extends (Space_Marine_* → cc_intercessor). Unmapped: SporeMine, ImpRaider.
  - Presets: the 61 `prop_inst_constructor_custom_preset` loadouts (`customPreset` or `presetsDict … geomObjNames`; 51 have objects), offered for their `geom.nameTpl`. Preset tpls that no template uses get 21 "presets only" pseudo templates (cc_mark8, cine_*, wpn_* …).
  - `--variant npc=TEMPLATE,layer=MeshTemplate|none,…` or `npc=TEMPLATE,npcpreset=NAME` (`NpcVariantFilter.Apply`, Team `NPC` in `*.variant.json`) shows only the subtrees of the picked objects (`X_LODn` counts as X). Every other visible part hides, including parts in no mesh template (noted, e.g. `cultist_caps`). Failsafe: if none of the picked objects is in the tpl, nothing hides and a "wrong model" note is added.
- **Melee (`MeleeCustomization.cs`):** `ssl/melee/weapon_actors/melee_skins_library.sso` (actors → skins → `geomCustomizerRegions`) + `geom_customizer_preset_melee.sso` (region → meshes). Actor → tpl via `melee_actor_factory` `actorDescs.type.__type` .cls `nameTpl` (or `properties.geom.nameTpl`).
  - A `wpn_` tpl without a firearm uid falls back to `MeleeVariant.Apply` (`skin=KEY` or `skin=ACTOR/KEY`, Team `MELEE`). Only the regions used by this tpl's actors' skins are touched, because generic names like `body`/`geom` must not hide parts elsewhere.
  - Examples: tzaangor_sword skin 02 shows `sword_2` and hides 4. The chainsword has one region (`chainsword_geo`), so its 28 skins + melee_mod_1..5 differ only by material/actor override.
  - `powerfist=KEY` on a constructor tpl (armor mode) force-shows the `power_fist_fake` skin's region objects (`ch_*` skipped on `cc_sm_`). Tank preset 1 + powerfist 03 shows 56 surfaces.
  - The export dialog treats MELEE like WEAPON (skin + region picker).
- **UI (`MainWindow.Npc.cs`, Kitbash tab):**
  - **Mode**: Space Marine armor / Enemy / NPC. NPC mode has Template (model + mapping source in the status card), Preset ("(template layers)" or a .cls loadout) and one combo per layer.
  - Badges: chance % (`*` = probability missing → weight 1.0), `requires X` / `disabled by Y`, `⚠ not in tpl`. An off layer is dimmed. "— empty —" is added when the layer has no EMPTY variant.
  - Buttons: **Template default** (heaviest variant per layer) and **Randomize** (weighted draw). Export goes through the same `--variant` path.
  - Armor mode adds a **Power fist** combo.
  - Persisted: mode, NPC template, preset, per-template layer picks, power fist.
- **Verified (box, dev pak `/workspace/kb_dev/game` with the enemy tpls incl. the extracted cc_cadian_shock_trooper_female):**
  - NPC visible before → after:

    | Tpl | Before | After |
    |---|---|---|
    | cultist | 39 | 15 |
    | cadian | 87 | 19 |
    | cadian `helmet=none` | — | 15 |
    | female | 25 | 13 |
    | wounded | 26 | 8 |
    | termagant | 24 | 11 |
    | hormagaunt | 26 | 11 |
    | tzaangor | 85 | 34 |
    | rubric | 77 | 28 |
    | servitor | 43 | 19 |
    | servo_skull | 14 | 5 |
    | tech_priest | 41 | 26 |
    | chaos_spawn | 31 | 12 |
    | warrior_ranged | 31 | 14 |
    | warrior_melee | 26 | 12 |

  - CLI export `npc=Cadian_Shock_Trooper,helmet=none`: 15 visible, and the variant.json lists the off layers. `wpn_tzaangor_sword_01 skin=02`: 1 visible.
- **Limits:** `materialModifier` and melee skin materials are noted, not applied (materials applied since mtl1-47). `meleeActorOverride` is noted, not followed (material-only on the target tpl since mtl1-47). The power fist doesn't hide the hand it replaces (hidden since mtl1-47). Hovering the mouse wheel over a part combo changes it (Avalonia ComboBox default).
- **Screenshots:** `/workspace/browser_run_shots/ui45/`
  - `01_npc_cultist.png`, `02_npc_cadian_rules.png`, `03_npc_cadian_female.png`
  - `04_npc_cultist_preset.png`, `05_armor_powerfist.png`, `06_npc_randomize.png`, `07_melee_export_picker.png`
- **Tests:** SM2.Assets 247 (+10 `InstConstructorTests`; the retail check runs when `LIBSABER_SM2_SSL` points at an extracted `ssl/`: 75/413/835/61), Extractor 43, LibSaber.Tests 5, python 15. All pass.

## Update mtl1-43 (2026-10-28 KST): cloak detail re-import, V-flip verdict, loop-seamless cloth bakes, hem spikes, CDT unresolved bones, Cadian finger curl, VFX RE

1. **cc_calgar re-export/re-import (cloak detail fix from mtl1-42).** Imported to `/Game/SM2_Work/Tests/mtl43/cc_calgar`; MI_cloak_REND_01/02 carry DetailDensity 16 / DetailScale 0.75. Stripes gone in the comparison captures (box: `/workspace/mtl_fix/mtl43/cap/m43_cape_vs_m42.png`, `m43_back_crop.png`, `m43_fit_strip.png`).
2. **Cloak V-flip lead: false lead, no code change.** Data test on calgar LOD0 (tool `/workspace/mtl_fix/mtl43/uvd/`, `seam.py`): seam texel continuity over 20 parts and alpha-kill coverage both agree with the current UV orientation (A) and break under a V flip (B). The mtl1-42 "decoded" sheet was itself shown V-flipped. Evidence: `/workspace/mtl_fix/mtl43/vflip/m43_vflip_verdict.png`. (Data evidence, not exe.)
3. **Loop-seamless cloth bakes** (`PhysicsBake.cs`, 2c15fac). Clips detected as loops (IsLoop, HEURISTIC: start/end pose match; override with `SM2_PHYS_LOOP=0|1`) get 2 warm-up cycles, then a seam correction ramp so frame N-1 → 0 is continuous. tacticus_shield_run_fwd seam 179.6° → 0.06°, tacticus_shield_idle 168.7° → 0.06°.
5. **Hem spikiness.** Temporal fit prior blends the cluster shape-match target toward the previous frame (`SM2_PHYS_FITPRIOR`, default 0.25, APPROXIMATION). Steps >90° per frame: run_fwd 216 → 12; calgar_run (non-loop) 356 → 5, >30° 1052 → 231; idle max step 48.1° → 6.1°. Fast runs still have 30–145° max steps on a few verts: reduced, not eliminated. Plot `/workspace/mtl_fix/mtl43/loop/m43_loop_steps.png`. Not re-baked/imported in UE this pass (box numbers only).
4. **~423 unresolved CDT bones: PROVEN dropped by the game, no renaming** (96ceffa). sm2_retail.exe: CDT shape bone names resolve via FUN_141c3fa90 (string pool intern, empty → 0) → FUN_141d91470 (model bone lookup) → 0xffff on miss; FUN_14109c400/c6d0/d260 skip shapes with 0xffff every frame (readers FUN_14109f430/f5f0/f730). So unresolved shapes are inactive in-game; our ClothSolver `Find → -1` filter matches. Classification (refs) `/workspace/mtl_fix/mtl43/cdt/cdt_missing_classified.tsv`: D typo/suffix 166, E nowhere 193, B host skeleton 36, A cross-tpl locator 14, C variant part 14. Open: pool-lookup case sensitivity not verified (we compare case-insensitively).
6. **Cadian finger curl** (a019f36): `tools/ue/ue_sm2_finger_curl.py` (generic, HEURISTIC) measures per-finger bind curl gap vs Manny and sets IKRetargeter retarget-pose offsets. Index curl mean Manny/before/after: cadian idle 54.8/40.3/51.9 (max|diff| 16.7 → 5.2), jog 32.8/15.5/26.2 (19.0 → 10.1); titus idle 54.8/58.2/53.4, jog 32.8/31.4/26.7 (5.8 → 10.3, regression). UE test assets `/Game/SM2_Work/Tests/mtl43/retarget/{cadian,titus}` (with _bak1/_bak2). `/workspace/mtl_fix/mtl43/curl/eval_summary.txt`. Open: ~6° clip-dependent jog residual on both rigs; titus jog worse → don't apply to titus-like rigs (gap < 5°) by default.
7. **VFX HEURISTICs (static RE, ghidra_proj3).**
   - sfx blendMode parser FUN_1426212d0 (PROVEN): "none"→0, "add"→1, "blend"→2, "premul_a"→3 at entry+0.
   - Define table FUN_142450650 (static init FUN_142332e80): SFX define group bits SFX_ALPHA_BLEND = bit0, SFX_BLEND_ADD_NO_ALPHA = bit1, SFX_IS_DECAL = bit2, SFX_USE_INVERTED_DECAL_TEST next (PROVEN slot order). The code that maps blendMode → these bits was not found (FUN_14243eb20 is a generic shader-type key builder, not it). Selector stays HEURISTIC.
   - UseSpecMap spec-flag writer, 0x400 vertex-alpha path, auto-exposure, UE falloff approximation: no new evidence this pass; still HEURISTIC.
   - New script `/workspace/rea/scripts/InsScanR.java` (range-limited instruction pattern scan) and `RangeDecomp.java`.

## Update mtl1-44 (2026-10-28 KST): SFX blend selector PROVEN (static RE) and applied per material

1. **Selector PROVEN** (sm2_retail.exe, Ghidra `/workspace/rea/ghidra_proj3` read-only; outputs `/workspace/rea/m44_*`):
   - td `shaders.sfx` loader **FUN_1425fd580** (0x1425fd76a / 0x1425fd77b): `blendMode` is matched with `_stricmp` against table **0x144cccd38** = {"blend"=0, "add"=1, "premul_a"=2}. The index is stored as a byte at desc+0. An unknown string fails the load; a missing key logs "Can't load %s!". modtools `sfx_sdr_desc.ps` has the same enum order.
   - Reflection **FUN_142605c60** (vtable slot 19; serializer FUN_142604070 is slot 18) fixes the layout: blendMode @0, isUseForPortals @1, alphaKillSmooth @4, softZ @0x110 (near @0x118), softFreshnel @0x128, writeDepth 0x460, zTest 0x461, noCull 0x462, particle 0x463, disable_fog 0x464, brightness 0x465, finalColor @0x568.
   - SFX key builder **FUN_142451840** (sfx shader-type vtable 0x1437f0670 slot 1, define table FUN_142450650). In the colour pass (`*pass == 0`): blendMode 0 sets bit0 **SFX_ALPHA_BLEND**; 1 sets bit1 **SFX_BLEND_ADD_NO_ALPHA**; premul_a sets neither. The bit order is cross-checked by bit2 IS_DECAL ← (flags & 0x100) && !particle, bit5 SOFT_Z ← softZ && !writeDepth, bit6 ← 0x111, bit7 ← 0x118, and bit16 SOFT_FRESNEL ← 0x128.
   - PSOs (`psos_sfx`, rt0; matched PS triples 173/256/201 and 588/743/631):
     - ADD_NO_ALPHA: One/One, alpha Zero/One.
     - ALPHA_BLEND: SrcAlpha/InvSrcAlpha, alpha Zero/One.
     - neither (non-DUDV): One/InvSrcAlpha, alpha One/Zero. PS 201 writes premultiplied rgb.
     - Correction to mtl1-38/SHADERS: "neither ⇒ SrcAlpha/InvSrcAlpha, alpha Zero/Zero" is only the DUDV/distortion pass (874 perms). There `*pass != 0`, so no blend bit is set.
   - Correction to mtl1-39/43: FUN_1426212d0 is the **game_material** pass-0x38 parser (none/add/blend/premul_a). It is not the td sfx loader.
2. **Code (generic rule):** `LibSaber.SM2.Assets.Effects.SfxBlend` (enum `SfxBlendMode`, `FromTd` reads only the sfx block's top-level blendMode, `State` / `Define` / `UeBlend` / `Composite`).
   - `EffectMeshMaterial.SfxBlend` is parsed per material.
   - The browser's `EffectRenderer` sets the blend per material: add → One/One; blend/premul_a → premultiplied over, which composites the same as both game PSOs.
   - The UE export `TriangleBatch` writes `ueBlend` (Additive / Translucent), `captureBlend` (the game PSO state), `sfxBlendMode` and `sfxDefine` per batch.
   - **HEURISTIC (final):** a .td with no blendMode stays additive. 66 of 107 extracted sfx blocks are like this; their value comes from the unextracted "sfx" preset. The capture shows One/One sfx draws. Explicit data: blend 30, premul_a 11, add 0.
   - `SM2_VFX_SFX_BLEND=0` restores "all sfx additive". `SM2_VFX_SFX_ADD=0` still forces over.
3. **Other VFX HEURISTICs: final, not pursued further.** UseSpecMap spec-flag writer, particle 0x400 vertex alpha, auto-exposure, and UE light falloff exponent 1 (APPROXIMATION) are left as documented in STATUS mtl1-38/39.
4. Tests: SM2.Assets 253 pass (new `SfxBlendTests`). Browser builds.

## Update mtl1-45 (2026-10-29 KST): hem spike fix (rotation-step cap), UE visual check of the cloth bakes, finger-curl min gap

1. **Hem spikes (generic, `PhysicsBake.cs` / `ClothSolver.cs`).** The remaining fast-run spikes were rotation-fit flips: the polar-decomposition fit of a cluster jumps to the other solution between frames.
   - FitCluster now caps the per-frame rotation step of every non-first fit at `SM2_PHYS_FIT_MAXSTEP` degrees (default 30, `0` = off; **APPROXIMATION**). It slerps from the previous rotation and recomputes the translation around the fit-particle bind centroid (`Fit0`), so the cluster does not swing about its centre.
   - ClothSolver lerps colliders between the previous and current frame across the sub-iterations (`SM2_PHYS_COLLIDER_LERP`, default on). The last iteration always uses the current colliders. `Reset` / `Translate` keep the previous-collider cache consistent.
   - tacticus_shield_run_fwd_intercessor on cc_calgar (box harness `/workspace/mtl_fix/m44/loop`):

     | Variant | steps > 30° | steps > 90° | max step |
     |---|---|---|---|
     | before | 217 | 12 | 144.7° |
     | lerp only | 185 | 12 | 170° |
     | cap only | 160 | 0 | 33.8° |
     | cap + lerp (default) | 139 | 0 | 33.2° |

     The loop seam stays at 0.06°. FitPrior 1 / 4 were rejected: the cloak freezes.
2. **UE visual check (mtl1-43 + the step above).** The CLI (`Bin\SM2Extractor_mtl44`) exported cc_calgar + the run clip twice: `Exports\mtl44\old` (FIT_MAXSTEP=0, COLLIDER_LERP=0) and `Exports\mtl44\new`. Both were imported to `/Game/SM2_Work/Tests/mtl44/{old,new}/cc_calgar` (untextured).
   - Captures at frames 0/6/8/26/30/34/55 (back + side) were made with `tools/ue/ue_sm2_pose_compare.py` in Simulate-In-Editor.
   - PC: `Captures\mtl_fix\mtl44\phys`. Box: `/workspace/mtl_fix/m44/m44_phys_sheet.png`, `m44_phys_spikes.png` (f26/f30/f34).
   - Result: old shows folded/twisted hem patches near the leg at f26 and f34; new is a smooth sheet at the same frames. The loop frames 0/55 match in both.
   - UE notes:
     - Editor-world SkeletalMesh/PoseableMesh components do not re-evaluate from the uemcp bridge (they stay in the ref pose). Use SIE.
     - EditorAssetLibrary errors in play mode, so resolve assets first.
     - Re-point SceneCapture ShowOnly at the PIE actor.
     - The import's post-process ABP (`ABP_*_SM2Phys`) was disabled per component, so the baked bones are what is seen.
3. **Cadian finger curl:** `ue_sm2_finger_curl.py` skips fingers whose bind-curl gap is < 5° (`MIN_GAP`). This removes the titus regression from mtl1-43. The ~6° jog residual is clip-dependent; it is final, not fixable by a retarget-pose offset.
4. Tests: SM2.Assets 253 pass.

## Update mtl1-46 (2026-10-29 KST): weapon skin attachments (purity seals) merged into the export

- **Data (ssl, REA-first):**
  - `firearm_skins_library.sso` skin `attachingActors{ slot = AttachingActorDescription{ locator, actorName } }`.
  - `ssl/geom_customizer/attaching_actors_library.sso`:
    - `locators` purity_seal_1..6;
    - `actors{ eagle_collision | demoskull_collision | skull_collision = IactorDesc{ type{ properties{ geom.nameTpl, prop_cloth_sim }, __type = "purity_seal_01" } } }`.
  - `ssl/weapons/accessories/firearm_seals/purity_seal_01.cls` has `geom.scale = 0.7`.
  - The type block overrides its `__type` class like ssl .cls inheritance (**HEURISTIC**), so the seals get scale 0.7 from the class.
  - Skin usage: eagle 131, demoskull 129, skull 116. Firearm tpls carry `purity_seal_N` nodes (wpn_bolt_rifle_01: 1–3).
- **Code:**
  - `WeaponCustomization.AttachingActors` / `LoadAttachingActors` / `SkinAttachments`.
  - `VariantResult.Attachments` [{Slot, Locator, Actor, Template, Class, Scale, Merged}] is written to `{model}.variant.json`.
  - `Meshes/MeshAttach.Merge`:
    - the attachment roots become children of the locator node with `Local * Scale`;
    - vertices are placed by `Scale * LocatorWorld`;
    - inverse binds are rebased (`Inverse(place) * IB`), so the bind pose is unchanged and the seal follows the locator when the weapon animates;
    - skin joints and part nodes are offset, and bone names are prefixed by the slot (`purity_seal_1_eagle_root`).
  - `AssetExporter.MergeAttachments` runs for firearm `--variant skin=...`:
    - it loads each actor tpl;
    - the cloth-sim setting picks paper_sim hidden or shown;
    - it merges `VisibleGeometry()` (seal on LOD0, paper_rend on its own pool).
  - `SM2_WPN_ATTACH=0` turns this off. A missing locator or tpl is a variant note.
- **Limits:**
  - The paper cloth is not simulated: paper_rend hangs straight in its bind pose (**APPROXIMATION**). The seal's prop_cloth_sim is not baked.
  - The Forge Lens 3D preview still shows the weapon without seals; only the export merges them.
  - The skin `material` (mastercrafted etc.) is still only noted (applied since mtl1-47).
- **Evidence:** CLI `Bin\SM2Extractor_mtl46` on wpn_bolt_rifle_01:
  - `skin=skin_03` merged 2 (seal_1 eagle, seal_3 skull);
  - `skin=skin_04` merged 3 (seal_3 eagle, seal_2 demoskull, seal_1 skull);
  - output in `Exports\mtl46\skin_0{3,4}`.
  - Box renders (`tools/glb_preview.py`): `/workspace/mtl_fix/m45/skin_03_view.png`, `skin_04_view.png` show the seals on the receiver side and the paper strips hanging down.
- Tests: `WeaponAttachTests` (class-scale inheritance, skin parse, merge placement + bind-pose preservation, missing locator). SM2.Assets 257 pass.

## Update mtl1-47 (2026-10-29 KST): skin materials (melee + firearm) and the power fist right-hand hide

- **Skin material (data):**
  - Melee (`melee_skins_library.sso`) and firearm (`firearm_skins_library.sso`) skins carry `material` = mastercrafted | red | blue | white | yellow | green | violet | twitch | chaos.
  - Most material skins also switch actor: melee `meleeActorOverride`, firearm `overridedActorName` (e.g. chainsword 01_red → chainsword_01_artificer_01; bolt rifle 01_red → arifle_bolt_rifle_artificer_01).
  - The textures exist as `<base>_<material>{,_nm,_spec,_em}` siblings (`td/wpn_chainsword_01_artificer_01_01_red{,_nm,_spec}`). Blue/white/violet/yellow ship the diffuse only (mtl1-48: they reuse the red/green NM/spec through the td `materials` block).
  - Melee actors list `excludeObjectsToApplyMaterial` (VFX helpers, `body` on some).
- **Code:**
  - `SkinMaterial.Candidate` (`base_nm` → `base_mat_nm`, else `base_mat`) and `SkinMaterial.Apply`:
    - swaps Texture/Normal/Specular/Emissive/Maps on every part and LOD when the sibling exists in the catalog;
    - skips excluded objects and their subtrees.
  - `AssetExporter.ApplySkinMaterial` runs after the melee/firearm variant. `SM2_SKIN_MATERIAL=0` turns it off.
  - `VariantResult.Material`, `MaterialSwaps` and `MaterialExclude` are written to variant.json.
  - Override skins:
    - `MeleeCustomization.OverrideSkinsFor(tpl)`: material-only on the target tpl;
    - `WeaponCustomization.OverrideUidFor(tpl, skin)`: the mgs `<override>_client.cls` nameTpl → tpl; regions, attachments and material apply.
  - So `--variant skin=01_red` on `wpn_chainsword_01_artificer_01` / `wpn_bolt_rifle_01_artificer_01` gives the in-game red look.
  - Fixed the melee factory parse: `nameTpl` is under `type.properties.geom`, so actors with their own tpl (artificer/relic) now resolve to it, not the class tpl.
- **Power fist hand (data):**
  - `ssl/body/customization/prop_power_fist_customizer_base.prop` has `powerFistIds` and `visibilityUnchangableMeshes` [torso_r_gauntlet, upper_arm_librarium_T, upper_arm_librarium_G] ("Meshes (of hand), which visibility can't be changed by this system").
  - Its sslbin has SetRightHandVisibility / FilterRightHandMeshes / handRegion.
- **Power fist hand (code):**
  - `powerfist=KEY` now sets `VariantRequest.HideHandSection = RIGHT_GAUNTLET` (**HEURISTIC**: the CharacterBodySection constant in the bytecode is not decoded).
  - It also sets `HandKeep`, which is read from the .prop with a built-in fallback.
  - ArmorVariant hides the active RIGHT_GAUNTLET region's meshes (subtrees) minus the kept ones. The fist objects stay forced on.
  - If none of the fist objects exist in the tpl (engineer), the hand is kept and a note is added.
- **Evidence:** CLI `Bin\SM2Extractor_mtl47`, output in `Exports\mtl47\`:
  - `cs_art_01_red`: 303 texture references swapped to `_red`. The written textures are `wpn_chainsword_01_artificer_01_01_red{,_nm,_spec}`.
  - `br_art_01_red`: 306 swapped.
  - `wpn_chainsword_01 skin=01_red`: noted "no _red siblings" (the skin switches tpl in game).
  - `raider_pf01` (cc_sm_constructor_raider preset=1, powerfist=01): hides `right_gauntlet_raider_common_01` (raider_hand_R_01, raider_arm_R_01).
  - Box images:
    - `/workspace/mtl_fix/m47/m47_powerfist.png`: base vs fist, the forearm replaced cleanly and the upper arm kept;
    - `/workspace/mtl_fix/m47/m47_chainsword_red.png`: base vs `_red` diffuse.
- **Limits:**
  - The Forge Lens 3D preview does not swap materials.
  - The auto-bolt and bolt rifle 01_red both target the same artificer tpl, so the first skin group wins: `UidsByTpl` maps the artificer tpl to `arifle_auto_bolt_rifle` via its firearm_versions cls.
  - `mastercrafted` on melee is a damage modifier in melee_modifiers_library; visually it uses `_mastercrafted` siblings where they exist.
  - The browser part-picker preset list for artificer tpls is not extended.
- Tests: `SkinMaterialTests` (candidate, apply, exclude, melee override, firearm override, hand hide, no-fist keep). SM2.Assets 267 pass.

## Update mtl1-48 (2026-10-29 KST): in-game comparison by static RE (no captures); skin materials now follow the td `materials` block

- **Skin material, PROVEN path (static RE of sm2_retail.exe, Ghidra):**
  - ssl native `ApplyMaterialSuffix` (registered in FUN_14143e030 with SetMaterialByRegion, ChangeMaterial, GetAppliedMaterialSuffix(ByRegion)) → FUN_141342870 → FUN_141ef5170(obj, name, suffix, flag, recursive): empty name = root object, recursive walks the subtree. `ApplyMaterialSuffixByRegion` → FUN_141ef54b0.
  - FUN_1420bfa40: per submesh, takes its material, FUN_14261c830(mat, …, suffix) looks the suffix up in the per-material variant list built by FUN_14261c000 from FUN_14261b0c0, sets it (FUN_1421dc890) and records the suffix (FUN_141d8ec60).
  - The list comes from the diffuse texture's `.td` `materials` block (FUN_14262abb0 reads "materials"; warnings "No %s.td found for texture!", "No default material found for texture '%s'").
  - Data: `td/wpn_chainsword_01_artificer_01_01.td` has `materials { default, red, blue, white, green, yellow, violet }`, each `shaders.glt.tex { diffOverride, NM, spec }`. The tpl layers already name `subMaterial = "default"`.
  - The mtl1-47 naming rule was wrong for 4 of 6 colours: `blue`/`white` use the **red** NM/spec, `yellow`/`violet` use the **green** spec.
- **Code:** `SkinMaterial.TdOverrides` / `ApplyTd` (diffOverride→diffuse, NM→normal, spec→specular, em→emissive; other keys reported). `ApplySkinMaterial` uses the td block first (`VariantResult.MaterialSource = "td"`) and falls back to the mtl1-47 sibling naming (`"naming"`, HEURISTIC) only when no part's diffuse td has the block. material.json slot textures follow the swap.
- **Evidence:** CLI `Bin\SM2Extractor_mtl48`, `Exports\mtl48\`: chainsword artificer blue 100 parts / 300 refs (blue diffuse + red_nm + red_spec), red 300, yellow 200 (yellow + green_spec); bolt rifle artificer red 148 parts / 296. Box `/workspace/mtl_fix/m48/m48_td_material_textures.png`.
- **VFX vs the user's in-game photos (bench rerun at c95af37 vs the 10-28 16:45 run before mtl1-44):** `/workspace/mtl_fix/m48/sheets/oldnew_*.png`, PC `Captures\mtl_fix\mtl48\`. The blend selector removes the blue/white cast on the bolter muzzle (mean abs diff 7.1) and the grenade early frames (5.7) and moves both toward the in-game orange; smoke/trail/blood nearly unchanged (<1.1).
- **Comparison table (static, no in-game captures per the user's rule):**

| Track | In-game evidence | Export | Verdict |
|---|---|---|---|
| VFX blend | 6 user photos (bolter muzzle, grenade, smoke, trail, blood) + FUN_142451840 | blend selector on | closer; grenade core still brighter than in-game (exposure HEURISTIC) |
| Skin material | ApplyMaterialSuffix → td materials (above) | td variant | PROVEN data path |
| Power fist hand | prop `visibilityUnchangableMeshes`, SetRightHandVisibility | RIGHT_GAUNTLET hide | section constant PROVEN from sslbin bytecode (datapars); subtree hide HEURISTIC |
| Purity seals | skin attachingActors + locators | merged, scale 0.7 | static data; paper not simulated |
| Physics | jiggle loop 0x14147e5b5 port | JiggleSolver | proven port; no in-game motion reference |

- **Limits:** the Forge Lens preview still does not swap materials; td variants that set non-texture shader params are not applied (only tex keys).
- Tests: `SkinMaterialTests` + TdOverrides / ApplyTd. SM2.Assets 269 pass.

## Update mtl1-49 (2026-10-29 KST): firearm uid rule, colour schemes, context playback (effect ↔ model + animation + weapon), props/effects toggles

- **Items 2+3 (commit bddd777):** generic firearm uid resolution (explicit `uid=` > skin override actor, mgs/ before firearm_versions/, then the shorter uid > own actor > prefix owner), so bolt rifle and auto-bolt rifle no longer swap. Colour schemes: td `materials` names + the game's display names + armor_color_library swatches; Forge Lens colour picker (swatches + game names, hover preview, material swap in the preview); `material=` on export. Tests: FirearmResolveTests.
- **Items 6–8, context playback (one rule set for both directions):**
  - `LibSaber.SM2.Assets/Context/ContextIndex.cs`: all `.cls` + `.cls.resource` in the catalog (2510 each), parsed once in parallel. Per class: `geom.nameTpl`, morpheme `nameBundle`, parent `__type`, weapon path, `.cls.resource` `linksDesc` (owned `.sso` blueprints), `linksDescVisual` (owned `.sfx`) and `parents`. Lazy `Detail()`: `sfx.list` key → effect, inventory weapon types / uids, `prop_model_weapon_holds` right/left hold, dummy locator, drop slots, equipment category, inline blueprints.
  - `ContextResolver.cs`:
    - `ForEffect(effect, map)`: effect owners (resource, then sfx.list, then text mentions) → BFS over children + users (depth 4) to a character class with a tpl + bundle; ranks event link > PROVEN > not `_base` > not `auto_` > depth. Events = blueprint morpheme events of the owner's blueprints that start the effect (directly or through the sfx.list key).
    - `CharactersFor(tpl, clip)`: exact tpl, else the clip's `_<bundle>` suffix (HEURISTIC). `WeaponsFor`: inventory (PROVEN), else clip-name token score; a clip with a `Shoot` track takes firearm `_client` classes only. `HoldFor`: `prop_model_weapon_holds`, drop-slot tokens, offhand → left, default GUN_R. `ShootCues`: the weapon's `muzzle*` sfx slots (HEURISTIC link Shoot track → muzzle).
  - `AnimEventVfxMap.AddNodes` (inline blueprints), source dedupe; `EffectHost(boneNames, sourceNames)` alias names (the merged weapon's `wpn_<bone>` also answers `<bone>`; host names win) and `Attach(..., scope)` (socket `scope_name` first); `AnimEventEffects(resolve, scope, trackCues)`; `SaniReader.ReadEvents` (event tracks without curves, fast clip scan).
  - Browser (`MainWindow.Context.cs`): VFX bar **Playback: Effect only / With model + animation**. The CONTEXT bar (shown with the ANIM bar) has **Show props/weapons**, **Show effects**, character picker (effect mode), clip picker, weapon picker (several fit), Shoot slot picker (several muzzle slots), **Effect only** back button, and a status line with PROVEN/HEURISTIC labels. The weapon tpl is merged into a copy of the model under the hold locator (prefix `wpn`, textures merged); both toggles off = model + animation only.
  - Proven chains on real data: `fx_terminator_sorc_stave_ray_prep` ← `wpn_melee_force_stave_terminator` (.cls.resource linksDescVisual, blueprint_vfx_terminator_sorcerer_stave.sso) ← `npc_occult_terminator_sorcerer_client` inventory; event `vfx_ray_prepare` (@0.79 s in terminator_sorc_onslaught_ray_from_psychic_2_right_scrab_terminator_sorcerer); socket `stave_fx_src_1` on the stave. Reverse: cc_rubric_marine + `rubric_infernobolt_shoot_rubric_marine` → npc_rubric_marine_client (HEURISTIC bundle) → rubric_bolter_client (HEURISTIC tokens) on GUN_R → Shoot@0.03 s → `fx_rubric_bolter_muzzle_client` (rubric_bolter_client.cls.resource linksDescVisual).
  - QA env (screenshots without clicks): `SM2_CTX=1`, `SM2_CLIP=<clip name part>`, `SM2_TIME=<s>` (pause), `SM2_CAM=yaw:pitch:distMul`, `SM2_SHOW_PROPS=0`, `SM2_SHOW_FX=0`, `SM2_CTX_CHAR/CLIP/WEAPON`; `SM2_OPEN` also takes a bare file name.
  - Screenshots: PC `Captures\mtl_fix\mtl49\ctx_sorc_stave_ray_prep.png`, `ctx_rubric_fire_weapon_muzzle.png`, `ctx_rubric_toggles_off.png`.
  - Limits / HEURISTIC: bundle-suffix character fallback, token weapon pick, Shoot → muzzle slot, clip ranking without an event link, hand-bone fallback without a hold locator. The rubric preview is untextured grey (pre-existing). The ANIM list can still say "Finding animations…" while the context clip already plays.
  - Tests: `ContextPlaybackTests` (resource parsing, effect → weapon → character + event, bundle fallback, weapon/hold/muzzle, Shoot track cue on the weapon socket, alias precedence). SM2.Assets 278, Extractor 43, LibSaber.Tests 5 pass; HaloCEA 4 fail (needs Halo data, unrelated).
- **Item 5, symbol database (SYMBOL_DB, the "genome map"):** `docs/SYMBOL_DB/INDEX.md` + 11 domain files (hand-written overview + generated symbol table) + `index.tsv` (symbol | address | domain | purpose | status | doc | dump | strings), 727 symbols (412 documented, 315 decompile-only with string hints). Generator `tools/symbol_db/build_symbol_db.py` merges HANDOFF + docs/handoff + the box Ghidra dumps (/workspace/rea, /workspace/mtl_fix/m48); rerun it after new RE. AGENTS.md read order now points at the map first.

## Update mtl1-50 (2026-10-29 KST): REA-first RE, SYMBOL_DB rename + coverage, HEURISTIC re-check, disk cleanup

- **REA is the primary RE path.** One-shot `rea` CLI calls re-import the 81 MB exe every time (6.8.0 has no project reuse; `REA_GHIDRA_PROJECT_PATH` is not supported). `tools/re/rea_daemon.py` keeps one `rea mcp` session (about 22 min startup with the reduced analyzer set) and serves a file queue; `tools/re/rea_call.py <tool> '<json>'` is the client. Setup, env and rules (never cancel/kill; 10 MiB paging) in WORKFLOW.md "REA". AGENTS.md, RE_NOTES and SYMBOL_DB/INDEX now say REA first, Ghidra headless as fallback.
- **SYMBOL_DB (was docs/RE_MAP):** renamed to the industry term; generator `tools/symbol_db/build_symbol_db.py`; `docs/RE_MAP/README.md` is a pointer stub. New: `docs/SYMBOL_DB/rea_string_xrefs.tsv` (5,062 asset-relevant functions found by REA string xrefs, with domains, a "in symbol DB" flag and the strings) from `tools/symbol_db/rea_string_xrefs.py`; coverage section in INDEX.md: 156,646 procedures, 743 symbols = 0.47 %, asset functions 193/5,062 = 3.8 % (vfx 11.9 %, rendering 11.2 %, customization 9.3 %, materials 9.2 %, animation 5.4 %, shaders 5.1 %, meshes 2.9 %, physics 2.5 %, containers 1.2 %).
- **HEURISTIC re-check with REA (details RE_NOTES "mtl1-50"):** missing sfx blendMode = add → **PROVEN** (preset chain mtl_template_sfx.td → sfx_sdr_preset_default.sd `blendMode = "add"`; comments in SfxBlend/CaptureRules updated, behaviour unchanged). .td sfx non-texture params → **PROVEN** key list with desc offsets and preset defaults. Still open: auto-exposure/grenade core (lead: emissive.adaptiveIntensity, default false, may be the sfx[560].w weight), light falloff (attenCurveParam / isConstAtten / useInverseSquaredFalloff schema PROVEN, curve in the light shader not traced, our UE exponent 1 stays APPROXIMATION), 0x400 vertex alpha (re-confirmed, writer not found), UseSpecMap (usage-mask chain to texture loaders, consumer not found), power-fist section (not in the exe; script bytecode).
- **Disk cleanup (item 4):** PC C: freed 142.3 GB (free 50.4 → 192.8 GB), UE `/Game/SM2_Work/Tests` about 16.2 GB (kept bulk_fix, chars_cloth, chars_phys, physics, retarget; pre_step3 and sani_before skipped because assets outside Tests reference them), box 716 MB. Lists in the box decision log `/workspace/mtl_fix/overnight_decisions.md`. Scripts: `Scripts\mtl_fix\ue_tests_cleanup.py` (dry/delete with AssetRegistry referencer check), `bulk_cleanup.ps1`, `file_cleanup.ps1`.

## Update mtl1-50b (2026-10-29 KST): REA pass 2 on the four open HEURISTICs

- **Exposure / grenade core → PROVEN** (RE_NOTES "mtl1-50b"): CB_DYNAMIC_SFX writer FUN_1424540e0 sets SFX_EMISSIVE_PARAMS.w (byte 560) = emissive.adaptiveIntensity ? 1 : 0; AVG_LUM_CLAMPED = clamp(avgLum, 0.05, 50) (FUN_141e90d00). **Code:** k of `SfxExposure` is now per material (`SfxExposure.AdaptiveFromTd` → `EffectMeshMaterial.SfxAdaptiveIntensity` → `EffectRenderer.SfxLumK(material)`, used by the browser mesh pass and `VfxUeExport.MeshBatches`); default 0 (preset default false), so non-adaptive sfx meshes now follow the frame exposure like in game; adaptive ones (part_candle, part_smoke_new_04, water_drops_01) keep k = 1. `SM2_VFX_SFX_LUM_K=<k>` forces one k (`SM2_VFX_SFX_LUM_K=1` = old look). Test `CaptureRulesTests.SfxAdaptiveIntensityIsK`.
- **Light falloff:** deferred light shader formula PROVEN (sat(1 - d^4·P1.w)^2 · P0.w, no curve slot); the CPU writer of COMMON_DYN_LIGHT_PARAMS / reader of attenCurveParam is not found (only serializers reference it) → stays HEURISTIC / APPROXIMATION, no code change.
- **0x400 vertex alpha:** flag lives in a GPU-bound per-emitter record (FUN_1417e1e50, rec+0x18); no CPU consumer exists (the only other 0x400 tests are zlib). Our vertex-colour emulation stays HEURISTIC, no code change.
- **UseSpecMap:** writer of CB_OBJ_MATERIAL_DATA byte 336 still not found (store scan too generic, likely memcpy'd block) → HEURISTIC.

## Update datapars (2026-10-29 KST): sslbin (compiled ssl) reader, data-driven power fist hand rule, NPC layer rules / probability default

- **Data found:** `ssl/body/customization/prop_power_fist_customizer_base_release.sslbin` (class PropPowerFistCustomizerBase), with `prop_character_customization_state_base_release.sslbin` (GetMeshesByBodySection, slot 82), `character_body_section_release.sslbin` (CharacterBodySection constants, RIGHT_GAUNTLET = 6) and `prop_power_fist_customizer_base.prop` (powerFistIds, visibilityUnchangableMeshes). Users: 23 raider/tank `.cls` use `prop_power_fist_customizer_by_melee_state` (ShowFist → SetRightHandVisibility(false), HideFist → SetRightHandVisibility(true)), 2 use `_by_loadout`. No other melee/gauntlet hand-hide system exists in the data.
- **Format (SSLB):** 16-byte header + protobuf (strings, classes with members / constants / field defaults, method bytecode). Layout and opcodes in `docs/SYMBOL_DB/scripting.md`; reader `A/Scripting/Sslb.cs`; dump/disasm `tools/re/sslb.py` (+ `pb.py`). Field numbers INFERRED from structure and checked against the names they compile.
- **Rule (PROVEN, bytecode):** `SetRightHandVisibility(v)`: `meshes = propCustomizerState.GetMeshesByBodySection(6)`; `FilterRightHandMeshes` copies the list and removes `visibilityUnchangableMeshes`; `handRegion.SetMeshes(filtered)`; `propGeomCustomizer.Show/Hide(handRegion)`. `Init`/`HideFist` → `HideAllFists` hides `powerFistIds`.
- **Code:** `A/Customization/PowerFistHandRule.cs` reads the section int from the call argument (no name guessing), maps it through the CharacterBodySection enum and checks the filter reads the keep list; `MeleeCustomization.HandRule` (Load) replaces the `HandSection` const. VariantRequest `FistIds` (powerFistIds, all constructors via AssetExporter / Forge Lens Kitbash; `powerfist*` name prefix only when the data is missing) and `HandRuleEvidence` (note text). Fallback when the scripts are missing: built-in RIGHT_GAUNTLET, labelled HEURISTIC. Still HEURISTIC: hiding listed meshes as subtrees, keeping the hand on tpls without fist objects (engineer), skipping `ch_` fists on `cc_sm_` tpls.
- **Other data HEURISTICs:** NPC `requiredLayer` / `disabledByLayer` now follow `InstConstructorTemplate.GenerateRandomPreset` (inst_constructor_template_release.sslbin): ONE pass in bodyLayers order, filled = pick present with non-empty geomObjNames, a removal is seen only by later layers (was a fixed point; retail results unchanged, the only forward references are `head requiredLayer helmet` and helmet has no rule). Dict order = .sso order stays HEURISTIC. Variant `probability` default 1.0 is PROVEN (class field-default table of InstConstructorMeshTemplateVariant) and is read at load (`InstConstructorLibrary.ApplyScriptDefaults`); the native distribution draw (weights / sum) stays HEURISTIC. Firearm `main` region stays HEURISTIC: PropGeomCustomizerEquipment.Init = HideAll, then initialBehaviour 0 ShowActiveRegions / 1 ShowDefault(defaultRegion) / else ShowActiveMaterial; it never names `main`, and the firearm skin scripts (`ssl/weapons/creator/customization/firearm_skin_release.sslbin`) are not extracted yet.
- **Tests:** `PowerFistHandRuleTests` (synthetic SSLB: parse, section from bytecode for two values, fallback, field defaults + ApplyScriptDefaults, single-pass layer order; retail with `LIBSABER_SM2_SSL`: RIGHT_GAUNTLET / 6 / keep list / 9 fist ids, probability 1.0). SM2.Assets 287 pass with retail on.

## Update phys52 (2026-10-29 KST): Titus/all characters — jiggle key fix, prop_motion_damper_controller, no world double-simulation

Owner requests (verbatim intent, ledger F52/F53/F48/F49): apply both fixes from the Titus skull-box investigation generically to all characters; rebuild the plugin (restart pre-authorized, crash protocol); parse the motion damper (authority `.cls` when the client lacks it), emit it in the sidecar, implement it in UE as a spring-damper with HEURISTIC labels; re-apply Titus physics with `.bakN` backups (never touch `/Game/SM2_Work/Original/*`, `/Game/Work/*`, never save `Lightning_01`, always stop PIE/SIE); verify 34 jiggle bodies and a swinging box in a walk clip (before/after into box `/workspace/mtl_fix/titus_phys/after/`); rebase + `push.ps1` ff-only alongside the Forge Lens worker; modest memory; then (steering) remove the 1,626 PIE "ComplexAsSimple collision" warnings generically. Pending from the owner: resume the interrupted REA work (F49, `attenCurveParam` / `UseSpecMap`).

- **Root cause 1 (PROVEN, code):** the plugin read jiggle `"bone"`, the sidecar writes `"Name"` → every character got 0 jiggle bodies (Titus 0/34). Now `"Name"`, fallback `"bone"`; missing/skipped jiggles are counted (`jiggleMissing=`) and logged (`jiggleSkipped=[..]`). Jiggles whose bone already had a kinematic cloth-anchor body (Titus Belt_jiggle, Shin_L_jiggle) are switched to simulated (`jiggleFromKinematic=`). `tools/ue/ue_physlive.py` read the same wrong key (fixed).
- **Jiggle → UE mapping (DERIVED from JiggleSolver, sidecar `jiggles[].ue.rigidBody`):** body on the jiggle bone, joint at the bone origin, twist locked, SLERP acceleration drive toward `restDirUe` with stiffness k/m and damping = game damping, swing = max angle to the clamp-box corners (0.5..60°, clamp HEURISTIC), gravity as a point mass at `comDistanceCm` (980/d = g/offset), `locked` when offsetDistance = 0, mass 0.25^depth (HEURISTIC, mimics the game's one-way parent→child order).
- **Root cause 2 (PROVEN, data):** the box chain is driven by `prop_motion_damper_controller` (`springDampers[{timeScale, windFlutter?, joints[{name, stiffness, dampingRation, mass, jointAngle, gravity, isParent?, isInvertedLook?}], constrains[{upLocatorName, downLocatorName, radius, hasExternalLocators?}]}]`), which only the authority classes carry (168 files; 96 client, 61 authority). `PhysicsLocator` now merges the dampers from the chain, then `Counterpart()` (`*_client` → `*_authority`), then all candidates; the cloth/jiggle search is no longer stopped by a damper-only class. Defaults when a field is absent (exe defaults not recovered, **HEURISTIC**): mass 1, jointAngle 30, gravity 0, stiffness 0, dampingRation 1 (listed per joint in `source.defaulted`). Titus: `pc_marine_pve_story_titus_assault_authority.cls`, timeScale 5, box_jiggle_02 (k 7.21, ζ 2.18, m 1, 30°, g 0), box_jiggle_end (k 6.81, ζ 2.21, m 2, 45°, g 0.5), capsule cloth_cdt_2_a→_b r 0.26 m.
- **Damper model in UE (HEURISTIC, exe step not reversed; sidecar `springDamperNote`):** `PA_<mesh>_SM2Damper` + a second RigidBody node ("SM2 motion damper (phys52)", base-bone space on bone 0, WorldAlpha/DampingAlpha 0, gravity 980·max(gravity)). Per joint: capsule body (r 3 cm) from the bone to `segmentTo`, ω0 = √(k/m)·timeScale, drive ω0² / 2ζω0 (acceleration), cone = jointAngle, twist locked; leaf joints skipped and logged (box_jiggle_end). `constrains` → kinematic capsule on the up locator, colliding only with the joint bodies, shrunk so the rest pose starts clear (Titus 26 → 23.9 cm). isParent / isInvertedLook / hasExternalLocators / windFlutter recorded, not simulated. Not baked into clips. `TuneRigidBody` only edits the main node.
- **Root cause 3 (PROVEN, engine source + live, steering request):** `PhysType_Simulated` bodies are also simulated by the SkeletalMeshComponent in the world scene whenever its collision has physics (`GetSimulatingAndBlendWeight`, BodyInstance.cpp: blend weight 1), so every SM2 PA was simulated twice (world ragdoll blended over the AnimNode) and, with the project's `DefaultShapeComplexity=CTF_UseComplexAsSimple` (DefaultEngine.ini:130, owner setting, untouched), each such body logged "Trying to simulate physics on ... but it has ComplexAsSimple collision" (1,626 in the Message Log; 8,120 lines in the previous editor log). Fix: simulated bodies are `PhysType_Default` + both RigidBody nodes `bUseDefaultAsSimulated = true` (Default follows the component's SimulatePhysics, which we never set), and every body gets `CollisionTraceFlag = CTF_UseSimpleAsComplex` (`NormalizeBodies`). New `FixWorldPhysics(PA, ABP, Report)` migrates older PA/ABP pairs in place (back them up first). Measured after: 0 warning lines in `Saved/Logs/UE_VFX_57.log` over two SIE runs with collision on; editor tick ≈20 → ≈50 fps with two Titus actors.
- **CLI:** box publish `/workspace/sm2cli_phys52` → PC `Desktop\SM2_Work\Bin\SM2Extractor_phys52` (ForgeLens_latest untouched). Export `Exports\chars_phys52\cc_titus_assault`: 6 cloths, 34 jiggles (all with `ue.rigidBody`), 1 damper from assault_authority.
- **Plugin:** `Build\sm2phys\pkg52b` (BUILD SUCCESSFUL), installed to `K:\Unreal Projects\UE_VFX_57\Plugins\SM2PhysicsSetup`; previous installs kept as `Build\sm2phys\installed_SM2PhysicsSetup.bak25` (pre-phys52) and `.bak26` (phys52 without the world-physics fix). Build fixes: a local named `PI` collided with UE's `PI` macro (renamed `ParIdx`); the build src header was stale (repo header copied).
- **Titus re-apply:** mesh `/Game/SM2_Work/Tests/chars_phys/cc_titus_assault/cc_titus_assault` backed up on disk as `cc_titus_assault.uasset.bak1` (before PA_6) and `.bak2` (before PA_7). Current: `PA_cc_titus_assault_SM2_7`, `ABP_cc_titus_assault_SM2Phys_7`, `PA_cc_titus_assault_SM2Damper_1` (PA_6 / ABP_6 / SM2Damper from the first pass kept). Report: `simulated=812 kinematic=160 constraints=956 jiggles=34 jiggleMissing=0 jiggleFromKinematic=2 damperBodies=1 damperSkipped=1 damperCapsules=1`. Check (`ue_phys52_check.py`): **JIGGLE_BODIES 34/34**, damper box_jiggle_02 body type 0 (Default), drive 180, cone 30.
- **Before/after (SIE, PhysTestMap, walk clip `tacticus_wpn1_walk_fwd`, 2 actors, `ue_phys52_live.py`):** before = box chain animation only (PA_5 had Belt_jiggle kinematic and no box_* bodies, verified with DescribeBody). Box (box_jiggle_02→end) swing about its mean direction: before 2.7° mean / 5.3° max, after 33° mean / 61° max, periodic with the gait, no NaN. Files: `titus_box_walk_before_after.gif`, `titus_box_walk_before_after_mid.png`, `titus_box_swing_chart.png`, `metrics_run3.json`, raw `run3/rec.json`. The amplitude is large and the tabard (child of the now-simulated Belt_jiggle) also moves more; the game amplitude is unknown (no capture, R13) → tuning stays **HEURISTIC** (knobs: timeScale use in ω0, capsule shrink, cone).
- **Tests:** `SpringDamperTests` (parse + defaults, authority counterpart merge, sidecar damper + jiggle rigidBody); SM2.Assets 295 pass (after rebase).
- **Not done / next:** re-apply the fixed plugin to the other characters (re-run `ue_bulk.py`/`ue_resetup.py`, or `FixWorldPhysics` on existing PA/ABP after backup); REA resume (F49).

## Update trail53 (2026-10-29 KST): weapon swing trails (ribbon) in Forge Lens context playback

Owner request (ledger F55, "트레일 fx 뷰어 표현"): render weapon trail FX as a ribbon in context playback (model + animation + props/effects), driven only by game data, inside the event windows, respecting the Effects toggle, generic for every melee weapon with trail data. No Niagara; **UE export of trails is out of scope** (not done).

- **What a weapon trail is (PROVEN, REA fallback headless Ghidra on ghidra_proj3, dump box `/workspace/mtl_fix/trail/re/tri1.c`):** `.sfx` node `__type = "sfx_trail_triangle"` (string @0x14363c4d0, class SfxTrailTriangle @0x143648c80), registered by FUN_141848c40 as a subclass of `sfx_trail_base` like `sfx_trail`; FUN_141a6e410 compares the type name. Its typeParams union reader FUN_1418296f0 maps `local__triangle` (@0x143646c18) to type 6 and reads only `triangle.name`, so the `billboard`/`tape` blocks found in these files are stale editor data. trailParams serializer FUN_141839b50: maxTrailLength, maxSegLength, angleMin, timeLifeSeg, scaleFreq, scaleRange, displacement, scaleArgDep, inheritedParams, width, addFirstPointOnStart, typeParams → the `trailParams.color/colorIntensity/texture/fadeParams` blocks some files carry are not read (INFERRED from that list); the look comes from `visuals` (same as sfx_trail). Shader define TRAIL_TRIANGLE @0x143803c88 (FUN_1424e32c0). The runtime ribbon builder was **not** traced.
- **Data chain (PROVEN, data):** weapon `.cls` `prop_blueprints` / `.cls.resource` → blueprint (`ssl/weapons/controllers/melee_controller/combo/blueprint_wpn_melee_effects_<weapon>.sso`, inline graphs in some cls e.g. khopesh, tzaangor) → `BlueprintNodeEquipmentUserMorphemeEvent` (`hit1_start_anim`, `hit4_start_trail` …) → `BlueprintSfxLibraryStartsfxtrailtriangle {nameSFX, locator1, locator1Offset, locator2, locator2Offset}` or `BlueprintNodeVfxTriangleRestart {vfxUidRef, locator1Ref, locator1OffsetRef, locator2Ref, locator2OffsetRef}` (offset `def` = Vector3d x/y/z, missing = 0), chained through OnStartSFX/OnExit pins → stopped by `mele_<n>hit_trail_stop_effect` → `BlueprintNodeStopPackOfVfx.vfxList` (also VfxStop, duration OnDeactivate). Chainsword: `trail_mele_01_yellow` (+ `trail_melee_distortion_01`, `trail_melee_dark`) on the edge **sfx_trail3 → sfx_trail2** (chainsword tpl locators sfx_trail, sfx_trail1–5, no "tip"); combat knife same; power sword `trail_melee_power_sword_01` on t5 → t1. `trail_mele_01_yellow` is the chainsword cls `sfx.list` entry = prototype `trail_melee_01_yellow` with `trailParams.timeLifeSeg 0.15`, `visuals.colorIntensity 5 × colorIntensityMultiplier 0.4` (merged now, see code). light_slash_1 (`tacticus_attack_melee_chainsword_light_slash_1_intercessor`, 2.167 s): hit1_start_anim @0.181 s … mele_1hit_trail_stop_effect @0.813 s.
- **Rule (code; HEURISTIC where marked):**
  - `AnimEventVfxMap`: triangle starters carry `TrailEdge(Locator1, Offset1, Locator2, Offset2)` on `EventVfx.Edge` (both spellings).
  - `EffectDefinition.SweptTrails` (TrailDef, `TrailShape.Triangle`); a triangle trail without `blendType` = add (mtl1-50 rule); `TimeLife` = node timeLife.
  - `AnimEventEffects.Swept`: window = start event → first of (stop event naming it, a later start of the same trail on the same edge, start + node timeLife, clip end) (**HEURISTIC** "first of"). Distortion-target trails (`isDistortion`) are skipped in the colour pass.
  - `SweptTrails.Build` (**HEURISTIC**, field-name driven): every 1/120 s of clip time the edge locator1+offset → locator2+offset (locator worlds re-posed from the clip, In-place rebase/root-motion removal matched to what the scene draws) is a cross-section that lives `timeLifeSeg`; n = age/timeLifeSeg; colour = `visuals.color` (t,A,R,G,B) at n × colorIntensity (the authored gradient is the age fade, e.g. yellow → black by 0.68); U = n − scroll, V = 0 at locator1 / 1 at locator2; degNear/degFar per vertex; maxTrailLength clip. Head = the drawn pose, so scrubbing is deterministic (no simulation state).
  - `HostVfx`: swept batches only with event playback on (= context **Show effects** toggle); `SM2_SWING_TRAILS=0` hides them; status line lists swing trails with windows. `ContextIndex.SfxEntries` + `ContextResolver.Overrides` + `EffectLibrary.ResolveNode` apply the owner's sfx.list entry (weapon first) to swept trails only.
- **Screenshots (PC `Captures\mtl_fix\trail53\`, box `/workspace/mtl_fix/trail/`):** `cs_slash1_t030/042/060.png` (+ crops, `cs_slash1_strip.png`): cc_intercessor + wpn_melee_chainsword, light_slash_1 at 0.30/0.42/0.60 s. Captured with `Scripts\mtl_fix\trail53\fl_shot_pw.ps1` (PrintWindow, no foreground, Idle priority). Visible: dark fan (trail_melee_dark) behind the blade along the swing, thin yellow additive edge near the blade.
- **Open / HEURISTIC:** ribbon build rule (runtime builder not traced; maxSegLength/angleMin/scaleRange/displacement not emulated); `trail_melee_dark` uses a normal map (`trail_mgs_distort_nm`) as its colour texture with intensity 11×111 — in the viewer it reads as a dark smear, not compared with the game (no capture); window "first of" rule; distortion trails not drawn; In-place detection compares one locator once per clip.
- **REA note:** the REA daemon's Ghidra was already OOM-killed (every request answered `oom_kill`); it was not restarted (memory: 3 other workers) — the documented fallback `GHIDRA_HEADLESS_MAXMEM=3G nice /workspace/rea/g.sh DecompAt.java` was used (~25 s).
- **Deploy:** `Bin\ForgeLens_latest\ForgeLens.exe` sha256 E91EDF1E…5079 = combined build (origin mtl1-52 + trail53), redeployed 11:50 KST. Backups: `.bak7` = 10:29 exe, `.bak8` = another worker's 11:30 exe (mtl1-52, superseded by the combined build), `.bak9` = trail53-only exe F91EABB4…36E0 (built on phys52 without mtl1-52; briefly deployed 11:23).
- **Tests:** `SweptTrailTests` (triangle parse, cls sfx.list override merge, starter edge, event-window gating + ribbon quads/fade, real chainsword blueprints + light_slash_1 window when the box data exists). SM2.Assets 300 pass, Extractor 43 pass.

## Update propanim (2026-10-29 KST): prop/weapon embedded animation — decode, play, context sync, condition picker, UE export

Owner request (ledger F56 + steering F57, approval "ㄱㄱ"): decode the animation embedded in templates (`animTPL.trackAnim`), play it on the prop standalone, sync it to the character's actions in context playback (shoot / reload / melee / charge, chainsword teeth), add the 56 tpl-folder `.sani` to the clip list, export to UE if feasible; then a context toggle "Prop anim / 프롭 애님" (default ON, persisted) and a condition picker ("Auto" + the weapon's game states).

- **Layout (DATA-PROVEN on all 14,149 retail tpls, 684 with tracks / 2,811 sequences; REA daemon was OOM-killed, not REA-PROVEN):** spline key = `[frame, v0..v(n-1)]`, n = `Unk_02` (T/S 3, R 4, visibility 1), stride `1+Unk_02` (`m3dSPL.Stride/KeyCount/KeyFrame/KeyValue`); 100 % fit (T 7,762 / R 7,826 / S 331 / V 3,415), integer increasing frames inside the sequence windows. Quaternions (keys and `iniRotation`) are w-first and **conjugated**: `Quaternion(-x,-y,-z,w)` reproduces rest `MatrixModel` 2,642/2,830 (plain wxyz 264, xyzw 0) and `MatrixLT` 1,617/1,627. `rootAnim` = identity on all 877 tracks. Sequence fps = (end-start)/timeSec. **The legacy SaniReader rotation assumption was wrong** (xyzw, non-conjugated): the legacy path now uses the same decoder (`TplAnimation.Timeline`). Visibility 0..1 → scale 0 below 0.5 (**HEURISTIC**, no per-object fade in the preview).
- **Binding (REA-PROVEN earlier, ClipBinding):** objects by `animNmb` rank, explicit bone indices when given; merged weapon bones = first + attachment bone index.
- **Which sequence plays when (DATA):** class `properties.anim.layerList` (`MainMultiStitch` AnimList + RootObjectNameList) = per-sequence object masks (chainsword anim1..4 → `sfx_chainsword_attack_0N`, chain_anim* → `anim_tooth`), needed because sequence windows overlap. Blueprint `BlueprintAnimationLibraryStartanim/Stopanim` traced backwards over exec pins (≤8 hops) to a morpheme event or a root node (`AnimEventVfxMap.Anims`, `EventAnim`): chainsword `OnHoldInArms` → chain_anim (cycle), `hitN_start_anim` → animN, OnRemoved → stop/init; firearm_anim_aux `OnShoot` → SHOOT_AUX; power sword hold → power_on/off; thunder hammer inc_*; 1H hammer hit_stack_*; power fist inc_hit; halberd/axe vfx_charge_*; usable props used/open/close. Firearm FSM: Idle ⇄ Shoot ⇄ Reload, `shootAnimation` default SHOOT_1.
- **Auto rule (`PropAnimStates.AutoPlays`):** holding = blueprint start on OnHoldInArms > OnUserSet > OnEquip > OnAttached (loops preferred, "off" avoided — branch pick **HEURISTIC**), else FSM IDLE; every event on the character clip's `Shoot` track → shootAnimation (+ OnShoot triggers; **HEURISTIC** shot = Shoot track); player firing clips have no Shoot track (only 55 NPC trooper clips do), so a clip named *shoot*/*fire* (not reload) loops shootAnimation from 0 (**HEURISTIC**: name, rate = sequence length); clip name contains "reload" → RELOAD_FULL else START+END (**HEURISTIC**); morpheme tags matching triggers start at the event (AtEnd: its end), a Stop ends the earlier play. Compose: the latest active play that moves an object (and whose mask allows it) wins.
- **Retail checks:** wpn_bolt_rifle_01 7 seqs (SHOOT_1 0-4: bolt_action slides −11 cm and back, base_02_01_rot turns 360° about Z; RELOAD_FULL 5-77 2.4 s moves the magazines; SHOOT_AUX moves the grenade-launcher magazine), frame 0 = bind 5/5; wpn_chainsword_01 13 seqs (chain_anim 3 s / chain_anim2 2 s / 3 1 s / 4 0.75 s = teeth speeds 1-4), frame 0 = bind 20/20.
- **Browser:** clip list gets "▶ SEQ" rows (embedded sequences, standalone loop) and "★ … model folder" rows (tpl-folder .sani, `SaniIndex.InFolder`); `PropCondBox` (anim bar) and context `CtxPropAnimToggle` + `CtxPropCondBox` (next to Props/Effects), entries "NAME · 한글" (`PropAnimStates.Label`), Auto first; settings `PropAnim`/`PropCondition`; env `SM2_PROP_ANIM=0|1`, `SM2_PROP_COND=<name|auto>`. Context: the overlay clip is composed on the weapon bones after the character clip (`ExportedMesh.Overlay`, `ClipPose.ApplyOverlay`); tracks on the merged weapon root are dropped so the hand placement stays. Tooltip lists the plays and why. Loose QA (`SM2_MODEL_OPEN`) lists the sequences too.
- **UE export:** Extractor `--anim seq:NAME[,NAME]` / `seq:all` → `{model}__{seq}.fbx` (anim-only, prop skeleton) + `.glb`. Box smoke: bolter 5/5 tracks bound, chainsword 20/20 for all 13 sequences, glb T/R/S channels.
- **Bench:** `ForgeLens --propbench <game-folder|loose> <model> <out> <spec;spec|all|auto:clip.sani> [frames] [zoom]` → `strip_<spec>.png` + `propbench.txt`.
- **Tests:** `PropAnimTests` (10). SM2.Assets 313, Extractor 43, LibSaber.Tests 5 pass.
- **Strips (PC `Scripts\mtl_fix\propanim\after\`, box `/workspace/mtl_fix/prop_anims/after/`):** bolter `SHOOT_1`, `RELOAD_FULL` (magazine drops), `SHOOT_AUX`, auto reload clip `tacticus_wpn10_reload` (IDLE + RELOAD_FULL), auto firing clip `tacticus_wpn1_blind_shooting_run` (SHOOT_1 looped; `bolter_fire12`, 12 frames — 8 frames alias onto the 0.133 s loop); chainsword `chain_anim`, `chain_anim4`, `anim1`, auto `light_slash_1` (chain_anim loop on hold + anim1 at 0.181 s from `hit1_start_anim`). Box UI shot `ui/loose_chainsword_cond.png` (condition box, loose model without tpl_data).
- **Deploy:** `Bin\ForgeLens_latest\ForgeLens.exe` sha256 479D9200…1E4A (origin mtl1-53 + propanim), 12:12 KST; previous exe (E91EDF1E…5079, combined trail53) → `.bak10`.
- **Open / HEURISTIC:** visibility fade; blueprint branch choice for holding; reload clip detection by name; the shot = Shoot-track rule; no REA confirmation of the key layout (data only).

## REA 일괄 처리 목록 (2026-10-29 KST, R32)

REA(또는 headless Ghidra 대체 경로)로 한 번에 몰아서 확인할 항목이에요. 메모리가 빠듯해서 하나씩 하지 말고, 데몬을 한 번 띄울 때 묶어서 처리해요. 결과는 항목마다 PROVEN / INFERRED / HEURISTIC으로 표시해요.

**REA 항목**
1. attenCurveParam 라이트 감쇠(falloff) — 셰이더 공식은 PROVEN, CPU 소비자(attenCurveParam 읽는 곳)를 아직 못 찾았어요. → **rea-batch 2: 일부 done**. 라이트 CB를 읽는 셰이더 3개(디퍼드 s29889, 파티클 CS s4462, 포그 s5611) 모두 창(window)에 P0.w를 곱하고 1/(d²+P0.w)를 곱해요. 즉 감쇠 = sat(1 − d⁴/r⁴)² × P0.w/(d² + P0.w)예요(PROVEN 셰이더). 캡처된 sfx 라이트 6개는 전부 P0.w = 1이에요(DATA). 벤치 바닥/커버/메시 조명과 UE 내보내기에 1/(d²+1)을 넣었어요. |P3.z|는 포그 산란 배율이에요(sfx −1, 레벨 스폿 −0.001). attenCurveParam → P0.w 변환은 여전히 못 찾았어요(open). sfx_light_base 등록(FUN_14198d480 in FUN_14174d720) → 팩토리 FUN_1419b0a00(vtable 0x1436423c0) → 갱신 slot 19 FUN_1418c3b30 → gsLIGHT_MANAGER::CreateLight FUN_1413831e0 → FUN_141383440 경로까지 따라갔지만, 라이트 desc(+0x18)에서 attenCurveParam을 읽는 곳은 안 나왔어요(desc 오프셋 스캔 음성). → **rea-batch 3: 일부 done**. FUN_141384090(B = desc+0x78)이 desc를 렌더 라이트 구조체(light+0x18)로 옮기는 코드예요(PROVEN). attenCurveParam → +0x54, isAdaptiveIntensity → +0x6d 비트 0, useInverseSquaredFalloff → 비트 1, isConstAtten → 비트 2, isNoSpecular → 비트 4, coefIndirect → +0x1fc, fogAmount(클램프) → +0x68, spotAngle/inner → +0x60/+0x64(spot일 때만), 타입 → +0x6c(point 0, spot 1, directional 2)예요. 그런데 +0x54와 +0x6d를 같이 읽어서 COMMON_DYN_LIGHT_PARAMS를 채우는 함수는 오프셋 조합 스캔에서 복사·비교 함수만 나왔어요. P0.w로 가는 마지막 단계는 여전히 open이에요. → **rea-batch 4: open(시도 기록)**. 렌더 라이트 기준 오프셋(+0x6c/+0x85/+0x80) 스캔은 FUN_14325ed30 하나만 나왔는데 라이트와 관계없어요. 레벨 스폿 P0.w = 300은 exe 상수가 아니에요(float 300.0은 0x14355c104·0x14368b68c 두 곳뿐이고 GameStorage 등 라이트와 무관한 곳에서만 써요, DATA). +0x50 벡터 로드 스캔(q:0x50 q:0x60 b:0x6d b:0x6c)도 정렬 비교 함수(FUN_1421dd3c0)와 복사 함수만 나왔어요. 데이터로 매핑을 구하는 것도 안 됐어요. 캡처된 sfx 라이트는 전부 기본 attenCurveParam이라 P0.w = 1뿐이고, 레벨 라이트 데이터는 아직 추출하지 않았어요. INFERRED: P0.w는 역제곱 완화 항이고 sfx 기본값(역제곱 on)이면 1이에요. → **level-lights: done(PROVEN 코드 + DATA)**. 게임 pak(hub_battle_barge)에서 씬 파일을 뽑아서 cd_list(이름·월드 행렬·오브젝트별 덮어쓰기)와 class_list(라이트 desc)를 파싱했어요. armory 캡처(128197 eid 9799) 라이트 4개가 character_customization의 spot_light_bot_set1 / bot_3 / kick_2 / bot_5와 위치 오차 0 m로 맞았어요. 반경(15 / 8.4 / 6 / 6.66 → P1.w = 1/r⁴), 원뿔(30/0.2, 34/0.1, 40/0.25, 32/0.2 → P3.x, P2.w), 색((c/255)^2.2 × Intensity)도 전부 맞아요. P0.w는 attenCurveParam 1인 세 개가 300, 기본값(0)인 kick_2가 1이에요. 그 다음 299.0 상수(0x1437e9974)를 쓰는 함수로 FUN_14224f010을 찾았어요. 식은 directional 0, useInverseSquaredFalloff 1/400, isConstAtten 8e22, a > 0이면 299·a⁴ + 1, a ≤ 0이면 256^a예요. 이 함수를 부르는 FUN_14224ecf0가 COMMON_DYN_LIGHT_PARAMS 16 float를 채우는 패커예요(PROVEN). rea-batch 4의 "useInverseSquaredFalloff 기본값은 True일 것" 추정은 틀렸어요. kick_2(키 없음)가 P0.w = 1이라 기본값은 false예요(DATA). → **rea-batch 6**: 적응형 라이트 배율(param_3)은 FUN_141e90cb0 = clamp(avgLum[뷰포트], 0.05, 50)이에요(PROVEN, 동적 라이트 경로 FUN_141ee9e80; 다른 호출 FUN_141de8140·FUN_14224fa80은 1.0). DAT_14502deac는 읽기 3곳뿐이고 쓰는 곳이 없어요. BSS라 0으로 시작해서 리테일에선 항상 false예요(INFERRED).
2. UseSpecMap — 쓰는 곳(writer)을 못 찾았어요. → **rea-batch 2: open(HEURISTIC 유지)**. OBJ_MTLBLEND_LAYER_CONST는 CB_OBJ_MATERIAL_DATA 멤버(float4 × 0x40)이고, 셰이더 레이아웃 정적 초기화 함수 약 40개(예: FUN_1421aa940)에만 이름이 나와요. 리플렉션 이름으로 바인딩되므로 이름으로 쓰는 곳은 없어요. → **rea-batch 3: DATA 추가, writer는 open**. armory 캡처를 RenderDoc으로 다시 돌려서 CB 원시 바이트를 읽었어요. 이전 덤프는 소수 반올림 때문에 정수 비트가 0으로 보였어요. 140개 draw의 [15].x 값은 8200(0x2008) 58, 4424(0x1148) 23, 4168 17, 88 11, 72 8, 0 6 … 이고, 0x40 한 개를 빼면 0이 아닌 값은 모두 비트 8이 켜져 있어요. 이 값을 만드는 CPU 코드와 .td/mtl 키는 아직 못 찾았어요. → **rea-batch 4: DATA로 비트 의미 확인, writer는 open, 코드 수정**. armory 140개 draw의 PS 텍스처 이름을 .td와 맞춰 봤어요. 0x2000 ↔ 묶음 MSCRGHAO spec td 68/68, 0x40 ↔ 옛 `_spec` td 66/67, 0x8 ↔ MNM 노멀 133/134, 0x100이면 MH 높이맵 31/31, 0x800이면 MEM 7/7(+4), 0x1000이면 MAO 40/41(+4), 0x10 ↔ 디테일은 일부만 맞아요. 즉 spec 비트는 spec 텍스처가 붙었을 때만 켜져요. 그래서 UE 임포터에서 specular 맵이 없는 슬롯은 UseSpecMap을 꺼요(카멜레온 규칙은 그대로).
3. 트레일 리본 빌더(locator 쌍 → 세그먼트, maxSegLength/angleMin/scaleRange/displacement) + 어두운 트레일 렌더링(노멀맵 텍스처를 쓰는데 isDistortion = False인 trail_melee_dark가 실제로 어느 패스에 그려지는지, 트레일 blendType → PSO 매핑). → **rea-batch: 일부 done(코드 변경 없음)**. PS_REG_TRAIL_EMISSIVE = [colorIntensityMultiplier, alphaMult, 1, explicitBloom](플라즈마 [10,1,1,1], rubric_eye [5,4,1,0]으로 DATA-PROVEN), MISC_2.x = 1/softDepth. 트레일 VS도 파티클처럼 COLOR.rgb × avgLum(CB_COMMON[22].y)이라 트레일만의 추가 노출 배율은 없어요(604 VS 조사). 노란 궤적이 흐린 건 벤치 노출 1/250(HEURISTIC, 파티클도 같음) 때문으로 봐요. 리본 점 작성기·TRAIL_DEGR·MNM 비distortion 경로는 여전히 open. → **rea-batch 2**: TRAIL_DEGR 의미를 PROVEN으로 확인했어요(트레일 VS s131403). near = sat((카메라거리 − y)·x), far = sat((거리 − z)·w)예요. 색 × lerp(MISC.w, 1, near) × lerp(1, OFFSET.z, far), 폭 × lerp(MISC.y, 1, near) × lerp(1, MISC.z, far)로 적용돼요. 캡처 값은 [0.05, 10, 0, 1] / [1/3, 5, 10, 0.1] / [2/3, 3, 1, ∞]이에요. fadeParams(distTranspFront/Max) → CB 변환은 INFERRED(미구현)예요. 트레일 셰이더 define 비트 표는 FUN_1424e32c0에 있어요(AKILL 0, SFX 1, DISTORT 2, GBUFFER 3, DEFERRED 4 …). trail_melee_dark 데이터는 deferredRender False, isDistortion False, blend라서 GBUFFER/DEFERRED 경로 설명은 맞지 않아요(DATA). 선택 코드는 못 따라가서 fix54 스킵은 HEURISTIC로 유지해요. RB_TRAIL_POINTS는 레이아웃 FUN_1421ca290에만 나오고, CPU 작성기는 못 찾았어요. → **rea-batch 3**: fadeParams는 TRAIL_DEGR가 아니에요. TRAIL_DEGR는 optimization.degNear/degFar(카메라 거리)예요. fadeParams는 visuals+0x1d0..0x1dc(FUN_141834320, 기본값 0/False/0/False, PROVEN)이고, 라이트닝 트레일 VS(LIGHTNING_TRAIL_DESC_DATA)에서 트레일 길이 방향으로 양 끝을 흐리게 하는 값이에요(PROVEN). 리본 트레일은 점 색을 CPU가 구워 넣어서(trlPOINT 색 0..255) 같은 식을 쓰는 건 HEURISTIC이에요. 그래서 `SM2_VFX_TRAIL_FADE=1`일 때만 적용해요. trail_melee_dark는 쓰는 파일 15개(무기 .cls + melee_actor_factory.sso)에서 전부 isAutoStart False, colorIntensityMultiplier 0.01~1(기본 11)로 덮어써져 있어요. 그래서 게임에서 거의 안 보이는 거예요(DATA). fix54 스킵 결과와 맞아요. → **rea-batch 4: open(시도 기록)**. 트레일 기본 serializer FUN_14183b3a0의 필드 오프셋을 PROVEN으로 확인했어요(visuals +0x90, trailParams +0x280, physics +0x3b0, degNear +0x3e8, degFar +0x400, nTrailData +0x420). 이 오프셋으로 (visuals.cim +0xa8, timeLifeSeg +0x28c) 조합을 스캔해서 FUN_14187e8b0(파라미터 lerp)과 FUN_141a9a470(렌더 인스턴스 빌더, 텍스처 애님 커브만)을 찾았는데, 둘 다 점 색 작성기는 아니에요. RB_TRAIL_POINTS 디스크립터 참조는 등록 함수 FUN_1421ca290뿐이에요. trlPOINT 필드는 color, intensity, globalAge, aclipAnimFrame, ambientAge, angleScale, uvOrSignLen, quatOrDisplacement 등 12개예요. RenderDoc으로 점 버퍼를 직접 덤프해 보려 했지만 sm2_frame7894 리플레이가 프레임 뒤쪽에서 멈춰서(300 s, 600 s 제한에 걸려 종료) 데이터를 못 얻었어요. → **rea-batch 6: CPU 점 작성기 PROVEN**. 트레일 매니저 DAT_144e7aae0 안에 0x50바이트 CPU 점 링이 있어요. 점 생성 FUN_141902840, 매 틱 갱신 FUN_141847c30(나이 LUT 64칸 샘플 → half float 쌍)이에요. +0x24 = colorIntensity 커브, +0x30 = alphaClip 커브, +0x34 = (ambientLight, 나이), +0x1c = 255.0 상수예요. CPU는 점마다 RGB 색을 계산하지 않아요. trlPOINT color 0..255 = +0x1c 상수(그라디언트는 셰이더가 나이로 조회)라는 매핑은 INFERRED예요. GPU 업로드(memcpy) 위치는 못 찾았어요. SM2_VFX_TRAIL_FADE는 HEURISTIC 그대로예요.
4. 스프링 댐퍼 기본값 / 해골 상자 흔들림 진폭(prop_motion_damper_controller 런타임 기본값과 적분). → **rea-batch: done(PROVEN)**. 기본값 FUN_1416e9a20: stiffness 0, dampingRation 0, mass 1, jointAngle 10, gravity 0. 적분 FUN_1414828a0: 축별 RK4, a = −(k/m)x − d·v, h = dt × timeScale, jointAngle 각도 제한(0이면 무제한), gravity는 힘이 아니라 휴지 방향을 아래(0,−1,0)로 gravity 비율만큼 꺾는 값(≤1 클램프). 파서 기본값·사이드카 UE 매핑을 고쳤어요.
5. 컬러 스킴이 어둡게 나오는 원인. → **rea-batch: done**. 원인은 베이크가 모든 레이어를 `tint × _a 알베도`로 곱한 것이에요. glt PS 003784에서 CB_STATIC_GLT[180+L] == 0일 때만 베이스를 곱하고, 아니면 틴트로 바꿔요(PROVEN). exe 문자열 표(0x144ccbf40)는 tintBlendMode = [multiply, linear_blend] 순서라 linear_blend = 1 → 교체예요(인덱스 INFERRED). 리테일 td 18,713개에서 linear_blend 20,052 / multiply 1,127 레이어라 거의 모든 갑옷 페인트가 틴트 그 자체예요.
6. 남은 컬러 블렌드 모드 / 패턴 / 렌즈. → **rea-batch: 일부 done**. 레이어 `blendMode` 표 = [tint_only, tex_hardlight, tex_blend](glt 레코드 +80+L, PS 분기 PROVEN): tint_only는 금속도·거칠기를 안 바꿔요(코드 반영). 데이터: tex_blend 16,444 / tint_only 1,243. texArray 패턴(tex_hardlight/tex_blend + textureIndex ≥ 0)과 렌즈 색은 아직 안 그려요(open). → **rea-batch 2: done(패턴), 렌즈는 파라미터만**. tex_blend 패턴 슬라이스 샘플링·spec 슬라이스는 glt PS 003784 그대로예요(PROVEN). 배열은 pct face예요(DATA). CB176 = td 레이어 density는 INFERRED, textureIndex 그대로 전달도 INFERRED예요. useCustomUVSet 레이어는 틴트 경로로 대체해요(HEURISTIC). tex_hardlight는 리테일에서 0개라 안 구워요(DATA). 렌즈 emissive 색·세기·bloom은 UE 파라미터와 툴팁에만 넣었어요(뷰어에 emissive 패스가 없어요). 주의: Corax_White / Celestra_Grey는 textureIndex 0인데 patternTint가 없어서 회색 109로 구워져요. → **rea-batch 3**: 새 exe로 cc_sm_soldier_story × White_Scars / Knights_of_the_Raven / Doom_Eagles / Atlantian_Spears를 찍어 봤어요. Corax_White, Celestra_Grey, Stormhost_Silver, Valdor_Gold가 들어간 스킴이에요. 평평한 회색은 안 보이고 색도 자연스러워요(스크린샷). 코드 수정은 없어요.
7. 프롭 애님 키 형식 확인 + 플레이어 사격 타이밍 + 재장전 판정(지금은 클립 이름 HEURISTIC). → **rea-batch 2: 일부 done**. 재장전 판정은 reload_sound_seq 트랙으로 해요(리테일 .sani 23,227개 중 18개, 18개 전부 reload 클립이에요 → DATA). 사격 간격은 firearm_library_pve shotsPerMin을 써요(DATA, 총기 58개; 클래스 → uid 매핑은 INFERRED). 플레이어 사격 클립에는 Shoot 트랙이 없어서 사격 클립 선택은 여전히 이름 기준이에요(HEURISTIC). → **rea-batch 6: open(exe 경로 없음)**. exe에 fireRateAffectShootAnim·fireRate·shootAnim 문자열이 아예 없어요(ASCII·UTF-16 0개). 그래서 사격 클립 선택은 exe 코드가 아니라 스크립트 데이터(SSL/.sso)에 있어요. 다음엔 PC의 ssl을 SSLB 리더로 디코드해야 해요. 지금은 HEURISTIC 그대로예요.

**REA 아닌 항목(데이터/도구 작업)**
1. 좌/우 견갑·건틀릿·그리브 오버라이드.
2. 총구 화염 오프셋 + 데이터 기준 조준(aim-up).
3. 임포터에서 .td의 텍스처가 아닌 파라미터 처리.
4. 컬러 스킴을 UE 머티리얼에 연결.
5. 다른 캐릭터 물리 다시 돌리기.

## Update fix54 (2026-10-29 KST): chainsword swing trail — additive trail blend + distortion/normal-map trails out of the colour pass

Owner report (ledger F58, "인게임은 저런 어두운 트레일이 아닌데"): the chainsword swing trail in the game is not the dark fan Forge Lens showed (trail53). Fix with a generic rule; evidence first (R32).

- **What the fan was (measured on the trail53 build, cc_intercessor + wpn_melee_chainsword, light_slash_1 t = 0.42 s):** a capture with `SM2_SWING_TRAILS=0` has no fan, so it is a swing trail. Skipping trail_melee_dark alone changed at most 6/765 per pixel (box `diff_t042.png`), so most of the fan is **trail_mele_01_yellow** (blendType `add`): its gradient goes bright yellow (A 207) → grey 55,55,55 (A 44) at n 0.19 → 0 at 0.68, and the viewer drew every trail with straight alpha (SrcAlpha/InvSrcAlpha, mtl1-28). With straight alpha the long grey tail pulls the background down (fan pixel 21,18,17 vs background 28,24,22) → a dark smoke fan instead of a bright additive arc.
- **Blend rule (PROVEN state, INFERRED mapping):** the RenderDoc plasma capture (101917, box `/workspace/rdc/plasma/nightly`) has three CB_PASS_TRAIL draws (CB_ANIM_TRAILS + CB_TRAIL_CHUNK_OFFSET bound): eid 4898/4905 SrcAlpha/InvSrcAlpha and **eid 4914 One/One (alpha Zero/One)** → trails do use an additive state (PROVEN). Mapping blendType `add` → One/One and `blend`/`blend_add` → SrcAlpha/InvSrcAlpha follows the PROVEN sfx selector FUN_142451840 (mtl1-44) and blend_add = blend (mtl1-38): INFERRED (the retail trail behind each captured draw was not identified). Code: `VfxBlendRules.ForTrail`, `EffectRenderer.ApplyKindState(kind, blend)`, trail shader branch writes `c*alpha, 0` for add. Applies to every trail (sfx_trail and sfx_trail_triangle), no per-weapon code.
- **Normal-map trails (PROVEN data, INFERRED meaning, HEURISTIC rendering):** `.td` name ending `_nm` ⇔ `usage = "MNM"` (Normalmap(rgb)) in 3,619/3,619 of 18,690 retail td (PROVEN, data). Of 139 retail trail nodes in 2,381 .sfx, 48 use an MNM texture and 46 of those set `isDistortion` (PROVEN count); the other two (trail_melee_dark, trail_sword_melee_warrior_03) are field-for-field clones of their weapon's distortion trail with colour/intensity/isDistortion/isAutoStart changed (PROVEN diff) → INFERRED distortion trails with the flag left off. `SfxTextureUsage.IsNormalMap` + `TrailDef.Parse`: isDistortion **or** MNM texture → `DrawTarget.Distortion`, which the colour pass skips (no trail refraction pass in the viewer, like root distortion trails). The exe path for an MNM texture on a non-distortion trail was not traced (REA batch item 3) → the skip itself is the remaining HEURISTIC. Sprite emitters with MNM textures are unchanged.
- **Status line:** swing trails on the Distortion target are listed with "(distortion, not drawn)".
- **Result (after, same frames):** the dark fan is gone; pixels darker than the background in the viewport drop 67,603 → 8,987 at t 0.42 (15,739 → 8,867 at 0.30, 11,848 → 8,518 at 0.60; the rest is the model).
- **Yellow arc check: too faint.** No bright yellow arc is visible at 0.30/0.42/0.60; the yellow-pixel count is unchanged (≈ 1,900 / 2,050 / 1,385, the gold armour trim). colorIntensity 5 × 0.4 = 2 and the bright gradient key lasts only n < 0.19 (≈ 29 ms of the 0.15 s segment life), so the bright band is a few pixels behind the blade; the grey tail now adds a faint lift instead of darkening. Open (REA batch item 3): PS_REG_TRAIL_EMISSIVE [5,4,1,0] / TRAIL_COLOR_FADEOFF and the HDR exposure the game applies to trails are not emulated, which may be why the in-game arc is brighter.
- **Screenshots:** box `/workspace/mtl_fix/trail/fix54/` (`before_t030/042/060.png`, `after_t030/042/060.png`, crops `crop_*`, `strip_before_after.png`, `diff_t0*.png`, `probe_noswing_t042.png`); PC `Captures\mtl_fix\trail54\`. In-game comparison still to do.
- **Tests:** `SweptTrailTests` (`NormalMapTextureName`, `NormalMapTrailIsNotColour`, `RetailDarkTrailIsDistortion`), `CaptureRulesTests` (ForTrail).

## Update rea-batch (2026-10-29 KST): REA batch — spring damper solver + defaults, colour-scheme tint blend, trail evidence

REA daemon not reopened (OOM risk); headless Ghidra read-only (`/workspace/rea/ghidra_proj3`, 3.5 GB heap) + capstone scans. Box notes `/workspace/mtl_fix/rea_batch/progress.md`, decompiles `dec/`.

- **Spring damper (PROVEN).** Joint array grow path FUN_1416e9a20 / FUN_1415e28a0 builds the 0x30-byte joint with stiffness 0, dampingRation 0, mass 1.0, isParent false, jointAngle 10.0, isInvertedLook false, gravity 0; reader FUN_141474d60 keeps them when a key is absent (3,828 retail joints: mass absent 505, jointAngle 20, gravity 78). Solver FUN_1414828a0 per joint: rest direction bent toward (0,−1,0) by gravity·angle(rest, down) (gravity clamped to 1, ≤ 1e-6 off — not a force), swing limited to jointAngle° by FUN_1427ab3c0 (jointAngle ≤ 1e-6 → unlimited FUN_1427aab90), then per-axis RK4 (k/2, h/6) of x = target − pos with a = −(stiffness/mass)·x − dampingRation·v, h = dt·timeScale; |v| > 2e6 or NaN resets v. Code: parser defaults dampingRation 1→0, jointAngle 30→10; sidecar `driveDamping = dampingRation·timeScale` (was 2ζω0), `coneDeg = jointAngle` (0 → 180), gravityScale clamp 0..1, note rewritten. UE mapping stays INFERRED. timeScale default 1 (2/1,511 absent) INFERRED.
- **Colour schemes too dark (PROVEN shader, INFERRED enum index).** Exe tables 0x144ccbf40…: tintBlendMode = [multiply, linear_blend], blendMode = [tint_only, tex_hardlight, tex_blend]. glt PS 003784: CB_STATIC_GLT[180+L] == 0 → col = lerp(tint, metalTint, metal)·base, else col replaces; record +80+L selects tint_only (running metal, no metal/rough change) / hardlight pattern / tex_blend (metal = saturate(metal0·scale + bias)). Retail td: linear_blend 20,052 vs multiply 1,127 layers → the preview multiplied 95 % of layers by the neutral _a albedo, hence too dark. Code: `ArmorSchemeBake.Albedo/Specular` take the per-layer tintBlendMode/blendMode, `CustomizationSlot.PatternModes`, UE params `Layer{i}TintBlendMode` / `Layer{i}MultiplyBase`. texArray patterns and lens colours still not drawn.
- **Trails (evidence only, no code change).** PS_REG_TRAIL_EMISSIVE = [colorIntensityMultiplier, alphaMult, 1, explicitBloom] and MISC_2.x = 1/softDepth (DATA-PROVEN from plasma/rubric captures); trail visuals serializer FUN_141836a80 (+0x18 cim, +0x58 alphaMult, +0x5c explicitBloom, +0x74 softDepth, +0x78 blendType); CB_PASS_TRAIL slot 0x12 (FUN_1421ca290). All 82 unlit trail VS and 82 lightning VS scale COLOR.rgb by avgLum like particle VS → no trail-only exposure; the faint yellow arc follows the HEURISTIC bench exposure (1/250) shared with particles. Ribbon point writer, TRAIL_DEGR, MNM-without-distortion route: open.
- **attenCurveParam (open).** Traced sfx_light_base → factory FUN_1419b0a00 (vtable 0x1436423c0) → slot 19 FUN_1418c3b30 → CreateLight FUN_1413831e0 → FUN_141383440; no reader of the curve param found. UseSpecMap and prop-anim items not revisited.
- **Tests:** SpringDamperTests (+JointDefaultsMatchExeConstructor, ConeFollowsExeClamp×3), ArmorColourSchemeTests (+TintBlendModeLinearReplacesMultiplyScalesBase, SlotReadsPatternBlendMode). Assets 327 / Extractor 43 / LibSaber 5 pass.

## Update rea-batch 2 (2026-10-29 KST): colour-scheme patterns, prop-anim reload/fire timing, light inverse-square, TRAIL_DEGR

Same rules as rea-batch: headless Ghidra read-only (3.5 GB heap), RenderDoc captures under box `/workspace/rdc`. Notes are in `/workspace/mtl_fix/rea_batch/progress.md`.

- **(a) Colour-scheme screenshots** (deployed exe before this change): cc_sm_soldier_story × Imperial Fists / Dark Angels / Blood Angels is now bright and correct. Campaign cc_titus armour has no CC block, so only the helmet and accessories recolour (PROVEN in mtl1-52).
- **Item 7 (DATA + HEURISTIC).** Reload detection now uses the `reload_sound_seq` track: it appears in 18 of 23,227 retail .sani, and all 18 are reload clips. RELOAD_END is taken at snd_reload_mag_in (INFERRED). Firing rate comes from `firearm_library_pve.sso` shotsPerMin (58 firearms); the class → uid mapping strips suffixes (INFERRED). Player firing clips have no Shoot track, so the clip is still picked by name (HEURISTIC). The fireRateAffectShootAnim default is not decoded.
- **Item 6 (PROVEN shader + DATA arrays; INFERRED/HEURISTIC noted).**
  - The tex_blend pattern from glt PS 003784 is emulated: slice = textureIndex, UV × density, then pat = lerp(t.rgb, patternTint.rgb, patternTint.a). Multiply mode × base. P = lerp(base, pat, t.a). Spec slices drive metal/rough.
  - Texture arrays are pct faces (ch_custom_01: BC7 512², 6 faces; slice 0 is flat grey 109).
  - CB176 = td layer density is INFERRED, and so is textureIndex passthrough. useCustomUVSet layers fall back to the tint path (HEURISTIC). tex_hardlight has 0 retail layers, so it is not baked.
  - Lenses: emissive tint/intensity/bloom go into the UE params and the tooltip only.
  - Risk: Corax_White / Celestra_Grey have textureIndex 0 and no patternTint, so they bake to grey 109.
- **Item 1 (PROVEN shader, DATA P0.w; mapping open).**
  - Every consumer of COMMON_DYN_LIGHT_PARAMS multiplies the window by P0.w and by 1/(d² + P0.w): deferred s29889 `_label226`, particle CS s4462 `_label132` (× 0.25), and fog s5611 `_label63`. Light = colour · sat(1 − d⁴/r⁴)² · P0.w/(d² + P0.w). The tube-light variant uses the segment form.
  - mtl1-36 had missed this term.
  - Captured sfx lights: P0.w = 1 on 6/6. Armory level spots: P0.w = 300 / 1, and P3 = (spot scale, shadow idx, −0.001, 1).
  - |P3.z| scales fog in-scattering (s5611 `_label59`): −1 on sfx lights.
  - The bench mesh/cover/ground shaders and the VFX UE vertex-colour export now use window · 1/(d²+1) (CaptureLightUnits.SurfaceAttenuation).
  - How attenCurveParam maps onto P0.w is still not found. The 200000 immediates only lead to fog/cascade inits FUN_14145f6c0 / FUN_1419e5250.
- **Item 2 (open).** OBJ_MTLBLEND_LAYER_CONST is a CB_OBJ_MATERIAL_DATA member (0x40 float4). It is named only in about 40 static shader-layout initialisers (FUN_14219f000…FUN_1421d19a0, e.g. FUN_1421aa940) and bound by reflection, so there is no named writer.
- **(e) Trails.**
  - TRAIL_DEGR is PROVEN (trail VS s131403): near = sat((camDist − y)·x), far = sat((camDist − z)·w). Colour × lerp(MISC.w, 1, near) · lerp(1, OFFSET.z, far); width × lerp(MISC.y, 1, near) · lerp(1, MISC.z, far).
  - Captures: [0.05, 10, 0, 1] with MISC [0, 0, 1, 0] in every scene, plasma [1/3, 5, 10, 0.1] / [0, 0, 2.5, 1], armory [2/3, 3, 1, ∞].
  - The fadeParams (distTranspFront/Max) → CB mapping is INFERRED and not emulated.
  - Trail shader define bits are in FUN_1424e32c0 (AKILL 0, SFX 1, DISTORT 2, GBUFFER 3, DEFERRED 4, TAPE 5, FLAT 6, …).
  - trail_melee_dark.sfx has deferredRender False, isDistortion False and blendType blend (DATA), so the dark fan is not a deferred/gbuffer trail. The fix54 skip stays HEURISTIC.
  - RB_TRAIL_POINTS is named only in layout initialiser FUN_1421ca290; no CPU point writer was found.
- **Tests:** Assets 334/334 (+SurfaceFalloffAddsSoftenedInverseSquare, +3 colour-scheme array tests).

## Update rea-batch 3 (2026-10-29 KST): light desc → render-light map, UseSpecMap raw flags, trail fadeParams, trail_melee_dark

Same rules (headless Ghidra read-only 3.5 GB, RenderDoc nightly replay on the PC at Idle). Notes: box `/workspace/mtl_fix/rea_batch/progress.md`; screenshots and dumps: box `/workspace/mtl_fix/rea_batch/round3/`.

- **(1) Colour-scheme screenshots: verified, no fix.** New exe, cc_sm_soldier_story × White_Scars, Knights_of_the_Raven, Doom_Eagles and Atlantian_Spears. These schemes contain Corax_White, Celestra_Grey, Stormhost_Silver and Valdor_Gold. All four render plausibly with no flat-grey parts (`round3/grid4.png`, `crop4.png`).
- **(2) attenCurveParam → P0.w: desc → render light PROVEN, last step open.**
  - FUN_141384090 (desc base +0x78) copies the desc into the render light (light+0x18):
    - attenCurveParam → +0x54.
    - +0x6d flags: bit0 isAdaptiveIntensity, bit1 useInverseSquaredFalloff, bit2 isConstAtten, bit3 desc+0x205, bit4 isNoSpecular.
    - coefIndirect → +0x1fc, fogAmount (clamped) → +0x68.
    - Spot angle/inner → +0x60/+0x64 (spot only).
    - Type → +0x6c (point 0, spot 1, directional 2).
  - FUN_14204a620 / FUN_1417f7830 / FUN_141a14050 / FUN_141ef9110 / FUN_142b7a2b0 only copy this struct. FUN_1421dd3c0 is a sort comparator.
  - Offset-set scans found no function that packs COMMON_DYN_LIGHT_PARAMS from +0x54/+0x6d, so the P0.w writer stays open. INFERRED: useInverseSquaredFalloff/isConstAtten select the P0.w regime. The UE falloff stays APPROXIMATION.
- **(3) UseSpecMap: raw DATA, writer open.**
  - Raw re-dump of the armory capture: `round3/usm.jsonl`, 140 draws with CB_OBJ_MATERIAL_DATA. The old cbuffers.json rounded floats, so the int flags read as 0.
  - asint(OBJ_MTLBLEND_LAYER_CONST[15].x) counts: 8200 (0x2008) ×58, 4424 (0x1148) ×23, 4168 ×17, 88 ×11, 72 ×8, 0 ×6, 10248 ×4, 328 ×3, 7496 ×3, ….
  - Every non-zero value except one 0x40 draw has bit 8, which the glt PS does not test. INFERRED: a "uses spec map" master bit. Bit 512 (invert) is never set.
  - The writer and source key are not found (CB filled by reflection/memcpy).
- **(4) Trail fadeParams: meaning PROVEN, and it is not TRAIL_DEGR.**
  - The visuals serializer FUN_141834320 stores distTranspFront / _useMeters / distTranspMax / _useMeters at visuals+0x1d0/+0x1d4/+0x1d8/+0x1dc. Ctor FUN_141834200 defaults them to 0 / False / 0 / False.
  - The same fields are in the GPU struct TRAIL_VIS_DESC = LIGHTNING_TRAIL_DESC_DATA (76 B, registered in FUN_1421ca290). The lightning trail VS (trail.sdc 000045) does:
    - front = m ? F : F·len, max = m ? M : M·len.
    - k = (end − s)/front · (s − start)/max. Each factor is skipped when ≤ 1e-6, and there is no clamp in the VS.
    - k goes on rgb for blendType 1, otherwise on alpha.
  - TRAIL_DEGR is optimization.degNear/degFar (already in code).
  - The ribbon VS (s131097) takes per-point colour 0..255 from trlPOINT and has no fade term, so ribbon fadeParams is baked by the CPU point writer (not found).
  - Code: `TrailDef.FadeFront/FadeMax(+Meters)` and `AlongFade`. The fade is applied only with `SM2_VFX_TRAIL_FADE=1`, with "end" = head and saturated factors (HEURISTIC, off by default).
- **(5) trail_melee_dark invisible in game: DATA + INFERRED.**
  - All 15 files that use it (weapon .cls: chainsword, knife, khopesh, mod_1..5, cinematic, heavy weapons, plus melee_actor_factory.sso) override isAutoStart to False and colorIntensityMultiplier to 0.01 ×2, 0.05 ×6, 0.3, or 1 ×5 (base sfx 11). The knife also sets alphaMult 0.1. The colour keys have low alpha (e.g. 61/255).
  - The texture is MNM normal map; blend is SrcAlpha/InvSrcAlpha.
  - So it only runs when an event starts it, and it is a near-invisible dark smear (INFERRED). The fix54 skip matches what the game shows.
  - The exe shader path for an MNM texture on a non-distortion trail is still not traced.
- **Tests:** Assets 335/335 (+FadeParamsAlongTrail).

## Update rea-batch 4 (2026-10-29 KST): UseSpecMap bit meanings (DATA) + importer fix; P0.w writer and ribbon point writer tried, still open

Same rules (headless Ghidra read-only 3.5 GB, RenderDoc nightly replay on the PC hidden at Idle). Time-boxed ~60 min. Notes: box `/workspace/mtl_fix/rea_batch/progress.md`; dumps: box `/workspace/mtl_fix/rea_batch/round4/`.

- **(1) attenCurveParam → P0.w: still open (what was tried).**
  - Offset-set scan with render-light offsets (light-relative +0x6c attenCurveParam, +0x85 flags, +0x80): only FUN_14325ed30, which reads +0x85 / +0x6c>>1 and walks a list at +0x80. Not a light packer.
  - Vector-load scan (q:0x50 q:0x60 b:0x6d b:0x6c, for a 16-byte load that would hide the +0x54 displacement): only struct copies (FUN_14204a620 and siblings) and sort comparators (FUN_1421dd3c0).
  - Level spots have P0.w = 300. The float 300.0 appears in the exe only at 0x14355c104 / 0x14368b68c, and its users (GameStorage.TaskLogKeepAfterTaskFinishedSecs FUN_14018dbe0, FUN_1402c4570, FUN_1402c4e90, FUN_141f8de30) are not lights. So 300 is level data, not a code constant (DATA).
  - Empirical mapping is not possible yet. Every captured sfx light uses the default attenCurveParam (P0.w = 1 ×6), and level light data is not extracted. sfx data never sets useInverseSquaredFalloff True (16 × False in 8 files), so the default is likely True.
  - INFERRED: P0.w is the inverse-square softening term (1 for default sfx). The UE falloff stays APPROXIMATION.
- **(2) UseSpecMap: bit meanings DATA, writer open, importer fixed.**
  - Re-ran the armory capture (sm2_frame128197) with the bound PS texture names per draw (`round4/usm4.jsonl`, 140 draws), matched against the .td files (`round4/feat.json`).
  - asint(OBJ_MTLBLEND_LAYER_CONST[15].x) bits vs bound textures:
    - 0x2000 ↔ packed MSCRGHAO spec td: 68/68.
    - 0x40 ↔ legacy `*_spec` td (no usage line): 66/67 (the miss has no textures).
    - 0x8 ↔ MNM normal map: 133/134.
    - 0x100 → MH height bound 31/31 (11 MH draws without the bit).
    - 0x800 → MEM emissive 7/7 (+4 draws with the bit but no MEM).
    - 0x1000 → MAO 40/41 (+4).
    - 0x10 ↔ detail (det/MDT): partial (13 with the bit all have detail; 11 detail draws lack it).
  - So the spec bits are set exactly when a spec texture is bound. Without one, the glt PS uses metal 0 / rough 1 raw.
  - Code: `import_sm2_materials.py` apply_spec_layers now sets UseSpecMap = has specular map and not chameleon (was: not chameleon).
  - The CPU code that builds the mask is still not found (CB filled by reflection).
- **(3) Ribbon per-point colour writer: still open (what was tried).**
  - Trail base serializer FUN_14183b3a0 (PROVEN offsets): visuals +0x90 (FUN_141836a80), trailParams +0x280 (FUN_141839b50), physics +0x3b0, degNear +0x3e8, degFar +0x400, degradationRadius +0x418, optimizeBranchQuality +0x41c, nTrailData +0x420. About 14 trail-type serializers call it.
  - Scan for desc readers of visuals.colorIntensityMultiplier (+0xa8) and trailParams.timeLifeSeg (+0x28c): FUN_14187e8b0 is a lerp between two parameter sets, and FUN_141a9a470 is the render-instance builder (sfx_trail_lightning_gpu / sfx_trail_static checks, tex-anim curves). Neither samples the colour gradient.
  - The RB_TRAIL_POINTS descriptor is referenced only by its registration (FUN_1421ca290). trlPOINT fields: color, intensity, globalAge, aclipAnimFrame, ambientAge, angleScale, uvOrSignLen, quatOrDisplacement (12 fields).
  - Data route: a RenderDoc script (`round4/trl4.py` on the PC) to dump the trlPOINT buffer from trail draws. sm2_frame7894 has no draw markers (4,876 draws). The replay stalls late in the frame (killed at the 300 s / 600 s caps), so no point data yet. Next step: replay a narrower eid window, or find the trail eids first by PS name.
- **Tests:** Assets 335/335 (no C# change).

## Update level-lights (2026-10-29 KST): attenCurveParam → P0.w PROVEN (packer FUN_14224ecf0 / FUN_14224f010 + armory level-light pairing), bench + UE export

Owner request: extract level light data to solve attenCurveParam → P0.w. Game install read-only (scene archive `paks/client/default/scenes/hub_battle_barge.pak` read, entries copied to `SM2_Work/Raw/level_lights/hub_battle_barge/`); headless Ghidra read-only 3 GB; no new capture (game not running, the existing armory capture 128197 was enough). Outputs: box `/workspace/mtl_fix/level_lights/` (`hub_battle_barge_lights.tsv`, `armory_pairing.tsv`, `cdlist.py`, `classlist.py`, `lights_tsv2.py`, `verify_armory.py`, `f299.c`, `f299_callers.c`).

- **Level data.** `scenes/hub_battle_barge.scn/hub_battle_barge.cd_list` (5,898 objects): names, 4×4 world matrices (translation = m[12..14]) and per-object PS overrides. Light descs live in `.class_list` (661 classes, name + PS text). Merged dump: 1,865 single-light objects with RadiusCtrl, ColorCtrl, Intensity, lightType, attenCurveParam, useInverseSquaredFalloff, isConstAtten, spot cone, fog, coefIndirect, flags, plus the source of each value (cd / class / default) and the predicted P0.w.
- **Pairing (DATA, exact).** Armory capture lights L0–L3 = `customization_room__character_customization__customization_lgt_1__spot_light_bot_set1 / bot_3 / kick_2 / bot_5`. All four are at 0 m position error.
  - Radius 15 / 8.4 / 6 / 6.66 → P1.w = 1/r⁴.
  - Cone spotAngle/inner 30/0.2, 34/0.1, 40/0.25, 32/0.2 → P3.x = 1/(cos(a·f·π/360) − cos(a·π/360)), P2.w = −cos(a·π/360)·P3.x (4 decimals).
  - Colour P1.rgb = (c/255)^2.2 × Intensity in 12/12 channels (1e-3). Piecewise sRGB is up to 1.5 % off.
  - P0.w: attenCurveParam 1 (bot_set1/bot_3/bot_5) → 300; no key (kick_2, class default) → 1.
- **Code (PROVEN).** The only reader of float 299.0 (0x1437e9974) is FUN_14224f010 = P0.w:
  - light+0x6c == 2 (directional) → 0;
  - +0x6d bit 1 (useInverseSquaredFalloff) or global DAT_14502deac → 0.0025 (DAT_1437e9930);
  - bit 2 (isConstAtten) → 8e22 (DAT_1437e9990);
  - else a = +0x54 attenCurveParam: a > 0 → 299·a⁴ + 1; a ≤ 0 → 256^a (DAT_1436dba7c).
  - Its only caller FUN_14224ecf0 is the COMMON_DYN_LIGHT_PARAMS packer (the writer rounds 2–4 could not find):
    - P0 = (pos +0x30/+0x38, or −dir for directional; P0.w above).
    - P1.rgb = colour (+0x40..48) × +0x50·+0x70·+0x4c, × param_3 when adaptive (bit 0), × 400 (DAT_14355c10c) when inverse-squared.
    - P1.w = 1/(+0x5c·+0x58)⁴, or −r⁴ for isConstAtten.
    - P2 = (normalised dir +0x20, spot: −cos·P3.x, else 1).
    - P3 = (spot cone scale (eps 1e-6) or 0, shadow index, fog +0x68 negated unless bit 3, +0x74/+0x70 or 0 when isNoSpecular).
- **Meaning.** The shader falloff is win²·P0.w/(d²+P0.w):
  - attenCurveParam 0 → softened inverse square (1 m²);
  - 1 → nearly flat inside the radius;
  - negative → sharper (−1 → 1/256);
  - useInverseSquaredFalloff → physical 1/d² (P0.w 1/400 with colour ×400);
  - isConstAtten → constant sphere (shader zeroes d⁴ > r⁴).
  - Correction: the rea-batch 4 guess "useInverseSquaredFalloff default True" is wrong. kick_2 has no key and P0.w 1, so the default is false (DATA).
- **Code changes.**
  - `CaptureLightUnits.InverseSquareSoft` / `LightColorScale` / constants (`InverseSquaredSoft`, `ConstAttenSoft`, `InverseSquaredColorScale`, `ConstAttenThreshold`). `SurfaceAttenuation` handles the constant sphere.
  - `LightColorGamma` 2.2 decode for light colours (`Linear`).
  - `LightDef.AttenCurveParam / UseInverseSquaredFalloff / IsConstAtten` (parsed from sfx_light) → `InverseSquareSoft`, `ColorScale`.
  - `LightSample.Soft / Falloff`, and `Relative` × ColorScale.
  - Browser mesh / ground / cover shaders: `uLightSoft[8]`.
  - `VfxUeExport` mesh baking uses `l.Falloff`.
  - `VfxFrameExport.Lights` writes `inverseSquareSoft` (−1 = const sphere), `attenCurveParam`, `useInverseSquaredFalloff`, `isConstAtten`.
  - `tools/vfx/vfxframe_scene.py` `falloff()` (ground also gets the soft inverse square, matching GroundFs).
  - `tools/ue/ue_vfx_frame.py` sets UE `use_inverse_squared_falloff` for inverse-squared lights. Other UE lights keep exponent 1 (APPROXIMATION, UE cannot express the SM2 curve).
- **Still open / caveats.**
  - isAdaptiveIntensity: the four armory lights have no key and their colour is not × avgLum, so for these level lights the adaptive factor was 1 (DATA). `DefaultAdaptive = true` (sfx HEURISTIC) is unchanged; param_3 of FUN_14224ecf0 was not traced.
  - Multi-light container objects (29 cd_list entries with several sfx_light blocks) are skipped in the TSV.
- **Tests:** Assets 343/343 (+8: `LevelLightSoftTests` armory pairing ×4, FUN_14224f010 table, const sphere, inverse-squared, LightDef/export). Python unittest 18/18 (+3 `test_level_lights.py`).

## Update oneclick (2026-10-29 KST): Direct-to-Unreal one-click export option (ledger F60)

Owner request (ledger F60, "원클릭 내보내기를 옵션으로도 만들어줘"): keep the existing export as the default and add an option that imports the export straight into a UE project.

- **UI.** Export window → bottom panel "Direct to Unreal: import into the project automatically after the export" (unchecked by default).
  - Unreal project (.uproject) with Browse; remembered in settings `LastUnrealProject`; default `K:\Unreal Projects\UE_VFX_57\UE_VFX_57.uproject`.
  - Content folder; default `/Game/SM2_Work/<category>/<asset>` (Characters / Weapons / Props / Environment / Animations for Anim Only (Manny)). It follows the Manny checkbox until the user edits it. `/Game/SM2_Work/Original/*`, `/Game/Work/*`, top-level `/Game/X`, spaces and non-/Game paths are refused (`UnrealContentPath.Validate`, also re-checked in Python).
  - Needs "Models as FBX" and the Unreal target (FBX Z-up cm).
  - After the export a non-modal "Forge Lens — Direct to Unreal" window shows the bridge, a progress bar, the live log (tail of `import_*.log`), a green/red result line and "Open log folder".
  - Also wired into Kitbash export (asset name = template).
- **Route** (`UnrealDirectImporter.Run`):
  1. EngineWire `:3000` (MCP `system_control` / `execute_python`, token from `ENGINEWIRE_TOKEN` or `%USERPROFILE%\.enginewire_token`), only if the open editor's `project_dir` is the chosen project.
  2. Python Remote Execution (UDP 239.0.0.1:6766 ping/pong, TCP command `ExecuteFile`), only if the pong's project root matches.
  3. Headless `UnrealEditor-Cmd.exe "<uproject>" -run=pythonscript -script=<runner> -unattended -nop4 -nosplash -NoSound -AutoDeclinePackageRecovery` (engine from EngineAssociation + LauncherInstalled.dat), BelowNormal, hidden; stdout kept as `import_*.headless.txt`. Only when no Unreal Editor is running: an open editor without a bridge gives a red message and imports nothing (never two processes saving the same packages).
  - Env `SM2_DIRECT_BRIDGE=enginewire|remote|headless` forces one route (QA). Forced headless also runs next to an open editor, so use it only with a fresh content folder.
- **Import** (`tools/ue/ue_forge_direct.py`, embedded and written to `<export>/_forge_direct/` with `ue_import_anim.py`, `ue_import_manny.py`, `manifest_<ts>.json`, `run_import_<ts>.py`, `import_<ts>.log`, `result_<ts>.json`):
  - Existing non-empty folder → `duplicate_directory` to `<folder>_bakN` (next free N, never overwritten), saved, then reimport in place. Assets the new export no longer produces stay in the folder (nothing is deleted).
  - Skeletal/static mesh FBX → materials via the export's `import_sm2_materials.py` → clips (incl. prop SEQ clips) to `<folder>/Anims` with the shared `sm2_anim_import` block → Manny clips to `<folder>/Anims_Manny` on `/Game/Characters/Mannequins/Meshes/SK_Mannequin` (`ue_import_manny.run` also keeps per-asset `_bakN` copies of clips it replaces) → physics `SM2PhysicsSetupLibrary.setup_from_json` + `tune_rigid_body` → colour scheme as mesh metadata tags `SM2.ColourScheme` / `SM2.ColourSchemeJson` (the scheme itself is baked in the exported albedo).
  - Fewer imported clips than FBX files is an error (found in QA: a character clip exported for the bolter failed silently).
  - Saves only the destination folder (`save_directory`), never the level / Lightning_01.
  - Headless: Interchange asserts `CurrentApplication.IsValid()` without Slate (mesh and texture imports, crash seen in QA), so the headless run switches FBX/PNG/TGA/DDS/JPG/EXR/BMP/TIFF/PSD/HDR to the legacy factories (`Interchange.FeatureFlags.Import.*` 0, process-local).
  - A crash without a result json writes `{"ok": false, "errors": ["Unreal crashed: <assertion line>"]}`; the result json carries `mode`.
- **PC tests** (UE 5.7 UE_VFX_57, editor PID 89776 kept running, PIE off, nothing outside `/Game/SM2_Work/Tests/oneclick/` and one stray folder below):

| Run (KST) | Asset | Bridge | Result |
|---|---|---|---|
| 16:29 | wpn_bolt_rifle_01 + SEQ RELOAD_FULL | EngineWire | OK, 30 assets (SkeletalMesh, 12 MI, clip 2.40 s), 8.4 s |
| 16:31 | same again | EngineWire | OK, old copy `wpn_bolt_rifle_01_bak1` (30 assets), 14.3 s |
| 16:33 | cc_calgar + tacticus_wpn10_reload | EngineWire | OK, 106 assets (44 MI, clip ADD 2.73 s, `PA_cc_calgar_SM2`, post-process `ABP_cc_calgar_SM2Phys`), 60.2 s |
| 16:42 | wpn_bolt_rifle_01 | Remote Execution (forced) | OK, `wpn_bolt_rifle_01_remote_bak1`, 18.7 s |
| 16:44 / 16:58 | wpn_bolt_rifle_01 | headless (forced) | crash (Interchange Slate assert) → fixed with legacy factories |
| 17:01 | wpn_bolt_rifle_01 | headless (forced) | OK, 30 assets, `_headless_bak3`, 5.8 s import (~15 s with engine start) |
| 17:04 | cc_calgar Anim Only (Manny) | EngineWire | OK, `Anims_Manny/..._ADD_Manny` on SK_Mannequin, 2.73 s |

  - The 17:02 Manny run before the QA-order fix imported into the suggested default `/Game/SM2_Work/Animations/cc_calgar` (1 new clip, no existing asset touched). It is left there; delete it in the next cleanup if not wanted.
  - UE captures (ue_cap_lit, level not saved): box `/workspace/mtl_fix/oneclick/07_ue_cc_calgar.png`, `08_ue_bolt_rifle.png`. UI: `01_export_dialog_direct_option.png`, `02_prop_headless_ok.png`, `03_prop_remoteexec_bak1_ok.png`, `04_char_enginewire_ok.png`, `05_char_export_dialog.png`, `06_manny_ok.png`.
- **QA env** (Forge Lens, see WORKFLOW "oneclick QA"): `SM2_OPEN`, `SM2_SEQ` / `SM2_CLIP`, `SM2_EXPORT_QA=<out dir>` (exports the opened asset through the Export window), `SM2_DIRECT_PROJECT`, `SM2_DIRECT_PATH`, `SM2_EXPORT_CLIPS` (0 none, 1 selected, 2 preview clip, 3 all), `SM2_EXPORT_MANNY=1`, `SM2_EXPORT_GO=<s>`, `SM2_DIRECT_BRIDGE`. Driver: box `/workspace/mtl_fix/oneclick/fl_oneclick.ps1` (PC copy `Scripts\mtl_fix\oneclick\`), run with Windows PowerShell 5 (`powershell.exe -File`); pwsh 7 cannot compile its System.Drawing type.
- **Tests:** SM2.Assets 372/372 (incl. `UnrealDirectTests`), SM2Extractor 43/43, LibSaber.Tests 5/5, Python unittest 20/20 (`test_forge_direct.py`). HaloCEA out of scope (4 fail, as before).
- **Deploy:** `Desktop\SM2_Work\Bin\ForgeLens_latest\ForgeLens.exe`; previous exe → `ForgeLens.exe.bak17`. sha256 in the final report / STATUS.

### SM2_Work cleanup (2026-10-29)
- The 10/29 SM2_Work disk cleanup freed 26.8 GB (log `Desktop\SM2_Work\cleanup_log.txt`).
- Deleted: `Anim\Titus_Anim` (regenerable with `export --anim`), `Captures\mrq16_before` (regenerable), `Export\Step2`, old `buildNN` folders, v4 temp dirs, and the old `ForgeLens_latest\ForgeLens.exe.bak5`–`.bak13`. Backup numbering continues upward (next free number after the highest existing one, never reused).

## Update rea-batch 6 (2026-10-29 KST): REA batch 6 — adaptive light multiplier PROVEN, DAT_14502deac never written, ribbon CPU point writer PROVEN, fire clip not in exe

Headless Ghidra (`/workspace/rea/ghidra_proj3`, read-only) + byte/RIP scans; REA daemon not started. Details: RE_NOTES "rea-batch 6", SYMBOL_DB (index.tsv + vfx/misc/animation md, hand-added). No code change.
- **Adaptive light multiplier (PROVEN):** `FUN_14224ecf0` param_3 = `FUN_141e90cb0` = clamp(avgLum[vp] @DAT_144f9d06c stride 0x198, 0.05, 50) on the dynamic-light path `FUN_141ee9e80` (0x141eea048/331/517); `FUN_141de8140`, `FUN_14224fa80` pass 1.0. Follow-up: bench + UE export multiply adaptive-light colour by the clamped avgLum used for particles (bench exposure 1/250 itself still HEURISTIC).
- **DAT_14502deac (INFERRED constant false):** 3 READs only (0x14224eda9, 0x14224eeaf, 0x14224f029); no RIP-relative write/LEA, no absolute pointer; BSS → 0; no cvar string.
- **Ribbon trail point writer (PROVEN CPU, INFERRED trlPOINT mapping):** emitter `FUN_141902840`, updater `FUN_141847c30` (0x50-byte points, half-float pairs, +0x1c = 255.0 constant, +0x24 colorIntensity, +0x30 alphaClip, +0x34 (ambientLight, age)). No CPU per-point RGB. GPU upload not located.
- **Player fire clip (HEURISTIC stays):** no fireRate/shootAnim strings in the exe → script data; decode SSL on the PC next.
- Also: `tools/re/box/g.sh` points at `/home/box/tools/ghidra_12.1.4_PUBLIC` (fresh-account path).

## Update bot-transfer (2026-10-29 20:50 KST): SM2 handed to a new Grok Bot
- Owner request (ledger R33): SM2 / Forge Lens moves to a new Grok Bot that should continue as if it were the same assistant; **BongThumbLive stays with the original bot** and is not part of the handoff.
- New single entry point for the new bot: **`docs/BOT_TRANSFER.md`** (goal, repo + push, environment, REA + Symbol DB, rules, status, ordered open list, routine). Box copy `/workspace/handoff/BOT_TRANSFER.md`; first prompt `/workspace/handoff/NEW_BOT_PROMPT.md`.
- Box clone `/workspace/LibSaber` was fast-forwarded 1565d17 → `e5b4e8b` with an incremental bundle from the PC clone `ls_push` (box has no GitHub login), then this commit was made and pushed from the PC via `push.ps1` (patch copy box `/workspace/handoff/`).
- State checked at 20:50 KST: no game running, UE closed (port 3000 closed), no leftover `0*.patch`, deployed `ForgeLens_latest\ForgeLens.exe` sha256 `3B74C617…2124A36` (+ `.bak14`–`.bak17`). REA daemon processes alive since 10:22 but without a Ghidra child (last answers 11:33) → treat as cold (≈22 min re-import) or use the headless fallback.
- 20:55 KST follow-ups: the new bot is on a **different Grok Bot account** (no shared box/memory/routines/PC registration) → BOT_TRANSFER §0 step 0 "Fresh environment setup"; box-only scripts/notes committed (`tools/re/ghidra_scripts`, `tools/re/box`, `tools/re/scans`, `tools/level`, `tools/ue/oneclick`, `docs/handoff/box_notes`); owner name in docs = **Baegovda**.
- Open list (order): REA batch {ribbon trail per-point colour writer, adaptive light multiplier (param_3 of FUN_14224ecf0), DAT_14502deac writer, player fire clip selection (HEURISTIC)} → L/R pauldron overrides → colour scheme → UE master material → F43 .td non-texture params → muzzle flash offset + aim pose from data → physics for other characters → cloth build28 → level/prop placement extraction → later PoE / UE games.

## Split skeleton (R35, 2026-10-29 KST)
Split skeleton — 애니메이션과 메시가 서로 다른 스켈레톤을 쓰는 문제. Reported by the BongThumbLive bot.
- **Symptom (PROVEN, uasset import tables + UE query):** `Tests/sani_before/hologram_planet/localbake/hologram_planet__anim` used `localbake/hologram_planet_Skeleton`, while the mesh next to it (`localbake/hologram_planet`) used `Props/hologram_planet/hologram_planet_Skeleton`. Same 79 bone names in the same order, but not compatible, so the clip could not play on any mesh.
- **Cause (INFERRED):** the "before" copy was assembled from separate imports/moves: the old local-bake clip kept its own `{mesh}_Skeleton` while the old mesh ended up pointing at the re-imported Props skeleton. Our importers never pinned the anim to the sibling mesh's skeleton by check, and the `ue_sani_fix.py import ... remesh` path re-imported the mesh without passing the existing `{mesh}_Skeleton`. The source FBX for that clip no longer exists (export folders cleaned), so it could not be re-imported from the original.
- **Rule (generic):**
  1. Anim import (`sm2_anim_import` block in `ue_import_anim.py`, `ue_sani_fix.py`, `ue_anim_survey.py`) always takes the sibling mesh's skeleton and now prints `SPLITSKEL <anim> uses X, expected Y` when the result is on another skeleton (a re-import over an existing clip keeps the clip's old skeleton — PROVEN 10/29).
  2. `ue_sani_fix.py` mesh re-import reuses the folder's existing `{mesh}_Skeleton` (`FbxImportUI.skeleton`).
  3. Repair: `uerun.ps1 tools/ue/ue_fix_split_skeletons.py scan|fix [root] [filter]`. Sibling mesh = SkeletalMesh in the same folder named like the clip prefix `{mesh}__clip`, else the only one. Skips `/Game/SM2_Work/Original/*`, `/Game/Work/*`, Lightning_01 and `_bakN` assets. `fix` only when the bone-name lists are equal: moves the clip aside to `<anim>_bakN` (rename, nothing deleted), imports its source FBX (or, if gone, a UE FBX export of the clip at `SM2_Work\Backup\split_skel\<anim>.fbx`, yaw 0) onto the mesh skeleton at the same path, copies root-motion flags from the backup. Last-resort fallback: mark the two skeletons compatible (with `_bakN` backups of both).
- **Scan (PROVEN, 10/29):** /Game/SM2_Work 90 anims / 52 skeletal meshes → 1 split (the hologram localbake clip); after the fix 0. By-design cross-folder sharing (not splits, left alone): `Animations/cc_titus_assault/*` on `Original/cc_titus_assault` skeleton (Lightning_01 chain), `Animations/cc_swarm_hormagaunt/Bulk/*` on `Tests/pre_step3` skeleton, `Sequences/TitusWalkFire/*` on `Tests/chars_phys/cc_titus_assault` skeleton.
- **UE repair (10/29 ~23:00 KST):** `localbake/hologram_planet__anim` now on `Props/hologram_planet/hologram_planet_Skeleton` (= mesh skeleton), length 200 s, world bone positions vs the original max diff 0.0003 cm over 79 bones × 6 times, root-motion flags restored (off / RefPose). Backups (cleanup candidates for the batch pass): `hologram_planet__anim_bak1`–`_bak4` (copies of the original; bak4 used as reference), `_bak5` (intermediate re-import); `localbake/hologram_planet_Skeleton_bak1`–`_bak3` and `Props/hologram_planet/hologram_planet_Skeleton_bak1`–`_bak3` (from compatible-fallback test runs; both live skeletons are back to an empty compatible list). `localbake/hologram_planet_Skeleton` is now unused (kept, not deleted).

## Split skeleton follow-up (R43, 2026-10-30 KST)
Re-check of the BongThumbLive bot report (same case as R35) after the R42 cleanup.
- **Current state (PROVEN, AssetRegistry headless commandlet `tools/ue/ue_skel_audit.py /Game`, editor was closed):** 39,224 AnimSequences / 2,473 SkeletalMeshes / 921 Skeletons in /Game; **0 splits**. `Tests/sani_before/hologram_planet/localbake/hologram_planet__anim` (+ `_bak1..5`) and `localbake/hologram_planet_Skeleton` (+ `_bak1..3`) were deleted by R42 (`CLEANUP_DELETED.tsv`); `localbake/hologram_planet` (mesh) and `Props/hologram_planet/hologram_planet` both import `Props/hologram_planet/hologram_planet_Skeleton` (PROVEN, package import strings). The live clip `Animations/hologram_planet/hologram_planet__anim` is on the same Props skeleton.
- **Bone claim:** the UE FBX export of the original clip (`SM2_Work\Backup\split_skel\hologram_planet__anim.fbx`) has 79 Models / 237 T/R/S curve nodes (PROVEN, string count) — 79 = mesh skeleton (R35 editor check). "78 animated + extra bone hologram_planet" could not be re-checked: the UE export keys every bone and the original anim is deleted (UNVERIFIED).
- **Cause (INFERRED):** skeletons were matched by name, not by asset. UE's FBX importer names every new skeleton `{mesh}_Skeleton` in the import folder; our mesh imports (`ue_bulk.py`, `ue_forge_direct.py`, `ue_anim_survey.py`; `ue_sani_fix.py` only same-folder since R35) never passed an existing skeleton, so importing a mesh (or a mesh+anim FBX) into a second folder (the "localbake" copy) made a second same-named, incompatible skeleton; R35's anim check only printed a warning. The Forge Lens exporter (C#) only writes the FBX (`{mesh}_Skeleton` Null, `FbxWriter.cs`) and cannot pick a UE skeleton; the fix belongs in the UE import step.
- **Rule (generic, `tools/ue/sm2_skeleton_rule.py`):** skeletons are identified by object path. Mesh import → `sm2_mesh_skeleton(folder, mesh)`: same-folder `{mesh}_Skeleton` → reuse; else exactly one live (non-`_bakN`) `{mesh}_Skeleton` anywhere in /Game → reuse (unify, log `SKELREUSE`); ≥2 → `SkeletonRuleError SKELCOLLISION` (import refused); none → UE makes a fresh one. Anim import (`sm2_anim_import`, all 3 copies identical) now **raises** `SPLITSKEL` when any clip lands off the mesh skeleton. Audit: `ue_skel_audit.py [root] [filter] [outfile]` prints SPLIT / COLLISION / DANGLING / ORPHAN.
- **Existing same-name collisions (PROVEN, audit):** 44 in /Game, 2 in SM2_Work: `cc_titus_assault_Skeleton` ×3 (Characters, Original, Tests/chars_phys) and `cc_swarm_hormagaunt_Skeleton` ×2 (Characters, Tests/pre_step3) — known by-design sharing (R35), no split today; a new import of those meshes into a third folder now fails loudly instead of making a 4th skeleton. The other 42 are marketplace packs (not ours, untouched).
- **SK_Sci_Fi_Cyborg_03_Skeleton (PROVEN audit + files):** third-party pack `/Game/Characters/Humanoids/Sci_Fi_Cyborg_03` (12 `Demo/Aim_*` anims, 3 `Mesh/Separate_parts/SK_Sci_Fi_Cyborg_03_{Head,Leg,Tors}` meshes, map, materials). The 12 anims point at `/Game/Characters/Mesh/SK_Sci_Fi_Cyborg_03_Skeleton`, which does not exist (folder absent) → DANGLING/ORPHAN; the 3 part meshes import no skeleton package (INFERRED: null skeleton). Not SM2 game data (marketplace naming; INFERRED), so the exporter has nothing to do; fix = re-add the pack's main mesh+skeleton from its vendor (owner's call). Not touched.
- **Assets changed:** none (no split left to repair).
- **Unverified:** the new importer wiring has not run inside UE yet (unit tests only); Interchange (`Import.FBX 1`) honouring `FbxImportUI.skeleton` in `ue_anim_survey.py`.

## R90 cloth rest-fit plugin in UE + game sky (2026-10-30 18:35-19:23 KST)
- Installed SM2PhysicsSetup built in R81 (deep particles ignored, fit floor 0.9) into UE_VFX_57; old plugin backed up to `K:\Unreal Projects\UE_VFX_57\SM2PhysicsSetup.bak1` (10/29 dll). Report now prints `deepIgnored` (titus tbrd_01_frnt 37, calgar cloak 2).
- New tools: `tools/ue/ue_r90_cloth.py` + `tools/ue/r90_pass.ps1` (prep -> env -> actor -> cams split into separate calls; fast clip; Simulate; capture + measure t1..t3). Shots/metrics: `Desktop\SM2_Work\Shots\r90_cloth\<subj_clip>\{before,after}`, `summary.txt`, `r90_cloth_sheet.png`, `r90_sorcerer_sheet.png`.
- Results (sum over t1..t3, inside>1cm / skin>1cm; before = 2 runs, after = 2 runs): sorcerer death 40-66/903-1151 -> 441-514/592-658 but BEFORE = cloth explosion (huge flying sheets), AFTER = stable cloth on the body (PROVEN by shots; penetration counts rise because cloth now stays at the body). titus run 180-200/289-368 -> 152/160-178. titus melee 84-99/98-120 -> 140-142/68-86. calgar run 10-79/0-17 -> 7-8/15. vortex melee 8-31/42-67 -> 56-64/38-41 (vortex fit unchanged, minRadiusScale 1.0 -> difference = run noise, INFERRED).
- Only 4 cloth characters exist in UE (calgar, sorcerer_exalted, titus, vortex); tech_priest/chaplain/commissar meshes are not imported -> 6-8 not reached.
- Crash cause (PROVEN twice, 18:35/18:42): editor launched minimized (rect -40000) -> zero-size viewport -> EXCEPTION_INT_DIVIDE_BY_ZERO when spawning actors via Python. Fix: after launch ShowWindow(SW_SHOWNOACTIVATE=4) before any spawn.
- Sky: `sky_env.py` must be called as `uerun.ps1 sky_env.py -argv import,<hdr>` (positional 2nd arg is dropped). Imported `T_buttle_barge_sky_1` (TextureCube), applied with skyInt 0.75; captures `r66_level.ps1 -NoSky -Ev 1.2`. vpersp mean/sd 142.6/40.6 -> 108.5/50.7 (viewer 114.3/58.6); overview 154.8/37.5 -> 130.1/44.7. Sheet `Shots\r66_level\r90_sky_sheet_v2.png`. Reflection probes not converted.
- Not done: sorcerer robe colour, animation+cloth capture beyond these clips, PNG imports, polish list (pauldrons, F43 .td, muzzle, other-char physics, props, _bakN cleanup).

R91 cloth A/B on 10 character-clips x 3 runs + 3 new cloth characters (2026-10-30 19:56-21:50 KST)
- Imported with _SIM cloth meshes (extractor --include-cloth-sim, set `Exports\chars_cloth2`): cc_tech_priest (hordemode death, censer walk), cc_chaplain (turn 180, idle; no death/run/melee clip exists for him), cc_commissar (death_1, run start, melee death) -> `/Game/ForgeLens/SM2/Tests/chars_cloth2/*` (R86 root). Chaplain mesh reuses `/Game/Characters/Humanoids/40K_Chaplain/cc_chaplain_Skeleton` (outside our folders, SKELREUSE rule; skeleton not modified/saved). chars_phys exports lack simPositions -> 0 cloths converted; use chars_cloth2.
- Plugin: rest-fit floor / deep fraction are now CVars `sm2.RestFitFloor` (0.9) and `sm2.RestFitDeepFrac` (0.5); legacy pre-R76 = 0.25 / 0. Built `Build\sm2phys\pkg91`, installed (old -> `UE_VFX_57\SM2PhysicsSetup.bak2`). A/B in one session: `tools/ue/r91_ab.ps1 -Runs 3` (+ `r91_cvar.py`), Message Log closer `close_msglog.ps1`, sheet `r91_sheet.py`.
- Results (`docs/handoff/r91_cloth_summary.txt`; shots `Desktop\SM2_Work\Shots\r91_cloth\run1-3`, sheet `r91_cloth_sheet.png`). skin = free cloth particles >1 cm inside the real skinned body (sum t1..t3), mean of 3 runs, legacy -> cur:
  titus melee 187 -> 74, titus run 419 -> 183, calgar run 26 -> 15, vortex melee 41 -> 29, chaplain turn 217 -> 200, commissar melee 75 -> 63, commissar death 269 -> 258, tech_priest death 173 -> 168, commissar run 167 -> 185 (worse), sorcerer death 888 -> 1244 (legacy = exploding cloth, runs noisy 1052-1574).
- Titus melee "got worse" in R90 = metric artifact (PROVEN by numbers): `pen` counts particles inside the collision SHAPES, and the fix makes shapes bigger (rs 0.25/0.39/0.73 -> 0.90), so pen rises 96 -> 140 while real body penetration (skin) falls 187 -> 74. Floor 1.0 (f100) is even better on titus (melee 66, run 119) and similar on sorcerer (1007); tested on 2 characters only -> default kept at 0.9 (decision: keep fix, no revert).
- Not done here: before/after on a 1.0 floor for all 8, sorcerer noise source.
- R91 sky/floor (21:50-21:55 KST): `tools/ue/r91_floor.py` = bottom-35 % band (floor proxy, not a true floor mask) of the viewer-matched vpersp/vclose captures: UE EV 1.2 mean 116.4 / 62.6, clipped (>=245) 0.12 % / 0.00 %; EV 0.6 90.6 / 43.1; EV 0.0 66.0 / 27.5; viewer levelbench 145.9 / 81.2, clipped 14.76 % / 1.37 %. -> UE floor is NOT overexposed and is darker than the viewer; EV 1.2 kept. Cameras of the two images do not match exactly (sheet `Shots\r66_level\r91_floor_sheet.png`): the washed look is flat lighting/contrast, not exposure (INFERRED).
- Reflection probes: `tools/ue/ue_r91_probes.py add|clear` puts 8 SphereReflectionCaptures (radius 4500, tag R91Refl) on the busiest cells of the r54 scratch level + BuildReflectionCaptures; vpersp mean abs diff only 1.2/255 (`Shots\r66_level\r91_refl_ev12`), kept and saved. Game probe data not converted (not found/parsed).
- Not done in R91: sorcerer robe colour bake (needs the browser-written `*.colour_scheme.*.json` -> `import_sm2_materials.py --scheme=`), PNG imports, polish list (pauldrons, F43 .td, muzzle, other-char physics, props, _bakN cleanup), floor 1.0 A/B on all 8 characters.
- Editor: saved 492 dirty packages, all under /Game/SM2_Work or /Game/ForgeLens (none outside); lock `editor-free 21:55:35`; UE_VFX_57 left running, Message Log closed.

## R92 (2026-10-30 ~22:45 KST) - cloth3 worker: sorcerer robe colour, PNG imports
- **Robe colour (item 1): no colour-scheme file applies, UE material colour already matches the viewer.** No `*.colour_scheme.*.json` exists anywhere under `Desktop\SM2_Work` (searched). The sorcerer has no CC block: the r72 sidecar (`Exports\r72\cc_sorcerer_exalted\...materials.json`) cloak/skirt/tabard/skirt_adds/cloth_01 slots are plain diffuse (tint 255, no layers/scheme); `glt.chameleon` is only on slot 7 (jetpack). The coat texture itself is red (`ch_exalted_sorcerer_coat_01.png` mean R142 G71 B72, sampled every 16 px), skirt R135 G96 B97 (red + blue tabard). UE BaseColor capture of `Characters/cc_sorcerer_exalted` (Map_Mats, `SCS_BASE_COLOR`) = red cloak, blue tabard, teal armour, same as the viewer r72 shot (`Shots\r72\viewer_cc_sorcerer_exalted.png`). The "pink" look in R72 lit shots is lighting/exposure (washed-out final LDR), not a material error. Found: the R72 SceneCapture output did NOT change with `auto_exposure_bias` -3/-1.5/0/1.2 (byte-identical PNG sizes), so EV on those captures is not effective (cause not checked). Shots PC `Desktop\SM2_Work\Shots\r92\robe\` (base_yaw0/180 = base colour). `tools/ue/ue_r92_robe.py` (set/cap/src/reset; capture needs a separate call after set, else it shows the previous state).
- **2 PNG imports (item 2):** the R72 gap 62/64 textures = `ch_exalted_sorcerer_hair_01_ao` (named in `material.json` only, not in the `materials.json` maps the importer reads) + `__default_material__` (68 B placeholder, also the vortex 33/34 gap). Imported to `/Game/ForgeLens/SM2/Textures/cc_sorcerer_exalted/` (hair_01_ao 512x512, sRGB off, grayscale; default 1x1), saved (`.uasset` on disk 22:44). Not wired into any MI (no slot uses them). `tools/ue/ue_r92_pngimp.py`, `tools/ue/ue_r92_texlist.py`.
