# Deploy

Mount the Home Assistant configuration share, then run the validated deployment script:

```bash
sudo mount -t cifs -o username=username //home-assistant/config /mnt/hass
pwsh ./deploy.ps1 -Destination /mnt/hass/nd5
```

The script restores locked dependencies, runs the full test suite, publishes to `artifacts/publish`, and only then copies the result to the destination.
