using System.Collections.Generic;
using UnityEngine;

namespace GameQuestSystem
{
    /// <summary>
    /// Loads character dialogue clips from Resources/Voice/Dialogue/{documentId}/{pageIndex:000}.mp3
    /// and branch choice clips from .../choice_{choiceIndex:000}.mp3
    /// </summary>
    public static class StoryDialogueVoice
    {
        static AudioSource _source;
        static readonly Dictionary<string, AudioClip> _cache = new Dictionary<string, AudioClip>();

        public static bool IsPlaying => _source != null && _source.isPlaying;

        static AudioSource EnsureSource()
        {
            if (_source != null)
                return _source;

            var host = GameObject.Find("[StoryDialogueVoice]");
            if (host == null)
            {
                host = new GameObject("[StoryDialogueVoice]");
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
            {
                Stop();
                return;
            }

            var clip = LoadPage(documentId, pageIndex);
            if (clip == null)
            {
                Stop();
                return;
            }

            var trim = StoryVoiceTrimUtil.GetTrimBounds(clip);
            StoryDialogueVoiceRunner.Instance.PlayTrimmed(EnsureSource(), clip, trim, volume);
        }

        public static void PlayChoice(string documentId, int choiceIndex, float volume = 0.95f)
        {
            if (string.IsNullOrWhiteSpace(documentId) || choiceIndex < 0)
            {
                Stop();
                return;
            }

            var clip = LoadChoice(documentId, choiceIndex);
            if (clip == null)
            {
                Stop();
                return;
            }

            var trim = StoryVoiceTrimUtil.GetTrimBounds(clip);
            StoryDialogueVoiceRunner.Instance.PlayTrimmed(EnsureSource(), clip, trim, volume);
        }

        public static void Stop()
        {
            StoryDialogueVoiceRunner.Instance.StopPlayback(_source);
        }

        static AudioClip LoadPage(string documentId, int pageIndex)
        {
            string key = $"{documentId}/page/{pageIndex:000}";
            if (_cache.TryGetValue(key, out var cached))
                return cached;

            var clip = Resources.Load<AudioClip>($"Voice/Dialogue/{documentId}/{pageIndex:000}");
            if (clip != null)
                _cache[key] = clip;
            return clip;
        }

        static AudioClip LoadChoice(string documentId, int choiceIndex)
        {
            string key = $"{documentId}/choice/{choiceIndex:000}";
            if (_cache.TryGetValue(key, out var cached))
                return cached;

            var clip = Resources.Load<AudioClip>(
                $"Voice/Dialogue/{documentId}/choice_{choiceIndex:000}");
            if (clip != null)
                _cache[key] = clip;
            return clip;
        }
    }
}
