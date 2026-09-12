import json
import re
import subprocess

SID = open('sid.txt').read().strip()

CODE = r"""
using System;
using System.Reflection;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.Profiling;
public class Script
{
    public static string Main()
    {
        var t = Type.GetType("UnityEditorInternal.ProfilerDriver, UnityEditor.CoreModule")
             ?? Type.GetType("UnityEditorInternal.ProfilerDriver, UnityEditor");
        var flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        var firstProp = t.GetProperty("firstFrameIndex", flags);
        var lastProp = t.GetProperty("lastFrameIndex", flags);
        int first = (int)firstProp.GetValue(null);
        int last = (int)lastProp.GetValue(null);
        var sb = new StringBuilder();
        sb.Append("[PCR] frames before test: ").Append(first).Append("..").Append(last).AppendLine();
        sb.Append("[PCR] Profiler.enabled=").Append(Profiler.enabled).AppendLine();
        if (first >= 0 && last >= 0)
        {
            Profiler.BeginSample("PCRTest.Marker2");
            Thread.Sleep(50);
            Profiler.EndSample();
            int first2 = (int)firstProp.GetValue(null);
            int last2 = (int)lastProp.GetValue(null);
            sb.Append("[PCR] frames after test: ").Append(first2).Append("..").Append(last2).AppendLine();
        }
        else
        {
            sb.Append("[PCR] no active capture session").AppendLine();
        }
        return sb.ToString();
    }
}
"""

payload = {
    "jsonrpc": "2.0", "id": 33, "method": "tools/call",
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
