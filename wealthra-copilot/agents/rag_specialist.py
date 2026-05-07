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

import re
import logging
import ast
from datetime import datetime, timedelta
from typing import Optional, Any

import httpx
from langchain_google_genai import ChatGoogleGenerativeAI
from langchain_community.utilities import SQLDatabase
from langchain_community.agent_toolkits import create_sql_agent
from sqlalchemy import text

from core.config import settings
from core.database import SessionLocal
from core.llm_utils import groq_invoke_with_retry, is_rate_limited_error
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
_READ_TABLES = ["Expenses", "Incomes", "Categories"]
_NON_ANSWER_SQL_PATTERNS = (
    "i can see that there are tables",
    "i should look at the schema",
    "let me see what tables are available",
    "need to look at the schema",
    "what columns i can query",
    "table schema",
    "the query will look something like this",
    "let's execute this query",
)
_SQL_ERROR_PATTERNS = (
    "undefinedcolumn",
    "column does not exist",
    "psycopg2.errors",
    "sqlalchemy.exc",
    "error:",
    "traceback",
)
_PLACEHOLDER_RESULT_PATTERNS = (
    "kategori 1",
    "kategori 2",
    "kategori 3",
    "category 1",
    "category 2",
    "category 3",
)
_MONTH_ALIASES = {
    1: ("january", "jan", "ocak"),
    2: ("february", "feb", "şubat", "subat"),
    3: ("march", "mar", "mart"),
    4: ("april", "apr", "nisan"),
    5: ("may", "mayıs", "mayis"),
    6: ("june", "jun", "haziran"),
    7: ("july", "jul", "temmuz"),
    8: ("august", "aug", "ağustos", "agustos"),
    9: ("september", "sep", "sept", "eylül", "eylul"),
    10: ("october", "oct", "ekim"),
    11: ("november", "nov", "kasım", "kasim"),
    12: ("december", "dec", "aralık", "aralik"),
}


def _extract_text_content(content: Any) -> str:
    """Normalize LLM/tool output payloads (str/list/dict) into plain text."""
    if isinstance(content, str):
        return _sanitize_sql_agent_output(content)
    if isinstance(content, list):
        parts: list[str] = []
        for item in content:
            if isinstance(item, str):
                parts.append(item)
            elif isinstance(item, dict):
                text_value = item.get("text")
                if isinstance(text_value, str):
                    parts.append(text_value)
                else:
                    parts.append(str(item))
            else:
                text_attr = getattr(item, "text", None)
                if isinstance(text_attr, str):
                    parts.append(text_attr)
                else:
                    parts.append(str(item))
        return _sanitize_sql_agent_output("\n".join(p for p in parts if p).strip())
    if isinstance(content, dict):
        text_value = content.get("text")
        if isinstance(text_value, str):
            return _sanitize_sql_agent_output(text_value)
    return _sanitize_sql_agent_output(str(content))


def _sanitize_sql_agent_output(raw: str) -> str:
    """
    Strip verbose Gemini tool artifacts from SQL-agent outputs.
    This keeps downstream prompts small and avoids long waits after chain completion.
    """
    text = (raw or "").strip()
    if not text:
        return text

    # Typical shape: "<rows_repr>[{'type': 'text', 'text': '...', 'extras': {...}}]"
    marker = "[{'type': 'text'"
    idx = text.find(marker)
    if idx >= 0:
        prefix = text[:idx].strip()
        block = text[idx:].strip()
        llm_text: str | None = None

        try:
            parsed = ast.literal_eval(block)
            if (
                isinstance(parsed, list)
                and parsed
                and isinstance(parsed[0], dict)
                and isinstance(parsed[0].get("text"), str)
            ):
                llm_text = parsed[0]["text"].strip()
        except Exception:
            match = re.search(r"'text':\s*'(.+?)'\s*,\s*'index'", block, flags=re.DOTALL)
            if match:
                llm_text = match.group(1).strip()

        if llm_text:
            # If rows are present, keep concise row preview + natural-language summary only.
            if prefix:
                return f"{prefix}\n{llm_text}"
            return llm_text

    # Remove oversized signature blobs if present in raw text.
    text = re.sub(r"'signature':\s*'[^']+'", "'signature':'<omitted>'", text)
    return text


