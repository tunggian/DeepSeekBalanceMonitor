Add-Type -AssemblyName System.Drawing
$img = [System.Drawing.Image]::FromFile("D:\Produce\ToDo\AICoding\Deepseek余额监控\AppIcon.png")
$bmp = New-Object System.Drawing.Bitmap($img)
Write-Host "=== Full pixel grid (every 8px) ==="
for ($y=0; $y -lt 256; $y+=16) {
    $line = ""
    for ($x=0; $x -lt 256; $x+=8) {
        $c = $bmp.GetPixel($x,$y)
        if ($c.A -gt 128) {
            $bright = [int]($c.R*0.3 + $c.G*0.59 + $c.B*0.11)
            if ($bright -gt 200) { $ch = "@" }
            elseif ($bright -gt 150) { $ch = "#" }
            elseif ($bright -gt 100) { $ch = "o" }
            elseif ($bright -gt 50) { $ch = "." }
            else { $ch = " " }
        } else { $ch = " " }
        $line += $ch
    }
    Write-Host $line
}
Write-Host ""
Write-Host "=== Key pixel colors ==="
$spots = @(
    @(16,16), @(128,16), @(240,16),
    @(16,128), @(128,128), @(240,128),
    @(16,240), @(128,240), @(240,240)
)
foreach ($s in $spots) {
    $c = $bmp.GetPixel($s[0], $s[1])
    Write-Host ("  (" + $s[0] + "," + $s[1] + "): R=" + $c.R + " G=" + $c.G + " B=" + $c.B + " A=" + $c.A)
}
$bmp.Dispose()
$img.Dispose()
