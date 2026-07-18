"""
corpus_common.py - shared parsing + deterministic classification for the corpus tooling.

Single source of truth so catalog_corpus.py and reduce_corpus.py never drift. Parses a
PlexCleaner process log (attributing detections/errors to files by thread affinity + line
filename) and classifies a file's State/detections/decode-signatures into a curated stable
bucket set.

Robust to BOTH failure-log formats: a logging change inserted {Operation} before ExitCode and
appended {FileName} after the error, so a fixed regex tied to the old shape would silently drop
all errors on newer logs.
"""

import glob
import json
import re
import sys
from pathlib import Path

REG_DIR = Path("/data/media/PlexCleaner/RegressionTest")
SRC_DIR = Path("/data/media/troublesome/full")

MEDIA_EXTS = {
    ".mkv",
    ".mp4",
    ".avi",
    ".wmv",
    ".mov",
    ".ts",
    ".mpg",
    ".m2ts",
    ".dv",
    ".webm",
    ".m4v",
}

# Robust to both log timestamp formats:
#   short : "10:41:46 [INF] <9> msg"
#   debug : "2026-07-15 10:41:46.977 -07:00 [INF] <9> msg"  (--loglevel Debug full timestamp)
# The old short-only pattern silently matched nothing on debug logs, dropping every detection/error
# and making the equivalence check vacuous (empty >= empty).
LINE_RE = re.compile(
    r"^(?:\d{4}-\d\d-\d\d )?\d\d:\d\d:\d\d(?:\.\d+)?(?: [+-]\d\d:\d\d)? \[\w+\] <(\d+)> (.*)$"
)
BEFORE_RE = re.compile(r'ProcessFiles.*?(?:Before|Skipping non-MKV file)\s*:\s*"([^"]+)"')
DETECT_RE = re.compile(r"([A-Z][A-Za-z0-9 /_-]+?) detected\b")
TRACK_RE = re.compile(
    r'MkvMerge\s*:\s*(Video|Audio|Subtitle)\s*:\s*Format:\s*"([^"]*)".*?Interlaced:\s*(True|False)'
)
#   old : Failed execution of FfMpeg : ExitCode: 183 : "e1 | e2 | e3"
#   new : Failed execution of FfMpeg : Verify : ExitCode: 183 : "e1 | e2 | e3" : "/path/file.mkv"
FAIL_RE = re.compile(r"Failed execution of (\w+)\b.*?ExitCode:\s*-?\d+\s*(?::\s*(.*))?$")
QUOTED_RE = re.compile(r'"([^"]*)"')
# Environmental / informational stderr that is NOT a file-intrinsic issue - a reduced clip must not be
# required to reproduce these (they depend on the host, not the media).
BENIGN_NOISE = re.compile(
    r"Cannot load lib|libnvidia|libcuda|Using the demultiplexer|Using the muxer for|"
    r"Using the encoder|Using the decoder|Press \[q\]|configuration:|built with|"
    r"deprecated pixel format",
    re.IGNORECASE,
)

DECODE_SIGNATURES = [
    ("non monotonically increasing dts", "DTS-NonMonotonic"),
    ("Invalid NAL unit size", "Decode-NAL"),
    ("mmco", "Decode-H264-RefPicture"),
    ("reference picture missing", "Decode-H264-RefPicture"),
    ("Missing reference picture", "Decode-H264-RefPicture"),
    ("number of reference frames", "Decode-H264-RefFrames"),
    ("cabac decode", "Decode-H264-Cabac"),
    ("error while decoding", "Decode-Generic"),
    # NOTE: "Invalid data found when processing input" is ffmpeg's blanket wrapper accompanying any
    # hard decode failure, not a distinct issue class - classifying it forced clips to reproduce
    # the wrapper severity rather than the actual signature, so it is intentionally absent.
    ("noise_facs_q", "Decode-AAC"),
    ("env_facs_q", "Decode-AAC"),
    ("Input buffer exhausted", "Decode-AAC"),
    ("Unknown subtitle segment", "Subtitle-Corrupt"),
    ("quant_step_size", "TrueHD-QuantStep"),
    ("Output file is empty", "Encode-EmptyOutput"),
    ("non-supported file type", "Container-Unsupported"),
    ("exceeds max length", "EBML-MaxLength"),
]

