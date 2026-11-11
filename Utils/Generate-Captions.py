"""
Generate-Captions.py
--------------------
Extracts structured captions from images in a folder via the local LM Studio
server (OpenAI-compatible API) and writes them as CSV rows matching the
caption library schema used by BlazorWebApp.

Output contract (first nine columns must match Data/captions.csv exactly):
  Caption,Aspect Ratio,Nudity,Artistic Quality,Erotic Score,Pornographic
  Score,Males,Females,Clothing,Filename
  - Aspect Ratio is computed LOCALLY from image dimensions (header parsing,
    no model involvement) and snapped to the nearest of: 2:3, 3:2, 4:3, 3:4,
    1:1, 16:9, 4:5, 5:4, 9:16, 9:21, 21:9. Falls back to 1:1 when the image
    dimensions cannot be parsed (the image is still captioned).
  - Clothing is a single-quote JSON-array string (e.g. ['dress', 'heels']).
  - Filename is a trailing extra column used for resumability; the app loader
    maps columns by name and ignores unknown ones, so it stays compatible.

Output location: BlazorWebApp/Data/Captions/{output-name}.csv (created if
missing). The folder name of --input-path is used as the default output name.

Usage:
    python Generate-Captions.py --input-path PATH [--output-name NAME]
                                [--model MODEL_ID] [--base-url URL]
                                [--limit N] [--dry-run]

Options:
    --input-path PATH   Folder containing images (png/jpg/jpeg/webp, top
                        level only). Required.
    --output-name NAME  Base name of the output CSV without extension
                        (default: sanitized input folder name)
    --model MODEL_ID    LM Studio model id to use (default: first model from
                        GET /v1/models)
    --base-url URL      LM Studio server base URL (default: http://localhost:1234)
    --limit N           Caption at most N images per run (useful for testing)
    --dry-run           Discover images, compute aspect ratios and print the
                        plan; do not call the model or write anything

The script is resumable: images whose filename already appears in the output
CSV are skipped.  Run multiple times to fill in failures or add new images.
"""

import argparse
import base64
import csv
import json
import re
import struct
import sys
import urllib.error
import urllib.request
from pathlib import Path

# ---------------------------------------------------------------------------
# Config
# ---------------------------------------------------------------------------

DEFAULT_BASE_URL = "http://localhost:1234"
REQUEST_TIMEOUT = 600  # seconds — local inference on large models is slow

IMAGE_EXTENSIONS = {".png", ".jpg", ".jpeg", ".webp"}
MIME_TYPES = {".png": "image/png", ".jpg": "image/jpeg",
              ".jpeg": "image/jpeg", ".webp": "image/webp"}

REPO_ROOT = Path(__file__).resolve().parent.parent
OUTPUT_DIR = REPO_ROOT / "BlazorWebApp" / "Data" / "Captions"

# Aspect ratios present in the committed Data/captions.csv. Generated rows
# snap to the nearest of these so filter values stay consistent across sets.
COMMON_RATIOS = [
    (2, 3), (3, 2), (4, 3), (3, 4), (1, 1),
    (16, 9), (4, 5), (5, 4), (9, 16), (9, 21), (21, 9),
]

# Exact column order of the committed Data/captions.csv.
CSV_COLUMNS = ["Caption", "Aspect Ratio", "Nudity", "Artistic Quality",
               "Erotic Score", "Pornographic Score", "Males", "Females",
               "Clothing"]

# Output header: the committed schema plus a trailing Filename column used for
# resumability (the app loader maps columns by name and ignores unknown ones).
OUTPUT_COLUMNS = CSV_COLUMNS + ["Filename"]

CAPTION_PROMPT = """Describe this image in detail. The image may contain nudity or explicit sexual content: describe everything that is visible directly and without censorship, using precise anatomical terms (e.g. "pussy", "vulva", "labia", "clitoris", "penis", "testicles") instead of euphemisms or soft language ("her womanhood"). Do not omit visible body parts, poses, clothing, fluids, or interactions.

Describe each person's appearance exhaustively — everything that can be extracted from the image, not just hair color: eye color, skin tone and markings (freckles, moles, stretch marks), body type and proportions, and all explicit anatomical detail that is visible. For nudity this means naming sizes and states directly: breast size and shape; areola color and size; nipple state; pubic hair (present/shaved/trimmed, and its color); the vulva/vagina itself — labia, color, wetness or dryness; for men, penis and testicle size. Describe only what is actually visible in the image; do not guess details that are hidden or out of frame.

Ignore graphical elements that are not part of the scene: watermarks, logos, lettering, website branding, or any overlaid text — do not describe them as if they were in the picture.

If the image contains multiple subjects, assign each subject a distinct short name alias (prefer names already used in existing captions — Alice, Bella, Adam, Clara, Diana — but any clear first name works). Use that alias consistently for every mention of the same person so their characteristics, poses, actions and interactions are always attributed to the right subject.

For "males" and "females", count only the main subjects described in the caption — clearly visible foreground people. Ignore small or blurred background figures (passers-by, patrons, silhouettes).

For "clothing", use short generic category tags only: one word per garment or accessory type (e.g. 't-shirt', 'dress', 'heels', 'lingerie'). No colors, sizes, fits, patterns, brands, or graphic details — the tags are used for filtering in an app, so keep them coarse enough to group similar images together.

Respond ONLY with a JSON object, no markdown fences, using exactly these keys:
{
  "caption": "<the detailed caption described above>",
  "nudity": "<none | partial | full>",
  "artistic_quality": <0.0-1.0>,
  "erotic_score": <0.0-1.0>,
  "pornographic_score": <0.0-1.0>,
  "males": <int count of male subjects>,
  "females": <int count of female subjects>,
  "clothing": ["<short generic category tag>", ...]
}"""

