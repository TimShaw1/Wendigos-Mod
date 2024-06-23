using System;
using System.Collections.Generic;
using System.Text;

namespace Wendigos
{
    using Newtonsoft.Json;
    using System.IO;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading.Tasks;

    class ElevenLabs
    {

        const string baseDir = @".\"; // Base Directory of output file
        const string baseURL = "https://api.elevenlabs.io/v1/text-to-speech/"; // Base URL of HTTP request
        public static string API_KEY; // Eleven Labs API key
        public static bool requesting = false;
        static HttpClient client;
        public static string VOICE_ID;
        public static void Init(string api_key, string voice_id)
        {
            API_KEY = api_key;
            VOICE_ID = voice_id;
            client = new HttpClient();

            client.DefaultRequestHeaders.Add("xi-api-key", API_KEY); // Add API Key header
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("audio/mpeg")); // Add accepted file extension header
        }

        // Requests WAV file containing AI Voice saying the prompt and outputs the directory to said file
        public static async Task<string> RequestAudio(string prompt, string voice, string fileName, string dir, int fileNum)
        {
            string url = baseURL + voice; // Concatenate Voice ID to end of URL

            var data = new
            {
                text = prompt,
                model_id = "eleven_multilingual_v2",
                voice_settings = new
                {
                    stability = 0.5f,
                    similarity_boost = 0.5f,
                    style = 0.3f,
                    use_speaker_boost = true
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
                    return dir + fileName + fileNameExtension.ToString() + ".mp3";
            }

            // Return Directory to file
            return null;

        }

    }
}
