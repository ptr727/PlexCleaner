"""Inject a minimal SMPTE ST 2094-40 (HDR10+) dynamic-metadata SEI into an HEVC Annex-B stream.

HDR10+ metadata is a ``user_data_registered_itu_t_t35`` SEI (Samsung terminal provider ``0x003C``,
application identifier 4) carrying bit-packed ST 2094-40 tone-mapping metadata. No common tool emits
it (the shipped x265 builds lack ``dhdr10`` support), so this constructs a minimal but structurally
valid payload - one window, the nine standard distribution percentiles, no tone-mapping curve - and
inserts it as a prefix-SEI NAL before every VCL NAL, preserving any existing SEI. MediaInfo then
reports "SMPTE ST 2094 App 4". Pair it with the closed-caption injector to reach PlexCleaner's
CC-on-HDR branch over the dynamic metadata that is genuinely at risk when the CC SEI is removed.

Standalone use::

    ffmpeg -i in.mkv -c:v copy -bsf:v hevc_mp4toannexb base.hevc
    python3 inject_hdr10plus_sei.py base.hevc hdr10plus.hevc
    mkvmerge -o out.mkv --default-duration 0:24fps hdr10plus.hevc

Or import ``build_hdr10plus_sei()`` / ``inject(hevc_bytes) -> hevc_bytes`` from another script.
"""

import sys
from pathlib import Path

from hevc_nal import BitWriter, insert_before_vcl, wrap_sei_t35

# ST 2094-40 distribution percentages (the standard nine)
_PERCENTAGES = (1, 5, 10, 25, 50, 75, 90, 95, 99)


def build_hdr10plus_sei() -> bytes:
    """Build one prefix-SEI NAL (type 39) carrying a minimal ST 2094-40 HDR10+ payload."""
    # T.35 header: country 0xB5, terminal provider 0x003C, oriented 0x0001, app id 4, app version 1
    header = bytes([0xB5, 0x00, 0x3C, 0x00, 0x01, 0x04, 0x01])
    bw = BitWriter()
    bw.write(1, 2)  # num_windows = 1
    bw.write(400, 27)  # targeted_system_display_maximum_luminance
    bw.write(0, 1)  # targeted_system_display_actual_peak_luminance_flag
    for _ in range(3):
        bw.write(1000, 17)  # maxscl[0..2]
    bw.write(500, 17)  # average_maxrgb
    bw.write(len(_PERCENTAGES), 4)  # num_distribution_maxrgb_percentiles
    for percentage in _PERCENTAGES:
        bw.write(percentage, 7)  # distribution_maxrgb_percentage
        bw.write(0, 17)  # distribution_maxrgb_percentile
    bw.write(0, 10)  # fraction_bright_pixels
    bw.write(0, 1)  # mastering_display_actual_peak_luminance_flag
    bw.write(0, 1)  # tone_mapping_flag (no bezier curve)
    bw.write(0, 1)  # color_saturation_mapping_flag
    return wrap_sei_t35(header + bw.to_bytes())


def inject(data: bytes) -> bytes:
    """Return the Annex-B HEVC stream with an HDR10+ SEI inserted before every VCL NAL."""
    return insert_before_vcl(data, build_hdr10plus_sei())


def main() -> None:
    if len(sys.argv) != 3:
        sys.exit("usage: inject_hdr10plus_sei.py <in.hevc> <out.hevc>")
    Path(sys.argv[2]).write_bytes(inject(Path(sys.argv[1]).read_bytes()))
    print(f"injected HDR10+ SEI before each VCL NAL -> {sys.argv[2]}")


if __name__ == "__main__":
    main()
