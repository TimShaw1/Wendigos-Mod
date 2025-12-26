using NAudio.Lame;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Profiling;

namespace Wendigos
{
    // Source - https://stackoverflow.com/a
    // Posted by Sarin Vadakkey Thayyil
    // Retrieved 2025-12-07, License - CC BY-SA 3.0

    public class RollingRecorder : IDisposable
    {
        private WaveInEvent sourceStream;
        private WaveFormat waveFormat;

        // Circular Buffer Variables
        private byte[] audioBuffer;
        private int bufferWriteHead;
        private int bufferLength;
        private bool isBufferFull;

        // Time tracking to map DateTime to buffer position
        private DateTime recordingStartTime;
        private long totalBytesWritten;

        private readonly int MaxDurationSeconds = 60;

        public RollingRecorder()
        {
            
        }

        /// <summary>
        /// Starts recording from the device matching the provided Unity microphone name.
        /// </summary>
        public void StartRecording(string unityMicName)
        {
            StopRecording(); // Safety cleanup

            waveFormat = new WaveFormat(AudioSettings.outputSampleRate, 16, 1);
            int deviceIndex = GetDeviceIndexByName(unityMicName);

            // Calculate buffer size: BytesPerSecond * Seconds
            bufferLength = waveFormat.AverageBytesPerSecond * MaxDurationSeconds;
            audioBuffer = new byte[bufferLength];

            sourceStream = new WaveInEvent
            {
                DeviceNumber = deviceIndex,
                WaveFormat = waveFormat,
                BufferMilliseconds = 50 // Lower latency
            };

            sourceStream.DataAvailable += SourceStreamDataAvailable;

            // Reset state
            bufferWriteHead = 0;
            totalBytesWritten = 0;
            isBufferFull = false;
            recordingStartTime = DateTime.Now; // Anchor time

            sourceStream.StartRecording();
        }

        private void SourceStreamDataAvailable(object sender, WaveInEventArgs e)
        {
            // Write incoming bytes into the circular buffer
            for (int i = 0; i < e.BytesRecorded; i++)
            {
                audioBuffer[bufferWriteHead] = e.Buffer[i];
                bufferWriteHead++;
                totalBytesWritten++;

                // Wrap around
                if (bufferWriteHead >= bufferLength)
                {
                    bufferWriteHead = 0;
                    isBufferFull = true;
                }
            }
        }

        /// <summary>
        /// Extracts a segment of audio as an MP3 byte array.
        /// </summary>
        public byte[] GetSegment(DateTime speechAbsStart, TimeSpan duration)
        {
            if (sourceStream == null) return null;

            // --- CONFIGURABLE PADDING ---
            double paddingStartSeconds = 0.5; // Grab 0.5s before speech started
            double paddingEndSeconds = 0.5;   // Grab 0.5s after speech ended
                                              // ----------------------------

            // 1. Apply Padding adjustments
            // Move start time back (earlier)
            DateTime adjustedStart = speechAbsStart.AddSeconds(-paddingStartSeconds);

            // Increase total duration to cover the shift + the extra tail
            TimeSpan adjustedDuration = duration.Add(TimeSpan.FromSeconds(paddingStartSeconds + paddingEndSeconds));

            // 2. Calculate offsets
            TimeSpan timeSinceStart = DateTime.Now - adjustedStart;

            // Sanity check: If we are asking for data from the future, abort
            if (timeSinceStart.TotalSeconds < 0) return null;

            int bytesAgo = (int)(timeSinceStart.TotalSeconds * waveFormat.AverageBytesPerSecond);
            int bytesToRead = (int)(adjustedDuration.TotalSeconds * waveFormat.AverageBytesPerSecond);

            // 3. Safety Clamps
            // Ensure we don't try to read more than the entire buffer size
            if (bytesToRead > bufferLength) bytesToRead = bufferLength;

            // 4. Alignment Fix (From previous step)
            bytesAgo -= (bytesAgo % waveFormat.BlockAlign);
            bytesToRead -= (bytesToRead % waveFormat.BlockAlign);

            // 5. Calculate start index
            int startIndex = bufferWriteHead - bytesAgo;

            // Handle loop wrap-around logic for negative indices
            while (startIndex < 0) startIndex += bufferLength;

            byte[] rawPcmChunk = new byte[bytesToRead];

            // 6. Copy Data (Handling wrap-around)
            if (startIndex + bytesToRead <= bufferLength)
            {
                Buffer.BlockCopy(audioBuffer, startIndex, rawPcmChunk, 0, bytesToRead);
            }
            else
            {
                int firstChunkSize = bufferLength - startIndex;
                int secondChunkSize = bytesToRead - firstChunkSize;
                Buffer.BlockCopy(audioBuffer, startIndex, rawPcmChunk, 0, firstChunkSize);
                Buffer.BlockCopy(audioBuffer, 0, rawPcmChunk, firstChunkSize, secondChunkSize);
            }

            // 7. Encode to MP3
            using (var retMs = new MemoryStream())
            using (var wtr = new LameMP3FileWriter(retMs, waveFormat, LAMEPreset.STANDARD))
            {
                wtr.Write(rawPcmChunk, 0, rawPcmChunk.Length);
                wtr.Flush();
                return retMs.ToArray();
            }
        }

        public void StopRecording()
        {
            if (sourceStream != null)
            {
                sourceStream.StopRecording();
                sourceStream.Dispose();
                sourceStream = null;
            }
        }

        public void Dispose()
        {
            StopRecording();
        }

        /// <summary>
        /// Helper to match Unity device name to NAudio ID.
        /// NAudio cuts names off at 31 chars, so we use `StartsWith`.
        /// </summary>
        private int GetDeviceIndexByName(string unityMicName)
        {
            int deviceCount = WaveIn.DeviceCount;
            for (int i = 0; i < deviceCount; i++)
            {
                var caps = WaveIn.GetCapabilities(i);
                // Unity names are often full, NAudio (WinMM) are often truncated.
                // Check if one contains the other.
                if (unityMicName.StartsWith(caps.ProductName) || caps.ProductName.StartsWith(unityMicName))
                {
                    return i;
                }
            }
            // Fallback to default if not found
            return 0;
        }
    }

}
