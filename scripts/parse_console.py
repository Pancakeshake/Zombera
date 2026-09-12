import json
import re

raw = open('console3.json', encoding='utf-8').read()
m = re.search(r'data: (\{.*\})', raw, re.S)
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
print('TOTAL ENTRIES:', len(logs))
for e in logs:
    msg = e.get('Message', '')
    if any(s in msg for s in ['ProceduralRoadDiag', 'City prefab road network built']):
        print('[' + e.get('LogType', '?') + ']', msg)
        print()
