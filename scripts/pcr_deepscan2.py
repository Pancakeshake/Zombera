import json
import re
import subprocess

SID = open('sid.txt').read().strip()

CODE = r"""
using System;
using System.Collections;
using System.Reflection;
using System.Text;
using UnityEngine;
public class Script
{
    public static string Main()
    {
        var t = Type.GetType("UnityEditorInternal.ProfilerDriver, UnityEditor.CoreModule")
             ?? Type.GetType("UnityEditorInternal.ProfilerDriver, UnityEditor");
        if (t == null) return "[PCR] type not found";
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
        var getChildren = viewType.GetMethod("GetItemChildren", new Type[] { typeof(int) });
        var getName = viewType.GetMethod("GetItemName", new Type[] { typeof(int) });
        var getCol = viewType.GetMethod("GetItemColumnDataAsSingle", new Type[] { typeof(int), typeof(int) });
        sb.Append("[PCR] methods: root=").Append(getRoot != null).Append(" children=").Append(getChildren != null)
          .Append(" name=").Append(getName != null).Append(" col=").Append(getCol != null).AppendLine();

        for (int th = 0; th < 20; th++)
        {
            bool anyHit = false;
            for (int f = first; f <= last; f += 100)
            {
                object view = null;
                try { view = getView.Invoke(null, new object[] { f, th, 0, 0, false }); }
                catch { continue; }
                if (view == null) continue;
                int root = (int)getRoot.Invoke(view, null);
                if (TreeContains(view, root, getChildren, getName, "Road")) { anyHit = true; break; }
            }
            if (anyHit) sb.Append("[PCR] thread ").Append(th).Append(" has Road markers!").AppendLine();
        }

        object lastView = null;
        try { lastView = getView.Invoke(null, new object[] { last, 0, 0, 0, false }); }
        catch { return sb.Append("[PCR] no view for last frame").ToString(); }
        int r = (int)getRoot.Invoke(lastView, null);
        sb.Append("[PCR] last-frame root='").Append((string)getName.Invoke(lastView, new object[] { r })).AppendLine("'");
        var kids = (IEnumerable)getChildren.Invoke(lastView, new object[] { r });
        if (kids != null)
            foreach (var k in kids)
            {
                int kid = (int)k;
                string n = (string)getName.Invoke(lastView, new object[] { kid });
                float ms = 0f;
                try { ms = (float)getCol.Invoke(lastView, new object[] { kid, 0 }); } catch { }
                sb.Append("[PCR]  + ").Append(n).Append("  ").Append(ms.ToString("F2")).Append("ms").AppendLine();
                var grand = (IEnumerable)getChildren.Invoke(lastView, new object[] { kid });
                int gcount = 0;
                if (grand != null)
                    foreach (var g in grand)
                    {
                        if (gcount++ > 5) break;
                        string gn = (string)getName.Invoke(lastView, new object[] { (int)g });
                        sb.Append("[PCR]     - ").Append(gn).AppendLine();
                    }
            }
        return sb.ToString();
    }

    static bool TreeContains(object view, int id, MethodInfo getChildren, MethodInfo getName, string needle)
    {
        string n = (string)getName.Invoke(view, new object[] { id });
        if (n != null && n.Contains(needle)) return true;
        var children = (IEnumerable)getChildren.Invoke(view, new object[] { id });
        if (children == null) return false;
        foreach (var c in children)
            if (c != null && TreeContains(view, (int)c, getChildren, getName, needle)) return true;
        return false;
    }
}
"""

payload = {
    "jsonrpc": "2.0", "id": 23, "method": "tools/call",
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