STATE_BUCKET = {
    "DeInterlaced": "Interlaced",
    "ClearedCaptions": "ClosedCaption",
    "BitrateExceeded": "Bitrate",
    "SetLanguage": "Language",
    "SetFlags": "Flags",
    "ClearedDefaultFlags": "Flags",
    "ClearedTags": "Tags",
    "RemovedAttachments": "Attachments",
    "RemovedCoverArt": "CoverArt",
    "ReEncoded": "ReEncode",
    "Repaired": "VerifyRepair",
    "VerifyFailed": "VerifyFailed",
    "FileReNamed": "ExtensionNormalize",
}
DETECT_BUCKET = [
    ("Interlaced", "Interlaced"),
    ("Closed Caption", "ClosedCaption"),
    ("Cover Art", "CoverArt"),
    ("Attachment", "Attachments"),
    ("language", "Language"),
    ("Default flags", "Flags"),
    ("flags to be set", "Flags"),
    ("Tags", "Tags"),
    ("Metadata", "Tags"),
    ("encode", "ReEncode"),
    ("Verify", "VerifyRepair"),
    ("Duplicate", "DuplicateTracks"),
    ("Extra video", "ExtraTracks"),
]


def stem_of(p):
    return Path(p).stem


def hms(seconds):
    """Seconds -> HH:MM:SS (mkvmerge rejects a seconds field > 59, so never emit 00:00:60)."""
    seconds = int(seconds)
    return f"{seconds // 3600:02d}:{(seconds % 3600) // 60:02d}:{seconds % 60:02d}"


def strip_language_ietf(path):
    """Surgical defect RE-INJECTION: delete the LanguageIETF element from every track header, in
    place (no remux). A mkvmerge cut writes IETF tags, repairing the 'Metadata errors' defect many
    sources carry; deleting them restores the defect so the clip exercises the same repair path.
    Track languages themselves are untouched."""
    import subprocess as _sp

    path = Path(path)
    try:
        ident = json.loads(
            _sp.run(
                ["mkvmerge", "-J", str(path)], stdin=_sp.DEVNULL, capture_output=True, text=True
            ).stdout
        )
        ntracks = len(ident.get("tracks", []))
    except Exception:
        return False
    if not ntracks:
        return False
    cmd = ["mkvpropedit", str(path)]
    for i in range(1, ntracks + 1):
        cmd += ["--edit", f"track:@{i}", "--delete", "language-ietf"]
    r = _sp.run(cmd, stdin=_sp.DEVNULL, stdout=_sp.DEVNULL, stderr=_sp.DEVNULL)
    return r.returncode == 0


def set_language_ietf(path):
    """Inverse surgery of strip_language_ietf: SET language-ietf on every track (from the track's
    existing language), in place. An ffmpeg cut strips IETF tags, which triggers PlexCleaner's
    metadata remux BEFORE verify - and that remux repairs timestamp defects the clip was built to
    carry. Fixing IETF up front lets the clip enter verify metadata-clean, so the defect drives
    the same verify->repair chain as the source did."""
    import subprocess as _sp

    path = Path(path)
    try:
        ident = json.loads(
            _sp.run(
                ["mkvmerge", "-J", str(path)], stdin=_sp.DEVNULL, capture_output=True, text=True
            ).stdout
        )
        tracks = ident.get("tracks", [])
    except Exception:
        return False
    if not tracks:
        return False
    cmd = ["mkvpropedit", str(path)]
    for i, t in enumerate(tracks, start=1):
        lang = t.get("properties", {}).get("language", "und") or "und"
        cmd += ["--edit", f"track:@{i}", "--set", f"language-ietf={lang}"]
    r = _sp.run(cmd, stdin=_sp.DEVNULL, stdout=_sp.DEVNULL, stderr=_sp.DEVNULL)
    return r.returncode == 0


