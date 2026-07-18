"""Synthetic media generation for targeted regression fixtures.

Build small, fully synthetic media files (ffmpeg lavfi sources - no copyrighted content) that
target specific PlexCleaner detections, so a regression fixture can be reproduced anywhere without
shipping media. Validate a generated file the same way the reduced corpus proves equivalence:
process it through the PlexCleaner image and confirm its detections and State.

Requires ffmpeg (with libx265) and mkvmerge on PATH.

Targeting notes - the PlexCleaner behaviors these builders exploit (see the README):

- Duplicate tracks: ``FindDuplicateTracks`` keeps every FLAGGED track, then dedups the rest by
  preferred codec, so a duplicate pair must be UNFLAGGED and in a keep language.
- Track flags to be set: a title-implied flag (SDH / CC / Commentary / Forced) that is not set as
  an actual track flag.
- Unwanted language: a language not in KeepLanguages and not the original language.
- Redundant Default flags: two or more tracks of a type flagged default.
- Extra video tracks: a second video stream.
- Tags: mkvmerge statistics tags plus a container title.
- HDR survives remux: the HDR SEI is copied through an mkvmerge remux (a regression guard).
- Closed captions on HDR: an A53 CC SEI (see ``inject_cc_sei``) on an HDR (ST 2086 / 2094) HEVC
  stream, which reaches PlexCleaner's CC-on-HDR branch.
"""

import argparse
import subprocess
import tempfile
from collections.abc import Callable
from pathlib import Path

from inject_cc_sei import inject

FFMPEG = "ffmpeg"
MKVMERGE = "mkvmerge"

# x265 params for a synthetic HDR10 (SMPTE ST 2086) stream: BT.2020 / PQ + mastering-display + MaxCLL
HDR10_X265 = (
    "colorprim=bt2020:transfer=smpte2084:colormatrix=bt2020nc:"
    "master-display=G(13250,34500)B(7500,3000)R(34000,16000)WP(15635,16450)L(10000000,1):"
    "max-cll=1000,400:hdr10=1:hdr10-opt=1:repeat-headers=1"
)


def run(cmd: list[str]) -> None:
    # Capture combined output and surface its tail on failure, so a regeneration error on another
    # machine is diagnosable rather than a bare non-zero exit.
    proc = subprocess.run(
        cmd,
        stdin=subprocess.DEVNULL,
        stdout=subprocess.PIPE,
        stderr=subprocess.STDOUT,
        text=True,
        errors="replace",
    )
    if proc.returncode != 0:
        tail = "\n".join((proc.stdout or "").splitlines()[-20:])
        raise RuntimeError(f"command failed (exit {proc.returncode}): {' '.join(cmd)}\n{tail}")


def gen_hdr10_base(out: Path, seconds: int = 6, width: int = 3840, height: int = 2160) -> None:
    """Generate a synthetic HDR10 (ST 2086) HEVC clip."""
    run(
        [FFMPEG, "-y", "-f", "lavfi",
         "-i", f"testsrc2=size={width}x{height}:duration={seconds}:rate=24",
         "-c:v", "libx265", "-preset", "ultrafast", "-pix_fmt", "yuv420p10le",
         "-x265-params", HDR10_X265, str(out)]
    )  # fmt: skip


def gen_sdr_video(out: Path, seconds: int = 6, width: int = 320, height: int = 240) -> None:
    """Generate a small SDR H.264 clip, used as an extra (second) video track."""
    run(
        [FFMPEG, "-y", "-f", "lavfi",
         "-i", f"testsrc2=size={width}x{height}:duration={seconds}:rate=24",
         "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p", str(out)]
    )  # fmt: skip


def gen_audio(out: Path, seconds: int = 6, freq: int = 440) -> None:
    """Generate a synthetic AAC tone."""
    run(
        [FFMPEG, "-y", "-f", "lavfi", "-i", f"sine=frequency={freq}:duration={seconds}",
         "-c:a", "aac", "-b:a", "128k", str(out)]
    )  # fmt: skip


