# Regression Tests

Tooling and a reproducible process for verifying that PlexCleaner's processing decisions stay consistent across versions, using a curated collection of troublesome media files.

PlexCleaner's behavior depends heavily on the specific media it processes: most functional changes are driven by a media file or media tool quirk affecting playback. This suite pins that behavior by processing the same collection through successive builds and comparing the results down to the per-file processing decision.

## What is (and is not) in this directory

The committed contents are code plus a synthetic example. No media and no media filenames live in the repository:

- The **media collection** lives on a server, next to the media (like a Plex library would). It is never committed. The copyrighted filenames stay out of source control entirely.
- The same holds for issues, pull requests, and commit messages, per [GOVERNANCE.md][governance] "Representative Data in Agent-Authored Text". A defect found in the collection is reported with a synthetic fixture that carries the triggering property.
- The **media-specific reduction rules** (issue-localized cut windows and relaxed-acceptance overrides) also live with the media, in an external JSON file the tooling reads and writes. The repo ships only [`reduction-rules.example.json`][reduction-rules-example] with synthetic placeholder names.

## Layout

- [`regression-test.sh`][regression-test] -- the harness: provision a test dataset, process it through one Docker image, and write per-version results, logs, and run inputs.
- [`check_results.py`][check-results] -- compare a run's per-file outcomes with a baseline run, or with the synthetic expected outcomes.
- [`integration-test.sh`][integration-test] -- the CI integration run over the synthetic corpus.
- [`corpus_common.py`][corpus-common] -- shared library: log parsing, deterministic issue classification, and the clip and metadata-surgery helpers.
- [`catalog_corpus.py`][catalog-corpus] -- derive a machine-readable issue catalog (`catalog.json`) for a collection from a processing run.
- [`reduce_corpus.py`][reduce-corpus] -- build and validate a reduced collection: shrink each sample while proving every issue survives.
- [`locate_issue.py`][locate-issue] -- find where a decode signature lives in a file, and optionally record the located window into the external rules file.
- [`audit_physical.py`][audit-physical] -- physical-error-shape coverage audit of the reduced collection.
- [`reduction-rules.example.json`][reduction-rules-example] -- synthetic example of the external rules schema.
- [`synthetic/`][synthetic] -- the synthetic-targeting library: build small, fully synthetic fixtures that reproduce specific detections without any copyrighted media (see below).
- [`pyproject.toml`][pyproject] -- ruff and mypy configuration for the Python tooling.
- [`tests/`][tests] -- the `unittest` suite for the pure helpers in the Python tooling, which CI runs under coverage.

## The collection

Two collections live side by side in the media dataset, each with its own generated `catalog.json`:

- `full/` -- the complete troublesome samples. The source of truth.
- `reduced/` -- shorter clips derived from `full/`, each proven to reproduce the same issue set. A reduced run is far faster (minutes instead of hours) and is the default for iterating.

Both are read-only during a run. The harness never mutates the collection; it processes a disposable copy.

## Running the harness

`regression-test.sh` provisions a disposable test dataset as a zero-copy ZFS clone of the newest collection snapshot. It processes the clone through a single Docker image tag. It writes the results into a directory named after the image build version, so runs stay durable and comparable across versions.

```shell
sudo ./regression-test.sh [quick|full] [tag] [corpus] [plugin...]
```

- `quick` (default) keeps full-file scanning as in production but writes short test snippets to shorten remux and re-encode. `full` also processes the complete media.
- `tag` is the Docker image tag to test (default `develop`).
- `corpus` selects `full` (default) or `reduced`.
- `plugin` names optional example plugins to build and run after processing, to confirm a plugin loads and runs against the processed dataset.

