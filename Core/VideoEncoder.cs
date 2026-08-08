using NReco.VideoConverter;

namespace ImportCanonPhotos.Core;

/// <summary>
/// Re-encodes MP4s with ffmpeg at default settings, which makes them roughly ten times
/// smaller. The full-size original is kept, as in the original script.
/// </summary>
public sealed class VideoEncoder
{
    private readonly FFMpegConverter _converter;

    public VideoEncoder()
    {
        // NReco.VideoConverter carries ffmpeg.exe inside the assembly and unpacks it at
        // run time. Point that at %LOCALAPPDATA% so it never tries to write to the
        // Program Files install directory.
        Directory.CreateDirectory(AppPaths.FfmpegFolder);
        _converter = new FFMpegConverter { FFMpegToolPath = AppPaths.FfmpegFolder };
    }

    public void Encode(string sourceFile, string destinationFile) =>
        _converter.ConvertMedia(sourceFile, destinationFile, Format.mp4);
}
