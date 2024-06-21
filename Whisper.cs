using System;
using System.Collections.Generic;
using System.Text;
using Whisper.net;
using System.IO;
using Whisper.net.Ggml;

namespace Wendigos
{
    internal class Whisper
    {
        public async void Process(string wavFileName)
        {
            using var whisperFactory = WhisperFactory.FromPath("ggml-base.bin");

            using var processor = whisperFactory.CreateBuilder()
                .WithLanguage("auto")
                .Build();

            using var fileStream = File.OpenRead(wavFileName);

            await foreach (var result in processor.ProcessAsync(fileStream))
            {
                Console.WriteLine($"{result.Start}->{result.End}: {result.Text}");
            }
        }

        public static string[] ModelNamesList =
        {
            "ggml-tiny.bin", "ggml-tiny.en.bin", "ggml-base.bin", "ggml-base.en.bin", "ggml-small.bin", 
            "ggml-small.en.bin", "ggml-medium.bin", "ggml-medium.en.bin", "Not supported", "Not supported", "Not supported"
        };

        public async void get_model(GgmlType model)
        {
            var modelName = ModelNamesList[(int)model];
            if (!File.Exists(modelName))
            {
                using var modelStream = await WhisperGgmlDownloader.GetGgmlModelAsync(GgmlType.Base);
                using var fileWriter = File.OpenWrite(modelName);
                await modelStream.CopyToAsync(fileWriter);
            }
        }
    }
}
