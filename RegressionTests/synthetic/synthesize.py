"""Synthetic media generation for targeted regression fixtures.

Build small, fully synthetic media files (ffmpeg lavfi sources - no copyrighted content) that
target specific PlexCleaner detections, so a regression fixture can be reproduced anywhere without
shipping media. Validate a generated file the same way the reduced corpus proves equivalence:
process it through the PlexCleaner image and confirm its detections and State.

Requires ffmpeg (with libx265), ffprobe, and mkvmerge on PATH.

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
- DTS audio tagged: the audio step back alone under a global tag and an attachment, which the lossless setts tier repairs.
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

# The ffmpeg idet filter reads small testsrc2 frames as interlaced, so a progressive SDR clip uses testsrc.
SDR_SOURCE = "testsrc"

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
        check=False,
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
         "-i", f"{SDR_SOURCE}=size={width}x{height}:duration={seconds}:rate={rate}", *limit,
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


def _build_dts_av(out: Path, swap_video: bool, extra: list[str]) -> None:
    """Mux B-frame video and AAC audio with one audio packet timestamp stepping back, optionally swapping two video frames."""
    with tempfile.TemporaryDirectory() as td:
        work = Path(td)
        video, video_tc = work / "video.h264", work / "video.txt"
        audio, audio_tc = work / "audio.aac", work / "audio.txt"
        video_ts = [i * 1001 / 24 for i in range(96)]
        if swap_video:
            video_ts[48], video_ts[49] = video_ts[49], video_ts[48]
        # The veryfast preset keeps B-frames, which the ultrafast default disables
        gen_sdr_video(video, rate="24000/1001", frames=len(video_ts), preset="veryfast")
        _write_timestamps(video_tc, video_ts, "v4")
        gen_audio(audio, seconds=4)
        audio_ts = [i * 1024 * 1000 / 44100 for i in range(_count_packets(audio))]
        audio_ts[100] = audio_ts[99] - 5
        _write_timestamps(audio_tc, audio_ts, "v4")
        run(
            [MKVMERGE, "-o", str(out), *extra, "--default-duration", "0:24000/1001p",
             "--timestamps", f"0:{video_tc}", str(video),
             "--timestamps", f"0:{audio_tc}", str(audio)]
        )  # fmt: skip


def build_dts_mixed(out: Path) -> None:
    """B-frame video with two frame timestamps swapped, plus AAC audio with one packet timestamp stepping back.

    The demuxer derives video DTS from the sorted timestamps, so only the decoded display order runs backward.
    The audio step is demux-visible, and the lossless setts tier repairs it alone.
    """
    _build_dts_av(out, swap_video=True, extra=[])


def build_dts_audio_tagged(out: Path) -> None:
    """Healthy B-frame video plus AAC audio with one packet timestamp stepping back, under a global tag and an attachment.

    The only DTS break is demux-visible audio, so the lossless setts tier repairs the whole file.
    The tag and the attachment show whether the repaired file drops them like every other rewrite.
    """
    with tempfile.TemporaryDirectory() as td:
        tags, note = Path(td) / "tags.xml", Path(td) / "note.txt"
        note.write_text("Synthetic attachment\n", encoding="utf-8")
        tags.write_text(
            '<?xml version="1.0"?>\n<Tags><Tag><Targets/><Simple><Name>COMMENT</Name>'
            "<String>Synthetic</String></Simple></Tag></Tags>\n",
            encoding="utf-8",
        )
        extra = [
            "--global-tags",
            str(tags),
            "--attachment-mime-type",
            "text/plain",
            "--attach-file",
            str(note),
        ]
        _build_dts_av(out, swap_video=False, extra=extra)


def _gen_sdr_av(out: Path, extra: list[str] | None = None) -> None:
    """Encode a small SDR H.264 + AAC clip straight into the container ``out``'s extension names."""
    run(
        [FFMPEG, "-y", "-f", "lavfi", "-i", f"{SDR_SOURCE}=size=320x240:duration=4:rate=24",
         "-f", "lavfi", "-i", "sine=frequency=440:duration=4",
         "-c:v", "libx264", "-preset", "ultrafast", "-pix_fmt", "yuv420p",
         "-c:a", "aac", "-b:a", "128k", *(extra or []), str(out)]
    )  # fmt: skip