class RAGSpecialist:
    """The Data Layer — handles all database interactions."""

    def __init__(
        self,
        model_fast: str | None = None,
        model_sql_primary: str | None = None,
        model_sql_fallback: str | None = None,
    ):
        fast_model = model_fast or settings.MODEL_FAST
        sql_primary_model = model_sql_primary or settings.MODEL_SQL_PRIMARY
        sql_fallback_model = model_sql_fallback or settings.MODEL_SQL_FALLBACK
        # Fast model: structured extraction for write drafts
        self.llm_fast = ChatGoogleGenerativeAI(
            google_api_key=settings.LLM_API_KEY,
            model=fast_model,
        ).with_structured_output(TransactionBatch)
        # Primary SQL model: high-throughput read/query planning.
        self.llm_sql_primary = ChatGoogleGenerativeAI(
            google_api_key=settings.LLM_API_KEY,
            model=sql_primary_model,
        )
        # Fallback SQL model: used when primary model is rate limited.
        self.llm_sql_fallback = ChatGoogleGenerativeAI(
            google_api_key=settings.LLM_API_KEY,
            model=sql_fallback_model,
        )
        # Keep SQL agent context narrowly scoped to financial tables.
        self.db = SQLDatabase.from_uri(
            settings.DATABASE_URL,
            include_tables=_READ_TABLES,
        )

    # -----------------------------------------------------------------------
    # READ — Fetch historical data
    # -----------------------------------------------------------------------

    async def read(
        self,
        message: str,
        user_id: str,
        lang: str,
        start_date: Optional[str] = None,
        end_date: Optional[str] = None,
    ) -> QueryResult:
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
        AUTHORIZED USER ID: "{user_id}"

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
        9. IMPORTANT SCHEMA:
           - "Expenses"."CategoryId" references "Categories"."Id"
           - JOIN MUST be: "Expenses"."CategoryId" = "Categories"."Id"
           - Use "Expenses"."TransactionDate" for expense dates.
           - There is no "Transactions" table in this context.
           - Currency columns exist as "Expenses"."Currency" and "Incomes"."Currency".
        10. NEVER fabricate values. If a query fails, return a short error summary only.
        11. USER SCOPING IS MANDATORY:
            - Any query reading "Expenses" MUST include filter:
              "Expenses"."CreatedBy" = '{user_id}'
            - Any query reading "Incomes" MUST include filter:
              "Incomes"."CreatedBy" = '{user_id}'
            - Never return aggregate or row data from other users.
        12. DATE RANGE RULES:
            - If the user message clearly specifies a period (e.g., "May", "last month", specific dates),
              apply date filtering on the relevant "TransactionDate" column.
            - If backend context dates are provided, use:
              start_date={start_date or "None"}, end_date={end_date or "None"}
              as additional bounds.
            - If no period is provided in message and no backend date bounds exist, do not force a date filter.
        13. CURRENCY RULES ARE MANDATORY:
            - NEVER sum or compare amounts across different currencies in a single total.
            - For aggregated expense/income outputs, include "Currency" in SELECT and GROUP BY.
            - If multiple currencies exist, return separate rows per currency.
        """

        try:
            full_query = self._build_sql_user_prompt(
                message=message,
                user_id=user_id,
                start_date=start_date,
                end_date=end_date,
            )
            raw_output = await self._run_sql_agent(
                llm=self.llm_sql_primary,
                full_query=full_query,
                custom_prefix=custom_prefix,
                call_name="rag.read_sql_agent.primary",
            )
            recovered = self._execute_embedded_sql_if_present(raw_output, user_id)
            if recovered:
                raw_output = recovered
            if self._is_non_answer_sql_output(raw_output):
                logger.warning("Primary SQL model produced non-answer output; retrying with stricter prompt.")
                raw_output = await self._run_sql_agent(
                    llm=self.llm_sql_primary,
                    full_query=self._build_sql_user_prompt(
                        message=message,
                        user_id=user_id,
                        start_date=start_date,
                        end_date=end_date,
                        force_execute=True,
                    ),
                    custom_prefix=custom_prefix,
                    call_name="rag.read_sql_agent.primary_strict",
                )
                recovered = self._execute_embedded_sql_if_present(raw_output, user_id)
                if recovered:
                    raw_output = recovered
            if self._is_non_answer_sql_output(raw_output):
                logger.warning("Primary SQL strict retry still weak; switching to SQL fallback model.")
                raw_output = await self._run_sql_agent(
                    llm=self.llm_sql_fallback,
                    full_query=self._build_sql_user_prompt(
                        message=message,
                        user_id=user_id,
                        start_date=start_date,
                        end_date=end_date,
                        force_execute=True,
                    ),
                    custom_prefix=custom_prefix,
                    call_name="rag.read_sql_agent.fallback_strict",
                )
                recovered = self._execute_embedded_sql_if_present(raw_output, user_id)
                if recovered:
                    raw_output = recovered
            if self._is_invalid_sql_output(raw_output):
                logger.error("SQL agent returned invalid/fabricated output after retries.")
                deterministic = self._run_deterministic_read(
                    message=message,
                    user_id=user_id,
                    lang=lang,
                    start_date=start_date,
                    end_date=end_date,
                )
                if deterministic:
                    return deterministic
            if self._is_non_answer_sql_output(raw_output):
                deterministic = self._run_deterministic_read(
                    message=message,
                    user_id=user_id,
                    lang=lang,
                    start_date=start_date,
                    end_date=end_date,
                )
                if deterministic:
                    return deterministic
                return self._build_read_failure_result(lang, raw_output)

            return QueryResult(
                summary=raw_output,
                raw_text=raw_output,
            )
        except Exception as e:
            if is_rate_limited_error(e):
                logger.warning("Primary SQL model rate-limited; switching to SQL fallback model.")
                try:
                    full_query = self._build_sql_user_prompt(
                        message=message,
                        user_id=user_id,
                        start_date=start_date,
                        end_date=end_date,
                        force_execute=True,
                    )
                    raw_output = await self._run_sql_agent(
                        llm=self.llm_sql_fallback,
                        full_query=full_query,
                        custom_prefix=custom_prefix,
                        call_name="rag.read_sql_agent.fallback",
                    )
                    recovered = self._execute_embedded_sql_if_present(raw_output, user_id)
                    if recovered:
                        raw_output = recovered
                    if self._is_invalid_sql_output(raw_output):
                        logger.error("SQL fallback output invalid after rate-limit fallback.")
                        deterministic = self._run_deterministic_read(
                            message=message,
                            user_id=user_id,
                            lang=lang,
                            start_date=start_date,
                            end_date=end_date,
                        )
                        if deterministic:
                            return deterministic
                        return self._build_read_failure_result(lang, raw_output)
                    if self._is_non_answer_sql_output(raw_output):
                        deterministic = self._run_deterministic_read(
                            message=message,
                            user_id=user_id,
                            lang=lang,
                            start_date=start_date,
                            end_date=end_date,
                        )
                        if deterministic:
                            return deterministic
                    return QueryResult(
                        summary=raw_output,
                        raw_text=raw_output,
                    )
                except Exception as fallback_error:
                    logger.error("RAG Read fallback failed: %s", fallback_error)
            deterministic = self._run_deterministic_read(
                message=message,
                user_id=user_id,
                lang=lang,
                start_date=start_date,
                end_date=end_date,
            )
            if deterministic:
                return deterministic
            logger.error("RAG Read failed: %s", e)
            error_msg = (
                "Veritabanı sorgusu sırasında bir hata oluştu."
                if lang == "tr"
                else "An error occurred while querying the database."
            )
            return QueryResult(summary=error_msg, raw_text=str(e))

    async def _run_sql_agent(
        self,
        llm: Any,
        full_query: str,
        custom_prefix: str,
        call_name: str,
    ) -> str:
        """Build and execute a SQL agent call with shared safeguards."""
        agent_executor = create_sql_agent(
            llm,
            db=self.db,
            agent_type="openai-tools",
            verbose=True,
            prefix=custom_prefix,
            use_query_checker=False,
            max_iterations=6,
        )
        response = await groq_invoke_with_retry(
            agent_executor,
            {"input": full_query},
            call_name,
        )
        return _extract_text_content(response.get("output"))

    @staticmethod
    def _build_sql_user_prompt(
        message: str,
        user_id: str,
        start_date: Optional[str] = None,
        end_date: Optional[str] = None,
        force_execute: bool = False,
    ) -> str:
        """Build user prompt for SQL agent with optional strict execution requirements."""
        date_context = (
            f"start_date={start_date or 'None'}, end_date={end_date or 'None'}"
        )
        base = (
            f'{message} '
            '(Provide a comprehensive list of categories and amounts if requested.) '
            f'CURRENT_USER_ID="{user_id}". '
            f'DATE_CONTEXT: {date_context}. '
            'MANDATORY: scope results to CURRENT_USER_ID using "CreatedBy" filters.'
        )
        if not force_execute:
            return base
        return (
            f"{base} "
            "IMPORTANT: You MUST execute a final sql_db_query before answering. "
            "Do NOT stop at listing tables or schemas. "
            "Return only the final data answer with concrete numbers."
        )

    @staticmethod
    def _is_non_answer_sql_output(output: str) -> bool:
        """Detect planning/meta outputs that did not actually answer the question with data."""
        lowered = (output or "").strip().lower()
        if not lowered:
            return True
        if any(p in lowered for p in _NON_ANSWER_SQL_PATTERNS):
            return True
        if "```sql" in lowered:
            return True
        # If no numeric signal exists, it is often a planning response.
        has_number = bool(re.search(r"\d", lowered))
        return not has_number

    @staticmethod
    def _is_invalid_sql_output(output: str) -> bool:
        """
        Detect outputs that should never be used for user-facing analytics:
        SQL/runtime errors or obvious placeholder/fabricated category labels.
        """
        lowered = (output or "").strip().lower()
        if not lowered:
            return True
        if any(p in lowered for p in _SQL_ERROR_PATTERNS):
            return True
        if any(p in lowered for p in _PLACEHOLDER_RESULT_PATTERNS):
            return True
        return False

    @staticmethod
    def _build_read_failure_result(lang: str, raw_output: str) -> QueryResult:
        """Return a safe, non-fabricated read response when SQL output is invalid."""
        msg = (
            "Sorguyu güvenilir şekilde tamamlayamadım. Lütfen tekrar deneyin veya soruyu daha net yazın."
            if lang == "tr"
            else "I couldn't complete the query reliably. Please try again or rephrase your request."
        )
        return QueryResult(summary=msg, raw_text=raw_output or msg)

    def _execute_embedded_sql_if_present(self, output: str, user_id: str) -> str | None:
        """
        Recovery path: if agent returns a draft SQL query instead of executing it,
        extract SELECT SQL and execute it directly in read-only mode.
        """
        sql = self._extract_select_sql(output)
        if not sql:
            return None
        if not self._is_user_scoped_sql(sql, user_id):
            logger.warning("Rejected embedded SQL without strict user scope.")
            return None

        db = SessionLocal()
        try:
            result = db.execute(text(sql))
            rows = result.fetchall()
            columns = list(result.keys())
            if not rows:
                return "Query executed successfully but returned no rows."

            preview_rows = rows[:20]
            lines = [f"Columns: {', '.join(columns)}", "Rows:"]
            for row in preview_rows:
                values = [str(v) for v in row]
                lines.append(" | ".join(values))
            if len(rows) > 20:
                lines.append(f"... ({len(rows) - 20} more rows)")
            return "\n".join(lines)
        except Exception as e:
            logger.warning("Embedded SQL execution failed: %s", e)
            return None
        finally:
            db.close()

    @staticmethod
    def _extract_select_sql(output: str) -> str | None:
        """Extract first SELECT statement from model text."""
        text_blob = (output or "").strip()
        if not text_blob:
            return None

        fence_match = re.search(r"```sql\s*(.*?)```", text_blob, flags=re.IGNORECASE | re.DOTALL)
        candidate = fence_match.group(1).strip() if fence_match else None
        if not candidate:
            select_match = re.search(r"(select\s+.*?;)", text_blob, flags=re.IGNORECASE | re.DOTALL)
            candidate = select_match.group(1).strip() if select_match else None
        if not candidate:
            return None

        normalized = candidate.strip().rstrip(";")
        if not normalized.lower().startswith("select"):
            return None
        # Disallow multi-statement SQL for safety.
        if ";" in normalized:
            return None
        return f"{normalized};"

    @staticmethod
    def _is_user_scoped_sql(sql: str, user_id: str) -> bool:
        """Require explicit CreatedBy=user_id predicate in direct SQL recovery mode."""
        lowered = sql.lower()
        escaped = user_id.replace("'", "''").lower()
        if '"createdby"' not in lowered:
            return False
        if escaped not in lowered:
            return False
        return True

    def _run_deterministic_read(
        self,
        message: str,
        user_id: str,
        lang: str,
        start_date: Optional[str],
        end_date: Optional[str],
    ) -> QueryResult | None:
        """
        Deterministic fallback for common category analytics when tool-calling fails.
        Always scoped to the authenticated user.
        """
        query_kind = self._detect_deterministic_query_kind(message)
        if not query_kind:
            return None

        range_start, range_end_excl = self._resolve_effective_date_range(
            message=message,
            start_date=start_date,
            end_date=end_date,
        )
        if range_start and range_end_excl and range_start >= range_end_excl:
            return None

        category_col = '"NameTr"' if lang == "tr" else '"NameEn"'
        base_sql = (
            f'SELECT c.{category_col} AS "Category", e."Currency" AS "Currency", '
            'SUM(e."Amount") AS "TotalAmount" '
            'FROM "Expenses" e '
            'JOIN "Categories" c ON e."CategoryId" = c."Id" '
            'WHERE e."CreatedBy" = :user_id'
        )
        params: dict[str, object] = {"user_id": user_id}
        if range_start:
            base_sql += ' AND e."TransactionDate" >= :date_start'
            params["date_start"] = range_start
        if range_end_excl:
            base_sql += ' AND e."TransactionDate" < :date_end_excl'
            params["date_end_excl"] = range_end_excl

        if query_kind == "top_category":
            sql = (
                "WITH ranked AS ("
                f"{base_sql} "
                f"GROUP BY c.{category_col}, e.\"Currency\""
                "), scoped AS ("
                'SELECT "Category", "Currency", "TotalAmount", '
                'ROW_NUMBER() OVER (PARTITION BY "Currency" ORDER BY "TotalAmount" DESC) AS rn '
                "FROM ranked"
                ") "
                'SELECT "Category", "Currency", "TotalAmount" FROM scoped WHERE rn = 1 '
                'ORDER BY "Currency"'
            )
        else:
            sql = (
                f"{base_sql} "
                f"GROUP BY c.{category_col}, e.\"Currency\" "
                'ORDER BY "Currency", "TotalAmount" DESC '
                "LIMIT 20"
            )

        db = SessionLocal()
        try:
            rows = db.execute(text(sql), params).fetchall()
            if not rows:
                msg = (
                    "Bu kriterlerde harcama verisi bulunamadı."
                    if lang == "tr"
                    else "No expense data was found for these filters."
                )
                return QueryResult(summary=msg, raw_text=msg)

            if query_kind == "top_category":
                parts = []
                for row in rows:
                    category, currency, amount = row[0], row[1], row[2]
                    if lang == "tr":
                        parts.append(f"{currency}: {category} ({amount})")
                    else:
                        parts.append(f"{currency}: {category} ({amount})")
                if lang == "tr":
                    summary = "Para birimine göre en yüksek harcama kategorileri: " + "; ".join(parts)
                else:
                    summary = "Top spending categories by currency: " + "; ".join(parts)
                return QueryResult(summary=summary, raw_text=summary)

            lines = []
            for row in rows:
                lines.append(f"{row[1]} | {row[0]}: {row[2]}")
            summary = "\n".join(lines)
            return QueryResult(summary=summary, raw_text=summary)
        except Exception as ex:
            logger.warning("Deterministic read fallback failed: %s", ex)
            return None
        finally:
            db.close()

    @staticmethod
    def _detect_deterministic_query_kind(message: str) -> Optional[str]:
        """
        Detect common category analytics intents:
        - top_category
        - category_breakdown
        """
        m = (message or "").lower()
        has_category = any(x in m for x in ["kategori", "category"])
        has_spend = any(x in m for x in ["harca", "harcam", "spend", "expense"])
        if not (has_category and has_spend):
            return None
        is_top = any(x in m for x in ["en fazla", "en çok", "highest", "most", "top"])
        return "top_category" if is_top else "category_breakdown"

    @classmethod
    def _resolve_effective_date_range(
        cls,
        message: str,
        start_date: Optional[str],
        end_date: Optional[str],
    ) -> tuple[Optional[datetime], Optional[datetime]]:
        """Resolve date range from message and optional backend bounds."""
        msg_start, msg_end_excl = cls._extract_month_range_from_message(message)
        ctx_start, ctx_end_excl = cls._parse_context_date_bounds(start_date, end_date)

        starts = [d for d in [msg_start, ctx_start] if d is not None]
        ends = [d for d in [msg_end_excl, ctx_end_excl] if d is not None]
        effective_start = max(starts) if starts else None
        effective_end = min(ends) if ends else None
        return effective_start, effective_end

    @staticmethod
    def _parse_context_date_bounds(
        start_date: Optional[str],
        end_date: Optional[str],
    ) -> tuple[Optional[datetime], Optional[datetime]]:
        """Parse API-provided date bounds into [start, end_exclusive)."""
        def parse_iso(d: Optional[str]) -> Optional[datetime]:
            if not d:
                return None
            try:
                return datetime.strptime(d, "%Y-%m-%d")
            except ValueError:
                return None

        s = parse_iso(start_date)
        e = parse_iso(end_date)
        end_excl = e + timedelta(days=1) if e else None
        return s, end_excl

    @classmethod
    def _extract_month_range_from_message(
        cls,
        message: str,
    ) -> tuple[Optional[datetime], Optional[datetime]]:
        """Extract month range from TR/EN month names in the message."""
        lowered = (message or "").lower()
        if not lowered:
            return None, None

        matched_month = None
        for month_num, names in _MONTH_ALIASES.items():
            if any(re.search(rf"\b{re.escape(name)}\b", lowered) for name in names):
                matched_month = month_num
                break
        if not matched_month:
            return None, None

        year_match = re.search(r"\b(20\d{2})\b", lowered)
        year = int(year_match.group(1)) if year_match else datetime.utcnow().year
        start = datetime(year, matched_month, 1)
        if matched_month == 12:
            end_excl = datetime(year + 1, 1, 1)
        else:
            end_excl = datetime(year, matched_month + 1, 1)
        return start, end_excl

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
Extract financial transactions from the user message.
Message: "{message}"
Categories: {categories_str}

Rules:
1. Extract multiple items if listed (e.g. "coffee and book").
2. Match categories exactly to the list provided.
3. Determine expense vs income from context (spending vs salary/gelir).
4. Default currency TRY; map $/dolar to USD, €/euro to EUR, tl/₺ to TRY.
5. If an item price is missing but a total is given, infer the difference only when unambiguous; otherwise use null for amount.
6. Do not create a separate transaction for words like "total" or "toplam".
7. Use null for date if not mentioned.
"""

        drafts: TransactionBatch = await groq_invoke_with_retry(
            self.llm_fast,
            prompt,
            "rag.write_extraction",
        )
        if self._is_low_confidence_batch(drafts):
            fallback = self._rule_based_extract_batch(message, categories_str)
            if fallback.items:
                drafts = fallback
        drafts = self._reconcile_missing_amount_from_total(message, drafts)

        # Merge with existing session batch (multi-turn info gathering)
        if session and session.batch and session.batch.items:
            drafts = self._merge_batches(session.batch, drafts)
            drafts = self._reconcile_missing_amount_from_total(message, drafts)

        return drafts

    @staticmethod
    def _is_low_confidence_batch(batch: TransactionBatch) -> bool:
        """
        Detect obviously bad extraction outputs.
        Typical failure mode is a single default/empty draft after JSON parse errors.
        """
        if not batch.items:
            return True

        if len(batch.items) == 1:
            item = batch.items[0]
            if item.amount is None and item.description is None and item.category_name is None:
                return True

        complete_items = sum(
            1 for item in batch.items
            if item.amount is not None and item.description is not None
        )
        return complete_items == 0

    def _rule_based_extract_batch(
        self, message: str, categories_str: str
    ) -> TransactionBatch:
        """
        Lightweight deterministic extractor used as a fallback when LLM output is unusable.
        Focuses on common expense narration patterns in TR/EN.
        """
        categories = [c.strip() for c in categories_str.split(",") if c.strip()]
        msg = message.strip()
        drafts: list[TransactionDraft] = []
        seen_desc: set[str] = set()

        amount_desc_patterns = [
            re.compile(
                r'(?P<amount>\d+(?:[.,]\d+)?)\s*(?:tl|lira|₺|try)\s*(?:["\'’]?(?:ye|ya))?\s*'
                r'(?P<desc>[a-zA-ZçğıöşüÇĞİÖŞÜ0-9\s]{2,40}?)(?=\s*(?:ald[ıi]m|harcad[ıi]m|ödedim|için|for|ve|,|;|\.|$))',
                flags=re.IGNORECASE,
            ),
            re.compile(
                r'(?P<desc>[a-zA-ZçğıöşüÇĞİÖŞÜ0-9\s]{2,40}?)\s*'
                r'(?P<amount>\d+(?:[.,]\d+)?)\s*(?:tl|lira|₺|try)',
                flags=re.IGNORECASE,
            ),
        ]

        for pattern in amount_desc_patterns:
            for match in pattern.finditer(msg):
                raw_desc = (match.group("desc") or "").strip(" ,.;:-")
                desc = self._clean_item_description(raw_desc)
                amount_raw = match.group("amount").replace(",", ".")
                if not desc:
                    continue
                try:
                    amount = float(amount_raw)
                except ValueError:
                    continue

                desc_key = desc.lower()
                if desc_key in seen_desc:
                    continue
                seen_desc.add(desc_key)

                drafts.append(
                    TransactionDraft(
                        transaction_type=TransactionType.EXPENSE,
                        amount=amount,
                        currency="TRY",
                        description=desc,
                        category_name=self._pick_category(desc, msg, categories),
                    )
                )

        implicit_desc_pattern = re.compile(
            r'(?:bir\s+(?:adet|tane)\s+)?(?P<desc>[a-zA-ZçğıöşüÇĞİÖŞÜ]{2,30})\s+ald[ıi]m',
            flags=re.IGNORECASE,
        )
        for match in implicit_desc_pattern.finditer(msg):
            desc = self._clean_item_description((match.group("desc") or "").strip())
            if not desc:
                continue
            desc_key = desc.lower()
            if desc_key in seen_desc:
                continue
            seen_desc.add(desc_key)

            drafts.append(
                TransactionDraft(
                    transaction_type=TransactionType.EXPENSE,
                    amount=None,
                    currency="TRY",
                    description=desc,
                    category_name=self._pick_category(desc, msg, categories),
                )
            )

        return TransactionBatch(items=drafts)

    @staticmethod
    def _clean_item_description(raw: str) -> str:
        """Normalize noisy item phrases into a compact description."""
        if not raw:
            return ""

        cleaned = re.sub(
            r'\b(?:için|onun dışında|onun disinda|bir adet|bir tane|adet|tane|de|da)\b',
            ' ',
            raw,
            flags=re.IGNORECASE,
        )
        cleaned = re.sub(r'\s+', ' ', cleaned).strip(" ,.;:-")
        if not cleaned:
            return ""

        tokens = cleaned.split()
        # Descriptions are usually short nouns; keep first 2 tokens maximum.
        return " ".join(tokens[:2])

    @staticmethod
    def _pick_category(desc: str, message: str, categories: list[str]) -> Optional[str]:
        """Pick the best available category name from existing categories."""
        if not categories:
            return None

        haystack = f"{desc} {message}".lower()
        keyword_groups = [
            (("market", "grocery", "bakkal", "su", "süt", "milk", "water"), ("market", "grocery")),
            (("yemek", "food", "kahve", "coffee", "restoran"), ("food", "yemek", "restoran")),
            (("ulaşım", "ulasim", "taxi", "metro", "bus", "otobüs"), ("travel", "transport", "ulaşım", "ulasim")),
            (("sağlık", "saglik", "eczane", "doctor", "hastane"), ("health", "sağlık", "saglik")),
            (("eğitim", "egitim", "course", "kitap", "book"), ("education", "eğitim", "egitim")),
        ]

        lowered_categories = [(c, c.lower()) for c in categories]
        for trigger_words, category_words in keyword_groups:
            if any(word in haystack for word in trigger_words):
                for original, lowered in lowered_categories:
                    if any(cat_word in lowered for cat_word in category_words):
                        return original

        for original, lowered in lowered_categories:
            if "other" in lowered or "diğer" in lowered or "diger" in lowered:
                return original

        return categories[0]

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

