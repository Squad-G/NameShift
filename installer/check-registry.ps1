<#
.SYNOPSIS
Checks the Explorer entry "Bulk Rename Here" that the NameShift installer writes. Read-only: it never changes the registry.

.DESCRIPTION
-State Installed: the three keys exist under -Root with exactly the values of NS-49 criterion 1.
-State Removed: the three keys are gone (empty parent keys may remain).
Prints PASS or FAIL per line; the exit code is 0 when every line passes, else 1.
Run it with: powershell -ExecutionPolicy Bypass -File installer\check-registry.ps1 -State Installed

.PARAMETER Root
Where the keys live. The default is the real one; another root is only for testing this script.
#>
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet('Installed', 'Removed')]
    [string]$State,

    [string]$InstallDir = (Join-Path $env:LOCALAPPDATA 'Programs\NameShift'),

    [string]$Root = 'HKCU:\Software\Classes'
)

$ErrorActionPreference = 'Stop'

$exe = '"' + $InstallDir.TrimEnd('\') + '\NameShift.exe"'
$keys = @(
    @{ Path = 'Directory\shell\NameShift'; Argument = '"%1"' },
    @{ Path = 'Directory\Background\shell\NameShift'; Argument = '"%V"' },
    @{ Path = '*\shell\NameShift'; Argument = '"%1"' }
)

$script:failed = 0

function Report([bool]$ok, [string]$what) {
    if ($ok) {
        Write-Output ('PASS ' + $what)
    }
    else {
        Write-Output ('FAIL ' + $what)
        $script:failed++
    }
}

# Opens a key by its literal path (the * key is a key name, never a wildcard); $null when it is absent.
function Open-Key([string]$path) {
    if (Test-Path -LiteralPath $path) {
        return Get-Item -LiteralPath $path
    }
    return $null
}

# A REG_SZ value equal to $expected, read without expanding anything.
function Test-StringValue($key, [string]$name, [string]$expected, [string]$what) {
    $names = @($key.GetValueNames())
    if ($names -notcontains $name) {
        Report $false ($what + ': missing (expected ' + $expected + ')')
        return
    }
    $kind = $key.GetValueKind($name)
    $value = $key.GetValue($name, $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)
    if ($kind -ne [Microsoft.Win32.RegistryValueKind]::String) {
        Report $false ($what + ': kind is ' + $kind + ', expected String (REG_SZ)')
    }
    elseif ($value -cne $expected) {
        Report $false ($what + ': is ' + $value + ', expected ' + $expected)
    }
    else {
        Report $true ($what + ' = ' + $value)
    }
}

# The key holds exactly the values named in $allowed ('' is the default value).
function Test-NoOtherValue($key, [string[]]$allowed, [string]$what) {
    $others = @($key.GetValueNames() | Where-Object { $allowed -notcontains $_ })
    if ($others.Count -eq 0) {
        Report $true ($what + ': no other value')
    }
    else {
        Report $false ($what + ': other values: ' + ($others -join ', '))
    }
}

Write-Output ('Checking ' + $State + ' under ' + $Root + ' (install folder ' + $InstallDir + ')')

foreach ($entry in $keys) {
    $path = $Root.TrimEnd('\') + '\' + $entry.Path
    $label = $entry.Path
    if ($State -eq 'Removed') {
        if (Test-Path -LiteralPath $path) {
            Report $false ($label + ': still present')
        }
        else {
            Report $true ($label + ': absent')
        }
        continue
    }

    $key = Open-Key $path
    if ($null -eq $key) {
        Report $false ($label + ': key missing')
        continue
    }
    Test-StringValue $key '' 'Bulk Rename Here' ($label + ' (default)')
    Test-StringValue $key 'Icon' $exe ($label + ' Icon')
    Test-NoOtherValue $key @('', 'Icon') $label

    $command = Open-Key ($path + '\command')
    if ($null -eq $command) {
        Report $false ($label + '\command: key missing')
        continue
    }
    Test-StringValue $command '' ($exe + ' ' + $entry.Argument) ($label + '\command (default)')
    Test-NoOtherValue $command @('') ($label + '\command')
}

if ($script:failed -eq 0) {
    Write-Output 'All checks passed.'
    exit 0
}
Write-Output ([string]$script:failed + ' check(s) failed.')
exit 1
