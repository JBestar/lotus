"""
Core betting state machine.

UI / Playwright adapters feed open/hidden cards and shuffle events.
All entry, martingale, and step-finish decisions live here so they stay unit-testable.
"""

from __future__ import annotations

from dataclasses import dataclass, field

from lotus.domain.cards import Card
from lotus.domain.config import BettingConfig
from lotus.domain.models import (
    BetAxis,
    BetDecision,
    BetOutcome,
    BetSide,
    EnginePhase,
    GameRound,
    SettleResult,
)
from lotus.engine.pattern import PatternDetector, build_card_stream
from lotus.engine.shoe import ShoeCounter


def can_place_bet(
    current_round: int,
    current_step: int,
    step_finish_round: int,
    is_step_finish_enabled: bool,
) -> bool:
    """
    Step-finish gate from the product spec.

    - Feature off  -> always allow
    - step > 1     -> always allow (finish the open martingale)
    - step == 1    -> allow only when current_round <= M
    """
    if not is_step_finish_enabled:
        return True
    if current_step > 1:
        return True
    if current_step == 1:
        return current_round <= step_finish_round
    return False


@dataclass
class EngineSnapshot:
    phase: EnginePhase
    current_step: int
    active_side: BetSide | None
    active_axis: BetAxis | None
    shoe_round: int
    shoe_index: int
    pending_bet: bool
    history_len: int
    last_decision: BetDecision | None = None
    last_settle: SettleResult | None = None


