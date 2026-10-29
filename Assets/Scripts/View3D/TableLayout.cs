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
            {
                int col = zone.Index % 6, row = zone.Index / 6;
                var anchor = new Vector3((col - 2.5f) * W * 1.3f, SurfaceY, 0.05f - row * L * 1.2f);
                return Stack(anchor, Quaternion.identity, index, count, faceUp, card, jitter: 2f);
            }

            case ZoneKind.Discard:
            {
                var anchor = DiscardAnchor + new Vector3(-zone.Index * W * 1.6f, 0f, 0f);
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
