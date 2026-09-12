import os, re, sys

ROOT = r'c:\Zombera\Assets\Editor'

# ── Step 1: Fix Tools/0. Scenes/ stragglers ─────────────────────────
migrations = [('Tools/0. Scenes/', 'Tools/Scenes/')]

changed_migrate = 0
for dirpath, _, filenames in os.walk(ROOT):
    for fn in filenames:
        if not fn.endswith('.cs'):
            continue
        fpath = os.path.join(dirpath, fn)
        with open(fpath, 'r', encoding='utf-8') as f:
            original = f.read()
        content = original
        for old, new in migrations:
            content = content.replace(old, new)
        if content != original:
            with open(fpath, 'w', encoding='utf-8', newline='\n') as f:
                f.write(content)
            changed_migrate += 1
            print(f'  MIGRATE  {os.path.relpath(fpath, ROOT)}')

# ── Step 2: Add priority=100 to MenuItems without priority ──────────

changed_priority = 0

for dirpath, _, filenames in os.walk(ROOT):
    for fn in filenames:
        if not fn.endswith('.cs'):
            continue
        fpath = os.path.join(dirpath, fn)
        with open(fpath, 'r', encoding='utf-8') as f:
            content = f.read()

        if 'Tools/' not in content:
            continue
        if '[MenuItem' not in content:
            continue

        original = content
        modified = False

        # Pattern A: [MenuItem("Tools/...")]  -- bare, no other args
        # Capture the full attribute, but only replace if no priority present
        lines = content.split('\n')
        new_lines = []
        for line in lines:
            stripped = line.strip()

            # Skip if already has priority
            if 'priority' in stripped:
                new_lines.append(line)
                continue

            # Pattern A: [MenuItem("Tools/...")]  (bare, one string arg)
            m = re.match(r'^(\s*\[MenuItem\("Tools/[^"]+"\)\s*\])\s*$', stripped)
            if m:
                # Replace the closing )] with , priority = 100)]
                old_attr = m.group(1)
                new_attr = old_attr[:-2] + ', priority = 100)]'
                line = line.replace(old_attr, new_attr)
                modified = True

            # Pattern B: [MenuItem("Tools/...", true)]  (validate arg)
            m = re.match(r'^(\s*\[MenuItem\("Tools/[^"]+",\s*true\)\s*\])\s*$', stripped)
            if m:
                old_attr = m.group(1)
                new_attr = old_attr[:-2] + ', priority = 100)]'
                line = line.replace(old_attr, new_attr)
                modified = True

            # Pattern C: [MenuItem(MenuPath)]  (const-based, bare)
            m = re.match(r'^(\s*\[MenuItem\(([A-Za-z_]\w*(\s*\+\s*"[^"]*")*)\)\s*\])\s*$', stripped)
            if m:
                inner = m.group(2)
                old_attr = m.group(1)
                new_attr = f'[MenuItem({inner}, priority = 100)]'
                line = line.replace(old_attr, new_attr)
                modified = True

            new_lines.append(line)

        if modified:
            with open(fpath, 'w', encoding='utf-8', newline='\n') as f:
                f.write('\n'.join(new_lines))
            changed_priority += 1
            print(f'  PRIORITY  {os.path.relpath(fpath, ROOT)}')

print(f'\nMigration: {changed_migrate} files  |  Priority: {changed_priority} files')
