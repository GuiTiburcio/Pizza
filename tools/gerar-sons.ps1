# Gera os sons do Pizza por síntese (FR-43): são originais do projeto, sem amostras de terceiros.
$ErrorActionPreference = 'Stop'
$assets = Join-Path (Split-Path -Parent $PSScriptRoot) 'src\Pizza.App\Assets'
$rate = 44100

function New-Tone([double]$start, [double]$frequency, [double]$decay, [double]$level, [double[]]$buffer) {
    $first = [int]($start * $rate)
    for ($i = $first; $i -lt $buffer.Length; $i++) {
        $t = ($i - $first) / $rate
        $attack = [Math]::Min(1.0, $t / 0.006)
        $body = [Math]::Sin(2 * [Math]::PI * $frequency * $t) + (0.25 * [Math]::Sin(4 * [Math]::PI * $frequency * $t))
        $buffer[$i] += $level * $attack * [Math]::Exp(-$t / $decay) * $body
    }
}

function Save-Wav([string]$name, [double[]]$buffer) {
    # Termina em silêncio, para não estalar no fim.
    $fade = [int](0.02 * $rate)
    for ($i = 0; $i -lt $fade; $i++) { $buffer[$buffer.Length - 1 - $i] *= $i / $fade }

    $stream = [IO.MemoryStream]::new()
    $writer = [IO.BinaryWriter]::new($stream)
    $ascii = [Text.Encoding]::ASCII
    $writer.Write($ascii.GetBytes('RIFF')); $writer.Write([int](36 + ($buffer.Length * 2))); $writer.Write($ascii.GetBytes('WAVE'))
    $writer.Write($ascii.GetBytes('fmt ')); $writer.Write([int]16); $writer.Write([int16]1); $writer.Write([int16]1)
    $writer.Write([int]$rate); $writer.Write([int]($rate * 2)); $writer.Write([int16]2); $writer.Write([int16]16)
    $writer.Write($ascii.GetBytes('data')); $writer.Write([int]($buffer.Length * 2))
    foreach ($sample in $buffer) {
        $writer.Write([int16][Math]::Round([Math]::Max(-1.0, [Math]::Min(1.0, $sample)) * 32767))
    }

    $writer.Flush()
    [IO.File]::WriteAllBytes((Join-Path $assets $name), $stream.ToArray())
}

# Concluído: dois toques curtos subindo (mi e lá).
$done = [double[]]::new([int](0.36 * $rate))
New-Tone 0.00 659.25 0.055 0.22 $done
New-Tone 0.10 880.00 0.075 0.24 $done
Save-Wav 'concluido.wav' $done

# Erro: um toque grave e curto.
$fail = [double[]]::new([int](0.30 * $rate))
New-Tone 0.00 196.00 0.085 0.34 $fail
Save-Wav 'erro.wav' $fail

Write-Host "Sons gerados em $assets"
