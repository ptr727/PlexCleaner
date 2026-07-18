"""Inject CEA-608 (A53) closed-caption SEI into an HEVC Annex-B stream.

No common tool embeds closed captions into HEVC - ffmpeg ``-a53cc`` is libx264 only, and libx265
drops caption side data. This inserts a prefix-SEI NAL (type 39) carrying an A53
``user_data_registered_itu_t_t35`` cc_data payload before every VCL NAL, leaving any existing HDR
SEI (mastering-display / MaxCLL, and any HDR10+ ST 2094) in place. The result is an HDR + CC HEVC
stream where ffprobe/MediaInfo report ``closed_captions=1`` and PlexCleaner reaches its
CC-on-HDR branch (which errors rather than removing CC, to avoid stripping HDR metadata).

Standalone use::

    ffmpeg -i in.mkv -c:v copy -bsf:v hevc_mp4toannexb base.hevc
    python3 inject_cc_sei.py base.hevc cc.hevc
    mkvmerge -o out.mkv --default-duration 0:24fps cc.hevc

Or import ``inject(hevc_bytes) -> hevc_bytes`` from another script.
"""

import re
import sys


def build_cc_sei(cc_count: int = 1) -> bytes:
    """Build one prefix-SEI NAL (type 39) carrying an A53 CEA-608 cc_data payload.

    The captions are minimal but validly structured: field-1, cc_valid, null (odd-parity) bytes -
    enough for the decoder to attach A53 side data and set the closed-captions property. Emulation
    prevention (``0x000003``) is applied to the RBSP.
    """
    if not 1 <= cc_count <= 31:
        raise ValueError("cc_count must be 1..31 (the cc_count field is 5 bits)")
    triples = bytes([0xFC, 0x80, 0x80]) * cc_count
    payload = (
        bytes([0xB5, 0x00, 0x31, 0x47, 0x41, 0x39, 0x34, 0x03, 0xC0 | cc_count, 0xFF])
        + triples
        + bytes([0xFF])
    )
    sei = bytes([0x04, len(payload)]) + payload  # payloadType=4 (t35), payloadSize
    rbsp = sei + bytes([0x80])  # rbsp_trailing_bits
    ep = bytearray()  # emulation prevention: 0x03 after any 00 00 {00..03}
    zeros = 0
    for byte in rbsp:
        if zeros >= 2 and byte <= 3:
            ep.append(0x03)
            zeros = 0
        ep.append(byte)
        zeros = zeros + 1 if byte == 0 else 0
    return b"\x00\x00\x00\x01" + bytes([0x4E, 0x01]) + bytes(ep)  # prefix-SEI NAL (type 39)


def inject(data: bytes) -> bytes:
    """Return the Annex-B HEVC stream with a CC SEI inserted before every VCL NAL."""
    cc = build_cc_sei()
    # re.finditer on 00 00 01 matches both 3- and 4-byte start codes (the 4-byte code's trailing
    # 00 00 01 is found; its leading 00 is trimmed from the previous NAL below).
    starts = [m.start() for m in re.finditer(b"\x00\x00\x01", data)]
    if not starts:
        raise ValueError("input is not an Annex-B HEVC stream (no start codes found)")
    out = bytearray()
    for i, start in enumerate(starts):
        payload_start = start + 3
        if i + 1 < len(starts):
            nxt = starts[i + 1]
            payload_end = nxt - 1 if data[nxt - 1] == 0 else nxt
        else:
            payload_end = len(data)
        nal = data[payload_start:payload_end]
        if not nal:  # consecutive start codes -> empty NAL, skip
            continue
        if (nal[0] >> 1) & 0x3F < 32:  # VCL NAL -> prepend the CC SEI
            out += cc
        out += b"\x00\x00\x00\x01" + nal
    return bytes(out)


def main() -> None:
    if len(sys.argv) != 3:
        sys.exit("usage: inject_cc_sei.py <in.hevc> <out.hevc>")
    data = open(sys.argv[1], "rb").read()
    result = inject(data)
    open(sys.argv[2], "wb").write(result)
    print(f"injected CC SEI before each VCL NAL -> {sys.argv[2]}")


if __name__ == "__main__":
    main()
