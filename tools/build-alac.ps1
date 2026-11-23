# ADR-041: compila fonte Apple fixada; nenhum download ou compilador é chamado pelo aplicativo.
param([Parameter(Mandatory)][string]$Output)
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$commit = 'c38887c5c5e64a4b31108733bd79ca9b2496d987'
$hash = 'd555102b7d57b05f26b626bf4f92c1681928d883d621b41d80de63c6c3d3097c'
$root = Split-Path -Parent $PSScriptRoot
$cache = Join-Path $root 'artifacts\deps\alac-apple'
$archive = Join-Path $cache 'source.zip'
New-Item -ItemType Directory -Force -Path $cache, $Output | Out-Null
# Serializa download e compilação entre builds paralelos (dotnet test em vários projetos, CI).
$mutex = [System.Threading.Mutex]::new($false, 'Global\Pizza.Alac.Build')
$locked = $false
try {
    try { $locked = $mutex.WaitOne([TimeSpan]::FromMinutes(10)) } catch [System.Threading.AbandonedMutexException] { $locked = $true }
    if (-not $locked) { throw 'Outro build ALAC não terminou em 10 minutos.' }
    # Outro processo pode ter terminado enquanto esperávamos: pula se a DLL já corresponde à fonte fixada.
    $marker = Join-Path $Output 'ALAC-BUILD.json'
    $library = Join-Path $Output 'Pizza.Alac.dll'
    if ((Test-Path -LiteralPath $library) -and (Test-Path -LiteralPath $marker) -and
        (Test-Path -LiteralPath (Join-Path $Output 'ALAC-LICENSE.txt')) -and (Test-Path -LiteralPath (Join-Path $Output 'ALAC-NOTICE.txt'))) {
        $built = Get-Content -LiteralPath $marker -Raw | ConvertFrom-Json
        if ($built.sourceSha256 -eq $hash -and $built.sourceCommit -eq $commit -and
            $built.binarySha256 -eq (Get-FileHash -LiteralPath $library -Algorithm SHA256).Hash.ToLowerInvariant()) { return }
    }
function Get-Sha256([string]$Path) { (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }
# Cache com hash divergente (corrompido ou adulterado): apaga e baixa de novo uma única vez.
if ((Test-Path -LiteralPath $archive) -and (Get-Sha256 $archive) -ne $hash) { Remove-Item -LiteralPath $archive -Force }
if (-not (Test-Path -LiteralPath $archive)) {
    # Baixa para um temporário exclusivo e só move para o cache depois de conferir o SHA-256.
    $download = Join-Path $cache ('source-' + [Guid]::NewGuid().ToString('N') + '.tmp')
    try {
        Invoke-WebRequest "https://codeload.github.com/macosforge/alac/zip/$commit" -OutFile $download
        if ((Get-Sha256 $download) -ne $hash) { throw 'Fonte ALAC baixada com hash divergente do fixado; nada foi guardado no cache.' }
        Move-Item -LiteralPath $download -Destination $archive
    }
    finally {
        if (Test-Path -LiteralPath $download) { Remove-Item -LiteralPath $download -Force }
    }
}
$locator = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
$installation = & $locator -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
if ($LASTEXITCODE -ne 0 -or -not $installation) { throw 'Compilador Microsoft C++ x64 não encontrado.' }
$msbuild = Join-Path $installation 'MSBuild\Current\Bin\MSBuild.exe'
$targets = Get-ChildItem -LiteralPath (Join-Path $installation 'MSBuild\Microsoft\VC') -Directory |
    Where-Object Name -Match '^v\d+$' | Sort-Object { [int]$_.Name.Substring(1) } -Descending | Select-Object -First 1
$toolset = Get-ChildItem -LiteralPath (Join-Path $targets.FullName 'Platforms\x64\PlatformToolsets') -Directory |
    Where-Object Name -Match '^v14\d+$' | Sort-Object { [int]$_.Name.Substring(1) } -Descending | Select-Object -First 1
if (-not $toolset) { throw 'Toolset Microsoft C++ x64 não encontrado.' }
$project = Join-Path $root 'native\Pizza.Alac\Pizza.Alac.vcxproj'
$staging = [IO.Path]::GetFullPath((Join-Path $cache ('build-' + [Guid]::NewGuid().ToString('N'))))
$cacheRoot = [IO.Path]::GetFullPath($cache).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
if (-not $staging.StartsWith($cacheRoot, [StringComparison]::OrdinalIgnoreCase)) { throw 'Staging fora do cache autorizado.' }
try {
    # Não compilar diretórios extraídos ou objetos de execuções anteriores: só bytes do ZIP conferido.
    Expand-Archive -LiteralPath $archive -DestinationPath $staging
    $source = Join-Path $staging "alac-$commit"
    & $msbuild $project /p:Configuration=Release /p:Platform=x64 "/p:AlacToolset=$($toolset.Name)" "/p:AlacSource=$source" "/p:AlacIntermediate=$(Join-Path $staging 'obj')" "/p:AlacOutput=$([IO.Path]::GetFullPath($Output))" /v:quiet /nologo
    if ($LASTEXITCODE -ne 0) { throw "Build ALAC falhou ($LASTEXITCODE)." }
    Copy-Item -LiteralPath (Join-Path $source 'LICENSE') -Destination (Join-Path $Output 'ALAC-LICENSE.txt')
    Copy-Item -LiteralPath (Join-Path $root 'native\Pizza.Alac\NOTICE.txt') -Destination (Join-Path $Output 'ALAC-NOTICE.txt')
    # O timestamp antigo do upstream não deve provocar nova compilação em todo dotnet test.
    foreach ($notice in 'ALAC-LICENSE.txt', 'ALAC-NOTICE.txt') { (Get-Item -LiteralPath (Join-Path $Output $notice)).LastWriteTimeUtc = [DateTime]::UtcNow }
    [ordered]@{
        sourceCommit = $commit
        sourceSha256 = $hash
        binarySha256 = (Get-FileHash -LiteralPath (Join-Path $Output 'Pizza.Alac.dll') -Algorithm SHA256).Hash.ToLowerInvariant()
        msbuildVersion = (Get-Item -LiteralPath $msbuild).VersionInfo.ProductVersion
        platformToolset = $toolset.Name
        architecture = 'x64'
        note = 'Determinismo limitado à mesma toolchain; não promete bytes iguais entre compiladores/SDKs diferentes.'
    } | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $Output 'ALAC-BUILD.json') -Encoding utf8
}
finally {
    # Só o staging exclusivo, cujo caminho absoluto já foi conferido dentro deste cache.
    if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force }
}
}
finally {
    if ($locked) { $mutex.ReleaseMutex() }
    $mutex.Dispose()
}
