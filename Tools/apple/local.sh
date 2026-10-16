#!/bin/bash
# Build Schofield for the iPhone and the Apple TV and install it straight onto them (development signing,
# no TestFlight). Log: Builds/local.log
cd ~/Schofield-Unity
export DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer
U=/Applications/Unity/Hub/Editor/6000.6.4f1/Unity.app/Contents/MacOS/Unity
echo "start $(date)" > Builds/local.log
local_one() { P=$1; METHOD=$2; DEST=$3; DEV=$4
  $U -batchmode -quit -projectPath ~/Schofield-Unity -buildTarget $P -executeMethod Schofield.EditorTools.BuildTools.$METHOD -logFile Builds/batch-$P.log
  echo "$P unity $(grep -E 'Build Finished' Builds/batch-$P.log)" >> Builds/local.log
  rm -rf Builds/$P.xcarchive Builds/$P-dev-export
  xcodebuild -project Builds/$P/Unity-iPhone.xcodeproj -scheme Unity-iPhone -configuration Release -destination "$DEST" -archivePath Builds/$P.xcarchive DEVELOPMENT_TEAM=2TJY362VLX CODE_SIGN_STYLE=Automatic CODE_SIGNING_ALLOWED=NO CODE_SIGNING_REQUIRED=NO CODE_SIGN_IDENTITY="" archive > Builds/$P-archive.log 2>&1 || { echo "$P archive failed" >> Builds/local.log; return; }
  xcodebuild -exportArchive -archivePath Builds/$P.xcarchive -exportOptionsPlist Builds/ExportOptions-$(echo $P | tr A-Z a-z)-dev.plist -exportPath Builds/$P-dev-export > Builds/$P-devexport.log 2>&1
  echo "$P export $(grep -E 'EXPORT (SUCCEEDED|FAILED)' Builds/$P-devexport.log)" >> Builds/local.log
  xcrun devicectl device install app --device $DEV Builds/$P-dev-export/Schofield.ipa > Builds/$P-install.log 2>&1 && echo "$P installed" >> Builds/local.log || echo "$P install failed: $(tail -3 Builds/$P-install.log)" >> Builds/local.log
}
local_one tvOS BuildTV "generic/platform=tvOS" 4a4b8e98ad8580b8008aa7670b263c90c36048ee
local_one iOS BuildIOS "generic/platform=iOS" 00008130-00142C5A3A78001C
echo "done $(date)" >> Builds/local.log
