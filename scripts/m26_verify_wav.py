#!/usr/bin/env python3
"""
DESIGN-TIME ONLY - not compiled into the Unity build, never shipped.

Checks the M26 (§25.6) listen artifact the way a listener would: it decodes the WAV that the BUILT
app wrote, measures the energy of each 1 s step, and reports whether the field actually gets louder
as the Husk closes. It reads the file, not the game's own report — so a harness that merely *said*
the audio rose cannot pass this.

Usage:
  python3 scripts/m26_verify_wav.py "/Users/<you>/Library/Application Support/arl480/Corn Field Maze/m26-threat-listen.wav"
"""

import sys
import wave

STEPS_CELLS = [8.0, 6.0, 4.0, 3.0, 2.0, 1.5]
STEP_SECONDS = 1.0


def rms(frames):
    if not frames:
        return 0.0
    return (sum(float(s) * float(s) for s in frames) / len(frames)) ** 0.5


def main():
    if len(sys.argv) < 2:
        print("usage: m26_verify_wav.py <path-to-m26-threat-listen.wav>")
        return 2
    path = sys.argv[1]

    with wave.open(path, "rb") as w:
        channels = w.getnchannels()
        rate = w.getframerate()
        width = w.getsampwidth()
        count = w.getnframes()
        raw = w.readframes(count)

    if width != 2:
        print("FAIL: expected 16-bit PCM, got %d bytes per sample" % width)
        return 1

    samples = [int.from_bytes(raw[i:i + 2], "little", signed=True) for i in range(0, len(raw), 2)]
    print("file      : %s" % path)
    print("format    : %d ch, %d Hz, %d-bit, %.2f s" % (channels, rate, width * 8, count / float(rate)))

    per_step = int(rate * STEP_SECONDS) * channels
    if len(samples) < per_step * len(STEPS_CELLS):
        print("FAIL: file is shorter than the %d s sweep it claims" % len(STEPS_CELLS))
        return 1

    print("")
    print("  cells |  RMS     | relative")
    print("  ------|----------|---------")
    values = []
    for i, cells in enumerate(STEPS_CELLS):
        chunk = samples[i * per_step:(i + 1) * per_step]
        value = rms(chunk)
        values.append(value)
        print("  %5.1f | %8.5f | %6.2fx" % (cells, value, value / values[0] if values[0] else 0.0))

    far, near = values[0], values[-1]
    rose = near > far * 1.05
    print("")
    print("far (%.1f cells) %.5f -> near (%.1f cells) %.5f = %.2fx"
          % (STEPS_CELLS[0], far, STEPS_CELLS[-1], near, near / far if far else 0.0))
    print("verdict: %s" % ("PASS - the field's own mix rises as the threat closes" if rose
                           else "FAIL - the mix does not change with proximity"))
    return 0 if rose else 1


if __name__ == "__main__":
    sys.exit(main())
