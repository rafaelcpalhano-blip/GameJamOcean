$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition (Get-Content -LiteralPath (Join-Path $PSScriptRoot '../Assets/Scripts/DiveDifficultyRules.cs') -Raw)
$cases = @(
    @{Total=27; Weights=[float[]](40,30,20,5,5); Expected='11,8,6,1,1'},
    @{Total=30; Weights=[float[]](20,30,30,10,10); Expected='6,9,9,3,3'},
    @{Total=33; Weights=[float[]](10,35,25,15,15); Expected='3,12,8,5,5'},
    @{Total=37; Weights=[float[]](10,25,25,25,25); Expected='3,9,9,8,8'},
    @{Total=42; Weights=[float[]](15,15,10,30,30); Expected='6,6,4,13,13'}
)
foreach ($case in $cases) {
    $actual = [GameJamOcean.Diving.DiveDifficultyRules]::Allocate($case.Total, $case.Weights)
    if (($actual -join ',') -ne $case.Expected) { throw "Wrong quota: $actual" }
    if (($actual | Measure-Object -Sum).Sum -ne $case.Total) { throw 'Budget exceeded' }
}
for ($points = 0; $points -le 13; $points++) {
    $actual = [GameJamOcean.Diving.DiveDifficultyRules]::SelectTier($points, [int[]](0,3,6,9,12))
    $expected = [Math]::Min(4, [Math]::Floor($points / 3))
    if ($actual -ne $expected) { throw "Wrong difficulty at $points purchases" }
}
foreach ($weights in @([float[]](0,0,0,0,0), [float[]](-1,1), [float[]]([float]::NaN,1))) {
    $rejected = $false
    try { [void][GameJamOcean.Diving.DiveDifficultyRules]::Allocate(27, $weights) } catch { $rejected = $true }
    if (!$rejected) { throw 'Invalid weights accepted' }
}
Write-Output 'PASS: 5 quota distributions, 14 progression boundaries and 3 invalid weight cases.'
