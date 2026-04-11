"""
RAG Specialist — The Data Layer.

Handles all database interactions through two modes:

  Read:  Executes SQL queries via LangChain SQL Agent to fetch
         historical spending, income, and category data. Returns
         structured QueryResult contracts.

  Write: Parses natural language into a TransactionDraft. Does NOT
         persist directly — returns a draft for the Orchestrator
         to manage through the confirmation flow.

  Persist: After user confirmation, calls the .NET API to save
           the transaction, preserving domain integrity (budget
           tracking, audit fields, domain events).
"""

import json
import re
import logging
from typing import Optional

import httpx
from langchain_groq import ChatGroq
from langchain_community.utilities import SQLDatabase
from langchain_community.agent_toolkits import create_sql_agent
from sqlalchemy import text

from core.config import settings
from core.database import SessionLocal
from core.contracts import (
    TransactionDraft,
    TransactionBatch,
    TransactionType,
    QueryResult,
    QueryRow,
    SessionData,
    SessionState,
)

logger = logging.getLogger(__name__)

# .NET API base URL (internal Docker network)
_API_BASE_URL = "http://api:8080/api"


class RAGSpecialist:
    """The Data Layer — handles all database interactions."""

    def __init__(self):
        self.llm = ChatGroq(
            api_key=settings.GROQ_API_KEY,
            model_name="llama-3.3-70b-versatile",
        )
        self.db = SQLDatabase.from_uri(settings.DATABASE_URL)

    # -----------------------------------------------------------------------
    # READ — Fetch historical data
    # -----------------------------------------------------------------------

    async def read(self, message: str, user_id: str, lang: str) -> QueryResult:
        """
        Execute SQL queries to fetch historical data.
        Returns a structured QueryResult contract.
        """
        lang_instruction = (
            "Yanıtını Türkçe olarak ver." if lang == "tr"
            else "Respond in English."
        )

        custom_prefix = f"""
        You are the Data Analyst for Wealthra.
        Your goal is to provide raw data summaries from the database using SELECT queries.

        CRITICAL RULES:
        1. YOU ARE A READ-ONLY AGENT. NEVER execute INSERT, UPDATE, DELETE, DROP, or ANY write operations.
           If the user asks to "add" or "save" something, politely remind them that you are only for viewing data.
        2. When asked for monthly summaries or date truncations, ALWAYS use Postgres-specific syntax:
           - Use `DATE_TRUNC('month', "TransactionDate")` for monthly groupings.
           - NEVER use `TRUNC()` with a date; that function does not exist in Postgres for dates.
        3. ALWAYS perform a JOIN between "Expenses" and "Categories" to get category names.
        4. Group by "Categories"."NameEn" or "Categories"."NameTr" as appropriate.
        5. ALWAYS use double quotes for all table and column names (e.g., "Expenses", "Amount").
        6. {lang_instruction}
7. Sadece ve sadece hedef dilde yanıt ver, araya başka dillerden karakter/kelime karıştırma.
8. Kendini tekrar etme (avoid redundancy), her bilgiyi bir kez ve öz söyle.
        """

        try:
            agent_executor = create_sql_agent(
                self.llm,
                db=self.db,
                agent_type="openai-tools",
                verbose=True,
                prefix=custom_prefix,
            )

            full_query = (
                f"{message} "
                "(Provide a comprehensive list of categories and amounts if requested)"
            )
            response = agent_executor.invoke({"input": full_query})
            raw_output = response["output"]

            return QueryResult(
                summary=raw_output,
                raw_text=raw_output,
            )
        except Exception as e:
            logger.error("RAG Read failed: %s", e)
            error_msg = (
                "Veritabanı sorgusu sırasında bir hata oluştu."
                if lang == "tr"
                else "An error occurred while querying the database."
            )
            return QueryResult(summary=error_msg, raw_text=str(e))

    # -----------------------------------------------------------------------
    # WRITE — Parse natural language into draft(s)
    # -----------------------------------------------------------------------

    async def write(
        self,
        message: str,
        user_id: str,
        session: SessionData,
        lang: str,
    ) -> TransactionBatch:
        """
        Parse natural language into one or more TransactionDrafts.

        Supports multi-item extraction:
          "45 TL kahve ve 120 TL kitap" → batch of 2 drafts

        Merges with existing session batch for multi-turn info gathering.
        Does NOT persist — returns batch for confirmation flow.
        """
        # Fetch available categories from DB
        categories_str = self._fetch_categories()

        prompt = f"""
Extract ALL financial transactions from the following message.
Return ONLY a valid JSON array — no markdown, no explanation.

Message: "{message}"

Existing Categories: {categories_str}

CRITICAL RULES:
1. If the message contains MULTIPLE transactions (e.g., "45 TL coffee and 120 TL book"),
   return EACH as a separate item in the array.
2. Determine if each item is an EXPENSE or INCOME:
   - Expenses: spending, buying, paying, harcama, ödeme, fatura, harcadım
   - Income: salary, payment received, earning, maaş, gelir, kazanç
3. category_name MUST exactly match one of the existing categories.
4. If the date is not mentioned, use null (system will default to today).
5. ALWAYS return an array, even for a single transaction.
6. Return ONLY the JSON — no explanation, no foreign characters in strings.

Return JSON array:
[
    {{
        "transaction_type": "expense" or "income",
        "amount": number or null,
        "description": string or null,
        "category_name": string (from existing categories) or null,
        "date": string (YYYY-MM-DD) or null,
        "payment_method": string or null,
        "is_recurring": false
    }}
]

JSON:
"""

        response = self.llm.invoke(prompt)
        drafts = self._parse_batch_response(response.content)

        # Merge with existing session batch (multi-turn info gathering)
        if session and session.batch and session.batch.items:
            drafts = self._merge_batches(session.batch, drafts)

        return drafts

    def _parse_batch_response(self, content: str) -> TransactionBatch:
        """Parse LLM JSON output into a TransactionBatch."""
        try:
            # Clean markdown fences if present
            cleaned = content.strip()
            if "```json" in cleaned:
                cleaned = cleaned.split("```json")[-1].split("```")[0]
            elif "```" in cleaned:
                cleaned = cleaned.split("```")[1].split("```")[0]

            parsed = json.loads(cleaned.strip())

            # Handle both array and single-object responses
            if isinstance(parsed, dict):
                parsed = [parsed]

            items = [TransactionDraft(**item) for item in parsed]
            return TransactionBatch(items=items)
        except Exception as e:
            logger.warning("Failed to parse batch JSON: %s — raw: %s", e, content)
            return TransactionBatch(items=[TransactionDraft()])

    def _merge_batches(
        self, existing: TransactionBatch, new: TransactionBatch
    ) -> TransactionBatch:
        """Merge new info into existing batch (fill in missing fields per item)."""
        # If batch sizes match, merge field by field
        if len(existing.items) == len(new.items):
            merged_items = []
            for old, updated in zip(existing.items, new.items):
                merged_items.append(self._merge_drafts(old, updated))
            return TransactionBatch(items=merged_items)

        # If the new batch has more items, it's a fresh extraction — use it
        # but carry over any info from the existing single draft
        if len(existing.items) == 1 and len(new.items) > 0:
            merged_items = []
            for item in new.items:
                merged_items.append(self._merge_drafts(existing.items[0], item))
            return TransactionBatch(items=merged_items)

        # Default: use the new batch as-is
        return new

    # -----------------------------------------------------------------------
    # PERSIST — Save via .NET API after confirmation
    # -----------------------------------------------------------------------

    async def persist(
        self,
        draft: TransactionDraft,
        user_id: str,
        auth_token: Optional[str] = None,
    ) -> dict:
        """
        After user confirmation, call the .NET API to save the transaction.
        Uses the user's JWT for authentication to preserve security context.
        """
        if draft.transaction_type == TransactionType.INCOME:
            return await self._persist_income(draft, auth_token)
        else:
            return await self._persist_expense(draft, auth_token)

    async def _persist_expense(
        self, draft: TransactionDraft, auth_token: Optional[str]
    ) -> dict:
        """POST to .NET Expenses API."""
        # Resolve category ID if we only have the name
        category_id = draft.category_id or self._resolve_category_id(
            draft.category_name
        )

        payload = {
            "description": draft.description or "",
            "amount": draft.amount,
            "paymentMethod": draft.payment_method or "Cash",
            "isRecurring": draft.is_recurring,
            "transactionDate": draft.date or self._today_iso(),
            "categoryId": category_id,
        }

        return await self._api_post(
            f"{_API_BASE_URL}/Expenses", payload, auth_token
        )

    async def _persist_income(
        self, draft: TransactionDraft, auth_token: Optional[str]
    ) -> dict:
        """POST to .NET Incomes API."""
        payload = {
            "name": draft.description or "",
            "amount": draft.amount,
            "method": draft.payment_method or "Cash",
            "isRecurring": draft.is_recurring,
            "transactionDate": draft.date or self._today_iso(),
        }

        return await self._api_post(
            f"{_API_BASE_URL}/Incomes", payload, auth_token
        )

    # -----------------------------------------------------------------------
    # Private helpers
    # -----------------------------------------------------------------------

    def _fetch_categories(self) -> str:
        """Fetch available categories from the database."""
        db = SessionLocal()
        try:
            result = db.execute(
                text('SELECT "NameEn", "NameTr" FROM "Categories"')
            )
            categories = [f"{row[0]} ({row[1]})" for row in result]
            return (
                ", ".join(categories)
                if categories
                else "Food, Market, Travel, Health, Entertainment, Education, Other"
            )
        except Exception as e:
            logger.warning("Failed to fetch categories: %s", e)
            return "Food, Market, Travel, Health, Entertainment, Education, Other"
        finally:
            db.close()

    def _resolve_category_id(self, category_name: Optional[str]) -> int:
        """Look up category ID by name. Returns first match or 1 as fallback."""
        if not category_name:
            return 1

        db = SessionLocal()
        try:
            result = db.execute(
                text(
                    'SELECT "Id" FROM "Categories" '
                    'WHERE LOWER("NameEn") = LOWER(:name) '
                    'OR LOWER("NameTr") = LOWER(:name) '
                    "LIMIT 1"
                ),
                {"name": category_name},
            )
            row = result.fetchone()
            return row[0] if row else 1
        except Exception as e:
            logger.warning("Failed to resolve category: %s", e)
            return 1
        finally:
            db.close()

    async def persist_batch(
        self,
        batch: 'TransactionBatch',
        user_id: str,
        auth_token: Optional[str] = None,
    ) -> dict:
        """
        Persist all items in a batch sequentially.
        Returns aggregate result with per-item status.
        """
        results = []
        all_success = True
        for i, draft in enumerate(batch.items):
            result = await self.persist(draft, user_id, auth_token)
            results.append({"index": i, **result})
            if not result.get("success"):
                all_success = False

        return {
            "success": all_success,
            "total_items": len(batch.items),
            "results": results,
        }

    def _merge_drafts(
        self, existing: TransactionDraft, new: TransactionDraft
    ) -> TransactionDraft:
        """Merge new info into existing draft (fill in missing fields)."""
        merged = existing.model_copy()

        if new.amount is not None:
            merged.amount = new.amount
        if new.description is not None:
            merged.description = new.description
        if new.category_name is not None:
            merged.category_name = new.category_name
        if new.date is not None:
            merged.date = new.date
        if new.payment_method is not None:
            merged.payment_method = new.payment_method
        if new.transaction_type != TransactionType.EXPENSE:
            merged.transaction_type = new.transaction_type

        return merged

    @staticmethod
    def _today_iso() -> str:
        """Return today's date in ISO format."""
        from datetime import date
        return date.today().isoformat()

    @staticmethod
    async def _api_post(
        url: str, payload: dict, auth_token: Optional[str]
    ) -> dict:
        """Make an authenticated POST to the .NET API."""
        headers = {"Content-Type": "application/json"}
        if auth_token:
            headers["Authorization"] = f"Bearer {auth_token}"

        async with httpx.AsyncClient(timeout=15.0) as client:
            try:
                resp = await client.post(url, json=payload, headers=headers)
                resp.raise_for_status()
                return {"success": True, "status_code": resp.status_code}
            except httpx.HTTPStatusError as e:
                logger.error("API POST failed: %s — %s", e.response.status_code, e)
                return {
                    "success": False,
                    "status_code": e.response.status_code,
                    "error": str(e),
                }
            except Exception as e:
                logger.error("API POST request error: %s", e)
                return {"success": False, "error": str(e)}

