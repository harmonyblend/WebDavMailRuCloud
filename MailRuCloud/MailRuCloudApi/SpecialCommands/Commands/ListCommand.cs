using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YaR.Clouds.Base;
using YaR.Clouds.Base.Repos;
using YaR.Clouds.Links;

namespace YaR.Clouds.SpecialCommands.Commands
{
    public class ListCommand : SpecialCommand
    {
        private const string FileListExtention = ".wdmrc.list.lst";

        //private static readonly log4net.ILog Logger = log4net.LogManager.GetLogger(typeof(FishCommand));

        public ListCommand(Cloud cloud, string path, IList<string> parameters) : base(cloud, path, parameters)
        {
        }

        protected override MinMax<int> MinMaxParamsCount { get; } = new(0, 1);

        public override async Task<SpecialCommandResult> Execute()
        {
            string target = Parames.Count > 0 && !string.IsNullOrWhiteSpace(Parames[0])
                ? Parames[0].StartsWith(WebDavPath.Separator) ? Parames[0] : WebDavPath.Combine(Path, Parames[0])
                : Path;

            var resolvedTarget = await RemotePath.Get(target, Cloud.LinkManager);

            var entry = await Cloud.RequestRepo.FolderInfo(resolvedTarget);
            string resFilepath = WebDavPath.Combine(Path, string.Concat(entry.Name, FileListExtention));

            var sb = new StringBuilder();

            foreach (var e in Flat(entry, Cloud.LinkManager))
            {
                string hash = (e as File)?.Hash.ToString() ?? "-";
                string link = e.PublicLinks.Values.FirstOrDefault()?.Uri.OriginalString ?? "-";
                sb.AppendLine(
                    $"{e.FullPath}\t{e.Size.DefaultValue}\t{e.CreationTimeUtc:yyyy.MM.dd HH:mm:ss}\t{hash}\t{link}");
            }

            Cloud.UploadFile(resFilepath, sb.ToString());

            return SpecialCommandResult.Success;
        }

        private IEnumerable<IEntry> Flat(IEntry entry, LinkManager lm)
        {
            yield return entry;

            var ifolders = entry.Descendants
                .AsParallel()
                .WithDegreeOfParallelism(5)
                .Select(it => it switch
                {
                    File => it,
                    Folder ifolder => ifolder.IsChildrenLoaded
                        ? ifolder
                        : Cloud.RequestRepo.FolderInfo(RemotePath.Get(it.FullPath, lm).Result, depth: 3).Result,
                    _ => throw new NotImplementedException("Unknown item type")
                })
                .OrderBy(it => it.Name);

            foreach (var item in ifolders.SelectMany(f => Flat(f, lm)))
                yield return item;
        }
    }
}
