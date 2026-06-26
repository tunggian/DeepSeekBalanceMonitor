Add-Type -AssemblyName System.Drawing
$img = [System.Drawing.Image]::FromFile("QGnGC.png")
$bmp = New-Object System.Drawing.Bitmap($img)
$w = $bmp.Width
$h = $bmp.Height
Write-Host ("Size: " + $w + "x" + $h)
Write-Host "=== Center vertical ==="
$yy = 0
while ($yy -lt $h) {
    $c = $bmp.GetPixel(($w/2), $yy)
    if ($c.A -gt 128) {
        $bri = [int]($c.R*0.3 + $c.G*0.59 + $c.B*0.11)
        if ($c.R -gt 200 -and $c.G -gt 200 -and $c.B -gt 200) { $ch = "W" }
        elseif ($c.G -gt 100 -and $c.R -lt 100) { $ch = "c" }
        elseif ($c.B -gt 100 -and $c.R -lt 100 -and $c.G -lt 100) { $ch = "B" }
        elseif ($bri -gt 150) { $ch = "#" }
        elseif ($bri -gt 80) { $ch = "o" }
        else { $ch = "." }
    } else { $ch = " " }
    $line += $ch
    $yy += 4
}
Write-Host $line

Write-Host "=== Center horizontal ==="
$line = ""
$xx = 0
while ($xx -lt $w) {
    $c = $bmp.GetPixel($xx, ($h/2))
    if ($c.A -gt 128) {
        $bri = [int]($c.R*0.3 + $c.G*0.59 + $c.B*0.11)
        if ($c.R -gt 200 -and $c.G -gt 200 -and $c.B -gt 200) { $ch = "W" }
        elseif ($c.G -gt 100 -and $c.R -lt 100) { $ch = "c" }
        elseif ($c.B -gt 100 -and $c.R -lt 100 -and $c.G -lt 100) { $ch = "B" }
        elseif ($bri -gt 150) { $ch = "#" }
        elseif ($bri -gt 80) { $ch = "o" }
        else { $ch = "." }
    } else { $ch = " " }
    $line += $ch
    $xx += 4
}
Write-Host $line
$bmp.Dispose()
$img.Dispose()
