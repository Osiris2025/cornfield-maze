#!/usr/bin/env python3
"""
DESIGN-TIME ONLY — not compiled into the Unity build, never shipped.

M25b (§25.5): measure the moon's disc in a CAPTURED FRAME.

The milestone's whole point is that the moon was a blob on screen, so the evidence cannot be an engine
number read off the object it built — it has to come out of the pixels Todd would look at. This decodes the
PNG itself (no PIL/numpy on this box: stdlib zlib + struct, which also means there is nothing to hide
behind) and measures the bright disc: its bounding box, its equivalent diameter, and how much it changes
with the threshold, because a soft limb means the answer depends on where you draw the line.

Then it compares that against the camera projection the built app reported, and appends the result to the
milestone report. Both numbers go in the same file, so "they agree" is checkable rather than asserted.

Usage:
  python3 scripts/m25b_measure_moon.py FRAME.png --label t40 \
      --expected-px 195.4 --expected-deg 7.00 --disc-fraction 0.62 --report artifacts/m25b-moon-report.txt
"""
import argparse
import struct
import sys
import zlib

CHANNELS = {0: 1, 2: 3, 3: 1, 4: 2, 6: 4}


def decode_luma(path):
    """Decode a non-interlaced 8-bit PNG into a list of bytearrays of luminance, one per row."""
    data = open(path, "rb").read()
    if data[:8] != b"\x89PNG\r\n\x1a\n":
        raise SystemExit(f"{path} is not a PNG")
    pos, idat, plte = 8, [], None
    w = h = bd = ct = interlace = None
    while pos < len(data):
        ln = struct.unpack(">I", data[pos:pos + 4])[0]
        typ = data[pos + 4:pos + 8]
        body = data[pos + 8:pos + 8 + ln]
        pos += 12 + ln
        if typ == b"IHDR":
            w, h, bd, ct, _comp, _filt, interlace = struct.unpack(">IIBBBBB", body)
        elif typ == b"IDAT":
            idat.append(body)
        elif typ == b"PLTE":
            plte = body
        elif typ == b"IEND":
            break
    if interlace:
        raise SystemExit(f"{path} is interlaced — not supported")
    if bd != 8:
        raise SystemExit(f"{path} is {bd}-bit — expected 8")
    ch = CHANNELS.get(ct)
    if ch is None:
        raise SystemExit(f"{path} colour type {ct} — not supported")

    raw = zlib.decompress(b"".join(idat))
    stride = w * ch
    prev = bytearray(stride)
    rows = []
    p = 0
    for _y in range(h):
        f = raw[p]
        p += 1
        cur = bytearray(raw[p:p + stride])
        p += stride
        if f == 1:
            for i in range(ch, stride):
                cur[i] = (cur[i] + cur[i - ch]) & 255
        elif f == 2:
            for i in range(stride):
                cur[i] = (cur[i] + prev[i]) & 255
        elif f == 3:
            for i in range(stride):
                a = cur[i - ch] if i >= ch else 0
                cur[i] = (cur[i] + ((a + prev[i]) >> 1)) & 255
        elif f == 4:
            for i in range(stride):
                a = cur[i - ch] if i >= ch else 0
                b = prev[i]
                c = prev[i - ch] if i >= ch else 0
                pa = b - c
                if pa < 0: pa = -pa
                pb = a - c
                if pb < 0: pb = -pb
                pc = a + b - 2 * c
                if pc < 0: pc = -pc
                pr = a if (pa <= pb and pa <= pc) else (b if pb <= pc else c)
                cur[i] = (cur[i] + pr) & 255
        prev = cur
        rows.append(cur)

    # luminance, per pixel; Rec.709 weights, integer maths
    if ch == 1:
        return w, h, [bytearray(r) for r in rows]
    luma = []
    for r in rows:
        row = bytearray(w)
        if ch == 3:
            for x in range(w):
                i = x * 3
                row[x] = (r[i] * 54 + r[i + 1] * 183 + r[i + 2] * 19) >> 8
        elif ch == 4:
            for x in range(w):
                row[x] = r[x * 4]
        else:
            for x in range(w):
                i = x * 4
                row[x] = (r[i] * 54 + r[i + 1] * 183 + r[i + 2] * 19) >> 8
        luma.append(row)
    return w, h, luma


