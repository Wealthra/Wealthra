"""
In-memory session store for conversation state management.

Each user gets a session keyed by user_id with a TTL of 5 minutes.
The session holds the current state machine position (IDLE, PENDING_INFO,
PENDING_CONFIRM), the transaction draft, and detected language.

Uses a simple dict with expiry tracking — lightweight but ephemeral
(sessions are lost on service restart).
"""

import time
import logging
from typing import Optional

from core.contracts import SessionData, SessionState

logger = logging.getLogger(__name__)

# Session TTL in seconds (5 minutes)
SESSION_TTL = 300


class _Entry:
    """Internal wrapper that pairs session data with an expiry timestamp."""
    __slots__ = ("data", "expires_at")

    def __init__(self, data: str, ttl: int):
        self.data = data
        self.expires_at = time.monotonic() + ttl


class SessionStore:
    """
    In-memory session store keyed by user_id.

    Entries auto-expire after SESSION_TTL seconds.
    A lazy eviction strategy is used — expired entries are removed
    on access.  A periodic sweep is NOT needed because the copilot
    service lifetime is bounded by container restarts.
    """

    def __init__(self):
        self._store: dict[str, _Entry] = {}
        logger.info("Session store initialised (in-memory, TTL=%ds)", SESSION_TTL)

    async def get(self, user_id: str) -> SessionData:
        """Load session for a user. Returns a fresh IDLE session if none exists."""
        entry = self._store.get(user_id)

        if entry is not None:
            if time.monotonic() <= entry.expires_at:
                try:
                    return SessionData.model_validate_json(entry.data)
                except Exception:
                    logger.warning("Corrupt session for user %s, resetting.", user_id)
            else:
                # Expired — evict
                del self._store[user_id]

        return SessionData()

    async def save(self, user_id: str, session: SessionData) -> None:
        """Persist session with TTL."""
        self._store[user_id] = _Entry(session.model_dump_json(), SESSION_TTL)

    async def clear(self, user_id: str) -> None:
        """Reset session to IDLE."""
        self._store.pop(user_id, None)
