#!/usr/bin/env pwsh
$ErrorActionPreference = 'Stop'
Push-Location $PSScriptRoot
try {
	$command = if ($args.Count -eq 0) { 'test' } else { $args[0] }
	switch ($command) {
		'build' { dotnet build ./EditorConfigFix.slnx --nologo }
		'pack' { dotnet pack ./src/EditorConfigFix/EditorConfigFix.csproj --nologo --artifacts-path ./artifacts }
		'test' { dotnet test ./EditorConfigFix.slnx --nologo }
		default { throw "Unknown build command '$command'. Use build, test, or pack." }
	}
	if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}
finally {
	Pop-Location
}