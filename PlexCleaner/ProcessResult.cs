namespace PlexCleaner;

public class ProcessResult
{
    public bool Result { get; set; }
    public string OriginalFileName { get; set; }
    public string NewFileName { get; set; }
    public bool Modified { get; set; }
    public SidecarFile.StatesType State { get; set; }
}
