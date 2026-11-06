# Runs the tester against every local sample page (original + mutated copies).
$root = $PSScriptRoot
$fail = 0
Get-ChildItem "$root/samples/*.html" | ForEach-Object {
  Write-Host "=================== $($_.Name)"
  $uri = ([System.Uri]$_.FullName).AbsoluteUri
  dotnet run --project "$root/src" -c Release -- --url $uri --headless @args
  if ($LASTEXITCODE -ne 0) { $fail = 1 }
}
exit $fail
