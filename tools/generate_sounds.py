#!/usr/bin/env python3
"""Generate sound effects for the Mechanical Linkage Drawing Simulator."""

import struct
import wave
import math
import os

SAMPLE_RATE = 44100

def generate_wav(filename, samples):
    """Write 16-bit mono WAV file."""
    with wave.open(filename, 'w') as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SAMPLE_RATE)
        for s in samples:
            clamped = max(-1.0, min(1.0, s))
            w.writeframes(struct.pack('<h', int(clamped * 32767)))

def click_sound():
    """Short mechanical click."""
    duration = 0.08
    samples = []
    n = int(SAMPLE_RATE * duration)
    for i in range(n):
        t = i / SAMPLE_RATE
        env = max(0, 1.0 - t / duration)
        # Mix of high freq bursts
        s = 0.6 * math.sin(2 * math.pi * 1200 * t) * env**3
        s += 0.3 * math.sin(2 * math.pi * 2400 * t) * env**4
        s += 0.1 * math.sin(2 * math.pi * 800 * t) * env**2
        samples.append(s * 0.7)
    return samples

def delete_sound():
    """Soft whoosh/erase sound."""
    duration = 0.2
    samples = []
    n = int(SAMPLE_RATE * duration)
    for i in range(n):
        t = i / SAMPLE_RATE
        env = math.sin(math.pi * t / duration)
        # Noise-like with decreasing frequency
        freq = 600 - 400 * (t / duration)
        s = 0.4 * math.sin(2 * math.pi * freq * t) * env
        s += 0.3 * math.sin(2 * math.pi * freq * 1.5 * t) * env
        s += 0.2 * math.sin(2 * math.pi * freq * 0.5 * t + 0.3) * env
        samples.append(s * 0.5)
    return samples

def connect_sound():
    """Pleasant connection snap."""
    duration = 0.15
    samples = []
    n = int(SAMPLE_RATE * duration)
    for i in range(n):
        t = i / SAMPLE_RATE
        env = max(0, 1.0 - t / duration)
        # Rising tone
        freq = 800 + 400 * (t / duration)
        s = 0.5 * math.sin(2 * math.pi * freq * t) * env**2
        s += 0.3 * math.sin(2 * math.pi * freq * 2 * t) * env**3
        samples.append(s * 0.6)
    return samples

if __name__ == '__main__':
    out_dir = os.path.join(os.path.dirname(os.path.dirname(__file__)), 'assets', 'sounds')
    os.makedirs(out_dir, exist_ok=True)

    generate_wav(os.path.join(out_dir, 'click.wav'), click_sound())
    generate_wav(os.path.join(out_dir, 'delete.wav'), delete_sound())
    generate_wav(os.path.join(out_dir, 'connect.wav'), connect_sound())

    print("Generated: click.wav, delete.wav, connect.wav")
