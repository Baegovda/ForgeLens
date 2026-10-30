# r97_orbbuild7.ps1 (R97x): glow = Minimal + SingleLoopingParticle (both M_orb_glow), runes = Minimal (M_orb_runes, 1 frame of 2x2), sparks = Fountain.
param($N="NS_vortex_orb_v7")
$D="/Game/ForgeLens/SM2/VFX/vortex_orb"; $S="$D/$N"; $T="/Niagara/DefaultAssets/Templates/Emitters"; cd $env:USERPROFILE
function ew($h){ ($h|ConvertTo-Json -Compress -Depth 5) | Set-Content $env:TEMP\ew1.json; $r=python enginewire_call.py manage_effect $env:TEMP\ew1.json 2>&1 | Select -First 3; "$($h.action) $($h.emitterName): $(($r -join ' ').Substring(0,[Math]::Min(160,($r -join ' ').Length)))" }
ew @{action="create_niagara_system";name=$N;path=$D;save=$true}
ew @{action="set_emitter_properties";systemPath=$S;assetPath=$S;emitterName="DefaultEmitter";emitterProperties=@{enabled=$false};save=$true}
function add($tpl,$mat){ $r=ew @{action="add_emitter_to_system";systemPath=$S;assetPath=$S;emitterPath="$T/$tpl";save=$true}; $r; $nm=if("$r" -match "Added emitter '([^']+)'"){$matches[1]}else{$tpl}; ew @{action="add_sprite_renderer_module";systemPath=$S;assetPath=$S;emitterName=$nm;materialPath="$D/$mat";save=$true} }
add "Minimal" "M_orb_glow"; add "SingleLoopingParticle" "M_orb_glow"; add "Minimal" "M_orb_runes"; add "Fountain" "M_orb_sparks"
ew @{action="get_niagara_info";systemPath=$S}
# second Minimal is named Minimal001: re-run add_sprite_renderer_module Minimal->M_orb_glow, Minimal001->M_orb_runes
# Minimal needs add_spawn_rate_module (Minimal 6/s, Minimal001 8/s), then compile (open/close asset editor)
# Minimal (glow) ignored its material (default grey disc) -> disabled; glow = SingleLoopingParticle + bloom
# correction: material calls on Minimal001 do not take effect (renderer stays default grey disc) -> Minimal001 disabled, Minimal = runes
