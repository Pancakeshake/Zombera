import json
import re
import subprocess

SID = open('sid.txt').read().strip()

CODE = r"""
using System;
using System.Reflection;
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
        var pe = t.GetProperty("profileEditor", flags);
        if (pe != null) pe.SetValue(null, true);
        Profiler.enabled = true;
        var clear = t.GetMethod("ClearAllFrames", flags);
        if (clear != null) clear.Invoke(null, null);
        Thread.Sleep(200); // let a few frames settle
        Profiler.BeginSample("PCRTest.Marker");
        Thread.Sleep(80);
        Profiler.EndSample();
        var enabled = Profiler.enabled;
        var peNow = (bool)pe.GetValue(null);
        return "[PCR] profileEditor=" + peNow + " enabled=" + enabled + " testMarkerEmitted";
    }
}
"""

payload = {
    "jsonrpc": "2.0", "id": 30, "method": "tools/call",
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
