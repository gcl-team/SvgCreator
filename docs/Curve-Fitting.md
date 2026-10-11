# Curve fitting

**Goal:** replace jagged pixel staircases with smooth cubic Bézier curves, while keeping real corners (hair tips, chins, eye corners) sharp.

Code: `src/SvgCreator/Fitting/`: `CurveBuilder.cs` (runs the stages), `CornerDetector.cs`, `BezierFitter.cs`

```mermaid
flowchart LR
    A[Corner vertices<br/>from tracer] --> B[Densify to<br/>unit steps]
    B --> C[Edge midpoints<br/>remove staircase]
    C --> D[Detect corners<br/>multi-scale angle]
    D --> E[Snap corners to<br/>pixel corner]
    E --> F[Schneider fit<br/>corner to corner]
```

## Why not "RDP, then Schneider"

The tempting pipeline is to simplify with Ramer–Douglas–Peucker (RDP), then fit curves. It works poorly:

- RDP can only **keep or drop** vertices. Every vertex it keeps is still a jittery pixel corner.
- Schneider estimates the curve direction at each end from the points near it. With RDP's sparse points, those directions swing around, and the curves come out wobbly.
- Schneider already merges points as part of fitting, so running RDP first throws away information it needs.

**Better:** let Schneider fit the *dense* points, and use corner detection to decide where curves must break.

## Step 1: densify and take midpoints

- Expand the tracer's corner vertices into every unit step along the outline.
- Replace each step with its **midpoint**. On a 45° staircase, `(0,0),(1,0),(1,1),(2,1)` becomes `(0.5,0),(1,0.5),(1.5,1)`, which is perfectly collinear. Shallower staircases keep ±0.5 px of noise, which the fit tolerance absorbs.
- Outlines with ≤ 8 points (specks) skip fitting and stay as their exact pixel polygon.

## Step 2: corner detection

- At each point, measure the angle between the vectors to the points `k` before and `k` after (`k = min(4, n/4)`).
- A wide span ignores the ±0.5 px staircase noise. Straight lines give cos = −1.
- A point is a corner if its angle is sharper than `CornerAngle` (135°).
- **Non-maximum suppression** keeps only the sharpest point within ±radius.

### Pitfall: short sides lost their corners

On a side shorter than the measuring span, *every* point scored exactly the same. Suppression then kept one corner per side, so a 6×4 rectangle came out as a lozenge. **Fix:** average the angle measured at two spans, `k` and `k/2`. The short span breaks the tie, and the long span still ignores noise. The suppression radius is also narrowed to `k/2`.

### Snapping corners

A corner found at a midpoint is moved onto the lattice vertex where the outline actually turns, so squares stay square. **Exception:** at a 1-px-wide tip, *both* neighbouring vertices are turns, so the midpoint stays. Snapping to either side would skew the direction of the curves on each side.

## Step 3: Schneider's algorithm

Philip J. Schneider, *"An Algorithm for Automatically Fitting Digitized Curves"*, Graphics Gems (1990). For each run of points between two corners:

1. **Chord-length parameterization.** Give each point a parameter `t` in proportion to the distance travelled along the polyline.
2. **Least-squares fit.** With the endpoints and end directions fixed, solve a 2×2 linear system for the two control-point distances `α₁` and `α₂`. If the result is degenerate or negative, fall back to `α = chord/3`.
3. **Measure the worst point**, i.e. the largest distance from a point to its position on the curve.
4. If the error is within the tolerance, accept. If it's within **4× the tolerance**, refine each point's `t` with up to 4 **Newton–Raphson** steps and refit.
5. Otherwise **split at the worst point** and recurse on both halves. Both halves share the curve direction at the split point, so the join is smooth.

### Pitfall: corner points skew the end directions

The direction at each end of a run was first estimated from the endpoint to the point 3 steps away. A snapped corner sits up to 0.5 px off the run's line, which tilted that direction, so straight sides bulged by about 0.7 px. **Fix:** estimate from `points[1]` to `points[4]`, skipping the endpoint.

### Smooth closed loops

A loop with no corners, such as a circle, is split at points `0` and `n/2`, and both halves share the curve direction at each split point. A disc of radius 24 fits into **2 cubics** within 1 px.

## Results

- Staircase diagonals collapse to a single straight segment.
- Rectangles keep exact corners.
- Circles become a few smooth cubics with matching directions where they join.
- Segment count fell about 45% compared with the polygon version, e.g. 1,511 → 824 on the sample. File size still went *up* at first; see [SVG Output](SVG-Output.md).
