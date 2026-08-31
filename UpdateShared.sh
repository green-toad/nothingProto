#!/usr/bin/bash

cd Shared
rm -rf bin
dotnet restore
dotnet build -c Release
cd ../lib/Shared
rm -rf Shared.dll
cp ../../Shared/bin/Release/net10.0/Shared.dll ./