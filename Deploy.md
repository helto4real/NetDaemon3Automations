# Deploy

```bash
sudo mount -t cifs -o username=username //ip/config /mnt/hass
ssudo pwsh ./deploy.ps1
sudo dotnet publish -c release -o /mnt/hass/nd4/

```
