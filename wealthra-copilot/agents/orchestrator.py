"""
Orchestrator — The Brain.

State-machine router that evaluates user intent and dispatches
to specialized modules. Handles five core flows:

  1. Small Talk:   General conversation with financial persona
  2. Read:         Data queries dispatched to RAG Specialist
  3. Write:        Transaction drafts with confirmation loop (batch support)
  4. Hybrid:       Multi-step pipeline (RAG Read → Consultant)
  5. Compound:     Write-first intent detected in compound messages
                   (e.g., "add 45 TL coffee and show my totals")

The orchestrator never contains domain logic — it only routes,
manages session state, and formats responses through the unified
ChatResponse contract.

KEY DESIGN RULE — Write-Priority Gate:
  If a message contains ANY write signals (amounts + action verbs),
  the write intent ALWAYS takes priority over read/hybrid. Data must
  be persisted before analysis makes sense. The analysis portion is
  queued as a pending_action to execute after confirmation.
"""

import re
import logging
from typing import Optional, List
from datetime import date

from langchain_groq import ChatGroq

from core.config import settings
from core.session import SessionStore
from core.language import LanguageDetector
from core.contracts import (
    ChatRequest,
    ChatResponse,
    SessionData,
    SessionState,
    IntentType,
    ResponseType,
    TransactionDraft,
    TransactionBatch,
    TransactionType,
    HybridResult,
)
from agents.rag_specialist import RAGSpecialist
from agents.consultant import ConsultantSpecialist

logger = logging.getLogger(__name__)


# ---------------------------------------------------------------------------
# Localized message templates
# ---------------------------------------------------------------------------

_MESSAGES = {
    "confirm_single": {
        "en": (
            "I'm ready to record your **{desc}** expense of **{amount} TL** "
            "under the **{cat}** category. Do you confirm?"
        ),
        "tr": (
            "**{desc}** harcamanızı **{amount} TL** olarak **{cat}** "
            "kategorisine kaydetmeye hazırım. Onaylıyor musunuz?"
        ),
    },
    "confirm_income": {
        "en": (
            "I'm ready to record your income of **{amount} TL** "
            "(**{desc}**). Do you confirm?"
        ),
        "tr": (
            "**{amount} TL** tutarındaki gelirinizi (**{desc}**) "
            "kaydetmeye hazırım. Onaylıyor musunuz?"
        ),
    },
    "confirm_batch": {
        "en": (
            "I'm ready to record the following **{count} transactions** "
            "(total: **{total} TL**):\n\n{item_list}\n\n"
            "Do you confirm all of them?"
        ),
        "tr": (
            "Aşağıdaki **{count} işlemi** kaydetmeye hazırım "
            "(toplam: **{total} TL**):\n\n{item_list}\n\n"
            "Hepsini onaylıyor musunuz?"
        ),
    },
    "missing_info": {
        "en": "To record your transaction, I still need: **{fields}**. Could you provide them?",
        "tr": "İşleminizi kaydetmek için şunlara ihtiyacım var: **{fields}**. Bildirir misiniz?",
    },
    "confirmed": {
        "en": "✅ Your transaction has been saved successfully!",
        "tr": "✅ İşleminiz başarıyla kaydedildi!",
    },
    "confirmed_batch": {
        "en": "✅ All **{count} transactions** have been saved successfully! (Total: **{total} TL**)",
        "tr": "✅ **{count} işlemin** tamamı başarıyla kaydedildi! (Toplam: **{total} TL**)",
    },
    "confirm_failed": {
        "en": "⚠️ I couldn't save the transaction right now. Please try again later.",
        "tr": "⚠️ İşleminizi şu anda kaydedemedim. Lütfen daha sonra tekrar deneyin.",
    },
    "auth_failed": {
        "en": "🔑 I'm not authorized to save this for you. Please make sure you're logged in and a valid session token is provided.",
        "tr": "🔑 Bu işlemi sizin adınıza kaydetmek için yetkim yok. Lütfen giriş yaptığınızdan ve geçerli bir oturum anahtarı (JWT) sağlandığından emin olun.",
    },
    "rejected": {
        "en": "Understood — I've discarded the draft. Feel free to start over anytime.",
        "tr": "Anlaşıldı — taslağı iptal ettim. Dilediğiniz zaman yeniden deneyebilirsiniz.",
    },
}


