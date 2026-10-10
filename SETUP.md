# Environment Setup Manual — .NET MAUI Location Heat Map

Written for macOS on Apple Silicon, which is what this project was built and tested on. Windows
notes are included where the commands differ. Follow the steps in order; each one states what it
does and how to tell it worked.

Steps 1–5 get the project building. Steps 6–7 run it on iOS. Steps 8–10 run it on Android, which
needs one extra thing iOS does not: a Google Maps API key. Steps 11–13 cover the deliverables.

**Versions this was verified against** (10 October 2026):

| Component | Version |
|---|---|
| .NET SDK | 10.0.401 |
| .NET MAUI workload | 10.0.110 |
| Xcode | 26.0 (iOS 26.0 simulator runtime) |
| JDK | Temurin 21.0.8 |
| Android SDK platform | API 36, build-tools 36.0.0 |
| macOS | Tahoe (Darwin 25) |

---

## Step 1 — Install the .NET 10 SDK

.NET MAUI is a workload on top of the .NET SDK, so the SDK comes first.

The installer script puts the SDK under your home directory. That matters: a SDK you own means
`dotnet workload install` in Step 3 works **without `sudo`**, which avoids a whole category of
permission problems later.

```bash
curl -fsSL https://dot.net/v1/dotnet-install.sh -o dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 10.0 --install-dir "$HOME/.dotnet"
```

Then put it on your `PATH`. Add these two lines to `~/.zshrc` so they apply to every new shell:

```bash
export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$HOME/.dotnet:$PATH"
```

Reload with `source ~/.zshrc`.

> **Alternative:** the `.pkg` installer from <https://dotnet.microsoft.com/download> installs to
> `/usr/local/share/dotnet` instead. It works, but then Step 3 needs `sudo dotnet workload
> install maui`. On Windows, install the .NET 10 SDK from the same page, or through the Visual
> Studio 2022 installer's **.NET Multi-platform App UI development** workload, which bundles
> everything in Steps 1–3 and 8.

**Verify:**

```bash
dotnet --version     # 10.0.401 or newer
```

---

## Step 2 — Install a JDK

The Android build runs Java tooling (`d8`, `aapt2`, the manifest merger), so it needs a JDK.
.NET 10 for Android wants **JDK 21**. Newer is not better here — JDK 25 is rejected by the
Android build targets.

```bash
brew install --cask temurin@21
```

Point `JAVA_HOME` at it. Add to `~/.zshrc`:

```bash
export JAVA_HOME="/Library/Java/JavaVirtualMachines/temurin-21.jdk/Contents/Home"
```

**Verify:**

```bash
/usr/libexec/java_home -V        # should list a 21.x JVM
"$JAVA_HOME/bin/java" -version   # openjdk version "21.0.x"
```

> If `java -version` reports some other version, that is a *different* JDK earlier on your
> `PATH`. It does not matter — the build reads `JAVA_HOME`, not `PATH`.

---

## Step 3 — Install the .NET MAUI workload

```bash
dotnet workload install maui
```

This downloads several hundred megabytes of iOS, Android and MAUI SDK packs. Give it a few
minutes.

**Verify:**

```bash
dotnet workload list    # the table should contain a row for "maui"
```

---

## Step 4 — Set up Xcode

Needed for the iOS build and the iOS Simulator.

1. Install Xcode from the Mac App Store.
2. Install the command line tools and accept the licence:

   ```bash
   xcode-select --install
   sudo xcodebuild -license accept
   ```

3. Launch Xcode once so it finishes installing its components, then check
   **Xcode → Settings → Components** has at least one iOS simulator runtime.

**Verify:**

```bash
xcodebuild -version                      # Xcode 26.0 or newer
xcrun simctl list devices available      # at least one iPhone listed
```

### If the iOS build complains about the Xcode version

This is the one setup problem most likely to bite you, so it is worth understanding rather than
just working around.

The MAUI workload ships Apple SDK "bands", and a band is pinned to one Xcode version. The
workload installed in Step 3 included `Microsoft.iOS.Sdk.net10.0_27.0`, which demands Xcode 27:

```
error : This version of .NET for iOS (27.0.10722) requires Xcode 27.0.
        The current version of Xcode is 26.0.
```