def largest_blob(luma, w, h, y_limit, thresh):
    """Largest 4-connected component at or above thresh, restricted to rows [0, y_limit)."""
    seen = bytearray(w * h)
    best = None
    for y0 in range(y_limit):
        row = luma[y0]
        base = y0 * w
        for x0 in range(w):
            if row[x0] < thresh or seen[base + x0]:
                continue
            stack = [base + x0]
            seen[base + x0] = 1
            area = 0
            minx = maxx = x0
            miny = maxy = y0
            while stack:
                q = stack.pop()
                qy, qx = divmod(q, w)
                area += 1
                if qx < minx: minx = qx
                if qx > maxx: maxx = qx
                if qy < miny: miny = qy
                if qy > maxy: maxy = qy
                if qx > 0:
                    n = q - 1
                    if luma[qy][qx - 1] >= thresh and not seen[n]:
                        seen[n] = 1
                        stack.append(n)
                if qx < w - 1:
                    n = q + 1
                    if luma[qy][qx + 1] >= thresh and not seen[n]:
                        seen[n] = 1
                        stack.append(n)
                if qy > 0:
                    n = q - w
                    if luma[qy - 1][qx] >= thresh and not seen[n]:
                        seen[n] = 1
                        stack.append(n)
                if qy < h - 1:
                    n = q + w
                    if luma[qy + 1][qx] >= thresh and not seen[n]:
                        seen[n] = 1
                        stack.append(n)
            if best is None or area > best["area"]:
                best = {"area": area, "minx": minx, "maxx": maxx, "miny": miny, "maxy": maxy,
                        "cx": (minx + maxx) / 2.0, "cy": (miny + maxy) / 2.0}
    return best


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("frame")
    ap.add_argument("--label", default="?")
    ap.add_argument("--expected-px", type=float, default=None,
                    help="disc diameter in px the built app's camera projection predicts")
    ap.add_argument("--expected-deg", type=float, default=7.00)
    ap.add_argument("--disc-fraction", type=float, default=0.62)
    ap.add_argument("--report", default=None)
    ap.add_argument("--headline", type=float, default=0.50,
                    help="threshold ratio used for the headline measurement (0.50 = the limb at half the "
                         "disc's own peak: a photograph's maria are darker than its highlands, so a high "
                         "threshold measures only the bright core and under-reports the disc)")
    ap.add_argument("--centre-x", type=float, default=None,
                    help="the moon's screen x from the harness aim; restricts the search to a box")
    ap.add_argument("--centre-y", type=float, default=None,
                    help="the moon's screen y from the harness aim")
    args = ap.parse_args()

    w, h, luma = decode_luma(args.frame)

    # M25b: if the aim told us where the moon is, measure THERE. A torn cloud can out-brighten the moon (it
    # did: the global peak sat on a cloud 600 px away and the headline number came out 71 % of projection),
    # and a threshold relative to a cloud peak measures the cloud. Inside the box the moon is the brightest
    # thing, so the threshold is relative to the moon.
    box = None
    if args.centre_x is not None and args.centre_y is not None:
        span = max(40.0, (args.expected_px or 83.0) * 1.6)
        x0 = int(max(0, args.centre_x - span))
        x1 = int(min(w, args.centre_x + span))
        y0 = int(max(0, args.centre_y - span))
        y1 = int(min(h, args.centre_y + span))
        box = (x0, y0)
        luma = [row[x0:x1] for row in luma[y0:y1]]
        w, h = x1 - x0, y1 - y0

    y_limit = int(h * 0.95)
    peak = 0
    for y in range(y_limit):
        m = max(luma[y])
        if m > peak:
            peak = m

    lines = []
    lines.append("")
    lines.append(f"--- frame measurement ({args.label}): {args.frame.split('/')[-1]}  {w}x{h} ---")
    lines.append(f"decoded in python from the file (stdlib zlib, no image library); sky scanned = top {y_limit} rows"
                 + (f"; search box {w}x{h} at ({box[0]},{box[1]}) centred on the harness aim" if box else ""))
    lines.append(f"peak sky luminance = {peak}/255")
    if peak < 90:
        lines.append("VERDICT: FAIL — nothing in the sky is bright enough to be a moon disc")
        out = "\n".join(lines)
        print(out)
        if args.report:
            open(args.report, "a").write(out + "\n")
        sys.exit(1)

    lines.append("threshold sensitivity (brightest connected region):")
    rows = {}
    for ratio in (0.40, 0.45, 0.50, 0.55, 0.60, 0.70, 0.85):
        blob = largest_blob(luma, w, h, y_limit, int(peak * ratio))
        if blob is None:
            lines.append(f"  >={ratio:.2f}*peak : none")
            continue
        bw = blob["maxx"] - blob["minx"] + 1
        bh = blob["maxy"] - blob["miny"] + 1
        eq = (4.0 * blob["area"] / 3.141592653589793) ** 0.5
        rows[ratio] = dict(bw=bw, bh=bh, area=blob["area"], eq=eq, cx=blob["cx"], cy=blob["cy"],
                           minx=blob["minx"], miny=blob["miny"])
        lines.append(f"  >={ratio:.2f}*peak : bbox {bw}x{bh} px, area {blob['area']} px, "
                     f"equivalent diameter {eq:.1f} px, centre "
                     f"({blob['cx'] + (box[0] if box else 0):.0f},{blob['cy'] + (box[1] if box else 0):.0f})")

    if args.headline not in rows:
        lines.append(f"VERDICT: FAIL — no disc at the headline threshold {args.headline}")
        out = "\n".join(lines)
        print(out)
        if args.report:
            open(args.report, "a").write(out + "\n")
        sys.exit(1)

    head = rows[args.headline]
    bw, bh = head["bw"], head["bh"]
    others = [r["bw"] for rat, r in rows.items()]
    lines.append(f"headline disc width = {bw} px (bbox at >={args.headline:.2f}*peak); "
                 f"widths across thresholds = {others}")
    lines.append(f"disc height/width = {bh / bw:.2f} (the oblate squash is §25.5 — expect ~0.62 low, ~1.00 high)")
    if head["minx"] <= 0 or head["minx"] + bw >= w or head["miny"] <= 0:
        lines.append("NOTE: the disc touches the frame edge — the true disc may be larger than measured")

    ok = True
    if args.expected_px:
        exp = args.expected_px
        ratio = bw / exp
        deg_measured = args.expected_deg * ratio
        quad_expected_px = exp / args.disc_fraction
        achieved = bw / quad_expected_px
        lines.append(f"angular size in degrees:  designed {args.expected_deg:.2f} deg  ->  "
                     f"as measured on this frame {deg_measured:.2f} deg")
        lines.append(f"pixel diameter:           measured {bw} px  vs  camera projection {exp:.1f} px  "
                     f"= {ratio * 100:.1f}%  (agree within 8%: {'yes' if abs(ratio - 1) <= 0.08 else 'NO'})")
        lines.append(f"disc fraction honoured:   achieved {achieved:.2f} (quad expected {quad_expected_px:.1f} px) "
                     f"vs {args.disc_fraction:.2f} built  ->  {'yes' if abs(achieved - args.disc_fraction) <= 0.05 else 'NO'}")
        ok = abs(ratio - 1) <= 0.08 and abs(achieved - args.disc_fraction) <= 0.05
    lines.append(f"VERDICT ({args.label}): {'PASS' if ok else 'FAIL'} — "
                 f"disc {bw}px wide on a {w}x{h} frame")

    out = "\n".join(lines)
    print(out)
    if args.report:
        with open(args.report, "a") as fh:
            fh.write(out + "\n")
        print(f"appended to {args.report}")


if __name__ == "__main__":
    main()
