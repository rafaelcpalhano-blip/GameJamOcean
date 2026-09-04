$ErrorActionPreference = 'Stop'
$rulesPath = Join-Path $PSScriptRoot '../Assets/Scripts/UpgradePurchaseRules.cs'
Add-Type -TypeDefinition (Get-Content -LiteralPath $rulesPath -Raw)

function Test-Purchase([int]$gold, [int]$level, [int]$expected, [int]$maximum, [int]$cost, [bool]$success, [int]$resultGold, [int]$resultLevel) {
    $message = ''
    $result = [GameJamOcean.Progression.UpgradePurchaseRules]::TryApply([ref]$gold, [ref]$level, $expected, $maximum, $cost, [ref]$message)
    if ($result -ne $success -or $gold -ne $resultGold -or $level -ne $resultLevel) {
        throw "Transaction failed assertion: result=$result gold=$gold level=$level message=$message"
    }
}

Test-Purchase 100 1 1 3 60 $true 40 2
Test-Purchase 60 1 1 3 60 $true 0 2
Test-Purchase 59 1 1 3 60 $false 59 1
Test-Purchase 100 2 1 3 60 $false 100 2
Test-Purchase 500 3 3 3 60 $false 500 3
Test-Purchase 900 3 3 4 900 $true 0 4
Test-Purchase 1000 4 4 4 900 $false 1000 4
Test-Purchase 0 1 1 3 0 $true 0 2
Test-Purchase 100 1 1 3 -1 $false 100 1
Test-Purchase 0 1 1 3 50 $false 0 1
Write-Output 'PASS: 10 transaction tests (balance, exact payment, stale offer, caps, village N4, zero/invalid costs).'
