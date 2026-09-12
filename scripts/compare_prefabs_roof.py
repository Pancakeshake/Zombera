import re
import json

def parse_prefab(path):
    """Parse a Unity prefab YAML. Returns dict of fileID -> {name, transform, children}."""
    with open(path, 'r', encoding='utf-8') as f:
        content = f.read()

    gameobjects = {}
    transforms = {}

    # Find all GameObject blocks
    go_blocks = re.split(r'--- !u!1 &', content)
    for block in go_blocks:
        if not block.strip():
            continue
        fid_match = re.match(r'(\d+)', block)
        if not fid_match:
            continue
        fid = fid_match.group(1)
        name_match = re.search(r'm_Name:\s*(.+)', block)
        if name_match:
            gameobjects[fid] = {'name': name_match.group(1).strip(), 'transform_fid': None}
        # Find the transform component reference (first one is usually the Transform)
        comp_matches = re.findall(r'- component: \{fileID: (\d+)\}', block)
        if comp_matches and fid in gameobjects:
            gameobjects[fid]['transform_fid'] = comp_matches[0]

    # Find all Transform blocks
    tf_blocks = re.split(r'--- !u!4 &', content)
    for block in tf_blocks:
        if not block.strip():
            continue
        fid_match = re.match(r'(\d+)', block)
        if not fid_match:
            continue
        fid = fid_match.group(1)
        pos = re.search(r'm_LocalPosition:\s*\{x:\s*([-\d.e]+),\s*y:\s*([-\d.e]+),\s*z:\s*([-\d.e]+)\}', block)
        rot = re.search(r'm_LocalRotation:\s*\{x:\s*([-\d.e]+),\s*y:\s*([-\d.e]+),\s*z:\s*([-\d.e]+),\s*w:\s*([-\d.e]+)\}', block)
        sca = re.search(r'm_LocalScale:\s*\{x:\s*([-\d.e]+),\s*y:\s*([-\d.e]+),\s*z:\s*([-\d.e]+)\}', block)
        father = re.search(r'm_Father:\s*\{fileID:\s*(\d+)\}', block)
        children = re.findall(r'-\s*\{fileID:\s*(\d+)\}', block)
        transforms[fid] = {
            'position': (float(pos.group(1)), float(pos.group(2)), float(pos.group(3))) if pos else None,
            'rotation': (float(rot.group(1)), float(rot.group(2)), float(rot.group(3)), float(rot.group(4))) if rot else None,
            'scale': (float(sca.group(1)), float(sca.group(2)), float(sca.group(3))) if sca else None,
            'father': father.group(1) if father else None,
            'children': children
        }

    return gameobjects, transforms


def find_roof_objects(gameobjects, transforms):
    """Find all roof-related GOs and build their hierarchy path."""
    # Build name lookup
    tf_to_go = {}
    for fid, go in gameobjects.items():
        if go.get('transform_fid'):
            tf_to_go[go['transform_fid']] = fid

    # Build parent lookup from transform children
    parent_of = {}
    for fid, tf in transforms.items():
        for child in tf.get('children', []):
            parent_of[child] = fid

    def get_path(tf_fid):
        path = []
        current = tf_fid
        while current and current != '0':
            go_fid = tf_to_go.get(current)
            if go_fid and go_fid in gameobjects:
                path.append(gameobjects[go_fid]['name'])
            else:
                path.append('[tf:%s]' % current)
            current = parent_of.get(current)
        path.reverse()
        return '/'.join(path)

    results = []
    for fid, go in gameobjects.items():
        name = go['name'].lower()
        if 'roof' in name or 'truss' in name or 'rafter' in name or 'gable' in name:
            tf = go.get('transform')
            tf_fid = go.get('transform_fid')
            results.append({
                'name': go['name'],
                'path': get_path(tf_fid) if tf_fid else '?',
                'position': tf['position'] if tf else None,
                'rotation': tf['rotation'] if tf else None,
                'scale': tf['scale'] if tf else None,
                'fid': fid,
                'tf_fid': tf_fid
            })
    return results


# Also get full hierarchy for all objects to compare structure
def get_full_hierarchy(gameobjects, transforms):
    tf_to_go = {}
    for fid, go in gameobjects.items():
        if go.get('transform_fid'):
            tf_to_go[go['transform_fid']] = fid

    parent_of = {}
    for fid, tf in transforms.items():
        for child in tf.get('children', []):
            parent_of[child] = fid

    def get_path(tf_fid):
        path = []
        current = tf_fid
        visited = set()
        while current and current != '0':
            if current in visited:
                break
            visited.add(current)
            go_fid = tf_to_go.get(current)
            if go_fid and go_fid in gameobjects:
                path.append(gameobjects[go_fid]['name'])
            else:
                path.append('[tf:%s]' % current)
            current = parent_of.get(current)
        path.reverse()
        return '/'.join(path)

    all_objects = []
    for fid, go in gameobjects.items():
        tf = go.get('transform')
        tf_fid = go.get('transform_fid')
        all_objects.append({
            'name': go['name'],
            'path': get_path(tf_fid) if tf_fid else '?',
            'position': tf['position'] if tf else None,
            'rotation': tf['rotation'] if tf else None,
            'scale': tf['scale'] if tf else None,
        })
    return all_objects


