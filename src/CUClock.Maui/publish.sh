#!/bin/bash

destination=../../win10-x64

[[ ! -d $destination ]] && mkdir -p $destination

dotnet publish \
    -f net10.0-windows10.0.26100.0 \
    -c Release \
    -p:Platform=x64 \
    -p:RuntimeIdentifierOverride=win-x64 

cp -rf ./bin/x64/Release/net10.0-windows10.0.26100.0/win-x64/publish/* \
    $destination

echo 'win10-x64\CUClock.Maui.exe' > ../../cuclock.bat
echo Done! Execute ../../cuclock.bat