A `tag` holding a colon names a local image instead, such as `plexcleaner:local`, which the harness runs without a pull. A reference naming the published repository runs as published, by its tag or its digest. That is how a branch is tested before any published image carries it. Commit the branch, then build the image from it per [OPERATIONS.md][operations] "Test a tool version the image does not carry", adding `--build-arg LABEL_VERSION=$(git rev-parse --short=10 HEAD)`. The build compiles the working tree, so an uncommitted edit would run under the label of a commit that lacks it. The label names the results directory, so the harness refuses a local label that is not a commit hash. That refuses the Dockerfile default `1.0.0.0`, and any published version, which always holds a dot. A local run records `local` as its channel, and a digest reference records the digest. So a tool picking the newest `develop` run picks neither. It builds no plugin, since a plugin builds from the release tag its image version names.

Setting `REGRESSION_COVERAGE_TOOL` to the absolute path of a host directory holding `dotnet-coverage.dll` runs the process and plugin runs under `dotnet-coverage collect`. Each run writes a Cobertura report beside its log, `coverage_process.xml` and `coverage_custom_<plugin>.xml`, and each harness invocation first removes the reports an earlier one left. The image's sources are under `/Builder`, and each plugin is built with a path map to the same root. So [codecov.yml][codecov]'s `fixes` rule maps every report. The version directory's name, less any `-reduced` suffix, is a published build's release tag or a local build's commit. So `git rev-parse <version>^{commit}` gives the run's commit. `sudo` resets the environment, so pass the variable through `sudo env REGRESSION_COVERAGE_TOOL=<dir> ./regression-test.sh ...`.

Provisioning uses ZFS clones rather than `rsync`: the clone is instant and drift-free, and a rollback to the clone's own snapshot works even while long-running media containers hold the mount.

### Comparing two runs

Each run writes `buildinfo.json` beside its results. It records the image and every input that shapes the outcome. Those inputs are the mode, the corpus, the corpus snapshot cloned, and the process options. They also cover the settings file's SHA-256, whether coverage ran, and the plugins built. Writing it needs `jq` on the host. Compare a run against a baseline run in one step:

```shell
python3 check_results.py --baseline <baseline>/Results_process.json --results <candidate>/Results_process.json
```

The comparison first checks the two runs' inputs. It reads each run's tool versions from the `<Tool> : Version:` lines at the start of its `PlexCleaner_process.log`. A log lacking the version of any tool PlexCleaner checks at startup records no tool versions. It warns of each input other than the image and the plugins that differs, or that either run does not record. A plugin difference is only noted, since the plugins run after `Results_process.json` is written. So a corpus refresh, a thread-count change, a settings change, or a tool update shows beside the outcome differences it can explain. It then prints one line per file whose result, new name, `Modified` flag, `State` flags, failed operation, or invalid reason differs.

The comparison then reads the two runs' `PlexCleaner_process.log` files, for two further checks:

- **Output tracks.** PlexCleaner logs a file's MkvMerge track list at `Debug` each time it reads the file, so the last dump describes the output. The comparison prints one line per file whose output track types, formats, or codecs differ. A run whose log holds no `Debug` dump leaves the track check unverified, and the comparison warns of that rather than reporting a match.
- **Normalized log lines.** A parallel run interleaves files differently each time, so the two logs compare as multisets of lines. Each line first loses the fields that differ between two runs of one build. Those are the timestamps, thread and process ids, the version path segment, and the regenerated track `Uid` values. They also include ASLR addresses, the version banner lines, progress percentages, elapsed and processing times, HandBrake clock stamps, and FFmpeg progress figures. The comparison warns of each normalized line one run holds more often than the other.

A re-encode is not byte-identical from run to run, so its bitrate summary can shift. A bitrate summary names no file, so the comparison attributes it to the file its thread last began processing. A shift in the summary or in a bitrate warning is only noted for such a file. That file was re-encoded, deinterlaced, or repaired, and its outcome matches in both runs.

The comparison exits `1` when the outcomes or the output tracks differ, and `0` otherwise, whatever it warned of. So an unverified track check exits `0` beside its warning. Judge each difference as an intended effect of the change under test, an effect of a warned input, a regression, or noise.

### Full-file scanning

The harness scans the whole file; it does not use `--quickscan`. Bounding the scan to the start of the file causes false negatives for defects that surface later, notably closed-caption detection and interlace detection, so a full scan is the correct default for regression comparison.

## The issue catalog

