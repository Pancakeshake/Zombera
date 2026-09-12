work on second story generation
new roof types 
detachable Building sections






These need a new enum value + placement logic, but use your existing Ridge/Panel/Gable prefabs:
Cross Gable	Two perpendicular gable ridges forming a cross. Common on larger houses.	RoofStyle.CrossGable. Two ridge+panel assemblies at 90°. Good for L-shaped/taller buildings.
Tier 3 — New mesh prefabs needed


These need Blender work to create new kit pieces:

Roof	What it is	Pieces needed
Hip	All 4 sides slope, no vertical gable walls. Extremely common residential roof — this is the biggest missing archetype.	Hip ridge (short center cap), 4 triangular hip panels. Or: reuse existing panels angled on all 4 sides with hip-end caps.
Gambrel (Barn)	Two slopes per side — steep lower, shallow upper. Iconic barn/farm look. Good for rural zones.	New gambrel panel (angle break baked in) — or two panels per side stacked. Reuse Ridge. New gambrel-end gable.
Mansard	Four-sided gambrel. French urban buildings, apartments.	Mansard panels (4), mansard ridge-cap corners. Higher piece count but visually distinct.








