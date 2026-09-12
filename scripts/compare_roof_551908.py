import re

def parse_prefab_instances(path):
    with open(path, 'r', encoding='utf-8') as f:
        content = f.read()
    instances = {}
    blocks = re.split(r'--- !u!1001 &', content)
    for block in blocks:
        m = re.match(r'(\d+)', block)
        if not m: continue
        fid = m.group(1)
        src = re.search(r'm_SourcePrefab:\s*\{fileID:\s*\d+,\s*guid:\s*([a-f0-9]+),\s*type:\s*\d+\}', block)
        parent = re.search(r'm_TransformParent:\s*\{fileID:\s*(\d+)\}', block)
        mods = []
        for mod in re.finditer(
            r'- target: \{fileID:\s*(\d+),\s*guid:\s*([a-f0-9]+),\s*type:\s*\d+\}\s*\n'
            r'\s*propertyPath:\s*(.+?)\s*\n'
            r'\s*value:\s*(.*?)\s*\n'
            r'\s*objectReference:\s*\{fileID:\s*(\d+)\}',
            block
        ):
            prop = mod.group(3).strip()
            val = mod.group(4).strip()
            try: val = float(val)
            except: pass
            mods.append({'property': prop, 'value': val})
        instances[fid] = {'fid': fid, 'guid': src.group(1) if src else None, 'parent_tf': parent.group(1) if parent else None, 'mods': mods}
    return instances

def get_mod(mods, prop, default=0.0):
    for m in mods:
        if m['property'] == prop: return m['value']
    return default

def extract_all(instances):
    results = []
    for fid, inst in instances.items():
        name = get_mod(inst['mods'], 'm_Name', '?')
        px = get_mod(inst['mods'], 'm_LocalPosition.x')
        py = get_mod(inst['mods'], 'm_LocalPosition.y')
        pz = get_mod(inst['mods'], 'm_LocalPosition.z')
        rx = get_mod(inst['mods'], 'm_LocalRotation.x')
        ry = get_mod(inst['mods'], 'm_LocalRotation.y')
        rz = get_mod(inst['mods'], 'm_LocalRotation.z')
        rw = get_mod(inst['mods'], 'm_LocalRotation.w', 1.0)
        sx = get_mod(inst['mods'], 'm_LocalScale.x', 1.0)
        sy = get_mod(inst['mods'], 'm_LocalScale.y', 1.0)
        sz = get_mod(inst['mods'], 'm_LocalScale.z', 1.0)
        results.append({'name': name, 'pos': (px, py, pz), 'rot': (rx, ry, rz, rw), 'scl': (sx, sy, sz), 'guid': inst['guid']})
    return results

ref = extract_all(parse_prefab_instances(r'Assets\02_Shared\Prefabs\Building\Buildings_Modular_Complete\reference.prefab'))
res = extract_all(parse_prefab_instances(r'Assets\02_Shared\Prefabs\Building\Buildings_Modular_Complete\Residential_551908.prefab'))

def roof_items(lst):
    return [i for i in lst if any(k in i['name'] for k in ['Shed_', 'Roof'])]

ref_r = roof_items(ref)
res_r = roof_items(res)

print('=== REFERENCE Roof ===')
for r in ref_r:
    print('  %-30s  pos=(%.3f,%.3f,%.3f)  rot=(%.3f,%.3f,%.3f,%.3f)  scl=(%.3f,%.3f,%.3f)  guid=%s' % (
        r['name'], r['pos'][0], r['pos'][1], r['pos'][2],
        r['rot'][0], r['rot'][1], r['rot'][2], r['rot'][3],
        r['scl'][0], r['scl'][1], r['scl'][2], r['guid'][:8] if r['guid'] else '?'))

print()
print('=== RESIDENTIAL 551908 Roof ===')
for r in res_r:
    print('  %-30s  pos=(%.3f,%.3f,%.3f)  rot=(%.3f,%.3f,%.3f,%.3f)  scl=(%.3f,%.3f,%.3f)  guid=%s' % (
        r['name'], r['pos'][0], r['pos'][1], r['pos'][2],
        r['rot'][0], r['rot'][1], r['rot'][2], r['rot'][3],
        r['scl'][0], r['scl'][1], r['scl'][2], r['guid'][:8] if r['guid'] else '?'))

# Deltas
print()
print('=== Roof Deltas (Res - Ref) ===')
ref_by_name = {r['name']: r for r in ref_r}
res_by_name = {r['name']: r for r in res_r}
for name in sorted(set(list(ref_by_name.keys()) + list(res_by_name.keys()))):
    rr = ref_by_name.get(name)
    rs = res_by_name.get(name)
    if rr and rs:
        dp = (rs['pos'][0]-rr['pos'][0], rs['pos'][1]-rr['pos'][1], rs['pos'][2]-rr['pos'][2])
        dr = (rs['rot'][0]-rr['rot'][0], rs['rot'][1]-rr['rot'][1], rs['rot'][2]-rr['rot'][2], rs['rot'][3]-rr['rot'][3])
        ds = (rs['scl'][0]-rr['scl'][0], rs['scl'][1]-rr['scl'][1], rs['scl'][2]-rr['scl'][2])
        print('  %-30s  dpos=(%+.3f,%+.3f,%+.3f)  drot=(%+.3f,%+.3f,%+.3f,%+.3f)  dscl=(%+.3f,%+.3f,%+.3f)' % (name, dp[0], dp[1], dp[2], dr[0], dr[1], dr[2], dr[3], ds[0], ds[1], ds[2]))
    elif rr:
        print('  %-30s  ONLY IN REFERENCE' % name)
    else:
        print('  %-30s  ONLY IN RESIDENTIAL' % name)
