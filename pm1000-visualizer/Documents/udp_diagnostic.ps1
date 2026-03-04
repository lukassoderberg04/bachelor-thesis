# UDP Diagnostic Script for PM1000 Streamer
# Listens on ports 5000-5002 for 5 seconds and reports what's received.
# Run while the streamer is active:  powershell -File udp_diagnostic.ps1

param(
    [int]$StokesPort = 5000,
    [int]$RawAudioPort = 5001,
    [int]$ProcessedPort = 5002,
    [int]$DurationSec = 5
)

Write-Host "=== PM1000 UDP Diagnostic ===" -ForegroundColor Cyan
Write-Host "Listening on ports $StokesPort (Stokes), $RawAudioPort (Raw Audio), $ProcessedPort (Processed)"
Write-Host "Duration: $DurationSec seconds"
Write-Host ""

# Shared counters (thread-safe via [ref] to volatiles)
$stokesCount = [System.Collections.Concurrent.ConcurrentBag[int]]::new()
$rawCount = [System.Collections.Concurrent.ConcurrentBag[int]]::new()
$procCount = [System.Collections.Concurrent.ConcurrentBag[int]]::new()

# Sample storage for inspection
$stokesSamples = [System.Collections.Concurrent.ConcurrentBag[string]]::new()
$rawSamples = [System.Collections.Concurrent.ConcurrentBag[string]]::new()

$cts = [System.Threading.CancellationTokenSource]::new()

function Start-Listener($port, $counter, $sampleBag, $label, $parseFunc) {
    $job = [scriptblock]::Create(@"
        param(`$port, `$counter, `$sampleBag, `$label, `$cts)
        try {
            `$client = [System.Net.Sockets.UdpClient]::new(`$port)
            `$client.Client.ReceiveTimeout = 1000
            `$ep = [System.Net.IPEndPoint]::new([System.Net.IPAddress]::Any, 0)
            `$uniqueTimestamps = @{}
            
            while (-not `$cts.Token.IsCancellationRequested) {
                try {
                    `$data = `$client.Receive([ref]`$ep)
                    `$counter.Add(1)
                    
                    # Parse first few for display
                    if (`$counter.Count -le 5) {
                        `$hex = [BitConverter]::ToString(`$data[0..([Math]::Min(23, `$data.Length-1))])
                        `$sampleBag.Add("`$label [`$(`$data.Length) bytes]: `$hex")
                    }
                    
                    # Track unique timestamps
                    if (`$data.Length -ge 8) {
                        `$tsOffset = if (`$data.Length -eq 24) { 20 } else { 4 }
                        if (`$tsOffset + 4 -le `$data.Length) {
                            `$ts = [BitConverter]::ToUInt32(`$data, `$tsOffset)
                            `$uniqueTimestamps[`$ts] = `$true
                        }
                    }
                } catch [System.Net.Sockets.SocketException] {
                    # timeout, just continue
                }
            }
            
            `$client.Close()
            
            # Return unique count via sampleBag
            `$sampleBag.Add("UNIQUE_TIMESTAMPS:`$(`$uniqueTimestamps.Count)")
        } catch {
            `$sampleBag.Add("ERROR: `$_")
        }
"@)
    return Start-Job -ScriptBlock $job -ArgumentList $port, $counter, $sampleBag, $label, $cts
}

$jobs = @()
$jobs += Start-Listener $StokesPort $stokesCount $stokesSamples "Stokes"
$jobs += Start-Listener $RawAudioPort $rawCount $rawSamples "RawAudio"

Write-Host "Listening..." -ForegroundColor Yellow
Start-Sleep -Seconds $DurationSec

$cts.Cancel()
Write-Host ""
Write-Host "Stopping listeners..." -ForegroundColor Yellow
$jobs | ForEach-Object { $_ | Wait-Job -Timeout 3 | Out-Null }

Write-Host ""
Write-Host "=== RESULTS ===" -ForegroundColor Cyan
Write-Host "Stokes  (port $StokesPort):  $($stokesCount.Count) packets received" -ForegroundColor Green
Write-Host "Raw Audio (port $RawAudioPort): $($rawCount.Count) packets received" -ForegroundColor Green

Write-Host ""
Write-Host "=== SAMPLE PACKETS ===" -ForegroundColor Cyan
foreach ($s in $stokesSamples) {
    if ($s -notlike "UNIQUE*" -and $s -notlike "ERROR*") { Write-Host $s }
}
foreach ($s in $rawSamples) {
    if ($s -notlike "UNIQUE*" -and $s -notlike "ERROR*") { Write-Host $s }
}

Write-Host ""
Write-Host "=== UNIQUE TIMESTAMPS ===" -ForegroundColor Cyan
$stokesUnique = ($stokesSamples | Where-Object { $_ -like "UNIQUE*" }) -replace "UNIQUE_TIMESTAMPS:", ""
$rawUnique = ($rawSamples | Where-Object { $_ -like "UNIQUE*" }) -replace "UNIQUE_TIMESTAMPS:", ""
if ($stokesUnique) { Write-Host "Stokes unique timestamps: $stokesUnique (vs $($stokesCount.Count) total)" }
if ($rawUnique) { Write-Host "Raw Audio unique timestamps: $rawUnique (vs $($rawCount.Count) total)" }

Write-Host ""
Write-Host "=== EXPECTED RATES ===" -ForegroundColor Cyan
$stokesRate = [Math]::Round($stokesCount.Count / $DurationSec)
$rawRate = [Math]::Round($rawCount.Count / $DurationSec)
Write-Host "Stokes:    ~$stokesRate packets/sec (expected: thousands due to spin loop)"
Write-Host "Raw Audio: ~$rawRate packets/sec   (expected: thousands due to spin loop)"
Write-Host ""
Write-Host "If unique timestamps << total packets, the streamer is sending duplicates." -ForegroundColor Yellow
Write-Host "The visualizer now down-samples audio to 8000 Hz and deduplicates stokes." -ForegroundColor Yellow

$jobs | Remove-Job -Force
