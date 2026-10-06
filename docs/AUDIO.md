# Audio

All sound is **synthesised in code** by `Assets/Scripts/Sfx.cs` at startup. There are no audio files.
Result: a tiny download and a consistent chiptune style.

## How a sound is made

A sound is an array of float samples (-1 to 1). `Sfx.Tone(...)` fills it one sample at a time:

```
for each sample i:
    u      = i / n                      # progress through the sound, 0 to 1
    phase += frequency(u) / sampleRate  # phase accumulator: keeps sweeps smooth, no clicks
    s      = oscillator(wave, phase)    # square, triangle, saw or noise
    data[i] = s * amplitude(u)          # volume envelope
```

- **Sample rate:** 22,050 Hz, mono. Plenty for chiptune, half the memory of CD quality.
- **Waveforms:** Square (hollow, retro), Triangle (soft), Saw (buzzy), Noise (percussive). Square with a narrow pulse (duty 25%) is
  used for the thin, nasal lead in the music.
- **Frequency and amplitude are functions of u**, so a sound can sweep (jump rises, hurt falls) and fade.
- Noise uses a `System.Random` seeded from the clip name, so a clip is identical every time it is generated.

## Sound effects

| Clip | Length | Recipe | When it plays |
|---|---|---|---|
| Jump | 0.22 s | square, 280 to 640 Hz rising | jump starts |
| Coin | 0.26 s | square, 988 Hz then 1319 Hz (two-note ding) | coin collected; pitch rises 6% per coin in a quick streak (max 8) |
| Stomp | 0.20 s | triangle, 260 to 70 Hz falling + 25% noise | enemy stomped |
| Hurt | 0.50 s | saw, 480 to 90 Hz falling | life lost |
| Step | 0.045 s | noise tick | footsteps about every 0.27 s while running (pitch varies 0.9 to 1.2) |
| Land | 0.09 s | triangle, 150 to 60 Hz + 30% noise | landing after a fall faster than 6 tiles/s |
| StageClear | 0.70 s | square arpeggio C-E-G-C (523, 659, 784, 1047 Hz) | stage completed |
| GameOver | 1.10 s | triangle, four notes down (392, 349, 311, 262 Hz) | out of lives |
| Start | 0.30 s | square, 660 then 880 Hz | game start, restart, help closed |
| OneUp | 0.45 s | square, fast rising run (659, 784, 1319, 1175, 1319, 1568 Hz) | a bonus heart is collected |

## Music

`Sfx.BuildMusic()` composes a 32-step loop, 0.19 s per step = **6.08 seconds**, as MIDI note numbers (0 = rest):

| Voice | Wave | Part |
|---|---|---|
| Lead | square, 25% pulse | bright pentatonic melody (C major feel) |
| Bass | triangle | root and fifth on alternate steps |
| Hi-hat | noise | quiet tick on every second step |

Chord pattern over four bars: **C, Dm, C, G**. Frequency from MIDI note *m*: `440 * 2^((m - 69) / 12)`.
Each note sounds for the first 90% of its step and fades by 55%, so notes are plucked and do not blur together.
The music volume is 0.22 so it sits behind the effects.

## Controls and behaviour

| Item | Behaviour |
|---|---|
| `Sfx.Play(clip, volume, pitch)` | One-shot effect. Does nothing if muted. |
| `Sfx.StartMusic()` | Starts the loop if it is not already playing. Called from `Game.BeginGame`. |
| `Sfx.ToggleMute()` | M key. Pauses music and silences effects. |
| Browser rule | Audio can only start after a user gesture, so music starts at the first press or tap, not at page load. |
| iPhone | iOS mutes web audio when the ring/silent switch is on. See KI-001 in [KNOWN_ISSUES.md](KNOWN_ISSUES.md). |

Known quirk: `AudioSource.pitch` is shared by all sounds playing on the source, so a pitch change briefly affects an overlapping
effect. Effects are very short, so it is not noticeable in practice.

## How to change audio

| I want to... | Do this |
|---|---|
| Tweak an effect | Edit its `Tone(...)` line in `Sfx.Awake` (duration, frequency function, amplitude function, wave, noise mix) |
| Add an effect | Add a name to the `Clip` enum, add a `clips[Clip.X] = Tone(...)` line, call `Sfx.Play(Clip.X)` |
| Change the tune | Edit the `lead` and `bass` arrays in `BuildMusic` (MIDI numbers; 60 is middle C) |
| Make music faster | Reduce `step` (0.19 s) |
| Add vibration (mobile, future) | Not implemented; plan in [ROADMAP.md](ROADMAP.md) |
