# Connected components

**Goal:** group touching ink pixels into separate *components* (blobs), so that:
- specks below `MinComponentArea` can be dropped as noise, and
- each component's outer border and holes can be written as **one** SVG `<path>`.

Code: `src/SvgCreator/Tracing/ComponentLabeler.cs`

## Connectivity: 4 vs 8

- **4-connectivity:** pixels touch only through edges (N, S, E, W).
- **8-connectivity:** diagonal neighbours also touch.

We use **8-connectivity for ink**, so a one-pixel diagonal line stays a single piece. Ink and background must use *complementary* connectivity, 8 for ink and 4 for background, or you get topological paradoxes: two diagonal ink pixels and the two background pixels crossing between them would *both* count as connected. The [contour tracer's](Contour-Tracing.md) turning rule is built to match this.

## Two-pass labeling with union-find

**Pass 1 (raster order).** For each ink pixel, look at its already-visited neighbours: W, NW, N and NE.
- If none of them are labelled, give the pixel a new provisional label.
- If one or more are labelled, take one of their labels and **union** all of them, recording that they're the same component.

**Pass 2.** Replace every provisional label with its root (`Find`). Count the area per root.

**Compaction.** Roots with area ≥ `MinComponentArea` are renumbered 1..Count. The others become 0, which removes the noise.

### Union-find details

- `parent[]` is a flat `int` array that grows by doubling.
- **Path halving** in `Find`: `parent[x] = parent[parent[x]]` while walking up. This keeps trees nearly flat, so operations are effectively constant time.
- **Union keeps the smaller label as the root**, so roots are stable in raster order.

Why two passes rather than a flood fill: the raster scan touches memory sequentially and needs no explicit stack. It's O(pixels), and the cost doesn't depend on the shapes.

## Test cases worth remembering

| Mask | Expected |
|---|---|
| Two separate squares | 2 components |
| A U shape | 1 component. The two arms get different labels at first and are merged when the bottom row joins them; this is the classic case that needs union-find |
| A diagonal staircase | 1 component (8-connectivity) |
| A speck smaller than `minArea` | Removed |
