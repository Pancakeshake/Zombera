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
using UnityEditor;
public class Script
{
    public static string Main()
    {
        var sb = new System.Text.StringBuilder();
        var w = EditorWindow.GetWindow<ProfilerWindow>();
        sb.Append("[PCR] window=").Append(w != null).AppendLine();
        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var t = typeof(ProfilerWindow);
        var isRec = t.GetMethod("IsRecording", flags);
        var setRec = t.GetMethod("SetRecordingEnabled", flags);
        var connectedProp = t.GetProperty("ConnectedToEditor", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        sb.Append("[PCR] before: isRecording=").Append(isRec != null && (bool)isRec.Invoke(w, null))
          .Append(" connectedToEditor=").Append(connectedProp != null && (bool)connectedProp.GetValue(w)).AppendLine();
        if (setRec != null) setRec.Invoke(w, new object[] { true });
        Thread.Sleep(300);
        sb.Append("[PCR] after: isRecording=").Append((bool)isRec.Invoke(w, null))
          .Append(" connectedToEditor=").Append((bool)connectedProp.GetValue(w)).AppendLine();

        var drv = Type.GetType("UnityEditorInternal.ProfilerDriver, UnityEditor.CoreModule")
               ?? Type.GetType("UnityEditorInternal.ProfilerDriver, UnityEditor");
        var dflags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        var pe = drv.GetProperty("profileEditor", dflags);
        if (pe != null) pe.SetValue(null, true);
        var clear = drv.GetMethod("ClearAllFrames", dflags);
        if (clear != null) clear.Invoke(null, null);
        Thread.Sleep(200);

        Profiler.BeginSample("PCRTest.Marker3");
        Thread.Sleep(60);
        Profiler.EndSample();

        var firstProp = drv.GetProperty("firstFrameIndex", dflags);
        var lastProp = drv.GetProperty("lastFrameIndex", dflags);
        sb.Append("[PCR] frames now: ").Append((int)firstProp.GetValue(null)).Append("..").Append((int)lastProp.GetValue(null)).AppendLine();
        sb.Append("[PCR] profileEditor=").Append((bool)pe.GetValue(null)).AppendLine();
        return sb.ToString();
    }
}
"""

payload = {
    "jsonrpc": "2.0", "id": 37, "method": "tools/call",
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
