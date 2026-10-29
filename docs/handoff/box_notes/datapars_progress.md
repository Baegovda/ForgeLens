# datapars progress (2026-10-29 KST) — data-driven hand hide + data-defined HEURISTICs
Worktree: /workspace/LibSaber-datapars (branch datapars from origin/cursor/sm2-pck-extractor-4420 @28489a0). Other worker in LibSaber-ui45 (REA heuristics).
- 09:25 started
- 09:45 FOUND: rule is in script bytecode ssl/body/customization/prop_power_fist_customizer_base_release.sslbin (SSLB = 16-byte header + protobuf: f6 strings, f8 classes {f4 members: name, slot, kind, f6 const}, f9 code {f2 sig, f5 instrs varint: op=v&0xff, arg=(v>>8)-0x800000}).
  SetRightHandVisibility(isVisible): meshes = propCustomizerState.GetMeshesByBodySection(03 6 = int 6) [slot 82 in prop_character_customization_state_base]; filtered = FilterRightHandMeshes(meshes) (copy minus visibilityUnchangableMeshes); handRegion.SetMeshes(filtered) [GeomRegion slot 12]; isVisible ? propGeomCustomizer.Show(handRegion) [56] : Hide(handRegion) [57].
  character_body_section_release.sslbin: CharacterBodySection.RIGHT_GAUNTLET = int 6 -> PROVEN.
  GetMeshesByBodySection = outfit region picked by settings for that section -> .GetMeshes() (active region meshes).
  Callers: by_melee_state ShowFist -> SetRightHandVisibility(false) (hand hidden while fist shown); HideFist -> HideActive + SetRightHandVisibility(true). by_loadout ApplyCustomizationByVersion -> SetRightHandVisibility(false).
  Init -> HideAllFists = geom.(167)(powerFistIds) (native hide list).
  Tools: /workspace/mtl_fix/datapars/pb.py, sslb.py

## 10:xx KST
- Sslb.cs + PowerFistHandRule.cs compile; wired into MeleeCustomization (HandRule from Load), VariantRequest.FistIds/HandRuleEvidence, ArmorVariant hides powerFistIds (prefix fallback), AssetExporter + Kitbash fill FistIds.
- PowerFistHandRuleTests (synthetic + retail): 284/284 pass; retail Evidence = PROVEN RIGHT_GAUNTLET int 6.
- Next: full solution build, other data heuristics, docs, commit/push.

## done (~09:50 KST)
- Rebased onto 8bdd80a (other worker mtl1-50b; doc conflicts in HANDOFF/RE_NOTES/STATUS merged, both kept). Build ok, SM2.Assets 288 pass (retail on).
- Pushed via push.ps1 (patch 0002-datapars.patch): origin tip fcc16bf, fast-forward.
- Firearm `main` region left HEURISTIC (equipment Init decoded; firearm_skin_release.sslbin not extracted on box/PC research dirs).
