import json
import re
import subprocess

SID = open('sid.txt').read().strip()

CODE = r"""
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
public class Script
{
    static readonly string[] Interesting = { "RoadSplines", "CityHub" };

    public static string Main()
    {
        var t = Type.GetType("UnityEditorInternal.ProfilerDriver, UnityEditor.CoreModule")
             ?? Type.GetType("UnityEditorInternal.ProfilerDriver, UnityEditor");
        var flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        var getView = t.GetMethod("GetHierarchyFrameDataView", flags);
        var firstProp = t.GetProperty("firstFrameIndex", flags);
        var lastProp = t.GetProperty("lastFrameIndex", flags);
        int first = (int)firstProp.GetValue(null);
        int last = (int)lastProp.GetValue(null);
        var sb = new StringBuilder();
        sb.Append("[PCR] frames ").Append(first).Append("..").Append(last).AppendLine();

        var viewType = Type.GetType("UnityEditor.Profiling.HierarchyFrameDataView, UnityEditor.CoreModule")
                    ?? Type.GetType("UnityEditor.Profiling.HierarchyFrameDataView, UnityEditor");
        var getRoot = viewType.GetMethod("GetRootItemID", Type.EmptyTypes);
        var getChildren = viewType.GetMethod("GetItemChildren", new Type[] { typeof(int), typeof(List<int>) });
        var getName = viewType.GetMethod("GetItemName", new Type[] { typeof(int) });
        var getCol = viewType.GetMethod("GetItemColumnDataAsSingle", new Type[] { typeof(int), typeof(int) });

        // Find threads containing our markers (sampled scan)
        var hitThreads = new List<int>();
        for (int th = 0; th < 10; th++)
        {
            bool anyHit = false;
            for (int f = first; f <= last; f += 50)
            {
                object view = null;
                try { view = getView.Invoke(null, new object[] { f, th, 0, 0, false }); }
                catch { continue; }
                if (view == null) continue;
                int root = (int)getRoot.Invoke(view, null);
                if (TreeContains(view, root, getChildren, getName, Interesting)) { anyHit = true; break; }
            }
            if (anyHit) { hitThreads.Add(th); sb.Append("[PCR] thread ").Append(th).Append(" has markers").AppendLine(); }
        }
        if (hitThreads.Count == 0)
        {
            sb.Append("[PCR] NO markers found in any thread 0..9").AppendLine();
            return sb.ToString();
        }

        // Aggregate per-marker TotalTime (col 0) across all frames, all hit threads
        var totals = new Dictionary<string, double>();
        var calls = new Dictionary<string, int>();
        int scanned = 0;
        for (int f = first; f <= last; f++)
        {
            foreach (var th in hitThreads)
            {
                object view = null;
                try { view = getView.Invoke(null, new object[] { f, th, 0, 0, false }); }
                catch { continue; }
                if (view == null) continue;
                scanned++;
                int root = (int)getRoot.Invoke(view, null);
                Accumulate(view, root, getChildren, getName, getCol, totals, calls);
            }
        }
        sb.Append("[PCR] scanned ").Append(scanned).Append(" frame-views").AppendLine();
        var keys = new List<string>(totals.Keys);
        keys.Sort((a, b) => totals[b].CompareTo(totals[a]));
        foreach (var k in keys)
            sb.Append("[PCR-TOTAL] ").Append(k).Append(" => ").Append(totals[k].ToString("F1"))
              .Append("ms (").Append(calls[k]).Append(" frames)").AppendLine();
        return sb.ToString();
    }

    static bool TreeContains(object view, int id, MethodInfo getChildren, MethodInfo getName, string[] needles)
    {
        string n = (string)getName.Invoke(view, new object[] { id });
        if (n != null)
            foreach (var s in needles)
                if (n.Contains(s)) return true;
        var kids = new List<int>();
        try { getChildren.Invoke(view, new object[] { id, kids }); } catch { return false; }
        foreach (var c in kids)
            if (TreeContains(view, c, getChildren, getName, needles)) return true;
        return false;
    }

    static void Accumulate(object view, int id, MethodInfo getChildren, MethodInfo getName, MethodInfo getCol,
                           Dictionary<string, double> totals, Dictionary<string, int> calls)
    {
        string n = (string)getName.Invoke(view, new object[] { id });
        if (n != null)
        {
            foreach (var s in Interesting)
            {
                if (!n.Contains(s)) continue;
                float ms = 0f;
                try { ms = (float)getCol.Invoke(view, new object[] { id, 0 }); } catch { }
                double acc = 0;
                totals.TryGetValue(n, out acc);
                totals[n] = acc + ms;
                int cc = 0;
                calls.TryGetValue(n, out cc);
                calls[n] = cc + 1;
                break;
            }
        }
        var kids = new List<int>();
        try { getChildren.Invoke(view, new object[] { id, kids }); } catch { return; }
        foreach (var c in kids)
            Accumulate(view, c, getChildren, getName, getCol, totals, calls);
    }
}
"""

payload = {
    "jsonrpc": "2.0", "id": 25, "method": "tools/call",
    "params": {"name": "script-execute", "arguments": {
        "csharpCode": CODE, "className": "Script", "methodName": "Main", "isMethodBody": False}}
}
out = subprocess.run(
    ["curl", "-s", "-m", "600", "-X", "POST", "http://localhost:24566/p/0cc3a677",
     "-H", "Content-Type: application/json", "-H", "Accept: application/json, text/event-stream",
     "-H", "Mcp-Session-Id: " + SID, "-d", json.dumps(payload)],
    capture_output=True, text=True).stdout
m = re.search(r'data: (\{.*\})', out, re.S)
if not m:
    print("RAW:", out[:500])
    raise SystemExit
d = json.loads(m.group(1))
for it in d.get('result', {}).get('content', []):
    txt = it.get('text', '')
    try:
        inner = json.loads(txt)
        print(inner['result']['value'])
    except Exception:
        print(txt[:3000])
