"""Inject CEA-608 (A53) closed-caption SEI into an HEVC Annex-B stream.

No common tool embeds closed captions into HEVC - ffmpeg ``-a53cc`` is libx264 only, and libx265
drops caption side data. This inserts a prefix-SEI NAL (type 39) carrying an A53
``user_data_registered_itu_t_t35`` cc_data payload before every VCL NAL, leaving any existing HDR
SEI (mastering-display / MaxCLL, and any HDR10+ ST 2094) in place. The result is an HDR + CC HEVC
stream where ffprobe/MediaInfo report ``closed_captions=1`` and PlexCleaner reaches its CC-on-HDR
branch (which errors rather than removing CC, to avoid stripping HDR metadata).

Standalone use::

    ffmpeg -i in.mkv -c:v copy -bsf:v hevc_mp4toannexb base.hevc
    python3 inject_cc_sei.py base.hevc cc.hevc
    mkvmerge -o out.mkv --default-duration 0:24fps cc.hevc

Or import ``build_cc_sei()`` / ``inject(hevc_bytes) -> hevc_bytes`` from another script.
"""

import sys
from pathlib import Path

from hevc_nal import insert_before_vcl, wrap_sei_t35


def build_cc_sei(cc_count: int = 1) -> bytes:
    """Build one prefix-SEI NAL (type 39) carrying an A53 CEA-608 cc_data payload.

    The captions are minimal but validly structured: field-1, cc_valid, null (odd-parity) bytes -
    enough for the decoder to attach A53 side data and set the closed-captions property.
    """
    if not 1 <= cc_count <= 31:
        raise ValueError("cc_count must be 1..31 (the cc_count field is 5 bits)")
    triples = bytes([0xFC, 0x80, 0x80]) * cc_count
    payload = (
        bytes([0xB5, 0x00, 0x31, 0x47, 0x41, 0x39, 0x34, 0x03, 0xC0 | cc_count, 0xFF])
        + triples
        + bytes([0xFF])
    )
    return wrap_sei_t35(payload)


def inject(data: bytes) -> bytes:
    """Return the Annex-B HEVC stream with a CC SEI inserted before every VCL NAL."""
    return insert_before_vcl(data, build_cc_sei())


def main() -> None:
    if len(sys.argv) != 3:
        sys.exit("usage: inject_cc_sei.py <in.hevc> <out.hevc>")
    Path(sys.argv[2]).write_bytes(inject(Path(sys.argv[1]).read_bytes()))
    print(f"injected CC SEI before each VCL NAL -> {sys.argv[2]}")


if __name__ == "__main__":
    main()
