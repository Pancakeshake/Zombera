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
        sb.Append("[PCR] viewType=").Append(viewType != null).AppendLine();
        if (viewType == null) return sb.ToString();
        foreach (var m in viewType.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        {
            var ps = new StringBuilder();
            foreach (var p in m.GetParameters()) ps.Append(p.ParameterType.Name).Append(",");
            sb.Append("[PCR-M] ").Append(m.Name).Append("(").Append(ps).Append(") -> ").Append(m.ReturnType.Name).AppendLine();
        }
        return sb.ToString();
    }
}
"""

payload = {
    "jsonrpc": "2.0", "id": 24, "method": "tools/call",
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