`catalog_corpus.py` turns a processing run into `catalog.json`: one entry per file recording its processing State, the detections it triggered, and the classified decode-error subtypes. This catalog is the ground truth the reduction proves against, and the artifact compared between versions.

Each entry also records the interlace scan's verdict and idet counts, from PlexCleaner's `Interlace scan counts` log line, as a survey of the corpus's field orders. The field is `null` where no scan ran, or where the scan exceeded FFmpeg's decode error rate. It is informational, and the reduction ignores it, since a cut clip's counts differ from its source's.

Classification is deterministic and lives in `corpus_common.py`: raw ffmpeg error lines carry per-site coordinates (macroblock positions, picture numbers) and accumulate across every corrupt site, so they are normalized to a stable signature class before comparison.

## Reducing the collection

`reduce_corpus.py` shrinks each sample while proving no issue is lost. A candidate clip is processed through the image and must match the source catalog entry on all of:

- State equality (the processing-decision fingerprint, which catches issues that leave no log signature).
- detections superset (every detection re-surfaces).
- verify-error signatures superset (every error class re-surfaces).

Any miss keeps the original whole, so an issue is never dropped.

### Cutter ladder

Cutting a clip can silently repair the very defect the sample exists to capture, and the two cutters have mirror-image side effects: an `mkvmerge` cut preserves timestamp defects but strips language-IETF metadata, while an `ffmpeg` cut preserves metadata but normalizes some timestamp defects. So the tool tries a ladder of cutters plus in-place metadata surgery and lets the prove-equivalence gate pick the one that keeps this file's issues:

- head clips and region clips via `mkvmerge` and `ffmpeg`. The order is defect-type driven: a decode-signature file (its defect lives in the video packets) tries the `ffmpeg` stream-copy first, because `mkvmerge` can pass the coarse gate while losing the physical error shape; other files try `mkvmerge` first, to preserve timestamp defects `ffmpeg` would normalize.
- surgical rungs that edit the header in place with no remux: a `noietf` rung re-injects the missing-IETF-metadata defect an `mkvmerge` cut would repair, and a `fixietf` rung sets IETF on an `ffmpeg` cut so a timestamp defect drives the verify-and-repair chain.
- an `artificial-container` last-resort rung that re-muxes the head with the MP4 muxer under the `.mkv` name, reproducing a source that IS a renamed MP4. It sits after every real MKV cutter, so a genuine MKV never reaches it (and would be rejected by the gate if it did).

Samples at or near the window length ship verbatim, because any cut remuxes and would repair container or metadata defects.

Some samples reproduce their defect but diverge from the source by a known, benign amount -- a downstream artifact of a repair that depends on full-file content, not the defect itself (for example a remux the 60s clip cannot trigger). Rather than keep the whole source, an `accept` rule in the external rules file declares the exact, bounded delta tolerated for a named rung -- in State, detections, or verify-error signatures; a clip within that bound passes as `relaxed` and the catalog records the delta so the divergence stays explicit.

### Region rules (generate on demand)

Most defects live in the head of the file, so the default is a head clip. Defects deep in a file need an issue-localized window. Those windows are media-specific, so they are not hard-coded; they live in an external rules file next to the collection (default `reduction-rules.json` beside the catalog).

Generate them from your own media on demand:

```shell
# locate the decode signature and record a padded window into the rules file's located map
python3 locate_issue.py --catalog /path/to/full/catalog.json --full --write-rules /path/to/full/reduction-rules.json

# build the reduced collection, reading those windows
python3 reduce_corpus.py --catalog /path/to/full/catalog.json --mode generate --out /path/to/reduced
```

Reduce processes each clip with `--testsnippets`, so a repair re-encodes only the clip's first 30 seconds. A repair copies a subtitle defect through rather than fixing it. So a window for a source that ends `RepairFailed` starts less than 30 seconds before the defect, and the repaired snippet fails verify again. A window for a source that repairs clean starts more than 30 seconds before the defect, so the snippet stays clear of it. A defect less than that lead into the file starts its window at 0 instead, which can put it inside the snippet. `locate_issue.py --write-rules` picks the lead from the catalog's state the same way.

