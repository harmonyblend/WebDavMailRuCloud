using System;
using System.Linq;
using System.Security.Authentication;
using System.Text.RegularExpressions;
using YaR.Clouds.Base.Repos.MailRuCloud;

namespace YaR.Clouds.Base
{
    public class Credentials : IBasicCredentials
    {
        private static readonly string[] AnonymousLogins = { "anonymous", "anon", "anonym", string.Empty };

        // протокол # логин # разделитель
        private static Regex _loginRegex1 = new Regex("^([^#]*)#([^#]*)#([^#]*)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        // протокол # логин
        private static Regex _loginRegex2 = new Regex(
            "^(1|2|WebM1Bin|WebV2|YadWeb|YadWebV2)#([^#]*)$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        // логин # разделитель
        private static Regex _loginRegex3 = new Regex("^([^#]*)#([^#]*)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public Credentials(string login, string password)
        {
            if (string.IsNullOrWhiteSpace(login))
                login = string.Empty;

            if (AnonymousLogins.Contains(login))
            {
                IsAnonymous = true;
                Login = login;
                Password = string.Empty;
                PasswordCrypt = string.Empty;
                CloudType = StringToCloud(Login);
                Protocol = Protocol.Autodetect;

                return;
            }

            /*
             * Login ожидается в форматах:
             * John     - здесь John - учетная запись в облаке без указания mail или yandex
             * John#SEP - здесь SEP - это разделитель для строки пароля
             * John@yandex.com - здесь John@yandex.com - учетная запись в облаке с указанием mail или yandex
             * John@yandex.com#SEP - указано облако и разделитель для строки пароля
             * Выше в форматах не задан протокол, поэтому принимается значение Autodetect
             * 1#John - учетная запись без указания mail или yandex с указанием версии протокола - не допустимо
             * WebM1Bin#John - учетная запись без указания mail или yandex с указанием версии протокола
             * WebM1Bin#John#SEP - учетная запись без указания mail или yandex с указанием версии протокола и разделителя
             * 1#John@mail.ru - учетная запись с указанием mail или yandex с указанием версии протокола
             * 1#John@mail.ru#SEP - учетная запись с указанием mail или yandex с указанием версии протокола и разделителя строки пароля
             * WebM1Bin#John@yandex.ru - учетная запись облака yandex с указанием несовместимой версии протокола - не допустимо
             */

            // протокол # логин # разделитель
            Match m = _loginRegex1.Match(login);
            if (m.Success)
            {
                if (string.IsNullOrEmpty(m.Groups[1].Value))
                {
                    throw new InvalidCredentialException("Invalid credential format: " +
                        "login doesn't have protocol part. See manuals.");
                }
                if (string.IsNullOrEmpty(m.Groups[2].Value))
                {
                    throw new InvalidCredentialException("Invalid credential format: " +
                        "login doesn't have email part. See manuals.");
                }
                if (string.IsNullOrEmpty(m.Groups[3].Value))
                {
                    throw new InvalidCredentialException("Invalid credential format: " +
                        "login doesn't have encryption part. See manuals.");
                }

                (Login, Password, PasswordCrypt) = StringToLoginPassword(m.Groups[2].Value, m.Groups[3].Value, password);
                CloudType = StringToCloud(Login);
                Protocol = StringToProtocol(m.Groups[1].Value, CloudType);
            }
            else
            {
                // протокол # логин
                m = _loginRegex2.Match(login);
                if (m.Success)
                {
                    if (string.IsNullOrEmpty(m.Groups[1].Value))
                    {
                        throw new InvalidCredentialException("Invalid credential format: " +
                            "login doesn't have protocol part. See manuals.");
                    }
                    if (string.IsNullOrEmpty(m.Groups[2].Value))
                    {
                        throw new InvalidCredentialException("Invalid credential format: " +
                            "login doesn't have email part. See manuals.");
                    }

                    (Login, Password, PasswordCrypt) = StringToLoginPassword(m.Groups[2].Value, null, password);
                    CloudType = StringToCloud(Login);
                    Protocol = StringToProtocol(m.Groups[1].Value, CloudType);
                }
                else
                {
                    // логин # разделитель
                    m = _loginRegex3.Match(login);
                    if (m.Success)
                    {
                        if (string.IsNullOrEmpty(m.Groups[1].Value))
                        {
                            throw new InvalidCredentialException("Invalid credential format: " +
                                "login doesn't have email part. See manuals.");
                        }
                        if (string.IsNullOrEmpty(m.Groups[2].Value))
                        {
                            throw new InvalidCredentialException("Invalid credential format: " +
                                "login doesn't have encryption part. See manuals.");
                        }

                        (Login, Password, PasswordCrypt) = StringToLoginPassword(m.Groups[1].Value, m.Groups[2].Value, password);
                        CloudType = StringToCloud(Login);
                        Protocol = CloudType == CloudType.Mail
                            // Т.к. протокол WebV2 отмечен как deprecated, всегда автоматически выбирается WebM1Bin
                            ? Protocol = Protocol.WebM1Bin
                            : CloudType == CloudType.Yandex && string.IsNullOrWhiteSpace(Password)
                              ? Protocol.YadWebV2
                              : Protocol.Autodetect;
                    }
                    else
                    {
                        (Login, Password, PasswordCrypt) = StringToLoginPassword(login, null, password);
                        CloudType = StringToCloud(Login);
                        Protocol = Protocol.Autodetect;
                    }
                }
            }
        }

        public bool IsAnonymous { get; set; }

        public Protocol Protocol { get; set; } = Protocol.Autodetect;
        public CloudType CloudType { get; set; }

        public string Login { get; }
        public string Password { get; }

        public string PasswordCrypt { get; set; }

        public bool CanCrypt => !string.IsNullOrEmpty(PasswordCrypt);

        private CloudType StringToCloud(string login)
        {
            foreach (var domain in MailRuBaseRepo.AvailDomains)
            {
#if NET48
                bool hasMail = System.Globalization.CultureInfo.CurrentCulture.CompareInfo
                    .IndexOf(login, string.Concat("@", domain, "."), System.Globalization.CompareOptions.OrdinalIgnoreCase) >= 0;
#else
                bool hasMail = login.Contains(string.Concat("@", domain, "."), StringComparison.InvariantCultureIgnoreCase);
#endif
                if (hasMail)
                    return CloudType.Mail;
            }

#if NET48
            bool hasYandex = System.Globalization.CultureInfo.CurrentCulture.CompareInfo
                .IndexOf(login, "@yandex.", System.Globalization.CompareOptions.OrdinalIgnoreCase) >= 0;
#else
            bool hasYandex = login.Contains("@yandex.", StringComparison.InvariantCultureIgnoreCase);
#endif
            if (hasYandex)
                return CloudType.Yandex;

            return CloudType.Unkown;
        }

        private Protocol StringToProtocol(string protocol, CloudType cloud)
        {
            switch (protocol)
            {
            case "1":
                if (cloud == CloudType.Mail)
                    return Protocol.WebM1Bin;
                if (cloud == CloudType.Yandex)
                    return Protocol.YadWeb;
                break;

            case "2":
                if (cloud == CloudType.Mail)
                    return Protocol.WebV2;
                if (cloud == CloudType.Yandex)
                    return Protocol.YadWebV2;
                break;

            case "WebM1Bin":
                if (cloud == CloudType.Mail)
                    return Protocol.WebM1Bin;
                if (cloud == CloudType.Yandex)
                    throw new InvalidCredentialException("Invalid credential format: " +
                        "protocol version isn't compatible with cloud specified by login. See manuals.");
                break;

            case "WebV2":
                if (cloud == CloudType.Mail)
                    return Protocol.WebV2;
                if (cloud == CloudType.Yandex)
                    throw new InvalidCredentialException("Invalid credential format: " +
                        "protocol version isn't compatible with cloud specified by login. See manuals.");
                break;

            case "YadWeb":
                if (cloud == CloudType.Yandex)
                    return Protocol.YadWeb;
                if (cloud == CloudType.Mail)
                    throw new InvalidCredentialException("Invalid credential format: " +
                        "protocol version isn't compatible with cloud specified by login. See manuals.");
                break;

            case "YadWebV2":
                if (cloud == CloudType.Yandex)
                    return Protocol.YadWebV2;
                if (cloud == CloudType.Mail)
                    throw new InvalidCredentialException("Invalid credential format: " +
                        "protocol version isn't compatible with cloud specified by login. See manuals.");
                break;

            default:
                throw new InvalidCredentialException("Invalid credential format: " +
                    "unknown protocol. See manuals.");
            };

            throw new InvalidCredentialException("Invalid credential format: " +
                "protocol version needs fully qualified login using email format. See manuals.");
        }

        private (string login, string password, string encPassword) StringToLoginPassword(
            string loginPart, string separatorPart, string passwordPart)
        {
            if (string.IsNullOrEmpty(separatorPart))
                return (loginPart, passwordPart, string.Empty);

            int sepPos = passwordPart.IndexOf(separatorPart, StringComparison.InvariantCulture /* case sensitive! */);
            if (sepPos < 0)
                throw new InvalidCredentialException("Invalid credential format: " +
                    "password doesn't contain encryption part. See manuals.");

            string password = passwordPart.Substring(0, sepPos);
            if (sepPos + separatorPart.Length >= passwordPart.Length)
                throw new InvalidCredentialException("Invalid credential format.");

            string passwordCrypt = passwordPart.Substring(sepPos + separatorPart.Length);

            return (loginPart, password, passwordCrypt);
        }
    }
}
