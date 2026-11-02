using UnityEngine;

public struct CardPose
{
    public Vector3    Position;
    public Quaternion Rotation;

    public CardPose(Vector3 position, Quaternion rotation)
    {
        Position = position;
        Rotation = rotation;
    }

    public bool Approximately(CardPose other)
        => (Position - other.Position).sqrMagnitude < 1e-8f && Quaternion.Angle(Rotation, other.Rotation) < 0.05f;
}

/// Maps the logical table (zones, positions, faces) to world poses. Pure function of the CardTable,
/// so any state — including every intermediate step of an action — has exactly one picture.
///
/// World frame: table centre at the origin, felt surface at y = <see cref="SurfaceY"/>.
/// The local player (seat 0, also the dealer) sits at −Z looking towards +Z; +X is to their right.
/// Seats proceed clockwise seen from above, so seat 1 is on the player's left, as in a real deal.
public class TableLayout
{
    public const float SurfaceY    = 0.76f;
    public const float TableRadius = 0.95f;
    public const int   DefaultSeats = 4;

    /// Number of seats around the table (at least DefaultSeats; grows if more hands are dealt).
    public int SeatCount { get; set; } = DefaultSeats;

    private const float T = CardMeshBuilder.Thickness;
    private const float L = CardMeshBuilder.Length;
    private const float W = CardMeshBuilder.Width;

    private static readonly Quaternion FaceDown = Quaternion.AngleAxis(180f, Vector3.forward);

    // Zone anchors (seat 0 is the dealer, so the deck and shuffling hands sit in front of them)
    public static readonly Vector3 DeckAnchor      = new Vector3(0.30f, SurfaceY, -0.42f);
    public static readonly Vector3 LeftHandAnchor  = new Vector3(-0.12f, SurfaceY + 0.09f, -0.56f);
    public static readonly Vector3 RightHandAnchor = new Vector3(0.12f, SurfaceY + 0.09f, -0.56f);
    public static readonly Vector3 DiscardAnchor   = new Vector3(-0.34f, SurfaceY, -0.30f);
    public static readonly Vector3 LocalHandPivot  = new Vector3(0f, SurfaceY + 0.07f, -0.70f);

    public static int SeatsFor(CardTable table) => Mathf.Max(DefaultSeats, table.HandCount());

    /// Unit vector from the table centre towards a seat.
    public Vector3 SeatDirection(int seat)
    {
        float angle = (270f - seat * 360f / SeatCount) * Mathf.Deg2Rad;
        return new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
    }

    /// Rotation whose forward points from the seat towards the table centre.
    public Quaternion SeatFacing(int seat) => Quaternion.LookRotation(-SeatDirection(seat), Vector3.up);

    public CardPose Pose(CardTable table, int card)
    {
        var zone   = table.LocationOf(card);
        int index  = table.IndexOf(card);
        int count  = table.Count(zone);
        bool faceUp = table.IsFaceUp(card);

        switch (zone.Kind)
        {
            case ZoneKind.Deck:
                return Stack(DeckAnchor, Quaternion.Euler(0f, 6f, 0f), index, count, faceUp, card, jitter: 0.4f);

            case ZoneKind.Held:
            {
                bool left   = zone.Index == 0;
                var anchor  = left ? LeftHandAnchor : RightHandAnchor;
                // Packets lean in towards each other, ready to riffle
                var tilt    = Quaternion.Euler(-8f, left ? 14f : -14f, left ? -14f : 14f);
                return Stack(anchor, tilt, index, count, faceUp, card, jitter: 0f);
            }

            case ZoneKind.Pile:
                return Stack(PileAnchor(zone.Index), Quaternion.identity, index, count, faceUp, card, jitter: 2f);

            case ZoneKind.Discard:
            {
                var anchor = DiscardAnchorFor(zone.Index);
                var pose = Stack(anchor, Quaternion.Euler(0f, -20f, 0f), index, count, faceUp, card, jitter: 22f);
                pose.Position += Scatter(card) * 0.018f;
                return pose;
            }

            case ZoneKind.Hand:
                return zone.Index == 0
                    ? HeldFan(index, count)
                    : TableFan(zone.Index, index, count, faceUp);

            default:
                return new CardPose(new Vector3(0f, SurfaceY, 0f), FaceDown);
        }
    }

    // ---- Anchors and drop targets ----

    /// Table piles run in rows of six across the middle of the table.
    public static Vector3 PileAnchor(int index)
    {
        int col = index % 6, row = index / 6;
        return new Vector3((col - 2.5f) * W * 1.3f, SurfaceY, 0.05f - row * L * 1.2f);
    }

    public static Vector3 DiscardAnchorFor(int index) => DiscardAnchor + new Vector3(-index * W * 1.6f, 0f, 0f);

    /// Places a dragged card can be dropped: the first row of table piles, the discard, and the
    /// local player's own hand (dropped near where it is held).
    public const float DropRadius = 0.16f;

    public static bool TryDropZone(Vector3 point, out ZoneId zone, out Vector3 anchor)
    {
        var candidates = new (ZoneId zone, Vector3 anchor)[8];
        for (int i = 0; i < 6; i++) candidates[i] = (ZoneId.Pile(i), PileAnchor(i));
        candidates[6] = (ZoneId.Discard(), DiscardAnchorFor(0));
        candidates[7] = (ZoneId.Hand(0), new Vector3(LocalHandPivot.x, SurfaceY, LocalHandPivot.z));

        zone = default;
        anchor = default;
        float best = DropRadius * DropRadius;
        foreach (var c in candidates)
        {
            var d = point - c.anchor;
            d.y = 0f;
            if (d.sqrMagnitude >= best) continue;
            best = d.sqrMagnitude;
            zone = c.zone;
            anchor = c.anchor;
        }
        return best < DropRadius * DropRadius;
    }

