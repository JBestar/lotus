"""Shared domain enums and result DTOs."""

from __future__ import annotations

from dataclasses import dataclass, field
from enum import Enum
from typing import TYPE_CHECKING

if TYPE_CHECKING:
    from .cards import Card


class BetAxis(str, Enum):
    UNDER_OVER = "under_over"
    RED_BLACK = "red_black"


class BetSide(str, Enum):
    UNDER = "under"
    OVER = "over"
    RED = "red"
    BLACK = "black"

    @property
    def opposite(self) -> BetSide:
        mapping = {
            BetSide.UNDER: BetSide.OVER,
            BetSide.OVER: BetSide.UNDER,
            BetSide.RED: BetSide.BLACK,
            BetSide.BLACK: BetSide.RED,
        }
        return mapping[self]

    @property
    def axis(self) -> BetAxis:
        if self in (BetSide.UNDER, BetSide.OVER):
            return BetAxis.UNDER_OVER
        return BetAxis.RED_BLACK


class EnginePhase(str, Enum):
    """IDLE = watching for a new entry; ACTIVE = martingale cycle in progress."""

    IDLE = "idle"
    ACTIVE = "active"


class BetOutcome(str, Enum):
    WIN = "win"
    LOSE = "lose"
    SKIP = "skip"


@dataclass(frozen=True, slots=True)
class PatternHit:
    axis: BetAxis
    streak_side: BetSide
    bet_side: BetSide
    streak_length: int
    cards: tuple[Card, ...]


@dataclass(frozen=True, slots=True)
class BetDecision:
    """Decision produced when the open card is known and betting is allowed."""

    place: bool
    reason: str
    side: BetSide | None = None
    axis: BetAxis | None = None
    step: int = 1
    amount: int = 0
    pattern: PatternHit | None = None
    shoe_round: int = 0


@dataclass(frozen=True, slots=True)
class SettleResult:
    outcome: BetOutcome
    won: bool
    side: BetSide | None
    step_before: int
    step_after: int
    amount: int
    phase_after: EnginePhase
    message: str


@dataclass
class GameRound:
    """One game round: left open card + right hidden (bet) card."""

    round_id: int
    open_card: Card
    hidden_card: Card | None = None
    shoe_round: int = 1
    is_shoe_start: bool = False
    meta: dict = field(default_factory=dict)

    @property
    def is_settled(self) -> bool:
        return self.hidden_card is not None
