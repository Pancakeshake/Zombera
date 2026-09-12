import re
text = open(r'C:\Users\codyc\AppData\Roaming\Blender Foundation\Blender\5.1\scripts\addons\addon.py', 'r', encoding='utf-8').read()
cmds = re.findall(r'''if\s+command_type\s*==\s*["'](.+?)["']''', text)
for c in sorted(set(cmds)):
    print(c)
