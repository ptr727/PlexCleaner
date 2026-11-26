"""Interrupt handling shared by the RegressionTests tools.

One handler takes SIGINT and SIGTERM, the entry wrapper reports the first and re-raises it.
"""

import contextlib
import os
import signal
import subprocess
import sys
import tempfile
from collections.abc import Callable
from pathlib import Path
from types import FrameType
from typing import Any

CHILD_STOP_SECONDS = 10

_received: list[int] = []


def _on_signal(signum: int, _frame: FrameType | None) -> None:
    # A later signal must not cut the cleanup short, such as the SIGTERM uv forwards after Ctrl-C.
    if _received:
        return
    _received.append(signum)
    raise KeyboardInterrupt


def install_handler() -> None:
    """Route SIGINT and SIGTERM through one handler that raises KeyboardInterrupt once."""
    # An inherited ignored SIGINT stays ignored, so only a default SIGINT is taken.
    if signal.getsignal(signal.SIGINT) is signal.default_int_handler:
        signal.signal(signal.SIGINT, _on_signal)
    signal.signal(signal.SIGTERM, _on_signal)


def _reraise(signum: int) -> None:
    if sys.platform == "win32":
        # On Windows, os.kill ends the process with the signal number as its status.
        sys.exit(130)
    for stream in (sys.stdout, sys.stderr):
        # The reader of a pipe may have gone with the same Ctrl-C.
        with contextlib.suppress(OSError):
            stream.flush()
    signal.signal(signum, signal.SIG_DFL)
    os.kill(os.getpid(), signum)
    # Only a process the signal does not end, such as PID 1 in a container, reaches here.
    sys.exit(128 + signum)


def run_main(main: Callable[[], int | None]) -> None:
    """Run main as the entry point, and on an interrupt print one line and re-raise the signal."""
    install_handler()
    try:
        status = main()
    except KeyboardInterrupt:
        pass
    else:
        sys.exit(status)
    signum = _received[0] if _received else signal.SIGINT
    with contextlib.suppress(OSError):
        print(
            f"{Path(sys.argv[0]).name}: interrupted by {signal.Signals(signum).name}",
            file=sys.stderr,
        )
    _reraise(signum)


def stop_child(proc: subprocess.Popen[Any]) -> None:
    """Stop a running child with SIGTERM, then kill it where it ignores that for too long."""
    if proc.poll() is not None:
        return
    proc.terminate()
    try:
        proc.wait(timeout=CHILD_STOP_SECONDS)
    except subprocess.TimeoutExpired:
        proc.kill()
        proc.wait()


def run_child(
    cmd: list[str], capture_output: bool = False, **kwargs: Any
) -> subprocess.CompletedProcess[Any]:
    """Run cmd to completion like subprocess.run without check, stopping the child on an interrupt.

    The child gets SIGTERM, which a docker CLI proxies to its container, where SIGKILL is never proxied.
    """
    if capture_output:
        kwargs["stdout"] = kwargs["stderr"] = subprocess.PIPE
    proc = subprocess.Popen(cmd, **kwargs)
    try:
        stdout, stderr = proc.communicate()
    finally:
        stop_child(proc)
        for stream in (proc.stdout, proc.stderr):
            if stream is not None:
                stream.close()
    return subprocess.CompletedProcess(proc.args, proc.returncode, stdout, stderr)


def _new_file_mode() -> int:
    # The umask can only be read by setting it, so put it straight back.
    mask = os.umask(0)
    os.umask(mask)
    return 0o666 & ~mask


def atomic_write_bytes(path: Path, data: bytes) -> None:
    """Write data to a temporary file beside path, then rename it over path."""
    # A symlinked target is written through, so the link itself survives the rename.
    path = Path(os.path.realpath(path))
    fd, name = tempfile.mkstemp(dir=path.parent, prefix=f".{path.name}.", suffix=".tmp")
    temp = Path(name)
    try:
        with os.fdopen(fd, "wb") as f:
            f.write(data)
        # The temporary file is created private, so the target's own mode is kept where it exists.
        temp.chmod(path.stat().st_mode & 0o7777 if path.exists() else _new_file_mode())
        os.replace(temp, path)
    except BaseException:
        temp.unlink(missing_ok=True)
        raise


def reserve_sibling(path: Path, tag: str) -> Path:
    """Create an empty, uniquely named sibling of path that keeps its extension, and return it."""
    fd, name = tempfile.mkstemp(
        dir=path.parent, prefix=f".{path.stem}.", suffix=f".{tag}{path.suffix}"
    )
    os.close(fd)
    # A builder writes into the reserved file and keeps its mode, so it takes the mode a new file would.
    Path(name).chmod(_new_file_mode())
    return Path(name)


def atomic_write_text(path: Path, text: str, encoding: str = "utf-8") -> None:
    """Write text to path atomically, with no newline translation."""
    atomic_write_bytes(path, text.encode(encoding))
