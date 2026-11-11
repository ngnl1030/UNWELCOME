param([string]$RunName = 'Smoke', [switch]$Internet)
$ErrorActionPreference = 'Stop'
$project = Split-Path $PSScriptRoot
$testRoot = Join-Path $project "Temp\MultiplayerTests\$RunName"
$exe = Join-Path $project 'Builds\MultiplayerTest\MakeTrap.exe'
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
$sequence = 0
$processes = @()
$checks = [System.Collections.Generic.List[string]]::new()
function Send-TestCommand([int]$id, [string]$action, [int]$slot = 0, [string]$value = '') {
    $script:sequence++
    $data = @{sequence=$script:sequence;action=$action;slot=$slot;value=$value;internet=[bool]$Internet}
    $path = Join-Path $testRoot "$id\command.json"
    $data | ConvertTo-Json | Set-Content -LiteralPath "$path.tmp" -Encoding UTF8
    if (Test-Path -LiteralPath $path) { [System.IO.File]::Move("$path.tmp", $path, $true) }
    else { [System.IO.File]::Move("$path.tmp", $path) }
}
function Read-State([int]$id) {
    $path = Join-Path $testRoot "$id\state.json"
    for($attempt=0;$attempt -lt 10;$attempt++) {
        try { if (Test-Path -LiteralPath $path) { return (Get-Content -LiteralPath $path -Raw | ConvertFrom-Json) }; return $null }
        catch { Start-Sleep -Milliseconds 30 }
    }
}
function Await-Test([scriptblock]$predicate,[string]$message,[int]$timeout=40) {
    $until = [DateTime]::UtcNow.AddSeconds($timeout)
    do { if (& $predicate) { $script:checks.Add($message); Write-Output "PASS $message"; return }; Start-Sleep -Milliseconds 300 } while ([DateTime]::UtcNow -lt $until)
    throw "FAIL $message"
}