The rules schema has three maps, each keyed by source basename. The `regions` map holds hand-tuned cut windows, which no tool writes. The `located` map holds the windows `locate_issue.py --write-rules` generates, which a later locate run replaces. A `regions` entry wins over a `located` one for the same file, and reduce says when it does. So tune a window by adding it to `regions`, where a locate run cannot overwrite it. The `accept` map holds relaxed-acceptance overrides. See [`reduction-rules.example.json`][reduction-rules-example]. If the rules file is absent, every file is head-clipped, no relaxed acceptance applies, and the tool says so.

## Physical-shape coverage audit

The reduction gate compares broad signature classes, and a class can lump several distinct physical ffmpeg messages together. `audit_physical.py` closes that gap: it extracts every physical error shape (the exact message template, with run- and site-varying content normalized out) from the ground run and from each reduced clip, and reports any source shape a clip fails to reproduce. It augments the reduced `catalog.json` with per-file and corpus-level coverage figures, so an under-covered area is always visible and it is known when a change warrants a full-collection run.

## Synthetic file generation

The corpus is built from real troublesome media. Many issues are *structural*, though (track layout, flags, tags, language, container, HDR, closed captions), and depend on nothing copyrighted. Those can be reproduced by **fully synthetic** files, ffmpeg `lavfi` video/audio (`testsrc`, `testsrc2`, `sine`) plus a targeted mux. A fixture can then be regenerated anywhere with no media to ship. The [`synthetic/`][synthetic] library is the start of this.

### Objectives

Synthetic generation is a *targeted* tool, not a wholesale replacement for the real corpus:

1. **Fill sample gaps** -- build a fixture for a code path or detection the corpus does not yet cover, without sourcing (often unobtainable) real media.
2. **Substitute oversized files** -- replace an excessively large sample with a small synthetic that reproduces the same issue set (e.g. a multi-GB 4K HDR title cut to a few MB), reducing run time with no loss of coverage.
3. **On-the-fly coverage generation (the larger goal)** -- generate inputs during testing to exercise code paths and grow coverage at CI time (generate a file, run the test, repeat); see *Toward xUnit coverage generation* below.

### Components

- [`synthetic/synthesize.py`][synthetic-synthesize] -- generation primitives (HDR10 base, SDR video, audio, subtitles) and builders that target a specific detection set, plus a small CLI.
- [`synthetic/inject_cc_sei.py`][synthetic-inject-cc-sei] -- insert CEA-608 closed-caption SEI into an HEVC stream, preserving any HDR SEI. No common tool does this, since ffmpeg `-a53cc` is libx264 only and libx265 drops captions. Importable, or a standalone CLI.
- [`synthetic/inject_hdr10plus_sei.py`][synthetic-inject-hdr10plus-sei] -- insert a minimal SMPTE ST 2094-40 (HDR10+) dynamic-metadata SEI (the shipped x265 builds lack `dhdr10`), so MediaInfo reports the stream as "SMPTE ST 2094 App 4". Importable, or a standalone CLI.
- [`synthetic/hevc_nal.py`][synthetic-hevc-nal] -- shared HEVC helpers used by both injectors. They pack bits, wrap a T.35 payload into a prefix-SEI NAL, and insert a SEI before every VCL NAL.
- [`synthetic/expected-results.json`][synthetic-expected-results] -- the expected process result of every file in the integration run, see *CI integration run* below.
- [`integration-test.sh`][integration-test] and [`check_results.py`][check-results] -- the integration run and its result check. The same script compares two harness runs, see *Comparing two runs* above.

Requires `ffmpeg` (with libx265), `ffprobe`, and `mkvmerge` on `PATH`. Examples:

