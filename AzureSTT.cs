using System;
using System.Collections.Generic;
using System.Text;
using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.CognitiveServices.Speech;
using Microsoft.CognitiveServices.Speech.Audio;
using System.Data;
using NAudio.CoreAudioApi;
using Unity.Netcode;

namespace Wendigos
{
    class AzureSTT
    {
        public static int num_gens = 0;
        public static bool is_init = false;
        public static string ChatGPT_System_Prompt = "You are playing the online game Lethal Company with friends. When someone speaks to you, reply with short and informal responses.";
        public static SpeechRecognizer speechRecognizer;
        public static string player_name = "";
        async static Task FromMic(SpeechConfig speechConfig)
        {
            try
            {
                Console.WriteLine("Wendigos: Start transcribing audio...");
                using var audioConfig = AudioConfig.FromDefaultMicrophoneInput();
                speechRecognizer = new SpeechRecognizer(speechConfig, audioConfig);

                var stopRecognition = new TaskCompletionSource<int>();

                speechRecognizer.Recognizing += (s, e) =>
                {
                    //Console.WriteLine($"RECOGNIZING: Text={e.Result.Text}");
                };

                speechRecognizer.Recognized += (s, e) =>
                {
                    if (e.Result.Reason == ResultReason.RecognizedSpeech)
                    {
                        if (e.Result.Text.Length > 1)
                        {
                            Console.WriteLine($"RECOGNIZED: Text={e.Result.Text}");
                            var closest_masked = Plugin.GetClosestMasked();
                            if (closest_masked == null || closest_masked.creatureVoice.isPlaying)
                                return;
                            try
                            {
                                if (!ChatManager.init_success) return;

                                var response = ChatManager.SendPromptToChatGPT(ChatGPT_System_Prompt + (player_name == "" ? "\n" : "\n" + player_name + ": ")  + e.Result.Text);
                                Console.WriteLine("RESPONSE: " + response);

                                var masked_id = closest_masked.GetComponent<Plugin.MaskedEnemyIdentifier>().id;
                                string voice_id;
                                try
                                {
                                    var client = Plugin.sharedMaskedClientDict[masked_id];
                                    voice_id = Plugin.clientVoiceIDLookup[client];
                                }
                                catch
                                {
                                    voice_id = ElevenLabs.VOICE_ID;
                                }

                                
                                // Overlap handled in this function
                                var t = ElevenLabs.RequestAudio(response, voice_id, voice_id, Plugin.assembly_path + "\\temp_elevenlabs_lines\\", 0);
                                t.Wait();

                                // Have client-voiceid lookup somehow
                                var new_clip = Plugin.LoadAudioFile(t.Result);
                                new_clip.name = "" + Convert.ToChar(NetworkManager.Singleton.LocalClientId + 33) + num_gens;
                                num_gens++;
                                
                                Plugin.SendClipForMe(new_clip, masked_id);
                                //closest_masked.creatureVoice.PlayOneShot(new_clip);
                                Console.WriteLine("ROUND TRIP DONE");
                                File.Delete(t.Result);

                                // Have closest masked to player play the new audio file

                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"GETRESPONSE BROKE: {ex.ToString()}");
                            }
                        }
                    }
                    else if (e.Result.Reason == ResultReason.NoMatch)
                    {
                        Console.WriteLine($"NOMATCH: Speech could not be recognized.");
                    }
                };

                speechRecognizer.Canceled += (s, e) =>
                {
                    Console.WriteLine($"CANCELED: Reason={e.Reason}");

                    if (e.Reason == CancellationReason.Error)
                    {
                        Console.WriteLine($"CANCELED: ErrorCode={e.ErrorCode}");
                        Console.WriteLine($"CANCELED: ErrorDetails={e.ErrorDetails}");
                        Console.WriteLine($"CANCELED: Did you set the speech resource key and region values?");
                    }

                    stopRecognition.TrySetResult(0);
                };

                speechRecognizer.SessionStopped += (s, e) =>
                {
                    Console.WriteLine("\n    Session stopped event.");
                    stopRecognition.TrySetResult(0);
                };

                await speechRecognizer.StartContinuousRecognitionAsync();

                Task.WaitAny(new[] { stopRecognition.Task });

                //await speechRecognizer.StopContinuousRecognitionAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine("STT BROKE");
                Console.WriteLine(ex.ToString());
            }
        }

        public async static Task Main(string api_key)
        {
            is_init = true;
            if (api_key.Length == 0)
            {
                Console.WriteLine("No azure API key. STT disabled.");
                return;
            }

            //Console.WriteLine("IN MAIN");
            try
            {
                var speechConfig = SpeechConfig.FromSubscription(api_key, "canadacentral");
                await FromMic(speechConfig);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        static void GetAudioDevices(string[] args)
        {
            var enumerator = new MMDeviceEnumerator();
            foreach (var endpoint in
            enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
            {
                Console.WriteLine("{0} ({1})", endpoint.FriendlyName, endpoint.ID);
            }
        }
    }
}
