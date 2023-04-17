# MultiHit

**Split the flytext of action damage into multiple.**

Install by adding 3rd party repo:
- CN: `https://raw.githubusercontent.com/Bluefissure/MultiHit/CN/repo.json`
- Intl: TBD

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

### 4. Does it make any sense since the game actions act like s**t?
It makes little sense if you don't use any vfx mods for actions (check [the one and only papachin](https://www.youtube.com/c/papapachin) for details).
