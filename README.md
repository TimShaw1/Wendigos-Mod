# Wendigos Voice Cloning (Mod for Lethal Company)

![LC Tnail 2](https://github.com/TimShaw1/Wendigos-Mod/assets/70497517/6f0e6168-82bd-4dbc-9c53-d15a2ea5027a)

The Masked have learned how to copy the voices of your friends. Can you tell who's real and who's fake?

This mod requires **every player** to have the mod to function corrcectly.

[Here's a video I made showing the mod off!](https://youtu.be/PNsyplFd2WU) 

## DISCLAIMER
If you are not using Elevenlabs, this mod downloads external binaries (about 350MB in size) from my github and loads an AI model locally to generate voice clips for the masked. The external binary is 350MB, and the voice cloning model is 1.75GB.

### Privacy
The clone of your voice is done at runtime entirely locally. This means that (1) there is no stored clone of your voice, the cloning is done at runtime, and (2) all the processing is done locally on your machine. It is not shared externally anywhere. Only the generated voice clips are shared between players.

If using Elevenlabs, everything is stored on **your account**.

## First Time Setup
0. If using Elevenlabs, clone everyone's voices in advance.
1. Enable the mod in `Wendigos.cfg`. Optionally enable Elevenlabs and add API key and voice ID. Also be sure to set your language and add custom voice lines (see Bonus Features).
2. (Local AI model only - Elevenlabs users are already done!) When launching the game for the first time, you will be asked to record some voice lines. Your current selected mic will be displayed. 
    - If the selected mic is not the one you would like to use, simply click "close", set the correct mic in settings, and relaunch the game.

    - Controls:
      - "R" starts the recording
      - "Q" stops the recording
      - "N" shows the next voice line
     
If your preferred language is not English, say anything you'd like during this step, then press Q. Try to record at least 1 minute of audio.

3. Once you stop the recording or finish the list of voice lines, the mod will start generating your voice lines.
    - This step can take a long time. The game will prompt you when the generation has finished!
    - The first time this happens, the mod will download the voice cloning model (1.75GB) to the Wendigos mod folder. Any subsequent line generations will be much faster since this model will already be downloaded.

4. If you make a mistake and need to record your voice lines again, exit the game and set "Record new player sample audio?" to true in "BepInEx/config/Wendigos.cfg" in your Thunderstone config.

## Bonus Features
### Custom voice lines
Not a fan of the default voice lines? Customize what the Masked can say in different behavior categories by editing the following files:
- BepInEx/config/Wendigos/player_sentences/player0_chasing_sentences.txt
- BepInEx/config/Wendigos/player_sentences/player0_idle_sentences.txt
- BepInEx/config/Wendigos/player_sentences/player0_nearby_sentences.txt
- BepInEx/config/Wendigos/player_sentences/player0_damaged_sentences.txt

Separate new sentences with new lines. You can make the AI say _anything_ (yes, _anything_).

Each player can have their own unique set of voice lines to better suit things that they might say.

### Masked improvements
This mod removes the masked masks and zombie arms to better fool players. Player clothing is also mimicked.

### Elevenlabs
Players can use Elevenlabs for voice cloning. This produces far better results and makes the masked much more deceptive. For this to work, **every client must have their voice already cloned by Elevenlabs**. You can all use the same api key, but each player needs a unique voice id.

### Real time responses with Azure and ChatGPT (EXPERIMENTAL)
Allows the masked to reply to things players say in real time. This feature does NOT respect push-to-talk.
#### Azure
go to https://portal.azure.com/ to get your AZURE API KEY. Create an account, from there create a new resource group. You will most likely be prompted to create a subscription, when you're doing that it's fine to pick the FREE subscription instead of pay-as-you-go. Make sure to pick a region that is CLOSEST to your real-life location, the closer it is the faster the mod will recognize what you're saying and make the responses overall quicker.

After that's over, go into your new resource group and press create, search for "Azure AI services", click create, punch in all the data, again put the region that's closest to you.

After that's done go into your new Azure AI Service and press on Keys and Endpoint. Put your KEY 1 into wendigos.cfg in AZURE API KEY and put your Location/Region in Region.

#### ChatGPT
go to https://platform.openai.com/docs/guides/text-generation and create an account if you don't have one. Go into your profile -> (under organization) Billing -> add a payment method

After which go back to Your profle -> User API keys -> View project API keys if it requires you to do so, create a new project, in the project API keys create a new key, save it and put it into wendigos.cfg in ChatGPT API key.

#### Config Setup
Set all api keys if you haven't already.

Set `General -> Enable mod?` and `Experimental -> Realtime Responses` to true.

Add your name to the Your Name setting if you'd like the AI to know who is who.

**Optimize Elevenlabs for Speed**: speeds up voice generation at the cost of losing most emotion in the voice. Less stylistic and emotional speaking overall.

**Talk Probability**: how likely the Masked is to talk when no players are nearby.


## Possible issues
- Voice lines fail to sync (working on this, should be fairly rare)
- players hear different sounds (rare, but can happen due to lag spikes)
- `Writing past end of buffer` unity error (you have a generated line that is too large)
- Your PC doesn't have enough storage space for the model and voice lines
    - Each player stores the voice cloning model (1.75GB), their sample audio (~10MB) and their own voice lines (<500KB each) locally.

## FAQ
**What languages does this mod support?**

This mod uses XTTSv2, which supports 17 languages: English (en), Spanish (es), French (fr), German (de), Italian (it), Portuguese (pt), Polish (pl), Turkish (tr), Russian (ru), Dutch (nl), Czech (cs), Arabic (ar), Chinese (zh-cn), Japanese (ja), Hungarian (hu), Korean (ko) Hindi (hi).

Elevenlabs supports 29 languages. Find out more here: https://elevenlabs.io/languages

**Can I use this with Mirage?**

I haven't tested it yet, but probably not (at least not with the voice features enabled).

**Why is the audio clip generation taking so long?**

The first time the mod is run, it needs to download the ai model (1.75GB) and ai script (350MB). These can take a while to download depending on your internet speed. This is only done once, so it should be faster on future generations. The script also loads the ai model and generates audio files for all the voice lines. Depending on your pc specs, this can take a while.

**Are the voice lines auto-translated into my language?**

No, you'll need to write your voice lines in your language in the text files.

**Does this mod store a clone of my voice?**

No, the voice cloning is done at runtime and is not saved. Only the audio files of the voice lines are saved.

**When is the mod listening to me?**
If you have realtime responses enabled, the mod listens only during rounds. If realtime responses is disabled, the mod is never listening to you.

## TODO
  - [x] Add damaged line category
  - [x] Masked should play certain line categories (i.e. chasing or idle) based on what they're doing.
  - [x] Allow players to use an ElevenLabs API key for better voice cloning results.
  - [ ] AI generates new voice lines between rounds
  - [ ] Allow any enemy to clone voices

## Credits
- https://github.com/coqui-ai/tts
- RugbugRedfern's Skinwalkers mod
- @Kalthun and @notgarrett for helping me test this mod
- The Lethal Company Modding Discord
- MadLike on Discord for helping me test the mod and for writing the realtime tutorial.

[![ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/Y8Y6ZWLYH)
