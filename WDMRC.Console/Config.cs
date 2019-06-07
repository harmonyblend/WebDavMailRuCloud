﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using YaR.MailRuCloud.Api.Common;
using YaR.MailRuCloud.Api.Extensions;
using YaR.WebDavMailRu.CloudStore;

namespace YaR.CloudMailRu.Console
{
    internal static class Config
    {
        static Config()
        {
            Document = new XmlDocument();
            var configpath = Path.Combine(Path.GetDirectoryName(typeof(Config).Assembly.Location), "wdmrc.config");
            Document.Load(File.OpenRead(configpath));
        }

        private static readonly XmlDocument Document;

        public static XmlElement Log4Net => (XmlElement)Document.SelectSingleNode("/config/log4net");

        public static TwoFactorAuthHandlerInfo TwoFactorAuthHandler
        {
            get
            {
                var info = new TwoFactorAuthHandlerInfo();

                var node = Document.SelectSingleNode("/config/TwoFactorAuthHandler");
                info.Name = node.Attributes["Name"].InnerText;
                var parames = new List<KeyValuePair<string, string>>();
                foreach (XmlNode childNode in node.ChildNodes)
                {
                    string pname = childNode.Attributes["Name"].InnerText;
                    string pvalue = childNode.Attributes["Value"].InnerText;
                    parames.Add(new KeyValuePair<string, string>(pname, pvalue));
                }

                info.Parames = parames;


                return info;
            }
        }

        public static string SpecialCommandPrefix => ">>";

        public static string AdditionalSpecialCommandPrefix
        {
            get
            {
                try
                {
                    var res = Document.SelectSingleNode("/config/AdditionalSpecialCommandPrefix").InnerText;
                    return res;
                }
                catch (Exception)
                {
                    return null;
                }

            }
        }

        public static SharedVideoResolution DefaultSharedVideoResolution
        {
            get
            {
                try
                {
                    string evalue = Document.SelectSingleNode("/config/DefaultSharedVideoResolution").InnerText;
                    var res = EnumExtensions.ParseEnumMemberValue<SharedVideoResolution>(evalue);
                    return (SharedVideoResolution)res;
                }
                catch (Exception)
                {
                    return SharedVideoResolution.All;
                }
            }
        }
    }
}