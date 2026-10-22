$m=Invoke-WebRequest -UseBasicParsing 'https://farmapp.runasp.net/manifest.webmanifest'
Write-Output ('MANIFEST_STATUS=' + $m.StatusCode)
Write-Output $m.Content
try {
  $sw=Invoke-WebRequest -UseBasicParsing 'https://farmapp.runasp.net/service-worker.js'
  Write-Output ('SW_STATUS=' + $sw.StatusCode)
  Write-Output ($sw.Content.Substring(0,[Math]::Min(500,$sw.Content.Length)))
} catch {
  Write-Output ('SW_ERROR=' + $_.Exception.Message)
}