try {
 foreach($i in 0..1){
  $dir=Join-Path $testRoot "$i"; New-Item -ItemType Directory $dir -Force | Out-Null
  $processes+=Start-Process $exe -ArgumentList @('-batchmode','-nographics','-mtTestPath',('"'+$dir+'"'),'-logFile',('"'+$dir+'\player.log"')) -WindowStyle Hidden -PassThru
 }
 Await-Test { (Read-State 0).scene -eq 'MainMenu' -and (Read-State 1).scene -eq 'MainMenu' } 'Two processes booted'
 Send-TestCommand 0 host
 Await-Test {(Read-State 0).host -and (Read-State 0).code} 'Host created'
 $code=if($Internet){(Read-State 0).code}else{'127.0.0.1'}
 Send-TestCommand 0 select 0
 Await-Test {(Read-State 0).players[0].slot -eq 0} 'Host selected slot'
 Send-TestCommand 0 fillbots
 Await-Test {@((Read-State 0).players | Where-Object bot).Count -eq 3} 'Three bots added'
 if((Read-State 0).canStart){throw 'Unready human accepted'}
 Send-TestCommand 1 join 0 $code
 Await-Test {(Read-State 1).players.Count -eq 4 -and @((Read-State 1).players | Where-Object bot).Count -eq 2} 'Human replaces bot and lobby syncs'
 Send-TestCommand 1 bot 1
 Start-Sleep -Milliseconds 800
 if(@((Read-State 0).players | Where-Object bot).Count -ne 2){throw 'Nonhost modified bots'}
 $checks.Add('Nonhost bot changes rejected')
 Send-TestCommand 0 bot 1
 Await-Test {@((Read-State 1).players | Where-Object bot).Count -eq 1} 'Host removes bot'
 Send-TestCommand 0 bot 1
 Await-Test {@((Read-State 1).players | Where-Object bot).Count -eq 2} 'Host adds bot'
 Send-TestCommand 0 bot 1
 Await-Test {@((Read-State 1).players | Where-Object bot).Count -eq 1} 'Freed kid slot'
 Send-TestCommand 0 bot 3
 Await-Test {@((Read-State 1).players | Where-Object bot).Count -eq 2} 'Moved bot to thief slot'
 Send-TestCommand 1 select 1
 Await-Test {@((Read-State 0).players | Where-Object { !$_.bot -and $_.slot -eq 1 }).Count -eq 1} 'Human selects freed slot'
 Send-TestCommand 0 ready
 Await-Test {@((Read-State 0).players | Where-Object { $_.slot -eq 0 -and $_.ready }).Count -eq 1} 'Ready before appearance change'
 Send-TestCommand 0 character 0 '1'
 Await-Test {@((Read-State 1).players | Where-Object { $_.slot -eq 0 -and $_.character -eq 1 -and !$_.ready }).Count -eq 1} 'Appearance change replicates and clears ready'
 Send-TestCommand 1 character 0 '1'
 Start-Sleep -Milliseconds 700
 if(((Read-State 0).players | Where-Object slot -eq 0).character -ne 1){throw 'Non-owner changed character'}
 $checks.Add('Cannot change another human appearance')
 Send-TestCommand 0 character 0 '-1'
 Await-Test {((Read-State 1).players | Where-Object slot -eq 0).character -eq 0} 'Left arrow restores boy'
 Send-TestCommand 0 character 2 '1'
 Await-Test {((Read-State 1).players | Where-Object slot -eq 2).character -eq 1} 'Host changes bot appearance'
 Send-TestCommand 0 character 2 '-1'
 Await-Test {((Read-State 1).players | Where-Object slot -eq 2).character -eq 0} 'Host restores fat thief'
 Send-TestCommand 1 character 1 '99'
 Start-Sleep -Milliseconds 500
 if(((Read-State 0).players | Where-Object slot -eq 1).character -ne 1){throw 'Invalid direction accepted'}
 Send-TestCommand 0 ready
 Send-TestCommand 1 ready
 Await-Test {(Read-State 0).canStart -and (Read-State 1).canStart} 'All ready'
 Send-TestCommand 0 start
 Await-Test {(Read-State 0).avatars.Count -eq 4 -and (Read-State 1).avatars.Count -eq 4} 'Mixed match spawned'
 foreach($i in 0..1){
  $models=@((Read-State $i).avatars | Sort-Object slot | Select-Object -ExpandProperty model)
  if(($models -join ',') -ne 'NetworkKid(Clone),NetworkKidGirl(Clone),NetworkThief(Clone),NetworkThiefThin(Clone)'){throw "Wrong selected models: $models"}
 }
 $checks.Add('All four chosen models spawned identically on both peers')
 foreach($i in 0..1){
  $s=Read-State $i
  if(@($s.avatars | Where-Object bot).Count -ne 2 -or @($s.avatars | Where-Object input).Count -ne 1 -or @($s.avatars | Where-Object movement).Count -ne 1 -or $s.cameras -ne 1 -or $s.listeners -ne 1){throw "Ownership/camera failure $i"}
  if(@($s.avatars | Where-Object patrol).Count -ne $(if($i -eq 0){2}else{0})){throw 'Patrol authority failure'}
  if(@($s.avatars.slot | Select-Object -Unique).Count -ne 4){throw 'Slot synchronization failure'}
 }
 $checks.Add('Local input and cameras isolated; bots simulated only on host')
 $before=@((Read-State 0).avatars | Where-Object bot)
 Start-Sleep -Seconds 2
 foreach($b in $before){
  $h=(Read-State 0).avatars | Where-Object slot -eq $b.slot
  $r=(Read-State 1).avatars | Where-Object slot -eq $b.slot
  $m=[Math]::Abs($h.position.x-$b.position.x)+[Math]::Abs($h.position.z-$b.position.z)
  $d=[Math]::Abs($h.position.x-$r.position.x)+[Math]::Abs($h.position.z-$r.position.z)
  if($m -lt .2 -or $d -gt 1){throw "Bot movement failure $($b.slot) moved=$m delta=$d"}
 }
 $checks.Add('Bots move and replicate to remote client')
 Send-TestCommand 0 trap
 Await-Test {(Read-State 0).traps.Count -eq 1 -and (Read-State 1).traps.Count -eq 1} 'Host animation spawns one shared trap'
 if((Read-State 0).traps[0] -ne (Read-State 1).traps[0]){throw 'Host trap identity or position differs'}
 Send-TestCommand 1 trap
 Await-Test {(Read-State 0).traps.Count -eq 2 -and (Read-State 1).traps.Count -eq 2} 'Client animation spawns one shared trap'
 if(((Read-State 0).traps -join '|') -ne ((Read-State 1).traps -join '|')){throw 'Client trap identity or position differs'}
 $checks.Add('Host and client see identical trap IDs and positions with no duplicates')
 Send-TestCommand 0 trap 0 banana
 Await-Test {(Read-State 0).traps.Count -eq 3 -and (Read-State 1).traps.Count -eq 3} 'Host banana animation spawns one shared trap'
 Send-TestCommand 1 trap 0 banana
 Await-Test {(Read-State 0).traps.Count -eq 4 -and (Read-State 1).traps.Count -eq 4} 'Client banana animation spawns one shared trap'
 if(((Read-State 0).traps -join '|') -ne ((Read-State 1).traps -join '|')){throw 'Banana identity, type or position differs'}
 if(@((Read-State 0).traps | Where-Object {$_ -match 'NetworkBananaTrap'}).Count -ne 2){throw 'Expected exactly two bananas'}
 $checks.Add('Both peers have two LEGO traps and two banana traps')
 Send-TestCommand 0 leave
 Await-Test {(Read-State 0).scene -eq 'MainMenu' -and (Read-State 1).scene -eq 'MainMenu'} 'Host exit cleans match'
 foreach($i in 0..1){if((Read-State $i).errors -gt 0){throw "Runtime errors $i"}}
 $checks.Add('No runtime errors')
 @{result='PASS';internet=[bool]$Internet;checks=$checks} | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $testRoot 'result.json')
} catch { @{result='FAIL';error=$_.ToString();checks=$checks} | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $testRoot 'result.json'); throw }
finally {foreach($p in $processes){if(!$p.HasExited){Stop-Process -Id $p.Id -ErrorAction SilentlyContinue}}}


