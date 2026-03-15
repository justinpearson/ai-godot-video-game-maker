#!/usr/bin/env python3
"""Generate sound effects for Itsy Bitsy Spider game."""

import struct
import wave
import math
import os

SAMPLE_RATE = 44100

def generate_wav(filename, samples):
    with wave.open(filename, 'w') as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SAMPLE_RATE)
        for s in samples:
            clamped = max(-1.0, min(1.0, s))
            w.writeframes(struct.pack('<h', int(clamped * 32767)))

def rain_sound():
    """Gentle rain patter."""
    duration = 0.8
    n = int(SAMPLE_RATE * duration)
    samples = []
    import random
    random.seed(42)
    for i in range(n):
        t = i / SAMPLE_RATE
        env = math.sin(math.pi * t / duration)
        # Mix of soft noise-like frequencies
        s = 0.15 * math.sin(2 * math.pi * 200 * t + random.random())
        s += 0.1 * math.sin(2 * math.pi * 400 * t + random.random() * 2)
        s += 0.08 * math.sin(2 * math.pi * 800 * t + random.random() * 3)
        s += 0.05 * math.sin(2 * math.pi * 1600 * t + random.random() * 4)
        # Add some randomness for rain texture
        s += 0.03 * (random.random() - 0.5)
        samples.append(s * env * 0.7)
    return samples

def splash_sound():
    """Quick water splash."""
    duration = 0.25
    n = int(SAMPLE_RATE * duration)
    samples = []
    import random
    random.seed(99)
    for i in range(n):
        t = i / SAMPLE_RATE
        env = max(0, 1.0 - t / duration) ** 2
        s = 0.4 * math.sin(2 * math.pi * 300 * t) * env
        s += 0.3 * math.sin(2 * math.pi * 600 * t) * env ** 1.5
        s += 0.15 * (random.random() - 0.5) * env
        samples.append(s * 0.6)
    return samples

def alert_sound():
    """Warning beep when spider reaches top."""
    duration = 0.3
    n = int(SAMPLE_RATE * duration)
    samples = []
    for i in range(n):
        t = i / SAMPLE_RATE
        env = math.sin(math.pi * t / duration)
        s = 0.5 * math.sin(2 * math.pi * 880 * t) * env
        s += 0.3 * math.sin(2 * math.pi * 1320 * t) * env
        # Quick vibrato
        s *= (1 + 0.3 * math.sin(2 * math.pi * 15 * t))
        samples.append(s * 0.5)
    return samples

def gameover_sound():
    """Descending sad tones."""
    duration = 1.2
    n = int(SAMPLE_RATE * duration)
    samples = []
    for i in range(n):
        t = i / SAMPLE_RATE
        env = max(0, 1.0 - t / duration) ** 0.5
        # Descending frequency
        freq = 600 - 300 * (t / duration)
        s = 0.4 * math.sin(2 * math.pi * freq * t) * env
        s += 0.2 * math.sin(2 * math.pi * freq * 0.5 * t) * env
        s += 0.15 * math.sin(2 * math.pi * freq * 0.75 * t) * env
        samples.append(s * 0.5)
    return samples

if __name__ == '__main__':
    out_dir = os.path.join(os.path.dirname(os.path.dirname(__file__)), 'assets', 'sounds')
    os.makedirs(out_dir, exist_ok=True)

    generate_wav(os.path.join(out_dir, 'rain.wav'), rain_sound())
    generate_wav(os.path.join(out_dir, 'splash.wav'), splash_sound())
    generate_wav(os.path.join(out_dir, 'alert.wav'), alert_sound())
    generate_wav(os.path.join(out_dir, 'gameover.wav'), gameover_sound())

    print("Generated: rain.wav, splash.wav, alert.wav, gameover.wav")
