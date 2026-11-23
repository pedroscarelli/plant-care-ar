# CuidAR – Care for a Virtual Plant in Augmented Reality

Put the printed QR marker on a table, point the phone at it and a thirsty plant appears on top of it. The phone becomes your gardening tool: water the plant with a **hose** or a **watering can**, prune its dry leaves with **scissors** and tap away the **bugs** that come to eat it, all in AR.

Project for the Virtual Reality course (Computer Science – FURB, 2026/2).

**Team:** João Fissmer ([@cyanide0081](https://github.com/cyanide0081)) and Pedro Scarelli ([@pedroscarelli](https://github.com/pedroscarelli))

**Kanban:** [GitHub Projects – cuidAR](https://github.com/users/pedroscarelli/projects/1) · **Slides:** [CuidAR – Apresentação.pdf](CuidAR%20–%20Apresentação.pdf)

## The idea

A printed **QR marker** goes on a table. Pointing the phone at it makes a **virtual potted plant** appear on the marker, at real scale, anchored to the table. The user takes care of it with three tools:

| Tool | How to use | What happens |
|------|------------|--------------|
| **Hose** (default) | put the **+** crosshair on the pot and **hold a finger on the screen** | a fast jet of water hits the soil and the plant hydrates |
| **Watering can** | put the **+** on the pot and **tilt the phone sideways**, like pouring | water arcs out of the spout; the more you tilt, the stronger the stream |
| **Scissors** | **tap a leaf** | the scissors fly to the leaf, snip it, the leaf falls spinning to the table and later grows back; dry brown leaves are the ones that need pruning |

While it drinks, the plant **comes back to life live**: leaves rise and turn from yellow to green, the soil darkens and at 100% a flower blooms with sparkles, a chime and a vibration. After that, little **bugs** crawl onto the leaves and eat the plant (it dries faster) until the user **taps them** away.

There are **three plant models** to choose from: **Daisy** (regular water, pink flower), **Cactus** (needs little water, shrivels and droops its arms when dry) and **Sunflower** (needs a lot of water, its heavy head hangs down with closed petals when dry).

This follows the professor's suggestion after N3 to simplify the project (the first version used a neural network to diagnose real leaves; see *Previous version* below) and focus on the AR interaction.

**Target audience:** children, students and beginners learning how to care for plants (home, school and community gardens).

**Interdisciplinarity:** Augmented Reality + Human-Computer Interaction (gestures with the phone) + Biology (water needs of different plants, pruning, pests) + Physics (the water is a ballistic particle stream under gravity).

## How it works

```
camera (AR Foundation) → image tracking of the QR marker (runtime reference library, full marker or QR only)
                       → virtual plant anchored on the marker (stays put when the marker leaves the view)
phone pose (6DoF)      → ray from the screen center (+) onto the soil / table → where the water lands
hose: finger held  /  can: phone roll  → water particles solved to land on the + → hydration ↑ → animations
scissors: tap      → closest leaf projected on screen → snip animation → falling leaf → regrowth
bugs: tap          → closest bug projected on screen → squash + poof
```

- **Marker:** `Marker/cuidar_marker_A4.pdf` (print at 100%, the square is 15 cm). The leaf border gives ARKit/ARCore more features than a bare QR code, so detection is faster. The marker also works **cut around the QR code** (7.4 cm square): both images are registered in a runtime reference library (`Assets/Resources/Markers`).
- **Everything is procedural:** the plants, pot, tools and bugs are built in code from generated meshes, and all sounds (water, chime, pop, snip) are synthesized, so the project has no model or audio files.
- **Plant state:** hydration from 0 to 1 (starts at 10%; the cactus fills 2× faster, the sunflower 0.7×; slowly dries again, faster with bugs on it).

## Functional requirements

| ID | The user... |
|----|-------------|
| RF01 | opens the app, allows camera access and sees the real world through the camera (with **Open Settings** if access was denied) |
| RF02 | points the camera at the printed QR marker (full or cut around the QR code) and the app recognizes it |
| RF03 | sees a thirsty virtual plant anchored on the marker, at real scale |
| RF04 | waters the plant with the **hose** (default tool): aims the **+** at the pot and holds the screen |
| RF05 | waters the plant with the **watering can** by tilting the phone sideways |
| RF06 | sees the plant hydrate (leaves, colors, soil, bar) and celebrate at 100% (flower, sparkles, chime, vibration), with feedback when watering wrong and **Dry again** to replay |
| RF07 | chooses between three plant models (Daisy, Cactus, Sunflower) with different looks and water needs |
| RF08 | takes care of the plant: prunes dry leaves with the **scissors** and taps away the **bugs** |

## Technologies

- **Unity 6000.6.9f1** (URP), C#
- **AR Foundation 6** with **Apple ARKit** (iPhone) and **Google ARCore** (Android)
- **Image tracking** (ARTrackedImageManager with a mutable runtime library)
- **Particle System** for the water, splashes, sparkles and bug poofs
- **TextMeshPro** for the interface, **Input System** for touch and sensors

## Running the project

Requirements: Unity **6000.6.9f1** with **Android Build Support** and/or **iOS Build Support**; Xcode for iPhone. Print `Marker/cuidar_marker_A4.pdf` at 100% scale.

### Android

1. Open the project in Unity Hub (the first open downloads the packages).
2. **File → Build Profiles → Android → Switch Platform**, connect the phone with USB debugging on and click **Build And Run**. The scenes (`MainMenu`, `ARScene`) and ARCore are already configured.

Or from the command line (Unity closed):

```bash
/Applications/Unity/Hub/Editor/6000.6.9f1/Unity.app/Contents/MacOS/Unity -batchmode -quit -projectPath . \
  -buildTarget Android -executeMethod CuidARBuild.BuildAndroid -logFile -
adb install -r Builds/cuidAR.apk
```

The phone must support ARCore ([list of devices](https://developers.google.com/ar/devices)).

### iPhone

- With Unity closed and the iPhone connected and unlocked: `Tools/run_ios.sh` (exports the Xcode project, builds, installs, launches and saves the logs to `Logs/iphone.log`). Edit the team ID at the top of the script for your Apple account.
- Or in Unity: **Build Profiles → iOS → Build**, open `plant-care-ar/Unity-iPhone.xcodeproj` in Xcode, choose your team under *Signing & Capabilities* and run on the iPhone (not the Simulator). With a free account the app must be reinstalled every 7 days, and you need to trust the developer in *Settings → General → VPN & Device Management*.

### Testing in the Editor

Press Play in `ARScene`: **P** places the plant in front of the camera, hold **Space** (or the mouse with the hose) to water, **B** spawns bugs, click leaves with the scissors.

## Platform notes

- **iOS camera permission:** Unity strips its iOS camera-permission code when the project doesn't use `WebCamTexture`, so `Application.RequestUserAuthorization` answered "denied" without asking. The app asks through a small native plugin, `Assets/Plugins/iOS/CuidARCameraPermission.mm` (AVFoundation). `Assets/Editor/IOSBuildCheck.cs` stops any iOS build where ARKit is disabled or the *Camera Usage Description* is empty.
- **iOS 27 orientation:** Unity's iOS layer uses an orientation API that became a no-op on iOS 27, which could leave the AR camera rotated by 90°. `ARCameraPose` compares the AR camera with the gravity sensor and corrects it (iPhone only).
- **Shaders/physics stripping:** materials are copies of `Assets/Resources/CuidAR/CuidARLit.mat` (keeps the URP Lit shader in the build) and meshes are generated instead of `CreatePrimitive` (whose colliders are stripped from builds).

## Previous version (N3): leaf diagnosis

The first version used a YOLOv8n network (Unity Inference Engine) to detect leaf problems on real plants. Its code is still in `Assets/Scripts/Detection`, `Assets/Scripts/UI` and `CuidARController.cs`, but it is no longer used by the scene. The model is `Assets/Resources/Models/cuidar_yolov8n.onnx` (trained on PlantDoc converted to 4 classes). The planning document of that version is [PlanejamentoAplicativo_CuidAR.docx](PlanejamentoAplicativo_CuidAR.docx) (in Portuguese).
