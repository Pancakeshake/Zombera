import os, re

ROOT = r'c:\Zombera\Assets\Editor'
changed = 0

for dirpath, _, filenames in os.walk(ROOT):
    for fn in filenames:
        if not fn.endswith('.cs'):
            continue
        fpath = os.path.join(dirpath, fn)
        with open(fpath, 'r', encoding='utf-8') as f:
            content = f.read()

        if '[MenuItem' not in content:
            continue

        original = content

        # Pattern D: [MenuItem(IDENT, true)]  ->  [MenuItem(IDENT, true, priority = 100)]
        content = re.sub(
            r'\[MenuItem\(([A-Za-z_]\w*(?:\s*\+\s*"[^"]*")*),\s*true\)\s*\]',
            r'[MenuItem(\1, true, priority = 100)]',
            content
        )

        # Pattern D2: [MenuItem(IDENT, validate = true)]  ->  [MenuItem(IDENT, validate = true, priority = 100)]
        content = re.sub(
            r'\[MenuItem\(([A-Za-z_]\w*(?:\s*\+\s*"[^"]*")*),\s*validate\s*=\s*true\)\s*\]',
            r'[MenuItem(\1, validate = true, priority = 100)]',
            content
        )

        # Pattern E: [MenuItem(IDENT + "...")]  ->  [MenuItem(IDENT + "...", priority = 100)]
        # IDENT can contain dots for static class access like MenuPaths.Assets
        content = re.sub(
            r'\[MenuItem\(([A-Za-z_]\w*(?:\.[A-Za-z_]\w*)*(?:\s*\+\s*"[^"]*")+)\)\s*\]',
            r'[MenuItem(\1, priority = 100)]',
            content
        )

        if content != original:
            with open(fpath, 'w', encoding='utf-8', newline='\n') as f:
                f.write(content)
            changed += 1
            print('  OK  ' + os.path.relpath(fpath, ROOT))

print('\nFinal pass: ' + str(changed) + ' files updated.')
