PCBS-Helper 1.0.1
An in-game helper for PC Building Simulator 1 on Windows

Tested with game version 1.15.3. Not compatible with PC Building Simulator 2.
All features run inside the game; both helpers are embedded in the DLL.
The interface follows the game's language: Russian or English.
Other game languages use the English helper interface.
For instructions in Russian, see README.txt.

RELEASE FILES

  PCBS-Helper-1.0.1-Full.zip   - BepInEx and the helper in one archive.
  BepInEx_win_x64_5.4.23.5.zip - The mod loader for Windows x64.
  PCBS-Helper.dll             - The helper plugin.
  README_EN.txt              - These instructions in English.
  README.txt                 - Instructions in Russian.

For your first installation, the Full.zip archive is all you need.
You do not need to download BepInEx separately. Source code archives are
not needed to install the helper. If BepInEx 5 x64 is already installed
and working, download only the DLL.

QUICK INSTALLATION FROM THE FULL ARCHIVE

1. Close the game. In your Steam library, right-click
   PC Building Simulator -> Manage -> Browse local files.
   This folder should contain PCBS.exe and the PCBS_Data folder.

2. Extract the CONTENTS of PCBS-Helper-1.0.1-Full.zip directly into
   this folder. winhttp.dll, doorstop_config.ini and the BepInEx folder
   should be next to PCBS.exe. Do not create an extra folder for the ZIP.
   The helper is already included in BepInEx\plugins\PCBS-Helper.

3. Launch the game through Steam, load a save and enter the workshop.
   Try F1, F2 and F3. No additional downloads are needed.

INSTALLATION FROM SEPARATE FILES

1. Close the game. In your Steam library, right-click
   PC Building Simulator -> Manage -> Browse local files.
   This folder should contain PCBS.exe and the PCBS_Data folder.

2. Extract the CONTENTS of BepInEx_win_x64_5.4.23.5.zip into the game
   folder. winhttp.dll, doorstop_config.ini and the BepInEx folder
   should be next to PCBS.exe, without an extra enclosing folder.
   If BepInEx 5 x64 is already working, skip steps 2 and 3.

3. Launch the game, wait for the main menu and close it.
   BepInEx creates its configuration and plugin folders, and
   BepInEx\LogOutput.log.

4. Create a PCBS-Helper folder inside BepInEx\plugins and copy
   PCBS-Helper.dll into it. Create the plugins folder too if needed.

   Final path:
   PC Building Simulator\BepInEx\plugins\PCBS-Helper\PCBS-Helper.dll

5. Launch the game through Steam, load a save and enter the workshop.
   Press F1, F2 or F3 to open a panel.

CONTROLS

  F1  - Convenience settings and switches for the F2/F3 helpers.
  F2  - Build Helper: 3DMark estimates, builds, upgrades and replacements.
  F3  - Will it Run? Helper; opens/pins the panel, or hides it when pinned.
  Alt - Toggle the cursor for buttons and fields in F2/F3.
  Esc - Close F1 or hide F2.

Drag a window by its title bar to move it. Settings, window positions
and Build Helper data are saved between game sessions.

F1 FEATURES

  Install All Apps       - Open Add/Remove Programs on the in-game PC
                           to install its applications.
  Instant 3DMark         - Start a 3DMark test to get the result immediately.
  Click Purchase        - Left-click a part in PC Stats opened from a job
                           email to add one new part of that model to a cart.
  Show Diagnose Objectives - Reveal the parts that need replacement.
  Show Hidden Objectives   - Reveal additional job objectives.

Closing F1 keeps the selected features active. Click Purchase is off
by default. Each additional click adds another unit. Unavailable parts
are skipped. Open the shop to choose delivery and complete the purchase;
adding parts to the cart does not spend money.

NEW WORKBENCH OPTIONS AND INVENTORY SORTING

Enable Automatic thermal paste, Remove dependencies while holding LMB,
and Replace all customizable cables separately in F1. All three are off
by default. In removal mode (2), hold LMB on a blocked part to remove its
required cables, covers and parts in sequence with normal animation and
screw speed. Release to pause, hold again to resume; Esc cancels the queue.
Empty full water-cooling loops manually first.

