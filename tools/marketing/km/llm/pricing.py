"""What a Claude call costs, from its usage. US dollars per million tokens (Claude API, 2026-09).

Cache writes (5-minute TTL) cost 1.25x the input price; cache reads have their own price.
A model not listed here is priced as the most expensive one, so the cap errs on the safe side.
"""
from __future__ import annotations

# model: (input, output, cache read)
PRICES: dict[str, tuple[float, float, float]] = {
    "claude-opus-5-5": (4.00, 20.00, 0.20),
    "claude-sonnet-5-5": (2.00, 10.00, 0.20),
    "claude-opus-5": (5.00, 25.00, 0.50),
    "claude-sonnet-5": (2.00, 10.00, 0.20),
    "claude-opus-4-8": (5.00, 25.00, 0.50),
    "claude-haiku-4-5": (1.00, 5.00, 0.10),
}
UNKNOWN = (10.00, 50.00, 1.00)
CACHE_WRITE = 1.25


def price(model: str | None) -> tuple[float, float, float]:
    return PRICES.get(model or "", UNKNOWN)


def cost(model: str | None, input_tokens: int, output_tokens: int, cache_write: int = 0, cache_read: int = 0) -> float:
    p_in, p_out, p_read = price(model)
    usd = (input_tokens * p_in + cache_write * p_in * CACHE_WRITE + cache_read * p_read + output_tokens * p_out) / 1e6
    return round(usd, 6)


def estimate_tokens(text: str) -> int:
    """A deliberately high guess of a prompt's tokens (JSON and Cyrillic run about 3 characters a token)."""
    return int(len(text) / 3) + 50
