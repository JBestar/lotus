"""Card parsing and Under/Over · Red/Black attribute mapping."""

from __future__ import annotations

from dataclasses import dataclass
from enum import Enum

from .models import BetAxis, BetSide


class Suit(str, Enum):
    SPADE = "spade"  # ♠ black
    CLUB = "club"  # ♣ black
    DIAMOND = "diamond"  # ♦ red
    HEART = "heart"  # ♥ red


_SUIT_ALIASES: dict[str, Suit] = {
    "s": Suit.SPADE,
    "spade": Suit.SPADE,
    "spades": Suit.SPADE,
    "♠": Suit.SPADE,
    "c": Suit.CLUB,
    "club": Suit.CLUB,
    "clubs": Suit.CLUB,
    "♣": Suit.CLUB,
    "d": Suit.DIAMOND,
    "diamond": Suit.DIAMOND,
    "diamonds": Suit.DIAMOND,
    "♦": Suit.DIAMOND,
    "h": Suit.HEART,
    "heart": Suit.HEART,
    "hearts": Suit.HEART,
    "♥": Suit.HEART,
}

_RANK_ALIASES: dict[str, int] = {
    "a": 1,
    "1": 1,
    "2": 2,
    "3": 3,
    "4": 4,
    "5": 5,
    "6": 6,
    "7": 7,
    "8": 8,
    "9": 9,
    "10": 10,
    "t": 10,
}


@dataclass(frozen=True, slots=True)
class Card:
    """Playing card used by Lotus Odd/Even (ranks 1–10 only)."""

    rank: int
    suit: Suit

    def __post_init__(self) -> None:
        if self.rank < 1 or self.rank > 10:
            raise ValueError(f"rank must be 1..10, got {self.rank}")

    @property
    def under_over(self) -> BetSide:
        return BetSide.UNDER if self.rank <= 5 else BetSide.OVER

    @property
    def red_black(self) -> BetSide:
        if self.suit in (Suit.SPADE, Suit.CLUB):
            return BetSide.BLACK
        return BetSide.RED

    def attribute(self, axis: BetAxis) -> BetSide:
        if axis is BetAxis.UNDER_OVER:
            return self.under_over
        if axis is BetAxis.RED_BLACK:
            return self.red_black
        raise ValueError(f"unknown axis: {axis}")

    def __str__(self) -> str:
        symbols = {
            Suit.SPADE: "♠",
            Suit.CLUB: "♣",
            Suit.DIAMOND: "♦",
            Suit.HEART: "♥",
        }
        return f"{self.rank}{symbols[self.suit]}"


def attribute_for(card: Card, axis: BetAxis) -> BetSide:
    return card.attribute(axis)


def parse_card(token: str) -> Card:
    """Parse compact tokens like '10S', 'A♥', '6d', '♠9'."""
    raw = token.strip().lower().replace(" ", "")
    if not raw:
        raise ValueError("empty card token")

    suit: Suit | None = None
    rank_part = raw

    for alias, mapped in _SUIT_ALIASES.items():
        if raw.startswith(alias):
            suit = mapped
            rank_part = raw[len(alias) :]
            break
        if raw.endswith(alias):
            suit = mapped
            rank_part = raw[: -len(alias)]
            break

    if suit is None:
        raise ValueError(f"cannot parse suit from: {token!r}")

    rank_key = rank_part.strip()
    if rank_key not in _RANK_ALIASES:
        raise ValueError(f"cannot parse rank from: {token!r}")
    return Card(rank=_RANK_ALIASES[rank_key], suit=suit)
