# Silksong: HK-Speedrunning Speed Patch

This is an injection script for board-legal modifications for Hollow Knight: Silksong speedruns.

Many thanks to @peekagrub for this version-independent implementation!

## Legality

WARNING: this patch is in preview; until this notice is removed, runs submitted may be rejected if problems are found with this implementation.

- CourierRNGFix is permitted for use in NMG & glitched runs.
- DowndashTransitionFix is permitted for use in glitched runs only.

## Usage

Please note that this patch interferes with BepInEx installations, even when not enabled; install this on an unmodded install to ensure continued function.

- Extract `Output.zip` to your game files.
- To run:
    - Windows (& Linux via Proton): Open the game normally.
    - Linux (native) & MacOS: Run the `run_hksr.sh` script.
- To configure, first run the game once tp generate the configuration file.
- Edit `hksr_patches/hksr_patches.json` & change `false` to `true` for each patch you wish to enable. Save this file then relaunch.
- Check the patches you expect are listed in the top left below the version text!