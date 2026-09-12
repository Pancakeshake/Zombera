import json
import re
import subprocess

SID = open('sid.txt').read().strip()

CODE = r"""
using System;
using System.Reflection;
using System.Text;
public class Script
{
    public static string Main()
    {
        var t = Type.GetType("UnityEditorInternal.ProfilerWindow, UnityEditor.CoreModule")
             ?? Type.GetType("UnityEditorInternal.ProfilerWindow, UnityEditor");
        var sb = new StringBuilder();
        sb.Append("[PCR] ProfilerWindow type=").Append(t != null).AppendLine();
        if (t == null) return sb.ToString();
        foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            if (m.Name.ToLower().Contains("record") || m.Name.ToLower().Contains("capture") || m.Name.ToLower().Contains("profile"))
            {
                var ps = new StringBuilder();
                foreach (var p in m.GetParameters()) ps.Append(p.ParameterType.Name).Append(",");
                sb.Append("[PCR-M] ").Append(m.Name).Append("(").Append(ps).Append(") -> ").Append(m.ReturnType.Name).AppendLine();
            }
        }
        foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
        {
            if (p.Name.ToLower().Contains("record"))
                sb.Append("[PCR-P] ").Append(p.PropertyType.Name).Append(" ").Append(p.Name)
                  .Append(" set=").Append(p.GetSetMethod(true) != null).AppendLine();
        }
        return sb.ToString();
    }
}
"""

payload = {
    "jsonrpc": "2.0", "id": 29, "method": "tools/call",
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