    // ---- Inspecting a stack (e.g. choosing where to cut) ----

    /// Upright frame for spreading a stack apart so each card can be seen and picked:
    /// the zone's anchor and its yaw, with no tilt. Only the deck and held packets can be inspected.
    public static bool TryInspectFrame(ZoneId zone, out Vector3 anchor, out Quaternion frame)
    {
        switch (zone.Kind)
        {
            case ZoneKind.Deck:
                anchor = DeckAnchor;
                frame  = Quaternion.Euler(0f, 6f, 0f);
                return true;
            case ZoneKind.Held:
                bool left = zone.Index == 0;
                anchor = left ? LeftHandAnchor : RightHandAnchor;
                frame  = Quaternion.Euler(0f, left ? 14f : -14f, 0f);
                return true;
            default:
                anchor = default;
                frame  = Quaternion.identity;
                return false;
        }
    }

    /// A card in a packet at <paramref name="height"/> above the anchor, slid <paramref name="slide"/> lengthwise.
    public static CardPose PacketPose(Vector3 anchor, Quaternion frame, float height, float slide, bool faceUp)
        => new CardPose(anchor + frame * new Vector3(0f, height + T * 0.5f, slide),
                        frame * (faceUp ? Quaternion.identity : FaceDown));

    /// Pose of a card in an inspected stack: cards <paramref name="spacing"/> apart (index 0 highest),
    /// and slid <paramref name="slide"/> metres lengthwise out of the stack when selected.
    public static CardPose InspectPose(CardTable table, int card, float spacing, float slide)
    {
        var zone = table.LocationOf(card);
        TryInspectFrame(zone, out var anchor, out var frame);
        int index = table.IndexOf(card), count = table.Count(zone);
        float height = spacing * (count - 1 - index) + T * 0.5f;
        var rot = frame * (table.IsFaceUp(card) ? Quaternion.identity : FaceDown);
        return new CardPose(anchor + frame * new Vector3(0f, height, slide), rot);
    }

    /// The layer (zone index, 0 = top) of an inspected stack nearest to a ray, e.g. from the mouse.
    /// Finds where the ray passes closest to the stack's vertical axis and converts that height to a card.
    public static int LayerAt(Ray ray, Vector3 anchor, int count, float spacing)
    {
        if (count <= 0) return -1;
        // Closest point between the ray and the vertical line through the anchor
        Vector3 u = ray.direction, v = Vector3.up, w = ray.origin - anchor;
        float a = Vector3.Dot(u, u), b = Vector3.Dot(u, v), c = Vector3.Dot(v, v);
        float d = Vector3.Dot(u, w), e = Vector3.Dot(v, w);
        float denom = a * c - b * b;
        float t = denom > 1e-6f ? (a * e - b * d) / denom : e; // height along the axis
        int fromBottom = Mathf.RoundToInt((t - T * 0.5f) / spacing);
        return Mathf.Clamp(count - 1 - fromBottom, 0, count - 1);
    }

    /// A squared-up stack: index 0 (top of the zone) is highest.
    private static CardPose Stack(Vector3 anchor, Quaternion yaw, int index, int count, bool faceUp, int card, float jitter)
    {
        float height = T * (count - 1 - index) + T * 0.5f;
        var rot = yaw * Quaternion.AngleAxis(Jitter(card) * jitter, Vector3.up) * (faceUp ? Quaternion.identity : FaceDown);
        return new CardPose(anchor + yaw * new Vector3(0f, height, 0f), rot);
    }

    /// The local player's hand, held up in a fan facing the camera. Faces always point at the owner.
    private static CardPose HeldFan(int index, int count)
    {
        float spread = Mathf.Min(70f, 9f * (count - 1));
        float angle  = count > 1 ? Mathf.Lerp(-spread * 0.5f, spread * 0.5f, index / (float)(count - 1)) : 0f;

        // Tilted 45° towards the player (faces the seated camera head-on and still catches the lamp),
        // then spun in the card's own plane around the grip point
        var rot = Quaternion.Euler(-45f, 0f, 0f) * Quaternion.AngleAxis(angle, Vector3.up);
        var pos = LocalHandPivot + rot * new Vector3(0f, index * T * 3f, L * 0.36f);
        return new CardPose(pos, rot);
    }

    /// Another seat's hand, fanned flat on the table in front of that seat (no one is holding it).
    private CardPose TableFan(int seat, int index, int count, bool faceUp)
    {
        float spread = Mathf.Min(60f, 8f * (count - 1));
        float angle  = count > 1 ? Mathf.Lerp(-spread * 0.5f, spread * 0.5f, index / (float)(count - 1)) : 0f;

        var facing = SeatFacing(seat);
        var pivot  = new Vector3(0f, SurfaceY, 0f) + SeatDirection(seat) * (TableRadius - 0.24f);
        var spin   = Quaternion.AngleAxis(angle, Vector3.up);
        var pos    = pivot + facing * (spin * new Vector3(0f, 0f, L * 0.4f)) + Vector3.up * (T * (index + 0.5f));
        return new CardPose(pos, facing * spin * (faceUp ? Quaternion.identity : FaceDown));
    }

    /// Deterministic per-card value in [-1, 1], so messy piles look the same every time.
    private static float Jitter(int card)
    {
        uint h = (uint)card * 2654435761u;
        h ^= h >> 13;
        return (h % 2001) / 1000f - 1f;
    }

    private static Vector3 Scatter(int card)
        => new Vector3(Jitter(card + 101), 0f, Jitter(card + 211));
}