def build_container_mp4(out: Path) -> None:
    """An MP4 container, which the ReMux extension list remuxes to MKV by its extension."""
    _gen_sdr_av(out)


def build_container_renamed(out: Path) -> None:
    """An MP4 container under an MKV name, which only the mkvmerge container check finds."""
    with tempfile.TemporaryDirectory() as td:
        mp4 = Path(td) / "renamed.mp4"
        _gen_sdr_av(mp4)
        out.write_bytes(mp4.read_bytes())


def build_uppercase_extension(out: Path) -> None:
    """A healthy MKV whose extension is uppercase, which processing renames."""
    _gen_sdr_av(out, ["-f", "matroska"])


def build_unwanted_extension(out: Path) -> None:
    """A non-media file outside the ignore masks, which processing deletes as an unwanted extension."""
    out.write_text("Synthetic unwanted file\n", encoding="utf-8")


def build_cover_art(out: Path) -> None:
    """A healthy MKV carrying a JPEG cover art attachment."""
    with tempfile.TemporaryDirectory() as td:
        work = Path(td)
        clip, cover = work / "clip.mkv", work / "cover.jpg"
        _gen_sdr_av(clip)
        run([FFMPEG, "-y", "-f", "lavfi", "-i", "testsrc2=size=320x240", "-frames:v", "1", str(cover)])  # fmt: skip
        run(
            [MKVMERGE, "-o", str(out), "--attachment-mime-type", "image/jpeg",
             "--attachment-name", "cover.jpg", "--attach-file", str(cover), str(clip)]
        )  # fmt: skip


def build_language_tags(out: Path) -> None:
    """Tracks whose language needs setting: an undefined audio and a subtitle with no IETF tag.

    The audio is ``und``, which SetUnknownLanguage replaces with the default language.
    The subtitle carries only the legacy ISO 639-2 tag, which reads as an IETF tag to set.
    """
    with tempfile.TemporaryDirectory() as td:
        work = Path(td)
        clip, sub = work / "clip.mkv", work / "en.srt"
        _gen_sdr_av(clip)
        write_srt(sub, "Hello World")
        run(
            [MKVMERGE, "-o", str(out), "--language", "0:en", "--language", "1:und", str(clip),
             "--disable-language-ietf", "--language", "0:eng", str(sub)]
        )  # fmt: skip


def build_title_flags(out: Path) -> None:
    """Subtitle and audio tracks whose titles imply SDH, commentary, and forced flags that are not set."""
    with tempfile.TemporaryDirectory() as td:
        work = Path(td)
        clip, aud, sub = work / "clip.mkv", work / "aud.m4a", work / "en.srt"
        _gen_sdr_av(clip)
        gen_audio(aud, seconds=4)
        write_srt(sub, "Hello World")
        run(
            [MKVMERGE, "-o", str(out), "--language", "0:en", "--language", "1:en", str(clip),
             "--language", "0:en", "--track-name", "0:Commentary", str(aud),
             "--language", "0:en", "--track-name", "0:English SDH", str(sub),
             "--language", "0:en", "--track-name", "0:Forced", str(sub)]
        )  # fmt: skip


def build_interlaced(out: Path) -> None:
    """True interlaced H.264, 59.94 fields woven into 29.97 top-field-first frames that idet detects."""
    run(
        [FFMPEG, "-y", "-f", "lavfi", "-i", "testsrc2=size=720x480:duration=4:rate=60000/1001",
         "-vf", "tinterlace=interleave_top,setfield=tff", "-flags", "+ilme+ildct", "-top", "1",
         "-c:v", "libx264", "-preset", "ultrafast", "-x264-params", "tff=1", "-pix_fmt", "yuv420p",
         "-metadata:s:v:0", "language=eng", str(out)]
    )  # fmt: skip


def build_reencode(out: Path) -> None:
    """MPEG-2 video and MP2 audio, both formats on the re-encode lists."""
    run(
        [FFMPEG, "-y", "-f", "lavfi", "-i", f"{SDR_SOURCE}=size=320x240:duration=4:rate=24",
         "-f", "lavfi", "-i", "sine=frequency=440:duration=4",
         "-c:v", "mpeg2video", "-c:a", "mp2", "-metadata:s:v:0", "language=eng",
         "-metadata:s:a:0", "language=eng", str(out)]
    )  # fmt: skip


