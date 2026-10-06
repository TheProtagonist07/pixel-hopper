using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>The sound effects the game can play (see <see cref="Sfx.Play"/>).</summary>
public enum Clip { Jump, Coin, Stomp, Hurt, Step, Land, StageClear, GameOver, Start, OneUp }

/// <summary>
/// All of the game's audio, generated in code. There are no audio files in the project:
/// every sound effect and the music loop are synthesised as raw samples at startup, which keeps the download tiny
/// and the style consistently "chiptune".
///
/// How a sound is made: <see cref="Tone"/> fills a float array sample by sample. For each sample it asks a
/// frequency function and an amplitude function (both take progress u from 0 to 1 over the sound's length),
/// advances the oscillator phase, and reads the waveform (square, triangle, saw or noise).
/// Sliding the frequency over time gives the classic "jump" (rising) or "hurt" (falling) sweeps.
///
/// Usage: add one instance to a GameObject (Game does this in Awake), then call the static methods anywhere:
/// Sfx.Play(Clip.Coin), Sfx.StartMusic(), Sfx.ToggleMute().
///
/// Browser note: web browsers only allow audio after the user has interacted with the page. The music is therefore
/// started from the first key press or tap (Game.BeginGame), not at load.
/// </summary>
public class Sfx : MonoBehaviour
{
    // 22.05 kHz mono is plenty for chiptune and halves the memory of CD-quality audio.
    const int SampleRate = 22050;
    enum Wave { Square, Triangle, Saw, Noise }

    static Sfx instance;
    readonly Dictionary<Clip, AudioClip> clips = new Dictionary<Clip, AudioClip>();
    AudioSource sfxSource;
    AudioSource musicSource;
    bool muted;

    void Awake()
    {
        instance = this;
        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.playOnAwake = false;
        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.volume = 0.22f; // music sits quietly behind the effects

        // Sound effect recipes. Tone(name, seconds, frequency-over-time, volume-over-time, waveform, noise amount).
        // Frequencies are in Hz; u runs from 0 to 1 over the clip.
        clips[Clip.Jump] = Tone("jump", 0.22f, u => Mathf.Lerp(280f, 640f, u), u => 0.32f * Mathf.Pow(1f - u, 1.4f), Wave.Square);            // rising blip
        clips[Clip.Coin] = Tone("coin", 0.26f, u => u < 0.3f ? 988f : 1319f, u => 0.3f * Mathf.Pow(1f - u, 1.2f), Wave.Square);               // two-note "ding"
        clips[Clip.Stomp] = Tone("stomp", 0.2f, u => Mathf.Lerp(260f, 70f, u), u => 0.5f * Mathf.Pow(1f - u, 1.5f), Wave.Triangle, 0.25f);    // falling thump with a little noise
        clips[Clip.Hurt] = Tone("hurt", 0.5f, u => Mathf.Lerp(480f, 90f, u), u => 0.34f * (1f - u), Wave.Saw);                                // long falling buzz
        clips[Clip.Step] = Tone("step", 0.045f, u => 120f, u => 0.16f * (1f - u), Wave.Noise);                                                 // tiny noise tick
        clips[Clip.Land] = Tone("land", 0.09f, u => Mathf.Lerp(150f, 60f, u), u => 0.4f * (1f - u), Wave.Triangle, 0.3f);                     // soft thud
        clips[Clip.StageClear] = Tone("clear", 0.7f, u => Arp(u, 523f, 659f, 784f, 1047f), u => 0.3f * (1f - 0.5f * u), Wave.Square);         // C-E-G-C arpeggio up
        clips[Clip.GameOver] = Tone("gameover", 1.1f, u => Arp(u, 392f, 349f, 311f, 262f), u => 0.34f * (1f - 0.4f * u), Wave.Triangle);      // four notes down
        clips[Clip.Start] = Tone("start", 0.3f, u => u < 0.5f ? 660f : 880f, u => 0.28f * (1f - u), Wave.Square);                             // two-note confirm

        clips[Clip.OneUp] = Tone("oneup", 0.45f, u => Arp(u, 659f, 784f, 1319f, 1175f, 1319f, 1568f), u => 0.3f * (1f - 0.35f * u), Wave.Square);   // bright rising run: extra life

        musicSource.clip = BuildMusic();
    }

    /// <summary>Plays a sound effect once. Does nothing if muted or if no Sfx instance exists yet.</summary>
    /// <param name="clip">Which effect.</param>
    /// <param name="volume">Volume multiplier from 0 to 1.</param>
    /// <param name="pitch">Playback speed; 1 is normal. Used for variation (footsteps) and rising coin pitch.</param>
    // Known quirk: AudioSource.pitch is shared by every sound currently playing on the source. Effects are very
    // short, so a pitch change only briefly affects an overlapping sound; this is acceptable.
    public static void Play(Clip clip, float volume = 1f, float pitch = 1f)
    {
        if (instance == null || instance.muted) return;
        instance.sfxSource.pitch = pitch;
        instance.sfxSource.PlayOneShot(instance.clips[clip], volume);
    }

    /// <summary>Starts the looping music if it is not already playing (and not muted).</summary>
    public static void StartMusic()
    {
        if (instance == null || instance.muted || instance.musicSource.isPlaying) return;
        instance.musicSource.Play();
    }

