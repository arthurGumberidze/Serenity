using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Game.Domain;
using Game.Domain.Time;
using Game.Simulation;

namespace Game.Infrastructure
{
    /// <summary>Strict version-1 UTF-8 XML; integer values retain all 64 bits. No external dependencies.</summary>
    public sealed class SaveXmlCodec
    {
        private static readonly string[] Fields = { "version", "sessionId", "calendarTicks", "biologicalTicks", "speed", "paused", "biologicalMultiplier" };
        public byte[] Serialize(SaveSnapshot snapshot)
        {
            SaveCoordinator.CreateClock(snapshot);
            var root = new XElement("serenitySave",
                new XElement("version", snapshot.FormatVersion), new XElement("sessionId", snapshot.SessionId.ToString()),
                new XElement("calendarTicks", snapshot.CalendarTicks), new XElement("biologicalTicks", snapshot.BiologicalTicks),
                new XElement("speed", snapshot.SpeedMultiplier), new XElement("paused", snapshot.IsPaused),
                new XElement("biologicalMultiplier", snapshot.BiologicalMultiplier));
            using (var stream = new MemoryStream())
            {
                new XDocument(root).Save(stream);
                return stream.ToArray();
            }
        }
        public SaveSnapshot Deserialize(byte[] data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            try
            {
                using (var stream = new MemoryStream(data, false))
                using (var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 65536 }))
                {
                    var root = XDocument.Load(reader).Root;
                    if (root == null || root.Name != "serenitySave" || root.HasAttributes) throw new FormatException("Invalid save root.");
                    var children = root.Elements().ToArray();
                    if (children.Length != Fields.Length || Fields.Any(f => children.Count(e => e.Name == f) != 1)
                        || children.Any(e => e.HasAttributes || e.HasElements)
                        || root.Nodes().OfType<XText>().Any(t => !string.IsNullOrWhiteSpace(t.Value)))
                        throw new FormatException("Invalid save fields.");
                    var version = XmlConvert.ToInt32(root.Element("version").Value);
                    if (version != SaveSnapshot.CurrentVersion) throw new NotSupportedException("Unsupported save format version: " + version);
                    var snapshot = new SaveSnapshot(version, StableEntityId.Parse(root.Element("sessionId").Value),
                        new GameTimeState(XmlConvert.ToInt64(root.Element("calendarTicks").Value), XmlConvert.ToInt64(root.Element("biologicalTicks").Value),
                            XmlConvert.ToInt32(root.Element("speed").Value), XmlConvert.ToBoolean(root.Element("paused").Value)),
                        XmlConvert.ToInt32(root.Element("biologicalMultiplier").Value));
                    SaveCoordinator.CreateClock(snapshot);
                    return snapshot;
                }
            }
            catch (Exception ex) when (ex is XmlException || ex is FormatException || ex is ArgumentException || ex is OverflowException)
            {
                throw new InvalidDataException("Corrupted save data.", ex);
            }
        }
    }
}
