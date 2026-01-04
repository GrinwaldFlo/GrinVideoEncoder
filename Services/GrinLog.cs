namespace GrinVideoEncoder.Services;

public class LogMain(IAppSettings settings) : GrinLogBase(settings.LogPath, "GrinVideoEncoder")
{
}

public class LogFfmpeg(IAppSettings settings) : GrinLogBase(settings.LogPath, "FFmpeg")
{
}

