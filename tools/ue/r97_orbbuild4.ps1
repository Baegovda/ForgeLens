# r97_orbbuild4.ps1 (R97t): glow=SingleLoopingParticle template, runes=Minimal template, sparks=EngineWire default (rising). Templates avoid default upward velocity.
param($N="NS_vortex_orb_v6")
$D="/Game/ForgeLens/SM2/VFX/vortex_orb"; $S="$D/$N"; cd $env:USERPROFILE
function ew($h){ ($h|ConvertTo-Json -Compress -Depth 5) | Set-Content $env:TEMP\ew1.json; $r=python enginewire_call.py manage_effect $env:TEMP\ew1.json 2>&1 | Select -First 3; "$($h.action) $($h.emitterName): $r" }
ew @{action="create_niagara_system";name=$N;path=$D;save=$true}
ew @{action="set_emitter_properties";systemPath=$S;assetPath=$S;emitterName="DefaultEmitter";emitterProperties=@{enabled=$false};save=$true}
$T="/Niagara/DefaultAssets/Templates/Emitters"
foreach($e in @(@{t="SingleLoopingParticle";m="M_orb_glow";size=60},@{t="Minimal";m="M_orb_runes";size=30;rate=8},@{t="Fountain";m="M_orb_sparks";size=4})){
 $b=@{systemPath=$S;assetPath=$S;emitterName=$e.t;save=$true}
 ew ($b+@{action="add_emitter_to_system";emitterPath="$T/$($e.t)"})
 ew ($b+@{action="add_sprite_renderer_module";materialPath="$D/$($e.m)"})
 if($e.rate){ ew ($b+@{action="add_spawn_rate_module";spawnRate=$e.rate}) } }

ew @{action="get_niagara_info";systemPath=$S}


