with open(r'Assets\Editor\ModularSingleLevelHouseGeneratorTool.Roofs.cs', 'r', encoding='utf-8') as f:
    content = f.read()

old = '''        private static void PlaceSingleShedGable(Transform roofParent, GameObject gablePrefab, int topLevel, int side,
            float wallTopY, bool isZAxis, float spanLength, float deltaY, float gBaseX, float gBaseY,
            Vector3 edgePos)
        {
            var g = (GameObject)PrefabUtility.InstantiatePrefab(gablePrefab, roofParent);
            if (g == null) return;

            g.name = $"Shed_Gable_L{topLevel}_{side}";

            if (isZAxis)
            {
                g.transform.localPosition = new Vector3(edgePos.x, wallTopY, edgePos.z);
                g.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            }
            else
            {
                g.transform.localPosition = new Vector3(edgePos.x, wallTopY, edgePos.z);
                g.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            }
            g.transform.localScale = new Vector3(spanLength / gBaseX, deltaY / gBaseY, 1f);
            BakeMeshScale(g);
        }'''

new = '''        private static void PlaceSingleShedGable(Transform roofParent, GameObject gablePrefab, int topLevel, int side,
            float wallTopY, bool isZAxis, float spanLength, float deltaY, float gBaseX, float gBaseY,
            Vector3 edgePos)
        {
            var g = (GameObject)PrefabUtility.InstantiatePrefab(gablePrefab, roofParent);
            if (g == null) return;

            g.name = $"Shed_Gable_L{topLevel}_{side}";
            g.transform.localPosition = new Vector3(edgePos.x, wallTopY, edgePos.z);

            // Use the Building Socket to orient the gable instead of hardcoded Euler angles.
            // socket.forward = gable +Y (up), -socket.up = gable +Z (face normal).
            var socket = g.transform.Find("Building Socket");
            if (socket != null)
            {
                var wallNormal = isZAxis
                    ? (side == 0 ? Vector3.right : Vector3.left)
                    : (side == 0 ? Vector3.forward : Vector3.back);

                // Align socket.forward (gable up) to world up.
                g.transform.rotation = Quaternion.FromToRotation(socket.forward, Vector3.up);
                // Then face the wall normal: -socket.up (gable face) -> wallNormal.
                g.transform.rotation = Quaternion.FromToRotation(-socket.up, wallNormal) * g.transform.rotation;
            }

            g.transform.localScale = new Vector3(spanLength / gBaseX, deltaY / gBaseY, 1f);
            BakeMeshScale(g);
        }'''

if old in content:
    content = content.replace(old, new)
    with open(r'Assets\Editor\ModularSingleLevelHouseGeneratorTool.Roofs.cs', 'w', encoding='utf-8') as f:
        f.write(content)
    print('Applied')
else:
    print('NOT FOUND')
    idx = content.find('PlaceSingleShedGable')
    print('Found at:', idx)
    print(repr(content[idx:idx+200]))