if __name__ == '__main__':
    import sys

    ref_go, ref_tf = parse_prefab(r'Assets\02_Shared\Prefabs\Building\Buildings_Modular_Complete\reference.prefab')
    res_go, res_tf = parse_prefab(r'Assets\02_Shared\Prefabs\Building\Buildings_Modular_Complete\Residential_177970.prefab')

    ref_roofs = find_roof_objects(ref_go, ref_tf)
    res_roofs = find_roof_objects(res_go, res_tf)

    print('=== REFERENCE Roof Objects (%d found) ===' % len(ref_roofs))
    for r in ref_roofs:
        pos_str = '(%.3f, %.3f, %.3f)' % r['position'] if r['position'] else 'None'
        rot_str = '(%.6f, %.6f, %.6f, %.6f)' % r['rotation'] if r['rotation'] else 'None'
        scl_str = '(%.3f, %.3f, %.3f)' % r['scale'] if r['scale'] else 'None'
        print('  %-40s pos=%s rot=%s scl=%s' % (r['name'], pos_str, rot_str, scl_str))
        print('    path: %s' % r['path'])

    print()
    print('=== RESIDENTIAL Roof Objects (%d found) ===' % len(res_roofs))
    for r in res_roofs:
        pos_str = '(%.3f, %.3f, %.3f)' % r['position'] if r['position'] else 'None'
        rot_str = '(%.6f, %.6f, %.6f, %.6f)' % r['rotation'] if r['rotation'] else 'None'
        scl_str = '(%.3f, %.3f, %.3f)' % r['scale'] if r['scale'] else 'None'
        print('  %-40s pos=%s rot=%s scl=%s' % (r['name'], pos_str, rot_str, scl_str))
        print('    path: %s' % r['path'])

    print()
    print('=== FULL HIERARCHY COMPARISON ===')

    ref_all = get_full_hierarchy(ref_go, ref_tf)
    res_all = get_full_hierarchy(res_go, res_tf)

    # Build name-based comparison
    ref_by_name = {}
    for o in ref_all:
        n = o['name']
        if n not in ref_by_name:
            ref_by_name[n] = []
        ref_by_name[n].append(o)

    res_by_name = {}
    for o in res_all:
        n = o['name']
        if n not in res_by_name:
            res_by_name[n] = []
        res_by_name[n].append(o)

    all_names = sorted(set(list(ref_by_name.keys()) + list(res_by_name.keys())))

    # Filter to structural names (ignore RoomVis, FloorFill, etc.)
    structural_keywords = ['Roof', 'roof', 'Wall', 'wall', 'Floor', 'floor',
                           'Stair', 'stair', 'Door', 'door', 'Window', 'window',
                           'Truss', 'truss', 'Gable', 'gable', 'Rafter', 'rafter',
                           'Socket', 'socket', 'Foundation', 'foundation']

    diffs = []
    for name in all_names:
        is_structural = any(kw in name for kw in structural_keywords)
        if not is_structural:
            continue
        ref_objs = ref_by_name.get(name, [])
        res_objs = res_by_name.get(name, [])
        if not ref_objs:
            diffs.append('ONLY IN RESIDENTIAL: %s (count=%d)' % (name, len(res_objs)))
        elif not res_objs:
            diffs.append('ONLY IN REFERENCE:   %s (count=%d)' % (name, len(ref_objs)))
        elif len(ref_objs) != len(res_objs):
            diffs.append('COUNT MISMATCH:       %s (ref=%d, res=%d)' % (name, len(ref_objs), len(res_objs)))
        else:
            for i in range(len(ref_objs)):
                r = ref_objs[i]
                s = res_objs[i]
                if r['position'] != s['position']:
                    diffs.append('POSITION DIFF: %-30s ref=%s res=%s' % (name, r['position'], s['position']))
                if r['rotation'] != s['rotation']:
                    diffs.append('ROTATION DIFF: %-30s ref=%s res=%s' % (name, r['rotation'], s['rotation']))
                if r['scale'] != s['scale']:
                    diffs.append('SCALE DIFF:    %-30s ref=%s res=%s' % (name, r['scale'], s['scale']))

    if diffs:
        print('\n  Structural differences found:')
        for d in diffs:
            print('    %s' % d)
    else:
        print('  No structural differences found.')

    # Also print the full hierarchy tree for reference
    print()
    print('=== REFERENCE Hierarchy Tree (structural only) ===')
    for o in ref_all:
        n = o['name']
        if any(kw in n for kw in structural_keywords) or n in ['Floors', 'Walls', 'Stairs', 'Roof', 'Rooms', 'L0', 'L1', 'L2']:
            pos_str = '(%.3f,%.3f,%.3f)' % o['position'] if o['position'] else '-'
            print('  %s  @ %s' % (o['path'], pos_str))

    print()
    print('=== RESIDENTIAL Hierarchy Tree (structural only) ===')
    for o in res_all:
        n = o['name']
        if any(kw in n for kw in structural_keywords) or n in ['Floors', 'Walls', 'Stairs', 'Roof', 'Rooms', 'L0', 'L1', 'L2']:
            pos_str = '(%.3f,%.3f,%.3f)' % o['position'] if o['position'] else '-'
            print('  %s  @ %s' % (o['path'], pos_str))
