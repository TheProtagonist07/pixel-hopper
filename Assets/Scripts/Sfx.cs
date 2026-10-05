using System;
using System.Collections.Generic;
using UnityEngine;

public enum Clip { Jump, Coin, Stomp, Hurt, Step, Land, StageClear, GameOver, Start }

public class Sfx : MonoBehaviour
{
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
        musicSource.volume = 0.22f;

        clips[Clip.Jump] = Tone("jump", 0.22f, u => Mathf.Lerp(280f, 640f, u), u => 0.32f * Mathf.Pow(1f - u, 1.4f), Wave.Square);
        clips[Clip.Coin] = Tone("coin", 0.26f, u => u < 0.3f ? 988f : 1319f, u => 0.3f * Mathf.Pow(1f - u, 1.2f), Wave.Square);
        clips[Clip.Stomp] = Tone("stomp", 0.2f, u => Mathf.Lerp(260f, 70f, u), u => 0.5f * Mathf.Pow(1f - u, 1.5f), Wave.Triangle, 0.25f);
        clips[Clip.Hurt] = Tone("hurt", 0.5f, u => Mathf.Lerp(480f, 90f, u), u => 0.34f * (1f - u), Wave.Saw);
        clips[Clip.Step] = Tone("step", 0.045f, u => 120f, u => 0.16f * (1f - u), Wave.Noise);
        clips[Clip.Land] = Tone("land", 0.09f, u => Mathf.Lerp(150f, 60f, u), u => 0.4f * (1f - u), Wave.Triangle, 0.3f);
        clips[Clip.StageClear] = Tone("clear", 0.7f, u => Arp(u, 523f, 659f, 784f, 1047f), u => 0.3f * (1f - 0.5f * u), Wave.Square);
        clips[Clip.GameOver] = Tone("gameover", 1.1f, u => Arp(u, 392f, 349f, 311f, 262f), u => 0.34f * (1f - 0.4f * u), Wave.Triangle);
        clips[Clip.Start] = Tone("start", 0.3f, u => u < 0.5f ? 660f : 880f, u => 0.28f * (1f - u), Wave.Square);

        musicSource.clip = BuildMusic();
    }

    public static void Play(Clip clip, float volume = 1f, float pitch = 1f)
    {
        if (instance == null || instance.muted) return;
        instance.sfxSource.pitch = pitch;
        instance.sfxSource.PlayOneShot(instance.clips[clip], volume);
    }

    public static void StartMusic()
    {
        if (instance == null || instance.muted || instance.musicSource.isPlaying) return;
        instance.musicSource.Play();
    }

    public static void ToggleMute()
    {
        if (instance == null) return;
        instance.muted = !instance.muted;
        if (instance.muted) instance.musicSource.Pause();
        else instance.musicSource.UnPause();
        if (!instance.muted && !instance.musicSource.isPlaying) instance.musicSource.Play();
    }

    public static bool Muted => instance != null && instance.muted;

    static float Arp(float u, params float[] notes)
    {
        int i = Mathf.Min(notes.Length - 1, (int)(u * notes.Length));
        return notes[i];
    }

    static float Osc(Wave wave, float phase, System.Random rng)
    {
        float p = phase - Mathf.Floor(phase);
        switch (wave)
        {
            case Wave.Square: return p < 0.5f ? 1f : -1f;
            case Wave.Triangle: return 4f * Mathf.Abs(p - 0.5f) - 1f;
            case Wave.Saw: return 2f * p - 1f;
            default: return (float)(rng.NextDouble() * 2.0 - 1.0);
        }
    }

    static AudioClip Tone(string name, float duration, Func<float, float> freq, Func<float, float> amp, Wave wave, float noiseMix = 0f)
    {
        int n = Mathf.CeilToInt(duration * SampleRate);
        var data = new float[n];
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
                int tick = stepSamples / 6;
                for (int i = 0; i < tick; i++)
                    data[start + i] += (float)(rng.NextDouble() * 2.0 - 1.0) * 0.05f * (1f - (float)i / tick);
            }
        }

        var clip = AudioClip.Create("music", data.Length, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

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
            float s = wave == Wave.Square ? (p < duty ? 1f : -1f) : 4f * Mathf.Abs(p - 0.5f) - 1f;
            float env = 1f - 0.55f * ((float)i / gate);
            data[start + i] += s * volume * env;
        }
    }
}
