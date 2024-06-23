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

namespace Wendigos
{
    

    class AzureSTT
    {
        public static bool is_init = false;
        public static string ChatGPT_System_Prompt = "You are playing the online game Lethal Company with friends. When someone speaks to you, reply with short and informal responses.";
        async static Task FromMic(SpeechConfig speechConfig)
        {
            try
            {
                Console.WriteLine("Wendigos: Start transcribing audio...");
                using var audioConfig = AudioConfig.FromDefaultMicrophoneInput();
                using var speechRecognizer = new SpeechRecognizer(speechConfig, audioConfig);

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
                            try
                            {
                                var response = ChatManager.SendPromptToChatGPT(ChatGPT_System_Prompt + "\nTim: " + e.Result.Text);
                                Console.WriteLine("RESPONSE: " + response);

                                // Overlap handled in this function
                                var t = ElevenLabs.RequestAudio(response, ElevenLabs.VOICE_ID, ElevenLabs.VOICE_ID, Plugin.assembly_path + "\\temp_elevenlabs_lines", 0);
                                t.Wait();
                                var new_clip = Plugin.LoadAudioFile(t.Result);
                                Console.WriteLine("ROUND TRIP DONE");

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
