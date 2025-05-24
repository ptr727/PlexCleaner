#region

using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Text.RegularExpressions;
using CliWrap;
using CliWrap.Buffered;
using Serilog;

#endregion

// https://mkvtoolnix.download/doc/mkvmerge.html

// mkvmerge [global options] {-o out} [options1] {file1} [[options2] {file2}] [@options-file.json]

// https://codeberg.org/mbunkus/mkvtoolnix/wiki/About-track-UIDs,-track-numbers-and-track-IDs

// TODO: There is currently no option to suppress progress output
// https://help.mkvtoolnix.download/t/option-to-suppress-progress-reporting-but-keep-static-output/1320

// Error output goes to stdout not stderr

namespace PlexCleaner;

public partial class MkvMerge
{
    public partial class Tool : MediaTool
    {
        public override ToolFamily GetToolFamily() => ToolFamily.MkvToolNix;

        public override ToolType GetToolType() => ToolType.MkvMerge;

        protected override string GetToolNameWindows() => "mkvmerge.exe";

        protected override string GetToolNameLinux() => "mkvmerge";

        public IGlobalOptions GetBuilder() => Builder.Create(GetToolPath());

        public override bool GetInstalledVersion(out MediaToolInfo mediaToolInfo)
        {
            // Get version info
            mediaToolInfo = new MediaToolInfo(this) { FileName = GetToolPath() };
            Command command = Builder.Version(GetToolPath());
            return Execute(command, out BufferedCommandResult result)
                && result.ExitCode == 0
                && GetVersion(result.StandardOutput, mediaToolInfo);
        }

        public static bool GetVersion(string text, MediaToolInfo mediaToolInfo)
        {
            // Get file info
            if (File.Exists(mediaToolInfo.FileName))
            {
                FileInfo fileInfo = new(mediaToolInfo.FileName);
                mediaToolInfo.ModifiedTime = fileInfo.LastWriteTimeUtc;
                mediaToolInfo.Size = fileInfo.Length;
            }

            // "mkvmerge v51.0.0 ('I Wish') 64-bit"
            // "mkvpropedit v92.0 ('Everglow') 64-bit"

            // Parse version
            string[] lines = text.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            Match match = InstalledVersionRegex().Match(lines[0]);
            Debug.Assert(match.Success && Version.TryParse(match.Groups["version"].Value, out _));
            mediaToolInfo.Version = match.Groups["version"].Value;
            return true;
        }

        protected override bool GetLatestVersionWindows(out MediaToolInfo mediaToolInfo)
        {
            mediaToolInfo = new MediaToolInfo(this);
            try
            {
                // Download latest release file
                const string uri = "https://mkvtoolnix.download/latest-release.xml.gz";
                Log.Information(
                    "{Tool} : Reading latest version from : {Uri}",
                    GetToolFamily(),
                    uri
                );
                Stream releaseStream = Program
                    .HttpClient.GetStreamAsync(uri)
                    .GetAwaiter()
                    .GetResult();

                // Get XML from Gzip
                using GZipStream gzStream = new(releaseStream, CompressionMode.Decompress);
                using StreamReader sr = new(gzStream);
                string xml = sr.ReadToEnd();

                // Get the version number from XML
                MkvToolXmlSchema.MkvToolnixReleases mkvtools =
                    MkvToolXmlSchema.MkvToolnixReleases.FromXml(xml);
                mediaToolInfo.Version = mkvtools.LatestSource.Version;

                // Create download URL and the output fileName using the version number
                // E.g. https://mkvtoolnix.download/windows/releases/18.0.0/mkvtoolnix-64-bit-18.0.0.7z
                mediaToolInfo.FileName = $"mkvtoolnix-64-bit-{mediaToolInfo.Version}.7z";
                mediaToolInfo.Url =
                    $"https://mkvtoolnix.download/windows/releases/{mediaToolInfo.Version}/{mediaToolInfo.FileName}";
            }
            catch (Exception e)
                when (Log.Logger.LogAndHandle(e, MethodBase.GetCurrentMethod()?.Name))
            {
                return false;
            }
            return true;
        }