```shell
python3 synthetic/synthesize.py hdr10-multitrack -o multitrack.mkv   # targets the track-structure set
python3 synthetic/synthesize.py hdr10-cc -o hdr-cc.mkv               # HDR10 (ST 2086) HEVC + closed captions
python3 synthetic/synthesize.py hdr10plus-cc -o hdr10plus-cc.mkv     # true HDR10+ (ST 2094) HEVC + closed captions
python3 synthetic/synthesize.py dts-timebase -o dts-timebase.mkv     # healthy VFR cadence, verifies clean
python3 synthetic/synthesize.py dts-duplicate-pts -o dts-dup.mkv     # one repeated frame timestamp, fails verify
python3 synthetic/synthesize.py dts-mixed -o dts-mixed.mkv           # audio DTS break plus post-decode video break
python3 synthetic/synthesize.py dts-audio-tagged -o dts-audio.mkv  # audio DTS break under a tag and an attachment, repaired losslessly
python3 synthetic/synthesize.py --corpus corpus                       # every corpus target, named by CORPUS_NAMES
```

### Targeting methodology

Pick the detections a fixture must reproduce, build a candidate, then **validate by processing it through the PlexCleaner image** and confirming its detections and State -- the same prove-equivalence idea the reduced corpus uses. Iterate the construction against the log until it matches the target. The builders encode what actually triggers each detection (verified this way):

- **Duplicate tracks** -- the dedup keeps every *flagged* track and only dedups the rest by preferred codec, so a duplicate pair must be **unflagged** and in a keep language.
- **Track flags to be set** -- a title-implied flag (SDH / CC / Commentary / Forced) that is not set as an actual flag (e.g. a subtitle titled "Forced" without the forced flag).
- **Unwanted language tracks** -- a language not in `KeepLanguages` and not the original.
- **Redundant Default flags** -- two or more tracks of a type flagged default.
- **Extra video tracks** -- a second video stream.
- **Tags** -- mkvmerge statistics tags plus a container title.
- **HDR survives remux** -- the HDR SEI is copied through an mkvmerge remux; pairing it with a removable track (above) forces the remux, so the fixture guards HDR passthrough during cleanup.
- **Closed captions on HDR** -- an injected A53 CC SEI on an HDR HEVC stream reaches the CC-on-HDR branch (which refuses removal to avoid stripping HDR metadata). `hdr10-cc` builds the static ST 2086 case; `hdr10plus-cc` also injects an ST 2094-40 SEI so the branch fires over the *dynamic* HDR10+ metadata that is genuinely at risk when the CC SEI is removed.
- **Verify DTS time base** -- `dts-timebase` muxes a 23.976 cadence followed by a denser 29.97 cadence under a 23.976 default duration. Every timestamp increases at 1 ms precision, so verify passes it. A verify that encodes in the 1/framerate time base instead rounds the dense frames onto the same tick and reports a false non-monotonic DTS.
- **Verify DTS duplicate PTS** -- `dts-duplicate-pts` repeats one frame timestamp. Verify reports the repeat at native precision. The demuxer derives a DTS from the repeated timestamp, so the repeat is demux-visible and takes the full repair ladder.
- **Verify DTS mixed** -- `dts-mixed` swaps two B-frame video timestamps and steps one AAC packet timestamp back, using the unsorted mkvmerge v4 timestamps format. The demuxer derives video DTS from the sorted timestamps, so only the audio break is demux-visible, while the decoded video display order runs backward. The lossless `setts` tier repairs the audio but leaves the video failing verify, so the repair stops after the remux and marks the file `RepairFailed`. The v2 timestamps format cannot build this fixture, because mkvmerge sorts v2 timestamps and so removes both the swap and the step back.
- **Progressive SDR source** -- ffmpeg `idet` reads small `testsrc2` frames as interlaced, with both field orders outnumbering the progressive frames. A deinterlace re-encode then hides the path a fixture targets, so the SDR primitives use `testsrc`, which `idet` reads as progressive. The 4K HDR base keeps `testsrc2`, which `idet` reads as progressive at that size.
- **Containers and names** -- `container-mp4` is remuxed by its extension. `container-renamed` is an MP4 under an MKV name, which only the mkvmerge container check finds. `uppercase-extension` is renamed, and `unwanted-extension` is deleted.
- **Cover art** -- `cover-art` attaches a JPEG, which ffprobe reports as a cover art video track, and the attachment removal clears it.
- **Language tags** -- `language-tags` carries only legacy ISO 639-2 tags, which read as IETF tags to set, and its audio language is undefined.
- **Interlaced** -- `interlaced` weaves 59.94 progressive frames into 29.97 top-field-first frames, which `idet` detects and processing deinterlaces.
- **Re-encode** -- `reencode` carries MPEG-2 video and MP2 audio, both on the re-encode lists.
- **Closed captions on SDR** -- `sdr-cc` injects the A53 CC SEI into an SDR HEVC stream, which processing removes.
- **Bitrate exceeded** -- `bitrate-exceeded` is lossless H.264 of per-frame noise, above the integration settings' 100 Mbps maximum.
- **Seek index** -- `no-cues` is written with `mkvmerge --no-cues`, so its SeekHead names no Cues and processing remuxes it.
- **Verify DTS audio tagged** -- `dts-audio-tagged` steps one AAC packet timestamp back under a global tag and an attachment, with healthy video. The lossless `setts` tier repairs the whole file. Its MkvMerge pass drops the tag, the attachment, and the tags FfMpeg writes, like every other rewrite. The regression gate compares only the media streams, since the attachment stream is gone after the pass.

