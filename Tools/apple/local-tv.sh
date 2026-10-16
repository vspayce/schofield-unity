#!/bin/bash
cd ~/Schofield-Unity
export DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer SCHOFIELD_DEV=1
U=/Applications/Unity/Hub/Editor/6000.6.4f1/Unity.app/Contents/MacOS/Unity
echo "start $(date)" > Builds/local.log
$U -batchmode -quit -projectPath ~/Schofield-Unity -buildTarget tvOS -executeMethod Schofield.EditorTools.BuildTools.BuildTV -logFile Builds/batch-tvOS.log
echo "tvOS unity $(grep -E 'Build Finished' Builds/batch-tvOS.log)" >> Builds/local.log
rm -rf Builds/tvOS.xcarchive Builds/tvOS-dev-export
xcodebuild -project Builds/tvOS/Unity-iPhone.xcodeproj -scheme Unity-iPhone -configuration Release -destination generic/platform=tvOS -archivePath Builds/tvOS.xcarchive DEVELOPMENT_TEAM=2TJY362VLX CODE_SIGN_STYLE=Automatic CODE_SIGNING_ALLOWED=NO CODE_SIGNING_REQUIRED=NO CODE_SIGN_IDENTITY="" archive > Builds/tvOS-archive.log 2>&1 || echo "archive failed" >> Builds/local.log
xcodebuild -exportArchive -archivePath Builds/tvOS.xcarchive -exportOptionsPlist Builds/ExportOptions-tvos-dev.plist -exportPath Builds/tvOS-dev-export > Builds/tvOS-devexport.log 2>&1
echo "tvOS export $(grep -E 'EXPORT (SUCCEEDED|FAILED)' Builds/tvOS-devexport.log)" >> Builds/local.log
xcrun devicectl device install app --device 4a4b8e98ad8580b8008aa7670b263c90c36048ee Builds/tvOS-dev-export/Schofield.ipa > Builds/tvOS-install.log 2>&1 && echo "tvOS installed" >> Builds/local.log
echo "done $(date)" >> Builds/local.log
