using System.Collections.Generic;
using UnityEngine;

/// How a riffle looks: the two packets held end to end above and behind a spot in front of the dealer,
/// their inner ends dipping towards each other. Each dropped card lands overlapping the other packet by a
/// fixed amount and sloping down towards the middle, so the inner ends of the two halves zip between one
/// another; pushing then flattens and slides them into one squared deck.
public sealed class RifflePresentation : ICardPoser
{
    public const float PushSeconds = 0.5f;

    /// How far the two halves overlap once woven, as a fraction of a card's length.
    private const float OverlapFraction = 0.3f;
    /// Spacing while woven: looser than a squared deck, so the alternating layers can be seen.
    private const float WovenSpacing = 0.0016f;
    /// Each woven half slopes up towards its outer end, as bent cards do.
    private const float WovenSlope = 5f;
    private const float HoldHeight = 0.12f;
    private const float HoldBehind = 0.07f;
    private const float HoldTilt = 12f;
    private const float MaxSkew = 1.2f;

    public static readonly Vector3 Spot = new Vector3(0f, TableLayout.SurfaceY, -0.5f);

    /// True once the halves are being pushed together.
    public bool Pushed { get; set; }

    private readonly Dictionary<int, bool> _landedFromLeft = new Dictionary<int, bool>();

    private static float Length => CardMeshBuilder.Length;
    private static float Thickness => CardMeshBuilder.Thickness;
    private static float Offset => Length * (1f - OverlapFraction) * 0.5f;

    // Long side of each card runs left to right, so the packets meet at their short ends
    private static readonly Quaternion EndToEnd = Quaternion.Euler(0f, 90f, 0f);
    private static readonly Quaternion FaceDown = Quaternion.AngleAxis(180f, Vector3.forward);

    public void OnMove(CardMove move, CardTable table)
    {
        if (move.To != ZoneId.Deck || move.From.Kind != ZoneKind.Held) return;
        var cards = table.Cards(ZoneId.Deck);
        for (int i = move.ToIndex; i < move.ToIndex + move.Count; i++)
            _landedFromLeft[cards[i]] = move.From == ZoneId.LeftHand;
    }

    public bool TryPose(CardTable table, int card, out CardPose pose)
    {
        var zone = table.LocationOf(card);
        int index = table.IndexOf(card);
        var face = table.IsFaceUp(card) ? Quaternion.identity : FaceDown;

        if (zone.Kind == ZoneKind.Held)
        {
            bool left = zone == ZoneId.LeftHand;
            float side = left ? -1f : 1f;
            // Measured down from the grip at the top, so the rest don't shift as the bottom card falls
            var tilt = Quaternion.AngleAxis(left ? -HoldTilt : HoldTilt, Vector3.forward);
            var position = Spot + new Vector3(side * Offset, HoldHeight, HoldBehind) + tilt * new Vector3(0f, -Thickness * index, 0f);
            pose = new CardPose(position, tilt * EndToEnd * face);
            return true;
        }

        if (zone == ZoneId.Deck && _landedFromLeft.TryGetValue(card, out bool fromLeft))
        {
            int fromBottom = table.Count(zone) - 1 - index;
            if (Pushed)
            {
                pose = new CardPose(Spot + Vector3.up * (Thickness * (fromBottom + 0.5f)), EndToEnd * face);
                return true;
            }
            float side = fromLeft ? -1f : 1f;
            float skew = Skew(card) * MaxSkew;
            // Raised by the slope's dip, so the inner ends stay above the table
            float lift = Mathf.Sin(WovenSlope * Mathf.Deg2Rad) * Length * 0.5f;
            var slope = Quaternion.AngleAxis(fromLeft ? -WovenSlope : WovenSlope, Vector3.forward);
            pose = new CardPose(Spot + new Vector3(side * Offset, lift + WovenSpacing * fromBottom + Thickness * 0.5f, 0f),
                                slope * Quaternion.AngleAxis(skew, Vector3.up) * EndToEnd * face);
            return true;
        }

        pose = default;
        return false;
    }

    /// Deterministic per-card value in [-1, 1]: no two cards land perfectly square.
    private static float Skew(int card)
    {
        uint h = (uint)card * 2654435761u;
        h ^= h >> 13;
        return (h % 2001) / 1000f - 1f;
    }
}