        public bool GetMediaProps(string fileName, out MediaProps mediaProps)
        {
            mediaProps = null;
            return GetMediaPropsJson(fileName, out string json)
                && GetMediaPropsFromJson(json, fileName, out mediaProps);
        }

        public bool GetMediaPropsJson(string fileName, out string json)
        {
            // Build command line
            json = string.Empty;
            Command command = GetBuilder()
                .GlobalOptions(options => options.NormalizeLanguageIetfExtended())
                .InputOptions(options => options.Identify(fileName))
                .OutputOptions(output => output.IdentificationFormatJson())
                .Build();

            // Execute command
            Log.Information("Getting media info : {FileName}", fileName);
            if (!Execute(command, false, true, out BufferedCommandResult result))
            {
                return false;
            }
            if (result.ExitCode != 0)
            {
                // Handle error, error reported to stdout
                // stdout is not summarized so summarize it before logging
                Log.Error(
                    "{ToolType} : Failed to to get media info : {FileName}",
                    GetToolType(),
                    fileName
                );
                Log.Error(
                    "{ToolType} : {Error}",
                    GetToolType(),
                    Summarize(result.StandardOutput).Trim()
                );
                return false;
            }
            if (result.StandardError.Length > 0)
            {
                // TODO: This probably never gets hit due to mkvmerge not using stderr
                Log.Warning(
                    "{ToolType} : Warning getting media info : {FileName}",
                    GetToolType(),
                    fileName
                );
                Log.Warning("{ToolType} : {Warning}", GetToolType(), result.StandardError.Trim());
            }

            // Get JSON from stdout
            json = result.StandardOutput;
            return true;
        }

        public static bool GetMediaPropsFromJson(
            string json,
            string fileName,
            out MediaProps mediaProps
        )
        {
            // Populate the MediaProps object from the JSON string
            mediaProps = new MediaProps(ToolType.MkvMerge, fileName);
            try
            {
                // Deserialize
                MkvToolJsonSchema.MkvMerge mkvMerge = MkvToolJsonSchema.MkvMerge.FromJson(json);
                if (mkvMerge == null || mkvMerge.Tracks.Count == 0)
                {
                    return false;
                }

                // Container type
                mediaProps.Container = mkvMerge.Container.Type;

                // Tracks
                foreach (MkvToolJsonSchema.Track track in mkvMerge.Tracks)
                {
                    // Process by track type
                    switch (track.Type.ToLowerInvariant())
                    {
                        case "video":
                            VideoProps videoProps = new(mediaProps);
                            if (videoProps.Create(track))
                            {
                                mediaProps.Video.Add(videoProps);
                            }
                            break;
                        case "audio":
                            AudioProps audioProps = new(mediaProps);
                            if (audioProps.Create(track))
                            {
                                mediaProps.Audio.Add(audioProps);
                            }
                            break;
                        case "subtitles":
                            // Some variants of DVBSUB are not supported by MkvToolNix
                            // https://gitlab.com/mbunkus/mkvtoolnix/-/issues/1648
                            // https://github.com/ietf-wg-cellar/matroska-specification/pull/77/
                            // https://gitlab.com/mbunkus/mkvtoolnix/-/issues/3258
                            // TODO: Reported fixed, to be verified
                            SubtitleProps subtitleProps = new(mediaProps);
                            if (subtitleProps.Create(track))
                            {
                                mediaProps.Subtitle.Add(subtitleProps);
                            }
                            break;
                        default:
                            Log.Warning(
                                "MkvToolJsonSchema : Unknown track type : {TrackType} : {FileName}",
                                track.Type,
                                fileName
                            );
                            break;
                    }
                }

                // Attachments
                mediaProps.Attachments = mkvMerge.Attachments.Count;

                // Chapters
                mediaProps.Chapters = mkvMerge.Chapters.Count;

                // Errors, any unsupported tracks
                mediaProps.HasErrors = mediaProps.Unsupported;

                // Unwanted tags
                mediaProps.HasTags =
                    mkvMerge.GlobalTags.Count > 0
                    || mkvMerge.TrackTags.Count > 0
                    || mediaProps.Attachments > 0
                    || !string.IsNullOrEmpty(mkvMerge.Container.Properties.Title);

                // Duration in nanoseconds
                mediaProps.Duration = TimeSpan.FromSeconds(
                    mkvMerge.Container.Properties.Duration / 1000000.0
                );
            }
            catch (Exception e)
                when (Log.Logger.LogAndHandle(e, MethodBase.GetCurrentMethod()?.Name))
            {
                return false;
            }
            return true;
        }

