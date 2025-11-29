using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Wendigos
{
    public static class AudioExtensions
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
    }
}
