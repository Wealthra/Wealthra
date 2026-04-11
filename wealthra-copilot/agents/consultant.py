"""
Consultant Specialist — The Financial Advisor.

Analyzes raw financial data provided by the RAG Specialist and
outputs structured advice in JSON format. The output follows the
ConsultantAdvice contract (insights, warnings, suggestions) so
the frontend can render rich UI cards instead of plain text.

This specialist never touches the database — it only reasons
over data that has been pre-fetched and injected by the Orchestrator.
"""

import json
import logging
from typing import Optional

from langchain_groq import ChatGroq

from core.config import settings
from core.contracts import (
    QueryResult,
    ConsultantAdvice,
    InsightItem,
)

logger = logging.getLogger(__name__)


class ConsultantSpecialist:
    """Analyzes raw financial data and produces structured advice."""

    def __init__(self):
        self.llm = ChatGroq(
            api_key=settings.GROQ_API_KEY,
            model_name="llama-3.3-70b-versatile",
        )

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
            "Tüm metinleri Türkçe olarak yaz." if lang == "tr"
            else "Write all text in English."
        )

        prompt = f"""
You are Owlaris, the premium financial advisor of Wealthra.

The user asked: "{message}"

Here is the raw financial data from their database:
{raw_data.summary}

{f"Detailed rows: {raw_data.model_dump_json()}" if raw_data.rows else ""}

YOUR TASK:
Analyze this SPECIFIC financial data and produce a structured JSON response.
Every insight MUST reference actual numbers from the data above.

You MUST return ONLY a valid JSON object with this exact structure:
{{
    "insights": [
        {{
            "icon": "📊",
            "title": "short title",
            "detail": "1-2 sentence explanation WITH specific numbers/percentages from this user's data"
        }}
    ],
    "warnings": [
        {{
            "icon": "⚠️",
            "title": "short title",
            "detail": "1-2 sentence explanation WITH specific amounts/ratios"
        }}
    ],
    "suggestions": [
        {{
            "icon": "💡",
            "title": "short title",
            "detail": "1-2 sentence actionable advice SPECIFIC to this user's spending patterns"
        }}
    ],
    "overall_score": "Good" or "Needs Attention" or "Critical"
}}

MANDATORY ANALYSIS RULES:
1. EVERY insight MUST contain a specific number or percentage from the data.
   BAD: "Your spending is high" → GOOD: "Food spending (3,200 TL) is 42% of your total expenses"
2. Calculate ratio of each category to total and flag any over 30%.
3. If income data is available, compute savings rate = (income - expenses) / income × 100.
4. Compare top 3 spending categories and suggest cutting the highest one with a SPECIFIC target.
   BAD: "Reduce food spending" → GOOD: "Cut Food from 3,200 TL to 2,500 TL by cooking 3x/week — saves 700 TL/month"
5. NEVER give generic advice like "increase income" or "save more money".
   Every suggestion must reference THIS user's actual spending data.
6. If data is sparse (few categories/amounts), acknowledge what you see and give
   advice based on the available numbers. Still reference the actual values.
7. {lang_instruction}
8. Return ONLY the JSON — no markdown fences, no explanation.
9. Sadece ve sadece hedef dilde yanıt ver, araya başka dillerden karakter/kelime karıştırma.
10. Kendini tekrar etme (avoid redundancy), her bilgiyi bir kez ve öz söyle.

JSON:
"""

        response = self.llm.invoke(prompt)
        return self._parse_advice(response.content, lang)

    def _parse_advice(self, content: str, lang: str) -> ConsultantAdvice:
        """Parse LLM output into ConsultantAdvice contract."""
        try:
            # Clean markdown fences if present
            cleaned = content.strip()
            if "```json" in cleaned:
                cleaned = cleaned.split("```json")[-1].split("```")[0]
            elif "```" in cleaned:
                cleaned = cleaned.split("```")[1].split("```")[0]

            data = json.loads(cleaned.strip())
            return ConsultantAdvice(
                insights=[InsightItem(**i) for i in data.get("insights", [])],
                warnings=[InsightItem(**w) for w in data.get("warnings", [])],
                suggestions=[InsightItem(**s) for s in data.get("suggestions", [])],
                overall_score=data.get("overall_score"),
            )
        except Exception as e:
            logger.warning(
                "Failed to parse consultant advice JSON: %s — raw: %s", e, content
            )
            # Graceful fallback: wrap the raw text as a single insight
            fallback_title = (
                "Finansal Analiz" if lang == "tr" else "Financial Analysis"
            )
            return ConsultantAdvice(
                insights=[
                    InsightItem(
                        icon="📋",
                        title=fallback_title,
                        detail=content[:500],
                    )
                ],
                overall_score="N/A",
            )
