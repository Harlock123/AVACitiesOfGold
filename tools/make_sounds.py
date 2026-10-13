#!/usr/bin/env python3
"""Synthesises AVACitiesOfGold's sound effects and tunes into ../src/AVACitiesOfGold/Sounds as 16-bit mono WAV.

Nothing is sampled from any other game: every effect is built here from square, triangle and
noise voices, and the tunes are new compositions. Run it from anywhere:
    python3 tools/make_sounds.py
"""
import math
import os
import random
import struct
import wave

RATE = 22050
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "src", "AVACitiesOfGold", "Sounds")
RNG = random.Random(1985)


def write(name, samples, volume=0.7):
    peak = max(1e-9, max(abs(s) for s in samples))
    with wave.open(os.path.join(OUT, name), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(s / peak * volume * 32767)) for s in samples))


def voice(kind, phase):
    p = phase % 1.0
    if kind == "square":
        return 1.0 if p < 0.5 else -1.0
    if kind == "pulse":
        return 1.0 if p < 0.25 else -1.0
    if kind == "tri":
        return 4 * p - 1 if p < 0.5 else 3 - 4 * p
    if kind == "saw":
        return 2 * p - 1
    if kind == "sine":
        return math.sin(2 * math.pi * p)
    return RNG.uniform(-1, 1)


def tone(seconds, f0, f1=None, kind="square", attack=0.004, release=1.0, vibrato=0.0, vib_rate=8.0, gain=1.0):
    """A note gliding from f0 to f1 (exponentially), with a short attack and a shaped decay."""
    n = int(RATE * seconds)
    f1 = f1 or f0
    out, phase, held = [], 0.0, 0.0
    for i in range(n):
        t = i / RATE
        f = f0 * (f1 / f0) ** (i / max(1, n - 1))
        if vibrato:
            f *= 1 + vibrato * math.sin(2 * math.pi * vib_rate * t)
        phase += f / RATE
        if kind == "noise":
            # Noise re-sampled at the voice's rate: a lower 'pitch' sounds rougher.
            if int(phase) != int(phase - f / RATE):
                held = RNG.uniform(-1, 1)
            s = held
        else:
            s = voice(kind, phase)
        env = min(1.0, t / attack) * (1 - i / n) ** release
        out.append(s * env * gain)
    return out


def silence(seconds):
    return [0.0] * int(RATE * seconds)


def cat(*parts):
    out = []
    for p in parts:
        out.extend(p)
    return out


def mix(*parts):
    n = max(len(p) for p in parts)
    out = [0.0] * n
    for p in parts:
        for i, v in enumerate(p):
            out[i] += v
    return out


NOTES = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}


def hz(note):
    n = NOTES[note[0]]
    rest = note[1:]
    while rest and rest[0] in "#b":
        n += 1 if rest[0] == "#" else -1
        rest = rest[1:]
    return 440.0 * 2 ** ((12 * (int(rest) + 1) + n - 69) / 12)


def tune(bpm, lead, bass, kind="pulse"):
    """Two voices: a lead and a bass line, as (note, beats) lists; '-' is a rest."""
    beat = 60.0 / bpm

    def line(notes, k, gain, release):
        out = []
        for note, beats in notes:
            d = beats * beat
            out.extend(silence(d) if note == "-" else tone(d, hz(note), kind=k, release=release, gain=gain))
        return out

    return mix(line(lead, kind, 0.55, 0.6), line(bass, "tri", 0.6, 0.3))


def ambience(seconds, layers):
    n = int(RATE * seconds)
    out = [0.0] * n
    for start, sound in layers:
        k = int(start * RATE)
        for j, v in enumerate(sound):
            if k + j < n:
                out[k + j] += v
    return out


