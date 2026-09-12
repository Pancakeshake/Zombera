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
        var viewType = Type.GetType("UnityEditor.Profiling.HierarchyFrameDataView, UnityEditor.CoreModule")
                    ?? Type.GetType("UnityEditor.Profiling.HierarchyFrameDataView, UnityEditor");
        var getRoot = viewType.GetMethod("GetRootItemID", Type.EmptyTypes);
        var getChildren = viewType.GetMethod("GetItemChildren", new Type[] { typeof(int), typeof(List<int>) });
        var getName = viewType.GetMethod("GetItemName", new Type[] { typeof(int) });
        var getCol = viewType.GetMethod("GetItemColumnDataAsSingle", new Type[] { typeof(int), typeof(int) });

        var sb = new StringBuilder();
        foreach (var f in new[] { 420, 500, 560, 674 })
        {
            object view = null;
            try { view = getView.Invoke(null, new object[] { f, 0, 0, 0, false }); }
            catch (Exception e) { sb.Append("[PCR] f=").Append(f).Append(" view ex ").Append(e.GetType().Name).AppendLine(); continue; }
            if (view == null) { sb.Append("[PCR] f=").Append(f).Append(" null view").AppendLine(); continue; }
            int root = (int)getRoot.Invoke(view, null);
            var stack = new Stack<(int id, int depth)>();
            stack.Push((root, 0));
            var total = 0.0f;
            int count = 0;
            var lines = new List<string>();
            while (stack.Count > 0)
            {
                var (id, depth) = stack.Pop();
                if (depth >= 5) continue;
                string n = null;
                try { n = (string)getName.Invoke(view, new object[] { id }); } catch { }
                float ms = 0f;
                try { ms = (float)getCol.Invoke(view, new object[] { id, 0 }); } catch { }
                if (depth == 0) total = ms;
                if (n != null && ms > 0.01f && count < 40)
                {
                    lines.Add(new string(' ', depth * 2) + n + "  " + ms.ToString("F2") + "ms");
                    count++;
                }
                var kids = new List<int>();
                try { getChildren.Invoke(view, new object[] { id, kids }); } catch { continue; }
                for (int i = kids.Count - 1; i >= 0; i--) stack.Push((kids[i], depth + 1));
            }
            sb.Append("[PCR] === frame ").Append(f).Append(" rootTotal=").Append(total.ToString("F1")).Append("ms ===\n");
            foreach (var l in lines) sb.Append("[PCR] ").Append(l).AppendLine();
        }
        return sb.ToString();
    }
}
"""

payload = {
    "jsonrpc": "2.0", "id": 34, "method": "tools/call",
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
        print(txt[:3000])
