#!/usr/bin/env node
/**
 * Rasterises the Everdue mark into the formats that will not take an SVG: the iOS home screen, the
 * Android installed-app icon, and the .ico that old favicons still ask for.
 *
 * The geometry below is the same as public/favicon.svg, expressed as signed-distance tests instead of
 * paths, so there is no rasteriser dependency and the output is byte-for-byte reproducible. Edit the
 * SVG and this file together (and src/components/BrandMark.tsx, which draws the in-app logo).
 *
 *   node scripts/gen-icons.mjs
 */

import { deflateSync } from 'node:zlib';
import { writeFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const publicDir = join(dirname(fileURLToPath(import.meta.url)), '..', 'public');

// ── Geometry, on the 32-unit grid the SVG uses ────────────────────────────────────────────────────

const GRID = 32;
const TILE_RADIUS = 7.5;

/** The open ring: 300° of track, the gap at the top right. */
const RING = { cx: 16, cy: 16, r: 10.2, half: 3.1 / 2, from: -10, to: 290 };

/** The marker sitting in the gap, on the same track. */
const MARKER = { cx: 23.81, cy: 9.44, r: 2 };

/** The check, as two round-capped segments. */
const CHECK = {
  points: [
    [12, 16.9],
    [14.9, 19.8],
    [20.4, 12.9],
  ],
  half: 2.9 / 2,
};

const GRADIENT_FROM = [0x4c, 0x6e, 0xf5];
const GRADIENT_TO = [0x0c, 0xa6, 0x78];
const INK = [0xff, 0xff, 0xff];
const MARKER_INK = [0xff, 0xd4, 0x3b];

// ── Distance fields ───────────────────────────────────────────────────────────────────────────────

function insideRoundedSquare(x, y, radius) {
  if (radius <= 0) return x >= 0 && y >= 0 && x <= GRID && y <= GRID;

  const half = GRID / 2;
  const qx = Math.abs(x - half) - (half - radius);
  const qy = Math.abs(y - half) - (half - radius);

  return Math.hypot(Math.max(qx, 0), Math.max(qy, 0)) + Math.min(Math.max(qx, qy), 0) <= radius;
}

function polar(cx, cy, r, degrees) {
  const radians = (degrees * Math.PI) / 180;
  return [cx + r * Math.cos(radians), cy + r * Math.sin(radians)];
}

/** Distance to an arc's centre line, with the round caps its stroke-linecap implies. */
function arcDistance(x, y, arc) {
  const distance = Math.hypot(x - arc.cx, y - arc.cy);
  const angle = (Math.atan2(y - arc.cy, x - arc.cx) * 180) / Math.PI;

  let relative = (angle - arc.from) % 360;
  if (relative < 0) relative += 360;

  if (relative <= arc.to - arc.from) return Math.abs(distance - arc.r);

  const [ax, ay] = polar(arc.cx, arc.cy, arc.r, arc.from);
  const [bx, by] = polar(arc.cx, arc.cy, arc.r, arc.to);

  return Math.min(Math.hypot(x - ax, y - ay), Math.hypot(x - bx, y - by));
}

function segmentDistance(x, y, [ax, ay], [bx, by]) {
  const dx = bx - ax;
  const dy = by - ay;
  const length = dx * dx + dy * dy;
  const t = length === 0 ? 0 : Math.min(1, Math.max(0, ((x - ax) * dx + (y - ay) * dy) / length));

  return Math.hypot(x - (ax + t * dx), y - (ay + t * dy));
}

function checkDistance(x, y) {
  const [a, b, c] = CHECK.points;
  return Math.min(segmentDistance(x, y, a, b), segmentDistance(x, y, b, c));
}

// ── Rendering ─────────────────────────────────────────────────────────────────────────────────────

/**
 * @param size      output edge in pixels
 * @param radius    corner radius on the 32-unit grid; 0 renders full-bleed (what a maskable icon and
 *                  the iOS home screen want, since both apply their own mask)
 * @param glyphScale shrinks the ring/check/marker about the centre, to leave a maskable safe zone
 */
function render(size, { radius = TILE_RADIUS, glyphScale = 1, samples = 4 } = {}) {
  const pixels = Buffer.alloc(size * size * 4);
  const step = GRID / size;

  for (let py = 0; py < size; py += 1) {
    for (let px = 0; px < size; px += 1) {
      let r = 0;
      let g = 0;
      let b = 0;
      let a = 0;

      for (let sy = 0; sy < samples; sy += 1) {
        for (let sx = 0; sx < samples; sx += 1) {
          const x = (px + (sx + 0.5) / samples) * step;
          const y = (py + (sy + 0.5) / samples) * step;

          if (!insideRoundedSquare(x, y, radius)) continue;

          const gx = 16 + (x - 16) / glyphScale;
          const gy = 16 + (y - 16) / glyphScale;

          const t = Math.min(1, Math.max(0, (x + y) / (2 * GRID)));
          let colour = GRADIENT_FROM.map((from, index) =>
            Math.round(from + (GRADIENT_TO[index] - from) * t),
          );

          if (arcDistance(gx, gy, RING) <= RING.half) colour = INK;
          else if (Math.hypot(gx - MARKER.cx, gy - MARKER.cy) <= MARKER.r) colour = MARKER_INK;
          else if (checkDistance(gx, gy) <= CHECK.half) colour = INK;

          r += colour[0];
          g += colour[1];
          b += colour[2];
          a += 1;
        }
      }

      const total = samples * samples;
      const offset = (py * size + px) * 4;

      // Averaged over covered subsamples only, so the anti-aliased edge keeps the tile's colour
      // rather than fading through black.
      if (a > 0) {
        pixels[offset] = Math.round(r / a);
        pixels[offset + 1] = Math.round(g / a);
        pixels[offset + 2] = Math.round(b / a);
        pixels[offset + 3] = Math.round((a / total) * 255);
      }
    }
  }

  return pixels;
}

// ── PNG / ICO containers ──────────────────────────────────────────────────────────────────────────

const CRC_TABLE = Array.from({ length: 256 }, (_, index) => {
  let c = index;
  for (let bit = 0; bit < 8; bit += 1) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
  return c >>> 0;
});

function crc32(buffer) {
  let c = 0xffffffff;
  for (const byte of buffer) c = CRC_TABLE[(c ^ byte) & 0xff] ^ (c >>> 8);
  return (c ^ 0xffffffff) >>> 0;
}

function chunk(type, data) {
  const length = Buffer.alloc(4);
  length.writeUInt32BE(data.length);

  const body = Buffer.concat([Buffer.from(type, 'ascii'), data]);
  const crc = Buffer.alloc(4);
  crc.writeUInt32BE(crc32(body));

  return Buffer.concat([length, body, crc]);
}

function png(size, pixels) {
  const header = Buffer.alloc(13);
  header.writeUInt32BE(size, 0);
  header.writeUInt32BE(size, 4);
  header[8] = 8; // bit depth
  header[9] = 6; // truecolour with alpha

  // One filter byte per scanline, filter type 0 (none): the images are small and tiny enough
  // deflated that choosing filters would buy nothing.
  const raw = Buffer.alloc((size * 4 + 1) * size);
  for (let row = 0; row < size; row += 1) {
    pixels.copy(raw, row * (size * 4 + 1) + 1, row * size * 4, (row + 1) * size * 4);
  }

  return Buffer.concat([
    Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
    chunk('IHDR', header),
    chunk('IDAT', deflateSync(raw, { level: 9 })),
    chunk('IEND', Buffer.alloc(0)),
  ]);
}

/** A PNG-in-ICO, which every browser and Windows since Vista reads. */
function ico(images) {
  const header = Buffer.alloc(6);
  header.writeUInt16LE(1, 2);
  header.writeUInt16LE(images.length, 4);

  let offset = 6 + images.length * 16;
  const entries = images.map(({ size, data }) => {
    const entry = Buffer.alloc(16);
    entry[0] = size >= 256 ? 0 : size;
    entry[1] = size >= 256 ? 0 : size;
    entry.writeUInt16LE(1, 4); // colour planes
    entry.writeUInt16LE(32, 6); // bits per pixel
    entry.writeUInt32LE(data.length, 8);
    entry.writeUInt32LE(offset, 12);
    offset += data.length;
    return entry;
  });

  return Buffer.concat([header, ...entries, ...images.map((image) => image.data)]);
}

// ── Output ────────────────────────────────────────────────────────────────────────────────────────

function write(name, data) {
  writeFileSync(join(publicDir, name), data);
  console.log(`${name} — ${(data.length / 1024).toFixed(1)} kB`);
}

write('icon-192.png', png(192, render(192)));
write('icon-512.png', png(512, render(512)));

// Full-bleed: iOS rounds the corners itself, and a maskable icon may be cropped to a circle, so the
// glyph shrinks to sit inside the 80% safe zone.
write('apple-touch-icon.png', png(180, render(180, { radius: 0, glyphScale: 0.88 })));
write('icon-maskable-512.png', png(512, render(512, { radius: 0, glyphScale: 0.72 })));

write(
  'favicon.ico',
  ico(
    [16, 32, 48].map((size) => ({
      size,
      // Corner radius scaled down a touch at favicon sizes, where a 7.5/32 radius reads as a blob.
      data: png(size, render(size, { radius: size <= 16 ? 5.5 : 6.5, samples: 6 })),
    })),
  ),
);