### Extending the library

Add a builder function that constructs the target with the primitives, register it in `TARGETS`, and validate it through the image. Keep everything synthetic (or publicly sourced) -- never commit copyrighted media or media filenames. Every target except `hdr10-base` joins the integration run, under the file name `CORPUS_NAMES` gives it. So a new target fails the run until its expected result is recorded as below. Candidate next steps: AVI and WMV container targets, and per-frame-varying HDR10+ metadata.

### CI integration run

The `integration` CI job runs [`integration-test.sh`][integration-test] against an image built from the pull request tree. It generates the corpus in a throwaway container from that image with `python3` added, and adds the [Matroska test files][matroska-test-files-link] at a pinned commit. It then runs the image's `/Test/test.sh` under `dotnet-coverage` with every optional processing step on. [`check_results.py`][check-results] compares each file's result, new name, `Modified` flag, `State` flags, failed operation, and invalid reason with [`synthetic/expected-results.json`][synthetic-expected-results], and any difference fails the job. Codecov receives the coverage under the `integration` flag. Run it locally against an image built per [OPERATIONS.md][operations] "Test a tool version the image does not carry":

```shell
dotnet tool restore
./RegressionTests/integration-test.sh plexcleaner:local /tmp/integration
```

A changed result fails the run until it is recorded. Confirm in the run's log that the change is intended. Then record it from the run's results file, and commit the expected file with the change:

```shell
python3 RegressionTests/check_results.py --write --expected RegressionTests/synthetic/expected-results.json --results /tmp/integration/out/Results.json
```

### Toward xUnit coverage generation

Objective 3 is the direction of travel: raise unit-test **code coverage** by exercising the decision logic directly, with no media files and no media tools in the test. The code worth covering runs *downstream* of the tool boundary -- the real flow is media file -> media tool -> tool output -> the branch-heavy C# decision and processing logic (track selection and de-duplication, flag / tag / language cleanup, verify classification, and the closed-caption / interlace / bitrate decisions) -- and only that last stage carries the complex logic. None of it needs a real file; it needs the *output a tool would have reported*.

The seams for this already exist. Tool execution goes through the `MediaTool` wrappers over CliWrap: usually a `BufferedCommandResult` of stdout / stderr / exit code, and for the bounded paths a streamed stderr (`MediaTool.ExecuteStreamStdErr`, used by `FfMpegTool.VerifyMedia` to keep per-packet output bounded). The parsed results live in `SidecarFile` as the ffprobe / mediainfo / mkvmerge JSON plus the `MediaProps` (`VideoProps` / `AudioProps` / `SubtitleProps` / `TrackProps`) the decision logic reads. So a test can either feed canned tool output at the execution seam (a `BufferedCommandResult`, or streamed stderr lines) or construct a synthetic sidecar / `MediaProps` and drive the processing logic straight from it -- `FfMpegIdetParsingTests`, `VerifyClassifierTests`, and `ToolFailureLogFormatTests` (which uses a minimal `TestTool` subclass) already exploit this seam; the goal is to apply it systematically across the processing paths.

