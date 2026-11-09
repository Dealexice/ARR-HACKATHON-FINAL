using UnityEngine;

namespace YataiTogether
{
    // ponytail: Minimal ambient audio player for looping BGM and street noise at 50% volume.
    public class AmbientAudioController : MonoBehaviour
    {
        [SerializeField] private AudioSource bgmSource;
        [SerializeField] private AudioSource streetNoiseSource;
        [Range(0f, 1f)] [SerializeField] private float volume = 0.5f;

        public AudioSource BgmSource => bgmSource;
        public AudioSource StreetNoiseSource => streetNoiseSource;
        public float TargetVolume => volume;

        private void Awake()
        {
            ApplySettings(bgmSource);
            ApplySettings(streetNoiseSource);
        }

        private void ApplySettings(AudioSource source)
        {
            if (source == null) return;
            source.loop = true;
            source.volume = volume;
            source.spatialBlend = 0f;
            if (!source.isPlaying && source.enabled && gameObject.activeInHierarchy)
            {
                source.Play();
            }
        }
    }
}
