#!/bin/bash

dotnet publish \
    -f net9.0-windows10.0.19041.0 \
    -c Release \
    -p:Platform=x64 \
    -p:RuntimeIdentifierOverride=win-x64 

