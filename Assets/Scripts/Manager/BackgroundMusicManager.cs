using UnityEngine;
using Utils;

public class BackgroundMusicManager : MonoBehaviour
{
    private static BackgroundMusicManager _instance;
    private AudioSource _audioSource;

    [Header("Music Settings")]
    [Tooltip("Assign your 'gardion background song' here in the Inspector.")]
    [SerializeField] private AudioClip backgroundMusic;
    
    [Tooltip("Normal base volume (0.0 to 1.0)")]
    [SerializeField, Range(0f, 1f)] private float baseVolume = 0.5f;

    public static BackgroundMusicManager GetInstance()
    {
        return _instance;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoInitialize()
    {
        if (_instance == null)
        {
            var go = new GameObject("BackgroundMusicManager");
            _instance = go.AddComponent<BackgroundMusicManager>();
            DontDestroyOnLoad(go);
        }
    }

    private void Awake()
    {
        if (_instance == null)
        {
            _instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeAudioSource();
        }
        else if (_instance != this)
        {
            // If another instance is loaded with a different music clip, update the singleton
            if (this.backgroundMusic != null && _instance.backgroundMusic != this.backgroundMusic)
            {
                _instance.ChangeMusic(this.backgroundMusic);
            }
            _isDestroyed = true;
            Destroy(gameObject);
            return;
        }
    }

    private double _dspTime;
    private float _phase;
    private int _noteIndex;
    private float[] _arpeggio = { 261.63f, 329.63f, 392.00f, 523.25f }; // C, E, G, C
    private float _currentVolume = 0f;
    private int _sampleRate = 48000;
    private bool _useProceduralAudio = false;
    private bool _isDestroyed = false;

    private void OnDestroy()
    {
        _isDestroyed = true;
    }

    private void InitializeAudioSource()
    {
        if (_audioSource == null)
            _audioSource = gameObject.AddComponent<AudioSource>();

        _sampleRate = AudioSettings.outputSampleRate;
            
        // Automatically try to load the song from a Resources folder if none is assigned
        if (backgroundMusic == null)
        {
            backgroundMusic = Resources.Load<AudioClip>("Audio/GameMusic");
        }

        _audioSource.clip = backgroundMusic;
        _audioSource.loop = true;
        _audioSource.playOnAwake = false;
        
        UpdateVolumeState();
        
        if (backgroundMusic != null)
        {
            _useProceduralAudio = false;
            _audioSource.Play();
        }
        else
        {
            _useProceduralAudio = true;
            // If still no clip, start playing the procedural synth by enabling the AudioSource
            _audioSource.Play();
        }
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        if (_isDestroyed) return;
        if (!_useProceduralAudio) return;
        if (_currentVolume <= 0f)
        {
            for (int i = 0; i < data.Length; i++) data[i] = 0f;
            return;
        }

        double sampleRate = _sampleRate;
        double noteDuration = 0.15; // Fast arpeggio

        for (int i = 0; i < data.Length; i += channels)
        {
            _dspTime += 1.0 / sampleRate;

            int currentNote = Mathf.FloorToInt((float)(_dspTime / noteDuration)) % _arpeggio.Length;
            float freq = _arpeggio[currentNote];
            
            // Octave shift every 16 notes for variety
            if (Mathf.FloorToInt((float)(_dspTime / (noteDuration * 16))) % 2 == 1)
                freq *= 2f;

            _phase += 2f * Mathf.PI * freq / (float)sampleRate;
            if (_phase > 2f * Mathf.PI) _phase -= 2f * Mathf.PI;

            // Simple square wave for a retro 8-bit game feel!
            float synth = Mathf.Sin(_phase) > 0 ? 0.5f : -0.5f;
            
            // Envelope for 'pluck' sound
            double timeInNote = _dspTime % noteDuration;
            float envelope = 1f - (float)(timeInNote / noteDuration);

            float val = synth * envelope * _currentVolume * 0.15f; // Keep it quiet

            data[i] = val;
            if (channels == 2) data[i + 1] = val;
        }
    }

    // You can call this from your Settings menu when the user toggles sound on/off
    public void UpdateVolumeState()
    {
        if (_audioSource != null)
        {
            bool isVolumeOn = PreferenceHelper.IsVolumeOn();
            _currentVolume = isVolumeOn ? baseVolume : 0f;
            _audioSource.volume = _currentVolume;
            
            if (!isVolumeOn)
            {
                _audioSource.Pause();
            }
            else if (!_audioSource.isPlaying)
            {
                _audioSource.Play();
            }
        }
    }

    // Set a new track if needed during gameplay
    public void ChangeMusic(AudioClip newClip)
    {
        if (_audioSource != null && newClip != null && _audioSource.clip != newClip)
        {
            this.backgroundMusic = newClip;
            _useProceduralAudio = false;
            _audioSource.clip = newClip;
            _audioSource.Play();
        }
    }
}
