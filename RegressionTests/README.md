# Regression Tests

Tooling and a reproducible process for verifying that PlexCleaner's processing decisions stay
consistent across versions, using a curated collection of troublesome media files.

PlexCleaner's behavior depends heavily on the specific media it processes: most functional changes
are driven by a media file or media tool quirk affecting playback. This suite pins that behavior by
processing the same collection through successive builds and comparing the results down to the
per-file processing decision.

## What is (and is not) in this directory

The committed contents are code plus a synthetic example. No media and no media filenames live in
the repository:

- The **media collection** lives on a server, next to the media (like a Plex library would). It is
  never committed. The copyrighted filenames stay out of source control entirely.
- The **media-specific reduction rules** (issue-localized cut windows) also live with the media, in
  an external JSON file the tooling reads and writes. The repo ships only
  [`reduction-rules.example.json`](reduction-rules.example.json) with synthetic placeholder names.

## Layout

- [`RegressionTest.sh`](RegressionTest.sh) -- the harness: provision a test dataset, process it
  through one Docker image tag, and write per-version results and logs for diffing.
- [`corpus_common.py`](corpus_common.py) -- shared library: log parsing, deterministic issue
  classification, and the clip and metadata-surgery helpers.
- [`catalog_corpus.py`](catalog_corpus.py) -- derive a machine-readable issue catalog
  (`catalog.json`) for a collection from a processing run.
- [`reduce_corpus.py`](reduce_corpus.py) -- build and validate a reduced collection: shrink each
  sample while proving every issue survives.
- [`locate_issue.py`](locate_issue.py) -- find where a decode signature lives in a file, and
  optionally record the located window into the external rules file.
- [`audit_physical.py`](audit_physical.py) -- physical-error-shape coverage audit of the reduced
  collection.
- [`reduction-rules.example.json`](reduction-rules.example.json) -- synthetic example of the
  external rules schema.
- [`pyproject.toml`](pyproject.toml) -- ruff and mypy configuration for the Python tooling.

## The collection

Two collections live side by side in the media dataset, each with its own generated `catalog.json`:

- `full/` -- the complete troublesome samples. The source of truth.
- `reduced/` -- shorter clips derived from `full/`, each proven to reproduce the same issue set. A
  reduced run is far faster (minutes instead of hours) and is the default for iterating.

Both are read-only during a run. The harness never mutates the collection; it processes a
disposable copy.

## Running the harness

`RegressionTest.sh` provisions a disposable test dataset as a zero-copy ZFS clone of the newest
collection snapshot, processes it through a single Docker image tag, and writes results into a
directory named after the image build version so runs stay durable and version-to-version
comparable.

```shell
sudo ./RegressionTest.sh [quick|full] [tag] [corpus] [plugin...]
```

- `quick` (default) keeps full-file scanning as in production but writes short test snippets to
  shorten remux and re-encode. `full` also processes the complete media.
- `tag` is the Docker image tag to test (default `develop`).
- `corpus` selects `full` (default) or `reduced`.
- `plugin` names optional example plugins to build and run after processing, to confirm a plugin
  loads and runs against the processed dataset.

Provisioning uses ZFS clones rather than `rsync`: the clone is instant and drift-free, and a
rollback to the clone's own snapshot works even while long-running media containers hold the mount.

### Full-file scanning

The harness scans the whole file; it does not use `--quickscan`. Bounding the scan to the start of
the file causes false negatives for defects that surface later, notably closed-caption detection
and interlace detection, so a full scan is the correct default for regression comparison.

## The issue catalog

`catalog_corpus.py` turns a processing run into `catalog.json`: one entry per file recording its
processing State, the detections it triggered, and the classified decode-error subtypes. This
catalog is the ground truth the reduction proves against, and the artifact compared between
versions.

Classification is deterministic and lives in `corpus_common.py`: raw ffmpeg error lines carry
per-site coordinates (macroblock positions, picture numbers) and accumulate across every corrupt
site, so they are normalized to a stable signature class before comparison.

