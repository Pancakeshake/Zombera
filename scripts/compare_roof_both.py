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

def roof_only(lst):
    return [i for i in lst if any(k in i['name'] for k in ['Shed_', 'Roof'])]

base = r'Assets\02_Shared\Prefabs\Building\Buildings_Modular_Complete'
ref = extract_all(parse_prefab_instances(base + r'\reference.prefab'))
a = extract_all(parse_prefab_instances(base + r'\Residential_135641.prefab'))
b = extract_all(parse_prefab_instances(base + r'\Residential_622580.prefab'))

ref_r = roof_only(ref)
a_r = roof_only(a)
b_r = roof_only(b)

import math

def euler_from_quat(rx, ry, rz, rw):
    """Approximate Euler angles from quaternion for display."""
    # Roll (x-axis rotation)
    sinr_cosp = 2 * (rw * rx + ry * rz)
    cosr_cosp = 1 - 2 * (rx * rx + ry * ry)
    roll = math.atan2(sinr_cosp, cosr_cosp)
    # Pitch (y-axis rotation)
    sinp = 2 * (rw * ry - rz * rx)
    if abs(sinp) >= 1:
        pitch = math.copysign(math.pi / 2, sinp)
    else:
        pitch = math.asin(sinp)
    # Yaw (z-axis rotation)
    siny_cosp = 2 * (rw * rz + rx * ry)
    cosy_cosp = 1 - 2 * (ry * ry + rz * rz)
    yaw = math.atan2(siny_cosp, cosy_cosp)
    return (math.degrees(roll), math.degrees(pitch), math.degrees(yaw))

def fmt_euler(r):
    e = euler_from_quat(r[0], r[1], r[2], r[3])
    return 'E(%+.0f,%+.0f,%+.0f)' % e

print('='*90)
print('REFERENCE')
print('='*90)
for r in ref_r:
    print('  %-30s  pos=(%7.3f,%7.3f,%7.3f)  rot=(%+.3f,%+.3f,%+.3f,%+.3f) %s  scl=(%.3f,%.3f,%.3f)' % (
        r['name'], r['pos'][0], r['pos'][1], r['pos'][2],
        r['rot'][0], r['rot'][1], r['rot'][2], r['rot'][3], fmt_euler(r['rot']),
        r['scl'][0], r['scl'][1], r['scl'][2]))

print()
print('='*90)
print('Residential_135641  (gables flipped: tall on low end)')
print('='*90)
for r in a_r:
    print('  %-30s  pos=(%7.3f,%7.3f,%7.3f)  rot=(%+.3f,%+.3f,%+.3f,%+.3f) %s  scl=(%.3f,%.3f,%.3f)' % (
        r['name'], r['pos'][0], r['pos'][1], r['pos'][2],
        r['rot'][0], r['rot'][1], r['rot'][2], r['rot'][3], fmt_euler(r['rot']),
        r['scl'][0], r['scl'][1], r['scl'][2]))

print()
print('='*90)
print('Residential_622580  (gables facing down, panel wrong)')
print('='*90)
for r in b_r:
    print('  %-30s  pos=(%7.3f,%7.3f,%7.3f)  rot=(%+.3f,%+.3f,%+.3f,%+.3f) %s  scl=(%.3f,%.3f,%.3f)' % (
        r['name'], r['pos'][0], r['pos'][1], r['pos'][2],
        r['rot'][0], r['rot'][1], r['rot'][2], r['rot'][3], fmt_euler(r['rot']),
        r['scl'][0], r['scl'][1], r['scl'][2]))

# Compare rotations to reference
print()
print('='*90)
print('ROTATION COMPARISON')
print('='*90)
ref_rn = {r['name']: r for r in ref_r}
for label, data in [('135641', a_r), ('622580', b_r)]:
    print('--- %s ---' % label)
    for r in data:
        rr = ref_rn.get(r['name'])
        if rr:
            re = fmt_euler(rr['rot'])
            ae = fmt_euler(r['rot'])
            match = 'MATCH' if ae == re else 'DIFF'
            print('  %-30s  ref=%s  res=%s  %s' % (r['name'], re, ae, match))
        else:
            print('  %-30s  (not in ref)' % r['name'])