def write_srt(out: Path, text: str) -> None:
    out.write_text(f"1\n00:00:00,500 --> 00:00:03,000\n{text}\n", encoding="utf-8")


def build_hdr10_multitrack(out: Path) -> None:
    """HDR10 HEVC targeting the full track-structure set (extra-video, duplicate, redundant-default,
    flags-to-set, unwanted-language, tags) and forcing an HDR-preserving remux.

    Layout: two video tracks (extra video); two en audio both default (redundant default); two ja
    audio both UNFLAGGED (duplicate - dedup keeps flagged tracks, so these must be flagless and in a
    keep language); one de audio + one de subtitle (unwanted language); one en subtitle titled
    "Forced" without the forced flag (flags to be set); statistics tags + title (tags).
    """
    with tempfile.TemporaryDirectory() as td:
        work = Path(td)
        base, sdr, aud = work / "base.mkv", work / "sdr.mkv", work / "aud.m4a"
        sub_en, sub_de = work / "en.srt", work / "de.srt"
        gen_hdr10_base(base)
        gen_sdr_video(sdr)
        gen_audio(aud)
        write_srt(sub_en, "Hello World")
        write_srt(sub_de, "Hallo Welt")
        run(
            [MKVMERGE, "-o", str(out), "--title", "Synthetic HDR10 Multitrack",
             "--default-track-flag", "0:no", "--language", "0:en", "--track-name", "0:HDR10 Main", str(base),
             "--default-track-flag", "0:no", "--language", "0:en", "--track-name", "0:Thumbnail", str(sdr),
             "--default-track-flag", "0:yes", "--language", "0:en", "--track-name", "0:English", str(aud),
             "--default-track-flag", "0:yes", "--language", "0:en", "--track-name", "0:English 2", str(aud),
             "--default-track-flag", "0:no", "--language", "0:ja", "--track-name", "0:Japanese", str(aud),
             "--default-track-flag", "0:no", "--language", "0:ja", "--track-name", "0:Japanese 2", str(aud),
             "--default-track-flag", "0:no", "--language", "0:de", "--track-name", "0:German", str(aud),
             "--default-track-flag", "0:no", "--language", "0:en", "--track-name", "0:English Forced", str(sub_en),
             "--default-track-flag", "0:no", "--language", "0:de", "--track-name", "0:German Subs", str(sub_de)]
        )  # fmt: skip


def build_hdr10_cc(out: Path) -> None:
    """HDR10 HEVC with embedded CEA-608 closed captions, targeting the CC-on-HDR branch.

    HDR10 (ST 2086) is enough to reach the branch. For the true HDR10+ (ST 2094) case - the dynamic
    metadata genuinely at risk when the CC SEI is removed - also inject an ST 2094-40 SEI.
    """
    with tempfile.TemporaryDirectory() as td:
        work = Path(td)
        base, annexb, ccb = work / "base.mkv", work / "base.hevc", work / "cc.hevc"
        gen_hdr10_base(base)
        run(
            [
                FFMPEG,
                "-y",
                "-i",
                str(base),
                "-c:v",
                "copy",
                "-bsf:v",
                "hevc_mp4toannexb",
                str(annexb),
            ]
        )
        ccb.write_bytes(inject(annexb.read_bytes()))
        run([MKVMERGE, "-o", str(out), "--default-duration", "0:24fps", str(ccb)])


TARGETS: dict[str, Callable[[Path], None]] = {
    "hdr10-base": gen_hdr10_base,
    "hdr10-multitrack": build_hdr10_multitrack,
    "hdr10-cc": build_hdr10_cc,
}


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument("target", choices=sorted(TARGETS), help="which synthetic fixture to build")
    ap.add_argument("-o", "--out", required=True, type=Path, help="output file")
    args = ap.parse_args()
    TARGETS[args.target](args.out)
    print(f"built {args.target} -> {args.out}")


if __name__ == "__main__":
    main()
