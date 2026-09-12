import UnityPy, collections


def analyze(path, label):
    print(f'===== {label} =====', flush=True)
    env = UnityPy.load(path)
    classes = collections.Counter()
    failures = 0
    for obj in env.objects:
        if obj.type.name != 'MonoBehaviour':
            continue
        try:
            d = obj.read_typetree()
        except Exception as e:
            failures += 1
            print('READ FAIL MB', obj.path_id, type(e).__name__, str(e)[:150], flush=True)
            continue
        cid = d.get('m_EditorClassIdentifier') or '<none>'
        go = d.get('m_GameObject', {})
        classes[(cid, go.get('m_FileID'))] += 1
    print(f'  (mono read failures: {failures})')
    for (cid, fid), c in sorted(classes.items()):
        print(f'  {cid} [fid={fid}]: {c}')
    gonames = collections.Counter()
    for obj in env.objects:
        if obj.type.name != 'GameObject':
            continue
        try:
            d = obj.read_typetree()
            gonames[d.get('m_Name')] += 1
        except Exception as e:
            print('READ FAIL GO', obj.path_id, type(e).__name__, str(e)[:150], flush=True)
    special = {n: c for n, c in gonames.items() if n in ('MapMagic', 'Citygen', 'Road Network')}
    print('  special GOs:', special)


if __name__ == '__main__':
    analyze('Assets/00_Scenes/01_Backups/7.CItygen Backup 2407.unity', 'BACKUP-2407')
    analyze('Assets/00_Scenes/02_System Dev Scenes/7.CItygen World.unity', 'CITYGEN-WORLD')
