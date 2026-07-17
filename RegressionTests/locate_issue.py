"""Find WHERE a localized decode signature lives, so a short issue-complete clip can be cut.

Strategy (cheapest first):

1. head-clip [0, W] (mkvmerge --split for mkv; ffmpeg -t copy otherwise -- reads only the head)
2. verify-decode the clip with the PlexCleaner image ffmpeg (matches the regression) and check
   whether the file's catalog decode-signature substrings re-appear in stderr
3. if not reproduced in the head, a full-decode locate with -stats timestamp correlation reports
   the approximate time of the first hit; ``--write-rules`` records a region window around it

Fidelity: uses the image's ffmpeg, NOT host ffmpeg, because decode error messages differ by
version. The source corpus is READ-ONLY; clips live under scratch.
"""

import argparse
import json
import os
import re
import subprocess
from pathlib import Path

from corpus_common import DECODE_SIGNATURES, SRC_DIR, make_head_clip

# Default paths for the reference server; override on the command line for another environment.
SCRATCH = Path("/data/media/PlexCleaner/scratch-trim")
IMAGE = "docker.io/ptr727/plexcleaner:develop"
WORK = SCRATCH / "locate"

# map a catalog subtype label -> the stderr substrings that evidence it (for grep-back)
SUBTYPE_NEEDLES: dict[str, list[str]] = {}
for _needle, _label in DECODE_SIGNATURES:
    SUBTYPE_NEEDLES.setdefault(_label, []).append(_needle)

TIME_RE = re.compile(r"time=(\d+):(\d\d):(\d\d(?:\.\d+)?)")


def sh(cmd: list[str]) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        cmd,
        stdin=subprocess.DEVNULL,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        text=True,
        errors="replace",
        check=False,
    )


# PlexCleaner's exact VerifyMedia args (no -map: default stream selection; -xerror aborts on first
# error). stats is -nostats for a plain reproduction check, -stats for timestamped locating.
def verify_args(input_path: str, stats: str = "-nostats") -> list[str]:
    return [
        "-nostdin",
        "-loglevel",
        "error",
        "-hide_banner",
        stats,
        "-abort_on",
        "empty_output",
        "-xerror",
        "-fflags",
        "+genpts",
        "-analyzeduration",
        "2G",
        "-probesize",
        "2G",
        "-i",
        input_path,
        "-max_muxing_queue_size",
        "1024",
        "-f",
        "null",
        "-",
    ]


def decode_stderr(media_dir: Path, name: str) -> str:
    """Verify-decode name with the image ffmpeg using PlexCleaner's exact args; return stderr."""
    uid, gid = os.getuid(), os.getgid()
    r = sh(
        [
            "docker",
            "run",
            "--rm",
            "--user",
            f"{uid}:{gid}",
            "--volume",
            f"{media_dir}:/media:ro",
            "--entrypoint",
            "ffmpeg",
            IMAGE,
        ]
        + verify_args(f"/media/{name}")
    )
    return r.stdout


def reproduced(stderr: str, needles: list[str]) -> bool:
    low = stderr.lower()
    return any(n.lower() in low for n in needles)


def full_decode_locate(src: Path, needles: list[str]) -> float | None:
    """Full-decode src (image ffmpeg + -stats, no -xerror); return the approx time (s) of the first
    line matching any needle, using the nearest preceding 'time=' progress stamp. None if unseen."""
    uid, gid = os.getuid(), os.getgid()
    args = [a for a in verify_args(f"/media/{src.name}", stats="-stats") if a != "-xerror"]
    proc = subprocess.Popen(
        [
            "docker",
            "run",
            "--rm",
            "--user",
            f"{uid}:{gid}",
            "--volume",
            f"{src.parent}:/media:ro",
            "--entrypoint",
            "ffmpeg",
            IMAGE,
        ]
        + args,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.PIPE,
        text=True,
        errors="replace",
        bufsize=1,
    )
    last_t = 0.0
    hit: float | None = None
    assert proc.stderr is not None
    for raw in proc.stderr:
        for chunk in raw.replace("\r", "\n").split("\n"):
            tm = TIME_RE.search(chunk)
            if tm:
                last_t = int(tm.group(1)) * 3600 + int(tm.group(2)) * 60 + float(tm.group(3))
            if hit is None and any(n.lower() in chunk.lower() for n in needles):
                hit = last_t
    proc.wait()
    return hit


