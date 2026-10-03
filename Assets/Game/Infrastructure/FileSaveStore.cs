using System;
using System.IO;
using Game.Domain;
using Game.Simulation;

namespace Game.Infrastructure
{
    /// <summary>One writer per directory. Same-directory temporary file + atomic rename/replace; never delete the old save first.</summary>
    public sealed class FileSaveStore : ISaveStore
    {
        private readonly string directory;
        private readonly SaveXmlCodec codec = new SaveXmlCodec();
        public FileSaveStore(string directory) { this.directory = Path.GetFullPath(directory ?? throw new ArgumentNullException(nameof(directory))); }
        private string SavePath(StableEntityId slotId)
        {
            if (!slotId.IsValid) throw new ArgumentException("Invalid slot ID.", nameof(slotId));
            return Path.Combine(directory, slotId + ".xml");
        }
        public void Save(StableEntityId slotId, SaveSnapshot snapshot)
        {
            var destination = SavePath(slotId);
            var bytes = codec.Serialize(snapshot);
            codec.Deserialize(bytes);
            Directory.CreateDirectory(directory);
            var temporary = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                if (File.Exists(destination)) File.Replace(temporary, destination, null);
                else File.Move(temporary, destination);
            }
            finally
            {
                // Cleanup must not mask the original write/replace error.
                try { if (File.Exists(temporary)) File.Delete(temporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
        public SaveSnapshot Load(StableEntityId slotId)
        {
            using (var stream = new FileStream(SavePath(slotId), FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                if (stream.Length > 65536) throw new InvalidDataException("Save exceeds version-1 size limit.");
                using (var buffer = new MemoryStream()) { stream.CopyTo(buffer); return codec.Deserialize(buffer.ToArray()); }
            }
        }
    }
}
