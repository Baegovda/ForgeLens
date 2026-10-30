# r97_orbbuild.ps1 (R97t): build NS via EngineWire. usage: r97_orbbuild.ps1 <NSname>
param($N="NS_vortex_orb_v3")
$D="/Game/ForgeLens/SM2/VFX/vortex_orb"; $S="$D/$N"; cd $env:USERPROFILE
function ew($h){ ($h|ConvertTo-Json -Compress -Depth 5) | Set-Content $env:TEMP\ew1.json; $r=python enginewire_call.py manage_effect $env:TEMP\ew1.json 2>&1 | Select -First 1; "$($h.action) $($h.emitterName): $r" }
ew @{action="create_niagara_system";name=$N;path=$D;save=$true}
ew @{action="set_emitter_properties";systemPath=$S;assetPath=$S;emitterName="DefaultEmitter";emitterProperties=@{enabled=$false};save=$true}
$E=@(@{n="NE_orb_glow";rate=6;life=0.8;size=45;v=@{x=0;y=0;z=0};m="M_orb_glow"},
     @{n="NE_orb_runes";rate=10;life=1.6;size=30;v=@{x=0;y=0;z=4};m="M_orb_runes"},
     @{n="NE_orb_sparks";rate=24;life=1.0;size=8;v=@{x=0;y=0;z=30};m="M_orb_sparks"})
foreach($e in $E){ $b=@{systemPath=$S;assetPath=$S;emitterName=$e.n;save=$true}
 ew @{action="create_niagara_emitter";name=$e.n;path=$D;save=$true}
 ew ($b+@{action="add_emitter_to_system";emitterPath="$D/$($e.n)"})
 ew ($b+@{action="add_niagara_module";modulePath="/Niagara/Modules/Emitter/EmitterState.EmitterState";scriptType="EmitterUpdate"})
 ew ($b+@{action="add_spawn_rate_module";spawnRate=$e.rate})
 ew ($b+@{action="add_initialize_particle_module";lifetime=$e.life;uniformSize=$e.size;sizeMode="Uniform"})
 if($e.v.z -ge 20){ew ($b+@{action="add_velocity_module";velocity=$e.v;velocityMode="Linear"})}
 ew ($b+@{action="add_particle_state_module"})
 ew ($b+@{action="add_sprite_renderer_module";materialPath="$D/$($e.m)"}) }
ew @{action="get_niagara_info";systemPath=$S}

