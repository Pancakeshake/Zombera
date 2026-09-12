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
        if (t == null) return "[PCR] type not found";
        var getStats = t.GetMethod("GetFormattedStatisticsValue", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        var lastProp = t.GetProperty("lastFrameIndex", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (getStats == null || lastProp == null) return "[PCR] api missing";
        int last = (int)lastProp.GetValue(null);
        var sb = new StringBuilder();
        for (int id = 0; id < 10; id++)
        {
            string s = null;
            try { s = getStats.Invoke(null, new object[] { last, id }) as string; }
            catch (Exception e) { sb.Append("[PCR] id=").Append(id).Append(" EX: ").Append(e.GetType().Name).AppendLine(); continue; }
            if (s == null) { sb.Append("[PCR] id=").Append(id).Append(" null").AppendLine(); continue; }
            var firstLine = s.Length > 160 ? s.Substring(0, 160).Replace("\n", " | ") : s.Replace("\n", " | ");
            sb.Append("[PCR] id=").Append(id).Append(" len=").Append(s.Length).Append(" :: ").Append(firstLine).AppendLine();
        }
        return sb.ToString();
    }
}
"""

payload = {
    "jsonrpc": "2.0", "id": 15, "method": "tools/call",
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
        print(txt[:2000])
