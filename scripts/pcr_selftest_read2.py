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

        bool foundAny = false;
        for (int th = 0; th < 6; th++)
        {
            try
            {
                for (int f = first; f <= last; f++)
                {
                    object view = null;
                    try { view = getView.Invoke(null, new object[] { f, th, 0, 0, false }); }
                    catch { continue; }
                    if (view == null) continue;
                    int root = 0;
                    try { root = (int)getRoot.Invoke(view, null); } catch { continue; }
                    var kids = new List<int>();
                    try { getChildren.Invoke(view, new object[] { root, kids }); } catch { continue; }
                    // scan depth-first with explicit stack
                    var stack = new Stack<int>();
                    for (int i = kids.Count - 1; i >= 0; i--) stack.Push(kids[i]);
                    int guard = 0;
                    while (stack.Count > 0 && guard++ < 20000)
                    {
                        int id = stack.Pop();
                        string n = null;
                        try { n = (string)getName.Invoke(view, new object[] { id }); } catch { }
                        if (n != null && n.Contains("PCRTest"))
                        {
                            float ms = 0f;
                            try { ms = (float)getCol.Invoke(view, new object[] { id, 0 }); } catch { }
                            sb.Append("[PCR] FOUND PCRTest frame ").Append(f).Append(" th=").Append(th)
                              .Append(" total=").Append(ms.ToString("F1")).Append("ms name='").Append(n).Append("'").AppendLine();
                            foundAny = true;
                            break;
                        }
                        var sub = new List<int>();
                        try { getChildren.Invoke(view, new object[] { id, sub }); } catch { continue; }
                        for (int i = sub.Count - 1; i >= 0; i--) stack.Push(sub[i]);
                    }
                    if (foundAny) break;
                }
            }
            catch (Exception e) { sb.Append("[PCR] th=").Append(th).Append(" scan error ").Append(e.GetType().Name).AppendLine(); }
            if (foundAny) break;
        }
        if (!foundAny) sb.Append("[PCR] PCRTest marker NOT found in capture").AppendLine();
        return sb.ToString();
    }
}
"""

payload = {
    "jsonrpc": "2.0", "id": 32, "method": "tools/call",
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
