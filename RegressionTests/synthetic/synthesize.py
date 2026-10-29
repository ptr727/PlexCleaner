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
- DTS time base: a healthy cadence denser than the default duration, a false DTS only in a 1/framerate encoder time base.
- DTS duplicate PTS: one repeated frame timestamp, a real repeat that verify reports at native precision.
- DTS mixed: a demux-visible audio timestamp step back plus a B-frame video timestamp swap that only surfaces after decode.
"""

import argparse
import subprocess
import tempfile
from collections.abc import Callable
from pathlib import Path

from hevc_nal import insert_before_vcl
from inject_cc_sei import build_cc_sei
from inject_hdr10plus_sei import build_hdr10plus_sei

FFMPEG = "ffmpeg"
FFPROBE = "ffprobe"
MKVMERGE = "mkvmerge"

# x265 params for a synthetic HDR10 (SMPTE ST 2086) stream: BT.2020 / PQ + mastering-display + MaxCLL
HDR10_X265 = (
    "colorprim=bt2020:transfer=smpte2084:colormatrix=bt2020nc:"
    "master-display=G(13250,34500)B(7500,3000)R(34000,16000)WP(15635,16450)L(10000000,1):"
    "max-cll=1000,400:hdr10=1:hdr10-opt=1:repeat-headers=1"
)


def run(cmd: list[str]) -> str:
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
    return proc.stdout or ""


def gen_hdr10_base(out: Path, seconds: int = 6, width: int = 3840, height: int = 2160) -> None:
    """Generate a synthetic HDR10 (ST 2086) HEVC clip."""
    run(
        [FFMPEG, "-y", "-f", "lavfi",
         "-i", f"testsrc2=size={width}x{height}:duration={seconds}:rate=24",
         "-c:v", "libx265", "-preset", "ultrafast", "-pix_fmt", "yuv420p10le",
         "-x265-params", HDR10_X265, str(out)]
    )  # fmt: skip


def gen_sdr_video(
    out: Path,
    seconds: int = 6,
    width: int = 320,
    height: int = 240,
    rate: str = "24",
    frames: int = 0,
    preset: str = "ultrafast",
) -> None:
    """Generate a small SDR H.264 clip, an extra video track or a raw stream to retime by frame count."""
    limit = ["-frames:v", str(frames)] if frames else []
    run(
        [FFMPEG, "-y", "-f", "lavfi",
         "-i", f"testsrc2=size={width}x{height}:duration={seconds}:rate={rate}", *limit,
         "-c:v", "libx264", "-preset", preset, "-pix_fmt", "yuv420p", str(out)]
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


def _build_hdr_cc(out: Path, sei: bytes) -> None:
    """Generate the HDR10 base, inject the given SEI NAL(s) before every VCL NAL, and remux to MKV.

    ``sei`` is the caption SEI, optionally preceded by an HDR10+ (ST 2094) SEI; the HDR10 (ST 2086)
    base metadata always comes from the x265 encode of the base.
    """
    with tempfile.TemporaryDirectory() as td:
        work = Path(td)
        base, annexb, ccb = work / "base.mkv", work / "base.hevc", work / "cc.hevc"
        gen_hdr10_base(base)
        run([FFMPEG, "-y", "-i", str(base), "-c:v", "copy", "-bsf:v", "hevc_mp4toannexb", str(annexb)])  # fmt: skip
        ccb.write_bytes(insert_before_vcl(annexb.read_bytes(), sei))
        run([MKVMERGE, "-o", str(out), "--default-duration", "0:24fps", str(ccb)])


def build_hdr10_cc(out: Path) -> None:
    """HDR10 (ST 2086) HEVC with CEA-608 closed captions, reaching the CC-on-HDR branch."""
    _build_hdr_cc(out, build_cc_sei())


def build_hdr10plus_cc(out: Path) -> None:
    """True HDR10+ (ST 2094) HEVC with CEA-608 closed captions.

    Injects both an ST 2094-40 HDR10+ SEI and the CC SEI, so the CC-on-HDR branch fires over the
    dynamic metadata that is genuinely at risk when the CC SEI is removed (MediaInfo reports
    "SMPTE ST 2094 App 4"), not just the static ST 2086 case.
    """
    _build_hdr_cc(out, build_hdr10plus_sei() + build_cc_sei())


def _write_timestamps(out: Path, timestamps: list[float], fmt: str = "v2") -> None:
    """Write an mkvmerge timestamps file, one millisecond timestamp per frame, v4 keeping the order unsorted."""
    out.write_text(
        f"# timestamp format {fmt}\n" + "".join(f"{t:.3f}\n" for t in timestamps),
        encoding="utf-8",
    )


def _build_retimed(out: Path, timestamps: list[float]) -> None:
    """Mux one H.264 frame per millisecond timestamp, under a 23.976 default duration."""
    with tempfile.TemporaryDirectory() as td:
        work = Path(td)
        video, timecodes = work / "video.h264", work / "timestamps.txt"
        gen_sdr_video(video, seconds=len(timestamps), rate="24000/1001", frames=len(timestamps))
        _write_timestamps(timecodes, timestamps)
        run(
            [MKVMERGE, "-o", str(out), "--default-duration", "0:24000/1001p",
             "--timestamps", f"0:{timecodes}", str(video)]
        )  # fmt: skip


def build_dts_timebase(out: Path) -> None:
    """Healthy VFR video, 23.976 cadence followed by a denser 29.97 cadence.

    Every timestamp increases at 1 ms precision.
    Verify reports a false non-monotonic DTS only when it encodes in the 1/framerate time base.
    """
    timestamps = [i * 1001 / 24 for i in range(48)]
    timestamps += [timestamps[-1] + i * 1001 / 30 for i in range(1, 49)]
    _build_retimed(out, timestamps)


def build_dts_duplicate_pts(out: Path) -> None:
    """23.976 video with one repeated frame timestamp, a real repeat at native precision."""
    timestamps = [i * 1001 / 24 for i in range(96)]
    timestamps[48] = timestamps[47]
    _build_retimed(out, timestamps)


def _count_packets(media: Path) -> int:
    return int(
        run(
            [FFPROBE, "-v", "error", "-select_streams", "0", "-count_packets",
             "-show_entries", "stream=nb_read_packets", "-of", "csv=p=0", str(media)]
        ).strip()
    )  # fmt: skip


def build_dts_mixed(out: Path) -> None:
    """B-frame video with two frame timestamps swapped, plus AAC audio with one packet timestamp stepping back.

    The demuxer derives video DTS from the sorted timestamps, so only the decoded display order runs backward.
    The audio step is demux-visible, and the lossless setts tier repairs it alone.
    """
    with tempfile.TemporaryDirectory() as td:
        work = Path(td)
        video, video_tc = work / "video.h264", work / "video.txt"
        audio, audio_tc = work / "audio.aac", work / "audio.txt"
        video_ts = [i * 1001 / 24 for i in range(96)]
        video_ts[48], video_ts[49] = video_ts[49], video_ts[48]
        gen_sdr_video(video, rate="24000/1001", frames=len(video_ts), preset="veryfast")
        _write_timestamps(video_tc, video_ts, "v4")
        gen_audio(audio, seconds=4)
        audio_ts = [i * 1024 * 1000 / 44100 for i in range(_count_packets(audio))]
        audio_ts[100] = audio_ts[99] - 5
        _write_timestamps(audio_tc, audio_ts, "v4")
        run(
            [MKVMERGE, "-o", str(out), "--default-duration", "0:24000/1001p",
             "--timestamps", f"0:{video_tc}", str(video),
             "--timestamps", f"0:{audio_tc}", str(audio)]
        )  # fmt: skip


TARGETS: dict[str, Callable[[Path], None]] = {
    "hdr10-base": gen_hdr10_base,
    "hdr10-multitrack": build_hdr10_multitrack,
    "hdr10-cc": build_hdr10_cc,
    "hdr10plus-cc": build_hdr10plus_cc,
    "dts-timebase": build_dts_timebase,
    "dts-duplicate-pts": build_dts_duplicate_pts,
    "dts-mixed": build_dts_mixed,
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
