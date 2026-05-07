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

_orchestrators: dict[str, Orchestrator] = {}


def _normalize_model_or_default(candidate: Optional[str], default_model: str, slot_name: str) -> str:
    """Allow only Gemini model identifiers; fallback safely for legacy values."""
    if not candidate or not candidate.strip():
        return default_model

    model = candidate.strip()
    lowered = model.lower()
    if lowered.startswith("gemini-") or lowered.startswith("models/gemini-"):
        return model

    logger.warning(
        "Ignoring unsupported %s model '%s' for Gemini backend. Falling back to '%s'.",
        slot_name,
        model,
        default_model,
    )
    return default_model


def _get_orchestrator(chat_model: str, enrichment_model: str) -> Orchestrator:
    """Lazy-init orchestrator instances keyed by both models."""
    key = f"{chat_model}::{enrichment_model}"
    if key not in _orchestrators:
        _orchestrators[key] = Orchestrator(
            model_fast=enrichment_model,
            model_reasoning=chat_model,
        )
    return _orchestrators[key]


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
    start_date: Optional[str] = None,
    end_date: Optional[str] = None,
    default_chat_model: Optional[str] = None,
    enrichment_model: Optional[str] = None,
    authorization: Optional[str] = Header(default=None),
):
    """
    Main chat endpoint.

    Accepts dynamic model selection from the frontend (chat vs enrichment).
    Optionally accepts an Authorization header with the user's JWT for
    forwarding to the .NET API during persist.
    """
    auth_token = None
    if authorization:
        if authorization.startswith("Bearer "):
            auth_token = authorization[7:]
        else:
            auth_token = authorization

    request = ChatRequest(
        message=message,
        user_id=user_id,
        start_date=start_date,
        end_date=end_date,
        auth_token=auth_token,
    )

    c_model = _normalize_model_or_default(
        default_chat_model,
        settings.MODEL_REASONING,
        "default_chat_model",
    )
    e_model = _normalize_model_or_default(
        enrichment_model,
        settings.MODEL_FAST,
        "enrichment_model",
    )

    orchestrator = _get_orchestrator(chat_model=c_model, enrichment_model=e_model)
    response = await orchestrator.process(request)
    return response
