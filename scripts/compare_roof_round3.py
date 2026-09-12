import re, math

def parse_insts(path):
    c = open(path, encoding='utf-8').read()
    insts = []
    for m in re.finditer(r'--- !u!1001 &(\d+)\n(.*?)(?=--- !u!|\Z)', c, re.S):
        body = m.group(2)
        src = re.search(r'm_SourcePrefab:\s*\{fileID:\s*\d+,\s*guid:\s*([a-f0-9]+),\s*type:\s*\d+\}', body)
        mods = {}
        for mod in re.finditer(
            r'- target: \{fileID:\s*\d+,\s*guid:\s*[a-f0-9]+,\s*type:\s*\d+\}\s*\n'
            r'\s*propertyPath:\s*(.+?)\s*\n'
            r'\s*value:\s*(.*?)\s*\n'
            r'\s*objectReference:\s*\{fileID:\s*(\d+)\}',
            body):
            mods[mod.group(1).strip()] = mod.group(2).strip()
        insts.append({'fid': m.group(1), 'guid': src.group(1) if src else None, 'mods': mods})
    return insts

def parse_meshes(path):
    c = open(path, encoding='utf-8').read()
    out = {}
    for m in re.finditer(r'--- !u!43 &(\d+)\n(.*?)(?=--- !u!|\Z)', c, re.S):
        body = m.group(2)
        name = re.search(r'm_Name:\s*(.*?)\n', body).group(1).strip()
        aabb = re.search(r'localAABB:\s*\n\s*m_Center:\s*\{x:\s*([-\d.eE+]+),\s*y:\s*([-\d.eE+]+),\s*z:\s*([-\d.eE+]+)\}\s*\n\s*m_Extent:\s*\{x:\s*([-\d.eE+]+),\s*y:\s*([-\d.eE+]+),\s*z:\s*([-\d.eE+]+)\}', body)
        if aabb:
            out[name] = (tuple(float(v) for v in aabb.groups()[:3]), tuple(float(v) for v in aabb.groups()[3:]))
    return out

def euler(q):
    x,y,z,w = q
    sinr = 2*(w*x + y*z); cosr = 1 - 2*(x*x + y*y); roll = math.atan2(sinr, cosr)
    sinp = 2*(w*y - z*x); pitch = math.asin(max(-1,min(1,sinp)))
    siny = 2*(w*z + x*y); cosy = 1 - 2*(y*y + z*z); yaw = math.atan2(siny, cosy)
    return tuple(round(math.degrees(v),1) for v in (roll, pitch, yaw))

def fnum(s, d=0.0):
    try: return float(s)
    except: return d

paths = {
 'W1 4x6_722666': r'Assets\02_Shared\Prefabs\Building\Buildings_Modular_Complete\Residential\4x6\Residential_722666.prefab',
 'W2 3x6_155640': r'Assets\02_Shared\Prefabs\Building\Buildings_Modular_Complete\Residential\3x6\Residential_155640.prefab',
 'W3 3x5_504857': r'Assets\02_Shared\Prefabs\Building\Buildings_Modular_Complete\Residential\3x5\Residential_504857.prefab',
 'C1 3x5_640034': r'Assets\02_Shared\Prefabs\Building\Buildings_Modular_Complete\Residential\3x5\Residential_640034.prefab',
 'C2 3x5_722532': r'Assets\02_Shared\Prefabs\Building\Buildings_Modular_Complete\Residential\3x5\Residential_722532.prefab',
 'C3 6x6_822166': r'Assets\02_Shared\Prefabs\Building\Buildings_Modular_Complete\Residential\6x6\Residential_822166.prefab',
}

for label, p in paths.items():
    print('='*104)
    print(label)
    print('='*104)
    insts = parse_insts(p)
    meshes = parse_meshes(p)
    for i in insts:
        m = i['mods']
        name = m.get('m_Name')
        if not name or not name.startswith('Roof_'): continue
        pos = (fnum(m.get('m_LocalPosition.x')), fnum(m.get('m_LocalPosition.y')), fnum(m.get('m_LocalPosition.z')))
        rot = (fnum(m.get('m_LocalRotation.x')), fnum(m.get('m_LocalRotation.y')), fnum(m.get('m_LocalRotation.z')), fnum(m.get('m_LocalRotation.w'), 1.0))
        scl = (fnum(m.get('m_LocalScale.x'),1.0), fnum(m.get('m_LocalScale.y'),1.0), fnum(m.get('m_LocalScale.z'),1.0))
        aabb = meshes.get(name)
        aabb_s = 'ext=(%.3f,%.3f,%.3f) ct=(%.3f,%.3f,%.3f)' % (aabb[1]+aabb[0]) if aabb else 'no-mesh'
        print('  %-20s pos=(%8.3f,%8.3f,%8.3f)  rot=(%+.3f,%+.3f,%+.3f,%+.3f) E(%+.1f,%+.1f,%+.1f)  scl=(%.2f,%.2f,%.2f)  %s'
              % (name, pos[0],pos[1],pos[2], rot[0],rot[1],rot[2],rot[3], *euler(rot), scl[0],scl[1],scl[2], aabb_s))
