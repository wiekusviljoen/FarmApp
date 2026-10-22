Add-Type -AssemblyName System.Drawing
function New-SheepIcon($path, $size) {
  $bmp = New-Object System.Drawing.Bitmap($size,$size)
  $g = [System.Drawing.Graphics]::FromImage($bmp)
  $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
  $green = [System.Drawing.Color]::FromArgb(47,107,63)
  $g.Clear($green)
  $white = [System.Drawing.Brushes]::White
  $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::White,($size*0.018))
  $s = $size/192.0
  $g.FillEllipse($white, (35*$s),(68*$s),(112*$s),(70*$s))
  $g.FillEllipse($white, (120*$s),(48*$s),(42*$s),(58*$s))
  $g.FillEllipse($white, (151*$s),(58*$s),(31*$s),(23*$s))
  $g.FillEllipse($white, (119*$s),(45*$s),(17*$s),(10*$s))
  $g.FillEllipse($white, (151*$s),(43*$s),(17*$s),(10*$s))
  $g.FillEllipse($white, (25*$s),(84*$s),(24*$s),(22*$s))
  $g.FillRectangle($white,(53*$s),(130*$s),(11*$s),(36*$s))
  $g.FillRectangle($white,(89*$s),(132*$s),(11*$s),(34*$s))
  $g.FillRectangle($white,(124*$s),(131*$s),(11*$s),(35*$s))
  $g.FillEllipse($green,(157*$s),(71*$s),(6*$s),(6*$s))
  $g.FillEllipse($green,(167*$s),(71*$s),(6*$s),(6*$s))
  $g.DrawLine($pen,(158*$s),(100*$s),(178*$s),(92*$s))
  $bmp.Save($path,[System.Drawing.Imaging.ImageFormat]::Png)
  $g.Dispose(); $bmp.Dispose()
}
New-SheepIcon "C:\Users\Wiekus\source\repos\Farm App\Farm App\wwwroot\farmflow-sheep-192-v3.png" 192
New-SheepIcon "C:\Users\Wiekus\source\repos\Farm App\Farm App\wwwroot\farmflow-sheep-512-v3.png" 512
Remove-Item "C:\Users\Wiekus\source\repos\Farm App\make-farmflow-sheep-icon.ps1" -Force