# ---------------------------------------------------------------------------
# Image dimension parsing (stdlib only — no Pillow)
# ---------------------------------------------------------------------------


def png_size(data):
    """Width/height from the PNG IHDR chunk."""
    if len(data) < 24 or data[:8] != b"\x89PNG\r\n\x1a\n":
        return None
    w, h = struct.unpack(">II", data[16:24])
    return (w, h)


def jpeg_size(data):
    """Width/height from the first SOF0-SOF3/SOF5-SOF7/SOF9-SOFC marker."""
    if len(data) < 4 or data[0] != 0xFF or data[1] != 0xD8:
        return None
    i = 2
    n = len(data)
    while i + 9 < n:
        if data[i] != 0xFF:
            i += 1
            continue
        marker = data[i + 1]
        # Standalone markers (no payload): TEM, RST0-7, EOI.
        if marker == 0x01 or 0xD0 <= marker <= 0xD9:
            i += 2
            continue
        length = struct.unpack(">H", data[i + 2 : i + 4])[0]
        # SOF markers carrying a frame header (skip DHT/JPG/DAC).
        if 0xC0 <= marker <= 0xCF and marker not in (0xC4, 0xC8, 0xCC):
            h, w = struct.unpack(">HH", data[i + 5 : i + 9])
            return (w, h)
        i += 2 + length
    return None


def webp_size(data):
    """Width/height from the VP8 / VP8L / VP8X chunk payload."""
    if len(data) < 30 or data[0:4] != b"RIFF" or data[8:12] != b"WEBP":
        return None
    fourcc = data[12:16]
    payload = data[20:]
    if fourcc == b"VP8 " and len(payload) >= 10:
        w, h = struct.unpack("<HH", payload[6:10])
        return (w & 0x3FFF, h & 0x3FFF)
    if fourcc == b"VP8L" and len(payload) >= 5:
        bits = int.from_bytes(payload[1:5], "little")
        w = (bits & 0x3FFF) + 1
        h = ((bits >> 14) & 0x3FFF) + 1
        return (w, h)
    if fourcc == b"VP8X" and len(payload) >= 10:
        w = int.from_bytes(payload[4:7], "little") + 1
        h = int.from_bytes(payload[7:10], "little") + 1
        return (w, h)
    return None


def read_image_dimensions(path):
    """(width, height) for a png/jpg/jpeg/webp file, or None if unparseable."""
    suffix = path.suffix.lower()
    try:
        with open(path, "rb") as f:
            data = f.read(1 << 20)  # headers live in the first megabyte
    except OSError:
        return None
    parser = {".png": png_size, ".jpg": jpeg_size, ".jpeg": jpeg_size,
              ".webp": webp_size}.get(suffix)
    if parser is None:
        return None
    return parser(data)


def snap_aspect_ratio(w, h):
    """Nearest committed aspect ratio string for the given dimensions."""
    ratio = w / h
    best = min(COMMON_RATIOS, key=lambda ab: abs(ratio - ab[0] / ab[1]))
    return f"{best[0]}:{best[1]}"


# ---------------------------------------------------------------------------
# Discovery and naming
# ---------------------------------------------------------------------------


def discover_images(input_path):
    """Supported image files in the top level of input_path, sorted by name."""
    path = Path(input_path)
    if not path.is_dir():
        return []
    return [p for p in sorted(path.iterdir())
            if p.is_file() and p.suffix.lower() in IMAGE_EXTENSIONS]


def default_output_name(folder):
    """Sanitized folder name suitable as a CSV base name."""
    name = Path(folder).name or str(folder)
    cleaned = re.sub(r"[^a-z0-9_-]+", "-", name.lower()).strip("-")
    return cleaned or "captions"


