#!/bin/bash

destination=/c/Users/manchax/cuclock/win10-x64

dotnet publish \
    -f net10.0-windows10.0.26100.0 \
    -c Release \
    -p:Platform=x64 \
    -p:RuntimeIdentifierOverride=win-x64 

cp -rf ./bin/x64/Release/net10.0-windows10.0.26100.0/win10-x64/publish/* \
	$destination