        public bool ReMuxToMkv(
            string inputName,
            SelectMediaProps selectMediaProps,
            string outputName,
            out string error
        )
        {
            // Delete output file
            File.Delete(outputName);

            // Build command line
            error = string.Empty;
            Command command = GetBuilder()
                .GlobalOptions(options => options.Default())
                .InputOptions(options =>
                    options.Default().SelectTracks(selectMediaProps.Selected).InputFile(inputName)
                )
                .OutputOptions(options => options.TestSnippets().OutputFile(outputName))
                .Build();

            // Execute command
            if (!Execute(command, true, true, out BufferedCommandResult result))
            {
                return false;
            }
            error = result.StandardOutput.Trim();
            return result.ExitCode is 0 or 1;
        }

        public bool ReMuxToMkv(string inputName, string outputName, out string error)
        {
            // Delete output file
            File.Delete(outputName);

            // Build command line
            error = string.Empty;
            Command command = GetBuilder()
                .GlobalOptions(options => options.Default())
                .InputOptions(options => options.Default().InputFile(inputName))
                .OutputOptions(options => options.TestSnippets().OutputFile(outputName))
                .Build();

            // Execute command
            if (!Execute(command, true, true, out BufferedCommandResult result))
            {
                return false;
            }
            error = result.StandardOutput.Trim();
            return result.ExitCode is 0 or 1;
        }

        public bool RemoveSubtitles(string inputName, string outputName, out string error)
        {
            // Delete output file
            File.Delete(outputName);

            // Build command line
            error = string.Empty;
            Command command = GetBuilder()
                .GlobalOptions(options => options.Default())
                .InputOptions(options => options.Default().NoSubtitles().InputFile(inputName))
                .OutputOptions(options => options.TestSnippets().OutputFile(outputName))
                .Build();

            // Execute command
            if (!Execute(command, true, true, out BufferedCommandResult result))
            {
                return false;
            }
            error = result.StandardOutput.Trim();
            return result.ExitCode is 0 or 1;
        }

        public bool MergeToMkv(
            string sourceOne,
            string sourceTwo,
            MediaProps keepTwo,
            string outputName,
            out string error
        )
        {
            // Merge all tracks from sourceOne with selected tracks in sourceTwo

            // Verify correct parser type
            Debug.Assert(keepTwo.Parser == ToolType.MkvMerge);

            // Delete output file
            File.Delete(outputName);

            // Build command line
            error = string.Empty;
            Command command = GetBuilder()
                .GlobalOptions(options => options.Default())
                .InputOptions(options =>
                    options
                        .Default()
                        .InputFile(sourceOne)
                        .Default()
                        .SelectTracks(keepTwo)
                        .InputFile(sourceTwo)
                )
                .OutputOptions(options => options.TestSnippets().OutputFile(outputName))
                .Build();

            // Execute command
            if (!Execute(command, true, true, out BufferedCommandResult result))
            {
                return false;
            }
            error = result.StandardOutput.Trim();
            return result.ExitCode is 0 or 1;
        }

        [GeneratedRegex(
            @"([^\s]+)\ v(?<version>.*?)\ \(",
            RegexOptions.IgnoreCase | RegexOptions.Multiline
        )]
        public static partial Regex InstalledVersionRegex();
    }
}
