import re
from collections import defaultdict

ROOF_GUIDS = {
    'efd0fc91f89bebd4c81cd98e29af788f': 'Shed_Panel',
    '87cfdc1b': 'Shed_Gable',
    '01863bc4': 'Wall_Generic',
    'cca99ed7': 'Wall_Window',
    '03988a35': 'DoorFrame',
    'ba907d7a': 'InteriorDoor',
    '50f132eb': 'ExteriorDoor',
    'a2c138a9': 'ExteriorStairs',
    '2a8c6df1': 'FuseBox',
    '79fd69a5': 'Window_Closed',
    '113f87fa': 'Window_Moulding',
    '272146bd': 'InteriorDoor_v2',
    'fca78a0b': 'ExteriorDoor_v2',
}

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
            try:
                val = float(val)
            except ValueError:
                pass
            mods.append({'property': prop, 'value': val})
        instances[fid] = {
            'fid': fid,
            'guid': src.group(1) if src else None,
            'parent_tf': parent.group(1) if parent else None,
            'mods': mods
        }
    return instances

def get_mod(mods, prop, default=0.0):
    for m in mods:
        if m['property'] == prop:
            return m['value']
    return default

def extract_all(instances):
    """Extract all instances with name, pos, rot, scl, guid."""
    results = []
    for fid, inst in instances.items():
        name = get_mod(inst['mods'], 'm_Name', 'UNNAMED')
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
        results.append({
            'name': name,
            'pos': (px, py, pz),
            'rot': (rx, ry, rz, rw),
            'scl': (sx, sy, sz),
            'guid': inst['guid'],
            'parent_tf': inst['parent_tf'],
            'fid': fid
        })
    return results

def find_roof_children(instances, roof_tf_fid):
    """Find all instances parented under the Roof container (recursively)."""
    direct = [inst for inst in instances if inst['parent_tf'] == roof_tf_fid]
    return direct

def find_container_tf(instances, name_filter):
    """Find the transform fileID of a container by its name (e.g., 'Roof', 'Floors')."""
    for inst in instances:
        if name_filter in inst['name']:
            return inst['fid']
    return None


if __name__ == '__main__':
    ref_path = r'Assets\02_Shared\Prefabs\Building\Buildings_Modular_Complete\reference.prefab'
    res_path = r'Assets\02_Shared\Prefabs\Building\Buildings_Modular_Complete\Residential_551908.prefab'

    ref_inst = parse_prefab_instances(ref_path)
    res_inst = parse_prefab_instances(res_path)

    ref_all = extract_all(ref_inst)
    res_all = extract_all(res_inst)

    # Find Roof container tf_fid
    ref_roof_tf = find_container_tf(ref_all, 'Roof')
    res_roof_tf = find_container_tf(res_all, 'Roof')
    print('Ref Roof tf_fid:', ref_roof_tf)
    print('Res Roof tf_fid:', res_roof_tf)

    ref_roof_children = find_roof_children(ref_all, ref_roof_tf)
    res_roof_children = find_roof_children(res_all, res_roof_tf)

    print('\n=== REFERENCE: Children parented to Roof ===')
    for c in ref_roof_children:
        gname = ROOF_GUIDS.get(c['guid'][:8], c['guid'][:8])
        print('  %-35s pos=(%.3f,%.3f,%.3f) rot=(%.3f,%.3f,%.3f,%.3f) scl=(%.3f,%.3f,%.3f) [%s]' % (
            c['name'], c['pos'][0], c['pos'][1], c['pos'][2],
            c['rot'][0], c['rot'][1], c['rot'][2], c['rot'][3],
            c['scl'][0], c['scl'][1], c['scl'][2], gname))

    print('\n=== RESIDENTIAL: Children parented to Roof ===')
    for c in res_roof_children:
        gname = ROOF_GUIDS.get(c['guid'][:8], c['guid'][:8])
        print('  %-35s pos=(%.3f,%.3f,%.3f) rot=(%.3f,%.3f,%.3f,%.3f) scl=(%.3f,%.3f,%.3f) [%s]' % (
            c['name'], c['pos'][0], c['pos'][1], c['pos'][2],
            c['rot'][0], c['rot'][1], c['rot'][2], c['rot'][3],
            c['scl'][0], c['scl'][1], c['scl'][2], gname))

    # Now print ALL structural elements sorted by name
    print('\n\n=== SIDE-BY-SIDE COMPARISON (by name) ===')
    ref_by_name = defaultdict(list)
    for c in ref_all:
        ref_by_name[c['name']].append(c)
    res_by_name = defaultdict(list)
    for c in res_all:
        res_by_name[c['name']].append(c)

    all_names = sorted(set(list(ref_by_name.keys()) + list(res_by_name.keys())))
    for name in all_names:
        ref_items = ref_by_name.get(name, [])
        res_items = res_by_name.get(name, [])
        if not ref_items:
            for item in res_items:
                print('  ONLY RES: %-35s pos=(%.3f,%.3f,%.3f) rot=(%.3f,%.3f,%.3f,%.3f)' % (
                    name, item['pos'][0], item['pos'][1], item['pos'][2],
                    item['rot'][0], item['rot'][1], item['rot'][2], item['rot'][3]))
        elif not res_items:
            for item in ref_items:
                print('  ONLY REF: %-35s pos=(%.3f,%.3f,%.3f) rot=(%.3f,%.3f,%.3f,%.3f)' % (
                    name, item['pos'][0], item['pos'][1], item['pos'][2],
                    item['rot'][0], item['rot'][1], item['rot'][2], item['rot'][3]))
        elif len(ref_items) == len(res_items):
            for i in range(len(ref_items)):
                r = ref_items[i]
                s = res_items[i]
                diffs = []
                if r['pos'] != s['pos']:
                    diffs.append('POS: ref=(%.3f,%.3f,%.3f) res=(%.3f,%.3f,%.3f)' % (r['pos']+s['pos']))
                if r['rot'] != s['rot']:
                    diffs.append('ROT: ref=(%.3f,%.3f,%.3f,%.3f) res=(%.3f,%.3f,%.3f,%.3f)' % (r['rot']+s['rot']))
                if r['scl'] != s['scl']:
                    diffs.append('SCL: ref=(%.3f,%.3f,%.3f) res=(%.3f,%.3f,%.3f)' % (r['scl']+s['scl']))
                if diffs:
                    print('  DIFF %-35s %s' % (name, ' | '.join(diffs)))
        else:
            print('  COUNT: %-35s ref=%d res=%d' % (name, len(ref_items), len(res_items)))
