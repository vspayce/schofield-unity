#!/bin/bash
cd ~/Schofield-Unity
export DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer SCHOFIELD_DEV=1
U=/Applications/Unity/Hub/Editor/6000.6.4f1/Unity.app/Contents/MacOS/Unity
echo "start $(date)" > Builds/local.log
$U -batchmode -quit -projectPath ~/Schofield-Unity -buildTarget iOS -executeMethod Schofield.EditorTools.BuildTools.BuildIOS -logFile Builds/batch-iOS.log
echo "iOS unity $(grep -E 'Build Finished' Builds/batch-iOS.log)" >> Builds/local.log
rm -rf Builds/iOS.xcarchive Builds/iOS-dev-export
xcodebuild -project Builds/iOS/Unity-iPhone.xcodeproj -scheme Unity-iPhone -configuration Release -destination generic/platform=iOS -archivePath Builds/iOS.xcarchive DEVELOPMENT_TEAM=2TJY362VLX CODE_SIGN_STYLE=Automatic CODE_SIGNING_ALLOWED=NO CODE_SIGNING_REQUIRED=NO CODE_SIGN_IDENTITY="" archive > Builds/iOS-archive.log 2>&1 || echo "archive failed" >> Builds/local.log
xcodebuild -exportArchive -archivePath Builds/iOS.xcarchive -exportOptionsPlist Builds/ExportOptions-ios-dev.plist -exportPath Builds/iOS-dev-export > Builds/iOS-devexport.log 2>&1
echo "iOS export $(grep -E 'EXPORT (SUCCEEDED|FAILED)' Builds/iOS-devexport.log)" >> Builds/local.log
xcrun devicectl device install app --device 00008130-00142C5A3A78001C Builds/iOS-dev-export/Schofield.ipa > Builds/iOS-install.log 2>&1 && echo "iOS installed" >> Builds/local.log
echo "done $(date)" >> Builds/local.log
