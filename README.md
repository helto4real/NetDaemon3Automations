# This is the dev setup for NetDaemon V3 - ALPHA

Use this repository as template when developing apps for NetDaemon for the alpha version of NetDaemon 3.

This template is a good starting point for your development environment. It has pre-built structure both for building
apps and unit test them. Unit tests fake objects are alpha and the fake API can be changed.

This version is in alpha and expect API:s to change!.

## Getting started

1. Make new repository with this repo as template
2. RENAME `src/_appsettings.json` to `src/appsettings.json`. Edit the appsettings.json file to provide details about how
   to connect to Home Assistant. Token, Host is mandatory. Rest is optional.
3. Run dotnet restore in the terminal
4. Run code generation tool `nd-codegen` to generate strongly typed classes from your entities and services in Home
   Assistant and copy the generated file in the apps folder
6. Add and edit your apps in the `/src/apps` folder. There are a few code-snippets you can use.

More information will be here soon how to run the docker container etc on the docs site. For now you can exeriment with
this making your own deployment from this template.

[![buymeacoffee](https://www.buymeacoffee.com/assets/img/custom_images/orange_img.png)](https://www.buymeacoffee.com/ij1qXRM6E)

## Issues

- If you have issues or suggestions of improvements to this template,
  please [add an issue](https://github.com/net-daemon/netdaemon-app-template)
- If you have issues or suggestions of improvements to NetDaemon,
  please [add an issue](https://github.com/net-daemon/netdaemon/issues)

## Discuss the NetDaemon

Please [join the Discord server](https://discord.gg/K3xwfcX) to get support or if you want to contribute and help
others.
