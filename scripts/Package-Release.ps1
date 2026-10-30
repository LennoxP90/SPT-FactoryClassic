# Package dist/<Configuration> into a release .7z a player extracts over their SPT install root.
# The archive IS dist/Release, which already has BepInEx\ and SPT_Runtime\ at the top.
param(
    [string]$Configuration = "Release",
    [string]$OutDir = "P:\",
    # The Halloween bundle after build_external_bundle.py; it is not in git (docs/RESUME.md has the rebuild).
    [string]$HalloweenBundle = "F:\Code\SPT docker server\sdk\staging\factory-event-bundles\external\factoryclassic_halloween.bundle",
    [switch]$SkipGates
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$dist = Join-Path $repoRoot "dist\$Configuration"
if (-not (Test-Path $dist)) { throw "nothing staged at $dist; build first" }

$plugin = Join-Path $dist "BepInEx\plugins\LennoxP90-FactoryClassic"
$serverMod = Join-Path $dist "SPT_Runtime\user\mods\LennoxP90-FactoryClassic"

# The bundle and the stamp the client checks it against (HalloweenBundle.cs reads BundleBytes).
$halloween = Join-Path $plugin "plugin-data\halloween"
if (-not (Test-Path $HalloweenBundle)) { throw "no Halloween bundle at $HalloweenBundle; rebuild it per docs/RESUME.md" }
# Only the external-assets step writes this file; a bundle without it carries every borrowed texture, mesh and clip.
if (-not (Test-Path ([IO.Path]::ChangeExtension($HalloweenBundle, ".external.json")))) {
    throw "$HalloweenBundle has no .external.json beside it, so it is the full build rather than the external one"
}
New-Item -ItemType Directory -Force $halloween | Out-Null
$bundleItem = Copy-Item $HalloweenBundle (Join-Path $halloween "factoryclassic_halloween.bundle") -Force -PassThru
[ordered]@{
    RipBuild    = "1.3.0.0.48484"
    BuiltUtc    = (Get-Item $HalloweenBundle).LastWriteTimeUtc.ToString("o")
    BundleBytes = $bundleItem.Length
    Scenes      = @("FactoryClassic_Halloween", "FactoryClassic_Halloween_Light", "FactoryClassic_Halloween_Sound")
} | ConvertTo-Json | Set-Content (Join-Path $halloween "halloween-stamp.json") -Encoding utf8
Write-Host ("staged the Halloween bundle: {0:N0} bytes" -f $bundleItem.Length)

if (-not $SkipGates) {
    foreach ($required in @(
        (Join-Path $plugin "FactoryClassic.Client.dll"),
        (Join-Path $plugin "plugin-data\factory_classic.audiobakedata"),
        (Join-Path $plugin "plugin-data\factory_classic_empty.audiobakedata"),
        (Join-Path $halloween "factoryclassic_halloween.bundle"),
        (Join-Path $halloween "halloween-stamp.json"),
        (Join-Path $halloween "factory_event_sound.audiobakedata"),
        (Join-Path $halloween "corpses.json"),
        (Join-Path $serverMod "FactoryClassic.Server.dll"),
        (Join-Path $serverMod "FactoryClassic.Server.Web.dll"),
        (Join-Path $serverMod "config\config.json"),
        (Join-Path $serverMod "db\quest-gate.json"),
        (Join-Path $serverMod "db\halloween\corpses.json")
    )) { if (-not (Test-Path $required)) { throw "missing from the payload: $required" } }

    # The external build is about 81 MB and the full one about 360 MB, so well over 150 means nothing was borrowed.
    if ($bundleItem.Length -gt 150MB) { throw ("the Halloween bundle is {0:N0} bytes; the external build is about 81 MB" -f $bundleItem.Length) }
    # Client and server must agree on which corpses exist, or loot lands in containers the client never names.
    if ((Get-FileHash (Join-Path $halloween "corpses.json")).Hash -ne (Get-FileHash (Join-Path $serverMod "db\halloween\corpses.json")).Hash) {
        throw "the client's and the server's corpses.json differ"
    }
    Write-Host "gate ok: Halloween bundle, stamp, bake and matching corpse lists"

    # A missing table here is classic geometry served the reworked tile's loot or exits.
    foreach ($tile in @("factory4_day", "factory4_night")) {
        foreach ($table in @("base.classic", "allExtracts", "looseLoot", "staticAmmo",
                             "staticContainers", "staticLoot", "statics")) {
            $path = Join-Path $serverMod "db\classic\$tile\$table.json"
            if (-not (Test-Path $path)) { throw "missing from the payload: $path" }
        }
    }
    Write-Host "gate ok: both tiles carry all seven data tables"

    # The last eight bytes are the per-pair maxima SpatialAudioRepair copies onto the location info
    # to size the propagation job. Zero there means empty buffers and a native crash in raid.
    $routing = Join-Path $plugin "plugin-data\factory_classic.audiobakedata"
    $tail = [System.IO.File]::ReadAllBytes($routing)[-8..-1]
    $maxRoutes = [BitConverter]::ToUInt32($tail, 0)
    $maxPortals = [BitConverter]::ToUInt32($tail, 4)
    if ($maxRoutes -eq 0 -or $maxPortals -eq 0) {
        throw "the routing table records maxRoutesPerPair=$maxRoutes maxPortalsPerPair=$maxPortals. " +
              "Zero means the propagation job gets empty buffers and the raid crashes natively. " +
              "Re-run analysis/convert_audiobake.py."
    }
    Write-Host "gate ok: routing table declares maxRoutesPerPair=$maxRoutes maxPortalsPerPair=$maxPortals"

    # The fallback holds no room pairs, so it must declare no capacities.
    $emptyTail = [System.IO.File]::ReadAllBytes((Join-Path $plugin "plugin-data\factory_classic_empty.audiobakedata"))[-8..-1]
    if ([BitConverter]::ToUInt32($emptyTail, 0) -ne 0 -or [BitConverter]::ToUInt32($emptyTail, 4) -ne 0) {
        throw "the empty routing table declares non-zero capacities; it has no room pairs and must declare none"
    }
    Write-Host "gate ok: empty table declares no capacities"
}

$version = ([xml](Get-Content (Join-Path $repoRoot "Directory.Build.props"))).Project.PropertyGroup.ModVersion | Where-Object { $_ }
if (-not $version) { $version = (Get-Item (Join-Path $plugin "FactoryClassic.Client.dll")).VersionInfo.FileVersion }
$name = "SPT-FactoryClassic-$version-SPT4.1.7z"
$archive = Join-Path $OutDir $name

if (Test-Path $archive) { Remove-Item $archive -Force }
$sevenZip = @("C:\Program Files\7-Zip\7z.exe", "C:\Programming\7-Zip\7z.exe", "7z") |
    Where-Object { (Get-Command $_ -ErrorAction SilentlyContinue) -or (Test-Path $_) } | Select-Object -First 1
if (-not $sevenZip) { throw "7z.exe not found; install 7-Zip or add it to PATH" }

Write-Host "packing $dist -> $archive"
Push-Location $dist
try {
    & $sevenZip a -t7z -mx9 -bso0 -bsp0 $archive "*" | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "7z failed ($LASTEXITCODE)" }
}
finally { Pop-Location }

$item = Get-Item $archive
"{0}  {1:N0} bytes  sha256 {2}" -f $item.Name, $item.Length, (Get-FileHash $item.FullName -Algorithm SHA256).Hash