def main():
    os.makedirs(OUT, exist_ok=True)
    write("Step.wav", tone(0.05, 120, 70, "noise", release=1.5), 0.2)
    write("Sail.wav", tone(0.18, 300, 200, "noise", release=1.0, gain=0.6), 0.16)
    write("Bump.wav", mix(tone(0.1, 90, 60, "square", release=1.2), tone(0.08, 300, 100, "noise", release=1.5)), 0.3)
    write("Ashore.wav", cat(tone(0.3, 600, 200, "noise", release=0.8), tone(0.15, 200, 120, "noise", release=1.0)), 0.35)
    write("Discover.wav", cat(*[tone(0.08, hz(n), kind="tri", release=0.4) for n in ["G5", "B5", "D6", "G6"]]), 0.4)
    write("Trade.wav", cat(tone(0.06, hz("C6"), kind="pulse", release=0.5), tone(0.1, hz("G5"), kind="pulse", release=0.6)), 0.3)
    write("Gift.wav", cat(*[tone(0.07, hz(n), kind="sine", release=0.4) for n in ["E5", "G5", "C6", "E6"]]), 0.38)
    write("Coin.wav", cat(*[tone(0.04, f, kind="square", release=0.6) for f in [2000, 2600, 2200, 2800]]), 0.28)
    write("Feast.wav", tune(180, [("C5", 0.5), ("E5", 0.5), ("G5", 0.5), ("E5", 0.5), ("C6", 1)], [("C3", 1), ("G2", 1), ("C3", 1)], kind="tri"), 0.42)
    write("Battle.wav", mix(cat(*[tone(0.12, 120, 90, "noise", release=0.7) for _ in range(6)]),
                            cat(*[tone(0.1, f, f * 0.8, "square", release=0.8, gain=0.4) for f in [800, 600, 900, 500, 700, 400, 600]])), 0.5)
    write("Storm.wav", mix(tone(1.8, 300, 80, "noise", release=0.3), tone(1.2, 60, 40, "saw", release=0.5, gain=0.5)), 0.55)
    write("Forage.wav", cat(tone(0.08, 400, 300, "noise", release=0.9), tone(0.12, hz("A5"), kind="tri", release=0.6)), 0.3)
    write("Loss.wav", cat(*[tone(0.25, hz(n), kind="tri", release=0.5) for n in ["D5", "A4", "F4"]]), 0.4)
    write("Post.wav", cat(*[tone(0.1, hz(n), kind="pulse", release=0.4) for n in ["C5", "E5", "G5", "C6", "G5", "C6"]]), 0.4)
    write("Menu.wav", tone(0.03, 1200, kind="tri", release=0.8), 0.2)
    write("Pick.wav", cat(tone(0.03, 900, kind="tri", release=0.6), tone(0.05, 1350, kind="tri", release=0.8)), 0.25)
    write("Drums.wav", cat(*[mix(tone(0.16, 90, 60, "sine", release=0.6), tone(0.08, 200, 100, "noise", release=1.0, gain=0.5)) for _ in range(5)]), 0.5)

    # The sea: waves rolling in and out, and a creak of timbers (new). Loops.
    sea = ambience(12.0, [(t, tone(2.6, 500, 200, "noise", attack=0.8, release=0.5, gain=0.5)) for t in [0.0, 3.0, 6.0, 9.0]]
                   + [(t, tone(0.5, 160, 140, "saw", release=0.6, vibrato=0.1, vib_rate=7, gain=0.15)) for t in [1.6, 7.3]])
    write("Sea.wav", sea, 0.32)
    # The land: wind, and birds now and then (new). Loops.
    land = ambience(12.0, [(0.0, tone(12.0, 300, 300, "noise", attack=0.5, release=0.0, gain=0.12))]
                    + [(t, cat(tone(0.08, 2600, 3400, "sine", release=0.5, gain=0.4), tone(0.1, 3000, 2400, "sine", release=0.6, gain=0.4))) for t in [1.2, 1.5, 5.4, 8.8, 9.1]])
    write("Land.wav", land, 0.3)

    write("TitleTune.wav", tune(84,
        [("D5", 1), ("A4", 0.5), ("D5", 0.5), ("F5", 1.5), ("E5", 0.5), ("D5", 1), ("C5", 1), ("A4", 2),
         ("Bb4", 1), ("C5", 0.5), ("D5", 0.5), ("E5", 1.5), ("F5", 0.5), ("E5", 1), ("C#5", 1), ("D5", 2),
         ("F5", 1), ("G5", 0.5), ("A5", 0.5), ("Bb5", 1.5), ("A5", 0.5), ("G5", 1), ("F5", 1), ("E5", 2),
         ("D5", 1), ("E5", 0.5), ("F5", 0.5), ("E5", 1), ("C#5", 1), ("D5", 4)],
        [(n, 2) for n in ["D3", "D3", "F3", "A2", "Bb2", "C3", "A2", "D3", "D3", "Bb2", "C3", "C3", "Bb2", "A2", "D3", "D3"]], kind="tri"), 0.55)
    write("Fanfare.wav", tune(140,
        [("C5", 0.5), ("C5", 0.25), ("C5", 0.25), ("G5", 1), ("E5", 0.5), ("G5", 0.5), ("C6", 2)],
        [("C3", 1), ("G2", 1), ("C3", 2), ("C3", 1)]), 0.55)
    write("Ending.wav", tune(72,
        [("A4", 1), ("D5", 1), ("F#5", 2), ("E5", 1), ("D5", 1), ("B4", 2), ("G4", 1), ("A4", 1), ("D5", 2), ("C#5", 1), ("E5", 1), ("D5", 4)],
        [(n, 2) for n in ["D3", "D3", "G2", "G2", "E2", "A2", "D3", "D3"]], kind="tri"), 0.5)


if __name__ == "__main__":
    main()