def build_sdr_cc(out: Path) -> None:
    """SDR HEVC carrying CEA-608 closed captions, which processing removes without an HDR concern."""
    with tempfile.TemporaryDirectory() as td:
        work = Path(td)
        annexb, ccb = work / "base.hevc", work / "cc.hevc"
        run(
            [FFMPEG, "-y", "-f", "lavfi", "-i", f"{SDR_SOURCE}=size=320x240:duration=4:rate=24",
             "-c:v", "libx265", "-preset", "ultrafast", "-pix_fmt", "yuv420p",
             "-x265-params", "repeat-headers=1:log-level=error", "-f", "hevc", str(annexb)]
        )  # fmt: skip
        ccb.write_bytes(insert_before_vcl(annexb.read_bytes(), build_cc_sei()))
        run([MKVMERGE, "-o", str(out), "--default-duration", "0:24fps", "--language", "0:en", str(ccb)])  # fmt: skip


def build_bitrate_exceeded(out: Path) -> None:
    """Lossless H.264 of per-frame random noise, well above the 100 Mbps the coverage settings allow."""
    run(
        [FFMPEG, "-y", "-f", "lavfi", "-i", "nullsrc=size=800x600:duration=3:rate=30",
         "-vf", "geq=random(1)*255:128:128", "-c:v", "libx264", "-preset", "ultrafast", "-qp", "0",
         "-pix_fmt", "yuv420p", "-metadata:s:v:0", "language=eng", str(out)]
    )  # fmt: skip


def build_no_cues(out: Path) -> None:
    """A healthy MKV written without a Cues element, so its seek index is unusable."""
    with tempfile.TemporaryDirectory() as td:
        clip = Path(td) / "clip.mkv"
        _gen_sdr_av(clip)
        run([MKVMERGE, "-o", str(out), "--no-cues", str(clip)])


TARGETS: dict[str, Callable[[Path], None]] = {
    "hdr10-base": gen_hdr10_base,
    "hdr10-multitrack": build_hdr10_multitrack,
    "hdr10-cc": build_hdr10_cc,
    "hdr10plus-cc": build_hdr10plus_cc,
    "dts-timebase": build_dts_timebase,
    "dts-duplicate-pts": build_dts_duplicate_pts,
    "dts-mixed": build_dts_mixed,
    "dts-audio-tagged": build_dts_audio_tagged,
    "container-mp4": build_container_mp4,
    "container-renamed": build_container_renamed,
    "uppercase-extension": build_uppercase_extension,
    "unwanted-extension": build_unwanted_extension,
    "cover-art": build_cover_art,
    "language-tags": build_language_tags,
    "title-flags": build_title_flags,
    "interlaced": build_interlaced,
    "reencode": build_reencode,
    "sdr-cc": build_sdr_cc,
    "bitrate-exceeded": build_bitrate_exceeded,
    "no-cues": build_no_cues,
}

# The file name each target takes in a corpus, its extension being part of what a target tests.
CORPUS_NAMES: dict[str, str] = {
    target: f"{target}.mkv" for target in TARGETS if target != "hdr10-base"
} | {
    "container-mp4": "container-mp4.mp4",
    "uppercase-extension": "uppercase-extension.MKV",
    "unwanted-extension": "unwanted-extension.txt",
}


def build_corpus(out_dir: Path) -> None:
    """Build every corpus target into ``out_dir`` under its corpus name."""
    out_dir.mkdir(parents=True, exist_ok=True)
    for target, name in sorted(CORPUS_NAMES.items()):
        TARGETS[target](out_dir / name)
        print(f"built {target} -> {out_dir / name}")


def main() -> None:
    ap = argparse.ArgumentParser(description=__doc__)
    group = ap.add_mutually_exclusive_group()
    group.add_argument(
        "target", nargs="?", choices=sorted(TARGETS), help="which synthetic fixture to build"
    )
    group.add_argument("--corpus", type=Path, help="build every corpus target into this directory")
    ap.add_argument("-o", "--out", type=Path, help="output file, required with a target")
    args = ap.parse_args()
    if args.corpus is not None:
        if args.out is not None:
            ap.error("--out applies to a single target, not to --corpus")
        build_corpus(args.corpus)
        return
    if args.target is None:
        ap.error("give a target or --corpus")
    if args.out is None:
        ap.error("a target requires --out")
    TARGETS[args.target](args.out)
    print(f"built {args.target} -> {args.out}")


if __name__ == "__main__":
    main()
