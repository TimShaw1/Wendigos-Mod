using System;
using System.Collections.Generic;
using System.Text;
using Whisper.net;
using System.IO;
using Whisper.net.Ggml;

namespace Wendigos
{
    public static class WhisperManager
    {
        public static async void ProcessFile(string wavFileName)
        {
            using var whisperFactory = WhisperFactory.FromPath("ggml-small.en.bin");

            using var processor = whisperFactory.CreateBuilder()
                .WithLanguage("auto")
                .Build();

            using var fileStream = File.OpenRead(wavFileName);

            await foreach (var result in processor.ProcessAsync(fileStream))
            {
                Console.WriteLine($"{result.Start}->{result.End}: {result.Text}");
            }
        }

        public static async void ProcessBuffer(byte[] buffer)
        {
            //get_model(GgmlType.SmallEn);

            
            using var whisperFactory = WhisperFactory.FromPath("ggml-small.en.bin");

            
            using var processor = whisperFactory.CreateBuilder()
                .WithLanguage("auto")
                .Build();

            using var audioStream = new MemoryStream(buffer);

            await foreach (var result in processor.ProcessAsync(audioStream))
            {
                Console.WriteLine($"{result.Start}->{result.End}: {result.Text}");
            }
            
        }

        public static string[] ModelNamesList =
        {
            "ggml-tiny.bin", "ggml-tiny.en.bin", "ggml-base.bin", "ggml-base.en.bin", "ggml-small.bin", 
            "ggml-small.en.bin", "ggml-medium.bin", "ggml-medium.en.bin", "Not supported", "Not supported", "Not supported"
        };

        public static async void get_model(GgmlType model)
        {
            var modelName = ModelNamesList[(int)model];
            if (!File.Exists(modelName))
            {
                using var modelStream = await WhisperGgmlDownloader.GetGgmlModelAsync(model);
                using var fileWriter = File.OpenWrite(modelName);
                await modelStream.CopyToAsync(fileWriter);
            }
        }
    }
}
