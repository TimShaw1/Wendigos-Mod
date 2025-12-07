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

        public struct TrimConfig
        {
            public float OpenThreshold;      // Energy needed to START a sentence (e.g., 0.05)
            public float CloseThreshold;     // Energy needed to KEEP a sentence going (e.g., 0.015)
            public float MinSpeechSeconds;   // Reject sounds shorter than this (e.g., 0.2s removes clicks)
            public float PadSeconds;         // Buffer to keep before/after (e.g., 0.2s)

            public TrimConfig(float openThreshold, float closeThreshold, float minSpeechSeconds, float padSeconds)
            {
                OpenThreshold = openThreshold;
                CloseThreshold = closeThreshold;
                MinSpeechSeconds = minSpeechSeconds;
                PadSeconds = padSeconds;
            }
        }

        public static byte[] TrimSpeechToMp3(AudioClip clip, TrimConfig config)
        {
            float[] samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);

            int channels = clip.channels;
            int freq = clip.frequency;

            // 1. Map out all valid "Speech Segments"
            List<Vector2Int> speechSegments = DetectSpeechSegments(samples, channels, freq, config);

            if (speechSegments.Count == 0)
            {
                Debug.LogWarning("No speech detected that met the duration criteria.");
                return new byte[0];
            }

            // 2. Determine the outer bounds
            // Start of the first valid segment -> End of the last valid segment
            int startSample = speechSegments[0].x;
            int endSample = speechSegments[speechSegments.Count - 1].y;

            // 3. Apply Padding
            int padSamples = (int)(config.PadSeconds * freq * channels);
            int finalStart = Mathf.Max(0, startSample - padSamples);
            int finalEnd = Mathf.Min(samples.Length, endSample + padSamples);

            // Align to stereo channels to prevent phase issues
            finalStart -= finalStart % channels;
            finalEnd -= finalEnd % channels;

            // 4. Encode
            return EncodeToMp3(samples, finalStart, finalEnd - finalStart, channels, freq);
        }

        private static List<Vector2Int> DetectSpeechSegments(float[] samples, int channels, int freq, TrimConfig config)
        {
            List<Vector2Int> validSegments = new List<Vector2Int>();

            int windowSize = freq / 50; // 20ms windows
            bool isSpeech = false;
            int currentStartIndex = 0;

            // Temporary tracking
            int lastLoudIndex = 0;

            for (int i = 0; i < samples.Length; i += windowSize * channels)
            {
                // Calculate RMS for this window
                float rms = CalculateRMS(samples, i, windowSize * channels);

                if (!isSpeech)
                {
                    // GATE OPEN: We are looking for a strong start (High Threshold)
                    if (rms > config.OpenThreshold)
                    {
                        isSpeech = true;
                        currentStartIndex = i;
                        lastLoudIndex = i;
                    }
                }
                else
                {
                    // GATE CLOSED: We stay in speech as long as we are above the Low Threshold
                    if (rms > config.CloseThreshold)
                    {
                        lastLoudIndex = i; // Extend the current segment
                    }
                    else
                    {
                        // We hit silence. Is the segment finished?
                        // To avoid cutting mid-sentence pauses, we usually check if silence has persisted.
                        // For simplicity in "trimming", we just check if we've drifted too far from the last loud sound.
                        // (Here we treat the segment as ended immediately upon dropping below LowThreshold for simplicity,
                        // relying on the PadSeconds to catch the breath).

                        isSpeech = false;

                        // CHECK DURATION: Was this just a keyboard click?
                        // We calculate length based on where the sound actually *stopped* (lastLoudIndex)
                        int segmentLength = lastLoudIndex - currentStartIndex;
                        float durationSeconds = (float)segmentLength / channels / freq;

                        if (durationSeconds > config.MinSpeechSeconds)
                        {
                            validSegments.Add(new Vector2Int(currentStartIndex, lastLoudIndex));
                        }
                    }
                }
            }

            // Handle case where clip ends while speaking
            if (isSpeech)
            {
                int segmentLength = lastLoudIndex - currentStartIndex;
                if (((float)segmentLength / channels / freq) > config.MinSpeechSeconds)
                {
                    validSegments.Add(new Vector2Int(currentStartIndex, lastLoudIndex));
                }
            }

            return validSegments;
        }

        private static float CalculateRMS(float[] samples, int offset, int length)
        {
            float sum = 0;
            int limit = Mathf.Min(offset + length, samples.Length);
            int count = 0;
            for (int i = offset; i < limit; i++)
            {
                sum += samples[i] * samples[i];
                count++;
            }
            return Mathf.Sqrt(sum / (count > 0 ? count : 1));
        }

        // Standard encoding logic (same as previous examples)
        private static byte[] EncodeToMp3(float[] allSamples, int start, int length, int channels, int freq)
        {
            byte[] pcmData = new byte[length * 2];
            int pcmIndex = 0;

            for (int i = 0; i < length; i++)
            {
                float sample = Mathf.Clamp(allSamples[start + i], -1f, 1f);
                short shortSample = (short)(sample * short.MaxValue);

                pcmData[pcmIndex++] = (byte)(shortSample & 0xFF);
                pcmData[pcmIndex++] = (byte)((shortSample >> 8) & 0xFF);
            }

            var format = new WaveFormat(freq, 16, channels);
            using (var ms = new MemoryStream())
            using (var writer = new LameMP3FileWriter(ms, format, LAMEPreset.STANDARD))
            {
                writer.Write(pcmData, 0, pcmData.Length);
                return ms.ToArray();
            }
        }

        public static TrimConfig AnalyzeAudioLevels(AudioClip clip)
        {
            float[] samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);

            float maxRMS = 0f;
            float avgRMS = 0f;
            float minRMS = 1f;

            int window = clip.frequency / 50;
            int windowsCount = 0;

            for (int i = 0; i < samples.Length; i += window * clip.channels)
            {
                float sum = 0;
                int limit = Mathf.Min(i + window, samples.Length);
                for (int j = i; j < limit; j++) sum += samples[j] * samples[j];

                float rms = Mathf.Sqrt(sum / (limit - i));

                if (rms > maxRMS) maxRMS = rms;
                if (rms < minRMS) minRMS = rms;
                avgRMS += rms;
                windowsCount++;
            }
            avgRMS /= windowsCount;

            return new TrimConfig(maxRMS * 0.10f, minRMS + 0.001f, 0.2f, 2f);
        }

        #region MULTI_SPLIT
        /// <summary>
        /// Splits an AudioClip into multiple MP3 byte arrays based on silence.
        /// Automatically trims silence from the start and end of each segment.
        /// </summary>
        /// <param name="clip">The source AudioClip.</param>
        /// <param name="silenceThreshold">Amplitude (0-1) below which is considered silence.</param>
        /// <param name="minSilenceSeconds">How many seconds of silence triggers a split.</param>
        public static List<byte[]> SplitAndTrimToMp3(AudioClip clip, float silenceThreshold = 0.01f, float minSilenceSeconds = 0.5f)
        {
            List<byte[]> resultMp3s = new List<byte[]>();

            // 1. Get raw float data (Interleaved: L, R, L, R...)
            float[] samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);

            int channels = clip.channels;
            int frequency = clip.frequency;

            // Calculate how many samples (frames) represent the minimum silence duration
            // We multiply by channels because the array is flattened.
            int minSilenceSamples = (int)(minSilenceSeconds * frequency) * channels;

            // 2. Find Segments (Islands of Sound)
            List<AudioSegment> segments = FindAudioSegments(samples, channels, silenceThreshold, minSilenceSamples);

            // 3. Process each segment independently
            foreach (var seg in segments)
            {
                byte[] mp3Data = EncodeSegmentToMp3(samples, seg.StartIndex, seg.Length, channels, frequency);
                resultMp3s.Add(mp3Data);
            }

            return resultMp3s;
        }

        private struct AudioSegment
        {
            public int StartIndex;
            public int Length;
        }

        private static List<AudioSegment> FindAudioSegments(float[] samples, int channels, float threshold, int minSilenceSamples)
        {
            var segments = new List<AudioSegment>();

            bool isRecording = false;
            int startIndex = 0;
            int silenceCounter = 0;
            int lastSoundIndex = 0;

            // Iterate by frame (step by channel count to keep L/R samples together)
            for (int i = 0; i < samples.Length; i += channels)
            {
                // Check volume of current frame (max of all channels)
                float currentFrameVol = 0f;
                for (int c = 0; c < channels; c++)
                {
                    if (i + c < samples.Length)
                        currentFrameVol = Mathf.Max(currentFrameVol, Mathf.Abs(samples[i + c]));
                }

                if (currentFrameVol > threshold)
                {
                    // -- LOUD FRAME --

                    if (!isRecording)
                    {
                        // Start of a new segment
                        isRecording = true;
                        startIndex = i;
                    }

                    // Reset silence counter because we heard sound
                    silenceCounter = 0;

                    // Track where the last sound was (to trim tail silence later)
                    lastSoundIndex = i;
                }
                else
                {
                    // -- SILENT FRAME --

                    if (isRecording)
                    {
                        silenceCounter += channels;

                        // If silence exceeds limit, cut the segment
                        if (silenceCounter >= minSilenceSamples)
                        {
                            // Calculate length based on the Last Sound Index, effectively trimming the tail silence
                            int length = (lastSoundIndex - startIndex) + channels; // +channels to include the final frame

                            segments.Add(new AudioSegment { StartIndex = startIndex, Length = length });

                            isRecording = false;
                            silenceCounter = 0;
                        }
                    }
                }
            }

            // Handle case where clip ends while still recording (add the final segment)
            if (isRecording)
            {
                int length = (lastSoundIndex - startIndex) + channels;
                segments.Add(new AudioSegment { StartIndex = startIndex, Length = length });
            }

            return segments;
        }

        private static byte[] EncodeSegmentToMp3(float[] allSamples, int start, int length, int channels, int freq)
        {
            // Convert just this segment of floats to PCM bytes
            byte[] pcmData = new byte[length * 2];
            int pcmIndex = 0;

            for (int i = 0; i < length; i++)
            {
                // Safety check for array bounds
                if (start + i >= allSamples.Length) break;

                float sample = Mathf.Clamp(allSamples[start + i], -1f, 1f);
                short shortSample = (short)(sample * short.MaxValue);

                pcmData[pcmIndex++] = (byte)(shortSample & 0xFF);
                pcmData[pcmIndex++] = (byte)((shortSample >> 8) & 0xFF);
            }

            // Encode to MP3
            var format = new WaveFormat(freq, 16, channels);
            using (var ms = new MemoryStream())
            using (var writer = new LameMP3FileWriter(ms, format, LAMEPreset.STANDARD))
            {
                writer.Write(pcmData, 0, pcmData.Length);
                writer.Flush();
                return ms.ToArray();
            }
        }
        #endregion
    }
}
