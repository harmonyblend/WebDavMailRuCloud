namespace YaR.Clouds.Base;

public enum Protocol
{
    Autodetect = 0,
    /// <summary>
    /// <para>(Cloud.Mail.Ru) mix of mobile and DiskO protocols</para>
    /// </summary>
    WebM1Bin,
    /// <summary>
    /// <para>(Cloud.Mail.Ru) [deprecated] desktop browser protocol</para>
    /// </summary>
    WebV2,
    /// <summary>
    /// <para>(Yandex.Disk) desktop browser protocol</para>
    /// <para>Протокол работает в двух вариантах:</para>
    /// <list type="bullet">
    /// <item>С аутентификацией через логин и пароль
    /// - это исходный вариант протокола YadWeb;</item>
    /// <item>С аутентификацией через браузер и с использованием его куки
    /// - это модифицированный вариант протокола, который раньше был отдельным протоколом YadWebV2.</item>
    /// </list>
    /// </summary>
    YadWeb
}