So the synthetic work shifts from emitting a media *file* to emitting what the tools would have *discovered*: the probe / mediainfo / mkvmerge JSON, the frame-by-frame stats that drive bitrate / closed-caption / interlace detection, and the success or failure shape of a tool run. A generator produces those fixtures (and a synthetic sidecar to hold them), a test feeds them into the target logic and asserts the resulting `State` and detections, and repeats -- all in-process, so it runs cheaply in CI with no ffmpeg or mkvmerge. This is the C# counterpart to the Python library's prove-equivalence loop: the Python side establishes what a real file makes the tools report, and the fixture encodes that report so the C# logic can be exercised against it without the file.

Tool output drifts as the media tools change, which is itself worth covering: keeping fixtures for a range of tool-version outputs (the parsers must handle each) means the same mechanism that raises coverage also guards against the parse regressions a tool upgrade can introduce. Objectives 1 and 2 stay served by the Python media generators (real files, real tools); objective 3's in-process coverage is served by generating tool-output and sidecar fixtures and mocking the tool seam in C#, so the complex logic is executed without any media.

## Naming conventions

Collection filenames follow a small set of conventions so the catalog stays readable:

- a descriptive real title for a naturally occurring sample.
- a codec-matrix name (`codec_container`) for a sample that exists to exercise a specific combination.
- a `Word-Word` behavior name for a sample built to test one behavior.
- a `[container]` disambiguation tag appended only when two samples would otherwise collide on the output stem (PlexCleaner renames every output to `<stem>.mkv`).
- a filename fixture whose media is a tiny synthetic clip and whose filename is the actual test.

## Update, validate, snapshot

The working loop when the collection changes:

1. Update the collection (add or adjust a sample).
2. Snapshot the dataset, since the harness clones the newest snapshot rather than the live collection.
3. Run the harness over the `full` corpus, so a run reflects the updated collection.
4. Regenerate the affected catalog with `catalog_corpus.py --run <that run's results directory>`.
5. Rebuild and validate the reduced collection with `reduce_corpus.py`, and audit coverage with `audit_physical.py --run` naming the same run.
6. Snapshot the dataset again, so a run clones the validated collection.

## Python tooling

The Python utilities are standalone and stdlib-only (subprocess, json, argparse, pathlib, re). They are linted with ruff, type-checked with mypy, and tested with the `unittest` suite under [`tests/`][tests]. The configuration is in [`pyproject.toml`][pyproject]. Run them via `uvx`, which needs no install:

```shell
uvx ruff@latest check .
uvx ruff@latest format --check .
uvx mypy@latest
uvx coverage@latest run -m unittest discover -s tests
```

These run the latest tools and are available as VSCode tasks. CI runs the same latest tools through the hub's reusable validator. Python source is LF, matching the repository's default line-ending convention.

<!-- Repo -->

[audit-physical]: audit_physical.py
[catalog-corpus]: catalog_corpus.py
[check-results]: check_results.py
[codecov]: ../codecov.yml
[corpus-common]: corpus_common.py
[governance]: ../GOVERNANCE.md
[integration-test]: integration-test.sh
[locate-issue]: locate_issue.py
[operations]: ../OPERATIONS.md
[pyproject]: pyproject.toml
[reduce-corpus]: reduce_corpus.py
[reduction-rules-example]: reduction-rules.example.json
[regression-test]: regression-test.sh
[synthetic]: synthetic/
[synthetic-expected-results]: synthetic/expected-results.json
[synthetic-hevc-nal]: synthetic/hevc_nal.py
[synthetic-inject-cc-sei]: synthetic/inject_cc_sei.py
[synthetic-inject-hdr10plus-sei]: synthetic/inject_hdr10plus_sei.py
[synthetic-synthesize]: synthetic/synthesize.py
[tests]: tests/

<!-- External -->

[matroska-test-files-link]: https://github.com/ietf-wg-cellar/matroska-test-files
