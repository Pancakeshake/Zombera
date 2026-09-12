import re
import sys

def analyze(path):
    names = {}
    transforms = []
    current_go = None
    current_go_name = None
    in_go = False
    in_tf = False
    tf_fileid = None
    tf_fields = {}

    with open(path, "r", encoding="utf-8") as fh:
        lines = fh.readlines()

    i = 0
    while i < len(lines):
        line = lines[i].rstrip("\n")
        m = re.match(r"^--- !u!(\d+) &(-?\d+)", line)
        if m:
            kind, fid = m.group(1), m.group(2)
            in_go = kind == "1"
            in_tf = kind == "4"
            if in_tf:
                tf_fileid = fid
                tf_fields = {}
            continue
        if in_go:
            m2 = re.match(r"^  m_Name: (.*)", line)
            if m2:
                names.setdefault("go", {})[None] = None  # noop
                current_go_name = m2.group(1)
        if in_tf:
            m3 = re.match(r"^  m_GameObject: \{fileID: (-?\d+)", line)
            if m3:
                tf_fields["go"] = m3.group(1)
            m4 = re.match(r"^  m_LocalPosition: \{x: ([-\d.eE]+), y: ([-\d.eE]+), z: ([-\d.eE]+)\}", line)
            if m4:
                tf_fields["pos"] = (m4.group(1), m4.group(2), m4.group(3))
            m5 = re.match(r"^  m_LocalRotation: \{x: ([-\d.eE]+), y: ([-\d.eE]+), z: ([-\d.eE]+), w: ([-\d.eE]+)\}", line)
            if m5:
                tf_fields["rot"] = (m5.group(1), m5.group(2), m5.group(3), m5.group(4))
            m6 = re.match(r"^  m_LocalEulerAnglesHint: \{x: ([-\d.eE]+), y: ([-\d.eE]+), z: ([-\d.eE]+)\}", line)
            if m6:
                tf_fields["eul"] = (m6.group(1), m6.group(2), m6.group(3))
            if "go" in tf_fields and "pos" in tf_fields and "rot" in tf_fields:
                transforms.append((tf_fileid, dict(tf_fields)))
                in_tf = False
        i += 1

    # second pass to map go id -> name: collect GameObject fileID -> name
    go_names = {}
    in_go = False
    go_id = None
    for line in lines:
        m = re.match(r"^--- !u!1 &(-?\d+)", line)
        if m:
            in_go = True
            go_id = m.group(1)
            continue
        if in_go and re.match(r"^--- !u!", line):
            in_go = False
            continue
        if in_go:
            m2 = re.match(r"^  m_Name: (.*)", line)
            if m2:
                go_names[go_id] = m2.group(1)

    for fid, tf in transforms:
        name = go_names.get(tf["go"], "?")
        if name.startswith("Roof_"):
            print(f"{name:22s} pos={tf['pos']} rot(xyz)={tf['rot'][:3]} eul={tf.get('eul','-')}")


if __name__ == "__main__":
    for p in sys.argv[1:]:
        print("=====", p)
        analyze(p)
