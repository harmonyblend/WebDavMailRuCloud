namespace YaR.Clouds.Base;

public enum Protocol
{
    Autodetect = 0,
    /// <summary>
    /// (Cloud.Mail.Ru) mix of mobile and DiskO protocols
    /// </summary>
    WebM1Bin,
    /// <summary>
    /// (Cloud.Mail.Ru) [deprecated] desktop browser protocol
    /// </summary>
    WebV2,
    /// <summary>
    /// (Yandex.Disk) desktop browser protocol
    /// </summary>
    YadWeb,
    /// <summary>
    /// (Yandex.Disk) desktop browser protocol with browser authentication
    /// </summary>
    YadWebV2
}
