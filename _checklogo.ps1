Add-Type -AssemblyName System.Drawing
$img = [System.Drawing.Image]::FromFile("gRCdP.png")
$bmp = New-Object System.Drawing.Bitmap($img)
$w = $bmp.Width
$h = $bmp.Height
Write-Host ("Size: " + $w + "x" + $h)

Write-Host "=== Vertical slice at x=256 ==="
$line = ""
$yy = 0
while ($yy -lt $h) {
    $c = $bmp.GetPixel(256, $yy)
    if ($c.A -gt 128) {
        $bri = [int]($c.R*0.3 + $c.G*0.59 + $c.B*0.11)
        if ($bri -gt 200) { $ch = "W" }
        elseif ($c.G -gt 100 -and $c.R -lt 120) { $ch = "c" }
        elseif ($bri -gt 50) { $ch = "b" }
        else { $ch = "." }
    } else { $ch = " " }
    $line += $ch
    $yy += 2
}
Write-Host $line

Write-Host "=== Horizontal slice at y=256 ==="
$line = ""
$xx = 0
while ($xx -lt $w) {
    $c = $bmp.GetPixel($xx, 256)
    if ($c.A -gt 128) {
        $bri = [int]($c.R*0.3 + $c.G*0.59 + $c.B*0.11)
        if ($bri -gt 200) { $ch = "W" }
        elseif ($c.G -gt 100 -and $c.R -lt 120) { $ch = "c" }
        elseif ($c.R -gt 80) { $ch = "b" }
        else { $ch = "." }
    } else { $ch = " " }
    $line += $ch
    $xx += 2
}
Write-Host $line

Write-Host "=== Level line rows ==="
$yy = 282
while ($yy -lt 298) {
    $out = ("row" + $yy + ": ")
    $xx = 180
    while ($xx -lt 350) {
        $c = $bmp.GetPixel($xx, $yy)
        if ($c.R -gt 200 -and $c.G -gt 200) { $ch = "W" }
        elseif ($c.A -gt 128) { $ch = "c" }
        else { $ch = "." }
        $out += $ch
        $xx += 2
    }
    Write-Host $out
    $yy += 1
}
$bmp.Dispose()
$img.Dispose()
