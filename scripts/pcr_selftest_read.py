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

        for (int th = 0; th < 6; th++)
        {
            for (int f = first; f <= last; f++)
            {
                object view = null;
                try { view = getView.Invoke(null, new object[] { f, th, 0, 0, false }); }
                catch { continue; }
                if (view == null) continue;
                int root = (int)getRoot.Invoke(view, null);
                double found = Find(view, root, getChildren, getName, getCol, "PCRTest");
                if (found >= 0)
                {
                    sb.Append("[PCR] FOUND PCRTest at frame ").Append(f).Append(" thread ").Append(th)
                      .Append(" total=").Append(found.ToString("F1")).Append("ms").AppendLine();
                    f = last; break; // stop scanning this thread
                }
            }
        }
        return sb.ToString();
    }

    static double Find(object view, int id, MethodInfo getChildren, MethodInfo getName, MethodInfo getCol, string needle)
    {
        string n = (string)getName.Invoke(view, new object[] { id });
        if (n != null && n.Contains(needle))
        {
            float ms = 0f;
            try { ms = (float)getCol.Invoke(view, new object[] { id, 0 }); } catch { }
            return ms;
        }
        var kids = new List<int>();
        try { getChildren.Invoke(view, new object[] { id, kids }); } catch { return -1; }
        foreach (var c in kids)
        {
            double r = Find(view, c, getChildren, getName, getCol, needle);
            if (r >= 0) return r;
        }
        return -1;
    }
}
"""

payload = {
    "jsonrpc": "2.0", "id": 31, "method": "tools/call",
    "params": {"name": "script-execute", "arguments": {
        "csharpCode": CODE, "className": "Script", "methodName": "Main", "isMethodBody": False}}
}
out = subprocess.run(
    ["curl", "-s", "-m", "300", "-X", "POST", "http://localhost:24566/p/0cc3a677",
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
        print(txt[:1000])
