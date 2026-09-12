import os

log = os.path.join(os.environ.get('LOCALAPPDATA', ''), 'Unity', 'Editor', 'Editor.log')
lines = open(log, encoding='utf-8', errors='replace').readlines()
print('total lines', len(lines))
count = 0
for i, line in enumerate(lines):
    if 'error CS' in line:
        s = line.replace('\\\\', '\\')
        idx = s.find('error CS')
        while idx != -1 and count < 25:
            print('---', i, '---')
            print(s[max(0, idx - 160): idx + 160].replace('\\u0022', '"').replace('\\n', ' | '))
            count += 1
            idx = s.find('error CS', idx + 1)
