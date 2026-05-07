import asyncio
import logging
import random
import re
import time
from typing import Any

from core.config import settings

logger = logging.getLogger(__name__)

_THROTTLE_LOCK = asyncio.Lock()
_NEXT_ALLOWED_AT = 0.0
_RETRY_AFTER_SECONDS = re.compile(r"retry after\s*([0-9]+(?:\.[0-9]+)?)", re.IGNORECASE)


def is_rate_limited_error(exc: Exception) -> bool:
    text = str(exc).lower()
    return "429" in text or "too many requests" in text or "rate limit" in text


def _extract_retry_after(exc: Exception) -> float | None:
    match = _RETRY_AFTER_SECONDS.search(str(exc))
    if not match:
        return None
    try:
        return float(match.group(1))
    except ValueError:
        return None


async def _respect_min_interval() -> None:
    global _NEXT_ALLOWED_AT
    interval = max(0.0, settings.GROQ_MIN_INTERVAL_SECONDS)
    if interval == 0:
        return

    async with _THROTTLE_LOCK:
        now = time.monotonic()
        wait_seconds = _NEXT_ALLOWED_AT - now
        if wait_seconds > 0:
            await asyncio.sleep(wait_seconds)
        _NEXT_ALLOWED_AT = max(time.monotonic(), _NEXT_ALLOWED_AT) + interval


async def groq_invoke_with_retry(llm: Any, payload: Any, call_name: str) -> Any:
    """
    Shared guard for all LLM calls:
      1) small global throttle to prevent bursty request spikes
      2) bounded retry with exponential backoff on rate limits
    """
    max_retries = max(0, settings.GROQ_MAX_RETRIES)
    base_delay = max(0.1, settings.GROQ_RETRY_BASE_SECONDS)
    attempt = 0

    while True:
        await _respect_min_interval()
        try:
            return llm.invoke(payload)
        except Exception as exc:
            if attempt >= max_retries or not is_rate_limited_error(exc):
                raise

            retry_after = _extract_retry_after(exc)
            delay = retry_after if retry_after is not None else base_delay * (2 ** attempt)
            delay += random.uniform(0, 0.5)
            attempt += 1

            logger.warning(
                "LLM rate limit during %s (attempt %s/%s). Retrying in %.2fs",
                call_name,
                attempt,
                max_retries,
                delay,
            )
            await asyncio.sleep(delay)
