using System.Text;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeJiangHuTests
{
    private sealed class Files : IDisposable
    {
        private readonly string prior=LegacyNativeServerConfig.DbLocation;
        private readonly Dictionary<string,byte[]?> expected=new(StringComparer.OrdinalIgnoreCase);
        internal string Root {get;}=Path.Combine(LegacyNativeDbReadOwnedFiles.ArtifactsRoot(),"native-jianghu-"+Guid.NewGuid().ToString("N"));
        internal string PathFor(string name)=>Path.Combine(Root,name);

        internal Files()
        {
            GuardRoot();
            Assert.False(File.Exists(Root)||Directory.Exists(Root));
            Directory.CreateDirectory(Root);
            foreach(string name in new[]{"JiangHu.txt","JianghuAttributes.txt","JingHuCultivateStatus.txt"})
                expected.Add(PathFor(name),null);
            LegacyNativeServerConfig.DbLocation=Root+Path.DirectorySeparatorChar;
        }

        internal void Seed(string name,string text)
        {
            string path=PathFor(name);
            Guard(path);
            Assert.Null(expected[path]);
            Assert.False(File.Exists(path)||Directory.Exists(path));
            byte[] bytes=Encoding.UTF8.GetBytes(text);
            using(var stream=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None))
            {stream.Write(bytes);stream.Flush(true);}
            expected[path]=bytes;
        }

        internal void Expect(string name,string text)
        {
            string path=PathFor(name);
            Guard(path);
            byte[] bytes=Encoding.UTF8.GetBytes(text);
            Assert.Equal(bytes,File.ReadAllBytes(path));
            expected[path]=bytes;
        }

        private void GuardRoot()
        {
            Assert.Equal(LegacyNativeDbReadOwnedFiles.ArtifactsRoot(),Path.GetDirectoryName(Path.GetFullPath(Root)));
            Assert.Matches("^native-jianghu-[0-9a-f]{32}$",Path.GetFileName(Root));
            LegacyNativeDbReadOwnedFiles.OrdinaryChain(Root);
        }

        private void Guard(string path)
        {
            GuardRoot();
            Assert.True(expected.ContainsKey(path));
            Assert.Equal(Root,Path.GetDirectoryName(Path.GetFullPath(path)));
            LegacyNativeDbReadOwnedFiles.OrdinaryChain(path);
        }

        public void Dispose()
        {
            LegacyNativeServerConfig.DbLocation=prior;
            GuardRoot();
            foreach(string path in Directory.GetFileSystemEntries(Root))
                Assert.True(expected.ContainsKey(path)&&!Directory.Exists(path),"Unexpected files are preserved.");
            foreach(var pair in expected)
            {
                Guard(pair.Key);
                if(pair.Value is null) Assert.False(File.Exists(pair.Key)||Directory.Exists(pair.Key));
                else Assert.Equal(pair.Value,File.ReadAllBytes(pair.Key));
            }
            foreach(var pair in expected) if(pair.Value is not null) File.Delete(pair.Key);
            Directory.Delete(Root,recursive:false);
        }
    }

    private static string FileText(params string[] rows)=>"Count="+rows.Length+Environment.NewLine+
        string.Concat(rows.Select(row=>row+Environment.NewLine));
}
