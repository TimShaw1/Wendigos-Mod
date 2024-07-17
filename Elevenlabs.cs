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
    using UnityEngine;
    using UnityEngine.Networking;

    static class ElevenLabs
    {

        const string baseDir = @".\"; // Base Directory of output file
        const string baseURL = "https://api.elevenlabs.io/v1/text-to-speech/"; // Base URL of HTTP request
        public static string API_KEY; // Eleven Labs API key
        public static bool requesting = false;
        static HttpClient client;
        public static string VOICE_ID;
        public static bool optimize_for_speed = false;
        public static float volume_boost = 0;
        public static void Init(string api_key, string voice_id, float volumeBoost)
        {
            try
            {
                if (api_key.Length == 0)
                {
                    Console.WriteLine("No Elevenlabs API key found.");
                    return;
                }
           
                API_KEY = api_key;
                VOICE_ID = voice_id;
                client = new HttpClient();

                client.DefaultRequestHeaders.Add("xi-api-key", API_KEY); // Add API Key header
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("audio/mpeg")); // Add accepted file extension header

                volume_boost = volumeBoost;
            }
            catch (Exception ex) 
            {
                Console.WriteLine(ex.ToString());
            }
        }

        public static async Task<string> GetLatestHistoryItem(string voice_id)
        {
            var history = await client.GetAsync($"https://api.elevenlabs.io/v1/history?page_size=1&voice_id={voice_id}");
            dynamic t = JsonConvert.DeserializeObject(await history.Content.ReadAsStringAsync());
            try
            {
                Console.WriteLine(t["history"][0]["history_item_id"]);
                return t["history"][0]["history_item_id"];
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
            return "";
        }

        public static async Task<AudioClip> GetLatestHistoryItemAudioClip(string history_item_id, string dir)
        {
            var data = new
            {
                history_item_id = history_item_id
            }; // Set-up Data

            string json = JsonConvert.SerializeObject(data);
            StringContent httpContent = new StringContent(json, System.Text.Encoding.Default, "application/json");

            var response = await client.PostAsync($"https://api.elevenlabs.io/v1/history/{history_item_id}/audio", httpContent);

            using (Stream stream = await response.Content.ReadAsStreamAsync())
            using (FileStream fileStream = File.Create(dir + history_item_id + ".mp3"))
            {
                await stream.CopyToAsync(fileStream);
            }

            return Plugin.LoadAudioFile(dir + history_item_id + ".mp3");
        }

        private static void ConvertMp3ToWav(string _inPath_, string _outPath_)
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
                        writer.WriteSample(frame[0] * (float)linearScalingRatio);
                    }
                }
            }
        }

        // Requests WAV file containing AI Voice saying the prompt and outputs the directory to said file
        public static async Task<string> RequestAudio(string prompt, string voice, string fileName, string dir, int fileNum)
        {
            string url = baseURL + voice; // Concatenate Voice ID to end of URL

            var data = new
            {
                text = prompt,
                model_id = optimize_for_speed ? "eleven_turbo_v2" : "eleven_multilingual_v2",
                voice_settings = new
                {
                    stability = 0.5f,
                    similarity_boost = 0.5f,
                    style = optimize_for_speed ? 0.0f : 0.3f,
                    use_speaker_boost = true,
                    optimize_streaming_latency = optimize_for_speed ? 3 : 0
                }
            }; // Set-up Data

            // Convert Data to JSON
            string json = JsonConvert.SerializeObject(data);
            StringContent httpContent = new StringContent(json, System.Text.Encoding.Default, "application/json");

            // Request MPEG
            var response = await client.PostAsync(url, httpContent);
            requesting = false;

            // Output Response to local MPEG file in the respective directory
            if (response != null)
            {
                int fileNameExtension = fileNum;
                int retries = 0;
                bool fileNameValid = false;

                while (!fileNameValid)
                {
                    try
                    {
                        // Stream response as binary data into a file
                        using (Stream stream = await response.Content.ReadAsStreamAsync())
                        using (FileStream fileStream = File.Create(dir + fileName + fileNameExtension.ToString() + ".mp3"))
                        {
                            await stream.CopyToAsync(fileStream);
                        }

                        fileNameValid = true;

                    }
                    catch (Exception ex)
                    {
                        retries++;
                        if (retries >= 50)
                            break;
                        fileNameExtension++;
                    }
                }

                if (fileNameValid)
                {
                    try
                    {
                        ConvertMp3ToWav(dir + fileName + fileNameExtension.ToString() + ".mp3", dir + fileName + fileNameExtension.ToString() + "z.wav");
                        File.Delete(dir + fileName + fileNameExtension.ToString() + ".mp3");
                        IncreaseVolume(dir + fileName + fileNameExtension.ToString() + "z.wav", dir + fileName + fileNameExtension.ToString() + ".wav", volume_boost);
                        File.Delete(dir + fileName + fileNameExtension.ToString() + "z.wav");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.ToString());
                    }
                    return dir + fileName + fileNameExtension.ToString() + ".wav";
                }
            }

            // Return Directory to file
            return null;

        }

    }
}
