using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditorInternal;
using UnityEngine;

// Offline export of a diagnostic Player capture; never runs in the measured Player.
public static class U29ProfileExport
{
    public static void Export()
    {
        var args = Environment.GetCommandLineArgs();
        var path = Arg(args, "-u29ProfilePath");
        var output = Arg(args, "-u29ProfileOutput");
        Directory.CreateDirectory(output);
        ProfilerDriver.enabled = false;
        if (!ProfilerDriver.LoadProfile(path, false)) throw new IOException("Cannot load profile: " + path);
        var totals = new Dictionary<string, Total>();
        var first = ProfilerDriver.firstFrameIndex;
        var last = ProfilerDriver.lastFrameIndex;
        if (first < 0 || last <= first) throw new InvalidOperationException("Profile has no complete frames.");
        using (var frames = new StreamWriter(Path.Combine(output, "native-frames.csv"), false, new UTF8Encoding(false)))
        {
            frames.WriteLine("frame,thread,frame_ms,samples");
            // Boundary frames may be partial because capture starts/stops inside Update.
            for (var frame = first + 1; frame < last; frame++)
            {
                for (var thread = 0; thread < 256; thread++)
                {
                    using (var view = ProfilerDriver.GetRawFrameDataView(frame, thread))
                    {
                        if (!view.valid) break;
                        var threadName = view.threadGroupName + "/" + view.threadName;
                        frames.WriteLine(frame + "," + Quote(threadName) + "," + Number(view.frameTimeMs) + "," + view.sampleCount);
                        var ancestors = new Stack<Tuple<int, string>>();
                        for (var sample = 0; sample < view.sampleCount; sample++)
                        {
                            while (ancestors.Count > 0 && ancestors.Peek().Item1 <= sample) ancestors.Pop();
                            var parent = ancestors.Count == 0 ? string.Empty : ancestors.Peek().Item2;
                            var name = view.GetSampleName(sample) ?? "<unnamed>";
                            var inclusive = view.GetSampleTimeMs(sample);
                            var childTime = 0d;
                            var child = sample + 1;
                            for (var c = 0; c < view.GetSampleChildrenCount(sample); c++)
                            {
                                childTime += view.GetSampleTimeMs(child);
                                child += 1 + view.GetSampleChildrenCountRecursive(child);
                            }
                            var exclusive = Math.Max(0d, inclusive - childTime);
                            var key = view.threadId + "\n" + parent + "\n" + name;
                            if (!totals.TryGetValue(key, out var total))
                            {
                                total = new Total { thread = threadName, marker = name, parent = parent };
                                totals.Add(key, total);
                            }
                            total.calls++;
                            total.inclusive += inclusive;
                            total.exclusive += exclusive;
                            total.maxInclusive = Math.Max(total.maxInclusive, inclusive);
                            if (view.GetSampleChildrenCount(sample) > 0)
                                ancestors.Push(Tuple.Create(sample + 1 + view.GetSampleChildrenCountRecursive(sample),
                                    parent.Length == 0 ? name : parent + "/" + name));
                        }
                    }
                }
            }
        }
        using (var writer = new StreamWriter(Path.Combine(output, "native-markers.csv"), false, new UTF8Encoding(false)))
        {
            writer.WriteLine("thread,marker,parent,calls,inclusive_ms,exclusive_ms,max_inclusive_ms,exclusive_ms_per_frame");
            foreach (var total in totals.Values.OrderByDescending(x => x.exclusive))
                writer.WriteLine(Quote(total.thread) + "," + Quote(total.marker) + "," + Quote(total.parent) + "," + total.calls + "," +
                    Number(total.inclusive) + "," + Number(total.exclusive) + "," + Number(total.maxInclusive) + "," +
                    Number(total.exclusive / (last - first - 1)));
        }
        Debug.Log("U29_PROFILE_EXPORT_COMPLETE: frames=" + (last - first - 1) + "; markers=" + totals.Count + "; output=" + output);
    }

    private static string Arg(string[] args, string key)
    {
        var index = Array.IndexOf(args, key);
        if (index < 0 || index + 1 >= args.Length) throw new ArgumentException("Missing " + key);
        return args[index + 1];
    }
    private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    private static string Quote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
    private sealed class Total
    {
        public string thread;
        public string marker;
        public string parent;
        public long calls;
        public double inclusive;
        public double exclusive;
        public double maxInclusive;
    }
}
