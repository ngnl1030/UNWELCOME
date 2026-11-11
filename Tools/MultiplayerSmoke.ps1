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
    foreach ($i in 0..4) {
        $dir=Join-Path $testRoot "$i"
        New-Item -ItemType Directory -Path $dir -Force | Out-Null
        $arguments=@('-batchmode','-nographics','-mtTestPath',('"'+$dir+'"'),'-logFile',('"'+(Join-Path $dir 'player.log')+'"'))
        $processes += Start-Process -FilePath $exe -ArgumentList $arguments -WindowStyle Hidden -PassThru
    }
    Await-Test { @((0..4 | Where-Object { (Read-State $_).scene -eq 'MainMenu' })).Count -eq 5 } 'Five separate processes booted'
    Send-TestCommand 0 'host'
    Await-Test { (Read-State 0).host -and (Read-State 0).code } 'Host created a room'
    $code = if($Internet) { (Read-State 0).code } else { '127.0.0.1' }
    foreach($i in 1..3) { Send-TestCommand $i 'join' 0 $code }
    Await-Test { @((0..3 | Where-Object { (Read-State $_).players.Count -eq 4 -and (Read-State $_).scene -eq 'CharacterSelectLobby' })).Count -eq 4 } 'Four clients share lobby'
    Send-TestCommand 4 'join' 0 $code
    Await-Test { (Read-State 4).status -match '가득|full|Full|maximum' } 'Fifth player rejected'
    Send-TestCommand 1 'ready'
    Start-Sleep -Milliseconds 600
    if (@((Read-State 0).players | Where-Object ready).Count -ne 0) { throw 'Ready without slot accepted' }
    $checks.Add('Ready without slot rejected')
    Send-TestCommand 0 'select' 1
    Await-Test { @((Read-State 0).players | Where-Object { $_.id -eq 0 -and $_.slot -eq 1 }).Count -eq 1 } 'Host selects a slot'
    Send-TestCommand 0 'ready'
    Await-Test { @((Read-State 0).players | Where-Object { $_.id -eq 0 -and $_.ready }).Count -eq 1 } 'Host readies'
    Send-TestCommand 0 'select' 2
    Await-Test { @((Read-State 0).players | Where-Object { $_.id -eq 0 -and $_.slot -eq 2 -and !$_.ready }).Count -eq 1 } 'Changing slot clears readiness'
    Send-TestCommand 0 'select' 99
    Start-Sleep -Milliseconds 600
    if(@((Read-State 0).players | Where-Object slot -eq 99).Count -gt 0) { throw 'Invalid slot accepted' }
    $checks.Add('Invalid slot rejected')
    foreach($i in 0..3) { Send-TestCommand $i 'select' 0 }
    Await-Test { @((Read-State 0).players | Where-Object slot -eq 0).Count -eq 1 } 'Concurrent selection has one winner'
    Start-Sleep -Milliseconds 600
    # Resolve test-player slot swaps through a free slot; the game correctly rejects occupied destinations.
    foreach($i in 0..3) {
        $id=(Read-State $i).localId
        $players=(Read-State 0).players
        $occupant=$players | Where-Object { $_.slot -eq $i -and $_.id -ne $id }
        if($occupant) {
            $free=0..3 | Where-Object { $_ -notin $players.slot } | Select-Object -First 1
            $process=0..3 | Where-Object { (Read-State $_).localId -eq $occupant.id } | Select-Object -First 1
            Send-TestCommand $process 'select' $free
            Await-Test { @((Read-State 0).players | Where-Object { $_.id -eq $occupant.id -and $_.slot -eq $free }).Count -eq 1 } 'Test player moves to free slot'
        }
        Send-TestCommand $i 'select' $i
        Await-Test { @((Read-State 0).players | Where-Object { $_.id -eq $id -and $_.slot -eq $i }).Count -eq 1 } "Player $i selects its test slot"
    }
    Await-Test { @((Read-State 0).players | Where-Object slot -ge 0).Count -eq 4 } 'Each player has a distinct slot'
    Send-TestCommand 3 'leave'
    Await-Test { (Read-State 0).players.Count -eq 3 -and (Read-State 3).scene -eq 'MainMenu' } 'Lobby departure frees occupied slot'
    Start-Sleep -Seconds 2
    Send-TestCommand 3 'join' 0 $code
    Await-Test { (Read-State 3).players.Count -eq 4 } 'Client can rejoin'
    Send-TestCommand 3 'select' 3
    Await-Test { @((Read-State 0).players | Where-Object slot -eq 3).Count -eq 1 } 'Freed slot can be selected'
    Send-TestCommand 0 'start'
    Start-Sleep -Milliseconds 700
    if((Read-State 0).scene -ne 'CharacterSelectLobby') { throw 'Started without all ready' }
    $checks.Add('Start without readiness rejected')
    foreach($i in 0..3) { Send-TestCommand $i 'ready' }
    Await-Test { @((0..3 | Where-Object { (Read-State $_).canStart })).Count -eq 4 } 'Ready state synchronized on all clients'
    Send-TestCommand 1 'start'
    Start-Sleep -Milliseconds 700
    if((Read-State 0).scene -ne 'CharacterSelectLobby') { throw 'Client started game' }
    $checks.Add('Non-host start rejected')
    Send-TestCommand 0 'start'
    Await-Test { @((0..3 | Where-Object { (Read-State $_).scene -eq 'GameScene' -and (Read-State $_).avatars.Count -eq 4 })).Count -eq 4 } 'All four entered game and spawned four avatars'
    foreach($i in 0..3) {
        $state=Read-State $i
        if(@($state.avatars | Where-Object input).Count -ne 1 -or @($state.avatars | Where-Object movement).Count -ne 1 -or @($state.avatars | Where-Object { $_.input -and !$_.owner }).Count -gt 0) { throw "Ownership incorrect on $i" }
        if($state.listeners -ne 1 -or $state.cameras -ne 1) { throw "Camera/listener count incorrect on $i" }
    }
    $checks.Add('Only owner input, movement, camera and audio active')
    $before=@{}
    # Move teams sideways so opposite spawn rows do not run into one another.
    foreach($i in 0..3) { $before[$i]=((Read-State $i).avatars | Where-Object owner).position; Send-TestCommand $i 'move' 2 }
    Start-Sleep -Seconds 2
    foreach($i in 0..3) { Send-TestCommand $i 'move' 0 }
    Start-Sleep -Seconds 1
    foreach($i in 0..3) {
        $own=(Read-State $i).avatars | Where-Object owner
        $distance=[Math]::Abs($own.position.z - $before[$i].z)+[Math]::Abs($own.position.x - $before[$i].x)
        if($distance -lt .5) { throw "Player $i did not move" }
        foreach($j in 0..3) {
            $remote=(Read-State $j).avatars | Where-Object id -eq $own.id
            if([Math]::Abs($remote.position.z-$own.position.z)+[Math]::Abs($remote.position.x-$own.position.x) -gt .3) { throw "Position mismatch $i -> $j" }
        }
    }
    $checks.Add('Both controller types move and replicate position')
    $thiefBefore=((Read-State 2).avatars | Where-Object owner).yaw
    Send-TestCommand 2 'look' 1
    Start-Sleep -Seconds 1
    Send-TestCommand 2 'look' 0
    Start-Sleep -Seconds 1
    $thief=(Read-State 2).avatars | Where-Object owner
    if([Math]::Abs($thief.yaw-$thiefBefore) -lt .5) { throw 'Thief camera/body did not rotate' }
    foreach($i in 0..3) {
        $remote=(Read-State $i).avatars | Where-Object id -eq $thief.id
        if([Math]::Abs($remote.yaw-$thief.yaw) -gt 2) { throw 'Rotation did not replicate' }
    }
    $checks.Add('Rotation replicates to every client')
    Send-TestCommand 3 'leave'
    Await-Test { (Read-State 0).avatars.Count -eq 3 -and (Read-State 0).players.Count -eq 3 -and (Read-State 3).scene -eq 'MainMenu' } 'Client exit removes player and returns to menu'
    Send-TestCommand 0 'leave'
    Await-Test { @((0..2 | Where-Object { (Read-State $_).scene -eq 'MainMenu' -and !(Read-State $_).connected })).Count -eq 3 } 'Host exit returns remaining clients to menu'
    foreach($i in 0..3) { if((Read-State $i).errors -gt 0) { throw "Runtime errors on process $i; inspect player.log" } }
    $checks.Add('No runtime errors')
    @{result='PASS';internet=[bool]$Internet;checks=$checks} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $testRoot 'result.json') -Encoding UTF8
    Write-Output 'ALL TESTS PASSED'
} catch {
    @{result='FAIL';error=$_.ToString();checks=$checks} | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $testRoot 'result.json') -Encoding UTF8
    throw
} finally {
    foreach($p in $processes) { if(!$p.HasExited) { Stop-Process -Id $p.Id -ErrorAction SilentlyContinue } }
}
