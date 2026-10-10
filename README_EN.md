# PCBS-Helper

[Русский](README.md) | **English**

**An in-game helper for PC Building Simulator 1 on Windows. Version 1.0.0.**
Tested with game version **1.15.3**. Not compatible with PC Building Simulator 2.

Convenience settings, a **Build Helper** and a **Will it Run? Helper**, all
available inside the game. The interface follows the game's language:
Russian or English, with English used for other languages.
Both helpers are embedded in the DLL; no separate application is needed.

This README includes installation instructions and a **step-by-step guide
with 9 GIF demonstrations**:
[F1 settings](#f1) · [F2 Build Helper](#f2) · [F3 Will it Run?](#f3).

## Download and install

Choose one of the two installation methods on the
[Releases page](https://github.com/Kalarono/PCBS-Helper/releases).

### Full archive — the easiest option

1. **Close the game** and download **PCBS-Helper-1.0.0-Full.zip**.
   The archive includes BepInEx 5 x64 and the helper.
2. In your Steam library, right-click PC Building Simulator →
   **Manage → Browse local files**.
   This folder should contain `PCBS.exe` and `PCBS_Data`.
3. **Extract the contents of the ZIP directly into this folder**, without
   creating an extra folder. `winhttp.dll`, `doorstop_config.ini` and
   the `BepInEx` folder should be next to `PCBS.exe`.
4. **Launch the game, load a save and enter the workshop.**
   Press **F1**, **F2** or **F3** to open a panel.

### DLL only — if BepInEx is already working

Close the game, download **PCBS-Helper.dll** and place it here:

```text
PC Building Simulator/BepInEx/plugins/PCBS-Helper/PCBS-Helper.dll
```

Create the `plugins/PCBS-Helper` folders if needed.
Requires **BepInEx 5 for Windows x64**.

For a separate installation, the release also includes
**BepInEx_win_x64_5.4.23.5.zip** and **README_EN.txt** with instructions
in English. **README.txt** contains the Russian instructions.
The **Source code** archives are not needed to install the helper.

## Controls

| Key | Action |
| --- | --- |
| **F1** | Settings: install apps, instant 3DMark, reveal objectives and enable the F2/F3 helpers. |
| **F2** | Build Helper: estimate 3DMark scores, find builds, upgrades and replacement parts. |
| **F3** | Will it Run? Helper; opens/pins the panel, or hides it when already pinned. |
| **Alt** | Toggle the cursor to interact with F2/F3. |
| **Esc** | Close F1 or hide F2. |

Drag a window by its title bar to move it. Settings, window positions and
Build Helper data are saved between game sessions.

## Feature guide

After installation, load a save and enter the workshop.
The steps below use **English interface labels**. The GIFs were recorded
with the Russian interface, but show the same controls and actions.
In **F1**, enable the features you need: **ON** means a feature is active.
Closing the F1 window keeps your selected settings.

<a id="f1"></a>

### F1 — convenience settings

#### 1. Install all apps and instant 3DMark

1. Press **F1** and enable **Install All Apps**.
2. On the in-game PC's desktop, open **Add/Remove Programs**.
   The applications will appear on that PC's desktop.
3. Enable **Instant 3DMark** in F1.
4. Open 3DMark on the in-game PC and start the test.
   The result appears without waiting for the benchmark.

The score still depends on the PC's hardware. To estimate a different
configuration before building it, use the **3DMark Score** tab in F2.

![Installing all apps and running instant 3DMark](docs/images/guide/01_apps_and_instant_3dmark.gif)

#### 2. Add parts to the cart with a click

1. Enable **Click Purchase** in **F1**.
2. Open a job email for a customer's PC and click **PC Stats**.
3. **Left-click** a part in the list to add one new part of the same model
   to the shop cart. Each additional click adds another unit.
4. Open the shop, check the cart, choose delivery and complete the purchase.

You do not need to open the shop before adding parts. Money is deducted
when you complete the purchase. Parts unavailable in the shop are skipped;
a message after each click shows the result.

![Enabling Click Purchase, selecting parts in PC Stats and checking the cart](docs/images/guide/02_click_purchase.gif)

#### 3. Reveal diagnosis objectives

1. Open an email for a diagnosis and repair job.
2. Enable **Show Diagnose Objectives** in **F1**.
3. Check the email's objective list: the parts that need replacement
   are now visible.

The demonstration reveals that the **CPU** and **PSU** need replacement.

![Revealing the parts that need replacement in a diagnosis job](docs/images/guide/03_diagnose_objectives.gif)

#### 4. Reveal hidden job objectives

1. Open a job email.
2. Enable **Show Hidden Objectives** in **F1**.
3. Check the additional objectives and account for them when building
   or repairing the PC.

The demonstration reveals requirements to use **new parts** and
**clean the dust from the PC**.

![Revealing hidden objectives: new parts and cleaning the PC](docs/images/guide/04_hidden_objectives.gif)

<a id="f2"></a>

### F2 — Build Helper

Enable **Build Helper (F2)** in **F1**. Press **F2**, then **Alt** to show
the cursor and interact with fields and buttons. You can also open the
helper by clicking **Helper** in an email with a 3DMark target.

#### 5. Estimate 3DMark without turning on the PC

1. Open the **3DMark Score** tab.
2. Select the **CPU**, **GPU** and number of graphics cards.
3. Set **RAM Speed** and **RAM Channels**, then click **Calculate**.
4. Read the estimated score. Change the selected parts to compare
   configurations before buying or installing them.

To estimate the current PC's score, manually match the selected models
to its installed hardware. This is a preliminary calculation; use the
in-game 3DMark test to verify the job's result.
The example produces an estimated score of **4263**.

![Selecting CPU, GPU and RAM parameters to estimate a 3DMark score](docs/images/guide/05_3dmark_calculator.gif)

#### 6. Find an upgrade from a job email

1. In an email asking for a higher 3DMark score, click **Helper**.
2. The **Build Upgrader** tab opens with the budget, target score and
   available hardware details imported from the customer's PC.
3. Review **Job objectives and calculation limits**, the reserved budget
   and the search options.
4. Click **Get Upgrades** and compare the parts, price, remaining budget
   and estimated score of each option.
5. Buy the parts you need in the shop and install them in the customer's PC.

The **Helper** button imports data. Accept the job separately and start
the search using the button in the tab.

![Importing a job email into Build Upgrader and comparing upgrade options](docs/images/guide/06_upgrade_helper.gif)

#### 7. Find a new build and order its parts

1. In an email requesting a new PC with a 3DMark target, click **Helper**.
2. The budget and target score are imported into **Build Maker**.
   Review the job objectives and **Reserved Budget**, which initially
   defaults to **$400** for new builds. Adjust it for the PSU, cooling
   and storage you plan to buy.
3. Click **Get Builds** and choose a suitable result.
4. Click **Order** at the end of its row. Scroll the table to the right
   if needed. Once the parts are added, the button shows **In cart**.
5. Open the shop, check the cart and add the remaining parts.
   Choose delivery and complete the purchase as usual.

**Order** adds the case, CPU, motherboard, RAM and GPU included in the
selected result, with the required numbers of RAM modules and graphics
cards. **Choose the PSU, storage and cooling separately.**
This button works independently of **Click Purchase** in F1.

If the button shows **Partly in cart**, read the list of skipped parts
below the form and order them manually.

![Finding a new build from an email, clicking Order and checking the shop cart](docs/images/guide/07_build_and_order.gif)

#### Other F2 features

| Tab or action | How to use it |
| --- | --- |
| **Part Replacer** | Enter the original PC and your constraints, then click **Get Parts** to find replacements. |
| **History & Saves** | Review calculation history and saved results. |
| **Expand / Compact** | Switch between a large window and the compact panel. |
| **− / +** | Adjust the size of text and interface elements. |
| **Clear** | Reset the fields and results of the current calculation tab. |

Career level and experience percentage are filled in automatically from
the game. In a normal career, leave **All Parts Unlocked?** unchecked so
the search respects part availability. **Budget** is the amount available
for parts; do not add the payment for your work.
Review repair, storage, cooling and overclocking objectives separately.
All fields and constraints are described in the
[detailed guide (Russian)](docs/USAGE.md).

<a id="f3"></a>

### F3 — Will it Run? Helper

Enable **Will it Run? Helper (F3)** in **F1**. The panel displays the
requirements for a game's job objective: the profile (minimum, recommended
or 4K), RAM, VRAM, GPU, CPU and storage space.

#### 8. Open requirements from an email and shop for parts

1. In an email asking for a PC that can play a particular game,
   click **Will it Run?**.
2. Review the job's requirements. Below the CPU, the helper shows a
   suitable available processor from the other brand and its socket.
3. Open the shop and compare parts against the panel. Opening it from
   the email button already pins it.
4. Press **F3** to hide the pinned panel.

Check that your chosen CPU and motherboard have compatible sockets.
The **Will it Run?** button opens the requirements without accepting the job.

![Opening Will it Run? from an email and choosing a CPU in the shop](docs/images/guide/08_will_it_run_email.gif)

#### 9. View requirements while building a customer's PC

1. Start working on a customer's PC with a job objective to play a game.
2. The requirements panel appears automatically. Use it as a reference
   while installing parts.
3. To move the panel, press **Alt** and drag it by its title bar.
4. Press **F3** to keep it on screen when switching to the shop.
   Press F3 again to hide it.

![Automatic Will it Run? requirements during assembly and moving the panel](docs/images/guide/09_will_it_run_workbench.gif)

For manual lookup, press **Alt** and click **Choose game**.
**From job** returns to the current job's requirements.
Press **Alt** again to turn off the cursor.

## Update and uninstall

**Update:** close the game and replace `PCBS-Helper.dll` in the same folder.
Your settings are preserved; a working BepInEx installation does not need
to be reinstalled.

**Uninstall:** with the game closed, move the
`BepInEx/plugins/PCBS-Helper` folder outside `plugins`.

## Troubleshooting

- Some keyboards require **Fn + F1/F2/F3**.
- If F1 works but F2/F3 do not, enable the corresponding helper in F1.
- To click buttons in F2/F3, toggle the cursor with **Alt**.
- If no panels open, check the DLL path and whether
  `BepInEx/LogOutput.log` exists. When reporting an issue, include this log,
  your game and helper versions, and a description of the problem.

## More information

The following additional documents are currently in Russian:

- [Detailed guide: panels, Build Helper fields and job import](docs/USAGE.md).
- [Troubleshooting and key bindings](docs/TROUBLESHOOTING.md).
- [Changelog](CHANGELOG.md) · [Building from source](DEVELOPMENT.md).

Calculations are based on the work of **Jacob W. Klein** —
[original calculator](https://jacobwklein.github.io/PC-Building-Simulator/HTML-Calculator/Current-Version/PC-Building-Simulator-HTML-Calculator.html).
BepInEx: [official project](https://github.com/BepInEx/BepInEx).
