using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YaR.MailRuCloud.Api.Base.Requests;
using YaR.MailRuCloud.Api.Extensions;

namespace YaR.MailRuCloud.Api.SpecialCommands
{
    public class SharedFolderLinkCommand : SpecialCommand
    {
        private readonly MailRuCloud _cloud;
        private readonly string _path;
        private readonly string _param;

        public SharedFolderLinkCommand(MailRuCloud cloud, string path, string param)
        {
            _cloud = cloud;
            _path = path;
            _param = param;
        }

        public override Task<SpecialCommandResult> Execute()
        {
            var m = Regex.Match(_param, @"(?snx-)link \s+ (https://?cloud.mail.ru/public)?(?<url>/\w*/\w*)/? \s* (?<name>.*) ");

            var info = new ItemInfoRequest(_cloud.CloudApi, m.Groups["url"].Value, true).MakeRequestAsync().Result.ToEntry();

            bool isFile = info.IsFile;
            long size = info.Size;


            string name = m.Groups["name"].Value;
            if (string.IsNullOrWhiteSpace(name)) name = info.Name;

            if (m.Success)
            {
                _cloud.LinkItem(m.Groups["url"].Value, _path, name, isFile, size, info.CreationDate);
            }

            return Task.FromResult(new SpecialCommandResult{Success = true});
        }
    }
}