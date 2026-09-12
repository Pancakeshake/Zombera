import json
import re
import subprocess

SID = open('sid.txt').read().strip()

CODE = r"""
using System;
using System.Collections;
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
        if (t == null) return "[PCR] ProfilerDriver type not found";
        var flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        var getView = t.GetMethod("GetHierarchyFrameDataView", flags);
        var firstProp = t.GetProperty("firstFrameIndex", flags);
        var lastProp = t.GetProperty("lastFrameIndex", flags);
        if (getView == null || firstProp == null || lastProp == null) return "[PCR] API missing";
        int first = (int)firstProp.GetValue(null);
        int last = (int)lastProp.GetValue(null);
        var sb = new StringBuilder();
        sb.Append("[PCR] frames ").Append(first).Append("..").Append(last).AppendLine();

        var viewType = Type.GetType("UnityEditor.Profiling.HierarchyFrameDataView, UnityEditor.CoreModule")
                    ?? Type.GetType("UnityEditor.Profiling.HierarchyFrameDataView, UnityEditor");
        if (viewType == null) return "[PCR] view type not found";
        var getRoot = viewType.GetMethod("GetRootItemID");
        var getChildren = viewType.GetMethod("GetItemChildren");
        var getName = viewType.GetMethod("GetItemName");
        var getCol = viewType.GetMethod("GetItemColumnDataAsSingle");

        // Discover main thread: scan thread indices on the last frame for our markers.
        int mainThread = -1;
        for (int th = 0; th < 6 && mainThread < 0; th++)
        {
            object view = null;
            try { view = getView.Invoke(null, new object[] { last, th, 0, 0, false }); }
            catch (Exception e) { sb.Append("[PCR] th=").Append(th).Append(" ex ").Append(e.GetType().Name).AppendLine(); continue; }
            if (view == null) continue;
            int root = (int)getRoot.Invoke(view, null);
            string rootName = (string)getName.Invoke(view, new object[] { root });
            bool hit = false;
            try
            {
                hit = NameTreeContains(view, root, getChildren, getName, Interesting);
            }
            catch { }
            sb.Append("[PCR] th=").Append(th).Append(" root='").Append(rootName).Append("' hit=").Append(hit).AppendLine();
            if (hit) mainThread = th;
        }
        if (mainThread < 0) return sb.ToString();

        // Aggregate per-marker totals across all frames.
        var totals = new Dictionary<string, double>();
        int framesScanned = 0;
        for (int f = first; f <= last; f++)
        {
            object view = null;
            try { view = getView.Invoke(null, new object[] { f, mainThread, 0, 0, false }); }
            catch { continue; }
            if (view == null) continue;
            framesScanned++;
            int root = (int)getRoot.Invoke(view, null);
            AccumulateTree(view, root, getChildren, getName, getCol, totals);
        }
        sb.Append("[PCR] scanned ").Append(framesScanned).Append(" frames on thread ").Append(mainThread).AppendLine();
        var keys = new List<string>(totals.Keys);
        keys.Sort((a, b) => totals[b].CompareTo(totals[a]));
        foreach (var k in keys)
            sb.Append("[PCR-TOTAL] ").Append(k).Append(" => ").Append(totals[k].ToString("F1")).Append("ms").AppendLine();
        return sb.ToString();
    }

    static bool NameTreeContains(object view, int id, MethodInfo getChildren, MethodInfo getName, string[] needles)
    {
        string n = (string)getName.Invoke(view, new object[] { id });
        if (n != null)
            foreach (var s in needles)
                if (n.Contains(s)) return true;
        var children = (IEnumerable)getChildren.Invoke(view, new object[] { id });
        if (children == null) return false;
        foreach (var c in children)
        {
            if (c == null) continue;
            if (NameTreeContains(view, (int)c, getChildren, getName, needles)) return true;
        }
        return false;
    }

    static void AccumulateTree(object view, int id, MethodInfo getChildren, MethodInfo getName, MethodInfo getCol, Dictionary<string, double> totals)
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
                break;
            }
        }
        var children = (IEnumerable)getChildren.Invoke(view, new object[] { id });
        if (children == null) return;
        foreach (var c in children)
        {
            if (c == null) continue;
            AccumulateTree(view, (int)c, getChildren, getName, getCol, totals);
        }
    }
}
"""

payload = {
    "jsonrpc": "2.0", "id": 17, "method": "tools/call",
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