def _msg(key: str, lang: str, **kwargs) -> str:
    """Get localized message with format substitution."""
    template = _MESSAGES.get(key, {}).get(lang, _MESSAGES[key]["en"])
    return template.format(**kwargs) if kwargs else template


# ---------------------------------------------------------------------------
# Strict language enforcement block (injected into every LLM prompt)
# ---------------------------------------------------------------------------

_LANG_BLOCK = {
    "tr": (
        "DİL KURALLARI (MUTLAK):\n"
        "- Yanıtının tamamını yalnızca Türkçe yaz.\n"
        "- Başka dillerden hiçbir kelime, karakter veya harf KULLANMA.\n"
        "- Çince, Japonca, Korece, Vietnamca, Arapça veya başka alfabe karakterleri YASAKTIR.\n"
        "- İngilizce kelimeler YASAKTIR (teknik terimler hariç: TL, API gibi).\n"
        "- Eğer bir kelimeyi Türkçeye çeviremiyorsan, o kelimeyi hiç kullanma.\n"
    ),
    "en": (
        "LANGUAGE RULES (ABSOLUTE):\n"
        "- Write your ENTIRE response in English only.\n"
        "- Do NOT include ANY characters from other languages or scripts.\n"
        "- Chinese, Japanese, Korean, Vietnamese, Arabic, or Cyrillic characters are FORBIDDEN.\n"
        "- Turkish-specific characters (ç, ş, ğ, ı, ö, ü) are FORBIDDEN in English responses.\n"
    ),
}

# ---------------------------------------------------------------------------
# Anti-repetition instruction (injected into narrative/analysis prompts)
# ---------------------------------------------------------------------------

_NO_REPEAT = (
    "TEKRAR YASAĞI: Aynı sayıyı veya bilgiyi yanıtın içinde birden fazla kez SÖYLEME. "
    "Bir veriyi bir kez net olarak belirt, sonra bir daha tekrarlama."
)

_NO_REPEAT_EN = (
    "NO REPETITION: Never state the same number or fact more than once in your response. "
    "Mention each data point exactly once, then move on."
)


# ---------------------------------------------------------------------------
# Write-priority heuristic: keyword-based pre-LLM gate
# ---------------------------------------------------------------------------

# Action verbs that signal a write intent
_WRITE_VERBS_TR = {
    "ekle", "ekledim", "ekleyelim", "kaydet", "kaydettim", "harcadım",
    "harcama", "ödedim", "ödeme", "aldım", "verdim", "yatırdım",
    "harcamayı", "koydum", "attım",
}
_WRITE_VERBS_EN = {
    "add", "added", "record", "spent", "spent", "paid", "bought",
    "log", "logged", "save", "register", "put",
}

# Pattern: number followed by currency or "tl" / "lira" / "$" / "€"
_AMOUNT_PATTERN = re.compile(
    r'\b\d+[\.,]?\d*\s*(?:tl|lira|₺|\$|€|dolar|dollar|euro)\b',
    re.IGNORECASE,
)


def _has_write_signals(message: str) -> bool:
    """
    Fast heuristic: does the message contain BOTH an amount AND a
    write-action verb? If yes, this is almost certainly a write intent
    regardless of what else the message contains.
    """
    lower = message.lower()
    words = set(re.findall(r'\b\w+\b', lower))

    has_amount = bool(_AMOUNT_PATTERN.search(message))
    has_verb = bool(words & (_WRITE_VERBS_TR | _WRITE_VERBS_EN))

    return has_amount and has_verb


