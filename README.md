# SLAL2OStim (for OCreatures)

Converts a SexLab (SLAL) creature animation pack into an OStim NG / OCreatures 1.4 pack.

- **FNIS lists:** converts every `FNIS_*_List.txt` to the AnimlistTransitionTool format (`b -Tn <event> <file>.hkx`), renumbering the events the way OCreatures does: first creature = `_0`, A1 = `_1`, the rest = 2, 3, …
- **Behavior hkx:** runs FNIS for Modders on every list (no FNIS window). If FNIS shows ERROR(2012) because the mod name contains spaces, the tool closes FNIS and converts with `hkxcmd` itself.
- **OStim scenes:** builds the scene json files in the OCreatures 1.4 layout: `SKSE/Plugins/OStim/scenes/OCreatures/OCr<Race>/<code>/<Prefix><Race>/...` (a hub plus one file per stage, with the climax as the last stage).
- **Other files:** keeps everything else in the pack (SLAnims, Textures, …).

## Usage

- **GUI:** double-click `SLAL2OStim.exe`, pick the pack folder and the output folder, then click **CONVERT PACK**.
- **Command line:**
  ```
  SLAL2OStim.exe --cli <pack folder> [out folder] [--prefix X] [--no-fnis] [--fnis GenerateFNISforModders.exe]
  SLAL2OStim.exe --cli FNIS_x_List.txt ...        convert lists only
  ```

## FNIS for Modders

FNIS isn't included in this repo because it is third-party software. Get **FNIS Behavior SE** (Fore), then put it next to the exe:

```
SLAL2OStim.exe
FNIS\tools\GenerateFNIS_for_Modders\GenerateFNISforModders.exe
```

Or choose the exe in the GUI.

## Build

`build.bat` (needs the .NET 8 SDK or newer) creates `SLAL2OStim.exe`. The code is in `src\`:
- `Converter.cs`: conversion logic
- `PyJson.cs`: writes json in the same format as Python's `json.dumps(indent=4)`
- `MainForm.cs` / `Program.cs`: the UI and the command line
