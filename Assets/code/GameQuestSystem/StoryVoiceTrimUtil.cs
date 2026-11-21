using System.Collections.Generic;
using UnityEngine;

namespace GameQuestSystem
{
    public readonly struct VoiceTrimBounds
    {
        public readonly float StartSeconds;
        public readonly float EndSeconds;

        public VoiceTrimBounds(float startSeconds, float endSeconds)
        {
            StartSeconds = startSeconds;
            EndSeconds = endSeconds;
        }

        public float Duration => EndSeconds - StartSeconds;
        public bool IsValid => EndSeconds > StartSeconds + 0.01f;
    }

    /// <summary>
    /// Detects leading/trailing silence in imported voice clips so playback skips dead air.
    /// </summary>
    public static class StoryVoiceTrimUtil
    {
        const float SilenceThreshold = 0.012f;
        const float EdgePaddingSeconds = 0.035f;
        const int WindowsPerSecond = 50;

        static readonly Dictionary<int, VoiceTrimBounds> _cache = new Dictionary<int, VoiceTrimBounds>();

        public static VoiceTrimBounds GetTrimBounds(AudioClip clip)
        {
            if (clip == null)
                return default;

            int id = clip.GetInstanceID();
            if (_cache.TryGetValue(id, out var cached))
                return cached;

            var bounds = Analyze(clip);
            _cache[id] = bounds;
            return bounds;
        }

        static VoiceTrimBounds Analyze(AudioClip clip)
        {
            int channels = clip.channels;
            int sampleRate = clip.frequency;
            int frameCount = clip.samples;
            if (channels <= 0 || sampleRate <= 0 || frameCount <= 0)
                return new VoiceTrimBounds(0f, clip.length);

            int sampleCount = frameCount * channels;
            var data = new float[sampleCount];
            if (!clip.GetData(data, 0))
                return new VoiceTrimBounds(0f, clip.length);

            int windowFrames = Mathf.Max(1, sampleRate / WindowsPerSecond);
            int windowCount = (frameCount + windowFrames - 1) / windowFrames;
            int firstActive = -1;
            int lastActive = -1;

            for (int w = 0; w < windowCount; w++)
            {
                int startFrame = w * windowFrames;
                int endFrame = Mathf.Min(frameCount, startFrame + windowFrames);
                float peak = 0f;

                for (int frame = startFrame; frame < endFrame; frame++)
                {
                    int baseIndex = frame * channels;
                    for (int c = 0; c < channels; c++)
                    {
                        float amp = Mathf.Abs(data[baseIndex + c]);
                        if (amp > peak)
                            peak = amp;
                    }
                }

                if (peak < SilenceThreshold)
                    continue;

                if (firstActive < 0)
                    firstActive = w;
                lastActive = w;
            }

            if (firstActive < 0)
                return new VoiceTrimBounds(0f, clip.length);

            float startSec = firstActive * windowFrames / (float)sampleRate;
            float endSec = (lastActive + 1) * windowFrames / (float)sampleRate;
            startSec = Mathf.Max(0f, startSec - EdgePaddingSeconds);
            endSec = Mathf.Min(clip.length, endSec + EdgePaddingSeconds);

            if (endSec <= startSec + 0.02f)
                return new VoiceTrimBounds(0f, clip.length);

            return new VoiceTrimBounds(startSec, endSec);
        }
    }
}
