"""User-configurable betting parameters."""

from __future__ import annotations

from dataclasses import dataclass, field

from .models import BetAxis


@dataclass
class BettingConfig:
    """UI-backed settings consumed by BettingEngine."""

    enable_under_over: bool = True
    enable_red_black: bool = True
    streak_n: int = 5
    is_step_finish_enabled: bool = False
    step_finish_round: int = 8
    martin_amounts: list[int] = field(default_factory=lambda: [1000, 2000, 4000, 8000, 16000])
    # When both axes hit in the same evaluation, prefer this order.
    axis_priority: tuple[BetAxis, ...] = (BetAxis.UNDER_OVER, BetAxis.RED_BLACK)

    def __post_init__(self) -> None:
        self.validate()

    def validate(self) -> None:
        if self.streak_n < 1:
            raise ValueError("streak_n must be >= 1")
        if self.step_finish_round < 1 or self.step_finish_round > 13:
            raise ValueError("step_finish_round must be in 1..13")
        if not self.martin_amounts:
            raise ValueError("martin_amounts must not be empty")
        if any(a <= 0 for a in self.martin_amounts):
            raise ValueError("martin amounts must be positive")

    @property
    def enabled_axes(self) -> tuple[BetAxis, ...]:
        axes: list[BetAxis] = []
        for axis in self.axis_priority:
            if axis is BetAxis.UNDER_OVER and self.enable_under_over:
                axes.append(axis)
            elif axis is BetAxis.RED_BLACK and self.enable_red_black:
                axes.append(axis)
        return tuple(axes)

    @property
    def max_step(self) -> int:
        return len(self.martin_amounts)

    def amount_for_step(self, step: int) -> int:
        if step < 1 or step > self.max_step:
            raise ValueError(f"step {step} out of range 1..{self.max_step}")
        return self.martin_amounts[step - 1]
