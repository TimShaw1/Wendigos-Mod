using System;
using System.Collections.Generic;
using System.Text;

namespace Wendigos
{
    using NAudio.Wave;
    using Newtonsoft.Json;
    using System.IO;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading.Tasks;
    using TimShaw.VoiceBox.Components;
    using TimShaw.VoiceBox.Modding;
    using TimShaw.VoiceBox.Data;
    using UnityEngine;
    using UnityEngine.Networking;

    static class ElevenLabs
    {

        const string baseDir = @".\"; // Base Directory of output file
        const string baseURL = "https://api.elevenlabs.io/v1/text-to-speech/"; // Base URL of HTTP request
        public static string VOICE_ID;
        public static float masked_volume = 0;

        public static Dictionary<string, TTSManager> ttsManagerComponents = new Dictionary<string, TTSManager>();
        public static void Init(string api_key, string voice_id, float volumeBoost)
        {
            try
            {
                if (voice_id == string.Empty) return;

                if (ttsManagerComponents.ContainsKey(voice_id))
                {
                    return;
                }

                Console.WriteLine($"[Wendigos TTS] Creating TTS manager object. Disregard \"Service config is null\" errors.");
                // Create a new GameObject and attach a TTSManager component to it
                GameObject ttsManager = new GameObject($"wendigosTtsManager{voice_id}");
                ttsManagerComponents.TryAdd(voice_id, ttsManager.AddComponent<TTSManager>());

                // Create an ElevenlabsServiceConfig object and choose a voiceId
                ElevenlabsTTSServiceConfig elevenlabsConfig = ModdingTools.CreateTTSServiceConfig<ElevenlabsTTSServiceConfig>();
                elevenlabsConfig.voiceId = voice_id;
                elevenlabsConfig.modelID = "eleven_flash_v2_5";

                if (api_key.Length == 0)
                {
                    Console.WriteLine($"[Wendigos TTS] No Elevenlabs API key found. Attempting to load from environment variable {elevenlabsConfig.apiKeyJSONString} ...");

                    // Configure the TTS manager with the elevenlabs config. 
                    // This also creates the TTS manager's TextToSpeechService via the ServiceFactory
                    ModdingTools.InitTTSManagerObject(ttsManagerComponents[voice_id], elevenlabsConfig);
                }
                else
                {
                    // Configure the TTS manager with the elevenlabs config. 
                    // This also creates the TTS manager's TextToSpeechService via the ServiceFactory
                    ModdingTools.InitTTSManagerObject(ttsManagerComponents[voice_id], elevenlabsConfig, ttsKey: api_key);
                }
                VOICE_ID = voice_id;


                masked_volume = volumeBoost;
                Console.WriteLine($"[Wendigos TTS] Created TTS manager.");
            }
            catch (Exception ex) 
            {
                Console.WriteLine(ex.ToString());
            }
        }

        public static void ConvertMp3ToWav(string _inPath_, string _outPath_)
        {
            using (Mp3FileReader mp3 = new Mp3FileReader(_inPath_))
            {
                using (WaveStream pcm = WaveFormatConversionStream.CreatePcmStream(mp3))
                {
                    WaveFileWriter.CreateWaveFile(_outPath_, pcm);
                }
            }
        }

        public static void IncreaseVolume(string inputPath, string outputPath, double db)
        {
            double linearScalingRatio = Math.Pow(10d, db / 10d);
            using (WaveFileReader reader = new WaveFileReader(inputPath))
            {
                VolumeWaveProvider16 volumeProvider = new VolumeWaveProvider16(reader);
                using (WaveFileWriter writer = new WaveFileWriter(outputPath, reader.WaveFormat))
                {
                    while (true)
                    {
                        var frame = reader.ReadNextSampleFrame();
                        if (frame == null)
                            break;
                        var sample = frame[0] * (float)linearScalingRatio;
                        if (sample < -0.6f)
                            sample = -0.6f;
                        if (sample > 0.6f)
                            sample = 0.6f;
                        writer.WriteSample(frame[0] * (float)linearScalingRatio);
                    }
                }
            }
        }

        // Requests WAV file containing AI Voice saying the prompt and outputs the directory to said file
        public static void RequestAudio(string prompt, string voiceID, string fileName, string dir, int fileNum, Action<string> onSuccess)
        {
            while (File.Exists(Path.Combine(dir, fileName + fileNum.ToString())))
            {
                fileNum++;
            }
            fileName = fileName + fileNum.ToString();
            string fullFilePath = null;
            ttsManagerComponents[voiceID].GenerateSpeechFileFromText(prompt, fileName, dir, result => {
                onSuccess.Invoke(dir + fileName + ".mp3");
                /*
                try
                {
                    ConvertMp3ToWav(dir + fileName + ".mp3", dir + fileName + "z.wav");
                    File.Delete(dir + fileName + ".mp3");
                    IncreaseVolume(dir + fileName + "z.wav", dir + fileName + ".wav", volume_boost);
                    File.Delete(dir + fileName + "z.wav");

                    fullFilePath = dir + fileName + ".wav";
                    onSuccess.Invoke(fullFilePath);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    onSuccess.Invoke("");
                }
                */
            }, err => Debug.LogError(err));

        }

        public static void StreamAudio(string prompt, string voiceID, AudioStreamer audioStreamer)
        {

            audioStreamer.StopStreaming(ttsManagerComponents[voiceID].TextToSpeechService);
            audioStreamer.GetComponent<AudioSource>().volume = masked_volume > 1f ? 1f : masked_volume;
            Console.WriteLine((ttsManagerComponents[voiceID].textToSpeechConfig as ElevenlabsTTSServiceConfig).voiceId);
            ttsManagerComponents[voiceID].RequestAudioAndStream(prompt, audioStreamer);
        }

        public static void StreamAudioChunk(string promptChunk, string voiceID, bool isFinalSegment, AudioStreamer audioStreamer)
        {
            audioStreamer.GetComponent<AudioSource>().volume = masked_volume > 1f ? 1f : masked_volume;
            Console.WriteLine((ttsManagerComponents[voiceID].textToSpeechConfig as ElevenlabsTTSServiceConfig).voiceId);
            ttsManagerComponents[voiceID].RequestAudioAndStream(promptChunk, isFinalSegment, audioStreamer);
        }

    }
}
