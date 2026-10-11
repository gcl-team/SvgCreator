# Contour tracing

**Goal:** turn each ink component into closed outline polygons: one outer border plus any holes.

Code: `src/SvgCreator/Tracing/ContourTracer.cs`

## Why not Suzuki–Abe

Suzuki–Abe border following (1985) is the algorithm behind OpenCV's `findContours`. It was the original plan. It finds outer borders, holes and how they nest in a single raster scan.

**Problem:** Suzuki–Abe returns contours that pass through the **centres of border pixels**. If you fill that polygon:
- every line loses half a pixel on each side, and
- a **1-px line has zero area and disappears completely.**

For line art, where thin lines are the whole point, that's fatal. So we trace **pixel edges** instead.

## Pixel-edge following ("crack following")

Contours run along the lattice of pixel *corners* (0..W, 0..H). Each boundary edge separates an ink pixel from a background pixel, and the filled polygon covers exactly the original ink.

### Walking rule

- Walk each boundary edge with **ink on the right** (directions E, S, W, N; y points down).
- At each lattice vertex, try **turning left first, then going straight, then turning right**. The first option that's a valid boundary edge wins.
- Preferring left treats ink pixels that touch diagonally as connected (8-connectivity), which matches the [labeler](Connected-Components.md).

An edge leaving vertex `V` in direction `d` is a boundary if the pixel on its right is ink and the pixel on its left isn't. Small offset tables per direction give those two pixels, so the check is a couple of array reads.

### Visiting every edge once

- A `visited` byte per pixel holds 4 bits, one per side.
- The main loop scans pixels in raster order and starts a new loop at every unvisited boundary edge.
- Each boundary edge is walked exactly once, so the total cost is O(pixels).
- Only **corner vertices**, where the direction changes, are recorded, which keeps the polygons compact.

### Telling outer borders from holes

Use the **signed area** (shoelace formula):

```
2A = Σ (x_i · y_{i+1} − x_{i+1} · y_i)
```

With ink on the right in y-down coordinates, outer borders go **clockwise** (A > 0) and holes go **counter-clockwise** (A < 0). There's no need to track which contour is nested inside which. The component label of the starting pixel says which component each loop belongs to.

### Why `fill-rule="evenodd"`

The SVG writer puts a component's outer border and holes into one `<path>`. With even-odd filling, a point inside an odd number of loops is filled, so holes come out empty automatically, whatever their winding direction.

## Test cases

| Shape | Expected |
|---|---|
| Filled square | 1 outer contour exactly on the pixel corners |
| Ring | 1 outer contour + 1 hole, both with the same label |
| 1×5 line | Signed area = 2 × 5, so the area is kept |
| Two pixels touching diagonally | One outer contour |
