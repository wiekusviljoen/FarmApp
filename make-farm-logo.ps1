Add-Type -AssemblyName System.Drawing
function Make-Logo([int]$size,[string]$path) {
  $bmp = New-Object System.Drawing.Bitmap $size,$size
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $g.Clear([System.Drawing.Color]::FromArgb(18,122,79))
  $bg = New-Object System.Drawing.Drawing2D.LinearGradientBrush([System.Drawing.Rectangle]::new(0,0,$size,$size),[System.Drawing.Color]::FromArgb(118,201,79),[System.Drawing.Color]::FromArgb(8,115,77),45)
  $g.FillRectangle($bg,0,0,$size,$size); $bg.Dispose()
  $black=[System.Drawing.Brushes]::Black
  $font=New-Object System.Drawing.Font('Arial',[int]($size*.31),[System.Drawing.FontStyle]::Bold)
  $g.DrawString('F',$font,$black,[float]($size*.08),[float]($size*.43))
  $g.DrawString('RM',$font,$black,[float]($size*.57),[float]($size*.43))
  $pen=New-Object System.Drawing.Pen([System.Drawing.Color]::FromArgb(7,17,13),[float]($size*.027))
  $g.DrawLine($pen,$size*.43,$size*.66,$size*.43,$size*.25)
  $g.DrawLine($pen,$size*.35,$size*.66,$size*.51,$size*.66)
  $g.DrawLine($pen,$size*.35,$size*.66,$size*.43,$size*.37)
  $g.DrawLine($pen,$size*.51,$size*.66,$size*.43,$size*.37)
  $g.DrawLine($pen,$size*.43,$size*.25,$size*.43,$size*.12)
  $g.DrawEllipse($black,$size*.405,$size*.19,$size*.05,$size*.05)
  $g.DrawLine($pen,$size*.45,$size*.215,$size*.59,$size*.215)
  $g.DrawLine($pen,$size*.43,$size*.215,$size*.43,$size*.09)
  $g.DrawLine($pen,$size*.43,$size*.215,$size*.29,$size*.15)
  $g.DrawLine($pen,$size*.43,$size*.215,$size*.52,$size*.34)
  $g.DrawLine($pen,$size*.43,$size*.215,$size*.34,$size*.34)
  $g.DrawArc($pen,$size*.07,$size*.65,$size*.86,$size*.18,0,180)
  $g.DrawArc($pen,$size*.12,$size*.71,$size*.76,$size*.14,0,180)
  $g.DrawArc($pen,$size*.2,$size*.77,$size*.62,$size*.11,0,180)
  $bmp.Save($path,[System.Drawing.Imaging.ImageFormat]::Png)
  $pen.Dispose(); $font.Dispose(); $g.Dispose(); $bmp.Dispose()
}
$root='C:\Users\Wiekus\source\repos\Farm App\Farm App\wwwroot'
Make-Logo 192 "$root\farm-logo-192.png"
Make-Logo 512 "$root\farm-logo-512.png"
Remove-Item "$root\test.png" -ErrorAction SilentlyContinue
