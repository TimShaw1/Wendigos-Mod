using NAudio.Lame;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace Wendigos
{
    public static class AudioUtils
    {
        // Copies settings from one AudioSource to another
        public static void CopyTo(this AudioSource original, AudioSource destination)
        {
            destination.volume = original.volume;
            destination.pitch = original.pitch;
            destination.spatialBlend = original.spatialBlend; // Crucial for 3D
            destination.dopplerLevel = original.dopplerLevel;
            destination.spread = original.spread;
            destination.rolloffMode = original.rolloffMode;
            destination.minDistance = original.minDistance;
            destination.maxDistance = original.maxDistance;
            destination.priority = original.priority;

            // Very Important: Copy the Mixer Group so it goes through the same logic
            destination.outputAudioMixerGroup = original.outputAudioMixerGroup;

            // Copy Bypass settings
            destination.bypassEffects = original.bypassEffects;
            destination.bypassListenerEffects = original.bypassListenerEffects;
            destination.bypassReverbZones = original.bypassReverbZones;
        }

        public static void CopyOcclusion(this OccludeAudio original, GameObject destination)
        {
            if (original == null) return;

            // 1. Add the component to the new child object
            // (This will automatically check for the AudioSource we added earlier)
            OccludeAudio newOcclusion = destination.AddComponent<OccludeAudio>();

            // 2. Copy the PUBLIC settings only
            // We do NOT copy private fields (like lowPassFilter) because the 
            // new component's Start() method needs to create its own fresh references.
            newOcclusion.useReverb = original.useReverb;
            newOcclusion.overridingLowPass = original.overridingLowPass;
            newOcclusion.lowPassOverride = original.lowPassOverride;
            newOcclusion.debugLog = original.debugLog;
        }

        public static void AudioClipToMp3File(AudioClip clip, string name, string path)
        {
            // 1. Check and sanitize the filename
            // If the name ends with .mp3 (case insensitive), strip it to avoid "name.mp3.mp3"
            if (name.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
            {
                name = name.Substring(0, name.Length - 4);
            }

            string fullPath = Path.Combine(path, name + ".mp3");

            // 2. Extract raw float data from the AudioClip
            // AudioClip data ranges from -1.0f to 1.0f
            float[] samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);

            // 3. Convert Float samples to 16-bit PCM bytes
            // MP3 encoders usually expect 16-bit Integer PCM data
            byte[] pcmData = new byte[samples.Length * 2]; // 2 bytes per short
            int pcmIndex = 0;

            for (int i = 0; i < samples.Length; i++)
            {
                // Clamp value to ensure it fits within short range
                float sample = Mathf.Clamp(samples[i], -1f, 1f);

                // Scale float (-1 to 1) to short (-32768 to 32767)
                short shortSample = (short)(sample * short.MaxValue);

                // Convert short to bytes (Little Endian)
                pcmData[pcmIndex++] = (byte)(shortSample & 0xFF);
                pcmData[pcmIndex++] = (byte)((shortSample >> 8) & 0xFF);
            }

            // 4. Encode and Write to File
            var format = new WaveFormat(clip.frequency, 16, clip.channels);

            using (var ms = new MemoryStream(pcmData))
            using (var reader = new RawSourceWaveStream(ms, format))
            using (var writer = new LameMP3FileWriter(fullPath, format, LAMEPreset.STANDARD))
            {
                reader.CopyTo(writer);
            }
        }

        public static byte[] AudioClipToMp3Data(AudioClip clip)
        {
            // 1. Convert Float samples to 16-bit PCM bytes (Same as before)
            float[] samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);

            byte[] pcmData = new byte[samples.Length * 2];
            int pcmIndex = 0;

            for (int i = 0; i < samples.Length; i++)
            {
                float sample = Mathf.Clamp(samples[i], -1f, 1f);
                short shortSample = (short)(sample * short.MaxValue);
                pcmData[pcmIndex++] = (byte)(shortSample & 0xFF);
                pcmData[pcmIndex++] = (byte)((shortSample >> 8) & 0xFF);
            }

            // 2. Encode to MP3 in Memory
            var format = new WaveFormat(clip.frequency, 16, clip.channels);

            using (var outputStream = new MemoryStream())
            {
                // Initialize the writer to write into the 'outputStream' memory buffer
                using (var writer = new LameMP3FileWriter(outputStream, format, LAMEPreset.STANDARD))
                {
                    writer.Write(pcmData, 0, pcmData.Length);
                }

                // Writer is disposed above, ensuring all MP3 frames are flushed to the stream.
                // Now we return the byte array containing the full valid MP3 file structure.
                return outputStream.ToArray();
            }
        }
    }
}
