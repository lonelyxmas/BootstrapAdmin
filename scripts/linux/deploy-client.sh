#! /bin/bash

cd ~/BootstrapAdmin
git pull
dotnet publish src/client/Bootstrap.Client -c Release

rm -f ~/BootstrapAdmin/src/client/Bootstrap.Client/bin/Release/netcoreapp3.1/publish/appsettings*.json
systemctl stop ba.client
\cp -fr ~/BootstrapAdmin/src/client/Bootstrap.Client/bin/Release/netcoreapp3.1/publish/* /usr/local/ba/client/
systemctl start ba.client
systemctl status ba.client -l
