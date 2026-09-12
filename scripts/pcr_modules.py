import json
import re
import subprocess

SID = open('sid.txt').read().strip()

CODE = r"""
using System;
using System.Linq;
using System.Reflection;
using System.Text;
public class Script
{
    public static string Main()
    {
        var sb = new StringBuilder();
        var asm = typeof(UnityEditor.EditorApplication).Assembly;
        var types = asm.GetTypes().Where(t => t.Name.Contains("ProfilerModule") || t.Name.Contains("ProfilerWindow")).ToArray();
        foreach (var t in types)
        {
            sb.Append("[PCR] type: ").Append(t.FullName).AppendLine();
            foreach (var p in t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                if (p.Name.ToLower().Contains("sample") || p.Name.ToLower().Contains("timeline") || p.Name.ToLower().Contains("script"))
                    sb.Append("[PCR-P] ").Append(p.PropertyType.Name).Append(" ").Append(p.Name)
                      .Append(" set=").Append(p.GetSetMethod(true) != null).AppendLine();
            }
            foreach (var f in t.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                if (f.Name.ToLower().Contains("sample") || f.Name.ToLower().Contains("timeline") || f.Name.ToLower().Contains("script"))
                    sb.Append("[PCR-F] ").Append(f.FieldType.Name).Append(" ").Append(f.Name).AppendLine();
            }
        }
        return sb.ToString();
    }
}
"""

payload = {
    "jsonrpc": "2.0", "id": 35, "method": "tools/call",
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
