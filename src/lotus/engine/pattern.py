"""N-consecutive attribute pattern detection on the Lotus card stream."""

from __future__ import annotations

from lotus.domain.cards import Card
from lotus.domain.models import BetAxis, BetSide, GameRound, PatternHit


def build_card_stream(
    history: list[GameRound],
    current_open: Card | None = None,
) -> list[Card]:
    """
    Build the time-ordered card stream used for streak detection.

    Order:
      past open -> past hidden -> ... -> previous open -> previous hidden
      -> [current open if provided]

    Settled rounds contribute both cards. The current round's open card is
    appended separately when evaluating during the betting window.
    """
    stream: list[Card] = []
    for round_ in history:
        stream.append(round_.open_card)
        if round_.hidden_card is not None:
            stream.append(round_.hidden_card)
    if current_open is not None:
        stream.append(current_open)
    return stream


class PatternDetector:
    """Detect N consecutive identical attributes ending at the stream tail."""

    def detect(
        self,
        stream: list[Card],
        axis: BetAxis,
        n: int,
    ) -> PatternHit | None:
        if n < 1:
            raise ValueError("n must be >= 1")
        if len(stream) < n:
            return None

        window = stream[-n:]
        sides = [card.attribute(axis) for card in window]
        first = sides[0]
        if all(side is first for side in sides):
            return PatternHit(
                axis=axis,
                streak_side=first,
                bet_side=first.opposite,
                streak_length=n,
                cards=tuple(window),
            )
        return None

    def detect_first(
        self,
        stream: list[Card],
        axes: tuple[BetAxis, ...] | list[BetAxis],
        n: int,
    ) -> PatternHit | None:
        """Return the first matching axis in priority order (same-round tiebreak)."""
        for axis in axes:
            hit = self.detect(stream, axis, n)
            if hit is not None:
                return hit
        return None

    def trailing_streak_length(self, stream: list[Card], axis: BetAxis) -> int:
        """Length of the trailing same-attribute run for diagnostics."""
        if not stream:
            return 0
        target: BetSide = stream[-1].attribute(axis)
        count = 0
        for card in reversed(stream):
            if card.attribute(axis) is target:
                count += 1
            else:
                break
        return count
