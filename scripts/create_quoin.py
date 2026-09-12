"""Create a quoin (corner stone) mesh for Zombera modular buildings."""
import bpy
import bmesh
from mathutils import Vector

# ── Delete existing Quoin ──
for obj_name in ["Quoin"]:
    obj = bpy.data.objects.get(obj_name)
    if obj:
        bpy.data.objects.remove(obj, do_unlink=True)

# ── Build quoin mesh ──
mesh = bpy.data.meshes.new("Quoin_Mesh")
obj = bpy.data.objects.new("Quoin", mesh)
bpy.context.collection.objects.link(obj)
bpy.context.view_layer.objects.active = obj
obj.select_set(True)

bm = bmesh.new()

W = 0.30   # width along X wall face
D = 0.30   # depth along Z wall face
H = 3.00   # full wall height (3m)
hw, hd = W / 2, D / 2
long_h = 0.35   # tall block
short_h = 0.20  # short block
core_w = 0.10   # inner corner core thickness


def add_box(cx, cy, cz, sx, sy, sz):
    """Add a box at (cx,cy,cz) with half-extents (sx,sy,sz)."""
    v = [
        bm.verts.new((cx - sx, cy - sy, cz - sz)),
        bm.verts.new((cx + sx, cy - sy, cz - sz)),
        bm.verts.new((cx + sx, cy + sy, cz - sz)),
        bm.verts.new((cx - sx, cy + sy, cz - sz)),
        bm.verts.new((cx - sx, cy - sy, cz + sz)),
        bm.verts.new((cx + sx, cy - sy, cz + sz)),
        bm.verts.new((cx + sx, cy + sy, cz + sz)),
        bm.verts.new((cx - sx, cy + sy, cz + sz)),
    ]
    bm.verts.ensure_lookup_table()
    faces = [
        (v[0], v[1], v[2], v[3]),
        (v[4], v[7], v[6], v[5]),
        (v[0], v[4], v[5], v[1]),
        (v[3], v[2], v[6], v[7]),
        (v[0], v[3], v[7], v[4]),
        (v[1], v[5], v[6], v[2]),
    ]
    for f in faces:
        bm.faces.new(f)


# Build alternating blocks from bottom to top
y = 0.0
block_idx = 0
while y < H:
    h = long_h if block_idx % 2 == 0 else short_h
    if y + h > H:
        h = H - y  # clip last block

    # Block on +X face (protrudes outward from wall)
    bx_cx = -hw + core_w + (W - core_w) / 2
    add_box(bx_cx, y + h / 2, -hd + core_w / 2,
            (W - core_w) / 2, h / 2, core_w / 2)

    # Block on +Z face
    bz_cz = -hd + core_w + (D - core_w) / 2
    add_box(-hw + core_w / 2, y + h / 2, bz_cz,
            core_w / 2, h / 2, (D - core_w) / 2)

    y += h
    block_idx += 1

# Shift so inner corner sits at origin (bottom-center pivot)
for v in bm.verts:
    v.co.x += hw
    v.co.z += hd

bm.to_mesh(mesh)
bm.free()

# ── Stone material ──
mat = bpy.data.materials.new(name="M_Quoin_Stone")
mat.use_nodes = True
nodes = mat.node_tree.nodes
nodes.clear()
bsdf = nodes.new('ShaderNodeBsdfPrincipled')
bsdf.inputs['Base Color'].default_value = (0.85, 0.82, 0.75, 1.0)  # warm limestone
bsdf.inputs['Roughness'].default_value = 0.55
out = nodes.new('ShaderNodeOutputMaterial')
mat.node_tree.links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])
mesh.materials.append(mat)

print(f"Quoin created: {len(mesh.vertices)}v, {len(mesh.polygons)}f, "
      f"height={H:.2f}m, {block_idx} blocks, origin=bottom-center")
