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
        if (t == null) return "[PCR] ProfilerDriver type not found";
        var getStats = t.GetMethod("GetFormattedStatisticsValue", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        var firstProp = t.GetProperty("firstFrameIndex", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        var lastProp = t.GetProperty("lastFrameIndex", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        if (getStats == null || firstProp == null || lastProp == null) return "[PCR] API missing";
        int first = (int)firstProp.GetValue(null);
        int last = (int)lastProp.GetValue(null);
        var sb = new StringBuilder();
        sb.Append("[PCR] frames ").Append(first).Append("..").Append(last).AppendLine();
        int mainId = -1;
        for (int id = 0; id < 16 && mainId < 0; id++)
        {
            try
            {
                var s = getStats.Invoke(null, new object[] { last, id }) as string;
                if (s != null && (s.Contains("RoadSplines") || s.Contains("EditorLoop"))) mainId = id;
            }
            catch { }
        }
        sb.Append("[PCR] mainId=").Append(mainId).AppendLine();
        if (mainId < 0) return sb.ToString();
        var sample = getStats.Invoke(null, new object[] { last, mainId }) as string;
        int shown = 0;
        foreach (var line in sample.Split('\n'))
        {
            if ((line.Contains("RoadSplines") || line.Contains("CityHub")) && shown < 8)
            {
                sb.Append("[PCR-SAMPLE] ").Append(line.Trim()).AppendLine();
                shown++;
            }
        }
        return sb.ToString();
    }
}
"""

payload = {
    "jsonrpc": "2.0", "id": 14, "method": "tools/call",
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
print(json.dumps(d.get('result', {}), indent=1)[:1500])
print("=== CONTENT ===")
for it in d.get('result', {}).get('content', []):
    print(it.get('text', '')[:1500])
