import re
import sys

def parse_prefab_instances(path):
    """Extract all PrefabInstance blocks with their modifications."""
    with open(path, 'r', encoding='utf-8') as f:
        content = f.read()

    instances = {}
    blocks = re.split(r'--- !u!1001 &', content)
    for block in blocks:
        m = re.match(r'(\d+)', block)
        if not m:
            continue
        fid = m.group(1)

        # Find source prefab GUID
        src = re.search(r'm_SourcePrefab:\s*\{fileID:\s*\d+,\s*guid:\s*([a-f0-9]+),\s*type:\s*\d+\}', block)

        # Find transform parent
        parent = re.search(r'm_TransformParent:\s*\{fileID:\s*(\d+)\}', block)

        # Extract all modifications
        mods = []
        for mod in re.finditer(
            r'- target: \{fileID:\s*(\d+),\s*guid:\s*([a-f0-9]+),\s*type:\s*\d+\}\s*\n'
            r'\s*propertyPath:\s*(.+?)\s*\n'
            r'\s*value:\s*(.*?)\s*\n'
            r'\s*objectReference:\s*\{fileID:\s*(\d+)\}',
            block
        ):
            mods.append({
                'target': mod.group(1),
                'guid': mod.group(2),
                'property': mod.group(3).strip(),
                'value': mod.group(4).strip(),
                'objectRef': mod.group(5)
            })

        instances[fid] = {
            'fid': fid,
            'source_guid': src.group(1) if src else None,
            'parent_tf': parent.group(1) if parent else None,
            'mods': mods
        }

    return instances


def get_mod_value(mods, property_name, default=None):
    """Extract a specific property value from modifications."""
    for m in mods:
        if m['property'] == property_name:
            try:
                return float(m['value'])
            except ValueError:
                return m['value']
    return default


def extract_transform(mods):
    """Extract position, rotation, scale from modifications."""
    px = get_mod_value(mods, 'm_LocalPosition.x', 0)
    py = get_mod_value(mods, 'm_LocalPosition.y', 0)
    pz = get_mod_value(mods, 'm_LocalPosition.z', 0)
    rx = get_mod_value(mods, 'm_LocalRotation.x', 0)
    ry = get_mod_value(mods, 'm_LocalRotation.y', 0)
    rz = get_mod_value(mods, 'm_LocalRotation.z', 0)
    rw = get_mod_value(mods, 'm_LocalRotation.w', 1)
    sx = get_mod_value(mods, 'm_LocalScale.x', 1)
    sy = get_mod_value(mods, 'm_LocalScale.y', 1)
    sz = get_mod_value(mods, 'm_LocalScale.z', 1)
    name = get_mod_value(mods, 'm_Name')
    return {
        'name': name,
        'pos': (px, py, pz),
        'rot': (rx, ry, rz, rw),
        'scl': (sx, sy, sz)
    }


def build_tree(instances, parent_tf_fid, depth=0):
    """Recursively build tree of instances."""
    children = []
    for fid, inst in instances.items():
        if inst['parent_tf'] == parent_tf_fid:
            t = extract_transform(inst['mods'])
            subs = build_tree(instances, inst['fid'], depth + 1)
            children.append({
                'fid': fid,
                'name': t['name'],
                'pos': t['pos'],
                'rot': t['rot'],
                'scl': t['scl'],
                'guid': inst['source_guid'],
                'children': subs
            })
    return children


def print_tree(nodes, indent=0):
    for n in nodes:
        pos_str = '(%.3f,%.3f,%.3f)' % n['pos']
        rot_str = '(%.3f,%.3f,%.3f,%.3f)' % n['rot']
        scl_str = '(%.3f,%.3f,%.3f)' % n['scl']
        print('%s- %s  pos=%s rot=%s scl=%s  [%s]' % ('  ' * indent, n['name'], pos_str, rot_str, scl_str, n['guid'][:8] if n['guid'] else '?'))
        print_tree(n['children'], indent + 1)


def find_root(instances):
    """Find the root instance (no parent or parent not an instance)."""
    all_fids = set(instances.keys())
    child_fids = {inst['parent_tf'] for inst in instances.values() if inst['parent_tf']}
    root_candidates = child_fids - all_fids
    return root_candidates


def flatten_tree(nodes, prefix=''):
    """Flatten tree into list of (path, name, pos, rot, scl, guid)."""
    result = []
    for n in nodes:
        path = prefix + '/' + n['name'] if prefix else n['name']
        result.append((path, n['name'], n['pos'], n['rot'], n['scl'], n['guid']))
        result.extend(flatten_tree(n['children'], path))
    return result


if __name__ == '__main__':
    ref_path = r'Assets\02_Shared\Prefabs\Building\Buildings_Modular_Complete\reference.prefab'
    res_path = r'Assets\02_Shared\Prefabs\Building\Buildings_Modular_Complete\Residential_177970.prefab'

    ref_inst = parse_prefab_instances(ref_path)
    res_inst = parse_prefab_instances(res_path)

    # Find root containers
    ref_roots = find_root(ref_inst)
    res_roots = find_root(res_inst)
    print('Ref root parents:', ref_roots)
    print('Res root parents:', res_roots)

    # Build trees from all roots
    ref_trees = []
    for root in ref_roots:
        ref_trees.extend(build_tree(ref_inst, root))
    res_trees = []
    for root in res_roots:
        res_trees.extend(build_tree(res_inst, root))

    print('=== REFERENCE Roof Sub-hierarchy ===')
    for t in ref_trees:
        if 'Roof' in str(t):
            print_tree([t])

    print()
    print('=== RESIDENTIAL Roof Sub-hierarchy ===')
    for t in res_trees:
        if 'Roof' in str(t):
            print_tree([t])

    # Also print ALL trees to understand the full structure
    print()
    print('=== REFERENCE FULL TREE ===')
    print_tree(ref_trees)

    print()
    print('=== RESIDENTIAL FULL TREE ===')
    print_tree(res_trees)
