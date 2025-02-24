dotnet publish src/NetDaemonApps/NetDaemonApps.csproj -c release -o ./nd5
cp -r ./nd5/* /mnt/homeassistant/nd5/
# /mnt/hass/nd4/