def make_head_clip(src, out, seconds, run=None, cutter="mkvmerge"):
    """Cut [0, seconds] from src into out (stream copy). Returns True on a non-empty output.

    The cutters have complementary side effects, so callers try each and validate:
    - mkvmerge: preserves timestamp defects and und-language, but normalizes missing IETF language
      tags (repairs the "Metadata errors" defect the large samples carry).
    - ffmpeg (-bitexact): preserves the missing-IETF defect, but STRIPS IETF tags from clean files
      (introducing a spurious SetLanguage) and can break DTS-repair clips.
    - ffmpeg-tags (no -bitexact): preserves IETF on clean files AND the metadata defect where
      present, at the cost of writing Lavf writing-app tags.
    All add their own track tags (ffmpeg: DURATION even with -bitexact; mkvmerge: statistics) -
    the caller strips them with mkvpropedit when the source had none. Non-mkv sources always use
    ffmpeg (mkvmerge cannot write their containers)."""
    import subprocess as _sp

    src, out = Path(src), Path(out)
    # escape the stem: corpus filenames contain glob metacharacters (e.g. brackets), which a raw
    # glob would read as character classes and mis-match, leaving stale outputs or matching others
    for p in out.parent.glob(glob.escape(out.stem) + ".*"):
        p.unlink()

    def _run(cmd):
        if run:
            return run(cmd)
        return _sp.run(cmd, stdin=_sp.DEVNULL, stdout=_sp.DEVNULL, stderr=_sp.DEVNULL)

    if cutter == "mkvmerge" and src.suffix.lower() == ".mkv":
        _run(["mkvmerge", "-o", str(out), "--split", f"parts:00:00:00-{hms(seconds)}", str(src)])
        if not out.exists():
            numbered = out.with_name(out.stem + "-001" + out.suffix)
            if numbered.exists():
                numbered.rename(out)
    else:
        cmd = [
            "ffmpeg",
            "-hide_banner",
            "-loglevel",
            "error",
            "-i",
            str(src),
            "-t",
            str(int(seconds)),
            "-map",
            "0",
            "-c",
            "copy",
            "-avoid_negative_ts",
            "make_zero",
        ]
        if cutter != "ffmpeg-tags":
            cmd.append("-bitexact")
        _run(cmd + [str(out)])
    return out.exists() and out.stat().st_size > 0


def is_filepath_quote(q):
    return (q.startswith("/") and Path(q).suffix.lower() in (MEDIA_EXTS | {".tmp"})) or bool(
        re.search(r"\.tmp\d+", q)
    )


def extract_errors(after):
    """From the text after 'ExitCode: N', return error strings (excluding the appended filename)."""
    if not after:
        return set()
    quoted = QUOTED_RE.findall(after)
    if quoted:
        payload = [q for q in quoted if not is_filepath_quote(q)]
    else:
        payload = [re.sub(r"\s*:\s*/\S+$", "", after).strip()]
    errs = set()
    for p in payload:
        for e in p.split(" | "):
            e = e.strip()
            if e and not BENIGN_NOISE.search(e):
                # normalize run-varying content so identical errors compare equal across runs:
                # media-root paths (/Test/Media vs /media) and ASLR pointer addresses (0x...)
                e = re.sub(r"(/Test/Media|/media)/", "<MEDIA>/", e)
                e = re.sub(r"0x[0-9a-fA-F]+", "0xADDR", e)
                errs.add(e)
    return errs


