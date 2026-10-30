# mtl1-31 A/B of one cloth variant on cc_tech_priest (chars_cloth): run31.ps1 <tag> <physics.json> ["Prop=Value;..." for cfgtune]
# Drops the old diag, builds the unsaved diag from <physics.json>, optional in-memory config tweak, SIE: sample+sampleref
# 4 s after start (pre-kick), +80z kick, 3x (sample, sampleref) 2 s apart, capture c28_m31_<tag>.png, stop, clean, PIE check.
param([string]$Tag, [string]$Json, [string]$Cfg = "")
cd C:\Users\Revaptor_FX\Desktop\SM2_Work\Scripts
$S = "C:\Users\Revaptor_FX\Desktop\SM2_Work\Scripts\mtl_fix"; $a = "cc_tech_priest,chars_cloth"
function U28($x) { & .\uerun.ps1 "$S\ue_c28.py" ($x -split ',') 2>&1 | Select-String -Pattern "^C28 (cloth|exp|spawned)" | % { "$Tag $($_.Line)" } }
& .\uerun.ps1 "$S\ue_diagdrop.py" 2>&1 | Out-Null
& .\uerun.ps1 "$S\ue_cdiag.py" @("cc_tech_priest", "chars_cloth", $Json) 2>&1 | Select-String -Pattern "^CD .*(collision shapes|boundSections)" | % { "$Tag $($_.Line)" }
if ($Cfg) { & .\uerun.ps1 "$S\cfgtune.py" @("/Game/ForgeLens/SM2/Tests/diag_unsaved/cc_tech_priest_diag", $Cfg) 2>&1 | Select-String "CFGT" | % { "$Tag $($_.Line)" } }
U28 "spawn,$a"; U28 "start,$a"; Start-Sleep 4; U28 "sample,$a"; U28 "sampleref,$a"; U28 "kick,$a"; Start-Sleep 3
foreach ($k in 1..3) { U28 "sample,$a"; U28 "sampleref,$a"; Start-Sleep 2 }
U28 "cap,$a"; U28 "exp,$a,m31_$Tag"; U28 "stop,$a"; U28 "clean,$a"
& .\uerun.ps1 "$S\ue_pie.py" 2>&1 | Select-Object -Last 1 | % { "$Tag $_" }
