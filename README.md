# MultiHit

[![GitHub all releases](https://img.shields.io/github/downloads/Bluefissure/MultiHit/total?color=green)](https://github.com/Bluefissure/MultiHit/releases)

[中文文档](https://github.com/Bluefissure/MultiHit/blob/CN/README_zh.md)

**Split the flytext of action damage into multiple.**

Install by adding 3rd party repo:
- CN: `https://raw.githubusercontent.com/Bluefissure/MultiHit/CN/repo.json`
- CN (Mirror): `https://dalamud_cn_3rd.otters.cloud/plugins/MultiHit`
- Intl: `https://raw.githubusercontent.com/Bluefissure/MultiHit/master/repo.json`

Open config window by `/mhit`.

A lot of the code is thankfully copy-pasted from [DamageInfoPlugin](https://github.com/lmcintyre/DamageInfoPlugin).


## Q&A

### 1. Does it change the actual damage applied?
Of cause no, all of the changes are client-side and only affect flytext, it won't make any difference to the data you send to / receive from the game server, nor will it affect any dps data parsed by ACT / FFLogs.

### 2. What if I use other plugins to change the action names in the flytext?
The flytext is retrived by the text in it (because it doesn't have action info), so if you change the names of action, the flytext cannot be matched by the unchanged action name in the game data.

Same reason, if you have multiple multihits enabled for the same action, only one will take effect.

### 3. Does it support different clients?
It will. I use CN client for developing & testing latest features first (because of the subscription), and I'll make another repo for distributing it for Intl clients. 

### 4. Since the game's actions are like s**t, what's the point?
Multhit aims to enhance the combat feedback for players who pursue a sense of hitting. Of course, this will inevitably sacrifice some practicality. If you don't care about these or care more about observing the exact damage value of each skill, then Multhit will be meaningless (for more user cases, please check the mods from [the one and only papachin](https://www.youtube.com/c/papapachin) for details).

### 5. My network latency is very high, and the effect is very poor after using this plugin...
Multhit is not recommended for use in high network latency environments.

### 6. Why is the appearance time of flytext and the delay I input for my own presets not consistent?
The splitted flytext is triggered by the in-game ones, and there's always a delay for most actions, which is defined in the TMB files.
In most cases, Multhit is best used with TMB modification. It is recommended to set the start time of flytext in TMB to 0 (3 for AOE skills). The [VFXEditor](https://github.com/0ceal0t/Dalamud-VFXEditor) is a good tool to make the modification.

### 7. Why is there no effect after I make any modifications (including enabling/disabling presets, adding/deleting/modifying values)?
Make sure to click "apply change" after modifying to apply the changes.

### 8. Why did the flytext color not change even though I modified it?
The colors only works when the alpha(transparency) is not 0. Please also modify the transparency value on the right bar when modifying the color.

### 9. After I turned on the "Interrupt" option, the flytext of the skill was interrupted by the subsequent skill before it was fully displayed. Will I lose damage?
As mentioned in 1, all modifications only affect your flytext display, so your actual damage will never change. In most cases, it is not recommended to enable the interruption option unless you have installed some VFX mods that can be interrupted (such as Papachin's NERO-GNB's Keen Edge series skills).

### 10. What is the use of Finalhit?
Finalhit can display the total damage of this attack after the last hit. If you want to visually see the real total damage of each skill, you can try it (it will also be interrupted if the interruption option is enabled).

