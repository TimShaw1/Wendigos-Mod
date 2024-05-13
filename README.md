# Wendigos (Voice Cloning Mod for Lethal Company)
The Masked have learned how to copy the voices of your friends. Can you tell who's real and who's fake?

This mod requires **every player** to have the mod to function corrcectly.

## DISCLAIMER
This mod downloads external binaries (about 350MB in size) from my github and loads an AI model locally to generate voice clips for the masked. If you would prefer not to download this large external binary, you can use Elevenlabs instead. The external binary is 350MB, and the voice cloning model is 1.75GB.

### Privacy
The clone of your voice is done at runtime entirely locally. This means that (1) there is no stored clone of your voice, the cloning is done at runtime, and (2) all the processing is done locally on your machine. It is not shared externally anywhere. Only the generated voice clips are shared between players.

## First Time Setup
0. Enable the mod in `Wendigos.cfg`. Optionally enable Elevenlabs and add API key and voice ID. Also be sure to set your language and add custom voice lines (see Bonus Features).
1. When launching the game for the first time, you will be asked to record some voice lines. Your current selected mic will be displayed. 
    - If the selected mic is not the one you would like to use, simply click "close", set the correct mic in settings, and relaunch the game.

    - Controls:
      - "R" starts the recording
      - "Q" stops the recording
      - "N" shows the next voice line
     
If your preferred language is not English, say anything you'd like during this step, then press Q. Try to record at least 1 minute of audio.

2. Once you stop the recording or finish the list of voice lines, the mod will start generating your voice lines.
    - This step can take a long time. The game will prompt you when the generation has finished!
    - The first time this happens, the mod will download the voice cloning model (1.75GB) to the Wendigos mod folder. Any subsequent line generations will be much faster since this model will already be downloaded.

3. If you make a mistake and need to record your voice lines again, exit the game and set "Record new player sample audio?" to true in "BepInEx/config/Wendigos.cfg" in your Thunderstone config.

## Bonus Features
### Custom voice lines
Not a fan of the default voice lines? Customize what the Masked can say in different behavior categories by editing the following files:
- BepInEx/config/Wendigos/player_sentences/player0_chasing_sentences.txt
- BepInEx/config/Wendigos/player_sentences/player0_idle_sentences.txt
- BepInEx/config/Wendigos/player_sentences/player0_nearby_sentences.txt

Separate new sentences with new lines. You can make the AI say _anything_ (yes, _anything_).

Each player can have their own unique set of voice lines to better suit things that they might say.

### Masked improvements
This mod removes the masked masks and zombie arms to better fool players. Player clothing is also mimicked.

### Elevenlabs
Players can use Elevenlabs for voice cloning. This produces far better results and makes the masked much more deceptive. For this to work, **every client must have their voice already cloned by Elevenlabs**. You can all use the same api key, but each player needs a unique voice id.

## Possible issues
- Voice lines fail to sync (working on this, should be fairly rare)
- players hear different sounds (rare, but can happen due to lag spikes)
- `Writing past end of buffer` unity error (you have a generated line that is too large)
- Game crashes when joining lobby (this happens when someone joins the lobby very quickly, should be rare on non-LAN games)
- Your PC doesn't have enough storage space for the model and voice lines
    - Each player stores the voice cloning model (1.75GB), their sample audio (~10MB) and their own voice lines (<500KB each) locally.

## FAQ
**What languages does this mod support?**

This mod uses XTTSv2, which supports 17 languages: English (en), Spanish (es), French (fr), German (de), Italian (it), Portuguese (pt), Polish (pl), Turkish (tr), Russian (ru), Dutch (nl), Czech (cs), Arabic (ar), Chinese (zh-cn), Japanese (ja), Hungarian (hu), Korean (ko) Hindi (hi).

Elevenlabs supports 26 languages. Find out more here: https://elevenlabs.io/languages

**Can I use this with Mirage?**

I haven't tested it yet, but probably not (at least not with the voice features enabled).

**Why is the audio clip generation taking so long?**

The first time the mod is run, it needs to download the ai model (1.75GB) and ai script (350MB). These can take a while to download depending on your internet speed. This is only done once, so it should be faster on future generations. The script also loads the ai model and generates audio files for all the voice lines. Depending on your pc specs, this can take a while.

**Are the voice lines auto-translated into my language?**

No, you'll need to write your voice lines in your language in the text files.

**Does this mod store a clone of my voice?**

No, the voice cloning is done at runtime and is not saved. Only the audio files of the voice lines are saved.

## TODO
  - [ ] Possessed players should have their own voices.
  - [ ] Masked should play certain line categories (i.e. chasing or idle) based on what they're doing.
  - [x] Allow players to use an ElevenLabs API key for better voice cloning results.
  - [ ] AI generates new voice lines between rounds
  - [ ] Allow any enemy to clone voices

## Credits
- https://github.com/coqui-ai/tts
- RugbugRedfern's Skinwalkers mod
- @Kalthun and @notgarrett for helping me test this mod
- The Lethal Company Modding Discord 
