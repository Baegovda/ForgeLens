# mtl1-33 (from run32): adds samplewrong (wrong side of a limb) after every sampleskin; capture c28_m33_<tag>.png.
# mtl1-32 (from run31): run33.ps1 <asset> <tag> <physics.json> ["Prop=Value;..." for cfgtune]
# Drops the old diag, builds the unsaved diag from <physics.json>, optional in-memory config tweak, SIE: sample+sampleref+sampleskin
# <physics.json> = "saved": skip ue_cdiag and check the saved <asset>_ChaosCloth instead (ue_c28 falls back when no diag exists).
# 4 s after start (pre-kick), +80z kick, 3x (sample, sampleref, sampleskin) 2 s apart, capture c28_m32_<tag>.png, stop, clean, PIE check.
param([string]$N, [string]$Tag, [string]$Json, [string]$Cfg = "")
cd C:\Users\Revaptor_FX\Desktop\SM2_Work\Scripts
$S = "C:\Users\Revaptor_FX\Desktop\SM2_Work\Scripts\mtl_fix"; $a = "$N,chars_cloth"
function U28($x) { & .\uerun.ps1 "$S\ue_c28.py" ($x -split ',') 2>&1 | Select-String -Pattern "^C28 (cloth|exp|spawned)" | % { "$Tag $($_.Line)" } }
& .\uerun.ps1 "$S\ue_diagdrop.py" 2>&1 | Out-Null
if ($Json -ne "saved") { & .\uerun.ps1 "$S\ue_cdiag.py" @($N, "chars_cloth", $Json) 2>&1 | Select-String -Pattern "^CD .*(collision |boundSections)" | % { "$Tag $($_.Line)" } }
if ($Cfg) { & .\uerun.ps1 "$S\cfgtune.py" @("/Game/SM2_Work/Tests/diag_unsaved/${N}_diag", $Cfg) 2>&1 | Select-String "CFGT" | % { "$Tag $($_.Line)" } }
U28 "spawn,$a"; U28 "start,$a"; Start-Sleep 4; U28 "sample,$a"; U28 "sampleref,$a"; U28 "sampleskin,$a"; U28 "samplewrong,$a"; U28 "kick,$a"; Start-Sleep 3
foreach ($k in 1..3) { U28 "sample,$a"; U28 "sampleref,$a"; U28 "sampleskin,$a"; U28 "samplewrong,$a"; Start-Sleep 2 }
U28 "cap,$a"; U28 "exp,$a,m33_$Tag"; U28 "stop,$a"; U28 "clean,$a"
& .\uerun.ps1 "$S\ue_pie.py" 2>&1 | Select-Object -Last 1 | % { "$Tag $_" }
