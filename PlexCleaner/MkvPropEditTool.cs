using System;
using System.Diagnostics;
using System.Linq;
using CliWrap;
using CliWrap.Buffered;

// https://mkvtoolnix.download/doc/mkvpropedit.html

// mkvpropedit [options] {source-filename} {actions}

// Use @ designation for track number from matroska header as discovered with mkvmerge identify

namespace PlexCleaner;

public partial class MkvPropEdit
{
    public class Tool : MediaTool
    {
        public override ToolFamily GetToolFamily() => ToolFamily.MkvToolNix;

        public override ToolType GetToolType() => ToolType.MkvPropEdit;

        protected override string GetToolNameWindows() => "mkvpropedit.exe";

        protected override string GetToolNameLinux() => "mkvpropedit";

        public IGlobalOptions GetBuilder() => Builder.Create(GetToolPath());

        public override bool GetInstalledVersion(out MediaToolInfo mediaToolInfo)
        {
            // Get version info
            mediaToolInfo = new MediaToolInfo(this) { FileName = GetToolPath() };
            Command command = Builder.Version(GetToolPath());
            return Execute(command, out BufferedCommandResult result)
                && result.ExitCode == 0
                && MkvMerge.Tool.GetVersion(result.StandardOutput, mediaToolInfo);
        }

        protected override bool GetLatestVersionWindows(out MediaToolInfo mediaToolInfo) =>
            throw new NotImplementedException();

        public bool SetTrackLanguage(string fileName, MediaProps mediaProps)
        {
            // Build command line
            Command command = GetBuilder()
                .GlobalOptions(options => options.Default())
                .InputOptions(options =>
                    options
                        .InputFile(fileName)
                        .Default()
                        .Add(options =>
                        {
                            // Set track language if defined
                            Debug.Assert(mediaProps.Parser == ToolType.MkvMerge);
                            mediaProps
                                .GetTrackList()
                                .Where(item => !Language.IsUndefined(item.LanguageAny))
                                .ToList()
                                .ForEach(item =>
                                    options.EditTrack(item.Number).SetLanguage(item.LanguageAny)
                                );
                            return options;
                        })
                )
                .Build();

            // Execute command
            return Execute(command, out CommandResult result) && result.ExitCode is 0;
        }

        public bool SetTrackFlags(string fileName, MediaProps mediaProps)
        {
            // Build command line
            Command command = GetBuilder()
                .GlobalOptions(options => options.Default())
                .InputOptions(options =>
                    options
                        .InputFile(fileName)
                        .Default()
                        .Add(options =>
                        {
                            // Set all flags for this track
                            Debug.Assert(mediaProps.Parser == ToolType.MkvMerge);
                            mediaProps
                                .GetTrackList()
                                .Where(item => item.Flags != TrackProps.FlagsType.None)
                                .ToList()
                                .ForEach(item =>
                                    options.EditTrack(item.Number).SetFlags(item.Flags)
                                );

                            return options;
                        })
                )
                .Build();

            // Execute command
            return Execute(command, out CommandResult result) && result.ExitCode is 0;
        }

        public bool ClearTags(string fileName, MediaProps mediaProps)
        {
            // Build command line
            Command command = GetBuilder()
                .GlobalOptions(options => options.Default())
                .InputOptions(options =>
                    options
                        .InputFile(fileName)
                        .Default()
                        // Delete all tags and title
                        .Tags()
                        .Add("all:")
                        .Delete()
                        .Add("title")
                        .Add(options =>
                        {
                            // Delete track titles if the title is not used as a flag
                            Debug.Assert(mediaProps.Parser == ToolType.MkvMerge);
                            mediaProps
                                .GetTrackList()
                                .Where(item => !item.TitleContainsFlag())
                                .ToList()
                                .ForEach(item =>
                                    options.EditTrack(item.Number).Delete().Add("name")
                                );

                            return options;
                        })
                )
                .Build();

            // Execute command
            return Execute(command, out CommandResult result) && result.ExitCode is 0;
        }

        public bool ClearAttachments(string fileName, MediaProps mediaProps)
        {
            // Build command line
            Command command = GetBuilder()
                .GlobalOptions(options => options.Default())
                .InputOptions(options =>
                    options
                        .InputFile(fileName)
                        .Default()
                        .Add(options =>
                        {
                            // Delete all attachments
                            Debug.Assert(mediaProps.Parser == ToolType.MkvMerge);
                            for (int i = 0; i < mediaProps.Attachments; i++)
                            {
                                _ = options.DeleteAttachment(i);
                            }

                            return options;
                        })
                )
                .Build();

            // Execute command
            return Execute(command, out CommandResult result) && result.ExitCode is 0;
        }
    }
}