Thermal paste is applied when installing a CPU and reapplied during cooler
installation if missing, including AIO coolers mounted in a case slot.

In cable mode (3), select an active cable to replace all connected cables
whose appearance the game allows you to change. Fixed PSU/cooler leads
are skipped. Inventory buttons sort by the resale price shown on item cards
or by arrival order, in either direction. The selection is saved. Sorting
is enabled by default, newest arrivals first. The game stores insertion
order rather than calendar timestamps.

BUILD HELPER (F2)

Enable Build Helper in F1, then press F2 and Alt to use its controls.

  3DMark Score    - Select CPU, GPU, GPU count, RAM Speed and RAM Channels,
                    then click Calculate for a preliminary score estimate.
  Build Maker     - Find a new build using Get Builds.
  Build Upgrader  - Find upgrades for an existing PC using Get Upgrades.
  Part Replacer   - Find replacement parts using Get Parts.
  History & Saves - Review calculation history and saved results.

In an email with a 3DMark target, click Helper to import the budget,
target and available PC details into Build Maker or Build Upgrader.
Review Job objectives and calculation limits, then start the search.
Clicking Helper does not accept the job or start the calculation.

In Build Maker results, Order adds the selected build's parts to the
shop carts with the required RAM and GPU quantities. The button changes
to In cart after confirmation. If it shows Partly in cart, review the
skipped parts below the form and order them manually.
Choose the PSU, storage and cooling separately. Complete the purchase
and select delivery in the shop. Order works independently of Click Purchase.

Budget is for parts only; do not add the payment for your work.
For new builds, Reserved Budget initially defaults to $400 for other parts.
Adjust it to your needs. Career Level and % Through are imported from the
game. Leave All Parts Unlocked? unchecked in a normal career.
Check repair, storage, cooling and overclocking objectives separately.
Verify the final score with the in-game 3DMark test.

Expand / Compact changes the window size; - / + changes the interface scale.
Clear resets only the current calculation tab. Close hides the panel
and preserves its data.

WILL IT RUN? HELPER (F3)

Enable Will it Run? Helper in F1. In a job email asking for a PC that can
play a particular game, click Will it Run? to open and pin its requirements.
The button does not accept the job. Requirements appear automatically
when working on the customer's PC.

The panel shows the profile (minimum, recommended or 4K), RAM, VRAM, GPU,
CPU and storage space. Job clarifications appears only when the order adds
conditions, including CPU/GPU brands, Team Group memory, Seagate drives and
cables. A suitable available CPU/GPU replacement is suggested when needed.
Check CPU and motherboard socket compatibility. Manual game lookup does
not carry over the previous order's conditions.

For an automatically displayed panel, press F3 to pin it while shopping.
Press F3 again to hide a pinned panel. Press Alt to use Choose game for
manual lookup, or From job to return to the current job's requirements.
Press Alt again to turn off the cursor.

UPDATE AND UNINSTALL

To update, close the game and replace PCBS-Helper.dll in the same folder.
Settings are preserved. A working BepInEx installation does not need
to be reinstalled.
To uninstall, close the game and move the PCBS-Helper folder
out of BepInEx\plugins.

TROUBLESHOOTING

  - Some keyboards require Fn + F1/F2/F3.
  - If F1 works but F2/F3 do not, enable the corresponding helper in F1.
  - If you cannot click F2/F3 buttons, toggle the cursor with Alt.
  - If no panels open, check the DLL path and LogOutput.log.
    If the log was not created, check the extraction and BepInEx installation.

When reporting an issue, include BepInEx\LogOutput.log, your game and
helper versions, and a short description of your actions.

English guide with GIF demonstrations:
https://github.com/Kalarono/PCBS-Helper/blob/main/README_EN.md
The demonstrations use the Russian interface; the English guide uses
the corresponding English button and field names.

Project: https://github.com/Kalarono/PCBS-Helper
PCBS-Helper's own calculator reads component data and 3DMark coefficients from the installed game.
Empty CPU/GPU clock fields use stock frequencies; you can enter BIOS clock values.
Scores assume stable hardware without thermal throttling. Compatibility loads in the
background on first use, while 3DMark scoring is available immediately.
BepInEx: https://github.com/BepInEx/BepInEx
