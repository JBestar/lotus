"""Shoe (shuffle-cycle) round counter."""

from __future__ import annotations

from dataclasses import dataclass


@dataclass
class ShoeCounter:
    """
    Tracks board number inside the current shoe.

    Shuffle detection resets ``current_round`` to 1. Martingale step state is
    owned by BettingEngine and is intentionally NOT reset here.
    """

    current_round: int = 1
    shoe_index: int = 1
    typical_shoe_size: int = 12

    def mark_shoe_start(self) -> None:
        """Call when a shuffle is detected; next/current board becomes 1판."""
        self.current_round = 1
        self.shoe_index += 1

    def advance(self) -> int:
        """Increment board number after a round settles (non-shuffle)."""
        self.current_round += 1
        return self.current_round

    def set_round(self, round_no: int) -> None:
        if round_no < 1 or round_no > 13:
            raise ValueError("shoe round must be in 1..13")
        self.current_round = round_no

    @property
    def is_near_shuffle(self) -> bool:
        return self.current_round >= self.typical_shoe_size
