using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class SpatialAudioPausable : MonoBehaviour
{
    private AudioSource _source;
    private bool _wasPlayingBeforePause;

    void Awake() => _source = GetComponent<AudioSource>();

    public void SetPause(bool isPaused)
    {
        if (isPaused)
        {
            if (_source.isPlaying)
            {
                _wasPlayingBeforePause = true;
                _source.Pause();
            }
        }
        else
        {
            if (_wasPlayingBeforePause)
            {
                _source.UnPause();
                _wasPlayingBeforePause = false;
            }
        }
    }
}