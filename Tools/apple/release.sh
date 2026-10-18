#!/bin/bash
# After the editor has written Builds/iOS and Builds/tvOS (Schofield ▸ Apple ▸ Build Xcode Project…):
# archive each, upload it to App Store Connect (TestFlight), and install a development build on the
# iPhone and the Apple TV if they're reachable. Log: Builds/release.log
cd ~/Schofield-Unity
export DEVELOPER_DIR=/Applications/Xcode.app/Contents/Developer
AUTH="-authenticationKeyPath $HOME/.appstoreconnect/private_keys/AuthKey_9PN666KWJ6.p8 -authenticationKeyID 9PN666KWJ6 -authenticationKeyIssuerID 4cc80cd6-9b32-419c-8f58-199cfa4a49bf"
LOG=Builds/release.log
echo "start $(date)" > $LOG
one() { P=$1; DEST=$2; DEV=$3; p=$(echo $P | tr A-Z a-z)
  [ -d Builds/$P/Unity-iPhone.xcodeproj ] || { echo "$P: no Xcode project" >> $LOG; return; }
  rm -rf Builds/$P.xcarchive Builds/$P-export Builds/$P-dev-export
  xcodebuild -project Builds/$P/Unity-iPhone.xcodeproj -scheme Unity-iPhone -configuration Release -destination "$DEST" -archivePath Builds/$P.xcarchive DEVELOPMENT_TEAM=2TJY362VLX CODE_SIGN_STYLE=Automatic CODE_SIGNING_ALLOWED=NO CODE_SIGNING_REQUIRED=NO CODE_SIGN_IDENTITY="" archive > Builds/$P-archive.log 2>&1 \
    || { echo "$P archive FAILED: $(grep -E 'error:' Builds/$P-archive.log | head -3)" >> $LOG; return; }
  echo "$P archived" >> $LOG
  xcodebuild -exportArchive -archivePath Builds/$P.xcarchive -exportOptionsPlist Builds/ExportOptions-$p-manual.plist -exportPath Builds/$P-export $AUTH > Builds/$P-upload.log 2>&1
  echo "$P upload $(grep -E 'EXPORT (SUCCEEDED|FAILED)|Uploaded' Builds/$P-upload.log | tr '\n' ' ')" >> $LOG
  xcodebuild -exportArchive -archivePath Builds/$P.xcarchive -exportOptionsPlist Builds/ExportOptions-$p-dev.plist -exportPath Builds/$P-dev-export > Builds/$P-devexport.log 2>&1
  xcrun devicectl device install app --device $DEV Builds/$P-dev-export/Schofield.ipa > Builds/$P-install.log 2>&1 && echo "$P installed on device" >> $LOG || echo "$P device install skipped: $(tail -1 Builds/$P-install.log)" >> $LOG
}
one tvOS "generic/platform=tvOS" 4a4b8e98ad8580b8008aa7670b263c90c36048ee
one iOS "generic/platform=iOS" 00008130-00142C5A3A78001C
echo "done $(date)" >> $LOG