@dataclass
class BettingEngine:
    """Pure domain engine — no UI, no browser I/O."""

    config: BettingConfig = field(default_factory=BettingConfig)
    shoe: ShoeCounter = field(default_factory=ShoeCounter)
    detector: PatternDetector = field(default_factory=PatternDetector)

    phase: EnginePhase = EnginePhase.IDLE
    current_step: int = 1
    active_side: BetSide | None = None
    active_axis: BetAxis | None = None

    history: list[GameRound] = field(default_factory=list)
    _pending_round: GameRound | None = field(default=None, init=False, repr=False)
    _pending_decision: BetDecision | None = field(default=None, init=False, repr=False)
    _bet_placed_this_round: bool = field(default=False, init=False, repr=False)
    last_decision: BetDecision | None = field(default=None, init=False)
    last_settle: SettleResult | None = field(default=None, init=False)
    log: list[str] = field(default_factory=list)

    # ------------------------------------------------------------------ config
    def update_config(self, config: BettingConfig) -> None:
        config.validate()
        self.config = config
        self._log(f"config updated: N={config.streak_n}, step_finish={config.is_step_finish_enabled}/{config.step_finish_round}")

    # ----------------------------------------------------------- public gates
    def can_place_bet_now(self) -> bool:
        return can_place_bet(
            current_round=self.shoe.current_round,
            current_step=self.current_step,
            step_finish_round=self.config.step_finish_round,
            is_step_finish_enabled=self.config.is_step_finish_enabled,
        )

    # -------------------------------------------------------------- shoe events
    def on_shuffle_detected(self) -> None:
        """
        Shuffle boundary: reset shoe board to 1판.

        Martingale step / active side are preserved so an unfinished cycle
        continues into the next shoe's round 1.
        """
        prev = self.shoe.current_round
        self.shoe.mark_shoe_start()
        self._log(
            f"shuffle detected (was {prev}판) -> shoe#{self.shoe.shoe_index} 1판 "
            f"(step={self.current_step} preserved)"
        )

    def set_shoe_round(self, round_no: int) -> None:
        self.shoe.set_round(round_no)

    # ----------------------------------------------------------- round lifecycle
    def begin_round(self, round_id: int, open_card: Card) -> BetDecision:
        """
        Called when the current-round open card is revealed (betting window).

        Returns a BetDecision describing whether / what to bet on the hidden card.
        """
        if self._pending_round is not None and not self._pending_round.is_settled:
            raise RuntimeError("previous round not settled; call settle_hidden() first")

        is_start = self.shoe.current_round == 1
        pending = GameRound(
            round_id=round_id,
            open_card=open_card,
            shoe_round=self.shoe.current_round,
            is_shoe_start=is_start,
        )
        self._pending_round = pending
        self._bet_placed_this_round = False

        decision = self._decide_bet(open_card)
        self.last_decision = decision
        self._pending_decision = decision if decision.place else None
        return decision

    def confirm_bet_placed(self) -> None:
        """Adapter acknowledges that the recommended bet was submitted."""
        if self._pending_decision is None or not self._pending_decision.place:
            raise RuntimeError("no pending bet to confirm")
        self._bet_placed_this_round = True
        self.phase = EnginePhase.ACTIVE
        self.active_side = self._pending_decision.side
        self.active_axis = self._pending_decision.axis
        self.current_step = self._pending_decision.step
        self._log(
            f"bet confirmed: {self.active_side} step={self.current_step} "
            f"amount={self._pending_decision.amount}"
        )

    def settle_hidden(self, hidden_card: Card) -> SettleResult:
        """
        Reveal the hidden card, settle any pending bet, then advance shoe board
        unless a shuffle event will be reported separately by the adapter.
        """
        if self._pending_round is None:
            raise RuntimeError("no open round to settle")

        round_ = self._pending_round
        round_.hidden_card = hidden_card
        self.history.append(round_)

        if self._bet_placed_this_round and self.active_side is not None and self.active_axis is not None:
            result = self._settle_active_bet(hidden_card)
        else:
            result = SettleResult(
                outcome=BetOutcome.SKIP,
                won=False,
                side=None,
                step_before=self.current_step,
                step_after=self.current_step,
                amount=0,
                phase_after=self.phase,
                message="no bet this round",
            )

        self.last_settle = result
        self._pending_round = None
        self._pending_decision = None
        self._bet_placed_this_round = False

        # Default: advance board. Adapters that detect shuffle should call
        # on_shuffle_detected() instead of relying on this increment.
        if self.shoe.current_round == round_.shoe_round:
            self.shoe.advance()

        return result

    def settle_hidden_then_shuffle(self, hidden_card: Card) -> SettleResult:
        """Convenience: settle, then treat the next board as shoe start (1판)."""
        # Prevent double-advance: mark shoe round already consumed.
        result = self._settle_without_advance(hidden_card)
        self.on_shuffle_detected()
        return result

    # ----------------------------------------------------------------- snapshot
    def snapshot(self) -> EngineSnapshot:
        return EngineSnapshot(
            phase=self.phase,
            current_step=self.current_step,
            active_side=self.active_side,
            active_axis=self.active_axis,
            shoe_round=self.shoe.current_round,
            shoe_index=self.shoe.shoe_index,
            pending_bet=self._bet_placed_this_round,
            history_len=len(self.history),
            last_decision=self.last_decision,
            last_settle=self.last_settle,
        )

    def card_stream(self, include_current_open: Card | None = None) -> list[Card]:
        return build_card_stream(self.history, include_current_open)

    # -------------------------------------------------------------- internals
    def _decide_bet(self, open_card: Card) -> BetDecision:
        shoe_round = self.shoe.current_round

        # Continuing martingale: same side, next step amount.
        if self.phase is EnginePhase.ACTIVE and self.active_side is not None:
            if not self.can_place_bet_now():
                # Spec: step>1 always allowed; step==1 ACTIVE shouldn't happen.
                return BetDecision(
                    place=False,
                    reason="step-finish blocked continuing bet",
                    shoe_round=shoe_round,
                )
            amount = self.config.amount_for_step(self.current_step)
            return BetDecision(
                place=True,
                reason=f"martingale continue step={self.current_step}",
                side=self.active_side,
                axis=self.active_axis,
                step=self.current_step,
                amount=amount,
                shoe_round=shoe_round,
            )

        # New entry (IDLE, step == 1)
        if not self.config.enabled_axes:
            return BetDecision(place=False, reason="no bet axis enabled", shoe_round=shoe_round)

        if not self.can_place_bet_now():
            return BetDecision(
                place=False,
                reason=(
                    f"step-finish: round {shoe_round} > M={self.config.step_finish_round}, "
                    "skip new entry"
                ),
                shoe_round=shoe_round,
            )

        stream = build_card_stream(self.history, open_card)
        hit = self.detector.detect_first(
            stream,
            self.config.enabled_axes,
            self.config.streak_n,
        )
        if hit is None:
            return BetDecision(
                place=False,
                reason=f"no {self.config.streak_n}-streak pattern",
                shoe_round=shoe_round,
            )

        amount = self.config.amount_for_step(1)
        return BetDecision(
            place=True,
            reason=f"new entry: {hit.streak_length}x {hit.streak_side} -> bet {hit.bet_side}",
            side=hit.bet_side,
            axis=hit.axis,
            step=1,
            amount=amount,
            pattern=hit,
            shoe_round=shoe_round,
        )

    def _settle_active_bet(self, hidden_card: Card) -> SettleResult:
        assert self.active_side is not None
        assert self.active_axis is not None

        step_before = self.current_step
        amount = self.config.amount_for_step(step_before)
        actual = hidden_card.attribute(self.active_axis)
        won = actual is self.active_side

        if won:
            self.current_step = 1
            self.phase = EnginePhase.IDLE
            side = self.active_side
            self.active_side = None
            self.active_axis = None
            msg = f"WIN step={step_before} ({side}); reset to idle"
            self._log(msg)
            return SettleResult(
                outcome=BetOutcome.WIN,
                won=True,
                side=side,
                step_before=step_before,
                step_after=1,
                amount=amount,
                phase_after=EnginePhase.IDLE,
                message=msg,
            )

        # Loss
        if step_before >= self.config.max_step:
            side = self.active_side
            self.current_step = 1
            self.phase = EnginePhase.IDLE
            self.active_side = None
            self.active_axis = None
            msg = f"LOSE max-step={step_before} ({side}); cycle ended"
            self._log(msg)
            return SettleResult(
                outcome=BetOutcome.LOSE,
                won=False,
                side=side,
                step_before=step_before,
                step_after=1,
                amount=amount,
                phase_after=EnginePhase.IDLE,
                message=msg,
            )

        self.current_step = step_before + 1
        self.phase = EnginePhase.ACTIVE
        msg = (
            f"LOSE step={step_before} ({self.active_side}); "
            f"advance to step={self.current_step}"
        )
        self._log(msg)
        return SettleResult(
            outcome=BetOutcome.LOSE,
            won=False,
            side=self.active_side,
            step_before=step_before,
            step_after=self.current_step,
            amount=amount,
            phase_after=EnginePhase.ACTIVE,
            message=msg,
        )

    def _settle_without_advance(self, hidden_card: Card) -> SettleResult:
        if self._pending_round is None:
            raise RuntimeError("no open round to settle")

        round_ = self._pending_round
        round_.hidden_card = hidden_card
        self.history.append(round_)

        if self._bet_placed_this_round and self.active_side is not None and self.active_axis is not None:
            result = self._settle_active_bet(hidden_card)
        else:
            result = SettleResult(
                outcome=BetOutcome.SKIP,
                won=False,
                side=None,
                step_before=self.current_step,
                step_after=self.current_step,
                amount=0,
                phase_after=self.phase,
                message="no bet this round",
            )

        self.last_settle = result
        self._pending_round = None
        self._pending_decision = None
        self._bet_placed_this_round = False
        return result

    def _log(self, message: str) -> None:
        self.log.append(message)

    def seed_history(self, rounds: list[GameRound]) -> None:
        """Test/helper: preload settled rounds without going through begin/settle."""
        for r in rounds:
            if r.hidden_card is None:
                raise ValueError("seed rounds must be settled")
        self.history.extend(rounds)

    def play_round(
        self,
        round_id: int,
        open_card: Card,
        hidden_card: Card,
        *,
        auto_confirm: bool = True,
        shuffle_after: bool = False,
    ) -> tuple[BetDecision, SettleResult]:
        """
        Test/simulation helper: begin → optional confirm → settle.

        If ``shuffle_after`` is True, the shoe resets to 1판 after settle
        (martingale step preserved).
        """
        decision = self.begin_round(round_id, open_card)
        if auto_confirm and decision.place:
            self.confirm_bet_placed()
        if shuffle_after:
            settle = self._settle_without_advance(hidden_card)
            self.on_shuffle_detected()
        else:
            settle = self.settle_hidden(hidden_card)
        return decision, settle
