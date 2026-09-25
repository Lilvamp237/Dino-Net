# Records the game's spoken lines with the offline Windows text-to-speech voice.
#
#   1. In Unity run:  DinoNet > Write Voice-Over Manifest
#   2. Then run:      powershell -ExecutionPolicy Bypass -File tools/generate_voiceover.ps1
#   3. Back in Unity the clips appear in Assets/Resources/VO
#
# Lines that already have a clip are skipped, so it is safe to re-run after adding text.
param(
    [string]$Manifest = "Dino Net/Temp/voice_manifest.json",
    [string]$OutDir = "Dino Net/Assets/Resources/VO",
    [string]$Voice = "Microsoft Zira Desktop",
    [switch]$Force
)

Add-Type -AssemblyName System.Speech
$repo = Split-Path -Parent $PSScriptRoot
$manifestPath = Join-Path $repo $Manifest
$outPath = Join-Path $repo $OutDir

if (-not (Test-Path $manifestPath)) {
    Write-Error "Manifest not found: $manifestPath. Run 'DinoNet > Write Voice-Over Manifest' in Unity first."
    exit 1
}

New-Item -ItemType Directory -Force -Path $outPath | Out-Null
$lines = Get-Content -Raw -Encoding UTF8 $manifestPath | ConvertFrom-Json

$synth = New-Object System.Speech.Synthesis.SpeechSynthesizer
try { $synth.SelectVoice($Voice) } catch { Write-Warning "Voice '$Voice' not found - using the default voice." }
$format = New-Object System.Speech.AudioFormat.SpeechAudioFormatInfo(16000, [System.Speech.AudioFormat.AudioBitsPerSample]::Sixteen, [System.Speech.AudioFormat.AudioChannel]::Mono)

$made = 0
foreach ($line in $lines) {
    $file = Join-Path $outPath ($line.slug + ".wav")
    if ((Test-Path $file) -and -not $Force) { continue }

    $text = [System.Security.SecurityElement]::Escape($line.text)
    # Slightly slower and higher than default, so it sounds friendlier for young children.
    $ssml = "<speak version=`"1.0`" xmlns=`"http://www.w3.org/2001/10/synthesis`" xml:lang=`"en-US`"><prosody rate=`"-8%`" pitch=`"+14%`">$text</prosody></speak>"

    $synth.SetOutputToWaveFile($file, $format)
    $synth.SpeakSsml($ssml)
    $synth.SetOutputToNull()
    $made++
}

$synth.Dispose()
Write-Host "Recorded $made new line(s); $($lines.Count) total in manifest. Output: $outPath"