class Orchestrator:
    """
    State-machine orchestrator that classifies intent and routes
    to the appropriate specialist module.
    """

    def __init__(self):
        # Fast model: intent classification, small talk, JSON extraction
        self.llm_fast = ChatGroq(
            api_key=settings.GROQ_API_KEY,
            model_name=settings.MODEL_FAST,
        )
        # Reasoning model: narrative synthesis, complex analysis
        self.llm_reasoning = ChatGroq(
            api_key=settings.GROQ_API_KEY,
            model_name=settings.MODEL_REASONING,
        )
        self.session_store = SessionStore()
        self.rag_specialist = RAGSpecialist()
        self.consultant = ConsultantSpecialist()
        self.lang_detector = LanguageDetector()

    # ===================================================================
    # PUBLIC API
    # ===================================================================

    async def process(self, request: ChatRequest) -> ChatResponse:
        """
        Main entry point. Processes a user message through the
        state machine and returns a ChatResponse.
        """
        # 1. Load session
        session = await self.session_store.get(request.user_id)

        # 2. Detect language
        lang = self.lang_detector.detect(request.message)
        session.language = lang

        # 3. Cache auth token in session
        if request.auth_token:
            session.auth_token = request.auth_token

        # 4. State machine: handle pending states first
        if session.state == SessionState.PENDING_CONFIRM:
            return await self._handle_confirmation(request, session)

        if session.state == SessionState.PENDING_INFO:
            return await self._handle_pending_info(request, session)

        # 5. Intent classification with write-priority gate
        intent = await self._classify_intent(request.message)
        session.last_intent = intent.value

        # 6. Dispatch to handler
        if intent == IntentType.WRITE:
            return await self._handle_write(request, session)
        elif intent == IntentType.READ:
            return await self._handle_read(request, session)
        elif intent == IntentType.HYBRID:
            return await self._handle_hybrid(request, session)
        else:
            return await self._handle_smalltalk(request, session)

    # ===================================================================
    # INTENT CLASSIFICATION (with write-priority gate)
    # Uses: llm_fast (simple 4-way classification)
    # ===================================================================

    async def _classify_intent(self, message: str) -> IntentType:
        """
        Classify user message into an IntentType.

        Uses a two-phase approach:
          Phase 1: Fast heuristic — if the message contains an amount
                   AND a write verb, it's a write intent. Period.
          Phase 2: LLM classification for ambiguous messages.

        Write ALWAYS wins over hybrid/read when both are present.
        """
        # Phase 1: Heuristic write-priority gate
        if _has_write_signals(message):
            logger.info("Write-priority gate triggered for: %s", message[:80])
            return IntentType.WRITE

        # Phase 2: LLM classification (fast model — simple task)
        prompt = f"""Analyze the user message and classify it into EXACTLY one of these categories:

- "write": User wants to ADD, RECORD, or LOG a financial transaction.
  The message contains a specific amount of money AND an action to save it.
  Examples: "Add 50 TL for food", "Spent 200 on clothes", "Bugün 30 lira harcadım",
  "I received my salary of 5000 TL", "Maaşım geldi", "45 TL kahve ve 120 TL kitap ekle"

  CRITICAL: If the message mentions specific amounts to ADD/RECORD, this is ALWAYS "write".
  Even if the user also asks for analysis, the write takes priority.

- "read": User asks a SPECIFIC question about their EXISTING financial data.
  NO new amounts are being added — they only want to SEE data.
  Examples: "How much did I spend?", "List my categories", "Show expenses this month",
  "Geçen ay ne kadar harcadım?", "Toplam harcamam ne kadar?"

- "hybrid": User asks for ANALYSIS, ADVICE, or a FINANCIAL HEALTH CHECK.
  They want the system to fetch data AND interpret it with suggestions.
  NO new amounts are being added.
  Examples: "How am I doing financially?", "Analyze my spending habits",
  "Am I saving enough?", "Harcamalarımı analiz et", "Mali durumum nasıl?"

- "smalltalk": Greetings, casual chat, emotional venting, or general questions
  that don't relate to specific financial data or transactions.
  Examples: "Hello", "Thanks", "I'm frustrated about money", "Merhaba"

Message: "{message}"

Return ONLY the category name (write, read, hybrid, or smalltalk). Nothing else."""

        response = self.llm_fast.invoke(prompt)
        raw = response.content.strip().lower().strip('"').strip("'")

        try:
            return IntentType(raw)
        except ValueError:
            # Fuzzy fallback
            if "write" in raw or "modify" in raw:
                return IntentType.WRITE
            elif "read" in raw or "query" in raw:
                return IntentType.READ
            elif "hybrid" in raw or "analy" in raw:
                return IntentType.HYBRID
            else:
                return IntentType.SMALLTALK

    # ===================================================================
    # HANDLER: Write (Transaction Draft — single or batch)
    # ===================================================================

    async def _handle_write(
        self, request: ChatRequest, session: SessionData
    ) -> ChatResponse:
        """
        Handle a write intent: parse NL into draft(s), enter confirmation flow.
        Supports batch transactions (e.g., "45 TL coffee and 120 TL book").
        """
        lang = session.language
        batch = await self.rag_specialist.write(
            request.message, request.user_id, session, lang
        )

        # Check if all items in the batch are complete
        if not batch.all_complete():
            # Collect missing fields across all items
            all_missing = set()
            for item in batch.items:
                all_missing.update(item.get_missing_fields())

            session.state = SessionState.PENDING_INFO
            session.batch = batch
            session.missing_fields = list(all_missing)
            await self.session_store.save(request.user_id, session)

            field_names = {
                "en": {"amount": "amount", "description": "description", "category": "category"},
                "tr": {"amount": "tutar", "description": "açıklama", "category": "kategori"},
            }
            localized_missing = [
                field_names.get(lang, field_names["en"]).get(f, f)
                for f in all_missing
            ]

            return ChatResponse(
                type=ResponseType.DRAFT,
                message=_msg("missing_info", lang, fields=", ".join(localized_missing)),
                language=lang,
                payload={
                    "batch": batch.model_dump(),
                    "missing_fields": list(all_missing),
                },
                ui_hints={"show_input_fields": list(all_missing)},
            )

        # All items complete → enter PENDING_CONFIRM
        session.state = SessionState.PENDING_CONFIRM
        session.batch = batch
        session.missing_fields = []
        await self.session_store.save(request.user_id, session)

        # Build confirmation message
        return self._build_confirm_response(batch, lang)

    def _build_confirm_response(
        self, batch: TransactionBatch, lang: str
    ) -> ChatResponse:
        """Build a confirmation ChatResponse for a batch of drafts."""
        if len(batch.items) == 1:
            item = batch.items[0]
            if item.transaction_type == TransactionType.INCOME:
                msg = _msg(
                    "confirm_income", lang,
                    desc=item.description or "—",
                    amount=item.amount,
                )
            else:
                msg = _msg(
                    "confirm_single", lang,
                    desc=item.description or "—",
                    amount=item.amount,
                    cat=item.category_name or "—",
                )
        else:
            # Batch: list each item
            lines = []
            for i, item in enumerate(batch.items, 1):
                cat_str = f" [{item.category_name}]" if item.category_name else ""
                lines.append(
                    f"  {i}. **{item.description or '—'}** — "
                    f"{item.amount} TL{cat_str}"
                )
            item_list = "\n".join(lines)
            msg = _msg(
                "confirm_batch", lang,
                count=len(batch.items),
                total=batch.total_amount,
                item_list=item_list,
            )

        return ChatResponse(
            type=ResponseType.CONFIRMATION,
            message=msg,
            language=lang,
            payload={"batch": batch.model_dump()},
            ui_hints={"show_confirm_buttons": True},
        )

    # ===================================================================
    # HANDLER: Pending Info (Multi-turn)
    # ===================================================================

    async def _handle_pending_info(
        self, request: ChatRequest, session: SessionData
    ) -> ChatResponse:
        """User is providing missing info for an existing draft batch."""
        lang = session.language

        # Check if user wants to cancel
        if self._is_cancellation(request.message):
            await self.session_store.clear(request.user_id)
            return ChatResponse(
                type=ResponseType.ADVISORY,
                message=_msg("rejected", lang),
                language=lang,
            )

        # Parse new info and merge with existing batch
        batch = await self.rag_specialist.write(
            request.message, request.user_id, session, lang
        )

        if not batch.all_complete():
            all_missing = set()
            for item in batch.items:
                all_missing.update(item.get_missing_fields())

            session.batch = batch
            session.missing_fields = list(all_missing)
            await self.session_store.save(request.user_id, session)

            field_names = {
                "en": {"amount": "amount", "description": "description", "category": "category"},
                "tr": {"amount": "tutar", "description": "açıklama", "category": "kategori"},
            }
            localized_missing = [
                field_names.get(lang, field_names["en"]).get(f, f)
                for f in all_missing
            ]

            return ChatResponse(
                type=ResponseType.DRAFT,
                message=_msg("missing_info", lang, fields=", ".join(localized_missing)),
                language=lang,
                payload={
                    "batch": batch.model_dump(),
                    "missing_fields": list(all_missing),
                },
                ui_hints={"show_input_fields": list(all_missing)},
            )

        # All complete → transition to PENDING_CONFIRM
        session.state = SessionState.PENDING_CONFIRM
        session.batch = batch
        session.missing_fields = []
        await self.session_store.save(request.user_id, session)

        return self._build_confirm_response(batch, lang)

    # ===================================================================
    # HANDLER: Confirmation (Yes/No)
    # ===================================================================

    async def _handle_confirmation(
        self, request: ChatRequest, session: SessionData
    ) -> ChatResponse:
        """Handle user response to a pending confirmation (yes/no)."""
        lang = session.language
        confirmed = self._is_confirmation(request.message)
        cancelled = self._is_cancellation(request.message)

        if confirmed and session.batch and session.batch.items:
            # Persist all items in the batch
            result = await self.rag_specialist.persist_batch(
                session.batch,
                request.user_id,
                session.auth_token or request.auth_token,
            )

            saved_batch = session.batch
            pending_action = session.pending_action

            # Reset session
            await self.session_store.clear(request.user_id)

            if result.get("success"):
                if len(saved_batch.items) == 1:
                    msg = _msg("confirmed", lang)
                else:
                    msg = _msg(
                        "confirmed_batch", lang,
                        count=len(saved_batch.items),
                        total=saved_batch.total_amount,
                    )

                return ChatResponse(
                    type=ResponseType.CONFIRMATION,
                    message=msg,
                    language=lang,
                    payload={
                        "saved": saved_batch.model_dump(),
                        "result": result,
                    },
                )
            else:
                # Check for 401 in any of the results
                is_auth_error = any(r.get("status_code") == 401 for r in result.get("results", []))
                msg_key = "auth_failed" if is_auth_error else "confirm_failed"

                return ChatResponse(
                    type=ResponseType.ERROR,
                    message=_msg(msg_key, lang),
                    language=lang,
                    payload={"error": result},
                )

        elif cancelled:
            await self.session_store.clear(request.user_id)
            return ChatResponse(
                type=ResponseType.ADVISORY,
                message=_msg("rejected", lang),
                language=lang,
            )

        else:
            # Ambiguous response — ask again
            if lang == "tr":
                msg = (
                    "Lütfen **Evet** veya **Hayır** ile yanıt verin. "
                    "İşlemi onaylıyor musunuz?"
                )
            else:
                msg = (
                    "Please respond with **Yes** or **No**. "
                    "Do you confirm this transaction?"
                )
            return ChatResponse(
                type=ResponseType.CONFIRMATION,
                message=msg,
                language=lang,
                payload={
                    "batch": session.batch.model_dump() if session.batch else None,
                },
                ui_hints={"show_confirm_buttons": True},
            )

    # ===================================================================
    # HANDLER: Read (Data Query)
    # ===================================================================

    async def _handle_read(
        self, request: ChatRequest, session: SessionData
    ) -> ChatResponse:
        """Handle a read intent: fetch data and synthesize conversational response."""
        lang = session.language
        query_result = await self.rag_specialist.read(
            request.message, request.user_id, lang
        )

        # Synthesize warm narrative from raw data
        narrative = await self._synthesize_narrative(
            raw_data=query_result.summary,
            user_message=request.message,
            lang=lang,
        )

        session.state = SessionState.IDLE
        await self.session_store.save(request.user_id, session)

        return ChatResponse(
            type=ResponseType.QUERY,
            message=narrative,
            language=lang,
            payload={
                "raw_data": query_result.model_dump(),
            },
        )

    # ===================================================================
    # HANDLER: Hybrid (Read + Consult)
    # ===================================================================

    async def _handle_hybrid(
        self, request: ChatRequest, session: SessionData
    ) -> ChatResponse:
        """
        Hybrid Execution Pipeline:
          Step 1: RAG Read fetches raw totals
          Step 2: Consultant generates structured advice (for payload)
          Step 3: Narrative synthesis produces warm conversational response
        """
        lang = session.language

        # Step 1: Fetch raw data
        raw_data = await self.rag_specialist.read(
            request.message, request.user_id, lang
        )

        # Step 2: Consultant analysis (structured for frontend payload)
        advice = await self.consultant.analyze(raw_data, request.message, lang)

        # Step 3: Build structured payload for frontend cards
        hybrid = HybridResult(
            raw_data=raw_data,
            statistics={
                "query": request.message,
                "data_summary": raw_data.summary[:200] if raw_data.summary else "",
            },
            advice=advice,
        )

        # Step 4: Serialize structured insights for the narrative synthesizer
        advice_bullets = []
        for item in advice.insights:
            advice_bullets.append(f"Insight: {item.title} — {item.detail}")
        for item in advice.warnings:
            advice_bullets.append(f"Warning: {item.title} — {item.detail}")
        for item in advice.suggestions:
            advice_bullets.append(f"Suggestion: {item.title} — {item.detail}")
        if advice.overall_score:
            advice_bullets.append(f"Overall score: {advice.overall_score}")

        combined_raw = (
            f"RAW DATA:\n{raw_data.summary}\n\n"
            f"STRUCTURED ANALYSIS:\n" + "\n".join(advice_bullets)
        )

        # Step 5: Synthesize warm narrative
        narrative = await self._synthesize_narrative(
            raw_data=combined_raw,
            user_message=request.message,
            lang=lang,
        )

        session.state = SessionState.IDLE
        await self.session_store.save(request.user_id, session)

        return ChatResponse(
            type=ResponseType.HYBRID,
            message=narrative,
            language=lang,
            payload=hybrid.model_dump(),
        )

    # ===================================================================
    # HANDLER: Small Talk
    # Uses: llm_fast (simple persona chat)
    # ===================================================================

    async def _handle_smalltalk(
        self, request: ChatRequest, session: SessionData
    ) -> ChatResponse:
        """Handle general conversation with the Owlaris persona."""
        lang = session.language
        lang_block = _LANG_BLOCK.get(lang, _LANG_BLOCK["en"])

        prompt = f"""You are Owlaris, the premium financial copilot of the Wealthra app.
The user is expressing something that isn't a direct data query or a transaction.
They might be frustrated, happy, or just talking.

User message: "{request.message}"

Task:
- Respond as a supportive, high-end financial advisor.
- If they are frustrated, offer encouragement and a practical tip.
- Always weave in a tip for wealth building, even in general talk.
- Never say "I can only help with X". You are their financial partner.
- Keep your response concise (3-4 sentences max).

{lang_block}"""

        response = self.llm_fast.invoke(prompt)

        session.state = SessionState.IDLE
        await self.session_store.save(request.user_id, session)

        return ChatResponse(
            type=ResponseType.ADVISORY,
            message=response.content,
            language=lang,
        )

    # ===================================================================
    # NARRATIVE SYNTHESIS — The Voice of Owlaris
    # Uses: llm_reasoning (creative writing, complex synthesis)
    # ===================================================================

    async def _synthesize_narrative(
        self,
        raw_data: str,
        user_message: str,
        lang: str,
    ) -> str:
        """
        Final synthesis pass: transforms raw data + structured analysis
        into a warm, flowing, conversational response.

        This is what makes Owlaris feel like a real financial advisor
        instead of a database query tool.
        """
        lang_block = _LANG_BLOCK.get(lang, _LANG_BLOCK["en"])
        no_repeat = _NO_REPEAT if lang == "tr" else _NO_REPEAT_EN

        prompt = f"""You are Owlaris, the premium financial copilot of Wealthra.
You are responding to a user who asked: "{user_message}"

Here is all the data and analysis you have:
---
{raw_data}
---

YOUR TASK:
Transform the above raw data and analysis into a WARM, CONVERSATIONAL response.
You are NOT a database — you are a trusted financial advisor sitting across the table
from your client, explaining their situation with empathy and clarity.

WRITING STYLE RULES:
1. OPEN WARMLY: Start with a friendly greeting acknowledging what they asked.
   Example (TR): "Hemen bakalım!" / Example (EN): "Let me walk you through your numbers."
   NEVER start with a raw list or bullet points.

2. LEAD WITH THE HEADLINE: What's the single most important takeaway?
   Is their spending out of control? Are they saving well? Tell them upfront.

3. TELL A STORY WITH THE DATA: Don't just list category amounts.
   Group and interpret them:
   - "Sabit giderleriniz (kira, faturalar) X TL tutuyor — bu tek başına gelirinizin Y%'si."
   - "Esnek harcamalarınızda ise yemek ve alışveriş Z TL'ye ulaşmış."
   Weave numbers INTO sentences, don't make separate bullet lists.

4. GIVE PERSONAL ADVICE: Every suggestion must reference ACTUAL numbers.
   BAD: "Harcamalarınızı azaltın."
   GOOD: "Yemek harcamanızı 2.840 TL'den 1.500 TL'ye çekerseniz, aylık 1.340 TL tasarruf edersiniz."

5. CLOSE WITH WARMTH: End with encouragement or a question.
   Example: "Başka bir konuya bakmamı ister misin?"

6. USE MARKDOWN for emphasis: **bold** for key numbers, emojis sparingly (🔴, 💡, 📊).
   Write in FLOWING PARAGRAPHS — not bullet-point lists.

7. LENGTH: 3-5 paragraphs. Concise but complete. No filler.

8. MATHEMATICAL ACCURACY: Only state numbers that appear in the data above.
   Never invent or estimate numbers.

{no_repeat}

{lang_block}

Output ONLY the final conversational message. No JSON, no markdown fences, no meta-commentary."""

        response = self.llm_reasoning.invoke(prompt)
        return response.content.strip()

    # ===================================================================
    # UTILITY: Confirmation / Cancellation detection
    # ===================================================================

    @staticmethod
    def _is_confirmation(message: str) -> bool:
        """Detect if user message is an affirmative confirmation."""
        affirmatives = {
            "yes", "yeah", "yep", "yup", "sure", "confirm", "ok", "okay",
            "do it", "go ahead", "save it", "absolutely",
            "evet", "tamam", "onayla", "onaylıyorum", "kaydet",
            "olur", "peki", "tabi", "tabii", "elbette",
        }
        lower = message.strip().lower()
        # Exact match or starts with affirmative
        return lower in affirmatives or any(
            lower.startswith(a) for a in affirmatives
        )

    @staticmethod
    def _is_cancellation(message: str) -> bool:
        """Detect if user message is a cancellation/rejection."""
        negatives = {
            "no", "nope", "cancel", "stop", "never mind", "abort",
            "don't", "dont", "discard", "forget it",
            "hayır", "iptal", "vazgeç", "vazgec", "bırak", "birak",
            "istemiyorum", "yapma", "dursun",
        }
        lower = message.strip().lower()
        return lower in negatives or any(
            lower.startswith(n) for n in negatives
        )
