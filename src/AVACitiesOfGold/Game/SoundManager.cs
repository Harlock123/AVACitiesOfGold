using AVACitiesOfGold.Audio;

namespace AVACitiesOfGold.Game;

/// <summary>
/// Plays the game's sounds through OpenAL Soft. The tunes play on the music channel and the sea and
/// land ambience loops on the ambience channel. In a virtual machine a larger mixing buffer
/// keeps the sound from breaking up.
/// </summary>
public sealed class SoundManager : IDisposable
{
    private readonly IAudioEngine _engine;
    private readonly string _folder;

    public SoundManager(bool silent = false)
    {
        _folder = Path.Combine(AppContext.BaseDirectory, "Sounds");
        _engine = silent
            ? new NullAudioEngine("silent")
            : OpenAlAudioEngine.CreateOrSilent(AudioBuffer.PeriodFrames(AudioBuffer.Automatic, VirtualMachine.Detect()));
        _engine.SetVolumes(0.9f, 0.8f, 0.7f);
        _engine.SetAmbienceVolume(0.35f);
    }

    public bool IsAvailable => _engine.IsAvailable;

    public bool IsMuted
    {
        get => _muted;
        set
        {
            _muted = value;
            if (value) StopAll();
        }
    }
    private bool _muted;

    private string File(Sfx s) => Path.Combine(_folder, s + ".wav");

    public void Play(Sfx sfx)
    {
        if (_muted) return;
        switch (sfx)
        {
            case Sfx.TitleTune or Sfx.Ending: _engine.PlayMusic(File(sfx), loop: true); return;
            case Sfx.Fanfare: _engine.PlayMusic(File(sfx), loop: false); return;
            case Sfx.Sea or Sfx.Land: _engine.PlayAmbience(File(sfx)); return;
            case Sfx.MusicStop:
                _engine.StopAmbience();
                _engine.StopMusic();
                return;
        }
        _engine.PlayEffect(File(sfx), sfx switch { Sfx.Step or Sfx.Sail => 0.3f, Sfx.Menu => 0.35f, _ => 1f });
    }

    public void StopAll()
    {
        _engine.StopAmbience();
        _engine.StopMusic();
    }

    public void Dispose() => _engine.Dispose();
}
