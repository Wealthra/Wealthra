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

    def __init__(self, model_fast: str | None = None, model_reasoning: str | None = None):
        fast_model = model_fast or settings.MODEL_FAST
        reasoning_model = model_reasoning or settings.MODEL_REASONING
        # Fast model: JSON extraction for write drafts
        self.llm_fast = ChatGroq(
            api_key=settings.GROQ_API_KEY,
            model_name=fast_model,
        )
        # Reasoning model: SQL agent for data queries
        self.llm_reasoning = ChatGroq(
            api_key=settings.GROQ_API_KEY,
            model_name=reasoning_model,
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
        7. LANGUAGE PURITY: Your response text MUST be entirely in the target language.
           - If target is Turkish: NO English, Chinese, Vietnamese, Japanese, Arabic, or any other language.
           - If target is English: NO Turkish, Chinese, Vietnamese, Japanese, Arabic, or any other language.
           - FORBIDDEN characters: any CJK ideographs, Vietnamese diacritics, Arabic script.
        8. Do not repeat the same number or fact more than once.
        """

        try:
            agent_executor = create_sql_agent(
                self.llm_reasoning,
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
        # Fetch available categories from DB (localized)
        categories_str = self._fetch_categories(lang)

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
3. category_name MUST exactly match one of the existing categories provided above.
   Pick the category that best fits the description.
4. Extract the CURRENCY (e.g., "TRY", "USD", "EUR", "GBP").
   - Default to "TRY" if not mentioned.
   - Map "dolar", "dollar", "$" to "USD".
   - Map "euro", "€" to "EUR".
   - Map "tl", "lira", "₺" to "TRY".
5. If the date is not mentioned, use null (system will default to today).
6. ALWAYS return an array, even for a single transaction.
7. Return ONLY the JSON — no explanation, no foreign characters in strings.
8. If the message contains an OVERALL TOTAL plus item-level amounts, treat the total as a CHECK,
   not as a separate transaction line.
9. If exactly one item amount is missing but an overall total is provided, infer the missing amount:
   missing_amount = total_amount - sum(other_item_amounts). Only do this when the result is positive.
10. Keep mathematical consistency: sum of item amounts should match stated overall total when possible.
11. Ignore narrative filler and storytelling details; extract only transactional facts.
12. If inference is ambiguous (multiple missing amounts, multiple conflicting totals, or non-positive result),
    leave ambiguous amount(s) as null instead of guessing.

ROBUST EXTRACTION POLICY (EN + TR):
- Detect item candidates from both explicit nouns and quantity patterns:
  "1 water", "2 coffees", "1LT milk", "bir su", "2 kahve", "1 litre süt".
- Detect total phrases in both languages:
  "total", "in total", "overall", "all in", "toplam", "totalde", "genel toplam", "tuttu".
- Do NOT create a separate transaction for "total/toplam" statements.
- If one line item has known amount and another does not, and one clear total exists, infer by subtraction.
- Preserve the user's language in descriptions when possible.

Return JSON array:
[
    {{
        "transaction_type": "expense" or "income",
        "amount": number or null,
        "currency": "TRY", "USD", "EUR", etc.,
        "description": string or null,
        "category_name": string (from existing categories) or null,
        "date": string (YYYY-MM-DD) or null,
        "payment_method": string or null,
        "is_recurring": false
    }}
]

JSON:
"""

        examples = """
EXAMPLES (follow these patterns exactly):

Example 1 (TR narrative):
Input: "Markete gittim, su aldım 10 TL, bir de süt aldım; totalde masrafım 45 TL."
Output:
[
  {"transaction_type":"expense","amount":10,"currency":"TRY","description":"su","category_name":"Market","date":null,"payment_method":null,"is_recurring":false},
  {"transaction_type":"expense","amount":35,"currency":"TRY","description":"süt","category_name":"Market","date":null,"payment_method":null,"is_recurring":false}
]

Example 2 (EN narrative):
Input: "I went to the store, got water for 10 TL and milk too, total was 45 TL."
Output:
[
  {"transaction_type":"expense","amount":10,"currency":"TRY","description":"water","category_name":"Market","date":null,"payment_method":null,"is_recurring":false},
  {"transaction_type":"expense","amount":35,"currency":"TRY","description":"milk","category_name":"Market","date":null,"payment_method":null,"is_recurring":false}
]

Example 3 (ambiguous, do not guess):
Input: "I bought water, milk, and bread; total 120 TL."
Output:
[
  {"transaction_type":"expense","amount":null,"currency":"TRY","description":"water","category_name":"Market","date":null,"payment_method":null,"is_recurring":false},
  {"transaction_type":"expense","amount":null,"currency":"TRY","description":"milk","category_name":"Market","date":null,"payment_method":null,"is_recurring":false},
  {"transaction_type":"expense","amount":null,"currency":"TRY","description":"bread","category_name":"Market","date":null,"payment_method":null,"is_recurring":false}
]

Example 4 (mixed language + quantity):
Input: "2 coffees aldım, each 60 TL, and cake 80 TL. toplam 200 TL."
Output:
[
  {"transaction_type":"expense","amount":120,"currency":"TRY","description":"coffee","category_name":"Food","date":null,"payment_method":null,"is_recurring":false},
  {"transaction_type":"expense","amount":80,"currency":"TRY","description":"cake","category_name":"Food","date":null,"payment_method":null,"is_recurring":false}
]
"""

        response = self.llm_fast.invoke(f"{prompt}\n{examples}")
        drafts = self._parse_batch_response(response.content)
        drafts = self._reconcile_missing_amount_from_total(message, drafts)

        # Merge with existing session batch (multi-turn info gathering)
        if session and session.batch and session.batch.items:
            drafts = self._merge_batches(session.batch, drafts)
            drafts = self._reconcile_missing_amount_from_total(message, drafts)

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
            "currency": draft.currency,
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
            "currency": draft.currency,
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

    def _fetch_categories(self, lang: str = "en") -> str:
        """Fetch available categories from the database in the target language."""
        db = SessionLocal()
        try:
            result = db.execute(
                text('SELECT "NameEn", "NameTr" FROM "Categories"')
            )
            # Use NameTr for Turkish, NameEn for others
            if lang == "tr":
                categories = [row[1] for row in result]
            else:
                categories = [row[0] for row in result]

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
        if new.currency is not None:
            merged.currency = new.currency
        if new.transaction_type != TransactionType.EXPENSE:
            merged.transaction_type = new.transaction_type

        return merged

    def _reconcile_missing_amount_from_total(
        self,
        message: str,
        batch: TransactionBatch,
    ) -> TransactionBatch:
        """
        Reliability backstop for multi-item parsing:
        if exactly one item amount is missing and a clear overall total exists
        in the message, infer the missing amount from subtraction.
        """
        if not batch.items or len(batch.items) < 2:
            return batch

        total_amount = self._extract_overall_total_amount(message)
        if total_amount is None:
            return batch

        missing_indices = [i for i, item in enumerate(batch.items) if item.amount is None]
        if len(missing_indices) != 1:
            return batch

        known_sum = sum((item.amount or 0.0) for item in batch.items)
        inferred = round(total_amount - known_sum, 2)
        if inferred <= 0:
            return batch

        updated_items = list(batch.items)
        target = updated_items[missing_indices[0]].model_copy()
        target.amount = inferred
        updated_items[missing_indices[0]] = target
        return TransactionBatch(items=updated_items)

    @staticmethod
    def _extract_overall_total_amount(message: str) -> float | None:
        """
        Extract a likely overall total amount from phrases like:
        - "toplam ... 45 TL"
        - "totalde ... 45 TL tuttu"
        - "in total 45"
        """
        total_patterns = [
            r'(?:totalde|toplamda|toplam|genel\s+toplam|in\s+total|overall|all\s+in)\D{0,24}(\d+(?:[.,]\d+)?)',
            r'(?:tüm\s+masraf(?:ım|im)?|masraf(?:ım|im)?\s+toplam[ıi])\D{0,24}(\d+(?:[.,]\d+)?)',
        ]

        for pattern in total_patterns:
            matches = re.findall(pattern, message, flags=re.IGNORECASE)
            if not matches:
                continue
            raw = matches[-1].replace(",", ".").strip()
            try:
                return float(raw)
            except ValueError:
                continue

        return None

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