def parse_log(path):
    """{stem: {detections:set, errors:set, tracks:list}} by thread affinity + line filename."""
    result = {}
    thread_file = {}
    path = Path(path)

    def bucket(stem):
        return result.setdefault(stem, {"detections": set(), "errors": set(), "tracks": []})

    if not path.exists():
        return result
    for raw in path.read_text(errors="replace").splitlines():
        m = LINE_RE.match(raw)
        if not m:
            continue
        tid, msg = m.group(1), m.group(2)
        b = BEFORE_RE.search(msg)
        if b:
            thread_file[tid] = stem_of(b.group(1))
            continue
        stem = thread_file.get(tid)
        d = DETECT_RE.search(msg)
        if d:
            fn = re.search(r'"([^"]+\.[A-Za-z0-9]+)"\s*$', msg)
            target = stem_of(fn.group(1)) if fn else stem
            if target:
                bucket(target)["detections"].add(d.group(1).strip())
            continue
        t = TRACK_RE.search(msg)
        if t and stem:
            entry = {"type": t.group(1), "format": t.group(2), "interlaced": t.group(3) == "True"}
            trk = bucket(stem)["tracks"]
            if entry not in trk:
                trk.append(entry)
            continue
        f = FAIL_RE.search(msg)
        if f and stem:
            errs = extract_errors(f.group(2))
            bucket(stem)["errors"] |= errs if errs else {"<silent-nonzero-exit>"}
    return result


def error_shape(e):
    """Reduce an (already ADDR/path-normalized) error line to its PHYSICAL SHAPE: the exact
    ffmpeg message template with site-varying content (stream indexes, MB coordinates, picture
    numbers, sizes) normalized out. Distinct shapes are distinct physical error identities -
    far finer than the broad signature classes, and stable across corruption sites and future
    PlexCleaner logic refinements."""
    s = e
    # decoder/stream context brackets -> keep only the codec identity
    s = re.sub(r"\[[a-z]+#\d+:\d+/(\w+) @ 0xADDR\]\s*", r"[\1] ", s)  # [vist#0:0/h264 @ ..]
    s = re.sub(r"\[dec:(\w+) @ 0xADDR\]\s*", r"[\1] ", s)  # [dec:h264 @ ..]
    s = re.sub(r"\[(\w+) @ 0xADDR\]", r"[\1]", s)  # [h264 @ ..]
    s = re.sub(r"\[SWR @ 0xADDR\]", "[SWR]", s)
    # collapse the known collection subdir in an embedded source path: the ground run processes
    # files under full/ while a clip is processed at the media root, so a path-bearing message
    # (e.g. the unsupported-container error) would otherwise never match between ground and clip.
    # Restricted to the corpus dirs so a real top-level path component is never stripped.
    s = re.sub(r"<MEDIA>/(?:full|reduced)/", "<MEDIA>/", s)
    s = re.sub(r"stream \d+", "stream N", s)
    s = re.sub(r"-?\b\d+(\.\d+)?\b", "N", s)  # coordinates/ids/sizes
    return re.sub(r"\s+", " ", s).strip()


def classify_signature(errors):
    subs = set()
    for e in errors:
        low = e.lower()
        for needle, label in DECODE_SIGNATURES:
            if needle.lower() in low:
                subs.add(label)
                break
    return subs


def buckets_for(state, detections, sig_subtypes, in_errors):
    b = set()
    for flag in state:
        if flag in STATE_BUCKET:
            b.add(STATE_BUCKET[flag])
    for det in detections:
        for needle, label in DETECT_BUCKET:
            if needle.lower() in det.lower():
                b.add(label)
    b |= sig_subtypes
    if in_errors:
        b.add("Error")
    return b


def find_run(explicit, channel="develop"):
    if explicit:
        p = Path(explicit)
        return p if p.is_absolute() else REG_DIR / p
    best, best_mt = None, 0
    for d in REG_DIR.glob("*/"):
        log, bi = d / "PlexCleaner_process.log", d / "buildinfo.json"
        if not (log.exists() and bi.exists()):
            continue
        try:
            if json.loads(bi.read_text()).get("Channel") != channel:
                continue
        except Exception:
            continue
        if log.stat().st_mtime > best_mt:
            best, best_mt = d, log.stat().st_mtime
    if not best:
        sys.exit(f"ERROR: no {channel} run under {REG_DIR}")
    return best