def write_region(rules_path: Path, name: str, start: int, end: int, note: str) -> None:
    """Merge a region window for name into the external rules file (create if absent)."""
    data = json.loads(rules_path.read_text()) if rules_path.exists() else {}
    data.setdefault("schema", 1)  # keep the file self-describing, matching the shipped example
    data.setdefault("regions", {})[name] = {"start": start, "end": end, "note": note}
    rules_path.write_text(json.dumps(data, indent=2, ensure_ascii=False))


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("--catalog", required=True, help="source catalog.json (from catalog_corpus.py)")
    ap.add_argument("-w", "--window", type=int, default=60, help="head-clip seconds")
    ap.add_argument("files", nargs="*", help="basenames (default: all decode-signature files)")
    ap.add_argument(
        "--full",
        action="store_true",
        help="full-decode locate: report the approx time of the first signature hit",
    )
    ap.add_argument("--write-rules", help="with --full: write located region windows to this file")
    ap.add_argument(
        "--pad",
        type=int,
        default=40,
        help="seconds of padding around a located time when writing a region",
    )
    args = ap.parse_args()

    cat = json.loads(Path(args.catalog).read_text())
    WORK.mkdir(parents=True, exist_ok=True)

    if args.files:
        targets = [e for e in cat["files"] if e["file"] in args.files]
    else:
        targets = [e for e in cat["files"] if e["decode_subtypes"]]

    if args.full:
        rules_path = Path(args.write_rules) if args.write_rules else None
        print(f"{'FILE':52} {'SUBTYPES':28} ERROR@")
        for e in targets:
            name = e["file"]
            src = SRC_DIR / name
            subs = e["decode_subtypes"]
            needles = [n for s in subs for n in SUBTYPE_NEEDLES.get(s, [])]
            if not src.exists():
                print(f"{name[:52]:52} {','.join(subs)[:28]:28} MISSING SOURCE")
                continue
            t = full_decode_locate(src, needles)
            loc = f"~{t:.0f}s ({t / 60:.1f}m)" if t is not None else "NOT FOUND in full decode"
            print(f"{name[:52]:52} {','.join(subs)[:28]:28} {loc}")
            if rules_path is not None and t is not None:
                start = max(0, int(t) - args.pad)
                write_region(
                    rules_path, name, start, int(t) + args.pad, f"decode signature at ~{int(t)}s"
                )
        return

    print(f"{'FILE':52} {'SUBTYPES':30} {'HEAD':>6}  RESULT")
    for e in targets:
        name = e["file"]
        src = SRC_DIR / name
        subs = e["decode_subtypes"]
        needles = [n for s in subs for n in SUBTYPE_NEEDLES.get(s, [])]
        if not src.exists():
            print(f"{name[:52]:52} {','.join(subs)[:30]:30} {'-':>6}  MISSING SOURCE")
            continue
        wd = WORK / Path(name).stem
        if wd.exists():
            for p in wd.iterdir():
                p.unlink()
        wd.mkdir(parents=True, exist_ok=True)
        if not make_head_clip(src, wd / name, args.window):
            print(f"{name[:52]:52} {','.join(subs)[:30]:30} {'-':>6}  CLIP FAILED")
            continue
        ok = reproduced(decode_stderr(wd, name), needles)
        verdict = f"head[{args.window}s] reproduces" if ok else f"not in first {args.window}s"
        print(f"{name[:52]:52} {','.join(subs)[:30]:30} {'yes' if ok else 'no':>6}  {verdict}")


if __name__ == "__main__":
    main()
