using System.Collections.Generic;
using UnityEngine;

namespace GameQuestSystem
{
    /// <summary>
    /// Loads narration clips from Resources/Voice/Narration/{documentId}/{pageIndex:000}.mp3
    /// </summary>
    public static class StoryNarrationVoice
    {
        static AudioSource _source;
        static readonly Dictionary<string, AudioClip> _cache = new Dictionary<string, AudioClip>();

        public static bool IsPlaying => _source != null && _source.isPlaying;

        static AudioSource EnsureSource()
        {
            if (_source != null)
                return _source;

            var host = GameObject.Find("[StoryNarrationVoice]");
            if (host == null)
            {
                host = new GameObject("[StoryNarrationVoice]");
                Object.DontDestroyOnLoad(host);
            }

            _source = host.GetComponent<AudioSource>();
            if (_source == null)
                _source = host.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = false;
            _source.spatialBlend = 0f;
            return _source;
        }

        public static void Play(string documentId, int pageIndex, float volume = 0.95f)
        {
            if (string.IsNullOrWhiteSpace(documentId) || pageIndex < 0)
                return;

            var clip = Load(documentId, pageIndex);
            if (clip == null)
            {
                Stop();
                return;
            }

            var source = EnsureSource();
            source.Stop();
            source.clip = clip;
            source.volume = Mathf.Clamp01(volume);
            source.time = 0f;
            source.Play();
        }

        public static void Stop()
        {
            if (_source == null)
                return;
            _source.Stop();
            _source.clip = null;
        }

        static AudioClip Load(string documentId, int pageIndex)
        {
            string key = $"{documentId}/{pageIndex:000}";
            if (_cache.TryGetValue(key, out var cached))
                return cached;

            var clip = Resources.Load<AudioClip>($"Voice/Narration/{documentId}/{pageIndex:000}");
            if (clip != null)
                _cache[key] = clip;
            return clip;
        }

        public static bool IsNarrationSpeaker(string speaker)
        {
            if (string.IsNullOrWhiteSpace(speaker))
                return true;
            speaker = speaker.Trim().Trim('"');
            return speaker == "旁白"
                || speaker == "（旁白）"
                || speaker.Equals("narration", System.StringComparison.OrdinalIgnoreCase)
                || speaker.Equals("Narrator", System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
