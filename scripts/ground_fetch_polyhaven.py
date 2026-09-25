#!/usr/bin/env python3
# DESIGN-TIME ONLY - not compiled into the Unity build, never shipped.
#
# ground_fetch_polyhaven.py -- pull the dry-register ground sets from Poly Haven at 2K.
#
# The six sets Todd supplied were 1K and mostly leaf litter; Poly Haven has 862 texture sets and the
# ones that actually read as a harvested October cornfield are different assets (withered grass, dry
# mud field, decayed leaves, farm soil). This fetches those at 2K, hashes every file it lands, and
# writes a manifest so the provenance record cites real bytes rather than asset names.
#
# Downloads go OUTSIDE the repo, into ../downloads/ground_textures_ph/ (the same place Todd's own
# batch lives), so the repo carries the licence record and the derived output, not the raw sources.
import hashlib, json, os, urllib.request

UA = {"User-Agent": "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7)"}
DEST = "/Volumes/files1/projects/cornmaze/downloads/ground_textures_ph"
RES = "2k"

# the dry-October field, and the lane that crosses it
WANTED = {
    "dry_mud_field_001":   "field base -- the bare earth of a cut cornfield",
    "withered_grass":      "field -- dead standing grass, the crop that is over",
    "dry_decay_leaves":    "field -- leaves gone brown and fallen",
    "stony_dirt_path":     "lane base -- a dirt track with stones in it",
    "rocky_gravel":        "lane -- loose worn stones",
    "gravel_road":         "lane -- the compacted crown of the track",
}
# Poly Haven map key -> our suffix
MAPS = {"Diffuse": "diff", "nor_gl": "nor_gl", "Rough": "rough"}


def get(url):
    return json.loads(urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=60).read())


manifest = {}
for aid, why in WANTED.items():
    try:
        files = get(f"https://api.polyhaven.com/files/{aid}")
    except Exception as e:
        print(f"!! {aid}: files lookup failed ({type(e).__name__})")
        continue
    out = os.path.join(DEST, f"{aid}_{RES}")
    os.makedirs(out, exist_ok=True)
    got = {}
    for map_key, suffix in MAPS.items():
        try:
            entry = files[map_key][RES]["jpg"]
            url, size = entry["url"], entry["size"]
        except (KeyError, TypeError):
            print(f"!! {aid}: no {map_key} at {RES} -- available: {list(files.get(map_key, {}).keys())}")
            continue
        dest = os.path.join(out, f"{aid}_{suffix}_{RES}.jpg")
        if not os.path.exists(dest) or os.path.getsize(dest) != size:
            with urllib.request.urlopen(urllib.request.Request(url, headers=UA), timeout=180) as r, \
                 open(dest, "wb") as fh:
                while True:
                    chunk = r.read(1 << 20)
                    if not chunk:
                        break
                    fh.write(chunk)
        digest = hashlib.sha256(open(dest, "rb").read()).hexdigest()
        got[suffix] = {"file": os.path.basename(dest), "bytes": os.path.getsize(dest), "sha256": digest}
        print(f"  {aid:20s} {suffix:6s} {os.path.getsize(dest)/1e6:6.2f} MB  {digest[:16]}")
    manifest[aid] = {"why": why, "resolution": RES, "maps": got}

json.dump(manifest, open(os.path.join(DEST, "manifest.json"), "w"), indent=1)
print(f"\n wrote {os.path.join(DEST, 'manifest.json')}  ({len(manifest)} sets)")