The same workload also ships the **26.0** band, and selecting it is simply a matter of naming it
in the target framework. That is why this project's `.csproj` says:

```xml
<TargetFrameworks>net10.0-ios26.0;net10.0-android</TargetFrameworks>
```

rather than a bare `net10.0-ios`. The `26.0` suffix picks the Apple SDK band, which is what
makes the project build against Xcode 26.

So: **if you are on Xcode 27 or newer**, you can drop the suffix back to `net10.0-ios` and
every `-f net10.0-ios26.0` below becomes `-f net10.0-ios`. If you are on Xcode 26, leave it
exactly as it is. If you are on something older, change `26.0` to your Xcode's major version,
as long as that band is installed — check with:

```bash
ls ~/.dotnet/packs | grep Microsoft.iOS.Sdk
```

---

## Step 5 — Install the Android SDK pieces

If you already have Android Studio, you have the SDK; it lives at
`~/Library/Android/sdk`. Set `ANDROID_HOME` to it in `~/.zshrc`:

```bash
export ANDROID_HOME="$HOME/Library/Android/sdk"
```

Rather than hunting for the exact platform and build-tools versions by hand, let the Android
build targets work out what is missing and fetch it:

```bash
cd FinalProject/LocationHeatMap

dotnet build -t:InstallAndroidDependencies -f net10.0-android \
  -p:AndroidSdkDirectory="$HOME/Library/Android/sdk" \
  -p:JavaSdkDirectory="$JAVA_HOME" \
  -p:AcceptAndroidSDKLicenses=True
```

This installs the API 36 platform and anything else the build needs, and accepts the SDK
licences non-interactively. If you have no Android SDK at all, point
`-p:AndroidSdkDirectory` at a path you want it installed to and it will create it.

**Verify:**

```bash
ls "$ANDROID_HOME/platforms"      # android-36 present
```

You also need an emulator image **with Google Play services**. The Map control renders through
the Google Maps SDK, which is part of Play services; a plain AOSP image does not have it and the
map will never draw. In Android Studio's Device Manager, pick a system image whose target says
*Google APIs* or *Google Play*. From the command line, `google_apis_playstore` images work.

---

## Step 6 — Build and run on iOS

Boot a simulator and find its UDID:

```bash
xcrun simctl list devices available | grep iPhone
xcrun simctl boot <UDID>
open -a Simulator
```

Then build, install and launch:

```bash
cd FinalProject/LocationHeatMap
dotnet build -t:Run -f net10.0-ios26.0 -p:_DeviceName=:v2:udid=<UDID>
```

The app asks for location permission the first time it reads a position. Tap
**Allow While Using App** — if you dismiss it, the status line reads "Location permission
denied" and nothing will be recorded.

**Verify:** a navigation bar reading **MainView**, a map filling the screen, and a control strip
at the bottom showing `0 points` and `No data yet — press Start`.

---

## Step 7 — Simulate movement on iOS

A stationary simulator produces one dot. To get an actual heat map you need the device to move.

**From the Simulator menu:** *Features → Location → Freeway Drive* runs a continuous loop around
Cupertino. This is the quickest option.

**From the command line,** for a route you control:

```bash
xcrun simctl location <UDID> start --speed 8 --interval 1 \
  37.3349,-122.0090 37.3368,-122.0058 37.3384,-122.0079 37.3349,-122.0090
```

`--speed` is metres per second (8 is a brisk cycle), and `--interval` is how often a new
position is issued. `xcrun simctl location <UDID> clear` stops it.

Press **Start** in the app before beginning the route, and leave it running a couple of minutes.

**Verify:** coloured circles appear along the path and the counter climbs. Cells you pass through
once are blue; places you linger shift through cyan and yellow to red.

---

## Step 8 — Get a Google Maps API key (Android only)

On iOS the Map control uses Apple MapKit, which needs no key or account. On Android it uses the
Google Maps SDK, which does. **Without a key the app still runs and still draws the heat map
circles, but the map underneath them is a blank grey grid.**

1. Go to <https://console.cloud.google.com/> and create a project.
2. **APIs & Services → Library →** enable **Maps SDK for Android**.
3. **APIs & Services → Credentials → Create credentials → API key**.
4. Click the new key and restrict it:
   - *Application restrictions* → **Android apps**. Add package name
     `com.companyname.locationheatmap` and your debug keystore's SHA-1 fingerprint, which you
     can print with:

     ```bash
     keytool -list -v -alias androiddebugkey \
       -keystore ~/.android/debug.keystore -storepass android -keypass android
     ```

   - *API restrictions* → **Restrict key** → Maps SDK for Android.

