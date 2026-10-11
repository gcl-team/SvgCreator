# Shape recognition: circles and ellipses

**Goal:** when an outline really is a circle or an ellipse, write it as a native SVG `<circle>` or `<ellipse>` instead of Bézier curves. The geometry is exact, the file is smaller, and the shape can be edited by hand.

Code: `src/SvgCreator/Fitting/EllipseFitter.cs`, `Ellipse.cs`; output in `Svg/SvgWriter.cs`

## Why it isn't automatic

[Curve fitting](Curve-Fitting.md) treats every outline the same way. A circle comes out as a few cubics: a disc of radius 24 becomes 2 cubics within 1 px. That looks right, but cubics can't represent a circle *exactly* (the best 4-cubic approximation is off by about 0.03% of the radius), and nothing in the SVG says "this is a circle". So recognition has to be an explicit step that runs *before* curve fitting.

## Direct least-squares ellipse fitting

Every conic (ellipse, parabola or hyperbola) satisfies

```
A x² + B xy + C y² + D x + E y + F = 0
```

Fitting a general conic to points is plain least squares, but the result may be a hyperbola. **Fitzgibbon, Pilu & Fisher (1999)** add the constraint **4AC − B² = 1**, which only ellipses satisfy. That turns the fit into an eigenvalue problem whose solution is *always* an ellipse. It needs no iteration and no starting guess.

We use the numerically stable form of **Halíř & Flusser (1998)**:

1. **Normalize.** Move the centroid to the origin and scale so the RMS radius is about 1. Without this, terms like x⁴ in 1000-px images lose precision.
2. **Build scatter matrices.** Split each point's terms into a quadratic part `[x², xy, y²]` and a linear part `[x, y, 1]`, then form the three 3×3 matrices S1, S2, S3.
3. **Eliminate the linear part.** `T = −S3⁻¹ S2ᵀ` and `M = S1 + S2·T`. This reduces the 6×6 problem to 3×3.
4. **Apply the constraint.** Pre-multiply by the constraint matrix's inverse, which just rearranges rows: `(M₂/2, −M₁, M₀/2)`.
5. **Solve the 3×3 eigenproblem.** Eigenvalues come from the characteristic cubic, solved in closed form (trigonometric/Cardano). Each eigenvector is the largest cross product of two rows of `M − λI`. **The ellipse is the eigenvector with 4AC − B² > 0.**
6. **Recover the linear coefficients:** `(D, E, F) = T·(A, B, C)`.

### From conic coefficients to centre, radii and angle

- **Centre:** where the gradient is zero, i.e. solve `[2A B; B 2C]·(x₀, y₀) = −(D, E)`.
- **Angle:** `θ = ½·atan2(B, A − C)`, the direction of the quadratic form's principal axes.
- **Radii:** `rᵢ = √(−F₀ / λᵢ)`, where F₀ is the conic's value at the centre and λᵢ are the quadratic form's values along each axis.
- **Canonical form:** rx ≥ ry, angle in [0°, 180°), then map back from normalized coordinates.

## Accepting only true ellipses

The fit *always* returns an ellipse, even for a square, so every fit is checked. **All** of these must pass:

| Check | What it rejects |
|---|---|
| Every outline point within tolerance of the ellipse (Sampson distance) | Squares, blobs, hair shapes |
| The tolerance shrinks for small shapes: `min(CurveTolerance, max(0.5, 0.08·ry))` | Small rounded squares passing as circles |
| Outline area ≈ π·rx·ry, within tolerance × perimeter | **Thin arcs.** Both sides of a 3 px "C" hug one circle, but its area is tiny |
| Minor radius ≥ 4 px | **Thin straight strokes.** A 2 px line lies within 1 px of a very flat ellipse |

These are fitted to the outline's edge midpoints, the same staircase-free points that curve fitting uses.

## Writing native shapes

| Ink component | SVG output |
|---|---|
| A single solid ellipse | `<circle>` (if rx − ry ≤ 0.5) or `<ellipse … transform="rotate(θ cx cy)">` |
| An outer ellipse + one concentric elliptical hole, with uniform gap | **One stroked shape:** `fill="none" stroke-width=gap`, with radius halfway between the two. A ring is exactly what a stroke draws |
| An elliptical contour inside a more complex component | Native arc commands in the path: two half-ellipse `a` arcs, e.g. `M90 50a40 30 0 0 1-80 0a40 30 0 0 1 80 0Z` |

## Results

- **image03:** the outer ring line became one `<circle cx="254" cy="258" r="244.5" stroke-width="4.4">`, saving 2.3 KB. An overlay showed it exactly on top of the traced curves. The inner ring line stayed as curves because hair strands touch it, so its outline isn't a pure ring.
- **image01 and image02:** one small ornament ellipse each, written as arcs inside a path. Nothing else was converted; no false positives.
- Turn it off with `DetectEllipses = false` / `--ellipses false`.
