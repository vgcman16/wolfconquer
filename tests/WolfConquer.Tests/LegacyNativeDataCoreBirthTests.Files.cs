using System.Text;
using WolfConquer.Core.World;

namespace WolfConquer.Tests;

public sealed partial class LegacyNativeDataCoreBirthTests
{
    private sealed class Files : IDisposable
    {
        private const string Prefix="WolfConquer-EagerPrerequisite-";
        private readonly string oldDb=LegacyNativeServerConfig.DbLocation;
        private readonly HashSet<string> paths=new(StringComparer.OrdinalIgnoreCase);
        internal string Root { get; }=Path.Combine(Path.GetTempPath(),Prefix+Guid.NewGuid().ToString("N"));
        internal string Users=>Path.Combine(Root,"Users");

        internal Files(bool users=true)
        {
            Validate();CheckChain(Path.GetTempPath());Directory.CreateDirectory(Root);if(users)Directory.CreateDirectory(Users);
            LegacyNativeServerConfig.DbLocation=Root;
        }
        internal string User(uint uid)
        {
            Validate();string path=Path.Combine(Users,uid+".ini");
            if(!Path.GetFullPath(path).StartsWith(Path.GetFullPath(Root)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new IOException("Fixture file escaped owned root.");
            CheckChain(Path.GetDirectoryName(path)!);paths.Add(path);return path;
        }
        internal LegacyNativeItemCounterShellIni Profile(uint uid)=>new(User(uid),overridePath:true);
        internal void Seed(uint uid)=>File.WriteAllText(User(uid),"; owned fixture\r\n[Other]\r\nSentinel=preserved\r\n[Character]\r\nUnrelated=keep\r\n",Encoding.Unicode);
        private void Validate()
        {
            string full=Path.GetFullPath(Root),name=Path.GetFileName(full);
            if(!StringComparer.OrdinalIgnoreCase.Equals(Path.GetDirectoryName(full),Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar))||
                !name.StartsWith(Prefix,StringComparison.Ordinal)||!Guid.TryParseExact(name[Prefix.Length..],"N",out _))throw new IOException("Invalid owned fixture root.");
        }
        private static void CheckChain(string path)
        {
            for(string? cursor=Path.GetFullPath(path);cursor is not null;cursor=Path.GetDirectoryName(cursor))
                if((Directory.Exists(cursor)||File.Exists(cursor))&&(File.GetAttributes(cursor)&FileAttributes.ReparsePoint)!=0)throw new IOException("Linked fixture path rejected.");
        }
        public void Dispose()
        {
            LegacyNativeServerConfig.DbLocation=oldDb;Validate();CheckChain(Root);
            if(Directory.GetFileSystemEntries(Root).Any(path=>!StringComparer.OrdinalIgnoreCase.Equals(path,Users)))throw new IOException("Unexpected fixture entry preserved.");
            if(Directory.Exists(Users))CheckChain(Users);
            if(Directory.Exists(Users)&&Directory.GetFileSystemEntries(Users).Any(path=>!paths.Contains(path)||(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0))throw new IOException("Unexpected user fixture entry preserved.");
            foreach(string path in paths)if(File.Exists(path)){CheckChain(Path.GetDirectoryName(path)!);File.Delete(path);}
            if(Directory.Exists(Users))Directory.Delete(Users,recursive:false);Directory.Delete(Root,recursive:false);
        }
    }

    private static void Character(Scope scope,string name="OwnedWolf",uint uid=17)
    {
        scope.Player.UID=uid;scope.Player.Name=name;scope.Player.Body=1008;scope.Player.Face=154;
        SetClass(scope.Player,7000);scope.Player.Map=1002;scope.Player.X=410;scope.Player.Y=354;
        typeof(LegacyNativePlayerRuntime).GetField("_level",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)!.SetValue(scope.Player,(ushort)137);
    }
}
