# XIVMitigation Plugin

A [Dalamud](https://github.com/goatcorp/Dalamud) plugin for Final Fantasy XIV that displays a real-time mitigation overlay during raid fights, highlighting the spells you need to cast directly on your hotbars.

Works with plans created and exported from **[xivmitigation.com](https://xivmitigation.com)**.

---

## What does it do?

When you load a mitigation plan into the plugin, it will:

- Show an **overlay window** listing upcoming mechanics and which spells each role needs to cast, along with a countdown timer for each mechanic.
- **Highlight the corresponding spell icons** on your hotbars with a colored frame so you can instantly see what to press without looking away from the fight.
- Display a **live countdown timer** directly on each highlighted spell icon.
- Automatically stop highlighting when combat ends.

The overlay and hotbar highlights update in real time as the fight progresses.

---

## Features

- Real-time overlay window with upcoming mechanics and assigned mitigation spells
- Hotbar spell highlighting with a visible colored frame
- Live countdown timer displayed on each highlighted spell icon
- Filter by role (show only your role, or all roles at once)
- Configurable lead time (how many seconds before a mechanic the highlight appears)
- Spell names displayed in your game client language
- Highlights only active during combat

---

## Requirements

- [Final Fantasy XIV](https://www.finalfantasyxiv.com/) with [XIVLauncher](https://goatcorp.github.io/) and [Dalamud](https://github.com/goatcorp/Dalamud) installed
- A mitigation plan exported from [xivmitigation.com](https://xivmitigation.com)

---

## Installation

This plugin is not available in the official Dalamud plugin repository. You need to load it manually as a dev plugin.

### Option 1 — Load the pre-built DLL (easiest)

1. Download the latest release from the [Releases](https://github.com/Maateria/XIVMitigationPlugin/releases) page.
2. Extract the ZIP and note the path to `XIVMitigationPlugin.dll`.
3. In-game, type `/xlsettings` and go to **Experimental**.
4. Under **Dev Plugin Locations**, add the full path to `XIVMitigationPlugin.dll`.
5. Open `/xlplugins`, go to **Dev Tools > Installed Dev Plugins**, and enable **XIVMitigation Plugin**.

### Option 2 — Build from source

1. Install the [.NET 10 SDK](https://dotnet.microsoft.com/download).
2. Clone this repository:
   ```
   git clone https://github.com/Maateria/XIVMitigationPlugin.git
   ```
3. Open `XIVMitigationPlugin.sln` in Visual Studio or JetBrains Rider.
4. Build the solution (Debug or Release).
5. The DLL will be located at:
   ```
   XIVMitigationPlugin/bin/x64/Debug/XIVMitigationPlugin.dll
   ```
6. Follow steps 3–5 from Option 1 to load it into Dalamud.

---

## How to create a mitigation plan

1. Go to **[xivmitigation.com](https://xivmitigation.com)** and select the fight you want to plan for.
2. Assign mitigation spells to each mechanic for each role (MT, OT, H1, H2, M1, M2, R1, R2).
3. Export the plan as a **JSON file** using the site's export feature.
4. Save the file somewhere accessible on your computer.

---

## How to use the plugin

### First setup

1. In-game, type `/xivmit config` to open the settings window.
2. Click **Browse** and select the JSON plan file you exported from xivmitigation.com.
3. Select your **role** (MT, OT, H1, H2, M1, M2, R1, R2).
4. Adjust the **lead time** (how many seconds before a mechanic the overlay and highlights activate — default is 10 seconds).
5. Close the settings window.

### During a fight

- The overlay window shows upcoming mechanics with a countdown and the spells assigned to each role.
- When a mechanic is within your configured lead time, the corresponding spells are highlighted on your hotbars with a red frame and a live countdown timer.
- When no mechanic is imminent, the next upcoming mechanic is shown in preview with a dimmer highlight so you can prepare in advance.
- Highlights disappear automatically when you leave combat.

---

## Commands

| Command | Description |
|---|---|
| `/xivmit` | Toggle the overlay window |
| `/xivmit config` | Open the settings window |
| `/xivmit debug` | Print debug information to `/xllog` (useful for troubleshooting) |

---

## Settings

| Setting | Description |
|---|---|
| **Plan file** | Path to the JSON file exported from xivmitigation.com |
| **Role** | Your role in the raid (MT, OT, H1, H2, M1, M2, R1, R2) |
| **Lead time** | Seconds before a mechanic when highlights activate |
| **Show all roles** | Display spells for every role instead of just yours |
| **Visible mechanics** | Number of upcoming mechanics shown in the overlay |

---

## Troubleshooting

**Spells are not highlighted on my hotbars**
- Make sure you are in combat (highlights only appear during active combat).
- Run `/xivmit debug` and check `/xllog` for details. It will show whether spell names were found in the cache and whether they exist on your hotbars.
- Ensure the spells assigned in your plan are actually placed on one of your visible hotbars (hotbars 1–10).

**Spell names in the overlay are in English**
- This can happen if Lumina could not load the localized action sheet. Try reloading the plugin.

**The plan does not load**
- Make sure the JSON file was exported directly from xivmitigation.com using the plugin export feature.
- Check that the file path in the settings does not contain special characters.

---

## License

[AGPL-3.0](LICENSE.md)