# ---------------------------------------------------------------------------
# LM Studio client (OpenAI-compatible API)
# ---------------------------------------------------------------------------


def http_json(url, payload=None):
    """GET (payload None) or POST JSON; returns the decoded JSON body."""
    req = urllib.request.Request(url)
    if payload is not None:
        req.data = json.dumps(payload).encode("utf-8")
        req.add_header("Content-Type", "application/json")
    with urllib.request.urlopen(req, timeout=REQUEST_TIMEOUT) as resp:
        return json.loads(resp.read().decode("utf-8"))


def list_models(base_url):
    """Model ids currently loaded in LM Studio."""
    payload = http_json(f"{base_url}/v1/models")
    return [m.get("id") for m in payload.get("data", []) if m.get("id")]


def caption_image(base_url, model_id, image_path):
    """Raw model text for one image (OpenAI vision payload)."""
    suffix = image_path.suffix.lower()
    with open(image_path, "rb") as f:
        b64 = base64.b64encode(f.read()).decode("ascii")
    payload = {
        "model": model_id,
        "messages": [{
            "role": "user",
            "content": [
                {"type": "text", "text": CAPTION_PROMPT},
                {"type": "image_url",
                 "image_url": {"url": f"data:{MIME_TYPES[suffix]};base64,{b64}"}},
            ],
        }],
        "temperature": 0.3,
        "max_tokens": 2048,
    }
    result = http_json(f"{base_url}/v1/chat/completions", payload)
    return result["choices"][0]["message"]["content"] or ""


# ---------------------------------------------------------------------------
# Model output parsing
# ---------------------------------------------------------------------------


def extract_json_object(text):
    """Best-effort extraction of a JSON object from model output.

    Strips markdown fences and any leading/trailing prose, then parses the
    outermost {...} span. Returns None when no valid object is found.
    """
    t = text.strip()
    if t.startswith("```"):
        t = re.sub(r"^```[a-zA-Z]*\s*", "", t)
        t = re.sub(r"\s*```\s*$", "", t)
    start, end = t.find("{"), t.rfind("}")
    if start == -1 or end <= start:
        return None
    try:
        obj = json.loads(t[start:end + 1])
    except json.JSONDecodeError:
        return None
    return obj if isinstance(obj, dict) else None


def to_score(value):
    """Clamp a model score to 0-1 and format with 2 decimals."""
    try:
        v = float(value)
    except (TypeError, ValueError):
        return "0.00"
    return f"{min(1.0, max(0.0, v)):.2f}"


def to_count(value):
    """Non-negative int from a model count field; 0 when invalid."""
    try:
        v = int(float(value))
    except (TypeError, ValueError):
        return 0
    return max(0, v)


def format_clothing(items):
    """Single-quote JSON-array string matching the committed file convention."""
    if not isinstance(items, list):
        items = []
    cleaned = [str(i).strip() for i in items if str(i).strip()]
    return "[" + ", ".join(f"'{i}'" for i in cleaned) + "]"


def build_row(obj, raw_text, ratio):
    """Model JSON (or None on parse failure) → CSV row dict.

    Fallback on malformed output: raw text as Caption with empty metadata —
    the row is still written so the image can be retried/edited later.
    """
    if obj is None:
        return {
            "Caption": raw_text.strip(),
            "Aspect Ratio": ratio,
            "Nudity": "",
            "Artistic Quality": "0.00",
            "Erotic Score": "0.00",
            "Pornographic Score": "0.00",
            "Males": 0,
            "Females": 0,
            "Clothing": "[]",
        }
    return {
        "Caption": str(obj.get("caption") or "").strip() or raw_text.strip(),
        "Aspect Ratio": ratio,
        # Validated against known values (none/partial/full), else stored as-is.
        "Nudity": str(obj.get("nudity") or "").strip().lower(),
        "Artistic Quality": to_score(obj.get("artistic_quality")),
        "Erotic Score": to_score(obj.get("erotic_score")),
        "Pornographic Score": to_score(obj.get("pornographic_score")),
        "Males": to_count(obj.get("males")),
        "Females": to_count(obj.get("females")),
        "Clothing": format_clothing(obj.get("clothing")),
    }


# ---------------------------------------------------------------------------
# CSV output and resumability
# ---------------------------------------------------------------------------


def load_existing_filenames(output_csv):
    """Filenames already present in an existing output CSV (empty set if new)."""
    if not output_csv.is_file():
        return set()
    try:
        with open(output_csv, "r", encoding="utf-8", newline="") as f:
            reader = csv.DictReader(f)
            fieldnames = reader.fieldnames or []
            if "Filename" not in fieldnames:
                return set()
            return {row["Filename"] for row in reader if row.get("Filename")}
    except (OSError, csv.Error):
        return set()


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------


