# Wendigos (Voice Cloning Mod for Lethal Company)
The Masked have learned how to copy the voices of your friends. Can you tell who's real and who's fake?

## DISCLAIMER
This mod downloads external binaries (about 350MB in size) from my github and loads an AI model locally to generate voice clips for the masked. If you would prefer not to download this large external binary, you can use Elevenlabs instead.

### Privacy
The clone of your voice is done at runtime entirely locally. This means that (1) there is no stored clone of your voice, the cloning is done at runtime, and (2) all the processing is done locally on your machine. It is not shared externally anywhere. Only the generated voice clips are shared between players.

## First Time Setup
0. Enable the mod in `Wendigos.cfg`. Optionally enable Elevenlabs and add API key and voice ID
1. When launching the game for the first time, you will be asked to record some voice lines. Your current selected mic will be displayed. 
    - If the selected mic is not the one you would like to use, simply click "close", set the correct mic in settings, and relaunch the game.

    - Controls:
      - "R" starts the recording
      - "Q" stops the recording
      - "N" shows the next voice line

2. Once you stop the recording or finish the list of voice lines, the mod will start generating your voice lines.
    - This step can take a long time. The game will prompt you when the generation has finished!
    - The first time this happens, the mod will download the voice cloning model (1.75GB) to the Wendigos mod folder. Any subsequent line generations will be much faster since this model will already be downloaded.

3. If you make a mistake and need to record your voice lines again, exit the game and set "Record new player sample audio?" to true in "BepInEx/config/Wendigos.cfg" in your Thunderstone config.

## Bonus Features
Not a fan of the default voice lines? Customize what the Masked can say in different behaviour categories by editing the following files:
- BepInEx/config/Wendigos/player_sentences/player0_chasing_sentences.txt
- BepInEx/config/Wendigos/player_sentences/player0_idle_sentences.txt
- BepInEx/config/Wendigos/player_sentences/player0_nearby_sentences.txt

Separate new sentences with new lines. You can make the AI say _anything_ (yes, _anything_).

Each player can have their own unique set of voice lines to better suit things that they might say.

## Possible issues
- Voice lines fail to sync (working on this, should be fairly rare)
- players hear different sounds (rare, but can happen due to lag spikes)
- `Writing past end of buffer` unity error (you have a generated line that is too large)
- Game crashes when joining lobby (this happens when someone joins the lobby very quickly, should be rare on non-LAN games)
- Your PC doesn't have enough storage space for the model and voice lines
    - Each player stores the voice cloning model (1.75GB), their sample audio (~10MB) and their own voice lines (<500KB each) locally.

## TODO
  - [ ] Possessed players should have their own voices.
  - [ ] Masked should play certain line categories (i.e. chasing or idle) based on what they're doing.
  - [x] Allow players to use an ElevenLabs API key for better voice cloning results.
  - [ ] AI generates new voice lines between rounds

## Credits
- https://github.com/coqui-ai/tts
- RugbugRedfern's Skinwalkers mod
- @Kalthun and @notgarrett for helping me test this mod
- The Lethal Company Modding Discord 
