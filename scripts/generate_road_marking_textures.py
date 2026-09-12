import struct
import zlib
import os

def write_png(path, width, height, pixels_rgba):
    """Write a minimal RGBA PNG file."""
    def chunk(chunk_type, data):
        c = chunk_type + data
        crc = struct.pack('>I', zlib.crc32(c) & 0xffffffff)
        return struct.pack('>I', len(data)) + c + crc

    # PNG signature
    sig = b'\x89PNG\r\n\x1a\n'
    # IHDR
    ihdr = struct.pack('>IIBBBBB', width, height, 8, 6, 0, 0, 0)  # 8-bit RGBA
    # IDAT: raw rows with filter byte 0
    raw = b''
    for y in range(height):
        raw += b'\x00'  # filter: None
        for x in range(width):
            raw += bytes(pixels_rgba[y * width + x])
    idat = zlib.compress(raw)

    data = sig + chunk(b'IHDR', ihdr) + chunk(b'IDAT', idat) + chunk(b'IEND', b'')
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, 'wb') as f:
        f.write(data)
    print(f"Wrote {path} ({width}x{height}, {len(data)} bytes)")

# --- Stop Line: thin white bar, soft alpha edges ---
SW, SH = 64, 4
stop = []
for y in range(SH):
    for x in range(SW):
        # White with soft fade at left/right edges
        edge_dist = min(x, SW - 1 - x)
        a = min(255, edge_dist * 64) if edge_dist < 4 else 255
        # Also fade top/bottom
        edge_dist_y = min(y, SH - 1 - y)
        a = min(a, min(255, edge_dist_y * 128) if edge_dist_y < 2 else 255)
        stop.append((255, 255, 255, a))

write_png('Assets/Shared/Textures/RoadMarkings/RoadMarking_StopLine.png', SW, SH, stop)

# --- Crosswalk: zebra stripes with alpha ---
CW, CH = 64, 8
STRIPE = 8
crosswalk = []
for y in range(CH):
    for x in range(CW):
        stripe_idx = x // STRIPE
        if stripe_idx % 2 == 0:
            crosswalk.append((255, 255, 255, 255))  # white stripe
        else:
            crosswalk.append((255, 255, 255, 0))    # transparent gap

write_png('Assets/Shared/Textures/RoadMarkings/RoadMarking_Crosswalk.png', CW, CH, crosswalk)
