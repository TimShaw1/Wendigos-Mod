# Wendigos Voice Cloning (Mod for Lethal Company)

![LC Tnail 2](https://github.com/TimShaw1/Wendigos-Mod/assets/70497517/6f0e6168-82bd-4dbc-9c53-d15a2ea5027a)

The Masked have learned how to copy the voices of your friends. Can you tell who's real and who's fake?

This mod requires **every player** to have the mod to function corrcectly.

[Here's a video I made showing the mod off!](https://youtu.be/PNsyplFd2WU) 

[Here's a SECOND video I made showing the real time stuff off!](https://youtu.be/GSBca7f7S5A?si=Hfohs8w3yxDyMuES) 

## Features
- Real-time AI generated responses to players, allowing the masked to hold full conversations with players.
- Automatic clip collection and transcription for intelligent responses to players (even without realtime responses enabled).
- Optional automatic voice cloning (no more cloning in advance!).
- Host config sync to streamline setup for non-technical friends.
- Masked improvements (hidden nametags, disable mask and zombie arms)

## First Time Setup (not real time) (free services)
#### 1. Run the game 
First, run the game once to generate the config file `Wendigos.cfg`.
#### 2. Get your Chat API key (Gemini Example) and set up the Chat config
- Chat example: to get a Gemini API key go to https://aistudio.google.com/ and click "Get API key". Then create a project and create an API key.
- Then, set your api key, chat service provider, and model in `Wendigos.cfg`.

#### 3. Get your STT API key (Azure Example) and set up STT
Go to https://portal.azure.com/ and create an account and a new resource group. You will most likely be prompted to create a subscription, when you're doing that it's fine to pick the FREE subscription, no need for pay-as-you-go. Make sure to pick a region that is CLOSEST to your real-life location for best results.

After that's done, go into your new resource group and press create, look for "Speech" under AI services, click create, punch in all the required info (name, resource group, etc), and again put the region that's closest to you. Be sure to select the Free F0 tier.

After that's done go into your new Speech Service and select Keys and Endpoint. Put your KEY 1 into wendigos.cfg in STT API KEY and put your Location/Region in Region (if using Azure). Also set your language with the correct language code (see `Wendigos.cfg` for more info).

## Real time responses (EXPERIMENTAL)
Allows the masked to reply to things players say in real time. This feature does NOT respect push-to-talk.

#### What you need
- An Elevenlabs subscription ($5 tier or better)
- A free Azure Speech to Text service OR you can use Elevenlabs for Speech to Text if you want to set up less
- A chat api key set (and credits purchased, if needed)

#### Elevenlabs
Create an account and subscribe to the $5 tier or better. Click on your profile and click "API Keys". Create a key and save it somewhere. 

- Optionally, you can clone your voice (and anyone else's who you are sharing the account with) in advance, then under Voices -> Personal click your voice, then click "ID" to copy the voice ID. Save that ID too.
    - Alternatively, you can choose not to do this and the mod will **automatically clone everyone's voices**. Note that autoclone tends to produce less convincing voice clones.

#### Final Realtime Setup Checklist
1. Ensure you set all api keys (Chat, STT, TTS), service providers for Chat and STT, and model name for Chat if you haven't already (and region under STT if using Azure).

2. Have everyone set their Elevenlabs voice ID if you cloned in advance

3. Set `Experimental -> Realtime Responses` to true in `Wendigos.cfg`.

4. Have everyone set their name in the `Experimental -> Your name` setting if you'd like the AI to know who is who.

## Bonus features
#### Optional configs
- `General -> Talk Probability`: how likely the Masked are to play recorded voice lines.
- `General -> Max voice clips`: the maximum number of voice recordings to store at a time.
- `Experimental -> Config sync prompt`: (host only) whether to show a config sync prompt when a new player joins the lobby. This will sync all relevant config options (Service selections, models, realtime enabled, **API KEYS!!!**, etc.
- `TTS -> Masked Voice Volume`: adjusts how loud the masked are.
- `TTS -> Voice Description`: provides a description of your voice to Elevenlabs to improve autoclone quality.
- `Chat -> Prompt`: the prompt provided to the chat service. Useful for improving quality of realtime responses.

#### Masked improvements
This mod removes the masked masks and zombie arms to better fool players. Player clothing is also mimicked.

## Possible issues
- STT crashes due to server issue
- Masked audio sounds choppy due to high network volatility

## FAQ
**What languages does the realtime mode support?**
- Elevenlabs Flash 2.5 (The TTS model this mod uses) supports 32 languages. Find out more here: [https://elevenlabs.io/languages](https://help.elevenlabs.io/hc/en-us/articles/13313366263441-What-languages-do-you-support)

**Can I use this with Mirage?**
- I wouldn't recommend it as this mod uses audio clip collection and playback.

**When is the mod listening to me?**
- The mod listens only during rounds. This enables realtime responses and transcribed clip collection.

**Can I share my Elevenlabs account?**
- Yes! You can have everyone on the same account. Everyone just has to set their own voice ID in their config.

**How did you add so many AI services to the mod?**
- I used my AI services framework [VoiceBox](https://github.com/TimShaw1/VoiceBox).

## TODO
  - [ ] Allow any enemy to clone voices

## Credits
- https://github.com/coqui-ai/tts
- RugbugRedfern's Skinwalkers mod
- @Kalthun and @notgarrett for helping me test this mod
- The Lethal Company Modding Discord

[![ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/Y8Y6ZWLYH)