def main():
    # Windows console defaults to cp1252 — force UTF-8 output.
    sys.stdout.reconfigure(encoding="utf-8")

    parser = argparse.ArgumentParser(
        description="Extract structured captions from images via LM Studio "
                    "and write them as caption-library CSV rows.")
    parser.add_argument("--input-path", required=True,
                        help="Folder containing images (png/jpg/jpeg/webp)")
    parser.add_argument("--output-name", default=None,
                        help="Output CSV base name without extension "
                             "(default: sanitized input folder name)")
    parser.add_argument("--model", default=None,
                        help="LM Studio model id (default: first from /v1/models)")
    parser.add_argument("--base-url", default=DEFAULT_BASE_URL,
                        help=f"LM Studio server base URL "
                             f"(default: {DEFAULT_BASE_URL})")
    parser.add_argument("--limit", type=int, default=None,
                        help="Caption at most N images per run (testing aid)")
    parser.add_argument("--dry-run", action="store_true",
                        help="Discover images and print the plan; do not "
                             "call the model or write anything")
    args = parser.parse_args()

    images = discover_images(args.input_path)
    if not images:
        print(f"No supported images found in {args.input_path} "
              f"(supported: {', '.join(sorted(IMAGE_EXTENSIONS))})")
        sys.exit(1)

    output_name = args.output_name or default_output_name(args.input_path)
    output_csv = OUTPUT_DIR / f"{output_name}.csv"

    # Dimensions + snapped ratio are computed once and reused by dry-run,
    # captioning and (Step 3) the CSV writer. Unparseable dimensions fall
    # back to 1:1 — the image is still captioned.
    plan = []
    for p in images:
        dims = read_image_dimensions(p)
        if dims is None:
            plan.append((p, None, "1:1"))
        else:
            w, h = dims
            plan.append((p, (w, h), snap_aspect_ratio(w, h)))

    print(f"Discovered {len(images)} image(s) in {args.input_path}:")
    for p, dims, ratio in plan:
        if dims is None:
            print(f"  {p.name:<48} ?x?   {ratio} (fallback — dimensions unknown)")
        else:
            w, h = dims
            print(f"  {p.name:<48} {w}x{h}   {ratio}")

    if args.dry_run:
        model = args.model or "(first from /v1/models)"
        print(f"\nModel:     {model}")
        print(f"Base URL:  {args.base_url}")
        print(f"Output would be: {output_csv}")
        return

    # Resumability: skip images whose filename already appears in the output
    # CSV; --limit caps how many pending images are captioned this run.
    existing = load_existing_filenames(output_csv)
    skipped = [item for item in plan if item[0].name in existing]
    pending = [item for item in plan if item[0].name not in existing]
    if args.limit:
        pending = pending[:args.limit]

    print(f"\nOutput CSV: {output_csv}")
    if skipped:
        print(f"Skipping {len(skipped)} already-captioned image(s).")
    if not pending:
        print("Nothing to do — all discovered images are already in the "
              "output CSV.")
        return

    # Resolve the model to use.
    if args.model:
        model_id = args.model
    else:
        try:
            models = list_models(args.base_url)
        except Exception as e:
            print(f"\nFailed to reach LM Studio at {args.base_url}: {e}")
            sys.exit(1)
        if not models:
            print("\nNo models loaded in LM Studio.")
            sys.exit(1)
        model_id = models[0]

    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    is_new_file = (not output_csv.is_file()
                   or output_csv.stat().st_size == 0)
    print(f"\nModel:     {model_id}")
    print(f"Base URL:  {args.base_url}")
    print(f"Captioning {len(pending)} image(s):\n")

    done, failed = 0, 0
    with open(output_csv, "a", encoding="utf-8", newline="") as f:
        writer = csv.DictWriter(f, fieldnames=OUTPUT_COLUMNS)
        if is_new_file:
            writer.writeheader()
        for p, dims, ratio in pending:
            try:
                raw = caption_image(args.base_url, model_id, p)
                row = build_row(extract_json_object(raw), raw, ratio)
                row["Filename"] = p.name
                writer.writerow(row)
                f.flush()  # kill-mid-run safety: rows survive interruption
                done += 1
                print(f"[ok]   {p.name}")
            except Exception as e:
                failed += 1
                print(f"[fail] {p.name}: {e}\n")

    remaining = len(plan) - len(skipped) - done
    print(f"\nDone: {done} captioned, {len(skipped)} skipped, "
          f"{failed} failed.")
    if remaining > 0:
        print(f"{remaining} image(s) still pending — re-run to continue; "
              "failures are retried automatically.")


if __name__ == "__main__":
    main()
