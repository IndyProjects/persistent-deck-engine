using UnityEngine;

/// Temporary plain models for the room: a chair and a seated stick figure. Both are built around a
/// chair's origin on the floor with +Z facing the table, and can be swapped for real art later.
public static class SeatedFigure
{
    public const float SeatHeight = 0.46f;

    public static void BuildChair(Transform chair, Material material)
    {
        Box(chair, "Seat", new Vector3(0f, SeatHeight, 0f), Quaternion.identity, new Vector3(0.44f, 0.05f, 0.42f), material);
        Box(chair, "Back", new Vector3(0f, 0.76f, -0.2f), Quaternion.Euler(-8f, 0f, 0f), new Vector3(0.44f, 0.55f, 0.04f), material);
        foreach (float x in new[] { -0.19f, 0.19f })
        foreach (float z in new[] { -0.18f, 0.18f })
            Limb(chair, "Leg", new Vector3(x, 0f, z), new Vector3(x, SeatHeight, z), 0.02f, material);
    }

    /// A figure sitting on the chair, with its hands resting on the table's edge.
    public static GameObject Build(Transform chair, Material material)
    {
        var figure = new GameObject("Stick Figure").transform;
        figure.SetParent(chair, false);

        var hips      = new Vector3(0f, SeatHeight + 0.05f, -0.02f);
        var shoulders = new Vector3(0f, 0.95f, -0.06f);

        Limb(figure, "Torso", hips, shoulders, 0.03f, material);
        Limb(figure, "Shoulders", shoulders + Vector3.left * 0.17f, shoulders + Vector3.right * 0.17f, 0.022f, material);
        Limb(figure, "Hip Bar", hips + Vector3.left * 0.09f, hips + Vector3.right * 0.09f, 0.022f, material);
        Limb(figure, "Neck", shoulders, shoulders + Vector3.up * 0.09f, 0.018f, material);

        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Head";
        Finish(head, figure, shoulders + Vector3.up * 0.19f, Quaternion.identity, Vector3.one * 0.2f, material);

        foreach (float side in new[] { -1f, 1f })
        {
            var shoulder = shoulders + new Vector3(side * 0.17f, 0f, 0f);
            var elbow    = new Vector3(side * 0.22f, 0.76f, 0.14f);
            var hand     = new Vector3(side * 0.14f, TableLayout.SurfaceY + 0.05f, 0.4f);
            Limb(figure, "Upper Arm", shoulder, elbow, 0.018f, material);
            Limb(figure, "Forearm", elbow, hand, 0.016f, material);
            Joint(figure, shoulder, 0.022f, material);
            Joint(figure, elbow, 0.02f, material);
            Joint(figure, hand, 0.03f, material);

            var hip  = hips + new Vector3(side * 0.09f, 0f, 0f);
            var knee = new Vector3(side * 0.11f, SeatHeight + 0.05f, 0.3f);
            var foot = new Vector3(side * 0.11f, 0.02f, 0.3f);
            Limb(figure, "Thigh", hip, knee, 0.022f, material);
            Limb(figure, "Shin", knee, foot, 0.02f, material);
            Joint(figure, hip, 0.024f, material);
            Joint(figure, knee, 0.024f, material);
            Joint(figure, foot, 0.028f, material);
        }
        return figure.gameObject;
    }

    /// A small ball that rounds off where limbs meet.
    private static void Joint(Transform parent, Vector3 at, float radius, Material material)
    {
        var joint = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        joint.name = "Joint";
        Finish(joint, parent, at, Quaternion.identity, Vector3.one * (radius * 2f), material);
    }

    private static void Limb(Transform parent, string name, Vector3 from, Vector3 to, float radius, Material material)
    {
        var limb = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        limb.name = name;
        var along = to - from;
        Finish(limb, parent, (from + to) * 0.5f, Quaternion.FromToRotation(Vector3.up, along),
               new Vector3(radius * 2f, along.magnitude * 0.5f, radius * 2f), material);
    }

    private static void Box(Transform parent, string name, Vector3 position, Quaternion rotation, Vector3 size, Material material)
    {
        var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        Finish(box, parent, position, rotation, size, material);
    }

    private static void Finish(GameObject go, Transform parent, Vector3 position, Quaternion rotation, Vector3 scale, Material material)
    {
        go.transform.SetParent(parent, false);
        go.transform.SetLocalPositionAndRotation(position, rotation);
        go.transform.localScale = scale;
        go.GetComponent<MeshRenderer>().sharedMaterial = material;

        // No colliders: only the cards and the felt should be pointed at
        var collider = go.GetComponent<Collider>();
        if (Application.isPlaying) Object.Destroy(collider);
        else Object.DestroyImmediate(collider);
    }
}
