# Regenerates data\adhkar.tsv from the Hisn al-Muslim API so the text is never retyped by hand.
# Columns: sets (M morning, E evening, A after prayer, S sleep), source item id, repeat count, text.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

function Get-Items([int]$chapter) {
    $bytes = (Invoke-WebRequest "https://hisnmuslim.com/api/ar/$chapter.json" -UseBasicParsing -TimeoutSec 30).RawContentStream.ToArray()
    $json = [Text.Encoding]::UTF8.GetString($bytes).TrimStart([char]0xFEFF) | ConvertFrom-Json
    @(($json.PSObject.Properties | Select-Object -First 1).Value)
}

function Clean([string]$text) {
    # Only typography changes: drop tatweel (U+0640) and collapse whitespace. Letters and marks stay untouched.
    (($text -replace [string][char]0x0640, '') -replace '\s+', ' ').Trim()
}

# The source's repeat field disagrees with its own text here: item 83 says seven times but has REPEAT 1.
$repeatFromText = @{ 83 = 7 }
$morningOnly = 93, 94, 95   # the text says "when morning comes"
$eveningOnly = @(97)        # the text says "when evening comes"

$lines = New-Object System.Collections.Generic.List[string]
foreach ($it in Get-Items 27) {
    $id = [int]$it.ID
    $sets = if ($morningOnly -contains $id) { 'M' } elseif ($eveningOnly -contains $id) { 'E' } else { 'ME' }
    $rep = if ($repeatFromText.ContainsKey($id)) { $repeatFromText[$id] } else { [int]$it.REPEAT }
    $lines.Add("$sets`t$id`t$rep`t$(Clean $it.ARABIC_TEXT)")
}
foreach ($it in Get-Items 25) { $lines.Add("A`t$([int]$it.ID)`t$([int]$it.REPEAT)`t$(Clean $it.ARABIC_TEXT)") }
foreach ($it in Get-Items 28) { $lines.Add("S`t$([int]$it.ID)`t$([int]$it.REPEAT)`t$(Clean $it.ARABIC_TEXT)") }

New-Item -ItemType Directory -Force (Join-Path $root 'data') | Out-Null
[IO.File]::WriteAllLines((Join-Path $root 'data\adhkar.tsv'), $lines, (New-Object Text.UTF8Encoding($false)))
"Wrote $($lines.Count) items"