Google requires a billing account on the project even though Maps SDK for Android usage is free
at the volumes this app will ever reach.

Then paste the key into the manifest, replacing the placeholder:

```
FinalProject/LocationHeatMap/Platforms/Android/AndroidManifest.xml

<meta-data android:name="com.google.android.geo.API_KEY"
           android:value="YOUR_GOOGLE_MAPS_API_KEY" />
```

> **On committing the key.** The value is compiled into the APK, so `.gitignore` cannot protect
> it — anything you put here is published along with the repository. The application and API
> restrictions above are what actually make that safe: a restricted key is useless to anyone who
> copies it out. This repository keeps the placeholder in source control, so swap your key in
> locally and take care not to commit it.

---

## Step 9 — Build and run on Android

Start the emulator, then deploy:

```bash
$ANDROID_HOME/emulator/emulator -avd <AVD_NAME> &
adb wait-for-device

cd FinalProject/LocationHeatMap
dotnet build -t:Run -f net10.0-android
```

**Verify:** the same **MainView** screen, this time over Google Maps, with a re-centre button in
the top-right corner that Google Maps draws itself.

---

## Step 10 — Simulate movement on Android

**From the emulator UI:** click the **⋯** (Extended controls) button beside the emulator window
→ **Location**. The **Routes** tab lets you draw a two-point route on a map and play it back at
a chosen speed. The **Single points** tab sets one position.

**From the command line:**

```bash
adb emu geo fix -122.0090 37.3349
```

Note the ordering — `geo fix` takes **longitude first**. Call it repeatedly, a few seconds
apart, to walk a path:

```bash
for i in $(seq 0 40); do
  adb emu geo fix $(echo "-122.0090 + $i * 0.00008" | bc -l) $(echo "37.3349 + $i * 0.00006" | bc -l)
  sleep 2
done
```

Press **Start** in the app first.

---

## Step 11 — Capture the screenshots

```bash
# iOS
xcrun simctl io <UDID> screenshot screenshots/ios.png

# Android
adb exec-out screencap -p > screenshots/android.png
```

These go into the Word document.


## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| `error : This version of .NET for iOS (27.0.x) requires Xcode 27.0` | The workload's default Apple SDK band is newer than your Xcode | Use the `net10.0-ios26.0` target framework — see Step 4 |
| Android map is a blank grey grid, heat circles still visible | No Google Maps API key, or the key is restricted to the wrong package/SHA-1 | Step 8. A wrong restriction fails the same way as a missing key |
| Android map is grey on an emulator that *has* a key | System image has no Play services | Recreate the AVD from a *Google APIs* or *Google Play* image |
| `error XA5207: Could not find android.jar for API level 36` | Android SDK platform not installed | Re-run the `InstallAndroidDependencies` command in Step 5 |
| Android build fails complaining about the Java version | `JAVA_HOME` points at JDK 22+ | Step 2 — .NET 10 Android needs JDK 21 |
| Status line reads "Location permission denied" | The permission dialog was dismissed | iOS: Settings → Privacy → Location Services → Location Heat Map → While Using. Android: long-press the app icon → App info → Permissions → Location |
| Status line reads "Location services are turned off" | Location disabled device-wide | Turn it on in device Settings |
| Counter climbs but no circles appear | Fewer than 5 fixes so far | The overlay is rebuilt every 5 fixes, so give it ~15 seconds |
| Map opens over the Atlantic | No stored data and no position fix yet | Set a simulated location (Steps 7/10), then relaunch |
| Map becomes sluggish after a long session | Thousands of circles in `MapElements` | Press **Clear**, or raise `HeatMapRenderer.CellSizeDegrees` so fixes collapse into fewer cells |
| `dotnet workload install` fails with a permission error | SDK installed system-wide | Prefix with `sudo`, or reinstall to `$HOME/.dotnet` as in Step 1 |
| Everything is blue, nothing turns red | Every cell visited the same number of times | Expected — stand still in one place for a minute to create a hotspot |
