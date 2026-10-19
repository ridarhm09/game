$ErrorActionPreference = 'Stop'
$root = 'C:\Donimal\server\Server_API'
$mainPath = Join-Path $root 'main.py'
$schemaPath = Join-Path $root 'schemas.py'
$main = [IO.File]::ReadAllText($mainPath)
$schema = [IO.File]::ReadAllText($schemaPath)
$old = '        subgame = db.query(models.GameSubgame).filter(models.GameSubgame.subgame_code == target_code).first()'
$new = @'
        # Prefer current database codes, then support legacy installations.
        canonical_mode = {"menyusun_angka": "susun_angka"}.get(mode, mode)
        subgame = db.query(models.GameSubgame).filter(models.GameSubgame.subgame_code == canonical_mode).first()
        if subgame is None:
            subgame = db.query(models.GameSubgame).filter(models.GameSubgame.subgame_code == target_code).first()
'@
if (-not $main.Contains($old)) { throw 'Expected mode lookup was not found; no files changed.' }
$updatedMain = $main.Replace($old, $new)
$pattern = '(?s)(class GameQuestionResponse\(BaseModel\):.*?\r?\n    )created_at: datetime'
if (-not [regex]::IsMatch($schema, $pattern)) { throw 'Expected question schema was not found; no files changed.' }
$updatedSchema = [regex]::Replace($schema, $pattern, '${1}created_at: Optional[datetime] = None', 1)
$stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
Copy-Item -LiteralPath $mainPath -Destination ($mainPath + '.backup-' + $stamp)
Copy-Item -LiteralPath $schemaPath -Destination ($schemaPath + '.backup-' + $stamp)
[IO.File]::WriteAllText($mainPath, $updatedMain, [Text.UTF8Encoding]::new($false))
[IO.File]::WriteAllText($schemaPath, $updatedSchema, [Text.UTF8Encoding]::new($false))
Write-Output 'Updated question mode lookup and nullable question created_at. Backups saved beside both files.'
