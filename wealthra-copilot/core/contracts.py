"""
Strict JSON contracts for inter-module communication.

Every specialist inputs and outputs these Pydantic models.
No raw strings are passed between modules — all data flows
through validated contracts to ensure structural integrity.
"""

from pydantic import BaseModel, Field
from typing import Optional, List, Dict, Any
from enum import Enum
from datetime import datetime


# ---------------------------------------------------------------------------
# Enums
# ---------------------------------------------------------------------------

class IntentType(str, Enum):
    WRITE = "write"
    READ = "read"
    HYBRID = "hybrid"
    SMALLTALK = "smalltalk"


class SessionState(str, Enum):
    IDLE = "IDLE"
    PENDING_INFO = "PENDING_INFO"
    PENDING_CONFIRM = "PENDING_CONFIRM"


class TransactionType(str, Enum):
    EXPENSE = "expense"
    INCOME = "income"


class ResponseType(str, Enum):
    QUERY = "query"
    DRAFT = "draft"
    CONFIRMATION = "confirmation"
    ADVISORY = "advisory"
    HYBRID = "hybrid"
    ERROR = "error"


# ---------------------------------------------------------------------------
# Request / Response Envelope
# ---------------------------------------------------------------------------

class ChatRequest(BaseModel):
    """Incoming chat request from the API layer."""
    message: str
    user_id: str
    auth_token: Optional[str] = None  # User JWT for forwarding to .NET API


class ChatResponse(BaseModel):
    """Unified response envelope returned to the frontend."""
    type: ResponseType
    message: str                                  # Human-readable response text
    language: str = "en"                          # Detected language (en/tr)
    payload: Optional[Dict[str, Any]] = None      # Structured data for UI rendering
    ui_hints: Optional[Dict[str, Any]] = None     # Frontend rendering hints


# ---------------------------------------------------------------------------
# Transaction Draft (RAG Write)
# ---------------------------------------------------------------------------

class TransactionDraft(BaseModel):
    """Draft transaction created from natural language input."""
    transaction_type: TransactionType = TransactionType.EXPENSE
    amount: Optional[float] = None
    description: Optional[str] = None
    category_name: Optional[str] = None
    category_id: Optional[int] = None
    date: Optional[str] = None                    # YYYY-MM-DD
    payment_method: Optional[str] = None
    is_recurring: bool = False

    def get_missing_fields(self) -> List[str]:
        """Return list of required fields that are still missing."""
        missing = []
        if self.amount is None:
            missing.append("amount")
        if self.description is None:
            missing.append("description")
        if self.category_name is None:
            missing.append("category")
        return missing

    def is_complete(self) -> bool:
        return len(self.get_missing_fields()) == 0


class TransactionBatch(BaseModel):
    """Batch of transaction drafts for multi-item messages."""
    items: List[TransactionDraft] = Field(default_factory=list)

    def all_complete(self) -> bool:
        return all(item.is_complete() for item in self.items)

    def get_incomplete_indices(self) -> List[int]:
        return [i for i, item in enumerate(self.items) if not item.is_complete()]

    @property
    def total_amount(self) -> float:
        return sum(item.amount or 0 for item in self.items)


# ---------------------------------------------------------------------------
# Query Result (RAG Read)
# ---------------------------------------------------------------------------

class QueryRow(BaseModel):
    """A single row in a query result."""
    label: str
    value: float
    metadata: Optional[Dict[str, Any]] = None


class QueryResult(BaseModel):
    """Structured result from a RAG Read operation."""
    summary: str                                  # Human-readable summary
    rows: List[QueryRow] = Field(default_factory=list)
    totals: Optional[Dict[str, float]] = None     # Aggregated totals
    raw_text: Optional[str] = None                # Fallback raw LLM output


# ---------------------------------------------------------------------------
# Consultant Advice
# ---------------------------------------------------------------------------

class InsightItem(BaseModel):
    """A single financial insight or suggestion."""
    icon: str = "💡"                               # Emoji for UI rendering
    title: str
    detail: str


class ConsultantAdvice(BaseModel):
    """Structured output from the Consultant Specialist."""
    insights: List[InsightItem] = Field(default_factory=list)
    warnings: List[InsightItem] = Field(default_factory=list)
    suggestions: List[InsightItem] = Field(default_factory=list)
    overall_score: Optional[str] = None           # e.g., "Good", "Needs Attention"


# ---------------------------------------------------------------------------
# Hybrid Result (Data + Advice)
# ---------------------------------------------------------------------------

class HybridResult(BaseModel):
    """Combined output for hybrid execution (read + consult)."""
    raw_data: QueryResult
    statistics: Dict[str, Any] = Field(default_factory=dict)
    advice: ConsultantAdvice


# ---------------------------------------------------------------------------
# Session Data
# ---------------------------------------------------------------------------

class SessionData(BaseModel):
    """Serializable session state persisted across conversation turns."""
    state: SessionState = SessionState.IDLE
    draft: Optional[TransactionDraft] = None
    batch: Optional[TransactionBatch] = None      # For multi-item transactions
    missing_fields: List[str] = Field(default_factory=list)
    language: str = "en"
    last_intent: Optional[str] = None
    pending_action: Optional[str] = None          # Follow-up intent after confirmation
    auth_token: Optional[str] = None              # Cached user JWT
