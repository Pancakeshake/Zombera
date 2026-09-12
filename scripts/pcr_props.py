import json
import re
import subprocess

SID = open('sid.txt').read().strip()

CODE = r"""
using System;
using System.Reflection;
using System.Text;
using UnityEngine;
public class Script
{
    public static string Main()
    {
        var t = Type.GetType("UnityEditorInternal.ProfilerDriver, UnityEditor.CoreModule")
             ?? Type.GetType("UnityEditorInternal.ProfilerDriver, UnityEditor");
        var sb = new StringBuilder();
        sb.Append("[PCR] type=").Append(t != null).AppendLine();
        if (t == null) return sb.ToString();
        var flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        foreach (var p in t.GetProperties(flags))
        {
            if (p.Name.ToLower().Contains("editor") || p.Name.ToLower().Contains("record") || p.Name.ToLower().Contains("profile"))
                sb.Append("[PCR-P] ").Append(p.PropertyType.Name).Append(" ").Append(p.Name).AppendLine();
        }
        foreach (var f in t.GetFields(flags))
        {
            if (f.Name.ToLower().Contains("editor") || f.Name.ToLower().Contains("record") || f.Name.ToLower().Contains("profile"))
                sb.Append("[PCR-F] ").Append(f.FieldType.Name).Append(" ").Append(f.Name).AppendLine();
        }
        return sb.ToString();
    }
}
"""

payload = {
    "jsonrpc": "2.0", "id": 26, "method": "tools/call",
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
