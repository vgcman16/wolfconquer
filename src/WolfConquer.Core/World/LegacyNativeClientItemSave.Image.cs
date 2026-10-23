using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace WolfConquer.Core.World;

internal static partial class LegacyNativeClientItemSave
{
    private sealed partial class SaveGraph
    {
        internal byte[] PrepareImage()
        {
            var reused = default(LegacyNativeClientItemRecord);
            int recordCount = 0;
            var mainPerfections = new List<SavedItem>();
            var depositedPerfections = new List<SavedItem>();
            foreach (var root in roots)
            {
                root.State.Prepare(ref reused); recordCount = checked(recordCount + 1);
                if (root.MainPerfection && root.State.HasPerfection && root.State.HasPerfectionOwner && root.State.IsEquipment)
                    mainPerfections.Add(root.State);
                foreach (var child in root.Children)
                {
                    child.Prepare(ref reused); recordCount = checked(recordCount + 1);
                    if (child.HasPerfection && child.IsEquipment) depositedPerfections.Add(child);
                }
            }
            int perfectionCount = checked(mainPerfections.Count + depositedPerfections.Count);
            int size = checked(8 + checked(recordCount * Unsafe.SizeOf<LegacyNativeClientItemRecord>()) +
                checked(perfectionCount * Unsafe.SizeOf<LegacyNativeClientItemPerfectionRecord>()));
            if (size > MaximumImageBytes) throw InvalidGraph("oversizedImage");
            using var image = new MemoryStream(size);
            WriteCount(image, roots.Count);
            foreach (var root in roots)
            {
                root.State.WriteRecord(image);
                foreach (var child in root.Children) child.WriteRecord(image);
            }
            WriteCount(image, perfectionCount);
            foreach (var item in mainPerfections) item.WritePerfection(image);
            foreach (var item in depositedPerfections) item.WritePerfection(image);
            if (image.Length != size || !IsCurrent()) throw InvalidGraph("imageInputsChanged");
            return image.ToArray();
        }
    }

    private static void WriteCount(Stream image, int count)
    {
        Span<byte> bytes = stackalloc byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, count);
        image.Write(bytes);
    }
}
