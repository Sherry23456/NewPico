#!/usr/bin/env python3
"""
Synthesize three seamless-loop ambient WAV files for VR farm project.
Pure Python (standard library only): wave, math, random, struct.
Output: Assets/Farm/Audio/ambient/
"""

import wave
import struct
import math
import random
import os

SAMPLE_RATE = 22050
BIT_DEPTH = 16
CHANNELS = 1
OUTPUT_DIR = os.path.join(os.path.dirname(__file__), "..", "Assets", "Farm", "Audio", "ambient")

def write_wav(filename, samples, sample_rate=SAMPLE_RATE):
    """Write samples (list of floats in [-1,1]) to 16-bit mono WAV."""
    filepath = os.path.join(OUTPUT_DIR, filename)
    with wave.open(filepath, 'w') as wf:
        wf.setnchannels(CHANNELS)
        wf.setsampwidth(BIT_DEPTH // 8)
        wf.setframerate(sample_rate)
        for s in samples:
            s = max(-1.0, min(1.0, s))
            wf.writeframes(struct.pack('<h', int(s * 32767)))
    return filepath

def crossfade_loop(samples, fade_ms=200, sample_rate=SAMPLE_RATE):
    """Make samples seamlessly loop by crossfading the tail into the head."""
    n = len(samples)
    fade_len = int(sample_rate * fade_ms / 1000.0)
    if fade_len * 2 > n:
        fade_len = n // 2
    result = list(samples)
    for i in range(fade_len):
        fade_out = 1.0 - (i / fade_len)
        fade_in = i / fade_len
        tail_idx = n - fade_len + i
        head_val = result[i]
        tail_val = result[tail_idx]
        result[i] = head_val * fade_in + tail_val * fade_out
        result[tail_idx] = tail_val * fade_in + head_val * fade_out
    return result

def fm_sweep(t, f_start, f_end, duration, phase=0.0):
    """Generate a single FM sweep sample at time t."""
    if t < 0 or t > duration:
        return 0.0
    freq = f_start + (f_end - f_start) * (t / duration)
    return math.sin(2 * math.pi * freq * t + phase)

def generate_birds_day(duration_s=8):
    """Generate daytime bird chirps with FM sweeps."""
    n_samples = int(SAMPLE_RATE * duration_s)
    samples = [0.0] * n_samples

    # Background: very light high-pass noise
    noise_level = 0.02
    prev_noise = 0.0
    for i in range(n_samples):
        white = random.uniform(-1, 1)
        # Simple high-pass: difference of current and previous
        hp = white - prev_noise * 0.95
        prev_noise = white
        samples[i] += hp * noise_level

    # Generate 5-8 chirp clusters
    n_clusters = random.randint(5, 8)
    cluster_start = 0.3  # start after 300ms

    for c in range(n_clusters):
        if cluster_start >= duration_s - 0.5:
            break
        n_chirps = random.randint(2, 5)
        t = cluster_start

        for ch in range(n_chirps):
            chirp_dur = random.uniform(0.06, 0.12)
            f_start = random.uniform(2500, 4200)
            f_end = f_start + random.uniform(-300, 500)
            f_end = max(2500, min(4200, f_end))
            amplitude = random.uniform(0.15, 0.35)
            phase = random.uniform(0, 2 * math.pi)

            start_idx = int(t * SAMPLE_RATE)
            end_idx = int((t + chirp_dur) * SAMPLE_RATE)

            for i in range(start_idx, min(end_idx, n_samples)):
                local_t = (i - start_idx) / SAMPLE_RATE
                # Envelope: quick attack, quick release
                env = 1.0
                attack = 0.01
                release = 0.02
                if local_t < attack:
                    env = local_t / attack
                elif local_t > chirp_dur - release:
                    env = (chirp_dur - local_t) / release

                sample = fm_sweep(local_t, f_start, f_end, chirp_dur, phase) * amplitude * env
                if i < n_samples:
                    samples[i] += sample

            t += chirp_dur + random.uniform(0.08, 0.15)

        cluster_start += random.uniform(0.8, 2.2)

    # Normalize
    peak = max(abs(s) for s in samples)
    if peak > 0:
        samples = [s / peak * 0.8 for s in samples]

    # Make seamless
    samples = crossfade_loop(samples, fade_ms=200)
    return samples

def generate_crickets_night(duration_s=8):
    """Generate cricket chirping with gated tremolo - naturally loopable."""
    n_samples = int(SAMPLE_RATE * duration_s)
    samples = [0.0] * n_samples

    # Background: very quiet low noise
    noise_level = 0.015
    for i in range(n_samples):
        samples[i] += random.uniform(-1, 1) * noise_level

    # 2-3 cricket layers with different tremolo rates
    n_layers = random.randint(2, 3)
    for layer in range(n_layers):
        carrier_freq = 4200.0  # 4.2 kHz
        # Choose tremolo rate so that duration_s contains integer cycles
        # This ensures natural loop alignment
        base_rate = random.uniform(22, 28)  # 22-28 Hz
        n_cycles = round(base_rate * duration_s)
        tremolo_rate = n_cycles / duration_s  # Exact integer cycles

        phase_offset = random.uniform(0, 2 * math.pi)
        base_amplitude = random.uniform(0.12, 0.25)

        # Volume fluctuation - also make loopable
        base_vol_rate = random.uniform(0.3, 0.8)
        n_vol_cycles = max(1, round(base_vol_rate * duration_s))
        vol_mod_rate = n_vol_cycles / duration_s
        vol_mod_depth = random.uniform(0.3, 0.6)

        for i in range(n_samples):
            t = i / SAMPLE_RATE

            # Tremolo gate: sin^2 for sharp on/off
            tremolo = math.sin(2 * math.pi * tremolo_rate * t + phase_offset)
            gate = max(0, tremolo) ** 2  # Half-wave rectified, squared

            # Slow volume modulation (loopable)
            vol_mod = 1.0 - vol_mod_depth * (0.5 + 0.5 * math.sin(2 * math.pi * vol_mod_rate * t + phase_offset))

            # Carrier
            carrier = math.sin(2 * math.pi * carrier_freq * t)

            sample = carrier * gate * base_amplitude * vol_mod
            samples[i] += sample

    # Normalize
    peak = max(abs(s) for s in samples)
    if peak > 0:
        samples = [s / peak * 0.75 for s in samples]

    # Longer crossfade to blend random noise component
    samples = crossfade_loop(samples, fade_ms=150)
    return samples

def generate_rain(duration_s=10):
    """Generate rain with low-pass noise base and random water ticks."""
    n_samples = int(SAMPLE_RATE * duration_s)
    samples = [0.0] * n_samples

    # Base: low-pass white noise (rain bed)
    noise_amp = 0.25
    prev1 = 0.0
    prev2 = 0.0
    for i in range(n_samples):
        white = random.uniform(-1, 1)
        # Two-pole low-pass filter
        lp = prev1 * 0.3 + prev2 * 0.2 + white * 0.5
        prev2 = prev1
        prev1 = lp
        samples[i] += lp * noise_amp

    # Random water ticks: 8-15 per second
    tick_rate = random.uniform(8, 15)
    n_ticks = int(duration_s * tick_rate)

    for _ in range(n_ticks):
        tick_time = random.uniform(0, duration_s)
        tick_dur = random.uniform(0.005, 0.02)  # 5-20ms
        tick_freq = random.uniform(2000, 6000)
        tick_amp = random.uniform(0.08, 0.2)

        start_idx = int(tick_time * SAMPLE_RATE)
        end_idx = int((tick_time + tick_dur) * SAMPLE_RATE)

        for i in range(start_idx, min(end_idx, n_samples)):
            local_t = (i - start_idx) / SAMPLE_RATE
            # Quick envelope
            env = math.exp(-local_t * 80)  # Fast decay
            tick = math.sin(2 * math.pi * tick_freq * local_t) * tick_amp * env
            samples[i] += tick

    # Normalize
    peak = max(abs(s) for s in samples)
    if peak > 0:
        samples = [s / peak * 0.85 for s in samples]

    # Make seamless with longer crossfade for rain
    samples = crossfade_loop(samples, fade_ms=300)
    return samples

def verify_wav(filepath):
    """Verify WAV file and print stats."""
    with wave.open(filepath, 'r') as wf:
        n_frames = wf.getnframes()
        sample_rate = wf.getframerate()
        duration = n_frames / sample_rate
        filesize = os.path.getsize(filepath)

        # Read all samples
        raw = wf.readframes(n_frames)
        samples = struct.unpack(f'<{n_frames}h', raw)

        # Check loop seamlessness: compare RMS of first 100ms vs last 100ms
        check_len = int(sample_rate * 0.1)
        head = samples[:check_len]
        tail = samples[-check_len:]

        rms_head = math.sqrt(sum(s*s for s in head) / len(head))
        rms_tail = math.sqrt(sum(s*s for s in tail) / len(tail))

        if rms_head > 0 and rms_tail > 0:
            rms_diff_pct = abs(rms_head - rms_tail) / max(rms_head, rms_tail) * 100
        else:
            rms_diff_pct = 0.0

        print(f"  {os.path.basename(filepath)}:")
        print(f"    Size: {filesize} bytes")
        print(f"    Duration: {duration:.2f}s")
        print(f"    Sample rate: {sample_rate} Hz")
        print(f"    Head RMS (first 100ms): {rms_head:.4f}")
        print(f"    Tail RMS (last 100ms): {rms_tail:.4f}")
        print(f"    RMS diff: {rms_diff_pct:.2f}% {'✓' if rms_diff_pct < 5 else '✗'}")

def main():
    os.makedirs(OUTPUT_DIR, exist_ok=True)

    print("Generating birds_day.wav...")
    birds = generate_birds_day(8)
    birds_path = write_wav("birds_day.wav", birds)
    print(f"  Written: {birds_path}")

    print("\nGenerating crickets_night.wav...")
    crickets = generate_crickets_night(8)
    crickets_path = write_wav("crickets_night.wav", crickets)
    print(f"  Written: {crickets_path}")

    print("\nGenerating rain.wav...")
    rain = generate_rain(10)
    rain_path = write_wav("rain.wav", rain)
    print(f"  Written: {rain_path}")

    print("\n" + "="*50)
    print("Verification:")
    print("="*50)
    verify_wav(birds_path)
    verify_wav(crickets_path)
    verify_wav(rain_path)

    print("\n" + "="*50)
    print("Summary:")
    print("="*50)
    for p in [birds_path, crickets_path, rain_path]:
        print(f"  ✓ {os.path.basename(p)}")

if __name__ == "__main__":
    main()
