#!/bin/bash
# Build CuidAR for iPhone, install it on the connected iPhone and launch it, streaming its logs.
#
#   Tools/run_ios.sh            # Unity export + Xcode build + install + launch (Unity editor must be closed)
#   Tools/run_ios.sh --no-unity # reuse the existing Xcode project in plant-care-ar/
#   Tools/run_ios.sh --launch   # only relaunch the installed app and stream its logs
#
# Logs (Unity Debug.Log included) are printed and saved to Logs/iphone.log.
set -euo pipefail
cd "$(dirname "$0")/.."

export DEVELOPER_DIR="${DEVELOPER_DIR:-/Applications/Xcode.app/Contents/Developer}"
UNITY="${UNITY:-/Applications/Unity/Hub/Editor/6000.6.9f1/Unity.app/Contents/MacOS/Unity}"
BUNDLE_ID="com.pedroscarelli.cuidar"
XCODE_DIR="plant-care-ar"
DERIVED="Library/XcodeBuild"
mkdir -p Logs

DEVICE="${DEVICE:-$(xcrun devicectl list devices 2>/dev/null | awk '/connected/ && /physical/ {for (i=1;i<=NF;i++) if ($i ~ /^[0-9A-F]{8}-[0-9A-F]{16}$/) print $i}' | head -1)}"
if [ -z "$DEVICE" ]; then
    echo "No connected iPhone found (plug it in, unlock it and trust this Mac)." >&2
    exit 1
fi
echo "iPhone: $DEVICE"

MODE="${1:-}"
if [ "$MODE" != "--launch" ]; then
    if [ "$MODE" != "--no-unity" ]; then
        if pgrep -f "Unity.app/Contents/MacOS/Unity -projectpath $(pwd)" >/dev/null; then
            echo "Close the Unity editor first (or use --no-unity)." >&2
            exit 1
        fi
        echo "== Unity: exporting Xcode project =="
        "$UNITY" -batchmode -quit -projectPath "$(pwd)" -buildTarget iOS \
            -executeMethod CuidARBuild.BuildIOS -logFile Logs/unity-ios-build.log || {
            grep -E "error CS|\[CuidAR\]|BuildFailed" Logs/unity-ios-build.log | head -20
            echo "Unity build failed (see Logs/unity-ios-build.log)." >&2
            exit 1
        }
    fi

    echo "== Xcode: building =="
    xcodebuild -project "$XCODE_DIR/Unity-iPhone.xcodeproj" -scheme Unity-iPhone -configuration Debug \
        -destination "id=$DEVICE" -derivedDataPath "$DERIVED" -allowProvisioningUpdates \
        DEVELOPMENT_TEAM=3D7686Y46B CODE_SIGN_STYLE=Automatic build > Logs/xcodebuild.log 2>&1 || {
        grep -E "error:" Logs/xcodebuild.log | head -20
        echo "Xcode build failed (see Logs/xcodebuild.log)." >&2
        exit 1
    }
    APP="$(find "$DERIVED/Build/Products/Debug-iphoneos" -maxdepth 1 -name '*.app' | head -1)"
    echo "== Installing $APP =="
    xcrun devicectl device install app --device "$DEVICE" "$APP" > /dev/null
fi

echo "== Launching (logs -> Logs/iphone.log, Ctrl+C to stop) =="
xcrun devicectl device process launch --device "$DEVICE" --console --terminate-existing "$BUNDLE_ID" 2>&1 | tee Logs/iphone.log