    /// <summary>Mutes or unmutes everything. Bound to the M key.</summary>
    public static void ToggleMute()
    {
        if (instance == null) return;
        instance.muted = !instance.muted;
        if (instance.muted) instance.musicSource.Pause();
        else instance.musicSource.UnPause();
        if (!instance.muted && !instance.musicSource.isPlaying) instance.musicSource.Play();
    }

    /// <summary>True while sound is muted.</summary>
    public static bool Muted => instance != null && instance.muted;

    // Picks one of the given notes depending on progress u: with 4 notes, each lasts a quarter of the clip.
    // Used for the stage-clear and game-over jingles.
    static float Arp(float u, params float[] notes)
    {
        int i = Mathf.Min(notes.Length - 1, (int)(u * notes.Length));
        return notes[i];
    }

    // One sample of a basic waveform at the given phase (phase is in cycles; only its fractional part matters).
    // Returns a value from -1 to 1.
    static float Osc(Wave wave, float phase, System.Random rng)
    {
        float p = phase - Mathf.Floor(phase);
        switch (wave)
        {
            case Wave.Square: return p < 0.5f ? 1f : -1f;                    // hollow, retro
            case Wave.Triangle: return 4f * Mathf.Abs(p - 0.5f) - 1f;        // soft, flute-like
            case Wave.Saw: return 2f * p - 1f;                               // buzzy
            default: return (float)(rng.NextDouble() * 2.0 - 1.0);           // white noise
        }
    }

    // Synthesises a mono clip. freq and amp are functions of progress u (0 to 1); noiseMix blends in white noise.
    // Phase accumulation (phase += frequency / sample rate) keeps frequency sweeps smooth with no clicks.
    static AudioClip Tone(string name, float duration, Func<float, float> freq, Func<float, float> amp, Wave wave, float noiseMix = 0f)
    {
        int n = Mathf.CeilToInt(duration * SampleRate);
        var data = new float[n];
        // Seeded from the clip name so noise-based effects use the same noise pattern each time they are generated.
        var rng = new System.Random(name.GetHashCode());
        float phase = 0f;
        for (int i = 0; i < n; i++)
        {
            float u = (float)i / n;
            phase += freq(u) / SampleRate;
            float s = Osc(wave, phase, rng);
            if (noiseMix > 0f) s = Mathf.Lerp(s, Osc(Wave.Noise, 0f, rng), noiseMix);
            data[i] = s * amp(u);
        }
        var clip = AudioClip.Create(name, n, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // Composes the looping background music.
    // The tune is 32 steps of 0.19 s each (a 6.08 second loop), written as MIDI note numbers (0 = rest):
    //   lead (square wave, thin 25% pulse): a bright pentatonic melody
    //   bass (triangle wave): root and fifth on alternate steps
    // Chords over the four bars are C, Dm, C and G. A quiet hi-hat noise tick is added on every second step.
    // MIDI note 69 is A4 = 440 Hz; every 12 notes up doubles the frequency.
    static AudioClip BuildMusic()
    {
        const float step = 0.19f;
        int[] lead =
        {
            72, 0, 76, 0, 79, 0, 76, 0,  74, 0, 77, 0, 81, 0, 77, 0,
            72, 0, 76, 0, 79, 0, 84, 0,  83, 0, 79, 0, 76, 0, 0, 0
        };
        int[] bass =
        {
            48, 0, 55, 0, 48, 0, 55, 0,  50, 0, 57, 0, 50, 0, 57, 0,
            48, 0, 55, 0, 48, 0, 55, 0,  43, 0, 50, 0, 43, 0, 50, 0
        };

        int stepSamples = Mathf.RoundToInt(step * SampleRate);
        var data = new float[lead.Length * stepSamples];
        var rng = new System.Random(5);

        for (int s = 0; s < lead.Length; s++)
        {
            int start = s * stepSamples;
            AddNote(data, start, stepSamples, lead[s], Wave.Square, 0.25f, 0.22f);
            AddNote(data, start, stepSamples, bass[s], Wave.Triangle, 0.5f, 0.3f);
            if (s % 2 == 1)
            {
                // Hi-hat: a very short burst of decaying noise at the start of the step.
                int tick = stepSamples / 6;
                for (int i = 0; i < tick; i++)
                    data[start + i] += (float)(rng.NextDouble() * 2.0 - 1.0) * 0.05f * (1f - (float)i / tick);
            }
        }

        var clip = AudioClip.Create("music", data.Length, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    // Adds one note into the buffer (mixing with whatever is already there).
    // The note sounds for the first 90% of its step and fades by 55% over that time, so notes sound plucked
    // and neighbouring notes do not blur together.
    static void AddNote(float[] data, int start, int length, int midi, Wave wave, float duty, float volume)
    {
        if (midi <= 0) return;
        float freq = 440f * Mathf.Pow(2f, (midi - 69) / 12f);
        float phase = 0f;
        int gate = Mathf.RoundToInt(length * 0.9f);
        for (int i = 0; i < gate; i++)
        {
            phase += freq / SampleRate;
            float p = phase - Mathf.Floor(phase);
            // 'duty' is the width of the square pulse (0.25 = thin and nasal, 0.5 = classic square).
            float s = wave == Wave.Square ? (p < duty ? 1f : -1f) : 4f * Mathf.Abs(p - 0.5f) - 1f;
            float env = 1f - 0.55f * ((float)i / gate);
            data[start + i] += s * volume * env;
        }
    }
}
