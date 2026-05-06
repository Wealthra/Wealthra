"""
Consultant Specialist — The Financial Advisor.

Analyzes raw financial data provided by the RAG Specialist and
outputs structured advice in JSON format. The output follows the
ConsultantAdvice contract (insights, warnings, suggestions) so
the frontend can render rich UI cards instead of plain text.

This specialist never touches the database — it only reasons
over data that has been pre-fetched and injected by the Orchestrator.
"""

from typing import Optional

from langchain_groq import ChatGroq

from core.config import settings
from core.contracts import (
    QueryResult,
    ConsultantAdvice,
)


class ConsultantSpecialist:
    """Analyzes raw financial data and produces structured advice."""

    def __init__(self, model_reasoning: str | None = None):
        reasoning_model = model_reasoning or settings.MODEL_REASONING
        self.llm = ChatGroq(
            api_key=settings.GROQ_API_KEY,
            model_name=reasoning_model,
        ).with_structured_output(ConsultantAdvice)

    async def analyze(
        self,
        raw_data: QueryResult,
        message: str,
        lang: str,
    ) -> ConsultantAdvice:
        """
        Analyze raw financial data and produce structured advice.

        Input:  - raw_data: QueryResult from RAG Read
                - message: Original user message for context
                - lang: Detected language (en/tr)

        Output: ConsultantAdvice with insights, warnings, and suggestions
        """
        lang_instruction = (
            "Lütfen tüm analizleri Türkçe yazın."
            if lang == "tr"
            else "Please write all analysis in English."
        )
        score_values = "Good, Needs Attention, Critical" if lang == "en" else "İyi, Dikkat Gerekli, Kritik"

        prompt = f"""You are Owlaris, the premium financial advisor.
User query: "{message}"

Data: {raw_data.summary}

{f"Detailed rows: {raw_data.model_dump_json()}" if raw_data.rows else ""}

Task: Provide actionable insights based strictly on the provided numbers.
1. {lang_instruction}
2. Every insight MUST contain a specific number or percentage from the data.
3. Do not repeat the same fact across multiple insights.
4. Do not invent missing numbers. If a metric is unavailable, skip that metric.
5. Keep each title under 8 words and each detail under 220 characters.
6. overall_score must be exactly one of: {score_values}
"""

        result: ConsultantAdvice = self.llm.invoke(prompt)
        normalized = self._normalize_score(result.overall_score, lang)
        return result.model_copy(update={"overall_score": normalized})

    @staticmethod
    def _normalize_score(score: Optional[str], lang: str) -> str:
        """Normalize model score to a stable UI-friendly value."""
        if not score:
            return "Needs Attention" if lang == "en" else "Dikkat Gerekli"

        raw = score.strip().lower()
        if lang == "tr":
            if any(x in raw for x in ["kritik", "critical"]):
                return "Kritik"
            if any(x in raw for x in ["iyi", "good"]):
                return "İyi"
            return "Dikkat Gerekli"

        if any(x in raw for x in ["critical", "kritik"]):
            return "Critical"
        if "good" in raw or "iyi" in raw:
            return "Good"
        return "Needs Attention"

