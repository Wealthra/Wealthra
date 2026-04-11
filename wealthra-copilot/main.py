"""
Wealthra Copilot — FastAPI Application.

Exposes the /chat endpoint that routes through the Orchestrator
state machine. Manages Redis lifecycle and uses Pydantic models
for request/response validation.
"""

import logging
from contextlib import asynccontextmanager

from fastapi import FastAPI, Header
from typing import Optional

from core.config import settings
from core.contracts import ChatRequest, ChatResponse
from agents.orchestrator import Orchestrator

# Configure logging
logging.basicConfig(
    level=logging.INFO,
    format="%(asctime)s | %(name)-25s | %(levelname)-5s | %(message)s",
)
logger = logging.getLogger(__name__)


# ---------------------------------------------------------------------------
# Application lifecycle
# ---------------------------------------------------------------------------

@asynccontextmanager
async def lifespan(app: FastAPI):
    """Startup / shutdown hooks."""
    logger.info("🦉 Wealthra Copilot starting up...")
    logger.info("   Project: %s", settings.PROJECT_NAME)
    yield
    logger.info("🦉 Wealthra Copilot shutting down...")


app = FastAPI(
    title=settings.PROJECT_NAME,
    description="Owlaris — Your Premium Financial Copilot",
    version="2.0.0",
    lifespan=lifespan,
)


# ---------------------------------------------------------------------------
# Shared orchestrator instance
# ---------------------------------------------------------------------------

_orchestrator: Optional[Orchestrator] = None


def _get_orchestrator() -> Orchestrator:
    """Lazy-init singleton orchestrator."""
    global _orchestrator
    if _orchestrator is None:
        _orchestrator = Orchestrator()
    return _orchestrator


# ---------------------------------------------------------------------------
# Endpoints
# ---------------------------------------------------------------------------

@app.get("/health")
def health_check():
    """Basic health check endpoint."""
    return {"status": "healthy", "service": "wealthra-copilot", "version": "2.0.0"}


@app.post("/chat", response_model=ChatResponse)
async def chat(
    message: str,
    user_id: str,
    authorization: Optional[str] = Header(default=None),
):
    """
    Main chat endpoint.

    Accepts a user message and routes it through the Orchestrator
    state machine. Optionally accepts an Authorization header with
    the user's JWT for forwarding to the .NET API during persist.
    """
    # Extract Bearer token if present
    auth_token = None
    if authorization:
        if authorization.startswith("Bearer "):
            auth_token = authorization[7:]
        else:
            # Fallback if user pastes raw JWT without Bearer prefix
            auth_token = authorization
    request = ChatRequest(
        message=message,
        user_id=user_id,
        auth_token=auth_token,
    )

    orchestrator = _get_orchestrator()
    response = await orchestrator.process(request)
    return response
