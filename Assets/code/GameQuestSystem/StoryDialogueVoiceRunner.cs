using System.Collections;
using UnityEngine;

namespace GameQuestSystem
{
    /// <summary>Coroutine host for trimmed dialogue voice playback and hard stops on page skip.</summary>
    public class StoryDialogueVoiceRunner : MonoBehaviour
    {
        static StoryDialogueVoiceRunner _instance;
        Coroutine _watchEnd;

        public static StoryDialogueVoiceRunner Instance
        {
            get
            {
                if (_instance != null)
                    return _instance;

                var host = GameObject.Find("[StoryDialogueVoice]");
                if (host == null)
                {
                    host = new GameObject("[StoryDialogueVoice]");
                    DontDestroyOnLoad(host);
                }

                _instance = host.GetComponent<StoryDialogueVoiceRunner>();
                if (_instance == null)
                    _instance = host.AddComponent<StoryDialogueVoiceRunner>();
                return _instance;
            }
        }

        public void PlayTrimmed(AudioSource source, AudioClip clip, VoiceTrimBounds trim, float volume)
        {
            CancelWatch();
            if (source == null)
                return;

            source.Stop();
            if (clip == null)
            {
                source.clip = null;
                return;
            }

            source.clip = clip;
            source.volume = Mathf.Clamp01(volume);
            source.time = trim.IsValid ? trim.StartSeconds : 0f;
            source.Play();

            if (trim.IsValid && trim.EndSeconds < clip.length - 0.04f)
                _watchEnd = StartCoroutine(CoStopAtTime(source, trim.EndSeconds));
        }

        public void StopPlayback(AudioSource source)
        {
            CancelWatch();
            if (source == null)
                return;

            source.Stop();
            source.clip = null;
        }

        void CancelWatch()
        {
            if (_watchEnd == null)
                return;
            StopCoroutine(_watchEnd);
            _watchEnd = null;
        }

        IEnumerator CoStopAtTime(AudioSource source, float endSeconds)
        {
            const float epsilon = 0.015f;
            while (source != null && source.isPlaying && source.clip != null &&
                   source.time + epsilon < endSeconds)
            {
                yield return null;
            }

            if (source != null && source.isPlaying)
                source.Stop();

            _watchEnd = null;
        }
    }
}
