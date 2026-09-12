import json
import re
import subprocess

SID = open('sid.txt').read().strip()
payload = {
    "jsonrpc": "2.0", "id": 12, "method": "tools/call",
    "params": {"name": "console-get-logs", "arguments": {"maxEntries": 80, "includeStackTrace": False, "lastMinutes": 10}}
}
out = subprocess.run(
    ["curl", "-s", "-m", "60", "-X", "POST", "http://localhost:24566/p/0cc3a677",
     "-H", "Content-Type: application/json", "-H", "Accept: application/json, text/event-stream",
     "-H", "Mcp-Session-Id: " + SID, "-d", json.dumps(payload)],
    capture_output=True, text=True).stdout
m = re.search(r'data: (\{.*\})', out, re.S)
if not m:
    print("RAW:", out[:300])
    raise SystemExit
d = json.loads(m.group(1))
content = d['result']['content']
txt = content[0].get('text', '') if content else ''
try:
    data = json.loads(txt)
except Exception:
    data = txt
logs = data.get('result', data) if isinstance(data, dict) else data
if isinstance(logs, dict):
    logs = logs.get('Logs', logs.get('logs', logs.get('entries', [])))
for e in logs:
    msg = e.get('Message', '')
    if '[PCR' in msg:
        print(msg[:500])
