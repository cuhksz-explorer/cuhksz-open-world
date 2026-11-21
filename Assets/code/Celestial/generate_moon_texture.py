import struct
import zlib
import math
import os

def write_png(path, w, h, pixels):
    def chunk(tag, data):
        return struct.pack('>I', len(data)) + tag + data + struct.pack('>I', zlib.crc32(tag + data) & 0xffffffff)

    raw = b''
    for y in range(h):
        raw += b'\x00' + pixels[y * w * 3 : (y + 1) * w * 3]

    ihdr = struct.pack('>IIBBBBB', w, h, 8, 2, 0, 0, 0)
    with open(path, 'wb') as f:
        f.write(b'\x89PNG\r\n\x1a\n')
        f.write(chunk(b'IHDR', ihdr))
        f.write(chunk(b'IDAT', zlib.compress(raw, 9)))
        f.write(chunk(b'IEND', b''))


def moon_pixel(u, v):
    x = (u - 0.5) * 2
    y = (v - 0.5) * 2
    r = math.sqrt(x * x + y * y)
    if r > 1:
        return (0, 0, 0)

    n = math.sin(x * 8 + y * 5) * 0.5 + math.sin(x * 13 - y * 9) * 0.3 + math.sin(x * 21 + y * 17) * 0.2
    mare = 0.55 + 0.15 * math.sin(x * 3.2 + y * 2.1 + n)
    crater = 0.0
    craters = [
        (-0.35, 0.25, 0.12, 0.18), (0.2, 0.35, 0.09, 0.15), (0.45, -0.1, 0.14, 0.2),
        (-0.15, -0.35, 0.11, 0.16), (0.05, 0.05, 0.18, 0.22), (-0.5, -0.15, 0.08, 0.12),
        (0.35, -0.4, 0.1, 0.14), (-0.25, 0.45, 0.07, 0.11), (0.55, 0.3, 0.06, 0.1),
    ]
    for cx, cy, rad, depth in craters:
        d = math.sqrt((x - cx) ** 2 + (y - cy) ** 2)
        if d < rad:
            rim = abs(d - rad * 0.85) < rad * 0.08
            crater = max(crater, depth * (1 - d / rad) + (0.08 if rim else 0))

    val = mare - crater + n * 0.04
    val = max(0.12, min(0.95, val))
    return (int(val * 220), int(val * 225), int(val * 235))


if __name__ == '__main__':
    w = h = 512
    pixels = bytearray()
    for j in range(h):
        for i in range(w):
            r, g, b = moon_pixel(i / w, j / h)
            pixels.extend([r, g, b])

    out = os.path.join(os.path.dirname(__file__), 'Textures', 'MoonSurface.png')
    os.makedirs(os.path.dirname(out), exist_ok=True)
    write_png(out, w, h, pixels)
    print('Wrote', out)