## Reducing the collection

`reduce_corpus.py` shrinks each sample while proving no issue is lost. A candidate clip is processed
through the image and must match the source catalog entry on all of:

- State equality (the processing-decision fingerprint, which catches issues that leave no log
  signature).
- detections superset (every detection re-surfaces).
- verify-error signatures superset (every error class re-surfaces).

Any miss keeps the original whole, so an issue is never dropped.

### Cutter ladder

Cutting a clip can silently repair the very defect the sample exists to capture, and the two cutters
have mirror-image side effects: an `mkvmerge` cut preserves timestamp defects but strips
language-IETF metadata, while an `ffmpeg` cut preserves metadata but normalizes some timestamp
defects. So the tool tries a ladder of cutters plus in-place metadata surgery and lets the
prove-equivalence gate pick the one that keeps this file's issues:

- head clips and region clips via `mkvmerge` and `ffmpeg`.
- surgical rungs that edit the header in place with no remux: a `noietf` rung re-injects the
  missing-IETF-metadata defect an `mkvmerge` cut would repair, and a `fixietf` rung sets IETF on an
  `ffmpeg` cut so a timestamp defect drives the verify-and-repair chain.

Samples at or near the window length ship verbatim, because any cut remuxes and would repair
container or metadata defects.

### Region rules (generate on demand)

Most defects live in the head of the file, so the default is a head clip. Defects deep in a file
need an issue-localized window. Those windows are media-specific, so they are not hard-coded; they
live in an external rules file next to the collection (default `reduction-rules.json` beside the
catalog).

Generate them from your own media on demand:

```shell
# locate the decode signature and record a padded window into the rules file
python3 locate_issue.py --catalog /path/to/full/catalog.json --full --write-rules /path/to/full/reduction-rules.json

# build the reduced collection, reading those windows
python3 reduce_corpus.py --catalog /path/to/full/catalog.json --mode generate --out /path/to/reduced
```

The rules schema is a `regions` map keyed by source basename; see
[`reduction-rules.example.json`](reduction-rules.example.json). If the rules file is absent, every
file is head-clipped and the tool says so.

## Physical-shape coverage audit

The reduction gate compares broad signature classes, and a class can lump several distinct physical
ffmpeg messages together. `audit_physical.py` closes that gap: it extracts every physical error
shape (the exact message template, with run- and site-varying content normalized out) from the
ground run and from each reduced clip, and reports any source shape a clip fails to reproduce. It
augments the reduced `catalog.json` with per-file and corpus-level coverage figures, so an
under-covered area is always visible and it is known when a change warrants a full-collection run.

## Naming conventions

Collection filenames follow a small set of conventions so the catalog stays readable:

- a descriptive real title for a naturally occurring sample.
- a codec-matrix name (`codec_container`) for a sample that exists to exercise a specific
  combination.
- a `Word-Word` behavior name for a sample built to test one behavior.
- a `[container]` disambiguation tag appended only when two samples would otherwise collide on the
  output stem (PlexCleaner renames every output to `<stem>.mkv`).
- a filename fixture whose media is a tiny synthetic clip and whose filename is the actual test.

## Update, validate, snapshot

The working loop when the collection changes:

1. Update the collection (add or adjust a sample).
2. Regenerate the affected catalog with `catalog_corpus.py`.
3. Rebuild and validate the reduced collection with `reduce_corpus.py`, and audit coverage with
   `audit_physical.py`.
4. Snapshot the dataset so a run can clone from it.

## Python tooling

The Python utilities are standalone and stdlib-only (subprocess, json, argparse, pathlib, re). They
are linted with ruff and type-checked with mypy; the configuration is in
[`pyproject.toml`](pyproject.toml). Run them via `uvx`, which needs no install:

```shell
uvx ruff check .
uvx ruff format --check .
uvx mypy .
```

The same commands run in CI and are available as VSCode tasks. Python source is CRLF, matching the
repository's default line-ending convention.
