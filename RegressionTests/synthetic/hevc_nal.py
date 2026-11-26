"""Shared HEVC Annex-B NAL helpers for the SEI injectors.

Both the closed-caption and HDR10+ injectors build a ``user_data_registered_itu_t_t35`` SEI
(payload type 4) and insert it before every VCL NAL. This module holds the parts they share: bit
packing, wrapping a T.35 payload into a prefix-SEI NAL, and walking the Annex-B start codes.
It also holds the command line both injectors run.
"""

import argparse
import re
import signal
import sys
from collections.abc import Callable
from pathlib import Path
from types import FrameType


class BitWriter:
    """Minimal MSB-first bit accumulator that packs to whole bytes (zero-padded)."""

    def __init__(self) -> None:
        self._bits: list[int] = []

    def write(self, value: int, count: int) -> None:
        if count < 1:
            raise ValueError("count must be >= 1")
        if not 0 <= value < (1 << count):
            raise ValueError(f"value {value} does not fit in {count} bits")
        for i in range(count - 1, -1, -1):
            self._bits.append((value >> i) & 1)

    def to_bytes(self) -> bytes:
        while len(self._bits) % 8:
            self._bits.append(0)
        out = bytearray()
        for i in range(0, len(self._bits), 8):
            byte = 0
            for bit in self._bits[i : i + 8]:
                byte = (byte << 1) | bit
            out.append(byte)
        return bytes(out)


def wrap_sei_t35(payload: bytes) -> bytes:
    """Wrap a T.35 payload into a prefix-SEI (type 39) NAL, with a start code.

    Builds ``payloadType=4`` + the ff-coded payload size + payload + rbsp trailing, applies
    emulation prevention (``0x000003``), and prepends the 4-byte start code and the 2-byte HEVC
    prefix-SEI NAL header.
    """
    size = bytearray()
    remaining = len(payload)
    while remaining >= 255:
        size.append(0xFF)
        remaining -= 255
    size.append(remaining)
    rbsp = bytes([0x04]) + bytes(size) + payload + bytes([0x80])
    ep = bytearray()
    zeros = 0
    for byte in rbsp:
        if zeros >= 2 and byte <= 3:
            ep.append(0x03)
            zeros = 0
        ep.append(byte)
        zeros = zeros + 1 if byte == 0 else 0
    return b"\x00\x00\x00\x01" + bytes([0x4E, 0x01]) + bytes(ep)


def insert_before_vcl(data: bytes, sei: bytes) -> bytes:
    """Return the Annex-B HEVC stream with ``sei`` inserted before every VCL NAL.

    ``re.finditer`` on ``00 00 01`` locates each start code; any number of leading ``zero_byte``
    padding before it (a 4-byte ``00 00 00 01`` or more) is stripped from the previous NAL, which is
    safe because a well-formed NAL never ends in ``0x00``. Empty NALs from consecutive start codes
    are skipped.
    """
    starts = [m.start() for m in re.finditer(b"\x00\x00\x01", data)]
    if not starts:
        raise ValueError("input is not an Annex-B HEVC stream (no start codes found)")
    out = bytearray()
    for i, start in enumerate(starts):
        payload_start = start + 3
        if i + 1 < len(starts):
            payload_end = starts[i + 1]
            while payload_end > payload_start and data[payload_end - 1] == 0:
                payload_end -= 1
        else:
            payload_end = len(data)
        nal = data[payload_start:payload_end]
        if not nal:
            continue
        if (nal[0] >> 1) & 0x3F < 32:  # VCL NAL -> prepend the SEI
            out += sei
        out += b"\x00\x00\x00\x01" + nal
    return bytes(out)


def run_injector(prog: str, description: str, inject: Callable[[bytes], bytes], label: str) -> None:
    """Read the input stream, inject the SEI, and write the output stream, as an injector's CLI."""
    ap = argparse.ArgumentParser(
        prog=prog, usage="%(prog)s [-h] input output", description=description
    )
    ap.add_argument("input", type=Path, nargs="?", help="input Annex-B HEVC stream")
    ap.add_argument("output", type=Path, nargs="?", help="output Annex-B HEVC stream")

    terminated = False

    def on_sigterm(_signum: int, _frame: FrameType | None) -> None:
        nonlocal terminated
        terminated = True
        raise KeyboardInterrupt

    signal.signal(signal.SIGTERM, on_sigterm)
    try:
        args, extra = ap.parse_known_args()
        if extra:
            ap.error(f"unrecognized arguments: {' '.join(extra)}")
        missing = [name for name in ("input", "output") if getattr(args, name) is None]
        if missing:
            ap.error(f"the following arguments are required: {', '.join(missing)}")
        args.output.write_bytes(inject(args.input.read_bytes()))
    except KeyboardInterrupt:
        print(f"{prog}: interrupted by {'SIGTERM' if terminated else 'SIGINT'}", file=sys.stderr)
        sys.exit(143 if terminated else 130)
    except OSError as e:
        sys.exit(f"{prog}: {e}, fix the path, then re-run")
    print(f"injected {label} SEI before each VCL NAL -> {args.output}")
