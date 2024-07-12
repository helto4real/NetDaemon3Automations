dotnet publish src/NetDaemonApps/NetDaemonApps.csproj -c release -o ./nd4
cp -r ./nd4/* /mnt/hass/nd4/
# /mnt/hass/nd4/
