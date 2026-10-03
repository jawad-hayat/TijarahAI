[CmdletBinding()]
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$AngularArguments
)

$angularCli = Join-Path $PSScriptRoot 'node_modules\@angular\cli\bin\ng.js'

if (-not (Test-Path -LiteralPath $angularCli)) {
    throw "Angular CLI was not found. Restore dependencies first, then run this script again."
}

# This intentionally invokes Node directly instead of the generated `ng.cmd` shim.
# The repository path contains `&`, which CMD interprets as a command separator.
& node $angularCli serve --configuration development @AngularArguments
