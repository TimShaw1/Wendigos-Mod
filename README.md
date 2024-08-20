# Wendigos Voice Cloning (Mod for Lethal Company)

![LC Tnail 2](https://github.com/TimShaw1/Wendigos-Mod/assets/70497517/6f0e6168-82bd-4dbc-9c53-d15a2ea5027a)

The Masked have learned how to copy the voices of your friends. Can you tell who's real and who's fake?

This mod requires **every player** to have the mod to function corrcectly.

[Here's a video I made showing the mod off!](https://youtu.be/PNsyplFd2WU) 

[Here's a SECOND video I made showing the real time stuff off!](https://youtu.be/GSBca7f7S5A?si=Hfohs8w3yxDyMuES) 

## DISCLAIMER
If you are not using Elevenlabs, this mod downloads external binaries (about 350MB in size) from my github and loads an AI model locally to generate voice clips for the masked. The external binary is 350MB, and the voice cloning model is 1.75GB.

### Privacy
The clone of your voice is done at runtime entirely locally. This means that (1) there is no stored clone of your voice, the cloning is done at runtime, and (2) all the processing is done locally on your machine. It is not shared externally anywhere. Only the generated voice clips are shared between players.

If using Elevenlabs, everything is stored on **your account**.

## First Time Setup (not real time)
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

## Real time responses with Azure and ChatGPT (EXPERIMENTAL)
Allows the masked to reply to things players say in real time. This feature does NOT respect push-to-talk.

#### What you need
- An Elevenlabs subscription ($5 tier or better)
- A free Azure Speech to Text service
- A ChatGPT account with some api credits bought (NOT THE SUBSCRIPTION)

#### Elevenlabs
Create an account and subscribe to the $5 tier or better. Click on your profile and click "API Keys". Create a key and save it somewhere. Clone your voice (and anyone else's who you are sharing the account with), then under Voices -> Personal click your voice, then click "ID" to copy the voice ID. Save that ID too.

#### Azure
Go to https://portal.azure.com/ and create an account and a new resource group. You will most likely be prompted to create a subscription, when you're doing that it's fine to pick the FREE subscription, no need for pay-as-you-go. Make sure to pick a region that is CLOSEST to your real-life location for best results.

After that's done, go into your new resource group and press create, search for "Azure AI services", click create, punch in all the data, and again put the region that's closest to you.

After that's done go into your new Azure AI Service and press on Keys and Endpoint. Put your KEY 1 into wendigos.cfg in AZURE API KEY and put your Location/Region in Region.

#### ChatGPT
go to https://platform.openai.com/docs/guides/text-generation and create an account if you don't have one. Go into your profile -> (under organization) Billing -> add a payment method. Purchase however many credits you'd like, but you likely wont need more than the minimum. **These credits do not expire at the end of the month.**

After which go back to Your profle -> User API keys -> View project API keys if it requires you to do so, create a new project, in the project API keys create a new key, save it and put it into wendigos.cfg in ChatGPT API key.

If you have trouble finding your api key, look here: https://help.openai.com/en/articles/4936850-where-do-i-find-my-openai-api-key

#### Config Setup
Set all api keys (Elevenlabs, Azure, ChatGPT) if you haven't already.

Set your Elevenlabs voice ID.

Set Azure region to the one you picked earlier (MOD WONT WORK WITHOUT THIS).

Set `General -> Enable mod?` and `Experimental -> Realtime Responses` to true.

Add your name to the Your Name setting if you'd like the AI to know who is who.

#### Optional configs
**Optimize Elevenlabs for Speed**: speeds up voice generation at the cost of losing most emotion in the voice. Less stylistic and emotional speaking overall.

**Talk Probability**: how likely the Masked is to play pre-generated voice lines.


## Possible issues
- Voice lines fail to sync (working on this, should be fairly rare)
- players hear different sounds (rare, but can happen due to lag spikes)
- `Writing past end of buffer` unity error (you have a generated line that is too large)
- Your PC doesn't have enough storage space for the model and voice lines
    - Each player stores the voice cloning model (1.75GB), their sample audio (~10MB) and their own voice lines (<500KB each) locally.

## FAQ
**What languages does this mod support?**

This mod uses XTTSv2, which supports 17 languages: English (en), Spanish (es), French (fr), German (de), Italian (it), Portuguese (pt), Polish (pl), Turkish (tr), Russian (ru), Dutch (nl), Czech (cs), Arabic (ar), Chinese (zh-cn), Japanese (ja), Hungarian (hu), Korean (ko) Hindi (hi).

Elevenlabs supports 32 languages. Find out more here: https://elevenlabs.io/languages

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

**Can I share my Elevenlabs account?**
Yes! You can have everyone on the same account. Everyone just has to set their own voice ID in their config.

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

[![ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/Y8Y6ZWLYH)
